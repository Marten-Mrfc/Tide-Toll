// Canvas 2D renderer.
// - Ground: a 128 x 128 base image (grass tint, crops) scaled up with
//   smoothing, plus a 512 x 512 wear layer: wear values bilinearly upsampled
//   and coloured through a lookup table with alpha, so trails have soft edges
//   (render-technique research, method b). Rebuilt a few times a second.
// - Static layer: river, buildings, labels, fences and bridges as vector ink,
//   redrawn only when the map, size or theme changes.
// - Every frame: walkers (interpolated between ticks), the tool ghost and the
//   flow preview.
import { World } from "../sim/world";
import { PARAMS } from "../sim/params";
import { N, NC } from "../sim/grid";
import { hash2 } from "../sim/rng";
import { followField } from "../sim/path";
import { FlowPreview } from "../sim/preview";
import { Palette, RGB } from "./palette";

export type ToolGhost =
  | { kind: "fence"; cells: number[]; ok: boolean[] }
  | { kind: "bridge"; cells: number[] | null; x: number; y: number }
  | { kind: "erase"; x: number; y: number; cells: number[] }
  | null;

const GROUND_MS = 250; // wear layer refresh interval (real time)
const M = 512; // wear layer resolution
const LABELS: Record<string, string> = { well: "Well", field: "Field", market: "Market" };

export class Renderer {
  private ctx: CanvasRenderingContext2D;
  private base = document.createElement("canvas");
  private wearCv = document.createElement("canvas");
  private wctx: CanvasRenderingContext2D;
  private wimg: ImageData;
  private wu32: Uint32Array;
  private lut = new Uint32Array(256);
  private tmp = new Float32Array(N * M);
  private i0 = new Int32Array(M);
  private fr = new Float32Array(M);
  private heat = document.createElement("canvas");
  private himg: ImageData;
  private layer = document.createElement("canvas");
  // Ground, wear, overlay and static layer composited once per ground refresh,
  // so a frame draws one full-size image plus walkers.
  private bg = document.createElement("canvas");
  private layerKey = "";
  private baseKey = "";
  private groundAt = -1e9;
  private groundVersion = -1;
  cssSize = 0;
  private dpr = 1;
  overlay = false;
  ghost: ToolGhost = null;
  flow: FlowPreview | null = null;
  private offX = new Float32Array(1024);
  private offY = new Float32Array(1024);

  constructor(public canvas: HTMLCanvasElement, public world: World, public pal: Palette) {
    this.ctx = canvas.getContext("2d")!;
    this.base.width = this.base.height = N;
    this.heat.width = this.heat.height = N;
    this.wearCv.width = this.wearCv.height = M;
    this.wctx = this.wearCv.getContext("2d")!;
    this.wimg = this.wctx.createImageData(M, M);
    this.wu32 = new Uint32Array(this.wimg.data.buffer);
    this.himg = this.heat.getContext("2d")!.createImageData(N, N);
    for (let j = 0; j < M; j++) {
      const s = Math.max(0, ((j + 0.5) * N) / M - 0.5), k = Math.min(N - 2, Math.floor(s));
      this.i0[j] = k; this.fr[j] = Math.min(1, s - k);
    }
    for (let i = 0; i < 1024; i++) { this.offX[i] = (hash2(i, 11) - 0.5) * 0.5; this.offY[i] = (hash2(i, 23) - 0.5) * 0.5; }
    this.buildLut();
  }

  setWorld(w: World) { this.world = w; this.flow = null; this.ghost = null; this.invalidate(); }
  setPalette(p: Palette) { this.pal = p; this.buildLut(); this.invalidate(); }
  invalidate() { this.layerKey = ""; this.baseKey = ""; this.groundAt = -1e9; }

  resize(cssSize: number) {
    const dpr = Math.min(3, window.devicePixelRatio || 1);
    if (cssSize === this.cssSize && dpr === this.dpr) return;
    this.cssSize = cssSize;
    this.dpr = dpr;
    this.canvas.style.width = this.canvas.style.height = `${cssSize}px`;
    this.canvas.width = this.canvas.height = Math.round(cssSize * dpr);
    this.invalidate();
  }

  /** Device pixels per cell. */
  get scale() { return this.canvas.width / N; }

  // ------------------------------------------------------------ ground

  /** Wear (0..100, in 256 steps) to packed RGBA: trampled, trail, track, fading in from transparent. */
  private buildLut() {
    const p = this.pal.rgb;
    const stops: [number, RGB][] = [[0, p.trampled], [10, p.trampled], [25, p.trail], [50, p.track], [92, mix(p.track, p.hollow, 0.5)]];
    for (let i = 0; i < 256; i++) {
      const v = i / 2.55;
      let k = 0;
      while (k < stops.length - 2 && v > stops[k + 1][0]) k++;
      const [a, ca] = stops[k], [b, cb] = stops[k + 1];
      const c = mix(ca, cb, smooth((v - a) / (b - a)));
      // Fade in from wear 4 to 26: trampled grass is a faint haze, and a trail's
      // soft edge is where its side wear falls off, so trails read narrow.
      const al = 0.95 * smooth((v - 4) / 22) + 0.05 * smooth((v - 26) / 30);
      this.lut[i] = ((Math.round(al * 255) << 24) | (Math.round(c[2]) << 16) | (Math.round(c[1]) << 8) | Math.round(c[0])) >>> 0;
    }
  }

  private buildBase() {
    const m = this.world.map, p = this.pal.rgb;
    const ctx = this.base.getContext("2d")!, img = ctx.createImageData(N, N), d = img.data;
    for (let c = 0; c < NC; c++) {
      const col = m.water[c] ? (m.ford[c] ? p.ford : p.water) : m.footprint[c] ? p.paper
        : m.crop[c] ? p.crop : mix(p.grass, p["grass-dark"], m.noise[c] * 0.5 + 0.5);
      d[c * 4] = col[0]; d[c * 4 + 1] = col[1]; d[c * 4 + 2] = col[2]; d[c * 4 + 3] = 255;
    }
    ctx.putImageData(img, 0, 0);
  }

  private buildWear() {
    const wear = this.world.wear, tmp = this.tmp, i0 = this.i0, fr = this.fr, u32 = this.wu32, lut = this.lut;
    for (let y = 0; y < N; y++) {
      const row = y * N, o = y * M;
      for (let x = 0; x < M; x++) {
        const a = row + i0[x];
        tmp[o + x] = wear[a] + (wear[a + 1] - wear[a]) * fr[x];
      }
    }
    for (let y = 0; y < M; y++) {
      const ra = i0[y] * M, rb = ra + M, f = fr[y], o = y * M;
      for (let x = 0; x < M; x++) {
        const v = tmp[ra + x] + (tmp[rb + x] - tmp[ra + x]) * f;
        u32[o + x] = lut[v >= 100 ? 255 : (v * 2.55 + 0.5) | 0];
      }
    }
    this.wctx.putImageData(this.wimg, 0, 0);
    if (this.overlay) this.buildHeat();
  }

  private buildHeat() {
    const w = this.world, h = this.pal.heat, d = this.himg.data;
    const stops = [0, ...PARAMS.stateThresholds];
    for (let c = 0; c < NC; c++) {
      const W = w.wear[c], o = c * 4;
      if (W < 0.5 || !w.land[c]) { d[o + 3] = 0; continue; }
      let i = 0;
      while (i < 3 && W >= stops[i + 1]) i++;
      const col = mix(h[i], h[i + 1], (W - stops[i]) / (stops[i + 1] - stops[i]));
      d[o] = col[0]; d[o + 1] = col[1]; d[o + 2] = col[2];
      d[o + 3] = Math.min(255, 120 + W * 4);
    }
    this.heat.getContext("2d")!.putImageData(this.himg, 0, 0);
  }

  // ------------------------------------------------------------ static layer

  private buildLayer() {
    const L = this.layer;
    L.width = this.canvas.width; L.height = this.canvas.height;
    const g = L.getContext("2d")!, w = this.world, m = w.map, s = this.scale, p = this.pal.css;
    g.clearRect(0, 0, L.width, L.height);
    g.lineJoin = "round"; g.lineCap = "round";

    // River: a smooth polygon along the banks, inked edges.
    const river = new Path2D();
    const bankX = (y: number, side: number) => (m.riverX[Math.max(0, Math.min(N - 1, Math.floor(y)))] + side * (m.riverHalfW + 0.1)) * s;
    for (const side of [-1, 1]) {
      const ys: number[] = [];
      for (let y = -1; y <= N + 1; y++) ys.push(y);
      if (side === 1) ys.reverse();
      ys.forEach((y, i) => {
        const x = bankX(y + 0.5, side), py = (y + 0.5) * s;
        if (i === 0 && side === -1) river.moveTo(x, py); else river.lineTo(x, py);
      });
    }
    river.closePath();
    g.fillStyle = p.water;
    g.fill(river);
    g.save();
    g.clip(river);
    g.fillStyle = p.ford;
    g.fillRect(0, (m.fordY - 1) * s, N * s, 3 * s);
    g.fillStyle = p["water-edge"];
    for (let k = -2; k <= 2; k++) {
      const x = m.riverX[m.fordY] + k * m.riverHalfW * 0.45;
      g.beginPath();
      g.ellipse(x * s, (m.fordY + 0.5 + (k % 2) * 0.6) * s, 0.35 * s, 0.25 * s, 0, 0, Math.PI * 2);
      g.fill();
    }
    g.strokeStyle = p["water-edge"];
    g.globalAlpha = 0.35;
    g.lineWidth = Math.max(1, s * 0.12);
    g.setLineDash([s * 2.5, s * 3.5]);
    for (const off of [-0.35, 0.3]) {
      g.beginPath();
      for (let y = 0; y <= N; y++) {
        const x = (m.riverX[Math.min(N - 1, y)] + off * m.riverHalfW) * s;
        if (y === 0) g.moveTo(x, y * s); else g.lineTo(x, y * s);
      }
      g.stroke();
    }
    g.restore();
    g.setLineDash([]);
    g.strokeStyle = p["water-edge"];
    g.lineWidth = Math.max(1.2, s * 0.22);
    g.stroke(river);

    for (const b of w.bridges) this.drawBridge(g, b.cells, null);
    for (const b of m.buildings) this.drawBuilding(g, b);
    this.drawFence(g, cellsOf(w.fence), p.fence, true);
    this.drawLabels(g);
  }

  private drawBuilding(g: CanvasRenderingContext2D, b: World["buildings"][number]) {
    const s = this.scale, p = this.pal.css, d = this.dpr;
    const x = b.x * s, y = b.y * s, bw = b.w * s, bh = b.h * s;
    g.strokeStyle = b.kind === "field" ? p.ink : p.outline;
    g.lineWidth = Math.max(1 * d, s * 0.16);
    if (b.kind === "field") {
      g.save();
      g.globalAlpha = 0.3;
      g.lineWidth = Math.max(1, s * 0.1);
      g.beginPath();
      for (let k = 1; k < b.w; k++) { g.moveTo(x + k * s, y + s * 0.3); g.lineTo(x + k * s, y + bh - s * 0.3); }
      g.stroke();
      g.restore();
      g.setLineDash([s * 0.6, s * 0.4]);
      g.strokeRect(x, y, bw, bh);
      g.setLineDash([]);
    } else if (b.kind === "well") {
      // A round well with a little roof beam across it.
      g.fillStyle = p.building;
      g.beginPath(); g.arc(x + bw / 2, y + bh / 2, bw * 0.47, 0, Math.PI * 2); g.fill(); g.stroke();
      g.fillStyle = p.water;
      g.beginPath(); g.arc(x + bw / 2, y + bh / 2, bw * 0.24, 0, Math.PI * 2); g.fill(); g.stroke();
    } else if (b.kind === "house") {
      // A cottage: walls, a pitched roof and a door, readable at a few pixels.
      const l = x + bw * 0.14, r = x + bw * 0.86, eave = y + bh * 0.46, base = y + bh * 0.96;
      g.fillStyle = p.building;
      g.fillRect(l, eave, r - l, base - eave);
      g.strokeRect(l, eave, r - l, base - eave);
      g.fillStyle = p.roof;
      g.beginPath();
      g.moveTo(x + bw * 0.02, eave + bh * 0.04); g.lineTo(x + bw / 2, y + bh * 0.04); g.lineTo(x + bw * 0.98, eave + bh * 0.04);
      g.closePath(); g.fill(); g.stroke();
      g.fillStyle = p.outline;
      g.fillRect(x + bw * 0.42, base - bh * 0.3, bw * 0.16, bh * 0.3);
    } else {
      // Market: a roofed hall with striped awnings.
      g.fillStyle = p.building;
      g.fillRect(x + s * 0.15, y + s * 0.15, bw - s * 0.3, bh - s * 0.3);
      g.strokeRect(x + s * 0.15, y + s * 0.15, bw - s * 0.3, bh - s * 0.3);
      g.fillStyle = p.roof;
      for (let k = 0; k < b.w; k += 2) g.fillRect(x + (k + 0.15) * s, y + s * 0.15, s, bh * 0.4);
      g.beginPath(); g.moveTo(x + s * 0.15, y + s * 0.15 + bh * 0.4); g.lineTo(x + bw - s * 0.15, y + s * 0.15 + bh * 0.4); g.stroke();
    }
    const dx = (b.door % N) * s, dy = ((b.door / N) | 0) * s;
    g.fillStyle = p.ink;
    if (b.kind === "field") g.fillRect(dx + s * 0.3, dy + s * 0.15, s * 0.4, s * 0.7);
  }

  /**
   * Labels go where they cover no building, no other label, and as little as
   * possible of the routes walkers take (traced on the current fields).
   */
  private drawLabels(g: CanvasRenderingContext2D) {
    const w = this.world, m = w.map, s = this.scale;
    const busy = new Uint8Array(NC);
    for (const h of w.houses)
      for (const b of w.buildings) if (b.kind !== "house") for (const c of followField(w.fields[b.id], w.cost, w.buildings[h].door, 400)) busy[c] = 1;
    const px = Math.max(10, Math.min(13, (s * 1.8) / this.dpr)) * this.dpr;
    g.font = `600 ${px}px system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`;
    const taken: number[][] = m.buildings.map(b => [b.x - 0.3, b.y - 0.3, b.x + b.w + 0.3, b.y + b.h + 0.3]);
    const place = (text: string, ax: number, ay: number, aw: number, ah: number) => {
      const tw = g.measureText(text).width / s + 0.6, th = px / s + 0.2;
      const cands = [
        [ax + aw / 2 - tw / 2, ay - th - 0.3], [ax + aw / 2 - tw / 2, ay + ah + 0.3],
        [ax + aw + 0.4, ay + ah / 2 - th / 2], [ax - tw - 0.4, ay + ah / 2 - th / 2],
      ];
      let best = cands[0], bv = Infinity;
      for (const [x0, y0] of cands) {
        const box = [x0, y0, x0 + tw, y0 + th];
        let v = box[0] < 0.5 || box[1] < 0.5 || box[2] > N - 0.5 || box[3] > N - 0.5 ? 1e6 : 0;
        for (const t of taken) if (box[0] < t[2] && box[2] > t[0] && box[1] < t[3] && box[3] > t[1]) v += 1000;
        for (let y = Math.max(0, Math.floor(y0)); y < Math.min(N, Math.ceil(y0 + th)); y++)
          for (let x = Math.max(0, Math.floor(x0)); x < Math.min(N, Math.ceil(x0 + tw)); x++) v += busy[y * N + x] * 10 + (m.water[y * N + x] ? 3 : 0);
        if (v < bv) { bv = v; best = [x0, y0]; }
      }
      taken.push([best[0], best[1], best[0] + tw, best[1] + th]);
      this.label(g, text, (best[0] + tw / 2) * s, (best[1] + th / 2) * s, px);
    };
    for (const b of m.buildings) if (LABELS[b.kind]) place(LABELS[b.kind], b.x, b.y, b.w, b.h);
    const fx = m.riverX[m.fordY];
    place("Ford", fx - m.riverHalfW, m.fordY - 1, m.riverHalfW * 2, 3);
  }

  private label(g: CanvasRenderingContext2D, text: string, x: number, y: number, px: number) {
    g.font = `600 ${px}px system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`;
    g.textAlign = "center";
    g.textBaseline = "middle";
    g.lineWidth = 3 * this.dpr;
    g.strokeStyle = this.pal.css["label-halo"];
    g.strokeText(text, x, y);
    g.fillStyle = this.pal.css.label;
    g.fillText(text, x, y);
  }

  /** A footbridge: a deck two cells wide with planks and rails (the sim's bridge is one row). */
  private drawBridge(g: CanvasRenderingContext2D, cells: number[], ghost: string | null) {
    const s = this.scale, p = this.pal.css;
    const x0 = (cells[0] % N) - 0.6, x1 = (cells[cells.length - 1] % N) + 1.6, y = (cells[0] / N) | 0;
    const top = (y - 0.45) * s, h = 1.9 * s;
    g.save();
    if (ghost) g.globalAlpha = 0.8;
    g.fillStyle = ghost ?? p.bridge;
    g.fillRect(x0 * s, top, (x1 - x0) * s, h);
    g.strokeStyle = ghost ?? p.ink;
    g.globalAlpha *= 0.6;
    g.lineWidth = Math.max(1, s * 0.1);
    g.beginPath();
    for (let x = x0 + 0.5; x < x1 - 0.2; x += 0.5) { g.moveTo(x * s, top); g.lineTo(x * s, top + h); }
    g.stroke();
    g.globalAlpha = ghost ? 0.9 : 1;
    g.strokeStyle = p.ink;
    g.lineWidth = Math.max(1.5 * this.dpr, s * 0.28);
    g.beginPath();
    g.moveTo(x0 * s, top); g.lineTo(x1 * s, top);
    g.moveTo(x0 * s, top + h); g.lineTo(x1 * s, top + h);
    g.stroke();
    g.fillStyle = p.ink;
    const post = Math.max(3 * this.dpr, s * 0.5);
    for (const px of [x0, x1]) for (const py of [top, top + h]) g.fillRect(px * s - post / 2, py - post / 2, post, post);
    g.restore();
  }

  /** Fences as straightened rails with posts, over a halo so they read on any ground. */
  private drawFence(g: CanvasRenderingContext2D, cells: number[], colour: string, halo: boolean) {
    if (!cells.length) return;
    const s = this.scale, d = this.dpr;
    const lines = fenceLines(cells);
    const rail = Math.max(2.5 * d, s * 0.38), post = Math.max(3.5 * d, s * 0.6);
    const path = new Path2D(), posts: [number, number][] = [];
    for (const ln of lines) {
      for (let i = 0; i < ln.length; i += 2) {
        if (i === 0) path.moveTo(ln[0] * s, ln[1] * s); else path.lineTo(ln[i] * s, ln[i + 1] * s);
      }
      // Posts every ~1.5 cells along the line, and at both ends.
      for (let i = 0; i + 2 < ln.length || i === 0; i += 2) {
        const ax = ln[i], ay = ln[i + 1], bx = ln[i + 2] ?? ax, by = ln[i + 3] ?? ay, len = Math.hypot(bx - ax, by - ay);
        const n = Math.max(1, Math.round(len / 1.5));
        for (let k = 0; k < n; k++) posts.push([ax + ((bx - ax) * k) / n, ay + ((by - ay) * k) / n]);
        if (i + 2 >= ln.length) break;
      }
      posts.push([ln[ln.length - 2], ln[ln.length - 1]]);
    }
    g.save();
    g.lineJoin = "round"; g.lineCap = "round";
    if (halo) {
      g.strokeStyle = this.pal.css["label-halo"];
      g.globalAlpha = 0.75;
      g.lineWidth = rail + 3 * d;
      g.stroke(path);
      g.globalAlpha = 1;
    }
    g.strokeStyle = colour;
    g.lineWidth = rail;
    g.stroke(path);
    g.fillStyle = colour;
    for (const [x, y] of posts) {
      if (halo) { g.strokeStyle = this.pal.css["label-halo"]; g.lineWidth = 1.5 * d; g.strokeRect(x * s - post / 2, y * s - post / 2, post, post); }
      g.fillRect(x * s - post / 2, y * s - post / 2, post, post);
    }
    g.restore();
  }

  // ------------------------------------------------------------ frame

  draw(alpha: number, now: number) {
    const ctx = this.ctx, w = this.world, s = this.scale, p = this.pal.css;
    const W = this.canvas.width;
    let dirty = false;
    const bkey = `${w.seed}`;
    if (bkey !== this.baseKey) { this.buildBase(); this.baseKey = bkey; dirty = true; }
    if (now - this.groundAt >= GROUND_MS || this.groundVersion !== w.version) {
      this.buildWear();
      this.groundAt = now;
      this.groundVersion = w.version;
      dirty = true;
    }
    const key = `${W}|${w.version}|${w.seed}`;
    if (key !== this.layerKey) { this.buildLayer(); this.layerKey = key; dirty = true; }
    if (dirty) {
      const bg = this.bg;
      if (bg.width !== W) bg.width = bg.height = W;
      const g = bg.getContext("2d")!;
      g.imageSmoothingEnabled = true;
      g.imageSmoothingQuality = "low"; // the wear layer is already interpolated; "high" costs ~8 ms a refresh
      g.drawImage(this.base, 0, 0, N, N, 0, 0, W, W);
      g.drawImage(this.wearCv, 0, 0, M, M, 0, 0, W, W);
      if (this.overlay) {
        g.globalAlpha = 0.6;
        g.fillStyle = p.paper;
        g.fillRect(0, 0, W, W);
        g.globalAlpha = 1;
        g.drawImage(this.heat, 0, 0, N, N, 0, 0, W, W);
      }
      g.drawImage(this.layer, 0, 0);
    }
    ctx.drawImage(this.bg, 0, 0);

    // Walkers: a fixed small scatter per walker so a crowd on one lane reads as a crowd.
    const r = Math.max(1.4 * this.dpr, s * 0.3);
    const halo = new Path2D(), dots = new Path2D();
    for (const k of w.walkers) {
      if (!w.isVisible(k)) continue;
      const o = k.id & 1023;
      const x = (k.px + (k.x - k.px) * alpha + this.offX[o]) * s;
      const y = (k.py + (k.y - k.py) * alpha + this.offY[o]) * s;
      halo.moveTo(x + r * 1.6, y); halo.arc(x, y, r * 1.6, 0, Math.PI * 2);
      dots.moveTo(x + r, y); dots.arc(x, y, r, 0, Math.PI * 2);
    }
    ctx.globalAlpha = 0.75;
    ctx.fillStyle = p["walker-halo"];
    ctx.fill(halo);
    ctx.globalAlpha = 1;
    ctx.fillStyle = p.walker;
    ctx.fill(dots);

    if (this.flow) this.drawFlowPreview(ctx, this.flow);
    this.drawGhost();
  }

  private drawGhost() {
    const gh = this.ghost, ctx = this.ctx, s = this.scale, p = this.pal.css;
    if (!gh) return;
    if (gh.kind === "fence") {
      const ok = gh.cells.filter((_, i) => gh.ok[i]), bad = gh.cells.filter((_, i) => !gh.ok[i]);
      this.drawFence(ctx, ok, p.ghost, true);
      this.drawFence(ctx, bad, p.invalid, true);
    } else if (gh.kind === "bridge") {
      if (gh.cells) this.drawBridge(ctx, gh.cells, p.ghost);
      else this.cross(gh.x, gh.y, 0.9);
    } else {
      if (gh.cells.length) this.drawFence(ctx, gh.cells, p.invalid, true);
      ctx.strokeStyle = p.invalid;
      ctx.lineWidth = Math.max(1.5, s * 0.15);
      ctx.beginPath();
      ctx.arc(gh.x * s, gh.y * s, 1.5 * s, 0, Math.PI * 2);
      ctx.stroke();
    }
  }

  private cross(x: number, y: number, size: number) {
    const ctx = this.ctx, s = this.scale;
    ctx.strokeStyle = this.pal.css.invalid;
    ctx.lineWidth = Math.max(2 * this.dpr, s * 0.25);
    const d = s * size;
    ctx.beginPath();
    ctx.moveTo(x * s - d, y * s - d); ctx.lineTo(x * s + d, y * s + d);
    ctx.moveTo(x * s + d, y * s - d); ctx.lineTo(x * s - d, y * s + d);
    ctx.stroke();
  }

  /**
   * Flow preview: current routes of the affected flows as thin dashed ink,
   * predicted routes as solid accent lines over a halo, with chevrons for
   * direction (colour is never the only cue). A blocked destination gets a
   * ring and a cross, and its route stops where it is cut.
   */
  drawFlowPreview(ctx: CanvasRenderingContext2D, fp: FlowPreview) {
    const s = this.scale, d = this.dpr, p = this.pal.css, B = this.world.buildings;
    ctx.save();
    ctx.lineJoin = "round"; ctx.lineCap = "round";
    ctx.strokeStyle = p.ink;
    ctx.globalAlpha = 0.45;
    ctx.lineWidth = 1.5 * d;
    ctx.setLineDash([5 * d, 4 * d]);
    for (const c of fp.changes) ctx.stroke(polyPath(c.oldPts, s));
    ctx.setLineDash([]);
    for (const c of fp.changes) {
      const path = polyPath(c.newPts, s);
      ctx.globalAlpha = 0.7;
      ctx.strokeStyle = p.halo;
      ctx.lineWidth = 6 * d;
      ctx.stroke(path);
      ctx.globalAlpha = 1;
      ctx.strokeStyle = c.reachable ? p.route : p.invalid;
      ctx.lineWidth = 3 * d;
      if (!c.reachable) ctx.setLineDash([2 * d, 5 * d]);
      ctx.stroke(path);
      ctx.setLineDash([]);
      if (c.reachable) this.chevrons(ctx, c.newPts, p.route);
    }
    for (const b of fp.blocked) {
      const door = B[b.dest].door, x = (door % N) + 0.5, y = ((door / N) | 0) + 0.5;
      ctx.strokeStyle = p.invalid;
      ctx.lineWidth = 2.5 * d;
      ctx.beginPath(); ctx.arc(x * s, y * s, Math.max(9 * d, 2.2 * s), 0, Math.PI * 2); ctx.stroke();
      this.cross(x, y, 1);
    }
    ctx.restore();
  }

  private chevrons(ctx: CanvasRenderingContext2D, pts: Float32Array, colour: string) {
    const s = this.scale, d = this.dpr, size = Math.max(4 * d, s * 0.9);
    let run = 4; // first chevron 4 cells in, then every 8
    ctx.strokeStyle = colour;
    ctx.lineWidth = 2 * d;
    for (let i = 2; i < pts.length; i += 2) {
      const ax = pts[i - 2], ay = pts[i - 1], bx = pts[i], by = pts[i + 1], len = Math.hypot(bx - ax, by - ay);
      run -= len;
      if (run > 0 || len < 1e-6) continue;
      run += 8;
      const ux = (bx - ax) / len, uy = (by - ay) / len, x = bx * s, y = by * s;
      ctx.beginPath();
      ctx.moveTo(x - (ux * 0.9 + uy * 0.7) * size, y - (uy * 0.9 - ux * 0.7) * size);
      ctx.lineTo(x, y);
      ctx.lineTo(x - (ux * 0.9 - uy * 0.7) * size, y - (uy * 0.9 + ux * 0.7) * size);
      ctx.stroke();
    }
  }
}

function polyPath(pts: Float32Array, s: number): Path2D {
  // Quadratic curves through the midpoints smooth the cell-to-cell kinks.
  const path = new Path2D();
  path.moveTo(pts[0] * s, pts[1] * s);
  for (let i = 2; i + 2 < pts.length; i += 2)
    path.quadraticCurveTo(pts[i] * s, pts[i + 1] * s, ((pts[i] + pts[i + 2]) / 2) * s, ((pts[i + 1] + pts[i + 3]) / 2) * s);
  if (pts.length >= 4) path.lineTo(pts[pts.length - 2] * s, pts[pts.length - 1] * s);
  return path;
}

function cellsOf(mask: Uint8Array): number[] {
  const out: number[] = [];
  for (let c = 0; c < NC; c++) if (mask[c]) out.push(c);
  return out;
}

/**
 * Turns fence cells (4-connected staircases) into straight polylines through
 * the cell centres: chains are walked between ends and junctions, then
 * simplified, so a diagonal fence draws as one straight rail.
 */
export function fenceLines(cells: number[]): number[][] {
  const set = new Set(cells), seen = new Set<number>(), lines: number[][] = [];
  const nb = (c: number) => {
    const x = c % N, out: number[] = [];
    if (x + 1 < N && set.has(c + 1)) out.push(c + 1);
    if (x > 0 && set.has(c - 1)) out.push(c - 1);
    if (set.has(c + N)) out.push(c + N);
    if (set.has(c - N)) out.push(c - N);
    return out;
  };
  const edge = (a: number, b: number) => Math.min(a, b) * NC + Math.max(a, b);
  const walk = (start: number, next: number) => {
    const chain = [start];
    let prev = start, cur = next;
    seen.add(edge(prev, cur));
    for (;;) {
      chain.push(cur);
      const ns = nb(cur);
      if (ns.length !== 2) break;
      const nx = ns[0] === prev ? ns[1] : ns[0];
      if (seen.has(edge(cur, nx))) break;
      seen.add(edge(cur, nx));
      prev = cur; cur = nx;
    }
    lines.push(simplify(chain.flatMap(c => [(c % N) + 0.5, ((c / N) | 0) + 0.5]), 0.75));
  };
  for (const pass of [0, 1])
    for (const c of cells) {
      const ns = nb(c);
      if (ns.length === 0 && pass === 0) { lines.push([(c % N) + 0.5, ((c / N) | 0) + 0.5]); continue; }
      if (pass === 0 && ns.length === 2) continue; // chain interiors are reached from their ends
      for (const n of ns) if (!seen.has(edge(c, n))) walk(c, n);
    }
  return lines;
}

/** Douglas-Peucker on a flat x,y list. */
function simplify(pts: number[], tol: number): number[] {
  const n = pts.length / 2;
  if (n <= 2) return pts;
  const keep = new Uint8Array(n);
  keep[0] = keep[n - 1] = 1;
  const stack: [number, number][] = [[0, n - 1]];
  while (stack.length) {
    const [a, b] = stack.pop()!;
    const ax = pts[2 * a], ay = pts[2 * a + 1], bx = pts[2 * b], by = pts[2 * b + 1];
    const len = Math.hypot(bx - ax, by - ay) || 1;
    let worst = -1, wd = tol;
    for (let i = a + 1; i < b; i++) {
      const dd = Math.abs((bx - ax) * (ay - pts[2 * i + 1]) - (ax - pts[2 * i]) * (by - ay)) / len;
      if (dd > wd) { wd = dd; worst = i; }
    }
    if (worst >= 0) { keep[worst] = 1; stack.push([a, worst], [worst, b]); }
  }
  const out: number[] = [];
  for (let i = 0; i < n; i++) if (keep[i]) out.push(pts[2 * i], pts[2 * i + 1]);
  return out;
}

function smooth(t: number): number {
  t = t < 0 ? 0 : t > 1 ? 1 : t;
  return t * t * (3 - 2 * t);
}

function mix(a: RGB, b: RGB, t: number): RGB {
  t = t < 0 ? 0 : t > 1 ? 1 : t;
  return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}

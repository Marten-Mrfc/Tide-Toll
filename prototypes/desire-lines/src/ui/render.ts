// Canvas 2D renderer. The ground is a 128 x 128 image built from the wear grid
// a few times a second and scaled up with smoothing, so trails read as soft
// lines. River, buildings, fences and bridges are vector ink on a cached layer
// that is redrawn only when the map changes. Walkers are drawn every frame,
// interpolated between simulation ticks.
import { World } from "../sim/world";
import { PARAMS } from "../sim/params";
import { N, NC } from "../sim/grid";
import { hash2 } from "../sim/rng";
import { Palette, RGB } from "./palette";

export type Preview =
  | { kind: "fence"; cells: number[]; ok: boolean[] }
  | { kind: "bridge"; cells: number[] | null; x: number; y: number }
  | { kind: "erase"; x: number; y: number }
  | null;

const GROUND_MS = 200; // ground image refresh interval (real time)
const LABELS: Record<string, string> = { well: "Well", field: "Field", market: "Market" };

export class Renderer {
  private ctx: CanvasRenderingContext2D;
  private ground = document.createElement("canvas");
  private gctx: CanvasRenderingContext2D;
  private gimg: ImageData;
  private heat = document.createElement("canvas");
  private hctx: CanvasRenderingContext2D;
  private himg: ImageData;
  private layer = document.createElement("canvas");
  private layerKey = "";
  private groundAt = -1e9;
  private groundVersion = -1;
  cssSize = 0;
  private dpr = 1;
  overlay = false;
  preview: Preview = null;
  private offX: Float32Array = new Float32Array(0);
  private offY: Float32Array = new Float32Array(0);

  constructor(public canvas: HTMLCanvasElement, public world: World, public pal: Palette) {
    this.ctx = canvas.getContext("2d")!;
    for (const c of [this.ground, this.heat]) { c.width = N; c.height = N; }
    this.gctx = this.ground.getContext("2d")!;
    this.hctx = this.heat.getContext("2d")!;
    this.gimg = this.gctx.createImageData(N, N);
    this.himg = this.hctx.createImageData(N, N);
  }

  setWorld(w: World) { this.world = w; this.invalidate(); }
  setPalette(p: Palette) { this.pal = p; this.invalidate(); }
  invalidate() { this.layerKey = ""; this.groundAt = -1e9; }

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

  // ------------------------------------------------------------ ground image

  private buildGround() {
    const w = this.world, m = w.map, p = this.pal.rgb, d = this.gimg.data;
    const states: RGB[] = [p.grass, p.trampled, p.trail, p.track];
    const [t10, t25, t50] = PARAMS.stateThresholds;
    const cuts = [t10, t25, t50];
    const BLEND = 2.5; // wear units over which one state fades into the next
    for (let c = 0; c < NC; c++) {
      let col: RGB;
      if (m.water[c]) col = m.ford[c] ? p.ford : p.water;
      else if (m.footprint[c]) col = p.paper;
      else {
        const W = w.wear[c];
        // Base: crops on the field, otherwise meadow with a gentle tint variation.
        const n = m.noise[c] * 0.5 + 0.5;
        const base: RGB = m.crop[c] ? p.crop : mix(p.grass, p["grass-dark"], n);
        if (w.hollow[c]) col = p.hollow;
        else {
          // Each state fades into the next over a short band round its threshold.
          col = base;
          for (let i = 0; i < 3; i++) {
            const t = (W - (cuts[i] - BLEND)) / (2 * BLEND);
            if (t <= 0) break;
            col = mix(col, states[i + 1], t);
          }
        }
      }
      const o = c * 4;
      d[o] = col[0]; d[o + 1] = col[1]; d[o + 2] = col[2]; d[o + 3] = 255;
    }
    this.gctx.putImageData(this.gimg, 0, 0);
    if (this.overlay) this.buildHeat();
  }

  private buildHeat() {
    const w = this.world, h = this.pal.heat, d = this.himg.data;
    const stops = [0, PARAMS.stateThresholds[0], PARAMS.stateThresholds[1], PARAMS.stateThresholds[2], PARAMS.stateThresholds[3]];
    for (let c = 0; c < NC; c++) {
      const W = w.wear[c], o = c * 4;
      if (W < 0.5 || !w.land[c]) { d[o + 3] = 0; continue; }
      let i = 0;
      while (i < 3 && W >= stops[i + 1]) i++;
      const t = Math.min(1, (W - stops[i]) / (stops[i + 1] - stops[i]));
      const col = mix(h[i], h[i + 1], t);
      d[o] = col[0]; d[o + 1] = col[1]; d[o + 2] = col[2];
      d[o + 3] = Math.min(255, 120 + W * 4);
    }
    this.hctx.putImageData(this.himg, 0, 0);
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
      for (let y = -1; y <= N + 1; y += 1) ys.push(y);
      if (side === 1) ys.reverse();
      ys.forEach((y, i) => {
        const x = bankX(y + 0.5, side), py = (y + 0.5) * s;
        if (i === 0 && side === -1) river.moveTo(x, py); else river.lineTo(x, py);
      });
    }
    river.closePath();
    g.fillStyle = p.water;
    g.fill(river);
    // The ford: pale shallows with stepping stones.
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
    // Faint current lines.
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
    g.fillStyle = p.label;
    this.label(g, "Ford", (m.riverX[m.fordY] + m.riverHalfW + 1.2) * s, (m.fordY + 0.9) * s, "left");

    // Bridges: planks with rails.
    for (const b of w.bridges) this.drawBridge(g, b.cells, false);

    // Buildings.
    const ink = p.ink;
    g.lineWidth = Math.max(1.2, s * 0.2);
    for (const b of m.buildings) {
      const x = b.x * s, y = b.y * s, bw = b.w * s, bh = b.h * s;
      g.strokeStyle = ink;
      if (b.kind === "field") {
        g.save();
        g.globalAlpha = 0.28;
        g.lineWidth = Math.max(1, s * 0.1);
        g.beginPath();
        for (let k = 1; k < b.w; k++) { g.moveTo(x + k * s, y + s * 0.3); g.lineTo(x + k * s, y + bh - s * 0.3); }
        g.stroke();
        g.restore();
        g.setLineDash([s * 0.6, s * 0.4]);
        g.strokeRect(x, y, bw, bh);
        g.setLineDash([]);
      } else if (b.kind === "well") {
        g.fillStyle = p.building;
        g.beginPath();
        g.arc(x + bw / 2, y + bh / 2, bw * 0.46, 0, Math.PI * 2);
        g.fill(); g.stroke();
        g.fillStyle = p.water;
        g.beginPath();
        g.arc(x + bw / 2, y + bh / 2, bw * 0.24, 0, Math.PI * 2);
        g.fill(); g.stroke();
      } else {
        const inset = s * 0.12;
        g.fillStyle = p.building;
        g.fillRect(x + inset, y + inset, bw - 2 * inset, bh - 2 * inset);
        g.strokeRect(x + inset, y + inset, bw - 2 * inset, bh - 2 * inset);
        g.beginPath();
        if (b.kind === "house") {
          // Hipped roof seen from above: ridge plus four hips.
          const cy = y + bh / 2, r0 = x + bw * 0.35, r1 = x + bw * 0.65;
          g.moveTo(r0, cy); g.lineTo(r1, cy);
          g.moveTo(x + inset, y + inset); g.lineTo(r0, cy); g.lineTo(x + inset, y + bh - inset);
          g.moveTo(x + bw - inset, y + inset); g.lineTo(r1, cy); g.lineTo(x + bw - inset, y + bh - inset);
        } else {
          // Market: a grid of stalls.
          for (let k = 1; k < b.w; k++) { g.moveTo(x + k * s, y + inset); g.lineTo(x + k * s, y + bh - inset); }
          g.moveTo(x + inset, y + bh / 2); g.lineTo(x + bw - inset, y + bh / 2);
        }
        g.stroke();
      }
      // Door: a short tick on the door edge.
      const dx = (b.door % N) * s, dy = ((b.door / N) | 0) * s;
      g.fillStyle = ink;
      if (b.kind === "field") g.fillRect(dx + s * 0.3, dy + s * 0.15, s * 0.4, s * 0.7);
      else g.fillRect(dx + s * 0.2, dy + s - Math.max(2, s * 0.3), s * 0.6, Math.max(2, s * 0.3));
      const text = LABELS[b.kind];
      if (text) this.label(g, text, x + bw / 2, y - s * 0.6, "center");
    }

    // Fences: posts joined by rails to orthogonal neighbours.
    g.strokeStyle = p.fence;
    g.fillStyle = p.fence;
    g.lineWidth = Math.max(1.5, s * 0.26);
    g.beginPath();
    for (let c = 0; c < NC; c++) {
      if (!w.fence[c]) continue;
      const x = (c % N) + 0.5, y = ((c / N) | 0) + 0.5;
      if (x + 1 < N && w.fence[c + 1]) { g.moveTo(x * s, y * s); g.lineTo((x + 1) * s, y * s); }
      if (c + N < NC && w.fence[c + N]) { g.moveTo(x * s, y * s); g.lineTo(x * s, (y + 1) * s); }
      if (!(x + 1 < N && w.fence[c + 1]) && !(c + N < NC && w.fence[c + N]) && !w.fence[c - 1] && !w.fence[c - N]) {
        g.moveTo((x - 0.3) * s, y * s); g.lineTo((x + 0.3) * s, y * s);
      }
    }
    g.stroke();
    const post = Math.max(2.5, s * 0.42);
    for (let c = 0; c < NC; c++) if (w.fence[c]) g.fillRect(((c % N) + 0.5) * s - post / 2, (((c / N) | 0) + 0.5) * s - post / 2, post, post);
  }

  private label(g: CanvasRenderingContext2D, text: string, x: number, y: number, align: CanvasTextAlign) {
    const px = Math.max(10, Math.min(13, this.scale * 1.7 / this.dpr)) * this.dpr;
    g.font = `600 ${px}px system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`;
    g.textAlign = align;
    g.textBaseline = "middle";
    g.lineWidth = 3 * this.dpr;
    g.strokeStyle = this.pal.css["label-halo"];
    g.strokeText(text, x, y);
    g.fillStyle = this.pal.css.label;
    g.fillText(text, x, y);
  }

  private drawBridge(g: CanvasRenderingContext2D, cells: number[], ghost: boolean, colour?: string) {
    const s = this.scale, p = this.pal.css;
    const x0 = (cells[0] % N) - 0.4, x1 = (cells[cells.length - 1] % N) + 1.4, y = (cells[0] / N) | 0;
    g.save();
    if (ghost) g.globalAlpha = 0.7;
    g.fillStyle = colour ?? p.bridge;
    g.fillRect(x0 * s, (y + 0.1) * s, (x1 - x0) * s, 0.8 * s);
    g.strokeStyle = colour ?? p.ink;
    g.lineWidth = Math.max(1, s * 0.15);
    g.beginPath();
    for (let x = x0 + 0.5; x < x1; x += 0.5) { g.moveTo(x * s, (y + 0.1) * s); g.lineTo(x * s, (y + 0.9) * s); }
    g.stroke();
    g.lineWidth = Math.max(1.3, s * 0.2);
    g.strokeRect(x0 * s, (y + 0.1) * s, (x1 - x0) * s, 0.8 * s);
    g.restore();
  }

  // ------------------------------------------------------------ frame

  draw(alpha: number, now: number) {
    const ctx = this.ctx, w = this.world, s = this.scale, p = this.pal.css;
    const W = this.canvas.width;
    if (now - this.groundAt >= GROUND_MS || this.groundVersion !== w.version) {
      this.buildGround();
      this.groundAt = now;
      this.groundVersion = w.version;
    }
    const key = `${W}|${w.version}|${w.seed}`;
    if (key !== this.layerKey) { this.buildLayer(); this.layerKey = key; }

    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = "high";
    ctx.drawImage(this.ground, 0, 0, N, N, 0, 0, W, W);
    if (this.overlay) {
      ctx.globalAlpha = 0.6;
      ctx.fillStyle = p.paper;
      ctx.fillRect(0, 0, W, W);
      ctx.globalAlpha = 1;
      ctx.drawImage(this.heat, 0, 0, N, N, 0, 0, W, W);
    }
    ctx.drawImage(this.layer, 0, 0);

    // Walkers: a fixed small scatter per walker so a crowd on one lane reads as a crowd.
    const walkers = w.walkers;
    if (this.offX.length < walkers.length + 1) {
      const n = 1024;
      this.offX = new Float32Array(n); this.offY = new Float32Array(n);
      for (let i = 0; i < n; i++) { this.offX[i] = (hash2(i, 11) - 0.5) * 0.5; this.offY[i] = (hash2(i, 23) - 0.5) * 0.5; }
    }
    const r = Math.max(1.4 * this.dpr, s * 0.3);
    const halo = new Path2D(), dots = new Path2D();
    for (const k of walkers) {
      if (!w.isVisible(k)) continue;
      const o = k.id & 1023;
      const x = (k.px + (k.x - k.px) * alpha + this.offX[o]) * s;
      const y = (k.py + (k.y - k.py) * alpha + this.offY[o]) * s;
      halo.moveTo(x + r * 1.6, y); halo.arc(x, y, r * 1.6, 0, Math.PI * 2);
      dots.moveTo(x + r, y); dots.arc(x, y, r, 0, Math.PI * 2);
    }
    ctx.globalAlpha = 0.55;
    ctx.fillStyle = p["walker-halo"];
    ctx.fill(halo);
    ctx.globalAlpha = 1;
    ctx.fillStyle = p.walker;
    ctx.fill(dots);

    this.drawPreview();
  }

  private drawPreview() {
    const pv = this.preview, ctx = this.ctx, s = this.scale, p = this.pal.css;
    if (!pv) return;
    if (pv.kind === "fence") {
      const post = Math.max(3, s * 0.5);
      pv.cells.forEach((c, i) => {
        ctx.fillStyle = pv.ok[i] ? p.ghost : p.invalid;
        ctx.globalAlpha = 0.85;
        ctx.fillRect(((c % N) + 0.5) * s - post / 2, (((c / N) | 0) + 0.5) * s - post / 2, post, post);
      });
      ctx.globalAlpha = 1;
    } else if (pv.kind === "bridge") {
      if (pv.cells) this.drawBridge(ctx, pv.cells, true, p.ghost);
      else {
        // Not buildable here: a small red cross at the pointer.
        ctx.strokeStyle = p.invalid;
        ctx.lineWidth = Math.max(2, s * 0.25);
        const x = pv.x * s, y = pv.y * s, d = s * 0.9;
        ctx.beginPath();
        ctx.moveTo(x - d, y - d); ctx.lineTo(x + d, y + d);
        ctx.moveTo(x + d, y - d); ctx.lineTo(x - d, y + d);
        ctx.stroke();
      }
    } else {
      ctx.strokeStyle = p.invalid;
      ctx.lineWidth = Math.max(1.5, s * 0.15);
      ctx.beginPath();
      ctx.arc(pv.x * s, pv.y * s, 1.5 * s, 0, Math.PI * 2);
      ctx.stroke();
    }
  }
}

function mix(a: RGB, b: RGB, t: number): RGB {
  t = t < 0 ? 0 : t > 1 ? 1 : t;
  return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}

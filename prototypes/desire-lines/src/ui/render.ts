// Canvas 2D renderer.
// - Ground: a 128 x 128 base image (grass tint, crops) scaled up with
//   smoothing, plus the worn-ground layer (ground.ts), rebuilt a few times a second.
// - Static layer: river, buildings, labels, gravel, fences, gates and bridges
//   as vector ink (ink.ts), redrawn only when the map, size or theme changes.
// - Every frame: walkers (interpolated between ticks), rain, new houses being
//   raised and their marks, the short marks of the player's actions, the tool
//   ghost and the flow preview (effects.ts).
import { World } from "../sim/world";
import { N, NC } from "../sim/grid";
import { hash2 } from "../sim/rng";
import { followField } from "../sim/path";
import { FlowPreview, RouteChange } from "../sim/preview";
import { Palette } from "./palette";
import { WearLayer, mix } from "./ground";
import { Pen, GateMark, drawFence, drawGate, drawGravel, drawBridge, gateMark } from "./ink";
import { Effects } from "./effects";

export type ToolGhost =
  | { kind: "fence"; cells: number[]; ok: boolean[] }
  | { kind: "gravel"; cells: number[]; ok: boolean }
  | { kind: "bridge"; cells: number[] | null; x: number; y: number }
  | { kind: "gate"; cells: number[]; remove: boolean; x: number; y: number }
  | { kind: "erase"; x: number; y: number; fence: number[]; bridge: number[]; gravel: number[] }
  | null;

const GROUND_MS = 250; // wear layer refresh interval (real time)
const SHARED = 1.5; // cells: where the routes before and after a change run this close, they are drawn faint
const LABELS: Record<string, string> = { well: "Well", field: "Field", market: "Market" };

export class Renderer {
  private ctx: CanvasRenderingContext2D;
  private base = document.createElement("canvas");
  private ground = new WearLayer();
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
  /** Which points of each shown route are shared with the other route (computed once per preview). */
  private shared = new WeakMap<RouteChange, { old: Uint8Array; now: Uint8Array }>();
  /** Rain, new houses and action marks, drawn every frame. */
  readonly fx: Effects;
  /** Whether the game is paused (rain stops falling on screen). */
  paused = false;

  constructor(public canvas: HTMLCanvasElement, public world: World, public pal: Palette) {
    this.ctx = canvas.getContext("2d")!;
    this.base.width = this.base.height = N;
    for (let i = 0; i < 1024; i++) { this.offX[i] = (hash2(i, 11) - 0.5) * 0.5; this.offY[i] = (hash2(i, 23) - 0.5) * 0.5; }
    this.fx = new Effects();
    this.setPalette(pal);
  }

  setWorld(w: World) { this.world = w; this.flow = null; this.ghost = null; this.fx.reset(); this.invalidate(); }
  setPalette(p: Palette) { this.pal = p; this.ground.setPalette(p); this.ground.setHeat(p.heat); this.fx.setPalette(p); this.invalidate(); }
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

  get pen(): Pen { return { s: this.scale, dpr: this.dpr, halo: this.pal.css["label-halo"], ink: this.pal.css.ink }; }

  // ------------------------------------------------------------ ground

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

    const pen = this.pen;
    drawGravel(g, pen, cellsOf(w.gravel), p["gravel-wash"], p.gravel, p["gravel-rim"]);
    for (const b of w.bridges) drawBridge(g, pen, b.cells, p.bridge, false);
    // Houses still being raised are drawn by the effects layer until they stand.
    const hidden = this.fx.raising(w);
    for (const b of m.buildings) if (!hidden.has(b.id)) this.drawBuilding(g, b, this.fx.isGrown(w, b.id));
    drawFence(g, pen, cellsOf(w.fence), p.fence, this.gateMarks(), p.gate);
    this.drawLabels(g);
  }

  /** Every gate as one mark: a gate cell with its 4-adjacent gate cell (a stair corner's pair). */
  private gateMarks(): GateMark[] {
    const w = this.world, done = new Uint8Array(NC), out: GateMark[] = [];
    for (let c = 0; c < NC; c++) {
      if (!w.gate[c] || done[c]) continue;
      const cells = [c];
      done[c] = 1;
      for (const v of [c + 1, c + N]) if (v < NC && w.gate[v] && !done[v] && (v !== c + 1 || (c % N) < N - 1)) { cells.push(v); done[v] = 1; }
      out.push(gateMark(cells, w.fence));
    }
    return out;
  }

  /** A building; a grown cottage stands in its garden. `rise` (0..1) draws it part raised (clipped from the ground up). */
  drawBuilding(g: CanvasRenderingContext2D, b: World["buildings"][number], grown = false, rise = 1) {
    if (grown) {
      const s = this.scale, p = this.pal.css, d = this.dpr;
      g.save();
      g.fillStyle = p.garden;
      g.strokeStyle = p.outline;
      g.globalAlpha = Math.min(1, rise * 2);
      g.lineWidth = Math.max(1 * d, s * 0.1);
      g.setLineDash([s * 0.25, s * 0.2]);
      g.fillRect((b.x + 0.06) * s, (b.y + 0.06) * s, (b.w - 0.12) * s, (b.h - 0.12) * s);
      g.strokeRect((b.x + 0.06) * s, (b.y + 0.06) * s, (b.w - 0.12) * s, (b.h - 0.12) * s);
      g.restore();
      // The cottage is a little smaller than the plot, set back from the door.
      const dx = (b.door % N) - (b.x + 1), dy = ((b.door / N) | 0) - (b.y + 1);
      const k = 0.68, cx = b.x + 1.5 - dx * 0.35, cy = b.y + 1.5 - dy * 0.35;
      g.save();
      if (rise < 1) {
        g.beginPath();
        g.rect((cx - 1.6) * s, (cy + 1.1 - 2.4 * rise) * s, 3.2 * s, 2.4 * rise * s);
        g.clip();
      }
      this.drawBuilding(g, { ...b, x: cx - 1.5 * k, y: cy - 1.5 * k, w: 3 * k, h: 3 * k }, false);
      g.restore();
      return;
    }
    this.drawBuildingShape(g, b);
  }

  private drawBuildingShape(g: CanvasRenderingContext2D, b: World["buildings"][number]) {
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

  // ------------------------------------------------------------ frame

  draw(alpha: number, now: number) {
    const ctx = this.ctx, w = this.world, s = this.scale, p = this.pal.css;
    const W = this.canvas.width;
    let dirty = false;
    const bkey = `${w.seed}|${w.buildings.length}`;
    if (bkey !== this.baseKey) { this.buildBase(); this.baseKey = bkey; dirty = true; }
    if (now - this.groundAt >= GROUND_MS || this.groundVersion !== w.version) {
      this.ground.build(w, this.overlay);
      this.groundAt = now;
      this.groundVersion = w.version;
      dirty = true;
    }
    const key = `${W}|${w.version}|${w.seed}|${this.fx.standing(w, now)}`;
    if (key !== this.layerKey) { this.buildLayer(); this.layerKey = key; dirty = true; }
    if (dirty) {
      const bg = this.bg;
      if (bg.width !== W) bg.width = bg.height = W;
      const g = bg.getContext("2d")!;
      g.imageSmoothingEnabled = true;
      g.imageSmoothingQuality = "low"; // the wear layer is already interpolated; "high" costs ~8 ms a refresh
      g.drawImage(this.base, 0, 0, N, N, 0, 0, W, W);
      g.drawImage(this.ground.canvas, 0, 0, this.ground.canvas.width, this.ground.canvas.height, 0, 0, W, W);
      // Wet ground is darker: grass and earth are multiplied by a cool grey as the ground wets.
      const wet = w.rainOn ? w.weather.wetness : 0;
      if (wet > 0.01 && !this.overlay) {
        g.globalCompositeOperation = "multiply";
        g.globalAlpha = 0.4 * wet;
        g.fillStyle = p.wet;
        g.fillRect(0, 0, W, W);
        g.globalAlpha = 1;
        g.globalCompositeOperation = "source-over";
      }
      if (this.overlay) {
        g.globalAlpha = 0.6;
        g.fillStyle = p.paper;
        g.fillRect(0, 0, W, W);
        g.globalAlpha = 1;
        g.drawImage(this.ground.heat, 0, 0, N, N, 0, 0, W, W);
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

    this.fx.draw(ctx, this, now);
    if (this.flow) this.drawFlowPreview(ctx, this.flow);
    this.drawGhost();
  }

  private drawGhost() {
    const gh = this.ghost, ctx = this.ctx, s = this.scale, p = this.pal.css, pen = this.pen;
    if (!gh) return;
    if (gh.kind === "fence") {
      drawFence(ctx, pen, gh.cells.filter((_, i) => gh.ok[i]), p.ghost);
      drawFence(ctx, pen, gh.cells.filter((_, i) => !gh.ok[i]), p.invalid);
    } else if (gh.kind === "gravel") {
      drawGravel(ctx, pen, gh.cells, gh.ok ? p.ghost : p.invalid, p["gravel-wash"], p["gravel-rim"], 0.75);
    } else if (gh.kind === "bridge") {
      if (gh.cells) drawBridge(ctx, pen, gh.cells, p.ghost, true);
      else this.cross(gh.x, gh.y, 0.9);
    } else if (gh.kind === "gate") {
      if (!gh.cells.length) this.cross(gh.x, gh.y, 0.9);
      else {
        const m = gateMark(gh.cells, this.world.fence);
        drawGate(ctx, pen, m, gh.remove ? p.invalid : p.ghost);
        if (gh.remove) this.cross(m.x, m.y, 0.6);
      }
    } else {
      // Erase: everything that would go, in the warning colour, and the reach of the eraser.
      drawGravel(ctx, pen, gh.gravel, p.invalid, p["gravel-wash"], p.invalid, 0.8);
      if (gh.bridge.length) drawBridge(ctx, pen, gh.bridge, p.invalid, true);
      drawFence(ctx, pen, gh.fence, p.invalid);
      ctx.strokeStyle = p.invalid;
      ctx.lineWidth = Math.max(1.5, s * 0.15);
      ctx.beginPath();
      ctx.arc(gh.x * s, gh.y * s, 1.5 * s, 0, Math.PI * 2);
      ctx.stroke();
    }
  }

  cross(x: number, y: number, size: number) {
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
   * Flow preview: for each changed flow, the route now (thin dashed ink) and
   * the route after (solid accent over a halo, with chevrons for direction, so
   * colour is never the only cue). Where the two run together they are drawn
   * faint, so the eye goes to the part that changes. A blocked destination gets
   * a ring and a cross, and its route stops where it is cut.
   */
  drawFlowPreview(ctx: CanvasRenderingContext2D, fp: FlowPreview) {
    const s = this.scale, d = this.dpr, p = this.pal.css, B = this.world.buildings;
    ctx.save();
    ctx.lineJoin = "round"; ctx.lineCap = "round";
    const masks = fp.changes.map(c => this.sharedMask(c));
    // Shared stretches first, faint, then what changes on top.
    ctx.strokeStyle = p.route;
    ctx.globalAlpha = 0.3;
    ctx.lineWidth = 2 * d;
    fp.changes.forEach((c, i) => { if (c.reachable) ctx.stroke(runsPath(c.newPts, masks[i].now, 1, s)); });
    ctx.strokeStyle = p.ink;
    ctx.globalAlpha = 0.55;
    ctx.lineWidth = 1.5 * d;
    ctx.setLineDash([5 * d, 4 * d]);
    fp.changes.forEach((c, i) => ctx.stroke(runsPath(c.oldPts, masks[i].old, 0, s)));
    ctx.setLineDash([]);
    fp.changes.forEach((c, i) => {
      const path = c.reachable ? runsPath(c.newPts, masks[i].now, 0, s) : polyPath(c.newPts, s);
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
      if (c.reachable) this.chevrons(ctx, c.newPts, masks[i].now, p.route);
    });
    for (const b of fp.blocked) {
      const door = B[b.dest].door, x = (door % N) + 0.5, y = ((door / N) | 0) + 0.5;
      ctx.strokeStyle = p.invalid;
      ctx.lineWidth = 2.5 * d;
      ctx.beginPath(); ctx.arc(x * s, y * s, Math.max(9 * d, 2.2 * s), 0, Math.PI * 2); ctx.stroke();
      this.cross(x, y, 1);
    }
    ctx.restore();
  }

  /** For each point of the old and new routes: 1 where the other route passes within SHARED cells. */
  private sharedMask(c: RouteChange): { old: Uint8Array; now: Uint8Array } {
    let m = this.shared.get(c);
    if (!m) {
      m = { old: nearMask(c.oldPts, c.newPts), now: c.reachable ? nearMask(c.newPts, c.oldPts) : new Uint8Array(c.newPts.length / 2) };
      this.shared.set(c, m);
    }
    return m;
  }

  /** Direction chevrons along the changed (unshared) stretches of a route. */
  private chevrons(ctx: CanvasRenderingContext2D, pts: Float32Array, shared: Uint8Array, colour: string) {
    const s = this.scale, d = this.dpr, size = Math.max(4 * d, s * 0.9);
    let run = 4; // first chevron 4 cells in, then every 8
    ctx.strokeStyle = colour;
    ctx.lineWidth = 2 * d;
    for (let i = 2; i < pts.length; i += 2) {
      const ax = pts[i - 2], ay = pts[i - 1], bx = pts[i], by = pts[i + 1], len = Math.hypot(bx - ax, by - ay);
      if (shared[i / 2]) { run = Math.min(run, 2); continue; }
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

/**
 * The stretches of a route whose mask equals `want`, as one path; each
 * stretch reaches one point into its neighbours so the pieces join up.
 */
function runsPath(pts: Float32Array, mask: Uint8Array, want: number, s: number): Path2D {
  const path = new Path2D(), n = pts.length / 2;
  let i = 0;
  while (i < n) {
    if (mask[i] !== want) { i++; continue; }
    let j = i;
    while (j + 1 < n && mask[j + 1] === want) j++;
    const a = Math.max(0, i - 1), b = Math.min(n - 1, j + 1);
    if (b > a) path.addPath(polyPath(pts.subarray(2 * a, 2 * b + 2), s));
    i = j + 1;
  }
  return path;
}

/** 1 for each point of `a` within SHARED cells of some point of `b`. */
function nearMask(a: Float32Array, b: Float32Array): Uint8Array {
  const out = new Uint8Array(a.length / 2), r2 = SHARED * SHARED;
  for (let i = 0; i < a.length; i += 2)
    for (let j = 0; j < b.length; j += 2)
      if ((a[i] - b[j]) ** 2 + (a[i + 1] - b[j + 1]) ** 2 <= r2) { out[i / 2] = 1; break; }
  return out;
}

function cellsOf(mask: Uint8Array): number[] {
  const out: number[] = [];
  for (let c = 0; c < NC; c++) if (mask[c]) out.push(c);
  return out;
}

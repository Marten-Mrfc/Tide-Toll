// The worn-ground layer: wear values bilinearly upsampled to 512 x 512 and
// coloured through a lookup table with alpha, so trails have soft edges
// (render-technique research, method b). Drawing only: nothing here changes
// the simulation.
// - The sampling is gently warped (at most WANDER cells), so a long straight
//   leg wanders a little, and the value is jittered per pixel near a trail's
//   edge, so edges are ragged like trodden grass, not ruled lines.
// - A wider pale fringe of flattened grass: the trampled band also takes a
//   blurred copy of the wear, so it spreads about a cell past the trail.
// - Trunks and side trails: a per-cell traffic level (the walkers' recent
//   footsteps, smoothed) pulls busy trunks towards a stronger compacted-earth
//   colour while quiet side trails stay lighter; along a trail the colour
//   follows its wear. Hollow ways stay the most extreme colour of all.
// - Mud is wet trail: the same worn ground, darker, more saturated and more
//   solid (wet earth is glossy, so the mottling evens out), with a few puddle
//   glints on the busiest cells only. It dries on the simulation's own clock:
//   over the last PARAMS.drySimSec of the mud, while the weather's wetness (which
//   sets the mud's cost and slowdown) falls from 1 to 0, cell by cell, the
//   least-worn cells first, so the trunks are the last to dry. The order is taken
//   from the wear when that drying window opens (a snapshot), so a trail worn up
//   while it dries keeps its place. A cell is never drawn wetter than the
//   ground's wetness. Drawing only: the mud rule and its timing (World.mud,
//   weather.ts) are untouched.
import { World } from "../sim/world";
import { PARAMS } from "../sim/params";
import { N, NC } from "../sim/grid";
import { hash2 } from "../sim/rng";
import { Palette, RGB } from "./palette";

export const M = 512; // wear layer resolution
export const GROUND_BANDS = 4; // a rebuild's pixels are drawn over this many frames
const WANDER = 0.45; // cells (the art spec: under half a cell)
const HOLLOW_VALUE = 112; // display value of a hollow-way cell (wear runs 0 to 100)
const LUT_MAX = 116; // display values 0..LUT_MAX map onto the 256 LUT entries
const TRUNK_LEVELS = 8; // traffic levels in the LUT
const MUD_LEVELS = 6; // mud levels in the LUT
const TRAFFIC_LO = 12, TRAFFIC_HI = 110; // footsteps (flow) from side trail to trunk
const FRINGE = 22; // the blurred wear spreads the trampled band up to this display value
const DRY_SPREAD = 0.2; // drying: share of the drying window over which one cell goes from wet to dry
const GLINT_TRAFFIC = 5; // puddle glints only on cells at this traffic level or more (of 0..7)

/**
 * How wet the ground under cell `c` is drawn, 0..1 (drawing only). While it rains
 * and until the drying window opens this is the weather's wetness; inside the
 * window (the last PARAMS.drySimSec of the mud, while the simulation's wetness
 * falls to 0) each muddy cell dries on its own: a cell worn to just trail (wear 25)
 * when the window opened dries first, the busiest track (wear 85 or more) last.
 * `wear` is the cell's wear when the window opened; `dry` is how far through the
 * window the clock is (0..1; below 0 before it). Never above `wetness`, so the
 * ground is never drawn wetter than its cost and slowdown say.
 */
export function wetAmount(wear: number, dry: number, wetness: number): number {
  if (dry <= 0) return wetness;
  const r = wear <= 25 ? 0 : wear >= 85 ? 1 : (wear - 25) / 60;
  const a = ((1 - DRY_SPREAD) * r + DRY_SPREAD - dry) / DRY_SPREAD;
  const v = a <= 0 ? 0 : a >= 1 ? 1 : a;
  return v < wetness ? v : wetness;
}

/**
 * How far through the mud's drying window the clock is: below 0 before it (and
 * while it rains), 1 when the mud is gone. The window is the simulation's own
 * (PARAMS.drySimSec, weather.ts), so the drawing dries as the walkers speed up.
 */
export function dryProgress(w: World): number {
  const wx = w.weather;
  if (wx.raining) return -1;
  return 1 - (wx.mudUntil - w.time) / PARAMS.drySimSec;
}

/**
 * The drying order's wear (drawing only): the wear as the drying window opens. Until
 * it opens the snapshot follows the wear at every call; inside the window it is
 * kept, so a trail worn up while the mud dries keeps its place in the order. A
 * snapshot from another window or world (a jump straight into a window) is
 * retaken at once.
 */
export class DryOrder {
  private snap = new Float32Array(NC);
  private world: World | null = null;
  private until = NaN;
  /** The wear to order the drying by, for world `w` at drying progress `dry` (dryProgress). */
  wear(w: World, dry: number): Float32Array {
    if (dry <= 0 || this.world !== w || this.until !== w.weather.mudUntil) {
      this.snap.set(w.wear);
      this.world = w;
      this.until = w.weather.mudUntil;
    }
    return this.snap;
  }
}

export class WearLayer {
  readonly canvas = document.createElement("canvas");
  readonly heat = document.createElement("canvas");
  private ctx: CanvasRenderingContext2D;
  private img: ImageData;
  private u32: Uint32Array;
  private himg: ImageData;
  /** Packed RGBA by [mud level][traffic level][display value]. */
  private lut = new Uint32Array(MUD_LEVELS * TRUNK_LEVELS * 256);
  private glint = 0; // packed puddle-glint colour
  private disp = new Float32Array(NC);
  private blur = new Float32Array(NC);
  private traffic = new Float32Array(NC); // smoothed footsteps per cell (drawing only)
  private trunk = new Float32Array(NC); // 0..TRUNK_LEVELS-1, blurred
  private mudv = new Float32Array(NC); // 0..MUD_LEVELS-1
  private dryOrder = new DryOrder();
  private gx = new Float32Array(M); // source x of output column, before the warp
  private wy = new Float32Array(M); // warp of the source y, by output column
  private wx = new Float32Array(M); // warp of the source x, by output row
  private mottle = new Uint8Array(M * M); // fixed per-pixel opacity, 205 to 255: trodden earth, not paving
  private jitter = new Int8Array(M * M); // fixed per-pixel nudge of the display value at trail edges
  /** The traffic smoothing is reset on a new world. */
  private worldRef: World | null = null;

  constructor() {
    this.canvas.width = this.canvas.height = M;
    this.ctx = this.canvas.getContext("2d")!;
    this.img = this.ctx.createImageData(M, M);
    this.u32 = new Uint32Array(this.img.data.buffer);
    this.heat.width = this.heat.height = N;
    this.himg = this.heat.getContext("2d")!.createImageData(N, N);
    const wave = (t: number, a: number, b: number) => 0.6 * Math.sin(t / 2.1 + a) + 0.4 * Math.sin(t / 1.15 + b);
    for (let j = 0; j < M; j++) {
      const s = ((j + 0.5) * N) / M - 0.5;
      this.gx[j] = s;
      this.wy[j] = WANDER * wave(s, 0.7, 2.9);
      this.wx[j] = WANDER * wave(s, 4.1, 1.3);
    }
    for (let i = 0; i < M * M; i++) {
      this.mottle[i] = 205 + Math.floor(50 * hash2(i, 5) * hash2(i >> 1, 9));
      // Clumpy noise (a pixel and its 2 x 2 block), so edges fray in tufts, not single pixels.
      const x = i % M, y = (i / M) | 0, blk = (y >> 1) * M + (x >> 1);
      this.jitter[i] = Math.round((hash2(blk, 13) - 0.5) * 9 + (hash2(i, 17) - 0.5) * 4);
    }
  }

  /**
   * Display value (wear, or HOLLOW_VALUE for a hollow way) to packed RGBA, for
   * each traffic and mud level: trampled, trail and track fading in from
   * transparent; busy trunks towards the trunk colour; hollow ways distinct;
   * mud as the same ground wet (darker, richer, more opaque). Track stays a
   * little translucent so it reads as worn earth, not paving.
   */
  setPalette(pal: Palette) {
    const p = pal.rgb;
    const stops: [number, RGB][] = [[0, p.trampled], [12, p.trampled], [25, p.trail], [50, p["trail-worn"]], [80, p.track], [100, p.track], [HOLLOW_VALUE, p.hollow]];
    for (let m = 0; m < MUD_LEVELS; m++)
      for (let t = 0; t < TRUNK_LEVELS; t++)
        for (let i = 0; i < 256; i++) {
          const v = (i / 255) * LUT_MAX;
          let k = 0;
          while (k < stops.length - 2 && v > stops[k + 1][0]) k++;
          const [a, ca] = stops[k], [b, cb] = stops[k + 1];
          let c = mix(ca, cb, smooth((v - a) / (b - a)));
          // Traffic matters once the ground is a trail; hollow ways keep their own colour.
          const tr = (t / (TRUNK_LEVELS - 1)) * smooth((v - 22) / 30) * (1 - smooth((v - 100) / 12));
          c = mix(c, p.trunk, 0.75 * tr);
          // Fade in from wear 2 to 30: trampled grass is a pale haze, and a trail's
          // soft edge is where its side wear falls off. Busy track tops out at 0.84,
          // so the grass still shows through a little; hollow ways are more solid.
          let al = 0.5 * smooth((v - 2) / 14) + 0.26 * smooth((v - 14) / 18) + 0.08 * smooth((v - 40) / 40) + 0.1 * smooth((v - 100) / 12) + 0.06 * tr;
          // Wet trail: the worn colour kept, pulled a third of the way to wet earth and
          // darkened a little more (the wet multiply over the whole map darkens it again);
          // a little more opaque, as wet earth hides the grass under a trail's edge.
          const mu = (m / (MUD_LEVELS - 1)) * smooth((v - 18) / 12);
          if (mu > 0) { c = mix(c, p.mud, 0.34 * mu); c = [c[0] * (1 - 0.14 * mu), c[1] * (1 - 0.14 * mu), c[2] * (1 - 0.1 * mu)]; al = al + 0.14 * mu; }
          al = Math.min(1, al);
          this.lut[(m * TRUNK_LEVELS + t) * 256 + i] = pack(c, al);
        }
    this.glint = pack(p["mud-glint"], 0.95);
  }

  /** Rebuilds the whole wear layer (and the heat map, if `heat`) from the world at once. */
  build(w: World, heat: boolean) {
    this.begin(w, heat);
    for (let b = 0; b < GROUND_BANDS; b++) this.band(b);
  }

  private anyMud = false;
  private heatPending: World | null = null;

  /**
   * Starts a rebuild: takes this moment's wear, traffic and mud into the per-cell
   * arrays. The pixels follow in GROUND_BANDS bands of rows (`band`), one a frame,
   * so a rebuild never lands on a single frame; the last band also redraws the heat map.
   */
  begin(w: World, heat: boolean) {
    const disp = this.disp, wear = w.wear, hol = w.hollow, blur = this.blur;
    for (let c = 0; c < NC; c++) disp[c] = hol[c] ? HOLLOW_VALUE : wear[c];
    // Fringe: a 3 x 3 blur of the wear (two passes in place via `blur`), spread into the trampled band.
    boxBlur(disp, blur, this.trunk);
    for (let c = 0; c < NC; c++) {
      const f = Math.min(FRINGE, blur[c] * 1.6);
      if (f > disp[c]) disp[c] = f;
    }
    // Traffic: smoothed footsteps, as a blurred level 0..TRUNK_LEVELS-1.
    if (this.worldRef !== w) { this.worldRef = w; this.traffic.fill(0); }
    const tr = this.traffic, flow = w.flow, tk = this.trunk;
    for (let c = 0; c < NC; c++) tr[c] += (flow[c] - tr[c]) * 0.2;
    for (let c = 0; c < NC; c++) { const v = (tr[c] - TRAFFIC_LO) / (TRAFFIC_HI - TRAFFIC_LO); blur[c] = v <= 0 ? 0 : v >= 1 ? 1 : v; }
    boxBlur(blur, tk, this.mudv);
    for (let c = 0; c < NC; c++) tk[c] = Math.max(tk[c], blur[c] * 0.8) * (TRUNK_LEVELS - 1);
    // Mud, scaled by how wet each cell is drawn (drying from the least-worn cells first).
    const mud = w.mud, mv = this.mudv, wetness = w.rainOn ? w.weather.wetness : 0;
    const dry = w.rainOn ? dryProgress(w) : 1;
    const anyMud = w.rainOn && w.mudCells > 0 && dry < 1 && (dry > 0 || wetness > 0.01);
    if (anyMud) {
      const order = this.dryOrder.wear(w, dry);
      for (let c = 0; c < NC; c++) mv[c] = mud[c] ? wetAmount(order[c], dry, wetness) * (MUD_LEVELS - 1) : 0;
    }
    this.anyMud = anyMud;
    this.heatPending = heat ? w : null;
  }

  /** Draws band b (0..GROUND_BANDS-1) of the pixels from the arrays `begin` filled, and puts it on the canvas. */
  band(b: number) {
    const disp = this.disp, tk = this.trunk, mv = this.mudv, anyMud = this.anyMud;
    const y0 = (b * M) / GROUND_BANDS, y1 = ((b + 1) * M) / GROUND_BANDS;
    const u32 = this.u32, lut = this.lut, gx = this.gx, wx = this.wx, wy = this.wy, mot = this.mottle, jit = this.jitter, k = 255 / LUT_MAX;
    const glint = this.glint;
    for (let y = y0; y < y1; y++) {
      const sy0 = gx[y], dx = wx[y], o = y * M;
      for (let x = 0; x < M; x++) {
        let sx = gx[x] + dx, sy = sy0 + wy[x];
        sx = sx < 0 ? 0 : sx > N - 1.001 ? N - 1.001 : sx;
        sy = sy < 0 ? 0 : sy > N - 1.001 ? N - 1.001 : sy;
        const ix = sx | 0, iy = sy | 0, fx = sx - ix, fy = sy - iy, a = iy * N + ix;
        const top = disp[a] + (disp[a + 1] - disp[a]) * fx;
        const bot = disp[a + N] + (disp[a + N + 1] - disp[a + N]) * fx;
        let v = top + (bot - top) * fy;
        if (v < 1) { u32[o + x] = 0; continue; }
        // Ragged edges: nudge the value where a trail fades into grass.
        if (v < 40) v += jit[o + x] * (v < 8 ? v / 8 : v > 30 ? (40 - v) / 10 : 1);
        if (v < 1) { u32[o + x] = 0; continue; }
        const tt = tk[a] + (tk[a + 1] - tk[a]) * fx, tb = tk[a + N] + (tk[a + N + 1] - tk[a + N]) * fx;
        const tl = (tt + (tb - tt) * fy + 0.5) | 0;
        let ml = 0;
        if (anyMud) {
          const mt = mv[a] + (mv[a + 1] - mv[a]) * fx, mb = mv[a + N] + (mv[a + N + 1] - mv[a + N]) * fx;
          ml = (mt + (mb - mt) * fy + 0.5) | 0;
        }
        const col = lut[(ml * TRUNK_LEVELS + tl) * 256 + (v >= LUT_MAX ? 255 : (v * k + 0.5) | 0)];
        const mo = mot[o + x];
        // A puddle glint: only in the ruts of the busiest wet trail.
        if (ml >= 3 && tl >= GLINT_TRAFFIC && mo > 252 && v > 40) { u32[o + x] = glint; continue; }
        // Wet earth is glossy: the mottling evens out as it wets.
        const m2 = ml > 0 ? mo + (((255 - mo) * ml) / (MUD_LEVELS - 1)) * 0.7 : mo;
        const alpha = ((col >>> 24) * m2) >> 8;
        u32[o + x] = (alpha << 24 | (col & 0xffffff)) >>> 0;
      }
    }
    this.ctx.putImageData(this.img, 0, 0, 0, y0, M, y1 - y0);
    if (b === GROUND_BANDS - 1 && this.heatPending) { this.buildHeat(this.heatPending); this.heatPending = null; }
  }

  private heatPal: RGB[] = [];
  setHeat(h: RGB[]) { this.heatPal = h; }

  private buildHeat(w: World) {
    const h = this.heatPal, d = this.himg.data;
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
}

/** A 3 x 3 box blur of `src` into `out` (`tmp` is scratch); edges clamp. */
function boxBlur(src: Float32Array, out: Float32Array, tmp: Float32Array) {
  for (let y = 0; y < N; y++) {
    const o = y * N;
    for (let x = 0; x < N; x++) tmp[o + x] = (src[o + (x > 0 ? x - 1 : x)] + src[o + x] + src[o + (x < N - 1 ? x + 1 : x)]) / 3;
  }
  for (let y = 0; y < N; y++) {
    const up = (y > 0 ? y - 1 : y) * N, dn = (y < N - 1 ? y + 1 : y) * N, o = y * N;
    for (let x = 0; x < N; x++) out[o + x] = (tmp[up + x] + tmp[o + x] + tmp[dn + x]) / 3;
  }
}

function pack(c: RGB, al: number): number {
  return ((Math.round(Math.max(0, Math.min(1, al)) * 255) << 24) | (Math.round(c[2]) << 16) | (Math.round(c[1]) << 8) | Math.round(c[0])) >>> 0;
}

export function smooth(t: number): number {
  t = t < 0 ? 0 : t > 1 ? 1 : t;
  return t * t * (3 - 2 * t);
}

export function mix(a: RGB, b: RGB, t: number): RGB {
  t = t < 0 ? 0 : t > 1 ? 1 : t;
  return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}

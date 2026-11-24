// The worn-ground layer: wear values bilinearly upsampled to 512 x 512 and
// coloured through a lookup table with alpha, so trails have soft edges
// (render-technique research, method b). The sampling is gently warped, so a
// long straight leg wanders a little, as a footpath does; the warp is at most
// WANDER cells and only changes the picture, never the simulation.
import { World } from "../sim/world";
import { PARAMS } from "../sim/params";
import { N, NC } from "../sim/grid";
import { hash2 } from "../sim/rng";
import { Palette, RGB } from "./palette";

export const M = 512; // wear layer resolution
const WANDER = 0.45; // cells
const HOLLOW_VALUE = 112; // display value of a hollow-way cell (wear runs 0 to 100)
const LUT_MAX = 116; // display values 0..LUT_MAX map onto the 256 LUT entries

export class WearLayer {
  readonly canvas = document.createElement("canvas");
  readonly heat = document.createElement("canvas");
  private ctx: CanvasRenderingContext2D;
  private img: ImageData;
  private u32: Uint32Array;
  private himg: ImageData;
  private lut = new Uint32Array(256);
  private disp = new Float32Array(NC);
  private gx = new Float32Array(M); // source x of output column, before the warp
  private wy = new Float32Array(M); // warp of the source y, by output column
  private wx = new Float32Array(M); // warp of the source x, by output row
  private mottle = new Uint8Array(M * M); // fixed per-pixel opacity, 205 to 255: trodden earth, not paving

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
    for (let i = 0; i < M * M; i++) this.mottle[i] = 205 + Math.floor(50 * hash2(i, 5) * hash2(i >> 1, 9));
  }

  /**
   * Display value (wear, or HOLLOW_VALUE for a hollow way) to packed RGBA:
   * trampled, trail and track fading in from transparent; hollow ways distinct.
   * Track stays a little translucent so it reads as worn earth, not paving.
   */
  setPalette(pal: Palette) {
    const p = pal.rgb;
    const stops: [number, RGB][] = [[0, p.trampled], [10, p.trampled], [25, p.trail], [50, p.track], [100, p.track], [HOLLOW_VALUE, p.hollow]];
    for (let i = 0; i < 256; i++) {
      const v = (i / 255) * LUT_MAX;
      let k = 0;
      while (k < stops.length - 2 && v > stops[k + 1][0]) k++;
      const [a, ca] = stops[k], [b, cb] = stops[k + 1];
      const c = mix(ca, cb, smooth((v - a) / (b - a)));
      // Fade in from wear 3 to 33: trampled grass is a faint haze, and a trail's
      // soft edge is where its side wear falls off. Busy track tops out at 0.84,
      // so the grass still shows through a little; hollow ways are more solid.
      const al = 0.76 * smooth((v - 3) / 30) + 0.08 * smooth((v - 40) / 40) + 0.1 * smooth((v - 100) / 12);
      this.lut[i] = ((Math.round(al * 255) << 24) | (Math.round(c[2]) << 16) | (Math.round(c[1]) << 8) | Math.round(c[0])) >>> 0;
    }
  }

  /** Rebuilds the wear layer (and the heat map, if `heat`) from the world. */
  build(w: World, heat: boolean) {
    const disp = this.disp, wear = w.wear, hol = w.hollow;
    for (let c = 0; c < NC; c++) disp[c] = hol[c] ? HOLLOW_VALUE : wear[c];
    const u32 = this.u32, lut = this.lut, gx = this.gx, wx = this.wx, wy = this.wy, mot = this.mottle, k = 255 / LUT_MAX;
    for (let y = 0; y < M; y++) {
      const sy0 = gx[y], dx = wx[y], o = y * M;
      for (let x = 0; x < M; x++) {
        let sx = gx[x] + dx, sy = sy0 + wy[x];
        sx = sx < 0 ? 0 : sx > N - 1.001 ? N - 1.001 : sx;
        sy = sy < 0 ? 0 : sy > N - 1.001 ? N - 1.001 : sy;
        const ix = sx | 0, iy = sy | 0, fx = sx - ix, fy = sy - iy, a = iy * N + ix;
        const top = disp[a] + (disp[a + 1] - disp[a]) * fx;
        const bot = disp[a + N] + (disp[a + N + 1] - disp[a + N]) * fx;
        const v = top + (bot - top) * fy;
        const col = lut[v >= LUT_MAX ? 255 : (v * k + 0.5) | 0];
        u32[o + x] = ((((col >>> 24) * mot[o + x]) >> 8) << 24 | (col & 0xffffff)) >>> 0;
      }
    }
    this.ctx.putImageData(this.img, 0, 0);
    if (heat) this.buildHeat(w);
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

export function smooth(t: number): number {
  t = t < 0 ? 0 : t > 1 ? 1 : t;
  return t * t * (3 - 2 * t);
}

export function mix(a: RGB, b: RGB, t: number): RGB {
  t = t < 0 ? 0 : t > 1 ? 1 : t;
  return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}

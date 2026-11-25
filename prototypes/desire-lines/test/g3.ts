// One seed of the trunk-share gate (G3), shared by npm test and the baseline
// script. A row keeps every daily sample, and `fingerprint` hashes their exact
// float64 bits, so two runs that print the same fingerprint gave bit-identical
// numbers.
import { World, WorldOptions } from "../src/sim/world";
import { PARAMS } from "../src/sim/params";
import { trailWidth } from "../src/sim/metrics";

export const SAMPLE_FROM = 20, DAYS = 45, WIDTH_DAY = 30;
const TPD = PARAMS.daySimSec * PARAMS.tickHz;

export interface Row {
  seed: number; walkers: number; ft: number; det: number; cov: number; mean: number; min: number; max: number; meanD: number;
  below: number; width: number; houses: number; finalWalkers: number; rainDays: number;
  /** Share of ticks with any mud on the map, and with rain falling (0 with rain off; not in the fingerprint). */
  mudTime: number; rainTime: number;
  trunk: number[]; fingerprint: string;
}

const mean = (a: number[]) => a.reduce((x, y) => x + y, 0) / Math.max(1, a.length);

/** FNV-1a over the float64 bits of the values. */
export function fingerprint(values: number[]): string {
  const f = new Float64Array(values), b = new Uint8Array(f.buffer);
  let h = 0x811c9dc5;
  for (let i = 0; i < b.length; i++) { h ^= b[i]; h = Math.imul(h, 0x01000193) >>> 0; }
  return h.toString(16).padStart(8, "0");
}

export function seedRow(seed: number, walkers: number, opts: WorldOptions = { rain: false, growth: false }): Row {
  // Older builds of World ignore the third argument (rain and growth did not exist).
  const w = new World(seed, walkers, opts);
  const trunk: number[] = [], trunkD: number[] = [], det: number[] = [], cov: number[] = [];
  let width = 0, mudTicks = 0, rainTicks = 0;
  const wx = w as unknown as { mudCells?: number; weather?: { raining: boolean } };
  for (let d = 1; d <= DAYS; d++) {
    for (let i = 0; i < TPD; i++) {
      w.tick();
      if (wx.mudCells) mudTicks++;
      if (wx.weather && wx.weather.raining) rainTicks++;
    }
    if (d === WIDTH_DAY) width = trailWidth(w.wear, w.land, w.metrics.window());
    if (d >= SAMPLE_FROM) {
      const s = w.stats();
      trunk.push(s.trunkShare); trunkD.push(s.trunkShareD); det.push(s.detourRatio); cov.push(s.trailCoverage);
    }
  }
  const ft = w.metrics.firstTrailTime;
  const ext = w as unknown as { grown?: unknown[]; weather?: { rainDays: number } };
  return {
    seed, walkers, ft, det: mean(det), cov: mean(cov), mean: mean(trunk),
    min: Math.min(...trunk), max: Math.max(...trunk), meanD: mean(trunkD), below: trunk.filter(v => v < 0.3 || v > 0.7).length, width,
    houses: ext.grown ? ext.grown.length : 0, finalWalkers: w.walkers.length, rainDays: ext.weather ? ext.weather.rainDays : 0,
    mudTime: mudTicks / (DAYS * TPD), rainTime: rainTicks / (DAYS * TPD),
    trunk, fingerprint: fingerprint([ft, width, ...trunk, ...trunkD, ...det, ...cov]),
  };
}

const pct = (v: number) => (v * 100).toFixed(1).padStart(5) + "%";

/** Per-seed table of a set, one line a seed, with the exact fingerprint (the baseline format). */
export function exactTable(title: string, rows: Row[]): string {
  const lines = [`${title}`, "seed | first trail | detour | coverage | trunk mean   min    max  | trunk D | width | fingerprint"];
  for (const r of [...rows].sort((a, b) => a.seed - b.seed))
    lines.push(`${String(r.seed).padStart(4)} | ${r.ft < 0 ? "   none" : (r.ft.toFixed(1) + " s").padStart(8)}  | ${r.det.toFixed(4)} | ${pct(r.cov)}   | ${pct(r.mean)} ${pct(r.min)} ${pct(r.max)} | ${pct(r.meanD)}  | ${r.width}     | ${r.fingerprint}`);
  return lines.join("\n");
}

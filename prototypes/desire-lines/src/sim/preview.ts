// Flow preview: where traffic would go if the player placed or removed
// something. It reads the live wear, crowding and trip counts, but writes only
// to its own scratch arrays: never to the world's fields, costs or RNG, so
// previewing cannot change a run.
//
// Costs are symmetric, so one cost-to-go field per destination gives every
// house's route to it. A "base" set of fields (no change) is cached and
// refreshed at most once a second of sim time; the hypothetical fields are
// built from a copy of the same base cost with the change applied, so the only
// difference between the two routes is the change itself.
import type { World } from "./world";
import { N, NC } from "./grid";
import { Heap, dijkstra, traceRoute } from "./path";

/** Per-cell overrides for a what-if cost. */
export const enum Ov { None, Block, Open, Bridge, CloseBridge }

/** A hypothetical edit. Cells are grid indices. */
export interface WhatIf {
  block?: number[]; // new fence cells
  open?: number[]; // fence cells removed
  bridge?: number[]; // water cells that become a footbridge
  closeBridge?: number[]; // footbridge cells that become water again
}

export interface RouteChange {
  home: number; // house building index
  dest: number; // destination building index
  rate: number; // trips out per day on this pair
  oldPts: Float32Array; // typical route now, x,y pairs in cell units
  newPts: Float32Array; // typical route after the change (cut short where it is blocked)
  reachable: boolean;
  costRatio: number; // new route cost / old route cost (Infinity when blocked)
}

export interface FlowPreview {
  changes: RouteChange[]; // the biggest changed pairs, at most maxPairs
  more: number; // changed pairs left out
  reroutedPerDay: number; // trips per day over all changed pairs
  blocked: { dest: number; houses: number }[]; // destinations cut off, and for how many houses
  version: number; // world.version the preview was computed against
}

const MIN_RATE = 0.5; // pairs with fewer trips a day than this are ignored
const BASE_MAX_AGE = 1; // sim seconds before the base fields are rebuilt
const SAME_COST = 0.002; // a route whose cost changes less than this is unchanged
const AFFECTED_COST = 0.03; // ... and is affected if it changes more than this
const AFFECTED_CELLS = 3; // ... or moves more than this many cells anywhere

export class FlowPredictor {
  private baseCost = new Float32Array(NC);
  private cost = new Float32Array(NC);
  private base: Float32Array[] = [];
  private hyp: Float32Array[] = [];
  private heap = new Heap(NC);
  private baseTime = -1e9;
  private baseVersion = -1;
  private dests: number[];
  private doors: number[];

  constructor(private world: World) {
    const b = world.buildings;
    this.dests = b.filter(x => x.kind !== "house").map(x => x.id);
    this.doors = world.houses.map(h => b[h].door);
    for (let i = 0; i < b.length; i++) { this.base.push(new Float32Array(0)); this.hyp.push(new Float32Array(0)); }
    for (const d of this.dests) { this.base[d] = new Float32Array(NC); this.hyp[d] = new Float32Array(NC); }
  }

  /**
   * Computes a preview in steps: each `next()` does about one Dijkstra's worth
   * of work (3 to 4 ms), so the page can spread it over frames. The final
   * `value` is the preview.
   */
  *run(change: WhatIf, maxPairs = 5): Generator<void, FlowPreview, void> {
    const w = this.world, B = w.buildings;
    if (w.version !== this.baseVersion || w.time - this.baseTime > BASE_MAX_AGE) {
      this.baseVersion = -1; // marks the base incomplete until the loop below ends
      const version = w.version, time = w.time;
      w.buildCost(this.baseCost);
      for (const d of this.dests) { yield; dijkstra(B[d].door, this.baseCost, this.base[d], this.heap, this.doors); }
      this.baseVersion = version; this.baseTime = time;
      yield;
    }
    const cost = this.cost;
    cost.set(this.baseCost);
    const apply = (cells: number[] | undefined, ov: Ov) => { if (cells) for (const c of cells) cost[c] = w.cellCost(c, ov); };
    apply(change.open, Ov.Open);
    apply(change.bridge, Ov.Bridge);
    apply(change.closeBridge, Ov.CloseBridge);
    apply(change.block, Ov.Block);
    for (const d of this.dests) { dijkstra(B[d].door, cost, this.hyp[d], this.heap, this.doors); yield; }

    const all: RouteChange[] = [];
    const blocked = new Map<number, number>();
    for (const h of w.houses) {
      const door = B[h].door;
      for (const d of this.dests) {
        const rate = w.tripRatePerDay(h, d);
        if (rate < MIN_RATE) continue;
        const f0 = this.base[d][door], f1 = this.hyp[d][door];
        if (!(f0 < Infinity)) continue; // not reachable now either
        const reachable = f1 < Infinity;
        const costRatio = reachable ? f1 / f0 : Infinity;
        if (reachable && Math.abs(costRatio - 1) < SAME_COST) continue;
        const oldPts = traceRoute(this.base[d], this.baseCost, door, B[d].door);
        const newPts = reachable ? traceRoute(this.hyp[d], cost, door, B[d].door) : cutAtBlock(oldPts, cost);
        if (reachable && Math.abs(costRatio - 1) <= AFFECTED_COST && maxDeviation(newPts, oldPts) <= AFFECTED_CELLS) continue;
        if (!reachable) blocked.set(d, (blocked.get(d) ?? 0) + 1);
        all.push({ home: h, dest: d, rate, oldPts, newPts, reachable, costRatio });
      }
    }
    all.sort((a, b) => b.rate - a.rate);
    return {
      changes: all.slice(0, maxPairs),
      more: Math.max(0, all.length - maxPairs),
      reroutedPerDay: all.reduce((s, c) => s + c.rate, 0),
      blocked: [...blocked].map(([dest, houses]) => ({ dest, houses })),
      version: w.version,
    };
  }

  /** Runs a preview to completion (tests and the debug API). */
  compute(change: WhatIf, maxPairs = 5): FlowPreview {
    const it = this.run(change, maxPairs);
    for (;;) { const r = it.next(); if (r.done) return r.value; }
  }
}

/** The largest distance from any point of `a` to the nearest point of `b`, in cells. */
export function maxDeviation(a: Float32Array, b: Float32Array): number {
  let worst = 0;
  for (let i = 0; i < a.length; i += 2) {
    let best = Infinity;
    for (let j = 0; j < b.length; j += 2) {
      const d = (a[i] - b[j]) ** 2 + (a[i + 1] - b[j + 1]) ** 2;
      if (d < best) best = d;
    }
    if (best > worst) worst = best;
  }
  return Math.sqrt(worst);
}

/** The route up to the first point that the change blocks. */
function cutAtBlock(pts: Float32Array, cost: Float32Array): Float32Array {
  for (let i = 0; i < pts.length; i += 2)
    if (cost[Math.floor(pts[i + 1]) * N + Math.floor(pts[i])] === Infinity) return pts.slice(0, Math.max(2, i));
  return pts;
}

/** One-line readout, e.g. "Reroutes about 40 walkers a day (+8% longer)". */
export function describePreview(p: FlowPreview, name: (b: number) => string): string {
  const parts: string[] = [];
  for (const b of p.blocked) parts.push(`Blocks the ${name(b.dest)} for ${b.houses} house${b.houses === 1 ? "" : "s"}`);
  const n = p.reroutedPerDay;
  if (n < 0.5) return parts[0] ?? "No change to routes";
  const shown = n < 5 ? `${Math.max(1, Math.round(n))}` : `about ${Math.max(5, Math.round(n / 5) * 5)}`;
  let line = `Reroutes ${shown} walker${shown === "1" ? "" : "s"} a day`;
  const big = p.changes.find(c => c.reachable);
  if (big && Math.abs(big.costRatio - 1) >= 0.05) {
    const pct = Math.round((big.costRatio - 1) * 100);
    line += pct > 0 ? ` (+${pct}% longer)` : ` (${-pct}% shorter)`;
  }
  parts.push(line);
  return parts.join(". ");
}

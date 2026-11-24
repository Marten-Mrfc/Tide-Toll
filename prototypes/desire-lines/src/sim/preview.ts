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
//
// Walkers come in two kinds (see PARAMS.explorerShare and detourCap): ordinary
// trips follow the ordinary fields, frugal trips the frugal ones. The preview
// builds both and splits each (house, destination) pair's trips the way the
// walkers do: a house's explorers always walk frugally, and the others do when
// the ordinary route is over the detour cap, before or after the change.
import type { World } from "./world";
import { N, NC } from "./grid";
import { PARAMS } from "./params";
import { Heap, dijkstra, traceRoute, routeLength } from "./path";

/** Per-cell override flags for a what-if cost (combine with |). */
export const Ov = {
  None: 0,
  Block: 1, // a new fence
  Open: 2, // a fence (and any gate in it) removed
  Bridge: 4, // water that becomes a footbridge
  CloseBridge: 8, // a footbridge removed
  Gravel: 16,
  Ungravel: 32,
  Gate: 64, // a fence cell that becomes a gate
  Ungate: 128, // a gate that becomes plain fence
} as const;

/** A hypothetical edit. Cells are grid indices. */
export interface WhatIf {
  block?: number[]; // new fence cells
  open?: number[]; // fence cells removed
  bridge?: number[]; // water cells that become a footbridge
  closeBridge?: number[]; // footbridge cells that become water again
  gravel?: number[]; // new gravel
  ungravel?: number[]; // gravel removed
  gate?: number[]; // fence cells that become gates
  ungate?: number[]; // gates that become fence
}

const CHANGE_FLAGS: [keyof WhatIf, number][] = [
  ["block", Ov.Block], ["open", Ov.Open], ["bridge", Ov.Bridge], ["closeBridge", Ov.CloseBridge],
  ["gravel", Ov.Gravel], ["ungravel", Ov.Ungravel], ["gate", Ov.Gate], ["ungate", Ov.Ungate],
];

export interface RouteChange {
  home: number; // house building index
  dest: number; // destination building index
  rate: number; // trips out per day that take this route (the pair's trips times this group's share)
  explorers: boolean; // the group: the house's explorers, or its other walkers
  frugal: boolean; // whether these trips are walked frugally after the change
  oldPts: Float32Array; // typical route now, x,y pairs in cell units
  newPts: Float32Array; // typical route after the change (cut short where it is blocked)
  reachable: boolean;
  costRatio: number; // new route cost / old route cost, in the after-change walker's terms (Infinity when blocked)
}

export interface FlowPreview {
  changes: RouteChange[]; // the biggest changed routes, at most maxPairs
  more: number; // changed routes left out
  reroutedPerDay: number; // trips per day over all changed routes
  /** Rate-weighted mean of costRatio over the rerouted trips that still arrive (1 if none). */
  meanCostRatio: number;
  blocked: { dest: number; houses: number }[]; // destinations cut off, and for how many houses
  version: number; // world.version when the computation started
}

const MIN_RATE = 0.5; // pairs with fewer trips a day than this are ignored
const MIN_GROUP_RATE = 0.05; // ... and groups (ordinary or explorer trips) with fewer than this
const BASE_MAX_AGE = 1; // sim seconds before the base fields are rebuilt
const SAME_COST = 0.002; // a route whose cost changes less than this is unchanged
const AFFECTED_COST = 0.03; // ... and is affected if it changes more than this
const AFFECTED_CELLS = 3; // ... or moves more than this many cells anywhere
const PAIRS_PER_STEP = 4; // traced pairs between two yields

/** One set of fields (ordinary and frugal) over one pair of costs. */
interface FieldSet { cost: Float32Array; costX: Float32Array; f: Float32Array[]; fX: Float32Array[]; }

export class FlowPredictor {
  private base: FieldSet;
  private hyp: FieldSet;
  private flags = new Uint8Array(NC);
  private heap = new Heap(NC);
  private baseTime = -1e9;
  private baseVersion = -1;
  private dests: number[];
  private doors: number[];

  constructor(private world: World) {
    const b = world.buildings;
    this.dests = b.filter(x => x.kind !== "house").map(x => x.id);
    this.doors = world.houses.map(h => b[h].door);
    const set = (): FieldSet => {
      const s: FieldSet = { cost: new Float32Array(NC), costX: new Float32Array(NC), f: [], fX: [] };
      for (let i = 0; i < b.length; i++) { s.f.push(new Float32Array(0)); s.fX.push(new Float32Array(0)); }
      for (const d of this.dests) { s.f[d] = new Float32Array(NC); s.fX[d] = new Float32Array(NC); }
      return s;
    };
    this.base = set();
    this.hyp = set();
  }

  /**
   * Computes a preview in steps: each `next()` does about one Dijkstra's worth
   * of work (a few ms), so the page can spread it over frames. The final
   * `value` is the preview.
   */
  *run(change: WhatIf, maxPairs = 5): Generator<void, FlowPreview, void> {
    const w = this.world, B = w.buildings, P = PARAMS, base = this.base, hyp = this.hyp;
    const version = w.version;
    if (w.version !== this.baseVersion || w.time - this.baseTime > BASE_MAX_AGE) {
      this.baseVersion = -1; // marks the base incomplete until the loop below ends
      const time = w.time;
      w.buildCost(base.cost);
      w.buildCost(base.costX, true);
      for (const d of this.dests) {
        yield; dijkstra(B[d].door, base.cost, base.f[d], this.heap, this.doors);
        yield; dijkstra(B[d].door, base.costX, base.fX[d], this.heap, this.doors);
      }
      this.baseVersion = version; this.baseTime = time;
      yield;
    }
    // The hypothetical costs: the base with the changed cells re-costed under their overrides.
    hyp.cost.set(base.cost);
    hyp.costX.set(base.costX);
    const flags = this.flags, touched: number[] = [];
    for (const [key, flag] of CHANGE_FLAGS)
      for (const c of change[key] ?? []) { if (c < 0 || c >= NC) continue; if (!flags[c]) touched.push(c); flags[c] |= flag; }
    for (const c of touched) {
      hyp.cost[c] = w.cellCost(c, flags[c]);
      hyp.costX[c] = w.cellCost(c, flags[c], true);
      flags[c] = 0;
    }
    for (const d of this.dests) {
      dijkstra(B[d].door, hyp.cost, hyp.f[d], this.heap, this.doors); yield;
      dijkstra(B[d].door, hyp.costX, hyp.fX[d], this.heap, this.doors); yield;
    }

    // Explorers per house (walkers are assigned to houses in turn).
    const walkersOf = new Map<number, number>(), explorersOf = new Map<number, number>();
    for (const k of w.walkers) {
      walkersOf.set(k.home, (walkersOf.get(k.home) ?? 0) + 1);
      if (k.explorer) explorersOf.set(k.home, (explorersOf.get(k.home) ?? 0) + 1);
    }

    const all: RouteChange[] = [];
    const blocked = new Map<number, number>();
    let traced = 0;
    for (const h of w.houses) {
      const door = B[h].door;
      const ex = (explorersOf.get(h) ?? 0) / Math.max(1, walkersOf.get(h) ?? 0);
      for (const d of this.dests) {
        const rate = w.tripRatePerDay(h, d);
        if (rate < MIN_RATE || !(base.f[d][door] < Infinity)) continue; // rare, or not reachable now either
        const to = B[d].door;
        const straight = Math.hypot((door % N) - (to % N), ((door / N) | 0) - ((to / N) | 0));
        const capped = (s: FieldSet) => P.detourCap > 0 && straight > 4 && routeLength(s.f[d], s.cost, door, to) > P.detourCap * straight;
        // Two groups of trips: the house's ordinary walkers and its explorers.
        const groups: [number, boolean, boolean, boolean][] = [[1 - ex, capped(base), capped(hyp), false], [ex, true, true, true]];
        for (const [share, x0, x1, explorers] of groups) {
          if (rate * share < MIN_GROUP_RATE) continue;
          const f0 = x0 ? base.fX[d] : base.f[d], c0 = x0 ? base.costX : base.cost;
          const f1 = x1 ? hyp.fX[d] : hyp.f[d], c1 = x1 ? hyp.costX : hyp.cost;
          const reachable = f1[door] < Infinity;
          // The cost change as the walker after the change judges it.
          const costRatio = reachable ? f1[door] / (x1 ? base.fX[d] : base.f[d])[door] : Infinity;
          if (reachable && x0 === x1 && Math.abs(costRatio - 1) < SAME_COST) continue;
          const oldPts = traceRoute(f0, c0, door, to);
          const newPts = reachable ? traceRoute(f1, c1, door, to) : cutAtBlock(oldPts, c1);
          if (++traced % PAIRS_PER_STEP === 0) yield;
          if (reachable && Math.abs(costRatio - 1) <= AFFECTED_COST && maxDeviation(newPts, oldPts) <= AFFECTED_CELLS) continue;
          if (!reachable) {
            // Blocking cuts off every walker of the pair alike: one entry for all of its trips.
            blocked.set(d, (blocked.get(d) ?? 0) + 1);
            all.push({ home: h, dest: d, rate, explorers, frugal: x1, oldPts, newPts, reachable, costRatio });
            break;
          }
          all.push({ home: h, dest: d, rate: rate * share, explorers, frugal: x1, oldPts, newPts, reachable, costRatio });
        }
      }
    }
    all.sort((a, b) => b.rate - a.rate);
    let rr = 0, rs = 0;
    for (const c of all) if (c.reachable) { rr += c.rate; rs += c.rate * c.costRatio; }
    return {
      changes: all.slice(0, maxPairs),
      more: Math.max(0, all.length - maxPairs),
      reroutedPerDay: all.reduce((s, c) => s + c.rate, 0),
      meanCostRatio: rr > 0 ? rs / rr : 1,
      blocked: [...blocked].map(([dest, houses]) => ({ dest, houses })),
      version,
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

/**
 * One-line readout, e.g. "Reroutes about 40 walkers a day (+8% longer)". The
 * percentage is the rate-weighted mean cost change over the rerouted trips.
 */
export function describePreview(p: FlowPreview, name: (b: number) => string): string {
  const parts: string[] = [];
  for (const b of p.blocked) parts.push(`Blocks the ${name(b.dest)} for ${b.houses} house${b.houses === 1 ? "" : "s"}`);
  const n = p.reroutedPerDay;
  if (n < 0.5) return parts[0] ?? "No change to routes";
  const shown = n < 5 ? `${Math.max(1, Math.round(n))}` : `about ${Math.max(5, Math.round(n / 5) * 5)}`;
  let line = `Reroutes ${shown} walker${shown === "1" ? "" : "s"} a day`;
  if (Math.abs(p.meanCostRatio - 1) >= 0.02) {
    const pct = Math.round((p.meanCostRatio - 1) * 100);
    line += pct > 0 ? ` (+${pct}% longer on average)` : ` (${-pct}% shorter on average)`;
  }
  parts.push(line);
  return parts.join(". ");
}

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
  GatePair: 256, // with Gate: one of the two cells of a stair-corner gate (half the delay each)
} as const;

/** A hypothetical edit. Cells are grid indices. */
export interface WhatIf {
  block?: number[]; // new fence cells
  open?: number[]; // fence cells removed
  bridge?: number[]; // water cells that become a footbridge
  closeBridge?: number[]; // footbridge cells that become water again
  gravel?: number[]; // new gravel
  ungravel?: number[]; // gravel removed
  gate?: number[]; // fence cells that become one gate (two cells: a stair-corner gate)
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
  /**
   * For a gravel strip that changes no route in the search: "far", no cell within FAR_CELLS
   * of a trail; "on", most of the strip lies on a route walkers use now; "near", a cell
   * within NEAR_CELLS of one. The search cannot see gravel win traffic over days (busy
   * gravel gets cheaper, as a trunk does), so "near" says it may, and promises nothing.
   */
  note?: "far" | "on" | "near";
}

// Pairs with fewer trips a day than this are ignored, unless the change cuts the
// house off. A grown cottage's 3 villagers make about 0.3 to 0.4 trips a day to
// each destination (round 3 used 0.5, which hid every grown house).
const MIN_RATE = 0.2;
const MIN_GROUP_RATE = 0.05; // ... and groups (ordinary or explorer trips) with fewer than this
const BASE_MAX_AGE = 1; // sim seconds before the base fields are rebuilt
const SAME_COST = 0.002; // a route whose cost changes less than this is unchanged
const AFFECTED_COST = 0.03; // ... and is affected if it changes more than this
const AFFECTED_CELLS = 3; // ... or moves more than this many cells anywhere
const GROUPS_PER_STEP = 8; // route groups classified between two yields
const FAR_CELLS = 6; // a gravel strip with no cell within this many cells of a trail is "too far from any route"
const NEAR_CELLS = 3; // ... and one within this many cells of a used route "may draw walkers" (round 4: 16 of 16 sites at 3 cells did)
const USED_FLOW = 3; // a cell is on a used route if it is trail (wear 25) or has this many recent footsteps (World.flow)

/** One set of fields (ordinary and frugal) over one pair of costs. */
interface FieldSet { cost: Float32Array; costX: Float32Array; f: Float32Array[]; fX: Float32Array[]; }

/** A changed route before it is traced for display. */
interface Candidate { change: RouteChange; x0: boolean; f0: Float32Array; c0: Float32Array; f1: Float32Array; c1: Float32Array; newPts: Float32Array | null; }

export class FlowPredictor {
  private base: FieldSet;
  private hyp: FieldSet;
  private flags = new Uint16Array(NC);
  private heap = new Heap(NC);
  /** Typical routes on the base fields, by destination, house and kind; valid while the base is. */
  private baseRoutes = new Map<number, Float32Array>();
  private baseTime = -1e9;
  private baseVersion = -1;
  private dests: number[] = [];
  private doors: number[] = [];
  /** Buildings when the sets were made: house growth adds buildings, and the sets follow. */
  private nb = -1;

  constructor(private world: World) {
    this.base = this.hyp = { cost: new Float32Array(0), costX: new Float32Array(0), f: [], fX: [] };
    this.init();
  }

  private init() {
    const world = this.world, b = world.buildings;
    this.nb = b.length;
    this.baseVersion = -1;
    this.baseRoutes.clear();
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
    if (this.world.buildings.length !== this.nb) this.init();
    const w = this.world, B = w.buildings, P = PARAMS, base = this.base, hyp = this.hyp;
    const version = w.version;
    if (w.version !== this.baseVersion || w.time - this.baseTime > BASE_MAX_AGE) {
      this.baseVersion = -1; // marks the base incomplete until the loop below ends
      const time = w.time;
      // The base costs in four slices, so the first preview of a drag does not stall a frame.
      for (let k = 0; k < 4; k++) { w.buildCosts(base.cost, base.costX, (k * NC) >> 2, ((k + 1) * NC) >> 2); yield; }
      for (const d of this.dests) {
        yield; dijkstra(B[d].door, base.cost, base.f[d], this.heap, this.doors);
        yield; dijkstra(B[d].door, base.costX, base.fX[d], this.heap, this.doors);
      }
      this.baseVersion = version; this.baseTime = time;
      this.baseRoutes.clear();
      yield;
    }
    // The hypothetical costs: the base with the changed cells re-costed under their overrides.
    hyp.cost.set(base.cost);
    hyp.costX.set(base.costX);
    const flags = this.flags, touched: number[] = [];
    for (const [key, flag] of CHANGE_FLAGS)
      for (const c of change[key] ?? []) {
        if (c < 0 || c >= NC) continue;
        if (!flags[c]) touched.push(c);
        flags[c] |= flag;
        if (key === "gate" && change.gate!.length === 2) flags[c] |= Ov.GatePair;
      }
    for (const c of touched) {
      hyp.cost[c] = w.cellCost(c, flags[c]);
      hyp.costX[c] = w.cellCost(c, flags[c], true);
      flags[c] = 0;
    }
    for (const d of this.dests)
      for (const x of [false, true]) {
        const bf = x ? base.fX[d] : base.f[d], hf = x ? hyp.fX[d] : hyp.f[d];
        if (this.outOfReach(bf, x ? base.costX : base.cost, x ? hyp.costX : hyp.cost, touched)) hf.set(bf);
        else { dijkstra(B[d].door, x ? hyp.costX : hyp.cost, hf, this.heap, this.doors); yield; }
      }

    // Explorers per house (walkers are assigned to houses in turn).
    const walkersOf = new Map<number, number>(), explorersOf = new Map<number, number>();
    for (const k of w.walkers) {
      walkersOf.set(k.home, (walkersOf.get(k.home) ?? 0) + 1);
      if (k.explorer) explorersOf.set(k.home, (explorersOf.get(k.home) ?? 0) + 1);
    }

    // Classify every (house, destination, group). Routes are traced with the
    // walkers' own aim rule, but only where it decides anything: a cost change
    // over AFFECTED_COST is a reroute whatever the route, so those are traced
    // only if they are shown.
    const all: Candidate[] = [];
    const blocked = new Map<number, number>();
    let groupsDone = 0;
    for (const h of w.houses) {
      const door = B[h].door;
      const ex = (explorersOf.get(h) ?? 0) / Math.max(1, walkersOf.get(h) ?? 0);
      for (const d of this.dests) {
        const rate = w.tripRatePerDay(h, d);
        if (!(base.f[d][door] < Infinity)) continue; // not reachable now either
        if (rate < MIN_RATE) {
          // A rare pair still counts if the change cuts a house with villagers off.
          if (!(walkersOf.get(h) ?? 0) || hyp.f[d][door] < Infinity) continue;
          const change: RouteChange = { home: h, dest: d, rate, explorers: false, frugal: false, oldPts: EMPTY, newPts: EMPTY, reachable: false, costRatio: Infinity };
          all.push({ change, x0: false, f0: base.f[d], c0: base.cost, f1: hyp.f[d], c1: hyp.cost, newPts: null });
          blocked.set(d, (blocked.get(d) ?? 0) + 1);
          continue;
        }
        const to = B[d].door;
        const straight = Math.hypot((door % N) - (to % N), ((door / N) | 0) - ((to / N) | 0));
        const capped = (s: FieldSet) => P.detourCap > 0 && straight > 4 && routeLength(s.f[d], s.cost, door, to) > P.detourCap * straight;
        // Two groups of trips: the house's ordinary walkers and its explorers.
        const groups: [number, boolean, boolean, boolean][] = [[1 - ex, capped(base), capped(hyp), false], [ex, true, true, true]];
        for (const [share, x0, x1, explorers] of groups) {
          if (rate * share < MIN_GROUP_RATE) continue;
          if (++groupsDone % GROUPS_PER_STEP === 0) yield;
          const f0 = x0 ? base.fX[d] : base.f[d], c0 = x0 ? base.costX : base.cost;
          const f1 = x1 ? hyp.fX[d] : hyp.f[d], c1 = x1 ? hyp.costX : hyp.cost;
          const reachable = f1[door] < Infinity;
          // The cost change as the walker after the change judges it.
          const costRatio = reachable ? f1[door] / (x1 ? base.fX[d] : base.f[d])[door] : Infinity;
          if (reachable && x0 === x1 && Math.abs(costRatio - 1) < SAME_COST) continue;
          let newPts: Float32Array | null = null;
          if (reachable && Math.abs(costRatio - 1) <= AFFECTED_COST) {
            newPts = traceRoute(f1, c1, door, to);
            const moved = maxDeviation(newPts, this.baseRoute(d, h, x0, f0, c0));
            yield; // up to two traces: a slice's worth
            if (moved <= AFFECTED_CELLS) continue;
          }
          const change: RouteChange = { home: h, dest: d, rate: rate * share, explorers, frugal: x1, oldPts: EMPTY, newPts: EMPTY, reachable, costRatio };
          all.push({ change, x0, f0, c0, f1, c1, newPts });
          if (!reachable) {
            // Blocking cuts off every walker of the pair alike: one entry for all of its trips.
            change.rate = rate;
            blocked.set(d, (blocked.get(d) ?? 0) + 1);
            break;
          }
        }
      }
    }
    all.sort((a, b) => b.change.rate - a.change.rate);
    const shown = all.slice(0, maxPairs);
    for (const { change: c, x0, f0, c0, f1, c1, newPts } of shown) {
      c.oldPts = this.baseRoute(c.dest, c.home, x0, f0, c0);
      c.newPts = !c.reachable ? cutAtBlock(c.oldPts, c1) : newPts ?? traceRoute(f1, c1, B[c.home].door, B[c.dest].door);
      yield;
    }
    let rr = 0, rs = 0;
    for (const { change: c } of all) if (c.reachable) { rr += c.rate; rs += c.rate * c.costRatio; }
    const gravelOnly = !!change.gravel?.length && CHANGE_FLAGS.every(([k]) => k === "gravel" || !change[k]?.length);
    return {
      note: gravelOnly && all.length === 0 ? gravelNote(w, change.gravel!) : undefined,
      changes: shown.map(x => x.change),
      more: Math.max(0, all.length - maxPairs),
      reroutedPerDay: all.reduce((s, x) => s + x.change.rate, 0),
      meanCostRatio: rr > 0 ? rs / rr : 1,
      blocked: [...blocked].map(([dest, houses]) => ({ dest, houses })),
      version,
    };
  }

  /**
   * Whether a change leaves a field as it is wherever routes from the houses
   * run: it only makes cells dearer, and every changed cell and its neighbours
   * are at least as far as the farthest house door (so no route to a door, and
   * no diagonal step past a corner, touches them). Then the base field serves.
   */
  private outOfReach(field: Float32Array, baseCost: Float32Array, cost: Float32Array, touched: number[]): boolean {
    let T = 0;
    for (const g of this.doors) T = Math.max(T, field[g]);
    for (const c of touched) {
      if (cost[c] < baseCost[c]) return false;
      const x = c % N, y = (c / N) | 0;
      for (let dy = -1; dy <= 1; dy++)
        for (let dx = -1; dx <= 1; dx++) {
          if (x + dx < 0 || y + dy < 0 || x + dx >= N || y + dy >= N) continue;
          if (field[(y + dy) * N + x + dx] < T) return false;
        }
    }
    return true;
  }

  /** The typical route from house h to destination d on the base fields (traced once per base). */
  private baseRoute(d: number, h: number, frugal: boolean, f: Float32Array, c: Float32Array): Float32Array {
    const key = (d * this.world.buildings.length + h) * 2 + (frugal ? 1 : 0);
    let r = this.baseRoutes.get(key);
    if (!r) { r = traceRoute(f, c, this.world.buildings[h].door, this.world.buildings[d].door); this.baseRoutes.set(key, r); }
    return r;
  }
  /** Runs a preview to completion (tests and the debug API). */
  compute(change: WhatIf, maxPairs = 5): FlowPreview {
    const it = this.run(change, maxPairs);
    for (;;) { const r = it.next(); if (r.done) return r.value; }
  }
}

const EMPTY = new Float32Array(0);

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

/** Whether no cell of `cells` lies within FAR_CELLS of a trail (wear 25 or more). */
function farFromTrails(wear: Float32Array, cells: number[]): boolean {
  const thr = PARAMS.stateThresholds[1];
  for (const c of cells) {
    const x = c % N, y = (c / N) | 0;
    for (let yy = y - FAR_CELLS; yy <= y + FAR_CELLS; yy++)
      for (let xx = x - FAR_CELLS; xx <= x + FAR_CELLS; xx++)
        if (xx >= 0 && yy >= 0 && xx < N && yy < N && wear[yy * N + xx] >= thr) return false;
  }
  return true;
}

/** Whether walkers use cell c now: trail, or recent footsteps. */
function used(w: World, c: number): boolean {
  return w.wear[c] >= PARAMS.stateThresholds[1] || w.flow[c] >= USED_FLOW;
}

/** The note for a gravel strip that changes no route (see FlowPreview.note). */
function gravelNote(w: World, cells: number[]): "far" | "on" | "near" | undefined {
  if (farFromTrails(w.wear, cells)) return "far";
  let on = 0, near = false;
  for (const c of cells) {
    if (used(w, c)) { on++; continue; }
    const x = c % N, y = (c / N) | 0;
    for (let yy = y - NEAR_CELLS; yy <= y + NEAR_CELLS && !near; yy++)
      for (let xx = x - NEAR_CELLS; xx <= x + NEAR_CELLS && !near; xx++)
        if (xx >= 0 && yy >= 0 && xx < N && yy < N && used(w, yy * N + xx)) near = true;
  }
  return on * 2 >= cells.length ? "on" : near || on > 0 ? "near" : undefined;
}

/** The route up to the first point that the change blocks. */
function cutAtBlock(pts: Float32Array, cost: Float32Array): Float32Array {
  for (let i = 0; i < pts.length; i += 2)
    if (cost[Math.floor(pts[i + 1]) * N + Math.floor(pts[i])] === Infinity) return pts.slice(0, Math.max(2, i));
  return pts;
}

export const GRAVEL_NOTES = {
  far: "Too far from any route to change anything",
  on: "On a route walkers already use: it keeps the trail from growing back",
  near: "May draw walkers from nearby trails",
} as const;

/**
 * One-line readout, e.g. "Reroutes about 40 walkers a day (+8% longer)". The
 * percentage is the rate-weighted mean cost change over the rerouted trips.
 */
export function describePreview(p: FlowPreview, name: (b: number) => string): string {
  const parts: string[] = [];
  for (const b of p.blocked) parts.push(`Blocks the ${name(b.dest)} for ${b.houses} house${b.houses === 1 ? "" : "s"}`);
  const n = p.reroutedPerDay;
  if (n < 0.5) return parts[0] ?? (p.note ? GRAVEL_NOTES[p.note] : "No change to routes");
  const shown = n < 5 ? `${Math.max(1, Math.round(n))}` : `about ${Math.max(5, Math.round(n / 5) * 5)}`;
  let line = `Reroutes ${shown} walker${shown === "1" ? "" : "s"} a day`;
  if (Math.abs(p.meanCostRatio - 1) >= 0.02) {
    const pct = Math.round((p.meanCostRatio - 1) * 100);
    line += pct > 0 ? ` (+${pct}% longer on average)` : ` (${-pct}% shorter on average)`;
  }
  parts.push(line);
  return parts.join(". ");
}

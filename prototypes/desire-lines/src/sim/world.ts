// The simulation: wear grid, tools, cost-to-go fields and walkers. Pure
// TypeScript with no DOM and no Math.random, so tests run it headless and a
// seed plus the same inputs at the same ticks always gives the same run.
import { PARAMS, Ground, groundOf } from "./params";
import { N, NC, idx, inside } from "./grid";
import { mulberry32, Rng, hash2 } from "./rng";
import { generateMap, MapData, Building, BuildingKind } from "./mapgen";
import { Heap, dijkstra, chooseAim, routeLength } from "./path";
import { Metrics, HealthStats, largestTrail } from "./metrics";
import { FlowPredictor, FlowPreview, Ov, WhatIf } from "./preview";
import { Weather } from "./weather";
import { GrownHouse, Plot, choosePlot } from "./growth";
import { GateTarget, gateCellsFor, gateGroup, gateTarget } from "./gates";

const enum WS { Home, Out, Away, Back }

export interface Walker {
  id: number;
  home: number; // building index
  state: WS;
  cell: number;
  // Point (cell units) the walker heads for in a straight line; recomputed on
  // entering each cell. Straight segments are what make trails any-angle
  // instead of 8-direction staircases.
  aimX: number;
  aimY: number;
  hasAim: boolean;
  timer: number; // dwell countdown
  target: number; // building index walked to
  tripStart: number;
  tripLen: number;
  stuck: number;
  explorer: boolean; // weighs distance more than company, on every trip (see PARAMS.explorerShare)
  frugal: boolean; // this trip is walked on the frugal fields
  taste: number; // -tasteNoise..+tasteNoise: how much this walker favours worn ground
  // Render positions in cell units (centre of a cell = x + 0.5) at this tick and the previous one.
  x: number; y: number; px: number; py: number;
}

export interface Bridge { id: number; row: number; cells: number[]; }

export interface Stats extends HealthStats {
  walkers: number;
  simTime: number;
  day: number;
  season: number;
  year: number;
  firstTrailTime: number;
}

export const SEASONS = ["Spring", "Summer", "Autumn", "Winter"];

/** Rules switched on for a run. The player gets both on; the old gate sets run with both off. */
export interface WorldOptions {
  rain?: boolean; // rain and mud (default PARAMS.rainOn)
  growth?: boolean; // house growth (default PARAMS.growthOn)
}


// Cost multipliers from wear and from recent traffic are lookup tables.
const WEAR_LUT_STEPS = 10, FLOW_LUT_STEPS = 4;
/** Whether walkers ever use the frugal fields (otherwise they are not built). */
const useFrugal = () => PARAMS.explorerShare > 0 || PARAMS.detourCap > 0;

export class World {
  readonly map: MapData;
  readonly seed: number;
  time = 0;
  ticks = 0;
  wear = new Float32Array(NC);
  hollowHold = new Float32Array(NC);
  hollow = new Uint8Array(NC);
  fence = new Uint8Array(NC);
  gate = new Uint8Array(NC); // a gate is a fence cell walkers may pass, at a time cost
  gatePair = new Uint8Array(NC); // 1 on the two cells of a two-cell gate (a stair corner): each has half the delay
  gravel = new Uint8Array(NC);
  bridgeAt = new Int16Array(NC).fill(-1);
  bridges: Bridge[] = [];
  /** Land cells (not water, not a building footprint): the base for coverage. */
  land = new Uint8Array(NC);
  cost = new Float32Array(NC);
  costX = new Float32Array(NC); // the frugal walkers' view of the same ground
  occ = new Float32Array(NC); // moving average of walkers per cell (crowding)
  here = new Uint16Array(NC); // walkers on each cell right now
  fields: Float32Array[] = [];
  fieldsX: Float32Array[] = []; // cost-to-go on costX, one per building
  fieldTime: number[] = [];
  urgent: boolean[] = [];
  walkers: Walker[] = [];
  metrics = new Metrics();
  /** Edit counter; the renderer redraws static layers when it changes. */
  version = 0;
  /** Recent footsteps per cell, decaying with flowHalfLife: busy lanes are cheaper. */
  flow = new Float32Array(NC);
  /**
   * Trips out for each (home, destination) building pair: a counter that decays
   * with a time constant of tripRateDays, so counter / tripRateDays is trips per day.
   * Pure bookkeeping for the flow preview; it never touches the RNG.
   */
  tripRate: Float32Array;
  private rng: Rng;
  private wearLut = new Float32Array(100 * WEAR_LUT_STEPS + 1);
  private flowLut = new Float32Array(256 * FLOW_LUT_STEPS + 1);
  private heap = new Heap(NC * 4);
  private nextId = 0;
  private nextDecay = PARAMS.decayStepSimSec;
  private byKind: Record<BuildingKind, number[]> = { house: [], well: [], field: [], market: [] };

  /** Which rules this run has (fixed for the run). */
  readonly rainOn: boolean;
  readonly growthOn: boolean;
  /** Rain and ground wetness (see weather.ts). Its own RNG stream; idle when rain is off. */
  readonly weather: Weather;
  /** 1 on cells that are mud now: worn earth (wear 25 or more) rained on, until it dries. Never gravel, bridges or the ford. */
  mud = new Uint8Array(NC);
  /** Cells currently mud (a count, for the page). */
  mudCells = 0;
  /** Houses added by growth, in the order they appeared. */
  grown: GrownHouse[] = [];
  /** Per cell, how many standing houses front it: it does not decay below frontageFloor. */
  fronted = new Uint8Array(NC);
  /** Sim time of the next household's arrival check. */
  nextHouseTime = PARAMS.houseFirstSimSec;
  /** The growth stream: only new villagers' dwell and taste draw from it, never the walkers' RNG. */
  private growthRng: Rng;

  constructor(seed: number, walkers = PARAMS.defaultWalkers, opts: WorldOptions = {}) {
    this.seed = seed;
    this.map = generateMap(seed);
    this.rng = mulberry32(seed * 31 + 5);
    this.rainOn = opts.rain ?? PARAMS.rainOn;
    this.growthOn = opts.growth ?? PARAMS.growthOn;
    this.weather = new Weather(seed);
    this.growthRng = mulberry32(((seed * 40503) ^ 0x6f7d) >>> 0);
    for (const b of this.map.buildings) this.byKind[b.kind].push(b.id);
    this.tripRate = new Float32Array(this.map.buildings.length ** 2);
    const P = PARAMS;
    for (let i = 0; i < this.wearLut.length; i++)
      this.wearLut[i] = 1 - P.trailDiscount * Math.pow(Math.min(1, i / WEAR_LUT_STEPS / P.discScale), P.discPow);
    for (let i = 0; i < this.flowLut.length; i++)
      this.flowLut[i] = 1 - P.flowDiscount * (1 - Math.exp(-(i / FLOW_LUT_STEPS) / P.flowScale));
    for (let i = 0; i < NC; i++) this.land[i] = !this.map.water[i] && !this.map.footprint[i] ? 1 : 0;
    for (const _ of this.map.buildings) {
      this.fields.push(new Float32Array(NC));
      this.fieldsX.push(new Float32Array(useFrugal() ? NC : 0));
      this.fieldTime.push(-1e9);
      this.urgent.push(true);
    }
    this.rebuildCosts();
    for (let t = 0; t < this.fields.length; t++) this.refreshField(t);
    this.setWalkerCount(walkers);
  }

  get buildings(): Building[] { return this.map.buildings; }
  get houses(): number[] { return this.byKind.house; }
  kind(k: BuildingKind): number[] { return this.byKind[k]; }

  // ---------------------------------------------------------------- cost

  blocked(c: number): boolean {
    const m = this.map;
    if ((this.fence[c] && !this.gate[c]) || m.footprint[c]) return true;
    return m.water[c] === 1 && !m.ford[c] && this.bridgeAt[c] < 0;
  }

  /**
   * Routing cost multiplier of a cell's ground: worn and busy ground is
   * cheaper, gravel costs at most gravelCost. Frugal walkers see only
   * frugalDisc of the wear and traffic discount, but all of the gravel.
   */
  groundCost(c: number, gravel: boolean, frugal: boolean): number {
    this.grounds(c, gravel);
    return this.gc[frugal ? 1 : 0];
  }

  /** Both walkers' ground multipliers for cell c, into gc[0] (ordinary) and gc[1] (frugal). */
  private grounds(c: number, gravel: boolean): void {
    const P = PARAMS;
    const f = this.flowLut[Math.min(this.flowLut.length - 1, (this.flow[c] * FLOW_LUT_STEPS) | 0)];
    const g = P.trailDiscount > 0 ? this.wearLut[Math.min(this.wearLut.length - 1, (this.wear[c] * WEAR_LUT_STEPS) | 0)]
      : P.surfaceCost[groundOf(this.wear[c], this.hollow[c] === 1)];
    const fr = 1 + (Math.max(P.minSurface, f * g) - 1) * P.frugalDisc;
    this.gc[0] = gravel ? Math.max(P.gravelFloor, f * Math.min(g, P.gravelCost)) : Math.max(P.minSurface, f * g);
    this.gc[1] = gravel ? Math.min(fr, P.gravelCost) : fr;
  }
  private gc = new Float64Array(2);

  /**
   * Mud on cell c at wetness `wet`, applied to gc[0..1]: the spec's muddy cost
   * (the state's base cost times its mud multiplier), blended with the toy's
   * own cost times the multiplier by mudReplace, then with the dry cost by wetness.
   */
  private mudCosts(c: number, wet: number): void {
    const P = PARAMS, s = groundOf(this.wear[c], this.hollow[c] === 1), mul = P.mudCostMul[s];
    for (let i = 0; i < 2; i++) {
      const dry = this.gc[i];
      const muddy = (P.mudReplace * P.surfaceCost[s] + (1 - P.mudReplace) * dry) * mul;
      this.gc[i] = dry + (muddy - dry) * wet;
    }
  }

  /** The ordinary walkers' ground multiplier for cell c (also their taste discount). */
  surf = (c: number): number => this.groundCost(c, this.gravel[c] === 1, false);

  /**
   * Step cost of cell c (Infinity where blocked). `ov` is a set of Ov flags
   * that override the cell's state for a what-if; `frugal` gives the frugal
   * walkers' cost.
   */
  cellCost(c: number, ov = 0, frugal = false): number {
    this.cellCosts(c, ov);
    return this.cc[frugal ? 1 : 0];
  }

  /** Step costs of cell c for both kinds of walker, into cc[0] (ordinary) and cc[1] (frugal). */
  private cellCosts(c: number, ov: number): void {
    const P = PARAMS, m = this.map, cc = this.cc;
    cc[0] = cc[1] = Infinity;
    if (m.footprint[c]) return;
    const fenced = (ov & Ov.Block) !== 0 || (this.fence[c] === 1 && (ov & Ov.Open) === 0);
    const gated = fenced && ((ov & Ov.Gate) !== 0 || (this.gate[c] === 1 && (ov & (Ov.Ungate | Ov.Block)) === 0));
    if (fenced && !gated) return;
    const bridge = (ov & Ov.Bridge) !== 0 || (this.bridgeAt[c] >= 0 && (ov & Ov.CloseBridge) === 0);
    if (m.water[c] && !m.ford[c] && !bridge) return;
    let k: number, kx: number;
    if (bridge) {
      // A footbridge's deck gets the same traffic discount as land (floored at
      // minSurface) but none from wear: a new bridge starts at full cost.
      const flow = this.flowLut[Math.min(this.flowLut.length - 1, (this.flow[c] * FLOW_LUT_STEPS) | 0)];
      k = kx = P.bridgeCost * (P.bridgeFlow > 0 ? Math.max(P.minSurface, flow) : 1);
    } else {
      const gravel = (ov & Ov.Gravel) !== 0 || (this.gravel[c] === 1 && (ov & Ov.Ungravel) === 0);
      const noise = 1 + P.staticNoise * m.noise[c];
      // The ford never wears and is not made easier by traffic: only land is discounted.
      if (m.water[c]) k = kx = noise;
      else {
        this.grounds(c, gravel);
        const wet = this.weather.wetness;
        if (wet > 0 && this.mud[c] && !gravel) this.mudCosts(c, wet);
        k = this.gc[0] * noise; kx = this.gc[1] * noise;
      }
      if (m.crop[c]) { k += P.cropPenalty; kx += P.cropPenalty; }
      if (m.ford[c]) { k *= P.fordCostMul; kx *= P.fordCostMul; }
    }
    const rho = this.occ[c] / P.cellAreaM2;
    if (rho > 0.05) {
      const v = Math.max(1 / P.crowdMaxCostMul, 1 - Math.exp(-P.weidmannGamma * (1 / rho - 1 / P.weidmannRhoMax)));
      const crowd = 1 + P.crowdStrength * (1 / Math.min(1, v) - 1);
      k *= crowd; kx *= crowd;
    }
    // A gate is a fixed delay: it does not grow with crowding. A two-cell gate splits it.
    if (gated) {
      const pair = (ov & Ov.GatePair) !== 0 || (this.gatePair[c] === 1 && (ov & Ov.Gate) === 0);
      const g = pair ? P.gateCost / 2 : P.gateCost;
      k += g; kx += g;
    }
    cc[0] = k; cc[1] = kx;
  }
  private cc = new Float64Array(2);

  /** Per-cell step costs for routing: ordinary walkers into `out`, frugal ones into `outX` if given. */
  buildCosts(out: Float32Array, outX?: Float32Array, from = 0, to = NC): void {
    const cc = this.cc;
    for (let c = from; c < to; c++) {
      this.cellCosts(c, 0);
      out[c] = cc[0];
      if (outX) outX[c] = cc[1];
    }
  }

  /** Rebuilds the world's own cost arrays (both views). */
  private rebuildCosts(): void {
    this.buildCosts(this.cost, useFrugal() ? this.costX : undefined);
  }

  private refreshField(t: number) {
    dijkstra(this.map.buildings[t].door, this.cost, this.fields[t], this.heap);
    if (useFrugal()) dijkstra(this.map.buildings[t].door, this.costX, this.fieldsX[t], this.heap);
    this.fieldTime[t] = this.time;
    this.urgent[t] = false;
  }

  /**
   * Whether the next tick will rebuild a cost-to-go field (about four times
   * the work of a plain tick), so the page can keep its frame budget honest by
   * not starting one it has no time for. Pure: changes nothing.
   */
  nextTickRefreshes(): boolean {
    // A household arriving searches for a plot and then marks every field for a rebuild.
    if (this.growthOn && this.time + 1 / PARAMS.tickHz >= this.nextHouseTime) return true;
    for (let t = 0; t < this.urgent.length; t++) if (this.urgent[t]) return true;
    let oldest = 0;
    for (let t = 1; t < this.fields.length; t++) if (this.fieldTime[t] < this.fieldTime[oldest]) oldest = t;
    return this.time + 1 / PARAMS.tickHz - this.fieldTime[oldest] >= PARAMS.fieldRefreshSimSec;
  }

  private refreshFields() {
    const P = PARAMS;
    let rebuilt = false;
    const ensureCost = () => { if (!rebuilt) { this.rebuildCosts(); rebuilt = true; } };
    // After an edit, destination fields first: most walkers out are heading to one.
    let budget = P.urgentFieldsPerTick;
    for (const pass of [0, 1])
      for (let t = 0; t < this.fields.length && budget > 0; t++)
        if (this.urgent[t] && (this.map.buildings[t].kind === "house") === (pass === 1)) { ensureCost(); this.refreshField(t); budget--; }
    if (rebuilt) return;
    // Otherwise rebuild at most the one stalest field, once it is due.
    let oldest = 0;
    for (let t = 1; t < this.fields.length; t++) if (this.fieldTime[t] < this.fieldTime[oldest]) oldest = t;
    if (this.time - this.fieldTime[oldest] >= P.fieldRefreshSimSec) { ensureCost(); this.refreshField(oldest); }
  }

  // ---------------------------------------------------------------- walkers

  setWalkerCount(n: number) {
    n = Math.max(0, Math.min(PARAMS.maxWalkers, Math.round(n)));
    const houses = this.byKind.house;
    while (this.walkers.length > n) {
      const w = this.walkers.pop()!;
      if (w.state === WS.Out || w.state === WS.Back) this.here[w.cell]--;
    }
    while (this.walkers.length < n && houses.length) this.addWalker(houses[this.nextId % houses.length], this.rng);
  }

  /** A new villager at home in building `home`; its dwell and taste come from `rng`. */
  private addWalker(home: number, rng: Rng) {
    const id = this.nextId++;
    const door = this.map.buildings[home].door;
    const x = (door % N) + 0.5, y = ((door / N) | 0) + 0.5;
    this.walkers.push({
      id, home, state: WS.Home, cell: door, aimX: x, aimY: y, hasAim: false,
      timer: rng() * PARAMS.dwellSimSec * 2, target: home, tripStart: door, tripLen: 0, stuck: 0,
      explorer: hash2(id, 77) < PARAMS.explorerShare, frugal: false,
      taste: (rng() * 2 - 1) * PARAMS.tasteNoise, x, y, px: x, py: y,
    });
  }

  /** Walkers drawn on the map (those not inside a building). */
  isVisible(w: Walker): boolean { return w.state === WS.Out || w.state === WS.Back; }

  private dwell(): number {
    const [a, b] = PARAMS.dwellJitter;
    return PARAMS.dwellSimSec * (a + this.rng() * (b - a));
  }

  private pickDestination(w: Walker): number {
    const mix = PARAMS.tripMix;
    const order: BuildingKind[] = ["well", "field", "market"];
    const r = this.rng() * (mix.well + mix.field + mix.market);
    const first = r < mix.well ? 0 : r < mix.well + mix.field ? 1 : 2;
    // Try the chosen kind first, then the others, nearest by travel cost.
    for (let k = 0; k < 3; k++) {
      let best = -1, bv = Infinity;
      for (const b of this.byKind[order[(first + k) % 3]]) {
        const v = this.fields[b][w.cell];
        if (v < bv) { bv = v; best = b; }
      }
      if (best >= 0) return best;
    }
    return -1;
  }

  private depart(w: Walker, target: number, state: WS) {
    w.state = state;
    w.target = target;
    w.tripStart = w.cell;
    w.tripLen = 0;
    w.stuck = 0;
    w.hasAim = false;
    w.frugal = w.explorer || this.overDetourCap(w.cell, target);
    this.here[w.cell]++;
    if (state === WS.Out) this.tripRate[w.home * this.map.buildings.length + target] += 1;
  }

  /** Whether a trip from `start` to building `target` is long enough a detour to be walked frugally. */
  overDetourCap(start: number, target: number): boolean {
    const cap = PARAMS.detourCap;
    if (!(cap > 0)) return false;
    const door = this.map.buildings[target].door;
    const d = Math.hypot((start % N) - (door % N), ((start / N) | 0) - ((door / N) | 0));
    return d > 4 && routeLength(this.fields[target], this.cost, start, door) > cap * d;
  }

  /** Flow preview for a hypothetical edit, computed in one go (the page spreads it over frames). Read-only. */
  previewFlows(change: WhatIf, maxPairs = 5): FlowPreview {
    this.predictor ??= new FlowPredictor(this);
    return this.predictor.compute(change, maxPairs);
  }
  private predictor?: FlowPredictor;

  /** Recent trips per day from house `home` to building `dest` (building indices). */
  tripRatePerDay(home: number, dest: number): number {
    return this.tripRate[home * this.map.buildings.length + dest] / PARAMS.tripRateDays;
  }

  private arrive(w: Walker) {
    const s = w.tripStart, e = w.cell;
    this.metrics.trip(this.time, w.tripLen, Math.hypot((s % N) - (e % N), ((s / N) | 0) - ((e / N) | 0)));
    this.here[w.cell]--;
    w.state = w.state === WS.Out ? WS.Away : WS.Home;
    w.timer = this.dwell();
    w.hasAim = false;
  }

  /** Sends a walker home at once (stuck, walled in, or its cell was fenced). */
  private sendHome(w: Walker) {
    if (w.state === WS.Out || w.state === WS.Back) this.here[w.cell]--;
    w.cell = this.map.buildings[w.home].door;
    w.state = WS.Home;
    w.timer = PARAMS.retrySimSec;
    w.hasAim = false;
    w.x = w.px = (w.cell % N) + 0.5;
    w.y = w.py = ((w.cell / N) | 0) + 0.5;
  }

  /** Picks the walker's next straight-line aim from its target's field (see path.ts chooseAim). */
  private computeAim(w: Walker): boolean {
    const fr = w.frugal;
    const ok = chooseAim(fr ? this.fieldsX[w.target] : this.fields[w.target], fr ? this.costX : this.cost, w.x, w.y, w.cell,
      this.map.buildings[w.target].door, fr ? 0 : w.taste, this.surf, w.id, this.aimPath, this.aimOut);
    if (ok) { w.aimX = this.aimOut[0]; w.aimY = this.aimOut[1]; w.hasAim = true; }
    return ok;
  }
  private aimPath: number[] = [];
  private aimOut = new Float64Array(2);

  private speedOn(c: number): number {
    const P = PARAMS, m = this.map;
    let s = P.walkSpeed;
    if (this.bridgeAt[c] >= 0) s *= P.bridgeSpeedMul;
    else {
      s *= this.gravel[c] ? P.gravelSpeedMul : P.stateSpeedMul[groundOf(this.wear[c], this.hollow[c] === 1)];
      if (m.ford[c]) s *= P.fordSpeedMul;
      if (this.gate[c]) s *= this.gatePair[c] ? 1 / (1 + (1 / P.gateSpeedMul - 1) / 2) : P.gateSpeedMul;
      if (this.mud[c]) { const wet = this.weather.wetness; if (wet > 0) s *= 1 + (P.mudSpeedMul - 1) * wet; }
    }
    const rho = this.here[c] / P.cellAreaM2;
    if (rho > 0.3) s *= Math.max(0.1, 1 - Math.exp(-P.weidmannGamma * (1 / rho - 1 / P.weidmannRhoMax)));
    return s;
  }

  /** Wear for entering cell c at point (x, y) while heading (dx, dy) / d. */
  private footstep(c: number, x: number, y: number, dx: number, dy: number, d: number) {
    const P = PARAMS;
    if (this.land[c] && this.bridgeAt[c] < 0) {
      const I = P.wearPerStep;
      this.wear[c] += I * (1 - this.wear[c] / 100);
      // Side neighbours, perpendicular to the heading, widen the trail.
      if (d > 1e-9) {
        // Measured from the walker's actual point, not the cell centre, so a
        // diagonal walk wears the cells either side of the corner it crosses
        // instead of leaving a checkerboard.
        const px = (-dy / d) * 0.8, py = (dx / d) * 0.8;
        for (const s of [1, -1]) {
          const sx = Math.floor(x + px * s), sy = Math.floor(y + py * s);
          if (!inside(sx, sy)) continue;
          const si = idx(sx, sy);
          if (si === c || !this.land[si] || this.fence[si]) continue;
          this.wear[si] += I * P.sideShare * (1 - this.wear[si] / 100);
        }
      }
    }
    this.flow[c]++;
    this.metrics.footstep(c);
  }

  private stepWalker(w: Walker, dt: number) {
    w.px = w.x; w.py = w.y;
    if (w.state === WS.Home || w.state === WS.Away) {
      w.timer -= dt;
      if (w.timer > 0) return;
      if (w.state === WS.Home) {
        const t = this.pickDestination(w);
        if (t < 0) { w.timer = PARAMS.retrySimSec; return; }
        this.depart(w, t, WS.Out);
      } else {
        if (!(this.fields[w.home][w.cell] < Infinity)) { this.sendHome(w); return; }
        this.depart(w, w.home, WS.Back);
      }
    }
    // Walking.
    if (w.cell === this.map.buildings[w.target].door) { this.arrive(w); return; }
    if (!w.hasAim && !this.computeAim(w)) {
      w.stuck += dt;
      // Cut off from a destination but able to get home: give up the errand.
      if (w.state === WS.Out && !(this.fields[w.target][w.cell] < Infinity) && this.fields[w.home][w.cell] < Infinity) {
        w.state = WS.Back; w.target = w.home;
      } else if (w.stuck > PARAMS.stuckSimSec) this.sendHome(w);
      return;
    }
    w.stuck = 0;
    // A runaway trip (should not happen) is ended rather than left wandering.
    if (w.tripLen > 4 * N) { this.sendHome(w); return; }
    let dist = this.speedOn(w.cell) * dt;
    for (let guard = 0; dist > 0 && guard < 4; guard++) {
      const dx = w.aimX - w.x, dy = w.aimY - w.y, d = Math.hypot(dx, dy);
      const move = Math.min(d, dist);
      if (d > 1e-9) {
        w.x += (dx / d) * move; w.y += (dy / d) * move;
        w.tripLen += move;
      }
      dist -= move;
      const c = Math.floor(w.y) * N + Math.floor(w.x);
      if (c !== w.cell) {
        this.here[w.cell]--; this.here[c]++;
        w.cell = c;
        this.footstep(c, w.x, w.y, dx, dy, d);
        w.hasAim = false;
      }
      if (move >= d) w.hasAim = false;
      if (!w.hasAim) {
        if (w.cell === this.map.buildings[w.target].door || !this.computeAim(w)) break;
      }
    }
  }

  // ---------------------------------------------------------------- tick

  tick() {
    const P = PARAMS, dt = 1 / P.tickHz;
    this.time += dt;
    this.ticks++;
    this.metrics.advance(this.time);
    const a = dt / P.crowdAvgSimSec, keep = 1 - a, occ = this.occ, here = this.here;
    for (let i = 0; i < NC; i++) occ[i] = occ[i] * keep + here[i] * a;
    const tr = this.tripRate, tk = Math.exp(-dt / (P.tripRateDays * P.daySimSec));
    for (let i = 0; i < tr.length; i++) tr[i] *= tk;
    // A new shower turns worn ground to mud at once; dry ground clears it at once.
    if (this.rainOn && (this.weather.update(this.time) || (this.mudCells > 0 && this.weather.wetness <= 0 && !this.weather.raining))) this.updateMud();
    this.refreshFields();
    for (const w of this.walkers) this.stepWalker(w, dt);
    if (this.time >= this.nextDecay) {
      this.nextDecay += P.decayStepSimSec;
      this.decay(P.decayStepSimSec);
      if (this.rainOn) this.updateMud();
      if (this.metrics.firstTrailTime < 0 && largestTrail(this.wear, this.land, P.firstTrailCells) >= P.firstTrailCells)
        this.metrics.firstTrailTime = this.time;
    }
    if (this.growthOn && this.time >= this.nextHouseTime) {
      this.nextHouseTime += P.houseEverySimSec;
      this.growHouse();
    }
  }

  // ---------------------------------------------------------------- rain and mud

  /**
   * Mud follows the rain: while it rains, every unpaved worn cell (wear 25 or
   * more, not gravel, not a bridge or the ford) is mud; the mud stays (on
   * cells still worn to trail) until the ground is dry. Runs once a sim second
   * and when a shower starts.
   */
  private updateMud(): void {
    const wx = this.weather, mud = this.mud;
    if (wx.wetness <= 0 && !wx.raining) {
      if (this.mudCells) { mud.fill(0); this.mudCells = 0; }
      return;
    }
    const thr = PARAMS.stateThresholds[1], wear = this.wear;
    let n = 0;
    for (let c = 0; c < NC; c++) {
      const worn = wear[c] >= thr && this.land[c] === 1 && this.gravel[c] === 0 && this.bridgeAt[c] < 0;
      mud[c] = worn && (wx.raining || mud[c] === 1) ? 1 : 0;
      n += mud[c];
    }
    this.mudCells = n;
  }

  /** Debug and tests: rain now for `simSec` (default a day); mud lasts as after a rain day of this season. */
  startRain(simSec = PARAMS.daySimSec, strength = 1): void {
    if (!this.rainOn) return;
    this.weather.startRain(this.time, simSec, strength);
    this.updateMud();
  }

  /** Debug and tests: stop the rain now; with `dry` the ground dries at once too. */
  stopRain(dry = false): void {
    if (!this.rainOn) return;
    this.weather.stopRain(this.time, dry);
    this.updateMud();
  }

  // ---------------------------------------------------------------- house growth

  /** A household arrives if the town is below the Hamlet target and a valid plot exists. */
  private growHouse(): Plot | null {
    const P = PARAMS;
    if (this.walkers.length + P.householdSize > Math.min(P.hamletTarget, P.maxWalkers)) return null;
    const plot = choosePlot(this);
    if (plot) this.addHouse(plot);
    return plot;
  }

  /** Builds a cottage on a plot (see growth.ts) and moves its household in. */
  addHouse(p: Plot): number {
    const P = PARAMS, m = this.map, B = m.buildings.length, id = B;
    m.buildings.push({ id, kind: "house", x: p.x, y: p.y, w: 3, h: 3, door: p.door });
    this.byKind.house.push(id);
    for (let y = p.y; y < p.y + 3; y++)
      for (let x = p.x; x < p.x + 3; x++) {
        const c = idx(x, y);
        if (c === p.door) continue;
        m.footprint[c] = 1;
        this.land[c] = 0;
        this.mud[c] = 0;
      }
    // Trip counts are indexed home * B + dest: copy them into the bigger table.
    const tr = new Float32Array((B + 1) * (B + 1));
    for (let i = 0; i < B; i++) for (let j = 0; j < B; j++) tr[i * (B + 1) + j] = this.tripRate[i * B + j];
    this.tripRate = tr;
    this.fields.push(new Float32Array(NC).fill(Infinity));
    this.fieldsX.push(new Float32Array(useFrugal() ? NC : 0).fill(Infinity));
    this.fieldTime.push(-1e9);
    this.urgent.push(true);
    for (const c of p.fronted) this.fronted[c]++;
    const time = this.time;
    this.grown.push({ building: id, x: p.x, y: p.y, w: 3, h: 3, door: p.door, front: p.front, time, day: Math.floor(time / P.daySimSec) + 1 });
    // Every field is rebuilt, one a tick, as after any edit (the new house's own one too: its
    // villagers first dwell at home and walk to a destination, so they need it only seconds later).
    this.edited();
    for (let i = 0; i < P.householdSize && this.walkers.length < P.maxWalkers; i++) this.addWalker(id, this.growthRng);
    return id;
  }

  private decay(dt: number) {
    const P = PARAMS;
    const fLow = Math.pow(0.5, dt / P.halfLifeLow), fMid = Math.pow(0.5, dt / P.halfLifeMid);
    const fHigh = Math.pow(0.5, dt / P.halfLifeHigh), fHol = Math.pow(0.5, dt / P.halfLifeHollow);
    const [, t25, t50, t85] = P.stateThresholds;
    const wear = this.wear, flow = this.flow, ff = Math.pow(0.5, dt / P.flowHalfLife);
    for (let i = 0; i < NC; i++) flow[i] *= ff;
    for (let i = 0; i < NC; i++) {
      const w = wear[i];
      if (w <= 0) continue;
      if (w >= t85) {
        this.hollowHold[i] += dt;
        if (this.hollowHold[i] >= P.hollowHoldSimSec) this.hollow[i] = 1;
      } else this.hollowHold[i] = 0;
      if (this.hollow[i] && w < t50) this.hollow[i] = 0;
      const f = this.hollow[i] ? fHol : w < t25 ? fLow : w < t50 ? fMid : fHigh;
      let nw = w * f;
      // Gravel stops a trail regrowing: wear under it never decays below trail.
      if (this.gravel[i] && w >= t25 && nw < t25) nw = t25;
      // A fronted cell (a house's frontage) does not fade below track while the house stands.
      if (this.fronted[i] && w >= P.frontageFloor && nw < P.frontageFloor) nw = P.frontageFloor;
      wear[i] = nw < 0.01 ? 0 : nw;
    }
  }

  // ---------------------------------------------------------------- tools

  private edited() {
    this.version++;
    for (let t = 0; t < this.urgent.length; t++) this.urgent[t] = true;
    // Rebuild costs now so walkers stop stepping onto new fences this tick.
    this.rebuildCosts();
    // Walkers standing on a cell that just became blocked are moved off it.
    for (const w of this.walkers) {
      if (!this.isVisible(w)) continue;
      if (this.blocked(w.cell)) this.evict(w);
      else w.hasAim = false; // re-aim against the new obstacles
    }
  }

  private evict(w: Walker) {
    // Nearest free cell within 6 rings, preferring the one closest to the walker's goal.
    const cx = w.cell % N, cy = (w.cell / N) | 0, f = this.fields[w.target];
    for (let r = 1; r <= 6; r++) {
      let best = -1, bv = Infinity;
      for (let y = cy - r; y <= cy + r; y++)
        for (let x = cx - r; x <= cx + r; x++) {
          if (Math.max(Math.abs(x - cx), Math.abs(y - cy)) !== r || !inside(x, y)) continue;
          const c = idx(x, y);
          if (this.blocked(c)) continue;
          const v = f[c] < Infinity ? f[c] : 1e12 + r;
          if (best < 0 || v < bv) { bv = v; best = c; }
        }
      if (best >= 0) {
        this.here[w.cell]--; this.here[best]++;
        w.cell = best; w.hasAim = false;
        w.x = w.px = (best % N) + 0.5; w.y = w.py = ((best / N) | 0) + 0.5;
        return;
      }
    }
    this.sendHome(w);
  }

  canFence(c: number): boolean {
    const m = this.map;
    if (m.water[c] || m.footprint[c] || this.bridgeAt[c] >= 0) return false;
    for (const b of m.buildings) if (b.door === c) return false;
    return true;
  }

  /** Places fence cells; returns how many were new. */
  placeFence(cells: number[]): number {
    let n = 0;
    // A fence on gravel takes the gravel up (the trail beneath stays).
    for (const c of cells) if (c >= 0 && c < NC && !this.fence[c] && this.canFence(c)) { this.fence[c] = 1; this.gravel[c] = 0; n++; }
    if (n) this.edited();
    return n;
  }

  /** Gravel goes on any land cell that is not fenced. */
  canGravel(c: number): boolean {
    return c >= 0 && c < NC && this.land[c] === 1 && !this.fence[c];
  }

  /**
   * Where a gravel brush at cell c lands: on the trail's centreline if a
   * worn trail is within one cell (the busiest-worn cell of the 3 x 3 round
   * it, when that is trail), otherwise on c itself.
   */
  gravelSnap(c: number): number {
    const x = c % N, y = (c / N) | 0, thr = PARAMS.stateThresholds[1];
    let best = c, bw = this.wear[c];
    for (let dy = -1; dy <= 1; dy++)
      for (let dx = -1; dx <= 1; dx++) {
        if (!inside(x + dx, y + dy)) continue;
        const v = idx(x + dx, y + dy);
        if (this.wear[v] > bw + 2 && this.canGravel(v)) { bw = this.wear[v]; best = v; }
      }
    return best !== c && bw >= thr ? best : c;
  }

  /** Lays gravel on these cells; returns how many were new. */
  placeGravel(cells: number[]): number {
    let n = 0;
    for (const c of cells) if (this.canGravel(c) && !this.gravel[c]) { this.gravel[c] = 1; this.mud[c] = 0; n++; }
    if (n) this.edited();
    return n;
  }

  /**
   * What the Gate tool does at (x, y) in cell units: the gate cells (one, or
   * a stair corner's two) on the nearest fence cell within 2 cells that can
   * take a gate, or the existing gate there (to remove), or why not. See gates.ts.
   */
  gateTarget(x: number, y: number): GateTarget {
    return gateTarget(this, x, y);
  }

  /** The first cell of what gateTarget finds at (x, y), or -1 (kept for the page and older tests). */
  gateCellAt(x: number, y: number): number {
    const t = gateTarget(this, x, y);
    return t.cells.length ? t.cells[0] : -1;
  }

  /** The cells a gate placed at fence cell c takes (one or two), or null if no gate can go there. */
  gateCellsFor(c: number): number[] | null {
    return c >= 0 && c < NC ? gateCellsFor(this, c) : null;
  }

  /** Puts a gate in the fence at cell c (and its stair partner if it needs one); false if it cannot go there. */
  placeGate(c: number): boolean {
    const cells = this.gateCellsFor(c);
    if (!cells) return false;
    for (const g of cells) { this.gate[g] = 1; this.gatePair[g] = cells.length === 2 ? 1 : 0; }
    this.edited();
    return true;
  }

  /** Turns a gate (both cells of a two-cell gate) back into plain fence. */
  removeGate(c: number): boolean {
    if (c < 0 || c >= NC || !this.gate[c]) return false;
    for (const g of this.gatePair[c] ? gateGroup(this, c) : [c]) { this.gate[g] = 0; this.gatePair[g] = 0; }
    this.edited();
    return true;
  }

  /** The bridge that would be built for a click at (x, y), or null if none fits there. */
  bridgeCellsAt(x: number, y: number): number[] | null {
    x = Math.floor(x); y = Math.floor(y);
    if (!inside(x, y)) return null;
    const m = this.map, rx = Math.floor(m.riverX[y]);
    if (Math.abs(x - rx) > m.riverHalfW + 4) return null;
    if (Math.abs(y - m.fordY) <= 2) return null;
    const cells: number[] = [];
    for (let xx = rx - 8; xx <= rx + 8; xx++) {
      if (!inside(xx, y)) continue;
      const c = idx(xx, y);
      if (m.water[c]) cells.push(c);
    }
    if (!cells.length) return null;
    for (const c of cells) if (this.bridgeAt[c] >= 0 || m.ford[c]) return null;
    // Both ends must land on open ground.
    const w0 = cells[0] - 1, w1 = cells[cells.length - 1] + 1;
    if (this.blocked(w0) || this.blocked(w1)) return null;
    return cells;
  }

  placeBridge(x: number, y: number): Bridge | null {
    const cells = this.bridgeCellsAt(x, y);
    if (!cells) return null;
    // Reuse the lowest free id so bridge ids stay small.
    let id = 0;
    while (this.bridges.some(b => b.id === id)) id++;
    const b: Bridge = { id, row: Math.floor(y), cells };
    this.bridges.push(b);
    for (const c of cells) this.bridgeAt[c] = id;
    this.edited();
    return b;
  }

  /** Removes fences (with their gates), gravel and whole bridges touching these cells; returns what was removed. */
  erase(cells: number[]): number {
    let n = 0;
    for (const c of cells) {
      if (c < 0 || c >= NC) continue;
      if (this.fence[c]) {
        // Half of a two-cell gate: its partner stays a gate on its own.
        if (this.gatePair[c]) for (const g of gateGroup(this, c)) this.gatePair[g] = 0;
        this.fence[c] = 0; this.gate[c] = 0; n++;
      }
      if (this.gravel[c]) { this.gravel[c] = 0; n++; }
      const b = this.bridgeAt[c];
      if (b >= 0) {
        const br = this.bridges.find(q => q.id === b)!;
        for (const bc of br.cells) this.bridgeAt[bc] = -1;
        this.bridges = this.bridges.filter(q => q !== br);
        n++;
      }
    }
    if (n) this.edited();
    return n;
  }

  // ---------------------------------------------------------------- read-outs

  ground(c: number): Ground { return groundOf(this.wear[c], this.hollow[c] === 1); }

  stats(): Stats {
    const h = this.metrics.compute(this.time, this.wear, this.land);
    const P = PARAMS;
    return {
      ...h,
      walkers: this.walkers.length,
      simTime: this.time,
      day: Math.floor(this.time / P.daySimSec) + 1,
      season: Math.floor(this.time / P.seasonSimSec) % 4,
      year: Math.floor(this.time / (P.seasonSimSec * 4)) + 1,
      firstTrailTime: this.metrics.firstTrailTime,
    };
  }

  walkersOut(): number {
    let n = 0;
    for (const w of this.walkers) if (this.isVisible(w)) n++;
    return n;
  }
}

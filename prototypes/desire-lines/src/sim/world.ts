// The simulation: wear grid, tools, cost-to-go fields and walkers. Pure
// TypeScript with no DOM and no Math.random, so tests run it headless and a
// seed plus the same inputs at the same ticks always gives the same run.
import { PARAMS, Ground, groundOf } from "./params";
import { N, NC, DL, idx, inside } from "./grid";
import { mulberry32, hash2, Rng } from "./rng";
import { generateMap, MapData, Building, BuildingKind } from "./mapgen";
import { Heap, dijkstra, canStep } from "./path";
import { Metrics, HealthStats, largestTrail } from "./metrics";

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

export class World {
  readonly map: MapData;
  readonly seed: number;
  time = 0;
  ticks = 0;
  wear = new Float32Array(NC);
  hollowHold = new Float32Array(NC);
  hollow = new Uint8Array(NC);
  fence = new Uint8Array(NC);
  bridgeAt = new Int16Array(NC).fill(-1);
  bridges: Bridge[] = [];
  /** Land cells (not water, not a building footprint): the base for coverage. */
  land = new Uint8Array(NC);
  cost = new Float32Array(NC);
  occ = new Float32Array(NC); // moving average of walkers per cell (crowding)
  here = new Uint16Array(NC); // walkers on each cell right now
  fields: Float32Array[] = [];
  fieldTime: number[] = [];
  urgent: boolean[] = [];
  walkers: Walker[] = [];
  metrics = new Metrics();
  /** Edit counter; the renderer redraws static layers when it changes. */
  version = 0;
  private rng: Rng;
  private heap = new Heap(NC * 4);
  private nextId = 0;
  private nextDecay = PARAMS.decayStepSimSec;
  private byKind: Record<BuildingKind, number[]> = { house: [], well: [], field: [], market: [] };

  constructor(seed: number, walkers = PARAMS.defaultWalkers) {
    this.seed = seed;
    this.map = generateMap(seed);
    this.rng = mulberry32(seed * 31 + 5);
    for (const b of this.map.buildings) this.byKind[b.kind].push(b.id);
    for (let i = 0; i < NC; i++) this.land[i] = !this.map.water[i] && !this.map.footprint[i] ? 1 : 0;
    for (const _ of this.map.buildings) { this.fields.push(new Float32Array(NC)); this.fieldTime.push(-1e9); this.urgent.push(true); }
    this.buildCost(this.cost);
    for (let t = 0; t < this.fields.length; t++) this.refreshField(t);
    this.setWalkerCount(walkers);
  }

  get buildings(): Building[] { return this.map.buildings; }
  get houses(): number[] { return this.byKind.house; }

  // ---------------------------------------------------------------- cost

  blocked(c: number): boolean {
    const m = this.map;
    if (this.fence[c] || m.footprint[c]) return true;
    return m.water[c] === 1 && !m.ford[c] && this.bridgeAt[c] < 0;
  }

  /** Per-cell step cost. `extraBlocked` lets a what-if (the flow preview) add hypothetical fences. */
  buildCost(out: Float32Array, extraBlocked?: Uint8Array): void {
    const P = PARAMS, m = this.map;
    const inv = 1 / (P.weidmannRhoMax);
    for (let c = 0; c < NC; c++) {
      if (this.blocked(c) || (extraBlocked && extraBlocked[c])) { out[c] = Infinity; continue; }
      let k: number;
      if (this.bridgeAt[c] >= 0) k = P.bridgeCost;
      else {
        k = P.surfaceCost[groundOf(this.wear[c], this.hollow[c] === 1)] * (1 + P.staticNoise * m.noise[c]);
        if (m.crop[c]) k += P.cropPenalty;
        if (m.ford[c]) k *= P.fordCostMul;
      }
      const rho = this.occ[c] / P.cellAreaM2;
      if (rho > 0.05) {
        const v = Math.max(1 / P.crowdMaxCostMul, 1 - Math.exp(-P.weidmannGamma * (1 / rho - inv)));
        k *= 1 + P.crowdStrength * (1 / Math.min(1, v) - 1);
      }
      out[c] = k;
    }
  }

  private refreshField(t: number) {
    dijkstra(this.map.buildings[t].door, this.cost, this.fields[t], this.heap);
    this.fieldTime[t] = this.time;
    this.urgent[t] = false;
  }

  /** What-if field for a target under hypothetical extra blocked cells. Touches no live state. */
  whatIfField(target: number, extraBlocked: Uint8Array): Float32Array {
    const cost = new Float32Array(NC), out = new Float32Array(NC);
    this.buildCost(cost, extraBlocked);
    dijkstra(this.map.buildings[target].door, cost, out, new Heap(NC));
    return out;
  }

  private refreshFields() {
    const P = PARAMS;
    let rebuilt = false;
    const ensureCost = () => { if (!rebuilt) { this.buildCost(this.cost); rebuilt = true; } };
    let budget = P.urgentFieldsPerTick;
    for (let t = 0; t < this.fields.length && budget > 0; t++)
      if (this.urgent[t]) { ensureCost(); this.refreshField(t); budget--; }
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
    while (this.walkers.length < n && houses.length) {
      const id = this.nextId++;
      const home = houses[id % houses.length];
      const door = this.map.buildings[home].door;
      const x = (door % N) + 0.5, y = ((door / N) | 0) + 0.5;
      this.walkers.push({
        id, home, state: WS.Home, cell: door, aimX: x, aimY: y, hasAim: false,
        timer: this.rng() * PARAMS.dwellSimSec * 2, target: home, tripStart: door, tripLen: 0, stuck: 0,
        taste: (this.rng() * 2 - 1) * PARAMS.tasteNoise, x, y, px: x, py: y,
      });
    }
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
    this.here[w.cell]++;
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

  /** One step of field descent from cell u, with this walker's taste. -1 if none. */
  private descend(w: Walker, u: number): number {
    const f = this.fields[w.target], cost = this.cost;
    const fu = f[u];
    if (!(fu < Infinity)) return -1;
    const P = PARAMS;
    let best = -1, bv = Infinity;
    for (let k = 0; k < 8; k++) {
      const v = canStep(u, k, cost);
      if (v < 0 || !(f[v] < fu)) continue; // strict progress, so no loops
      const edge = DL[k] * 0.5 * (cost[u] + cost[v]);
      const g = groundOf(this.wear[v], this.hollow[v] === 1);
      // Taste scales the worn-ground discount; step noise spreads walkers over near-equal lanes.
      const discount = (1 - P.surfaceCost[g]) * DL[k];
      const val = f[v] + edge * (1 + P.stepNoise * (hash2(w.id, v) * 2 - 1)) - w.taste * discount;
      if (val < bv) { bv = val; best = v; }
    }
    return best;
  }

  /**
   * Cost of walking straight from (x0,y0) to (x1,y1), or Infinity if the line
   * crosses a blocked cell or cuts a blocked corner.
   */
  private lineCost(x0: number, y0: number, x1: number, y1: number): number {
    const cost = this.cost;
    const len = Math.hypot(x1 - x0, y1 - y0);
    const steps = Math.max(1, Math.ceil(len / 0.2)), ds = len / steps;
    let pcx = Math.floor(x0), pcy = Math.floor(y0), sum = 0;
    for (let i = 1; i <= steps; i++) {
      const t = i / steps;
      const cx = Math.floor(x0 + (x1 - x0) * t), cy = Math.floor(y0 + (y1 - y0) * t);
      const c = cy * N + cx;
      if (cx !== pcx || cy !== pcy) {
        if (cost[c] === Infinity) return Infinity;
        if (cx !== pcx && cy !== pcy && (cost[pcy * N + cx] === Infinity || cost[cy * N + pcx] === Infinity)) return Infinity;
        pcx = cx; pcy = cy;
      }
      sum += cost[c] * ds;
    }
    return sum;
  }

  /** Looks a few cells down the field and aims at the farthest one in clear sight. */
  private computeAim(w: Walker): boolean {
    const door = this.map.buildings[w.target].door;
    const path = this.aimPath;
    path.length = 0;
    let u = w.cell;
    for (let s = 0; s < PARAMS.lookahead && u !== door; s++) {
      u = this.descend(w, u);
      if (u < 0) break;
      path.push(u);
    }
    if (!path.length) return false;
    // Take the farthest look-ahead cell whose straight line is open and no
    // dearer than the field's route there: walkers cut across uniform grass
    // but keep to a trail that bends.
    const f = this.fields[w.target], fHere = f[w.cell];
    for (let j = path.length - 1; j >= 0; j--) {
      const ax = (path[j] % N) + 0.5, ay = ((path[j] / N) | 0) + 0.5;
      const lc = this.lineCost(w.x, w.y, ax, ay);
      if (j === 0 || lc <= (fHere - f[path[j]]) * PARAMS.shortcutSlack) {
        if (lc === Infinity) {
          // Off-centre next to a corner: step back to the cell centre first.
          w.aimX = (w.cell % N) + 0.5; w.aimY = ((w.cell / N) | 0) + 0.5;
        } else { w.aimX = ax; w.aimY = ay; }
        w.hasAim = true;
        return true;
      }
    }
    return false;
  }
  private aimPath: number[] = [];

  private speedOn(c: number): number {
    const P = PARAMS, m = this.map;
    let s = P.walkSpeed;
    if (this.bridgeAt[c] >= 0) s *= P.bridgeSpeedMul;
    else {
      s *= P.stateSpeedMul[groundOf(this.wear[c], this.hollow[c] === 1)];
      if (m.ford[c]) s *= P.fordSpeedMul;
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
    this.refreshFields();
    for (const w of this.walkers) this.stepWalker(w, dt);
    if (this.time >= this.nextDecay) {
      this.nextDecay += P.decayStepSimSec;
      this.decay(P.decayStepSimSec);
      if (this.metrics.firstTrailTime < 0 && largestTrail(this.wear, this.land, P.firstTrailCells) >= P.firstTrailCells)
        this.metrics.firstTrailTime = this.time;
    }
  }

  private decay(dt: number) {
    const P = PARAMS;
    const fLow = Math.pow(0.5, dt / P.halfLifeLow), fMid = Math.pow(0.5, dt / P.halfLifeMid);
    const fHigh = Math.pow(0.5, dt / P.halfLifeHigh), fHol = Math.pow(0.5, dt / P.halfLifeHollow);
    const [, t25, t50, t85] = P.stateThresholds;
    const wear = this.wear;
    for (let i = 0; i < NC; i++) {
      const w = wear[i];
      if (w <= 0) continue;
      if (w >= t85) {
        this.hollowHold[i] += dt;
        if (this.hollowHold[i] >= P.hollowHoldSimSec) this.hollow[i] = 1;
      } else this.hollowHold[i] = 0;
      if (this.hollow[i] && w < t50) this.hollow[i] = 0;
      const f = this.hollow[i] ? fHol : w < t25 ? fLow : w < t50 ? fMid : fHigh;
      const nw = w * f;
      wear[i] = nw < 0.01 ? 0 : nw;
    }
  }

  // ---------------------------------------------------------------- tools

  private edited() {
    this.version++;
    for (let t = 0; t < this.urgent.length; t++) this.urgent[t] = true;
    // Rebuild costs now so walkers stop stepping onto new fences this tick.
    this.buildCost(this.cost);
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
    for (const c of cells) if (c >= 0 && c < NC && !this.fence[c] && this.canFence(c)) { this.fence[c] = 1; n++; }
    if (n) this.edited();
    return n;
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

  /** Removes fences and whole bridges touching these cells; returns what was removed. */
  erase(cells: number[]): number {
    let n = 0;
    for (const c of cells) {
      if (c < 0 || c >= NC) continue;
      if (this.fence[c]) { this.fence[c] = 0; n++; }
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

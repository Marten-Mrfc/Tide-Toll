// Cost-to-go fields. One Dijkstra field per target (each destination and each
// house door); walkers descend the field instead of running A* per trip. At 500
// walkers and 8x speed that is about 1000 trips/s, which per-trip A* (3-5 ms
// each across a river) cannot afford; a full field costs about 3 ms.
import { N, NC, DX, DY, DL } from "./grid";
import { PARAMS } from "./params";
import { hash2 } from "./rng";

export class Heap {
  keys: Float64Array;
  vals: Int32Array;
  n = 0;
  constructor(cap: number) {
    this.keys = new Float64Array(cap);
    this.vals = new Int32Array(cap);
  }
  push(key: number, val: number) {
    if (this.n === this.keys.length) this.grow();
    const k = this.keys, v = this.vals;
    let i = this.n++;
    while (i > 0) {
      const p = (i - 1) >> 1;
      if (k[p] <= key) break;
      k[i] = k[p]; v[i] = v[p]; i = p;
    }
    k[i] = key; v[i] = val;
  }
  /** Pops the minimum; its key is left in `lastKey`. */
  lastKey = 0;
  pop(): number {
    const k = this.keys, v = this.vals;
    const top = v[0];
    this.lastKey = k[0];
    const n = --this.n;
    const key = k[n], val = v[n];
    let i = 0;
    for (;;) {
      let c = 2 * i + 1;
      if (c >= n) break;
      if (c + 1 < n && k[c + 1] < k[c]) c++;
      if (k[c] >= key) break;
      k[i] = k[c]; v[i] = v[c]; i = c;
    }
    k[i] = key; v[i] = val;
    return top;
  }
  private grow() {
    const k = new Float64Array(this.keys.length * 2), v = new Int32Array(this.vals.length * 2);
    k.set(this.keys); v.set(this.vals);
    this.keys = k; this.vals = v;
  }
}

/**
 * Fills `out` with the cost to reach `target` from every cell. `cost[c]` is the
 * per-cell step cost, Infinity where blocked. An edge costs its length times the
 * mean of its two cells' costs, which is symmetric, so expanding outward from
 * the target gives cost-to-go. Diagonals may not cut a blocked corner, so
 * walkers never slip between two diagonal fence posts or round a water corner.
 * Pure: pass scratch arrays to compute a what-if field without touching live state.
 */
export function dijkstra(target: number, cost: Float32Array, out: Float32Array, heap: Heap, goals?: number[]): void {
  out.fill(Infinity);
  heap.n = 0;
  if (!(cost[target] < Infinity)) return;
  out[target] = 0;
  heap.push(0, target);
  // With `goals`, stop once all of them are settled: every cell on a descent
  // from a goal is cheaper than it, so it is settled too.
  let left = goals ? goals.length : -1;
  while (heap.n > 0) {
    const u = heap.pop();
    const du = heap.lastKey;
    if (du > out[u]) continue; // stale entry
    if (left > 0 && goals!.includes(u) && --left === 0) return;
    const ux = u % N, uy = (u / N) | 0, cu = cost[u];
    // Away from the border no neighbour can be off the grid (the common case, without bounds checks).
    const interior = ux > 0 && uy > 0 && ux < N - 1 && uy < N - 1;
    for (let k = 0; k < 8; k++) {
      if (!interior) {
        const vx = ux + DX[k], vy = uy + DY[k];
        if (vx < 0 || vy < 0 || vx >= N || vy >= N) continue;
      }
      const v = u + OFF[k];
      const cv = cost[v];
      if (cv === Infinity) continue;
      if (k >= 4 && (cost[u + DX[k]] === Infinity || cost[u + DY[k] * N] === Infinity)) continue;
      // Round to float32 so the stale check above compares like with like.
      const nd = Math.fround(du + HALF_DL[k] * (cu + cv));
      if (nd < out[v]) { out[v] = nd; heap.push(nd, v); }
    }
  }
}
const OFF = DX.map((dx, k) => DY[k] * N + dx);
const HALF_DL = DL.map(l => l * 0.5);

/** Whether a step from u in direction k is allowed under `cost` (bounds, blocking, corner rule). */
export function canStep(u: number, k: number, cost: Float32Array): number {
  const ux = u % N, uy = (u / N) | 0;
  const vx = ux + DX[k], vy = uy + DY[k];
  if (vx < 0 || vy < 0 || vx >= N || vy >= N) return -1;
  const v = vy * N + vx;
  if (cost[v] === Infinity) return -1;
  if (k >= 4 && (cost[uy * N + vx] === Infinity || cost[vy * N + ux] === Infinity)) return -1;
  return v;
}

// ---------------------------------------------------------------- walking
// The step and aim rules walkers use, as pure functions of (field, cost), so the
// flow preview traces exactly the route a walker would take, from the same code.

/**
 * One step of field descent from u, or -1. `taste` scales the worn-ground
 * discount `disc(v)` (a cost multiplier below 1); `noiseId` >= 0 adds that
 * walker's fixed per-cell step noise. Taste 0 and noiseId -1 give the modal step.
 */
export function descendStep(field: Float32Array, cost: Float32Array, u: number,
  taste: number, disc: ((c: number) => number) | null, noiseId: number): number {
  const fu = field[u];
  if (!(fu < Infinity)) return -1;
  const noise = PARAMS.stepNoise;
  let best = -1, bv = Infinity;
  for (let k = 0; k < 8; k++) {
    const v = canStep(u, k, cost);
    if (v < 0 || !(field[v] < fu)) continue; // strict progress, so no loops
    const edge = DL[k] * 0.5 * (cost[u] + cost[v]);
    let val = field[v] + (noiseId >= 0 ? edge * (1 + noise * (hash2(noiseId, v) * 2 - 1)) : edge);
    if (taste !== 0 && disc) val -= taste * (1 - disc(v)) * DL[k];
    if (val < bv) { bv = val; best = v; }
  }
  return best;
}

/**
 * Cost of walking straight from (x0,y0) to (x1,y1), or Infinity if the line
 * crosses a blocked cell or cuts a blocked corner.
 */
export function lineCost(cost: Float32Array, x0: number, y0: number, x1: number, y1: number): number {
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

/**
 * Where a walker at (x, y) in `cell` heads next: it looks `lookahead` cells down
 * the field and aims at the farthest one whose straight line is open and no
 * dearer than the field's route there, so walkers cut across uniform grass but
 * keep to a trail that bends. Writes the aim to out[0..1]; false if no step.
 */
export function chooseAim(field: Float32Array, cost: Float32Array, x: number, y: number, cell: number, door: number,
  taste: number, disc: ((c: number) => number) | null, noiseId: number, path: number[], out: Float64Array): boolean {
  path.length = 0;
  let u = cell;
  for (let s = 0; s < PARAMS.lookahead && u !== door; s++) {
    u = descendStep(field, cost, u, taste, disc, noiseId);
    if (u < 0) break;
    path.push(u);
  }
  if (!path.length) return false;
  const fHere = field[cell];
  for (let j = path.length - 1; j >= 0; j--) {
    const ax = (path[j] % N) + 0.5, ay = ((path[j] / N) | 0) + 0.5;
    const lc = lineCost(cost, x, y, ax, ay);
    if (j === 0 || lc <= (fHere - field[path[j]]) * PARAMS.shortcutSlack) {
      if (lc === Infinity) {
        // Off-centre next to a corner: step back to the cell centre first.
        out[0] = (cell % N) + 0.5; out[1] = ((cell / N) | 0) + 0.5;
      } else { out[0] = ax; out[1] = ay; }
      return true;
    }
  }
  return false;
}

const tracePath: number[] = [];
const traceAim = new Float64Array(2);

/**
 * The typical route from `start` to `door` (no taste, no step noise), as x,y
 * points in cell units: the walker's aim rule, re-aimed on entering each cell.
 */
export function traceRoute(field: Float32Array, cost: Float32Array, start: number, door: number): Float32Array {
  let x = (start % N) + 0.5, y = ((start / N) | 0) + 0.5, cell = start;
  const pts: number[] = [x, y];
  for (let it = 0; it < 4 * N && cell !== door; it++) {
    if (!chooseAim(field, cost, x, y, cell, door, 0, null, -1, tracePath, traceAim)) break;
    const ax = traceAim[0], ay = traceAim[1], d = Math.hypot(ax - x, ay - y);
    if (d < 1e-6) break;
    // Walk towards the aim until entering a new cell (where a walker re-aims) or arriving.
    const ux = (ax - x) / d, uy = (ay - y) / d;
    let t = 0, nc = cell;
    while (t < d) {
      t = Math.min(d, t + 0.1);
      nc = Math.floor(y + uy * t) * N + Math.floor(x + ux * t);
      if (nc !== cell) break;
    }
    x += ux * t; y += uy * t; cell = nc;
    pts.push(x, y);
  }
  return Float32Array.from(pts);
}

/**
 * Follows a field downhill from `start`; returns the cells visited (start
 * included). Shared by tests and, later, by the flow preview's ghost routes.
 */
export function followField(field: Float32Array, cost: Float32Array, start: number, maxSteps = NC): number[] {
  const path = [start];
  let u = start;
  for (let s = 0; s < maxSteps && field[u] > 0 && field[u] < Infinity; s++) {
    let best = -1, bv = field[u];
    for (let k = 0; k < 8; k++) {
      const v = canStep(u, k, cost);
      if (v >= 0 && field[v] < bv) { bv = field[v]; best = v; }
    }
    if (best < 0) break;
    path.push(best);
    u = best;
  }
  return path;
}

/**
 * Length in cells of the route a plain field descent takes from `start` to
 * `door` (steepest descent, no taste or noise), or Infinity if it cannot get
 * there. Used for the detour cap: a trip whose route is much longer than the
 * straight line is walked on the frugal fields.
 */
export function routeLength(field: Float32Array, cost: Float32Array, start: number, door: number): number {
  let u = start, len = 0;
  for (let s = 0; s < 4 * N && u !== door; s++) {
    let best = -1, bv = field[u], bk = 0;
    for (let k = 0; k < 8; k++) {
      const v = canStep(u, k, cost);
      if (v >= 0 && field[v] < bv) { bv = field[v]; best = v; bk = k; }
    }
    if (best < 0) return Infinity;
    len += DL[bk]; u = best;
  }
  return u === door ? len : Infinity;
}

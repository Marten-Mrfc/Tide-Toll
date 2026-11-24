// Cost-to-go fields. One Dijkstra field per target (each destination and each
// house door); walkers descend the field instead of running A* per trip. At 500
// walkers and 8x speed that is about 1000 trips/s, which per-trip A* (3-5 ms
// each across a river) cannot afford; a full field costs about 3 ms.
import { N, NC, DX, DY, DL } from "./grid";

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
export function dijkstra(target: number, cost: Float32Array, out: Float32Array, heap: Heap): void {
  out.fill(Infinity);
  heap.n = 0;
  if (!(cost[target] < Infinity)) return;
  out[target] = 0;
  heap.push(0, target);
  while (heap.n > 0) {
    const u = heap.pop();
    const du = heap.lastKey;
    if (du > out[u]) continue; // stale entry
    const ux = u % N, uy = (u / N) | 0, cu = cost[u];
    for (let k = 0; k < 8; k++) {
      const vx = ux + DX[k], vy = uy + DY[k];
      if (vx < 0 || vy < 0 || vx >= N || vy >= N) continue;
      const v = vy * N + vx;
      const cv = cost[v];
      if (cv === Infinity) continue;
      if (k >= 4 && (cost[uy * N + vx] === Infinity || cost[vy * N + ux] === Infinity)) continue;
      // Round to float32 so the stale check below compares like with like.
      const nd = Math.fround(du + DL[k] * 0.5 * (cu + cv));
      if (nd < out[v]) { out[v] = nd; heap.push(nd, v); }
    }
  }
}

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

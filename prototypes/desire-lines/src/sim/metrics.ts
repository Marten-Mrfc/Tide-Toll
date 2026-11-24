// Health metrics, as defined by the lead (decision 11) and the trail-tuning
// research. The window is the last 2 in-game days, kept as ring buckets.
import { PARAMS } from "./params";
import { N, NC, DX, DY } from "./grid";

export interface HealthStats {
  detourRatio: number; // mean actual/straight route length, trips ending in the window
  trailCoverage: number; // share of walkable land at trail state or above
  trunkShare: number; // literal: footsteps on busiest 10% of trail cells / all footsteps
  trunkShareD: number; // research variant: same numerator / footsteps on trail cells
  tripsInWindow: number;
  trailCells: number;
}

export class Metrics {
  readonly bucketSec = (PARAMS.metricWindowDays * PARAMS.daySimSec) / PARAMS.metricBuckets;
  buckets: Uint16Array[] = [];
  cur = 0;
  private bucketStart = 0;
  // Completed trips as parallel arrays: end time, path length, straight length.
  tripEnd: number[] = [];
  tripLen: number[] = [];
  tripStraight: number[] = [];
  firstTrailTime = -1;

  constructor() {
    for (let b = 0; b < PARAMS.metricBuckets; b++) this.buckets.push(new Uint16Array(NC));
  }

  advance(time: number) {
    if (time - this.bucketStart >= this.bucketSec) {
      this.bucketStart = time;
      this.cur = (this.cur + 1) % this.buckets.length;
      this.buckets[this.cur].fill(0);
      // Forget trips older than the window.
      const cutoff = time - PARAMS.metricWindowDays * PARAMS.daySimSec;
      let drop = 0;
      while (drop < this.tripEnd.length && this.tripEnd[drop] < cutoff) drop++;
      if (drop > 0) {
        this.tripEnd.splice(0, drop);
        this.tripLen.splice(0, drop);
        this.tripStraight.splice(0, drop);
      }
    }
  }

  footstep(c: number) {
    const b = this.buckets[this.cur];
    if (b[c] < 65535) b[c]++;
  }

  trip(time: number, len: number, straight: number) {
    if (straight <= 3) return; // trips this short say nothing about detours
    this.tripEnd.push(time);
    this.tripLen.push(len);
    this.tripStraight.push(straight);
  }

  /** Footsteps per cell over the window. */
  window(out: Uint32Array = new Uint32Array(NC)): Uint32Array {
    out.fill(0);
    for (const b of this.buckets) for (let i = 0; i < NC; i++) out[i] += b[i];
    return out;
  }

  compute(time: number, wear: Float32Array, walkable: Uint8Array): HealthStats {
    const cutoff = time - PARAMS.metricWindowDays * PARAMS.daySimSec;
    let sum = 0, n = 0;
    for (let i = 0; i < this.tripEnd.length; i++)
      if (this.tripEnd[i] >= cutoff) { sum += this.tripLen[i] / this.tripStraight[i]; n++; }

    const thr = PARAMS.stateThresholds[1];
    const fw = this.window();
    let land = 0, total = 0, onTrail = 0;
    const trail: number[] = [];
    for (let i = 0; i < NC; i++) {
      total += fw[i];
      if (!walkable[i]) continue;
      land++;
      if (wear[i] >= thr) { trail.push(fw[i]); onTrail += fw[i]; }
    }
    trail.sort((a, b) => b - a);
    const k = Math.ceil(trail.length * PARAMS.trunkTopShare);
    let top = 0;
    for (let i = 0; i < k; i++) top += trail[i];
    return {
      detourRatio: n ? sum / n : NaN,
      trailCoverage: land ? trail.length / land : 0,
      trunkShare: total ? top / total : 0,
      trunkShareD: onTrail ? top / onTrail : 0,
      tripsInWindow: n,
      trailCells: trail.length,
    };
  }
}

/** Size of the largest 8-connected group of trail cells, stopping early at `enough`. */
export function largestTrail(wear: Float32Array, walkable: Uint8Array, enough: number): number {
  const thr = PARAMS.stateThresholds[1];
  const seen = new Uint8Array(NC);
  const stack: number[] = [];
  let best = 0;
  for (let i = 0; i < NC; i++) {
    if (seen[i] || wear[i] < thr || !walkable[i]) continue;
    let size = 0;
    stack.push(i);
    seen[i] = 1;
    while (stack.length) {
      const u = stack.pop()!;
      size++;
      const ux = u % N, uy = (u / N) | 0;
      for (let k = 0; k < 8; k++) {
        const x = ux + DX[k], y = uy + DY[k];
        if (x < 0 || y < 0 || x >= N || y >= N) continue;
        const v = y * N + x;
        if (!seen[v] && wear[v] >= thr && walkable[v]) { seen[v] = 1; stack.push(v); }
      }
    }
    if (size > best) best = size;
    if (best >= enough) return best;
  }
  return best;
}

/**
 * Median trunk width in cells: for the busiest 10% of trail cells (wear 25 or
 * more, by footsteps in the window), the run of trail cells straight across
 * the trail's local direction (the traffic-weighted principal axis of the
 * trail cells within 3 cells). Report only; the round 3 tuning definition.
 */
export function trailWidth(wear: Float32Array, walkable: Uint8Array, steps: Uint32Array): number {
  const thr = PARAMS.stateThresholds[1];
  const cells: number[] = [];
  for (let c = 0; c < NC; c++) if (walkable[c] && wear[c] >= thr) cells.push(c);
  cells.sort((a, b) => steps[b] - steps[a]);
  const top = cells.slice(0, Math.ceil(cells.length * PARAMS.trunkTopShare));
  const widths: number[] = [];
  const trail = (x: number, y: number) => x >= 0 && y >= 0 && x < N && y < N && wear[y * N + x] >= thr;
  for (const c of top) {
    const cx = c % N, cy = (c / N) | 0;
    let sxx = 0, syy = 0, sxy = 0;
    for (let y = cy - 3; y <= cy + 3; y++)
      for (let x = cx - 3; x <= cx + 3; x++) {
        if (!trail(x, y)) continue;
        const m = steps[y * N + x] + 1;
        sxx += m * (x - cx) ** 2; syy += m * (y - cy) ** 2; sxy += m * (x - cx) * (y - cy);
      }
    const ang = 0.5 * Math.atan2(2 * sxy, sxx - syy); // along the trail
    const px = -Math.sin(ang), py = Math.cos(ang); // across it
    let n = 1;
    for (const s of [1, -1])
      for (let t = 1; t <= 8; t++) {
        if (!trail(Math.round(cx + px * s * t), Math.round(cy + py * s * t))) break;
        n++;
      }
    widths.push(n);
  }
  widths.sort((a, b) => a - b);
  return widths.length ? widths[widths.length >> 1] : 0;
}

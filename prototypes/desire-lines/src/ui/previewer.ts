// Runs the flow preview for the page: throttles requests while the pointer
// moves, spreads each computation over frames within a time budget, and keeps
// showing the last result until the next one is ready.
import { World } from "../sim/world";
import { FlowPredictor, FlowPreview, WhatIf } from "../sim/preview";

const MIN_GAP_MS = 100; // between starting two computations
const REFRESH_MS = 1000; // recompute an unchanged request this often (traffic drifts)

export class FlowPreviewer {
  enabled = true;
  current: FlowPreview | null = null;
  private predictor: FlowPredictor;
  private world: World;
  private want: { change: WhatIf; key: string } | null = null;
  private job: Generator<void, FlowPreview, void> | null = null;
  private jobKey = "";
  private shownKey = "";
  private shownAt = 0;
  private shownVersion = -1;
  private startedAt = -1e9;

  constructor(world: World) {
    this.world = world;
    this.predictor = new FlowPredictor(world);
  }

  setWorld(w: World) { this.world = w; this.predictor = new FlowPredictor(w); this.clear(); }

  /** Asks for a preview of `change`; `key` identifies it so repeats are free. */
  request(change: WhatIf, key: string) {
    if (!this.enabled) return;
    if (this.want && this.want.key === key) return;
    this.want = { change, key };
  }

  clear() { this.want = null; this.job = null; this.current = null; this.shownKey = ""; }

  /** Call once a frame. `budgetMs` is how long it may compute this frame. */
  update(now: number, budgetMs: number) {
    if (!this.enabled || !this.want) { this.job = null; return; }
    const w = this.want;
    const stale = w.key !== this.shownKey || this.world.version !== this.shownVersion || now - this.shownAt > REFRESH_MS;
    if (!this.job && stale && now - this.startedAt >= MIN_GAP_MS) {
      this.job = this.predictor.run(w.change);
      this.jobKey = w.key;
      this.startedAt = now;
    }
    if (!this.job) return;
    if (this.jobKey !== w.key && now - this.startedAt >= MIN_GAP_MS) {
      // The pointer moved on: drop the old computation and start the new one.
      this.job = this.predictor.run(w.change);
      this.jobKey = w.key;
      this.startedAt = now;
    }
    const t0 = performance.now();
    do {
      const r = this.job.next();
      if (r.done) {
        this.job = null;
        // A result computed across an edit describes a map that is gone: drop it (a new run starts).
        if (this.jobKey === this.want.key && r.value.version === this.world.version) {
          this.current = r.value;
          this.shownKey = this.jobKey;
          this.shownAt = now;
          this.shownVersion = r.value.version;
        }
        return;
      }
    } while (performance.now() - t0 < budgetMs);
  }

  /** Whether the shown preview matches the latest request. */
  get settled(): boolean { return !!this.want && this.shownKey === this.want.key && !this.job; }
}

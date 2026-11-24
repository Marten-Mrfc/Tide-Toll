// Pointer input for the three tools. Pointer events cover mouse, pen and touch.
import { World } from "../sim/world";
import { N, idx, inside, line4 } from "../sim/grid";
import { Renderer } from "./render";

export type Tool = "none" | "fence" | "bridge" | "erase";

export class Input {
  tool: Tool = "none";
  private dragStart: [number, number] | null = null;
  private erasing = false;
  private lastErase: [number, number] | null = null;

  constructor(
    private canvas: HTMLCanvasElement,
    private getWorld: () => World,
    private renderer: Renderer,
    private say: (msg: string) => void,
  ) {
    canvas.addEventListener("pointerdown", e => this.down(e));
    canvas.addEventListener("pointermove", e => this.move(e));
    canvas.addEventListener("pointerup", e => this.up(e));
    canvas.addEventListener("pointercancel", () => this.cancel());
    canvas.addEventListener("pointerleave", () => { if (!this.dragStart && !this.erasing) this.renderer.preview = null; });
  }

  setTool(t: Tool) {
    this.tool = t;
    this.cancel();
    this.canvas.dataset.tool = t;
  }

  /** Pointer position in cell units (fractional). */
  private at(e: PointerEvent): [number, number] {
    const r = this.canvas.getBoundingClientRect();
    return [((e.clientX - r.left) / r.width) * N, ((e.clientY - r.top) / r.height) * N];
  }

  private cell(e: PointerEvent): [number, number] {
    const [x, y] = this.at(e);
    return [Math.max(0, Math.min(N - 1, Math.floor(x))), Math.max(0, Math.min(N - 1, Math.floor(y)))];
  }

  private down(e: PointerEvent) {
    if (this.tool === "none" || (e.pointerType === "mouse" && e.button !== 0)) return;
    e.preventDefault();
    this.canvas.setPointerCapture(e.pointerId);
    const w = this.getWorld();
    if (this.tool === "fence") {
      this.dragStart = this.cell(e);
      this.fencePreview(this.dragStart);
    } else if (this.tool === "bridge") {
      const [x, y] = this.at(e);
      const b = w.placeBridge(x, y);
      this.say(b ? `Footbridge built (${b.cells.length} cells).` : "A footbridge needs open water with a bank on both sides, away from the ford.");
      this.hover(e);
    } else if (this.tool === "erase") {
      this.erasing = true;
      this.lastErase = null;
      this.eraseAt(e);
    }
  }

  private move(e: PointerEvent) {
    if (this.tool === "fence" && this.dragStart) this.fencePreview(this.cell(e));
    else if (this.tool === "erase" && this.erasing) this.eraseAt(e);
    else this.hover(e);
  }

  private up(e: PointerEvent) {
    if (this.tool === "fence" && this.dragStart) {
      const cells = this.fenceCells(this.cell(e));
      const n = this.getWorld().placeFence(cells);
      this.say(n ? `Fence placed (${n} cells). Watch the walkers find a way round.` : "Nothing to fence there.");
      this.dragStart = null;
      this.hover(e);
    }
    this.erasing = false;
    this.lastErase = null;
  }

  private cancel() {
    this.dragStart = null;
    this.erasing = false;
    this.renderer.preview = null;
  }

  private fenceCells(end: [number, number]): number[] {
    const [x0, y0] = this.dragStart!;
    return line4(x0, y0, end[0], end[1]);
  }

  private fencePreview(end: [number, number]) {
    const w = this.getWorld();
    const cells = this.fenceCells(end);
    this.renderer.preview = { kind: "fence", cells, ok: cells.map(c => w.canFence(c)) };
  }

  private eraseAt(e: PointerEvent) {
    const w = this.getWorld();
    const [x, y] = this.cell(e);
    // Erase along the drag so a fast swipe leaves no gaps.
    const path = this.lastErase ? line4(this.lastErase[0], this.lastErase[1], x, y) : [idx(x, y)];
    this.lastErase = [x, y];
    const cells: number[] = [];
    for (const c of path) {
      const cx = c % N, cy = (c / N) | 0;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) if (inside(cx + dx, cy + dy)) cells.push(idx(cx + dx, cy + dy));
    }
    if (w.erase(cells)) this.say("Removed.");
    const [fx, fy] = this.at(e);
    this.renderer.preview = { kind: "erase", x: fx, y: fy };
  }

  private hover(e: PointerEvent) {
    const [x, y] = this.at(e);
    if (this.tool === "bridge") this.renderer.preview = { kind: "bridge", cells: this.getWorld().bridgeCellsAt(x, y), x, y };
    else if (this.tool === "erase") this.renderer.preview = { kind: "erase", x, y };
    else if (this.tool === "fence") {
      const c = this.cell(e);
      this.renderer.preview = { kind: "fence", cells: [idx(c[0], c[1])], ok: [this.getWorld().canFence(idx(c[0], c[1]))] };
    } else this.renderer.preview = null;
  }
}

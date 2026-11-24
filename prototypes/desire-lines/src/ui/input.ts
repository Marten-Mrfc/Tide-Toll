// Pointer input for the three tools. Pointer events cover mouse, pen and touch.
// Each tool shows a ghost and asks for a flow preview before it commits:
// - Fence: drag; the preview runs during the drag, placed on release.
// - Footbridge: hover (or press, on touch) previews; placed on release over the same spot.
// - Erase: hover (or press, on touch) previews what goes; click or drag removes.
import { World } from "../sim/world";
import { N, idx, inside, line4 } from "../sim/grid";
import { WhatIf } from "../sim/preview";
import { Renderer } from "./render";
import { FlowPreviewer } from "./previewer";

export type Tool = "none" | "fence" | "bridge" | "erase";

export class Input {
  tool: Tool = "none";
  /** Pointer position over the map in CSS px relative to the canvas, or null when away. */
  pointer: [number, number] | null = null;
  private dragStart: [number, number] | null = null;
  private erasing = false;
  private eraseDown: [number, number] | null = null;
  private lastErase: [number, number] | null = null;
  private bridgeDown: number[] | null = null;

  constructor(
    private canvas: HTMLCanvasElement,
    private getWorld: () => World,
    private renderer: Renderer,
    private previewer: FlowPreviewer,
    private say: (msg: string) => void,
  ) {
    canvas.addEventListener("pointerdown", e => this.down(e));
    canvas.addEventListener("pointermove", e => this.move(e));
    canvas.addEventListener("pointerup", e => this.up(e));
    canvas.addEventListener("pointercancel", () => this.cancel());
    canvas.addEventListener("pointerleave", () => { if (!this.dragStart && !this.erasing && !this.bridgeDown) this.cancel(); });
  }

  setTool(t: Tool) {
    this.tool = t;
    this.cancel();
    this.canvas.dataset.tool = t;
  }

  /** Pointer position in cell units (fractional), inside the canvas's border; null if the canvas has no size. */
  private at(e: PointerEvent): [number, number] | null {
    const c = this.canvas, r = c.getBoundingClientRect();
    const w = c.clientWidth, h = c.clientHeight;
    if (!(w > 0 && h > 0)) return null;
    const x = e.clientX - r.left - c.clientLeft, y = e.clientY - r.top - c.clientTop;
    this.pointer = [x + c.clientLeft, y + c.clientTop];
    return [(x / w) * N, (y / h) * N];
  }

  private cell(e: PointerEvent): [number, number] | null {
    const p = this.at(e);
    if (!p) return null;
    return [Math.max(0, Math.min(N - 1, Math.floor(p[0]))), Math.max(0, Math.min(N - 1, Math.floor(p[1])))];
  }

  private down(e: PointerEvent) {
    if (this.tool === "none" || (e.pointerType === "mouse" && e.button !== 0)) return;
    const c = this.cell(e);
    if (!c) return;
    e.preventDefault();
    this.canvas.setPointerCapture(e.pointerId);
    if (this.tool === "fence") {
      this.dragStart = c;
      this.fenceGhost(c);
    } else if (this.tool === "bridge") {
      this.hover(e);
      const g = this.renderer.ghost;
      this.bridgeDown = g && g.kind === "bridge" ? g.cells : null;
      if (!this.bridgeDown) this.say("A footbridge needs open water with a bank on both sides, away from the ford.");
    } else if (this.tool === "erase") {
      this.eraseDown = c;
      this.lastErase = null;
      this.hover(e);
      // Mouse and pen erase at once and along a drag; touch waits for release or a drag.
      if (e.pointerType !== "touch") { this.erasing = true; this.eraseAt(e); }
    }
  }

  private move(e: PointerEvent) {
    if (this.tool === "fence" && this.dragStart) { const c = this.cell(e); if (c) this.fenceGhost(c); }
    else if (this.tool === "erase" && this.eraseDown && !this.erasing) {
      const c = this.cell(e);
      if (c && Math.max(Math.abs(c[0] - this.eraseDown[0]), Math.abs(c[1] - this.eraseDown[1])) > 1) { this.erasing = true; this.lastErase = this.eraseDown; this.eraseAt(e); }
    } else if (this.tool === "erase" && this.erasing) this.eraseAt(e);
    else this.hover(e);
  }

  private up(e: PointerEvent) {
    const w = this.getWorld();
    if (this.tool === "fence" && this.dragStart) {
      const c = this.cell(e);
      const n = c ? w.placeFence(this.fenceCells(c)) : 0;
      this.say(n ? `Fence placed (${n} cells). Watch the walkers find a way round.` : "Nothing to fence there.");
      this.dragStart = null;
      this.previewer.clear();
      this.hover(e);
    } else if (this.tool === "bridge" && this.bridgeDown) {
      const p = this.at(e);
      const cells = p ? w.bridgeCellsAt(p[0], p[1]) : null;
      // Placed only if released over the same crossing; sliding away cancels.
      if (cells && cells[0] === this.bridgeDown[0] && p) {
        const b = w.placeBridge(p[0], p[1]);
        this.say(b ? `Footbridge built (${b.cells.length} cells).` : "No room for a footbridge there.");
      } else this.say("Footbridge cancelled.");
      this.bridgeDown = null;
      this.previewer.clear();
      this.hover(e);
    } else if (this.tool === "erase" && this.eraseDown) {
      if (!this.erasing) this.eraseAt(e); // a tap on touch
      this.previewer.clear();
      this.hover(e);
    }
    this.erasing = false;
    this.eraseDown = null;
    this.lastErase = null;
  }

  private cancel() {
    this.dragStart = null;
    this.erasing = false;
    this.eraseDown = null;
    this.bridgeDown = null;
    this.pointer = null;
    this.renderer.ghost = null;
    this.previewer.clear();
  }

  private fenceCells(end: [number, number]): number[] {
    const [x0, y0] = this.dragStart!;
    return line4(x0, y0, end[0], end[1]);
  }

  private fenceGhost(end: [number, number]) {
    const w = this.getWorld();
    const cells = this.fenceCells(end), ok = cells.map(c => w.canFence(c) && !w.fence[c]);
    this.renderer.ghost = { kind: "fence", cells, ok };
    const block = cells.filter((_, i) => ok[i]);
    if (block.length) this.preview({ block }, `f${block.join(",")}`);
    else this.previewer.clear();
  }

  /** Fence cells in the 3x3 round a cell, and every cell of any bridge touched. */
  private eraseTargets(x: number, y: number): { fence: number[]; bridge: number[] } {
    const w = this.getWorld(), fence: number[] = [], bridge: number[] = [];
    for (let dy = -1; dy <= 1; dy++)
      for (let dx = -1; dx <= 1; dx++) {
        if (!inside(x + dx, y + dy)) continue;
        const c = idx(x + dx, y + dy);
        if (w.fence[c]) fence.push(c);
        const b = w.bridgeAt[c];
        if (b >= 0 && !bridge.includes(c)) for (const bc of w.bridges.find(q => q.id === b)!.cells) if (!bridge.includes(bc)) bridge.push(bc);
      }
    return { fence, bridge };
  }

  private eraseAt(e: PointerEvent) {
    const w = this.getWorld();
    const c = this.cell(e);
    if (!c) return;
    // Erase along the drag so a fast swipe leaves no gaps.
    const path = this.lastErase ? line4(this.lastErase[0], this.lastErase[1], c[0], c[1]) : [idx(c[0], c[1])];
    this.lastErase = c;
    const cells: number[] = [];
    for (const p of path) {
      const t = this.eraseTargets(p % N, (p / N) | 0);
      cells.push(...t.fence, ...t.bridge);
    }
    if (w.erase(cells)) this.say("Removed.");
    this.hover(e);
  }

  private hover(e: PointerEvent) {
    const p = this.at(e), w = this.getWorld();
    if (!p) return;
    const [x, y] = p;
    if (this.tool === "bridge") {
      const cells = w.bridgeCellsAt(x, y);
      this.renderer.ghost = { kind: "bridge", cells, x, y };
      if (cells) this.preview({ bridge: cells }, `b${cells[0]}`);
      else this.previewer.clear();
    } else if (this.tool === "erase") {
      const t = this.eraseTargets(Math.floor(x), Math.floor(y));
      this.renderer.ghost = { kind: "erase", x, y, cells: [...t.fence, ...t.bridge] };
      if (t.fence.length || t.bridge.length) this.preview({ open: t.fence, closeBridge: t.bridge }, `e${t.fence.join(",")}|${t.bridge[0] ?? ""}`);
      else this.previewer.clear();
    } else if (this.tool === "fence") {
      const c = idx(Math.floor(x), Math.floor(y));
      this.renderer.ghost = { kind: "fence", cells: [c], ok: [w.canFence(c)] };
      this.previewer.clear();
    } else { this.renderer.ghost = null; this.previewer.clear(); }
  }

  private preview(change: WhatIf, key: string) {
    this.previewer.request(change, key);
  }
}

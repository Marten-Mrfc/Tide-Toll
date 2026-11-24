// Pointer input for the tools. Pointer events cover mouse, pen and touch, and
// every event maps through the canvas rectangle as it is at that moment.
// Each tool shows a ghost and asks for a flow preview before it commits:
// - Fence: drag; the preview runs during the drag, placed on release.
// - Gravel: brush a strip by dragging (it snaps to a trail's centreline within
//   one cell); placed on release.
// - Footbridge and Gate: hover (or press, on touch) previews; placed on release
//   over the same spot, so sliding away cancels. The Gate tool on a gate removes it.
// - Erase: hover (or press, on touch) previews what goes; click or drag removes.
import { World } from "../sim/world";
import { N, idx, inside, line4 } from "../sim/grid";
import { WhatIf } from "../sim/preview";
import { Renderer } from "./render";
import { FlowPreviewer } from "./previewer";

export type Tool = "none" | "fence" | "bridge" | "gravel" | "gate" | "erase";

/** What the Erase tool would remove round a cell. */
interface EraseTargets { fence: number[]; bridge: number[]; gravel: number[]; }

export class Input {
  tool: Tool = "none";
  /** Pointer position over the map in CSS px relative to the canvas, or null when away. */
  pointer: [number, number] | null = null;
  /** Whether the last pointer was a finger (the floating readout is then not shown). */
  touch = false;
  private dragStart: [number, number] | null = null;
  private brush: number[] | null = null; // gravel cells brushed so far in this stroke
  private lastBrush = -1;
  private erasing = false;
  private eraseDown: [number, number] | null = null;
  private lastErase: [number, number] | null = null;
  private pressAt = -1; // footbridge or gate: the spot the press is previewing
  private pressed = false;

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
    canvas.addEventListener("pointerleave", () => { if (!this.busy) this.cancel(); });
  }

  /** Whether a press or drag is in progress. */
  get busy(): boolean { return !!this.dragStart || !!this.brush || this.erasing || !!this.eraseDown || this.pressed; }

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
    this.touch = e.pointerType === "touch";
    return [(x / w) * N, (y / h) * N];
  }

  /** The cell under the pointer, clamped to the grid. */
  private cell(e: PointerEvent): [number, number] | null {
    const p = this.at(e);
    if (!p) return null;
    return [clampCell(p[0]), clampCell(p[1])];
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
    } else if (this.tool === "gravel") {
      this.brush = [];
      this.lastBrush = -1;
      this.brushTo(c);
    } else if (this.tool === "bridge" || this.tool === "gate") {
      this.pressed = true;
      this.hover(e);
      if (this.pressAt < 0) this.say(this.tool === "bridge" ? "Footbridges go across open water, away from the ford." : "Gates go on fences.");
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
    else if (this.tool === "gravel" && this.brush) { const c = this.cell(e); if (c) this.brushTo(c); }
    else if (this.tool === "erase" && this.eraseDown && !this.erasing) {
      const c = this.cell(e);
      if (c && Math.max(Math.abs(c[0] - this.eraseDown[0]), Math.abs(c[1] - this.eraseDown[1])) > 1) {
        this.erasing = true; this.lastErase = this.eraseDown; this.eraseAt(e);
      } else this.hover(e); // a finger resting within a cell still moves the ghost
    } else if (this.tool === "erase" && this.erasing) this.eraseAt(e);
    else this.hover(e);
  }

  private up(e: PointerEvent) {
    const w = this.getWorld();
    if (this.tool === "fence" && this.dragStart) {
      const c = this.cell(e);
      const n = c ? w.placeFence(this.fenceCells(c)) : 0;
      this.say(n ? `Fence placed (${n} cells).` : "Nothing to fence there.");
      this.dragStart = null;
      this.previewer.clear();
      this.hover(e);
    } else if (this.tool === "gravel" && this.brush) {
      const n = w.placeGravel(this.brush);
      this.say(n ? `Gravel laid (${n} cells).` : "Nothing to gravel there.");
      this.brush = null;
      this.previewer.clear();
      this.hover(e);
    } else if ((this.tool === "bridge" || this.tool === "gate") && this.pressed) {
      const before = this.pressAt;
      this.pressed = false;
      this.hover(e);
      // Placed only if released over the same spot; sliding away cancels.
      if (before >= 0 && this.pressAt === before) {
        const p = this.at(e)!;
        if (this.tool === "bridge") {
          const b = w.placeBridge(p[0], p[1]);
          this.say(b ? `Footbridge built (${b.cells.length} cells).` : "No room for a footbridge there.");
        } else if (w.gate[before]) this.say(w.removeGate(before) ? "Gate removed." : "");
        else this.say(w.placeGate(before) ? "Gate placed." : "Gates go on fences.");
      } else if (before >= 0) this.say(this.tool === "bridge" ? "Footbridge cancelled." : "Gate cancelled.");
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

  /** Puts the current press or drag down without placing anything. */
  cancel() {
    this.dragStart = null;
    this.brush = null;
    this.erasing = false;
    this.eraseDown = null;
    this.pressed = false;
    this.pressAt = -1;
    this.pointer = null;
    this.renderer.ghost = null;
    this.previewer.clear();
  }

  // ------------------------------------------------------------ fence

  private fenceCells(end: [number, number]): number[] {
    const [x0, y0] = this.dragStart!;
    return line4(x0, y0, end[0], end[1]);
  }

  private fenceGhost(end: [number, number]) {
    const w = this.getWorld();
    const cells = this.fenceCells(end), ok = cells.map(c => w.canFence(c) && !w.fence[c]);
    this.renderer.ghost = { kind: "fence", cells, ok };
    const block = cells.filter((_, i) => ok[i]);
    if (block.length) this.previewer.request({ block }, `f${block.join(",")}`);
    else this.previewer.clear();
  }

  // ------------------------------------------------------------ gravel

  /** Extends the gravel stroke to cell c (snapped to a trail centreline), filling gaps with a 4-connected line. */
  private brushTo(c: [number, number]) {
    const w = this.getWorld(), brush = this.brush!;
    const s = w.gravelSnap(idx(c[0], c[1]));
    if (s === this.lastBrush) return;
    const path = this.lastBrush < 0 ? [s] : line4(this.lastBrush % N, (this.lastBrush / N) | 0, s % N, (s / N) | 0);
    this.lastBrush = s;
    for (const v of path) if (w.canGravel(v) && !w.gravel[v] && !brush.includes(v)) brush.push(v);
    this.renderer.ghost = { kind: "gravel", cells: brush.slice(), ok: true };
    if (brush.length) this.previewer.request({ gravel: brush.slice() }, `g${brush.join(",")}`);
    else this.previewer.clear();
  }

  // ------------------------------------------------------------ erase

  /** Fence and gravel cells in the 3x3 round a cell, and every cell of any bridge touched. */
  private eraseTargets(x: number, y: number): EraseTargets {
    const w = this.getWorld(), t: EraseTargets = { fence: [], bridge: [], gravel: [] };
    for (let dy = -1; dy <= 1; dy++)
      for (let dx = -1; dx <= 1; dx++) {
        if (!inside(x + dx, y + dy)) continue;
        const c = idx(x + dx, y + dy);
        if (w.fence[c]) t.fence.push(c);
        if (w.gravel[c]) t.gravel.push(c);
        const b = w.bridgeAt[c];
        if (b >= 0 && !t.bridge.includes(c)) for (const bc of w.bridges.find(q => q.id === b)!.cells) if (!t.bridge.includes(bc)) t.bridge.push(bc);
      }
    return t;
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
      cells.push(...t.fence, ...t.bridge, ...t.gravel);
    }
    if (w.erase(cells)) {
      this.say("Removed.");
      this.previewer.clear(); // what it showed is gone
    }
    this.hover(e);
  }

  // ------------------------------------------------------------ hover

  private hover(e: PointerEvent) {
    const p = this.at(e), w = this.getWorld();
    if (!p) return;
    const [x, y] = p, cx = clampCell(x), cy = clampCell(y);
    if (this.tool === "bridge") {
      const cells = w.bridgeCellsAt(x, y);
      this.pressAt = cells ? cells[0] : -1;
      this.renderer.ghost = { kind: "bridge", cells, x, y };
      if (cells) this.previewer.request({ bridge: cells }, `b${cells[0]}`);
      else this.previewer.clear();
    } else if (this.tool === "gate") {
      const c = w.gateCellAt(x, y);
      this.pressAt = c;
      const remove = c >= 0 && w.gate[c] === 1;
      this.renderer.ghost = { kind: "gate", cell: c, remove, x, y };
      if (c >= 0) this.previewer.request(remove ? { ungate: [c] } : { gate: [c] }, `${remove ? "u" : "t"}${c}`);
      else this.previewer.clear();
    } else if (this.tool === "erase") {
      const t = this.eraseTargets(cx, cy);
      this.renderer.ghost = { kind: "erase", x, y, fence: t.fence, bridge: t.bridge, gravel: t.gravel };
      if (t.fence.length || t.bridge.length || t.gravel.length) {
        const change: WhatIf = { open: t.fence, closeBridge: t.bridge, ungravel: t.gravel };
        this.previewer.request(change, `e${t.fence.join(",")}|${t.bridge[0] ?? ""}|${t.gravel.join(",")}`);
      } else this.previewer.clear();
    } else if (this.tool === "fence") {
      const c = idx(cx, cy);
      this.renderer.ghost = { kind: "fence", cells: [c], ok: [w.canFence(c) && !w.fence[c]] };
      this.previewer.clear();
    } else if (this.tool === "gravel") {
      const c = w.gravelSnap(idx(cx, cy));
      this.renderer.ghost = { kind: "gravel", cells: [c], ok: w.canGravel(c) };
      this.previewer.clear();
    } else { this.renderer.ghost = null; this.previewer.clear(); }
  }
}

function clampCell(v: number): number {
  return Math.max(0, Math.min(N - 1, Math.floor(v)));
}

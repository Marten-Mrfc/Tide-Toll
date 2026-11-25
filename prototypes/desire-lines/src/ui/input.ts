// Pointer input for the tools. Pointer events cover mouse, pen and touch, and
// every event maps through the canvas rectangle as it is at that moment.
// Each tool shows a ghost and asks for a flow preview before it commits:
// - Fence: drag; the preview runs during the drag, placed on release.
// - Gravel: brush a strip by dragging (it snaps to a trail's centreline within
//   one cell); placed on release.
// - Footbridge and Gate: hover (or press, on touch) previews; placed on release
//   over the same spot, so sliding away cancels. The Gate tool on a gate removes it.
// - Erase: hover (or press, on touch) previews what goes; click or drag removes.
// Each press or drag belongs to the pointer that started it (its pointerId):
// a second finger landing during a fence drag, a gravel stroke or a press is
// ignored, and so are its moves, its release and its cancel.
import { World } from "../sim/world";
import { N, idx, inside, line4 } from "../sim/grid";
import { WhatIf } from "../sim/preview";
import { Renderer } from "./render";
import { FlowPreviewer } from "./previewer";

export type Tool = "none" | "fence" | "bridge" | "gravel" | "gate" | "erase";

/** What the player's action did, for sound and its visual twin on the map: placed, removed, or refused (with the cell). */
export type Cue = "place" | "erase" | "refuse";

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;

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
  private erased = false; // the current erase gesture removed something
  private eraseDown: [number, number] | null = null;
  private lastErase: [number, number] | null = null;
  private pressAt = -1; // footbridge or gate: the spot the press is previewing
  private pressed = false;
  private gateReason = "Gates go on fences."; // why the Gate tool cannot place one at the pointer
  /** The pointer that owns the current press or drag (-1: none). Other pointers are ignored until it lifts. */
  private owner = -1;
  private lastKey = "";
  /** Called when an action places, removes or is refused, with the cell (fractional cell units). */
  onCue: (cue: Cue, x: number, y: number) => void = () => {};

  constructor(
    private canvas: HTMLCanvasElement,
    private getWorld: () => World,
    private renderer: Renderer,
    private previewer: FlowPreviewer,
    private say: (msg: string) => void,
  ) {
    canvas.addEventListener("pointerdown", e => this.down(e));
    canvas.addEventListener("pointermove", e => { if (this.mine(e)) this.move(e); });
    canvas.addEventListener("pointerup", e => { if (this.mine(e)) { this.up(e); this.owner = -1; } });
    canvas.addEventListener("pointercancel", e => { if (this.mine(e)) this.cancel(); });
    canvas.addEventListener("pointerleave", e => { if (this.mine(e) && !this.busy) this.cancel(); });
  }

  /** Whether an event belongs to the pointer that owns the gesture (any pointer while none does). */
  private mine(e: PointerEvent): boolean { return this.owner < 0 || e.pointerId === this.owner; }

  /** Asks for a flow preview; a new one clears the last action's status message, which described something else. */
  private request(change: WhatIf, key: string) {
    if (key !== this.lastKey) { this.lastKey = key; this.say(""); }
    this.previewer.request(change, key);
  }

  private clearPreview() { this.lastKey = ""; this.previewer.clear(); }

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
    if (this.owner >= 0 && e.pointerId !== this.owner) { e.preventDefault(); return; } // a second finger
    if (this.tool === "none" || (e.pointerType === "mouse" && e.button !== 0)) return;
    const c = this.cell(e);
    if (!c) return;
    e.preventDefault();
    this.owner = e.pointerId;
    try { this.canvas.setPointerCapture(e.pointerId); } catch { /* the pointer is already gone */ }
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
      if (this.pressAt < 0) { this.say(this.tool === "bridge" ? "Footbridges go across open water, away from the ford." : this.gateReason); this.cueAt("refuse", e); }
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
      this.dragStart = null;
      this.clearPreview();
      this.hover(e);
      this.say(n ? `Fence placed (${plural(n, "cell")}).` : "Nothing to fence there.");
      this.cueAt(n ? "place" : "refuse", e);
    } else if (this.tool === "gravel" && this.brush) {
      const n = w.placeGravel(this.brush);
      this.brush = null;
      this.clearPreview();
      this.hover(e);
      this.say(n ? `Gravel laid (${plural(n, "cell")}).` : "Nothing to gravel there.");
      this.cueAt(n ? "place" : "refuse", e);
    } else if ((this.tool === "bridge" || this.tool === "gate") && this.pressed) {
      const before = this.pressAt;
      this.pressed = false;
      this.hover(e);
      // Placed only if released over the same spot; sliding away cancels.
      let msg = "", cue: Cue | null = null;
      if (before >= 0 && this.pressAt === before) {
        const p = this.at(e)!;
        if (this.tool === "bridge") {
          const b = w.placeBridge(p[0], p[1]);
          msg = b ? `Footbridge built (${plural(b.cells.length, "cell")}).` : "No room for a footbridge there.";
          cue = b ? "place" : "refuse";
        } else if (w.gate[before]) { const ok = w.removeGate(before); msg = ok ? "Gate removed." : ""; cue = ok ? "erase" : null; }
        else { const ok = w.placeGate(before); msg = ok ? "Gate placed." : this.gateReason; cue = ok ? "place" : "refuse"; }
      } else if (before >= 0) msg = this.tool === "bridge" ? "Footbridge cancelled." : "Gate cancelled.";
      this.clearPreview();
      this.hover(e);
      if (msg) this.say(msg);
      if (cue) this.cueAt(cue, e);
    } else if (this.tool === "erase" && this.eraseDown) {
      if (!this.erasing) this.eraseAt(e); // a tap on touch
      this.clearPreview();
      this.hover(e);
      if (this.erased) this.say("Removed.");
    }
    this.erased = false;
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
    this.owner = -1;
    this.erased = false;
    this.renderer.ghost = null;
    this.clearPreview();
  }

  /** Reports a cue at the pointer's cell. */
  private cueAt(cue: Cue, e: PointerEvent) {
    const p = this.at(e);
    if (p) this.onCue(cue, p[0], p[1]);
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
    if (block.length) this.request({ block }, `f${block.join(",")}`);
    else this.clearPreview();
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
    if (brush.length) this.request({ gravel: brush.slice() }, `g${brush.join(",")}`);
    else this.clearPreview();
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
      this.erased = true;
      this.clearPreview(); // what it showed is gone
      this.say("Removed.");
      this.cueAt("erase", e);
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
      if (cells) this.request({ bridge: cells }, `b${cells[0]}`);
      else this.clearPreview();
    } else if (this.tool === "gate") {
      // The nearest fence cell within 2 that takes a gate (a stair corner takes a two-cell gate), or why not.
      const t = w.gateTarget(x, y), c = t.cells.length ? t.cells[0] : -1, remove = t.remove;
      this.pressAt = c;
      this.gateReason = t.reason || "Gates go on fences.";
      this.renderer.ghost = { kind: "gate", cells: t.cells, remove, x, y };
      if (c >= 0) this.request(remove ? { ungate: t.cells } : { gate: t.cells }, `${remove ? "u" : "t"}${t.cells.join(",")}`);
      else this.clearPreview();
    } else if (this.tool === "erase") {
      const t = this.eraseTargets(cx, cy);
      this.renderer.ghost = { kind: "erase", x, y, fence: t.fence, bridge: t.bridge, gravel: t.gravel };
      if (t.fence.length || t.bridge.length || t.gravel.length) {
        const change: WhatIf = { open: t.fence, closeBridge: t.bridge, ungravel: t.gravel };
        this.request(change, `e${t.fence.join(",")}|${t.bridge[0] ?? ""}|${t.gravel.join(",")}`);
      } else this.clearPreview();
    } else if (this.tool === "fence") {
      const c = idx(cx, cy);
      this.renderer.ghost = { kind: "fence", cells: [c], ok: [w.canFence(c) && !w.fence[c]] };
      this.clearPreview();
    } else if (this.tool === "gravel") {
      const c = w.gravelSnap(idx(cx, cy));
      this.renderer.ghost = { kind: "gravel", cells: [c], ok: w.canGravel(c) };
      this.clearPreview();
    } else { this.renderer.ghost = null; this.clearPreview(); }
  }
}

function clampCell(v: number): number {
  return Math.max(0, Math.min(N - 1, Math.floor(v)));
}

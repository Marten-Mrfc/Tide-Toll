// Where a gate can go. A fence drawn at an angle is a 4-connected staircase,
// so most of its cells have fence on two adjacent sides and open ground only
// on the other two (both on the same side of the fence) or across a corner
// that walkers may not cut. A gate in such a cell would lead nowhere. The rule
// here asks the real question instead: does opening these cells join the open
// ground on one side of the fence to the open ground on the other, under the
// walkers' own step rule (8 neighbours, no cutting a blocked corner)? It looks
// only at the cells round the gate, so the two sides count as separate even
// where they meet again round the fence's far end.
//
// A single cell is tried first (any straight fence, and the outer corners of
// a staircase); otherwise the cell and one fence neighbour (a stair's corner
// pair) become one gate of two cells, each with half the delay.
import { N, idx, inside, DX, DY } from "./grid";

/** What a gate needs from the world: blocking as walkers see it, and the fence and gate flags. */
export interface GateWorld {
  fence: Uint8Array;
  gate: Uint8Array;
  blocked(c: number): boolean;
}

/** What the Gate tool would do at a point: the cells, whether it removes a gate, or why it cannot. */
export interface GateTarget {
  cells: number[]; // empty when no gate can go here
  remove: boolean; // the cells are an existing gate (the Gate tool takes it out)
  reason: string; // why not, when cells is empty
}

export const GATE_REASONS = {
  noFence: "Gates go on fences: there is no fence here.",
  lonePost: "A gate needs fence beside it: this is a lone post.",
  noWay: "No way through here: both sides are blocked.",
} as const;

/** Whether opening the cells `g` joins two separate pieces of open ground next to them. */
export function gatePassage(w: GateWorld, g: number[]): boolean {
  let x0 = N, y0 = N, x1 = -1, y1 = -1;
  for (const c of g) {
    const x = c % N, y = (c / N) | 0;
    x0 = Math.min(x0, x - 1); y0 = Math.min(y0, y - 1); x1 = Math.max(x1, x + 1); y1 = Math.max(y1, y + 1);
  }
  const inG = (c: number) => g.includes(c);
  const inWin = (x: number, y: number) => x >= x0 && x <= x1 && y >= y0 && y <= y1 && inside(x, y);
  // Open as the walkers see it, with or without the gate cells opened.
  const open = (x: number, y: number, withG: boolean) => inside(x, y) && (inG(idx(x, y)) ? withG : !w.blocked(idx(x, y)));
  const step = (ux: number, uy: number, k: number, withG: boolean) => {
    const vx = ux + DX[k], vy = uy + DY[k];
    if (!open(vx, vy, withG)) return false;
    return k < 4 || (open(vx, uy, withG) && open(ux, vy, withG));
  };
  // Label the open ground in the window, gate cells shut.
  const W = x1 - x0 + 1, label = new Int8Array(W * (y1 - y0 + 1)).fill(-1);
  const at = (x: number, y: number) => (y - y0) * W + (x - x0);
  let next = 0;
  for (let y = y0; y <= y1; y++)
    for (let x = x0; x <= x1; x++) {
      if (!inWin(x, y) || !open(x, y, false) || label[at(x, y)] >= 0) continue;
      const stack = [x, y];
      label[at(x, y)] = next;
      while (stack.length) {
        const uy = stack.pop()!, ux = stack.pop()!;
        for (let k = 0; k < 8; k++) {
          const vx = ux + DX[k], vy = uy + DY[k];
          if (!inWin(vx, vy) || label[at(vx, vy)] >= 0 || !step(ux, uy, k, false)) continue;
          label[at(vx, vy)] = next;
          stack.push(vx, vy);
        }
      }
      next++;
    }
  // The gate cells form one opening (a pair is 4-adjacent); which pieces does it reach?
  let seen = -1;
  for (const c of g) {
    const ux = c % N, uy = (c / N) | 0;
    for (let k = 0; k < 8; k++) {
      const vx = ux + DX[k], vy = uy + DY[k];
      if (!inWin(vx, vy) || inG(idx(vx, vy)) || !step(ux, uy, k, true)) continue;
      const l = label[at(vx, vy)];
      if (l < 0) continue;
      if (seen < 0) seen = l;
      else if (l !== seen) return true;
    }
  }
  return false;
}

/** Whether any of the 8 neighbours of the cells `g` (not in g) is fence. */
function hasRail(w: GateWorld, g: number[]): boolean {
  for (const c of g) {
    const x = c % N, y = (c / N) | 0;
    for (let k = 0; k < 8; k++) {
      const vx = x + DX[k], vy = y + DY[k];
      if (inside(vx, vy) && w.fence[idx(vx, vy)] && !g.includes(idx(vx, vy))) return true;
    }
  }
  return false;
}

/** The gate an existing gate cell belongs to: it and any 4-adjacent gate cell. */
export function gateGroup(w: GateWorld, c: number): number[] {
  const out = [c], x = c % N, y = (c / N) | 0;
  for (let k = 0; k < 4; k++) {
    const vx = x + DX[k], vy = y + DY[k];
    if (inside(vx, vy) && w.gate[idx(vx, vy)]) out.push(idx(vx, vy));
  }
  return out;
}

/**
 * The cells a gate at fence cell c would take: [c] if a gate there opens a way
 * through, else [c, n] for the first fence neighbour n that makes a two-cell
 * gate with it, else null. Lone posts (no fence round them) never take a gate.
 */
export function gateCellsFor(w: GateWorld, c: number): number[] | null {
  if (!w.fence[c] || w.gate[c]) return null;
  if (!hasRail(w, [c])) return null;
  if (gatePassage(w, [c])) return [c];
  const x = c % N, y = (c / N) | 0;
  for (let k = 0; k < 4; k++) {
    const vx = x + DX[k], vy = y + DY[k];
    if (!inside(vx, vy)) continue;
    const n = idx(vx, vy);
    if (!w.fence[n] || w.gate[n]) continue;
    const pair = [c, n];
    if (hasRail(w, pair) && gatePassage(w, pair)) return pair;
  }
  return null;
}

/**
 * What a Gate click at (x, y) (cell units) does: the nearest fence cell within
 * 2 cells (by distance from the point to the cell centre) that is a gate (to
 * remove) or can take one (to place). Otherwise the reason it cannot.
 */
export function gateTarget(w: GateWorld, x: number, y: number): GateTarget {
  const cx = Math.floor(x), cy = Math.floor(y);
  const near: [number, number][] = [];
  for (let dy = -2; dy <= 2; dy++)
    for (let dx = -2; dx <= 2; dx++) {
      if (!inside(cx + dx, cy + dy)) continue;
      const c = idx(cx + dx, cy + dy);
      if (w.fence[c]) near.push([c, (cx + dx + 0.5 - x) ** 2 + (cy + dy + 0.5 - y) ** 2]);
    }
  if (!near.length) return { cells: [], remove: false, reason: GATE_REASONS.noFence };
  near.sort((a, b) => a[1] - b[1] || a[0] - b[0]);
  let rail = false;
  for (const [c] of near) {
    if (w.gate[c]) return { cells: gateGroup(w, c), remove: true, reason: "" };
    const cells = gateCellsFor(w, c);
    if (cells) return { cells, remove: false, reason: "" };
    if (hasRail(w, [c])) rail = true;
  }
  return { cells: [], remove: false, reason: rail ? GATE_REASONS.noWay : GATE_REASONS.lonePost };
}

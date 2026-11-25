// House growth by the frontage rules (spec "Where houses appear", "Growth rate
// and density", "Frontage"), cut down for the toy: there is no economy, so a
// Hamlet household arrives at the spec's capped rate (one per two days) while
// the town is below the Hamlet target, and takes the best valid plot.
//
// A plot is a 3 x 3 cottage with its garden (gardens block walkers, as in the
// spec), its door in the middle of the side that faces its frontage cell. It
// is valid when:
//  1. its door faces a cell at wear 50 or more (a track): the frontage;
//  2. none of its 9 cells is at wear 25 or more (it never sits on a trail);
//  3. it is on dry land: no water, ford, bridge, field, fence, gravel, building or door,
//     and keeps a cell clear of the water's edge and 2 cells from the map's edge;
//     (the flood band and pastures do not exist in the toy; slope neither)
//  4. it keeps a one-cell gap from every other building (Hamlet rule);
//  5. it blocks no way through: every building door that walkers can reach
//     now stays reachable (checked by a flood fill on the plot's best candidates).
// Score: 3 x frontage wear / 100, plus access (the mean over the well, field and
// market of 1 - route cost / 100, floored at 0), plus 1 per house within 6 cells
// (up to 3); minus 1 if the frontage is mud now, minus the crowding there (walkers
// per cell, up to 1). Mill noise does not exist in the toy. Ties go to the
// first plot in scan order, so growth is deterministic.
import { PARAMS } from "./params";
import { N, NC, idx, inside, DX, DY } from "./grid";
import type { World } from "./world";

export interface Plot {
  x: number; y: number; // footprint top-left cell
  door: number;
  front: number; // the frontage cell the door faces
  fronted: number[]; // the frontage cells along the plot's front (wear 50 or more)
  score: number;
}

/** A house added by growth, for the page to draw it appearing. */
export interface GrownHouse {
  building: number; // building index
  x: number; y: number; w: number; h: number;
  door: number;
  front: number;
  time: number; // sim seconds
  day: number; // in-game day (1-based) it was built
}

// Orientations: the house lies in direction k (0 +x, 1 -x, 2 +y, 3 -y) from the frontage cell.
// Top-left corner of the 3 x 3 relative to the frontage cell, and the door relative to it.
const ORIENT: [number, number][] = [[1, -1], [-3, -1], [-1, 1], [-1, -3]];

/** Every valid plot by rules 1 to 4, best first (rule 5 is checked by the caller on the best ones). */
export function candidatePlots(w: World): Plot[] {
  const P = PARAMS, m = w.map, out: Plot[] = [];
  const isDoor = new Uint8Array(NC);
  for (const b of w.buildings) isDoor[b.door] = 1;
  const free = (c: number) => w.land[c] === 1 && !m.crop[c] && !w.fence[c] && !w.gravel[c] && w.bridgeAt[c] < 0 && !isDoor[c] && w.wear[c] < P.plotClearWear;
  for (let f = 0; f < NC; f++) {
    if (w.wear[f] < P.frontageWear || !w.land[f] || isDoor[f]) continue;
    const fx = f % N, fy = (f / N) | 0;
    for (let k = 0; k < 4; k++) {
      const x0 = fx + ORIENT[k][0], y0 = fy + ORIENT[k][1];
      if (x0 < 3 || y0 < 3 || x0 + 3 > N - 3 || y0 + 3 > N - 3) continue;
      let ok = true;
      for (let y = y0 - 1; y <= y0 + 3 && ok; y++)
        for (let x = x0 - 1; x <= x0 + 3 && ok; x++) {
          const c = idx(x, y);
          const ring = x < x0 || y < y0 || x > x0 + 2 || y > y0 + 2;
          // The ring keeps clear of water and other buildings; the footprint must be free ground.
          if (ring) ok = !m.water[c] && !m.footprint[c] && !isDoor[c];
          else ok = free(c);
        }
      if (!ok) continue;
      const door = idx(fx + DX[k], fy + DY[k]);
      // The plot's front: the frontage cell and its two neighbours along the side, where worn to track.
      const fronted: number[] = [];
      for (const s of [-1, 0, 1]) {
        const ax = fx + (k < 2 ? 0 : s), ay = fy + (k < 2 ? s : 0);
        if (inside(ax, ay) && w.land[idx(ax, ay)] && w.wear[idx(ax, ay)] >= P.frontageWear) fronted.push(idx(ax, ay));
      }
      out.push({ x: x0, y: y0, door, front: f, fronted, score: plotScore(w, x0, y0, door, f) });
    }
  }
  // Best first; ties keep scan order (Array.prototype.sort is stable).
  return out.sort((a, b) => b.score - a.score);
}

function plotScore(w: World, x0: number, y0: number, door: number, front: number): number {
  const P = PARAMS;
  let s = (3 * w.wear[front]) / 100;
  let acc = 0;
  for (const kind of ["well", "field", "market"] as const) {
    let best = Infinity;
    for (const b of w.kind(kind)) best = Math.min(best, w.fields[b][door]);
    acc += best < Infinity ? Math.max(0, 1 - best / 100) : 0;
  }
  s += acc / 3;
  let near = 0;
  const cx = x0 + 1.5, cy = y0 + 1.5;
  for (const h of w.houses) {
    const b = w.buildings[h];
    if (Math.hypot(b.x + b.w / 2 - cx, b.y + b.h / 2 - cy) <= P.plotNeighbourRadius) near++;
  }
  s += Math.min(3, near);
  if (w.mud[front] && w.weather.wetness > 0) s -= 1;
  s -= Math.min(1, w.occ[front]);
  return s;
}

/**
 * Whether placing a footprint (the plot minus its door) keeps every door that
 * is reachable now reachable: a flood fill from the first reachable door with
 * and without the plot.
 */
export function keepsWaysOpen(w: World, p: Plot, before: Uint8Array = reachable(w, null)): boolean {
  const plot = new Uint8Array(NC);
  for (let y = p.y; y < p.y + 3; y++) for (let x = p.x; x < p.x + 3; x++) plot[idx(x, y)] = 1;
  plot[p.door] = 0;
  const after = reachable(w, plot);
  // The start door is the same both times (the first one reachable before), so compare door by door.
  for (const b of w.buildings) if (before[b.door] && !after[b.door]) return false;
  return after[p.door] === 1;
}

/** Open ground reachable from the first open building door (walkers' step rule), with `extra` cells shut. */
function reachable(w: World, extra: Uint8Array | null): Uint8Array {
    const doors = w.buildings.map(b => b.door);
    const seen = new Uint8Array(NC);
    const open = (c: number) => !w.blocked(c) && !(extra && extra[c]);
    const start = doors.find(d => open(d));
    if (start === undefined) return seen;
    const stack = [start];
    seen[start] = 1;
    while (stack.length) {
      const u = stack.pop()!, ux = u % N, uy = (u / N) | 0;
      for (let k = 0; k < 8; k++) {
        const vx = ux + DX[k], vy = uy + DY[k];
        if (!inside(vx, vy)) continue;
        const v = idx(vx, vy);
        if (seen[v] || !open(v)) continue;
        if (k >= 4 && (!open(idx(vx, uy)) || !open(idx(ux, vy)))) continue;
        seen[v] = 1;
        stack.push(v);
      }
    }
    return seen;
}

/** The plot the next household takes, or null if none is valid. Checks rule 5 on the best few. */
export function choosePlot(w: World, tries = 8): Plot | null {
  const all = candidatePlots(w);
  if (!all.length) return null;
  const before = reachable(w, null);
  for (let i = 0; i < all.length && i < tries; i++) if (keepsWaysOpen(w, all[i], before)) return all[i];
  return null;
}

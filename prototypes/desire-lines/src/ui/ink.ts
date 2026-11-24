// Vector ink for the things the player places: fences (continuous post-and-rail
// lines), gates, gravel and footbridges. Shared by the static layer and the
// tool ghosts, so a ghost looks like what will be built.
import { N, NC } from "../sim/grid";
import { hash2 } from "../sim/rng";

/** Scale and theme for drawing: device px per cell, device px per CSS px, and colours. */
export interface Pen {
  s: number;
  dpr: number;
  halo: string; // a light (or dark) underlay that makes ink read on any ground
  ink: string;
}

/**
 * Turns fence cells into straight polylines through the cell centres. Cells
 * join their 4-neighbours, and their diagonal neighbours when no shared
 * 4-neighbour is fence (so a diagonal leftover after erasing still joins).
 * Chains are walked between ends and junctions, then simplified, so a
 * staircase of cells draws as one straight rail. `skip` cells (gates) are
 * left out, which breaks the rail there.
 */
export function fenceLines(cells: number[], skip?: Uint8Array): number[][] {
  const set = new Set(skip ? cells.filter(c => !skip[c]) : cells), seen = new Set<number>(), lines: number[][] = [];
  const nb = (c: number) => {
    const x = c % N, y = (c / N) | 0, out: number[] = [];
    const has = (dx: number, dy: number) => x + dx >= 0 && x + dx < N && y + dy >= 0 && y + dy < N && set.has(c + dy * N + dx);
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) if (has(dx, dy)) out.push(c + dy * N + dx);
    for (const [dx, dy] of [[1, 1], [1, -1], [-1, 1], [-1, -1]])
      if (has(dx, dy) && !has(dx, 0) && !has(0, dy)) out.push(c + dy * N + dx);
    return out;
  };
  const edge = (a: number, b: number) => Math.min(a, b) * NC + Math.max(a, b);
  const walk = (start: number, next: number) => {
    const chain = [start];
    let prev = start, cur = next;
    seen.add(edge(prev, cur));
    for (;;) {
      chain.push(cur);
      const ns = nb(cur);
      if (ns.length !== 2) break;
      const nx = ns[0] === prev ? ns[1] : ns[0];
      if (seen.has(edge(cur, nx))) break;
      seen.add(edge(cur, nx));
      prev = cur; cur = nx;
    }
    lines.push(simplify(chain.flatMap(c => [(c % N) + 0.5, ((c / N) | 0) + 0.5]), 0.75));
  };
  for (const pass of [0, 1])
    for (const c of set) {
      const ns = nb(c);
      if (ns.length === 0 && pass === 0) { lines.push([(c % N) + 0.5, ((c / N) | 0) + 0.5]); continue; }
      if (pass === 0 && ns.length === 2) continue; // chain interiors are reached from their ends
      for (const n of ns) if (!seen.has(edge(c, n))) walk(c, n);
    }
  return lines;
}

/** Douglas-Peucker on a flat x,y list. */
function simplify(pts: number[], tol: number): number[] {
  const n = pts.length / 2;
  if (n <= 2) return pts;
  const keep = new Uint8Array(n);
  keep[0] = keep[n - 1] = 1;
  const stack: [number, number][] = [[0, n - 1]];
  while (stack.length) {
    const [a, b] = stack.pop()!;
    const ax = pts[2 * a], ay = pts[2 * a + 1], bx = pts[2 * b], by = pts[2 * b + 1];
    const len = Math.hypot(bx - ax, by - ay) || 1;
    let worst = -1, wd = tol;
    for (let i = a + 1; i < b; i++) {
      const dd = Math.abs((bx - ax) * (ay - pts[2 * i + 1]) - (ax - pts[2 * i]) * (by - ay)) / len;
      if (dd > wd) { wd = dd; worst = i; }
    }
    if (worst >= 0) { keep[worst] = 1; stack.push([a, worst], [worst, b]); }
  }
  const out: number[] = [];
  for (let i = 0; i < n; i++) if (keep[i]) out.push(pts[2 * i], pts[2 * i + 1]);
  return out;
}

/**
 * Fences as continuous post-and-rail lines: a rail over a halo, with posts at
 * the ends and at corners only. A lone cell is a single post.
 */
export function drawFence(g: CanvasRenderingContext2D, pen: Pen, cells: number[], colour: string, skip?: Uint8Array) {
  if (!cells.length) return;
  const { s, dpr: d } = pen;
  const lines = fenceLines(cells, skip);
  const rail = Math.max(2.5 * d, s * 0.42), post = Math.max(4 * d, s * 0.62);
  const path = new Path2D(), posts: [number, number][] = [];
  for (const ln of lines) {
    for (let i = 0; i < ln.length; i += 2) {
      if (i === 0) path.moveTo(ln[0] * s, ln[1] * s); else path.lineTo(ln[i] * s, ln[i + 1] * s);
    }
    const n = ln.length / 2;
    posts.push([ln[0], ln[1]]);
    if (n > 1) posts.push([ln[ln.length - 2], ln[ln.length - 1]]);
    // A corner: the line turns by more than about 25 degrees.
    for (let i = 1; i < n - 1; i++) {
      const ax = ln[2 * i] - ln[2 * i - 2], ay = ln[2 * i + 1] - ln[2 * i - 1];
      const bx = ln[2 * i + 2] - ln[2 * i], by = ln[2 * i + 3] - ln[2 * i + 1];
      const cos = (ax * bx + ay * by) / ((Math.hypot(ax, ay) * Math.hypot(bx, by)) || 1);
      if (cos < 0.9) posts.push([ln[2 * i], ln[2 * i + 1]]);
    }
  }
  g.save();
  g.lineJoin = "round"; g.lineCap = "butt";
  g.strokeStyle = pen.halo;
  g.globalAlpha = 0.75;
  g.lineWidth = rail + 3 * d;
  g.stroke(path);
  g.globalAlpha = 1;
  g.strokeStyle = colour;
  g.lineWidth = rail;
  g.stroke(path);
  // A thin light line down the middle reads as two rails.
  g.strokeStyle = pen.halo;
  g.globalAlpha = 0.55;
  g.lineWidth = Math.max(0.8 * d, rail * 0.28);
  g.stroke(path);
  g.globalAlpha = 1;
  g.fillStyle = colour;
  g.strokeStyle = pen.halo;
  g.lineWidth = 1.5 * d;
  for (const [x, y] of posts) {
    g.strokeRect(x * s - post / 2, y * s - post / 2, post, post);
    g.fillRect(x * s - post / 2, y * s - post / 2, post, post);
  }
  g.restore();
}

/**
 * A gate in a fence: a wooden leaf, standing a little ajar, that spans the gap
 * between the rail ends on either side (their posts are the gate posts).
 * `along` is true when the fence runs east-west through the cell.
 */
export function drawGate(g: CanvasRenderingContext2D, pen: Pen, cell: number, along: boolean, colour: string) {
  const { s, dpr: d } = pen;
  const cx = ((cell % N) + 0.5) * s, cy = (((cell / N) | 0) + 0.5) * s;
  const ux = along ? 1 : 0, uy = along ? 0 : 1; // along the fence
  const hx = cx - ux * s, hy = cy - uy * s; // hinge: the centre of the fence cell before the gate
  const ang = 0.3, len = 1.85 * s;
  const lx = (ux * Math.cos(ang) - uy * Math.sin(ang)) * len, ly = (ux * Math.sin(ang) + uy * Math.cos(ang)) * len;
  const hh = Math.max(2.5 * d, 0.3 * s); // half the leaf's height
  const px = (-ly / len) * hh, py = (lx / len) * hh;
  const leaf = new Path2D();
  leaf.moveTo(hx + px, hy + py); leaf.lineTo(hx + lx + px, hy + ly + py);
  leaf.lineTo(hx + lx - px, hy + ly - py); leaf.lineTo(hx - px, hy - py);
  leaf.moveTo(hx - px, hy - py); leaf.lineTo(hx + lx + px, hy + ly + py); // the brace
  g.save();
  g.lineCap = "round"; g.lineJoin = "round";
  g.strokeStyle = pen.halo;
  g.globalAlpha = 0.8;
  g.lineWidth = Math.max(2 * d, s * 0.2) + 3 * d;
  g.stroke(leaf);
  g.globalAlpha = 1;
  g.strokeStyle = colour;
  g.lineWidth = Math.max(2 * d, s * 0.2);
  g.stroke(leaf);
  g.restore();
}

/** Gravel: a pale wash with a scatter of grey stones on each cell. */
export function drawGravel(g: CanvasRenderingContext2D, pen: Pen, cells: number[], colour: string, wash: number) {
  if (!cells.length) return;
  const { s, dpr: d } = pen;
  const r = Math.max(0.9 * d, s * 0.09);
  g.save();
  g.fillStyle = colour;
  g.globalAlpha = wash;
  for (const c of cells) g.fillRect((c % N) * s, ((c / N) | 0) * s, s, s);
  g.globalAlpha = 1;
  const stones = new Path2D();
  for (const c of cells) {
    const x0 = (c % N) * s, y0 = ((c / N) | 0) * s;
    for (let k = 0; k < 5; k++) {
      const x = x0 + (0.15 + 0.7 * hash2(c, 31 + k)) * s, y = y0 + (0.15 + 0.7 * hash2(c, 67 + k)) * s;
      stones.moveTo(x + r, y); stones.arc(x, y, r, 0, Math.PI * 2);
    }
  }
  g.strokeStyle = pen.halo;
  g.lineWidth = Math.max(0.6 * d, r * 0.6);
  g.globalAlpha = 0.6;
  g.stroke(stones);
  g.globalAlpha = 1;
  g.fill(stones);
  g.restore();
}

/** A footbridge: a deck two cells wide with planks and rails (the sim's bridge is one row). */
export function drawBridge(g: CanvasRenderingContext2D, pen: Pen, cells: number[], deck: string, ghost: boolean) {
  const { s, dpr: d } = pen;
  const x0 = (cells[0] % N) - 0.6, x1 = (cells[cells.length - 1] % N) + 1.6, y = (cells[0] / N) | 0;
  const top = (y - 0.45) * s, h = 1.9 * s;
  g.save();
  if (ghost) g.globalAlpha = 0.8;
  g.fillStyle = deck;
  g.fillRect(x0 * s, top, (x1 - x0) * s, h);
  g.strokeStyle = ghost ? deck : pen.ink;
  g.globalAlpha *= 0.6;
  g.lineWidth = Math.max(1, s * 0.1);
  g.beginPath();
  for (let x = x0 + 0.5; x < x1 - 0.2; x += 0.5) { g.moveTo(x * s, top); g.lineTo(x * s, top + h); }
  g.stroke();
  g.globalAlpha = ghost ? 0.9 : 1;
  g.strokeStyle = pen.ink;
  g.lineWidth = Math.max(1.5 * d, s * 0.28);
  g.beginPath();
  g.moveTo(x0 * s, top); g.lineTo(x1 * s, top);
  g.moveTo(x0 * s, top + h); g.lineTo(x1 * s, top + h);
  g.stroke();
  g.fillStyle = pen.ink;
  const post = Math.max(3 * d, s * 0.5);
  for (const px of [x0, x1]) for (const py of [top, top + h]) g.fillRect(px * s - post / 2, py - post / 2, post, post);
  g.restore();
}

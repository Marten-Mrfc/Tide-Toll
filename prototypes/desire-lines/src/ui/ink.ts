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
    lines.push(simplify(unstair(chain.flatMap(c => [(c % N) + 0.5, ((c / N) | 0) + 0.5])), 0.6));
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

/**
 * Irons out a staircase: two passes of a 3-point moving average (the ends
 * stay), so the steps of a diagonal fence fall onto one straight line before
 * simplifying. A real corner moves inward by a third of a cell at most.
 */
function unstair(pts: number[]): number[] {
  const n = pts.length / 2;
  if (n < 3) return pts;
  // Real corners (a turn of more than about 50 degrees in a coarse simplification) stay where they are.
  const keep = simplifyKeep(pts, 1.2), anchor = new Uint8Array(n);
  anchor[0] = anchor[n - 1] = 1;
  for (let j = 1; j + 1 < keep.length; j++) {
    const [p, q, r] = [keep[j - 1], keep[j], keep[j + 1]];
    const ax = pts[2 * q] - pts[2 * p], ay = pts[2 * q + 1] - pts[2 * p + 1], bx = pts[2 * r] - pts[2 * q], by = pts[2 * r + 1] - pts[2 * q + 1];
    if ((ax * bx + ay * by) / ((Math.hypot(ax, ay) * Math.hypot(bx, by)) || 1) < 0.64) anchor[q] = 1;
  }
  let a = pts.slice(), b = pts.slice();
  for (let pass = 0; pass < 2; pass++) {
    for (let i = 1; i < n - 1; i++)
      for (let k = 0; k < 2; k++) b[2 * i + k] = anchor[i] ? a[2 * i + k] : (a[2 * i - 2 + k] + a[2 * i + k] + a[2 * i + 2 + k]) / 3;
    [a, b] = [b, a];
  }
  return a;
}

/** Douglas-Peucker on a flat x,y list. */
function simplify(pts: number[], tol: number): number[] {
  const out: number[] = [];
  for (const i of simplifyKeep(pts, tol)) out.push(pts[2 * i], pts[2 * i + 1]);
  return out;
}

/** The indices Douglas-Peucker keeps, in order. */
function simplifyKeep(pts: number[], tol: number): number[] {
  const n = pts.length / 2;
  if (n <= 2) return n === 2 ? [0, 1] : n === 1 ? [0] : [];
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
  for (let i = 0; i < n; i++) if (keep[i]) out.push(i);
  return out;
}

/** A gate to draw: its cells (one, or a stair corner's two), its centre and the fence's direction there. */
export interface GateMark { cells: number[]; x: number; y: number; ux: number; uy: number; half: number; }

/**
 * Where a gate sits and which way its fence runs: the centre of its cells, and
 * the main axis of the fence cells within 2 cells (any angle; on a staircase
 * this is the stair's slope, not the step's). `fence` holds the fence flags.
 */
export function gateMark(cells: number[], fence: Uint8Array): GateMark {
  let x = 0, y = 0;
  for (const c of cells) { x += (c % N) + 0.5; y += ((c / N) | 0) + 0.5; }
  x /= cells.length; y /= cells.length;
  let sxx = 0, syy = 0, sxy = 0, n = 0;
  const cx = Math.floor(x), cy = Math.floor(y);
  for (let dy = -2; dy <= 2; dy++)
    for (let dx = -2; dx <= 2; dx++) {
      const vx = cx + dx, vy = cy + dy;
      if (vx < 0 || vy < 0 || vx >= N || vy >= N || !fence[vy * N + vx]) continue;
      const ox = vx + 0.5 - x, oy = vy + 0.5 - y;
      sxx += ox * ox; syy += oy * oy; sxy += ox * oy; n++;
    }
  // Principal axis of the scatter (a lone cell: east-west).
  const ang = n > 1 ? 0.5 * Math.atan2(2 * sxy, sxx - syy) : 0;
  const ux = Math.cos(ang), uy = Math.sin(ang);
  // The opening spans the gate's cells along the fence: a cell on a straight fence, about 1.4 on a stair corner.
  // Drawn a little wider than the cells, so the leaf and its two posts read at map scale.
  const half = cells.length > 1 ? 1.05 : 0.9;
  return { cells, x, y, ux, uy, half };
}

/**
 * Cuts the gate openings out of a fence polyline. Returns the pieces of rail,
 * each with whether its start and its end are a gate post (so they get a gate
 * post, not a plain one).
 */
function cutForGates(ln: number[], gates: GateMark[]): { pts: number[]; a: boolean; b: boolean }[] {
  const n = ln.length / 2;
  const acc = new Float64Array(n);
  for (let i = 1; i < n; i++) acc[i] = acc[i - 1] + Math.hypot(ln[2 * i] - ln[2 * i - 2], ln[2 * i + 1] - ln[2 * i - 1]);
  const L = acc[n - 1], cuts: [number, number][] = [];
  for (const g of gates) {
    // Nearest point of the polyline to the gate's centre.
    let best = Infinity, at = 0, px = g.x, py = g.y, dx = g.ux, dy = g.uy;
    for (let i = 0; i + 1 < n; i++) {
      const ax = ln[2 * i], ay = ln[2 * i + 1], bx = ln[2 * i + 2], by = ln[2 * i + 3];
      const l2 = (bx - ax) ** 2 + (by - ay) ** 2 || 1e-9;
      const t = Math.max(0, Math.min(1, ((g.x - ax) * (bx - ax) + (g.y - ay) * (by - ay)) / l2));
      const d = Math.hypot(ax + (bx - ax) * t - g.x, ay + (by - ay) * t - g.y);
      if (d < best) {
        best = d; at = acc[i] + t * (acc[i + 1] - acc[i]);
        px = ax + (bx - ax) * t; py = ay + (by - ay) * t;
        const l = Math.sqrt(l2); dx = (bx - ax) / l; dy = (by - ay) / l;
      }
    }
    if (n > 1 && best < 0.9) {
      cuts.push([at - g.half, at + g.half]);
      // The gate sits on the rail: its posts are the rail's cut ends.
      g.x = px; g.y = py; g.ux = dx; g.uy = dy;
    }
  }
  if (!cuts.length) return [{ pts: ln, a: false, b: false }];
  cuts.sort((p, q) => p[0] - q[0]);
  const pointAt = (s: number, out: number[]) => {
    let i = 0;
    while (i + 2 < n && acc[i + 1] < s) i++;
    const t = acc[i + 1] > acc[i] ? (s - acc[i]) / (acc[i + 1] - acc[i]) : 0;
    out.push(ln[2 * i] + (ln[2 * i + 2] - ln[2 * i]) * t, ln[2 * i + 1] + (ln[2 * i + 3] - ln[2 * i + 1]) * t);
  };
  const pieces: { pts: number[]; a: boolean; b: boolean }[] = [];
  let s0 = 0, a = false;
  const emit = (s1: number, b: boolean) => {
    if (s1 - s0 > 0.05) {
      const pts: number[] = [];
      pointAt(s0, pts);
      for (let i = 1; i < n - 1; i++) if (acc[i] > s0 + 1e-6 && acc[i] < s1 - 1e-6) pts.push(ln[2 * i], ln[2 * i + 1]);
      pointAt(s1, pts);
      pieces.push({ pts, a, b });
    }
  };
  for (const [c0, c1] of cuts) {
    if (c1 <= s0) continue;
    emit(Math.max(s0, Math.min(L, c0)), true);
    s0 = Math.max(s0, c1); a = true;
  }
  if (s0 < L) emit(L, false);
  return pieces;
}

/**
 * Fences as continuous post-and-rail lines: a rail over a halo, with posts at
 * the ends and at real corners only (a staircase of cells is one straight
 * rail). A lone cell is a single post. Gates (`gates`) are cut out of the rail
 * and drawn as a leaf standing ajar between two gate posts.
 */
export function drawFence(g: CanvasRenderingContext2D, pen: Pen, cells: number[], colour: string, gates: GateMark[] = [], gateColour = colour) {
  if (!cells.length) return;
  const { s, dpr: d } = pen;
  const lines = fenceLines(cells);
  const rail = Math.max(2.5 * d, s * 0.42), post = Math.max(4 * d, s * 0.62);
  const path = new Path2D(), posts: [number, number][] = [];
  for (const ln0 of lines) {
    const n0 = ln0.length / 2;
    if (n0 === 1) { posts.push([ln0[0], ln0[1]]); continue; }
    for (const { pts: ln, a, b } of cutForGates(ln0, gates)) {
      const n = ln.length / 2;
      path.moveTo(ln[0] * s, ln[1] * s);
      for (let i = 2; i < ln.length; i += 2) path.lineTo(ln[i] * s, ln[i + 1] * s);
      if (!a) posts.push([ln[0], ln[1]]);
      if (!b) posts.push([ln[ln.length - 2], ln[ln.length - 1]]);
      // A corner: the line turns by more than about 25 degrees.
      for (let i = 1; i < n - 1; i++) {
        const ax = ln[2 * i] - ln[2 * i - 2], ay = ln[2 * i + 1] - ln[2 * i - 1];
        const bx = ln[2 * i + 2] - ln[2 * i], by = ln[2 * i + 3] - ln[2 * i + 1];
        const cos = (ax * bx + ay * by) / ((Math.hypot(ax, ay) * Math.hypot(bx, by)) || 1);
        if (cos < 0.9) posts.push([ln[2 * i], ln[2 * i + 1]]);
      }
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
  for (const m of gates) drawGate(g, pen, m, gateColour, colour);
}

/**
 * A gate: its two posts, one at each side of the opening, and a wooden leaf
 * hung on the first, standing a little ajar, across the opening. Drawn along
 * the fence's own direction, so it sits in a diagonal fence too.
 */
export function drawGate(g: CanvasRenderingContext2D, pen: Pen, m: GateMark, colour: string, postColour = colour) {
  const { s, dpr: d } = pen;
  const ax = (m.x - m.ux * m.half) * s, ay = (m.y - m.uy * m.half) * s; // hinge post
  const bx = (m.x + m.ux * m.half) * s, by = (m.y + m.uy * m.half) * s; // latch post
  const ang = 0.32, len = 2 * m.half * s * 0.96;
  const c = Math.cos(ang), sn = Math.sin(ang);
  const lx = (m.ux * c - m.uy * sn) * len, ly = (m.ux * sn + m.uy * c) * len;
  const hh = Math.max(2.2 * d, 0.26 * s); // half the leaf's height (drawn flat, seen from above)
  const px = (-ly / len) * hh, py = (lx / len) * hh;
  const leaf = new Path2D();
  leaf.moveTo(ax + px, ay + py); leaf.lineTo(ax + lx + px, ay + ly + py);
  leaf.lineTo(ax + lx - px, ay + ly - py); leaf.lineTo(ax - px, ay - py); leaf.closePath();
  leaf.moveTo(ax - px, ay - py); leaf.lineTo(ax + lx + px, ay + ly + py); // the brace
  const post = Math.max(4 * d, s * 0.58);
  g.save();
  g.lineCap = "round"; g.lineJoin = "round";
  g.strokeStyle = pen.halo;
  g.globalAlpha = 0.8;
  g.lineWidth = Math.max(2 * d, s * 0.18) + 3 * d;
  g.stroke(leaf);
  g.globalAlpha = 1;
  g.strokeStyle = colour;
  g.lineWidth = Math.max(2 * d, s * 0.18);
  g.stroke(leaf);
  g.fillStyle = postColour;
  g.strokeStyle = pen.halo;
  g.lineWidth = 1.5 * d;
  for (const [x, y] of [[ax, ay], [bx, by]]) {
    g.strokeRect(x - post / 2, y - post / 2, post, post);
    g.fillRect(x - post / 2, y - post / 2, post, post);
  }
  g.restore();
}

/**
 * Gravel: a pale strip of grit with soft rounded edges and a thin dark rim, so
 * it reads on grass and on worn earth in both themes, with speckled stones.
 * Neighbouring cells (8 ways) join into one strip.
 */
export function drawGravel(g: CanvasRenderingContext2D, pen: Pen, cells: number[], wash: string, speck: string, rim: string, alpha = 1) {
  if (!cells.length) return;
  const { s, dpr: d } = pen;
  const set = new Set(cells), strip = new Path2D();
  for (const c of cells) {
    const x = (c % N) + 0.5, y = ((c / N) | 0) + 0.5;
    strip.moveTo(x * s, y * s); strip.lineTo(x * s + 0.01, y * s);
    for (const [dx, dy] of [[1, 0], [0, 1], [1, 1], [-1, 1]]) {
      const v = c + dy * N + dx;
      if ((c % N) + dx >= 0 && (c % N) + dx < N && set.has(v)) { strip.moveTo(x * s, y * s); strip.lineTo((x + dx) * s, (y + dy) * s); }
    }
  }
  const W = 1.15 * s;
  g.save();
  g.globalAlpha = alpha;
  g.lineCap = "round"; g.lineJoin = "round";
  g.strokeStyle = rim;
  g.lineWidth = W + Math.max(2 * d, s * 0.16);
  g.stroke(strip);
  g.strokeStyle = wash;
  g.lineWidth = W;
  g.stroke(strip);
  const r = Math.max(0.7 * d, s * 0.075), stones = new Path2D();
  for (const c of cells) {
    const x0 = (c % N) * s, y0 = ((c / N) | 0) * s;
    for (let k = 0; k < 6; k++) {
      const x = x0 + (0.1 + 0.8 * hash2(c, 31 + k)) * s, y = y0 + (0.1 + 0.8 * hash2(c, 67 + k)) * s;
      stones.moveTo(x + r, y); stones.arc(x, y, r * (0.7 + 0.6 * hash2(c, 97 + k)), 0, Math.PI * 2);
    }
  }
  g.fillStyle = speck;
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

import { PARAMS } from "./params";

export const N = PARAMS.gridSize;
export const NC = N * N;

// 8 neighbours: the first four are orthogonal.
export const DX = [1, -1, 0, 0, 1, 1, -1, -1];
export const DY = [0, 0, 1, -1, 1, -1, 1, -1];
export const DL = [1, 1, 1, 1, Math.SQRT2, Math.SQRT2, Math.SQRT2, Math.SQRT2];

export const idx = (x: number, y: number) => y * N + x;
export const inside = (x: number, y: number) => x >= 0 && y >= 0 && x < N && y < N;

/** 4-connected line between two cells, so a fence drawn diagonally has no gaps to slip through. */
export function line4(x0: number, y0: number, x1: number, y1: number): number[] {
  // Non-finite input (a pointer over a zero-size canvas) would never terminate.
  if (!Number.isFinite(x0) || !Number.isFinite(y0) || !Number.isFinite(x1) || !Number.isFinite(y1)) return [];
  x0 = Math.round(x0); y0 = Math.round(y0); x1 = Math.round(x1); y1 = Math.round(y1);
  const out: number[] = [idx(x0, y0)];
  const dx = Math.abs(x1 - x0), dy = -Math.abs(y1 - y0);
  const sx = x1 > x0 ? 1 : -1, sy = y1 > y0 ? 1 : -1;
  let err = dx + dy, x = x0, y = y0;
  while (x !== x1 || y !== y1) {
    const e2 = 2 * err;
    const stepX = e2 >= dy, stepY = e2 <= dx;
    if (stepX && stepY) {
      // A diagonal Bresenham step: fill the corner so the line stays 4-connected.
      x += sx; out.push(idx(x, y));
      err += dy + dx; y += sy;
    } else if (stepX) { err += dy; x += sx; }
    else { err += dx; y += sy; }
    out.push(idx(x, y));
  }
  return out;
}

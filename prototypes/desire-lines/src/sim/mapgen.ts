import { N, NC, idx, inside } from "./grid";
import { mulberry32, hash2, Rng } from "./rng";

export type BuildingKind = "house" | "well" | "field" | "market";

export interface Building {
  id: number;
  kind: BuildingKind;
  x: number; // footprint top-left cell
  y: number;
  w: number;
  h: number;
  door: number; // cell index walkers go to and leave from
}

export interface MapData {
  seed: number;
  water: Uint8Array; // river cells (impassable unless ford or bridge)
  ford: Uint8Array; // natural shallows: walkable but slow
  footprint: Uint8Array; // building footprints that block walking (doors excluded)
  crop: Uint8Array; // field cells: walkable with a crop penalty
  noise: Float32Array; // smooth fixed noise in [-1, 1]
  riverX: Float32Array; // river centre x per row
  riverHalfW: number;
  fordY: number; // centre row of the ford
  buildings: Building[];
  /** Side of the river each cell is on: -1 west, +1 east, 0 water. */
  bank: Int8Array;
}

const SIZES: Record<BuildingKind, [number, number]> = {
  house: [3, 3],
  well: [2, 2],
  field: [9, 6],
  market: [5, 4],
};

function smoothNoise(seed: number): Float32Array {
  const raw = new Float32Array(NC);
  for (let i = 0; i < NC; i++) raw[i] = hash2(seed, i) * 2 - 1;
  // Two box-blur passes give gentle blobs rather than per-cell static.
  let a = raw, b = new Float32Array(NC);
  const r = 3;
  for (let pass = 0; pass < 2; pass++) {
    for (let y = 0; y < N; y++)
      for (let x = 0; x < N; x++) {
        let s = 0, n = 0;
        for (let k = -r; k <= r; k++) {
          const xx = pass === 0 ? x + k : x, yy = pass === 0 ? y : y + k;
          if (inside(xx, yy)) { s += a[idx(xx, yy)]; n++; }
        }
        b[idx(x, y)] = s / n;
      }
    [a, b] = [b, a];
  }
  let mx = 1e-6;
  for (let i = 0; i < NC; i++) mx = Math.max(mx, Math.abs(a[i]));
  for (let i = 0; i < NC; i++) a[i] /= mx;
  return a;
}

export function generateMap(seed: number): MapData {
  const r: Rng = mulberry32(seed * 7919 + 13);
  const water = new Uint8Array(NC), ford = new Uint8Array(NC);
  const footprint = new Uint8Array(NC), crop = new Uint8Array(NC);
  const bank = new Int8Array(NC);

  // A north-south river that meanders: two sines of different period.
  const riverX = new Float32Array(N);
  const a1 = 5 + r() * 6, f1 = 0.035 + r() * 0.03, p1 = r() * 6.283;
  const a2 = 1.5 + r() * 2.5, f2 = 0.1 + r() * 0.05, p2 = r() * 6.283;
  const cx = 60 + r() * 8;
  for (let y = 0; y < N; y++) riverX[y] = cx + a1 * Math.sin(y * f1 + p1) + a2 * Math.sin(y * f2 + p2);
  const riverHalfW = (3 + Math.floor(r() * 3)) / 2; // 3 to 5 cells wide
  const fordY = 40 + Math.floor(r() * 48);
  for (let y = 0; y < N; y++)
    for (let x = 0; x < N; x++) {
      const d = x + 0.5 - riverX[y];
      const i = idx(x, y);
      if (Math.abs(d) <= riverHalfW) {
        water[i] = 1;
        if (Math.abs(y - fordY) <= 1) ford[i] = 1;
      } else bank[i] = d < 0 ? -1 : 1;
    }

  // East or west for the main village is picked per seed so maps vary.
  const home = r() < 0.5 ? -1 : 1;
  const buildings: Building[] = [];
  const occupied = new Uint8Array(NC);
  const free = (x: number, y: number, w: number, h: number, side: number) => {
    const m = 2; // keep a walkable margin round every building
    if (x - m < 2 || y - m < 2 || x + w + m > N - 2 || y + h + m > N - 2) return false;
    for (let yy = y - m; yy < y + h + m; yy++)
      for (let xx = x - m; xx < x + w + m; xx++) {
        const i = idx(xx, yy);
        if (occupied[i] || bank[i] !== side) return false;
        if (Math.abs(xx + 0.5 - riverX[yy]) < riverHalfW + 4) return false;
      }
    return true;
  };
  const place = (kind: BuildingKind, side: number, x0: number, x1: number, y0: number, y1: number) => {
    const [w, h] = SIZES[kind];
    for (let t = 0; t < 400; t++) {
      // Coordinates are given for the west bank and mirrored for the east.
      let x = Math.floor(x0 + r() * (x1 - x0));
      if (side === 1) x = N - 1 - x - w;
      const y = Math.floor(y0 + r() * (y1 - y0));
      if (!free(x, y, w, h, side)) continue;
      for (let yy = y; yy < y + h; yy++)
        for (let xx = x; xx < x + w; xx++) {
          occupied[idx(xx, yy)] = 1;
          if (kind === "field") crop[idx(xx, yy)] = 1;
          else footprint[idx(xx, yy)] = 1;
        }
      let door: number;
      if (kind === "field") {
        // The field gate sits just outside the edge facing the river.
        const gx = side === -1 ? x + w : x - 1;
        door = idx(gx, y + (h >> 1));
      } else {
        door = idx(x + (w >> 1), y + h - 1);
        footprint[door] = 0;
      }
      occupied[door] = 1;
      buildings.push({ id: buildings.length, kind, x, y, w, h, door });
      return true;
    }
    return false;
  };

  const nHouses = 6 + Math.floor(r() * 3);
  // Destinations first so they always fit; the market is across the river.
  place("well", home, 30, 46, 36, 84);
  const north = r() < 0.5;
  place("field", home, 8, 34, north ? 10 : 88, north ? 30 : 110) || place("field", home, 6, 40, 6, 116);
  place("market", -home, 18, 40, 30, 92);
  let made = 0;
  const eastHouses = r() < 0.6 ? 1 : 0;
  // Houses cluster round the well, as a hamlet does; the field and market lie
  // further out, so routes from different houses share trunks on the way.
  const well = buildings[0];
  const wx = home === 1 ? N - 1 - well.x - well.w : well.x, wy = well.y;
  for (let i = 0; i < nHouses - eastHouses; i++)
    if (place("house", home, Math.max(4, wx - 24), wx + 12, Math.max(6, wy - 26), Math.min(118, wy + 26))) made++;
  for (let i = 0; i < eastHouses; i++) if (place("house", -home, 22, 48, 14, 110)) made++;
  // Fallback so every map has a working town even on a crowded seed.
  for (let t = 0; made < 4 && t < 20; t++) if (place("house", home, 4, 56, 4, 120)) made++;

  return { seed, water, ford, footprint, crop, noise: smoothNoise(seed), riverX, riverHalfW, fordY, buildings, bank };
}

// Golden data from the toy, for the C# port's tests (sim.tests/ToyGolden*.cs). Run through export.mjs,
// which maps "@toy/..." to the toy source folder given on its command line. Writes one JSON file.
import { writeFileSync } from "node:fs";
import { World } from "@toy/world";
import { generateMap } from "@toy/mapgen";
import { mulberry32, hash2 } from "@toy/rng";
import { N, NC, idx, inside, line4 } from "@toy/grid";
import { Heap, dijkstra, traceRoute } from "@toy/path";
import { describePreview, FlowPreview } from "@toy/preview";
import { PARAMS } from "@toy/params";

type Arr = Uint8Array | Int8Array | Uint16Array | Int16Array | Float32Array | Float64Array | Int32Array;
/** FNV-1a over the little-endian bytes of a typed array. */
function fnv(a: Arr): number {
  const u = new Uint8Array(a.buffer, a.byteOffset, a.byteLength);
  let h = 0x811c9dc5;
  for (let i = 0; i < u.length; i++) h = Math.imul(h ^ u[i], 16777619) >>> 0;
  return h;
}
const TPD = PARAMS.daySimSec * PARAMS.tickHz;
const t0 = Date.now();
const say = (s: string) => console.log(`[${((Date.now() - t0) / 1000).toFixed(0)} s] ${s}`);

// ---------------------------------------------------------------- rng, hash, line4, dijkstra
const rng = [0, 1, 42, 123456789, 4294967295].map(seed => { const r = mulberry32(seed); return { seed, values: [r(), r(), r(), r(), r(), r()] }; });
const hash = [[0, 0], [1, 0], [0, 1], [7, 1234], [42, 16383], [-1, 5], [2147483647, -2147483648], [100000, 99999]]
  .map(([a, b]) => ({ a, b, value: hash2(a, b) }));
const line4Cases = [[0, 0, 5, 2], [3, 3, 0, 0], [2, 7, 2, 1], [0, 0, 4, 4], [10, 3, 1, 9], [60, 2, 0, 127], [30, 30, 50, 60]]
  .map(([x0, y0, x1, y1]) => ({ from: [x0, y0], to: [x1, y1], cells: line4(x0, y0, x1, y1) }));
// Dijkstra on 128 x 128: costs 1 + 3 * mulberry32(7), a wall on column 64 for rows 0..99, target (10,10).
const dcost = new Float32Array(NC);
{ const r = mulberry32(7); for (let i = 0; i < NC; i++) dcost[i] = 1 + 3 * r(); for (let y = 0; y < 100; y++) dcost[idx(64, y)] = Infinity; }
const dfield = new Float32Array(NC);
dijkstra(idx(10, 10), dcost, dfield, new Heap(1024));
let dsum = 0, dinf = 0;
for (const f of dfield) { if (f === Infinity) dinf++; else dsum += f; }
const probes = [[10, 10], [11, 10], [63, 50], [65, 50], [64, 100], [127, 0], [127, 127], [0, 127]];
const dijkstraGolden = { infCount: dinf, sum: dsum, fieldFnv: fnv(dfield), probes: probes.map(([x, y]) => ({ x, y, value: dfield[idx(x, y)] })) };
say("rng, hash, line4, dijkstra");

// ---------------------------------------------------------------- maps
const kinds = { house: 0, well: 1, field: 2, market: 3 } as const;
const maps = [];
for (let s = 1; s <= 50; s++) {
  const m = generateMap(s);
  maps.push({
    seed: s, fordY: m.fordY, riverHalfW: m.riverHalfW,
    buildings: m.buildings.map(b => [kinds[b.kind], b.x, b.y, b.w, b.h, b.door]),
    water: fnv(m.water), ford: fnv(m.ford), crop: fnv(m.crop), footprint: fnv(m.footprint), bank: fnv(m.bank), noise: fnv(m.noise), riverX: fnv(m.riverX),
  });
}
say("maps 1-50");

// ---------------------------------------------------------------- world traces
function statsOf(w: World) {
  const s = w.stats();
  return { detourRatio: s.detourRatio, trailCoverage: s.trailCoverage, trunkShare: s.trunkShare, trunkShareD: s.trunkShareD, tripsInWindow: s.tripsInWindow, trailCells: s.trailCells, firstTrailTime: s.firstTrailTime };
}
const traces = [];
for (const [seed, walkers] of [[1, 200], [2, 200], [3, 200], [4, 200], [5, 200], [6, 200], [7, 200], [8, 200], [1, 500], [2, 500]]) {
  const w = new World(seed, walkers);
  const wearFnv: number[] = [], posFnv: number[] = [], firstTrip: number[] = [];
  let tripDone = false, prev = -1;
  for (let t = 0; t < 45 * TPD; t++) {
    w.tick();
    if (t % TPD === TPD - 1) {
      wearFnv.push(fnv(w.wear));
      const p = new Float64Array(walkers * 2);
      w.walkers.forEach((k, i) => { p[2 * i] = k.x; p[2 * i + 1] = k.y; });
      posFnv.push(fnv(p));
    }
    // Walker 0's first trip out and back: the cells it entered while walking.
    const k = w.walkers[0];
    if (!tripDone) {
      if (k.cell !== prev && (k.state === 1 || k.state === 3)) firstTrip.push(k.cell);
      prev = k.cell;
      if (k.state === 0 && firstTrip.length > 1) tripDone = true;
    }
  }
  traces.push({ seed, walkers, days: 45, wearFnv, posFnv, firstTrip, final: statsOf(w) });
  say(`trace seed ${seed} walkers ${walkers}`);
}

// ---------------------------------------------------------------- 20-seed table (the toy test's seedRow)
const mean = (a: number[]) => a.reduce((x, y) => x + y, 0) / Math.max(1, a.length);
const table = [];
for (let seed = 101; seed <= 120; seed++) {
  const w = new World(seed, 200);
  const trunk: number[] = [], trunkD: number[] = [], det: number[] = [], cov: number[] = [];
  for (let d = 1; d <= 45; d++) {
    for (let i = 0; i < TPD; i++) w.tick();
    if (d >= 20) { const s = w.stats(); trunk.push(s.trunkShare); trunkD.push(s.trunkShareD); det.push(s.detourRatio); cov.push(s.trailCoverage); }
  }
  table.push({ seed, walkers: 200, ft: w.metrics.firstTrailTime, det: mean(det), cov: mean(cov), mean: mean(trunk), min: Math.min(...trunk), max: Math.max(...trunk), meanD: mean(trunkD), below: trunk.filter(v => v < 0.3 || v > 0.7).length, wearFnv: fnv(w.wear) });
  say(`table seed ${seed}`);
}

// ---------------------------------------------------------------- flow preview, exact
function doorDistance(w: World, c: number): number {
  let best = Infinity;
  for (const b of w.buildings) best = Math.min(best, Math.hypot((b.door % N) - (c % N), ((b.door / N) | 0) - ((c / N) | 0)));
  return best;
}
function fenceAcrossRoute(w: World, house: number, at: number, half = 7): number[] {
  const dest = w.kind("market")[0], B = w.buildings;
  const r = traceRoute(w.fields[dest], w.cost, B[house].door, B[dest].door);
  const n = r.length / 2;
  for (let k = Math.max(1, Math.floor(n * at)); k < n - 1; k++) {
    const x = r[2 * k], y = r[2 * k + 1];
    if (Math.abs(x - w.map.riverX[Math.floor(y)]) < w.map.riverHalfW + 6 || doorDistance(w, idx(Math.floor(x), Math.floor(y))) < 8) continue;
    const dx = r[2 * k + 2] - r[2 * k - 2], dy = r[2 * k + 3] - r[2 * k - 1], L = Math.hypot(dx, dy) || 1;
    const px = -dy / L, py = dx / L;
    return line4(Math.round(x - px * half), Math.round(y - py * half), Math.round(x + px * half), Math.round(y + py * half))
      .filter(c => inside(c % N, (c / N) | 0) && w.canFence(c));
  }
  return [];
}
const pvJson = (w: World, p: FlowPreview) => ({
  reroutedPerDay: p.reroutedPerDay, more: p.more, version: p.version, blocked: p.blocked.map(b => [b.dest, b.houses]),
  describe: describePreview(p, b => w.buildings[b].kind),
  changes: p.changes.map(c => ({ home: c.home, dest: c.dest, rate: c.rate, reachable: c.reachable, costRatio: c.reachable ? c.costRatio : -1, oldLen: c.oldPts.length, newLen: c.newPts.length, oldFnv: fnv(c.oldPts), newFnv: fnv(c.newPts) })),
});
const previews = [];
for (const seed of [2, 4]) {
  const w = new World(seed, 200);
  for (let i = 0; i < 20 * TPD; i++) w.tick();
  const market = w.buildings[w.kind("market")[0]];
  const route = traceRoute(w.fields[market.id], w.cost, w.buildings[w.houses[0]].door, market.door);
  const fence = fenceAcrossRoute(w, w.houses[0], 0.3);
  const block = w.previewFlows({ block: fence }, 99);
  const my = (market.door / N) | 0;
  let cells: number[] | null = null, row = -1;
  for (let d = 0; d < 40 && !cells; d++)
    for (const y of [my + d, my - d])
      if (!cells && y > 3 && y < N - 4 && Math.abs(y - w.map.fordY) >= 8) { cells = w.bridgeCellsAt(w.map.riverX[y], y); row = y; }
  const bridge = w.previewFlows({ bridge: cells ?? [] });
  const well = w.buildings[w.kind("well")[0]];
  const ring: number[] = [];
  for (let x = well.x - 2; x <= well.x + well.w + 1; x++) ring.push(idx(x, well.y - 2), idx(x, well.y + well.h + 1));
  for (let y = well.y - 2; y <= well.y + well.h + 1; y++) ring.push(idx(well.x - 2, y), idx(well.x + well.w + 1, y));
  const ringCells = ring.filter(c => w.canFence(c));
  const enclose = w.previewFlows({ block: ringCells });
  previews.push({
    seed, walkers: 200, days: 20, wearFnv: fnv(w.wear), costFnv: fnv(w.cost), tripRateFnv: fnv(w.tripRate),
    routeLen: route.length, routeFnv: fnv(route), fence, bridgeRow: row, bridgeCells: cells, ring: ringCells,
    block: pvJson(w, block), bridge: pvJson(w, bridge), enclose: pvJson(w, enclose),
  });
  say(`preview seed ${seed}`);
}

writeFileSync(process.env.TOY_GOLDEN_OUT!, JSON.stringify({
  toyCommit: process.env.TOY_GOLDEN_COMMIT, command: process.env.TOY_GOLDEN_COMMAND,
  note: "FNV-1a hashes are over the little-endian bytes of the toy's typed arrays. Regenerate with the command; never edit by hand.",
  rng, hash, line4: line4Cases, dijkstra: dijkstraGolden, maps, traces, table, previews,
}, null, 1));
say(`wrote ${process.env.TOY_GOLDEN_OUT}`);

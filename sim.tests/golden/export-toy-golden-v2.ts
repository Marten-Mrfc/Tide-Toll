// Comparison data, version 2: the finished toy (rounds 3 to 5), for the C# port's tests. Run through export.mjs, which
// maps "@toy/..." to the toy's src/sim and "@toytest/..." to its test folder. Writes one JSON file. Everything in it
// comes from running the toy; nothing is copied from the C# code. Sections (docs/plans/sync-plan.md, section 4):
//   params, legacyParams      every PARAMS value of the finished toy, and the round-2 values the Legacy preset uses
//   probes                    lookup-table hashes and exact doubles of the pow, exp and sin calls on results paths
//   rng, hash, dijkstra       the two new streams (weather, growth); a low-cost Dijkstra (bucket queue), routeLength,
//                             lineCost with and without a limit
//   tracesOff                 seeds 1-8 at 200 and 1-2 at 500 walkers, 45 days, rain and growth off: per day hashes of
//                             wear, flow, cost, costX and positions; walker 0's first trip; final stats; and, for seed 1
//                             at 200, the wear hash of every tick of the first two days (to find a first divergence)
//   tracesLive                seeds 1-3 at 200 and 1 at 500, rain and growth on: per day also mud, weather, houses
//   legacyCheck               the finished toy run with the round-2 values: per day wear hashes (export.mjs compares them
//                             with the legacy file's traces)
//   weather                   the weather alone, seeds 1-50, 120 days
//   plots                     the growth plot search on seeds 1 and 2, days 3 and 9 (live worlds)
//   edits                     edits at fixed ticks (fence, gravel, gates at five angles, bridge, erase, rain), off and live
//   previews                  flow previews off (seeds 2, 4) and live (seed 2 with grown houses, gravel, gates)
//   table, tableLive          the G3 gate rows on seeds 101-120 (off; with the toy's printed table) and 601-620 (live)
//   baselines                 seeds 1-3 at 200 walkers, 20 days: day-20 wear hash and the next three walker draws,
//                             off and live (the river-off baseline is re-recorded from these)
import { writeFileSync, readFileSync } from "node:fs";
import { World } from "@toy/world";
import { generateMap } from "@toy/mapgen";
import { mulberry32, hash2 } from "@toy/rng";
import { N, NC, idx, inside, line4 } from "@toy/grid";
import { Heap, dijkstra, traceRoute, routeLength, lineCost } from "@toy/path";
import { describePreview, FlowPreview } from "@toy/preview";
import { PARAMS } from "@toy/params";
import { Weather } from "@toy/weather";
import { candidatePlots, keepsWaysOpen, choosePlot } from "@toy/growth";
import { seedRow, exactTable } from "@toytest/g3";

type Arr = Uint8Array | Int8Array | Uint16Array | Int16Array | Float32Array | Float64Array | Int32Array;
/** FNV-1a over the little-endian bytes of a typed array (as in version 1). */
function fnv(a: Arr): number {
  const u = new Uint8Array(a.buffer, a.byteOffset, a.byteLength);
  let h = 0x811c9dc5;
  for (let i = 0; i < u.length; i++) h = Math.imul(h ^ u[i], 16777619) >>> 0;
  return h;
}
const f64 = (v: number[]) => fnv(new Float64Array(v));
const TPD = PARAMS.daySimSec * PARAMS.tickHz;
const t0 = Date.now();
const say = (s: string) => console.log(`[${((Date.now() - t0) / 1000).toFixed(0)} s] ${s}`);
const OFF = { rain: false, growth: false }, LIVE = { rain: true, growth: true };
const P = PARAMS as any;

// ---------------------------------------------------------------- params
const params = JSON.parse(JSON.stringify(PARAMS));
const LEGACY = { wearPerStep: 2.4, sideShare: 0.3, minSurface: 0.6, fordCostMul: 3, explorerShare: 0, detourCap: 0, bridgeFlow: 0 };
function withParams<T>(over: Record<string, number>, f: () => T): T {
  const old: Record<string, number> = {};
  for (const k of Object.keys(over)) { old[k] = P[k]; P[k] = over[k]; }
  try { return f(); } finally { Object.assign(P, old); }
}

// ---------------------------------------------------------------- probes
const probeWorld = new World(1, 0, OFF) as any;
const pows = { low: Math.pow(0.5, 1 / P.halfLifeLow), mid: Math.pow(0.5, 1 / P.halfLifeMid), high: Math.pow(0.5, 1 / P.halfLifeHigh), hollow: Math.pow(0.5, 1 / P.halfLifeHollow), flow: Math.pow(0.5, 1 / P.flowHalfLife) };
const tripDecay = Math.exp(-(1 / P.tickHz) / (P.tripRateDays * P.daySimSec));
// Weidmann speed: rho = walkers here / cell area, for 2..21 walkers (rho > 0.3).
const speedExp = Array.from({ length: 20 }, (_, i) => { const rho = (i + 2) / P.cellAreaM2; return { here: i + 2, value: Math.exp(-P.weidmannGamma * (1 / rho - 1 / P.weidmannRhoMax)) }; });
// Crowd cost: occ is a float32 moving average; 20 values from mulberry32(99).
const crowdExp = (() => { const r = mulberry32(99), out = []; for (let i = 0; i < 20; i++) { const occ = Math.fround(0.25 + r() * 6); const rho = occ / P.cellAreaM2; out.push({ occ, value: Math.exp(-P.weidmannGamma * (1 / rho - 1 / P.weidmannRhoMax)) }); } return out; })();
// Plain exp, pow and sin over a spread of arguments.
const expProbe = Array.from({ length: 40 }, (_, i) => { const x = -12 + i * 0.3137; return { x, value: Math.exp(x) }; });
const sinProbe = Array.from({ length: 40 }, (_, i) => { const x = i * 0.1731 + 0.25; return { x, value: Math.sin(x) }; });
const powProbe = Array.from({ length: 20 }, (_, i) => { const x = i / 19; return { x, value: Math.pow(x, P.discPow) }; });
const probes = {
  wearLutFnv: fnv(probeWorld.wearLut), flowLutFnv: fnv(probeWorld.flowLut), wearLutLen: probeWorld.wearLut.length, flowLutLen: probeWorld.flowLut.length,
  pows, tripDecay, speedExp, crowdExp, exp: expProbe, sin: sinProbe, pow: powProbe,
  riverXFnv: [1, 2, 3, 7, 50].map(s => ({ seed: s, value: fnv(generateMap(s).riverX) })),
};
say("params, probes");

// ---------------------------------------------------------------- rng, hash, dijkstra
const weatherSeed = (s: number) => ((s * 2654435761) ^ 0x5eed) >>> 0;
const growthSeed = (s: number) => ((s * 40503) ^ 0x6f7d) >>> 0;
const streams = [1, 2, 3, 101, 601, 99991].map(seed => {
  const a = mulberry32(weatherSeed(seed)), b = mulberry32(growthSeed(seed));
  return { seed, weatherSeed: weatherSeed(seed), weather: [a(), a(), a(), a()], growthSeed: growthSeed(seed), growth: [b(), b(), b(), b()] };
});
const explorers = Array.from({ length: 64 }, (_, id) => hash2(id, 77));
// Low-cost Dijkstra (the toy's bucket queue): costs 0.3 + 0.5 * mulberry32(11), a wall on row 70 for columns 0..99.
const lcost = new Float32Array(NC);
{ const r = mulberry32(11); for (let i = 0; i < NC; i++) lcost[i] = 0.3 + 0.5 * r(); for (let x = 0; x < 100; x++) lcost[idx(x, 70)] = Infinity; }
const lfield = new Float32Array(NC);
dijkstra(idx(20, 100), lcost, lfield, new Heap(1024));
let lsum = 0, linf = 0;
for (const f of lfield) { if (f === Infinity) linf++; else lsum += f; }
const lprobes = [[20, 100], [21, 100], [5, 69], [5, 71], [100, 70], [127, 0], [0, 0], [127, 127]].map(([x, y]) => ({ x, y, value: lfield[idx(x, y)] }));
const routes = [[0, 0], [5, 20], [120, 10], [64, 64], [3, 125]].map(([x, y]) => ({ x, y, length: routeLength(lfield, lcost, idx(x, y), idx(20, 100)) }));
const lines = [[0.5, 0.5, 30.5, 12.5], [10.2, 80.7, 13.9, 60.1], [50.5, 69.5, 52.5, 71.5], [99.9, 69.2, 100.3, 71.8], [3.3, 3.3, 3.3, 3.3]].flatMap(([x0, y0, x1, y1]) =>
  [Infinity, 2.5].map(limit => ({ x0, y0, x1, y1, limit: limit === Infinity ? -1 : limit, value: lineCost(lcost, x0, y0, x1, y1, limit) })));
const dijkstraLow = { infCount: linf, sum: lsum, fieldFnv: fnv(lfield), probes: lprobes, routes, lines: lines.map(l => ({ ...l, value: l.value === Infinity ? -1 : l.value })) };
say("rng, hash, dijkstra");

// ---------------------------------------------------------------- traces
function statsOf(w: World) {
  const s = w.stats();
  return { detourRatio: s.detourRatio, trailCoverage: s.trailCoverage, trunkShare: s.trunkShare, trunkShareD: s.trunkShareD, tripsInWindow: s.tripsInWindow, trailCells: s.trailCells, firstTrailTime: s.firstTrailTime };
}
function posFnv(w: World) {
  const p = new Float64Array(w.walkers.length * 2);
  w.walkers.forEach((k, i) => { p[2 * i] = k.x; p[2 * i + 1] = k.y; });
  return fnv(p);
}
function weatherOf(w: World) {
  const x = w.weather as any;
  return [x.rain, x.wetness, x.raining ? 1 : 0, x.rainStart, x.rainEnd, x.mudUntil, x.rainDays];
}
function trace(seed: number, walkers: number, days: number, opts: typeof OFF, ticksFor = 0) {
  const w = new World(seed, walkers, opts);
  const wear: number[] = [], flow: number[] = [], cost: number[] = [], costX: number[] = [], pos: number[] = [];
  const mud: number[] = [], weather: number[][] = [], houses: number[] = [], walk: number[] = [], frugal: number[] = [];
  const tickWear: number[] = [], firstTrip: number[] = [];
  let tripDone = false, prev = -1;
  for (let t = 0; t < days * TPD; t++) {
    w.tick();
    if (t < ticksFor) tickWear.push(fnv(w.wear));
    if (t % TPD === TPD - 1) {
      wear.push(fnv(w.wear)); flow.push(fnv(w.flow)); cost.push(fnv(w.cost)); costX.push(fnv(w.costX)); pos.push(posFnv(w));
      frugal.push(w.walkers.filter(k => k.frugal && (k.state as number) % 2 === 1).length);
      if (opts.rain || opts.growth) { mud.push(fnv(w.mud)); weather.push(weatherOf(w)); houses.push(w.houses.length); walk.push(w.walkers.length); }
    }
    const k = w.walkers[0];
    if (!tripDone) {
      if (k.cell !== prev && ((k.state as number) === 1 || (k.state as number) === 3)) firstTrip.push(k.cell);
      prev = k.cell;
      if ((k.state as number) === 0 && firstTrip.length > 1) tripDone = true;
    }
  }
  const out: any = { seed, walkers, days, explorers: w.walkers.filter(k => k.explorer).length, wearFnv: wear, flowFnv: flow, costFnv: cost, costXFnv: costX, posFnv: pos, frugalOut: frugal, firstTrip, final: statsOf(w) };
  if (ticksFor) out.tickWearFnv = tickWear;
  if (opts.rain || opts.growth) {
    Object.assign(out, { mudFnv: mud, weather, houses, walkerCount: walk });
    out.grown = w.grown.map(g => [g.building, g.x, g.y, g.door, g.front, g.time, g.day]);
  }
  return out;
}
const tracesOff = [];
for (const [seed, walkers] of [[1, 200], [2, 200], [3, 200], [4, 200], [5, 200], [6, 200], [7, 200], [8, 200], [1, 500], [2, 500]]) {
  tracesOff.push(trace(seed, walkers, 45, OFF, seed === 1 && walkers === 200 ? 2 * TPD : 0));
  say(`trace off seed ${seed} walkers ${walkers}`);
}
const tracesLive = [];
for (const [seed, walkers] of [[1, 200], [2, 200], [3, 200], [1, 500]]) {
  tracesLive.push(trace(seed, walkers, 45, LIVE, seed === 1 && walkers === 200 ? 2 * TPD : 0));
  say(`trace live seed ${seed} walkers ${walkers}`);
}
const legacyCheck = withParams(LEGACY, () => [1, 2, 3].map(seed => {
  const w = new World(seed, 200, OFF), wear: number[] = [];
  for (let d = 1; d <= 20; d++) { for (let i = 0; i < TPD; i++) w.tick(); wear.push(fnv(w.wear)); }
  return { seed, walkers: 200, days: 20, wearFnv: wear };
}));
let legacyMatches: boolean | null = null;
if (process.env.TOY_GOLDEN_LEGACY) {
  const old = JSON.parse(readFileSync(process.env.TOY_GOLDEN_LEGACY, "utf8"));
  legacyMatches = legacyCheck.every(c => {
    const t = old.traces.find((x: any) => x.seed === c.seed && x.walkers === c.walkers);
    return t && c.wearFnv.every((v, i) => v === t.wearFnv[i]);
  });
}
say(`legacy check (matches the legacy traces: ${legacyMatches})`);

// ---------------------------------------------------------------- weather alone
const weather = [];
for (let seed = 1; seed <= 50; seed++) {
  const wx = new Weather(seed) as any;
  let t = 0;
  const all: number[] = [], days: number[] = [], rainStarts: number[] = [];
  let day: number[] = [];
  for (let tick = 0; tick < 120 * TPD; tick++) {
    t += 1 / P.tickHz;
    if (wx.update(t)) rainStarts.push(tick);
    const v = [wx.rain, wx.wetness, wx.raining ? 1 : 0, wx.mudUntil];
    all.push(...v); day.push(...v);
    if (tick % TPD === TPD - 1) { days.push(f64(day)); day = []; }
  }
  weather.push({ seed, days: 120, fnv: f64(all), rainDays: wx.rainDays, rainStarts, dayFnv: seed <= 3 ? days : undefined });
}
say("weather 1-50");

// ---------------------------------------------------------------- plots
const plots = [];
for (const seed of [1, 2]) {
  const w = new World(seed, 200, LIVE) as any;
  for (const day of [3, 9]) {
    while (w.ticks < day * TPD) w.tick();
    const all = candidatePlots(w);
    const top = all.slice(0, 8).map(p => ({ x: p.x, y: p.y, door: p.door, front: p.front, fronted: p.fronted, score: p.score, keeps: keepsWaysOpen(w, p) }));
    const c = choosePlot(w);
    plots.push({ seed, day, ticks: w.ticks, houses: w.houses.length, candidates: all.length, top, chosen: c ? { x: c.x, y: c.y, door: c.door, score: c.score } : null, wearFnv: fnv(w.wear) });
  }
}
say("plots");

// ---------------------------------------------------------------- edits at fixed ticks
function routeOf(w: World, house: number, dest: number) {
  const B = w.buildings;
  return traceRoute(w.fields[dest], w.cost, B[house].door, B[dest].door);
}
function doorDistance(w: World, c: number): number {
  let best = Infinity;
  for (const b of w.buildings) best = Math.min(best, Math.hypot((b.door % N) - (c % N), ((b.door / N) | 0) - ((c / N) | 0)));
  return best;
}
/** A point on house h's route to the market, away from the river and doors, at fraction `at` or later. */
function routePoint(w: World, house: number, at: number): [number, number] | null {
  const dest = w.kind("market")[0], r = routeOf(w, house, dest), n = r.length / 2;
  for (let k = Math.max(1, Math.floor(n * at)); k < n - 1; k++) {
    const x = r[2 * k], y = r[2 * k + 1];
    if (Math.abs(x - w.map.riverX[Math.floor(y)]) < w.map.riverHalfW + 6 || doorDistance(w, idx(Math.floor(x), Math.floor(y))) < 8) continue;
    return [x, y];
  }
  return null;
}
/** A fence line through (x, y) at `deg` degrees, `half` cells each way, fenceable cells only. */
function fenceAt(w: World, x: number, y: number, deg: number, half = 6): number[] {
  const a = (deg * Math.PI) / 180, ux = Math.cos(a), uy = Math.sin(a);
  return line4(Math.round(x - ux * half), Math.round(y - uy * half), Math.round(x + ux * half), Math.round(y + uy * half))
    .filter(c => inside(c % N, (c / N) | 0) && w.canFence(c));
}
const hashes = (w: World) => [fnv(w.wear), fnv(w.cost), fnv(w.costX), fnv(w.gravel), fnv(w.gate), fnv(w.gatePair), fnv(w.fence), fnv(w.mud), w.version, fnv(w.flow), posFnv(w)];
const gt = (t: any) => ({ cells: t.cells, remove: t.remove, reason: t.reason });
function edits(seed: number, opts: typeof OFF) {
  const w = new World(seed, 200, opts) as any;
  const log: any[] = [], rows: any[] = [];
  let fence1: number[] = [], gateCells: number[][] = [], gravelCells: number[] = [], bridgeCells: number[] | null = null;
  for (let t = 1; t <= 1500; t++) {
    w.tick();
    if (t === 300) {
      const p = routePoint(w, w.houses[0], 0.3)!;
      fence1 = fenceAt(w, p[0], p[1], 90);
      log.push({ tick: t, op: "fence", cells: fence1, n: w.placeFence(fence1) });
    }
    if (t === 450) {
      // A gravel strip along house 1's route to the well, each cell snapped as the brush does.
      const dest = w.kind("well")[0], r = routeOf(w, w.houses[1 % w.houses.length], dest), snaps: number[][] = [];
      const cells: number[] = [];
      for (let k = 2; k < Math.min(r.length / 2 - 2, 14); k++) {
        const c = idx(Math.floor(r[2 * k]), Math.floor(r[2 * k + 1]));
        const s = w.gravelSnap(c);
        snaps.push([c, s]);
        if (!cells.includes(s)) cells.push(s);
      }
      gravelCells = cells;
      log.push({ tick: t, op: "gravel", snaps, cells, n: w.placeGravel(cells) });
    }
    if (t === 600) {
      // Fences across five routes at 0, 30, 45, 60 and 90 degrees, then a gate where the route crosses each.
      const angles = [0, 30, 45, 60, 90];
      for (let i = 0; i < angles.length; i++) {
        // The first house (from house 2 + i on) whose route has a point clear of the river, the doors and fences so far.
        let p: [number, number] | null = null;
        for (let j = 0; j < w.houses.length && !p; j++) {
          const q = routePoint(w, w.houses[(2 + i + j) % w.houses.length], 0.4 + 0.05 * i);
          if (q && fenceAt(w, q[0], q[1], angles[i]).every(c => !w.fence[c])) p = q;
        }
        if (!p) { log.push({ tick: t, op: "gate", angle: angles[i], skipped: true }); continue; }
        const cells = fenceAt(w, p[0], p[1], angles[i]);
        const n = w.placeFence(cells);
        const target = gt(w.gateTarget(p[0], p[1]));
        const placed = target.cells.length && !target.remove ? w.placeGate(target.cells[0]) : false;
        gateCells.push(target.cells);
        log.push({ tick: t, op: "gate", angle: angles[i], point: p, fence: cells, n, target, placed, again: gt(w.gateTarget(p[0], p[1])) });
      }
      // Refusals: no fence near; a lone post; a fence along a building's wall (no way through).
      const far = w.gateTarget(2.5, 2.5);
      const post = (() => { for (let c = idx(10, 10); c < NC; c++) { const x = c % N, y = (c / N) | 0; if (x < 3 || x > N - 4) continue; let ok = true; for (let dy = -3; dy <= 3 && ok; dy++) for (let dx = -3; dx <= 3 && ok; dx++) ok = inside(x + dx, y + dy) && w.canFence(idx(x + dx, y + dy)) && !w.fence[idx(x + dx, y + dy)] && !w.map.crop[idx(x + dx, y + dy)]; if (ok) return c; } return -1; })();
      w.placeFence([post]);
      const lone = w.gateTarget((post % N) + 0.5, ((post / N) | 0) + 0.5);
      const b = w.buildings[w.kind("market")[0]];
      const wall: number[] = [];
      for (let x = b.x; x < b.x + b.w; x++) { const c = idx(x, b.y - 1); if (w.canFence(c)) wall.push(c); }
      w.placeFence(wall);
      const mid = wall[(wall.length / 2) | 0];
      const noWay = w.gateTarget((mid % N) + 0.5, ((mid / N) | 0) + 0.5);
      log.push({ tick: t, op: "refusals", far: gt(far), post, lone: gt(lone), wall, noWay: gt(noWay) });
    }
    if (t === 750) {
      const market = w.buildings[w.kind("market")[0]], my = (market.door / N) | 0;
      for (let d = 0; d < 40 && !bridgeCells; d++)
        for (const y of [my + d, my - d])
          if (!bridgeCells && y > 3 && y < N - 4 && Math.abs(y - w.map.fordY) >= 8) { const b = w.placeBridge(w.map.riverX[y], y); if (b) bridgeCells = b.cells; }
      log.push({ tick: t, op: "bridge", cells: bridgeCells });
    }
    if (t === 900) {
      // Erase: half of a two-cell gate if there is one (its partner stays a gate), part of the gravel, and the bridge.
      const pair = gateCells.find(g => g.length === 2) ?? gateCells[0] ?? [];
      const cells = [...pair.slice(0, 1), ...gravelCells.slice(0, 3), ...(bridgeCells ? [bridgeCells[0]] : [])];
      log.push({ tick: t, op: "erase", cells, n: w.erase(cells), pairPartnerGate: pair.length === 2 ? w.gate[pair[1]] : -1, pairPartnerPair: pair.length === 2 ? w.gatePair[pair[1]] : -1 });
    }
    if (t === 1000) { w.startRain(); log.push({ tick: t, op: "startRain", weather: weatherOf(w), mudCells: w.mudCells }); }
    if (t === 1200) { w.stopRain(true); log.push({ tick: t, op: "stopRain", weather: weatherOf(w), mudCells: w.mudCells }); }
    if (t % 50 === 0) rows.push([t, ...hashes(w)]);
  }
  return { seed, walkers: 200, opts, columns: ["tick", "wear", "cost", "costX", "gravel", "gate", "gatePair", "fence", "mud", "version", "flow", "pos"], log, rows };
}
const editsOff = edits(1, OFF);
say("edits off");
const editsLive = edits(1, LIVE);
say("edits live");

// ---------------------------------------------------------------- previews
const pvJson = (w: World, p: FlowPreview) => ({
  reroutedPerDay: p.reroutedPerDay, more: p.more, version: p.version, meanCostRatio: p.meanCostRatio, note: p.note ?? null,
  blocked: p.blocked.map(b => [b.dest, b.houses]),
  describe: describePreview(p, b => w.buildings[b].kind),
  changes: p.changes.map(c => ({ home: c.home, dest: c.dest, rate: c.rate, explorers: c.explorers, frugal: c.frugal, reachable: c.reachable, costRatio: c.reachable ? c.costRatio : -1, oldLen: c.oldPts.length, newLen: c.newPts.length, oldFnv: fnv(c.oldPts), newFnv: fnv(c.newPts) })),
});
function previewSet(seed: number, opts: typeof OFF) {
  const w = new World(seed, 200, opts) as any;
  for (let i = 0; i < 20 * TPD; i++) w.tick();
  const out: any = { seed, walkers: 200, days: 20, opts, houses: w.houses.length, wearFnv: fnv(w.wear), costFnv: fnv(w.cost), costXFnv: fnv(w.costX), tripRateFnv: fnv(w.tripRate) };
  const p = routePoint(w, w.houses[0], 0.3)!;
  const fence = fenceAt(w, p[0], p[1], 90, 7);
  out.fence = fence; out.block = pvJson(w, w.previewFlows({ block: fence }, 99));
  const market = w.buildings[w.kind("market")[0]], my = (market.door / N) | 0;
  let cells: number[] | null = null;
  for (let d = 0; d < 40 && !cells; d++)
    for (const y of [my + d, my - d])
      if (!cells && y > 3 && y < N - 4 && Math.abs(y - w.map.fordY) >= 8) cells = w.bridgeCellsAt(w.map.riverX[y], y);
  out.bridgeCells = cells; out.bridge = pvJson(w, w.previewFlows({ bridge: cells ?? [] }));
  const well = w.buildings[w.kind("well")[0]], ring: number[] = [];
  for (let x = well.x - 2; x <= well.x + well.w + 1; x++) ring.push(idx(x, well.y - 2), idx(x, well.y + well.h + 1));
  for (let y = well.y - 2; y <= well.y + well.h + 1; y++) ring.push(idx(well.x - 2, y), idx(well.x + well.w + 1, y));
  out.ring = ring.filter((c: number) => w.canFence(c)); out.enclose = pvJson(w, w.previewFlows({ block: out.ring }));
  // Gravel: on a route (along house 1's way to the well), near one (2 cells to the side), far from any (a quiet corner).
  const r = routeOf(w, w.houses[1 % w.houses.length], w.kind("well")[0]), on: number[] = [], near: number[] = [];
  for (let k = 2; k < Math.min(r.length / 2 - 2, 12); k++) {
    const x = Math.floor(r[2 * k]), y = Math.floor(r[2 * k + 1]);
    if (w.canGravel(idx(x, y)) && !on.includes(idx(x, y))) on.push(idx(x, y));
    if (inside(x + 2, y) && w.canGravel(idx(x + 2, y)) && !near.includes(idx(x + 2, y))) near.push(idx(x + 2, y));
  }
  let far: number[] = [];
  for (const [cx, cy] of [[4, 4], [N - 12, 4], [4, N - 5], [N - 12, N - 5]]) {
    const cand = Array.from({ length: 8 }, (_, i) => idx(cx + i, cy)).filter(c => w.canGravel(c));
    let quiet = true;
    for (const c of cand) for (let dy = -6; dy <= 6; dy++) for (let dx = -6; dx <= 6; dx++) { const x = (c % N) + dx, y = ((c / N) | 0) + dy; if (inside(x, y) && w.wear[idx(x, y)] >= 25) quiet = false; }
    if (quiet && cand.length) { far = cand; break; }
  }
  out.gravelOn = on; out.gravelNear = near; out.gravelFar = far;
  out.gravelOnPreview = pvJson(w, w.previewFlows({ gravel: on }));
  out.gravelNearPreview = pvJson(w, w.previewFlows({ gravel: near }));
  out.gravelFarPreview = pvJson(w, w.previewFlows({ gravel: far }));
  // A gate: the fence across the route is built, then a gate in it is previewed; then built, and its removal previewed.
  w.placeFence(fence);
  for (let i = 0; i < TPD; i++) w.tick();
  const target = w.gateTarget(p[0], p[1]);
  out.gateTarget = gt(target);
  out.gate = target.cells.length ? pvJson(w, w.previewFlows({ gate: target.cells })) : null;
  if (target.cells.length && !target.remove) {
    w.placeGate(target.cells[0]);
    for (let i = 0; i < TPD; i++) w.tick();
    out.ungate = pvJson(w, w.previewFlows({ ungate: target.cells }));
  }
  out.after = { ticks: w.ticks, wearFnv: fnv(w.wear), costFnv: fnv(w.cost), costXFnv: fnv(w.costX) };
  return out;
}
const previews = [previewSet(2, OFF), previewSet(4, OFF)];
say("previews off");
const previewsLive = [previewSet(2, LIVE)];
say("previews live");

// ---------------------------------------------------------------- tables
const strip = (r: any) => ({ ...r });
const table = [];
for (let seed = 101; seed <= 120; seed++) { table.push(strip(seedRow(seed, 200))); say(`table seed ${seed}`); }
const tableText = exactTable("G3 seeds 101-120 (200 walkers, rain and growth off)", table);
const tableLive = [];
for (let seed = 601; seed <= 620; seed++) { tableLive.push(strip(seedRow(seed, 200, {}))); say(`table live seed ${seed}`); }

// ---------------------------------------------------------------- river-off baselines (and the live family)
const baselines = [];
for (const [name, opts] of [["off", OFF], ["live", LIVE]] as const)
  for (const seed of [1, 2, 3]) {
    const w = new World(seed, 200, opts) as any;
    for (let i = 0; i < 20 * TPD; i++) w.tick();
    baselines.push({ family: name, seed, walkers: 200, days: 20, wearFnv: fnv(w.wear), next: [w.rng(), w.rng(), w.rng()] });
  }
say("baselines");

/** JSON with arrays of plain numbers, strings or booleans on one line, so the file stays small and readable. */
function compact(v: unknown): string {
  const s = JSON.stringify(v, (_k, x) => (typeof x === "number" && !Number.isFinite(x) ? (x > 0 ? "Infinity" : x < 0 ? "-Infinity" : "NaN") : x), 1);
  return s.replace(/\[\s+((?:-?[\d.eE+-]+|"[^"\n]*"|true|false|null)(?:,\s+(?:-?[\d.eE+-]+|"[^"\n]*"|true|false|null))*)\s+\]/g, (_m, inner) => "[" + inner.replace(/,\s+/g, ",") + "]");
}
const out = process.env.TOY_GOLDEN_OUT!;
writeFileSync(out, compact({
  version: 2, toyCommit: process.env.TOY_GOLDEN_COMMIT, command: process.env.TOY_GOLDEN_COMMAND,
  note: "FNV-1a hashes are over the little-endian bytes of the toy's typed arrays (f64 lists: over their float64 bytes). Non-finite numbers are written as strings. Regenerate with the command; never edit by hand.",
  seconds: Math.round((Date.now() - t0) / 1000),
  params, legacyParams: LEGACY, probes, streams, explorers, dijkstraLow,
  tracesOff, tracesLive, legacyCheck, legacyMatches, weather, plots, edits: [editsOff, editsLive],
  previews, previewsLive, table, tableText, tableLive, baselines,
}) + "\n");
say(`wrote ${out}`);
if (legacyMatches === false) { console.error("the finished toy with the round-2 values does NOT match the legacy traces"); process.exitCode = 1; }

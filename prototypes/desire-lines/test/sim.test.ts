// Headless simulation tests (npm test). Prints the trunk-share gate tables and
// runs the reroute, bridge, gravel, gate, robustness, determinism and flow
// preview checks. Exit code is non-zero if any check fails.
//
// Gate G3 (round 3): at 200 walkers, EVERY daily sample of the literal trunk
// share from day 20 to day 45 must lie within 30% to 70%, on each of five seed
// sets: fresh seeds 401 to 420 (run only after the round 3 model was final,
// never tuned on), and the earlier sets 1 to 20, 101 to 120 and 201 to 220 (the
// round 3 tuning sets) and 301 to 320 (never tuned on). The gate passes only
// if all five pass. These five sets run with rain and house growth off and must
// give the round 3 numbers bit for bit (test/g3-baseline.txt).
//
// Round 4 adds a sixth set: seeds 601 to 620 with rain and house growth on at the
// player's defaults (200 walkers to start), first run after the round 4 rules
// and numbers were final. The round 4 rule checks are in test/rules.test.ts.
//
// Also reported: median trunk width per set, the share of
// river crossings a new footbridge takes on seeds 1 to 10, and 500 walkers on
// seeds 1 to 5. The seeds run in worker threads; the other checks run meanwhile.
import { Worker, isMainThread, parentPort } from "node:worker_threads";
import { availableParallelism } from "node:os";
import { World } from "../src/sim/world";
import { PARAMS } from "../src/sim/params";
import { N, NC, idx, inside, line4 } from "../src/sim/grid";
import { traceRoute } from "../src/sim/path";
import { FlowPreview, describePreview, Ov } from "../src/sim/preview";
import { readFileSync } from "node:fs";
import { seedRow, exactTable, Row, SAMPLE_FROM, DAYS, WIDTH_DAY } from "./g3";
import { gateAngleTests, gateRefusalTests, fenceOverGravelTest, rainTests, growthTests, newDeterminismTest, newIsolationTest, gravelNoteTest } from "./rules.test";

const TPD = PARAMS.daySimSec * PARAMS.tickHz; // ticks per in-game day
/** The tool and preview checks below predate rain and growth: they run with both off. */
const OFF = { rain: false, growth: false };
const run = (w: World, days: number) => { for (let i = 0; i < days * TPD; i++) w.tick(); };
const pct = (v: number) => (v * 100).toFixed(1).padStart(5) + "%";
const mean = (a: number[]) => a.reduce((x, y) => x + y, 0) / Math.max(1, a.length);

// ------------------------------------------------------------------ one seed
// seedRow (test/g3.ts) runs one seed for 45 days and keeps every daily sample.
// The five old sets run with rain and house growth off, the new set with both on.
type Job = { kind: "trunk"; seed: number; walkers: number; live?: boolean } | { kind: "bridge"; seed: number };
interface BridgeRow { kind: "bridge"; seed: number; share: number; bridge: number; ford: number; row: number; }
type Result = (Row & { kind: "trunk" }) | BridgeRow;

function runJob(job: Job): Result {
  if (job.kind === "bridge") return { kind: "bridge", ...bridgeShare(job.seed) };
  // live: rain and house growth at the player's defaults; otherwise both off.
  return { kind: "trunk", ...seedRow(job.seed, job.walkers, job.live ? {} : { rain: false, growth: false }) };
}

function runSeedsInWorkers(jobs: Job[], threads: number): Promise<Result[]> {
  return new Promise((resolve, reject) => {
    const rows: Result[] = [];
    let next = 0;
    const workers: Worker[] = [];
    const feed = (wk: Worker) => { if (next < jobs.length) wk.postMessage(jobs[next++]); };
    for (let i = 0; i < Math.min(threads, jobs.length); i++) {
      const wk = new Worker(new URL(import.meta.url));
      wk.on("message", (r: Result) => {
        rows.push(r);
        if (rows.length === jobs.length) { for (const x of workers) void x.terminate(); resolve(rows); } else feed(wk);
      });
      wk.on("error", reject);
      workers.push(wk);
      feed(wk);
    }
  });
}

function printTable(title: string, rows: Row[]): void {
  rows.sort((a, b) => a.seed - b.seed);
  console.log(`\n${title}`);
  console.log(`(${rows[0].walkers} walkers, ${DAYS} in-game days, 1 day = ${PARAMS.daySimSec} s at 1x; trunk share sampled daily, days ${SAMPLE_FROM} to ${DAYS})`);
  console.log(`seed | first trail | detour | coverage | trunk mean   min    max  | out of band | trunk D | width day ${WIDTH_DAY}`);
  for (const r of rows)
    console.log(`${String(r.seed).padStart(4)} | ${r.ft < 0 ? "   none" : (r.ft.toFixed(0) + " s").padStart(7)}     | ${r.det.toFixed(3)}  |  ${pct(r.cov)}  |     ${pct(r.mean)} ${pct(r.min)} ${pct(r.max)} | ${String(r.below).padStart(4)} days   | ${pct(r.meanD)}  | ${r.width} cells`);
  const col = (k: keyof Row) => rows.map(r => r[k] as number);
  console.log(`all  | ${(mean(col("ft")).toFixed(1) + " s").padStart(7)} avg | ${mean(col("det")).toFixed(3)}  |  ${pct(mean(col("cov")))}  |     ${pct(mean(col("mean")))} ${pct(Math.min(...col("min")))} ${pct(Math.max(...col("max")))} | ${String(rows.filter(r => r.below > 0).length).padStart(4)} seeds  | ${pct(mean(col("meanD")))}  | median ${median(col("width"))}`);
  console.log(`     lowest seed mean ${pct(Math.min(...col("mean")))}; detour within 1.05-1.30 on ${rows.filter(r => r.det >= 1.05 && r.det <= 1.3).length} of ${rows.length}; coverage within 3%-8% on ${rows.filter(r => r.cov >= 0.03 && r.cov <= 0.08).length} of ${rows.length}`);
}

/** One line per seed set: the band result, the extremes, the trunk width and any out-of-band seeds. */
function summaryLine(title: string, rows: Row[]): string {
  const bad = rows.filter(r => r.min < 0.3 || r.max > 0.7);
  const col = (k: keyof Row) => rows.map(r => r[k] as number);
  return `${title.padEnd(16)} ${bad.length ? "FAIL" : "pass"}: ${rows.length - bad.length} of ${rows.length} seeds in band every day; mean ${pct(mean(col("mean")))}, lowest daily ${pct(Math.min(...col("min")))}, highest daily ${pct(Math.max(...col("max")))}; ` +
    `median trunk width ${median(col("width"))} cells; first trail ${mean(col("ft")).toFixed(1)} s avg` +
    (bad.length ? `; OUT OF BAND: ${bad.map(r => `seed ${r.seed} (${r.below} days, ${pct(r.min)} to ${pct(r.max)})`).join(", ")}` : "");
}

function median(a: number[]): number {
  const s = [...a].sort((x, y) => x - y);
  return s.length ? s[s.length >> 1] : 0;
}

// Check output is collected and printed after the gate tables.
const out: string[] = [];
const log = (...a: unknown[]) => { out.push(a.join(" ")); };
const failures: string[] = [];
const check = (ok: boolean, what: string) => {
  log(`  ${ok ? "PASS" : "FAIL"}  ${what}`);
  if (!ok) failures.push(what);
};

// ------------------------------------------------------------------ helpers
function doorDistance(w: World, c: number): number {
  let best = Infinity;
  for (const b of w.buildings) best = Math.min(best, Math.hypot((b.door % N) - (c % N), ((b.door / N) | 0) - ((c / N) | 0)));
  return best;
}

/** Counts river crossings by whether the walker crossed on the ford or on a bridge. */
class CrossingCounter {
  side: Int8Array; via: Int8Array; ford = 0; bridge = 0;
  constructor(w: World) { this.side = new Int8Array(4096); this.via = new Int8Array(4096); for (const k of w.walkers) this.side[k.id] = w.map.bank[k.cell]; }
  sample(w: World) {
    for (const k of w.walkers) {
      const b = w.map.bank[k.cell];
      if (b === 0) { this.via[k.id] = w.bridgeAt[k.cell] >= 0 ? 2 : w.map.ford[k.cell] ? 1 : this.via[k.id]; continue; }
      if (this.side[k.id] !== 0 && b !== this.side[k.id] && w.isVisible(k)) {
        if (this.via[k.id] === 2) this.bridge++; else if (this.via[k.id] === 1) this.ford++;
      }
      this.side[k.id] = b;
    }
  }
}

// ------------------------------------------------------------------ reroute
function rerouteTest(seed: number) {
  const w = new World(seed, 200, OFF);
  run(w, 20);
  const { cell: best, fence } = fenceAcrossBusiest(w);
  const bx = best % N, by = (best / N) | 0;
  const before = Float32Array.from(w.wear);
  const tripsBefore = w.stats().tripsInWindow;
  const placed = w.placeFence(fence);
  const ends = [fence[0], fence[fence.length - 1]];
  let onFence = 0, newTrailAt = -1;
  const t0 = w.time;
  for (let i = 0; i < 6 * TPD; i++) {
    w.tick();
    for (const k of w.walkers) if (w.isVisible(k) && w.fence[k.cell]) onFence++;
    if (newTrailAt < 0 && i % 10 === 0) {
      // New trail cells (not trail before the fence) near either end of the fence.
      let n = 0;
      for (const e of ends)
        for (let y = (e / N | 0) - 5; y <= (e / N | 0) + 5; y++)
          for (let x = (e % N) - 5; x <= (e % N) + 5; x++) {
            if (!inside(x, y)) continue;
            const c = idx(x, y);
            if (!w.fence[c] && w.wear[c] >= 25 && before[c] < 25) n++;
          }
      if (n >= 8) newTrailAt = w.time - t0;
    }
  }
  const fwAfter = w.metrics.window();
  let fenceSteps = 0;
  for (const c of fence) if (w.fence[c]) fenceSteps += fwAfter[c];
  const tripsAfter = w.stats().tripsInWindow;
  log(`\nREROUTE TEST (seed ${seed}): fence of ${placed} cells across the busiest trail at (${bx},${by}), 6 days after placing`);
  log(`  footsteps on fence cells: ${fenceSteps}; walker-ticks on fence cells: ${onFence}; trips in window before/after: ${tripsBefore}/${tripsAfter}`);
  log(`  new trail round the fence ends: ${newTrailAt < 0 ? "not formed" : `formed after ${newTrailAt.toFixed(1)} s sim (${(newTrailAt / PARAMS.daySimSec).toFixed(1)} days)`}`);
  check(placed >= 10, "reroute: fence placed across the trail");
  check(fenceSteps === 0 && onFence === 0, "reroute: traffic across the fenced line drops to zero");
  check(newTrailAt >= 0 && newTrailAt <= 3 * PARAMS.daySimSec, "reroute: a new trail forms round the fence within 3 in-game days (45 s at 1x)");
  check(tripsAfter >= 0.7 * tripsBefore, "reroute: trips keep completing (at least 70% of the rate before)");
}

// ------------------------------------------------------------------ bridge
/**
 * Day 15: a footbridge on the row of the market door (or the nearest row that
 * takes one, at least 8 rows from the ford), where it shortens the market
 * trip; then 1 day to re-plan and 4 days of counting river crossings.
 */
function bridgeRun(seed: number) {
  const w = new World(seed, 200, OFF);
  run(w, 15);
  const market = w.buildings.find(b => b.kind === "market")!;
  const my = (market.door / N) | 0;
  let bridge = null;
  for (let d = 0; d < 40 && !bridge; d++)
    for (const y of [my + d, my - d]) {
      if (bridge || y < 4 || y > N - 5 || Math.abs(y - w.map.fordY) < 8) continue;
      bridge = w.placeBridge(w.map.riverX[y], y);
    }
  if (!bridge) return { w, bridge, cc: null };
  run(w, 1); // let walkers re-plan and finish trips already under way
  const cc = new CrossingCounter(w);
  for (let i = 0; i < 4 * TPD; i++) { w.tick(); cc.sample(w); }
  return { w, bridge, cc };
}

function bridgeShare(seed: number): Omit<BridgeRow, "kind"> {
  const { bridge, cc } = bridgeRun(seed);
  if (!bridge || !cc) return { seed, share: -1, bridge: 0, ford: 0, row: -1 };
  return { seed, share: cc.bridge / Math.max(1, cc.bridge + cc.ford), bridge: cc.bridge, ford: cc.ford, row: bridge.row };
}

function bridgeTest(seed: number) {
  const { w, bridge, cc } = bridgeRun(seed);
  if (!bridge || !cc) { check(false, "bridge: a footbridge could be placed"); return; }
  const share = cc.bridge / Math.max(1, cc.bridge + cc.ford);
  log(`\nBRIDGE TEST (seed ${seed}): footbridge of ${bridge.cells.length} cells on row ${bridge.row} (ford at row ${w.map.fordY}); 4 days after`);
  log(`  crossings by bridge: ${cc.bridge}, by ford: ${cc.ford}; bridge share ${pct(share)}`);
  check(share >= 0.4, "bridge: the new footbridge takes at least 40% of river crossings");
  // Removing it sends traffic back to the ford.
  w.erase(bridge.cells.slice(0, 1));
  run(w, 1);
  const cc2 = new CrossingCounter(w);
  for (let i = 0; i < 2 * TPD; i++) { w.tick(); cc2.sample(w); }
  check(cc2.bridge === 0 && cc2.ford > 0 && w.bridges.length === 0, "bridge: erasing it returns all crossings to the ford");
}

// ------------------------------------------------------------------ gravel
/**
 * Gravel pulls a young trail onto it: on day 1, a 24-cell gravel strip laid
 * 3 cells to the side of a house's typical route to the market. The flow
 * preview should predict routes along it; after 6 days the strip should carry
 * far more footsteps than the same cells in a run without gravel, and most of
 * it should be worn to trail. (Gravel costs 0.8 of grass, so it cannot pull a
 * mature trunk, which costs as little as minSurface.)
 */
function gravelTest(seeds: number[]) {
  log(`\nGRAVEL TEST (seeds ${seeds.join(", ")}, 200 walkers): a 24-cell strip 3 cells beside a house-market route on day 1; 6 days later`);
  let pulled = 0, predicted = 0;
  for (const seed of seeds) {
    const a = new World(seed, 200, OFF), b = new World(seed, 200, OFF);
    run(a, 1); run(b, 1);
    const strip = besideRoute(a, a.houses[seed % a.houses.length], 24, 3);
    if (!strip.length) { log(`  seed ${seed}: no spot for a strip`); continue; }
    const pv = a.previewFlows({ gravel: strip }, 99);
    const along = strip.filter(c => pv.changes.some(ch => ch.reachable && distToPts((c % N) + 0.5, ((c / N) | 0) + 0.5, ch.newPts) <= 1.5)).length;
    if (along >= 0.5 * strip.length) predicted++;
    const laid = a.placeGravel(strip);
    run(a, 6); run(b, 6);
    const fa = a.metrics.window(), fb = b.metrics.window();
    const sa = strip.reduce((s, c) => s + fa[c], 0), sb = strip.reduce((s, c) => s + fb[c], 0);
    const ta = strip.filter(c => a.wear[c] >= 25).length, tb = strip.filter(c => b.wear[c] >= 25).length;
    const ok = sa >= 3 * Math.max(1, sb) && ta >= 0.5 * strip.length;
    if (ok) pulled++;
    log(`  seed ${seed}: ${laid} gravel cells, ${along} on predicted routes ("${describePreview(pv, k => a.buildings[k].kind)}"); footsteps on them in the last 2 days ${sa} (without gravel ${sb}); trail cells ${ta} (without ${tb}) ${ok ? "" : "  <- not pulled"}`);
  }
  check(predicted >= seeds.length - 1, `preview: the predicted routes run along the gravel strip (half its cells within 1.5 cells) on ${predicted} of ${seeds.length} seeds`);
  check(pulled >= seeds.length - 1, `gravel: a strip beside a young route pulls the trail onto it (3x the footsteps, half of it trail) on ${pulled} of ${seeds.length} seeds`);

  // No regrowth below trail: worn ground under gravel stays trail with no traffic.
  const w = new World(1, 50, OFF);
  let g = -1, h = -1;
  for (let c = 0; c < NC && (g < 0 || h < 0); c++) {
    const x = c % N, y = (c / N) | 0;
    if (!w.land[c] || x < 3 || x > 6 || y < 3 || y > 124 || doorDistance(w, c) < 20) continue;
    if (g < 0) g = c; else if (Math.abs(c - g) > 2 * N) h = c;
  }
  w.placeGravel([g]);
  w.wear[g] = w.wear[h] = 60;
  run(w, 20);
  check(w.wear[g] >= 25 && w.wear[h] < 25, `gravel: worn ground under it does not grow back below trail (wear ${w.wear[g].toFixed(0)} under gravel, ${w.wear[h].toFixed(0)} beside it after 20 quiet days)`);
  check(w.erase([g]) === 1 && !w.gravel[g] && w.wear[g] >= 25, "gravel: erasing it leaves the trail beneath");
}

/** A straight line of `len` cells, `off` cells to one side of a chord of the house's typical route to the market. */
function besideRoute(w: World, house: number, len: number, off: number): number[] {
  const dest = w.kind("market")[0], B = w.buildings;
  const r = traceRoute(w.fields[dest], w.cost, B[house].door, B[dest].door), n = r.length / 2;
  for (let k = 1; k < n; k++) {
    const x0 = r[2 * k], y0 = r[2 * k + 1];
    let j = k;
    while (j < n - 1 && Math.hypot(r[2 * j] - x0, r[2 * j + 1] - y0) < len) j++;
    const x1 = r[2 * j], y1 = r[2 * j + 1], L = Math.hypot(x1 - x0, y1 - y0);
    if (L < len - 1) break;
    const px = (-(y1 - y0) / L) * off, py = ((x1 - x0) / L) * off;
    const line = line4(Math.round(x0 + px), Math.round(y0 + py), Math.round(x1 + px), Math.round(y1 + py));
    if (line.every(c => w.canGravel(c) && Math.abs((c % N) - w.map.riverX[(c / N) | 0]) >= w.map.riverHalfW + 3 && doorDistance(w, c) >= 5)) return line;
  }
  return [];
}

// ------------------------------------------------------------------ gate
/**
 * A gate in a fence: on day 20 a fence across the busiest trail (as in the
 * reroute test); 3 days later a gate where the trail crossed it. Walkers pass
 * through it at a time cost, and the trail re-forms through the gate.
 */
function gateTest(seed: number) {
  const w = new World(seed, 200, OFF);
  run(w, 20);
  const { cell: best, fence } = fenceAcrossBusiest(w);
  const bx = best % N, by = (best / N) | 0;
  const before = w.metrics.window()[best];
  w.placeFence(fence);
  run(w, 3);
  const gate = w.gateCellAt(bx, by);
  // Footsteps round the fence ends (cells within 3 of an end, not fence): the detour trail.
  const ends = [fence[0], fence[fence.length - 1]], round: number[] = [];
  for (const e of ends)
    for (let y = ((e / N) | 0) - 3; y <= ((e / N) | 0) + 3; y++)
      for (let x = (e % N) - 3; x <= (e % N) + 3; x++) if (inside(x, y) && !w.fence[idx(x, y)] && !round.includes(idx(x, y))) round.push(idx(x, y));
  const fw0 = w.metrics.window();
  const shut = fw0[gate], roundBefore = round.reduce((s, c) => s + fw0[c], 0);
  const pv = w.previewFlows({ gate: [gate] }, 99);
  const placed = w.placeGate(gate);
  run(w, 6);
  const fw = w.metrics.window();
  const gx = gate % N, gy = (gate / N) | 0;
  const along = w.fence[idx(gx + 1, gy)] || w.fence[idx(gx - 1, gy)]; // fence runs east-west: the way through is north-south
  const through = [-2, -1, 1, 2].map(t => (along ? idx(gx, gy + t) : idx(gx + t, gy)));
  const trail = [gate, ...through].every(c => w.wear[c] >= 25);
  const others = fence.filter(c => c !== gate).reduce((s, c) => s + fw[c], 0);
  const roundAfter = round.reduce((s, c) => s + fw[c], 0);
  const extra = w.cellCost(gate) - w.cellCost(gate, Ov.Open);
  log(`\nGATE TEST (seed ${seed}): fence of ${fence.length} cells across the busiest trail at (${bx},${by}); gate at (${gx},${gy}) 3 days later; 6 days after`);
  log(`  footsteps (2-day window): on the trail cell before the fence ${before}, on the gate cell while shut ${shut}, through the gate ${fw[gate]}, on the other fence cells ${others}; round the fence ends ${roundBefore} before the gate, ${roundAfter} after`);
  log(`  wear at the gate and 2 cells either side: ${[gate, ...through].map(c => w.wear[c].toFixed(0)).join(" ")}; extra step cost of the gate ${extra.toFixed(2)}`);
  log(`  preview before placing it: "${describePreview(pv, b => w.buildings[b].kind)}"`);
  check(placed, "gate: a gate can be placed on the fence");
  check(pv.reroutedPerDay > 0 && pv.meanCostRatio < 1, "preview: a gate predicts shorter routes through it");
  check(fw[gate] >= 0.5 * before && others === 0, "gate: walkers pass through the gate (at least half the traffic the trail had) and nowhere else on the fence");
  check(trail && roundAfter <= 0.5 * roundBefore, "gate: the trail runs through the gate again and the detour round the fence ends loses at least half its traffic");
  check(Math.abs(extra - PARAMS.gateCost) < 0.01, `gate: passing costs ${PARAMS.gateCost} more than open ground`);
  w.removeGate(gate);
  let onGate = 0;
  for (let i = 0; i < TPD; i++) { w.tick(); for (const k of w.walkers) if (w.isVisible(k) && k.cell === gate) onGate++; }
  check(w.fence[gate] === 1 && onGate === 0, "gate: removing the gate closes the fence again");
}

/** The busiest trail cell well away from doors and the river, and a 15-cell fence across it. */
function fenceAcrossBusiest(w: World): { cell: number; fence: number[] } {
  const fw = w.metrics.window();
  let best = -1;
  for (let c = 0; c < NC; c++) {
    if (!w.land[c] || w.wear[c] < 25 || w.map.crop[c]) continue;
    const x = c % N, y = (c / N) | 0;
    if (x < 10 || y < 10 || x > N - 11 || y > N - 11) continue;
    if (Math.abs(x + 0.5 - w.map.riverX[y]) < w.map.riverHalfW + 8 || doorDistance(w, c) < 10) continue;
    if (best < 0 || fw[c] > fw[best]) best = c;
  }
  const bx = best % N, by = (best / N) | 0;
  // Trail direction: principal axis of trail cells within 4 cells, weighted by traffic.
  let sxx = 0, syy = 0, sxy = 0;
  for (let y = by - 4; y <= by + 4; y++)
    for (let x = bx - 4; x <= bx + 4; x++) {
      if (!inside(x, y)) continue;
      const c = idx(x, y), m = fw[c];
      if (w.wear[c] < 25 || !m) continue;
      sxx += m * (x - bx) ** 2; syy += m * (y - by) ** 2; sxy += m * (x - bx) * (y - by);
    }
  const ang = 0.5 * Math.atan2(2 * sxy, sxx - syy); // along the trail
  const px = -Math.sin(ang), py = Math.cos(ang); // across it
  const H = 7;
  return { cell: best, fence: line4(Math.round(bx - px * H), Math.round(by - py * H), Math.round(bx + px * H), Math.round(by + py * H)) };
}

// ------------------------------------------------------------------ robustness
function robustnessTest() {
  const w = new World(5, 300, OFF);
  run(w, 4);
  // Wall a house in completely, fence a crowded strip, then check every walker.
  const h = w.buildings[w.houses[0]];
  const ring: number[] = [];
  for (let x = h.x - 2; x <= h.x + h.w + 1; x++) ring.push(idx(x, h.y - 2), idx(x, h.y + h.h + 1));
  for (let y = h.y - 2; y <= h.y + h.h + 1; y++) ring.push(idx(h.x - 2, y), idx(h.x + h.w + 1, y));
  w.placeFence(ring);
  // A fence right through the busiest cells, with walkers on them.
  const fw = w.metrics.window();
  const busy = [...Array(NC).keys()].filter(c => w.land[c]).sort((a, b) => fw[b] - fw[a]).slice(0, 40);
  w.placeFence(busy);
  let bad = 0, stuckLong = 0;
  for (let i = 0; i < 3 * TPD; i++) {
    w.tick();
    for (const k of w.walkers) {
      if (w.isVisible(k) && w.blocked(k.cell)) bad++;
      if (k.stuck > PARAMS.stuckSimSec + 0.2) stuckLong++;
    }
  }
  log(`\nROBUSTNESS: house walled in and 40 busy cells fenced; walker-ticks on blocked cells: ${bad}; stuck past limit: ${stuckLong}`);
  check(bad === 0, "robustness: no walker ever stands on a blocked cell");
  check(stuckLong === 0, "robustness: no walker stays stuck past the limit");
  w.erase(ring.concat(busy));
  run(w, 2);
  check(w.fence.every(v => v === 0) && w.stats().tripsInWindow > 50, "robustness: erasing restores normal traffic");
}

function determinismTest() {
  const a = new World(9, 250, OFF), b = new World(9, 250, OFF);
  const act = (w: World) => { run(w, 3); w.placeFence(line4(30, 30, 50, 60)); w.placeBridge(w.map.riverX[20], 20); run(w, 2); };
  act(a); act(b);
  let same = true;
  for (let i = 0; i < NC; i++) if (a.wear[i] !== b.wear[i]) { same = false; break; }
  check(same && a.walkers.every((k, i) => k.x === b.walkers[i].x), "determinism: same seed and inputs give an identical run");
}

// ------------------------------------------------------------------ flow preview
/** The typical route from a house to the market now, and a fence across it about `at` of the way along. */
function fenceAcrossRoute(w: World, house: number, at: number, half = 7): number[] {
  const dest = w.kind("market")[0], B = w.buildings;
  const r = traceRoute(w.fields[dest], w.cost, B[house].door, B[dest].door);
  const n = r.length / 2;
  // Walk along from `at` until a spot that is well away from the river and doors.
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

function rngState(w: World): number[] {
  const rng = (w as unknown as { rng: () => number }).rng;
  return [rng(), rng(), rng()];
}

function previewIsolationTest() {
  const a = new World(3, 250, OFF), b = new World(3, 250, OFF);
  run(a, 2); run(b, 2);
  let calls = 0;
  for (let i = 0; i < 3 * TPD; i++) {
    a.tick(); b.tick();
    if (i % 2 === 0) {
      // Fences, bridges and erases, as the page asks while a player drags.
      const k = calls++ % 4, y = 20 + (calls % 80);
      if (k === 0) b.previewFlows({ block: line4(10 + (calls % 50), 20, 30, 70).filter(c => b.canFence(c)) });
      else if (k === 1) b.previewFlows({ bridge: b.bridgeCellsAt(b.map.riverX[y], y) ?? [] });
      else if (k === 2) b.previewFlows({ open: [idx(40, y)] });
      else b.previewFlows({});
    }
  }
  const same = (x: ArrayLike<number>, y: ArrayLike<number>) => { for (let i = 0; i < x.length; i++) if (x[i] !== y[i]) return false; return x.length === y.length; };
  const ok = same(a.wear, b.wear) && same(a.cost, b.cost) && same(a.flow, b.flow) && same(a.tripRate, b.tripRate) &&
    a.fields.every((f, t) => same(f, b.fields[t])) && a.walkers.every((k, i) => k.x === b.walkers[i].x && k.y === b.walkers[i].y && k.cell === b.walkers[i].cell) &&
    same(rngState(a), rngState(b));
  log(`\nFLOW PREVIEW: isolation (seed 3, 250 walkers, ${calls} preview queries over 3 days)`);
  check(calls >= 200 && ok, "preview: queries leave wear, costs, fields, walkers and the RNG stream byte-identical");
}

function previewBasicsTest() {
  log("\nFLOW PREVIEW: basics (seed 2, 200 walkers, day 20)");
  const w = new World(2, 200, OFF);
  run(w, 20);
  const empty = w.previewFlows({});
  check(empty.changes.length === 0 && empty.reroutedPerDay === 0, "preview: an empty change shows no changed routes");

  // Speed: a full preview (base fields stale) and a hypothetical-only one.
  const fence = fenceAcrossRoute(w, w.houses[0], 0.3);
  const times: number[] = [], hypTimes: number[] = [];
  for (let i = 0; i < 6; i++) {
    run(w, 0.1); // the base is rebuilt after a second of sim time
    let t = performance.now(); w.previewFlows({ block: fence }); times.push(performance.now() - t);
    t = performance.now(); w.previewFlows({ block: fence.slice(1) }); hypTimes.push(performance.now() - t);
  }
  times.sort((x, y) => x - y); hypTimes.sort((x, y) => x - y);
  log(`  preview time in Node: full ${times[3].toFixed(1)} ms median (worst ${times[5].toFixed(1)}), with cached base ${hypTimes[3].toFixed(1)} ms median`);
  check(times[3] < 20, "preview: a full preview takes under 20 ms (median) in Node");

  // Bridge: the preview route crosses it, and the built bridge gets walkers.
  const market = w.buildings[w.kind("market")[0]];
  const my = (market.door / N) | 0;
  let cells: number[] | null = null, row = -1;
  for (let d = 0; d < 40 && !cells; d++)
    for (const y of [my + d, my - d])
      if (!cells && y > 3 && y < N - 4 && Math.abs(y - w.map.fordY) >= 8) { cells = w.bridgeCellsAt(w.map.riverX[y], y); row = y; }
  const pb = w.previewFlows({ bridge: cells! });
  const onBridge = (pts: Float32Array) => { for (let i = 0; i < pts.length; i += 2) if (cells!.includes(Math.floor(pts[i + 1]) * N + Math.floor(pts[i]))) return true; return false; };
  const crossing = pb.changes.filter(c => onBridge(c.newPts)).length;
  log(`  bridge on row ${row}: ${pb.changes.length} changed routes shown, ${crossing} cross the bridge; "${describePreview(pb, b => w.buildings[b].kind)}"`);
  check(crossing > 0, "preview: a footbridge's predicted routes cross the bridge");
  w.placeBridge(w.map.riverX[row], row);
  run(w, 2);
  const fw = w.metrics.window();
  const steps = cells!.reduce((s, c) => s + fw[c], 0);
  check(steps > 0, `preview: the bridge then carries walkers (${steps} footsteps in 2 days)`);

  // Unreachable: a fence ring round the well's door.
  const well = w.buildings[w.kind("well")[0]];
  const ring: number[] = [];
  for (let x = well.x - 2; x <= well.x + well.w + 1; x++) ring.push(idx(x, well.y - 2), idx(x, well.y + well.h + 1));
  for (let y = well.y - 2; y <= well.y + well.h + 1; y++) ring.push(idx(well.x - 2, y), idx(well.x + well.w + 1, y));
  const pu = w.previewFlows({ block: ring.filter(c => w.canFence(c)) });
  const blockedWell = pu.blocked.find(b => b.dest === well.id);
  log(`  fence ring round the well: "${describePreview(pu, b => (w.buildings[b].kind === "well" ? "Well" : w.buildings[b].kind))}"`);
  check(!!blockedWell && pu.changes.some(c => c.dest === well.id && !c.reachable), "preview: enclosing a destination is shown as unreachable");
}

/**
 * Agreement with the walkers: place a fence across a busy route, then check
 * that the predicted detour is where the new trail forms, and that the readout
 * matches the trips per day that actually take the new route.
 */
function previewAgreementTest(seeds: number[]) {
  log(`\nFLOW PREVIEW: agreement with the walkers (seeds ${seeds.join(", ")}, 200 walkers, fence placed on day 20)`);
  log("seed | fence | pairs | readout       | detour points near new trail | trips/day after: all  new route | predicted/day");
  let predicted = 0, measured = 0, detourPts = 0, detourNear = 0, eraseShorter = 0;
  for (const seed of seeds) {
    const w = new World(seed, 200, OFF);
    run(w, 20);
    const fence = fenceAcrossRoute(w, w.houses[seed % w.houses.length], 0.3);
    const pv: FlowPreview = w.previewFlows({ block: fence }, 99);
    const before = Float32Array.from(w.wear);
    w.placeFence(fence);
    const B = w.buildings.length;
    // Predicted routes by (house, destination, explorer or not): the preview splits each
    // pair's trips between the house's explorers and its other walkers, so each trip is
    // checked against its own group's route.
    const routes = new Map<number, Float32Array>();
    const keyOf = (home: number, dest: number, explorers: boolean) => (home * B + dest) * 2 + (explorers ? 1 : 0);
    for (const c of pv.changes) routes.set(keyOf(c.home, c.dest, c.explorers), c.newPts);
    // Follow every trip of a changed pair for 6 days: count departures, and
    // classify completed trips by whether they kept within 3 cells of the
    // predicted route for at least 80% of their positions.
    const DAYS_AFTER = 6;
    const track = new Map<number, number[]>(), prev = w.walkers.map(k => k.state), keys = new Map<number, number>();
    let departures = 0, done = 0, onNew = 0;
    for (let i = 0; i < DAYS_AFTER * TPD; i++) {
      w.tick();
      w.walkers.forEach((k, j) => {
        if (k.state === 1 && prev[j] !== 1) {
          const key = keyOf(k.home, k.target, k.explorer);
          if (routes.has(key)) { track.set(k.id, []); keys.set(k.id, key); departures++; }
        }
        prev[j] = k.state;
        const tr = track.get(k.id);
        if (!tr) return;
        if (k.state === 1) { if (i % 3 === 0) tr.push(k.x, k.y); return; }
        track.delete(k.id);
        const route = routes.get(keys.get(k.id)!)!;
        let near = 0;
        for (let q = 0; q < tr.length; q += 2) if (distToPts(tr[q], tr[q + 1], route) <= 3) near++;
        done++;
        if (near >= 0.8 * (tr.length / 2)) onNew++;
      });
    }
    const perDay = (departures / DAYS_AFTER) * (done ? onNew / done : 0);
    predicted += pv.reroutedPerDay; measured += perDay;
    // Where the predicted route leaves the old one, is there new trail within 3 cells?
    let pts = 0, near = 0;
    for (const c of pv.changes.filter(x => x.reachable)) {
      for (let q = 0; q < c.newPts.length; q += 2) {
        if (distToPts(c.newPts[q], c.newPts[q + 1], c.oldPts) <= 3) continue;
        pts++;
        const cx = Math.floor(c.newPts[q]), cy = Math.floor(c.newPts[q + 1]);
        let ok = false;
        for (let y = cy - 3; y <= cy + 3 && !ok; y++)
          for (let x = cx - 3; x <= cx + 3 && !ok; x++)
            if (inside(x, y) && Math.hypot(x - cx, y - cy) <= 3 && w.wear[idx(x, y)] >= 25 && before[idx(x, y)] < 25) ok = true;
        if (ok) near++;
      }
    }
    detourPts += pts; detourNear += near;
    // Erase: hovering erase over the whole fence predicts shorter routes again.
    const pe = w.previewFlows({ open: fence });
    if (pe.changes.some(c => c.reachable && c.costRatio < 1)) eraseShorter++;
    log(`${String(seed).padStart(4)} | ${String(fence.length).padStart(5)} | ${String(pv.changes.length).padStart(5)} | ${describePreview(pv, b => w.buildings[b].kind).padEnd(13).slice(0, 34)} | ${String(near).padStart(4)} of ${String(pts).padStart(4)} ` +
      `                | ${(departures / DAYS_AFTER).toFixed(1).padStart(5)} ${perDay.toFixed(1).padStart(10)} | ${pv.reroutedPerDay.toFixed(1).padStart(8)}`);
  }
  const share = detourNear / Math.max(1, detourPts), err = measured ? predicted / measured - 1 : Infinity;
  log(`  all seeds: ${(share * 100).toFixed(0)}% of predicted detour points lie within 3 cells of new trail; predicted ${predicted.toFixed(1)} vs measured ${measured.toFixed(1)} rerouted trips a day (${err >= 0 ? "+" : ""}${(err * 100).toFixed(0)}%)`);
  check(detourPts > 0 && share >= 0.8, "preview: at least 80% of the predicted detour lies within 3 cells of the trail that forms (3 days later)");
  check(eraseShorter >= seeds.length - 1, `preview: erasing the fence 6 days later predicts shorter routes (${eraseShorter} of ${seeds.length} seeds)`);
  check(Math.abs(err) <= 0.15, "preview: the readout is within 15% of the trips per day that take the new route (summed over seeds)");
}

function distToPts(x: number, y: number, pts: Float32Array): number {
  let best = Infinity;
  for (let i = 0; i < pts.length; i += 2) best = Math.min(best, (x - pts[i]) ** 2 + (y - pts[i + 1]) ** 2);
  return Math.sqrt(best);
}

// ------------------------------------------------------------------ main
const GATE_SETS: [string, number][] = [["seeds 401-420", 401], ["seeds 1-20", 1], ["seeds 101-120", 101], ["seeds 201-220", 201], ["seeds 301-320", 301]];
/**
 * Round 4: the gate with rain and house growth on at the player's defaults
 * (200 walkers to start; growth adds households up to the Hamlet target), on
 * seeds 601 to 620, first run only after the round 4 rules and numbers were final.
 */
const LIVE_FIRST = 601;

/** The live set's table: the gate columns plus houses, population and rain days (report only). */
function printLiveTable(title: string, rows: Row[]): void {
  rows.sort((a, b) => a.seed - b.seed);
  console.log(`\n${title}`);
  console.log(`(rain and house growth on at the player's defaults; 200 walkers at the start; ${DAYS} in-game days; trunk share sampled daily, days ${SAMPLE_FROM} to ${DAYS}; detour, coverage, first trail and houses are report only)`);
  console.log("seed | first trail | detour | coverage | trunk mean   min    max  | out of band | houses | population | rain days");
  for (const r of rows)
    console.log(`${String(r.seed).padStart(4)} | ${r.ft < 0 ? "   none" : (r.ft.toFixed(0) + " s").padStart(7)}     | ${r.det.toFixed(3)}  |  ${pct(r.cov)}  |     ${pct(r.mean)} ${pct(r.min)} ${pct(r.max)} | ${String(r.below).padStart(4)} days   | ${String(r.houses).padStart(6)} | ${String(r.finalWalkers).padStart(10)} | ${String(r.rainDays).padStart(9)}`);
  const col = (k: keyof Row) => rows.map(r => r[k] as number);
  console.log(`all  | ${(mean(col("ft")).toFixed(1) + " s").padStart(7)} avg | ${mean(col("det")).toFixed(3)}  |  ${pct(mean(col("cov")))}  |     ${pct(mean(col("mean")))} ${pct(Math.min(...col("min")))} ${pct(Math.max(...col("max")))} | ${String(rows.filter(r => r.below > 0).length).padStart(4)} seeds  | ${mean(col("houses")).toFixed(1).padStart(6)} | ${mean(col("finalWalkers")).toFixed(0).padStart(10)} | ${mean(col("rainDays")).toFixed(1).padStart(9)}`);
  console.log(`     detour within 1.05-1.30 on ${rows.filter(r => r.det >= 1.05 && r.det <= 1.3).length} of ${rows.length}; coverage within 3%-8% on ${rows.filter(r => r.cov >= 0.03 && r.cov <= 0.08).length} of ${rows.length}; first trail within 45 s on ${rows.filter(r => r.ft >= 0 && r.ft <= 45).length} of ${rows.length}`);
}

async function main() {
  const t0 = performance.now();
  const quick = process.argv.includes("--quick");
  const range = (a: number, n: number) => Array.from({ length: n }, (_, i) => a + i);
  const threads = Math.max(1, Math.min(4, availableParallelism() - 1));
  const n = quick ? 4 : 20;
  const sets = quick ? GATE_SETS.slice(0, 1) : GATE_SETS;
  const jobs: Job[] = [
    ...sets.flatMap(([, first]) => range(first, n).map(seed => ({ kind: "trunk" as const, seed, walkers: 200 }))),
    ...(quick ? [] : range(1, 10).map(seed => ({ kind: "bridge" as const, seed }))),
    ...(quick ? [] : range(1, 5).map(seed => ({ kind: "trunk" as const, seed, walkers: 500 }))),
    ...(quick ? [] : range(LIVE_FIRST, 20).map(seed => ({ kind: "trunk" as const, seed, walkers: 200, live: true }))),
  ];
  const tables = runSeedsInWorkers(jobs, threads);

  log("\nCHECKS");
  rerouteTest(1);
  rerouteTest(2);
  bridgeTest(1);
  bridgeTest(2);
  gravelTest([1, 2, 3, 4, 5, 6]);
  gateTest(1);
  gateTest(2);
  robustnessTest();
  determinismTest();
  previewIsolationTest();
  previewBasicsTest();
  previewAgreementTest([1, 2, 3, 4, 5, 6]);
  // Round 4 rules (test/rules.test.ts).
  gateAngleTests(check, log);
  gateRefusalTests(check, log);
  fenceOverGravelTest(check);
  gravelNoteTest(check, log);
  rainTests(check, log);
  growthTests(check, log);
  newDeterminismTest(check);
  newIsolationTest(check, log);

  const results = await tables;
  const rows = results.filter((r): r is Row & { kind: "trunk" } => r.kind === "trunk");
  const pick = (a: number, b: number, walkers = 200) => rows.filter(r => r.seed >= a && r.seed <= b && r.walkers === walkers);
  const live = pick(LIVE_FIRST, LIVE_FIRST + 19);
  const gate = pick(401, 400 + n);
  printTable(`GATE G3, FRESH SEEDS 401 TO ${400 + n} (never run before round 3's model was final): every daily sample must lie within 30% to 70%`, gate);
  console.log(`\nGATE G3, ALL SETS (200 walkers, every daily sample from day ${SAMPLE_FROM} to ${DAYS} within 30% to 70%; tuning sets for round 3: 1-20, 101-120, 201-220)`);
  for (const [name, first] of sets) console.log("  " + summaryLine(name, pick(first, first + n - 1)));
  if (!quick) {
    const bridges = results.filter((r): r is BridgeRow => r.kind === "bridge").sort((a, b) => a.seed - b.seed);
    const shares = bridges.map(b => b.share);
    console.log(`\nFOOTBRIDGE SHARE (report only; seeds 1 to 10, 200 walkers: on day 15 a footbridge on the market door's row, or the nearest row that takes one; share of river crossings over 4 days, 1 day after)`);
    console.log("  " + bridges.map(b => `seed ${b.seed} ${b.share < 0 ? "no bridge" : `${(b.share * 100).toFixed(0)}% (${b.bridge}/${b.bridge + b.ford})`}`).join(", "));
    console.log(`  mean ${pct(mean(shares))}, lowest ${pct(Math.min(...shares))}`);
    printTable("500 WALKERS (seeds 1 to 5, report only)", pick(1, 5, 500));
    printLiveTable(`GATE G3 WITH RAIN AND HOUSE GROWTH, SEEDS ${LIVE_FIRST} TO ${LIVE_FIRST + 19} (first run after the round 4 rules were final): every daily sample must lie within 30% to 70%`, live);
    console.log("  " + summaryLine(`seeds ${LIVE_FIRST}-${LIVE_FIRST + 19} live`, live));
  }
  console.log(out.join("\n"));
  console.log("\nGATE CHECKS");
  out.length = 0;
  for (const [name, first] of sets) {
    const r = pick(first, first + n - 1);
    check(r.length === n && r.every(x => x.min >= 0.3 && x.max <= 0.7), `G3 ${name}: every daily trunk-share sample (days ${SAMPLE_FROM} to ${DAYS}) within 30%-70% on all ${r.length} seeds`);
  }
  check(gate.every(r => r.ft >= 0 && r.ft <= 3 * PARAMS.daySimSec), `a first trail forms within 3 in-game days (45 s at 1x) on every seed of 401 to ${400 + n}`);
  if (!quick)
    check(live.length === 20 && live.every(x => x.min >= 0.3 && x.max <= 0.7), `G3 seeds ${LIVE_FIRST}-${LIVE_FIRST + 19} with rain and house growth on (player defaults): every daily trunk-share sample (days ${SAMPLE_FROM} to ${DAYS}) within 30%-70% on all ${live.length} seeds`);
  if (!quick) {
    // With rain and growth off, the five old sets must give the round 3 numbers bit for bit.
    const baseline = readFileSync(new URL("../test/g3-baseline.txt", import.meta.url), "utf8"); // the bundle runs from .cache/
    const now = sets.map(([name, first]) => exactTable(`G3 ${name} (200 walkers, rain and growth off)`, pick(first, first + n - 1)) + "\n").join("\n");
    const same = now.trim() === baseline.trim();
    if (!same) console.log(`\nEXACT TABLES NOW (differ from test/g3-baseline.txt):\n${now}`);
    check(same, "G3 old sets with rain and growth off: per-seed numbers bit-identical to the round 3 baseline (test/g3-baseline.txt)");
  }
  console.log(out.join("\n"));
  console.log(`\n${failures.length ? `FAILED: ${failures.length} check(s)` : "ALL CHECKS PASSED"} in ${((performance.now() - t0) / 1000).toFixed(0)} s (${threads} worker threads)${quick ? " [quick mode: 4 fresh gate seeds, no other sets or reports]" : ""}`);
  if (failures.length) process.exitCode = 1;
}

if (!isMainThread) {
  // Worker thread: run the jobs the main thread sends, one at a time.
  parentPort!.on("message", (job: Job) => parentPort!.postMessage(runJob(job)));
} else await main();

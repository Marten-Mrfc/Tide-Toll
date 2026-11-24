// Headless simulation tests (npm test). Prints a per-seed table for the step 1
// gate and runs the reroute, bridge, robustness and determinism checks.
// Exit code is non-zero if any check fails.
import { World } from "../src/sim/world";
import { PARAMS } from "../src/sim/params";
import { N, NC, idx, inside, line4 } from "../src/sim/grid";

declare const process: { exitCode?: number; argv: string[] };

const TPD = PARAMS.daySimSec * PARAMS.tickHz; // ticks per in-game day
const failures: string[] = [];
const check = (ok: boolean, what: string) => {
  console.log(`  ${ok ? "PASS" : "FAIL"}  ${what}`);
  if (!ok) failures.push(what);
};
const pct = (v: number) => (v * 100).toFixed(1).padStart(5) + "%";
const run = (w: World, days: number) => { for (let i = 0; i < days * TPD; i++) w.tick(); };

// ------------------------------------------------------------------ gate table
// Trunk share over a 2-day window still wobbles from day to day, so each seed
// is judged on its settled mean (daily samples, days 20 to 30). The minimum
// daily sample is printed as well.
const SETTLE_FROM = 20, DAYS = 30;
function gateTable(walkers: number, seeds: number, gated: boolean) {
  console.log(`\nSTEP 1 GATE TABLE: ${walkers} walkers, ${seeds} seeds, ${DAYS} in-game days (1 day = ${PARAMS.daySimSec} s at 1x)`);
  console.log("seed | first trail      | detour | coverage | trunk (mean, min) days 20-30 | trunk D (mean)");
  const all: number[][] = [];
  for (let seed = 1; seed <= seeds; seed++) {
    const w = new World(seed, walkers);
    const trunk: number[] = [], trunkD: number[] = [], det: number[] = [], cov: number[] = [];
    for (let d = 1; d <= DAYS; d++) {
      run(w, 1);
      if (d >= SETTLE_FROM) {
        const s = w.stats();
        trunk.push(s.trunkShare); trunkD.push(s.trunkShareD); det.push(s.detourRatio); cov.push(s.trailCoverage);
      }
    }
    const mean = (a: number[]) => a.reduce((x, y) => x + y, 0) / a.length;
    const ft = w.metrics.firstTrailTime;
    const row = [seed, ft, mean(det), mean(cov), mean(trunk), Math.min(...trunk), mean(trunkD)];
    all.push(row);
    console.log(
      `${String(seed).padStart(4)} | ${ft < 0 ? "  none" : (ft.toFixed(0) + " s").padStart(6)} (${ft < 0 ? "-" : (ft / PARAMS.daySimSec).toFixed(1)} days) | ` +
      `${row[2].toFixed(3)}  | ${pct(row[3])}   | ${pct(row[4])}  ${pct(row[5])}               | ${pct(row[6])}`);
  }
  const col = (i: number) => all.map(r => r[i]);
  const avg = (a: number[]) => a.reduce((x, y) => x + y, 0) / a.length;
  console.log(`mean | ${avg(col(1)).toFixed(1)} s | ${avg(col(2)).toFixed(3)}  | ${pct(avg(col(3)))}   | ${pct(avg(col(4)))}  ${pct(Math.min(...col(5)))} (lowest)      | ${pct(avg(col(6)))}`);
  if (!gated) return;
  check(all.every(r => r[4] >= 0.3 && r[4] <= 0.7), `trunk share (settled mean) within 30%-70% on all ${seeds} seeds at ${walkers} walkers`);
  check(all.every(r => r[1] >= 0 && r[1] <= 3 * PARAMS.daySimSec), "a first trail forms within 3 in-game days (45 s at 1x) on every seed");
  // The detour band is a spec health target, not a gate item, so it is reported only.
  const inBand = all.filter(r => r[2] >= 1.05 && r[2] <= 1.3).length;
  console.log(`  INFO  detour ratio within the 1.05-1.30 target on ${inBand} of ${seeds} seeds`);
}

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
  const w = new World(seed, 200);
  run(w, 20);
  const fw = w.metrics.window();
  // The busiest trail cell well away from doors and the river.
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
  const fence = line4(Math.round(bx - px * H), Math.round(by - py * H), Math.round(bx + px * H), Math.round(by + py * H));
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
  console.log(`\nREROUTE TEST (seed ${seed}): fence of ${placed} cells across the busiest trail at (${bx},${by}), 6 days after placing`);
  console.log(`  footsteps on fence cells: ${fenceSteps}; walker-ticks on fence cells: ${onFence}; trips in window before/after: ${tripsBefore}/${tripsAfter}`);
  console.log(`  new trail round the fence ends: ${newTrailAt < 0 ? "not formed" : `formed after ${newTrailAt.toFixed(1)} s sim (${(newTrailAt / PARAMS.daySimSec).toFixed(1)} days)`}`);
  check(placed >= 10, "reroute: fence placed across the trail");
  check(fenceSteps === 0 && onFence === 0, "reroute: traffic across the fenced line drops to zero");
  check(newTrailAt >= 0 && newTrailAt <= 3 * PARAMS.daySimSec, "reroute: a new trail forms round the fence within 3 in-game days (45 s at 1x)");
  check(tripsAfter >= 0.7 * tripsBefore, "reroute: trips keep completing (at least 70% of the rate before)");
}

// ------------------------------------------------------------------ bridge
function bridgeTest(seed: number) {
  const w = new World(seed, 200);
  run(w, 15);
  // Bridge on the row of the market door, or the nearest row that takes one.
  const market = w.buildings.find(b => b.kind === "market")!;
  const my = (market.door / N) | 0;
  let bridge = null;
  for (let d = 0; d < 40 && !bridge; d++)
    for (const y of [my + d, my - d]) {
      if (bridge || y < 4 || y > N - 5 || Math.abs(y - w.map.fordY) < 8) continue;
      bridge = w.placeBridge(w.map.riverX[y], y);
    }
  if (!bridge) { check(false, "bridge: a footbridge could be placed"); return; }
  run(w, 1); // let walkers re-plan and finish trips already under way
  const cc = new CrossingCounter(w);
  for (let i = 0; i < 4 * TPD; i++) { w.tick(); cc.sample(w); }
  const share = cc.bridge / Math.max(1, cc.bridge + cc.ford);
  console.log(`\nBRIDGE TEST (seed ${seed}): footbridge of ${bridge.cells.length} cells on row ${bridge.row} (ford at row ${w.map.fordY}); 4 days after`);
  console.log(`  crossings by bridge: ${cc.bridge}, by ford: ${cc.ford}; bridge share ${pct(share)}`);
  check(share >= 0.4, "bridge: the new footbridge takes at least 40% of river crossings");
  // Removing it sends traffic back to the ford.
  w.erase(bridge.cells.slice(0, 1));
  run(w, 1);
  const cc2 = new CrossingCounter(w);
  for (let i = 0; i < 2 * TPD; i++) { w.tick(); cc2.sample(w); }
  check(cc2.bridge === 0 && cc2.ford > 0 && w.bridges.length === 0, "bridge: erasing it returns all crossings to the ford");
}

// ------------------------------------------------------------------ robustness
function robustnessTest() {
  const w = new World(5, 300);
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
  console.log(`\nROBUSTNESS: house walled in and 40 busy cells fenced; walker-ticks on blocked cells: ${bad}; stuck past limit: ${stuckLong}`);
  check(bad === 0, "robustness: no walker ever stands on a blocked cell");
  check(stuckLong === 0, "robustness: no walker stays stuck past the limit");
  w.erase(ring.concat(busy));
  run(w, 2);
  check(w.fence.every(v => v === 0) && w.stats().tripsInWindow > 50, "robustness: erasing restores normal traffic");
}

function determinismTest() {
  const a = new World(9, 250), b = new World(9, 250);
  const act = (w: World) => { run(w, 3); w.placeFence(line4(30, 30, 50, 60)); w.placeBridge(w.map.riverX[20], 20); run(w, 2); };
  act(a); act(b);
  let same = true;
  for (let i = 0; i < NC; i++) if (a.wear[i] !== b.wear[i]) { same = false; break; }
  check(same && a.walkers.every((k, i) => k.x === b.walkers[i].x), "determinism: same seed and inputs give an identical run");
}

// ------------------------------------------------------------------ main
const t0 = performance.now();
const quick = process.argv.includes("--quick");
gateTable(200, quick ? 4 : 20, true);
if (!quick) gateTable(500, 5, false);
console.log("\nCHECKS");
rerouteTest(1);
rerouteTest(2);
bridgeTest(1);
bridgeTest(2);
robustnessTest();
determinismTest();
console.log(`\n${failures.length ? `FAILED: ${failures.length} check(s)` : "ALL CHECKS PASSED"} in ${((performance.now() - t0) / 1000).toFixed(0)} s`);
if (failures.length) process.exitCode = 1;

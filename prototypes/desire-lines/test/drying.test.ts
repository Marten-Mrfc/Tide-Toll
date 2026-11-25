// The drawn drying of mud against the simulation's own wetness (round 6 fixes 1
// and 2). Drawing only: src/ui/ground.ts decides how wet each muddy cell is
// drawn (wetAmount, dryProgress) and in what order the cells dry (DryOrder); the
// mud's cost and slowdown follow world.weather.wetness. Imported by sim.test.ts,
// which collects the PASS/FAIL lines.
import { World } from "../src/sim/world";
import { PARAMS } from "../src/sim/params";
import { NC } from "../src/sim/grid";
import { wetAmount, dryProgress, DryOrder } from "../src/ui/ground";

type Check = (ok: boolean, what: string) => void;
type Log = (...a: unknown[]) => void;
const TPD = PARAMS.daySimSec * PARAMS.tickHz;

export function dryingTests(check: Check, log: Log) {
  log("\nDRAWN DRYING OF MUD (src/ui/ground.ts against the simulation's wetness)");
  const w = new World(4, 200, { rain: true, growth: false });
  for (let i = 0; i < 10 * TPD; i++) w.tick();
  w.stopRain(true);
  w.startRain();
  while (w.weather.raining) w.tick();
  const order = new DryOrder();
  const until = w.weather.mudUntil;
  let ticks = 0, drawnDryWhileSlow = 0, drawnWetterThanSlow = 0, clockAhead = 0, clockBehind = 0, wetWhenFast = 0, inWindow = 0;
  let opened = false, snapOk = false, prevWear: Float32Array | null = null;
  let mid: { dp: number; wet: number; light: number[]; heavy: number[]; lightMax: number; heavyMin: number; worn: number; wornDrawn: number; wornLive: number } | null = null;
  // Tick by tick through the mud days to dry ground (a rain day rolled meanwhile ends the run early).
  while (w.time < until + 1 && w.weather.rainStart < until - 2 * PARAMS.daySimSec + 1) {
    const dp = dryProgress(w), wet = w.weather.wetness, ord = order.wear(w, dp);
    ticks++;
    if (dp > 0 && wet >= 1) clockAhead++; // drawn drying before the walkers speed up
    if (dp <= 0 && wet < 1 && !w.weather.raining) clockBehind++; // walkers speeding up while it is drawn fully wet
    if (dp > 0 && dp < 1) inWindow++;
    if (dp > 0 && !opened) {
      opened = true;
      // The order is the wear as the window opened: the wear at the last moment before it.
      snapOk = prevWear !== null && ord.every((v, c) => v === prevWear![c]);
    }
    for (let c = 0; c < NC; c++) {
      if (!w.mud[c]) continue;
      const d = wetAmount(ord[c], dp, wet);
      if (wet >= 1 && d < 1) drawnDryWhileSlow++;
      if (d > wet + 1e-12) drawnWetterThanSlow++;
      if (wet <= 0 && d > 0) wetWhenFast++;
    }
    if (!mid && dp >= 0.5) {
      const light: number[] = [], heavy: number[] = [];
      for (let c = 0; c < NC; c++) if (w.mud[c]) { if (ord[c] < 32) light.push(c); else if (ord[c] >= 85) heavy.push(c); }
      // A light trail worn up to track while it dries keeps its place: still drawn dry.
      const worn = light[0], saved = w.wear[worn];
      w.wear[worn] = 95;
      const wornDrawn = wetAmount(order.wear(w, dp)[worn], dp, wet), wornLive = wetAmount(w.wear[worn], dp, wet);
      w.wear[worn] = saved;
      mid = { dp, wet, light, heavy, lightMax: Math.max(...light.map(c => wetAmount(ord[c], dp, wet))), heavyMin: Math.min(...heavy.map(c => wetAmount(ord[c], dp, wet))), worn, wornDrawn, wornLive };
    }
    prevWear = Float32Array.from(w.wear);
    w.tick();
  }
  log(`  seed 4, day 10, a forced shower: ${ticks} ticks from the rain's end to dry ground, ${inWindow} of them in the drying window (${PARAMS.drySimSec} s at ${PARAMS.tickHz} ticks a second)`);
  log(`  clock: drawn drying before the wetness falls on ${clockAhead} ticks, wetness falling before the drawn drying on ${clockBehind}`);
  log(`  muddy cells drawn less than fully wet at full wetness: ${drawnDryWhileSlow}; drawn wetter than the wetness: ${drawnWetterThanSlow}; drawn wet at zero wetness: ${wetWhenFast}`);
  check(opened && clockAhead === 0 && clockBehind === 0 && inWindow >= PARAMS.drySimSec * PARAMS.tickHz - 1 && inWindow <= PARAMS.drySimSec * PARAMS.tickHz + 1,
    "drying: the drawn drying runs on the simulation's clock (it starts as the wetness starts to fall and lasts the drying window)");
  check(drawnDryWhileSlow === 0 && drawnWetterThanSlow === 0 && wetWhenFast === 0 && w.weather.wetness === 0,
    "drying: mud is never drawn dry while the ground is fully wet (slow), nor wetter than the wetness that sets its cost and slowdown");
  if (mid) log(`  halfway (${(mid.dp * 100).toFixed(0)}% of the window, wetness ${mid.wet.toFixed(2)}): ${mid.light.length} light cells drawn at most ${mid.lightMax.toFixed(2)}, ${mid.heavy.length} busy cells at least ${mid.heavyMin.toFixed(2)}`);
  check(!!mid && mid.light.length >= 10 && mid.heavy.length >= 10 && mid.lightMax === 0 && mid.heavyMin > 0.4,
    "drying: inside the window the least-worn mud dries first and the busiest last");
  if (mid) log(`  a light cell worn up to 95 halfway through: drawn ${mid.wornDrawn.toFixed(2)} by the snapshot (${mid.wornLive.toFixed(2)} if it followed the live wear)`);
  check(snapOk && !!mid && mid.wornDrawn === 0 && mid.wornLive > 0,
    "drying: the order is the wear as the window opened (a snapshot), so a trail worn up while it dries keeps its place");
}

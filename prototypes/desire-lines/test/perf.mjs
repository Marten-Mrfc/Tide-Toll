// npm run perf: loads dist/index.html in headless Chromium and measures frame
// times for 10 s at 1x and 8x, with 500 and then 200 walkers (pass criteria),
// then the same 500-walker runs under 4x CPU throttling (report only), and the
// cost of a flow preview while a fence is dragged.
// Note: headless rAF is capped at 60 Hz, so the frame interval cannot show
// headroom above 60 fps; the in-page work time per frame shows the headroom.
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const here = dirname(fileURLToPath(import.meta.url));
const url = pathToFileURL(join(here, "..", "dist", "index.html")).href;
const SECONDS = Number(process.env.PERF_SECONDS || 10);

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 860 }, deviceScaleFactor: 1 });
const errors = [];
page.on("pageerror", e => errors.push(String(e)));
page.on("console", m => { if (m.type() === "error") errors.push(m.text()); });
await page.goto(url);
await page.waitForFunction(() => window.__tt);

const rows = [];
let ok = true;
for (const walkers of [500, 200]) {
  // Warm up at 8x so trails exist and every walker is out on the map.
  await page.evaluate(n => { __tt.reset(1); __tt.setWalkers(n); __tt.setSpeed(8); __tt.play(); }, walkers);
  await page.waitForTimeout(5000);
  for (const speed of [1, 8]) {
    await page.evaluate(s => __tt.setSpeed(s), speed);
    await page.waitForTimeout(500);
    await page.evaluate(() => __tt.perfStart());
    await page.waitForTimeout(SECONDS * 1000);
    const r = await page.evaluate(() => __tt.perfStop());
    const pass = r.fps >= 55 && r.worstIntervalMs < 100;
    ok = ok && pass;
    rows.push({ walkers, speed, ...r, pass });
  }
}
// 4x CPU throttling (report only): the frame budget should keep frames short.
const cdp = await page.context().newCDPSession(page);
await cdp.send("Emulation.setCPUThrottlingRate", { rate: 4 });
const throttled = [];
await page.evaluate(() => { __tt.reset(1); __tt.setWalkers(500); __tt.setSpeed(8); __tt.play(); });
await page.waitForTimeout(5000);
for (const speed of [1, 8]) {
  await page.evaluate(s => __tt.setSpeed(s), speed);
  await page.waitForTimeout(500);
  await page.evaluate(() => __tt.perfStart());
  await page.waitForTimeout(SECONDS * 1000);
  throttled.push({ walkers: 500, speed, ...(await page.evaluate(() => __tt.perfStop())) });
}
await cdp.send("Emulation.setCPUThrottlingRate", { rate: 1 });

// Flow preview while dragging a fence (200 walkers, 1x): frame times with the preview computing.
await page.evaluate(() => { __tt.reset(1); __tt.setWalkers(200); __tt.setSpeed(8); });
await page.waitForTimeout(4000);
await page.evaluate(() => __tt.setSpeed(1));
await page.keyboard.press("f");
// Across the busiest trail cell away from the river, so the preview has routes to change.
const busy = await page.evaluate(() => {
  const w = __tt.world, fw = w.metrics.window(); let b = -1;
  for (let c = 0; c < 128 * 128; c++) { const x = c % 128, y = (c / 128) | 0; if (!w.land[c] || Math.abs(x - w.map.riverX[y]) < 10 || x < 12 || x > 115 || y < 12 || y > 115) continue; if (b < 0 || fw[c] > fw[b]) b = c; }
  return [b % 128, (b / 128) | 0];
});
const a = await page.evaluate(([x, y]) => __tt.cellToClient(x - 7, y + 7), busy), b = await page.evaluate(([x, y]) => __tt.cellToClient(x + 7, y - 7), busy);
await page.mouse.move(a.x, a.y); await page.mouse.down();
await page.evaluate(() => __tt.perfStart());
for (let i = 0; i <= 40; i++) { await page.mouse.move(a.x + ((b.x - a.x) * i) / 40, a.y + ((b.y - a.y) * i) / 40); await page.waitForTimeout(50); }
await page.waitForTimeout(500);
const drag = await page.evaluate(() => __tt.perfStop());
const pstate = await page.evaluate(() => __tt.previewState());
await page.mouse.up();
await browser.close();

console.log(`PERF (headless Chromium, 1280x860, ${SECONDS} s per run; rAF capped at 60 Hz)`);
console.log("walkers speed | fps   avg frame  worst frame  >50ms | work avg  p95   worst | tick ms | sim s per s | result");
for (const r of rows) {
  console.log(`${String(r.walkers).padStart(7)} ${String(r.speed + "x").padStart(5)} | ${r.fps.toFixed(1).padStart(5)} ${r.avgIntervalMs.toFixed(2).padStart(7)} ms ${r.worstIntervalMs.toFixed(1).padStart(8)} ms  ${String(r.longFrames).padStart(5)} | ` +
    `${r.avgWorkMs.toFixed(2).padStart(6)} ${r.p95WorkMs.toFixed(2).padStart(6)} ${r.worstWorkMs.toFixed(1).padStart(6)} | ${r.tickMs.toFixed(2).padStart(7)} (worst frame of ticks ${r.worstTickFrameMs.toFixed(1)}) | ${r.simSecondsPerSecond.toFixed(2).padStart(11)} | ${r.pass ? "PASS" : "FAIL"}`);
}
console.log("\n4x CPU throttling (report only; frame budget: at most 10 ms of sim work per frame, dropped sim time shows as sim s per s below the speed)");
for (const r of throttled)
  console.log(`${String(r.walkers).padStart(7)} ${String(r.speed + "x").padStart(5)} | ${r.fps.toFixed(1).padStart(5)} ${r.avgIntervalMs.toFixed(2).padStart(7)} ms ${r.worstIntervalMs.toFixed(1).padStart(8)} ms  ${String(r.longFrames).padStart(5)} | ` +
    `${r.avgWorkMs.toFixed(2).padStart(6)} ${r.p95WorkMs.toFixed(2).padStart(6)} ${r.worstWorkMs.toFixed(1).padStart(6)} | ${r.tickMs.toFixed(2).padStart(7)} (worst frame of ticks ${r.worstTickFrameMs.toFixed(1)}, budget hit ${r.budgetHits}x) | ${r.simSecondsPerSecond.toFixed(2).padStart(11)}`);
console.log(`\nFLOW PREVIEW while dragging a fence (200 walkers, 1x, 2 s): ${drag.fps.toFixed(1)} fps, work avg ${drag.avgWorkMs.toFixed(2)} ms, p95 ${drag.p95WorkMs.toFixed(2)} ms, worst ${drag.worstWorkMs.toFixed(1)} ms, worst frame gap ${drag.worstIntervalMs.toFixed(1)} ms; readout "${pstate.text}"`);
console.log(`criteria: average >= 55 fps and no frame gap of 100 ms or more. Page errors: ${errors.length ? errors.join("; ") : "none"}`);
if (!ok || errors.length) process.exitCode = 1;

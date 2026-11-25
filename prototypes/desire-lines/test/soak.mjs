// npm run soak: the page with sound on at 8x for 10 minutes (SOAK_MINUTES to
// change it), 200 walkers to start, rain and growth at the player's defaults,
// counting live Web Audio nodes. A node is live from its create call until it
// is disconnected from everything (disconnect() with no arguments). The engine
// keeps a fixed set of nodes (buses, limiter, reverb, the 35 footstep lanes,
// the river bed) and must release every node of a cue, a theme note, a step
// and a rain bed when its source ends, so the count must not grow.
import { createRequire } from "node:module";
import { hostedUrl } from "./host.mjs";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const MINUTES = Number(process.env.SOAK_MINUTES || 10);
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1280, height: 860 }, deviceScaleFactor: 1 });
const errors = [];
page.on("pageerror", e => errors.push(String(e)));
page.on("console", m => { if (m.type() === "error" || m.type() === "warning") errors.push(m.text()); });
await page.addInitScript(() => {
  const live = new Set();
  let made = 0;
  window.__nodes = () => ({ live: live.size, made });
  const P = (window.BaseAudioContext || window.AudioContext).prototype;
  for (const k of ["createGain", "createBufferSource", "createStereoPanner", "createBiquadFilter", "createOscillator", "createConvolver", "createDynamicsCompressor", "createWaveShaper"]) {
    const f = P[k];
    P[k] = function (...a) { const n = f.apply(this, a); live.add(n); made++; return n; };
  }
  const d = AudioNode.prototype.disconnect;
  AudioNode.prototype.disconnect = function (...a) { if (a.length === 0) live.delete(this); return d.apply(this, a); };
});
await page.goto(hostedUrl());
await page.waitForFunction(() => window.__tt);
await page.evaluate(() => { __tt.reset(1); __tt.setSpeed(8); __tt.play(); });
const b = await page.locator("#btn-sound").boundingBox();
await page.mouse.click(b.x + b.width / 2, b.y + b.height / 2);

const rows = [];
const t0 = Date.now();
for (let m = 1; m <= MINUTES; m++) {
  // A shower now and then, and a tool cue, so the rain bed and the cues come and go.
  if (m % 3 === 1) await page.evaluate(() => __tt.startRain(1));
  await page.waitForTimeout(60000 - ((Date.now() - t0) % 60000));
  const r = await page.evaluate(() => ({ n: window.__nodes(), s: __tt.sound(), day: Math.floor(__tt.world.time / 15), rain: __tt.weather().rainDays }));
  rows.push({ m, ...r });
  console.log(`  minute ${String(m).padStart(2)}: ${String(r.n.live).padStart(4)} live nodes of ${String(r.n.made).padStart(6)} made; ${r.s.stats.steps} steps, ${r.s.stats.cues} cues, ${r.s.stats.bells} bells, at most ${r.s.stats.maxBeatSteps} steps a beat; day ${r.day}, ${r.rain} rain days; context ${r.s.ctxState}`);
}
await browser.close();

const first = rows[0].n.live, last = rows[rows.length - 1].n.live, made = rows[rows.length - 1].n.made, most = Math.max(...rows.map(r => r.n.live));
const ok = last <= first + 40 && most <= 300 && made > 10 * last && errors.length === 0 && rows.every(r => r.s.stats.maxBeatSteps <= 8);
console.log(`SOAK (${MINUTES} min at 8x, sound on): live nodes ${first} after a minute, ${last} at the end (most ${most}), ${made} made in all; ${ok ? "PASS" : "FAIL"}: live nodes must not grow (at most 40 above the first minute, never above 300), at most 8 steps a beat, no errors${errors.length ? ": " + errors.join(" | ") : ""}`);
if (!ok) process.exitCode = 1;

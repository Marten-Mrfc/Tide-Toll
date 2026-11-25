// npm run levels: renders the sound engine (src/ui/audio.ts) offline in headless
// Chromium (OfflineAudioContext, 48 kHz stereo, through the engine's own master
// chain, limiter and soft ceiling) and prints the level of each bed and cue and
// of a busy mix. Measured, not heard: nobody listens here.
// Levels: RMS of the mid channel in dBFS, over the whole measured span and over
// its loudest 400 ms block; peak is the highest absolute sample of either channel.
import { createRequire } from "node:module";
import * as esbuild from "esbuild";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const here = dirname(fileURLToPath(import.meta.url));
const bundle = await esbuild.build({ entryPoints: [join(here, "..", "src", "ui", "audio.ts")], bundle: true, format: "iife", globalName: "DA", write: false, target: "es2020" });
const code = bundle.outputFiles[0].text;

const browser = await chromium.launch();
const page = await browser.newPage();
await page.setContent("<p>levels</p>");
await page.addScriptTag({ content: code });

const scenes = await page.evaluate(async () => {
  const SR = 48000;
  const db = v => (v > 0 ? 20 * Math.log10(v) : -Infinity);
  /** Renders `dur` seconds; `setup(au, areas)` runs at 0, `each(au, areas, t)` every 50 ms; measures from `from`. */
  async function scene(dur, from, setup, each) {
    const ctx = new OfflineAudioContext(2, Math.round(dur * SR), SR);
    const au = DA.create(ctx, { volume: 0.7 });
    const areas = Array.from({ length: 8 }, (_, i) => ({ pan: ((i % 4) + 0.5) / 4 * 1.6 - 0.8, walkers: 0, surface: "grass", ford: 0 }));
    setup(au, areas);
    for (let t = 0.05; t < dur - 0.05; t += 0.05) {
      const at = t;
      ctx.suspend(at).then(() => { if (each) each(au, areas, at); au.beat(areas, areas.length); ctx.resume(); });
    }
    au.beat(areas, areas.length);
    const buf = await ctx.startRendering();
    const L = buf.getChannelData(0), R = buf.getChannelData(1), a = Math.round(from * SR);
    let sum = 0, peak = 0, best = 0;
    const B = Math.round(0.4 * SR), H = Math.round(0.1 * SR);
    for (let i = a; i < L.length; i++) { const m = (L[i] + R[i]) / 2; sum += m * m; peak = Math.max(peak, Math.abs(L[i]), Math.abs(R[i])); }
    for (let s = a; s + B <= L.length; s += H) { let e = 0; for (let i = s; i < s + B; i++) { const m = (L[i] + R[i]) / 2; e += m * m; } best = Math.max(best, e / B); }
    return { rms: db(Math.sqrt(sum / (L.length - a))), loud: db(Math.sqrt(best)), peak: db(peak), steps: au.stats().steps, maxBeatSteps: au.stats().maxBeatSteps };
  }
  const fill = (areas, n, surface) => { for (let i = 0; i < 8; i++) { areas[i].walkers = i < 8 ? n : 0; areas[i].surface = surface; } };
  const out = {};
  // The page plays the river at 0.5 all the time and rain at the shower's strength (0.5 to 1).
  out["river bed (level 0.5, as the page)"] = await scene(10, 4, au => au.river(0.5));
  out["rain bed (strength 1)"] = await scene(8, 3, au => au.rain(1));
  out["rain bed (strength 0.5)"] = await scene(8, 3, au => au.rain(0.5));
  out["dawn bell"] = await scene(4, 0, au => au.bell());
  out["footsteps, 2 walkers on grass"] = await scene(10, 1, (au, ar) => { ar[1].walkers = 2; });
  out["footsteps, 8 areas x 6 walkers on worn earth"] = await scene(10, 1, (au, ar) => fill(ar, 6, "dirt"));
  out["footsteps, 8 areas x 30 walkers on worn earth"] = await scene(10, 1, (au, ar) => fill(ar, 30, "dirt"));
  out["place cue"] = await scene(2, 0, au => au.place());
  out["busy mix: river, rain 1, 8 x 30 walkers, theme, bell, place"] = await scene(30, 2, (au, ar) => { au.river(0.5); au.rain(1); au.theme(true); fill(ar, 30, "dirt"); ar[2].ford = 3; },
    (au, ar, t) => { if (Math.abs(t - 3) < 0.01 || Math.abs(t - 17) < 0.01) au.bell(); if (Math.abs(t - 5) < 0.01) au.place(); });
  out["busy mix in mud: river, rain 1, 8 x 30 walkers on mud, theme, bell"] = await scene(30, 2, (au, ar) => { au.river(0.5); au.rain(1); au.theme(true); fill(ar, 30, "mud"); },
    (au, ar, t) => { if (Math.abs(t - 3) < 0.01) au.bell(); });
  return out;
});
await browser.close();

console.log("SOUND LEVELS (offline render, 48 kHz, volume slider 70%, through the limiter; measured, not heard)");
console.log("scene                                                             | RMS dBFS | loudest 400 ms | peak dBFS");
let ok = true;
for (const [k, v] of Object.entries(scenes)) {
  console.log(`${k.padEnd(65)} | ${v.rms.toFixed(1).padStart(8)} | ${v.loud.toFixed(1).padStart(14)} | ${v.peak.toFixed(1).padStart(9)}${k.startsWith("footsteps") || k.startsWith("busy") ? `   (steps: at most ${v.maxBeatSteps} a beat)` : ""}`);
  if (v.peak > -1 || v.maxBeatSteps > 8) ok = false;
}
console.log(ok ? "every scene peaks below -1 dBFS and keeps to 8 step voices a beat" : "FAILED: a scene peaks at -1 dBFS or above, or plays more than 8 step voices a beat");
if (!ok) process.exitCode = 1;

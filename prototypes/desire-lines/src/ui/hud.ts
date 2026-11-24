import { Stats, SEASONS } from "../sim/world";

const $ = (id: string) => document.getElementById(id)!;
const pct = (v: number) => (Number.isFinite(v) ? `${(v * 100).toFixed(1)}%` : "-");

function band(el: HTMLElement, v: number, lo: number, hi: number) {
  el.classList.toggle("ok", Number.isFinite(v) && v >= lo && v <= hi);
  el.classList.toggle("warn", Number.isFinite(v) && (v < lo || v > hi));
}

export function updateHud(s: Stats, fps: number, tickMs: number, seed: number, walkersOut: number) {
  $("hud-time").textContent = `Day ${s.day}, ${SEASONS[s.season]}, year ${s.year}`;
  $("hud-walkers").textContent = `${walkersOut} out of ${s.walkers}`;
  $("hud-seed").textContent = `Seed ${seed}`;
  $("hud-fps").textContent = `${fps.toFixed(0)} fps, sim ${tickMs.toFixed(2)} ms/tick`;
  const d = $("hud-detour"), c = $("hud-coverage"), t = $("hud-trunk");
  d.textContent = Number.isFinite(s.detourRatio) ? s.detourRatio.toFixed(2) : "-";
  c.textContent = pct(s.trailCoverage);
  t.textContent = pct(s.trunkShare);
  $("hud-trunkd").textContent = pct(s.trunkShareD);
  // Bands only mean something once the town has had a couple of days to wear in.
  const settled = s.simTime > 30;
  band(d, settled ? s.detourRatio : NaN, 1.05, 1.3);
  band(c, settled ? s.trailCoverage : NaN, 0.03, 0.08);
  band(t, settled ? s.trunkShare : NaN, 0.3, 0.7);
}

/** The flow preview readout, on its own line of the status bar (its space is always kept). */
export function setPreviewLine(text: string) {
  const el = $("preview-line");
  el.textContent = text;
  el.title = text;
}

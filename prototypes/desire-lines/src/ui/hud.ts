import { Stats, SEASONS, World } from "../sim/world";
import { PARAMS } from "../sim/params";

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

let lastDay = -1;
/**
 * The village panel's weather and houses, and the dawn mark: when a new day
 * begins the day line flashes (the visual twin of the dawn bell).
 */
export function updateVillage(w: World) {
  const day = Math.floor(w.time / PARAMS.daySimSec) + 1;
  if (lastDay > 0 && day > lastDay) {
    const el = $("hud-time");
    el.classList.remove("dawn");
    void el.offsetWidth; // restart the animation
    el.classList.add("dawn");
  }
  lastDay = day;
  const wx = w.weather, el = $("hud-weather");
  let text = "Dry";
  if (!w.rainOn) text = "Off (no rain on this map)";
  else if (wx.raining) text = `Rain, day ${Math.floor(wx.rainStart / PARAMS.daySimSec) + 1}`;
  else if (wx.wetness > 0) text = `Mud until day ${Math.floor(wx.mudUntil / PARAMS.daySimSec) + 1}`;
  if (el.textContent !== text) el.textContent = text;
  el.classList.toggle("wet", w.rainOn && (wx.raining || wx.wetness > 0));
  const g = w.grown, last = g.length ? g[g.length - 1] : null;
  const houses = `Houses: ${w.houses.length}`, note = !w.growthOn ? "no growth on this map" : last ? `newest on day ${last.day}` : "none new yet";
  // "Houses: 11", then the note on its own line; a visually hidden "; " keeps the two apart in
  // copied text, innerText and screen readers ("Houses: 11; newest on day 7").
  const hEl = $("hud-houses");
  if (hEl.firstChild?.textContent !== houses || hEl.lastChild?.textContent !== note) {
    hEl.textContent = houses;
    const sep = document.createElement("span"), small = document.createElement("small");
    sep.className = "sr-sep";
    sep.textContent = "; ";
    small.textContent = note;
    hEl.append(sep, small);
  }
}

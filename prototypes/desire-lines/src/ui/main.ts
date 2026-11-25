// Page entry: owns the world, the frame loop, the controls and the debug API.
import { World, WorldOptions } from "../sim/world";
import { PARAMS } from "../sim/params";
import { N, idx } from "../sim/grid";
import { Renderer } from "./render";
import { readPalette, onThemeChange } from "./palette";
import { Input, Tool } from "./input";
import { updateHud, setPreviewLine, updateVillage } from "./hud";
import { FlowPreviewer } from "./previewer";
import { WhatIf } from "../sim/preview";
import { Sound } from "./sound";
import { readout } from "./readout";

const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const MAX_TICKS_PER_FRAME = 8; // beyond this a slow frame drops sim time instead of stalling
const TICK_BUDGET_MS = 10; // and no more sim work than this per frame, so a slow machine slows the sim instead of stalling
const LATE_FRAME_MS = 25; // a frame that came this late (about 1.5 frames at 60 Hz) ...
const LATE_BUDGET_MS = 5; // ... gets only this much sim work, so a slow machine catches up on frames first
const PREVIEW_BUDGET_MS = 6; // flow preview work per frame (3 ms when the sim was busy)
const SPEEDS = [1, 2, 4, 8];

function initialSeed(): number {
  try {
    const m = location.hash.match(/seed=(\d+)/);
    if (m) return Number(m[1]) >>> 0 || 1;
  } catch { /* no location access: use the default */ }
  return 1;
}

let seed = initialSeed();
let walkerCount = PARAMS.defaultWalkers;
/** Rain and house growth for new worlds: the player's defaults (both on, PARAMS.rainOn and growthOn) unless the debug API changes them. */
let rules: WorldOptions = {};
let world = new World(seed, walkerCount, rules);
let paused = false;
let speed = 1;

const canvas = $<HTMLCanvasElement>("map");
const renderer = new Renderer(canvas, world, readPalette());
const status = $("status");
const say = (msg: string) => { status.textContent = msg; status.title = msg; };
const previewer = new FlowPreviewer(world);
const input = new Input(canvas, () => world, renderer, previewer, say);
const tip = $("preview-tip");
const sound = new Sound($<HTMLButtonElement>("btn-sound"), $<HTMLInputElement>("volume"), $("sound-note"));
// Each tool action has a mark on the map (its visual twin) and, with sound on, a cue.
input.onCue = (cue, x, y) => {
  renderer.fx.pulse(cue === "place" ? 0 : cue === "erase" ? 1 : 2, x, y, performance.now());
  sound.cue(cue);
};
const kindName = (b: number) => ({ well: "Well", field: "Field", market: "Market", house: "house" })[world.buildings[b].kind];

// ------------------------------------------------------------------ layout
const mainEl = $("main");
const narrow = window.matchMedia("(max-width: 760px)");
const SIDE_MIN = 236; // side panel minimum width plus the gap
function fit() {
  // The map is the largest square that fits beside the side panel (or above it when narrow).
  const w = mainEl.clientWidth, h = mainEl.clientHeight;
  const size = Math.floor((narrow.matches ? w : Math.min(w - SIDE_MIN, h)) - 2);
  renderer.resize(Math.max(160, size));
}
new ResizeObserver(fit).observe(mainEl);
window.addEventListener("resize", fit);
fit();
onThemeChange(() => renderer.setPalette(readPalette()));

// ------------------------------------------------------------------ controls
const pauseBtn = $<HTMLButtonElement>("btn-pause");
// A fixed label with aria-pressed: pressed means paused.
function setPaused(p: boolean) {
  paused = p;
  renderer.paused = p;
  sound.setPaused(p);
  pauseBtn.setAttribute("aria-pressed", String(p));
}
pauseBtn.addEventListener("click", () => setPaused(!paused));

function setSpeed(x: number) {
  speed = SPEEDS.includes(x) ? x : 1;
  for (const s of SPEEDS) $(`speed-${s}`).setAttribute("aria-pressed", String(s === speed));
}
for (const s of SPEEDS) $(`speed-${s}`).addEventListener("click", () => setSpeed(s));

const walkersInput = $<HTMLInputElement>("walkers"), walkersOut = $("walkers-out");
function setWalkers(n: number) {
  walkerCount = Math.max(PARAMS.minWalkers, Math.min(PARAMS.maxWalkers, Math.round(n)));
  world.setWalkerCount(walkerCount);
  showPopulation();
}
/** The slider and its readout follow the population, which house growth raises after the start. */
let shownPopulation = -1;
function showPopulation() {
  const n = world.walkers.length;
  if (n === shownPopulation) return;
  shownPopulation = n;
  walkersInput.value = String(n);
  walkersOut.textContent = String(n);
}
walkersInput.addEventListener("input", () => setWalkers(Number(walkersInput.value)));

const TOOLS: [Tool, string, string][] = [
  ["fence", "tool-fence", "f"], ["gravel", "tool-gravel", "g"], ["gate", "tool-gate", "t"], ["bridge", "tool-bridge", "b"], ["erase", "tool-erase", "e"],
];
// Short enough for one line at phone width.
const TOOL_HINTS: Record<Tool, string> = {
  none: "",
  fence: "Fence: drag to draw a fence line.",
  gravel: "Gravel: brush a strip on or beside a route.",
  gate: "Gate: tap a fence to put a gate in it.",
  bridge: "Footbridge: tap the river to lay one.",
  erase: "Erase: tap or drag over what to remove.",
};
function setTool(t: Tool) {
  input.setTool(t);
  for (const [name, id] of TOOLS) $(id).setAttribute("aria-pressed", String(name === t));
  say(TOOL_HINTS[t]);
}
for (const [name, id] of TOOLS) $(id).addEventListener("click", () => setTool(input.tool === name ? "none" : name));
const toolKeys = new Map(TOOLS.map(([name, , key]) => [key, name]));

const overlayBtn = $("btn-overlay");
function setOverlay(on: boolean) {
  renderer.overlay = on;
  renderer.invalidate();
  overlayBtn.setAttribute("aria-pressed", String(on));
  $("legend-heat").hidden = !on;
  $("legend-ground").hidden = on;
}
overlayBtn.addEventListener("click", () => setOverlay(!renderer.overlay));

const previewBtn = $("btn-preview");
function setPreview(on: boolean) {
  previewer.enabled = on;
  if (!on) previewer.clear();
  previewBtn.setAttribute("aria-pressed", String(on));
  try { localStorage.setItem("tt-preview", on ? "1" : "0"); } catch { /* storage blocked: keep it for this visit */ }
}
previewBtn.addEventListener("click", () => setPreview(!previewer.enabled));
let previewSaved: string | null = null;
try { previewSaved = localStorage.getItem("tt-preview"); } catch { /* no storage */ }
setPreview(previewSaved !== "0");

function reset(newSeed = seed) {
  seed = newSeed >>> 0 || 1;
  world = new World(seed, walkerCount, rules);
  renderer.setWorld(world);
  previewer.setWorld(world);
  input.setTool(input.tool);
  sound.reset();
  shownPopulation = -1;
  showPopulation();
  acc = 0;
  updateHud(world.stats(), fps, tickMs, seed, 0);
}
$("btn-reset").addEventListener("click", () => { reset(); say("Map reset."); });
$("btn-newmap").addEventListener("click", () => { reset((((Math.imul(seed, 48271) + 12345) % 99991) + 99991) % 99991 + 1); say(`New map, seed ${seed}.`); });

window.addEventListener("keydown", e => {
  const t = e.target as HTMLElement;
  if (e.ctrlKey || e.metaKey || e.altKey || t.tagName === "INPUT") return;
  const k = e.key.toLowerCase();
  // Space always pauses, as in most games; buttons still respond to Enter.
  if (k === " ") { setPaused(!paused); e.preventDefault(); }
  else if (k >= "1" && k <= "4") setSpeed(SPEEDS[Number(k) - 1]);
  else if (toolKeys.has(k)) { const t = toolKeys.get(k)!; setTool(input.tool === t ? "none" : t); }
  else if (k === "escape") setTool("none");
  else if (k === "w") setOverlay(!renderer.overlay);
  else if (k === "p") setPreview(!previewer.enabled);
  else if (k === "r") reset();
  else if (k === "n") $("btn-newmap").click();
  else if (k === "m") sound.toggle();
});

// ------------------------------------------------------------------ loop
let acc = 0, last = performance.now(), fps = 60, tickMs = 0, hudAt = 0, simMs = 0;
let plainTickMs = 1, heavyTickMs = 4; // moving averages of a plain tick and of one that rebuilds a field
let tipText = "";
/**
 * The preview readout: always on the status bar's second line, and with a
 * mouse also next to the pointer, on the side away from the ghost routes'
 * likely direction (above the pointer in the lower half of the map). On touch
 * there is no floating readout: a finger would cover it.
 */
function showTip() {
  const p = renderer.flow;
  const text = p ? readout(p, kindName, renderer.ghost?.kind ?? "") : "";
  const at = input.pointer;
  if (text !== tipText) { tipText = text; tip.textContent = text; setPreviewLine(text ? `${text}.` : ""); }
  tip.hidden = !text || !at || input.touch;
  if (!tip.hidden && at && p) {
    const [x, y] = placeTip(at, tip.offsetWidth, tip.offsetHeight, p);
    tip.style.transform = `translate(${x + canvas.offsetLeft}px, ${y + canvas.offsetTop}px)`;
  }
}
/**
 * Where the floating readout goes: of eight spots round the pointer, the one
 * that covers the fewest points of the ghost routes (and stays on the map);
 * ties keep the last spot, so the box does not jump about.
 */
let tipSpot = 0;
const TIP_SPOTS: [number, number][] = [[16, 18], [16, -14], [-16, 18], [-16, -14], [16, 50], [16, -46], [-16, 50], [-16, -46]];
function placeTip(at: [number, number], tw: number, th: number, p: NonNullable<typeof renderer.flow>): [number, number] {
  const k = canvas.clientWidth / N, W = canvas.clientWidth, H = canvas.clientHeight;
  let best = tipSpot, bv = Infinity;
  for (let i = 0; i < TIP_SPOTS.length; i++) {
    const [ox, oy] = TIP_SPOTS[i];
    const x0 = ox >= 0 ? at[0] + ox : at[0] + ox - tw, y0 = oy >= 0 ? at[1] + oy : at[1] + oy - th;
    let v = x0 < 4 || y0 < 4 || x0 + tw > W - 4 || y0 + th > H - 4 ? 1000 : 0;
    const pad = 6;
    for (const c of p.changes)
      for (const pts of [c.newPts, c.oldPts])
        for (let j = 0; j < pts.length; j += 2) {
          const px = pts[j] * k, py = pts[j + 1] * k;
          if (px > x0 - pad && px < x0 + tw + pad && py > y0 - pad && py < y0 + th + pad) v++;
        }
    if (i === tipSpot) v -= 0.5;
    if (v < bv) { bv = v; best = i; }
  }
  tipSpot = best;
  const [ox, oy] = TIP_SPOTS[best];
  const x = ox >= 0 ? at[0] + ox : at[0] + ox - tw, y = oy >= 0 ? at[1] + oy : at[1] + oy - th;
  return [Math.max(4, Math.min(W - tw - 4, x)), Math.max(4, Math.min(H - th - 4, y))];
}
// Frame recorder for the perf test: every frame's interval and in-page work time.
let rec: { intervals: number[]; works: number[]; draws: number[]; sim0: number; t0: number; worstTick: number; budgetHits: number } | null = null;
const intervals: number[] = [], works: number[] = [];
function frame(now: number) {
  const t0 = performance.now();
  const dt = Math.min(0.25, (now - last) / 1000);
  const budget = now - last > LATE_FRAME_MS ? LATE_BUDGET_MS : TICK_BUDGET_MS;
  if (now > last) { intervals.push(now - last); if (intervals.length > 120) intervals.shift(); }
  last = now;
  if (!paused) {
    acc += dt * speed * PARAMS.tickHz;
    let n = Math.floor(acc);
    if (n > MAX_TICKS_PER_FRAME) { n = MAX_TICKS_PER_FRAME; acc = n; }
    if (n > 0) {
      const ts = performance.now();
      let done = 0;
      while (done < n) {
        // An honest budget: a tick is only started if it should fit. Ticks that rebuild a route
        // field cost about four plain ones, so each kind's cost is tracked; at least one tick a frame runs.
        const heavy = world.nextTickRefreshes();
        if (done > 0 && performance.now() - ts + (heavy ? heavyTickMs : plainTickMs) > budget) break;
        const t1 = performance.now();
        world.tick();
        const d = performance.now() - t1;
        if (heavy) heavyTickMs = heavyTickMs * 0.8 + d * 0.2; else plainTickMs = plainTickMs * 0.8 + d * 0.2;
        done++;
      }
      const spent = performance.now() - ts;
      simMs = spent;
      tickMs = tickMs * 0.9 + (spent / done) * 0.1;
      if (rec) { rec.worstTick = Math.max(rec.worstTick, spent); if (done < n) rec.budgetHits++; }
      // Ticks the budget cut are dropped (at most one is carried over), so the sim slows instead of falling behind.
      acc = Math.min(1, acc - done);
    } else simMs = 0;
  } else simMs = 0;
  previewer.update(now, simMs > TICK_BUDGET_MS * 0.8 ? PREVIEW_BUDGET_MS / 2 : PREVIEW_BUDGET_MS);
  renderer.flow = previewer.enabled ? previewer.current : null;
  showTip();
  const td = performance.now();
  renderer.draw(paused ? 1 : Math.min(1, acc), now);
  if (rec) rec.draws.push(performance.now() - td);
  sound.update(now, world);
  if (now - hudAt > 500) {
    hudAt = now;
    const recent = intervals.slice(-60);
    fps = recent.length ? 1000 / (recent.reduce((a, b) => a + b, 0) / recent.length) : 60;
    updateHud(world.stats(), fps, tickMs, seed, world.walkersOut());
    updateVillage(world);
    showPopulation();
  }
  const work = performance.now() - t0;
  works.push(work);
  if (works.length > 120) works.shift();
  if (rec) { rec.intervals.push(intervals[intervals.length - 1] ?? 0); rec.works.push(work); }
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);

// ------------------------------------------------------------------ debug API (tests)
type CellList = number[] | [number, number][];
const toCells = (cells: CellList) => (cells as (number | [number, number])[]).map(c => (Array.isArray(c) ? idx(c[0], c[1]) : c));
const api = {
  get world() { return world; },
  get renderer() { return renderer; },
  setWalkers,
  setSpeed,
  pause: () => setPaused(true),
  play: () => setPaused(false),
  reset: (s?: number) => reset(s ?? seed),
  setTool,
  setOverlay,
  stats() {
    const s = world.stats();
    const avg = (a: number[]) => (a.length ? a.reduce((x, y) => x + y, 0) / a.length : 0);
    return {
      fps, tickMs, frameWorkMs: avg(works.slice(-60)), worstFrameWorkMs: Math.max(0, ...works.slice(-60)),
      walkers: s.walkers, walkersOut: world.walkersOut(), simTime: s.simTime, seed, speed, paused,
      detourRatio: s.detourRatio, trailCoverage: s.trailCoverage, trunkShare: s.trunkShare, trunkShareD: s.trunkShareD,
      firstTrailTime: s.firstTrailTime, fences: world.fence.reduce((a, b) => a + b, 0), bridges: world.bridges.length,
      gates: world.gate.reduce((a, b) => a + b, 0), gravel: world.gravel.reduce((a, b) => a + b, 0), tool: input.tool,
    };
  },
  perfStart() { const t = renderer.timing; t.ground = t.groundBuilds = t.bg = t.walkers = t.fx = 0; rec = { intervals: [], works: [], draws: [], sim0: world.time, t0: performance.now(), worstTick: 0, budgetHits: 0 }; },
  perfStop() {
    const r = rec!;
    rec = null;
    const iv = r.intervals.slice(1), wk = r.works;
    const avg = (a: number[]) => a.reduce((x, y) => x + y, 0) / Math.max(1, a.length);
    const sorted = [...wk].sort((a, b) => a - b), ds = [...r.draws].sort((a, b) => a - b);
    const secs = (performance.now() - r.t0) / 1000;
    return {
      frames: iv.length, seconds: secs, fps: 1000 / avg(iv), avgIntervalMs: avg(iv), worstIntervalMs: Math.max(...iv),
      longFrames: iv.filter(v => v > 50).length, avgWorkMs: avg(wk), p95WorkMs: sorted[Math.floor(sorted.length * 0.95)] ?? 0,
      worstWorkMs: sorted[sorted.length - 1] ?? 0, avgDrawMs: avg(ds), p95DrawMs: ds[Math.floor(ds.length * 0.95)] ?? 0, worstDrawMs: ds[ds.length - 1] ?? 0,
      groundMs: renderer.timing.ground / Math.max(1, renderer.timing.groundBuilds), groundBuilds: renderer.timing.groundBuilds,
      perFrameMs: { bg: renderer.timing.bg / Math.max(1, wk.length), walkers: renderer.timing.walkers / Math.max(1, wk.length), fx: renderer.timing.fx / Math.max(1, wk.length), ground: renderer.timing.ground / Math.max(1, wk.length) },
      simSecondsPerSecond: (world.time - r.sim0) / secs, tickMs, worstTickFrameMs: r.worstTick, budgetHits: r.budgetHits,
    };
  },
  placeFence: (cells: CellList) => world.placeFence(toCells(cells)),
  placeBridge: (x: number, y: number) => world.placeBridge(x + 0.5, y + 0.5) !== null,
  placeGravel: (cells: CellList) => world.placeGravel(toCells(cells)),
  /** A gate on the fence at or next to cell (x, y); false if there is no fence with a way through. */
  placeGate: (x: number, y: number) => world.placeGate(world.gateCellAt(x + 0.5, y + 0.5)),
  /** What the Gate tool would do at cell (x, y): the gate's cells, whether it removes one, or why not. */
  gateTarget: (x: number, y: number) => world.gateTarget(x + 0.5, y + 0.5),
  /** Rain now (debug): for `days` in-game days (default 1); mud lasts as after a rain day. */
  startRain: (days = 1) => world.startRain(days * PARAMS.daySimSec),
  /** Stops the rain (debug); with dry = true the mud goes at once too. */
  stopRain: (dry = false) => world.stopRain(dry),
  /**
   * Weather and growth state for drawing and sound: rain (0..1, how hard it rains now), wetness (0..1),
   * whether it is raining, the mud cell count (world.mud is the per-cell flag), and the houses grown so far.
   */
  weather() {
    const wx = world.weather;
    return { rainOn: world.rainOn, raining: wx.raining, rain: wx.rain, wetness: wx.wetness, mudCells: world.mudCells, rainDays: wx.rainDays, mudUntil: wx.mudUntil, simTime: world.time };
  },
  /** Switches rain and house growth for the next map and resets it (debug; both on by default). */
  setRules(r: { rain?: boolean; growth?: boolean }) { rules = { ...rules, ...r }; reset(seed); return { rain: world.rainOn, growth: world.growthOn }; },
  houses() {
    return { growthOn: world.growthOn, grown: world.grown.map(h => ({ ...h })), population: world.walkers.length, target: PARAMS.hamletTarget, nextHouseTime: world.nextHouseTime };
  },
  removeGate: (x: number, y: number) => world.removeGate(idx(x, y)),
  /** Sound state: whether it is on, whether an AudioContext exists, and what the engine scheduled. */
  sound: () => sound.state(),
  erase: (cells: CellList) => world.erase(toCells(cells)),
  /** Client coordinates of a cell centre, for tests that drive the pointer. */
  cellToClient(x: number, y: number) {
    const r = canvas.getBoundingClientRect();
    return { x: r.left + canvas.clientLeft + ((x + 0.5) / N) * canvas.clientWidth, y: r.top + canvas.clientTop + ((y + 0.5) / N) * canvas.clientHeight };
  },
  setPreview,
  /** The flow preview on screen now (null if none) and its readout. */
  previewState() {
    const p = renderer.flow;
    return { enabled: previewer.enabled, settled: previewer.settled, text: tipText, tipVisible: !tip.hidden, line: $("preview-line").textContent ?? "",
      changes: p ? p.changes.length : 0, reroutedPerDay: p ? p.reroutedPerDay : 0, blocked: p ? p.blocked.length : 0 };
  },
  /** Computes a preview directly (no throttle) for a hypothetical edit, as cell lists. */
  previewFlows(change: { [K in keyof WhatIf]?: CellList }) {
    const c: WhatIf = {};
    for (const k of ["block", "open", "bridge", "closeBridge", "gravel", "ungravel", "gate", "ungate"] as const) if (change[k]) c[k] = toCells(change[k]!);
    const p = world.previewFlows(c);
    return { changes: p.changes.map(x => ({ home: x.home, dest: x.dest, rate: x.rate, reachable: x.reachable, costRatio: x.costRatio, points: x.newPts.length / 2 })),
      reroutedPerDay: p.reroutedPerDay, text: readout(p, kindName, change.bridge ? "bridge" : "") };
  },
};
(window as unknown as { __tt: typeof api }).__tt = api;

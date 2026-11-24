// Page entry: owns the world, the frame loop, the controls and the debug API.
import { World } from "../sim/world";
import { PARAMS } from "../sim/params";
import { N, idx } from "../sim/grid";
import { Renderer } from "./render";
import { readPalette, onThemeChange } from "./palette";
import { Input, Tool } from "./input";
import { updateHud } from "./hud";

const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const MAX_TICKS_PER_FRAME = 8; // beyond this a slow frame drops sim time instead of stalling
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
let world = new World(seed, walkerCount);
let paused = false;
let speed = 1;

const canvas = $<HTMLCanvasElement>("map");
const renderer = new Renderer(canvas, world, readPalette());
const status = $("status");
const say = (msg: string) => { status.textContent = msg; };
const input = new Input(canvas, () => world, renderer, say);

// ------------------------------------------------------------------ layout
const wrap = $("map-wrap");
const narrow = window.matchMedia("(max-width: 760px)");
function fit() {
  const w = wrap.clientWidth, h = wrap.clientHeight;
  const size = Math.floor((narrow.matches ? w : Math.min(w, h)) - 2);
  renderer.resize(Math.max(160, size));
}
new ResizeObserver(fit).observe(wrap);
window.addEventListener("resize", fit);
fit();
onThemeChange(() => renderer.setPalette(readPalette()));

// ------------------------------------------------------------------ controls
const pauseBtn = $<HTMLButtonElement>("btn-pause");
function setPaused(p: boolean) {
  paused = p;
  pauseBtn.textContent = p ? "Play" : "Pause";
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
  walkersInput.value = String(walkerCount);
  walkersOut.textContent = String(walkerCount);
  world.setWalkerCount(walkerCount);
}
walkersInput.addEventListener("input", () => setWalkers(Number(walkersInput.value)));

const TOOLS: [Tool, string][] = [["fence", "tool-fence"], ["bridge", "tool-bridge"], ["erase", "tool-erase"]];
const TOOL_HINTS: Record<Tool, string> = {
  none: "",
  fence: "Fence: drag across the map to draw a fence line.",
  bridge: "Footbridge: click on the river to lay a bridge across it.",
  erase: "Erase: click or drag over fences and bridges to remove them.",
};
function setTool(t: Tool) {
  input.setTool(t);
  for (const [name, id] of TOOLS) $(id).setAttribute("aria-pressed", String(name === t));
  say(TOOL_HINTS[t]);
}
for (const [name, id] of TOOLS) $(id).addEventListener("click", () => setTool(input.tool === name ? "none" : name));

const overlayBtn = $("btn-overlay");
function setOverlay(on: boolean) {
  renderer.overlay = on;
  renderer.invalidate();
  overlayBtn.setAttribute("aria-pressed", String(on));
  $("legend-heat").hidden = !on;
  $("legend-ground").hidden = on;
}
overlayBtn.addEventListener("click", () => setOverlay(!renderer.overlay));

function reset(newSeed = seed) {
  seed = newSeed >>> 0 || 1;
  world = new World(seed, walkerCount);
  renderer.setWorld(world);
  renderer.preview = null;
  acc = 0;
  updateHud(world.stats(), fps, tickMs, seed, 0);
}
$("btn-reset").addEventListener("click", () => { reset(); say("Map reset."); });
$("btn-newmap").addEventListener("click", () => { reset((Math.imul(seed, 48271) + 12345) % 99991 + 1); say(`New map, seed ${seed}.`); });

window.addEventListener("keydown", e => {
  const t = e.target as HTMLElement;
  if (e.ctrlKey || e.metaKey || e.altKey || t.tagName === "INPUT") return;
  const k = e.key.toLowerCase();
  // Space always pauses, as in most games; buttons still respond to Enter.
  if (k === " ") { setPaused(!paused); e.preventDefault(); }
  else if (k >= "1" && k <= "4") setSpeed(SPEEDS[Number(k) - 1]);
  else if (k === "f") setTool(input.tool === "fence" ? "none" : "fence");
  else if (k === "b") setTool(input.tool === "bridge" ? "none" : "bridge");
  else if (k === "e") setTool(input.tool === "erase" ? "none" : "erase");
  else if (k === "escape") setTool("none");
  else if (k === "w") setOverlay(!renderer.overlay);
  else if (k === "r") reset();
  else if (k === "n") $("btn-newmap").click();
});

// ------------------------------------------------------------------ loop
let acc = 0, last = performance.now(), fps = 60, tickMs = 0, hudAt = 0;
// Frame recorder for the perf test: every frame's interval and in-page work time.
let rec: { intervals: number[]; works: number[]; sim0: number; t0: number; worstTick: number } | null = null;
const intervals: number[] = [], works: number[] = [];
function frame(now: number) {
  const t0 = performance.now();
  const dt = Math.min(0.25, (now - last) / 1000);
  if (now > last) { intervals.push(now - last); if (intervals.length > 120) intervals.shift(); }
  last = now;
  if (!paused) {
    acc += dt * speed * PARAMS.tickHz;
    let n = Math.floor(acc);
    if (n > MAX_TICKS_PER_FRAME) { n = MAX_TICKS_PER_FRAME; acc = n; }
    if (n > 0) {
      const ts = performance.now();
      for (let i = 0; i < n; i++) world.tick();
      const spent = performance.now() - ts;
      tickMs = tickMs * 0.9 + (spent / n) * 0.1;
      if (rec) rec.worstTick = Math.max(rec.worstTick, spent);
      acc -= n;
    }
  }
  renderer.draw(paused ? 1 : Math.min(1, acc), now);
  if (now - hudAt > 500) {
    hudAt = now;
    const recent = intervals.slice(-60);
    fps = recent.length ? 1000 / (recent.reduce((a, b) => a + b, 0) / recent.length) : 60;
    updateHud(world.stats(), fps, tickMs, seed, world.walkersOut());
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
    };
  },
  perfStart() { rec = { intervals: [], works: [], sim0: world.time, t0: performance.now(), worstTick: 0 }; },
  perfStop() {
    const r = rec!;
    rec = null;
    const iv = r.intervals.slice(1), wk = r.works;
    const avg = (a: number[]) => a.reduce((x, y) => x + y, 0) / Math.max(1, a.length);
    const sorted = [...wk].sort((a, b) => a - b);
    const secs = (performance.now() - r.t0) / 1000;
    return {
      frames: iv.length, seconds: secs, fps: 1000 / avg(iv), avgIntervalMs: avg(iv), worstIntervalMs: Math.max(...iv),
      longFrames: iv.filter(v => v > 50).length, avgWorkMs: avg(wk), p95WorkMs: sorted[Math.floor(sorted.length * 0.95)] ?? 0,
      worstWorkMs: sorted[sorted.length - 1] ?? 0, simSecondsPerSecond: (world.time - r.sim0) / secs, tickMs, worstTickFrameMs: r.worstTick,
    };
  },
  placeFence: (cells: CellList) => world.placeFence(toCells(cells)),
  placeBridge: (x: number, y: number) => world.placeBridge(x + 0.5, y + 0.5) !== null,
  erase: (cells: CellList) => world.erase(toCells(cells)),
  /** Client coordinates of a cell centre, for tests that drive the pointer. */
  cellToClient(x: number, y: number) {
    const r = canvas.getBoundingClientRect();
    return { x: r.left + ((x + 0.5) / N) * r.width, y: r.top + ((y + 0.5) / N) * r.height };
  },
};
(window as unknown as { __tt: typeof api }).__tt = api;

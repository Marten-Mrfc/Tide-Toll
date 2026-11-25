// Round 4 rule checks: gates on fences at any angle, gate refusals and their
// reasons, fence over gravel, rain and mud, house growth, determinism of the
// weather and growth streams, and preview isolation with both on. Imported by
// sim.test.ts, which collects the PASS/FAIL lines.
import { World } from "../src/sim/world";
import { PARAMS } from "../src/sim/params";
import { N, NC, idx, inside, line4, DX, DY } from "../src/sim/grid";
import { GATE_REASONS } from "../src/sim/gates";
import { keepsWaysOpen, Plot } from "../src/sim/growth";
import { describePreview } from "../src/sim/preview";

type Check = (ok: boolean, what: string) => void;
type Log = (...a: unknown[]) => void;
const TPD = PARAMS.daySimSec * PARAMS.tickHz;
const run = (w: World, days: number) => { for (let i = 0; i < Math.round(days * TPD); i++) w.tick(); };
const OFF = { rain: false, growth: false };
const pct = (v: number) => (v * 100).toFixed(1) + "%";

// ------------------------------------------------------------------ gates at any angle
/**
 * A square fence ring round the well, turned by `deg`, closed except for one
 * gate placed by a click at the middle of the side facing the houses. Every
 * walker who fetches water must pass the gate. Returns null if no seed of 1 to 40
 * has room for an unbroken ring.
 */
function ringWithGate(deg: number) {
  const th = (deg * Math.PI) / 180, ux = Math.cos(th), uy = Math.sin(th), vx = -uy, vy = ux, R = 6;
  for (let seed = 1; seed <= 40; seed++) {
    const w = new World(seed, 200, OFF);
    const well = w.buildings[w.kind("well")[0]];
    const cx = well.x + well.w / 2, cy = well.y + well.h / 2;
    const corner = (a: number, b: number): [number, number] => [Math.floor(cx + R * (a * ux + b * vx)), Math.floor(cy + R * (a * uy + b * vy))];
    const cs = [corner(-1, -1), corner(1, -1), corner(1, 1), corner(-1, 1)];
    const ring: number[] = [];
    for (let i = 0; i < 4; i++) for (const c of line4(...cs[i], ...cs[(i + 1) % 4])) if (!ring.includes(c)) ring.push(c);
    if (!ring.every(c => w.canFence(c))) continue;
    // Nothing else inside: the ring holds only the well (no corner of another building within the square).
    const within = (x: number, y: number) => Math.abs((x - cx) * ux + (y - cy) * uy) < R + 1 && Math.abs((x - cx) * vx + (y - cy) * vy) < R + 1;
    if (w.buildings.some(b => b !== well && [[b.x, b.y], [b.x + b.w, b.y], [b.x, b.y + b.h], [b.x + b.w, b.y + b.h]].some(([x, y]) => within(x, y)))) continue;
    // The side along u facing the houses' mean position.
    let hx = 0, hy = 0;
    for (const h of w.houses) { hx += w.buildings[h].x; hy += w.buildings[h].y; }
    hx /= w.houses.length; hy /= w.houses.length;
    const side = (hx - cx) * vx + (hy - cy) * vy > 0 ? 1 : -1;
    const click: [number, number] = [cx + R * side * vx + 0.3 * ux, cy + R * side * vy + 0.3 * uy];
    run(w, 4);
    w.placeFence(ring);
    const target = w.gateTarget(click[0], click[1]);
    const placed = target.cells.length > 0 && w.placeGate(target.cells[0]);
    return { w, seed, ring, target, placed, well };
  }
  return null;
}

export function gateAngleTests(check: Check, log: Log) {
  log("\nGATES AT ANY ANGLE: a closed square fence round the well, turned by the angle; a gate clicked on the side facing the houses; 3 days after");
  for (const deg of [0, 30, 45, 60, 90]) {
    const r = ringWithGate(deg);
    if (!r) { check(false, `gate at ${deg} degrees: room for a ring`); continue; }
    const { w, seed, ring, target, placed, well } = r;
    run(w, 3);
    const fw = w.metrics.window();
    const through = target.cells.reduce((s, c) => s + fw[c], 0);
    const otherFence = ring.filter(c => !w.gate[c]).reduce((s, c) => s + fw[c], 0);
    const atWell = fw[well.door];
    log(`  ${String(deg).padStart(2)} degrees (seed ${seed}): ring of ${ring.length} cells; gate of ${target.cells.length} cell(s) at ${target.cells.map(c => `(${c % N},${(c / N) | 0})`).join(" ")}; footsteps in 2 days: through the gate ${through}, on the rest of the fence ${otherFence}, at the well's door ${atWell}`);
    check(placed && through >= 20 && otherFence === 0 && atWell >= 10, `gate: a gate on a fence at ${deg} degrees places and carries walkers to the well`);
  }
}

export function gateRefusalTests(check: Check, log: Log) {
  log("\nGATE REFUSALS AND SNAPPING (seed 1)");
  const w = new World(1, 50, OFF);
  // An open patch of meadow away from buildings and the river.
  let ox = -1, oy = -1;
  for (let y = 10; y < N - 20 && ox < 0; y += 3)
    for (let x = 6; x < N - 20 && ox < 0; x += 3) {
      let ok = true;
      for (let yy = y - 3; yy < y + 12 && ok; yy++) for (let xx = x - 3; xx < x + 16 && ok; xx++) ok = w.canFence(idx(xx, yy)) && w.land[idx(xx, yy)] === 1;
      if (ok) { ox = x; oy = y; }
    }
  // A lone post.
  w.placeFence([idx(ox, oy)]);
  const lone = w.gateTarget(ox + 0.5, oy + 0.5);
  check(lone.cells.length === 0 && lone.reason === GATE_REASONS.lonePost, `gate: refused on a lone post ("${lone.reason}")`);
  w.erase([idx(ox, oy)]);
  // A fence three cells thick: no way through.
  const block: number[] = [];
  for (let y = oy; y < oy + 3; y++) for (let x = ox; x < ox + 12; x++) block.push(idx(x, y));
  w.placeFence(block);
  const thick = w.gateTarget(ox + 6.5, oy + 1.5);
  check(thick.cells.length === 0 && thick.reason === GATE_REASONS.noWay, `gate: refused where both sides are blocked ("${thick.reason}")`);
  w.erase(block);
  // A straight fence: a click 2 cells off snaps onto it, 3 cells off finds no fence.
  const line = line4(ox, oy + 5, ox + 12, oy + 5);
  w.placeFence(line);
  const near = w.gateTarget(ox + 6.5, oy + 3.5), far = w.gateTarget(ox + 6.5, oy + 2.5);
  check(near.cells.length === 1 && near.cells[0] === idx(ox + 6, oy + 5), "gate: a click 2 cells from a fence snaps onto it");
  check(far.cells.length === 0 && far.reason === GATE_REASONS.noFence, `gate: a click 3 cells from any fence says so ("${far.reason}")`);
  // A diagonal staircase: every cell of it but the ends takes a gate (one cell or a corner pair).
  w.erase(line);
  const stair = line4(ox, oy, ox + 9, oy + 9);
  w.placeFence(stair);
  const takes = stair.slice(2, -2).filter(c => w.gateCellsFor(c) !== null).length;
  check(takes === stair.length - 4, `gate: every inner cell of a 45-degree fence takes a gate (${takes} of ${stair.length - 4})`);
  // Removing one cell of a two-cell gate removes both.
  const pairAt = stair.slice(2, -2).find(c => w.gateCellsFor(c)?.length === 2);
  if (pairAt !== undefined) {
    const cells = w.gateCellsFor(pairAt)!;
    w.placeGate(pairAt);
    const both = cells.every(c => w.gate[c] === 1 && w.gatePair[c] === 1);
    w.removeGate(cells[1]);
    check(both && cells.every(c => !w.gate[c] && !w.gatePair[c] && w.fence[c]), "gate: a stair-corner gate is two cells and goes as one");
    const half = w.cellCost(cells[0], 0) ; void half;
  } else check(false, "gate: a 45-degree fence has a stair corner that takes a two-cell gate");
}

export function fenceOverGravelTest(check: Check) {
  const w = new World(2, 50, OFF);
  const cells = line4(20, 20, 30, 20).filter(c => w.canGravel(c));
  w.placeGravel(cells);
  const n = w.placeFence(cells.slice(0, 5));
  check(n === 5 && cells.slice(0, 5).every(c => w.fence[c] && !w.gravel[c]) && cells.slice(5).every(c => w.gravel[c]), "gravel: a fence laid over gravel takes the gravel up");
}

// ------------------------------------------------------------------ rain and mud
/** The busiest trail cell that is not gravel. */
function busiestTrail(w: World): number {
  const fw = w.metrics.window();
  let best = -1;
  for (let c = 0; c < NC; c++) if (w.land[c] && w.wear[c] >= 25 && !w.gravel[c] && !w.map.crop[c] && (best < 0 || fw[c] > fw[best])) best = c;
  return best;
}

export function rainTests(check: Check, log: Log) {
  log("\nRAIN AND MUD");
  const w = new World(4, 200, { rain: true, growth: false });
  run(w, 10);
  w.stopRain(true);
  run(w, 0.1);
  const c = busiestTrail(w);
  let grass = -1;
  for (let g = 0; g < NC && grass < 0; g++) if (w.land[g] && w.wear[g] === 0 && !w.map.crop[g] && w.map.noise[g] !== undefined) grass = g;
  const dry = w.cellCost(c), dryGrass = w.cellCost(grass);
  // A gravel strip on a trail: it must never turn to mud.
  const fw = w.metrics.window();
  const trailCells = [...Array(NC).keys()].filter(v => w.land[v] && w.wear[v] >= 25 && v !== c && !w.map.crop[v]).sort((a, b) => fw[b] - fw[a]).slice(5, 25);
  w.placeGravel(trailCells);
  w.startRain();
  run(w, 0.4);
  const wet = w.cellCost(c);
  log(`  trail cell (${c % N},${(c / N) | 0}) at wear ${w.wear[c].toFixed(0)}: step cost dry ${dry.toFixed(3)}, in rain ${wet.toFixed(3)}; grass ${dryGrass.toFixed(3)} -> ${w.cellCost(grass).toFixed(3)}; wetness ${w.weather.wetness.toFixed(2)}, ${w.mudCells} mud cells`);
  check(w.weather.raining && w.weather.wetness === 1 && w.mud[c] === 1 && wet > dry * 1.2, "rain: worn ground turns to mud and costs more to walk");
  check(w.cellCost(grass) === dryGrass, "rain: grass does not get muddy");
  const speed = (cell: number) => (w as unknown as { speedOn(c: number): number }).speedOn(cell);
  check(speed(c) < PARAMS.walkSpeed * PARAMS.stateSpeedMul[w.ground(c)] * 0.85, "rain: walkers are slower on mud");
  // Through the rain day and the mud days after it: gravel stays firm.
  let mudOnGravel = 0, mudOnWater = 0, maxWet = 0;
  const rolled0 = w.weather.rainDays;
  const season = Math.floor(w.time / PARAMS.seasonSimSec) % 4, forced = w.weather.rainStart, until = w.weather.mudUntil;
  // Until the ground is dry (a rain day rolled meanwhile makes it last longer).
  for (let t = 0; t < 20 * 15 && (w.weather.wetness > 0 || w.weather.raining); t++) {
    run(w, 1 / 15);
    for (let v = 0; v < NC; v++) { if (w.mud[v] && w.gravel[v]) mudOnGravel++; if (w.mud[v] && (w.map.water[v] || w.bridgeAt[v] >= 0)) mudOnWater++; }
    maxWet = Math.max(maxWet, w.weather.wetness);
  }
  check(mudOnGravel === 0 && mudOnWater === 0 && trailCells.every(v => w.gravel[v]), "rain: mud never appears on gravel (nor on water, the ford or a bridge)");
  const expected = PARAMS.daySimSec * PARAMS.mudDays[season];
  log(`  forced shower: mud for ${(until - forced).toFixed(1)} s (${PARAMS.mudDays[season]} days in ${["spring", "summer", "autumn", "winter"][season]}: ${expected} s); dry ${(w.time - forced).toFixed(1)} s after it began (${w.weather.rainDays - rolled0} rain day(s) rolled meanwhile); then wetness ${w.weather.wetness}, ${w.mudCells} mud cells, cost ${w.cellCost(c).toFixed(3)}`);
  check(PARAMS.mudDays.every(d => d === 2) && Math.abs(until - forced - expected) < 0.01 && w.weather.wetness === 0 && w.mudCells === 0 && w.cellCost(c) < wet && maxWet === 1, "rain: mud lasts the rain day plus one in every season (autumn too), then the ground dries and costs fall back");

  // Mud follows a tool at once, with no tick in between: gravel erased in rain turns to mud,
  // a fence laid over gravel or over mud is never mud, and the step costs follow.
  w.startRain();
  run(w, 0.4);
  const [g0, g1] = trailCells, muddy = [...Array(NC).keys()].find(v => w.mud[v] && !w.gravel[v] && v !== c && w.canFence(v))!;
  // (Each update also picks up cells worn to trail since the last one, so the count is checked against the flags.)
  const n0 = w.mudCells, counted = () => w.mud.reduce((s, v) => s + v, 0) === w.mudCells;
  w.erase([g0]);
  const erasedMud = w.mud[g0] === 1 && counted() && w.cellCost(g0) > w.cellCost(g1);
  w.placeFence([g1, muddy]);
  const fencedDry = w.mud[g1] === 0 && w.mud[muddy] === 0 && counted();
  w.placeGravel([c]);
  const gravelDry = w.mud[c] === 0 && counted();
  log(`  tools in rain, no tick between: gravel erased -> mud ${w.mud[g0]}; fence over gravel and over mud -> mud ${w.mud[g1]}, ${w.mud[muddy]}; gravel on mud -> mud ${w.mud[c]}; mud cells ${n0} -> ${w.mudCells}`);
  check(erasedMud && fencedDry && gravelDry, "rain: mud updates at once after a tool (gravel erased, fence over gravel or mud, gravel on mud)");

  // Walkers step beside mud: two identical runs, one with a shower; walker time on lightly
  // worn trail (wear 25 to 50, dearer than grass when muddy) falls and time on the ground beside rises.
  const ta = [0, 0, 0], tb = [0, 0, 0];
  for (const seed of [2, 3, 4, 5]) {
    const mk = () => { const v = new World(seed, 200, { rain: true, growth: false }); run(v, 10); v.stopRain(true); return v; };
    const a = mk(), b = mk();
    b.startRain(PARAMS.daySimSec);
    const band = new Uint8Array(NC); // 1 light trail, 2 track or more, 0 other ground
    for (let v = 0; v < NC; v++) if (a.land[v] && !a.gravel[v]) band[v] = a.wear[v] >= 50 ? 2 : a.wear[v] >= 25 ? 1 : 0;
    const count = (v: World, t: number[]) => { for (const k of v.walkers) if (k.state === 1 || k.state === 3) t[band[k.cell]]++; };
    for (let i = 0; i < TPD; i++) { a.tick(); b.tick(); count(a, ta); count(b, tb); }
  }
  const share = (t: number[], i: number) => t[i] / (t[0] + t[1] + t[2]);
  log(`  seeds 2 to 5, day 10, one day with and without a shower: walker time on lightly worn trail ${pct(share(ta, 1))} dry, ${pct(share(tb, 1))} in rain; on track ${pct(share(ta, 2))} / ${pct(share(tb, 2))}; on other ground ${pct(share(ta, 0))} / ${pct(share(tb, 0))}`);
  check(share(tb, 1) < 0.8 * share(ta, 1) && share(tb, 0) > 1.1 * share(ta, 0), "rain: walkers step off lightly worn mud onto the ground beside it");

  // Natural weather over 60 days: the seasons' chances, and the walkers' stream untouched.
  const days: number[][] = [];
  for (const walkers of [50, 300]) {
    const v = new World(7, walkers, { rain: true, growth: false });
    const starts: number[] = [];
    let last = v.weather.rainStart;
    for (let i = 0; i < 60 * TPD; i++) { v.tick(); if (v.weather.rainStart !== last) { last = v.weather.rainStart; starts.push(Math.round(last / PARAMS.daySimSec)); } }
    days.push(starts);
  }
  log(`  seed 7, 60 days: rain on days ${days[0].map(d => d + 1).join(", ")}`);
  check(days[0].length > 0 && days[0].join() === days[1].join(), "rain: the weather depends on the seed only (50 or 300 walkers: same rain days)");
  // Forcing a shower does not move the daily rolls.
  const a = new World(7, 50, { rain: true, growth: false }), b = new World(7, 50, { rain: true, growth: false });
  run(a, 3); run(b, 3); b.startRain(5); b.stopRain(true);
  const rolls = (x: World) => { const s: number[] = []; let l = x.weather.rainStart; for (let i = 0; i < 30 * TPD; i++) { x.tick(); if (x.weather.rainStart !== l) { l = x.weather.rainStart; s.push(l); } } return s; };
  check(rolls(a).join() === rolls(b).join(), "rain: a forced shower leaves the daily rolls that follow unchanged");
  // Rain off: no weather at all.
  const off = new World(7, 50, OFF);
  run(off, 30);
  check(off.weather.rainDays === 0 && off.mudCells === 0, "rain: switched off, it never rains");
}

// ------------------------------------------------------------------ house growth
export function growthTests(check: Check, log: Log) {
  log("\nHOUSE GROWTH");
  const w = new World(5, 200, { rain: true, growth: true });
  const B0 = w.buildings.length;
  // Growth is the last step of a tick, so the wear after the tick is the wear the plot was chosen on.
  const bad: string[] = [], prevWear = w.wear;
  let blocked = 0;
  const isDoorOf = (c: number, upTo: number) => w.buildings.slice(0, upTo).some(b => b.door === c);
  for (let i = 0; i < 40 * TPD; i++) {
    const before = w.grown.length, nb = w.buildings.length;
    const fence = w.fence.slice(), gravel = w.gravel.slice(), bridge = w.bridgeAt.slice();
    w.tick();
    if (w.grown.length > before) {
      const h = w.grown[w.grown.length - 1];
      const cells: number[] = [];
      for (let y = h.y; y < h.y + 3; y++) for (let x = h.x; x < h.x + 3; x++) cells.push(idx(x, y));
      const fx = h.front % N, fy = (h.front / N) | 0, dx = h.door % N, dy = (h.door / N) | 0;
      if (prevWear[h.front] < PARAMS.frontageWear) bad.push(`house ${h.building}: frontage wear ${prevWear[h.front].toFixed(0)}`);
      if (Math.abs(fx - dx) + Math.abs(fy - dy) !== 1) bad.push(`house ${h.building}: door does not face its frontage`);
      for (const c of cells) {
        if (prevWear[c] >= PARAMS.plotClearWear) bad.push(`house ${h.building}: on a trail cell (wear ${prevWear[c].toFixed(0)})`);
        if (w.map.water[c] || w.map.crop[c] || fence[c] || gravel[c] || bridge[c] >= 0 || isDoorOf(c, nb)) bad.push(`house ${h.building}: on water, field, fence, gravel, bridge or a door`);
      }
      for (let y = h.y - 1; y <= h.y + 3; y++)
        for (let x = h.x - 1; x <= h.x + 3; x++) {
          const c = idx(x, y);
          if (cells.includes(c)) continue;
          if (w.map.water[c] || isDoorOf(c, nb) || w.buildings.slice(0, nb).some(b => x >= b.x && x < b.x + b.w && y >= b.y && y < b.y + b.h)) bad.push(`house ${h.building}: no gap to its neighbours`);
        }
      // Every door still reachable from every other.
      if (!allDoorsConnected(w)) blocked++;
    }
  }
  const s = w.stats();
  log(`  seed 5, 40 days, rain and growth on: ${w.grown.length} houses built (days ${w.grown.map(h => h.day).join(", ")}), population ${w.walkers.length}; trunk share ${(s.trunkShare * 100).toFixed(1)}%`);
  for (const b of bad.slice(0, 5)) log(`    ${b}`);
  check(w.grown.length >= 5 && w.buildings.length === B0 + w.grown.length, "growth: houses appear along well-used trails");
  check(bad.length === 0, "growth: every house is on valid frontage (door faces a track, no trail under it, dry free land, a gap to its neighbours)");
  check(blocked === 0, "growth: no house cuts any building off");
  const perHouse = w.walkers.filter(k => k.home >= B0).length;
  check(perHouse === PARAMS.householdSize * w.grown.length, `growth: each house brings ${PARAMS.householdSize} villagers who live there`);
  check(w.walkers.length <= PARAMS.hamletTarget, `growth: the population stays at or under the Hamlet target of ${PARAMS.hamletTarget}`);
  const tripsNew = w.walkers.filter(k => k.home >= B0 && k.tripLen > 0).length;
  check(tripsNew > 0 && s.tripsInWindow > 100, "growth: the new villagers walk to the well, field and market");
  // The population rule: growth stops at the Hamlet target.
  const full = new World(5, PARAMS.hamletTarget - 2, { rain: false, growth: true });
  run(full, 10);
  check(full.grown.length === 0 && full.walkers.length === PARAMS.hamletTarget - 2, "growth: no house comes when it would take the town over the target");
  // A plot that would close the only way into a fenced yard is refused.
  const v = new World(1, 50, OFF);
  const well = v.buildings[v.kind("well")[0]];
  const x0 = well.x - 4, y0 = well.y - 4, x1 = well.x + well.w + 3, y1 = well.y + well.h + 3;
  const ring: number[] = [];
  for (let x = x0; x <= x1; x++) ring.push(idx(x, y0), idx(x, y1));
  for (let y = y0; y <= y1; y++) ring.push(idx(x0, y), idx(x1, y));
  const gapX = x0 + 3;
  v.placeFence(ring.filter(c => !((c % N) >= gapX && (c % N) < gapX + 3 && ((c / N) | 0) === y1)));
  const sealing: Plot = { x: gapX, y: y1, door: idx(gapX + 1, y1 + 2), front: idx(gapX + 1, y1 + 3), fronted: [], score: 0 };
  const open: Plot = { x: x1 + 6, y: y1 + 6, door: idx(x1 + 7, y1 + 6), front: idx(x1 + 7, y1 + 5), fronted: [], score: 0 };
  check(!keepsWaysOpen(v, sealing) && keepsWaysOpen(v, open), "growth: a plot that would close the only way to a building is refused");
  // Growth off: no houses.
  const off = new World(5, 200, OFF);
  run(off, 10);
  check(off.grown.length === 0, "growth: switched off, no houses appear");
}

/** Whether every building door can reach every other on open ground (8 neighbours, no corner cutting). */
function allDoorsConnected(w: World): boolean {
  const seen = new Uint8Array(NC), doors = w.buildings.map(b => b.door);
  const stack = [doors[0]];
  seen[doors[0]] = 1;
  while (stack.length) {
    const u = stack.pop()!, ux = u % N, uy = (u / N) | 0;
    for (let k = 0; k < 8; k++) {
      const vx = ux + DX[k], vy = uy + DY[k];
      if (!inside(vx, vy)) continue;
      const v = idx(vx, vy);
      if (seen[v] || w.blocked(v)) continue;
      if (k >= 4 && (w.blocked(idx(vx, uy)) || w.blocked(idx(ux, vy)))) continue;
      seen[v] = 1; stack.push(v);
    }
  }
  return doors.every(d => seen[d] === 1);
}

// ------------------------------------------------------------------ determinism and isolation
export function newDeterminismTest(check: Check) {
  const act = () => { const w = new World(11, 220, { rain: true, growth: true }); run(w, 8); w.placeFence(line4(30, 30, 50, 60)); w.startRain(10); run(w, 6); return w; };
  const a = act(), b = act();
  let same = a.grown.length === b.grown.length && a.grown.every((h, i) => h.x === b.grown[i].x && h.y === b.grown[i].y && h.time === b.grown[i].time);
  for (let i = 0; i < NC && same; i++) if (a.wear[i] !== b.wear[i] || a.mud[i] !== b.mud[i]) same = false;
  same = same && a.walkers.length === b.walkers.length && a.walkers.every((k, i) => k.x === b.walkers[i].x && k.y === b.walkers[i].y);
  check(same && a.grown.length > 0 && a.weather.rainDays > 0, "determinism: with rain and growth on, the same seed and inputs give an identical run (houses, mud, wear, walkers)");
}

export function newIsolationTest(check: Check, log: Log) {
  const a = new World(3, 200, { rain: true, growth: true }), b = new World(3, 200, { rain: true, growth: true });
  run(a, 1.9); run(b, 1.9);
  a.startRain(); b.startRain();
  let calls = 0;
  const g0 = a.grown.length;
  for (let i = 0; i < 3 * TPD; i++) {
    a.tick(); b.tick();
    if (i % 4 === 0) {
      const k = calls++ % 4, y = 20 + (calls % 80);
      if (k === 0) b.previewFlows({ block: line4(10 + (calls % 50), 20, 30, 70).filter(c => b.canFence(c)) });
      else if (k === 1) b.previewFlows({ gravel: line4(20, y, 40, y).filter(c => b.canGravel(c)) });
      else if (k === 2) b.previewFlows({ bridge: b.bridgeCellsAt(b.map.riverX[y], y) ?? [] });
      else b.previewFlows({});
    }
  }
  const same = (x: ArrayLike<number>, y: ArrayLike<number>) => { if (x.length !== y.length) return false; for (let i = 0; i < x.length; i++) if (x[i] !== y[i]) return false; return true; };
  const ok = same(a.wear, b.wear) && same(a.cost, b.cost) && same(a.mud, b.mud) && same(a.tripRate, b.tripRate) && a.fields.every((f, t) => same(f, b.fields[t])) &&
    a.walkers.length === b.walkers.length && a.walkers.every((k, i) => k.x === b.walkers[i].x && k.cell === b.walkers[i].cell) && a.grown.length === b.grown.length;
  // After houses were added, previews still work and see the new houses' trips:
  // a fence ring round the oldest grown house cuts its own villagers off.
  run(b, 5);
  const h = b.grown[0];
  // The ring in the one-cell gap every house keeps round its 3 x 3 plot: nothing else stands there.
  const ring: number[] = [];
  for (let x = h.x - 1; x <= h.x + 3; x++) ring.push(idx(x, h.y - 1), idx(x, h.y + 3));
  for (let y = h.y; y <= h.y + 2; y++) ring.push(idx(h.x - 1, y), idx(h.x + 3, y));
  const p = b.previewFlows({ block: ring }, 99);
  const fromNew = p.changes.filter(c => c.home === h.building && !c.reachable).length;
  log(`\nFLOW PREVIEW WITH RAIN AND GROWTH (seed 3): ${calls} queries in 3 days with ${a.grown.length - g0} houses added and a shower; then a fence ring round grown house ${h.building} (built day ${h.day}): "${describePreview(p, k => b.buildings[k].kind)}", ${fromNew} cut-off routes from that house`);
  check(calls >= 100 && ok && a.grown.length > g0, "preview: with rain and growth on, queries leave the run byte-identical across new houses and mud");
  check(ring.every(c => b.canFence(c)) && fromNew > 0 && p.blocked.length > 0, "preview: sees the trips of grown houses (a fence round one shows its villagers cut off)");
}

export function gravelNoteTest(check: Check, log: Log) {
  const w = new World(1, 200, OFF);
  run(w, 10);
  // A strip in open meadow far from any trail.
  let strip: number[] = [];
  for (let y = 6; y < N - 6 && !strip.length; y++)
    for (let x = 4; x < N - 30 && !strip.length; x++) {
      const s = line4(x, y, x + 20, y);
      let ok = s.every(c => w.canGravel(c));
      for (const c of s) for (let dy = -6; dy <= 6 && ok; dy++) for (let dx = -6; dx <= 6 && ok; dx++) {
        const xx = (c % N) + dx, yy = ((c / N) | 0) + dy;
        if (inside(xx, yy) && w.wear[idx(xx, yy)] >= 10) ok = false;
      }
      if (ok) strip = s;
    }
  const p = w.previewFlows({ gravel: strip });
  const text = describePreview(p, k => w.buildings[k].kind);
  log(`\nGRAVEL FAR FROM ROUTES (seed 1, day 10): a 21-cell strip at (${strip[0] % N},${(strip[0] / N) | 0}): "${text}"`);
  check(strip.length > 0 && text === "Too far from any route to change anything", "preview: a gravel strip far from every route says so plainly");
}

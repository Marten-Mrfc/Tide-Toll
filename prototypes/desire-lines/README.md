# Prototype 1: desire lines

A throwaway browser toy that answers one question for Tide & Toll: **is steering worn paths fun?**

Villagers walk between their houses, the well, the field and the market. Every footstep wears the grass. Grass turns to trampled ground, then trail, then track. Paths nobody uses grow back. You never draw a road: you place fences, gates, gravel and footbridges, and the walkers find their own way.

The page is one self-contained file, `dist/index.html`. It has no runtime libraries, fetches nothing and is published as a claude.ai Artifact.

## Run it

```sh
npm install          # typescript and esbuild only
npm run build        # writes dist/index.html; open it in a browser
npm run check        # typecheck + build + headless simulation tests (about 9 minutes on 4 cores)
npm run perf         # Playwright: frame times at 500 and 200 walkers, 1x and 8x; 200 walkers at 8x in rain with sound on; a 4x CPU-throttled run; preview while dragging
npm run smoke        # Playwright: page contract, every control and tool (a gate on a diagonal fence too), the flow preview, rain and growth, 400 px layout in both themes, touch at 400 x 800 and 360 x 740 (a second finger too), sound (off until pressed, then a minute at 8x), rain, mud and new houses on screen
```

The perf and smoke scripts load the page the way the Artifact host serves it: `test/host.mjs` wraps `dist/index.html` in a document skeleton (doctype, charset, a `width=device-width` viewport meta, body) and writes `test-results/host.html`. The published page itself never carries those tags. Without the viewport meta, Playwright's mobile emulation lays the page out 980 px wide; the phone runs now assert `innerWidth` is 400 and 360.

`npm test -- --quick` runs the gate on 4 fresh seeds (401 to 404) and skips the other sets and the report tables. The full run uses worker threads. Playwright is not a dependency: the perf and smoke scripts use the copy installed globally in the build container.

Add `#seed=42` to the page URL to open a given map.

## Controls

| Control | Key | What it does |
| --- | --- | --- |
| Pause | Space | Stops and starts the simulation clock (the button stays "Pause" and shows pressed while paused) |
| Speed 1x 2x 4x 8x | 1 2 3 4 | At 1x one in-game day is 15 s and a season is 60 s |
| Walkers | | 50 to 500 villagers (default 200). The slider and its number follow the population, which new houses raise (by 3 a cottage, up to 248 from 200); moving it sets the population at once, and Reset starts again from the number you set |
| Fence | F | Drag to draw a fence line; it is placed on release. Walkers cannot cross it |
| Gravel | G | Drag to brush a strip of gravel; it is laid on release, and snaps onto a worn trail's centre line when one is within a cell. Gravel costs walkers 0.6 of grass (busy gravel gets cheaper still, down to the same 0.45 floor as a trunk), never grows back below trail and never turns to mud, so a trail up to about 5 cells away moves onto it and stays. A fence laid over gravel takes the gravel up. When a strip changes no route and no trail is within 6 cells of it, the readout says "Too far from any route to change anything" |
| Gate | T | Click (or tap) a fence to put a gate in it: walkers pass at a cost of 1.5 grass cells and at 0.4 of their speed, so a trail re-forms through it. On a gate, the tool takes it out again. It snaps to the nearest fence cell within 2 cells that can take a gate. A cell takes a gate when opening it joins the open ground on the two sides of the fence (any angle: on a diagonal fence, which is a staircase of cells, a stair's corner pair becomes one two-cell gate with half the delay in each cell). Otherwise the status line says why: "Gates go on fences: there is no fence here.", "No way through here: both sides are blocked." or "A gate needs fence beside it: this is a lone post." |
| Footbridge | B | Hover over the river to preview, click to lay a bridge (not next to the ford). On touch: press to preview, release on the same spot to build, slide away to cancel |
| Erase | E | Hover to preview what goes; click or drag over fences (with their gates), gravel and bridges to remove them. On touch: press to preview, release to remove, or drag |
| Flow preview | P | On by default. While a tool is in use, shows the typical routes that would change: dashed is the route now, solid with arrows is the route after, both faint where they run together. The readout, such as "Reroutes about 20 walkers a day (+13% longer on average)", is in the status line, and next to the pointer with a mouse (not under a finger). The percentage is the trip-weighted mean cost change over the rerouted trips. A cut-off destination gets a red ring and "Blocks the Well for 3 houses" |
| Wear overlay | W | Heat map of wear (0 to 100) over a dimmed map (it works in rain too) |
| Sound | M | Off until you press it. Once on, a Volume slider appears beside it. Nothing about sound is remembered across reloads (see Sound below) |
| Reset | R | Restarts the same map |
| New map | N | Generates a new map from a new seed |
| | Esc | Puts the tool down |

At phone width the controls wrap into five rows of buttons at least 40 x 40 px (title, Pause and Sound; the four speeds, each 48 px wide; Walkers; the tools; Preview, Wear, Reset and New map, with short labels), and the whole map sits under them on the first screen at 400 x 800 and 360 x 740. Each press or drag on the map belongs to the finger that started it: a second finger during a fence drag, a gravel stroke or a footbridge or gate press is ignored. The status bar keeps a fixed height (one line on desktop, two on a phone), so the map never moves when a message or the readout appears; long text is cut with an ellipsis (the full text is in its tooltip).

The side panel shows the day and season (the line flashes at each dawn), the walkers out, the town ("Houses: N" and the day the newest was built), the weather ("Dry", "Rain, day N" or "Mud until day N"), the frame rate, and the health metrics: detour ratio, trail coverage and trunk share (see below).

Status messages count properly ("Fence placed (1 cell)."), and the last action's message is cleared when a new flow preview starts, so it never sits beside a readout about something else. With a mouse, the floating readout takes whichever of eight spots round the pointer covers the fewest ghost-route points. When a footbridge would save under 5% of the walk, its readout says "Saves under 5% of the walk, so many will keep to their old way" instead of promising a shorter route (round 3: such bridges take only 24% to 45% of the crossings).

## What the player sees

- **Worn ground.** Trails fade in from a wide pale fringe of flattened grass with ragged edges. Along a trail the colour follows its wear; busy trunks (the walkers' recent footsteps, smoothed over a few seconds) pull towards a stronger compacted-earth colour, darker in the light theme and brighter in the dark one, so side trails stay lighter. Hollow ways keep the most extreme colour. This is drawing only: the wear data is untouched.
- **Rain.** Light slanted streaks fall over the map (they stop while paused), the whole ground darkens as it wets, and every muddy cell turns wet, dark, churned earth with a few puddle glints, clearly unlike dry trail in both themes. The mud fades as the ground dries.
- **New houses.** A new cottage rises out of its garden plot with a puff of dust (about a second), two rings spread from it, and a small red badge marks it for about 15 seconds. Grown cottages stand in a garden (the plot is 3 x 3 cells), so they read apart from the first houses.
- **Fences, gates, gravel.** A diagonal fence is one straight rail: stair steps are ironed out, with posts only at the ends and real corners. A gate is cut into the rail and drawn with its two posts and a leaf standing ajar, along the fence at any angle. Gravel is a pale strip with a thin dark rim and speckled grit.
- **Tool marks.** A ring pops where something is placed, dust puffs where something is removed, and a red cross shakes where an action is refused.

## Sound

Made in code with Web Audio (`src/ui/audio.ts`, copied from the audio round's measured module, and `src/ui/sound.ts`, which drives it). No files are loaded.

- **Off until pressed.** No AudioContext exists until the Sound button (or M) is pressed; it is made inside that press, and only one is ever made. Sound off costs one null check a frame. Nothing is stored, so a reload starts silent.
- **Footsteps** are counted, not played per villager: every 120 ms the walkers out on the map are counted in a 4 x 2 grid of map areas, with the ground most of them are on (grass, worn earth, gravel, mud, the bridge deck) and how many are in the ford. Once a beat (64 BPM, fixed, whatever the game speed) the engine plays one step for 1 or 2 walkers, two for 3 to 11 and one pre-mixed shuffle for 12 or more, panned by the area's position, never more than 8 step voices a beat. Rain makes grass steps quieter and worn earth squelch.
- **Beds and cues.** The river with its D drone; the ford's wading; rain, with the weather's rain level; a bell at dawn each in-game day (at most one every 6 s at high speed); place, erase and refused cues for the tools; the 8-bar theme on a pipe with lyre and frame drum, quiet, with 12 to 26 s of silence between rounds.
- **Pauses.** Sound pauses (the context is suspended) with the game and when the tab is hidden. A press on the page resumes a context the browser suspended.
- **Visual twins.** Steps: the walkers. River and ford: the river and the walkers wading. Rain: the streaks and darker ground. Bell: the flashing day line. Tool cues: the tool marks above. Theme: a note on the Sound button while a round plays.
- **Measured, not heard.** Levels were set offline against the game's own recipes (about -20 LUFS for a busy scene, peaks under -1 dBTP). The smoke test checks scheduling, not how it sounds: after a minute at 8x it counts beats, steps (at most 8 a beat), bells, theme notes and voices alive at once. Not proven: the sound itself, the loop seams, iOS (the hardware silent switch mutes Web Audio; no silent-audio workaround is added).
- **Debug:** `__tt.sound()` returns whether it is on, whether a context exists, its state and the engine's counts.

## What is in, and what is not

In: a 128 x 128 meadow of 2 m cells, a meandering river with one ford, 6 to 8 houses, a well, a field and a market across the river; wear with saturation, side spread and state-based decay; five ground states plus gravel; walkers routed on wear-weighted cost-to-go fields with Weidmann crowding and per-walker taste, and a frugal minority (below); fences, gates, gravel, footbridges and erase; pause, speed, reset, new map; the wear overlay; health metrics; a debug API on `window.__tt` for tests.

Round 4 added rain and mud, house growth by the frontage rules and sound made in code (below). Not yet (from the step 1 build list in the design doc): placeable buildings; tool costs (the toy has no money), night-closing gates, stepping stones and toll gates. Hollow ways need wear above 85 held for 10 in-game years (40 minutes at 1x), so they will rarely appear in a session; they are drawn in their own colour when they do.

## How it works

- `src/sim/` is pure TypeScript with no DOM and no `Math.random`. A seed plus the same inputs at the same ticks always gives the same run. The simulation ticks at 10 Hz of sim time; higher speeds run more ticks per frame.
- **Routing.** Each building door has a cost-to-go field over the 8-neighbour grid. Fields are rebuilt in rotation, each every 5 sim seconds, and one per tick, destinations first, right after an edit. The search is Dijkstra's algorithm with a bucket queue (buckets a little narrower than the cheapest step), which gives bit-for-bit the values of a binary-heap Dijkstra at about half the cost (about 1 ms a field). Per-trip A* would cost 3 to 5 ms a trip, and 500 walkers at 8x make about 1,000 trips a second.
- **Route cost of worn ground.** Two discounts multiply: a smooth convex wear discount `1 - 0.3 * min(1, W/80)^3` (a lane worn by one house gets almost none; lanes shared by several houses do, which is what makes house paths merge into trunks) and a flow discount `1 - 0.5 * (1 - exp(-flow/40))`, where `flow` is a per-cell footstep count that halves every 20 s (walkers join the lane others are using now). The product is floored at `minSurface` 0.45. The ford is not discounted and costs 4 times grass. A footbridge deck gets the flow discount (not the wear one), so a new bridge starts at full cost.
- **Frugal walkers.** Busy trunks are cheap, so without help walkers would keep to long detours and ignore new bridges and gaps. Every building therefore has a second, frugal field, on costs that keep only 30% of the wear and flow discount (`frugalDisc`). A trip uses it if the walker is one of the 10% "explorers" (fixed per walker by a hash of its id), or if the ordinary route from where it stands is more than 1.25 times the straight distance (`detourCap`). This is what keeps new footbridges and fence gaps winning traffic while trunks stay strong.
- **Gravel and gates.** A gravel cell's surface cost is at most 0.6 of grass (traffic can make it cheaper, down to the 0.45 floor of a trunk); walkers walk 1.15 times faster on it; worn ground under it never decays below trail and never turns to mud; erasing it leaves the trail beneath; a fence laid on it takes it up. A gate is a flag on a fence cell: walkable, with 1.5 added to its step cost (not scaled by crowding) and 0.4 of the walking speed, which is about the same delay. Where a gate goes is decided in `src/sim/gates.ts`: a cell takes a gate when opening it joins two separate pieces of open ground beside it under the walkers' own step rule (8 neighbours, no cutting a blocked corner); on a diagonal staircase where no single cell does, the cell and one fence neighbour (a stair's corner pair) become one gate of two cells with half the delay each. Removing a fence cell removes its gate (and turns the other half of a two-cell gate into a one-cell gate).
- **Flow preview** (`src/sim/preview.ts`). Trip counts per (house, destination) decay with a 3-day time constant and read as trips per day. A preview copies a cached base cost (refreshed each sim second), applies the hypothetical change, rebuilds the ordinary and the frugal field of each destination (costs are symmetric, so a destination's field serves every house) with an early exit once every house door is settled, and splits each pair's trips the way the walkers do: the house's explorers on the frugal fields, the others on the ordinary fields unless their route is over the detour cap before or after the change. A group of trips counts as rerouted when its cost changes by more than 3% or its typical route (traced with the walkers' own aim rule, `chooseAim` in `path.ts`, with no taste and no noise) moves more than 3 cells, or it becomes unreachable. Only the shown routes are traced when the cost change alone decides. The preview writes only scratch arrays; the page runs it as a generator, a few ms per frame, at most every 100 ms, and drops a result computed across an edit.
- **Walking.** A walker looks 20 cells down its target's field (with its own taste and a small per-cell noise) and heads in a straight line for the farthest of those cells it can reach in clear sight, as long as that line costs no more than the field's route. That gives trails at any angle instead of 8-direction staircases, while walkers still keep to a trail that bends.
- **Wear.** Entering a cell adds `I * (1 - W/100)` to it and a share of that to the two cells either side of the heading. Wear decays with a half-life that depends on its band.
- **Blocked walkers.** A walker on a newly fenced cell is moved to the nearest open cell; a walker cut off from its errand heads home; a walker with no way forward for 5 s is sent home.
- **Drawing** (`src/ui/`). Worn ground (`ground.ts`) is a 512 x 512 layer bilinearly sampled from the wear grid and coloured through a lookup table by wear, traffic level (8) and mud level (6), with alpha, a fixed mottling, a per-pixel nudge of the value at trail edges, a 3 x 3 blur that widens the trampled fringe, and a gentle warp of up to 0.45 cells so long straight legs wander a little. It is rebuilt every 250 ms (about 3.3 ms). Wet ground is a multiply over the composited ground. Fences (`ink.ts`) are chains of cells smoothed (two 3-point averages with real corners held) and simplified into straight rails; gates are cut out of the rail. Rain, new houses and tool marks are drawn each frame by `effects.ts` from one pre-drawn rain tile and fixed buffers, with no allocation per frame.

## Tunable parameters

All of them are in `src/sim/params.ts`. The ones that differ from the design spec:

| Parameter | Spec | Here | Why |
| --- | --- | --- | --- |
| `walkSpeed` | 0.67 cells/s (1.34 m/s) | 3 cells/s | Time compression: at real speed a town of 200 crosses too few cells a day for any trail to form |
| `dwellSimSec` | | 6 s | Same reason |
| `wearPerStep` | 0.75 | 2.2 | Round 3 tuning on seeds 1 to 20, 101 to 120 and 201 to 220 |
| `sideShare` | 0.15 | 0.4 | Wider soft side wear: about +1 point of trunk share and better bridge shares, without fattening trails (median trunk width 3 cells) |
| `halfLifeLow/Mid/High` | 60 / 120 / 240 s | 60 / 120 / 240 s | Back to the spec in round 2 |
| `trailDiscount`, `discScale`, `discPow` | table 1.00 to 0.72 | 0.3, 80, 3 | Smooth convex wear discount; replaces the four-step table for routing (the table still sets ground states and speeds) |
| `flowDiscount`, `flowScale`, `flowHalfLife` | | 0.5, 40, 20 s | Research: the flow discount is what merges house paths into trunks |
| `minSurface` | trail discount capped at 0.72 | 0.45 | Floor on the combined discount (round 2: 0.6). About +1.5 points of trunk share per 0.05; safe for bridges only with the three below |
| `explorerShare`, `frugalDisc`, `detourCap` | | 0.1, 0.3, 1.25 | Frugal walkers (see How it works). The detour cap is what keeps footbridges winning; 15% explorers fattened trails |
| `bridgeFlow` | | 1 | A bridge deck gets the flow discount |
| `fordCostMul` / `fordSpeedMul` | speed 0.4 | 4 / 0.4 | The ford is slow and walkers avoid it when there is a bridge (round 2: 3) |
| `bridgeCost` / `bridgeSpeedMul` | | 0.8 / 1.2 | The spec gives none; a footbridge is "cheap and fast" |
| `gravelCost` / `gravelSpeedMul` / `gravelFloor` | 0.80 / 1.15 / | 0.6 / 1.15 / 0.45 | Round 4. The toy's worn ground is cheaper than the spec's (a trunk costs 0.45 against the spec's 0.72), so at 0.8 gravel only pulled a trail within a cell or two (see Round 4 below) |
| `gateCost` / `gateSpeedMul` | 1.5 s | 1.5 / 0.4 | One cost unit per grass cell, so +1.5; the speed gives about the same delay |
| `stepNoise` | | 0.06 | Per-walker, per-cell noise on a step; spreads lanes a little |
| `lookahead` | | 20 cells | How far a walker looks down the field to aim a straight line |
| `urgentFieldsPerTick` | | 1 | After an edit the fields are rebuilt one per tick, destinations first, instead of three at once |
| `tripRateDays` | | 3 | Time constant of the preview's trips-per-day counts |
| `rainChance` | spring 30%, summer 15%, autumn 40% | the same; winter 0 | Winter has snow only in the spec, which the toy leaves out |
| `mudDays` | that day plus one, plus two in autumn | 2, 2, 3, 2 days | See the conflict note under Rain and mud |
| `mudCostMul`, `mudReplace` | trail x1.6, track x1.7, hollow way x1.8 | the same, on the walker's own cost (`mudReplace` 0) | The spec does not say what the multiplier applies to |
| `mudSpeedMul` | | 0.8 | Not in the spec (its speed column has no weather); the round asked for mud that slows walkers |
| `wetUpSimSec`, `drySimSec`, `rainRampSimSec` | | 3 s, 7.5 s, 2 s | Not in the spec: the ground wets over 3 s, mud dries over the last half day of its run, rain fades in and out over 2 s (drawing and sound) |
| `houseEverySimSec`, `houseFirstSimSec` | Hamlet: 1 household per 2 days | 30 s, 30 s | As the spec |
| `hamletTarget`, `householdSize` | bell target 250; cottage 3 people | 250 people, 3 | The spec gives no household cap, only the Hamlet target, whose unit it does not state; the toy reads it as people |
| `frontageWear`, `plotClearWear`, `frontageFloor`, `plotNeighbourRadius` | 50, 25, 50, 6 | the same | As the spec |

Unchanged from the spec: the state thresholds (10, 25, 50, 85), state speeds, taste (plus or minus 8%), the crop penalty (+0.5), and the Weidmann curve (v0 1.34 m/s, rho max 5.4, gamma 1.913; crowding cost capped at 3x).

The page caps simulation work at 10 ms per frame (and 8 ticks); on a slow machine the sim runs slower than the chosen speed instead of stalling. The cap is kept honestly: the page tracks the cost of a plain tick and of a heavy one (a tick that rebuilds a route field, or in which a household arrives, about four times a plain one; `World.nextTickRefreshes()` says which is next) and starts a tick only if it should still fit (the first tick of a frame always runs). The flow preview builds its base costs in four slices, so the first preview of a drag does not stall a frame. Neither changes any result.

## Rain and mud (round 4)

Spec: "Weather and mud", "Ground states" and "Seasons and weather" (Gameplay tab). Code: `src/sim/weather.ts` and `World.updateMud`.

- **Weather stream.** The weather has a random stream of its own, seeded from the map seed. It rolls once at the start of every in-game day (15 s): rain with a chance of 30% in spring, 15% in summer, 40% in autumn and none in winter. Walker randomness never changes: the same seed rains on the same days with 50 or 300 walkers, and a forced shower does not move the daily rolls.
- **Mud.** While it rains, every unpaved land cell at wear 25 or more (trail, track, hollow way; not gravel, a bridge or the ford) is mud. Mud stays on cells still worn to trail until the ground is dry: for the rain day plus one more (plus two in autumn), the last half day drying out. A rain day during mud extends it.
- **Wetness** (0 to 1) rises over 3 s when rain starts and falls to 0 over the last 7.5 s of the mud. Costs and speeds on mud scale with it.
- **Cost.** An ordinary walker's step on mud costs its dry cost times 1.6 on trail, 1.7 on track and 1.8 on a hollow way. A busy trunk (0.45) becomes 0.72, still cheaper than grass; a lightly worn trail (about 0.95) becomes about 1.5, dearer than the grass beside it, so walkers step beside light mud and keep to the trunks (in the test, walker time on lightly worn trail falls by about 40% in rain and time on the grass beside rises). Frugal walkers see 0.3 of the muddy cost, as they see 0.3 of all worn ground. Gravel never turns to mud, so in rain it is the cheapest ground there is.
- **Speed.** Walkers are 0.8 times as fast on mud at full wetness.
- **Switch.** `PARAMS.rainOn` (true: the player's default). `new World(seed, walkers, { rain: false })` turns it off; the old gate sets run with it off. The page's debug API has `__tt.startRain(days)`, `__tt.stopRain(dry)`, `__tt.setRules({ rain, growth })` and `__tt.weather()`.
- **State for drawing and sound:** `world.weather.rain` (how hard it rains now, 0 to 1, fading in and out), `world.weather.wetness` (0 to 1), `world.weather.raining`, `world.mud` (a per-cell flag) and `world.mudCells` (a count); `__tt.weather()` returns them with the rain day count.
- **Spec conflict for the lead:** autumn mud lasts "two more" days (3 in all) in "Weather and mud", "Seasons and weather" and the prototype test plan, but "mud lasts 2 days" in the autumn row of "The season cycle". The toy uses 3.
- **Not in the spec, chosen here:** winter rain (none), what the mud multiplier applies to, wear gain in rain (unchanged), walking speed on mud, the drying rate, stacked rain days (mud lasts to the later end).
- **How the cost was chosen** (tuning seeds 1-20, 101-120 and 201-220, rain on, growth off unless said): with the multiplier on the spec's base cost for the state (trail 0.85 x 1.6 = 1.36, dearer than grass on every trunk) walkers left every trail in rain, coverage doubled to about 10%, and trunk share left the 30% to 70% band on 30 of 60 seeds (21% to 78%). Multiplying the walker's own cost: 1 seed of 60 out of band; with growth on as well, 2 of 60 (and 2 to 4 of 60 at mud speeds 0.8, 0.9 and 1.0). The braiding came from the frugal walkers, who saw the full multiplier on a cost that keeps only 0.3 of the trail discount, so every muddy trail was dearer than grass to them. With frugal walkers seeing 0.3 of the muddy cost as of all ground: 0 of 60 out of band, lowest daily 34.7%, highest 56.1%, mean coverage 5.5% (5.4% without rain).

## House growth (round 4)

Spec: "Where houses appear", "Growth rate and density", "Frontage", "Households and trips". Code: `src/sim/growth.ts` and `World.addHouse`.

- **Arrival.** The toy has no economy, so a Hamlet household arrives at the spec's capped rate: one every 2 in-game days (30 s), the first after 2 days, while one more household keeps the town at or under the Hamlet target of 250 people.
- **Population rule.** The Walkers slider sets the population now (the starting population on a new map). Growth then adds 3 villagers with each new cottage until the town would pass 250. From the default 200 that is 16 houses (248 people) by about day 33; a slider at 248 or more means no growth. Moving the slider later adds or removes villagers at once (spread over all houses, grown ones too), and growth carries on from the new number.
- **The house.** A Hamlet cottage: 3 x 3 cells with its garden, all blocking walkers except the door, which is in the middle of the side facing its frontage. Each brings 3 villagers who live there and walk to the well, field and market like everyone else.
- **Plot rules** (all must pass): (1) the door faces a land cell at wear 50 or more (the frontage); (2) none of the 9 cells is at wear 25 or more, so a house never sits on a trail; (3) dry free land: no water, ford, bridge, field, fence, gravel, building or door, a cell clear of the water's edge and 2 cells from the map's edge (the toy has no flood band, pastures or slope); (4) a one-cell gap from every other building (Hamlet rule); (5) it blocks no way through: every door walkers can reach stays reachable (a flood fill with and without the plot, on the best 8 plots).
- **Score:** 3 x frontage wear / 100, plus access (the mean over the well, field and market of 1 - route cost / 100, at least 0), plus 1 per house within 6 cells (up to 3), minus 1 if the frontage is mud, minus crowding there (up to 1). The best plot is built; ties go to the first in scan order. The spec's access weights and mill noise are not specified or not in the toy.
- **Frontage.** The frontage cell and its neighbours along the plot's front that are worn to track are fronted: they do not decay below wear 50 while the house stands.
- **Deterministic.** Growth draws no random numbers except the new villagers' dwell and taste, from a stream of its own (not the walkers'), so a seed and the same inputs give the same houses.
- **Switch.** `PARAMS.growthOn` (true: the player's default); `{ growth: false }` turns it off, and the old gate sets run with it off.
- **State for drawing:** `world.grown`, one entry per new house in order: building index, x, y, w, h, door, frontage cell, sim time and in-game day built; `world.buildings` includes them. `__tt.houses()` returns them with the population, the target and the next arrival time.
- **The flow preview** follows: it rebuilds its fields when the building count changes, counts pairs from 0.2 trips a day (a cottage's 3 villagers make about 0.3 to 0.4 a day to each destination; round 3's 0.5 hid them), and always reports a house it would cut off, however few its trips.
- **Left out** (spec sheet): demand from jobs and trade, upgrades, abandonment and ruin, frontage rent, other house types, demolition.

## Gate status (round 4)

`npm run check`, 25 November 2026. G3 passes on all six sets.

| Seeds | Rules | Mean | Lowest daily | Highest daily | Seeds in band |
| --- | --- | --- | --- | --- | --- |
| 601 to 620 | rain and house growth on, player defaults; first run after the rules were final | 46.3% | 37.8% | 55.3% | 20 of 20 |
| 401 to 420, 1 to 20, 101 to 120, 201 to 220, 301 to 320 | rain and growth off | as round 3 (below) | | | 100 of 100 |

With rain and growth off, the five old sets give the round 3 numbers bit for bit: `npm test` compares every per-seed row (with a hash of the float64 bits of every daily sample) with `test/g3-baseline.txt`, which was recorded from the round 3 source before any round 4 change. On 601 to 620 (report only): detour 1.106 (1.057 to 1.199), coverage 5.7% (4.4% to 7.6%), first trail 9.4 s, 16 houses and 248 villagers by day 45 on every seed, 4 to 14 rain days. The tuning seeds with both on (1-20, 101-120, 201-220): lowest daily 34.7%, highest 56.1%, 0 of 60 out of band.

Gravel pull (`gravelCost` 0.6 against the spec's 0.8; tuning seeds 1-8, 101-108, 201-208; a 24-cell strip parallel to the busiest trail; share of the strip's and the trail's footsteps on the strip over the last 2 of 6 days, without gravel / with it):

| Offset | Day 4, gravel 0.8 | Day 4, gravel 0.6 | Day 20, gravel 0.8 | Day 20, gravel 0.6 |
| --- | --- | --- | --- | --- |
| 1 cell | 24% / 38% | 24% / 53% | 22% / 40% | 22% / 55% |
| 3 cells | 2% / 12% | 2% / 58% | 2% / 12% | 2% / 57% |
| 5 cells | 1% / 8% | 1% / 55% | 1% / 3% | 1% / 53% |

At 0.6 the strip takes most of the traffic on 11 to 16 of 24 seeds at every offset and age, and 85% to 100% of its cells wear to trail. On mature maps (day 20) a strip 5 cells from the busiest trunk takes most of its traffic on 13 of 24 seeds (at 0.8: none), because busy gravel gets the same traffic discount as a trunk, down to the same 0.45 floor. The footbridge test is unchanged (mean 72%, lowest 38%).

## Gate status (round 3)

G3 passes on all five seed sets, judged on every daily sample from day 20 to day 45 at 200 walkers (`npm test`):

| Seeds | Role | Mean | Lowest daily | Highest daily | Seeds in band | Median trunk width |
| --- | --- | --- | --- | --- | --- | --- |
| 401 to 420 | fresh, first run after the model was final | 43.8% | 34.9% | 52.0% | 20 of 20 | 3 cells |
| 1 to 20 | round 3 tuning | 43.4% | 34.2% | 53.4% | 20 of 20 | 3 cells |
| 101 to 120 | round 3 tuning | 44.6% | 34.5% | 54.2% | 20 of 20 | 3 cells |
| 201 to 220 | round 3 tuning | 43.5% | 35.8% | 53.6% | 20 of 20 | 3 cells |
| 301 to 320 | never tuned on | 43.3% | 34.0% | 51.6% | 20 of 20 | 3 cells |

A new footbridge on the market's row takes 72% of river crossings on seeds 1 to 10 (lowest 38%, seed 7). The headroom above 30% is about 4 points; seed 403's lowest day is 34.9%.

## Health metrics

Defined in `src/sim/metrics.ts`, over the last 2 in-game days:

- **Detour ratio**: mean of actual route length / straight-line length over completed trips longer than 3 cells. Target 1.05 to 1.30.
- **Trail coverage**: share of land cells (not water, not buildings) at trail state or above. Hamlet target 3% to 8%.
- **Trunk share**: footsteps on the busiest 10% of trail cells / all footsteps. Gate: 30% to 70%.
- **Trunk share D**: the same numerator / footsteps on trail cells only (the research's variant, which removes the dependence on coverage). Reported, not gated.
- **Trunk width** (`trailWidth`, tests only): for the busiest 10% of trail cells, the run of trail cells straight across the trail's local direction; the median, on day 30. Reported, not gated.

`npm test` judges gate G3 on fresh seeds 401 to 420 and on the earlier sets 1 to 20, 101 to 120, 201 to 220 and 301 to 320, at 200 walkers over 45 in-game days: on every set, every daily trunk-share sample from day 20 to day 45 must lie within 30% to 70%, and a first trail (a connected run of 10 trail cells) must form within 3 in-game days on the fresh seeds. It prints a full table for 401 to 420 and a line per set (with any out-of-band seeds), the footbridge share on seeds 1 to 10 and 500 walkers on seeds 1 to 5 (report only). With rain and growth off, the five sets must match `test/g3-baseline.txt` bit for bit. In round 4 it also judges G3 on seeds 601 to 620 with rain and house growth on at the player's defaults and prints their detour, coverage, first trail, houses and rain days (report only), and runs the round 4 rule checks in `test/rules.test.ts`: gates on fences at 0, 30, 45, 60 and 90 degrees place and carry walkers; gate refusals give their reasons; snapping within 2 cells; a fence over gravel; a far gravel strip says so; rain makes worn ground mud that costs more and slows walkers, walkers step beside light mud, mud never on gravel, water or bridges, mud lasts its days and dries; the weather depends on the seed only; houses appear only on valid frontage, never cut a building off, bring 3 villagers each and stop at the target; weather and growth are deterministic; previews stay isolated with both on and see grown houses. It also runs the reroute, bridge, gravel, gate, robustness and determinism tests, and the flow preview tests: isolation (200 preview queries leave the run byte-identical), an empty change, speed, bridge, erase, gate, gravel, unreachable, and agreement with the walkers (the predicted detour is where the new trail forms, and the readout is within 15% of the trips a day that then take the new route, each trip checked against its own group's predicted route).

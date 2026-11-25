# Prototype 1: desire lines

A throwaway browser toy that answers one question for Tide & Toll: **is steering worn paths fun?**

Villagers walk between their houses, the well, the field and the market. Every footstep wears the grass. Grass turns to trampled ground, then trail, then track. Paths nobody uses grow back. You never draw a road: you place fences, gates, gravel and footbridges, and the walkers find their own way.

The page is one self-contained file, `dist/index.html`. It has no runtime libraries, fetches nothing and is published as a claude.ai Artifact.

## Run it

```sh
npm install          # typescript and esbuild only
npm run build        # writes dist/index.html; open it in a browser
npm run check        # typecheck + build + headless simulation tests (about 5 minutes on 4 cores)
npm run perf         # Playwright: frame times at 500 and 200 walkers, 1x and 8x; a 4x CPU-throttled run; preview while dragging
npm run smoke        # Playwright: page contract, every control and tool, the flow preview, 400 px layout in both themes, touch at 400 x 800
```

`npm test -- --quick` runs the gate on 4 fresh seeds (401 to 404) and skips the other sets and the report tables. The full run uses worker threads. Playwright is not a dependency: the perf and smoke scripts use the copy installed globally in the build container.

Add `#seed=42` to the page URL to open a given map.

## Controls

| Control | Key | What it does |
| --- | --- | --- |
| Pause | Space | Stops and starts the simulation clock (the button stays "Pause" and shows pressed while paused) |
| Speed 1x 2x 4x 8x | 1 2 3 4 | At 1x one in-game day is 15 s and a season is 60 s |
| Walkers | | 50 to 500 villagers (default 200) |
| Fence | F | Drag to draw a fence line; it is placed on release. Walkers cannot cross it |
| Gravel | G | Drag to brush a strip of gravel; it is laid on release, and snaps onto a worn trail's centre line when one is within a cell. Gravel costs walkers 0.6 of grass (busy gravel gets cheaper still, down to the same 0.45 floor as a trunk), never grows back below trail and never turns to mud, so a trail up to about 5 cells away moves onto it and stays. A fence laid over gravel takes the gravel up. When a strip changes no route and no trail is within 6 cells of it, the readout says "Too far from any route to change anything" |
| Gate | T | Click (or tap) a fence to put a gate in it: walkers pass at a cost of 1.5 grass cells and at 0.4 of their speed, so a trail re-forms through it. On a gate, the tool takes it out again. It snaps to the nearest fence cell within 2 cells that can take a gate. A cell takes a gate when opening it joins the open ground on the two sides of the fence (any angle: on a diagonal fence, which is a staircase of cells, a stair's corner pair becomes one two-cell gate with half the delay in each cell). Otherwise the status line says why: "Gates go on fences: there is no fence here.", "No way through here: both sides are blocked." or "A gate needs fence beside it: this is a lone post." |
| Footbridge | B | Hover over the river to preview, click to lay a bridge (not next to the ford). On touch: press to preview, release on the same spot to build, slide away to cancel |
| Erase | E | Hover to preview what goes; click or drag over fences (with their gates), gravel and bridges to remove them. On touch: press to preview, release to remove, or drag |
| Flow preview | P | On by default. While a tool is in use, shows the typical routes that would change: dashed is the route now, solid with arrows is the route after, both faint where they run together. The readout, such as "Reroutes about 20 walkers a day (+13% longer on average)", is in the status line, and next to the pointer with a mouse (not under a finger). The percentage is the trip-weighted mean cost change over the rerouted trips. A cut-off destination gets a red ring and "Blocks the Well for 3 houses" |
| Wear overlay | W | Heat map of wear (0 to 100) over a dimmed map |
| Reset | R | Restarts the same map |
| New map | N | Generates a new map from a new seed |
| | Esc | Puts the tool down |

At phone width the controls wrap into rows of buttons at least 40 px high, and the map sits under them on the first screen. The status bar keeps a fixed height (one line on desktop, two on a phone), so the map never moves when a message or the readout appears; long text is cut with an ellipsis (the full text is in its tooltip).

The side panel shows the day and season, the walkers out, the frame rate, and three health metrics: detour ratio, trail coverage and trunk share (see below).

## What is in, and what is not

In: a 128 x 128 meadow of 2 m cells, a meandering river with one ford, 6 to 8 houses, a well, a field and a market across the river; wear with saturation, side spread and state-based decay; five ground states plus gravel; walkers routed on wear-weighted cost-to-go fields with Weidmann crowding and per-walker taste, and a frugal minority (below); fences, gates, gravel, footbridges and erase; pause, speed, reset, new map; the wear overlay; health metrics; a debug API on `window.__tt` for tests.

Round 4 added rain and mud and house growth by the frontage rules (below). Not yet (from the step 1 build list in the design doc): sound, placeable buildings; tool costs (the toy has no money), night-closing gates, stepping stones and toll gates. Hollow ways need wear above 85 held for 10 in-game years (40 minutes at 1x), so they will rarely appear in a session; they are drawn in their own colour when they do.

## How it works

- `src/sim/` is pure TypeScript with no DOM and no `Math.random`. A seed plus the same inputs at the same ticks always gives the same run. The simulation ticks at 10 Hz of sim time; higher speeds run more ticks per frame.
- **Routing.** Each building door has a cost-to-go field over the 8-neighbour grid. Fields are rebuilt in rotation, each every 5 sim seconds, and one per tick, destinations first, right after an edit. The search is Dijkstra's algorithm with a bucket queue (buckets a little narrower than the cheapest step), which gives bit-for-bit the values of a binary-heap Dijkstra at about half the cost (about 1 ms a field). Per-trip A* would cost 3 to 5 ms a trip, and 500 walkers at 8x make about 1,000 trips a second.
- **Route cost of worn ground.** Two discounts multiply: a smooth convex wear discount `1 - 0.3 * min(1, W/80)^3` (a lane worn by one house gets almost none; lanes shared by several houses do, which is what makes house paths merge into trunks) and a flow discount `1 - 0.5 * (1 - exp(-flow/40))`, where `flow` is a per-cell footstep count that halves every 20 s (walkers join the lane others are using now). The product is floored at `minSurface` 0.45. The ford is not discounted and costs 4 times grass. A footbridge deck gets the flow discount (not the wear one), so a new bridge starts at full cost.
- **Frugal walkers.** Busy trunks are cheap, so without help walkers would keep to long detours and ignore new bridges and gaps. Every building therefore has a second, frugal field, on costs that keep only 30% of the wear and flow discount (`frugalDisc`). A trip uses it if the walker is one of the 10% "explorers" (fixed per walker by a hash of its id), or if the ordinary route from where it stands is more than 1.25 times the straight distance (`detourCap`). This is what keeps new footbridges and fence gaps winning traffic while trunks stay strong.
- **Gravel and gates.** A gravel cell's surface cost is at most 0.8 of grass (traffic can still make it cheaper); walkers walk 1.15 times faster on it; worn ground under it never decays below trail; erasing it leaves the trail beneath. A gate is a flag on a fence cell: walkable, with 1.5 added to its step cost (not scaled by crowding) and 0.4 of the walking speed, which is about the same delay. Removing a fence cell removes its gate.
- **Flow preview** (`src/sim/preview.ts`). Trip counts per (house, destination) decay with a 3-day time constant and read as trips per day. A preview copies a cached base cost (refreshed each sim second), applies the hypothetical change, rebuilds the ordinary and the frugal field of each destination (costs are symmetric, so a destination's field serves every house) with an early exit once every house door is settled, and splits each pair's trips the way the walkers do: the house's explorers on the frugal fields, the others on the ordinary fields unless their route is over the detour cap before or after the change. A group of trips counts as rerouted when its cost changes by more than 3% or its typical route (traced with the walkers' own aim rule, `chooseAim` in `path.ts`, with no taste and no noise) moves more than 3 cells, or it becomes unreachable. Only the shown routes are traced when the cost change alone decides. The preview writes only scratch arrays; the page runs it as a generator, a few ms per frame, at most every 100 ms, and drops a result computed across an edit.
- **Walking.** A walker looks 20 cells down its target's field (with its own taste and a small per-cell noise) and heads in a straight line for the farthest of those cells it can reach in clear sight, as long as that line costs no more than the field's route. That gives trails at any angle instead of 8-direction staircases, while walkers still keep to a trail that bends.
- **Wear.** Entering a cell adds `I * (1 - W/100)` to it and a share of that to the two cells either side of the heading. Wear decays with a half-life that depends on its band.
- **Blocked walkers.** A walker on a newly fenced cell is moved to the nearest open cell; a walker cut off from its errand heads home; a walker with no way forward for 5 s is sent home.
- **Drawing** (`src/ui/`). Worn ground is a 512 x 512 layer bilinearly sampled from the wear grid, coloured through a lookup table with alpha, with a fixed mottling and a gentle warp of up to 0.45 cells so long straight legs wander a little (drawing only; the simulation is untouched). Fences are continuous rails through the cell centres (diagonal neighbours join up) with posts at ends and corners; a gate is a leaf standing ajar in the gap; gravel is a scatter of grey stones.

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
| `gravelCost` / `gravelSpeedMul` | 0.80 / 1.15 | 0.8 / 1.15 | As the spec |
| `gateCost` / `gateSpeedMul` | 1.5 s | 1.5 / 0.4 | One cost unit per grass cell, so +1.5; the speed gives about the same delay |
| `stepNoise` | | 0.06 | Per-walker, per-cell noise on a step; spreads lanes a little |
| `lookahead` | | 20 cells | How far a walker looks down the field to aim a straight line |
| `urgentFieldsPerTick` | | 1 | After an edit the fields are rebuilt one per tick, destinations first, instead of three at once |
| `tripRateDays` | | 3 | Time constant of the preview's trips-per-day counts |

Unchanged from the spec: the state thresholds (10, 25, 50, 85), state speeds, taste (plus or minus 8%), the crop penalty (+0.5), and the Weidmann curve (v0 1.34 m/s, rho max 5.4, gamma 1.913; crowding cost capped at 3x).

The page caps simulation work at 10 ms per frame (and 8 ticks); on a slow machine the sim runs slower than the chosen speed instead of stalling.

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

`npm test` judges gate G3 on fresh seeds 401 to 420 and on the earlier sets 1 to 20, 101 to 120, 201 to 220 and 301 to 320, at 200 walkers over 45 in-game days: on every set, every daily trunk-share sample from day 20 to day 45 must lie within 30% to 70%, and a first trail (a connected run of 10 trail cells) must form within 3 in-game days on the fresh seeds. It prints a full table for 401 to 420 and a line per set (with any out-of-band seeds), the footbridge share on seeds 1 to 10 and 500 walkers on seeds 1 to 5 (report only). It also runs the reroute, bridge, gravel, gate, robustness and determinism tests, and the flow preview tests: isolation (200 preview queries leave the run byte-identical), an empty change, speed, bridge, erase, gate, gravel, unreachable, and agreement with the walkers (the predicted detour is where the new trail forms, and the readout is within 15% of the trips a day that then take the new route, each trip checked against its own group's predicted route).

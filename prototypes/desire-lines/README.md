# Prototype 1: desire lines

A throwaway browser toy that answers one question for Tide & Toll: **is steering worn paths fun?**

Villagers walk between their houses, the well, the field and the market. Every footstep wears the grass. Grass turns to trampled ground, then trail, then track. Paths nobody uses grow back. You never draw a road: you place fences and footbridges, and the walkers find their own way round.

The page is one self-contained file, `dist/index.html`. It has no runtime libraries, fetches nothing and is published as a claude.ai Artifact.

## Run it

```sh
npm install          # typescript and esbuild only
npm run build        # writes dist/index.html; open it in a browser
npm run check        # typecheck + build + headless simulation tests (about 3 minutes on 4 cores)
npm run perf         # Playwright: frame times at 500 and 200 walkers, 1x and 8x; a 4x CPU-throttled run; preview while dragging
npm run smoke        # Playwright: page contract, every control, the flow preview, 400 px layout, both themes
```

`npm test -- --quick` runs the gate on 4 held-out seeds and skips the report tables. The full run uses worker threads. Playwright is not a dependency: the perf and smoke scripts use the copy installed globally in the build container.

Add `#seed=42` to the page URL to open a given map.

## Controls

| Control | Key | What it does |
| --- | --- | --- |
| Pause | Space | Stops and starts the simulation clock (the button stays "Pause" and shows pressed while paused) |
| Speed 1x 2x 4x 8x | 1 2 3 4 | At 1x one in-game day is 15 s and a season is 60 s |
| Walkers | | 50 to 500 villagers (default 200) |
| Fence | F | Drag to draw a fence line; it is placed on release. Walkers cannot cross it |
| Footbridge | B | Hover over the river to preview, click to lay a bridge (not next to the ford). On touch: press to preview, release on the same spot to build, slide away to cancel |
| Erase | E | Hover to preview what goes; click or drag over fences and bridges to remove them. On touch: press to preview, release to remove, or drag |
| Flow preview | P | On by default. While a tool is in use, shows the typical routes that would change: dashed is the route now, solid with arrows is the route after, and a readout such as "Reroutes about 20 walkers a day (+13% longer)" near the pointer and in the status line. A cut-off destination gets a red ring and "Blocks the Well for 3 houses" |
| Wear overlay | W | Heat map of wear (0 to 100) over a dimmed map |
| Reset | R | Restarts the same map |
| New map | N | Generates a new map from a new seed |
| | Esc | Puts the tool down |

The side panel shows the day and season, the walkers out, the frame rate, and three health metrics: detour ratio, trail coverage and trunk share (see below).

## What is in, and what is not

In: a 128 x 128 meadow of 2 m cells, a meandering river with one ford, 6 to 8 houses, a well, a field and a market across the river; wear with saturation, side spread and state-based decay; five ground states; walkers routed on wear-weighted cost-to-go fields with Weidmann crowding and per-walker taste; fences, footbridges and erase; pause, speed, reset, new map; the wear overlay; health metrics; a debug API on `window.__tt` for tests.

Not yet (from the step 1 build list in the design doc): gravel and gate tools, rain and mud, placeable buildings and house growth by frontage. Hollow ways need wear above 85 held for 10 in-game years (40 minutes at 1x), so they will rarely appear in a session.

## How it works

- `src/sim/` is pure TypeScript with no DOM and no `Math.random`. A seed plus the same inputs at the same ticks always gives the same run. The simulation ticks at 10 Hz of sim time; higher speeds run more ticks per frame.
- **Routing.** Each building door has a Dijkstra cost-to-go field over the 8-neighbour grid (about 3 ms each). Fields are rebuilt in rotation, each every 5 sim seconds, and all at once (3 per tick) right after a fence or bridge changes. Per-trip A* would cost 3 to 5 ms a trip, and 500 walkers at 8x make about 1,000 trips a second.
- **Route cost of worn ground.** Two discounts multiply: a smooth convex wear discount `1 - 0.3 * min(1, W/80)^3` (a lane worn by one house gets almost none; lanes shared by several houses do, which is what makes house paths merge into trunks) and a flow discount `1 - 0.5 * (1 - exp(-flow/40))`, where `flow` is a per-cell footstep count that halves every 20 s (walkers join the lane others are using now). The product is floored at `minSurface` 0.6, so a busy trunk can be at most 1.67 times the beeline and new footbridges and fence gaps still win traffic. The ford is not discounted.
- **Flow preview** (`src/sim/preview.ts`). Trip counts per (house, destination) decay with a 3-day time constant and read as trips per day. A preview copies a cached base cost (refreshed each sim second), applies the hypothetical change, rebuilds one field per destination (costs are symmetric, so a destination's field serves every house) with an early exit once every house door is settled, and traces each house's typical route with the walkers' own aim rule (`chooseAim` in `path.ts`, with no taste and no step noise). A pair counts as rerouted when its cost changes by more than 3%, its route moves more than 3 cells anywhere, or it becomes unreachable. It writes only scratch arrays; the page runs it as a generator, a few ms per frame, at most every 100 ms.
- **Walking.** A walker looks 20 cells down its target's field (with its own taste and a small per-cell noise) and heads in a straight line for the farthest of those cells it can reach in clear sight, as long as that line costs no more than the field's route. That gives trails at any angle instead of 8-direction staircases, while walkers still keep to a trail that bends.
- **Wear.** Entering a cell adds `I * (1 - W/100)` to it and a share of that to the two cells either side of the heading. Wear decays with a half-life that depends on its band.
- **Blocked walkers.** A walker on a newly fenced cell is moved to the nearest open cell; a walker cut off from its errand heads home; a walker with no way forward for 5 s is sent home.

## Tunable parameters

All of them are in `src/sim/params.ts`. The ones that differ from the design spec:

| Parameter | Spec | Here | Why |
| --- | --- | --- | --- |
| `walkSpeed` | 0.67 cells/s (1.34 m/s) | 3 cells/s | Time compression: at real speed a town of 200 crosses too few cells a day for any trail to form |
| `dwellSimSec` | | 6 s | Same reason |
| `wearPerStep` | 0.75 | 2.0 | Round 2 tuning on seeds 1 to 20 (research 1.8; raised to 2.0 with the floor below to keep the trunk-share minimum above 30%) |
| `sideShare` | 0.15 | 0.3 | Research: narrow trails (about 2 cells) with soft edges |
| `halfLifeLow/Mid/High` | 60 / 120 / 240 s | 60 / 120 / 240 s | Back to the spec in round 2 |
| `trailDiscount`, `discScale`, `discPow` | table 1.00 to 0.72 | 0.3, 80, 3 | Smooth convex wear discount; replaces the four-step table for routing (the table still sets ground states and speeds) |
| `flowDiscount`, `flowScale`, `flowHalfLife` | | 0.5, 40, 20 s | Research: the flow discount is what merges house paths into trunks |
| `minSurface` | trail discount capped at 0.72 | 0.6 | Floor on the combined discount. At the research settings without it (0.35), a new footbridge took 15% of crossings on seeds 1 to 10 (round 1: 76%); with it, 58% |
| `stepNoise` | | 0.06 | Per-walker, per-cell noise on a step; spreads lanes a little |
| `lookahead` | | 20 cells | How far a walker looks down the field to aim a straight line |
| `fordCostMul` / `fordSpeedMul` | speed 0.4 | 3 / 0.4 | The ford is slow and walkers avoid it when there is a bridge |
| `bridgeCost` / `bridgeSpeedMul` | | 0.8 / 1.2 | The spec gives none; a footbridge is "cheap and fast" |
| `urgentFieldsPerTick` | | 1 | After an edit the fields are rebuilt one per tick, destinations first, instead of three at once |
| `tripRateDays` | | 3 | Time constant of the preview's trips-per-day counts |

Unchanged from the spec: the state thresholds (10, 25, 50, 85), state speeds, taste (plus or minus 8%), the crop penalty (+0.5), and the Weidmann curve (v0 1.34 m/s, rho max 5.4, gamma 1.913; crowding cost capped at 3x).

The page caps simulation work at 10 ms per frame (and 8 ticks); on a slow machine the sim runs slower than the chosen speed instead of stalling.

## Health metrics

Defined in `src/sim/metrics.ts`, over the last 2 in-game days:

- **Detour ratio**: mean of actual route length / straight-line length over completed trips longer than 3 cells. Target 1.05 to 1.30.
- **Trail coverage**: share of land cells (not water, not buildings) at trail state or above. Hamlet target 3% to 8%.
- **Trunk share**: footsteps on the busiest 10% of trail cells / all footsteps. Gate: 30% to 70%.
- **Trunk share D**: the same numerator / footsteps on trail cells only (the research's variant, which removes the dependence on coverage). Reported, not gated.

`npm test` judges gate G3 on held-out seeds 101 to 120 (never used for tuning) at 200 walkers over 45 in-game days: every daily trunk-share sample from day 20 to day 45 must lie within 30% to 70%, and a first trail (a connected run of 10 trail cells) must form within 3 in-game days. It prints per seed the mean, minimum and maximum daily sample, and the same table for seeds 1 to 20 (the tuning set), seeds 201 to 220 (a final exam, never tuned on) and 500 walkers on seeds 1 to 5, all report only. It also runs a reroute test, a bridge test, a robustness test, a determinism test, and the flow preview tests: isolation (200 preview queries leave the run byte-identical), an empty change, speed, bridge, erase, unreachable, and agreement with the walkers (the predicted detour is where the new trail forms, and the readout is within 15% of the trips a day that then take the new route).

# Prototype 1: desire lines

A throwaway browser toy that answers one question for Tide & Toll: **is steering worn paths fun?**

Villagers walk between their houses, the well, the field and the market. Every footstep wears the grass. Grass turns to trampled ground, then trail, then track. Paths nobody uses grow back. You never draw a road: you place fences and footbridges, and the walkers find their own way round.

The page is one self-contained file, `dist/index.html`. It has no runtime libraries, fetches nothing and is published as a claude.ai Artifact.

## Run it

```sh
npm install          # typescript and esbuild only
npm run build        # writes dist/index.html; open it in a browser
npm run check        # typecheck + build + headless simulation tests (about 2 minutes)
npm run perf         # Playwright: frame times at 500 and 200 walkers, 1x and 8x
npm run smoke        # Playwright: page contract, every control, 400 px layout, both themes
```

`npm test -- --quick` runs the gate table on 4 seeds instead of 20. Playwright is not a dependency: the perf and smoke scripts use the copy installed globally in the build container.

Add `#seed=42` to the page URL to open a given map.

## Controls

| Control | Key | What it does |
| --- | --- | --- |
| Pause / Play | Space | Stops and starts the simulation clock |
| Speed 1x 2x 4x 8x | 1 2 3 4 | At 1x one in-game day is 15 s and a season is 60 s |
| Walkers | | 50 to 500 villagers (default 200) |
| Fence | F | Drag to draw a fence line. Walkers cannot cross it |
| Footbridge | B | Click on the river to lay a bridge across it (not next to the ford) |
| Erase | E | Click or drag over fences and bridges to remove them |
| Wear overlay | W | Heat map of wear (0 to 100) over a dimmed map |
| Reset | R | Restarts the same map |
| New map | N | Generates a new map from a new seed |
| | Esc | Puts the tool down |

The side panel shows the day and season, the walkers out, the frame rate, and three health metrics: detour ratio, trail coverage and trunk share (see below).

## What is in, and what is not

In: a 128 x 128 meadow of 2 m cells, a meandering river with one ford, 6 to 8 houses, a well, a field and a market across the river; wear with saturation, side spread and state-based decay; five ground states; walkers routed on wear-weighted cost-to-go fields with Weidmann crowding and per-walker taste; fences, footbridges and erase; pause, speed, reset, new map; the wear overlay; health metrics; a debug API on `window.__tt` for tests.

Not yet (from the step 1 build list in the design doc): the flow preview (planned for round 2; the routing API already computes what-if fields, see `World.whatIfField`), gravel and gate tools, rain and mud, placeable buildings and house growth by frontage. Hollow ways need wear above 85 held for 10 in-game years (40 minutes at 1x), so they will rarely appear in a session.

## How it works

- `src/sim/` is pure TypeScript with no DOM and no `Math.random`. A seed plus the same inputs at the same ticks always gives the same run. The simulation ticks at 10 Hz of sim time; higher speeds run more ticks per frame.
- **Routing.** Each building door has a Dijkstra cost-to-go field over the 8-neighbour grid (about 3 ms each). Fields are rebuilt in rotation, each every 5 sim seconds, and all at once (3 per tick) right after a fence or bridge changes. Per-trip A* would cost 3 to 5 ms a trip, and 500 walkers at 8x make about 1,000 trips a second.
- **Walking.** A walker looks 20 cells down its target's field (with its own taste and a small per-cell noise) and heads in a straight line for the farthest of those cells it can reach in clear sight, as long as that line costs no more than the field's route. That gives trails at any angle instead of 8-direction staircases, while walkers still keep to a trail that bends.
- **Wear.** Entering a cell adds `I * (1 - W/100)` to it and a share of that to the two cells either side of the heading. Wear decays with a half-life that depends on its band.
- **Blocked walkers.** A walker on a newly fenced cell is moved to the nearest open cell; a walker cut off from its errand heads home; a walker with no way forward for 5 s is sent home.

## Tunable parameters

All of them are in `src/sim/params.ts`. The ones that differ from the design spec:

| Parameter | Spec | Here | Why |
| --- | --- | --- | --- |
| `walkSpeed` | 0.67 cells/s (1.34 m/s) | 3 cells/s | Time compression: at real speed a town of 200 crosses too few cells a day for any trail to form (trail-tuning research) |
| `dwellSimSec` | | 6 s | Same reason: about one round trip per walker per day |
| `wearPerStep` | 0.75 | 1.4 | Research raised it to 1.0 for coverage; the 20-seed trunk-share sweep to 1.4 (at 1.2 one seed fell to 28%) |
| `sideShare` | 0.15 | 0.35 | Wider, softer trails; keeps trunk share above 30% at 200 walkers |
| `halfLifeLow/Mid/High` | 60 / 120 / 240 s | 90 / 180 / 360 s | Research: longer memory so trails at 200 walkers persist |
| `stepNoise` | | 0.06 | Per-walker, per-cell noise on a step; spreads lanes a little |
| `lookahead` | | 20 cells | How far a walker looks down the field to aim a straight line |
| `fordCostMul` / `fordSpeedMul` | speed 0.4 | 3 / 0.4 | The ford is slow and walkers avoid it when there is a bridge |
| `bridgeCost` / `bridgeSpeedMul` | | 0.8 / 1.2 | The spec gives none; a footbridge is "cheap and fast" |

Unchanged from the spec: the state thresholds (10, 25, 50, 85), surface costs (1.00, 0.95, 0.85, 0.75, 0.72), state speeds, taste (plus or minus 8%), the crop penalty (+0.5), and the Weidmann curve (v0 1.34 m/s, rho max 5.4, gamma 1.913; crowding cost capped at 3x).

## Health metrics

Defined in `src/sim/metrics.ts`, over the last 2 in-game days:

- **Detour ratio**: mean of actual route length / straight-line length over completed trips longer than 3 cells. Target 1.05 to 1.30.
- **Trail coverage**: share of land cells (not water, not buildings) at trail state or above. Hamlet target 3% to 8%.
- **Trunk share**: footsteps on the busiest 10% of trail cells / all footsteps. Gate: 30% to 70%.
- **Trunk share D**: the same numerator / footsteps on trail cells only (the research's variant, which removes the dependence on coverage). Reported, not gated.

`npm test` runs 20 seeds at 200 walkers for 30 in-game days and fails if any seed's trunk share (mean of daily samples, days 20 to 30) leaves 30% to 70%, or a seed takes longer than 3 in-game days to form a first trail (a connected run of 10 trail cells). It also runs a reroute test, a bridge test, a robustness test (a walled-in house, a fence dropped on a crowd) and a determinism test.

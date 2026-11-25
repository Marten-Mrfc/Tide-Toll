# Tide & Toll

A town-builder where you never draw a road. Villagers wear their own paths into the grass, boats compete with them for the river, and every era inherits the streets of the one before. The town is a living map drawn on the pages of an open book.

## What is in this repository

| Folder | What it holds |
| --- | --- |
| `sim/` | `TideToll.Sim`: the engine-free C# simulation library (seeded, deterministic, no Godot) |
| `sim.tests/` | xUnit tests for the simulation, run without the game |
| `game/` | the Godot 4.7 .NET project (rendering, camera, UI, audio) |
| `prototypes/desire-lines/` | Prototype 1: a browser toy that tests whether steering worn paths is fun |
| `assets/` | art and sound, each file listed in `assets/LEDGER.csv` with its source and licence |
| `tools/` | setup, the one-command check, ledger check, asset sync and Windows export |
| `docs/` | short notes: asset pipeline, Windows setup, CI template, the overnight log |

## What the game does now

Prototype 1 (the desire-lines toy) runs in Godot on the map page of an open book: the same town as the browser toy for the same seed, villagers wearing trails into the page, and the player steering them with fences, footbridges and the eraser, with a flow preview of the routes that would change ("Reroutes about 35 walkers a day (+47% longer)"). The left page is the chapter page: how to play, the village facts, the path-health figures with their bands, the legend and a status line. Paper bookmarks out of the map page hold pause, 1x to 8x, the tools, flow preview, wear overlay, sound and volume, reset, new map and a walker count from 50 to 500; every one has a key. Sound: the river, footsteps by the crowd on screen, a bell at dawn, tool knocks and the theme now and then. Run it with `godot --path game`, or the Windows build (see below).

The river prototype (Prototype 1, step 2) is on in the game: rowboats and masted barges come up the river on a schedule to the landing and buy the grain villagers carry from the field; the player buys crossings from the bookmarks, each with its price (footbridge, ferry, drawbridge, set in the simulation's `RiverRules`), raises the drawbridge by hand (click it, L, or the lever on its badge) or hires a keeper. A waiting boat shows its patience running down, a boat that gives up turns back with a mark on the water, coins float up by the landing, and the chapter page keeps a ledger (coin, income today, reputation, boats served and turned back, the river's state, grain brought to the landing today and how many walkers had to wait at each crossing) and margin notes of the latest events. At the default play zoom the river reads as a network of its own: boats drawn large with their wakes, each crossing with its own map mark (the ferry's dotted rope and raft, the footbridge's plank deck, the drawbridge's gantry and signal disc). The seasons reach the river: a spring flood washes over the band beside it and closes the ford (a vermillion bar and a "Ford closed" tag; mud on the worn paths after), and winter ice freezes it (boats held, villagers crossing anywhere, their paths scratched pale into the ice until the thaw, the river's sound all but gone). `godot --path game -- --no-river` plays without it.

## How to run the checks

On Ubuntu 24.04, install the toolchain once (needs root; about 2 GB with export templates):

```
sudo tools/setup-linux.sh
```

Then, from the repository root:

```
./check                 # sim build + tests, Godot build, headless smoke, tools, river, interface and perf tests,
                        # screenshots at 1280x720 and 1920x1080, ledger
./check --export        # also build the Windows x86_64 export into build/windows/ and check the pack holds every asset
./check --gate          # also the long statistical runs (gate tables, toy comparison, ~6 min)
./check --bench         # also the simulation tick benchmark
./check --with-toy      # also run the browser toy's own checks
./check --no-screenshot # skip the rendered screenshots (no xvfb)
```

It prints a summary table and exits non-zero if anything fails. Logs go to `build/check-logs/`, screenshots to `build/screenshots/` (`book`, `map`, `wear`, `preview` and the river's `river-*` views at both window sizes, plus close-ups).

| Step | What it proves |
| --- | --- |
| `sim-build`, `sim-test` | the simulation builds and its fast tests pass (parity with the toy, no allocation per tick) |
| `game-build`, `game-smoke` | the game builds; the scene is complete, the frame loop ticks, picking, camera limits, edits rebuild |
| `game-tools` | fence, footbridge and erase through real mouse events; nothing placed through the toolbar; the flow preview never changes a run (same wear hash with and without it) |
| `game-river` | the river through real input: ferry and drawbridge placed (and refused off the river or for coin, with the reason), the drawbridge raised and lowered by click, L, its badge's lever and Ctrl+1, the keeper hired, both erased; four in-game days of boats fill the chronicle, the ledger and the coins; the bridge badge clear of its bridge and the place tags at 720p and 1080p; a flood and a freeze driven through the simulation's hooks, with their marks, tags, notes, ledger lines, tool reason and sound coming and going |
| `game-ui` | every control exists, has a tooltip and label, and toggles by click; every shortcut; keyboard focus; text sizes at 720p and 1080p |
| `game-perf` | with the river on (a ferry, a drawbridge and its keeper), CPU per frame with 500 walkers under 6 ms (1x) and 10 ms (8x) mean, under 64 bytes allocated per frame, also at 8x in a flood and on the ice; the preview's frames under 25 ms (bounded preview slices); the stand-in for "60 fps with 500 walkers" until a real graphics card measures it |
| `screenshot`, `screenshot-1080` | rendered views with the interface, the wear overlay and a fence drag with the preview, and the river (a barge waiting at the lowered drawbridge, the bridge raised, the ferry, a barge held and turning back at a footbridge, all crossings, a rowboat moored close, the play view, the ledger, a flood, the frozen river with its ice paths), at both sizes |
| `ledger` | every asset has a ledger row |

On Windows, see [docs/setup-windows.md](docs/setup-windows.md) to run the exported build or open the game in Godot and press Play, and what to check on a real PC. Adding art or sound: [docs/asset-pipeline.md](docs/asset-pipeline.md).

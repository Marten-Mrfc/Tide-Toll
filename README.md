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

## How to run the checks

On Ubuntu 24.04, install the toolchain once (needs root; about 2 GB with export templates):

```
sudo tools/setup-linux.sh
```

Then, from the repository root:

```
./check                 # sim build + tests, Godot build, headless smoke, tools, interface and perf tests,
                        # screenshots at 1280x720 and 1920x1080, ledger
./check --export        # also build the Windows x86_64 export into build/windows/ and check the pack holds every asset
./check --gate          # also the long statistical runs (gate tables, toy comparison, ~6 min)
./check --bench         # also the simulation tick benchmark
./check --with-toy      # also run the browser toy's own checks
./check --no-screenshot # skip the rendered screenshots (no xvfb)
```

It prints a summary table and exits non-zero if anything fails. Logs go to `build/check-logs/`, screenshots to `build/screenshots/` (`book`, `map`, `wear` and `preview` at both window sizes, plus close-ups).

| Step | What it proves |
| --- | --- |
| `sim-build`, `sim-test` | the simulation builds and its fast tests pass (parity with the toy, no allocation per tick) |
| `game-build`, `game-smoke` | the game builds; the scene is complete, the frame loop ticks, picking, camera limits, edits rebuild |
| `game-tools` | fence, footbridge and erase through real mouse events; nothing placed through the toolbar; the flow preview never changes a run (same wear hash with and without it) |
| `game-ui` | every control exists, has a tooltip and label, and toggles by click; every shortcut; keyboard focus; text sizes at 720p and 1080p |
| `game-perf` | CPU per frame with 500 walkers under 6 ms (1x) and 10 ms (8x) mean, under 64 bytes allocated per frame; the stand-in for "60 fps with 500 walkers" until a real graphics card measures it |
| `screenshot`, `screenshot-1080` | rendered views with the interface, the wear overlay and a fence drag with the preview, at both sizes |
| `ledger` | every asset has a ledger row |

On Windows, see [docs/setup-windows.md](docs/setup-windows.md) to run the exported build or open the game in Godot and press Play, and what to check on a real PC. Adding art or sound: [docs/asset-pipeline.md](docs/asset-pipeline.md).

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

## How to run the checks

On Ubuntu 24.04, install the toolchain once (needs root; about 2 GB with export templates):

```
sudo tools/setup-linux.sh
```

Then, from the repository root:

```
./check                 # sim build + tests, Godot build, headless smoke, screenshot, ledger
./check --export        # also build the Windows x86_64 export into build/windows/
./check --with-toy      # also run the browser toy's own checks
```

It prints a summary table and exits non-zero if anything fails. Logs go to `build/check-logs/`, the screenshot to `build/screenshots/main.png`.

On Windows, see [docs/setup-windows.md](docs/setup-windows.md) to open the game in Godot and press Play. Adding art or sound: [docs/asset-pipeline.md](docs/asset-pipeline.md).

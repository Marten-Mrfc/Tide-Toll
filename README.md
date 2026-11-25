# Tide & Toll

A town that grows one path at a time. Villagers walk the roads and earn coin on every trip; coin buys upgrades, upgrades bring levels, and every level hands over the next piece of town. The town is a living map drawn on the pages of an open book.

## What is in this repository

| Folder | What it holds |
| --- | --- |
| `sim/` | `TideToll.Sim`: the engine-free C# simulation library (seeded, deterministic, no Godot) |
| `sim.tests/` | xUnit tests for the simulation, run without the game |
| `game/` | the Godot 4.7 .NET project (rendering, camera, UI, audio); `game/legacy/` holds the retired desire-line game, not built |
| `prototypes/desire-lines/` | Prototype 1: a browser toy that tests whether steering worn paths is fun |
| `assets/` | art and sound, each file listed in `assets/LEDGER.csv` with its source and licence |
| `tools/` | setup, the one-command check, ledger check, asset sync and Windows export |
| `docs/` | short notes: asset pipeline, Windows setup, CI template, the overnight log |

## What the game does now

An incremental town on the right-hand page of an open book (the design: [docs/plans/incremental-design.md](docs/plans/incremental-design.md)). It starts with one cottage, the well, one faint trail and one villager. Every finished trip earns coin (a "+N" floats up at the door). Coin buys upgrades from the bookmarks on the right: a villager, pace, load, paving and buildings, each with its price and either "Buy" or the reason it must wait. Every purchase adds renown; each level hands over the next piece of town by itself (a cottage and its lane, the bell on its green, a field, the market, the mill, the charter), staked out in advance and popping in with a margin note and a bell. Roads start as faint trails that wear in visibly; worn-in roads can be paved with gravel and later cobbles (P, then click a lit road; the busiest is suggested); crowded roads take a warm wash. The bell (Space) hurries everyone for a few seconds, then rests.

Run it with `godot --path game` (`-- --seed=N` for another town), or the Windows build (see below). The camera starts on the town and follows it as it grows until you move it: drag, WASD or the arrow keys to move, the wheel to zoom (up to the whole page), Home to follow the town again. The old desire-line game (two chapters, fences, gates, gravel, crossings) is kept for reference in `game/legacy/` and is not built.

Sound: coins, purchases, a long bell for a new level, the bell, footsteps, the river and the theme now and then.

## How to run the checks

On Ubuntu 24.04, install the toolchain once (needs root; about 2 GB with export templates):

```
sudo tools/setup-linux.sh
```

Then, from the repository root:

```
./check                 # sim build + tests, Godot build, the town's smoke, play-through (real input) and perf tests,
                        # screenshots at 1920x1080, ledger
./check --export        # also build the Windows x86_64 export into build/windows/ and check the pack holds every asset
./check --gate          # also the long statistical runs (gate tables, toy comparison, ~6 min)
./check --bench         # also the simulation tick benchmark
./check --with-toy      # also run the browser toy's own checks
./check --no-screenshot # skip the rendered screenshots (no xvfb)
```

It prints a summary table and exits non-zero if anything fails. Logs go to `build/check-logs/`, screenshots to `build/screenshots/` (`town-first-minute`, `town-first-purchase`, `town-level-3`, `town-pave`, `town-end-of-age`, at 1920x1080).

| Step | What it proves |
| --- | --- |
| `sim-build`, `sim-test` | the simulation builds and its fast tests pass (the town in `sim/Town/` and the earlier desire-line and river simulation) |
| `game-build`, `game-smoke` | the game builds; the town scene loads with all its pieces and runs 30 game seconds with no error line |
| `game-town` | real mouse and key events buy the first upgrades, then fast play to level 3: every purchase changes the town, each gift arrives and is drawn, a road wears in visibly, a click in Pave mode paves a road, the bell rings by key and by click, villagers never jump or slide sideways |
| `game-perf` | CPU per frame with the end-of-age town (played there by the simulation's bot) under 6 ms (1x) and 10 ms (8x) mean |
| `screenshot` | the five rendered town views above |
| `ledger` | every asset has a ledger row |

Retired steps of the old game and why: [docs/plans/retired-tests.md](docs/plans/retired-tests.md).

On Windows, the quickest way to play is the build GitHub makes on every push: open the [Windows build runs](https://github.com/Marten-Mrfc/Tide-Toll/actions/workflows/windows-build.yml), pick the newest green one and download the `TideToll-windows-x64` artifact. See [docs/setup-windows.md](docs/setup-windows.md) for that, for opening the game in Godot instead, and for what to check on a real PC. Adding art or sound: [docs/asset-pipeline.md](docs/asset-pipeline.md).

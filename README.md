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

Run it with `godot --path game` (`-- --seed=N` for another town), or the Windows build (see below). The camera starts on the town and follows it as it grows until you move it: drag, WASD or the arrow keys to move, the wheel (or + and -) to zoom: close in the camera looks at the town at a slant, and zooming out it flattens to the whole page and then the whole open book, whose left page shows the town's summary. Home goes back to following the town. The old desire-line game (two chapters, fences, gates, gravel, crossings) is kept for reference in `game/legacy/` and is not built.

Esc (or the Pause mark at the bottom right) closes the book: the town stops and the cover is the menu, in gilt: Resume, Settings, New town, Quit, with the mouse or the arrow keys and Enter (Esc resumes). Settings are the book's pages (Sound, Display, Keys, Saving); every key can be rebound there. The game saves itself (every 2 minutes by default, on pause and on quit) and continues the town where it stopped; a save it cannot read is set aside and a new town begins.

Sound: coins, purchases, a long bell for a new level, the bell, footsteps, the river and the theme now and then.

## How to run the checks

On Ubuntu 24.04, install the toolchain once (needs root; about 2 GB with export templates):

```
sudo tools/setup-linux.sh
```

Then, from the repository root:

```
./check                 # sim build + tests, Godot build, the town's smoke, play-through (real input) and perf tests,
                        # the zoom to the book (game-book), the settings pages, the pause menu, saving,
                        # screenshots at 1920x1080, ledger
./check --export        # also build the Windows x86_64 export into build/windows/ and check the pack holds every asset
./check --gate          # also the long statistical runs (gate tables, toy comparison, ~6 min)
./check --bench         # also the simulation tick benchmark
./check --with-toy      # also run the browser toy's own checks
./check --no-screenshot # skip the rendered screenshots (no xvfb)
```

It prints a summary table and exits non-zero if anything fails. Logs go to `build/check-logs/`, screenshots to `build/screenshots/` (`town-first-minute`, `town-first-purchase`, `town-level-3`, `town-pave`, `town-level-6`, `town-extend`, `town-request`, `town-charter`, `town-end-of-age`, `town-achievements`, `town-zoom-1` to `town-zoom-4`, `town-summary`, `town-next-age`, `town-first-age`, `town-book-half-closed`, `town-cover-menu`, `town-settings-sound`, `-display`, `-keys`, `-saving`, `town-new-town-question`, at 1920x1080).

| Step | What it proves |
| --- | --- |
| `sim-build`, `sim-test` | the simulation builds and its fast tests pass (the town in `sim/Town/` and the earlier desire-line and river simulation) |
| `game-build`, `game-smoke` | the game builds; the town scene loads with all its pieces and runs 30 game seconds with no error line |
| `game-town` | real mouse and key events buy the first upgrades, then fast play to level 6 buying every kind of shop item (Extend by clicking a cottage, Pave by clicking a road); each gift arrives and is drawn, a road wears in visibly, the bell rings by key and by click, the request card and hint line match the simulation, villagers never jump; then 30 Pave clicks at the end of the age, tilted and flat, each on exactly the road the price tip named; round 5: a boon picked by click (level 4) and by key (level 7), a milestone announced, touches arriving and drawn at their spots, an extra hand bought from its bookmark with its marker shown, the charter drive's card filling in tenths and met |
| `game-playthrough` | the whole first age through injected clicks and keys only, like a sensible person (bookmarks, their keys, Pave and Extend by pointing until the tip names the road or cottage, the bell), then Ring in the next age and the first age's page; fails on any error, a Pave or Extend click that did not do what its tip said, a lit bookmark that bought nothing, or a minute after the second with nothing to buy; prints purchases and decisions a minute per ten minutes and the charter's minute; round 5: every choice of two picked (by click and by key in turn), the charter drive seen and met, an extra hand bought, touches drawn, milestones announced |
| `game-perf` | CPU per frame with the end-of-age town (played there by the simulation's bot) under 6 ms (1x) and 10 ms (8x) mean |
| `game-settings` | the settings pages alone: store, applier, every page, rebinding, the Saving page, keyboard only |
| `game-menu` | Esc closes the book and stops the town; the cover menu by keyboard; settings and Back; Resume puts the camera back; settings changed there take effect in the game; New town asks first |
| `game-save` | a saved town loads back the same (coin, level, villagers, roads, paving, camera) and runs on; a damaged save is set aside with a note; quitting saves |
| `screenshot` | the rendered town views and the menu views above |
| `ledger` | every asset has a ledger row |

Retired steps of the old game and why: [docs/plans/retired-tests.md](docs/plans/retired-tests.md).

On Windows, the quickest way to play is the build GitHub makes on every push: open the [Windows build runs](https://github.com/Marten-Mrfc/Tide-Toll/actions/workflows/windows-build.yml), pick the newest green one and download the `TideToll-windows-x64` artifact. See [docs/setup-windows.md](docs/setup-windows.md) for that, for opening the game in Godot instead, and for what to check on a real PC. Adding art or sound: [docs/asset-pipeline.md](docs/asset-pipeline.md).

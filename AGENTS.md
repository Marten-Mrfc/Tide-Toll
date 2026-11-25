# AGENTS.md

Instructions for AI coding agents (Claude reads this through CLAUDE.md). Keep it short and factual.

## Pinned versions

- Godot 4.7-stable, .NET edition (`Godot.NET.Sdk/4.7.0`), installed by `tools/setup-linux.sh` to `/opt/godot/4.7`
- .NET SDK 8 (`net8.0` everywhere), C#
- xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1 (`sim.tests/`)
- Browser toy: Node 20+, TypeScript and esbuild as pinned in `prototypes/desire-lines/package.json`

## Layout

| Path | What it holds |
| --- | --- |
| `sim/` | `TideToll.Sim`: the engine-free simulation; `sim/Town/` is the incremental town the game plays (interface: `docs/plans/town-sim-api.md`); the rest is the earlier desire-line and river simulation |
| `sim.tests/` | `TideToll.Sim.Tests` (xUnit); `Harness/` holds the toy's checks, also compiled into the tools |
| `sim.tests/golden/` | `toy-golden.json` (round-2 toy), `toy-golden-v2.json` (finished toy) and their generator `export.mjs` (never edit the JSON by hand) |
| `tools/TideToll.Tools/` | headless runner: `run`, `bench`, `gate`, `checks`, `river` (the river strategy bot) |
| `game/` | the Godot project; references `sim/`. `scripts/Main.cs` + `scripts/town/` are the town game; `game/legacy/` is the retired desire-line game (`.gdignore`, not compiled) |
| `prototypes/desire-lines/` | the browser toy (Prototype 1) |
| `assets/` | shipped art and sound, with `assets/LEDGER.csv` |
| `tools/` | `check.sh` (`./check`), setup, ledger check, asset sync, export |
| `docs/` | asset pipeline, Windows setup, CI template, overnight log |

## Architecture rules

- `sim/` never references Godot, `System.Random`, `DateTime`, `Stopwatch` or any clock. A test scans the source for them.
- Same seed and same commands at the same ticks give the same run. Iterate arrays, never hash sets or dictionaries.
- The game reads the town through its views (`Town`, `docs/plans/town-sim-api.md`) and changes it only through the player actions (`Buy`, `Pave`, `RingBell`, ...). `TownDriver` steps it at `TownRules.TickHz` whatever the frame rate and interpolates drawing. Never write a number from `sim/Town/TownRules.cs` into game code or a test: read prices and timings from the simulation.
- Files under about 400 lines, one system per file.
- Presets: `SimParams.Default` is the finished toy as a player meets it (its trail model, rain and mud, house growth, preview version 2); `SimParams.Toy` is the same with rain and growth off (the gate and the comparison traces); `SimParams.Legacy` is the round-2 toy (toy-golden.json, the pre-river baselines, the round-2 checks). A test that compares with recorded data names its preset.
- The river rules (`SimParams.River`) are off by default and must stay invisible when off: `RiverOffBaselineTests` holds hashes recorded before they existed (on `Legacy`) and the finished toy's own day-20 baselines (on `Toy` and `Default`), and river code runs only behind `_river != null`. With the river rules on, rain and house growth are off whatever the preset says.
- Until PCG32 and integer costs land, the port keeps the toy's mulberry32, float32 storage and V8's `Math.hypot`, so the goldens stay exact. A deliberate change of results means regenerating the goldens.
- Mirroring the toy: same order of operations and of random draws (the walkers', the weather's and the growth stream are separate); time accumulates `time += 0.1`; JS stable sorts become insertion sorts or a fixed best-N array filled in order; JS `Map` iteration becomes an ordered list; `exp`, `pow` and `sin` go through .NET's Math (the probes in toy-golden-v2.json show at once if another machine's maths differs: `MathsProbesMatchV8BitForBit`).
- House growth adds buildings and walkers during `Tick`: per-building arrays are pre-sized (`SimParams.MaxGrownHouses`), so loop over `Buildings.Length`/`_nb`, never an array's length.

## Hot-path rules (anything `World.Tick` calls)

- No allocation: no `new`, LINQ, lambdas, closures, boxing, `params` arrays or string building. Scratch buffers are allocated once in the constructor.
- Grids are flat arrays indexed `y * width + x`; walkers are a pre-sized `Walker[]`.
- To mirror the toy: compute in double, store as float with `(float)(...)`; cast the first operand of a float sum to double.
- `InvariantTests.TickAndStatsAllocateNothing` and the Perf test fail on any allocation.

## Checks

```
./check                 # sim build + fast tests (Release), Godot build, game-smoke, game-town (real input), game-perf, game-book (the zoom), screenshots, ledger
./check --gate          # gate tables, comparison with the toy, 45-day traces, preview agreement (~6 min)
./check --bench         # ms per tick at 200 and 500 walkers in Release; fails only above 5 ms mean / 25 ms p99
dotnet test sim.tests -c Release --filter "Category!=Gate&Category!=Perf"
dotnet run --project tools/TideToll.Tools -c Release -- run --seed 1 --walkers 500 --days 30
dotnet run --project tools/TideToll.Tools -c Release -- ticks --preset river --walkers 500 --days 20   # every tick timed, the worst ones with what ran in them
dotnet run --project tools/TideToll.Tools -c Release -- river --seed-sets 101-120,201-220 --sweep   # river bot, 12 and 30 days (~16 min); seeds 501-524 are the reviewer's only: nobody else runs them
node sim.tests/golden/export.mjs prototypes/desire-lines                 # regenerate toy-golden-v2.json from the finished toy (~11 min; needs its node_modules)
node sim.tests/golden/export.mjs --legacy <toy-r2-dir> <final-toy-dir>   # both files; the legacy one byte-equal from the round-2 toy (~4 min more)
TIDETOLL_RIVER_PRESET=legacy dotnet test sim.tests -c Release --filter "FullyQualifiedName~River"   # the river tests on another trail model (legacy|toy)
```

`./check` must pass before every commit. The gate may fail honestly (the round-2 toy's own G3 fails on seed 115; the gate itself now runs on `SimParams.Toy`); never tune parameters or edit a test to make it pass. Logs go to `build/check-logs/`.

## Game tests

`game/tests/`: `settings.tscn` (the settings pages alone), `menu.tscn` (pause, cover menu, settings joined, New town), `save.tscn` (save, reload, damaged save, quit paths), `smoke.tscn`, `town.tscn` (real input through `TestInput`), `perf.tscn` (end-of-age town by `TownBot`; CPU and allocation gates), `book.tscn` (wheel zoom from the tilted town to the book, the summary page, Pave while tilted, at 1920 x 1080), `screenshot.tscn` (run under xvfb with `--resolution`), all on `TownTestBase` (prints the marker or `FAIL:` and quits 0 or 1). `tools/godot-test.sh MARKER cmd...` fails on any error line. Run one alone: `tools/godot-test.sh 'TOWN PASS' godot --headless --path game --audio-driver Dummy res://tests/town.tscn`.

## The view (round 2)

The camera tilts with the zoom (`CameraRig.PitchFor`: 56 degrees close in, straight down from the whole page out); everything that maps screen to ground goes through `CameraRig.PickLocal` / `LocalToScreen` (ray to the ground plane), never through `Camera.Size` alone. `Main.Stage` (Town, Page, Book) drives the interface: full, slim strip, and the summary on the left page (`TownSummary`). Interface text is rebuilt only when its value changes; `game-perf` gates allocation per frame.

## The town interface (round 3, rules v2)

- The shop is data-driven from `Town.ShopItems`, grouped People / Work / Roads / Town (`TownHud.Shop.cs`); every bookmark has its own InputMap action (`tt_buy_0..9`, `tt_offer_1..3`); locked ("Unlocks at level N") and finished steps are read from the item's `Reason`.
- Pave and Extend are click modes (`TownHud.Pave.cs`, `TownHud.Extend.cs`): hover and click use one picking (`PaveTarget`, `ExtendTarget`) that only finds what can be bought this moment; a click acts on exactly what the tip names. Never mark the left release handled: the camera must see it.
- Roads are drawn from `Road.Px/Py` (the curve villagers walk), never `Xs/Ys`. Villagers stand where the simulation puts them (work spots, queue spots); no display fan-out.
- Request card and hint line: `TownHud.Request.cs`; level banner and the Home control: `TownHud.Banner.cs`.

## Menus, settings, saving (round 2)

- `Main` adds `SettingsRuntime` first (loads and applies `settings.cfg`); buses and their volumes belong to the settings, so no other code sets bus volumes. Game keys are the InputMap actions of `settings/GameInput.cs` (`tt_*`), never raw keys. The interface size is the window's content scale only: build interface sizes from `UiKit.ScaleFor(GetWindow().Size.Y)`, lay out in the visible rect.
- `PauseMenu` (tt_pause): camera to the closed book, `BookCover` swings shut, `TownDriver` paused, `TownAudio` pause piece; the cover lettering is 2D over the 3D board (`PauseMenu.CoverRect`). Settings open `SettingsPages` on the menu's layer.
- `TownSaver`: `Main.Profile + town.json` (game version, camera, `TownSave` JSON), autosave from the settings, on pause and on quit (`Main.QuitGame`, also the window's close button); unreadable saves are renamed `town-unreadable-*.json`.
- Tests: `TownTestBase` gives each test its own `Main.Profile` (`user://tests/<marker>/`, emptied first) and sets `Main.KeepWindow` (the stored window size is not applied), so the player's files never reach a test.

## Assets

New models and icons load by name with a named stand-in when the file is missing (`ModelLib.Pick`, `TownHud.IconOr`). Every file under `assets/` needs a row in `assets/LEDGER.csv` (source, licence, URL, where used) before it goes into the game; `./check` fails without one. See `docs/asset-pipeline.md`.

## Git

Work on branch `claude/prototype-1`. Do not edit `docs/overnight-log.md` unless asked.

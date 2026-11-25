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
| `sim/` | `TideToll.Sim`: the engine-free simulation (map, wear, walkers, tools, metrics, flow preview) |
| `sim.tests/` | `TideToll.Sim.Tests` (xUnit); `Harness/` holds the toy's checks, also compiled into the tools |
| `sim.tests/golden/` | `toy-golden.json` (round-2 toy), `toy-golden-v2.json` (finished toy) and their generator `export.mjs` (never edit the JSON by hand) |
| `tools/TideToll.Tools/` | headless runner: `run`, `bench`, `gate`, `checks`, `river` (the river strategy bot) |
| `game/` | the Godot project; references `sim/` |
| `prototypes/desire-lines/` | the browser toy (Prototype 1) |
| `assets/` | shipped art and sound, with `assets/LEDGER.csv` |
| `tools/` | `check.sh` (`./check`), setup, ledger check, asset sync, export |
| `docs/` | asset pipeline, Windows setup, CI template, overnight log |

## Architecture rules

- `sim/` never references Godot, `System.Random`, `DateTime`, `Stopwatch` or any clock. A test scans the source for them.
- Same seed and same commands at the same ticks give the same run. Iterate arrays, never hash sets or dictionaries.
- The game reads the simulation through read-only spans and changes it only through commands (`World` API in `sim/README.md`).
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
./check                 # sim build + fast tests (Release, under a minute), Godot build, smoke, screenshot, ledger
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

## Assets

Every file under `assets/` needs a row in `assets/LEDGER.csv` (source, licence, URL, where used) before it goes into the game; `./check` fails without one. See `docs/asset-pipeline.md`.

## Interrupted work (temporary note from the lead, 25 November 2026)

The container restarted twice tonight (about 00:20 and 01:08 UTC) and may do so again. If you find work in the tree that matches your task but that you did not write, an earlier agent with the same task was interrupted: check each item of your task against what is there, keep what is good, finish or redo the rest, and run the checks again. Do not start again from nothing. Compare with the last commit that names a finished round (`git log --oneline`) to see what is new.

## Git

Work on branch `claude/prototype-1`. Do not edit `docs/overnight-log.md` unless asked.

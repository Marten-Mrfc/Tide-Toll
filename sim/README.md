# sim: TideToll.Sim

Purpose: the engine-free simulation library. The game (`game/`), the tests (`sim.tests/`) and the headless runner (`tools/TideToll.Tools`) all reference it, so the same code runs everywhere.

Rules: no Godot types, no `System.Random`, no clock. Everything is seeded and deterministic. Grids are flat arrays indexed `y * width + x`. `World.Tick()` allocates nothing (a test checks it).

It is a faithful port of the browser toy's simulation (`prototypes/desire-lines/src/sim/`, snapshot at commit `a057b50`): same order of operations, formulas, constants and random draws. It keeps the toy's mulberry32 generator, float32 storage of wear, flow, cost and fields, and a replica of V8's `Math.hypot`, so runs can be compared with the toy bit for bit (see "Parity with the toy").

## Files

| File | Toy source | What it holds |
| --- | --- | --- |
| `SimParams.cs` | params.ts | every tunable, same names and values; `Ground` and `GroundOf` |
| `Rng.cs` | rng.ts | `Mulberry32`, `Hash.Hash2` |
| `JsMath.cs` | (V8) | `Hypot` (V8's algorithm) and `Round` (JavaScript rounding) |
| `Grid.cs` | grid.ts | grid shape, neighbour tables, `Line4` |
| `BinaryHeap.cs`, `CostField.cs` | path.ts | the heap, Dijkstra cost-to-go fields, `CanStep` |
| `MapData.cs`, `MapGen.cs` | mapgen.ts | `Building`, `MapData`, `MapGen.Generate(seed)` |
| `Walker.cs` | world.ts | `Walker` struct, `WalkState`, `Bridge` |
| `World.cs` | world.ts | state, cost building, field refresh schedule, `Tick`, decay |
| `WorldWalk.cs` | world.ts, path.ts | trips, dwell, aim rule, footsteps and side wear, crowding speed |
| `WorldEdit.cs` | world.ts | fence, footbridge, erase, eviction, urgent refresh |
| `WorldViews.cs` | world.ts | the read-only API, `Stats()`, `PreviewFlows` |
| `Metrics.cs` | metrics.ts | footstep window, trip ring, detour, coverage, both trunk shares, first trail |
| `FlowPreview.cs` | preview.ts | `FlowPredictor`, `WhatIf`, `FlowPreview`, `Describe` |

## API for the game

```csharp
var world = new World(seed: 1, walkers: 200);          // walkers null = SimParams.DefaultWalkers
world.Tick();                                           // one fixed 1/TickHz step; never allocates

// Read-only views (spans over live state: read them each frame, do not keep them across ticks)
ReadOnlySpan<float>  Wear, Flow, Cost, Occupancy, RiverX, TripRate
ReadOnlySpan<byte>   Fence, Hollow, Land, Water, Ford, Crop, Footprint
ReadOnlySpan<short>  BridgeAt                           // bridge id per cell, -1 none
ReadOnlySpan<ushort> WalkersOnCell
ReadOnlySpan<Walker> Walkers                            // X, Y (now), Px, Py (previous tick), State, Id, Home, Target, Cell
ReadOnlySpan<Building> Buildings; ReadOnlySpan<int> Houses; ReadOnlySpan<int> OfKind(BuildingKind k)
ReadOnlySpan<float>  Field(int building)                // cost-to-go towards a building's door
IReadOnlyList<Bridge> Bridges; MapData Map; Grid Grid; SimParams Params; Metrics Metrics
double Time; long Ticks; int Version; int Seed; int WalkerCount; uint RngState
Ground GroundAt(int cell); bool IsBlocked(int cell); bool CanFence(int cell)
double CellCost(int cell, CostOverride ov = None); double TripRatePerDay(int home, int dest); int WalkersOut()
SimStats Stats()       // DetourRatio, TrailCoverage, TrunkShare, TrunkShareD, TripsInWindow, TrailCells,
                       // Walkers, SimTime, Day, Season, Year, FirstTrailTime; allocation-free

// Commands (each edit bumps Version, rebuilds costs at once and refreshes fields over the next ticks)
int     PlaceFence(ReadOnlySpan<int> cells)             // returns cells newly fenced
Bridge? PlaceBridge(double x, double y)                 // null if no bridge fits at that river row
bool    BridgeCellsAt(double x, double y, List<int> cells)   // the cells a bridge would cover (for a ghost); false if an end is off the map
int     Erase(ReadOnlySpan<int> cells)                  // fences and whole bridges touching the cells
void    SetWalkerCount(int n)                           // clamped to 0..MaxWalkers; never allocates
Grid.Line4(x0, y0, x1, y1, List<int> cells)             // a 4-connected fence line, clipped to the grid

// Flow preview: never touches the world's state or random stream
var p = new FlowPredictor(world);
p.Begin(new WhatIf { Block = cells }, maxPairs: 5);     // also Open, Bridge, CloseBridge
while (p.Step()) { /* one Dijkstra per call: spread over frames */ }
FlowPreview r = p.Result!;                              // Changes (old/new route points), More, ReroutedPerDay, Blocked
string line = FlowPredictor.Describe(r, b => name);     // "Reroutes about 40 walkers a day (+8% longer)"
world.PreviewFlows(whatIf, maxPairs)                    // the same in one call
```

Positions are in cell units (cell x spans x to x + 1). Draw walkers by interpolating `Px, Py` to `X, Y`; only `Visible` walkers (Out or Back) are on the map.

## Parity with the toy

Tested in `sim.tests` against `sim.tests/golden/toy-golden.json` (made from the toy by `sim.tests/golden/export.mjs`):

- Maps for seeds 1 to 50, the Dijkstra field, RNG, hash and `Line4`: identical.
- Wear grids: identical at every day mark for 45 days on seeds 1 to 8 (200 walkers) and 1 and 2 (500 walkers), with identical final stats and walker 0's first trip.
- The 20 gate seeds (101 to 120, 45 days): every table value identical.
- Flow previews at day 20 (seeds 2 and 4): routes, rates, cost ratios and readouts identical.
- Walker positions drift by about 1 ulp within a day, because .NET's `Math.Exp` (crowding speed) differs from V8's in the last bit on about 10% of inputs. Float32 storage absorbs it, so wear and metrics stay identical. Porting fdlibm's `exp` would make positions exact too.

The Engine tab's PCG32 generator and integer costs come later and will break this parity on purpose; regenerate the goldens then.

## Tests

`./check` (fast tier, Release), `./check --gate` (gate tables, comparison, 45-day traces, preview agreement), `./check --bench`. Headless runner: `dotnet run --project tools/TideToll.Tools -c Release -- run --seed 1 --walkers 500 --days 30` (also `bench`, `gate`, `checks`).

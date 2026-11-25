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
| `BinaryHeap.cs`, `CostField.cs` | path.ts | the heap, Dijkstra cost-to-go fields, `CanStep`; `DijkstraRun` (the same field in bounded slices) |
| `MapData.cs`, `MapGen.cs` | mapgen.ts | `Building`, `MapData`, `MapGen.Generate(seed)` |
| `Walker.cs` | world.ts | `Walker` struct, `WalkState`, `Bridge` |
| `World.cs` | world.ts | state, cost building, field refresh schedule, `Tick`, decay |
| `WorldWalk.cs` | world.ts, path.ts | trips, dwell, aim rule, footsteps and side wear, crowding speed |
| `WorldEdit.cs` | world.ts | fence, footbridge, erase, eviction, urgent refresh |
| `WorldViews.cs` | world.ts | the read-only API, `Stats()`, `PreviewFlows` |
| `Metrics.cs` | metrics.ts | footstep window, trip ring, detour, coverage, both trunk shares, first trail |
| `FlowPreview.cs` | preview.ts | `FlowPredictor` (`Step()`, bounded `Step(maxWork)`), `WhatIf` (with `CrossingWhatIf`), `FlowPreview`, `Describe` |
| `RiverRules.cs` | (new) | `RiverRules` (every river number), `Boat`, the enums, `RiverEvent`, `RiverResult` |
| `RiverCrossing.cs` | (new) | `Crossing` (read-only view of the ford, a footbridge, ferry or drawbridge), the bank queues |
| `River.cs` | (new) | river state, set-up, `RiverStep`, the season books, `RiverStats`, `RiverView` |
| `RiverBoats.cs` | (new) | boat schedule, sailing, waiting, patience, turning back, berths, mooring, grain sales |
| `RiverGates.cs` | (new) | drawbridge state machine and keeper, ferry cycle, crossing costs |
| `RiverQueues.cs` | (new) | walkers at a gate: bank queues, bridge capacity (token bucket), waiting, crossing counts |
| `RiverSeasons.cs` | (new) | spring flood (band, ford closure, wading, mud, repairs) and winter ice (boats held, ice-wear layer, thaw) |
| `RiverCommands.cs` | (new) | `PlaceCrossing` and friends, `SetBridge`, `HireKeeper`, `SpawnBoat`, erase hooks, preview helpers |

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
while (p.Step()) { /* one Dijkstra per call (1 to 12 ms): spread over frames */ }
// or bounded slices, bit-identical results: about 1000 units is 0.1 to 0.8 ms (thread CPU time, Release)
while (p.Step(maxWork: 1000) && frameClockMs < 2) { }
FlowPreview r = p.Result!;                              // Changes (old/new route points), More, ReroutedPerDay, Blocked
string line = FlowPredictor.Describe(r, b => name);     // "Reroutes about 40 walkers a day (+8% longer)"
world.PreviewFlows(whatIf, maxPairs)                    // the same in one call
```

Positions are in cell units (cell x spans x to x + 1). Draw walkers by interpolating `Px, Py` to `X, Y`; only `Visible` walkers (Out or Back) are on the map.

## River rules (Prototype 1, step 2; second pass 25 November 2026)

Off by default: `SimParams.River` is null, nothing is allocated and no river code runs, so every result above is
unchanged (`RiverOffBaselineTests` holds wear and random-stream hashes recorded before the river code existed).
Turn them on with `new World(seed, walkers, SimParams.Default with { River = RiverRules.Default })`.

The rules in short (numbers are `RiverRules` defaults; 1 day = 15 s = 150 ticks):

- **Boats** enter at the south edge on a schedule (rowboat, mast 0, 12 units, 1 per 2 days; barge, mast 2, 40 units,
  1 per day), gap = base / (0.5 + reputation / 100) with +-25% jitter, drawn from the river's own stream
  (`Mulberry32(seed * 7919 + 13)`; the walkers' stream is never touched). They sail north at 10 rows/s to the
  landing (row = market door row clamped to 14..108, on the market bank), moor 15 s at one of 2 berths, buy grain,
  then leave by the north edge. A boat stops 2 rows below a crossing its mast cannot pass and keeps 2 rows behind
  the boat ahead (it overtakes a boat held at a crossing it can pass itself). Waiting costs patience (15 s); at 0 it
  turns back. Reputation 0..100 starts at 50, +1 per boat served, -5 per boat turned back.
- **Goods**: with `HaulGoods` (default) a Landing building is appended after the map's buildings (ids unchanged).
  A walker leaving the field takes 1 grain to the landing, then goes home. A moored boat with room buys it at
  2c x `BoatValueMul` (2) = 4c, booked when the boat leaves; otherwise the village market pays 70% of 2c, at most
  1 unit per house per day (the design doc's limit); the rest goes unsold.
- **Crossings** (`Crossing.Clearance` is the highest mast class that passes): the ford (free, all boats); footbridge
  (50c, class 1, walker speed 1.2); ferry (40c, 2c upkeep + 3c ferryman a season, never blocks, 6 walkers per trip,
  leaves 0.5 s after the first boards or when full, 1.0 s across); drawbridge (240c, 4c upkeep, class 1 closed and
  all boats raised; raise 1.0 s, one boat per 1.5 s, lower 1.0 s). A raised bridge lowers itself once the boats
  within 4 rows below are through, or after 8 s with no boat. The keeper (30c, 5c a season) raises 1 s after a
  masted boat is within 6 rows and lowers once it is through.
- **Capacity** (doc, crossing table "Walker capacity per day"): every crossing but the ford has a first come, first
  served queue on each bank. A footbridge lets on at most `FootbridgeWalkersPerDay` (120, doc "about 120, 1 cell wide")
  and a closed drawbridge `DrawbridgeWalkersPerDay` (250, doc "about 250 while closed") walkers a day, as a token bucket
  holding `BridgeBurstSec` (1 s, chosen) of capacity; a walker arriving goes straight on only when nobody queues on
  either bank and the bucket has room. A walker only clipping the corner of an open bridge's end cell (its route runs
  along the bank) is not queued. The ferry turns round for `FerryTurnSec` (0.5 s, chosen) at each stage before
  boarding, so a full ferry carries 6 per 1.0 + 0.5 s leg: the doc's "about 30 each way per boat" a day. The ford
  has no limit, only its slow speed (`SimParams.FordSpeedMul` 0.4, doc "walker speed 0.4") and cost (x3). A walker
  who waited delivers its grain later (boats leave after their mooring window, so late grain goes to the market at
  35% of the boat price, or unsold); one who gives up after 40 s drops it (`RiverStats.GrainLost`).
  Counted: `Crossing.WalkersCrossed` and `WalkersWaited` (walkers who reached the other bank by it, and how many waited
  at a gate since their last crossing), `RiverStats.Crossings`, `CrossingsWaited`, `IceCrossings`, `GrainDelivered`.
- **Spring high water** (doc, "Spring high water", "Seasons and weather"; `SpringFlood`, on): on the first day of each
  spring (the clock starts on one), with `FloodChance` (0.7, doc Riverbend), the flood band (`RiverView.FloodBand`: land
  within `FloodBandCells` (4, chosen) of the water, fixed for the map) is under water for 1 or 2 days (doc;
  `FloodMinDays`, `FloodMaxDays`). The ford is closed (blocked, infinite cost; walkers on it are set on a bank) and walkers
  wade the band at `FloodWadeSpeedMul` 0.4 and cost x`FloodWadeCostMul` 3 (chosen: like the ford; the band holds every
  bridge end and the landing, so it cannot simply be water), making no wear. Bridges and the ferry still work. Then the
  band is mud for the rest of spring: its trail, track and hollow-way cells cost x1.6, x1.7, x1.8 (doc mud multipliers;
  `MudTrailMul` and so on), so walkers braid round them; summer dries it. Each building in the band (not the landing)
  pays `FloodRepairCost` 10c per flood (doc, Costs); generated maps keep buildings out of a 4-cell band.
- **Winter ice** (doc, "Winter ice"; `WinterIce`, on): on the second day of each winter, with `IceChance` (0.5, doc
  Hamlet), the river freezes until the first day of spring. Boats stop where they are (patience and mooring clocks
  frozen, the schedule waits), boats buy nothing (grain goes to the market), a raised drawbridge comes down, the
  ferry's riders step off and it stays at its stage. Every water cell is walkable ice (cost and speed like grass,
  `IceCostMul`, `IceSpeedMul` 1, chosen; the doc's snow is weather and not built). Footsteps there wear the separate
  ice-wear layer (`RiverView.IceWear`: x`IceWearMul` 2 gain, side cells too, no decay, read by routing like wear, so
  lines form fast) and never the wear grid; it is wiped at the thaw, and walkers on the ice step off to a bank.
- **Weather stream**: the flood (two draws every spring, used or not) and the ice (one draw every winter) come from
  the river's own weather stream, `Mulberry32(seed * 7919 + 101)`, separate from the boat schedule's, so a flood or a
  freeze never moves a boat's arrival; the walkers' stream is never touched. The flood rises and the ice comes at the
  end of the tick that closes the previous day (the state between ticks belongs to the day the clock shows).
- **Walkers** route through the crossings with the existing cost fields. A ferry cell costs
  3 x (0.75 s + 1.0 s + learned wait) / cells, a drawbridge cell 0.8 + 3 x learned wait / cells; the learned wait is
  a moving average (alpha 0.2 per walker served), read when costs are rebuilt. A walker about to step onto a ferry
  or a drawbridge that is not down stops on its bank (`Walker.Hold = Waiting`), makes no footsteps, keeps `Stuck`
  at 0 and re-checks once a second that its route still uses the crossing. Ferry boarding is first come, first
  served; riders are drawn moving with the boat (`Hold = Riding`, counted on their boarding cell until they land).
  A walker still on the span when a leaf is fully up is set down on the nearer bank. Erasing a crossing releases
  its queue and lands its riders. A walker that waits 40 s (a safety net) goes home.
- **Money** starts at 120c. Purchases need the full price in hand; upkeep and flood repairs can take the purse into
  debt (10% interest a season). Each season: hearth tax 1c per house, minus upkeep and wages. Erasing a crossing costs
  half its price (chosen; the doc's 2x would stop testers trying several crossings) and is refused, with the reason in
  words, when it would take the purse below `DebtLimit` (-100c).
- **Tick order**: time and metrics, crowding, field refresh, `RiverStep` (boat spawns, boats, drawbridges and
  keepers, ferries, bridge gates), walkers (they read this tick's gates), decay, then the day and season books with the
  season's river events (flood, mud, ice, thaw).

```csharp
var world = new World(1, 200, SimParams.Default with { River = RiverRules.Default });
RiverView r = world.River!;                              // null when the rules are off
ReadOnlySpan<Boat> boats = r.Boats;                      // Id, Kind, State, Mast, Y, PrevY, Dir, Cargo, Capacity, Earned, PatienceLeft, Served, WaitingAt, Berth
IReadOnlyList<Crossing> xs = r.Crossings;                // ford first: Id, Type, Row, Cells, WestEnd, EastEnd, BridgeId, Clearance, State,
                                                         // StateT, Progress (leaf 0..1 or ferry 0 west..1 east), Keeper, QueueWest, QueueEast,
                                                         // BoatsWaiting, AvgWait, FerrySide, Passengers, WalkersServed, BoatsPassed,
                                                         // WalkersCrossed, WalkersWaited, Tokens (a bridge's room now)
bool flood = r.Flooded, ford = r.FordOpen, mud = r.Muddy, ice = r.Frozen; double dries = r.FloodEndsAt;
ReadOnlySpan<byte> band = r.FloodBand;                   // 1 on flood-band cells (fixed for the map)
ReadOnlySpan<float> iceWear = r.IceWear;                 // paths across the ice, 0..100 (draw like wear; never in World.Wear)
double money = r.Money, rep = r.Reputation, today = r.IncomeDay, season = r.IncomeSeason;
int row = r.LandingRow, cell = r.LandingCell, landing = r.LandingBuilding; // landing building -1 without goods
RiverStats s = r.Stats;                                  // trade, tax, upkeep, grain, boats served and turned back, walker waits, trip times
r.DrainEvents(list);                                     // since the last call; ring of 256; allocation-free once the list has room
// RiverEventKind: BoatArrived, BoatWaiting, BoatPassed, BoatMoored, BoatSold (coin), BoatTurnedBack, BoatLeft, BridgeRaising,
// BridgeRaised, BridgeLowering, BridgeLowered, FerryDeparted (riders), FerryLanded (riders), CannotAfford (price),
// SeasonSettled (net coin), KeeperHired, CrossingBuilt, CrossingRemoved, FloodRising (days), FordClosed, FloodReceded,
// FordOpen, FloodRepairs (coin), RiverFrozen, Thaw
// Walker: Hold (None, Waiting, Riding), HoldAt (crossing id), Cargo, WaitT

// Commands: RiverResult { Ok, Reason (words, e.g. "Not enough coin: the drawbridge costs 240c, you have 118c"), Id }
world.CheckCrossing(CrossingType.Drawbridge, x, y);      // what PlaceCrossing would say, without building
world.PlaceFerry(x, y); world.PlaceDrawbridge(x, y); world.PlaceFootbridge(x, y); world.PlaceCrossing(type, x, y);
world.PlaceBridge(x, y);                                 // with the river rules: a 50c footbridge; null on refusal, reason in LastRiverRefusal
world.SetBridge(id, raise: true); world.HireKeeper(id);
world.Erase(cells);                                      // also removes ferries and drawbridges (half the price, no refund);
                                                         // a crossing whose erase would take the purse below the debt limit stays
world.CheckErase(id); world.EraseCost(type);             // what Erase would say for a crossing, without removing it
world.SpawnBoat(BoatKind.Barge);                         // outside the schedule, for tests and tutorials
world.StartFlood(days); world.Freeze(); world.Thaw();    // the season's events now, for tests, tutorials and events (draw nothing)
world.CrossingAt(cell);                                  // crossing id on a cell, -1

// Flow preview of a crossing: same per-cell cost as built (nothing learned yet), plus the boats it would hold
WhatIf? change = world.CrossingWhatIf(CrossingType.Footbridge, x, y);   // null where none can stand
FlowPreview p = world.PreviewFlows(change!.Value);       // p.BoatsBlockedPerDay, p.BoatsNeedRaisePerDay
FlowPredictor.Describe(p, name);                         // "... Blocks barges: about 1 boat a day will turn back"
```

Where this differs from the implementation spec (the design doc wins, or a spec rule needed a decision):

- The village market buys at most 1 unit per household per day (design doc, Income sources); the spec left it out.
  Grain over the limit goes unsold (`RiverStats.GrainUnsold`).
- A served boat held north of the landing waits with its patience running and then leaves by the south edge with no
  loss of reputation (the spec has it wait for free, which would let a footbridge north of the landing trap boats
  for ever; flag E). The bot never builds there.
- The drawbridge and ferry span the river's water cells at that row (3 to 5 cells), not a fixed 4.
- Mast lowering (flag on the doc's barges) and loading speed are not modelled: walkers bring one unit at a time.
- Purchases cannot go into debt; only upkeep and repairs can (the doc allows debt to -100c in Standard mode). Erasing
  never takes the purse below the debt limit.
- The flooded band is waded slowly, not impassable: bridge ends and the landing stand in it.
- Rain, snow, fog and drought (doc, Weather) are not built; only the spring flood and winter ice are. Fields in the
  band do not yield more (the doc's +25%): fields have no yield yet.

### The river strategy bot

```
dotnet run --project tools/TideToll.Tools -c Release -- river --seed-sets 101-120,201-220 --sweep   # the tuning table: 12 and 30 days (~30 min)
dotnet run --project tools/TideToll.Tools -c Release -- river --seeds 4 --brief       # quick look: 30 days, medians only
dotnet run --project tools/TideToll.Tools -c Release -- river --days 12 --only S2,S5-30,S6 --set KeeperPrice=15   # tuning runs
dotnet test sim.tests -c Release --filter "FullyQualifiedName~RiverGateTests"         # the checks as tests (Category=Gate, ~10 min)
```

Players: S1 ford only; S2 ferry, S3 footbridge, S4 drawbridge never raised, all at row LandingRow + 4 (or the next
valid row south) once the coin is there; S5 drawbridge raised by hand when a boat it noticed has waited 4 s or two
noticed boats wait, at three attention levels: missing 10%, 30% and 50% of boats (S5-10, S5-30, S5-50; a fixed draw
per boat, so a less attentive player misses a superset); S6 drawbridge with a keeper hired as soon as affordable
(raised by hand until then, missing 30%). `--sweep` runs every player at 200 walkers and S2, S5-10 and S6 at 60, for
12 and 30 days. Mean trip time, mean wait, crossings a day (`cross/d`, all crossings; `at built/d` the player's own),
the share of crossings that waited at a gate first (`waited`) and grain delivered to the landing a day (`deliv/d`) are
measured over the second half of the run, when every player has built; the worst wait, the largest queue at the
player's crossing (`queue`), lost grain, flood and ice days and the money cover the whole run.

The checks (`RiverGateChecks`, on the longest run length, "most towns" = more than half the seeds):
(a) the footbridge gives the fastest trips and clearly the worst trade (at most 80% of any other player's but the
never-raised drawbridge); (b) ford only is clearly the slowest (10%+) where the ford is 15+ rows from the landing, and
nobody crosses the ford while the band is under water; (c) at 60 walkers the ferry ends richer than the hand-raised
drawbridge (S5-10) in most towns, and at 200 its capacity bites (mean wait and largest queue at least double those at
60, fewer deliveries than the drawbridge); (d) at 200 walkers S5-10 ends richer than the ferry in most towns, and not at
60; (e) at 200 walkers the keeper ends richer than S5-30 and than S5-50 in most towns (S5-10 is shown, need not lose);
(f) no player is best at trade income, trip time and net money at once. Then the spec's earlier checks (footbridge
turns back 60%+ of barges, the ford under 10% of boats, a never-raised drawbridge earns no more than a footbridge,
S5-10 and S6 turn back under 15%, manual batching has the lower worst wait). The old "keeper minus manual net money is
positive at 200 walkers" assumed a player who never looks away; it is printed as report only.

Last sweep: being run (25 November 2026); the table goes here when it finishes.

## Parity with the toy

Tested in `sim.tests` against `sim.tests/golden/toy-golden.json` (made from the toy by `sim.tests/golden/export.mjs`):

- Maps for seeds 1 to 50, the Dijkstra field, RNG, hash and `Line4`: identical.
- Wear grids: identical at every day mark for 45 days on seeds 1 to 8 (200 walkers) and 1 and 2 (500 walkers), with identical final stats and walker 0's first trip.
- The 20 gate seeds (101 to 120, 45 days): every table value identical.
- Flow previews at day 20 (seeds 2 and 4): routes, rates, cost ratios and readouts identical.
- Walker positions drift by about 1 ulp within a day, because .NET's `Math.Exp` (crowding speed) differs from V8's in the last bit on about 10% of inputs. Float32 storage absorbs it, so wear and metrics stay identical. Porting fdlibm's `exp` would make positions exact too.

The Engine tab's PCG32 generator and integer costs come later and will break this parity on purpose; regenerate the goldens then.

## Tests

`./check` (fast tier, Release, including the river rule tests), `./check --gate` (gate tables, comparison, 45-day traces, preview agreement, the river trade-off checks), `./check --bench` (also a 500-walker line with the river rules on). Headless runner: `dotnet run --project tools/TideToll.Tools -c Release -- run --seed 1 --walkers 500 --days 30` (also `bench`, `gate`, `checks`, `river`).

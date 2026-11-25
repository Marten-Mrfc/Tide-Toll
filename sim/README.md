# sim: TideToll.Sim

Purpose: the engine-free simulation library. The game (`game/`), the tests (`sim.tests/`) and the headless runner (`tools/TideToll.Tools`) all reference it, so the same code runs everywhere.

Rules: no Godot types, no `System.Random`, no clock. Everything is seeded and deterministic. Grids are flat arrays indexed `y * width + x`. `World.Tick()` allocates nothing (a test checks it).

It is a faithful port of the browser toy's simulation (`prototypes/desire-lines/src/sim/`): first of round 2 (commit `a057b50`), now of the finished toy (commit `6982009`, rounds 3 to 5: the retuned trail model with explorers and the detour cap, gravel, gates, rain and mud, house growth, the second flow preview; `docs/plans/sync-plan.md`, progress in `docs/plans/sync-progress.md`): same order of operations, formulas, constants and random draws. It keeps the toy's mulberry32 generator, float32 storage of wear, flow, cost and fields, and a replica of V8's `Math.hypot`, so runs can be compared with the toy bit for bit (see "Parity with the toy").

## Files

| File | Toy source | What it holds |
| --- | --- | --- |
| `SimParams.cs` | params.ts | every tunable, same names; presets `Default` (the finished toy as a player meets it: rain and growth on), `Toy` (the same with rain and growth off, as the gate runs) and `Legacy` (round 2); `Ground` and `GroundOf` |
| `Rng.cs` | rng.ts | `Mulberry32`, `Hash.Hash2` |
| `JsMath.cs` | (V8) | `Hypot` (V8's algorithm) and `Round` (JavaScript rounding) |
| `Grid.cs` | grid.ts | grid shape, neighbour tables, `Line4` |
| `BinaryHeap.cs`, `BucketQueue.cs`, `CostField.cs` | path.ts | the heap, the toy's bucket queue (the live fields; exactly the heap's values), Dijkstra cost-to-go fields, `CanStep`; `DijkstraRun` (the same field in bounded slices) |
| `MapData.cs`, `MapGen.cs` | mapgen.ts | `Building`, `MapData`, `MapGen.Generate(seed)` |
| `Walker.cs` | world.ts | `Walker` struct, `WalkState`, `Bridge` |
| `World.cs` | world.ts | state, field refresh schedule (both views), `Tick`, decay (gravel floor) |
| `WorldGround.cs` | world.ts | `CostOverride` flags, both walkers' ground (`Grounds`), `CellCosts`, `BuildCost`, blocking |
| `FenceGates.cs` | gates.ts | `GateTarget`, gate passage, `GateCellsFor`, `GateGroup`, the refusal reasons |
| `WorldWalk.cs` | world.ts, path.ts | trips, dwell, aim rule, footsteps and side wear, crowding speed |
| `WorldEdit.cs` | world.ts | fence, gravel (with the brush's snap), gates, footbridge, erase, eviction, urgent refresh |
| `WorldViews.cs` | world.ts | the read-only API, `Stats()`, `PreviewFlows` |
| `Metrics.cs` | metrics.ts | footstep window, trip ring, detour, coverage, both trunk shares, first trail |
| `FlowPreview.cs` | preview.ts | `WhatIf` (with `CrossingWhatIf`), `RouteChange`, `FlowPreview`, `GravelNote`, `Describe`, the gravel note |
| `FlowPredictor.cs`, `FlowPredictorRoutes.cs` | preview.ts | `FlowPredictor` (`Step()`, bounded `Step(maxWork)`): base and what-if fields for both kinds of walker, `outOfReach`; then the groups, sort, shown routes and the result |
| `RainWeather.cs`, `WorldRain.cs` | weather.ts, world.ts | the weather (own stream), the mud layer, its cost and speed, `StartRain`/`StopRain` |
| `Growth.cs` | growth.ts, world.ts | house growth: plot search (allocation-free), `AddHouse`, `Plot`, `GrownHouse`, `PlotSearch()` |
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
double CellCost(int cell, CostOverride ov = None, bool frugal = false); double TripRatePerDay(int home, int dest); int WalkersOut()
ReadOnlySpan<float>  CostX; ReadOnlySpan<float> FieldX(int building)   // the frugal walkers' view (empty unless Params.UseFrugal)
ReadOnlySpan<byte>   Gravel, Gate, GatePair                // Walker.Explorer, Walker.Frugal
int GatePartner(int cell)                               // the other cell of a two-cell gate, -1 (stored per cell, never found by adjacency)
ReadOnlySpan<byte>   Mud; int MudCells; RainWeather Weather // Rain, Wetness, Raining, RainStart, RainEnd, MudUntil, RainDays; bool RainOn
ReadOnlySpan<GrownHouse> Grown; ReadOnlySpan<byte> Fronted; double NextHouseTime; bool GrowthOn   // houses added by growth
bool GrowthRoomFull; int GrowthRoomLeft                 // growth's pre-sized house slots used up (say so: no more houses come)
long FieldSearches                                      // route searches run for the live fields (profiling)
int TripRateStride                                      // TripRate row length (room for grown houses; TripRatePerDay hides it)
SimStats Stats()       // DetourRatio, TrailCoverage, TrunkShare, TrunkShareD, TripsInWindow, TrailCells,
                       // Walkers, SimTime, Day, Season, Year, FirstTrailTime; allocation-free

// Commands (each edit bumps Version, rebuilds costs at once and refreshes fields over the next ticks)
int     PlaceFence(ReadOnlySpan<int> cells)             // returns cells newly fenced
Bridge? PlaceBridge(double x, double y)                 // null if no bridge fits at that river row
bool    BridgeCellsAt(double x, double y, List<int> cells)   // the cells a bridge would cover (for a ghost); false if an end is off the map
int     Erase(ReadOnlySpan<int> cells)                  // fences (with their gates), gravel and whole bridges touching the cells
int     PlaceGravel(ReadOnlySpan<int> cells); int GravelSnap(int cell); bool CanGravel(int cell)   // a fence over gravel takes it up
void    StartRain(double? simSec = null, double strength = 1); void StopRain(bool dry = false)   // debug and tutorials; no-ops with rain off
GateTarget GateTarget(double x, double y)               // Cells (1, or a stair corner's 2), Remove, Reason (FenceGates.NoFence, LonePost, NoWay)
bool    PlaceGate(int fenceCell); bool RemoveGate(int cell); int[]? GateCellsFor(int fenceCell)
void    SetWalkerCount(int n)                           // clamped to 0..MaxWalkers; never allocates
Grid.Line4(x0, y0, x1, y1, List<int> cells)             // a 4-connected fence line, clipped to the grid

// Flow preview: never touches the world's state or random stream
var p = new FlowPredictor(world);
p.Begin(new WhatIf { Block = cells }, maxPairs: 5);     // also Open, Bridge, CloseBridge, Gravel, Ungravel, Gate, Ungate (combined per cell)
while (p.Step()) { /* one Dijkstra per call (1 to 12 ms): spread over frames */ }
// or bounded slices, bit-identical results: about 1000 units is 0.1 to 0.8 ms (thread CPU time, Release)
while (p.Step(maxWork: 1000) && frameClockMs < 2) { }
FlowPreview r = p.Result!;                              // Changes (old/new route points, Explorers, Frugal), More, ReroutedPerDay,
                                                        // MeanCostRatio, Blocked, Note (gravel: Far, On, Near)
string line = FlowPredictor.Describe(r, b => name);     // "Reroutes about 40 walkers a day (+8% longer on average)"
world.PreviewFlows(whatIf, maxPairs)                    // the same in one call
```

Positions are in cell units (cell x spans x to x + 1). Draw walkers by interpolating `Px, Py` to `X, Y`; only `Visible` walkers (Out or Back) are on the map.

With `SimParams.Default` houses grow: `Buildings`, `Houses` and the walker count change during `Tick` (a cottage every
30 s while the town is below 250 people; the toy's frontage rules). Building indices never change; the game should
watch `Buildings.Length` (or `Grown`) and draw new cottages. The world pre-sizes room for `SimParams.MaxGrownHouses`
houses (84 at the defaults, about 10 MB of fields), so growth allocates nothing; if the walker count is lowered by hand
again and again that room can run out, and then no more houses come (the toy has no such limit): `GrowthRoomFull`
says so, so the game can tell the player instead of growth stopping silently.

### Presets

| Preset | Trail model | Rain and mud | House growth | Preview | Used by |
| --- | --- | --- | --- | --- | --- |
| `Default` (`new SimParams()`) | finished toy | on | on | version 2 | `new World(seed)`, the game, the bench |
| `Toy` | finished toy | off | off | version 2 | the gate (`tools gate`, GateTests), the comparison traces |
| `Legacy` | round 2 (2.4 / 0.3 / 0.6 / ford x3, no explorers) | off | off | version 1 | toy-golden.json, the pre-river baselines, the round-2 checks (`tools checks`) |

The river rules turn rain and growth off whatever the preset says (a river town's prices assume a village of fixed
size, and the river has its own flood mud).

## River rules (Prototype 1, step 2; second pass and third pass 25 November 2026)

Off by default: `SimParams.River` is null, nothing is allocated and no river code runs, so every result above is
unchanged (`RiverOffBaselineTests` holds wear and random-stream hashes recorded before the river code existed).
Turn them on with `new World(seed, walkers, SimParams.Default with { River = RiverRules.Default })`.

The rules in short (numbers are `RiverRules` defaults; 1 day = 15 s = 150 ticks):

- **Boats** enter at the south edge on a schedule (rowboat, mast 0, 12 units, 1 per 2 days; barge, mast 2, 40 units,
  1 per day), gap = base / (0.5 + reputation / 100) with +-25% jitter, drawn from the river's own stream
  (`Mulberry32(seed * 7919 + 211)`, `World.RiverStreamSeed`; until the third pass it was `+ 13`, the map generator's
  start state; the walkers' stream is never touched). They sail north at 10 rows/s to the
  landing (row = market door row clamped to 14..108, on the market bank), moor 15 s at one of 2 berths, buy grain,
  then leave by the north edge. A boat stops 2 rows below a crossing its mast cannot pass and keeps 2 rows behind
  the boat ahead (it overtakes a boat held at a crossing it can pass itself). Waiting costs patience (15 s); at 0 it
  turns back. Reputation 0..100 starts at 50, +1 per boat served, -5 per boat turned back.
- **Goods**: with `HaulGoods` (default) a Landing building is appended after the map's buildings (ids unchanged).
  A walker leaving the field takes 1 grain to the landing, then goes home. A moored boat with room buys it at
  2c x `BoatValueMul` (3) = 6c, booked when the boat leaves; otherwise the village market pays 70% of 2c, at most
  1 unit per house per day (the design doc's limit); the rest goes unsold. **Wet grain** (third pass, the lead's
  decision B): grain carried through the ford gets wet (`Walker.CargoWet`; not over the ice) and sells for
  `WetGrainValueShare` (0.5, chosen) of its value at a boat and at the market; a walker carrying grain wades the ford at
  `LoadedWadeSpeedMul` (0.7, chosen) of an unloaded walker's speed (routing costs are unchanged). Counted:
  `RiverStats.GrainDeliveredWet`, `GrainDeliveredDry`, `WetValueLost` (coin the wet grain did not fetch). The flood
  still closes the ford.
- **Crossings** (`Crossing.Clearance` is the highest mast class that passes): the ford (free, all boats); footbridge
  (50c, class 1, walker speed 1.2); ferry (40c, 2c upkeep + 3c ferryman a season, the doc's numbers (third pass; tuned to 5c + 15c before), never blocks, 5 walkers per trip,
  leaves 0.5 s after the first boards or when full, 1.0 s across); drawbridge (200c, 4c upkeep, class 1 closed and
  all boats raised; raise 1.0 s, one boat per 1.5 s, lower 1.0 s). A raised bridge lowers itself once the boats
  within 4 rows below are through, or after 8 s with no boat. The keeper (120c, 5c a season) raises 1 s after a
  masted boat is within 6 rows and lowers once it is through. **The keeper is unlocked by use** (third pass, decision C):
  it can be hired for a drawbridge only once the player has raised that bridge by hand `KeeperUnlockRaises` (10) times
  (`Crossing.HandRaises`, `RaisesToKeeper`, `KeeperUnlocked`; a press while it is already going up, and the keeper's
  own raises, do not count). Until then `HireKeeper` is refused in words, e.g. "No keeper will take this bridge on yet:
  raise it by hand six times more first (four of ten raises done)"; the `KeeperUnlocked` event names the bridge when
  it unlocks.
- **Capacity** (doc, crossing table "Walker capacity per day"): every crossing but the ford has a first come, first
  served queue on each bank. A footbridge lets on at most `FootbridgeWalkersPerDay` (120, doc "about 120, 1 cell wide")
  and a closed drawbridge `DrawbridgeWalkersPerDay` (250, doc "about 250 while closed") walkers a day, as a token bucket
  holding `BridgeBurstSec` (1 s, chosen) of capacity; a walker arriving goes straight on only when nobody queues on
  either bank and the bucket has room. Every walker on a deck was let on through the gate (third pass): a walker whose
  route does not use an open bridge (its line only cuts the corner of an end cell, or it aimed across before the route
  changed, as a drawbridge's learned wait changes its cost) is neither queued nor let on; it stops short of the deck
  and aims at the next cell down its own field. (Before, such walkers were let on free and some walked across
  uncounted: 25.5 crossings a day on a 20-a-day drawbridge.) The ferry turns round for `FerryTurnSec` (0.75 s, tuned) at each stage before
  boarding, so a full ferry carries 5 per 1.0 + 0.75 s leg, about 21 each way a day (the doc says about 30; tuned down so the ferry's capacity bites at 200 walkers). The ford
  has no limit, only its slow speed (`SimParams.FordSpeedMul` 0.4, doc "walker speed 0.4") and cost (x3). A walker
  who waited delivers its grain later (boats leave after their mooring window, so late grain goes to the market at
  35% of the boat price, or unsold); one who gives up after 40 s drops it (`RiverStats.GrainLost`).
  Counted: `Crossing.WalkersCrossed` and `WalkersWaited` (walkers who reached the other bank by it, and how many waited
  at a gate since their last crossing), `RiverStats.Crossings`, `CrossingsWaited`, `IceCrossings`, `GrainDelivered`.
- **Spring high water** (doc, "Spring high water", "Seasons and weather"; `SpringFlood`, on): on the first day of each
  spring but the first (the clock starts on one; the lead's rule: a new town never floods in its first spring, so a new
  player does not open with the ford closed; `FloodInFirstSpring`, off), with `FloodChance` (0.7, doc Riverbend), the flood band (`RiverView.FloodBand`: land
  within `FloodBandCells` (4, chosen) of the water, fixed for the map) is under water for 1 or 2 days (doc;
  `FloodMinDays`, `FloodMaxDays`). The ford is closed (blocked, infinite cost; walkers on it are set on a bank) and walkers
  wade the band at `FloodWadeSpeedMul` 0.4 and cost x`FloodWadeCostMul` 3 (chosen: like the ford; the band holds every
  bridge end and the landing, so it cannot simply be water), making no wear. Bridges and the ferry still work. Then the
  band is mud for the rest of spring: its trail, track and hollow-way cells cost x1.6, x1.7, x1.8 (doc mud multipliers;
  `MudTrailMul` and so on), so walkers braid round them; summer dries it. A flood in spring ends at the latest when summer
  begins, however many days `StartFlood` asked for, so no mud outlasts spring; a flood started outside spring (an event or
  a tutorial) keeps its days and leaves no mud. Each building in the band (not the landing) pays `FloodRepairCost` 10c per
  flood (doc, Costs; see Money for the debt limit); generated maps keep buildings out of a 4-cell band.
- **Winter ice** (doc, "Winter ice"; `WinterIce`, on): on the second day of each winter, with `IceChance` (0.5, doc
  Hamlet), the river freezes until the first day of spring. Boats stop where they are (patience and mooring clocks
  frozen, the schedule waits), boats buy nothing (grain goes to the market), a raised drawbridge comes down, the
  ferry's riders step off and it stays at its stage. Every water cell is walkable ice (cost and speed like grass,
  `IceCostMul`, `IceSpeedMul` 1, chosen; the doc's snow is weather and not built). Footsteps there wear the separate
  ice-wear layer (`RiverView.IceWear`: x`IceWearMul` 2 gain, side cells too, no decay, read by routing like wear, so
  lines form fast) and never the wear grid; it is wiped at the thaw, and walkers on the ice step off to a bank.
- **Not an edit**: the flood, the mud drying, the ice and the thaw rebuild costs, refresh every field urgently and move
  walkers off cells that became blocked, but do not bump `World.Version` (the player's edits), so the game's edit
  handlers do not run; a flow preview's base is at most a second old anyway.
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
- **Money** starts at 50c (third pass, decision D: enough for a ferry or a footbridge in the first minute; it was
  tuned from 120c to 0c before). Each season: hearth tax 1c per house, minus upkeep and wages, and 10% interest
  on a negative purse. Erasing a crossing costs half its price (chosen; the doc's 2x would stop testers trying several
  crossings). **The money rule, in one place:** a purchase (a crossing, a keeper) needs the full price in hand and never
  goes into debt; demolition (erasing a crossing) may take the purse down to `DebtLimit` (-100c) and no further, and is
  refused, with the reason in words, when it would go below; flood repairs may also take the purse into debt but stop at
  `DebtLimit` (the rest is let off, and the `FloodRepairs` event carries what was charged); the season's upkeep, wages
  and interest are charged in full, and are the only thing that can take the purse below the limit.
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
                                                         // WalkersCrossed, WalkersWaited, Tokens (a bridge's room now),
                                                         // HandRaises, RaisesToKeeper, KeeperUnlocked (drawbridge)
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
// FordOpen, FloodRepairs (coin), RiverFrozen, Thaw, KeeperUnlocked (crossing; value = hand raises)
// Walker: Hold (None, Waiting, Riding), HoldAt (crossing id), Cargo, CargoWet, WaitT
// RiverStats also: GrainDeliveredWet, GrainDeliveredDry, WetValueLost (coin)

// Commands: RiverResult { Ok, Reason (words, e.g. "Not enough coin: the drawbridge costs 200c, you have 118c"), Id }
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
- Purchases cannot go into debt (the doc allows debt to -100c in Standard mode); see the money rule above.
- The flooded band is waded slowly, not impassable: bridge ends and the landing stand in it.
- Rain, snow, fog and drought (doc, Weather) are not built; only the spring flood and winter ice are. Fields in the
  band do not yield more (the doc's +25%): fields have no yield yet.
- The browser toy's rain and mud and its house growth (ported from the toy in steps A6 and A7 of
  `docs/plans/sync-plan.md`) stay off while the river rules are on (the lead's decision, 25 November 2026): the river
  has its own flood mud, and its prices assume a village of fixed size. The toy's gravel and gates cost nothing under the
  river's money rules.

### The river strategy bot

```
dotnet run --project tools/TideToll.Tools -c Release -- river --seed-sets 101-120,201-220 --sweep   # the tuning table: 12 and 30 days (~16 min)
dotnet run --project tools/TideToll.Tools -c Release -- river --seeds 4 --brief       # quick look: 30 days, medians only
dotnet run --project tools/TideToll.Tools -c Release -- river --days 12 --only S2,S5-30,S6 --set KeeperPrice=15   # tuning runs
dotnet test sim.tests -c Release --filter "FullyQualifiedName~RiverGateTests"         # the checks as tests (Category=Gate, ~10 min)
```

Players: S1 ford only; S2 ferry, S3 footbridge, S4 drawbridge never raised, all at row LandingRow + 4 (or the next
valid row south) once the coin is there; S5 drawbridge raised by hand when a boat it noticed has waited 4 s or two
noticed boats wait, at three attention levels: missing 10%, 30% and 50% of boats (S5-10, S5-30, S5-50; a fixed draw
per boat, so a less attentive player misses a superset); S6 drawbridge with a keeper hired as soon as it is unlocked
(`KeeperUnlockRaises` hand raises of that bridge) and affordable (raised by hand until then, missing 30%). `--sweep` runs every player at 200 walkers and S2, S5-10 and S6 at 60, for
12 and 30 days. Mean trip time, mean wait, crossings a day (`cross/d`, all crossings; `at built/d` the player's own),
the share of crossings that waited at a gate first (`waited`) and grain delivered to the landing a day (`deliv/d`) are
measured over the second half of the run, when every player has built; the worst wait, the largest queue at the
player's crossing (`queue`), lost grain, flood and ice days and the money cover the whole run, as do `unlock d` (the day
the keeper was unlocked), `wet` (share of grain delivered wet) and `wet lost c` (coin wet grain did not fetch).

The checks (`RiverGateChecks`, on the longest run length, "most towns" = more than half the seeds; the lead's decision E,
third pass): (a) the footbridge gives the fastest trips and clearly the worst trade (at most 80% of any other player's
but the never-raised drawbridge); (b) the free ford costs trade: at 200 walkers ford only ends with clearly less money
(5%+) than the best purchase in most towns and 15%+ less at the median, and is the richest strategy in at most a quarter
of towns (the old form, "ford only is clearly the slowest where the ford is 15+ rows from the landing", is printed as
report only with its result), and nobody crosses the ford while the band is under water; (c) at 60 walkers the ferry
ends richer than the hand-raised drawbridge (S5-10) in most towns, and at 200 its capacity bites (mean wait and largest
queue at least double those at 60, fewer deliveries than the drawbridge); (d) at 200 walkers S5-10 ends richer than the
ferry in most towns, and not at 60; (e) at 200 walkers the keeper ends richer than S5-30 and than S5-50 in most towns
(S5-10 is shown, need not lose), and no keeper is ever hired before its unlock; (f) no player is best at trade income,
trip time and net money at once; (g) pacing: a crossing is affordable on the first day (ferry and footbridge runs build
by day 1), the drawbridge is bought after 6 to 10 days at 200 walkers (median of S5-10), and the keeper comes no sooner
than its unlock. Then the spec's earlier checks (footbridge turns back 60%+ of barges, the ford under 10% of boats, a
never-raised drawbridge earns no more than a footbridge, S5-10 and S6 turn back under 15%, manual batching has the lower
worst wait). The old "keeper minus manual net money is positive at 200 walkers" is printed as report only.

The sweep below predates the third pass (the table of the third pass is in its own section further down).

Last sweep (25 November 2026, `river --seed-sets 101-120,201-220 --sweep`, 16 min on 3 threads; every `RiverRules`
number at the values listed above, none tuned this round). Medians over 40 seeds, 30 days:

```
MEDIANS OVER SEEDS, 30 days (40 seeds)
 player                                | walkers | built d | keeper d | trade c | boat c | market c | turn-back | barges | mean wait s | worst wait s | trip s | cross/d | at built/d | waited | deliv/d | lost | queue | rep | money c
 S2 ferry                              |      60 |     0.0 |        - |     554 |    542 |       13 |      0.0% |   0.0% |        1.65 |          6.1 |  16.38 |      23 |         18 |  72.6% |     5.2 |    0 |    12 |  94 |     647
 S5 drawbridge, manual, misses 10%     |      60 |     8.0 |        - |     572 |    556 |       22 |      4.8% |   7.1% |        1.23 |          2.3 |  15.35 |      24 |         20 |  14.4% |     5.6 |    0 |     8 |  80 |     474
 S6 drawbridge + keeper                |      60 |     8.0 |      9.3 |     564 |    550 |       16 |      0.0% |   0.0% |        1.32 |          2.9 |  15.03 |      24 |         21 |  19.5% |     5.5 |    0 |     7 |  94 |     416
 S1 ford only                          |     200 |       - |        - |    1454 |   1444 |       22 |      0.0% |   0.0% |        0.00 |          0.0 |  18.13 |      57 |          0 |   0.0% |    14.2 |    0 |     0 |  94 |    1630
 S2 ferry                              |     200 |     0.0 |        - |    1591 |   1578 |       25 |      0.0% |   0.0% |        4.71 |         20.4 |  19.02 |      68 |         15 |  44.0% |    15.5 |    0 |    52 |  94 |    1690
 S3 footbridge                         |     200 |     0.0 |        - |     636 |    382 |      259 |     66.7% | 100.0% |        0.20 |          1.0 |  15.67 |      77 |         73 |   3.7% |    17.9 |    0 |     8 |   0 |     759
 S4 drawbridge, never raised           |     200 |     5.6 |        - |     796 |    568 |      214 |     50.0% |  75.0% |        0.00 |          0.0 |  16.10 |      76 |         69 |   0.0% |    17.9 |    0 |     0 |   0 |     704
 S5 drawbridge, manual, misses 10%     |     200 |     5.6 |        - |    1652 |   1600 |       48 |      4.8% |   7.3% |        1.11 |          2.3 |  17.13 |      69 |         57 |  15.3% |    17.2 |    0 |    20 |  80 |    1564
 S5 drawbridge, manual, misses 30%     |     200 |     5.6 |        - |    1479 |   1402 |       97 |     16.7% |  25.4% |        1.14 |          2.3 |  16.86 |      72 |         61 |   9.7% |    17.4 |    0 |    17 |  50 |    1387
 S5 drawbridge, manual, misses 50%     |     200 |     5.6 |        - |    1262 |   1118 |      139 |     26.9% |  40.4% |        1.10 |          2.3 |  16.20 |      75 |         63 |   5.4% |    17.2 |    0 |    16 |  26 |    1167
 S6 drawbridge + keeper                |     200 |     5.6 |      5.8 |    1652 |   1632 |       27 |      0.0% |   0.0% |        1.30 |          4.4 |  16.86 |      68 |         55 |  22.1% |    16.9 |    0 |    24 |  94 |    1496
```

```
RIVER CHECKS over 30 days (40 seeds)
  PASS  (a) footbridge gives the fastest walker trips (median mean trip: S3 15.67 s; fastest S3 15.67 s)
  PASS  (a) footbridge has clearly the worst trade: at most 80% of every other player's but the never-raised drawbridge (S3 636c, next lowest S5-50 1262c; S4 796c)
  FAIL  (b) ford only is clearly the slowest (10%+ over every other player) on the 25 towns with the ford 15+ rows from the landing (S1 19.40 s, next slowest S2 19.28 s)
  PASS  (b) nobody crosses the ford while the flood band is under water (0 crossings over 1012 flood days in all runs)
  PASS  (c) at 60 walkers the ferry ends with more net money than the hand-raised drawbridge (S5, 10% missed) in most towns (38 of 40 towns; medians S2 647c, S5 474c)
  PASS  (c) at 200 walkers the ferry's capacity bites: mean wait and largest queue at least double those at 60, and fewer deliveries than the drawbridge (wait 1.65 s -> 4.71 s, queue 12 -> 52, deliveries a day S2 15.5, S5 17.2)
  FAIL  (d) at 200 walkers the hand-raised drawbridge (10% missed) ends with more net money than the ferry in most towns (7 of 40 towns; medians S5 1564c, S2 1690c)
  PASS  (d) at 60 walkers it does not (2 of 40 towns)
  PASS  (e) at 200 walkers the keeper ends with more net money than the player missing 30% of boats in most towns (34 of 40 towns; medians S6 1496c, S5-30 1387c)
  PASS  (e) at 200 walkers the keeper ends with more net money than the player missing 50% of boats in most towns (40 of 40 towns; S5-50 1167c)
  info  (e) against the player missing 10% (need not win): 19 of 40 towns; S5-10 1564c
  PASS  (f) no strategy is best at trade income, trip time and net money at once (trade income: S6, trip time: S3, net money: S2)
  PASS  footbridge turns back more than 60% of barges (S3 median 100.0%)
  PASS  ford only turns back fewer than 10% of boats (S1 median 0.0%)
  PASS  never-raised drawbridge earns no more than the footbridge (net money S4 704c, S3 759c)
  PASS  manual (10% missed) and keeper drawbridges turn back fewer than 15% of boats (S5 4.8%, S6 0.0%)
  PASS  manual drawbridge has the lower worst walker wait of S5 (10% missed) and S6 (S5 2.3 s, S6 4.4 s)
  report only  keeper minus manual (10% missed) net money at 200 walkers is positive: no (-68c; at 60 walkers -59c)

FAILED: 2 of 16 check(s) in 969 s (3 threads)
```

At 12 days (same seeds) the drawbridge is built on day 5.6 and the keeper on day 5.8 at 200 walkers (8.0 and 9.3 at
60): money still comes faster than the doc's early figure, which is the tuner's job. What fails: (b) the ford is not
clearly the slowest, because at 200 walkers the ferry's queue makes it as slow (19.3 s against 19.4 s); (d) the
hand-raised drawbridge ends richer than the ferry in only 7 of 40 towns (the ferry costs 40c against 240c and walkers
learn its wait and use the ford instead).

### The finished toy's trail model and the river (sync step A8, 25 November 2026; nothing retuned)

`river --seed-sets 101-120 --days 30 --sweep --trail toy` against `--trail legacy`, same build, same seeds (20 towns, 30 days;
both runs include the first-spring rule, so they compare only with each other, not with the 40-seed table above). Medians:

| player (200 walkers unless noted) | net money legacy -> toy | trip s legacy -> toy | trade c legacy -> toy |
| --- | --- | --- | --- |
| S1 ford only | 2276 -> 2359 | 18.05 -> 18.00 | 2223 -> 2306 |
| S2 ferry | 2242 -> 2227 | 19.13 -> 18.68 | 2351 -> 2339 |
| S3 footbridge | 1043 -> 1047 | 14.77 -> 15.17 | 1038 -> 1052 |
| S4 drawbridge, never raised | 990 -> 1025 | 15.01 -> 15.71 | 1161 -> 1197 |
| S5 drawbridge by hand, misses 10% | 2425 -> 2378 | 15.82 -> 16.39 | 2600 -> 2549 |
| S5, misses 30% / 50% | 2155 / 1703 -> 2084 / 1759 | 15.60 / 15.21 -> 16.15 / 15.95 | 2323 / 1878 -> 2259 / 1934 |
| S6 drawbridge + keeper | 2342 -> 2154 | 15.95 -> 16.96 | 2660 -> 2476 |
| S2 ferry, 60 walkers | 767 -> 772 | 15.47 -> 15.38 | 876 -> 880 |
| S5-10, 60 walkers | 727 -> 730 | 14.50 -> 14.33 | 898 -> 898 |
| S6, 60 walkers | 579 -> 538 | 14.38 -> 14.42 | 899 -> 846 |

What shifts: with the new trail model, trips over the bridges get slower (footbridge +0.4 s, drawbridges +0.5 to 1.0 s; explorers
and detour-capped walkers ignore the trunks), the ferry's share of walkers who wait rises from 12% to 30% and its queue from 40 to
48, and the keeper loses most (-188c at 200 walkers: 6.1 days to hire instead of 5.9 and fewer boats a day through). Checks: legacy
fails 1 of 16 ((b) ford clearly slowest: S1 18.34 s, S2 19.67 s on the 11 far towns); toy fails 2 of 16: (b) again (S1 18.27 s, S2
19.04 s) and (d) the hand-raised drawbridge ends richer than the ferry in only 9 of 20 towns at 200 walkers (14 of 20 on legacy;
medians S5 2378c, S2 2227c). The keeper minus the 10% player at 200 walkers goes from -83c to -224c (report only). Left for the
river's next tuning round.

Three River* tests fail on the new trail model (all pass on `Legacy`: `TIDETOLL_RIVER_PRESET=legacy`): a narrow drawbridge (20 a
day) lets on 25.5 a day (a walker whose any-angle line cuts across the span while its three-step descent does not touch it is
treated as clipping the corner and let on without the queue; on Legacy no walker does); the learned wait at a raised drawbridge
ends at 0.35 s, under the test's 0.5 s (more walkers use the bridge, so short waits at capacity outnumber the raised spells in its
moving average); and a walker set down from a rising leaf takes over 40 s to finish its trip (it waits out the raise, crosses,
then has a long walk; all three fixed in the third pass, below). Fixed on the way (both presets): the route check of a waiting walker now uses the walker's own view
(frugal walkers descend the frugal field: sync plan conflict A), and a queue pass for a drawbridge that was raised since is spent
instead of letting the walker onto the raised span.

### Tuning, 25 November 2026 (strategy bot, seeds 101-120 and 201-220, 30 days)

Changed `RiverRules` defaults (before -> after; doc figure; why):

| Value | Before | After | Doc | Why |
| --- | --- | --- | --- | --- |
| StartMoney | 120 | 0 | none | drawbridge bought on day 5.5 at 200 walkers (day ~9 at 60); ferry on day 4.4 |
| BoatValueMul | 2 | 3 | "raise boat value" | a delivery is worth more, so a faster crossing pays back |
| DrawbridgePrice | 240 | 200 | 200c + 40 stone | payback against the ferry within 30 days at 200 walkers |
| KeeperPrice | 30 | 120 | none (chosen) | keeper hired about 1 day after the drawbridge at 200 walkers (3.5 at 60) |
| FerryCapacity | 6 | 5 | none | the ferry's queue bites at 200 walkers |
| FerryTurnSec | 0.5 | 0.75 | none | same (about 21 a day each way against the doc's 30) |
| FerryUpkeep / FerryWage | 2 / 3 | 5 / 15 | 2 / 3 | the ferry is cheap to buy but costs more to run (150c over 30 days against 40c) |

Result on 40 seeds at 30 days (medians, 200 walkers): ford only 2231c net, ferry 2153c, drawbridge by hand missing 10% 2246c,
30% 2013c, 50% 1623c, keeper 2129c, footbridge 1005c. Checks: 15 of 16 pass; (b) fails (ford 19.40 s, ferry 20.32 s on the
25 far towns: a ferry whose capacity bites is as slow as the ford). Tried and dropped: a longer, likelier flood (FloodChance .9,
3 days, wade speed .25) made bridges worse too; FerryCapacity 8 with turn 0.25 passes (b) but then (c) and (d) fail; keeper at
160c makes (e) against the 30% player a coin flip (22 of 40 towns). Cliffs: keeper price (60 to 120 safe, 160 not), ferry
capacity and turn (above 7 or below 0.4 s flips (b)/(c)/(d)).

### Third pass, 25 November 2026 (green on the new trail model, bugs, the lead's decisions B to E)

**The three failing river tests** had one cause. On the finished toy's trail model a drawbridge's learned wait changes its
routing cost, a field refresh sends some walkers along the bank instead, but they still walk towards the aim they took
before, onto the first deck cell. The corner-clipper rule (route does not use the bridge within three steps) then let them
on without the queue, and from the deck they walked across uncounted (seed 5, a 20-a-day drawbridge: 28 of 28 such walkers
crossed, 25.5 crossings a day; on Legacy and on footbridges none). Now such a walker never steps onto the deck: it stops
short and aims at the next cell down its own field (`RiverGate` returns `TurnAside`). The capacity test (18.25 a day on a
20-a-day bridge) and the rising-leaf test pass unchanged; one expectation was restated with its reason beside it:
`WalkersQueueWhileTheBridgeIsUp` read the drawbridge's learned wait (a moving average over the last walkers served) at the
very end, 100 ticks after the last queue had cleared; it now reads it right after each raise's queue has gone over (> 0.5 s
every time). Every River* test passes on all three trail models (`TIDETOLL_RIVER_PRESET` unset, `legacy`, `toy`).

**Bugs fixed**, each with a test (`RuleFixes3Tests`, `ToyV2ParityTests.TheWeatherStreamOfLargeSeedsMatchesTheToy`):
gate pairs are stored per cell (`World.GatePartner`), so removing a two-cell gate never takes a neighbouring gate with it
and the tool names exactly the cells it removes (the toy found partners by adjacency); the weather stream's seed is the
toy's double product (`RainWeather.StreamSeed`; the exporter's new `streamsLarge` section, drawn from the toy's own
`Weather` class, differs from the old integer formula for seeds 3400001, 12345679, 99999999 and 2147483647, and every
earlier section of the re-exported file is identical); the boat schedule's stream has its own seed (`seed * 7919 + 211`,
was the map generator's `+ 13`: every river run's boat gaps changed, and no river test had recorded them); growth says
when its pre-sized room is used up (`GrowthRoomFull`, `GrowthRoomLeft`); and a test holds rain, mud and growth off
through four years of a river town (floods every spring) although `SimParams.Default` has them on.

**The worst tick**: a tick that refreshes a field runs `RebuildCosts` and two route searches (both views): about 3 ms
against 0.4 ms for a tick without. The live fields now use the toy's bucket queue (`BucketQueue.cs`, plan step A10),
with a fast path for cells off the border: 1.03 ms a search against the heap's 2.40 on the 128 x 128 test map, every value
identical (`CostFieldTests`: random maps, wide cost ranges, fallbacks, three live towns with the river; the toy's own
`dijkstraLow`; every golden trace). The first household's tick (10.4 ms; later ones 1.7 ms) was the plot search's first
compile: the world runs the search once at construction (it only fills scratch). `tools ... ticks` times every tick of a
run and lists the worst with what ran in them. Measured with `ticks --days 20`, seed 1, 3000 ticks (4 shared cores, so
single ticks above about 8 ms without a route search are the machine):

PERF_TABLE

**The lead's decisions B, C and D** (rules above): wet grain at the ford (`WetGrainValueShare` 0.5, `LoadedWadeSpeedMul`
0.7, both new and chosen); the keeper unlocked by `KeeperUnlockRaises` (10, new) hand raises of that drawbridge; the
starting purse; the ferry's running costs. Tests: `RiverRules3Tests`. River mode only: without the river rules nobody
carries grain, and every comparison with the toy is unchanged.

| `RiverRules` value | Before | After | Source |
| --- | --- | --- | --- |
| StartMoney | 0 | 50 | decision D: a ferry (40c) or a footbridge (50c) in the first minute |
| FerryUpkeep | 5 | 2 | decision D: the design doc's number |
| FerryWage | 15 | 3 | decision D: the design doc's number |
| WetGrainValueShare | (none) | 0.5 | decision B, chosen starting number |
| LoadedWadeSpeedMul | (none) | 0.7 | decision B, chosen starting number |
| KeeperUnlockRaises | (none) | 10 | decision C: "start at 10" |

The tuner works from this table: `river --seed-sets 101-120 --sweep` at the numbers above, nothing tuned (439 s on 3
threads; seeds 501-524 are the reviewer's only). Medians over 20 seeds, 30 days:

```
MEDIANS OVER SEEDS, 30 days (20 seeds)
 player                                | walkers | built d | unlock d | keeper d | trade c | boat c | market c | turn-back | barges | mean wait s | worst wait s | trip s | cross/d | at built/d | waited | deliv/d | lost | queue |    wet | wet lost c | rep | money c
 S2 ferry                              |      60 |     0.0 |        - |        - |     799 |    777 |       11 |      0.0% |   0.0% |        2.08 |          7.8 |  15.59 |      24 |         20 |  86.7% |     5.7 |    0 |    14 |   0.0% |          0 |  94 |     823
 S5 drawbridge, manual, misses 10%     |      60 |    11.0 |     21.2 |        - |     700 |    692 |       16 |      4.0% |   6.1% |        1.21 |          2.3 |  14.45 |      24 |         21 |  16.0% |     5.7 |    0 |     7 |  34.0% |        156 |  86 |     588
 S6 drawbridge + keeper                |      60 |    11.0 |     23.7 |     23.7 |     692 |    668 |       24 |      9.2% |  13.8% |        1.19 |          2.4 |  14.67 |      23 |         21 |  12.4% |     5.8 |    0 |     7 |  34.6% |        156 |  70 |     442
 S1 ford only                          |     200 |       - |        - |        - |    1098 |   1084 |        9 |      0.0% |   0.0% |        0.00 |          0.0 |  18.54 |      60 |          0 |   0.0% |    13.9 |    0 |     0 |  97.4% |       1119 |  94 |    1204
 S2 ferry                              |     200 |     0.0 |        - |        - |    1418 |   1410 |       11 |      0.0% |   0.0% |        5.31 |         19.5 |  19.33 |      68 |         24 |  40.3% |    14.8 |    0 |    53 |  75.8% |        882 |  94 |    1438
 S3 footbridge                         |     200 |     0.0 |        - |        - |     888 |    621 |      267 |     66.0% | 100.0% |        0.27 |          1.1 |  15.40 |      81 |         75 |   6.4% |    18.2 |    0 |     9 |   0.0% |          0 |   0 |     940
 S4 drawbridge, never raised           |     200 |     6.2 |        - |        - |    1013 |    804 |      213 |     48.4% |  73.9% |        0.00 |          0.0 |  16.01 |      78 |         73 |   0.0% |    17.5 |    0 |     0 |  18.0% |        233 |   0 |     884
 S5 drawbridge, manual, misses 10%     |     200 |     6.2 |     16.4 |        - |    2185 |   2150 |       34 |      4.5% |   7.0% |        1.12 |          2.3 |  16.45 |      68 |         60 |  17.4% |    17.1 |    0 |    20 |  18.8% |        245 |  82 |    2053
 S5 drawbridge, manual, misses 30%     |     200 |     6.2 |     19.2 |        - |    1954 |   1872 |       67 |     16.7% |  25.0% |        1.08 |          2.3 |  16.53 |      71 |         63 |  12.7% |    16.9 |    0 |    18 |  18.4% |        240 |  50 |    1829
 S5 drawbridge, manual, misses 50%     |     200 |     6.2 |     20.3 |        - |    1589 |   1472 |      123 |     25.0% |  38.8% |        1.07 |          2.3 |  16.17 |      76 |         67 |   6.2% |    16.9 |    0 |    16 |  18.8% |        239 |  31 |    1471
 S6 drawbridge + keeper                |     200 |     6.2 |     19.2 |     19.2 |    1992 |   1944 |       46 |      7.9% |  12.0% |        1.12 |          2.4 |  16.29 |      69 |         61 |  16.2% |    16.6 |    0 |    20 |  18.8% |        240 |  73 |    1734
```

```
RIVER CHECKS over 30 days (20 seeds)
  PASS  (a) footbridge gives the fastest walker trips (median mean trip: S3 15.40 s; fastest S3 15.40 s)
  FAIL  (a) footbridge has clearly the worst trade: at most 80% of every other player's but the never-raised drawbridge (S3 888c, next lowest S1 1098c; S4 1013c)
  PASS  (b) the free ford costs trade: at 200 walkers ford only ends with clearly less money (5%+) than the best purchase in most towns (20 of 20) and 15%+ less at the median (median shortfall 42.2%; wet grain lost, median 1119c, 97.4% of its grain delivered wet)
  PASS  (b) ford only is the richest strategy at 200 walkers in at most a quarter of towns (0 of 20)
  report only  (b, old form) ford only is clearly the slowest (10%+ over every other player) on the 11 towns with the ford 15+ rows from the landing: no (S1 18.95 s, next slowest S2 19.42 s)
  PASS  (b) nobody crosses the ford while the flood band is under water (0 crossings over 209 flood days in all runs)
  PASS  (c) at 60 walkers the ferry ends with more net money than the hand-raised drawbridge (S5, 10% missed) in most towns (16 of 20 towns; medians S2 823c, S5 588c)
  PASS  (c) at 200 walkers the ferry's capacity bites: mean wait and largest queue at least double those at 60, and fewer deliveries than the drawbridge (wait 2.08 s -> 5.31 s, queue 14 -> 53, deliveries a day S2 14.8, S5 17.1)
  PASS  (d) at 200 walkers the hand-raised drawbridge (10% missed) ends with more net money than the ferry in most towns (18 of 20 towns; medians S5 2053c, S2 1438c)
  PASS  (d) at 60 walkers it does not (4 of 20 towns)
  FAIL  (e) at 200 walkers the keeper ends with more net money than the player missing 30% of boats in most towns (4 of 20 towns; medians S6 1734c, S5-30 1829c)
  PASS  (e) at 200 walkers the keeper ends with more net money than the player missing 50% of boats in most towns (18 of 20 towns; S5-50 1471c)
  info  (e) against the player missing 10% (need not win): 0 of 20 towns; S5-10 2053c
  PASS  (e) the keeper is never hired before its unlock (10 hand raises): 40 hires over 40 keeper runs, all after the unlock: yes
  PASS  (f) no strategy is best at trade income, trip time and net money at once (trade income: S5-10, trip time: S3, net money: S5-10)
  PASS  (g) one crossing is affordable in the first in-game day: purse 50c, ferry 40c, footbridge 50c; 60 of 60 ferry and footbridge runs built by day 1
  PASS  (g) at 200 walkers the drawbridge is bought after 6 to 10 in-game days (median S5-10 6.2; S6 6.2)
  PASS  (g) the keeper comes no sooner than its unlock after the drawbridge: unlocked a median 12.9 days and hired 12.9 days after the drawbridge at 200 walkers (20 of 20 unlocked, 20 hired)
  PASS  footbridge turns back more than 60% of barges (S3 median 100.0%)
  PASS  ford only turns back fewer than 10% of boats (S1 median 0.0%)
  PASS  never-raised drawbridge earns no more than the footbridge (net money S4 884c, S3 940c)
  PASS  manual (10% missed) and keeper drawbridges turn back fewer than 15% of boats (S5 4.5%, S6 7.9%)
  PASS  manual drawbridge has the lower worst walker wait of S5 (10% missed) and S6 (S5 2.3 s, S6 2.4 s)
  report only  keeper minus manual (10% missed) net money at 200 walkers is positive: no (-319c; at 60 walkers -147c)

FAILED: 2 of 21 check(s) in 439 s (3 threads)
```

What fails, for the tuner: (a) the footbridge's trade is 81% of ford only's (888c against 1098c; the check wants at most
80%), now that wet grain costs ford only 1119c at the median; (e) the keeper beats the player missing 30% in only 4 of 20
towns: it is unlocked a median 12.9 days after the drawbridge (the S6 player raises about once a day and misses 30% of
boats until then) and then costs 120c, so it has little of the run left to pay back. (b) in its new form passes with room
to spare (ford only 42% below the best purchase at the median, the lead aimed at 15%), so `WetGrainValueShare` can go up.

## Parity with the toy

Tested in `sim.tests` against `sim.tests/golden/toy-golden.json` (made from the round-2 toy by `sim.tests/golden/export.mjs --legacy`), on `SimParams.Legacy`:

- Maps for seeds 1 to 50, the Dijkstra field, RNG, hash and `Line4`: identical.
- Wear grids: identical at every day mark for 45 days on seeds 1 to 8 (200 walkers) and 1 and 2 (500 walkers), with identical final stats and walker 0's first trip.
- The 20 gate seeds (101 to 120, 45 days): every table value identical.
- Flow previews at day 20 (seeds 2 and 4): routes, rates, cost ratios and readouts identical.
- Walker positions drift by about 1 ulp within a day, because .NET's `Math.Exp` (crowding speed) differs from V8's in the last bit on about 10% of inputs. Float32 storage absorbs it, so wear and metrics stay identical. Porting fdlibm's `exp` would make positions exact too.

The finished toy (`toy-golden-v2.json`, made by `node sim.tests/golden/export.mjs --legacy <toy-r2> <final-toy>`, or
`node sim.tests/golden/export.mjs prototypes/desire-lines` for version 2 alone, about 11 minutes; `ToyV2*Tests`): every one of the
10 off traces (rain and growth off, `SimParams.Toy`) identical for 45 days in wear, flow, cost, costX, frugal walkers, walker 0's first
trip and final stats; the 4 rain-only, 2 growth-only and 4 live traces likewise, with mud, the weather and the grown houses; the weather
alone on 50 seeds for 120 days; the plot search (counts, best eight, exact scores, choice); the gate table on seeds 101 to 120 exactly
the toy's, printed text included; the edits at fixed ticks (fence, gravel, gates at 0, 30, 45, 60 and 90 degrees, refusals, bridge,
erase, a forced shower), off, rain only and live, identical every 50 ticks; the flow previews (block, bridge, enclose, gravel on, near
and far, gate, ungate) off and in a live town with grown houses: rates, groups, cost ratios, mean, note, readout and route hashes; the
day-20 baselines (wear and the walkers' next draws) off and live; the lookup tables identical. On this machine 11 of 146 maths probes (`pow(0.5, 1/20)`, some `exp` and `pow`) differ from V8 in the last
bit of the double; they agree at float32, where results are stored (`MathsProbesMatchV8BitForBit`, Gate, fails honestly).

The Engine tab's PCG32 generator and integer costs come later and will break this parity on purpose; regenerate the goldens then.

## Tests

`./check` (fast tier, Release, including the river rule tests), `./check --gate` (gate tables, comparison, 45-day traces, preview agreement, the river trade-off checks), `./check --bench` (also a 500-walker line with the river rules on). Headless runner: `dotnet run --project tools/TideToll.Tools -c Release -- run --seed 1 --walkers 500 --days 30` (also `bench`, `gate`, `checks`, `river`).

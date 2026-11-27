# Code map: how villagers get their work today

Read-only lookup of the repository as it is. Line numbers are as read. "Not read" means the file was not opened in full. No design proposals.

## 1. How a villager gets its work

**Per tick** (`sim/Town/Town.cs:76-77`): every villager in array order is stepped (`for (i...) StepVillager(vs[i])`). Then `StepRequests`, `StepHands`, `StepRiver`.

`StepVillager` (`sim/Town/TownJobs.cs:8-29`): `Idle` calls `Arrive(v, 0)` every tick (line 15). `Walking` calls `Arrive` when `Walk(v)` returns true. `Working` counts `WorkLeft` down (line 21). `Queued` does nothing.

`Arrive` (`TownJobs.cs:32-57`):
- `if (v.Job == JobKind.None) { if (!ChooseJob(v)) return; ... }` (lines 35-37). A villager with no job retries `ChooseJob` on every tick.
- At the workplace (`Stage < 2`): if the workplace is closed, `DropJob` (line 45). If a place is free and nobody queued, `StartWork` (line 46). Otherwise the villager joins `w.Queue` and becomes `Queued` (lines 47-51).
- At the delivery (`Stage == 2`): `Deliver`, `DropJob`, then `ChooseJob` again at once (lines 54-56).

`DropJob` (`TownJobs.cs:59-62`) sets `Job=None, Stage=0, Workplace=-1, Phase=Idle`. It is called when a workplace closes (45), after a delivery (55), when there is no route (117-121, 221) and at the end of a river job (`TownRiverJobs.cs`, `EndRiverJob`).

**Scoring** (`ChooseJob`, `TownJobs.cs:184-223`). The docstring says: "Picks the job paying the most coin per second (travel, work and expected wait), then sets off. Ties: lower workplace, then job order."

Candidates, scanned in workplace index order `wi` (lines 193-213). Mills are skipped as workplaces (line 198):
- Well: `Water`, seconds = `toW + wait + WorkTime(Well)/ToolsFactor + TravelTime(well, home)` (line 205).
- Field: `GrainHome` (line 209). `GrainMarket` if the market stands (line 210). `Flour` if market and mill stand (lines 211-212), using `ExpectedWait(mill, v)`.

`Consider` (`TownJobs.cs:225-230`):
```
double rate = load * JobValue(j) / Math.Max(0.1, seconds);
if (rate > best + 1e-12) { best = rate; bestJob = j; bestW = wi; }
```
`load = LoadUnits * CoinBonus` (line 192). `best` starts at 0, so a zero-rate job is never chosen. Ties: the strict `>` keeps the first candidate in scan order, which is the lower workplace index, then the earlier job in the order above.

`JobValue` (`TownJobs.cs:130-143`): river jobs go to `RiverJobValue`. Otherwise `base * ValueFactor(good) * (RequestBonus if the request asks for this good) * (DriveBonus if the drive asks for it)`. Base values come from `TownRules.WaterValue`, `GrainHomeValue`, `GrainMarketValue`, `FlourValue`.

River candidates (`TownJobs.cs:214-218`): `ConsiderRiver(v, ...)` is run with the same `best`. If the winner is `WoolBoat`, `WoolMarket`, `ClothBoat` or `ClothMarket`, the villager goes to `StartRiverJob` (line 217). `GrainBoat` and `FlourBoat` winners fall through to the ordinary path at lines 219-222 with `Workplace = bestW`.

**Time term** (`ExpectedWait`, `TownJobs.cs:170-182`):
```
int committed = w.Busy + w.QueueLen;
foreach (var o in S.Villagers)
    if (o != self && o.Workplace == wi && o.Phase == VillagerPhase.Walking) committed++;
int over = committed - w.Places + 1;
if (over <= 0) return 0;
return over * work / ToolsFactor / w.Places;
```
The wait counts villagers already walking to the same workplace, so it is the only thing that changes between two decisions in one tick. Villagers step in array order, so a later villager sees an earlier one's committed walk once `SetRoute` has set `Walking` (`TownWalk.cs:100-119` calls `EnterRoad`; `EnterRoad` body not read).

**Re-choosing**: an idle villager (no job) calls `ChooseJob` every tick until one is found. A villager that has delivered calls it straight after `DropJob`. A job is not revisited while it is held. There is no timer or reroll.

**Two villagers deciding in the same tick**: nothing random and no per-villager preference. Each one scores the same values. They differ only by home node (travel), the array order, and the committed count from `ExpectedWait`, which grows as earlier villagers start walking to a place. That count is the only spreading mechanism in the sim. Once a villager is `Walking` to a workplace, a later villager's wait term for that place is higher, so it may pick another workplace or the same one if still cheapest.

**Places and queues**: `StartWork` (`TownJobs.cs:64-76`) takes a free place and the first free work slot (`SlotMask`, up to 30). `FinishWork` (`TownJobs.cs:91-115`) pulls the head of the queue into every free place (line 98, `PopQueue` at 78-84), then routes the carried good on.

## 2. What a villager is in the data

**`VillagerState`** (`sim/Town/TownState.cs:17-46`):

| Field | Line | Meaning |
|---|---|---|
| `Id` | 19 | index in `S.Villagers` |
| `Home` | 20 | cottage plot id |
| `Phase` | 21 | `VillagerPhase` |
| `Job` | 22 | `JobKind` |
| `Stage` | 23 | 0 to first workplace, 1 to second (flour), 2 to delivery |
| `Workplace` | 24 | workplace index aimed at, queued at or working at (-1) |
| `Node` | 25 | node at, or node the road leg started from |
| `Target` | 26 | node aimed at (-1) |
| `Road` | 27 | road walked now (-1) |
| `Dir` | 28 | +1 from A to B, -1 B to A |
| `Dist` | 29 | distance along the road from A |
| `Path` | 30 | `int[48]` roads still to walk |
| `PathLen, PathPos` | 31 | path length and position |
| `ArrivedRoad` | 32 | road came in on (queue line) |
| `Carry` | 33 | `Good` |
| `Amount` | 34 | units carried |
| `WorkLeft` | 35 | seconds of work left |
| `Slot` | 36 | work spot taken (-1 none) |
| `X, Y, PrevX, PrevY, Heading` | 37 | drawn position (cells) and heading |
| `RMode` | 39 | 1 = walking the river plan |
| `RSteps` | 40 | `int[12]` river itinerary |
| `RLen, RPos` | 41 | itinerary length and position |
| `RPoint, RTarget, RLink, RDir, RWait, RSite` | 42 | river point, target, link, direction, wait and site (-1 each by default; `RDir` 1) |
| `RDist` | 43 | river distance |
| `RHome` | 44 | river cottage plot when the villager lives by the river (-1) |
| `RStuckSince` | 45 | time waiting at a crossing (-1) |

**`WorkplaceState`** (`TownState.cs:49-62`): `Plot`, `Places`, `Open`, `Busy`, `Queue` (`int[64]`), `QueueLen`, `SlotMask` (bit i = spot i), `Hands` (extra places bought), `QueueSince` (time the queue reached `HandQueueLength`, -1), `HandOffered`.

**`JobKind`** (`TownState.cs:7-9`): `None=0, Water=1, GrainHome=2, GrainMarket=3, Flour=4, GrainBoat=5, FlourBoat=6, WoolBoat=7, WoolMarket=8, ClothBoat=9, ClothMarket=10`. Second-age jobs are `>= GrainBoat` (`TownRiverJobs.cs:30`).

**`VillagerPhase`** (`TownState.cs:12`): `Idle=0, Walking=1, Queued=2, Working=3`.

**`Good`** (`TownState.cs:5`): `None=0, Water=1, Grain=2, Flour=3, Wool=4, Cloth=5`.

**Save**:
- `TownSave.CurrentVersion = 3`, `FirstAgeVersion = 2` (`sim/Town/TownSave.cs:11-12`).
- `Save` is `JsonSerializer.Serialize(town.S, Options)` with `IncludeFields = true` (`TownSave.cs:14-16`). Whole `TownState`, including `Villagers` and `Workplaces`.
- `Load` accepts 2 or 3 (`TownSave.cs:25`). A version 2 save with `River` is refused (line 26). It rebuilds the plan from the seed and checks road and plot counts (lines 27-29).
- `TownState.Version` defaults to `FirstAgeVersion` and is set to 3 when the second age is rung in (`TownState.cs:67`, `sim/Town/TownRiver.cs:30`).
- Old saves get defaults for missing fields (comments in `TownState.cs:107-145`).
- The game side writes and reads through `game/scripts/town/TownSaver.cs` (not read in full). Save round-trip test: `game/tests/SaveTest.cs:138-139`.

Villagers are created in `Town.AddVillager` (`sim/Town/Town.cs:~209-225`). The ID is `S.Villagers.Length`. The array is re-allocated and grown by one.

## 3. What the game can ask the simulation about villagers

Source: `sim/Town/TownViews.cs` and `docs/plans/town-sim-api.md`.

| View | Where | Fields |
|---|---|---|
| `VillagerCount` | `TownViews.cs:97` | count |
| `Villager(i)` | `TownViews.cs:99-109` | `VillagerView`: `Id, Home, Phase, Job, Carry, Amount, X, Y, PrevX, PrevY, Heading, Road` (-1 unless walking), `Dist, Workplace, QueuePosition` (-1 unless queued; `QueuePosition` at 233-239) |
| `WorkplaceCount` / `Workplace(i)` | `TownViews.cs:139-145` | `WorkplaceView`: `Index, Plot, Kind, Open, Places, Busy, QueueLength` |
| `QueuedVillager(w, pos)` | `TownViews.cs:148` | villager id at a queue place |
| `QueueSpot(w, pos, out x, out y, out heading)` | `TownViews.cs:151-155` | drawn queue spot |
| `RoadInfo(r)` / `RoadView` | `TownViews.cs:23-35, 113-122` | `Walkers`, `Comfort`, `Crowding`, `SpeedFactor`, `Footsteps` |
| `BusiestRoad` | `TownViews.cs:125-137` | road id |
| `VillagerPrice`, `HousingRoom` | `sim/Town/TownShop.cs:39, 42` | price and room |
| `Trips`, `Purchases` | `TownViews.cs:57-58` | counts |

`docs/plans/town-sim-api.md`:
- Line 50: `VillagerCount`, `Villager(i)` and `VillagerView`.
- Line 55-56: `WorkplaceCount`, `Workplace(i)`, `QueuedVillager`, `QueueSpot`.
- Line 61: "Villagers only stand on roads ... working or idle at the door node". This is stale: line 159 says working villagers now stand at their work spot.
- Line 96 lists only Water, Grain and Flour for jobs. It does not mention the river jobs, so it is stale.
- Line 159: `town.WorkSpot(plot, slot, out x, out y, out heading)`.

No view exposes `VillagerState` directly, and no view exposes `Job` choice reasons or scores.

## 4. How villagers are drawn and kept apart

`game/scripts/town/TownVillagers.cs`:
- MultiMeshes: `_bodies, _heads, _shadows, _frames, _carts` (line 25), `_loads` for six goods (line 38), `_headKinds` for three head kinds (line 26). Capacity-sized `Transform3D` instances (lines 83-98, 136-139).
- `Spread(Town t, int n, float dt)` (lines 269-316) is the only separation code. It is drawing-only; the comment at 266 says "drawing only; the simulation is untouched".
  - Pairs closer than `r = Personal * WalkerScale / Cell` are pushed apart at 0.55 of the overlap (lines 288-299). `Personal = 1.0f` metres (line 45).
  - Offsets are capped at `cap = 0.6` cell (line 271, 297).
  - Walking villagers get a lane by id: `lane = ((i*7)%5 - 2) * 0.12` across the heading (lines 279-282).
  - Offsets are eased: `k = min(1, dt*8)` (lines 298-300).
  - Queued villagers are moved out of building footprints by `OutOfBuildings` (lines 302, 322-337). Plots with `Kind == Field` are skipped; deep points (`m > 1.5`) are left alone.
  - Queued and working villagers' minimum gap is recorded as `MinStandingGap` (lines 304-313). Working villagers are not pushed apart.
- The sim's `Place` puts villagers at queue spots and work spots (`TownWalk.cs:181-200`). The game draws those positions; `Spread` only adds offsets on top.

**Collision or physics**: none. Searches of `game/scripts` and `game/tests` for `Area3D`, `CollisionShape`, `PhysicsBody`, `RayCast`, `IntersectRay` returned no matches. Villagers have no hitbox.

## 5. What can be pointed at or clicked today

- Pave mode: `game/scripts/town/TownHud.Pave.cs`. `PaveTarget(Vector2 screen)` (line 142) returns -1 over interface cards (`OverInterface`) or when `_main.Rig.PickLocal(screen, out local)` fails (line 144). Hover and click use this one function (comment lines 10-11). `LocalToScreen` is used to draw the target (lines 122, 179, 187).
- Extend mode: `game/scripts/town/TownHud.Extend.cs`. `ExtendTarget(Vector2 screen)` (line 67) uses `_main.Rig.PickLocal(screen, out var local)` (line 72), then tests against plot footprints using `LocalToScreen` (lines 83-85). Hover at line 104 and 123.
- Map projection: `CameraRig.PickLocal` / `LocalToScreen` (AGENTS.md "The view"). The `CameraRig` source was not opened, so the ray and plane math is not verified here.
- Villagers: no picking exists. The only villager screen positions used in the HUD are `CoversVillager` in `TownHud.Extend.cs:184-201`, which keeps a label off villagers. `TownHud.cs:282` ignores `VillagerArrived` events.

## 6. Where villagers show in the interface

| Place | File:line |
|---|---|
| Shop group People (`Villager`, `Pace`, `Load`) | `game/scripts/town/TownHud.Shop.cs:46` |
| Shop order (`Villager` = 10) | `TownHud.Shop.cs:60` |
| Shop key `tt_buy_1` for Villager | `TownHud.Shop.cs:70`; `FirstKey` line 98 |
| Shop icon `up-villager`, `ui-reputation` | `TownHud.Shop.cs:115, 155` |
| Shop track for Villager | `TownHud.Shop.cs:279` |
| Shop name "Villager" and the text "More villagers" | `game/scripts/town/TownHud.ShopText.cs:62, 123` |
| Shop tooltip: "One more villager. Each cottage houses two, more when extended." | `TownHud.ShopText.cs:143` |
| Book and summary: "and N villagers" | `game/scripts/town/TownHud.Book.cs:260`; `TownHud.River.cs:259` |
| Summary page row "Villagers" = `VillagerCount` | `game/scripts/town/TownSummary.cs:32, 191` |
| Achievements `villagers_5` "Hamlet", `villagers_10` "Village" | `sim/Town/TownAchievements.cs:37-38` |
| Ledger (`TownHud.Ledger.cs`) | no villager reference found |
| Hints: queue hint ("A queue at the well...") | `sim/Town/TownRequests.cs:185` (`HandHint`); API doc line 155 |

## 7. Tests that pin today's behaviour

Test files that mention `Villager`, `Job` or `Workplace` (search of `sim.tests/`, `game/tests/`, `tools/`):
- `sim.tests/Town/`: `TownPlanTests.cs`, `TownRulesV3Tests.cs`, `TownRiverPacingTests.cs`, `TownGreenTests.cs`, `TownRulesTests.cs`, `TownRulesV2Tests.cs`, `TownRiverTests.cs`.
- `game/tests/`: about 22 files, including `TownTest.cs`, `TownTest.V3.cs`, `PlayThrough.cs`, `PlayThrough.V3.cs`, `SaveTest.cs`, `Perf.cs`, `Perf.River.cs`, `RiverTest.cs`, `Tour.Age1.cs`, `Tour.Roads.cs`, `FeelStrips.cs`, `BookTest.cs`, `MenuTest.cs`, `Smoke.cs`, `Screenshot*.cs`.
- `tools/TideToll.Tools/TownCommand.cs` (the headless town tool).

Specific assertions:
- `sim.tests/Town/TownRulesTests.cs:183`: a villager at its workplace plot (`Workplace(v.Workplace).Plot`) or home plot, by phase.
- `sim.tests/Town/TownRulesV2Tests.cs:102-104`: `JobValue(JobKind.Water)` scales with `ValueStep`.
- `sim.tests/Town/TownRulesV2Tests.cs:258`: the test chooses a job per good.
- `sim.tests/Town/TownRulesV2Tests.cs:279-317`: workers stand at different spots; a queue exists only when places are full.
- `sim.tests/Town/TownRulesV2Tests.cs:329`: the well workplace.
- `sim.tests/InvariantTests.cs:21`: unique IDs (HashSet).
- `game/tests/PlayThrough.cs:85` writes `charter=... buys=...`. `tools/check.sh:133` greps `PLAY PASS charter=[0-9.]*min buys=[0-9]*`. `docs/plans/n4/shop-progress.md:104, 190` and `docs/plans/n5/work-build-sheet.md:7` record `charter=33.9min buys=201` as the pinned result.
- `game/tests/PlayThrough.cs:116-117`: the bot's cost weights (Villager = 2).

Bots:
- `sim/Town/TownBots.cs` (123 lines): bot kinds, used by `TownPacing.Run` (`sim/Town/TownPacing.cs:65`). `ByKind` has 22 slots (line 31).
- `tools/TideToll.Tools/TownCommand.cs:148-254`: prints queues, workplaces, villager counts.

Hashing:
- The sim has no whole-town state hash found. Searches for `Hash` and `hash` in `sim.tests/Town` found only the river/toy FNV checks: `sim.tests/RiverPreviewTests.cs:25-28` (FNV of wear and cost), `sim.tests/ToyParityTests.cs:36-38`.
- `RiverOffBaselineTests` (named in AGENTS.md) was not found in source. Only build output (`sim.tests/obj/...`) matches the name.

## 8. Upgrades that touch villagers

| Upgrade | Where it is applied |
|---|---|
| Pace | `sim/Town/TownWalk.cs:45` `PaceFactor` (times `StrongerBacks` boon); `PaceFactorAt` line 47. Road speed `TownWalk.cs:56`; river walk `TownRiverJobs.cs:50, 437`. Buy: `TownShop.cs` `BuyPace`. |
| Load | `sim/Town/Town.cs:229` `LoadUnits = BaseLoad + LoadLevel` (`BaseLoad = 2`, `TownRules.cs:46`). Used at `TownJobs.cs:99` (amount picked up), `:192` (`load` in ChooseJob), `TownRiverJobs.cs:107, 315`, `TownRequests.cs:57-58`. Buy: `TownShop.cs:154` `BuyLoad`. |
| Tools | `TownJobs.cs:87` `ToolsFactor`; `ToolsFactorAt` line 89. Applied to work time at `TownJobs.cs:21` (`WorkLeft` decrement), `:181`, `:205-212`; river at `TownRiverJobs.cs:118, 138, 143, 150, 154, 310`. Buy: `TownShop.cs` `BuyTools` (case at line 126). |
| Extra hands | `sim/Town/TownDrive.cs:79-128`. `Hands` and `HandOffered` on `WorkplaceState`. `StepHands` (97-110) watches the queue (`HandQueueLength`, `HandQueueSeconds`). `BuyHand` (122-128) does `w.Hands++`. Places: `sim/Town/Town.cs:173-174` (`Places = base + Hands`). Hands count into `Places`, so `ExpectedWait` and `FinishWork` see them. The "Faster hands" boon (`TownRules.cs:145`, `BoonHands`) was not traced. |
| Regulars | `sim/Town/TownRegulars.cs:21` `RegularsFactor`; `CoinBonus` in `sim/Town/Town.cs` (`1 + VillagerPerkCoin*(VillagersBought/PerkEvery) + RegularsStep*Regulars`). Buy at `TownRegulars.cs:43-50`. |
| The bell | `sim/Town/TownWalk.cs:52` `BellRinging` (`Time < S.BellUntil`). Work speed `TownJobs.cs:21`, `TownRiverJobs.cs:310`. Walk speed `TownWalk.cs:56`, `TownRiverJobs.cs:50`. `RingBell` sets `S.BellUntil` at `TownShop.cs:225`. |
| Villager (more people) | `TownShop.cs:135-141` `BuyVillager` -> `Town.cs` `AddVillager` (~line 209): new `VillagerState` in the cottage with most free room. `VillagersBought` drives `CoinBonus`. |
| Value (per good) | `TownJobs.cs:152` `ValueFactor(g)`; used in `JobValue` (line 142). `ValueFactorAt` line 154. |
| Requests and drive | `JobValue` (`TownJobs.cs:142`) multiplies by `RequestBonus` and `DriveBonus`. Request also in `TownRequests.cs:57-59`. |
| Extend | `CottageRoom` (`Town.cs`, 2 plus extensions) sets how many villagers a cottage houses, used by `AddVillager`. Not otherwise traced. |

## Not found or not read

- Not found: Area3D, CollisionShape, PhysicsBody, RayCast, IntersectRay in the game (none in `game/scripts` or `game/tests`).
- Not found: `RiverOffBaselineTests` in source.
- Not found: a whole-town state hash test.
- Not read: `EnterRoad` body (`TownWalk.cs:121`), `CameraRig` (its `PickLocal`/`LocalToScreen`), `TownSaver.cs` in full, `TownBots.cs` internals, the "Faster hands" boon's application.

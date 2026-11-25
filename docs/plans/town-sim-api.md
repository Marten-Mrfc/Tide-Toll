# Town simulation API (for the game builder)

Namespace `TideToll.Sim.Town`, project `sim/` (already referenced by `game/`). Engine-free and deterministic. All numbers live in `sim/Town/TownRules.cs`. The class `Town` shares its name with the namespace's last part: from `namespace TideToll.Game` with `using TideToll.Sim.Town;` plain `Town` works; from inside `TideToll.Sim.*` write `using T = TideToll.Sim.Town.Town;`.

## Make, step, save

```csharp
using TideToll.Sim.Town;
var town = new Town(seed);                 // plans the town (about 3 ms) and starts level 1: one cottage, the well, one road, one villager
town.Step();                               // one fixed step of TownRules.Dt = 0.1 game seconds (TownRules.TickHz = 10 per game second)
town.DrainEvents(list);                    // List<TownEvent>, cleared then filled with everything since the last drain
string json = TownSave.Save(town);         // whole state, System.Text.Json, field "Version" = TownSave.CurrentVersion (2 since rules v2)
Town t2 = TownSave.Load(json);             // plan rebuilt from the saved seed; continues exactly as the original would
Town t3 = TownSave.Load(json, town.Plan);  // same, reusing a plan already built for that seed
```

- Run `Step()` 10 times per game second from a fixed-step accumulator; interpolate drawing with `PrevX/PrevY` and `X/Y`.
- Load throws `InvalidOperationException` on another version or a save that does not match its seed's plan.
- Events not yet drained are not saved. The constructor queues `LevelReached` (level 1) and `VillagerArrived` (villager 0).
- Time: `town.Time` (game seconds, = `Tick * 0.1`), `town.Tick`.

## Coordinates

The town uses the existing 128 x 128 map from `MapGen.Generate(seed)` (`town.Plan.Map`, the same `MapData` the game already draws: river, ground noise). The map's own buildings, crops and footprints from MapGen are **not** part of the town; draw `Plan.Plots` instead.

- A grid point `(x, y)` is a map cell: column x, row y, y grows south, cell `i = y * 128 + x`.
- Every position the town gives (villagers, queue spots, deliveries) is in **map cell units with cell centres at +0.5**: road point `(Xs[k], Ys[k])` is drawn at `(Xs[k] + 0.5, Ys[k] + 0.5)`. Multiply by the game's cell size in world units to place it.
- Headings are radians, `atan2(dy, dx)` in these units (0 = east, +pi/2 = south).
- The whole first-age town lies in `Plan.WindowX, WindowY, WindowW (33), WindowH (36)` on the west bank, at least 6 cells from the water; the riverside is free for the second age.

## The plan (`town.Plan`, read only, never changes)

- `Plot[] Plots`: `Id, Kind (Cottage, Well, Field, Market, Mill, BellGreen), X, Y, W, H` (footprint cells), `DoorX, DoorY` (door cell just outside the footprint, in the middle of the side facing the well, where its roads start), `Node`, `Level` (level whose gift lays it out), `Offer` (-1, or the offer that must be bought before it works).
- `Road[] Roads`: stable id = index. `A, B` (node ids), `Xs, Ys` (polyline of grid points, first = node A, last = node B), `Cum` (distance at each point), `Length` (cells), `Level`. `road.At(dist, out x, out y, out heading)` gives the drawn point at a distance from A. `TownPlanner.RoadCells(road, 128)` lists the 4-connected cells under a road (for painting the ground's wear layer).
- `RoadNode[] Nodes`: `X, Y, Roads`. Every node is a plot door.
- `LevelDef[] Levels` (`Plan.Level(n)`, n from 1): `Number, Renown` (needed), `Gift` (words), `Plots, Roads, Offers` (ids it gives), `Charter, UnlocksBell, UnlocksCobbles`.
- `Offer[] Offers`: `Id, Kind (SecondBucket, MarketStall, SecondField, Mill), Name, Price, Level, Plot`.
- Roads never cross water, plots or each other; later roads close loops (tested on seeds 1 to 100).
- Levels (12 in rules v1; **16 since rules v2**, see "Added in rules v2"): 1 start; 2 cottage, lane, bell green (the bell); 3 first field; 4 cottage, lane, second bucket offer; 5 market square, stall offer; 6 loop; 7 two cottages; 8 second field offer, two tracks, cobbles; 9 cottage, two lanes; 10 mill offer and road; 11 cottage, two lanes; 12 charter and a last lane.

## Views (read each frame)

| Member | Meaning |
| --- | --- |
| `Coin`, `CoinPerMinute` (rolling last 60 game s), `CoinEarned` | money |
| `Renown`, `Level`, `MaxLevel`, `LevelBar` (0..1), `NextLevelRenown`, `NextGift` ("Next: ...") | level bar |
| `HasCharter`, `CanRingNextAge`, `NextAgeRung` | end of the age |
| `ShopItems(List<ShopItem>)` | every purchasable thing: `Kind (Villager, Pace, Load, Pave, Building), Index, Name, Level, Price, Affordable, Reason` ("" or e.g. "Not enough coin", "No room: wait for a cottage", "No worn-in road to pave"), `Renown` it adds. Fixed order: Villager, Pace, Load (while any left), Pave (the suggested road), then open building offers |
| `BellReady`, `BellWait` (s), `BellRinging`, `BellRest` | the bell button (unlocked at level 2) |
| `VillagerCount`, `Villager(i)` | `VillagerView`: `Id, Home, Phase (Idle, Walking, Queued, Working), Job, Carry (None, Water, Grain, Flour), Amount, X, Y, PrevX, PrevY, Heading, Road, Dist, Workplace, QueuePosition` |
| `Housing`, `HousingRoom` | villager room (2 per given cottage) |
| `RoadCount`, `RoadOpen(r)`, `RoadInfo(r)` | `RoadView`: `Open, Wear (0..1), WornIn, Surface (Track, Gravel, Cobbles), Walkers, Comfort, Crowding (walkers / comfort; above 1 slows), SpeedFactor, Footsteps` |
| `BusiestRoad` | open road with most walkers now (-1 none) |
| `SuggestedPave()`, `CanPave(r, out reason)`, `PavePrice(r)`, `NextSurface(r)` | for the pave click |
| `WorkplaceCount`, `Workplace(i)` | `WorkplaceView`: `Plot, Kind, Open, Places, Busy, QueueLength` |
| `QueuedVillager(w, pos)`, `QueueSpot(w, pos, out x, out y, out heading)` | the visible queue: place 0 is next to the door, then every 0.8 cells back along the road the person came in on |
| `PlotGiven(p)`, `PlotWorking(p)`, `OfferAvailable(o)`, `OfferBought(o)` | what to draw on each plot (given but not bought: draw the empty plot) |
| `AchievementCount`, `Achievement(i)` | `AchievementView`: `Id, Name, Description, Progress (0..1), Value, Target, Unlocked` (24) |
| `Trips`, `Purchases`, `Delivered(good)`, `PaceLevel`, `LoadLevel`, `RoadsPaved`, `BellRings` | counts |

Villagers only stand on roads: walking on `Road` at `Dist`; queued at a queue spot; working or idle at the door node of their workplace or home (draw them at the door or fade them into the plot).

## Player actions (each returns true if it happened)

```csharp
town.Buy(item);            // any ShopItem from ShopItems (the Pave item paves its Index road)
town.BuyVillager(); town.BuyPace(); town.BuyLoad();
town.Pave(roadId);         // the click on a worn-in road: gravel, then cobbles from level 8
town.BuyOffer(offerId);    // second bucket, market stall, second field, mill
town.RingBell();           // everyone walks and works 1.5x faster for 6 s; then it rests 30 s
town.RingNextAge();        // after the charter: the second-age hook (not built: emits NextAge, the town keeps running)
```

## Events (`TownEvent`, `Kind` then the fields that matter)

| Kind | Fields |
| --- | --- |
| `Delivery` | `Villager, Good, Amount, Coin, X, Y` (door where delivered), `Plot` |
| `Purchase` | `Item, Index` (road for Pave, offer for Building), `Coin` (price), `Value` (renown added), `Level` (new track level) |
| `LevelReached` | `Level, Text` (the gift in words); what it added: `Plan.Level(Level).Plots / Roads / Offers` |
| `RoadWornIn` | `Index` (road) |
| `RoadPaved` | `Index` (road), `Surface` |
| `OfferAvailable` | `Item` (Building, Bell or Pave for cobbles), `Index` (offer or -1), `Text` |
| `BuildingOpened` | `Index` (offer), `Plot`, `Text` |
| `PerkEarned` | `Item` (Villager, Pace or Pave track), `Level`, `Text` (perk name) |
| `AchievementUnlocked` | `Index`, `Text` |
| `BellRung` | `Value` (seconds of hurry) |
| `VillagerArrived` | `Villager, Plot` (home), `X, Y` |
| `Charter` | the last level is reached |
| `NextAge` | RingNextAge was called |

Every event has `Time` (game seconds).

## Rules in one paragraph

A villager takes the job paying most coin per second (travel by current road speeds, expected wait, work): Water (well, home), Grain (field, home; or field, market once the stall is bought), Flour (field, mill, market once the mill is bought). After a delivery the villager picks the next job from where it stands. Coin = load x value x bonus on delivery (load 2 units, +1 per Load level; values per unit: Water 2, Grain 3 at home or 4 at the market, Flour 10). Road speed = 2.5 cells/s x pace x bell x surface (a new trail 0.7, worn-in track 1, gravel 1.25, cobbles 1.5) x crowding (1 up to the road's comfort, then smoothly down to 0.35). Pace is a long track with shrinking steps (factor 1 + 1.5 x (1 - 0.98^level)). Wear rises with footsteps; six full walks wear a road in. Prices rise geometrically and never fall; renown = price; levels apply their gifts by themselves. Perks every fifth Villager (Good neighbours, +5 % coin), Pace (Light feet, bell rests 10 % shorter) and Pave (Mason's eye, paved roads carry 20 % more). Every number is in `sim/Town/TownRules.cs` (static fields, so the balancer can try values; nothing writes them while a town runs).

## Tools

`dotnet run --project tools/TideToll.Tools -c Release -- town --seed 1` (pacing tables of the three bots; `docs/plans/town-pacing.md`), `-- town --plan --seed 1` (the plan as text), `-- town --detail --seed 1` (roads, queues and CPU time at the charter).

## Cost

Building a plan takes about 3 ms. One game minute (600 steps) at the end of the age with 14 villagers costs 2 to 5 ms of CPU; `Step()` allocates nothing while events are drained regularly (tested in `OneGameMinuteAtTheEndOfTheAgeIsCheap`); the views that return strings (`NextGift`, `ShopItems`) are fine once a frame. The bots (`TownBot`) are for tests and the tool; the game does not need them.

## Added in rules v2

Everything above still works as written, except where this section says a number changed. Nothing was renamed or removed. Save version is now **2**; a version 1 save throws `InvalidOperationException` from `TownSave.Load` as any other version does.

### Numbers that changed

- 16 levels instead of 12 (`MaxLevel` = 16). Level 1 is still one cottage, the well, one road and one villager. Gifts: 1 start; 2 cottage, lane, bell green (the bell); 3 first field and track; 4 cottage, lane, second bucket offer; 5 market and stall offer; 6 loop; 7 cottage and lane; 8 cottage and lane; 9 second field offer, its track, cobbles; 10 track from the second field to the market; 11 cottage and lane; 12 lane joining the south cottages; 13 mill offer and road; 14 cottage and lane; 15 lane closing the east loop; 16 charter and a last lane. Plot and road ids and geometry are the same as before (only their `Level` changed). Offers: bucket level 4, stall 5, second field 9, mill 13.
- Pace has 20 levels of +5 % each (`TownRules.PaceStep`, `PaceMax` = 20). At 20 the shop still lists Pace with `Reason` "Fastest pace reached", `Price` 0, not affordable.
- Load steps unlock at levels 3 (Basket), 7 (Yoke), 11 (Handcart); `TownRules.LoadLevels`. A locked step is listed with `Reason` "Unlocks at level 7"; `BuyLoad()` returns false while locked. `town.LoadLockReason` gives the same text ("" when it can be bought).
- Unpaved roads carry fewer walkers (`ComfortPerCellTrack` 0.1, `MinComfort` 1.5), so a town of 8 to 10 villagers crowds its busiest unpaved road.
- `BellRung.Value` is now `town.BellDuration` (6 s plus the bell track).
- Renown of a purchase rounds half up (2.5 gives 3); before it rounded half to even.
- `OfferAvailable` events are also sent for `Item = Load` (Index = step 0..2, Text = its name, when its level is reached), `Item = Tools` (level 3) and `Item = Extend` (level 5).

### New shop item kinds (appended to `ShopItemKind`; old values unchanged)

`Tools = 7, Value = 8, Extend = 9, BellTrack = 10`. `ShopItems(list)` adds them after the building offers, in this order: Tools (always listed; locked before level 3 with "Unlocks at level 3"); one Value item per good that is shown (Water always; Grain once the first field works; Flour once the mill works) with `Index = (int)Good` and `Name` "Water price" / "Grain price" / "Flour price"; Extend (`Index` = suggested cottage plot, or -1 with a reason); BellTrack "Louder bell" (from level 2). `Buy(item)` buys any of them.

| Track | Levels | Effect | Methods and views |
| --- | --- | --- | --- |
| Tools | 10 | work at the well, field and mill is 5 % faster per level (work time / (1 + 0.05 x level)) | `BuyTools()`, `ToolsLevel`, `ToolsPrice`, `ToolsLockReason` ("Unlocks at level 3", "Best tools reached") |
| Value (per good) | 8 each | that good pays 6 % more per unit per level; villagers take the best-paying job, so it shifts what the town does | `BuyValue(Good)`, `ValueLevel(Good)`, `ValuePrice(Good)`, `ValueFactor(Good)`, `ValueTrackShown(Good)`, `ValueLockReason(Good)` ("Comes with the first field", "Comes with the mill", "Best price reached") |
| Extend a cottage | 2 per cottage | the cottage houses one more villager | see below |
| Bell track | 5 | each level: hurry +0.6 s, rest -2 s | `BuyBellTrack()`, `BellTrackLevel`, `BellTrackPrice`, `BellTrackLockReason` ("Comes with the bell", "The bell is at its best"), `BellDuration` |

Every purchase still emits `Purchase` (`Item`, `Index` = good for Value, cottage plot for Extend, else -1; `Level` = new track level; for Extend the total extensions bought) and adds renown = price.

### Extending cottages (a per-cottage click, like paving)

- `CanExtend(plot, out reason)`: false with "Not a cottage", "No such cottage yet", "Unlocks at level 5" or "Already extended twice".
- `ExtendPrice` (one price for any cottage; rises with every extension bought), `Extensions(plot)` (0, 1 or 2), `ExtensionsBought`.
- `SuggestedExtend()`: a full cottage with fewest extensions (lowest id on a tie), else any that can be extended, else -1. The shop's Extend item carries it as `Index`.
- `Extend(plot)`: returns true if it happened. Event `CottageExtended`: `Plot`, `Level` (its extensions now), `X, Y` (door, map cells).
- `CottageRoom(plot)` (2 + extensions; 0 for anything else) and `Residents(plot)`. `Housing` counts extensions. A new villager moves into the cottage with most free room.

### Requests

From level 3, someone asks for an amount of one good within a time; one request at a time. Deterministic from the seed and the request number, saved with the town.

- `town.Request` gives a `RequestView`: `Active`, `Who` (index into `Town.RequestWho`: 0 "The innkeeper" (Water), 1 "The miller" (Grain), 2 "The baker" (Flour)), `WhoName`, `Good`, `Amount`, `Delivered`, `SecondsLeft`, `Seconds` (allowed in all), `RewardCoin`, `RewardRenown`, `NextIn` (seconds to the next one when none runs, -1 if unknown).
- `town.RequestText`: "The miller asks for 12 Grain within 90 seconds" ("" when none runs). Build card text from it or from the view.
- While a request runs, its good pays `TownRules.RequestBonus` (1.5x), so villagers shift to it by themselves. Any delivery of that good counts (Grain home or to the market).
- Met in time: `RewardCoin` is added to `Coin` (not to `CoinEarned`; summed in `RequestCoin`) and `RewardRenown` to renown; event `RequestMet`. The next comes 70 s later.
- Missed: no penalty; event `RequestMissed`; the next comes 45 s later.
- Counts: `RequestsAsked`, `RequestsMet`, `RequestsMissed`. No requests after the charter.
- Events (`Index` = who, `Good` = the good): `RequestAsked` (`Amount` asked, `Coin` = reward, `Value` = seconds allowed), `RequestMet` (`Amount`, `Coin` paid, `Value` renown added), `RequestMissed` (`Amount` asked, `Level` = amount delivered). Their `Text` is null (Step allocates nothing); use `RequestText` or `Town.RequestWho`.

### Hints

`town.Hint`: one read-only line of advice, or "" when there is nothing to say. Read it once a frame; after reading, `HintRoad` (road to highlight, -1) and `HintPlot` (plot to highlight, -1) say what it is about. In order of priority: a Load step unlocked in the last 90 s ("The Yoke is ready. Every trip carries one more."); housing full with a cottage that can be extended ("Every cottage is full. Extend one for another villager.", HintPlot = the suggested cottage); a queue of 2 or more at a workplace that a building offer or Tools would fix ("A queue at the well. A second bucket would help.", HintPlot = the workplace); a crowded road that can be paved ("A road is crowded. Pave it so more can walk it.", HintRoad).

### Where villagers stand

Walking and queued villagers are as before. A working villager now stands at its own work spot in or beside the workplace (well: on the rim; field: along the rows; mill: inside beside the door), different for each worker at the same place; an idle villager at home stands at the door or in the yard. `X, Y, PrevX, PrevY, Heading` stay the fields to draw (map cells; the jump between a road and a spot happens in one step, so interpolation shows a short slide). `town.WorkSpot(plot, slot, out x, out y, out heading)` gives a spot if the game wants to place props.

### Other new members

- `EarnedBy(Good)`: coin earned by deliveries of each good. `RequestCoin`: coin paid as request rewards.
- `BotKind.Saver = 3` (the saving bot), `TownBot.NoBell` / `NoPave` (test switches). The game does not need the bots.
- `TownTargets` holds the v2 targets (`MaxTrackShare`, `EveryKind`, `MaxMapGap`, `BellMin/BellMax`, `NoPaveSlower`, `RequestsMetMin/Max`, `SeedFrom/SeedTo`).
- Tool: `town --seed 1` now prints four bots with purchases by kind, map gaps and requests; `town --extras --seed 1` prints bell worth, never-paving slowdown and requests met; `town --shopat 25 --seed 1` prints the shop at a minute.

### Rules v2.1 (2026-11-25, `docs/plans/town-rules21-progress.md`)

Still compatible: nothing renamed or removed, save version still 2 (a v2 save without the new field loads with `ReqScale` 1).

- **Steps unlock with town levels.** Pace, Tools, the value tracks, the bell track and cottage extensions put only some steps on sale at each town level (tables `TownRules.PaceAt`, `ToolsAt`, `ValueAt`, `FlourValueAt`, `BellTrackAt`, `ExtendAt`; index 0 = level 1, value = steps that can have been bought by then). Every level from 2 to 15 adds steps; the last ones come at level 15. A step not yet on sale is listed in `ShopItems` with `Reason` "Unlocks at level N" (not affordable) and its `Buy...` returns false. New views: `PaceLockReason`, `ExtendLockReason`; `ToolsLockReason`, `ValueLockReason(g)`, `BellTrackLockReason` and `CanExtend(plot, out reason)` can now also say "Unlocks at level N". When extensions are locked by level, the shop's Extend item has `Index` -1 and that reason.
- **Numbers that changed:** Pace 25 steps of +4 % (was 20 of 5 %; "Fastest pace reached" after 25), growth 1.36; Tools 18 steps of 3 % (base 30, growth 1.33), Value 14 steps of 4 % per good (growth 1.55 / 1.43 / 1.17), Villager growth 1.3, Extend growth 1.33, `BaseSpeed` 1.6, `LevelRenown` up to 80000 for the charter.
- **Requests adapt to the player.** Each request met multiplies the next amount by `TownRules.RequestGrowMet` (1.10), each missed by `RequestShrinkMissed` (0.84), within `RequestScaleMin..Max` (0.3..2.5); so any player settles near 65 % met. `town.RequestScale` reads it (1 at the start); it is saved (`TownState.ReqScale`) and deterministic.
- **Roads are smooth curves.** `Road.Px, Py` (double, same units as `Xs, Ys`: draw at +0.5; first point = node A, last = node B exactly) and `Road.PCum` (distance at each curve point). The router's grid line is smoothed by corner cutting; every move is checked against water, plots and earlier roads. `Length` is the curve's length; `At(dist, ...)` follows the curve (binary search, no allocation); villagers walk the curve. `Xs, Ys` still hold the grid route (one entry per bend) and `Cum` still has one entry per grid point, rescaled so `Cum[^1] = Length`. `TownPlanner.RoadCells` now lists the cells under the curve. Games that draw roads should draw `Px, Py`.
- **Targets:** `TownTargets.BellMin/Max` and `RequestsMetMin/Max` are now measured on the average over seeds 1 to 10 per bot, with single-run sanity bands `BellRunMin/Max` (5 to 35 %) and `RequestsRunMin/Max` (30 to 95 %). The lead decided this.
- Tool: `town --seeds 1-10 --misses [--averages]` runs every bot on every seed in parallel and prints every miss with counts (and the bell averages); `--set` also takes int arrays (`PaceAt=4,6,...`).

### Rules v2.2 (2026-11-25, `docs/plans/town-rules22-progress.md`)

No API change for the game. Numbers: bell hurry 1.4 for 8 s (was 1.17 for 6 s), bell track +0.4 s hurry and -1 s rest per step (was 0.6 and 2), Pace +3 % per step (was 4 %), Basket 250 (was 300), extensions from 40 (was 60), request reward 0.4 (was 0.6), levels 15 and 16 need 62000 and 102000 renown (were 59000 and 84000). Requests start easy: the first three are scaled by `TownRules.RequestEaseIn` (0.4, 0.6, 0.8), then the adaptation alone. For tuning: `TownPacing.BellWorth(seed)` now returns one value per minute in `TownTargets.BellAtMinutes` (frozen payback towns), `PacingResult.LongestPurchaseGap`, tool option `town --seed N --bot B --buys A-B`.

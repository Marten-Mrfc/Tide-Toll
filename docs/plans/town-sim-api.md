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

## Added in rules v3

2026-11-25, `docs/plans/town-rules3-progress.md`. Everything above still works as written: nothing renamed or removed, no enum value changed. Save version is still **2**: a v2 save without the new fields loads with no boons, no choice offered yet, no drive and no extra hands.

### Numbers that changed

Goods pay Water 1.8, Grain 2.4 at home or 3.2 at the market, Flour 6.5 (were 2, 3, 4, 10). Villager from 4.5 (was 5), growth 1.32 (was 1.3); Pace from 1.8 (was 2), growth 1.4 (was 1.36); Tools growth 1.38 (was 1.33); Basket 200 (was 250); Water and Grain price steps on sale at levels 7 to 13 one more (`ValueAt`); levels 9 to 16 need 5700, 9500, 15000, 22500, 33000, 48000, 74000, 140000 renown (were 5500 ... 66000, 102000). Pace, Tools and value factors now include milestones (below), so `PaceFactorAt(level)` is no longer a straight line.

### 1. Choices of two (boons)

At levels 4, 7, 10 and 13 (`TownRules.ChoiceLevels`, choice 0 to 3) the town is offered two boons and keeps the one it picks, for good. The town runs on while a choice is open; nothing is lost by waiting. If two are open, the oldest is shown first.

| Choice (level) | Side A (`Boon`) | Side B (`Boon`) |
| --- | --- | --- |
| 0 (4) | `WiderLanes = 0` "Wider lanes": every road carries 50 % more people before it crowds (`BoonLanes`) | `StrongerBacks = 1` "Stronger backs": everyone walks 4.5 % faster (`BoonWalk`) |
| 1 (7) | `DeeperWell = 2` "A deeper well": water is drawn 20 % faster (`BoonWellWork`) | `LongerRows = 3` "Longer field rows": grain is cut 60 % faster (`BoonFieldWork`) |
| 2 (10) | `ThirdRoom = 4` "A third room": every cottage houses one more villager (`BoonRoom`) | `FasterHands = 5` "Faster hands": all work 40 % faster (`BoonHands`) |
| 3 (13) | `MarketRight = 6` "A market right": Grain pays 12 % more (`BoonGrain`) | `MillersGuild = 7` "A miller's guild": Flour pays 20 % more (`BoonFlour`) |

- `town.ChoiceOpen` (bool); `town.Choice` gives a `ChoiceView`: `Open, Choice` (0..3, -1 when none), `Level, A, B` (the two `Boon`s), `AName, AText, BName, BText` (name and one plain line, e.g. "Wider lanes" / "Every road carries 50 % more people before it crowds."), `AWorth, BWorth` (estimated extra coin per minute now, see `BoonWorth`).
- `town.ChooseSide(0 or 1)` or `town.Choose(Boon b)`: true when it was taken; false when no choice is open, the boon is not on the open choice, or it was decided already.
- `town.HasBoon(b)`, `town.BoonsHeld(List<Boon>)` (cleared, then filled in Boon order), `town.ChoicePicked(choice)` (the `(int)Boon` taken, -1 none), static `Town.BoonName(b)`, `Town.BoonText(b)`, `town.BoonWorth(b)`.
- Events: `ChoiceOffered = 17` (`Index` = choice, `Level` = town level); `BoonChosen = 18` (`Index` = `(int)Boon`, `Level` = choice, `Text` = name).
- Bots: payback and saver take the side with the higher `BoonWorth`, cheapest first takes side A, random affordable takes a random side.

### 2. The charter drive

When level 15 (`MaxLevel - 1`) is reached the town is asked for one last great effort. It is not a `Request` (the request card, `Request`, `RequestText` and the request counts are unchanged); no new ordinary request starts while it runs.

- `town.Drive` gives a `DriveView`: `Active, Done, Name` ("The charter drive", also `Town.DriveName`), `Good` (Flour when the mill and market work, else Grain, else Water), `Amount, Delivered, RewardRenown` (renown in all: 65 % of the renown still needed for the charter when it was asked, `TownRules.DriveShare`), `RenownGiven` (so far).
- `town.DriveText`: "The charter drive: send 300 Flour for the charter (120 sent)", or "" when none runs.
- It cannot be missed (no time limit) and is met in parts: every delivery of the good counts and pays its share of the renown at once, so the level bar moves as goods arrive. While it runs the good pays `TownRules.DriveBonus` (1.25x) more, so villagers turn to it by themselves. Amount = the town's coin rate for `DriveMinutes` (1) in units of the good, at most 80 % of what its workplaces could make in that time, at least 10.
- Events: `DriveAsked = 20` (`Good, Amount, Value` = renown in all); `DriveProgress = 21` (`Good, Amount` = delivered so far, `Value` = renown added by this delivery); `DriveMet = 22` (`Good, Amount, Value`).

### 3. Steps that show

- `ShopItem.Worth` (new field): estimated extra coin per minute the item would add now (rounded to 0.1; 0 where it has no direct worth: the Pave item without a road, Extend while there is room, Pace at its end, the bell). Same estimate the payback bot uses, without the bot's own pull towards cheap tracks.
- Milestones: every fifth step (`TownRules.MilestoneEvery`) of Pace, Tools and each value track adds extra on top of the step: Pace +2 % walking speed (`PaceMilestone`), Tools +4 % work speed (`ToolsMilestone`), a value track +4 % price (`ValueMilestone`). New static `Town.ToolsFactorAt(level)`, `Town.ValueFactorAt(level)` (no boons), and `PaceFactorAt` includes them.
- Event `Milestone = 19`: `Item` (Pace, Tools or Value), `Index` (`(int)Good` for Value, else -1), `Level` (the track level), `Text`: Pace "Long strides", "Sure feet", "Quick march", "Light step", "Fleet feet" (5 to 25); Tools "Sharp tools", "Fine tools", "Master tools" (5, 10, 15); a value track "Water: a fair name", "Water: a good name" (5, 10; Grain and Flour likewise). `Town.MilestoneName(kind, index, level)` gives the same text ("" when that level is no milestone). `PerkEarned` is still sent as before.

### 4. Extra hands at queued workplaces

When 2 or more people (`HandQueueLength`) stay queued at an open workplace for 30 s (`HandQueueSeconds`), an offer appears there to add a place: at most 2 per workplace (`HandMax`).

- New shop kind `ShopItemKind.Hand = 11`, listed after the bell track, one item per workplace with an offer: `Index` = workplace index (as in `Workplace(i)`), `Name` e.g. "A third hand at the mill" (the ordinal is the place it would add), `Level` = hands bought there, `Price` = 150 (well), 600 (field), 4000 (mill) x 2.5 per hand bought there. `Buy(item)` or `town.BuyHand(workplace)` (false when none is on offer there).
- `town.HandOffered(w)`, `town.HandsBought(w)`, `town.HandName(w)`, `town.HandPrice(w)`. `WorkplaceView.Places` includes the hands.
- The offer stays until bought. Events: `OfferAvailable` with `Item = Hand`, `Index` = workplace, `Plot` = its plot, `Text` = the name; `Purchase` with `Item = Hand`, `Index` = workplace, `Level` = hands there now.
- `Hint`: a queue of 2 or more at a workplace with a hand on offer now says "A queue at the mill. A third hand at the mill would help." (`HintPlot` = the workplace's plot), before the bucket, field and Tools advice.

### Tools and tuning

`PacingResult.Boons` (boon per choice), `ChoiceWorth`, `DriveAsked`, `DriveMet`, `Milestones`, `LastGap` (minutes from level 15 to the charter), `LongestEarlierGap` (longest of the three level gaps before it). Tool: `town --seeds 1-10 --misses --v3 [--worth]` adds boons picked (A/B per choice), last gap / longest earlier gap and hands bought to each bot's column, and with `--worth` the payback bot's boon estimates when each choice was offered.

## The second age

2026-11-25, `docs/plans/town-age2-progress.md`, design brief `docs/plans/age2-design.md`. Everything above still works as written: nothing renamed or removed, no enum value changed. A town that never calls `RingNextAge()` behaves exactly as before, tick for tick, and still saves as version 2. Numbers live in `sim/Town/TownRulesRiver.cs`.

### Ringing it in

- `town.RingNextAge()` (after the charter, as before) still emits `NextAge`, and now starts the second age in the same town: villagers, coin, renown, upgrades, paving, boons and achievements stay. It at once reaches level 17 (the landing and its road: `LevelReached`, Level 17). `MaxLevel` becomes 28; `Level`, `LevelBar`, `NextLevelRenown` and `NextGift` ("Next: Two cottages by the river", at the end "The borough seal is yours") carry on through levels 17 to 28.
- `town.InRiverAge` (bool), `town.HasSeal` (level 28 reached; event `Seal = 30`, Text "The borough seal"). The bots stop at the seal.
- `town.RiverLevelRenown(n)`: renown needed for level n (1 to 28). Second-age levels need `RenownBase + round(RenownScale x LevelRenown[i])`, where RenownBase is the renown at the ring and RenownScale the town's best coin per minute by then (at least 2000), so towns of any size take about as long.
- Levels: 17 the landing and its road; 18 two cottages by the river; 19 the ford and a lane on the far bank; 20 the pasture (Wool); 21 the weaver (Cloth); 22 the ferry (offer); 23 a second berth (offer); 24 two cottages on the far bank; 25 the footbridge; 26 the crane (offer); 27 the drawbridge; 28 the borough seal. `RiverPlan.Levels[i]` (i = level - 17) lists the river `Plots` and `Links` each level gives.

### Coordinates

Same map and units as the first age (map cells, 128 x 128, y grows south, headings `atan2(dy, dx)`), with one difference: **every river position is already at cell centres** (draw `(x, y)` as given; do not add 0.5). The town's bank is the west (near) bank; the far bank is east of the water. Boats come up the river from the **south edge** (row 127), moor at the landing, and go back down to the south edge. Nothing of the river is north of the footbridge for boats.

### The plan (`town.RiverPlan`, null before the ring; built from the seed, never changes)

- `RiverPlot[] Plots`: `Id, Kind (Landing, Weaver, Pasture, Cottage), X, Y, W, H` (footprint cells), `Point` (door point id), `Level`, `FarBank`. Ids: 0 landing, 1 weaver, 2 and 3 near cottages, 4 pasture, 5 and 6 far cottages.
- Points: `Px[i], Py[i]`, `PointFar[i]`, ids in `RiverPoint`: `Bank` (the first-age door node `BankNode` where the bank road starts), `Landing, Weaver, Pasture, FordNear, FordFar, FerryNear, FerryFar, FootNear, FootFar, DrawNear, DrawFar, FarHub, CottageNear0, CottageNear1, CottageFar0, CottageFar1`.
- `RiverLink[] Links`: `Id, Kind (Lane, Ford, Ferry, Footbridge, Drawbridge), A, B` (point ids; for a crossing A is on the near bank), `Px, Py` (polyline), `Cum`, `Length`, `Level`; `link.At(dist, out x, out y, out heading)`. Lanes are 4-connected paths over free land (never through water or a plot of either age). Crossings are straight lines over the water on their row. `LinkOf(kind)`, `PlotOf(kind)`.
- Rows: `LandingRow, FordRow` (the map's own `FordY` unless it is too close to the others; then the nearest clear row), `FerryRow` (landing - 8), `FootRow` (landing - 14, north of every berth, so boats never meet it), `DrawRow` (landing + 11, between the south edge and the landing: every boat passes it twice).
- Boats' course: `CourseX[row]` (river centre for every row), berths `BerthX[k], BerthY[k]` (k = 0, 1), waiting places on the river `WaitY[0..2]` (x = `CourseX`).
- `town.RiverPlotGiven(plot)`, `town.RiverLinkOpen(link)` (the ferry: given and bought).
- Valid on seeds 1 to 100 (`TownRiverPlanTests`).

### The tide

`TideLevel` (0 lowest .. 1 highest; 0 before the age), `TideRising`, `TideSecondsToTurn` (to the next high or low water), `HighWater` (level >= 0.6: boats may arrive), `FordOpen` (level < 0.4). One cycle is `TownRulesRiver.TidePeriod` = 240 game seconds; the age starts at high water.

### Boats

`BoatCount` (3 slots), `Boat(i)` gives a `BoatView`: `Id` (boat number, -1 unused), `Kind` (`BoatKind.Rowboat` / `Barge`, every third), `Phase` (`BoatPhase.Gone` (draw nothing), `Arriving`, `Waiting` (on the river for a berth), `Moored`, `Leaving`), `Good` (wanted: Grain, Flour, Wool or Cloth), `Want`, `Loaded`, `Patience` (seconds left, moored or waiting), `PatienceMax`, `Berth` (-1 none), `Request`, `Skipper` (name), `X, Y, PrevX, PrevY, Heading` (interpolate like villagers). `BoatRequestText(i)`: "The barge Marit wants 30 Flour before the tide turns" or "" (every second boat carries a skipper's request: 25 % more cargo, fill bonus doubled).
- A boat arrives only at high water, at most one every 15 s, at most 3 on the river; it goes to a free berth (1, or 2 with the second berth) or waits on the river (up to 120 s). Cargo = the town's coin rate for 0.6 (rowboat) or 1.4 (barge) minutes in units (balance round 3; was 0.8 and 1.8), times the crane. Every unit is paid on delivery at `BoatUnitPrice(g)` (1.6 x the market price, times better boat prices); filled: a bonus of 30 % of the cargo's price and it leaves; patience over (150 s rowboat, 210 s barge): it leaves with what it has.
- Events: `BoatArrived = 23` (Index boat id, Good, Amount wanted, Level = kind, Value 1 for a request, X, Y), `BoatFilled = 24` (Coin = bonus), `BoatLeft = 25` (Amount loaded, Level wanted), `LandingDelivery = 31` (Villager, Good, Amount, Coin, X, Y = the landing, Index = boat id or -1 when sold to the landing store at market price).
- Models: `rowboat.glb`, `barge.glb`, at the landing `landing.glb`; the cargo carried by villagers `villager-cargo-wool.glb`, `villager-cargo-cloth.glb` (Grain and Flour as before); sounds `boat-call` on `BoatArrived`, `oar-splash` while moving.

### Wool and Cloth

`Good.Wool = 4`, `Good.Cloth = 5`; jobs `JobKind.GrainBoat = 5, FlourBoat = 6, WoolBoat = 7, WoolMarket = 8, ClothBoat = 9, ClothMarket = 10`. Villagers choose them by the same coin-per-second rule (boat jobs only while boats still want that good). Wool: shear at the pasture (far bank, 3 places, 3 s); Cloth: Wool woven at the weaver (near bank, 2 places, 4 s). Market price Wool = 2 x Grain at the market, Cloth = 1.8 x Flour (each with its value factors and the new Wool / Cloth price tracks). `StorePrice(g)`, `RiverDelivered(g)`, `RiverEarnedBy(g)`. A villager on a river job: `VillagerView.Road` is -1 and `X, Y, Heading` follow the river plan (walking a lane, waiting at a crossing, on the raft, working at the pasture or weaver); `Phase` Walking, Queued (at a crossing or for a place) or Working. Pasture, sheep and the weaver's cottage are new code-made models (no asset yet).

### Crossings

`CrossingExists(RiverLinkKind k)`, `Crossings(k)` (people carried), `CrossingsPerMinute(k)`, `CrossingWaiting(k, bank)` (0 near, 1 far), `LongestCrossingWait`, `CrossingCapacity(k)` (people a minute it can carry with a full queue now: width or raft load over the time of one crossing, times the share of time it is open). At most 3 people on the ford at once, 6 on the footbridge, 8 on the drawbridge. Villagers pick their crossing by time, counting the queue already waiting there and the wait for low water.
- Ford (`ford-stones.glb`, sound `water-ford`): walking speed x 0.35, steps on only while `FordOpen`; people queue on the bank (0.8 cells apart, away from the water) at high water.
- Ferry (offer at level 22, `BuyFerry()`; `ferry-raft.glb`, `ferry-post.glb`, `ferry-rope.glb`, sound `ferry-rope`): `Ferry` gives a `FerryView`: `Built, Pos` (0 near .. 1 far), `Dir` (+1 to far, -1 to near, 0 docked), `Aboard, Capacity, WaitingNear, WaitingFar, Trips, X, Y, Heading`; `FerryRider(k)` = villager id aboard. It docks 2 s, leaves when someone is aboard or waits on the other side. Event `FerryDocked = 27` (Index bank, X, Y).
- Footbridge (level 25; `footbridge-end.glb`, `footbridge-span.glb`): always open, normal speed.
- Drawbridge (level 27; `drawbridge-end.glb`, `drawbridge-lift.glb`, `drawbridge-span.glb`, `keeper-hut.glb`; sounds `drawbridge-raise`, `drawbridge-lower`): `DrawbridgeAngle` (radians, 0 down .. 1.2 up). It lifts when a boat comes within 6 rows (once no walker is on it); boats wait below it until it is up; walkers wait at its ends while it is up or a boat is near. Every boat pays `Toll` on its way in (event `TollPaid = 26`, Coin, X, Y). `LiftSeconds`. Events `DrawbridgeRaised = 28`, `DrawbridgeLowered = 29`.

### Purchases (new `ShopItemKind` values, listed in `ShopItems` after the first age's)

| Kind | Index | Name | Steps | Effect | Method, view |
| --- | --- | --- | --- | --- | --- |
| `Ferry = 12` | -1 | "The ferry" | 1 (from level 22) | the ferry works | `BuyFerry()`, `FerryPrice` |
| `Berth = 13` | -1 | "A second berth" | 1 (from level 23) | two boats moor at once | `BuyBerth()`, `BerthPrice` |
| `Crane = 14` | -1 | "Crane" | 6 (from level 26) | boats ask 25 % more cargo per step | `BuyCrane()`, `CraneFactor`, `CranePrice` |
| `BoatPrices = 15` | -1 | "Better boat prices" | 20 | +4 % per unit from boats | `BuyBoatPrices()`, `BoatPriceFactor` |
| `RiverValue = 16` | 4 or 5 | "Wool price", "Cloth price" | 20 each (from 20 / 21) | +4 % per step | `BuyRiverValue(g)`, `RiverValueFactor(g)`, `RiverValuePrice(g)` |
| `Raft = 17` | -1 | "A bigger raft" | 5 | +1 person per crossing (3 at first) | `BuyRaft()`, `FerryCapacity` |
| `Rope = 18` | -1 | "A faster rope" | 5 | raft +35 % speed per step | `BuyRope()`, `FerrySpeed` |
| `Toll = 19` | -1 | "A higher toll" | 8 | +25 % toll per step | `BuyToll()`, `Toll` |
| `Winch = 20` | -1 | "A faster winch" | 6 | lift 12 % quicker per step | `BuyWinch()`, `LiftSeconds` |

`Buy(item)` buys any of them; `BuyRiver(kind, index)`; `RiverLockReason(kind, index)` ("" or e.g. "Comes with the ferry", "Unlocks at level 22", "Best prices reached"); `RiverTrackLevel(kind, index)`. Each purchase emits `Purchase` and adds renown = price, as before; the ferry and the berth also emit `BuildingOpened` (Index -1, Plot -1, `Item` = Ferry / Berth). `OfferAvailable` is sent with `Item` Ferry, Berth or Crane when its level comes. `ShopItem.Worth` is filled for them too. The first age's items (Villager with room in the river cottages, Pace, Tools, value tracks, paving, extensions, the bell) stay on sale as before.

### Hints, achievements, money views

- `town.RiverHint`: "A boat waits for a berth. A second berth would help.", "People wait at the ford for low water. The ferry would carry them now.", "A queue at the ferry. A bigger raft would help.", "A boat will leave soon. Send it Flour." or "". (Not merged into `Hint`, so the first age's hint is unchanged; show it first when not empty.)
- 16 achievements: `RiverAchievementCount`, `RiverAchievement(i)` (an `AchievementView` with `Index` 100 + i, `Id` "river0".."river15"); unlock events are `AchievementUnlocked` with `Index` 100 + i. The first age's `AchievementCount` / `Achievement(i)` are unchanged.
- `LandingCoin` (coin earned at the landing), `TollCoin`, `RiverMarketCoin` (Wool and Cloth sold at the market), `BoatsCome`, `BoatsFilled`, `BoatsLeftShort`, `Tolls`, `RiverStartTime`.

### Saving

`TownSave.CurrentVersion` is now **3**; `TownSave.FirstAgeVersion` = 2. A town saves as 3 once the second age is rung in (`TownState.River` holds it), else as 2 exactly as before. `Load` accepts 2 and 3; a version 2 first-age save loads and can ring in the second age.

### Bots and tool

`TownBot.PlayOn = true` rings in the next age at the charter and plays on to the seal (off by default). `TownRiverPacing.Run(seed, bot)` gives a `RiverPacingResult`; targets in `TownRiverTargets`. Tool: `town --river --seeds 1-10 [--bot cheapest|payback|random|saver] [--trace]`, `town --river --plan --seed N`.

### Cost

One game minute at the seal (about 30 villagers) stays under 10 ms of CPU and `Step()` allocates nothing (`OneGameMinuteAtTheEndOfTheSecondAgeIsCheap`).

## Added in balance round 3 (numbers only)

No API change. `TownRulesRiver` numbers changed: `BoatMinutes` {0.8, 1.8} to {0.6, 1.4}; `FerryPrice` 3000 to 2000; `BerthPrice` 8000 to 5000; `CraneBase` 6000 to 4000; `TollBase` 300 to 3000; `TollTrackBase` 5000 to 2500. Prices in the shop views follow. Tool: `town --set` now also takes `TownRulesRiver` fields (a name that exists in both classes means the first age's; write `River.LevelRenown=...` for the river's), used with `town --river`. Details in docs/plans/town-balance-r3.md.

## Added in the green pass

Nothing renamed or removed. Saves: new fields default when missing, so version 2 and version 3 saves from before this pass load and play on; a fresh first-age town still saves as version 2 and a rung-in town as version 3 (decided: the existing version tests pin these numbers and every new field has a safe default, so the version did not have to move).

### Touches (sim/Town/TownTouches.cs)

Small things the town dresses itself with. They cost nothing and do nothing.

- `enum TouchKind`: `Woodpile, WashingLine, FlowerBed, HenCoop, Bench, Beehives, Haystack, AppleTree, Handcart, Signpost` (names match the art).
- `Town.TouchSpots` (`TouchSpot[]`): every reserved spot of the seed, the same in both ages: `Kind`, `X`, `Y` (cell centre), `CellX`, `CellY`, `Heading` (radians, faces its plot), `Plot` (first-age plot id or -1), `RiverPlot` (river plot id or -1). Up to 3 per plot of either age, at most `TownRules.TouchSpotMax` (64). Clear of roads, lanes, crossings, plots, doors, water (and one cell of bank) and of each other (no two in touching cells); tested on seeds 1 to 100.
- `Town.TouchCount`, `Town.Touch(i)` (`TouchView`: Kind, X, Y, Heading, Plot, RiverPlot), `Town.NextTouchAtCoin`.
- Event `TouchAdded` (32): Index = touch number, Level = (int)TouchKind, Plot, Amount = river plot, X, Y, Value = heading, Text = kind name.
- The beat: a touch appears when the coin earned in all passes `TouchNextCoin`; the next beat is `TouchMinutes` (3.5, first age) or `TouchMinutesRiver` (1.5, second age) of the town's income, taken as the lowest of the last beat's average, the beat before and the last minute (a boat's lump of coin would otherwise stretch the next beat). A backstop: `TouchMaxGap` (170 s) after the last touch the next one comes anyway. The next free spot (dressing order: first spot of each plot by level, then the second, then the third) beside a plot the town has gets it. At most `TouchMax` (44). Seeds 1 to 10, all four bots: 16 to 17 at the ring, 33 to 43 at the seal. Saved (`TouchCount`, `TouchOrder`, `TouchNextCoin`, `TouchLastCoin`, `TouchLastTime`, `TouchPrevRate`).
- Both ages' pacing count a touch as something new on the map.

### Tracks that go on

- In the second age Pace, Tools, the value of Water, Grain and Flour, the bell and extending cottages go on past their first-age last step: `TownRulesRiver.PaceMore`, `ToolsMore`, `ValueMore` (3 more steps on sale per river level, 36 by level 28), `BellTrackMore` (1 per level), `ExtendMore` (a third extension of each first-age cottage, `ExtendMaxRiver` = 3). Further steps start at `TrackStart` (0.3) of the first age's last price and grow by `TrackGrowth` (1.08) a step, so cheap, middling and dear steps are on sale at every river level. `Town.PaceCap`, `ToolsCap`, `ValueCap`, `BellTrackCap`, `ExtendCap` give the last step now. In the first age everything is as before.
- First age, levels 13 to 16: `PaceMax` 27 (was 25), `ToolsMax` 20 (was 18), `ValueMax` 16 (was 14), tables changed at levels 13 to 16 only.
- `Town.TrackPrice(base, growth, level, firstAgeMax)`.

### Age lengths

- First age: `LevelRenown` for levels 14 to 16 is 52000, 84000, 160000 (were 48000, 74000, 140000), and the charter needs at least `TownRules.CharterMinutes` (10) of the town's best coin rate above the renown held at level 15, fixed when level 15 is reached (`TownState.CharterRenown`, `Town.LevelRenownOf(n)`). A town that is slow to level 15 meets the table's number as before.
- Second age: each river level's renown is fixed when the level before it is reached, in minutes of the best coin rate then (`RiverState.LevelAt`); `LevelRenown` minutes are 0.64 of rules v3's. A version 3 save from before keeps the old ring-time scale.
- The landing's opening: the first boat 5 s after the ring (`FirstBoatAfter`), and until the first `OpeningBoats` (8) boats are served, boats pay `OpeningPremium` (1.6) times more (`Town.OpeningTrade`). Boats still come only at high water.

### The game builders' wishes

- `ShopItem.Locked` (not on sale yet) and `ShopItem.Finished` (nothing more will come), set from the reason inside the simulation (`Town.IsLockedReason`, `Town.IsFinishedReason`).
- `Town.CanPaveNow(road)`, `Town.CanExtendNow(plot)`: include the coin.
- `Hint` builds no string per read (Load and hand texts built once and kept; level texts cached); every branch checks that what it points at can be bought (ignoring coin).
- `Town.OfferBoughtOf(OfferKind)`.
- Achievement texts print large numbers with thousands separators ("Earn 20,000 coin in all.").
- After the borough seal: `Town.SecondAgeComplete`, `Town.SecondAgeCompleteText`, `Town.StillOnSale(list)` and `Town.StillOnSaleCount` (what can still be bought, now or later). Saved through the seal itself.

### Regulars: the first age's ladder of small steps (green pass 2; sim/Town/TownRegulars.cs)

Something cheap is always on sale in the first age, so a player who buys whenever they can always finds a purchase, early and late, and no track is bought out for it.

- `ShopItemKind.Regulars` (21, appended). Listed in the first age only, after the other tracks and the hands (the order of every other item is unchanged), name "Regulars", `Level` = regulars won in all, `Worth` = the coin a minute it adds now. Not listed in the second age (its own tracks go on there); what was won stays.
- Effect: each regular makes every delivery (and request, boat and store payment, everything that goes through the coin bonus) pay `TownRules.RegularsStep` (0.004, so 0.4 %) more, for good. `Town.RegularsFactor` = 1 + step x regulars.
- Price: `TownRules.RegularsMinutes` (0.15) of the town's best coin rate when the current level was reached (at least `RegularsRateMin`, 20, a minute), times `RegularsGrowth` (1.35) per regular already won at this level, rounded up. The ladder starts again at every level-up. `Town.RegularsPrice`.
- `Town.Regulars`, `Town.RegularsThisLevel`, `Town.RegularsLockReason` ("" in the first age), `Town.BuyRegulars()`; `Town.Buy(item)` handles it.
- Saved: `TownState.Regulars`, `RegularsAtLevel`, `RegularsRate` (0 when missing, so older saves load with no regulars).
- Game: the HUD shows unknown kinds with its default icon and the item's own name; a short line for it could read "A regular customer: every delivery pays 0.4 % more."
- Pacing: `PacingResult.ByKind` and `RiverPacingResult.ByKind` now have 22 entries (were 12 and 21).

### Not done

- Villagers housed in the riverside and far-bank cottages still keep a first-age cottage as their errand home (decision 6 of the green pass is not built; green pass 2 left it out too: not safe to build and test in its time box).

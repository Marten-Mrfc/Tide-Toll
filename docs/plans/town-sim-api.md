# Town simulation API (for the game builder)

Namespace `TideToll.Sim.Town`, project `sim/` (already referenced by `game/`). Engine-free and deterministic. All numbers live in `sim/Town/TownRules.cs`. The class `Town` shares its name with the namespace's last part: from `namespace TideToll.Game` with `using TideToll.Sim.Town;` plain `Town` works; from inside `TideToll.Sim.*` write `using T = TideToll.Sim.Town.Town;`.

## Make, step, save

```csharp
using TideToll.Sim.Town;
var town = new Town(seed);                 // plans the town (about 3 ms) and starts level 1: one cottage, the well, one road, one villager
town.Step();                               // one fixed step of TownRules.Dt = 0.1 game seconds (TownRules.TickHz = 10 per game second)
town.DrainEvents(list);                    // List<TownEvent>, cleared then filled with everything since the last drain
string json = TownSave.Save(town);         // whole state, System.Text.Json, field "Version" = TownSave.CurrentVersion (1)
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
- Levels (12): 1 start; 2 cottage, lane, bell green (the bell); 3 first field; 4 cottage, lane, second bucket offer; 5 market square, stall offer; 6 loop; 7 two cottages; 8 second field offer, two tracks, cobbles; 9 cottage, two lanes; 10 mill offer and road; 11 cottage, two lanes; 12 charter and a last lane.

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

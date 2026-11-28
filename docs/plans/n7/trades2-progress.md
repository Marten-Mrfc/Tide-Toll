# Trades, step 3, part 1: the second age's trades in the simulation (progress)

**Names commit for the second builder: `a5f0660`** ("Snapshot: trades 2, the names"). Rules: `c924c80`; V2 `47e34db`; **as shipped: no porters and the second age's floors fitted to the work, `77f5224`** (see "Staffing each trade to its work" at the end: it supersedes the porter and the target figures above it).

## Where it stands (read this first)

- The three trades are built and filled by the town as the sheet says; `sim.tests` **314 of 314 pass**; every band of both ages passes; the first age is tick for tick as before (pinned with trades on and off; `bands` prints the first age identically).
- **Two of the four targets for the second age are met, two are not** (seeds 1 to 10, four bots, `people --mix` and `people --river --bot four`):
  - no trade over about 40 % of the town on average: **met** (largest mean share 30-34 %, the reapers; the porters 16-19 %);
  - hat changes by the town at most about three a minute: **met** (1.64-1.81 a minute; before 0.18-0.49);
  - trips at the own trade at least 80 %: **not met: 54 / 59 / 54 / 59 %** (payback / cheapest / random / saver; before 69 / 74 / 70 / 74 % by the old rule, where grain and flour to the boats were the reapers' and millers' own);
  - herding no higher than now (26 / 19 / 24 / 22 %): **24 / 26 / 25 / 21 %: the cheapest bot 7 points over, the random 1 point over**, payback and saver under.
- With the sheet's constants (the lean, the reward, the like, Training are step 2's) **the porters' floor is the only constant left to tune, and it does not move the own-trade share** (51-60 % at every value from 0 to 9 a berth) nor the herding (21-27 %). What does move both is the lean in the second age; **at the lean that meets 80 % (about 2.5) the band "landing share >= 1/3" fails (seed 9: 0.136, -59 %)**; at the largest lean that keeps that band (1.8) the own share is 61-66 % and the cheapest bot herds 21 % (2 over). Per the brief I stopped there and shipped the sheet's constants: the table is under "What the lean would do".

Branch `wt/n7-trades2`. Brief: `docs/plans/n7/trades2-build-sheet.md`, "The three trades" and "The simulation" (items 1 to 6), and the `sim.tests`.

## The names commit (the second builder starts here)

**Commit `a5f0660`** ("Snapshot: trades 2, the names"). In that commit the three new trades are named and open with their places, and the views and calls take them, but the town does not fill them and no job belongs to them yet (`TradeOfJob` and `TradeOfLeaning` as before), so nobody is in them unless a call puts someone there. Every test passes; the pin with trades off, the new pin of the first age with trades on, and a pin of both ages with trades on (recorded before any change) are identical.

### The names as built

```csharp
public enum Trade : byte { None = 0, Water = 1, Grain = 2, Flour = 3, Wool = 4, Cloth = 5, Boat = 6 }   // appended
// Wool: shepherds, Cloth: weavers, Boat: porters (grain and flour to the landing)

Town.TradeCount = 7                    // including None (index 0, the Helpers); every array by trade has 7 entries
Town.FirstAgeTrades = 4                // the first age's trades are 1..3; the second age's 4..6

town.TradeOpen(Trade.Wool)             // the pasture given (second age only)
town.TradeOpen(Trade.Cloth)            // the weaver's cottage given
town.TradeOpen(Trade.Boat)             // the landing given
town.TradeInfo(t).Places               // Wool: TownRulesRiver.PasturePlaces (3), Cloth: WeaverPlaces (2), Boat: 0 (the landing has no work places)
```

Everything else is step 2's names, now taking the three new values: `TradeInfo`, `SetTradeCount`, `SetVillagerTrade`, `CanRaiseTrade`, `CanLowerTrade`, `TradeEarnPerHead`, `VillagerView.Trade` / `.Likes`, `TownEventKind.TradeChanged` (Index / Amount up to 6), `TownState.TradeCountByPlayer`, `TradeWant`, `TradeEarn`, `TradeAt` (length 7).

On the cards (the game's stand-ins in `TownHud.Trades.cs`, `TownHud.PeopleTrade.cs`, `TownHud.TradesParts.cs`, by `(int)Trade`): "Shepherds", "Weavers", "Porters"; one villager "shepherd", "weaver", "porter"; tooltips "Shepherds bring in the wool.", "Weavers make the cloth.", "Porters carry grain and flour to the boats."; icon names `trade-wool`, `trade-cloth`, `trade-boat` (stand-in: the coin while missing).

Saves: a save from before has four entries by trade; it loads with the three new ones at their defaults (nobody in them, their counts the town's, no earnings). Versions 2 and 3 kept.

### Game code touched (only so nothing indexes past the end with seven trades)

- `game/scripts/town/TownHud.Trades.cs`: `TradeNames`, `TradeFullNames`, `TradeOne`, `TradeIcons` grown to seven (the sheet's names). `TradeOrder` (the card's lines) unchanged: the second builder's.
- `game/scripts/town/TownHud.PeopleTrade.cs`: `TradeTitle` grown to seven. The caps (four) unchanged.
- `game/scripts/town/TownHud.TradesParts.cs`: `TradeSentences` grown to seven (the sheet's tooltips).
- `game/scripts/town/TownVillagers.Heads.cs`: `HeadOf` gives the second age's trades a Helper's hair (there are six heads; their headwear is the figures' builder's).

## The pins (recorded before any change)

`sim.tests/Town/TownTradesPinTests.cs`:
- `WithTradesOffARunIsTheRunBeforeTrades` (as step 2: trades off, both ages, hashes from `70922b8`).
- `WithTradesOnTheFirstAgeIsAsBeforeTheSecondAgesTrades` (new): seeds 1 to 3, payback and cheapest, trades on as shipped, the first age to the ring; every 10 ticks coin, renown, level, purchases, trade changes and every villager's job, phase, trade and position. Recorded at `db85993` before any change.
- `WithTradesOnBothAgesAreAsBeforeStep3` (names commit only): the same run as the trades-off pin with trades on, both ages (8 minutes of the second). Recorded at `db85993`. The rules change the second age, so the rules commit replaces it (below, "Tests replaced").

## The rules as they stand (`c924c80`)

1. **Jobs** (`Town.TradeOfJob`): `WoolBoat`, `WoolMarket` → Wool (shepherds); `ClothBoat`, `ClothMarket` → Cloth (weavers); `GrainBoat`, `FlourBoat` → Boat (porters). Reapers keep `GrainHome`, `GrainMarket`; millers `Flour`; carriers `Water`. **Leanings** (`TradeOfLeaning`): Wool → Wool, Cloth → Cloth; nobody leans to the porters (so no porter has the like).
2. **Opening** (`TradeOpen`): Wool with the pasture given, Cloth with the weaver's cottage, Boat with the landing (second age only; in the first age all three are closed, so nothing of this step can act before the ring). Places (`TradeInfo.Places`): the pasture's 3, the weaver's 2, the porters 0.
3. **The lean, the reward, the like, Training**: step 2's, unchanged, for all six trades. The lean applies through `Leaned` as before (river jobs too). The work bonus now also applies at the pasture and the weaver's (`StepRiverVillager`: a cloth trip's shearing and weaving are the weaver's, as a flour trip's field is the miller's), the worth bonus on wool and cloth sold at the market (`DeliverMarket`; the landing had it already). A Helper's factor is 1 exactly, so the trades-off pin stays.
4. **The filling**: step 2's rule, unchanged, over all six trades (a member at once into an open trade with none, from the trade with most above its floor; Helpers to the trade furthest under its floor, else the best coin a head; one move every 20 s, the floor first, then the figure past the 20 % gap). Floors: three a place for the pasture (9) and the weaver's (6) as for the old workplaces; **the porters' floor `TradePortersPerBerth` x berths (6 or 12)**; all floors scaled down together when the town is smaller (in the second age it always is: about 50-65 floor places for 25-40 villagers, so every trade sits at its scaled floor and the mix is the floors' proportions).
5. **Old saves and towns in the second age**: the arrays by trade grow to seven on load (new entries at their defaults). In the first second after a load (or after a trade opens) each open new trade takes a member from the trade most above its floor (rule 2 of the filling); the floors then fill one move every 20 s. Tested on the v3 save from before trades and on a step 2 save in the second age (arrays cut to four).
6. **Determinism and allocation**: as step 2 (by index, `stackalloc` spans of `TradeCount`); the second age's step allocates nothing with a rebalancing move in the window (tested).

## The constants

| constant | value | reason |
| --- | --- | --- |
| `TownRules.TradePortersPerBerth` | 6 | the landing has no work places; a boat trip is a field's work plus the walk to the bank and over the river, about twice a home trip, so twice the three a place of the well and fields. Measured (seeds 1-10, four bots) at 0 / 2 / 3 / 4 / 6 / 9 a berth: porters 5 / 9 / 11 / 14 / 18 / 23 % of the town, reapers 39 / 36 / 35 / 33 / 32 / 29 %; own trade 51-58 / 53-59 / 52-60 / 53-59 / 54-59 / 54-59 %; herding 24-27 / 23-26 / 24-27 / 24-26 / 21-26 / 23-27 %; hats 0.9-1.2 / 1.7-2.0 / 1.7-1.9 / 1.3-2.0 / 1.6-1.8 / 1.7-2.0 a minute. 6 gives the lowest herding and the most even mix; no value moves the own share far. |
| everything else | unchanged | the sheet: the lean, the reward, the like and Training are step 2's, for all six trades; the floors three a place. |

## Targets for the second age, per bot (seeds 1 to 10; before = the names commit, i.e. step 2 as merged; after = `c924c80`)

| bot | largest trade, mean share | own trade before → after | hats/min before → after | herding before → after (target: no higher) | seal min before → after | coin/min 2nd before → after |
| --- | --- | --- | --- | --- | --- | --- |
| payback | reapers 54 % → 31 % | 69 % → **54 %** | 0.49 → 1.76 | 26 % → 24 % | 25.75 → 25.01 | 40215 → 44266 |
| cheapest | reapers 59 % → 34 % | 74 % → **59 %** | 0.18 → 1.81 | 19 % → **26 %** | 28.08 → 27.85 | 24503 → 24995 |
| random | reapers 53 % → 30 % | 70 % → **54 %** | 0.23 → 1.64 | 24 % → **25 %** | 25.21 → 25.96 | 33930 → 32124 |
| saver | reapers 56 % → 33 % | 74 % → **59 %** | 0.49 → 1.69 | 22 % → 21 % | 26.54 → 26.39 | 39216 → 39030 |

First age (all bots): identical before and after (charter 29.46 / 36.72 / 35.76 / 27.04 min, herding 16 / 16 / 17 / 15 %, own 91-92 %, hats 1.44-1.76).

### The mix (`people --mix --seeds 1-10`, second age from the ring to the seal, mean share of the town)

| bot | | carriers | reapers | millers | shepherds | weavers | porters |
| --- | --- | --- | --- | --- | --- | --- | --- |
| payback | before | 19 | 54 | 27 | - | - | - |
| payback | after | 11 | 31 | 13 | 14 | 11 | 19 |
| payback | after, at the seal | 9 | 28 | 11 | 16 | 15 | 22 |
| cheapest | before | 19 | 59 | 22 | - | - | - |
| cheapest | after | 10 | 34 | 13 | 15 | 12 | 16 |
| cheapest | after, at the seal | 10 | 31 | 10 | 14 | 12 | 23 |
| random | before | 20 | 53 | 27 | - | - | - |
| random | after | 12 | 30 | 14 | 15 | 12 | 18 |
| random | after, at the seal | 10 | 26 | 10 | 16 | 16 | 22 |
| saver | before | 18 | 56 | 26 | - | - | - |
| saver | after | 10 | 33 | 13 | 14 | 11 | 19 |
| saver | after, at the seal | 9 | 29 | 11 | 15 | 15 | 21 |

The work as the best-pay rule would split it (filling off, every villager a Helper, `--play helpers`, counted by the trade of the job each is on): carriers 1, reapers 3-7, millers 15-20, shepherds 9-15, weavers 29-33, porters 31-36 %. The floors (three a place, the fields having the most places) put about a third of the town in the reapers, who then go to the boats and the cloth.

### Why the own share is low: trips by trade and job (`people --away`, payback, second age, share of each trade's trips)

| trade | own work | the rest |
| --- | --- | --- |
| carriers | water 48 % | grain and flour to the boats 28 %, flour 9 %, wool and cloth 15 % |
| reapers | grain home and market 51 % | to the boats 29 %, wool and cloth 14 %, flour 5 % |
| millers | flour 75 % | flour to the boats 14 %, grain to the boats 5 % |
| shepherds | wool 32 % | flour 20 %, to the boats 30 %, cloth 15 % |
| weavers | cloth 61 % | flour 11 %, to the boats 19 %, wool 7 % |
| porters | grain and flour to the boats 57 % | flour (market) 23 %, wool and cloth 17 % |

Boats pay `BoatPremium` (1.6) times the market price, as much as the lean, and come in bursts (a boat wants 0.6 to 1.4 minutes of the town's income at once): when one is in, every trade's members find its cargo worth as much as their own work and go; when none is, the porters and shepherds have no own work (wool sells at the market for less than flour) and do other trips. Neither the floor nor the mix changes that; only a larger lean does.

### What the lean would do (measured only; not shipped: the sheet keeps step 2's lean)

The lean for every trade in the second age only (the first age keeps 1.6 and is unchanged), porters 6 a berth:

| lean in the second age | own trade P / C / R / S | herding P / C / R / S | hats/min | landing share >= 1/3 (nearest run) | age floor 20 min (nearest) |
| --- | --- | --- | --- | --- | --- |
| 1.6 (shipped) | 54 / 59 / 54 / 59 % | 24 / 26 / 25 / 21 % | 1.6-1.8 | seed 8 cheapest 0.423, +27 % | seed 1 payback 20.88, +4.4 % |
| 1.75 | 60 / 63 / 59 / 62 % | 19 / 21 / 23 / 21 % | 1.6-1.8 | seed 9 payback 0.402, +20.7 % | seed 5 payback 21.04, +5.2 % |
| 1.8 | 61 / 66 / 62 / 64 % | 17 / 21 / 20 / 17 % | 1.5-1.8 | seed 9 saver 0.375, +12.5 % | seed 5 payback 21.43, +7.2 % |
| 1.85 | 63 / 67 / 62 / 66 % | 17 / 19 / 20 / 15 % | 1.6-1.9 | seed 9 random 0.318, **-4.5 % FAIL** | 4.7 % |
| 2.0 | 68 / 69 / 67 / 71 % | 14 / 17 / 17 / 14 % | 1.6-1.8 | seed 9 saver 0.252, **-24 % FAIL** | 7.9 % |
| 2.25 | 76 / 78 / 73 / 80 % | 10 / 10 / 11 / 6 % | 1.5-1.8 | seed 9 saver 0.193, **-42 % FAIL** | 12.9 % |
| 2.5 | 81 / 84 / 80 / 85 % | 6 / 6 / 8 / 2 % | 1.6-1.8 | seed 9 saver 0.136, **-59 % FAIL** | 2.5 % |
| 2.75 | 85 / 87 / 82 / 89 % | 3 / 2 / 6 / 2 % | 1.6-1.8 | seed 9 random 0.118, **-65 % FAIL** | seed 1 payback 19.71, **-1.5 % FAIL** |

So: the own-trade target needs a lean of about 2.5 in the second age and that tips "landing share >= 1/3" by 59 % on seed 9 (members stay at the market and the boats are served short); the herding target alone is met from 1.85, which tips the same band by 4.5 %. With a larger porters' floor at lean 2.5 the band still fails (9 a berth: 0.159; 20 a berth: 0.163, porters 34-40 % of the town), and lower floors in the second age (one a place, so the coin a head decides) made it worse (0.077). The boats are being rebuilt (turns on the river), which changes exactly this (bursts of cargo): the lean question is best taken up again after both are merged.

## Every band: nearest run and margin, before (the names commit) and after (`c924c80`)

`bands --bell`. The first age's lines are identical before and after (all 17 bands; nearest runs and margins as in `trades-progress.md`: the thin ones are the charter ceiling 1.5 % and floor 0.6 %, the requests average 3.0 %).

| band (second age) | nearest run before | margin before | nearest run after | margin after |
| --- | --- | --- | --- | --- |
| age 20-35 min, buyers (ceiling) | seed 9 cheapest 32.02 min | 8.5 % | seed 9 cheapest 32.48 min | 7.2 % |
| age 20-35 min, buyers (floor) | seed 2 payback 21.64 min | 8.2 % | seed 1 payback 20.88 min | 4.4 % |
| crossing wait <= 240 s | seed 4 cheapest 187 s | 22.2 % | seed 4 cheapest 211 s | 12.0 % |
| drawbridge to seal >= 3.5 min | seed 3 cheapest 5.03 min | 43.8 % | seed 2 cheapest 4.75 min | 35.7 % |
| each crossing carries more | seed 1 cheapest | 100 % | seed 1 cheapest | 100 % |
| ford crossings, all runs >= 300 | 718 | 139.3 % | 960 | 220.0 % |
| ford high-water wait, all runs >= 1000 | 6311 | 531.1 % | 12631 | 1163.1 % |
| ford only >= 2 min | seed 2 payback 2.99 min | 49.7 % | seed 10 saver 3.10 min | 55.1 % |
| ford used on seeds >= 4 | 9 | 125.0 % | 10 | 150.0 % |
| landing share >= 1/3 | seed 9 saver 0.442 | 32.6 % | seed 8 cheapest 0.423 | 27.0 % |
| map gap <= 210 s | seed 6 cheapest 170 s | 19.0 % | seed 7 cheapest 159 s | 24.3 % |
| nothing affordable <= 60 s | seed 6 cheapest 46 s | 24.2 % | seed 6 cheapest 46 s | 22.5 % |
| purchases a minute >= 2 | seed 9 cheapest 4.78 | 138.9 % | seed 9 cheapest 4.68 | 134.0 % |
| saver buys every new kind (misses) | seed 1 saver 0 | 0.0 % (a count) | seed 1 saver 0 | 0.0 % (a count) |
| seal (every run) | | 100 % | | 100 % |
| toll steps back, all runs >= 1.0 | spent 983975 earned 1136250 | 15.5 % | earned 1143750 | 16.2 % |
| toll steps back, single run >= 0.35 | seed 1 cheapest earned 17250 | 100.4 % | seed 4 cheapest earned 15000 | 74.2 % |
| tolls before seal >= 2 | seed 1 cheapest 3 | 50.0 % | seed 2 cheapest 3 | 50.0 % |

**Room left for the boats' rebuild:** the second age's floor (20 min) has 0.88 minute on its nearest run (seed 1 payback 20.88, 4.4 %), the ceiling (35) 2.5 minutes (seed 9 cheapest 32.48, 7.2 %); the crossing wait 29 s (12 %); the landing share 0.09 (27 %); the toll payback, all runs, 16 %. The thinnest is the age's floor: a change that speeds the richest town (payback, seed 1) by more than about 0.9 minute tips it. `LevelMinutesScale` was not touched. Expect one more `bands` pass after both are merged.

## Tables per seed: second age, before → after (`people --seeds 1-10 --river --bot four`)

| seed | bot | seal min | buys 2nd | coin/min 2nd | herd 2nd | own 2nd | hats/min 2nd |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | payback | 22.4 → 20.9 | 220 → 223 | 55179 → 64855 | 33 % → 35 % | 53 % → 46 % | 0.45 → 1.82 |
| 2 | payback | 21.6 → 22.9 | 205 → 199 | 55534 → 50862 | 17 % → 15 % | 61 % → 49 % | 0.51 → 1.57 |
| 3 | payback | 25.7 → 26.9 | 195 → 194 | 41803 → 37288 | 19 % → 27 % | 71 % → 55 % | 1.01 → 2.23 |
| 4 | payback | 26.1 → 23.8 | 195 → 204 | 38224 → 47978 | 33 % → 35 % | 67 % → 53 % | 0.38 → 1.60 |
| 5 | payback | 23.2 → 20.9 | 198 → 201 | 44149 → 56479 | 50 % → 30 % | 58 % → 53 % | 0.47 → 2.06 |
| 6 | payback | 28.0 → 28.5 | 184 → 186 | 32807 → 31301 | 24 % → 22 % | 72 % → 58 % | 0.36 → 1.79 |
| 7 | payback | 27.8 → 28.3 | 195 → 185 | 33496 → 32086 | 22 % → 14 % | 78 % → 61 % | 0.58 → 1.62 |
| 8 | payback | 27.7 → 26.4 | 187 → 190 | 31456 → 35157 | 30 % → 25 % | 72 % → 54 % | 0.40 → 1.29 |
| 9 | payback | 29.9 → 29.6 | 165 → 166 | 25669 → 26322 | 15 % → 12 % | 81 % → 64 % | 0.23 → 1.86 |
| 10 | payback | 24.9 → 21.9 | 210 → 221 | 43838 → 60330 | 17 % → 28 % | 69 % → 44 % | 0.56 → 1.78 |
| 1 | cheapest | 26.4 → 26.2 | 176 → 177 | 27559 → 28117 | 17 % → 26 % | 68 % → 58 % | 0.19 → 1.64 |
| 2 | cheapest | 27.2 → 25.9 | 176 → 183 | 25674 → 29156 | 14 % → 29 % | 77 % → 62 % | 0.22 → 1.89 |
| 3 | cheapest | 29.2 → 29.2 | 169 → 170 | 22407 → 22812 | 11 % → 21 % | 79 % → 63 % | 0.14 → 1.99 |
| 4 | cheapest | 27.0 → 27.0 | 174 → 174 | 26542 → 26384 | 24 % → 31 % | 72 % → 60 % | 0.15 → 1.33 |
| 5 | cheapest | 26.0 → 25.5 | 174 → 176 | 26654 → 27745 | 15 % → 35 % | 64 % → 48 % | 0.23 → 2.00 |
| 6 | cheapest | 28.8 → 29.2 | 163 → 161 | 22338 → 21173 | 28 % → 22 % | 70 % → 58 % | 0.28 → 2.36 |
| 7 | cheapest | 28.0 → 28.3 | 177 → 178 | 26098 → 25504 | 21 % → 19 % | 75 % → 55 % | 0.14 → 1.98 |
| 8 | cheapest | 30.8 → 28.6 | 175 → 185 | 23316 → 27323 | 16 % → 17 % | 80 % → 66 % | 0.13 → 1.82 |
| 9 | cheapest | 32.0 → 32.5 | 153 → 152 | 17079 → 16391 | 17 % → 33 % | 78 % → 62 % | 0.12 → 1.23 |
| 10 | cheapest | 25.4 → 26.1 | 174 → 170 | 27366 → 25349 | 26 % → 25 % | 68 % → 49 % | 0.24 → 1.88 |
| 1 | random | 24.9 → 26.0 | 183 → 180 | 31151 → 28176 | 26 % → 21 % | 71 % → 59 % | 0.16 → 1.23 |
| 2 | random | 23.9 → 22.1 | 209 → 217 | 41366 → 50352 | 33 % → 20 % | 72 % → 56 % | 0.25 → 1.41 |
| 3 | random | 27.9 → 29.4 | 182 → 175 | 27485 → 23906 | 22 % → 26 % | 78 % → 63 % | 0.18 → 2.01 |
| 4 | random | 23.5 → 24.2 | 204 → 202 | 42004 → 38781 | 26 % → 25 % | 72 % → 54 % | 0.17 → 1.78 |
| 5 | random | 22.9 → 23.4 | 187 → 182 | 38109 → 34099 | 33 % → 36 % | 57 % → 44 % | 0.26 → 1.71 |
| 6 | random | 24.2 → 25.2 | 194 → 188 | 35205 → 32092 | 20 % → 25 % | 62 % → 45 % | 0.17 → 1.59 |
| 7 | random | 24.9 → 24.6 | 199 → 198 | 37042 → 37958 | 19 % → 26 % | 72 % → 52 % | 0.12 → 1.96 |
| 8 | random | 26.3 → 27.2 | 194 → 187 | 32025 → 29543 | 18 % → 17 % | 67 % → 47 % | 0.27 → 1.73 |
| 9 | random | 28.2 → 31.7 | 169 → 152 | 23691 → 17155 | 22 % → 26 % | 74 % → 56 % | 0.14 → 1.52 |
| 10 | random | 25.4 → 26.0 | 187 → 183 | 31221 → 29179 | 26 % → 28 % | 68 % → 55 % | 0.59 → 1.50 |
| 1 | saver | 24.1 → 24.2 | 210 → 207 | 49927 → 48495 | 19 % → 25 % | 68 % → 58 % | 0.46 → 1.90 |
| 2 | saver | 25.7 → 24.5 | 202 → 201 | 43789 → 47337 | 12 % → 16 % | 76 % → 65 % | 0.51 → 1.51 |
| 3 | saver | 27.9 → 26.7 | 176 → 179 | 30752 → 34377 | 37 % → 19 % | 75 % → 52 % | 0.47 → 1.91 |
| 4 | saver | 23.7 → 23.7 | 199 → 202 | 46936 → 46867 | 32 % → 32 % | 67 % → 56 % | 0.42 → 1.56 |
| 5 | saver | 23.3 → 24.8 | 195 → 187 | 45169 → 38617 | 24 % → 24 % | 56 % → 56 % | 0.69 → 1.97 |
| 6 | saver | 28.9 → 28.7 | 185 → 183 | 33098 → 33069 | 22 % → 22 % | 77 % → 57 % | 0.66 → 1.81 |
| 7 | saver | 26.7 → 27.8 | 193 → 191 | 37268 → 33591 | 15 % → 12 % | 77 % → 64 % | 0.60 → 1.62 |
| 8 | saver | 31.4 → 30.1 | 160 → 163 | 22895 → 26063 | 27 % → 14 % | 77 % → 62 % | 0.29 → 1.20 |
| 9 | saver | 30.2 → 29.8 | 175 → 178 | 29717 → 31155 | 13 % → 18 % | 84 % → 69 % | 0.23 → 1.82 |
| 10 | saver | 23.5 → 23.6 | 200 → 210 | 52606 → 50732 | 16 % → 34 % | 66 % → 44 % | 0.72 → 1.70 |

## Tests

Added (`sim.tests/Town/TownTrades2Tests.cs`):
- `TheSecondAgesJobsBelongToTheirTrades`: the jobs and leanings of the new trades; nobody leans to the porters; seven trades.
- `TheyOpenWithTheirPlacesAndTakeAMemberAtOnce`: closed through the first age; each opens with its place and has a member within a second of opening; places 3 / 2 / 0.
- `TheTownFillsTheNewTrades`: 30 minutes after the ring (or the seal), no Helper, every open trade has a member, the counts add up, the porters at least their scaled floor less one, the new trades' members make more than a third of their trips at their own work.
- `TheLeanAndTheRewardForTheNewTrades`: the lean on wool for a shepherd, none on boats; the work and worth factors for a shepherd and a porter, 1 for a reaper on a boat job; in play, a shepherd's wool at the market pays the factor.
- `YourCountAndOneVillagerForTheNewTrades`: closed in the first age (false); the porters' count set and kept for 90 s; the weavers' count down to one; a villager set to shepherd and kept; the switch hands every count back.
- `OldSecondAgeSavesTakeTheNewTradesWithinASecond`: the v3 save from before trades, and a step 2 second-age save (arrays cut to four): within a second no Helper and every open new trade has a member; save, load and step exactly.
- `SecondAgeStepsAllocateNothingWithTheNewTrades`: 180 s of the second age with a rebalancing move inside, 0 bytes.
- `SameSeedGivesTheSameSecondAge`.
- `TownTradesPinTests.WithTradesOnTheFirstAgeIsAsBeforeTheSecondAgesTrades` (above, "The pins").

Replaced:
- `TownTradesTests.JobsBelongToTrades`. Old: `GrainBoat` → Grain, `FlourBoat` → Flour, wool and cloth jobs → None. New: `GrainBoat`, `FlourBoat` → Boat, the rest as before (wool and cloth asserted in `TheSecondAgesJobsBelongToTheirTrades`). Why: the sheet gives the boat work to the porters and wool and cloth their trades.
- `TownTradesPinTests.WithTradesOnBothAgesAreAsBeforeStep3` (added in the names commit, removed in the rules commit). Old: both ages with trades on hash as at `db85993`. New: the first age with trades on hashes as at `db85993` (`WithTradesOnTheFirstAgeIsAsBeforeTheSecondAgesTrades`, kept), the trades-off pin over both ages kept. Why: the rules change the second age by design; it held at the names commit.

Unchanged and passing: `WithTradesOffARunIsTheRunBeforeTrades` (hashes from `70922b8`), every band of `TownPacingTests` and `TownRiverPacingTests`, `NeitherSideOfAChoiceIsAMistake`, `TheLastGapIsNoLongerThanTheOnesBeforeIt`.

## Tools

- `people --mix` (`MixCommand.cs`, brought from `wt/n7-trades` with `HandsCommand.cs`): seven trades (six and the Helpers) in the second age, the four bots, `--set`.
- `people`: "own" counts a trip as the trade of the job it was made on, by the rule in force (`Town.TradeOfJob`); before, the good's number against the trade's (the same for step 2's trades). `--away` prints each trade's second-age trips by job.

## Game code touched

The names commit's four stand-ins (above); nothing in the rules commit.

## The porter (the lead's second round)

The porter's own work comes in bursts (only while a boat lies at the landing), so as a full trade its members are away most of the time, and by taking the boat trips from the reapers and millers it makes their boat trips count as away too. Two variants, each a switch in `TownRules` (both read live, so the other stays reachable with `--set`):

- **V1, no porters** (`TradePorters = false`): the Boat trade never opens (the enum, the arrays and the names stay; the card shows open trades only); `GrainBoat` is the reapers' again and `FlourBoat` the millers', as in step 2. Five trades in the second age.
- **V2, a small crew and boat trips owned twice** (`BoatTripsOwnedTwice = true`, `TradePortersPerBerth` 1 or 2): a grain or flour boat trip is at its own trade for the porter **and** for the trade of its good (`Town.AtOwnTrade`); the lean, the reward and the own-trade count all read it. Wool and cloth boat trips were the shepherds' and weavers' already. The figure the town fills by still counts a boat trip's coin to the porters (one trade, no double count).

Seeds 1 to 10, four bots; own trade by the rule of each variant (`people --away` / `AtOwnTrade`); before = `c924c80` (porters 6 a berth, boat trips theirs alone).

| | largest trade, mean share (P / C / R / S) | own trade | hats/min | herding (today 26 / 19 / 24 / 22) | targets met |
| --- | --- | --- | --- | --- | --- |
| `c924c80` (6 a berth) | reapers 31 / 34 / 30 / 33 % | 54 / 59 / 54 / 59 % | 1.76 / 1.81 / 1.64 / 1.69 | 24 / **26** / **25** / 21 % | 2 (share, hats) |
| V1 (no porters) | reapers 39 / **41** / 36 / 40 % | 66 / 69 / 63 / 71 % | 1.40 / 1.16 / 1.18 / 1.08 | 21 / **20** / 20 / 18 % | 2, and the share at about 40 (cheapest 41 %, saver 40 %); herding 1 over on the cheapest bot |
| **V2, 1 a berth (shipped)** | reapers 37 / 39 / 34 / 38 % | 65 / 68 / 63 / 70 % | 1.71 / 1.34 / 1.71 / 1.65 | 23 / 18 / 22 / 19 % | **3** (share, hats, herding) |
| V2, 2 a berth | reapers 36 / 37 / 33 / 37 % | 65 / 69 / 63 / 71 % | 1.95 / 1.66 / 1.83 / 1.76 | 21 / **20** / 22 / 19 % | 2 (herding 1 over on the cheapest bot) |

Seal minutes after the ring (P / C / R / S): `c924c80` 25.01 / 27.85 / 25.96 / 26.39; V1 25.20 / 27.76 / 25.63 / 26.52; V2 (1) 25.13 / 27.79 / 25.14 / 26.33; V2 (2) 25.60 / 27.86 / 24.97 / 26.19. Coin a minute in the second age: V1 42538 / 25094 / 32547 / 38466; V2 (1) 42989 / 25196 / 34444 / 39246.

Mix, second age mean share (payback): V1 carriers 13, reapers 39, millers 18, shepherds 17, weavers 13 %; V2 (1) carriers 12, reapers 37, millers 15, shepherds 16, weavers 12, porters 7 %. (Other bots within 2 points; reapers 34-41 %.)

Trips by trade and job, V2 (1), payback: carriers water 40 %; reapers grain home 42 % + grain to the boats 30 % (own 72 %); millers flour 72 % + flour to the boats 20 % (own 92 %); shepherds wool 29 % (flour 23 %, grain and flour boats 29 %); weavers cloth 59 %; porters boats 64 %. The shepherds are the weakest: wool sells at the market for less than flour and wool boats are a third of the boats at most.

**Shipped: V2 at one porter a berth** (`TradePorters = true`, `BoatTripsOwnedTwice = true`, `TradePortersPerBerth = 1`): it meets three of the four targets with every band passing (V1 two). **The own-trade share is 63-70 %, not 80 %**: no variant reaches it, and the lean was not raised (the sheet keeps step 2's; above, the lean that would reach 80 % tips the landing share). The herding differences between the variants are 1 to 3 points, about the noise of ten seeds; V2's lead over V1 is the herding on the cheapest bot (18 against 20, today 19) and the largest share (39 against 41).

### Every band, second age: nearest run and margin

The first age's bands are identical in all four (`bands --bell`).

| band | `c924c80` | V1 | V2 (1), shipped | V2 (2) |
| --- | --- | --- | --- | --- |
| age 20-35 min, buyers (ceiling) | seed 9 cheapest first 32.48 min, 7.2 % | seed 9 random affordable 31.02 min, 11.4 % | seed 9 cheapest first 32.02 min, 8.5 % | seed 9 cheapest first 31.31 min, 10.5 % |
| age 20-35 min, buyers (floor) | seed 1 best payback first 20.88 min, 4.4 % | seed 5 best payback first 20.97 min, 4.8 % | seed 2 best payback first 20.95 min, 4.7 % | seed 5 random affordable 21.42 min, 7.1 % |
| crossing wait <= 240 s | seed 4 cheapest first 211 s, 12.0 % | seed 4 cheapest first 221 s, 8.0 % | seed 4 cheapest first 139 s, 42.0 % | seed 4 cheapest first 174 s, 27.3 % |
| drawbridge to seal >= 3.5 min | seed 2 cheapest first 4.75 min, 35.7 % | seed 9 cheapest first 4.77 min, 36.2 % | seed 2 cheapest first 4.38 min, 25.2 % | seed 6 random affordable 5.37 min, 53.3 % |
| each crossing carries more | seed 1 cheapest first, 100.0 % | seed 1 cheapest first, 100.0 % | seed 1 cheapest first, 100.0 % | seed 1 cheapest first, 100.0 % |
| ford crossings, all runs >= 300 | 960, 220.0 % | 954, 218.0 % | 931, 210.3 % | 908, 202.7 % |
| ford high-water wait, all runs >= 1000 | 12631, 1163.1 % | 8642, 764.2 % | 9038, 803.8 % | 9245, 824.5 % |
| ford only >= 2 min | seed 10 saver 3.10 min, 55.1 % | seed 2 best payback first 3.01 min, 50.5 % | seed 10 saver 3.09 min, 54.7 % | seed 10 saver 3.08 min, 53.8 % |
| ford used on seeds >= 4 | 10, 150.0 % | 9, 125.0 % | 9, 125.0 % | 9, 125.0 % |
| landing share >= 1/3 | seed 8 cheapest first 0.423, 27.0 % | seed 9 saver 0.469, 40.8 % | seed 8 cheapest first 0.455, 36.6 % | seed 8 cheapest first 0.456, 36.7 % |
| map gap <= 210 s | seed 7 cheapest first 159 s, 24.3 % | seed 6 cheapest first 170 s, 19.0 % | seed 6 cheapest first 170 s, 19.0 % | seed 6 cheapest first 170 s, 19.0 % |
| nothing affordable <= 60 s | seed 6 cheapest first 46 s, 22.5 % | seed 6 cheapest first 46 s, 23.3 % | seed 6 cheapest first 46 s, 22.5 % | seed 6 cheapest first 46 s, 23.3 % |
| purchases a minute >= 2 | seed 9 cheapest first 4.68, 134.0 % | seed 9 random affordable 5.06, 153.0 % | seed 9 cheapest first 4.78, 138.9 % | seed 9 cheapest first 4.98, 149.1 % |
| saver buys every new kind (misses) | seed 1 saver 0, 0.0 %  thin | seed 1 saver 0, 0.0 %  thin | seed 1 saver 0, 0.0 %  thin | seed 1 saver 0, 0.0 %  thin |
| seal (every run) | seed 1 cheapest first, 100.0 % | seed 1 cheapest first, 100.0 % | seed 1 cheapest first, 100.0 % | seed 1 cheapest first, 100.0 % |
| toll steps back, all runs >= 1.0 | spent 983975 earned 1143750, 16.2 % | spent 983975 earned 1127250, 14.6 % | spent 983975 earned 1109250, 12.7 % | spent 983975 earned 1114500, 13.3 % |
| toll steps back, single run >= 0.35 | seed 4 cheapest first spent 24599 earned 15000, 74.2 % | seed 1 random affordable spent 24599 earned 16500, 91.6 % | seed 2 cheapest first spent 24599 earned 18000, 109.1 % | seed 3 best payback first spent 24599 earned 12750, 48.1 % |
| tolls before seal >= 2 | seed 2 cheapest first 3, 50.0 % | seed 1 best payback first 3, 50.0 % | seed 2 cheapest first 3, 50.0 % | seed 1 best payback first 3, 50.0 % |

Room the shipped V2 leaves for the boats' rebuild: the age's floor 0.95 minute (seed 2 payback 20.95, 4.7 %), the ceiling 3.0 minutes (seed 9 cheapest 32.02, 8.5 %), the toll payback over all runs 12.7 %, the landing share 36.6 %, the crossing wait 42 %; the rest 19 % or more.

### Tests for the porter

- Added `TownTrades2Tests.BoatTripsAreOwnedByThePorterAndByTheirGoodsTrade` (the rule, the lean and the reward under V2). V1 is measured with the tools' `--set TradePorters=0`; no test flips a rule other tests run under.
- Changed (a test added in this step, not one from before): `TheTownFillsTheNewTrades`, its last assertion. Old: the new trades' members (shepherds, weavers, porters) make more than a third of their trips at their own work, the maker's trade read at the end of the run. New: every trade's members make more than half their second-age trips at their own trade by `Town.AtOwnTrade`, the maker's trade read when the trip is delivered. Why: with V2 the porters are one a berth and the boat trips are owned twice, so the own-trade figure of the step is the town's, not the three trades'; and the trade must be the one held at the delivery (the old form read it after 30 minutes of moves). At V2 it measured 316 of 1229 (26 %) by the old form, below its third: the shepherds' low share (above).

## Staffing each trade to its work (the lead's third round, `77f5224`)

**No porters** (the lead's decision): `TownRules.TradePorters = false`; V2 stays reachable (`TradePorters` and `BoatTripsOwnedTwice` true), with the reason at the switch. Grain and flour to the boats are the reapers' and millers'.

**The fault:** the floors (three a place for every trade) gave a trade more members than its work keeps: the shepherds made 22-36 % of their trips at wool. In the second age the floors always bind (the town is smaller than all of them), so their ratios are the town's mix. **The fix:** a second-age floor per trade, villagers a place (`TownRules.TradeFloorSecondAge`, index = Trade; the first age keeps `TradeFloorShare` 3 and is tick for tick as before), fitted to the work each trade's places keep, read from the best-pay rule's split of the second age (filling off: carriers 1, reapers 15-23, millers 33-40, shepherds 9-15, weavers 29-33 % of the town) over the places (well 2, fields about 5.6, mill 2, pasture 3, weaver 2):

| trade | floor, villagers a place (second age) | why |
| --- | --- | --- |
| carriers | 0.5 (one member) | water hardly pays in the second age: its well is busy 1-2 % of the time |
| reapers | 2.5 | the fields' work and the grain to the boats |
| millers | 5 | flour (market and boats) is the second age's main work; 6 took the richest seal under the age's floor (seed 5 random 19.3 min) |
| shepherds | 0.5 (one member) | wool sells for less than flour and wool boats are a third of the boats at most: the pasture is busy 15-21 % of the time even with one or two shepherds |
| weavers | 5 | 4 took the seal under the floor too (19.3); 6: weavers 52-57 % |

An open trade keeps at least one member (the filling's rule 2 and the rebalance's floor of one, unchanged). Tried on the way (all at the first age's rules): one share for all (1 a place: town 70-75 %, hats 2.7-3.1 a minute, the floor band fails at 19.24); 0.5 / 1 / 5 / 1 / 5 (town 72-75 %, the floor and crossing-wait bands fail); 0.5 / 1.5 / 7.5 / 0.5 / 7.5 (73-77 %, floor fails); 0.5 / 2 / 6 / 0.5 / 4 (74-77 %, floor fails at 18.93; with `LevelMinutesScale` 1.10 every band passed, but that changes the trades-off run and so the pin: not shipped).

### Per trade, second age: before (no porters, three a place for all) → after (`77f5224`)

Members (mean, share of the town); places (mean while open); own = share of its members' trips at its own trade; busy = share of time its workplace's places are working; coin a head = its members' delivery coin a head against the town's (`people --seeds 1-10 --river --bot four --trades`).

| bot | trade | members | places | own | busy | coin a head |
| --- | --- | --- | --- | --- | --- | --- |
| payback | carriers | 4.0 (13 %) → 1.1 (4 %) | 2.0 | 41 % → **48 %** | 3 % → 2 % | 0.95 → 0.85 |
| payback | reapers | 11.9 (39 %) → 11.9 (38 %) | 5.6 | 71 % → **70 %** | 33 % → 33 % | 0.90 → 0.87 |
| payback | millers | 5.4 (18 %) → 9.5 (31 %) | 2.2 | 90 % → **88 %** | 43 % → 50 % | 1.07 → 1.07 |
| payback | shepherds | 5.4 (18 %) → 1.3 (4 %) | 2.8 | 32 % → **36 %** | 21 % → 21 % | 1.03 → 0.93 |
| payback | weavers | 4.0 (13 %) → 7.1 (23 %) | 1.8 | 62 % → **62 %** | 27 % → 30 % | 1.21 → 1.17 |
| cheapest | carriers | 3.2 (12 %) → 1.1 (4 %) | 2.0 | 48 % → **33 %** | 3 % → 1 % | 0.93 → 0.91 |
| cheapest | reapers | 10.8 (41 %) → 10.9 (41 %) | 6.0 | 78 % → **77 %** | 30 % → 31 % | 0.87 → 0.84 |
| cheapest | millers | 4.2 (16 %) → 7.3 (28 %) | 2.0 | 94 % → **91 %** | 42 % → 46 % | 1.13 → 1.14 |
| cheapest | shepherds | 4.7 (18 %) → 1.2 (5 %) | 2.8 | 28 % → **35 %** | 17 % → 16 % | 1.07 → 1.11 |
| cheapest | weavers | 3.4 (13 %) → 5.9 (22 %) | 1.8 | 49 % → **52 %** | 19 % → 20 % | 1.22 → 1.10 |
| random | carriers | 3.7 (14 %) → 1.1 (4 %) | 2.0 | 38 % → **27 %** | 3 % → 1 % | 0.96 → 0.87 |
| random | reapers | 9.6 (36 %) → 9.9 (36 %) | 5.0 | 68 % → **69 %** | 28 % → 28 % | 0.88 → 0.86 |
| random | millers | 5.1 (19 %) → 8.4 (31 %) | 2.0 | 97 % → **95 %** | 47 % → 52 % | 1.12 → 1.12 |
| random | shepherds | 4.9 (18 %) → 1.2 (4 %) | 2.8 | 22 % → **27 %** | 16 % → 15 % | 1.05 → 1.00 |
| random | weavers | 3.5 (13 %) → 6.6 (24 %) | 1.8 | 50 % → **53 %** | 21 % → 22 % | 1.11 → 1.09 |
| saver | carriers | 4.1 (13 %) → 1.1 (4 %) | 2.0 | 46 % → **50 %** | 4 % → 2 % | 0.92 → 0.85 |
| saver | reapers | 12.5 (40 %) → 12.4 (40 %) | 5.8 | 81 % → **78 %** | 35 % → 36 % | 0.89 → 0.86 |
| saver | millers | 5.3 (17 %) → 9.5 (30 %) | 2.2 | 87 % → **83 %** | 36 % → 43 % | 1.08 → 1.07 |
| saver | shepherds | 5.5 (17 %) → 1.3 (4 %) | 2.7 | 36 % → **44 %** | 23 % → 20 % | 1.05 → 0.99 |
| saver | weavers | 4.0 (13 %) → 7.1 (22 %) | 1.7 | 60 % → **56 %** | 27 % → 26 % | 1.24 → 1.18 |

### The targets (second age, seeds 1-10)

| target | payback | cheapest | random | saver | met |
| --- | --- | --- | --- | --- | --- |
| town's own share >= 75 % | 74 % | 76 % | 75 % | 75 % | **3 of 4; payback 1 point under** (before 66 / 69 / 63 / 71) |
| every trade's own >= 60 % | millers 88, reapers 70, weavers 62; **carriers 48, shepherds 36** | millers 91, reapers 77; **weavers 52, carriers 33, shepherds 35** | millers 95, reapers 69; **weavers 53, carriers 27, shepherds 27** | millers 83, reapers 78; **weavers 56, carriers 50, shepherds 44** | **no**: the carriers and shepherds (one member each, 4-5 % of the town) and on three bots the weavers |
| largest trade <= about 45 % | reapers 38-39 % | 41 % | 36 % | 40 % | yes |
| hats <= about 3 a minute | 1.31 | 0.90 | 1.22 | 1.21 | yes |
| herding no higher than today (26 / 19 / 24 / 22) | 20 % | 19 % | 19 % | 23 % | yes (saver 1 over, within the noise) |

What stands in the way of the rest: the carriers' and shepherds' work does not keep even one villager busy (well busy 1-2 %, pasture 15-21 %), so the one member a trade keeps is away most of the time; the weavers' cloth trip is the longest (pasture, weaver, landing or market) and loses to flour 1.6 times over about half the time. The lean was not raised.

### Bands (`bands --bell`, `77f5224`)

No band fails; the first age's lines are identical. Second age: age floor seed 5 random 20.53 min (**2.7 %**, the thinnest), ceiling seed 9 cheapest 29.64 (15.3 %), crossing wait 141 s (41.1 %), drawbridge to seal 4.97 min (41.9 %), landing share seed 9 cheapest 0.446 (33.9 %), map gap 170 s (19.0 %), nothing affordable 40 s (32.5 %), toll payback all runs 9.4 %, single run 100.4 %, tolls before seal 3 (50 %); the ford bands 49-691 %. **Room for the boats' rebuild:** the age's floor has only 0.53 minute (anything that speeds the richest towns by more tips it), the toll payback 9.4 %.

### Tests this round

`TownTrades2Tests` follows the shipped rule: no porters (`TheSecondAgesJobsBelongToTheirTrades`, `BoatTripsAreOwnedByThePorterAndByTheirGoodsTrade` now assert V1: the Boat trade closed, boat trips the reapers' and millers'); `TheTownFillsTheNewTrades` checks each open trade holds its second-age floor scaled to the town (less one) instead of the porters' floor. `TownTradesTests.JobsBelongToTrades`: back to step 2's assertion for `GrainBoat` → Grain and `FlourBoat` → Flour (old, this step's first round: → Boat; why: no porters). All tests added in this step; nothing from before was loosened.

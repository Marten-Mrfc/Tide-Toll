# Trades, step 3, part 1: the second age's trades in the simulation (progress)

**Names commit for the second builder: `a5f0660`** ("Snapshot: trades 2, the names"). Rules: `c924c80` and after.

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

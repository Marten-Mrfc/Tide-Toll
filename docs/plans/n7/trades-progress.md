# Trades, step 2, part 1: the simulation and its tuning (progress)

## Where it stands (read this first)

- **Names commit for the second builder: `a2ce2a5`.** Rules and tuning: `2d2bac4` and after.
- **sim.tests: 297 of 298 pass.** The one failure is `TownRulesV3Tests.ThePaybackBotDoesNotAlwaysPickTheSameSide`: with trades the payback bot takes Stronger backs at the first choice on all 10 seeds (was at most 8). It is not a constant of trades that can bring it back (below, "Not done").
- **Every band of `TownPacingTests` and `TownRiverPacingTests` passes**, but the first age does **not** keep the margins the retune left: the charter ceiling 4.1 % to 1.5 %, the charter floor 3.2 % to 0.6 %, the requests average 7.7 % to 3.0 %. The second age has more room than before (floor 5.0 % to 8.2 %).
- The pinned run (trades off, every villager a Helper) is tick for tick the run before trades.
- Additions to the sheet's names, made while building the rules: `TradeView` for `Trade.None` is the Helpers' line; `town.TradeEarnPerHead(t)`, `town.BestPayJob(id)` (the best-pay rule's choice, for the careful player and tools), `TownTradePlayer` / `TradePlay` (the careful and careless players), `TownRules.TradeFloorShare`.

Branch `wt/n7-trades`. Brief: `docs/plans/n7/trades-build-sheet.md` part 1 (the simulation), part 4 (the tuning) and the `sim.tests` of "Tests and pictures".

## The names commit (the second builder starts here)

**Commit `a2ce2a5`** ("Snapshot: trades, the names").  In that commit trades do nothing yet: the lean is 1, every bonus 0, the town does not fill trades yet (the rules come in the next commits), the Training row is listed but locked ("Comes with the first trade") while no villager has a trade. Every test passes and the pinned run is identical.

### The names as built

```csharp
public enum Trade : byte { None = 0, Water = 1, Grain = 2, Flour = 3 }    // sim/Town/TownTrades.cs; None = a Helper

// VillagerState:  Trade Trade;  bool TradeByPlayer;                         (set on its card by the player)
// TownState:      bool TownFillsTrades = true;  bool[] TradeCountByPlayer (by Trade, index 0 = the Helpers);  int Training;
//                 int[] TradeWant (the count the player set);  double[] TradeEarn (members' coin, about the last minute);
//                 double TradeMoveAt;  long TradeChanges, TradeChangesByTown   (counters)

public struct TradeView { bool Open; int Members, Places, Away, Waiting; bool ByPlayer; double WorkBonus, WorthBonus; }

town.TradeOpen(Trade t)                 // its workplace exists (Water from the start, Grain with the first field, Flour with the mill)
town.TradesShown                        // true from the first field on
town.TradeInfo(Trade t)                 // TradeView; TradeInfo(Trade.None) is the Helpers' line (Places 0, no bonus)
town.Helpers                            // villagers with no trade
town.TownFillsTrades                    // the switch as it stands
town.VillagerLikes(int i)               // the trade villager i likes (None while that trade is not open)
VillagerView.Trade, .TradeByPlayer, .Likes
Town.TradeOfJob(JobKind j)              // static; wool and cloth jobs: None (their trades come with step 3)
Town.TradeOfLeaning(Leaning l)          // static
town.TradeWorkBonus, town.TradeWorthBonus   // fractions, Training included, without the like
town.TradeEarnPerHead(Trade t)          // coin a minute a head of the trade's work lately (the figure the town fills by)   [rules commit 2d2bac4]
town.TrainingStepBonus                  // what one more Training step adds to both bonuses (0.04 = +4 %; 0 after ten)      [second round, see below]

town.SetTradeCount(Trade t, int n)      // yours from now on (Trade.None: the Helpers' count); false if nothing changed
town.SetVillagerTrade(int id, Trade t)  // that villager is yours from now on
town.SetTownFillsTrades(bool on)        // on: every count is the town's again (villagers set one by one stay set)
town.SetOpenCard(int id)                // the game says whose card is open (-1 none); the town never moves that villager

TownEventKind.TradeChanged = 33         // Villager, Index = (int)new trade, Amount = (int)old trade, Level = 1 by the player / 0 by the town, X, Y
ShopItemKind.Training = 22              // one row, ten steps (TownRules.TrainingMax); town.Training, town.TrainingPrice,
                                        // town.TrainingLockReason, town.BuyTraining(); listed from TradesShown on, after Load
```

Reasons on the Training row: "Comes with the first trade" (locked: nobody has a trade), "All training done" (finished).

### Game code touched (only what the shop needs to list the row)

- `game/scripts/town/TownHud.Shop.cs`: `Training` in the People group, rank 35 (after Load, no key), a track (its step number shows).
- `game/scripts/town/TownHud.ShopText.cs`: its line "Better at their trades" and help sentence (stand-ins; the interface builder writes the real ones).
- `sim/Town/TownPacing.cs`, `TownRiverPacing.cs`: `ByKind` arrays sized 23 (Training = 22).

## The pinned run

`sim.tests/Town/TownTradesPinTests.cs`: seeds 1 to 3, the payback and the cheapest bot, the first age to the charter, rung in, 8 minutes of the second age; a SHA-256 of coin, renown, level, purchases and every villager's job, phase and position every 10 ticks. Recorded on the main branch (`70922b8`) before any change (the same test run on `git archive` of it passes); with `SetTownFillsTrades(false)` at the start (every villager a Helper) the branch must give the same hashes.

## The rules as they stand (`sim/Town/TownTradeRules.cs`, `TownPeople.cs` `Leaned`)

1. **The lean.** In `Consider` and `RConsider` a job of the villager's own trade counts `TradeLean` (1.6) times its rate; a Helper weighs every job at 1. A field read in `Leaned` (no delegate). Wool and cloth jobs belong to no trade yet (step 3), so in the second age everyone weighs them at 1.
2. **The reward** (`TradeWorkFactor`, `TradeWorthFactor`). On a job of its own trade a villager's work goes `1 + TradeWork + Training x TradeStep` times as fast (applied where `WorkLeft` falls, so the field, the well and the mill of a flour trip), and the delivery is worth `1 + TradeWorth + Training x TradeStep` more (`Deliver`, `DeliverLanding`); in the trade it likes, `TradeLikes` more on both. Away from its trade, 1. A Helper, 1 (so the pinned run is exact: multiplying by 1.0).
3. **The figure the town fills by.** Each delivery's coin counts to the trade whose work it was, whoever made it (`TradeEarn`); each second each trade's member-seconds are added (`TradeAt`); both decay by exp(-1/60) a second. Coin a minute a head = `TradeEarn / TradeAt x 60` (a trade with under 30 member-seconds behind it counts as best paid, so a newly opened trade is tried). Why "whoever made it": when others do a trade's work that trade has too few members, when its members go elsewhere it has too many. Tried first and dropped: the members' own earnings (all jobs) and the members' earnings at their trade over their members: both kept one reaper beside fifteen carriers from minute 6 to 12 (a lone grain member's figure is lumpy and requests pull it to the well), herding 27-31 % in the first age.
4. **The town fills** (each game second, while `TownFillsTrades`): every Helper above the player's Helper count takes a trade: first the open town trade furthest under its **floor** (`TradeFloorShare` = 3 villagers a place of its workplaces, scaled down when the town is smaller than all the places), else the one with the best figure (thinned by the Helpers already given it that second, so newcomers spread); the Helper who likes it first, then the lowest number. An open town trade with nobody in it takes one at once from the town trade with most members above its floor.
5. **The rebalance**: at most one move every `TradeMoveEvery` (20 s): into the town trade furthest under its floor from the one with most above its floor; else from the trade with the worst figure (with more than its floor and at least 2) to the best, when the best is over `1 + TradeMoveGap` (1.2) times the worst. Never a villager set on its card, never the open card's (`SetOpenCard`), never into or out of a trade whose count the player set; a trade keeps at least one member.
6. **Your count** (`SetTradeCount`): the count is yours (`TradeCountByPlayer`); up takes Helpers first (the one who likes it first, then the lowest number), then a member of the largest town-decided trade that keeps one; down makes members Helpers (one set on its card last, then the one who likes it least, then the highest number) and the Helpers' count becomes yours, so the town does not fill them again. `SetVillagerTrade` sets one villager for good. `SetTownFillsTrades(true)` hands every count back.
7. **Training**: one row "Training", after Load, ten steps; price `ceil(TrainingMinutes x max(TrainingRateMin, best coin a minute) x TrainingGrowth^steps)`; locked "Comes with the first trade" while nobody has a trade, "All training done" at ten. Its worth for the bots: `rate x share of income from trades' work x (step / (1 + worth bonus) + 0.3 x step / (1 + work bonus))`.
8. **Saves**: the new fields default to every villager a Helper, the town filling, no counts set, Training 0; versions 2 and 3 kept. An old town's villagers take trades within its first second, so its pace from then on differs a little from what it would have been.
9. **Determinism and cost**: by index, no sets or dictionaries, `stackalloc` spans of 4; nothing allocates in the step (tested with a rebalancing move inside the window). The careful player's `BestPayJob` runs the choice once per villager a minute (outside the rules).

## The constants (`TownRules`, with these reasons at the constants)

| constant | value | reason |
| --- | --- | --- |
| `TradeLean` | 1.6 | at 1.5 the second age's members made 66-70 % of their trips at their trade (target 70); at 1.75 the first age's requests fell under half met (0.495) and the payback bot had minutes of 2 purchases early (seed 8, it waits longer for the Load step); 1.6 is the lowest that held both. Herding falls as the lean rises (1.5: 18/18/15/18 %; 1.75: 12/10/9/11 % first age). |
| `TradeWork`, `TradeWorth` | 0.12, 0.05 | they give back what the lean costs: with no bonus the bots reached the charter about 15 % later. 0.12/0.05 bring every bot's mean charter within 0.5 minute of before (table below). Work 0.10: a payback minute of 3 purchases early (seed 8) and the landing share under a third (seed 9 random); 0.15: the saver under 25 minutes (seed 1) and requests under half met. |
| `TradeLikes` | 0.05 | the sheet's "+5 %"; 0 took seed 3 random over 40 minutes and requests to 0.456; 0.1 seed 1 saver under 25. |
| `TradeStep` | 0.04 | at 0.03 the saver's last stretch (level 15 to the charter, mean 2.96 min) was longer than the longest of the three gaps before it (2.93), `TheLastGapIsNoLongerThanTheOnesBeforeIt`; at 0.04, 2.91 against 3.10. The tooltip example "3%" should read 4 %. |
| `TradeMoveEvery`, `TradeMoveGap` | 20 s, 0.2 | the sheet's values; 30 s tipped three first-age bands (floor, an early minute, requests), 10 s doubled the hat changes (about 4 a minute). |
| `TradeFloorShare` | 3 | without a floor the herding rose (above); 3 gives one working and two on the road a place; at 2 the saver's last stretch grew longer than the gaps before it (3.22 against 3.02); at 1, five bands failed. |
| `TrainingMinutes`, `TrainingGrowth`, `TrainingRateMin` | 0.5, 1.45, 20 | at 1.5 minutes no bot bought it in 40 first-age runs; at 0.5 it is bought about once a run in the first age (31 steps in 40 runs) and goes on in the second. |
| `TrainingWorkShare` | 0.3 | the share of a trip that is work, as the Tools estimate uses. |
| `CharterMinutes` | 10 (unchanged) | see "The first age's room" below. |

## The first age's room (item 6)

- The charter rule was **not** replaced. Tried: the charter's renown above the renown held at level 15 easing by `e` a minute (the second age's rule), with `CharterMinutes` 12 to 40 and `e` 0.04 to 0.3: it made every bot faster, the saver included (lean 1.75: the saver's floor run 24.04 min at the old rule, 23.41 at 16 / 0.1, 23.72 at 40 / 0.3), because the fastest towns' charter is not set by that renown (the charter drive pays 65 % of what is left and the saver's big purchases carry it over). The code was removed.
- `CharterMinutes` 9 shortens the last stretch (the saver's 2.82 against 2.93) but changes the run for every town, trades off too, and so breaks the pinned run; it was not shipped. `TradeStep` 0.04 does the same within trades.
- The first-age bands are noisy at the level of single runs: on seeds 11 to 30 (not in the tests) the main branch itself fails four bands (charter floor seed 27 saver 23.71; an early minute of 3, seed 16 payback; requests single run seed 22 cheapest 1.00; landing share seed 17 saver 0.232); with trades as shipped, five (charter floor seed 22 saver 24.40, an early minute seed 13 payback, requests average 0.484 and single run seed 16 payback 0.17, landing share seed 14 random 0.250). Early minutes under 4 purchases (not the saver): 2 of 60 runs before, 3 of 60 with trades.

## Tables: per bot, before (the main branch, `people-before`) and after (trades left alone)

From `people --seeds 1-10 --river --bot four [--play alone|careful|carriers|helpers]`. Seal minutes after the ring. Herd = share of 10-second samples with more than three quarters of the villagers on one job. Own = share of the trips by villagers with a trade made at it. Hats/min = the town's trade changes a minute.

- before: Payback charter 29.30, seal 25.07, herd 22 % / 44 %; Cheapest 36.71, 27.64, 16 % / 33 %; Random 35.25, 26.88, 15 % / 35 %; Saver 27.08, 25.95, 22 % / 40 %.
- after: Payback charter 29.46, seal 25.75, herd 16 % / 26 %, own 91 % / 69 %, hats 1.63 / 0.49; Cheapest 36.72, 28.08, 16 % / 19 %, own 92 % / 74 %, hats 1.44 / 0.18; Random 35.76, 25.21, 17 % / 24 %, own 91 % / 70 %, hats 1.59 / 0.23; Saver 27.04, 26.54, 15 % / 22 %, own 92 % / 74 %, hats 1.76 / 0.49.
- careful (every minute the counts the best-pay rule would choose): Payback 29.50 / 24.22; Cheapest 37.01 / 27.77; Random 35.45 / 25.29; Saver 26.88 / 24.12 (at most 1.1 % sooner than left alone; the sheet allows 8 %). Herding 28-37 % / 39-46 %: following the best-pay rule's picks herds.
- careless, every villager a carrier: Payback 29.24 / 25.08; Cheapest 35.86 / 28.49; Random 34.46 / 25.32; Saver 27.20 / 25.37 (no slower than left alone in the first age: carriers still take other work above 1.6 times the pay; second-age coin a minute 5-20 % lower). Never an idle villager (`CarefulAndCarelessStayNearLeftAlone`: under 1 % idle samples).
- careless, every villager a Helper with the town's filling off: exactly the before figures.

Targets (part 4 item 4): own trade at least 70 %: first age 91-92 %, second age 69-74 % (payback 69 %: the second age's wool and cloth have no trade until step 3, so every trip to them is "away"). Herding well under today's: second age 19-26 % against 33-44 %; first age 15-17 % against 15-22 % (equal for the cheapest and random bots, not under). Hat changes by the town: 1.4-1.8 a minute in the first age, 0.2-0.5 in the second (target at most about 3).

### Per seed (before → after, left alone)

| seed | bot | charter min | buys 1st | herd 1st | seal min | buys 2nd | coin/min 2nd | herd 2nd | own 1st / 2nd | hats/min 1st / 2nd |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | payback | 31.2 → 29.4 | 190 → 187 | 25 % → 13 % | 21.1 → 22.4 | 210 → 220 | 56601 → 55179 | 55 % → 33 % | 94 % / 53 % | 1.66 / 0.45 |
| 2 | payback | 30.1 → 32.8 | 183 → 194 | 16 % → 12 % | 24.6 → 21.6 | 191 → 205 | 39304 → 55534 | 28 % → 17 % | 93 % / 61 % | 1.68 / 0.51 |
| 3 | payback | 29.9 → 29.1 | 185 → 190 | 16 % → 17 % | 26.2 → 25.7 | 176 → 195 | 34524 → 41803 | 48 % → 19 % | 92 % / 71 % | 1.82 / 1.01 |
| 4 | payback | 29.5 → 30.7 | 187 → 187 | 25 % → 16 % | 22.8 → 26.1 | 201 → 195 | 51980 → 38224 | 55 % → 33 % | 90 % / 67 % | 1.76 / 0.38 |
| 5 | payback | 27.3 → 29.2 | 186 → 187 | 27 % → 19 % | 21.0 → 23.2 | 225 → 198 | 65468 → 44149 | 60 % → 50 % | 91 % / 58 % | 1.47 / 0.47 |
| 6 | payback | 29.8 → 28.9 | 187 → 187 | 15 % → 7 % | 27.0 → 28.0 | 198 → 184 | 36383 → 32807 | 57 % → 24 % | 93 % / 72 % | 1.62 / 0.36 |
| 7 | payback | 28.5 → 26.2 | 183 → 175 | 23 % → 30 % | 25.3 → 27.8 | 193 → 195 | 40903 → 33496 | 36 % → 22 % | 92 % / 78 % | 1.56 / 0.58 |
| 8 | payback | 28.3 → 30.9 | 186 → 184 | 28 % → 17 % | 29.3 → 27.7 | 223 → 187 | 44739 → 31456 | 44 % → 30 % | 89 % / 72 % | 1.72 / 0.40 |
| 9 | payback | 28.7 → 29.6 | 187 → 187 | 19 % → 17 % | 29.9 → 29.9 | 162 → 165 | 25418 → 25669 | 16 % → 15 % | 91 % / 81 % | 1.52 / 0.23 |
| 10 | payback | 29.6 → 27.6 | 185 → 181 | 22 % → 10 % | 23.5 → 24.9 | 193 → 210 | 43232 → 43838 | 48 % → 17 % | 90 % / 69 % | 1.49 / 0.56 |
| 1 | cheapest | 37.2 → 36.6 | 208 → 206 | 18 % → 14 % | 28.0 → 26.4 | 169 → 176 | 23204 → 27559 | 43 % → 17 % | 94 % / 68 % | 1.40 / 0.19 |
| 2 | cheapest | 38.3 → 37.9 | 206 → 207 | 19 % → 9 % | 27.2 → 27.2 | 171 → 176 | 24121 → 25674 | 28 % → 14 % | 95 % / 77 % | 1.35 / 0.22 |
| 3 | cheapest | 37.5 → 39.4 | 201 → 210 | 6 % → 12 % | 30.0 → 29.2 | 155 → 169 | 18268 → 22407 | 27 % → 11 % | 94 % / 79 % | 1.14 / 0.14 |
| 4 | cheapest | 36.1 → 36.2 | 206 → 210 | 18 % → 14 % | 26.9 → 27.0 | 162 → 174 | 22577 → 26542 | 32 % → 24 % | 92 % / 72 % | 1.36 / 0.15 |
| 5 | cheapest | 36.3 → 36.1 | 206 → 206 | 17 % → 13 % | 24.7 → 26.0 | 189 → 174 | 32985 → 26654 | 44 % → 15 % | 92 % / 64 % | 1.55 / 0.23 |
| 6 | cheapest | 36.4 → 34.7 | 205 → 205 | 14 % → 17 % | 29.0 → 28.8 | 163 → 163 | 20905 → 22338 | 35 % → 28 % | 93 % / 70 % | 1.50 / 0.28 |
| 7 | cheapest | 34.5 → 34.5 | 204 → 205 | 19 % → 19 % | 28.3 → 28.0 | 171 → 177 | 23248 → 26098 | 31 % → 21 % | 89 % / 75 % | 1.71 / 0.14 |
| 8 | cheapest | 37.5 → 36.5 | 207 → 204 | 21 % → 21 % | 29.0 → 30.8 | 163 → 175 | 21457 → 23316 | 20 % → 16 % | 90 % / 80 % | 1.40 / 0.13 |
| 9 | cheapest | 37.0 → 38.7 | 204 → 209 | 12 % → 13 % | 29.9 → 32.0 | 162 → 153 | 19099 → 17079 | 18 % → 17 % | 90 % / 78 % | 1.55 / 0.12 |
| 10 | cheapest | 36.4 → 36.8 | 204 → 206 | 18 % → 23 % | 23.5 → 25.4 | 193 → 174 | 36104 → 27366 | 55 % → 26 % | 93 % / 68 % | 1.50 / 0.24 |
| 1 | random | 35.1 → 33.9 | 205 → 202 | 13 % → 18 % | 27.5 → 24.9 | 181 → 183 | 27527 → 31151 | 32 % → 26 % | 91 % / 71 % | 1.71 / 0.16 |
| 2 | random | 37.8 → 37.9 | 202 → 207 | 17 % → 2 % | 27.1 → 23.9 | 180 → 209 | 25335 → 41366 | 32 % → 33 % | 95 % / 72 % | 1.34 / 0.25 |
| 3 | random | 37.0 → 38.4 | 205 → 210 | 8 % → 16 % | 29.5 → 27.9 | 167 → 182 | 23551 → 27485 | 22 % → 22 % | 93 % / 78 % | 1.33 / 0.18 |
| 4 | random | 35.1 → 35.5 | 207 → 207 | 19 % → 18 % | 24.2 → 23.5 | 190 → 204 | 34252 → 42004 | 46 % → 26 % | 91 % / 72 % | 1.35 / 0.17 |
| 5 | random | 33.9 → 37.5 | 204 → 209 | 16 % → 18 % | 24.7 → 22.9 | 197 → 187 | 35796 → 38109 | 34 % → 33 % | 91 % / 57 % | 1.57 / 0.26 |
| 6 | random | 34.8 → 33.0 | 205 → 202 | 7 % → 12 % | 26.0 → 24.2 | 175 → 194 | 28744 → 35205 | 29 % → 20 % | 92 % / 62 % | 1.70 / 0.17 |
| 7 | random | 32.5 → 34.9 | 202 → 206 | 22 % → 16 % | 28.9 → 24.9 | 195 → 199 | 29633 → 37042 | 31 % → 19 % | 85 % / 72 % | 2.01 / 0.12 |
| 8 | random | 34.6 → 34.4 | 204 → 202 | 17 % → 30 % | 27.9 → 26.3 | 193 → 194 | 30216 → 32025 | 36 % → 18 % | 89 % / 67 % | 1.51 / 0.27 |
| 9 | random | 36.4 → 36.8 | 204 → 206 | 13 % → 14 % | 31.4 → 28.2 | 144 → 169 | 15004 → 23691 | 44 % → 22 % | 89 % / 74 % | 1.71 / 0.14 |
| 10 | random | 35.2 → 35.3 | 204 → 205 | 20 % → 24 % | 21.6 → 25.4 | 182 → 187 | 35422 → 31221 | 48 % → 26 % | 91 % / 68 % | 1.70 / 0.59 |
| 1 | saver | 27.3 → 25.6 | 172 → 169 | 37 % → 15 % | 21.3 → 24.1 | 182 → 210 | 52773 → 49927 | 46 % → 19 % | 92 % / 68 % | 1.80 / 0.46 |
| 2 | saver | 28.5 → 27.4 | 172 → 167 | 15 % → 14 % | 25.7 → 25.7 | 165 → 202 | 34325 → 43789 | 38 % → 12 % | 93 % / 76 % | 1.79 / 0.51 |
| 3 | saver | 26.9 → 27.4 | 171 → 172 | 18 % → 14 % | 27.4 → 27.9 | 173 → 176 | 31202 → 30752 | 52 % → 37 % | 95 % / 75 % | 1.75 / 0.47 |
| 4 | saver | 27.4 → 28.4 | 171 → 171 | 23 % → 14 % | 22.6 → 23.7 | 189 → 199 | 47628 → 46936 | 55 % → 32 % | 89 % / 67 % | 1.62 / 0.42 |
| 5 | saver | 26.4 → 25.8 | 170 → 165 | 29 % → 16 % | 24.5 → 23.3 | 185 → 195 | 41851 → 45169 | 34 % → 24 % | 91 % / 56 % | 1.82 / 0.69 |
| 6 | saver | 27.5 → 27.6 | 171 → 176 | 8 % → 5 % | 28.6 → 28.9 | 169 → 185 | 29377 → 33098 | 33 % → 22 % | 95 % / 77 % | 1.70 / 0.66 |
| 7 | saver | 25.8 → 25.1 | 167 → 165 | 24 % → 27 % | 27.5 → 26.7 | 181 → 193 | 34390 → 37268 | 35 % → 15 % | 91 % / 77 % | 1.87 / 0.60 |
| 8 | saver | 27.4 → 29.4 | 169 → 176 | 26 % → 14 % | 26.3 → 31.4 | 193 → 160 | 39367 → 22895 | 49 % → 27 % | 92 % / 77 % | 1.63 / 0.29 |
| 9 | saver | 26.9 → 27.0 | 173 → 176 | 20 % → 17 % | 30.2 → 30.2 | 172 → 175 | 27133 → 29717 | 21 % → 13 % | 90 % / 84 % | 1.85 / 0.23 |
| 10 | saver | 26.6 → 26.7 | 176 → 172 | 27 % → 13 % | 25.3 → 23.5 | 202 → 200 | 45263 → 52606 | 42 % → 16 % | 91 % / 66 % | 1.80 / 0.72 |

## Every band: nearest run and margin, before and after

From `bands --bell` (before: `a2ce2a5`'s parent `70922b8`, built from the same tool; after: this branch). Margin as a share of the band's value; "thin" under 3 %.

| band (test) | nearest run before | margin before | nearest run after | margin after |
| --- | --- | --- | --- | --- |
| 1 bell average 0.10-0.25 | 0.118 | 18.3 % | 0.122 | 21.6 % |
| 1 bell single 0.05-0.35 | seed 4 minute 10 0.065 | 29.3 % | seed 10 minute 20 0.170 | 51.5 % |
| 1 bots' spread <= 2 | seed 3 1.39 | 30.4 % | seed 5 1.46 | 27.2 % |
| 1 charter 25-40 min (ceiling) | seed 2 cheapest first 38.34 min | 4.1 % | seed 3 cheapest first 39.39 min | 1.5 %  thin |
| 1 charter 25-40 min (floor) | seed 7 saver 25.80 min | 3.2 % | seed 7 saver 25.15 min | 0.6 %  thin |
| 1 every kind, load step, building (misses) | seed 1 best payback first 0 | 0.0 %  thin | seed 1 best payback first 0 | 0.0 %  thin |
| 1 first coin <= 25 s | seed 5 cheapest first 16.5 s | 34.0 % | seed 5 cheapest first 16.4 s | 34.4 % |
| 1 first purchase <= 45 s | seed 5 best payback first 31.5 s | 30.0 % | seed 5 best payback first 31.0 s | 31.1 % |
| 1 map gap <= 210 s | seed 3 saver 154 s | 26.7 % | seed 9 random affordable 150 s | 28.8 % |
| 1 nothing affordable <= 60 s | seed 2 cheapest first 27 s | 55.0 % | seed 4 cheapest first 28 s | 54.2 % |
| 1 paving matters (seed 1) >= 15 % slower | cheapest first 54.2 vs 37.2 | 206.4 % | cheapest first 53.3 vs 36.6 | 205.6 % |
| 1 purchases a minute, early >= 4 | seed 1 cheapest first 4 in minute 1 | 0.0 %  thin | seed 1 cheapest first 4 in minute 1 | 0.0 %  thin |
| 1 purchases a minute, late >= 2 | seed 2 random affordable 2 in minute 35 | 0.0 %  thin | seed 1 cheapest first 2 in minute 24 | 0.0 %  thin |
| 1 requests average 0.5-0.8 | cheapest first 0.738 | 7.7 % | best payback first 0.515 | 3.0 %  thin |
| 1 requests single run 0.3-0.95 | seed 4 cheapest first 0.93 | 2.3 %  thin | seed 3 best payback first 0.33 | 11.1 % |
| 1 saver gap <= 90 s | seed 1 saver 58 s | 36.1 % | seed 8 saver 54 s | 40.0 % |
| 1 track share <= 0.4 | seed 2 random affordable 0.269 | 32.8 % | seed 2 cheapest first 0.267 | 33.3 % |
| 2 age 20-35 min, buyers (ceiling) | seed 9 random affordable 31.35 min | 10.4 % | seed 9 cheapest first 32.02 min | 8.5 % |
| 2 age 20-35 min, buyers (floor) | seed 5 best payback first 21.00 min | 5.0 % | seed 2 best payback first 21.64 min | 8.2 % |
| 2 crossing wait <= 240 s | seed 8 cheapest first 141 s | 41.1 % | seed 4 cheapest first 187 s | 22.2 % |
| 2 drawbridge to seal >= 3.5 min | seed 3 best payback first 4.78 min | 36.7 % | seed 3 cheapest first 5.03 min | 43.8 % |
| 2 each crossing carries more | seed 1 cheapest first | 100.0 % | seed 1 cheapest first | 100.0 % |
| 2 ford crossings, all runs >= 300 | 1367 | 355.7 % | 718 | 139.3 % |
| 2 ford high-water wait, all runs >= 1000 | 9797 | 879.7 % | 6311 | 531.1 % |
| 2 ford only >= 2 min | seed 5 random affordable 3.26 min | 63.0 % | seed 2 best payback first 2.99 min | 49.7 % |
| 2 ford used on seeds >= 4 | 10 | 150.0 % | 9 | 125.0 % |
| 2 landing share >= 1/3 | seed 9 random affordable 0.414 | 24.1 % | seed 9 saver 0.442 | 32.6 % |
| 2 map gap <= 210 s | seed 6 saver 170 s | 19.0 % | seed 6 cheapest first 170 s | 19.0 % |
| 2 nothing affordable <= 60 s | seed 6 cheapest first 44 s | 27.5 % | seed 6 cheapest first 46 s | 24.2 % |
| 2 purchases a minute >= 2 | seed 9 random affordable 4.59 | 129.7 % | seed 9 cheapest first 4.78 | 138.9 % |
| 2 saver buys every new kind (misses) | seed 1 saver 0 | 0.0 %  thin | seed 1 saver 0 | 0.0 %  thin |
| 2 seal (every run) | seed 1 cheapest first | 100.0 % | seed 1 cheapest first | 100.0 % |
| 2 toll steps back, all runs >= 1.0 | spent 983975 earned 1140750 | 15.9 % | spent 983975 earned 1136250 | 15.5 % |
| 2 toll steps back, single run >= 0.35 | seed 2 random affordable spent 24599 earned 15750 | 82.9 % | seed 1 cheapest first spent 24599 earned 17250 | 100.4 % |
| 2 tolls before seal >= 2 | seed 2 random affordable 3 | 50.0 % | seed 1 cheapest first 3 | 50.0 % |

Not a real margin ("0.0 %" on a count): the pass/fail rows and the purchases-a-minute floors, which sat at exactly the floor on the main branch too.

**Room left for the boats' changes (item 7):** the second age's floor (20 min) has 1.6 minutes on its nearest run (seed 2 payback 21.64), the ceiling (35) 3.0 minutes (seed 9 cheapest 32.02); `LevelMinutesScale` was not touched (k on it moves every seal by about ln(k) / 0.06 minutes). Expect one more `bands` pass after both are merged.

## Tests

Added (`sim.tests/Town/TownTradesPinTests.cs`, `TownTradesTests.cs`):
- `WithTradesOffARunIsTheRunBeforeTrades` (seeds 1-3, payback and cheapest, both ages): hashes recorded on the main branch.
- `JobsBelongToTrades`; `AMemberWeighsItsOwnTradeTradeLeanTimes` (the lean); `TheRewardAtItsOwnTrade` (factors, Training, and in play: a carrier's first delivery pays the factor more and comes sooner); `TheTownGivesEveryVillagerATrade` (within a second, every open trade keeps a member); `TheRebalanceIsSlowAndLeavesThePlayersAlone` (one move per 20 s, never the card's villager, never the open card's, never into or out of a count the player set); `YourCountAndOneVillagersTrade`; `HelpersYouMakeStayHelpers`; `TrainingIsOneRowOfTenSteps`; `SameSeedAndCallsGiveTheSameTown`; `StepsAllocateNothingWithTradesOn` (a rebalance inside the window); `OldSavesLoadWithHelpersAndTheTownFills` (v2 and v3); `ViewsSayTheTradeAndTheLike`; `CarefulAndCarelessStayNearLeftAlone` (seeds 1-3, payback).

Replaced: `TownRulesV3Tests.TheCharterDriveComesAfterTheSecondToLastLevelAndIsMetInParts`. Old: a one-villager town lifted to level 15 delivers to the drive within 20 minutes. New: the same, after the villager's trade is set to the drive's good, as a player would. Why: the lone villager is a carrier and leaves water only for 1.6 times the pay, so it never answered a grain or flour drive; the test is about the drive's parts, not the choice of work.

Failing: `TownRulesV3Tests.ThePaybackBotDoesNotAlwaysPickTheSameSide` ("choice 0: side A 0, side B 10 of 10"). Wider lanes' worth is `rate x (0.1 + crowded share) x 0.6 x (1 - 1/1.5)` against Stronger backs' `rate x 0.55 x 0.045`; uncrowded, lanes are worth 0.81 of backs, so lanes win only where the roads are crowded at level 4. With trades the town is spread over the well's and the fields' roads and no seed is crowded then. No trades constant changes it (lean, bonuses, floors 1 to 3: 10 of 10 every time), nor `BoonLanes` 0.5 to 0.7 (still backs); above about 0.75 lanes would win every time. The lead's decision: the boon pair's worth or values.

## Check steps (result lines)

- `dotnet test sim.tests -c Release --filter "Category!=Gate&Category!=Perf"`: `Failed!  - Failed:     1, Passed:   297, Skipped:     0, Total:   298` (the one: `ThePaybackBotDoesNotAlwaysPickTheSameSide`, "choice 0: side A 0, side B 10 of 10"). Before: 276 of 276.
- game build: 0 errors, 0 warnings.
- game-town: `TOWN PASS faint=0.40 maxstep=0.008 worn=0.95 paved=0 boonclick=Wider lanes extended=0 level=6 bought=50 Pace:11,Villager:6,Regulars:15,Pave:1,Value:8,Tools:3,Training:1,...`
- game-save: `SAVE PASS autosave-clock=0.64/0.64s level 2 paved 1 villagers 4`
- game-playthrough (lock): `PLAY PASS charter=34.5min buys=201 paves=24 extends=14 bells=78 clicks=200 keys=107` (before `charter=33.9min buys=201`: +0.6 minute, the same purchases).
- game-river (lock): `RIVER PASS ... fordcrossings=119 checks=356 waiting=71 boats=769 cargo=407 fordwait=29/1059t raft=282 lift=9/245t tide=153/159 level=28 villagers=35`
- game-work (lock): `WORK RATES PASS` (60 fps `pairs=371 gap=1.93-2.31m ... bytes=570 villagerbytes=2`; 30 and 120 fps pass)
- game-people (lock): `PEOPLE RATES PASS` (60 fps `picked=21/21 shared=0 passpairs=15165 sidefails=0 ... villagerbytes=0 pickbytes=0`; 30 and 120 fps pass)
- Not run: `--filter Category=Gate` and `Category=Perf`, game-perf, game-motion, game-layout, game-ink (the interface and figures are the second builder's).

## How sensitive the first age is (neighbours of the shipped point)

One constant moved at a time from the shipped values; charter ceiling / floor margins, requests average margin, second-age floor margin, the saver's last stretch against the gaps before it, the payback bot's first choice:

| change | bands failing | ceiling | floor | requests | 2nd floor | saver last / before | choice 0 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| (shipped) | 0 | 1.5 % | 0.6 % | 3.0 % | 8.2 % | 2.91 / 3.10 | B 10 |
| TrainingMinutes 0.4 | 2 | -0.0 % | 2.6 % | 7.4 % | 9.0 % | 3.06 / 3.05 | B 10 |
| TrainingMinutes 0.7 | 0 | 1.5 % | 2.2 % | 6.8 % | 1.1 % | 2.98 / 3.05 | B 10 |
| TrainingGrowth 1.6 | 0 | 1.5 % | 0.6 % | 4.6 % | 6.0 % | 3.00 / 3.09 | B 10 |
| TradeMoveGap 0.15 | 1 | 2.4 % | 0.7 % | 0.5 % | 8.8 % | 3.21 / 3.04 | B 10 |
| TradeMoveGap 0.25 | 2 | 1.7 % | -0.2 % | 5.5 % | 8.2 % | 3.12 / 3.00 | B 10 |
| TradeWorth 0.06 | 3 | 0.4 % | -1.8 % | 4.5 % | 10.2 % | 3.16 / 3.08 | A 1, B 9 |
| TradeWork 0.13 | 0 | 1.4 % | 0.0 % | 1.1 % | 5.9 % | 3.01 / 3.01 | A 1, B 9 |

The single runs at the first age's edges move by a minute or more for small changes; no setting found keeps the retune's margins (4.1 % / 3.2 %) on both charter edges. Plainly: the first age passes its bands with trades on, thinly, and the next change to it (the boats are second-age only, but any first-age change) may tip one.

## Not done

- `ThePaybackBotDoesNotAlwaysPickTheSameSide` fails (above): needs the lead's decision on the lanes/backs pair.
- The first age's margins are not the retune's (above).
- The second age's members make 69-74 % of their trips at their trade (payback 69 %, target 70 %): wool and cloth have no trade until step 3.
- The first age's herding is about today's (15-17 % against 15-22 %), not well under; the second age's is well under.
- Parts 2 and 3 (interface, figures), `game-trades`, `game-layout`, pictures: the second builder's.

## Second round (the lead's decisions, 27 November evening)

### 1. The choice at level 4: `BoonWalk` cannot split it

The payback bot's first choice (Wider lanes A, Stronger backs B) on seeds 1 to 10, `bands --first-only --set BoonWalk=w`:

| BoonWalk | picks |
| --- | --- |
| 0.045 (today) | B 10 |
| 0.042 | B 10 |
| 0.040 | B 10 |
| 0.039 | B 10 |
| 0.038 | B 10 |
| 0.037 | B 10 |
| 0.0365 | B 10 |
| 0.0364 | B 10 |
| 0.03635 | A 10 |
| 0.0363 | A 10 |
| 0.0362 | A 10 |
| 0.036 | A 10 |
| 0.035 | A 10 |
| 0.030 | A 10 |

All ten seeds flip together between 0.03635 and 0.0364, which is where the backs' worth (rate x 0.55 x BoonWalk) meets the lanes' worth with no crowding (rate x 0.1 x 0.6 x (1 - 1/1.5), i.e. BoonWalk = 0.0364). With trades no seed has a crowded road at the moment of the choice, so the lanes' crowding term is 0 everywhere and nothing separates the seeds. **No value of `BoonWalk` between 3 and 4.5 percent makes the picks split; per the decision I stopped there.** `BoonWalk` is unchanged (0.045), the test still fails ("choice 0: side A 0, side B 10 of 10").

### 2. The pin

(a) The pin passes with only the trades added: at `ff77486` (same code as `2d2bac4`, the page only) `TownTradesPinTests`: `Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6`. (b) and (c) were not done: the constant did not change (above), so the pin was not recorded anew and still dates from `70922b8`.

### 3. Re-centring the first age: not done (the pin is not free)

Measured only, not shipped (it would change the trades-off run and so the pin, which the decision freed only for `BoonWalk`). `bands --set CharterMinutes=x`, everything else as shipped:

| CharterMinutes | bands failing | ceiling (nearest run, margin) | floor (nearest run, margin) | requests average | saver last stretch / longest before |
| --- | --- | --- | --- | --- | --- |
| 9 | 0 | seed 3 cheapest 39.05, 2.4 % | seed 7 saver 25.14, 0.5 % | 4.7 % | 2.69 / 3.10 |
| 9.5 | 0 | seed 3 cheapest 39.05, 2.4 % | seed 7 saver 25.14, 0.6 % | 3.0 % | 2.80 / 3.10 |
| 10 (shipped) | 0 | seed 3 cheapest 39.39, 1.5 % | seed 7 saver 25.15, 0.6 % | 3.0 % | 2.91 / 3.10 |
| 10.5 | 0 | seed 3 cheapest 39.39, 1.5 % | seed 7 saver 25.34, 1.4 % | 1.9 % | 3.08 / 3.10 |
| 11 | 0 | seed 3 cheapest 39.74, 0.6 % | seed 7 saver 25.47, 1.9 % | 1.9 % | 3.20 / 3.10 (fails the last-gap test) |

`CharterMinutes` hardly moves the floor's run (seed 7 saver 25.14 to 25.47 over 9 to 11: its charter is carried by the drive and its own purchases), so the best it offers is 9 (2.4 % / 0.5 %) or 10.5 (1.5 % / 1.4 %). If the pin is freed for it, 10.5 is the more even split; 9 gives the most room on the last-gap test.

How fragile: the margins of the first age's bands are those of single runs that move by a minute for small changes; on seeds 11 to 30 (not in the tests) the main branch itself fails 4 bands and the trades 5.

### 5. The Training tooltip's view

Added `town.TrainingStepBonus` (what one more step adds to both bonuses, 0 after the tenth); the tooltip's percentages: `TradeWorkBonus`, `TradeWorthBonus` (now) and `TrainingStepBonus` (one more step). No rule changed.

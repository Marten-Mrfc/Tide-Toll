# Town balance, round 3 (both ages, rules v3 and the second age)

Written 25 November 2026, 23:42 to about 00:00 UTC. Seeds 1, 2, 3, 7, 42, all four bots (cheapest first, best payback first, random affordable, saver). Second age played by `town --river` (first age to the charter, then rung in, to the borough seal). Only numbers changed, in `sim/Town/TownRulesRiver.cs`. Tool change: `town --set` also takes river fields (`River.Name=...`). No test edited.

## What changed

| Number | Before | After | Why |
| --- | --- | --- | --- |
| `BoatMinutes` | 0.8, 1.8 | 0.6, 1.4 | Boats asked for more than a town could carry in their patience: 10.4 of 21 boats left short on average and the slow seeds (3, 7, 9) filled 4 to 9 boats of 23 to 32. Smaller cargoes fill more boats (average filled 9.4 to 11.6 for random, 8.4 to 10.8 for cheapest), the age gets shorter at the slow end (worst 36.5 to 31.3 minutes). |
| `FerryPrice` | 3000 | 2000 | The first purchase of the age after the free gifts. With the town's coin rate at 4 to 8 thousand a minute in the slow seeds it left a long wait; this removes part of the "nothing affordable" spells and the map gaps. |
| `BerthPrice` | 8000 | 5000 | The cheapest-first bot never reached it (0 bought on 5 seeds); now every bot buys it on most seeds (a second berth is the one purchase that clearly lifts boat income). |
| `CraneBase` | 6000 | 4000 | Same reason: cheapest bought 1, now 2. The step is still +25 % cargo. |
| `TollBase` | 300 | 3000 | The toll was never worth buying: 300 coin a boat, 4 to 8 boats pass the drawbridge before the seal, so the whole age paid about 1 000 coin against 5 000 to 15 000 spent on steps. Now about 10 to 20 thousand a run (3 to 7 percent of the landing's 210 to 310 thousand): a small second income, not a main one. |
| `TollTrackBase` | 5000 | 2500 | The saver bought 0 to 2 toll steps; it now buys 7.6 on average, so "the saver buys every kind of new thing" is green on all of seeds 1 to 10. |

Tried and put back (all measured on seeds 1, 2, 3, 7, 42 and again on 1 to 10):
- `LevelRenown` of the river, flatter or lower tails (four shapes). Fewer map gaps (20 to 11) but the age shrinks to 21 to 24 minutes on average and 6 to 8 of 20 non-saver runs end under 20 minutes. Every shape that shortened the long levels shortened the fast seeds more. The original shape stays.
- `BoatPremium` 2.0: more income, but the age falls to 21.0 minutes on average (8 runs under 20).
- `BoatPatience` longer, `BoatGap` 10: no clear gain, sometimes worse (age 39 minutes on one seed).
- `PricesGrowth`, `RiverValueGrowth` 1.06 to 1.08: nothing affordable did not improve, map gaps moved at random.
- First age `LevelRenown` for levels 14 to 16 (to lift the saver's charter from 23.3 and 24.1 to 25): 50000/80000/150000 fixed the charter but made seeds 6, 8 miss purchases per minute and still left 5 misses; 52000/84000/160000 caused stalls of 135 to 330 seconds on seeds 1, 2, 4, 6, 9, 10 (25 misses). The first age is on a cliff there; not touched. The two first-age pacing misses stay as found (see below).

## Before and after (second age, mean over seeds 1, 2, 3, 7, 42)

| Bot | Age minutes (before / after) | Buys a minute | Longest nothing affordable, s (mean / worst) | Longest map gap, s | Landing share at mid | Boats come / filled / short | Coin: landing / toll / river market / town (after) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| cheapest first | 26.2 / 25.6 | 2.79 / 2.92 | 60 / 69 to 68 / 79 | 206 / 199 | 0.84 / 0.76 | 21.6/8.4/11.6 to 23.0/10.8/10.0 | 213k / 10k / 3.7k / 40k |
| best payback first | 22.1 / 20.7 | 3.76 / 4.02 | 45 / 53 to 35 / 40 | 195 / 155 | 0.85 / 0.81 | 18.0/7.4/8.8 to 19.0/10.6/5.4 | 308k / 20k / 2.0k / 37k |
| random affordable | 25.3 / 25.0 | 3.41 / 3.39 | 54 / 68 to 54 / 66 | 235 / 199 | 0.89 / 0.77 | 22.4/9.4/10.4 to 21.4/11.6/8.6 | 272k / 13k / 7.0k / 65k |
| saver | 26.7 / 24.2 | 3.11 / 3.39 | 44 / 51 to 35 / 41 | 223 / 147 | 0.79 / 0.74 | 22.4/8.4/11.6 to 21.0/12.2/7.4 | 298k / 12k / 0.8k / 51k |

Runs outside 20 to 35 minutes for the three buy-when-you-can bots: 5 of 15 before, 2 of 15 after (seed 1 payback 19.0 and seed 42 payback 18.4 / random 17.5 after; earlier seed 3 random 36.5 gone, now worst 31.3). Longest map gap above 210 s (target): 10 runs before, 3 after. Nothing affordable above 60 s: 4 before, 6 after (the slow seeds 3 and 7: 62 to 79 s; the cheaper ferry, berth and crane moved the stall later, not away).

Seeds 1 to 10 (40 runs, the tests' set), tool run: failing checks before 25 rows (20 map gap, 8 nothing affordable, 4 age, 1 landing share); after the changes the town test shows map gap on 8 runs, nothing affordable on 5, age out on a few, landing share on seed 9 (see Test state).

## Test state

Town tests (`--filter FullyQualifiedName~Town`, 85 tests): before 7 failed, 78 passed; after 6 failed, 79 passed. The one that turned green: `TheSaverBuysEveryKindOfNewThing` (the toll). Still red, all as found:
- `TownPacingTests.PurchasesKeepComing` (seed 10 cheapest: 1 purchase in minute 32; seed 10 payback: 3 in minute 8), `EveryBotFinishesTheAgeInTime` (saver charter 24.1 on seed 4, 23.3 on seed 7).
- `TownRiverPacingTests`: `TheSecondAgeTakes20To35MinutesForTheBuyers`, `SomethingNewOnTheMapEvery3AndAHalfMinutes` (8 runs of 214 to 286 s on seeds 5 to 10), `TheLandingEarnsAThirdOfAllCoinByTheMiddleOfTheAge` (seed 9 saver), `NeverMoreThan60SecondsWithNothingAffordable` (5 runs of 62 to 70 s).
No test changed. Whole fast suite once at the end: see the last section.

## Judgement

Choices of two. They split the bots, but not all four. Over seeds 1 to 10 cheapest first picks the first option of all four (AAAA) on every seed; payback, random and saver vary (payback BABB/AABB/BAAA...; random mixed; saver mostly B first, then A). So the choice is a real fork for the three bots that weigh things and a non-choice for the bot that always takes the cheapest. Worth estimates printed by `--worth` for payback (levels 4, 7, 10, 13; eight numbers per seed, seeds 1 to 10) differ by a point on the first pair (3 vs 4, 2 vs 3, 10 vs 3, 11 vs 3) and are far apart later (214 vs 196, 303 vs 336, up to 511 vs 384), so the first choice is nearly a coin toss and the last two matter. Fine as design (a first choice that costs nothing to get wrong), but a player who wants a "lanes or backs" decision at level 4 will find it small.

Charter drive. The last gap between levels (level 15 to 16) is no longer the longest: mean last gap 2.9 minutes against 3.8 for the longest earlier gap (cheapest), 2.3 against 2.8 (payback), 2.5 against 2.7 (random), 2.3 against 2.7 (saver), seeds 1 to 10. By purchases a minute the last four minutes are still the thinnest of the age (3.1 to 3.8 against 4.3 to 6.5 earlier); the busy part is the drive's deliveries, not the shop. For a player that is right, if the drive reads as a task; the bots do not show it.

Second age start. Level 17 (landing) is at once, level 18 (cottages) 0.6 to 1.1 minutes later, level 19 (ford and far lane) 1.3 to 2.5 minutes. So there is a new thing within the first minute on every run, then one every 40 to 70 seconds up to level 21 (the weaver, 2.2 to 4.2 minutes). Clear.

The tide. Period 240 s, high above 0.6, ford open below 0.4. A player feels it only if boats come in bunches at high water and the ford is the only crossing; once the ferry (level 22, 3.6 to 5.8 minutes in) and the footbridge (level 25) exist the tide stops mattering for walkers, and boats still come (21 a run, one a 15 s window at most 3). The bots cannot say if it is felt. My judgement: the tide is a rhythm for about the first six minutes of the age (queues at the ford, boats in bunches), then noise. See the rule changes below.

Crossings as steps up. Capacity at the seal per minute: ford 16 to 21, ferry 74 to 149, footbridge 213 to 278, drawbridge 242 to 316. By capacity each step is clearly up. By use, the ferry and footbridge are used by the same people: crossings per minute while the footbridge is newest is 14 to 87 against 3 to 53 at the ferry, but the drawbridge is not always above the footbridge (seed 7 random 26 vs 51 to 57), so only capacity is a real step; the player will see queues at the ford and ferry shrink (longest wait 7 to 72 s, one 135 s on a seed 9 payback run).

Boats and the landing. Landing share at the middle of the age is 0.74 to 0.89 (boats and the landing store): boats are the main income by a wide margin. That is a problem for the first age's work: the town (Water, Grain, Flour to the market) is 12 to 24 percent of second-age coin (40k of 270k to 350k), so the first age's mill and fields matter only as feeders of the landing. It is not pointless (Grain and Flour are the cargo), but a player will feel the market no longer matters. Wool and Cloth at the market (0.8k to 7k) are noise.

Never worth buying. Before: Toll (fixed). Still weak: Winch (3000 base, 4 steps bought by everyone, it only shortens a lift of 4 s for 4 to 8 boats), Raft and Rope on the ferry (no one needs a ferry once the footbridge comes at level 25; bots buy 2 to 5 of each anyway because they are cheap), Handcart-class first age items after level 16 (the tracks clamp at level 16, so nothing new of Pace, Tools or Value comes with levels 17 to 28). The toll now pays about 3 to 7 percent of the landing, so it is a small but real purchase; steps beyond 5 cost more than they bring.

Waiting. A player would sit for over two minutes mainly in the second age's late levels 25 to 28 (3 to 5 minutes apart on seeds 3, 7, 9: 214 to 354 s with nothing new on the map) and in the first age's first-age levels 14 to 16 on the slowest runs (2.9 to 3.8 minutes). Nothing affordable stalls of 60 to 80 s are in the slow second-age seeds.

What is left after the borough seal. Nothing is scripted. The bots stop at the seal. Unspent on the shop at the seal (payback bot, mean of seeds): RiverValue about 17 of 40 steps, Crane 6 max (5.2 bought), Raft 5 max (3.4), Rope 5 (4.4), Toll 8 (7.6), Winch 6 (4), Berth 2 (1), BoatPrices done (19.6 of 20). At boat income of 10 to 25 thousand a minute that is roughly 5 to 10 more minutes of buying (estimate, not played), then nothing: no requests after the charter in the first age, skipper requests continue, achievements (24 + 16) are the only other goal. So the real end of play is about 8 to 10 minutes after the seal.

## Purchases by kind (mean over seeds 1, 2, 3, 7, 42)

First age (before the charter; from `town --seed N`; seeds 1, 2, 3, 7, 42):

| Kind | cheapest | payback | random | saver |
| --- | --- | --- | --- | --- |
| Villager | 25 | 25 to 27 | 25 | 25 to 28 |
| Pace | 23 | 22 to 23 | 23 | 22 to 23 |
| Load | 2 | 3 | 2 to 3 | 3 |
| Pave | 24 | 22 to 24 | 22 to 24 | 20 to 22 |
| Building | 4 | 4 | 4 | 4 |
| Tools | 15 to 16 | 14 to 15 | 15 | 13 to 14 |
| Value | 40 to 42 | 39 to 41 | 40 to 42 | 37 to 39 |
| Extend | 14 | 14 | 14 | 11 to 14 |
| BellTrack | 4 to 5 | 5 | 5 | 5 |
| Hand | 1 to 2 | 1 to 3 | 1 to 2 | 1 to 2 |

Second age (after the changes):

| Kind | cheapest | payback | random | saver |
| --- | --- | --- | --- | --- |
| Ferry | 1 | 1 | 1 | 1 |
| Berth | 1 | 1 | 1 | 1 |
| Crane | 2 | 5.2 | 2.8 | 5.6 |
| BoatPrices | 18.4 | 19.6 | 19.4 | 20 |
| RiverValue | 27.2 | 23.2 | 30.4 | 16.6 |
| Raft | 2 | 3.4 | 2.6 | 4.2 |
| Rope | 3 | 4.4 | 3.8 | 5 |
| Toll | 5 | 7.6 | 5.6 | 7.6 |
| Winch | 3.6 | 4 | 4.6 | 3.4 |

Purchases a minute in the first age (mean seeds 1, 2, 3, 7, 42; minutes 1 to 10 / 11 to 20 / 21 to four before the charter / last four): cheapest 5.4 / 5.2 / 4.3 / 3.1; payback 6.0 / 5.8 / 5.3 / 3.6; random 5.5 / 5.4 / 4.9 / 3.8; saver 6.5 / 6.0 / 5.3 / 3.8. Second age: 2.9 to 4.0 a minute (table above).

## Decisions a minute for a human (estimate)

These are my estimates, not measured. A human is slower than the bots and does not buy every cheap step; I count a decision as a thing chosen, not a click.

First age (charter at 25 to 33 minutes):
- Minutes 0 to 10: about 4 to 5 a minute. Which cottage or building to buy, which villager's work, first pave of roads, first Pace and Value steps. High, all coin goes into a visible thing. The bell at the busy road.
- Minutes 10 to 20: about 3 to 4 a minute. Value steps, Tools, Extend a cottage (where), the choice of two at level 10 (once), requests (answer or skip), the bell.
- Minutes 20 to 30: about 2 to 3 a minute, fewer when saving for a big thing; at level 13 a choice, the hands at the workplaces (queues).
- After level 15: the charter drive (deliveries) takes the attention, 1 to 2 decisions a minute plus watching the drive.

Second age (20 to 30 minutes):
- First 10 minutes: about 3 a minute. The landing's first goods, which cargo to send first (Grain or Flour), the bridge or road choice at the ford, the pasture and weaver as they arrive, Wool and Cloth prices, the ferry. Many new things.
- Minutes 10 to 20: about 2 to 3 a minute. Berth and crane (the real decisions), better prices, bunching the bell at high water, request from skippers.
- Minutes 20 to the seal: about 1.5 to 2 a minute and falling; the drawbridge and toll, steps of the river tracks, nothing new on the map for 3 to 5 minutes.
- After the seal, until nothing is left: about 1 to 2 a minute of buying out tracks for roughly 8 to 10 minutes (estimate), and then nothing to do.

## How long until nothing is left

First age 25 to 33 minutes, second age 17 to 31 minutes (mean 24), seal at about 45 to 60 minutes of play, the last tracks bought out 8 to 10 minutes later (estimate). So 55 to 70 minutes in total.

## Further rule changes, ranked by fun per hour of building

1. First age tracks past level 16 (Pace, Tools, Value and the bell get steps 17 to 28 from the new levels): about 2 hours. The first age's best purchases currently stop at level 16, so in the second age the town's own work has nothing to improve; this also lifts the town's share of coin and ends "after the seal there is nothing". Largest fun for the smallest change (tables only).
2. One more gift between levels 23 and 27: a mooring bell for the harbour master, a toll house or a second weaver, one per late level gap, about 3 hours. Closes the map gaps of 3.5 to 5.9 minutes that fail on seeds 3 to 10.
3. Make the tide matter after the footbridge: boats ask a bonus cargo at high water and the drawbridge raises only at high water (walkers wait for the low water); about 3 hours. Today the tide is felt for about six minutes then ignored.
4. Make the market matter in the second age: the market pays a daily "wanted" good (Water, Grain or Flour) at 1.5 times, rotating on the tide; about 2 hours. The landing is 75 to 90 percent of income, so the first age's work feels pointless.
5. A third choice kind at levels 4 and 7: the first option is chosen by the cheapest bot always and the worth gap at level 4 is 1 point; give the level-4 pair a clear different effect (for example lanes against a free villager), about 1 hour.

## Whole fast suite

See the result line appended below by the last step.

Whole fast suite (Category!=Gate&Category!=Perf), after the changes: Failed: 6, Passed: 231, Total: 237, 2 m 48 s. Before (town-age2-progress.md): Failed 7, Passed 230. The 6 are the ones listed under Test state. Nothing else changed.

# Town green pass 2: progress

Started 2026-11-26 00:42:49 UTC (time box about 35 min).

## Status
- reading
- 00:44 Regulars (sim/Town/TownRegulars.cs, ShopItemKind.Regulars = 21): the first age's ladder of small steps. Each: +0.4 % on every delivery; price 0.15 min of the best coin rate at the level's start x 1.3 per regular won this level; restarts each level; first age only.
- 00:45 Tool, first age: seeds 1-10 no miss (was 3), seeds 11-20 no miss. Nothing affordable <= 28 s (was <= 60 s target). Charter 26.9-38.4 min (step 0.005 gave 25.1 for the saver; 0.003 gave 39.1 for cheapest; 0.004 is the middle).
- Decision: item 2 (villagers living in river cottages) left out entirely: it needs new river itineraries, resting by the river and crossing safety in every tide state; not safe to build and test in this time box.
- 00:47-00:51 Second age: one new miss after Regulars (seed 2 random, 69 s nothing affordable, limit 60). Regulars in the second age tried (0.15, 0.04, 0.02 of the best rate): worse (62-82 s), dropped. TrackStart 0.2/0.15: no help. RegularsGrowth 1.35 (was 1.3): both ages 0 misses on seeds 1-10 (second age nothing affordable 58 s: luck of boat lumps). Seeds 11-20: first age 1 miss (seed 16 payback, 3 in minute 9), second age 7 misses (5 nothing affordable 65-90 s, 2 landing share 0.31/0.33).
- 00:52 Test fix: TownRulesV3Tests.AQueueThatStaysOffersAnExtraHand checked an offer after the bot had already bought it in the same tick; now it checks offers right after Step and again after the bot acts (same assertions). New: sim.tests/Town/TownGreen2Tests.cs (4 tests). Town tests 90/90.
- 00:53 Docs: town-sim-api.md (Regulars under "Added in the green pass"), town-pacing.md (new current section "Green pass 2"). Whole fast suite x3 running.
- Whole fast suite (Category!=Gate&Category!=Perf) three times: each "Failed: 0, Passed: 249, Total: 249" (3 m 28 s, 2 m 40 s, 2 m 41 s).

## Status
Finished. Green on the fast suite x3. Item 2 (villagers living in river cottages) not built, by decision. Second age margins on "nothing affordable" (58 s of 60) and landing share (0.34 of 0.333) remain thin and miss on seeds 11-20.

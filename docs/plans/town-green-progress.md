# Town green pass progress

Started 2026-11-25 23:57 UTC. Time box about 50 minutes (to about 00:47 UTC).

## Status
- [ ] read docs and tests
- 00:00 Touches (sim/Town/TownTouches.cs), event TouchAdded, counted as new on the map in both ages' pacing.
- 00:01 First-age tracks go on in the second age (TownRulesRiver.PaceMore etc., Town.PaceCap etc.).
- 00:03 River level renown fixed level by level at the best coin rate then (RiverState.LevelAt); LevelRenown x0.61. Second age 23-33 min on seeds 1-10.
- 00:08 Continued steps: first at half the first age's last price, growth 1.1 (TownRulesRiver.TrackStart/TrackGrowth), 3 more steps a river level; river price growths 1.08. Nothing affordable now under 60 s on seeds 1-10.
- 00:09 Touch beat: income over the last beat, lowest of the last two beats and the last minute; 2.6 min in the first age, 1.3 in the second. Second-age map gaps now under 210 s on seeds 1-10 (worst 205). Left: landing share (seeds 3, 8, 9).
- 00:14 Opening boats (first 6 come whatever the tide, from 5 s; boats pay 1.3x until all 6 are served). Touch beat 6 min first age, 1.4 min second. Tool: no second-age miss on seeds 1-10 (age 20.6-32.2, nothing affordable <=49 s, map gap <=197 s, landing share >=0.37; touches 27-40 at the seal).
- 00:21 Charter renown: at least CharterMinutes (10) of best coin rate above the renown at level 15. River LevelRenown x0.64 of v3. Opening boats 8 at 1.6x. TrackStart 0.3. Touch backstop 170 s, TouchMax 44. Pacing: only TownPacingTests.PurchasesKeepComing red (3 misses).
- 00:25 Wishes: ShopItem.Locked/Finished, CanPaveNow/CanExtendNow, cached hint texts, OfferBoughtOf, thousands separators, SecondAgeComplete/StillOnSale (sim/Town/TownGreen.cs). Tests: sim.tests/Town/TownGreenTests.cs (8, pass).
- 00:32 Docs: town-sim-api.md "Added in the green pass", town-pacing.md green-pass section (older sections marked history). Whole fast suite x3 running.
- NOT done: decision 6 (villagers living in river cottages). Still red: TownPacingTests.PurchasesKeepComing (3 runs).
- 00:42 Whole fast suite (Category!=Gate&Category!=Perf) three times: each "Failed: 1, Passed: 244, Total: 245" (4 m 57 s, 2 m 5 s, 2 m 42 s). The one: TownPacingTests.PurchasesKeepComing (same 3 misses every run: deterministic).

## Status
Finished inside the time box. Not green: PurchasesKeepComing. Decision 6 not built.

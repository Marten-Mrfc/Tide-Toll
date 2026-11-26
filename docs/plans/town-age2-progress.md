# Town age 2 (The River) progress

Started 2026-11-25 23:07 UTC (time box ~55 min, until about 00:02).
Worktree already held rules v3 changes (uncommitted) from the agent before; built on top of them.

## Done
- 23:20 Code: sim/Town/TownRulesRiver.cs (numbers), TownRiverPlan.cs (plan from the seed), TownRiverState.cs, TownRiver.cs (ring, levels, tide, boats, ferry, drawbridge), TownRiverJobs.cs (river jobs, itineraries, crossings), TownRiverShop.cs (purchases, views, achievements, hint), TownRiverPacing.cs (targets, bot runs). Edits: Town.cs, TownJobs.cs, TownShop.cs, TownViews.cs, TownState.cs, TownSave.cs, TownEvents.cs, TownBots.cs (PlayOn), TownBoons.cs (worth of river items). Tool: `town --river [--plan] --seeds A-B [--bot B]` (tools/TideToll.Tools/TownRiverCommand.cs).
- 23:25 Tests: sim.tests/Town/TownRiverTests.cs (11 pass).
- 23:28 Plan tests: sim.tests/Town/TownRiverPlanTests.cs (seeds 1-100 valid: plots, points, lanes, crossings, boats' course, reachability per level). Lanes use an own 4-connected search (the first-age Router returns bends only and its straight segments cut plots; not changed, so first-age plans stay byte-identical).
- 23:30 Pacing tests: sim.tests/Town/TownRiverPacingTests.cs (10 tests, targets in `TownRiverTargets`, sim/Town/TownRiverPacing.cs). Tuned three rounds.
- 23:32 Docs: town-sim-api.md "The second age"; town-pacing.md second-age tables. Whole fast suite run at the end (result below).

## Decisions written down
- Boats come from the south edge (row 127), moor, and go back south. So the footbridge goes north of the landing (boats never reach it), the ferry north too (its rope never crosses the boats' course), the drawbridge south of the landing (every boat passes it in and out; toll once, on the way in).
- The ring's own gift is level 17 (the landing and its road): without it nothing new is on sale right after the ring (no requests after the charter) and the bar would stall.
- Level renown is in minutes of the town's best coin rate at the ring (`RenownScale`), like the charter drive, so towns of any size pace alike.
- Prices are relative: boats pay 1.6 x market price; Wool = 2 x Grain at the market, Cloth = 1.8 x Flour; cargo wanted = coin rate x 0.8 / 1.8 minutes. The landing then takes most of the work by itself.
- The river layer is its own small graph (RiverPlan points and links, positions at cell centres) joined to the first age's road graph at one door node (`BankNode`). A river job is an itinerary (walk roads to the bank node, walk the river plan, work, deliver). The first age's roads, plots and plan never change.
- River cottages add housing (2 each); a villager housed there keeps a first-age cottage as its errand home (Water and home Grain deliveries go there), `VillagerState.RHome` says which river cottage it lives in. Not drawn as a home move yet.
- Save version 3 only once rung in; a never-rung town still writes version 2 (the existing `LoadRefusesAnotherVersion` test checks "Version":2 on a fresh save and passes unchanged). Load takes 2 and 3.
- River achievements and the river hint are separate views (`RiverAchievement(i)`, `RiverHint`) so the first age's `AchievementCount` (24) and `Hint` stay as they were.
- "Something new on the map" also counts the purchase of a ferry, berth, crane step, raft, rope or winch step (each changes a model), and a villager arriving.
- "Each crossing carries more a minute than the one before" is measured as people crossed (by any crossing) per minute while that crossing was the newest one. This measures use, which follows demand, not capacity.

## Existing tests changed
None. (sim.tests/Town/TownRulesTests.cs, TownRulesV2Tests.cs, TownRulesV3Tests.cs, TownPacingTests.cs, TownPlanTests.cs untouched.)
- 23:37 Crossing capacity (`Town.CrossingCapacity`, widths 3/6/8 on the ford/footbridge/drawbridge, raft +1 a step) is what the crossing test checks; routes count the queue at the ford and the ferry (this fixed waits of 250 to 440 s at the ferry).
- 23:39 Final tables in town-pacing.md. Whole fast suite run (result below).

## Status (23:40)
Items 1 to 6, 9, 11, 12 done. 10: tests exist; some targets red (below). 7: all new purchases done; "further steps of the first age's tracks unlocked by the new levels" NOT done (the first age's track tables clamp at level 16, so nothing new of Pace, Tools, value or the bell comes with levels 17 to 28). 8: 12 levels, skipper requests (on every second boat), 16 achievements, river hint done.

Red tonight (sim.tests/Town/TownRiverPacingTests.cs, seeds 1-10, 40 runs; numbers from the last tool run in town-pacing.md):
- TheSecondAgeTakes20To35MinutesForTheBuyers: 4 of 30 runs out: seed 1 payback 19.0, seed 1 random 16.0, seed 4 random 19.0, seed 3 random 36.5; the rest 21.0 to 33.8.
- SomethingNewOnTheMapEvery3AndAHalfMinutes: 20 of 40 runs above 210 s; worst 354 s (seed 3 random affordable). Gaps sit between the late levels (25 to 28 are 3 to 5 minutes apart). Next step: one more visible gift between the late levels (for example a second weaver or a toll house) or split the last levels.
- NeverMoreThan60SecondsWithNothingAffordable: 8 runs, worst 71 s (target 60).
- TheLandingEarnsAThirdOfAllCoinByTheMiddleOfTheAge: seed 9 saver 0.23 (all other runs 0.52 to 0.95).
- TheSaverBuysEveryKindOfNewThing: seeds 1 and 8: the saver never buys the Toll (it buys every other kind on every seed).
- The first age's own two pacing tests (TownPacingTests.PurchasesKeepComing, EveryBotFinishesTheAgeInTime) fail exactly as before this work (same 4 misses as town-rules3-progress.md): the first age is unchanged.
- 23:42 Whole fast suite (Category!=Gate&Category!=Perf): "Failed!  - Failed:     7, Passed:   230, Skipped:     0, Total:   237, Duration: 3 m 11 s". The 7: TownRiverPacingTests.TheSecondAgeTakes20To35MinutesForTheBuyers, TheSaverBuysEveryKindOfNewThing, SomethingNewOnTheMapEvery3AndAHalfMinutes, TheLandingEarnsAThirdOfAllCoinByTheMiddleOfTheAge, NeverMoreThan60SecondsWithNothingAffordable (second-age targets, above) and TownPacingTests.PurchasesKeepComing, EveryBotFinishesTheAgeInTime (first age, red before this work, same misses).

## Finished
Work stopped at 23:43 inside the time box. Nothing committed (the lead commits).

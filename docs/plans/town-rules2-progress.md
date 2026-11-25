# Town rules v2 progress

Started 2026-11-25 21:10 UTC (time box ~45 min).

## Done
- Rules v2 mechanics written (TownRules, TownPlanner 16 levels, TownTracks.cs, TownRequests.cs, work spots in TownWalk, Saver bot, pacing measures, tool `town --extras`). Builds.
- r1 bug fixes in code: queue fills every free place (TownJobs FinishWork, RefreshWorkplaces), renown rounds half up, offer ids by kind (OfferOf).

## Next
- run the tool, tune numbers, update tests (list changes), docs (town-sim-api "Added in rules v2", town-pacing.md).
- 21:19 numbers locked in TownRules (first pass). Seed 1: charters 28.0 / 25.2 / 28.2 / 23.4 (cheapest, payback, random, saver); ~127 purchases each.
- Next: tests (save v2, new mechanics, bug fixes), test changes for rules v2, docs.
- 21:22 tests written (sim.tests/Town/TownRulesV2Tests.cs; TownPacingTests now 4 bots x seeds 1-10 + new targets). All non-pacing town tests pass (37). Pacing misses: early dips (3/min in minute 5-6), late 1/min, saver/payback charter 22-24 min, requests met ratio on some seeds, bell 26.5 % for saver.
- Next: one more tuning pass, then docs (town-sim-api Added in rules v2, town-pacing.md).
- 21:26 town-sim-api.md "Added in rules v2" written. Numbers final-ish (BellHurry 1.22). Pacing tests: 6 of 10 pass-fail listed in report.
- Next: town-pacing.md tables, r1-sim-bugs recheck, final test run.
- 21:30 town-pacing.md regenerated (4 bots, seeds 1-10, extras). Achievements use OfferOf (r1 finding 6). Final fast test run in progress.

## Status for a successor
- Mechanics, tests and docs are done. Pacing targets partly unmet (see docs/plans/town-pacing.md "Targets not met"). Main open problem: late-age purchase rate (content runs out after Pace/Tools/Value finish); tuning knobs in TownRules (LevelRenown, growths, RequestShare, BellHurry).
- r1 bug hunt: findings 1, 2, 6 fixed with tests (TownRulesV2Tests: NoOneQueuesWhileAPlaceIsFree, BuyingTheSecondBucketFillsItFromTheQueueAtOnce, RenownRoundsHalfUp). 3, 4 (nits), 5, 7 (latent) not changed.

# Town rules v2.1 progress

Started 2026-11-25 21:36 UTC (time box ~40 min, so until about 22:16).

## Done
- a. Level-gated steps: tables PaceAt, ToolsAt, ValueAt, FlourValueAt, BellTrackAt, ExtendAt in TownRules; StepLock/OnSale in TownTracks.cs; PaceLockReason, ExtendLockReason. Tools now 15 steps of 3.5 %, Value 12 steps of 4.5 %.
- b. Adaptive requests: TownState.ReqScale (saved), TownRules.RequestGrowMet/RequestShrinkMissed/RequestScaleMin/Max; Town.RequestScale view.
- c. TownTargets: bell and requests measured on the average over seeds 1-10 with single-run sanity bands; tests changed.
- d. Road smoothing: Router.Smooth (corner cutting, 24 passes, every move checked against open cells); Road.Px/Py/PCum; At() follows the curve with a binary search; RoadCells follows the curve; Cum rescaled so Cum[^1] = Length. Plan tests on seeds 1-100 pass; VillagersStayOnRoads now checks the curve.
- Tool: town --seeds 1-10 --misses [--averages] (all misses, counts, bell averages); --set takes int[] too.

- Tests: TownRulesV2Tests updated for gating (Pace test now buys PaceAt[0], checks "Unlocks at level", then the rest at the last level; Pace max literal 20 -> TownRules.PaceMax; Tools/Bell and Extend tests raise the town level first). New: EveryLevelUpPutsNewStepsOnSaleAndNoTableRunsBackwards, RequestsAdaptToThePlayerAndTheAdaptationIsSaved, RoadsAreSmoothCurvesFromDoorToDoor. TownRulesTests.OnSomeRoad checks the curve (Px, Py).
- Tuned (21:58): Pace 25 x 4 % growth 1.36; Tools 18 x 3 % base 30 growth 1.33; Value 14 x 4 % growth 1.55/1.43/1.17; Villager growth 1.3; Extend growth 1.33; Load 300/1600/4000; BaseSpeed 1.6; BellHurry 1.17; LevelRenown ... 59000, 84000.
- Docs: town-sim-api.md "Rules v2.1" under "Added in rules v2"; town-pacing.md regenerated.

## Status
- 16 pacing misses left on seeds 1-10 (12 purchases-per-minute, 4 saver ages 24.2-24.9) plus bell/requests single-run band misses: see town-pacing.md "Targets not met".
- 22:15 three fast test runs (Category!=Gate&Category!=Perf): each "Failed: 4, Passed: 198, Total: 202" (5 m 4 s to 5 m 40 s under load 15), the same 4 pacing tests with identical messages each time (not flaky): PurchasesKeepComing (12), ANormalBotMeetsHalfToFourFifthsOfTheRequests (seed 2 payback 3 of 13), TheBellIsWorthTenToTwentyFivePercent (3 single runs + saver average 25.1), EveryBotFinishesTheAgeInTime (saver 24.2-24.9 on seeds 1, 4, 7, 8). Time box used up.

## Next for a successor
- Saver too fast (24.2-24.9 min on 4 seeds) and dips early (minute 6-8): try making early steps a bit dearer for the saver or a slightly higher LevelRenown at 10-12 with more steps on sale there.
- Bell single-run noise: measure the bell with the same purchase sequence (replay) instead of a free second run.

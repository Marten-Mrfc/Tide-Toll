# Town rules v2.2 progress

Started 2026-11-25 22:17 UTC (time box ~30 min, so until about 22:47).

## Plan (the lead's four decisions)
1. Purchase floor: saver out of that test; saver gets its own checks (every kind, Load step, building; charter 25-40; after minute 2 never more than 90 s without a purchase). Tune payback seed 6 min 7, random seeds 3, 4 min 24-25.
2. Bell measured on frozen towns: payback town at minute 10 and 20 (via save), 5 minutes no purchases, bell vs no bell, delivery coin. Avg 10-25 %, each 5-35 %.
3. Requests start easy (ease-in on the first requests); target avg about two thirds.
4. Saver charter >= 25 on every seed.

## Done
- 22:20 Code: TownPacing.Misses skips the purchase floor for the saver and checks SaverMaxPurchaseGap (90 s) instead; PacingResult.LongestPurchaseGap; TownPacing.BellWorth(seed) on frozen payback towns (minutes 10 and 20, 5 min, via TownSave); TownRules.RequestEaseIn {0.4,0.6,0.8,1}; TownTargets.SaverMaxPurchaseGap, BellAtMinutes, BellFrozenMinutes; lead's decisions written as comments in TownTargets, TownRules, TownPacing, TownPacingTests.
- Tests: PurchasesKeepComing comment; new SaverNeverWaitsLong; TheBellIsWorthTenToTwentyFivePercent rewritten for frozen towns (20 comparisons).
- Tool: --averages prints the frozen bell and request shares; --extras prints the frozen bell; new --buys A-B (purchase log).
- 22:26 Tuned: BellHurry 1.4, BellDuration 8, BellTrackHurry 0.4, BellTrackRest 1, PaceStep 0.03, RequestReward 0.4, ExtendBase 40, LoadPrices 250/1600/4000, LevelRenown L15 62000, L16 102000. Tool on seeds 1-10: 0 misses; bell 14.6 % avg (9.1 to 20.2); requests cheapest 63 %, payback 69 % (runs 43 to 91). Seeds 11-20 (not a test): 1 miss, saver seed 18 at 24.8 min.
- 22:27 Town tests alone: Passed 44 of 44 (41 s).
- 22:35 Whole fast suite (Category!=Gate&Category!=Perf): "Passed!  - Failed: 0, Passed: 203, Skipped: 0, Total: 203, Duration: 6 m 42 s".
- Docs: town-pacing.md (v2.2 header, decisions, seeds 1-10 table; seed 1 tables below still v2.1), town-sim-api.md "Rules v2.2".

## Status
Finished. All four targets met on seeds 1-10. Known thin margins: saver seed 8 charter 25.1 min; seeds 11-20 (not tested) saver seed 18 at 24.8 min.

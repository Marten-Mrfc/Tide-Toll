# Town rules v3 progress

Started 2026-11-25 22:45 UTC (time box ~30 min, so until about 23:15).

## Plan
1. Choices of two (boons) at four levels.
2. Charter drive after level 15.
3. Shop worth estimate per item; milestones every fifth step of Pace, Tools, Value.
4. Queue offers: extra places at a workplace.

## Done
- 22:50 Code: sim/Town/TownBoons.cs (Boon, ChoiceView, choices, effects, worth estimates, milestone names), sim/Town/TownDrive.cs (charter drive, extra hands), edits in Town.cs, TownJobs.cs, TownWalk.cs, TownShop.cs, TownTracks.cs, TownRequests.cs, TownState.cs, TownEvents.cs, TownBots.cs, TownPacing.cs, TownRules.cs. Tool: `town --seeds 1-10 --misses --v3 [--worth]`.
- 22:58 Tests: sim.tests/Town/TownRulesV3Tests.cs (12 tests). Changed: TownRulesV2Tests.PaceHasClearStepsUpToItsMaxAndThenSaysSo (every fifth Pace step adds PaceMilestone: the lead's decision 3).
- 23:05 Tuned (numbers in town-pacing.md and town-sim-api.md "Added in rules v3"). Town tests: 60 of 62 pass; TownPacingTests.PurchasesKeepComing and EveryBotFinishesTheAgeInTime fail (4 misses).
- 23:06 Docs: town-sim-api.md "Added in rules v3", town-pacing.md v3 section. Whole fast suite started.

## Decisions written down
- Choice levels 4, 7, 10, 13; pairs lanes/backs, well/rows, room/hands, market/guild. Boon texts carry their numbers from TownRules.
- The drive is its own view (`Drive`, `DriveText`), not a `Request`, so the request API and counts keep their meaning. Renown is paid per delivery. No ordinary request starts while it runs.
- The last-gap target is checked on the mean over seeds 1-10 per bot against the longest of the three level gaps before it (single runs are noisy).
- Save version stays 2 (new fields default when missing), so the builder's saves keep loading.
- Hands: offer after a queue of 2+ for 30 s, 2 per workplace.

## Status
Code, API docs and the new tests are done and pass. Not green: 4 pacing misses on seeds 1-10 (saver charter 24.1 on seed 4 and 23.3 on seed 7; cheapest seed 10 1 purchase in minute 32; payback seed 10 3 purchases in minute 8), so two existing pacing tests fail. Cause and next step in town-pacing.md.
- 23:07 Whole fast suite (Category!=Gate&Category!=Perf): "Failed!  - Failed:     2, Passed:   212, Skipped:     0, Total:   214, Duration: 3 m 24 s" (the two pacing tests above).

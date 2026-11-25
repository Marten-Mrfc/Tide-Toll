# Town sim progress (agent: town simulation)

Started: Wed Nov 25 20:32:40 UTC 2026. Finished about 20:57 UTC. STATUS: ALL ITEMS DONE, handed over.

## Done
- sim/Town (namespace TideToll.Sim.Town): TownRules (every number, static fields; TownTargets), TownPlan + TownPlanner + TownRouter (plan),
  TownState, Town (+TownWalk, TownJobs, TownShop, TownViews), TownEvents, TownAchievements (24), TownSave (JSON v1),
  TownBots (cheapest, payback, random), TownPacing (runner + target check).
- tools/TideToll.Tools/TownCommand.cs and one line in Program.cs: `town` command (--seed, --bot, --minutes, --seeds, --markdown, --plan, --detail, --set).
- sim.tests/Town: TownPlanTests, TownRulesTests, TownPacingTests: 20 tests, all pass, about 6 s.
- docs/plans/town-sim-api.md (for the game builder), docs/plans/town-pacing.md (tables of all three bots, seed 1; seeds 1-10 summary).
- Full fast suite: 179 passed. dotnet build game/TideToll.csproj: succeeded.

## For a successor
- Pacing: seed 1 meets every target. On seeds 4 and 9 the cheapest bot takes 43.9 min (target 40); not required tonight.
- Tune with `town --seed 1 --set "Name=v;Arr=a,b,c"` and keep TownPacingTests green.
- Changing the plan (template, doors, router) changes road lengths and so the pacing: re-run `town --seeds 1-10` after.

## Decisions
- Plan: fixed template of 13 plots (33 x 36 cells) placed where the west bank is widest near the middle, at least 6 cells from the water,
  flipped north-south on half the seeds, cottages nudged +-1 cell; doors face the well; roads by A* on the map grid between doors
  (around water, plots and earlier roads; MapData.Noise bends them), simplified to polylines. MapGen's own buildings are ignored.
- Every road joins two plot doors (no mid-road junctions), so road ids stay stable. 17 roads, 12 levels.
- Tick 10 Hz. After a delivery a villager picks its next job from where it stands (it does not walk home first).
- Bots think every 5 ticks, buy at most one item per think, ring the bell when ready, stop buying at the charter.

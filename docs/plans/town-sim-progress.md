# Town sim progress (agent: town simulation)

Started: Wed Nov 25 20:32:40 UTC 2026. Time box about 85 minutes (hand over by about 21:58 UTC).

## Done (as of ~20:50 UTC)
- sim/Town: TownRules (all numbers + TownTargets), TownPlan/TownPlanner/TownRouter (plan), TownState, Town (+Walk, Jobs, Shop, Views),
  TownEvents, TownAchievements (24), TownSave (JSON v1), TownBots (3 bots), TownPacing (runner + target check).
- tools/TideToll.Tools/TownCommand.cs + one line in Program.cs: `town` command.
- Tests sim.tests/Town: plan (100 seeds), rules, determinism, save, stuck/off-road (3 bots x 10 seeds x 60 min), speed: all pass.
- docs/plans/town-sim-api.md first version.
- TownPacingTests added: fail until tuning is done.

## Next
- Tune TownRules for seed 1 targets; write docs/plans/town-pacing.md; final test run + game build.

## Decisions
- Plan: fixed template of plots (local frame 33 x 36 cells) placed on the west bank by seed, flipped north-south on half the seeds,
  cottages nudged +-1; roads routed by A* on the map grid between plot doors (avoid water, plots, other roads), simplified to polylines.
  Only MapGen terrain is used; MapGen's own buildings are ignored.
- Every road joins two plot doors (no mid-road junctions), so road ids stay stable.
- Tick rate 10 Hz. Bots think every 5 ticks, at most one purchase per think, ring the bell when ready, stop buying at the charter.

# Town river polish: progress

Started 2026-11-26 01:11 UTC (date -u). Time box about 40 minutes. Note: the clock times in the step list below were my estimates while working; `date -u` at the end read 01:34 UTC after three suite runs, so trust date -u, not the step times.

## Done
- Item 4: `RiverHint` "Send it <good>" texts are fixed strings per good (`TownRiverShop.SendItText`).
- Item 1: lanes smoothed like the first age's roads (`TownRiverPlanner.SmoothLane`, `Router.SmoothPasses` passes, ends fixed, moves kept only off water and off plots of both ages, the two end cells allowed). `RiverLink.PCum` (same array as `Cum`), `RiverLink.Xs/Ys` (grid route), `At` by binary search. Crossings unchanged (straight). Touch spots follow the new lane lines (TownTouches reads Px/Py); touch tests pass.
- Item 2 (PARTIAL): `Town.FordOnlyStretch`; while it holds and the pasture works, one more boat in three wants Wool (TownRiver boat pick, `h % 3 == 1`). `LevelRenown[5]` (level 22, ferry) 3.65 -> 3.90. Result seeds 1-10: ford alone 2.2-6.9 min every run; ford used before the ferry on seeds 1, 2, 6, 9, 10 (460 crossings, ~1,550 villager-s high-water waiting over 40 runs); seeds 3, 4, 5, 7, 8: nobody crosses the ford. Cause seen on seed 4: the ford stretch falls mostly at low water, when no boats come, and selling Wool at the market does not beat Grain/Flour work. Tried and dropped: WoolRel 2.6 (no help), ferry later (4.3-4.6: ford used on 20-25 of 40 runs but seed 8 saver landing share 0.25-0.30 < 1/3, an existing test), two boats in three for Wool (same landing-share miss).
- Item 3: `LevelRenown[10]` (level 27, drawbridge) 15.82 -> 14.00; toll steps `TollTrackBase` 2500 -> 2000, `TollGrowth` 1.25 -> 1.12. Seeds 1-10: drawbridge 3.8-10.3 min before the seal, 2-8 tolls a run; toll steps earn back more than they cost over all runs (per run worst 9,000 of 20,178, seed 9 random).
- Item 5: boats' cargo in loads for display: `Town.BoatCargoText(i)` ("Flour 12 of 30 loads"), `Town.LoadsOf(units)`; cached per slot (sim/Town/TownRiverPolish.cs). Internals unchanged.
- Item 6: nothing else from sim-wishes.md (the four river wishes are items 1-5 here).
- Pacing: new `RiverPacingResult` fields and `TownRiverTargets` (see town-sim-api.md "New in the river polish"); tool prints them.
- Tests: sim.tests/Town/TownRiverPolishTests.cs (4), TownRiverPacingTests: TheFordIsTheOnlyWayOverForARealStretch, VillagersCrossTheFordAndWaitAtHighWaterBeforeTheFerry, BoatsPayTollsBeforeTheSeal, TollStepsPayBackBeforeTheSeal.
- Docs: town-sim-api.md "New in the river polish" (end of "The second age"); town-pacing.md new top section "River polish" with the full tool table.
- Whole fast suite (Category!=Gate&Category!=Perf) three times: Passed 257/257 each (4 m 10 s, 5 m 2 s, 4 m 41 s).

## Existing assertions changed
None.

## For a successor
- Item 2 is not fully met: the ford is used on 5 of 10 seeds. A next step: let villagers sell Wool at the landing store (market price, no first-age walk back) or count the ford's wait once (ConsiderRiver adds FordWait on both legs of a Wool trip), then retune the landing share.
- Nothing committed (the lead commits).

## Status
Finished.

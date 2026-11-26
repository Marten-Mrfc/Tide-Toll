# Round 4, the river (game2) progress

Started 2026-11-26 00:05 UTC. Time box about 75 minutes (to about 01:20 UTC).

## Done
- nothing yet

## Next
- read docs

## Update (machine clock reads oddly slow; elapsed about 55 min)
- Done: RiverLayer.cs (plots, stakes, lanes, crossings, raft, drawbridge leaf, tide drop, tide post, sheep, seal stone),
  RiverBoats.cs (boats, cargo, labels, wake), RiverAudio.cs (river sounds), TownHud.River.cs (tide dial, page turn,
  river title, second-age page, seal page, skipper request in the card, river hint), shader tide_drop, shop river group,
  Wool/Cloth loads, Main hooks, view widening (RiverLayer.Widen in Main.TownExtent).
- game-river test (game/tests/RiverTest.cs, river.tscn, step in tools/check.sh) PASSES.
- Found: seed 1 payback bot: nobody uses the ford in the whole age (Crossings(Ford) == 0); the drawbridge comes so
  late that no boat passes it before the seal. Test plays on after the seal for the lift and toll.
- Next: screenshots (--river-only), perf second-age town, handover file, ./check.
- game-river PASSES; perf second-age town within budgets (1x 0.27 ms, 1230 B/frame; 8x 0.34 ms, 1366 B/frame).
- game-perf FAILS on the FIRST-age town at 1x: 3442 bytes/frame (gate 1536). Disabling River/Boats/RiverAudio and
  RiverFrame did not change it (3442/3455), so it is not the river drawing; cause not found (suspect the merged sim's
  first-age views or interface text). Not fixed.
- Screenshots read (river-only): ring, landing, ferry, drawbridge, seal (+ page). Fixed: Stretch offset (rope/spans
  were shifted a length off), tide dial wording, river shop group overflow (locked/finished river steps hidden),
  seal shot timing.
- Docs: r4-river-handover.md, sim-wishes.md, AGENTS.md. Running full ./check.
- First full ./check: game-save and game-playthrough failed on assertions the river changes (fixed, listed in the
  handover); game-book failed once on an exit-time "RenderingServer null" line after BOOK PASS (passed on rerun:
  flaky at exit); game-perf fails on the first-age town allocation (above). Second full ./check running.
- Second full ./check: every game step PASS except game-perf (first-age town 1x: 3442 bytes/frame, gate 1536; second-age
  town within all budgets). sim-test red on the known pacing tests only. Work stopped here (over the time box).
## State: finished as far as the time box allowed. Missing: summary page and achievements page for the second age
  (item 9 partly: the seal page covers both ages); game-perf first-age allocation not explained.

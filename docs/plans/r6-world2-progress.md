# Round 6, the world half, second agent: progress

Started 03:31 UTC (clock), stop new items at 04:00, report by 04:12. Read r6-world-progress.md first (W1, W2, W12 done there).

## Done
- W3 (people through each other / through buildings), game/scripts/town/TownVillagers.cs: a drawing-only spread
  (`Spread`): villagers closer than `Personal` (1.0 m x WalkerScale) are pushed apart (eased, at most 0.6 cell); walkers
  keep a small lane across the road by id (-2..2 x 0.12 cell) so a crowd reads as a crowd; a queued villager whose
  drawn spot lies in a built plot's footprint (not a field, within 1.5 cells of its edge) is drawn just outside its
  nearest edge (`OutOfBuildings`). The simulation is untouched. Test: TownTest WatchVillagers "standing villagers drawn
  apart" (`MinStandingGap`, at most a fifth of the frames under 0.3 x WalkerScale m).
- W5 (a new road almost invisible), game/shaders/ground.gdshader: a narrow, mostly unbroken line of soil down a trail's
  middle from the lowest drawn wear (`line_soil`), broken only by tall tufts. The wear values (and TownTest's "faint
  trail" bounds) are unchanged. Picture a01-first-minute retaken: a plain light-earth trail from the cottage to the well.
- W7 (one and two extensions alike; dark boxes), TownBuildings.cs: two extensions add a second lean-to on the other
  side (`LeanTo2`, a row of three gables; the group fitted as 1.8 x the house's width, 1.5 for one extension), and the
  wattle yard behind is gone (its thin fences under the ink outline were the dark box from above; the depth factor 1.45
  went with it). A first try kept the yard with pale wood: the b05 retake still showed dark fence outlines, so removed.
- W6 (a bigger load looks the same), TownVillagers.cs: loads drawn at `LoadScale(level)` (1, 1.25, 1.4, 1.75) and,
  from the second Load step, a second bundle on the back (`BundlesAt`); load MultiMeshes sized 2 x Capacity. Test:
  TownTest "each Load step shows on the villager".
- W8, the crowded half (ground.gdshader): crowd tint is now a strong brick red (#CC3E22) at up to 0.8, ramped 1.8x
  faster. The pave-mode highlight is drawn by TownHud.Pave.cs (`UiKit.Focus`), an interface file: NOT mine, left for the
  interface agent (suggest a saturated teal/green, clearly different from the crowd's red).
- W10 (closing book shows only the right page), BookCover.cs: the left page turns over with the board until the cover is
  down (transform about the hinge), the endpaper shows only when shut. Tests: MenuTest "the left page shows while the
  cover closes" (Hold 0.4) and "the left page is hidden under the shut cover".
- Charter stone paler (TownBuildings.cs): pale dressed-stone material #F0EADB on the stone. Test: PlayThrough "the charter
  stone is pale dressed stone".

## Pictures read
- a01-first-minute (build/tour): the new trail from the cottage to the well is a plain light-earth path, faint at the
  edges; before, only a few reddish blots.
- b02-pace-and-load (build/tour): yoke carriers (Load level 1), loads a little bigger; no overlap of the two walkers.
- book-half-closed (build/w2shots, screenshot --menu-only): the summary page (numbers, "Not yet", "78 of 300") stands on
  the rising board's inside; before this fix the left side was a bare leather board (as g05 showed).
- river-landing (build/w2river, --river-only): queue for the landing stands on the lane before the landing, none on the
  crates; the crowd at the junction is many people with big white flour sacks (Load level 3), packed but not merged.

- river-ferry (build/w2river): the raft is in mid-river on its rope with its rider aboard, so W4 looks right in this
  build. Bottom left of the same shot: a crowd of people carrying big flour/grain sacks (Load level 3) reads as a pile of
  sacks: LoadScale(3) = 1.75 may be too big; 1.5 would be the first thing to try.
- b05-extended: NOT retaken with the final W7 (the run at 03:51 gave "TOUR NOT MADE b05-extended: the cottage has 1
  extensions after two clicks", level 8 at 956 s; the run before reached it at 676 s; the tour picks the cottage by
  its plot, not by its model, so I believe the timing differed, but unconfirmed). The 03:50 b05 shows W7's first try.

## Not done
- W4 (ferry raft drawn late): nothing changed. RiverLayer sets the raft every frame at `Town.Ferry` X/Y, the same
  formula (`RaftPos * link.Length`) the simulation uses for riders; river-ferry shows it mid-river with its rider.
- W9 (page wipe): drawn by TownHud.River.cs (`_wipe`, a ColorRect at 0.97 alpha), an interface file: not mine. Suggest
  alpha 1 and drawing it above the shop cards and labels (top CanvasLayer).
- W11 skipped as told.

## Tests (after the last code change, 03:51-03:52 UTC; book and menu at 03:48 after the last BookCover change)
game-smoke, game-town, game-river, game-book, game-menu, game-save, game-perf and game-playthrough: all PASS.
Logs: build/w2/*.log. Changed assertions: none (only new checks added).

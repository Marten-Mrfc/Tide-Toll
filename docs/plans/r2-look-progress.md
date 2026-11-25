# Round 2 part 1: the look and the zoom (progress)

Started 2026-11-25 21:36 UTC. Time box about 40 minutes.

## Done
- CameraRig: tilt with zoom (`PitchFor(size)`: 56 deg up to `TiltFullSize` 80 m, smoothstep to 90 at
  `TiltFlatSize` = 0.97 x page view), Clamp uses the tilted ground extent, the whole book centres itself at the outer
  end of the zoom (rule 3). Pitch applied before the anchored zoom re-solve, so zoom to the cursor holds tilted.
- Main: zoom out to the whole book (`MaxSize` from the book's fit for the window shape), `Stage` (Town, Page, Book),
  tilt-aware `Follow` in the space the HUD leaves, never past the slim point; WalkerScale 15 px / 1.1..3.
- Villagers: dyed bodies (shaders/villager.gdshader, 6 light clothes colours), heads in model colours, both soft ink,
  soft shadow discs, `Spread()` fans queue and work spots.
- Roads: GravelStrip.Res 4, gravel half 0.22 cells, cobbles half 0.30 cells (0.6 cell wide), paved roads' worn earth
  0.5, paved strips run 0.7 cell on past the door cell to the building.
- Pave bands: stencil (each pixel once, hovered road drawn first), depth tested, narrower.
- HUD: text rebuilt only on change (ledger, bookmarks by signature, bell); slim strip; full HUD fades out over the
  last 12% before `SlimSize`; one-time "Scroll out to see the whole book." note at game second 75 unless the player
  already zoomed out.
- TownSummary (left page, Label3D, book fonts, 2 Hz while watched) + TownName from seed.
- tests/BookTest.cs + book.tscn, game-book step in check.sh; Screenshot.cs adds level-6, zoom-1..4, summary.
- Perf: allocation gates 1536 B/frame (1x), 6144 B/frame (8x); info line with the interface off.
- AGENTS.md, README.md updated.

## Next
- Finished 22:01 UTC: ./check PASSED (all 11 steps). Nothing left in this round's scope.

## Known gaps for a successor
- Road ends: only paved strips reach the doors; a track's worn earth still stops at the door cell.
- About 1 KB/frame is allocated with the interface off (engine glue and the test's awaits); not chased.
- Work spots are a visual fan-out only; the simulation will give real ones later.

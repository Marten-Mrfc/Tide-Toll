# n10 performance, round 2 (builder's notes)

Round 1: `perf-progress.md` (the readout, the matrix, the first wins) and `perf-measures.md` (the tables).

## 1. Two settings on the Display page

**Drawing detail: 100, 85, 70, 50 percent** (default 100). The 3D view is drawn at that fraction of the window's
pixels and scaled up bilinear (`Viewport.Scaling3DScale`, `Scaling3DMode.Bilinear`); the interface is 2D and stays
at full size. `GameSettings.DrawingDetail` (`[display] drawing_detail=100` in `settings.cfg`), applied at start and
at once on a change through `SettingsApplier` and `DisplaySeam.ApplyRenderScale` (a test seam records it). The readout
prints it ("1920 x 1080  detail 85%"; the CSV's `scale_3d` column already had it).

**Outline: Full / Light** (default Full). Light keeps the hard ink pass at one step at every window size (the
shader's `max_step` 1: 5 depth reads, never 9 to 17; the line no longer grows heavier on large windows). The soft ink
is always at one step. `GameSettings.OutlineLight` (`outline="full"|"light"`); `InkOutline.Update` reads the bool each
frame and sets the parameter only when it changes.

Both are rows of the Display page's right column under a new heading "Drawing" (`SettingsPages.SoundDisplay.cs`,
built with the page's own `Heading`, `Row` and `ChoiceButton`): "Drawing detail" with the note "Lower it if the game
runs slowly. The map gets softer." and "Outline". Keys for tests: `display.detail`, `display.outline`.

`game-settings` now steps Drawing detail through 85, 70, 50 (store and seam each time), Outline to Light, reads both
back from the file, and goes back to 100 and Full; its "fits" check (the spread at 1280 x 720 and 1920 x 1080 at 80,
100 and 150 percent: nothing cut or overlapping) covers the two new rows.

**How the page looks** (`build/n10/r2/settings-display-*.png`, looked at): at 1920 x 1080 the right column reads
Reading the page (three rows), then the heading Drawing with Drawing detail (100 %, its note) and Outline (Full),
with room to spare below; the left column is unchanged. At 1280 x 720 and 150 percent the right column scrolls (its
scroll bar shows and the Outline row is reached by scrolling), as the left column already does at that size; nothing
overlaps or is cut outside the scrolled column.

**How the town looks at each detail** (closest view and the follow view, 1920 x 1080, `build/n10/shots-r2/`, looked
at side by side at 1:1 and 2:1; `build/n10/r2-closest-crops.png`, `r2-detail-crops.png`):
- **85 percent: the ink holds.** The line is a touch softer and the cobbles' joints a little less crisp; at 1:1 I had
  to look twice to see it. A good first step down.
- **70 percent: clearly softer.** The ink line is wider and greyer at its edges, cobbles and the ground's grain
  blur, figures lose their small details; everything still reads (roads, plots, people), it looks like a smaller
  window scaled up.
- **50 percent: blurry.** The ink smears into a soft dark band, cobble joints and the figures' faces are mush, text on
  the map (tags) is unaffected (it is 2D). Usable on a weak card, but plainly a lower quality; the note under the row
  says the map gets softer.
- **Outline Light at 1080p**: almost the same as Full. Against Full in the same build: 22,728 pixels (1.1 percent)
  differ in the follow view and 30,143 (1.5 percent) in the closest view, largest 122, all on the ink's outer edge
  (the line at the play view is about 1.5 px, so only the outer half-strength ring goes: edges read a little
  thinner); the whole-book view is identical (its line is already one step). It matters at 1440p and above, where Full draws two
  and three steps.

The interface stays sharp at every detail (it is drawn at full size over the scaled 3D view).

**The matrix for the two settings** (`perf-draw`, first-age town, 1920 x 1080, llvmpipe; two runs, 21:24 and
21:43; ratios against the mean of the second run's two baselines, 632 and 604 ms):

| Variant | run 1 mean ms | run 2 mean ms | run 2 / baseline | pixels drawn |
| --- | --- | --- | --- | --- |
| baseline (100 %) | 897 (an outlier: p95 1168) | 632 and 604 | 1.00 | 1.00 |
| detail 85 % | 504 | 445 | 0.72 | 0.72 |
| detail 70 % | 353 | 363 | 0.59 | 0.49 |
| detail 50 % | 224 | 232 | 0.38 | 0.25 |
| outline light | 617 | 624 | 1.01 | |

Drawing detail follows the pixels down to about 70 percent, then the fixed costs (shadow map, the scale-up, the
interface) show: 85 gives a quarter of the frame back, 70 two fifths, 50 three fifths. The light outline gains
nothing measurable at 1080p (there the hard ink is mostly one step already); it is for 1440p and above, where the
full outline takes two or three steps (13 to 17 depth reads a pixel).

## 2. The wins in the files that were held

- **Q8, the shop's list** (`TownHud.Shop.cs` `ShopFrame`): `Town.ShopItems` and the grouping ran every frame. Now
  they run when the town object, its tick, its coin, its purchases, its level, whether a choice is open, or the key
  bindings changed, or when any event came this frame (every player action raises one), or the list is empty. The
  per-row work after it (pill state, the fill by coin, the pointer, the modes) still runs every frame, unchanged.
  Between two ticks with nothing happening (5 frames in 6 at 60 fps and 1x) the list is the last frame's.
- **Q13, the hover pick** (`TownVillagers.Pick.cs` `PickCached`, called by `TownHud.PersonTarget`): the pick (two
  projections and a segment distance per villager) runs again only when the pointer, the radius, the hovered one,
  the camera's transform or size, the viewport, the map's place, the figures' height or any drawn villager's feet
  changed (an exact comparison with the last frame's copy of each; no hash). The click still picks afresh.
  Villagers that walk move every frame, so this saves most when the town is still (paused, the book closed, a page
  open) and when the pointer is off the map is already early (`PersonTarget` returns before picking).
- **Q1 for the river** (`RiverLayer.cs`): the river's stakes (plots and crossings) add their words to
  `RiverLayer.Words` when they are made, cleared with the layer; the tags read it in the tree's order (stakes are only
  appended, never moved or freed alone). Only the boats are still searched (their files are on another branch).
- **Q11, the labels' layout: not done.** Tried the exact part (read the 14 cards' rectangles only in a frame where a
  tag is placed) and took it back: the villagers' trade tags (`TownHud.PeopleTrade.cs`, new today) read the same card
  list in the next frame, so skipping it would let a trade tag lie on a card. A full "only when a card, the camera or
  a tag changed" needs each card's rectangle, the camera and every tag's source position compared each frame, which
  is most of what the layout itself reads; the saving is small for the risk. What it would need: one list of card
  rectangles owned by `TownHud` and refreshed on a card's own change (resize, fade, show), read by both tag kinds.

`game-perf` before (round 1's branch with the trades merged, 20:27) and after these three (21:20), CPU ms a frame
(mean / worst) and managed bytes a frame:

| | before | after |
| --- | --- | --- |
| town, 28 villagers, 1x | 0.741 / 2.18, 505 B | 0.800 / 2.76, 380 B |
| town, 8x | 0.934 / 2.13, 915 B | 0.999 / 2.10, 820 B |
| river, 35 villagers, 1x | 0.848 / 3.07, 699 B | 0.735 / 1.61, 574 B |
| river, 8x | 0.859 / 3.80, 1538 B | 0.956 / 3.99, 1548 B |

The bytes fall by about 125 a frame at 1x in both ages (the shop's list and the pick no longer built each frame);
the CPU means move by up to 0.1 ms either way between two single runs (the machine is shared; the first-age 1x
rose 0.06 ms, the river 1x fell 0.11 ms): within this check's noise, so no CPU gain can be claimed from these
numbers. At 8x the town steps every frame, so the shop's list is built every frame anyway.

## 3. The ground's rest (on paper only)

What a pixel of plain grass far from any road, field and river still does now (`ground.gdshader` after round 1):

| Work | Reads | Noise (hash12) | Note |
| --- | --- | --- | --- |
| wear texture at the pixel (`wh`, for the hollow way and the overlay) | 1 | | needed for `overlay` only there |
| near mask | 1 | | |
| meadow: `two(g0)` | 2 mipmapped | | the colour itself |
| `n2` (used later by gravel, mud and the bank) | | 4 | |
| field mask | 1 | | |
| gravel strip (any road paved: on in most of the first age) | 1 | 4 to 5 (stones) | weight 0 away from a road |
| cobble strip (cobbles anywhere) | 1 | 1 | weight 0 away from a road |
| crowding (a crowded road anywhere) | 1 | | weight 0 away from a road |
| river centre for the row | 1 | | |
| `sw` swirls, kept outside the river branch for its derivative | | 8 | weight 0 away from the river |
| flood band mark (second age, `band_mark`) | 1 | | |
| paper grain | 2 mipmapped | | the look |
| `light()`: the shadow map | soft filter taps | | the look |

So about 11 to 13 reads and 17 to 18 hashes, against 55 to 60 reads and about 56 hashes before. Of these, the ones
whose weight is nought there: gravel, cobble and crowd (3 reads, 5 to 6 hashes), `sw` (8 hashes), `n2` when no
gravel or mud is drawn.

Next steps, most promising first:

1. **Gravel, cobble and crowding behind the near-wear mask.** They lie on worn cells (a paved road is worn), so where
   the mask is 0 their strip, rim and wash weigh exactly 0, the same argument as round 1's. Gain: 3 reads and 5 to 6
   hashes on most pixels; their `fwidth` would be taken inside the branch, where the boundary pixels weigh 0.
   Risk to the look: none if the pictures stay identical (they should); half an hour plus the picture run.
2. **`sw` inside the river branch**, its derivative taken there: on the boundary quads (3 m and more from the bank)
   the swirl weighs 0, so a wrong derivative there changes nothing; elsewhere in the branch all four pixels of a quad
   take it. Gain: 8 hashes a pixel on most of the screen. Risk: low (relies on quads being whole inside the branch
   where the swirl shows; check the river pictures pixel for pixel).
3. **Value noise from a small texture** (a 256 x 256 R8 tileable noise, one bilinear read for `vnoise`): 4 hashes and
   the smoothstep become one read, for every noise left (`n1 n2 n3`, the warp, strokes, swirls, the bank's lip).
   Gain: biggest near trails and the river, where most noise is spent. Risk: the noise's values change, so the
   grass, the paths' edges and the water's swirls look slightly different (same kind, different pattern): an art
   check, not a free win.
4. **Shadow map 2048 instead of 4096** as part of "Drawing detail" below 100 (shadows are 17 to 19 percent of the frame
   here): softer shadow edges at the lower settings only. Risk: visible, but only where the player chose less detail.
5. **The ground drawn once into a texture for the zoomed-out view** (the whole page, about 2048 x 2048, redrawn when the
   wear uploads, used above the page-stage zoom where a metre is under 4 pixels): the ground pass becomes one read a
   pixel there. Risk: the water's motion would stop in that view unless the river is left live; a seam where the view
   switches; a day's work. Gain only in the book and page views (0.66 of the frame now).

The Drawing detail setting is the lever that needs none of this: 0.37 of the frame at 50 percent.

## 4. game-town's "a hand is offered at a queued workplace"

Ten runs of `town.tscn` under the lock (runs 1 to 5 at 20:47 to 20:51, 6 to 10 later), on this branch with the
main branch merged: **4 failures in 10** (runs 2, 4, 6 and 9; the other six pass, each in about 41 s). With the three runs of round 1
(one failure, two passes) that is 5 in 13. Three of the four failures stop at game time 30,430 s, one at 30,503 s.

When it fails the town looks the same each time: **level 16 (the end of the first age), game time 30,430 s (once
30,503 s)**, four workplaces open with two places each, no hand bought, none offered; workplace 2 has 2 busy and a
queue of 1, the others 0 or 1 busy and no queue. The test, after the charter drive, lets the bot play up to four more game hours until a hand is offered
(`TownTest.V3.cs`: `RunBotUntil(... HandOffered ..., 20 * 60 * 60 * 4)`); a hand is offered when a workplace has a
queue of `TownRules.HandQueueLength` (2) or more for `HandQueueSeconds` (30 s). In the failing runs no queue of two
lasted 30 s in those four hours, and the bot stopped at its cap (nearly the same time each failure, so the
run is close to deterministic after some earlier point that differs between runs: the test's first part is real
input over real frames). With trades since today the villagers spread over more work and lines are shorter, so a test that waits for
a line of two now sits on an edge: some towns never make one. Not changed (as asked).

## 5. The checks (each once, under the lock)

Branch `wt/n10-perf`, round 2's code (21:20 to 21:45 UTC, each under the lock):

- `game-perf` PASS: town 1x 0.800 ms mean / 2.76 worst, 380 B; 8x 0.999 / 2.10, 820 B; river 1x 0.735 / 1.61, 574 B;
  8x 0.956 / 3.99, 1548 B (gates 6 and 10 ms, 1536 and 6144 B).
- `game-settings` PASS: `sound display(18 seam calls) keys saving reopen damaged keyboard(183 keys) fits(2 sizes x 3
  interface sizes x 4 sections)`.
- `game-layout` PASS: states 36, canvases 8, checks 923 (312 s).
- `game-people` PASS at 60/30/120 (pickbytes 0; bytes 554 / 628 / 447, before 681 / 734 / 582).
- `game-trades` PASS at 60/30/120 (hint frame 2264 / 2696 / 2704 B).
- `game-motion`: the first run failed at 120 fps on one frame ("settings-open: frame 114 took 13.49 ms of CPU (gate
  6)"; 60 and 30 passed); run again under the lock: **PASS at 60, 30 and 120** (cpu max 1.95, 3.79, 3.86 ms).
- `game-readout` PASS: cost at 60 fps 25.6 us and 21 B a frame (gates 50 us, 300 B); the box now one line taller
  (the detail line): 1280 x 720 at 12,476 250 x 188, clear of every zone.
- `game-ink` PASS with its ceilings untouched (cottage-a0 0.169, mill 0.221, villagers-group as before).
- `game-smoke` PASS; `game-river` PASS (the river's words come from the list now).
- `game-town`: 4 failures in 10 (section 4).
- Not run: `./check` as a whole, the screenshots, game-playthrough, game-book, game-menu, game-work, game-save (not
  touched this round).

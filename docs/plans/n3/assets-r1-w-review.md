# Assets round 1, track W: review (work buildings, nature, fields, stones)

Reviewer: judged from the pictures in `build/n3/assets-audit/r1-w/` against `before/`. Nothing was changed. The black-fill picture is not produced by the audit, so "page" is judged from the silhouette of the normal view, squinting. Villager = the red pawn in the pictures (1.9 m).

Short verdict: the track is a real step up on fields, mill, market stall, trees and charter stone. The well, bell post and seal stone are barely changed. The fields and the trees still look generated, not drawn. Nothing is a blocker for play; several things spoil the town picture.

## 1. Per asset

Key: Page / Normal / Close / Palette / Size / Differs.

### Mill (mill.py, 1244 tris)
- Page PASS: tall octagon tower, gabled cap, cross of sails: named a windmill at once.
- Normal PASS: dark tower on a stone plinth, three paler bands, red cap, pale sails with panels. Tallest thing in view, about 2.5 villager heights of tower plus cap. Sails now upright, not flat.
- Close PASS: reefing stage, rails, flour sack, patched sail all show.
- Palette PASS. Size PASS (taller than the cottage; sails about 7 m).
- Differs PASS (nothing else like it).
- Faults (not fails): (a) the cap is a straight gable like a barn roof, not the "boat-shaped" cap the notes claim; at the normal view it reads as a red box on top, and from the front (town-mill) it is a pitched red barn roof. (b) The sails hang in front of the cap and cross the tower body, so in the town view (town-mill-normal) from the plot's yaw the sails are edge-on and almost vanish behind the porters' sacks: you see a thin white bar, not a windmill. (c) The tower is almost all one dark brown; the door is not visible at the normal view.
- Fix (SHOULD): round the cap: a half-cylinder ridge 2.4 m long, Brick with a Brick-shade underside and a Paper ridge band, so the silhouette from above is an oval, not a rectangle. Stand the sails 0.6 m clear of the cap and widen each sail to 1.4 m so the cross still reads edge-on. Put a Cast-iron green door 1.2 x 2.0 m on the plinth side facing the door yaw, so it reads at the normal view.

### Market stall (608 tris)
- Page PASS: a striped rectangle on four legs: reads as a stall.
- Normal PASS: awning Brick and Limewash stripes, goods readable as colour dabs (red basket, green cabbages, sack, cheeses). The goods are only 0.2 to 0.3 m: they are dabs, not shapes.
- Close PASS: good. Basket, cabbages, cheeses, loaves, crate, stool, sack all clear.
- Palette PASS. Size PASS (4 x 3 m, about 2 villagers wide). Differs PASS (only one).
- Fault: awning covers the back two thirds, so the goods are in the open and the stall reads as a table with a hat. That is fine. Fix (SHOULD): grow the goods to 0.35 m minimum so they read at the normal view without the close picture.
- Not seen in the game: town-market shows only the empty plot marker ("The market, to buy"), so the stall was not shown in the town at all.

### Trees: oak, willow, poplar, birch, hawthorn (up to 300 tris)
- Oak: Page PASS (round three-lump crown on a clear trunk). Normal PASS. Close FAIL-ish: the light cap of each lump is a flat pentagon plateau (a geometric facet, not foliage). Palette PASS. Size PASS (about 5 villagers tall).
- Poplar: Page PASS (column). Normal PASS. Close PASS. Differs from oak: yes.
- Birch: Page PASS (lollipop on a pale stem). Normal PASS, but the crown is small and thin: it reads as a sapling beside the others, and its colour is the same yellow-green as the poplar's light caps. Close PASS (white banded trunk is the best part). Fix: crown 5 m wide, not 3.5 m; add a third lump low on one side; use Meadow, not Bent grass, for the lump bodies, leave Bent grass for the caps only.
- Willow: Page FAIL-ish: an umbrella. The drooping strands are 5 to 6 stiff triangles, at the normal view they read as the jagged legs of a spider or a jellyfish, not weeping. Normal FAIL. Close FAIL (strands are flat yellow cut cards). It is also the only tree with no shade tone and no dark underside, so it looks flat. Before it was equally weak; now slightly better in colour. Fix: 12 to 16 strands in two rings, each 0.25 m wide and 2.5 to 3 m long, alternating Bent grass and Meadow, strands overlapping the crown edge, trunk visible inside; add a Cast-iron green shade ring under the crown (the other trees have it).
- Hawthorn: Page PASS (low lump, hedge). Normal PASS, but near black (Cast-iron green is #44604F, at the normal view it reads dark navy-black) and the red haws are 2 to 3 px: nothing. Close FAIL: three flat slabs of one dark green, with Bent grass patches that look like holes. Fix: lighten the body to Meadow shade (mix 10 percent Ink, not 40), put 12 Brick haws of 0.18 m round the crown instead of 3, break the top with two Bent grass lumps, not flat plateaus.
- Differs: five kinds do tell apart by size and outline (round, column, lollipop, umbrella, bush). Oak against birch crown is the weakest pair.
- Cost note: the sway shader is not visible in any picture, so it cannot be judged.

### Fields (362, 433, 397 tris)
- Page: all three PASS as a soft patch with a headland (the plank frame is gone). The three do not tell apart at silhouette; they differ by colour only, which is fine.
- Sprouting: Normal FAIL-ish. Dark furrows with neat green dashes, each with a black outline: it looks like a tray of seedling trays, too regular and too graphic. Close not looked at in detail; same. Fix: no outline on the shoot dashes: make them one lighter Meadow band per row, 0.3 m wide, broken at two or three places by unequal gaps, and keep the black ink only for the outer edge.
- Ripe: Normal FAIL-ish. A flat gold sheet with black tick marks scattered across it: the ticks look like glitches, not crop. The "bunched heads" do not show at the normal view. In the town (town-field-normal) the ripe field shows eight to ten gold pentagon blobs outlined in ink on gold, which read as scales or cracked plates, not grain. Fix: drop the ticks and the outlined pentagons; make rows with alternating Thatch and Thatch-light bands 0.4 m wide (so the ink finds the step), and keep three stooks.
- Stubble: Normal PASS. Best of the three: pale rows, five stooks, lying sheaves are visible. Fault: the black ticks at row ends (about 14 of them) are noise. Remove them.
- Palette PASS. Size: about 14 x 11 m, five villagers wide; big and fine. Differs: sprouting / ripe / stubble differ clearly in colour, better than before (before: one pallet of planks in every state).
- In the town (town-field): the stooks stand on the headland at the top edge and a villager stands on them; stooks overlap the ink edge. Put the stooks inside the cut strip, 0.8 m in from the edge.

### Charter stone (254 tris)
- Page PASS (an upright stone on a ring). Normal PASS: pale stone, Brick seal, paved ring. But the stone is shorter than expected: at the normal view it is about 1.4 villagers, and 2.7 m was asked. Close PASS: the standing stone with leaning top, seal with Paper ribbons and the paved pentagon slabs are good; the best civic piece.
- Palette: the ring's brown edge is Trail earth, fine. The paved slabs are slightly lighter than the stone, which is right.
- In the town (town-view-charter): the picture shows the ring of pale slabs west of the bell post and no clear stone; the stone is lost beside the bell post, which is bigger. Also the lead has not yet dropped the PaleStone override, so the seal is pale in the game. Fix: the lead's item 1 in the progress note; and give the stone a 3.4 m height so that it is clearly taller than the bell post frame (about 3 m).
- Differs from bell post: yes (stone against a frame).

### Seal stone (248 tris)
- Page FAIL-ish: a square block, indistinguishable from a plinth or a crate. Normal PASS only because of the blue disc: the disc with the Paper ship is the only reason it is named. Close PASS. Palette PASS (River blue disc).
- Size: about 1.3 villagers, 3 m square footprint: a big flat slab with a small disc.
- Differs from charter stone: yes in colour, no in shape language (both are "stone on a plinth").
- Fix (SHOULD): make it a tall rounded stele or a round drum 1.6 m high with the disc standing on top at 0.9 m diameter, so its black silhouette is a coin on a post, not a square.

### Well (256 tris)
- Page PASS. Normal PASS: thatch roof, round stone drum, blue water. Close PASS: bucket on the rim and the course bar. Almost the same as before; the trodden patch is the only difference at the normal view. Palette PASS. Size PASS (about 1.4 villagers).
- Faults: the thatch roof is a plain tan plank: no ridge, no courses visible at the normal view; the roof bar is "proud" as the builder says. In the town (town-well) the bucket and its long beam lie out on the ground east of the well like a fallen pole: this is the game's rope/beam, and it makes the well read as broken.
- Differs from bell post: roof colour is the same Thatch plank as the bell post's roof: the two read as the same family. Fix: give the well roof a pitch with a ridge, 2 courses and a Brick ridge cap; give the bell post a Slate roof so the two differ.

### Bell post (400 tris)
- Page PASS (frame with a bell). Normal PASS: green bell, flags, trodden patch. Close PASS.
- Same as before in all but the trodden patch and flags. The wide grey side blocks (flags) look like two crates, not paving. Palette PASS. Size PASS (taller than the well: about 3 m).
- Fix (SHOULD): make the flagged base one slab 2.4 x 1.8 m, 5 cm high, rather than two tall blocks; or lower the blocks to 0.3 m.

## 2. The town pictures

Compared with before (town-field, town-mill, town-well, town-bell-green, trees):

- Street / wood and field picture (wood-and-field-normal): BETTER. Five tree kinds in a row over three field states: the strip looks like a map page. What spoils it: (a) the three fields are the same outline drawn three times, big rounded patches with a thick black edge, all with the same headland ring; (b) the hawthorn bushes are near-black blobs between pale trees; (c) the willow strands; (d) the ink on the sprouting dashes and the ripe ticks is too busy next to the clean trees. Colour variety is good.
- Town view level 5 (town-view-level-5-normal): BETTER than before, but only slightly. The town is a paved lane, a well, a bell post and one cottage at the edge; the first age is still empty. Fields and mill are not in this frame. What still spoils it: the well's bucket-beam lying on the ground; the goods table (the tray with fruit at the well) looks like a dropped plank; the trees are a few lumps at the edges. The bell post and well use the same roof, so the two main civic pieces blur.
- Town view level 9 and charter (town-view-level-9-normal, town-view-charter-normal): BETTER in ground and in the stone paving; the lanes look good. What spoils it: the villagers' loads (yokes) dwarf the well; the charter stone is not found at once in the charter frame (the pale pave ring sits left of the bell post and the stone itself is lost); the cottages at the edges are the other track's.
- Town mill (town-mill-normal): BETTER: a real windmill tower stands where before there was a barn on stilts with a flat blade. Spoiled by: the sails edge-on behind the porters' sacks, the cap as a barn roof, and the queue of porters with grain sacks piled to the north (the hexagon sacks are bigger than the mill door).
- Town field (town-field-normal): BETTER in shape (no planks), WORSE in noise: the ripe field shows outlined gold pentagon blobs and the stooks stand on the edge.
- Market: not judged: only the empty plot marker was shown.

## 3. MUST FIX, worst first (at most eight)

1. Ripe field: remove the black ticks and the outlined pentagon blobs; use two alternating Thatch tones in 0.4 m bands (field.py).
2. Willow: rebuild the weeping strands (12 to 16, 2.5 to 3 m, overlapping, two tones) and add the shade ring (tree-willow).
3. Sprouting field: drop the black outline on the shoot dashes; one Meadow band per row, uneven gaps (field.py).
4. Hawthorn: lighter body, 12 haws of 0.18 m, a Bent grass lump on top; at the normal view it is a dark blob (tree-hawthorn).
5. Charter stone: the lead drops the PaleStone override (his item 1); and raise the stone to 3.4 m so it beats the bell post frame in the town.
6. Mill in the town: from the plot's yaw the sails are edge-on and vanish. Move the sail plane 0.6 m forward of the cap and make the cap an oval Brick drum, not a barn gable; add a visible door (mill.py).
7. Seal stone: a square block; make it a drum or stele with the disc standing on top so its silhouette is not a plinth (river-town.py).
8. Stubble and ripe: remove the row-end ticks; move the stooks 0.8 m inside the edge so no one stands on them (field.py).

## SHOULD FIX

- Well: pitched thatch roof with a ridge and two courses; bell post: Slate roof, so the two differ. Tell the lead about the lying bucket-beam in the town.
- Bell post: one low paved slab instead of two grey blocks.
- Oak and poplar: the flat pentagon plateaus on the light caps; break each cap into two lumps of different heights.
- Birch crown 5 m wide with a third lump.
- Market stall goods to 0.35 m minimum.
- Lead's item 2 (sails turn always, slowly): do it; a still mill looks dead.
- Tree sway cannot be judged from pictures: take one two-frame strip in the next round.
- Pasture and sheep were not done: say so in the ranking.
- The audit does not make the black-fill picture: add one so the "page" test can be read directly.

## 4. Better, same or worse than before

| Asset | Verdict |
| --- | --- |
| mill | Better |
| market stall | Better |
| oak | Better |
| poplar | Better |
| willow | Same |
| birch | Better (new) |
| hawthorn | Same (new, weak) |
| field sprouting | Better |
| field ripe | Better |
| field stubble | Better |
| charter stone | Better |
| seal stone | Same |
| well | Same |
| bell post | Same |
| town: mill | Better |
| town: field | Better |
| town: well and bell green | Same |
| town overall (levels 5, 9, charter) | Better |

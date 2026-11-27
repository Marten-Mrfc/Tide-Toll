# Assets round 2, track RV (river, banks, crossings, boats): review

Reviewer judged from the pictures in `build/n3/assets-audit/r2-rv/` against `before/`, and the style sheet section 10. Nothing was changed. "Page" = silhouette/main colour at whole-town scale (judged from the town-view pictures, the black fill itself was not rendered). "Normal" = the -normal picture beside the villager and the in-town pictures. "Close" = the -close picture.

Overall: a real step up. Every crossing now reads as a different kind of crossing, the river has banks, the boats have crew and are Brick against slate water. The faults left are mostly: clutter where things pile on the landing, reeds that look like red lanterns on stalks, a ford that does not look like a way across, a muddy grey-blue low-water flat, and a ferry raft that is hard to read.

## 1. Per asset

Key: P = page, N = normal, C = close, Pal = palette, Size = beside villager (1.9 m), Diff = differs from neighbours.

| Asset | P | N | C | Pal | Size | Diff |
| --- | --- | --- | --- | --- | --- | --- |
| reeds (3 sizes) | PASS (gold tufts with dark tops along bank, in town-view-river) | FAIL (a 1.5 m clump is the size of a villager's torso, a dark scribble; sizes small/mid/tall are not told apart) | FAIL | FAIL (Brick seed heads, the darkest and most saturated thing on the bank) | FAIL (reeds-small barely 0.5 m wide) | FAIL (three variants are the same shape scaled) |
| bank-stones | PASS (not seen) | FAIL (a grey lump the size of a boot, a stray pebble) | PASS | PASS | PASS | n/a |
| ford-stones | FAIL (a row of grey dots reads as "rocks", not a crossing) | PASS (one stone 1.3 m, villager-sized) | PASS (nice ripple crescent) | PASS | PASS | PASS |
| footbridge span | PASS (clear arched bridge, golden planks) | PASS | PASS | PASS | PASS (10 m x 2.6 m, hump reads) | PASS (its own gold, not like the paths) |
| footbridge end | PASS | PASS | PASS | PASS | PASS | PASS |
| ferry post | PASS (tall dark post, Brick cap, flag) | PASS | PASS | PASS | PASS | PASS |
| ferry rope | FAIL (nothing to see alone) | PASS in place (dark sagging line) | n/a (alone it is two flecks) | PASS | PASS | PASS |
| ferry raft | FAIL (a brown smudge with a black bar; the green cap and Brick-white pole stand on top and confuse it) | FAIL (rails, crate, ferryman and pole mush into one dark rectangle; no clear deck) | FAIL (rails dark, deck planks the same tan as the ground paths, a lone Thatch plank rail floats off the right edge) | PASS | PASS (about 5 x 3.5 m) | FAIL (reads like the landing's deck) |
| drawbridge towers + frame (lift) | PASS (dark towers, X-brace) | PASS | PASS (Slate caps, counterweight; busy but readable) | PASS | FAIL (tower 7 m plus a 10 m frame is bigger than any building; a reader sees a gatehouse, not a bridge) | PASS |
| drawbridge span | PASS | PASS | PASS | PASS | PASS | PASS (dark timber vs the footbridge's gold) |
| drawbridge end | PASS | PASS | PASS | PASS | PASS | PASS |
| rowboat | PASS (Brick against water) | PASS (oars out, boatman) | PASS | PASS | PASS (5 m) | PASS |
| barge | PASS | PASS (Slate cabin, Limewash yard) | PASS | PASS | PASS (11 m) | FAIL beside the rowboat only in the cargo: cloth bolts striped Brick and Limewash look like an awning (seen twice stacked on the barge in landing-with-boats-normal) |
| landing | PASS | PASS | PASS (piles with Paper caps, Brick tarp, barrel, sack heap, lantern) | PASS | PASS | PASS |
| crane | PASS (arm and counterweight) | PASS | PASS (Limewash sack on hook) | PASS | PASS | PASS |
| tide post | FAIL (a 5.5 m striped needle: at the page it is a speck and its pointer is invisible) | FAIL (a 0.5 m wide pole; the pointer, its whole job, is not visible) | PASS | PASS | FAIL (5.5 m tall is almost the mill's height for a gauge) | PASS |
| river water + banks (shader) | PASS | PASS | PASS | FAIL at low water (flats grey-blue, not mud) | n/a | PASS |

### Fixes for every FAIL

- **reeds, palette and size.** Change the seed heads from Brick to Mud-over-Thatch (a brown cylinder 0.12 m wide by 0.35 m long on a stem), no Brick anywhere. Make the three variants different in shape: reeds-small = three short blades only, no heads, 0.9 m; reeds = five blades and two heads, 1.6 m; reeds-tall = seven blades, two heads, 2.2 m, leaning. Drop the pyramid hub at the base (it reads as a yellow cone). Blades 0.14 m wide at the base, in Bent grass light and Cast-iron green, not three-sided spikes. Plant in rows of 5 to 9, not scattered singles, so the bank gets a band of reed rather than dots.
- **bank-stones normal.** Enlarge to 1.8 m wide, two stones touching, one stone 0.5 m high, Stone grey with a Paper top; or drop them: at the normal view they are litter.
- **ford-stones at the page.** The row alone is not a crossing. Add a worn Trail earth path 2 m wide on each bank running into the first and last stone, make the stones a little larger (1.6 x 1.3 m) and space them 1.6 m apart so they touch visually, and keep the Paper treads. Ban trees from the bank end of a ford (a tree sits on the last stones in crossing-ford-normal).
- **ferry raft.** Make the deck one clear Packed earth slab with Paper edge band 0.15 m, only two rail posts per side (not six), rails Thatch not dark, remove the lone Thatch pole from the right edge, put the Brick-white pole upright at the bow as a mooring post with a 0.3 m Brick cap. Keep the ferryman, sacks and crate; make the crate Thatch, not the deck's tan. The green cap on the near ferry post must not overlap the raft in the ferry's normal view; move the raft attachment 1.5 m out.
- **drawbridge size.** Lower the lift frame so the top is 5 m (not 7 m), shrink the towers to 0.9 x 0.9 m. The raised leaf must be one clear Packed earth plank slab standing at 70 degrees in a different tone (Packed earth) from the towers (Soot brick) so it reads as a leaf, not as more frame.
- **barge cargo.** Cloth is a rolled bolt (style sheet section 6): draw it as a plain Brick cylinder 0.6 m long laid across the hold, three of them, no Limewash stripe. The Limewash stripe is the awning's.
- **tide post.** Make it a stubby gauge: 3.2 m, 0.7 m wide, bands 0.5 m, and the Thatch pointer a flag 0.9 m wide, standing on a small Stone plinth. It must read from 70 m: put one Brick band at the top. Place it so it is in the landing pictures.
- **low-water flats.** In the ground shader, mix Mud (not River) into the flats: the shallows band is not allowed over the flats (flat colour Packed earth over Mud, puddles River light, no grey).

## 2. Things in place

Compared with the BEFORE sheets, honestly:

- **town-view-river (70 m, the river town):** More beautiful. The river now has a Brick-brown lip each side, the slate water has soft flecks, the crossings are clear, the ford stones read as stones. Still spoils it: (a) the reeds are dark specks with red heads and, at this scale, look like dirt on the bank, (b) the footbridge and drawbridge stand alone with no path leading to them at the far end, so they look dropped on the bank, (c) the whole bank is too uniform: the lip is the same brown width everywhere, and the one grass-green sliver on the east bank is the only break.
- **town-view-ferry (40 m):** Better. The raft and the two posts, with the sagging rope, read as a ferry. Spoils it: the raft is one dark blob (see section 1); the near post's Brick-white flag and the raft's Brick pole fight; two palm-like willow trees sit on the footbridge end at the right, hiding the abutment.
- **landing-with-boats:** Clearly better than before, the white heaps are no longer boxes and the boats are Brick against the water. Spoils it: (a) the villager load heaps at the landing and below the crane are white clouds that bury the crane's foot; (b) the barge carries two striped bolt stacks side by side that read as two awnings; (c) the crane stands so close to the landing that its jib crosses the cottage roof; (d) a big willow stands on the landing path directly in front of the jetty and hides half of it.
- **river-low-water / landing-low-water:** The two states are clearly different (good). Spoils it: the flats are grey-blue and look like wet slate, not mud; the flat bands are two grey slabs with a blue stripe: it reads as a road, not a tide flat.
- **crossing-ford:** The best ford picture, but it reads at low water as "stones in a ditch". The tree at the east end sits on the last stone.
- **crossing-footbridge:** Strong. This is the best picture of the set. Spoils it: the abutments are small grey boxes that look unrelated to the bank, and the bridge is wider than the river lane.
- **crossing-ferry:** Fine. The raft is the weak part (above).
- **crossing-drawbridge-down / lifted:** Better. Down, the gate looks like a gatehouse with a bridge at the right; the queue of villagers on the span is believable. Lifted, the leaf is hard to tell from the frame. Spoils it: the lift frame hides about half the deck; the villagers' load heaps cover the span's far end.

## 3. MUST FIX (worst first)

1. Ferry raft reads as one dark blob in the normal view: simplify to one clear deck slab, two posts a side, a Brick-white mooring post, remove the stray rail and the green cap collision.
2. Reeds: Brick seed heads are the loudest thing on the bank, the clump is a pyramid hub with spikes, and the three sizes are one shape. Rebuild as in section 1 and plant in bands.
3. Low-water flats read grey-blue slate, not mud: the shallows band must not overlay them.
4. Ford: no approach to the first and last stone, tree on the last stone, so it does not read as a crossing at the page.
5. Drawbridge lift frame is too tall (7 m + 10 m frame) and hides the deck; a lifted leaf must read as one tilted slab, not more frame.
6. Barge cloth cargo reads as a second awning: draw the cloth as three plain Brick rolls.
7. Tide post is a 5.5 m thin needle whose pointer cannot be seen at the normal view or the page: make it a 3.2 m stubby gauge with a big Thatch pointer flag, and put it in frame of the landing.
8. A willow stands in front of the landing on the path, a crane's jib crosses the cottage roof, and the white load heaps bury the crane's foot: the lead must move the tree and the crane 3 m off the cottage (placement, not model), and track P must keep load heaps off the crane's footprint.

## SHOULD FIX

- Bank-stones are litter at the normal view: enlarge or drop.
- Footbridge abutments: widen to match the span (2.6 m), Stone with a Paper cap, and make the bridge no wider than the lane.
- Bank lip width varies 1.1 to 1.7 m in the shader but looks uniform at the page: vary to 0.6 to 2.4 m and add one grass bite per 40 m.
- The ferry post's Brick cap and the raft mooring pole must not be the same Brick/white band: keep the band only on the tide post.
- The footbridge and drawbridge need a trodden Trail earth patch on each end of the approach so the bridge does not stand on bare grass.
- Reeds in the first age: none (accepted, the river there is a dashed channel).
- Ferry rope alone is invisible: fine, it works in place; the audit picture could show it in a scene.

## 4. Better, same or worse than before

| Asset | Verdict |
| --- | --- |
| river water and banks | Better |
| reeds | Better (before they were a dark scribble) but still weak |
| bank-stones | New (same as nothing at the normal view) |
| ford stones | Better |
| footbridge span | Better |
| footbridge end | Better |
| ferry post | Better |
| ferry rope | Better |
| ferry raft | Same |
| drawbridge towers and frame | Better |
| drawbridge span | Better |
| drawbridge end | Better |
| rowboat | Better |
| barge | Better |
| landing | Better |
| crane | Better |
| tide post | Same |
| low-water flats | Worse (grey slate) |

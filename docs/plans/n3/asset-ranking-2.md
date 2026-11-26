# Asset ranking 2: river, props, book, textures, icons, sounds

Reviewer 2, 26 November 2026. Judged from `build/n3/assets-audit/before/` (sheets river-1, river-2, prop-1, prop-2, book-1, texture-1, icon-1 to 5, sounds.md, assets.json) and the town pictures `build/n3/ui-audit/1920x1080-100/` u10, u13, u18 (u32 and u34 do not exist). Style: the Living Map (`docs/asset-pipeline.md`, `docs/plans/n3/style-sheet.md`): ink outline, small warm palette, readable from above, toy proportions. Reference sizes: villager 1.9 m (drawn 2.6 m tall in the model, about 11 px at play zoom), cottage 8 x 6 m.

OWNER = on the owner's list ("feels off").

## 1. Ranking, worst first

Common fault of nearly every river model: **the normal view is a 40 px brown or grey smudge next to a villager.** They are built life-size (2 m wide bridge, 1.6 m ford) but the villager is drawn 1.5 times bigger, so everything of the river is small, thin and the same plank brown. The close-up pictures are fine; the town pictures are where they fail. The fix for almost all of them is the same: widen to the module of the cell (2 m) times 2, add one bright accent from the palette, and give the silhouette a shape that is not a rectangle.

| # | Asset | At the normal view | Close up | Rework note |
| --- | --- | --- | --- | --- |
| 1 | **river and banks** (river-high-water, river-low-water, texture-water-river) OWNER-adjacent (crossings sit on it) | A flat slate-blue ribbon with one dark line either side. No bank at all: grass runs straight into water; no reeds (`reeds.glb` is UNUSED), no mudflat although `Wet mudflat #6F6450` is in the palette; no foam, no shimmer. At low water the ribbon just narrows. Reads as a road painted blue. | Water texture is horizontal dark dashes, all pointing one way: the pipeline forbids this (camera turns, marks must go every direction). Looks like ruled paper, not water. | Draw a bank strip 1.5 m wide along both sides: Wet mudflat at low water, Packed earth edge at high. Plant `reeds` (2 x 2 m, 1.5 m high) every 6 to 10 m on the outer bank, on a seeded scatter. Replace the dashes by short curved ripple arcs in River/Deep water in random headings, plus 6 to 10 Paper highlight flecks that slide (shimmer). Add a thin Paper foam line where water meets bank. |
| 2 | **ford-stones** OWNER (crossing) | Five grey chips 0.3 m high, about the size of a villager's head. In the town they are three loose clusters of dots, they look like litter, and the crossing has no line. | Grey rocks, ok but tiny and all the same colour (Concrete). | Make a stepping line, not a cluster: 5 flat-topped stones 1.0 to 1.4 m across, 0.45 m high, in a gently curved row 2 m apart across the whole width, Concrete with a Paper top and Wet mudflat wet foot. Add a small white ripple ring round each stone and a Packed earth worn patch on both banks. |
| 3 | **footbridge** (span + end) OWNER (crossing) | A thin brown rectangle with rungs: reads as a ladder lying on the water or a fence. No ramp, no link to the paths; deck is the same brown as the paths. | Planks and rails are decent but rails are thin (0.1 m) and the span is a bare slab. | Make it 2.6 m wide with a hump: deck arched 0.5 m, rails 0.2 m thick in Soot brick with Paper post caps, plank courses in Trail earth and Packed earth alternating, two stone abutments (Concrete, 1.2 x 1.2 x 0.6 m) at the banks, a Cast-iron green lantern on one post. Under it a Mud shadow band. |
| 4 | **ferry** (raft, post, rope) OWNER (crossing) | Raft is a pallet of planks; post is a 3 px dot; rope (16 triangles, 0.13 m) is invisible. In the town the ferry is a long thin line with a box on it; no one stands on the raft. | Raft is nicely planked but flat; post with lantern is sweet but only 2.8 m. | Raft 3.2 x 5 m with a low Mud rail on three sides, a Thatch cargo pile and a ferryman figure (reuse villager, Cast-iron green tunic) on it. Post to 4.5 m with a Brick painted cap and a Thatch flag. Rope: draw it as a 0.15 m ink-dark catenary line from post to post (sag 0.8 m) that rises out of the water when pulled. Paired landing steps (two Packed earth steps) on each bank. |
| 5 | **drawbridge** (lift, span, end) OWNER (crossing) | Lift tower (1188 tris, over the landmark limit of 1,500 almost) is a pale scaffold of thin beams that blends into the grass; lifted state in the town is a tangle of lines and a white mess of cargo. Span is a plain plank. | End with red and white barrier arm is the best part. Tower beams are 0.1 m: noise. | Replace the frame by two chunky towers 1.4 x 1.4 x 6 m in Soot brick with Slate roof caps (a gatehouse), a Brick and Paper striped barrier arm on both ends (as the end piece), a visible chain (0.12 m) to the span. Lifted: the span stands up at 70 degrees in Trail earth, its underside Mud. Cut to under 700 triangles. |
| 6 | **rowboat** OWNER (boats) | Dark brown sliver 2.4 x 6.7 m, same colour as mud; reads as a plank. No oars, no seat, no figure, no cargo. | Clear boat shape but flat brown hull, no strake lines, no bow. | Two-tone hull: Brick outside, Paper inside rim and a Thatch gunwale stripe. Add two oars (0.15 m wide, Packed earth) sticking out at 40 degrees, a boatman (villager head and shoulders, River tunic) and a Paper sack at the bow. Pointed bow raised 0.3 m. |
| 7 | **barge** OWNER (boats) | Reads as a boat, which is good, but cargo is invisible and the whole thing is a cream mast bar plus brown oval; in the town it is almost hidden under its own label. | Slate cabin and barrels are fine; boxy. | Hull 12 x 3.6 m, Soot brick sides with a Thatch wale stripe, cargo hold open and heaped in the cargo's colour (Wool Limewash domes, Cloth Brick rolls, Grain Thatch sacks) so the load changes colour with what it carries. Limewash square sail 4 x 4 m, furled when moored. A Brick pennant. |
| 8 | **book-table** OWNER | Nearly black brown, flat; in u18 40 percent of the picture is this empty dark. A few faint diagonal lines. Nothing sits on it. | Flat; grain not visible; sharp-edged shadow band only. | Wood table in Packed earth and Mud with wide planks (0.4 m), seeded grain lines in two tones, a 1 px Ink plank gap. Props at the edge, all above the page: an ink pot (Ink with a Paper highlight), a quill, a Brick wax stick, a Thatch candle with a flame (flicker only), a pebble. Keep the centre empty. |
| 9 | **book-leather** OWNER | Flat brick-brown slab with one thin gold line; spine ribbon a bare red stick. | No grain, no stitching, no corner pieces, no clasp, edges perfectly square. | Soot brick leather with a 3 percent noise grain, blind-stamped double border, four Thatch metal corner caps, a stitched spine (Ink dashes 0.1 m), a clasp strap with a Thatch buckle. Raised centre plaque: a small town-and-river emblem. Round the corners 0.15 m. |
| 10 | **book-paper** OWNER | Cream page with almost invisible blotches; written lines are pale grey (u18: the "Done" column is hard to read); page lies dead flat. | Texture reads as blank paper; 25 identical "Done" rows. | Raise ink contrast (Ink at 85 percent, achievements not done in 55 percent). Add fibre flecks in Packed earth at 4 percent, a faint ruled margin line in Trail earth, deckled page edge, a curve toward the gutter with a soft Mud shadow, page stack lines visible at the right edge, a ribbon marker. Replace the "Done" text with a small ink tick icon. |
| 11 | **tide-post** | A 3 px red and white dot; 3.9 m tall and 1.2 m wide: it does not read as the thing the tide dial speaks of. | Striped lighthouse-like post, charming but tiny. | 5.5 m, 1.6 m wide, Brick and Limewash bands 0.6 m each, a Thatch water-level pointer plate that slides with the tide, a Cast-iron green cap with a bird perch. Plant on a small Concrete footing in the bank. |
| 12 | **crane** | A small dark frame that looks like a gallows; crate hangs on a hair-thin line. | Timber A-frame is good; wheel and hook are thin. | Make the jib 1.2 times thicker (0.35 m), add a Thatch rope coil, a Brick painted counterweight box, a Paper sack on the hook; a Concrete base 3 x 3 m. Sway the load by shader. |
| 13 | **charter-stone** OWNER | Small grey blob at 1.5 villager height; no patch under it; not found at a glance. | Nice slab with oval and text lines; cuts into the grass at the bottom with a chunky hex base. | 3 m tall, on a 5 x 5 m Packed earth and Concrete paved patch with a low step; carved lines in Ink; a Brick banner cloth on a pole beside it; two Meadow flower tufts. One bright accent: a Thatch seal oval. |
| 14 | **seal-stone** OWNER | Grey box with a red dot; the dot is the only thing visible. | Pedestal with red wax seal disc is nice and clean. | Keep the shape (it is good), scale 1.3 times, give the seal disc a small rope loop in Thatch and add 2 Cast-iron green lanterns on the paved square's corners. Make the seal disc glow slowly (emission pulse). |
| 15 | **landing** | Cluster of crates and sacks on a thin jetty; in the town the landing disappears behind heaps of white bale boxes that look like stacks of paper. | Close: planks, crate, barrel, well done. | Dock 4 x 8 m on four visible piles; sacks and crates in goods colours instead of all Limewash; one mooring post with a rope coil; a Brick lantern on a post at the end. Keep stack height under 1.2 m. |
| 16 | **reeds** (UNUSED) | Never shown. | 108 triangles, small tufts. | Use them: see row 1. Make 3 variants (1.2, 1.5, 1.9 m), Bent grass and Thatch with Cast-iron green stems, `ink="soft"`. |
| 17 | **fence-wattle** | Dark brown bar, reads as a bench. | Three flat planks with posts: not woven. | Show a woven look: 4 horizontal Trail earth withies alternating over and under 3 posts in Packed earth; height 1.1 m; each segment varies +-8 percent. |
| 18 | **fence-post** | One-pixel dark tick. | A plain dark box with a flat top, 20 triangles. | Taper the top (4 sides, 0.3 m cap), Paper tip, slight lean 2 degrees, width 0.22 m. |
| 19 | **fence-gate** (UNUSED) | Not shown. | Good five-bar gate, big posts. | Wire it into the fence row where a lane crosses a fence; add a Brick latch. |
| 20 | **prop-flower-bed** | Brown slab with cone-shaped lumps; looks like pawns on a table, not flowers. | Faceted domes in Brick/Thatch/Limewash on a brown tray. | Low raised bed in Concrete edge stones; 12 to 16 small round blossoms (0.25 m, three colours) on Cast-iron green leaf cushions; no cone shapes. |
| 21 | **prop-hen-coop** | Orange blob beside a barn: unclear. | Small barn with a ladder and a hen in a pen; the hen is a polygon pile. | Make the coop a Thatch-roofed box 1.8 x 1.4 m with a Packed earth pen of 4 post fence; 3 hens as Limewash and Brick ovals with Thatch combs. |
| 22 | **plot-stakes** | A heavy black rectangle outline, 8.7 x 6.7 m, the darkest ink in the picture (u10). Reads as a prison yard. | Four small posts and Paper rope. | Show the plot as 4 Paper-capped stakes and a rope in Limewash, ink at half weight; a Brick pennant on one stake; a faint paper-white survey line on the ground. |
| 23 | **prop-signpost** | Thin black "Y": barely visible. | Pale Paper arm and a wooden arm on a stone base: good shape, thin. | Thicken post to 0.3 m, arms 1.2 m long, Brick painted tip, a Thatch lantern. |
| 24 | **prop-handcart / bench / beehives / apple tree** | Readable brown shapes; acceptable. | Boxy but charming. | Beehives: add a bee-line of 3 Thatch dots circling (shader). Bench: add a Paper cushion. Handcart: Limewash sack in it, Brick wheel rim. |
| 25 | **texture-ground-hollow-way** | Dark Mud with blue puddle specks; looks like speckle noise. | Puddle specks are 1 to 2 px dots in the wrong blue (not a palette River). | Fewer, larger puddles (0.5 to 1.0 m) in Deep water and a Paper highlight; rut lines along the path. |
| 26 | **texture-paper-page** | Invisible: reads as a flat cream fill. | Blotches 1 shade off. | See book-paper. Add 1 px fibres and a tonal drift (Limewash to Paper). |
| 27 | **texture-ground-grass / trail / track / trampled** | Fine, but the blobs are large and repeat in two sizes only. | OK. | Add a third, smaller blob size (0.3 m), seeded tufts in Bent grass; check the 4 x 4 tiling for a visible repeat at u10. |
| 28 | **icons: crossing tools** (tool-drawbridge, tool-ferry, tool-footbridge, tool-gate, tool-erase, tool-fence) | Not shown anywhere (UNUSED, 6 files). | At 24 px they are dense blobs (drawbridge, ferry, footbridge are identical: a stack of stripes with a wave). | Wire into the tools, then redraw at 24 px: footbridge = arch with 3 rungs; ferry = raft with rope line; drawbridge = lifted span with chain; gate = 5-bar; fence = three posts and a rail. One 24 px silhouette each. |
| 29 | **icons: dense ui set** (ui-toll, ui-boat-berth, ui-crane, ui-bridge, ui-flow-preview, ui-wear-overlay, up-pace, good-cloth) | At 24 px: unreadable mush; good-cloth looks like a tape measure; ui-toll a cannon; ui-crane a digger. | Heavy 6 px strokes at 192, little interior space. | Simplify to 3 shapes each, keep 2 px stroke at 24 px grid: toll = coin on a gate bar; berth = boat beside a post; crane = hook on a jib; cloth = folded bolt with a band. |
| 30 | **icons: UNUSED utility** (check, lock, hint, request, hire-keeper, raise-bridge, lower-bridge, keys, pause, play, quit, reset, save, settings, sound-off, display) | Not shown at all; locked shop lines have no lock. | Mostly clean. Several fine (lock, pause, play, settings). | Use them (see section 4). |
| 31 | **sounds: 15 unused** (place-01 to 06, erase-01 to 06, boat-turn-away-01 to 03) | n/a | Table: "not found in the cue tables or code". Erase and place peaks at -3 to -5 dBFS are the loudest of all files. | Wire them: place on a model placement, erase on removal, boat-turn-away on `BoatLeft`; trim peaks to -6 dBFS before wiring. |
| 32 | **sounds: levels** | n/a | Quiet outliers: mill-loop (-30.0 LUFS), bells -27.6 to -29.5, bell-long -27.7 to -29.2, moor -28.4, oar-splash-05 -29.1, ambience water loop -27.2. Loud: ui-press -21.7 to -22.9 (the loudest of the one-shots, louder than coin at -24 to -27). Coin varies 3 dB (-23.9 to -27.1). | Bring bells and mill to about -25, water loop to about -25, ui-press to -25; make the six coins within 1.5 dB. |
| 33 | **sounds: missing cues** | n/a | No cue for RoadWornIn, PerkEarned, VillagerArrived, OfferAvailable, DriveProgress, FerryDocked, BoatLeft (turn-away unused), TouchAdded, ChoiceOffered (reuses request-asked). No bird, wind, wood-creak or town-murmur ambience. Sheep peak is -15 to -17: very quiet next to others. | Add: soft chime for worn-in, a pen scratch for achievements, a wood knock for new villager, a small rope creak for ferry, a distant murmur loop, birdsong at day, wind. |
| 34 | **sounds: lengths** | n/a | Looks right: one-shots 0.09 to 0.9 s, bells 3 to 6 s, seal 5 s, charter 4.2 s, mill loop 6 s. Level-up 2.2 s plus achievement 2.0 s can overlap at a milestone. | Keep; add a least gap (the code has one for achievement). |

## 2. What is good and should set the standard

1. **bell-post** (336 tri): chunky timber arch, green bell, pale paving base, readable at once from above, accent colour, charm in the close-up. This is the civic landmark standard the charter and seal stones must reach.
2. **prop-haystack** (112 tri): two-tone dome with a ladder, round toy-like silhouette, perfect colour (Thatch), reads at any distance. Standard for props.
3. **prop-washing-line** (68 tri) with prop-woodpile and prop-beehives: few triangles, Limewash and Brick cloth against Mud posts, instantly clear, the best "small touch" in the game. The same family: the landing's close-up and the seal stone's pedestal are the best river craft.

Honourable: the ground tiles on the palette; icons good-water, ui-bell, ui-lock, ui-save and ui-settings read well at 24 px.

## 3. What is missing from the town to make it beautiful

Sizes in metres (footprint x height). Stand where the sim already puts a plot or lane; none of these changes play.

1. **Chimney smoke**: a soft Limewash puff column on every cottage chimney, 3 puffs rising 2.5 m and sliding with the wind, 8 particles each. Biggest charm for the least cost.
2. **Cottage variants** (2 to 3 more): a Slate roof cottage, a Brick-chimney cottage with a porch, a long cottage with a lean-to; 8 x 6 m; each with its own accent door (Brick, Slate, Cast-iron green, River, Thatch). A street must not repeat.
3. **Birds**: 4 to 6 small dark Ink V-shapes (0.6 m span) gliding in a circle at 12 m high above the whole map; a flock of 12 startles from the field when a villager walks by.
4. **River shimmer and foam**: 10 Paper flecks (0.3 m) sliding on the water, foam arcs at each bank, and ripple rings at the ford stones and boat bows.
5. **Washing on a line between two cottages**: 4 m line, 6 cloth pieces in Brick, Limewash and River (extend the prop-washing-line), swaying.
6. **Bank reeds and a heron**: reed clumps every 8 m on the outer bank (1.5 m), 1 heron (Limewash, 1.2 m tall) standing at the shallows; wades off when a boat comes.
7. **Ducks on the river**: 3 ducks (0.5 m) in a line drifting with the current; they scatter at the barge.
8. **Stone well curb and bucket** with a small Brick-roofed canopy, 2.4 m wide, 3 m tall, next to the well; a bucket swing.
9. **Dovecote / pigeon loft** on the market or bell green: 1.5 x 1.5 x 3 m on a post, 3 pigeons (0.3 m) that fly down to the paved square.
10. **Lanterns on posts** along paved roads, one every 12 m; Thatch lamp 0.5 m with a faint warm glow at night grade (Night tint).
11. **Vegetable garden rows** beside each cottage: 3 x 2 m bed of Cast-iron green and Brick cabbages with a scarecrow (0.9 x 2 m) in Thatch and Brick.
12. **Cat and dog**: a Soot brick cat (0.5 m) asleep on a cottage step; a dog (0.8 m) that follows a villager for a few metres.
13. **Fallen leaves and flowers**: 20 Bent grass tufts with Thatch and Brick flower dots on the meadow, 0.2 m, none on paths.
14. **Market flags and bunting** on the stall: a line of 8 triangles in Brick, Thatch, River; 5 m long, flutters.
15. **Boat wakes and a gull on the mooring post** (0.4 m, Limewash) at the landing.
16. **Stepping-stone path over the stream beds** (small side brooks) 1 m wide, and a duckboard walk where roads cross wet ground.
17. **Rock outcrops** along the river bank (Concrete and Slate roof, 1.5 to 3 m) and a lone big tree on a hill as a landmark.
18. **Stone wall sections** (0.5 x 2 m x 0.8 m, Concrete) round the fields instead of only wattle.

## 4. ICONS: who has no icon of their own, or shares one

How the code picks them: `TownHud.Shop.cs` `IconFor` / `StandInIcon`, `TownHud.Choice.cs` `BoonIcon`, `TownHud.Book.cs:77`. The picture u10 shows the result: **Yoke and Tools are the same basket, Water price and Grain price are the same coin, Extend shows the generic house.**

**Two broken names (file exists, code asks for a missing name, so the stand-in shows):**

- `up-tools` is asked for, the file is `ui-tools`: Tools shows `up-load` (the basket, same as Yoke).
- `up-extend` is asked for, the file is `ui-extend`: Extend shows `up-building` (house with a plus).
- `up-value` is asked for and does not exist: Value shows `ui-coin`.

**Shop lines (`ShopItemKind`):**

| Line | Icon today | Fix |
| --- | --- | --- |
| Villager | up-villager | own, keep |
| Hand (extra hands) | up-villager (shared) | new: two hands |
| Pace | up-pace | own, keep (simplify) |
| Load (Yoke steps) | up-load | own, keep; Tools needs another |
| Tools | missing name, shows up-load | new: hammer on a bench (`ui-tools` exists, rename) |
| Value (Water, Grain, Flour price) | ui-coin for all three | new: three icons, the good's icon with a coin badge |
| RiverValue (Wool, Cloth price) | good-wool, good-cloth (same as the goods) | new: good with a coin badge |
| Regulars | ui-value (shared with the Market right boon) | new: a customer with a purse |
| Pave | up-pave | own; shared with Wider lanes boon |
| Building offers: Second bucket, Market stall, Second field, Mill | one `up-building` for all four | new: bucket, striped stall, field, mill |
| Bell (ring) / BellTrack (Louder bell) | ui-bell both | new for the track: bell with a plus arrow |
| NextAge (Ring in the next age) | up-building | new: book with a turning page |
| Extend a cottage | up-building (broken name) | use `ui-extend`; shared with the Third room boon |
| Ferry, Raft, Rope | `ui-ferry` for all three | new: ferry, raft, rope |
| Berth | ui-boat-berth | own |
| Crane | ui-crane | own |
| BoatPrices | ui-boat (also the generic stand-in) | new: boat with coin badge |
| Toll | ui-toll | own (redraw, see ranking 29) |
| Winch | ui-bridge (raise-bridge and lower-bridge are UNUSED) | use `ui-raise-bridge` / `ui-lower-bridge` or a winch |
| Locked line ("Unlocks at level N") | none; `ui-lock` UNUSED | show `ui-lock` |
| Finished line | none; `ui-check` UNUSED | show `ui-check` |

**Boons (eight, all borrowed):** Wider lanes (up-pave, shared with Pave), Stronger backs (up-pace, shared with Pace), Deeper well (good-water, shared with the Water good), Longer rows (good-grain), Third room (ui-extend, shared with Extend), Faster hands (ui-tools), Market right (ui-value), Miller's guild (good-flour). All eight need own icons: roads with wide arrows, a strong arm, a deep well, long furrow rows, a three-bed room, quick hands, a market scale, a miller's sack with a seal.

**Achievements: 40, one shared icon (`ui-achievement` rosette, `ui-reputation` as the stand-in).** First age (24): First coin, A full purse, Town treasury, Well trodden, Busy lanes, Water carrier, Harvest home, Daily bread, Hamlet, Village, First gravel, Cobbled, Every road paved, Desire lines, Ring out, Quick step, Handcart, Field and furrow, Closing the loop, Growing town, Chartered, Market day, The miller, Thriving. River age (16): First mooring, Full hold, Ten holds, Fifty holds, Wet feet, Ferryman, Dry shoes, Raise the bridge, Toll keeper, First fleece, Weaver's pride, Skipper's friend, Two berths, The crane, River trade, Borough. Each needs a Paper medal with its own glyph in the centre (coin, purse, chest, footprints, crowd, pail, wheat, loaf, one house, houses, gravel, cobbles, road ring, worn path, bell, boot, cart, plough, circle arrow, tree, charter scroll, stall, mill, rising arrow; moored boat, full boat, 10 and 50 boats, wet foot, ferry, shoe, lifted bridge, toll coin, fleece, cloth, skipper hat, two berths, crane, boat with coin, seal). A shared rosette frame with a different glyph is enough; only the glyph needs drawing.

**Perks (`PerkEarned`):** Good neighbours, Light feet, Mason's eye: no icon.
**Milestones (`Milestone`, Pace, Tools, Value):** no icon.
**Touches (ten, shown as notes):** woodpile, washing line, flower bed, hen coop, bench, beehives, haystack, apple tree, handcart, signpost: no icon (the models exist; a 24 px silhouette each).

**Message kinds (from `message-inventory.md`):** none has an icon. Needed (24 px, one each): level reached (a flag with a number), charter (a scroll with a seal), charter drive (scroll with a bar), boon chosen (a star), next age (book page), seal (wax seal), cottage extended (the `ui-extend`), building open (a door), new to buy (a tag), road worn in (footprints on a road), road paved gravel and cobbles (two), perk, achievement, new villager (person with a plus), request asked / met / missed (the unused `ui-request` plus a tick and a cross), choice offered (two arrows), milestone, touch note, boat arrived, boat filled, boat left (a boat leaving), toll paid, ferry docked, drawbridge raised and lowered (the two unused icons), save note, start note, scroll hint, bell rest, cannot buy (the unused `ui-lock`), hint (the unused `ui-hint`). Tide high and low already have icons; the hint line should use `ui-hint`.

**Unused files that should be used:** ui-check, ui-flow-preview, ui-hint, ui-hire-keeper (the keeper's badge), ui-keys, ui-lock, ui-lower-bridge, ui-raise-bridge, ui-request, ui-wear-overlay, plus the six tool-* icons.

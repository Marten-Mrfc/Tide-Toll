# The Living Map style sheet (version 1, the lead)

Every asset is checked against this page before it goes into the game. The town is a hand-drawn map on a page of a book that has come alive: ink lines, a small warm palette on paper, shapes that read from above, toy-like proportions. The owner's goal for the night: **make the town beautiful to look at.**

What the audit of 26 November showed (`build/n3/assets-audit/before/`): the models share a style, but at the normal view a cottage is a blank yellow rectangle, a villager is a red pawn with no arms or legs, loads are plain boxes, a field looks like a pallet of planks, the mill's sails lie flat like a cross on the ground, and every cottage is the same cottage.

## 1. Three reading distances (every model must work at all three)

| Distance | What must read | How to check |
| --- | --- | --- |
| **The page** (whole town in view) | What kind of thing it is, from its silhouette and its one main colour | Fill it black: can you still name it? |
| **The normal view** (play: about 9 m per 100 px at 1920 x 1080, the camera looking down at about 53 degrees) | Its main parts: roof, walls, door, chimney; a person's head, body, arms, load | `<name>-normal.png` of `tools/asset-audit.sh`, beside a villager |
| **Close up** | Charm: shutters, a flower pot, thatch courses, a patched sail, a hat | `<name>-close.png` |

**The top-down rule:** the camera looks down, so the roof is most of what is seen. A roof is never one blank plane: it has a ridge, courses, a chimney, a dormer, a sag or a patch, and the walls are tall enough (2.6 m) that door and windows show under the eaves from the game's tilt.

## 2. Palette

Only the named colours of `game/scripts/Palette.cs`, each in three tones: **base**, **shade** (mixed 18 percent toward Ink) for faces turned away and undersides, **light** (mixed 12 percent toward Paper) for tops. No gradients, no gloss, no textures on models: colour is per face.

| Name | Value | Use |
| --- | --- | --- |
| Paper | #F2EAD8 | pages, cards, limewashed trim |
| Ink | #2B2620 | lines, never pure black |
| Meadow, Bent grass | #7FA35B, #A9B86E | ground, leaves (light) |
| Trail earth, Packed earth, Mud | #B89568, #8C6A48, #5E4A36 | roads, timber, trunks |
| River, Deep water | #5B7F95, #3F5F75 | water, pails, one tunic |
| Thatch | #C9A55A | thatch, grain, straw, money |
| Brick, Soot brick | #A4553A, #7A4436 | chimneys, awnings, cloth, one tunic, leather |
| Slate roof | #5E6B78 | slate, stone in shade |
| Cast-iron green | #44604F | doors, shutters, one tunic, dark leaves |
| Limewash | #E9E2CF | walls, flour, wool, sails |

New named colours may be added only where nothing fits, at most six tonight, each added to `Palette.cs`, to the ledger note of the asset and to this table. Expected: Stone (a warm grey), Leaf dark (shade under crowns), Timber dark.

**Accents:** every dwelling has one accent colour for its door and shutters (Brick, Slate roof, Cast-iron green, River or Thatch), chosen per plot, so a street varies. Villagers' tunics take five colours (Brick, River, Cast-iron green, Thatch, Soot brick).

## 3. Line

- The ink line is drawn by the game (`InkOutline`: a screen pass on depth, 1.5 to 2 px at play zoom). Models are built for it: give every part that should have a line a real edge (a step of at least 6 cm or a crease of at least 30 degrees).
- Inner lines (planks, thatch courses, timber framing, door boards) are thin bands of the shade tone or of Timber, 4 to 6 cm wide, standing 1 to 2 cm proud so the ink finds them. Three to five courses on a roof, not twenty.
- Nothing is perfectly straight or square: ridges sag 3 to 5 percent, walls lean up to 2 degrees, planks differ in length, posts tilt a little. Use the generator's seed so each model is the same every time it is built.

## 4. Proportions (toy-like: chunky, heads and roofs a little too big)

| Thing | Size | Notes |
| --- | --- | --- |
| Villager | 1.9 m tall; head a third of the height; shoulders 0.7 m | A person, not a pawn: head with hair, hood or hat; body in tunic and legs; two arms that hold the load. 250 to 420 triangles |
| Cottage (plain) | 6 x 5 m, walls 2.6 m, roof pitch 45 to 50 degrees, eaves 0.4 m out | Door 2 m facing its lane, two windows or more, a chimney 0.7 m square rising 1 m above the ridge |
| Cottage extended once / twice | 8.5 x 5 m with a lower side wing / 11 x 5 m L-shaped or long with a second chimney | **Each size has its own silhouette** from above; never a stretched copy |
| Mill | 9 to 10 m to the cap; sails 7 m across, standing upright, turning | The tallest thing in the first age |
| Market stall | 4 x 3 m, awning in Brick and Limewash stripes | Goods on the table readable as shapes |
| Tree | 6 to 9 m; trunk always visible; crown in two or three lumps | Light tone on top, shade tone under |
| Boat | rowboat 5 m, barge 11 m | Cargo visible on deck |
| Smallest part that must read at the normal view | 0.25 m | Posts, rails and beams are at least 0.18 m thick |
| Smallest part at all | 0.08 m | Anything smaller is noise: leave it out |

Triangles: a building 300 to 900, a prop up to 250, a tree up to 300.

## 5. Kinds read by their roofs and shapes

| Kind | Tell |
| --- | --- |
| Dwelling | Thatch roof, chimney, accent door |
| Work building (mill, weaver, crane) | Dark timber, something that moves |
| Trade (market stall, landing) | Striped awning or crates and sacks |
| Civic (bell, charter stone, seal stone) | Stone and one bright mark; stands on a small paved or trodden patch so it is found at once |
| River craft | Limewash sail or awning against the water |

## 6. People and what they carry

A load is recognised by its shape and colour without reading a label: water, two pails on a yoke (River); grain, a tied sack (Thatch); flour, a white sack with a dark band (Limewash); timber, a log on the shoulder (Packed earth); wool, a round bale tied twice (Limewash with Thatch ties); cloth, a rolled bolt (Brick). The Load upgrade shows: step 1 one load in the arms or on the shoulder, step 2 a yoke or two loads, step 3 a pack frame, step 4 a handcart.

## 7. Life

The town moves a little everywhere, cheaply (a vertex shader or a transform per instance; no more than 200 particles on screen; the per-frame cost test must pass): smoke from every chimney, crowns that sway, sails that turn, shimmer on the river, washing that lifts, sheep that graze, a few birds over the map.

## 8. Icons

48 x 48 files shown at 24: an ink line 3 px wide at 48 with round ends, one flat palette colour, the same visual weight for all, no letters. One for every purchase, achievement and kind of message.

## 9. Sound

New sounds belong to the family already there: short (under 0.6 s for the interface, under 1.5 s for the world), soft attacks, the same loudness as their neighbours (see `sounds.md` of the audit), made in code with a ledger row.

## 10. Before an asset is called done

1. Its three pictures (black fill, normal, close) pass section 1.
2. Only palette colours (section 2); every dwelling has its accent.
3. Sizes within section 4, checked beside the villager and the cottage.
4. It differs from its neighbours: a street of five cottages shows at least three different silhouettes or accents.
5. Before and after pictures side by side, read by someone who did not build it.
6. A ledger row; the generator under `assets/src/`.

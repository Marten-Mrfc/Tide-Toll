# Work you can watch: the mock-up board (notes)

The idea: `docs/plans/n5/work-you-can-watch.md`. This is a picture to say yes or no to. Nothing in the game changed.

## Where the pictures are

- `work-now-and-planned.png` (1648 x 2680): the four pairs, one pair a row, each panel 800 x 600, "now" on the left in white and "planned" on the right in wheat, on the dark strip.
- `work-planned-normal.png` (1328 x 1108): the four staged scenes at the asset audit's normal distance (the play zoom, top-down tilt of that zoom, 640 x 480 at their own pixels).
- Single pictures: `<place>-now.png`, `<place>-planned.png` (800 x 600, as on the board), `<place>-planned-normal.png` (640 x 480), for well, field, mill, market.
- How they are made: the four subjects `work-well`, `work-field`, `work-mill`, `work-market` in `game/tests/AssetAudit.Work.cs` (added to the asset audit's subject list). Each has its own framing (`WorkFrame`), and its close picture is taken at 1280 x 960 with the same camera size, so it has twice the pixels at the same framing. Render them with
  `xvfb-run -n 161 -s '-screen 0 1920x1080x24' godot --path game --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 res://tests/asset-audit.tscn --quit-after 1000000 -- --out-dir=DIR --only=work-well,work-field,work-mill,work-market --no-town`
  (about 50 s), then `python3 tools/mockup/work_board.py DIR NOWDIR docs/plans/n5/mockup` (NOWDIR holds `now-<place>.jpg`).

## The pairs

**The well.** Now: the asset audit's town pass, last night (`docs/review/n3/after/assets/town-well-close.jpg`). It is a real frame: one villager stands straight behind the well, half hidden by it, and nothing at the well shows any work. Planned: four villagers wait in a line along the lane, a step apart, all facing the well. One worker leans 30 degrees into the windlass on the crank side. A bucket hangs on its rope just above the shaft's front rim. Two full pails stand on the rim. A ring of darker wet ground lies round the well. One villager walks off along the cross lane with the yoke of pails.

**A field.** Now: the same town pass (`town-field-close.jpg`). One villager stands straight at the field's edge, and the field is one even gold slab. Planned: two workers are bent 32 degrees and crouched a little, each in a row of their own, side by side, each with a sickle raised in the backswing. Behind them is stubble and ahead of them the ripe gold. The back row is growing back green, so cut, growing and ripe show at once. Sheaves stand at the ends of the cut strips. A third villager is bent at the far end of the finished row, lifting a sheaf. Two wait in a line on the lane at the field's edge, and a handcart stands by.

**The mill.** Now: the same town pass (`town-mill-close.jpg`). Villagers stand in a knot against the mill's side, half hidden, and the mill does not answer. Planned: the door is open (a dark doorway with the leaf swung out). A villager carries a grain sack in through it. Grain sacks are set down on the step, with pale flour sacks on the other side. A puff of flour comes out at the door. A hoist beam on the tower carries a rope and a sack. Two villagers, each with a sack, wait in a line on the lane facing the door.

**The market.** Now: the town pass from the night before (`build/ck/n3-final/docs/review/n3/before/assets/town-market-close.jpg`). Last night's copy shows the stall not yet bought. This one is the stall bought, in the same game: empty, with a villager in the lane behind it and no one at it. Planned: goods on the table (crates, pots, a grain sack and a flour sack). The stallholder stands at the table's end, outside the awning so they can be seen. A villager bends over the front of the table, setting a sack down. Another walks off along the lane with empty hands.

No new "now" frames were taken: every "now" is a real frame from the game's own asset-audit town pass, not staged.

## What is faked with a prop (code meshes in test code only, no asset files, no ledger rows)

- Well: the rope and bucket, the two pails on the rim, the wet ring (a flat disc of packed-earth colour).
- Field: the ripe rows and the green regrowth row are boxes laid on `field-stubble.glb`, because the field models are one piece and cannot be cut into rows. Also the sickles (wood handle, pale blade in two pieces) and the sheaves (two gold cones and a band). The handcart is `prop-handcart.glb`.
- Mill: the open doorway and door leaf, the hoist beam and rope, the flour puff (four low-poly pale balls), and the step's sacks. Grain sacks are `villager-cargo-grain.glb` stood on the ground. Flour sacks are the same mesh in pale paper colour, because `villager-cargo-flour.glb` lies on its side when stood up.
- Market: the crates and pots, and the sacks on the table (as at the mill).
- Lanes: flat planes of trail colour. In the town these would be the real roads.
- Plain meshes come out much darker than the palette-textured models under the game's sun. The props' material therefore gives part of its colour back as emission (`Mat`, 0.45; sheaves 0.8, flour and puff 0.75 to 0.95). The real feature would want the props in the models' palette texture instead.

## What does not read well yet

- Distance: the "now" well is much closer than its "planned" picture (a villager about 150 px tall against about 60). The real frame is cropped tight on the well, and a staged picture that close would cut off the line. The field and mill pairs are close in distance; the market pair is roughly so.
- The sickles read as tools only close up. At the normal distance they are a grey stroke. The cut strip and the bent backs carry the field instead.
- At the well the bucket and the pails sit together on the rim and blur into one group of tubs. The rope is a short dark stick, and most of it is under the roof.
- The flour puff is a white lumpy blob. The ink pass draws its facets, so it reads as "something white at the door" more than as dust.
- The mill's hoist is small and sits on the far side of the tower.
- The field's gold rows are boxes with hard edges. They read as rows of crop, but their straight sides look more like planks than standing corn.
- In the "now" market frame nobody is at the stall at all. A frame with a delivery under way would be a fairer "now".
- The normal-distance board shows villagers about 28 px tall, not the 16 px of the idea's note. It uses the asset audit's play zoom (20.4 m for 480 px, the same metres per pixel as the start view at 1080p). At that size the line, the bent workers, the cut strip and the goods on the table still read; the sickles, bucket and puff do not.

## Posing the rigid villager: what the real feature should know

- The figure's front is +Z at yaw 0 (`TownVillagers.Figure`). To face a point, use `yaw = atan2(dx, dz)`.
- Rotation order: a node rotation of (lean, yaw, 0) in Godot's default YXZ order leans the body about its own feet toward its own front, then turns it. A positive X rotation moves the head toward +Z, which is forward. The loads are children with the same transform, so they lean with the body.
- 25 to 35 degrees of lean reads clearly as "bent" close up and still reads at the normal view, because the head moves off the feet by about half a body width. Under about 20 degrees it looks like standing. Above about 35 degrees the figure looks like it is falling, since the legs do not bend.
- A crouch of 0.9 in height on top of the lean helps the stoop. Less than about 0.85 squashes the head as well and looks wrong.
- A hand-held tool must be a child of the figure (a hand point at about (-0.7, 1.3, 0.6) times the walker scale is the right hand), or it will not follow the lean. Where it points decides whether it can be seen. Pointing down or forward hides it in the crop or behind the body. A raised backswing shows against the ground.
- Lines read as lines only with a clear step (2 m, about two-thirds of a body height) and everyone facing the same point. With less space they become a knot again.
- Heads: the hood is pale, so from behind a hooded villager can look like a face. That is fine in a line, but a worker who should face the camera needs a hat or bare head to read as facing.
- Anything under a roof (the stall, the well's shaft) is hidden at the game's tilt (56 degrees close in). A person under the awning disappears more than about 1 m behind its front edge. A stallholder must stand at the end of the table, or the awning must be drawn lower or see-through.

# Round 2 (27 November, after the owner's "the field is a bit ugly ... make it fit more")

What was asked for (lead-look.md and audit.md) and what was done. The board, the second board and the single pictures in this folder are all new.

## How the pictures are made now
- **Planned:** every prop in a staged scene is now a model made the way the game's models are made. Each is a generator under `assets/src/models/` using `common.glb` and `_kit`: one palette colour per face, real steps and creases for the ink line, jittered so nothing is ruler-straight, seed 5. Each has a ledger row (`python3 tools/check-ledger.py` passes, 491 rows). The models load through `ModelLib.Mesh`, so they get the game's own palette material, light and soft ink. The round-1 glow trick is gone.
- **Exceptions in code:** only the flour puffs and the dust on the mill's step are made in code. They are translucent, so the ink pass draws no line round them, as the lead asked.
- **Lanes:** these are now the game's own worn trail. For the picture the staged scene paints the ground's wear texture (`TownGround`'s wear image) along each lane, and gives the ground back afterwards. The lanes curve and fork; none meet at a right angle.
- **Now:** new real frames, taken by the asset audit with `--now-work` (`game/tests/AssetAudit.NowWork.cs`). The town bot plays the first age to level 8, then plays on until each workplace is open and busy. Each frame is taken with exactly the staged scene's camera: yaw 35 degrees, the same camera size, 1280 x 960. So each pair is now seen from the same distance and angle.
  - Well (level 8): one villager works at the well; another walks off with the yoke, upright.
  - Field (level 8): two villagers stand straight at the edge of the plank-like ripe field.
  - Mill (later in the first age): a knot of villagers stands round the mill's door side, half hidden. The orange ring round the mill is the game's own, drawn in that real frame.
  - Market: the bot did not build the market within the run, so its "now" is the lead's frame, `docs/review/n3/after/assets/market-stall-close.jpg` (the stall as it is today, no villager at it).
- **Commands** (about 50 s for the staged scenes; about 6 min for the "now" pass, with the bot):
  - Staged scenes: `xvfb-run -n 16x ... godot ... res://tests/asset-audit.tscn -- --out-dir=DIR --only=work-well,work-field,work-mill,work-market --no-town`
  - Now frames: `... -- --out-dir=DIR --only=none --now-work`
  - Board: `python3 tools/mockup/work_board.py DIR NOWDIR docs/plans/n5/mockup`
- **Second board:** `work-planned-normal.png` now shows each pair at the normal play distance: the real frame on the left, the staged one on the right. For the market the "now" is `market-stall-normal.jpg` from the review.

## The new models (`assets/src/models/work-scenes.py`; `python3 assets/src/models/work-scenes.py [name ...]`)

**Field.** Built from row pieces, so the real feature can show any row in any state. A piece is 2 m of one row. X runs along the row, across it is the field's own 1.6 m pitch, and the origin is on the row's centre line. A row is drawn as pieces side by side. The reaper's progress is the boundary between ripe and stubble pieces: an `edge` piece where the reaper stands and a `swath` piece behind them. That boundary moves a piece at a time. Growing back goes stubble, then shoots, then green, then ripe.

| Model | What it is |
| --- | --- |
| `work-field-bed` | The field with no crop: field.py's 16 m footprint and grass headland, a ragged inner edge, furrow earth (Mud). In the scene it is drawn 0.62 deep, to the proportions of a first-age field plot in the real frame. |
| `work-row-ripe` | A ridge of earth; a crop body with sloping sides (so no slab or plank); four staggered lines of upright tufts with toothed tops and ears, about 0.85 m. Reed and thatch, every third tuft Trail earth for the darker tone. 451 triangles. |
| `work-row-green` | Regrowth, later: the same build, lower (0.45 m), Meadow with Bent grass tips and Cast-iron green for the dark tone. |
| `work-row-shoots` | Regrowth, early: small Bent grass and Meadow shoots in two dotted lines (0.2 m). |
| `work-row-stubble` | A low pale ridge with short stumps in dotted lines and a few fallen stalks (0.15 m). |
| `work-row-swath` | Cut stalks lying across the row, ears one way. |
| `work-row-edge` | Where the reaper stands: one half still standing with a ragged face, the other half cut and fallen. |
| `work-sheaf`, `work-stook` | A sheaf waisted at a band at 40 percent, cut ends below and splayed ears above (0.62 m). A stook is three sheaves leaning together, tied at the top. In the scene they are placed at 1.5 times, so they read beside the 3 m villager. |
| `work-sickle` | Drawn larger than life: a crescent blade (Slate roof) 0.25 m at its broadest, a handle (Packed earth). Origin at the grip. |
| `work-scarecrow`, `work-jug` | The two small touches taken for the field, with two of the game's own birds (`animal-bird`) on the shoots. |

**Well.**

| Model | What it is |
| --- | --- |
| `work-wet-patch` | Overlapping uneven blobs, 1 cm high, sunk so they nearly meet the ground: trodden earth (Packed earth) outside and wet ground (Wet mudflat) inside, so the edge is soft; two puddles (River); small footprints going toward the lane. |
| `work-bucket` | Staves, two iron bands, a bail and a rope (separate node "Rope"). |
| `work-pail` | Unlike the bucket: wider and lower, Trail earth, a rope handle, water near the rim. |

**Mill.**

| Model | What it is |
| --- | --- |
| `work-sack-grain`, `work-sack-flour` | Slumped sacks with a tied, gathered neck. Grain is Reed and thatch; flour is Limewash with a dark band. |
| `work-door-leaf` | Four planks of different lengths, two ledges and a brace; origin at the hinge. |
| `work-doorway` | Frame, lintel, a dark inside (Mud), a stone sill and a sack just inside. |
| `work-hoist` | A beam and strut from the wall, a pulley block, the rope and a grain sack. |

**Market.**

| Model | What it is |
| --- | --- |
| `work-scales` | A balance (the thing that says "selling"). |
| `work-crate` | A slatted crate of turnips. |
| `work-pot` | A clay pot with a lid. |
| `work-apron` | An apron in the villager body's frame (Limewash, Brick ties), worn by the stallholder. |

Triangles: a ripe piece is about 450. A whole field of ripe pieces (six rows of seven) would be about 19,000, which is heavy for the game. Before it goes in, the pieces should be multimeshed, or the tufts halved for the normal view.

## Scene by scene
- **Field:**
  - The rows are now ridges of crop on dark furrow earth: stubble lowest, then shoots, then green, ripe tallest. Two reapers stand bent at the ragged edge with sickles, a swath lies behind them, and stooks of three stand on the cut rows. One villager lifts a sheaf at the end of the finished row.
  - Small touches: two wait in a line on the trail; a handcart; a scarecrow at the far corner; a water jug at the row's end; two crows.
  - The footprint is the real plot's proportions and about its size in the real frame.
- **Well:**
  - The wet patch is uneven with puddles and footprints.
  - The crank and the worker are on the side the camera sees; the bucket hangs on its rope just above the near rim (staves and iron bands) and differs from the two pails.
  - The line of four follows the curving trail; one walks off on the fork with the yoke.
  - Small touch: the game's cat.
- **Mill:**
  - A plank door stands open on a dark doorway with a stone sill.
  - Soft pale puffs at the door, with no ink line, and a dusting on the step.
  - Slumped grain sacks stacked on the left, banded flour sacks on the right.
  - The hoist with its sack is on the camera's side; the carrier goes in at the door; two wait with sacks.
- **Market:**
  - Crates of turnips, pots, scales, and grain and flour sacks.
  - The stallholder stands at the table's end, wearing the apron; a villager sets a sack down; another walks off with empty hands on the trail.

## What still does not look right
- **Well, wet patch:** it still gets a thin ink line round its outer edge, and the footprints are small grey dashes. A decal in the ground shader would be the right tool in the game.
- **Well, bucket and rope:** they now show above the near rim, but the bucket is still one tub among the pails at the normal view.
- **Field, sickles:** small at the normal view. The bent backs and the cut edge carry the scene.
- **Field, swath:** the stalks read as zigzags close up.
- **Field, rim:** the grass rim is still the old hard-edged headland. Round 2 tried rim tufts, but at this view they looked like tick marks, so they were dropped.
- **Mill:** the puffs sit partly in front of the doorway, so the dark inside is half covered. The sacks are still rounder than real sacks.
- **Market:**
  - The stall model's own goods (green and white boxes) mix with the new goods, so "goods by kind" is only partly told.
  - The apron is small at this distance.
  - There is no real "now" frame of a delivery: the bot did not build the market in the run.
- **Scale:** the audit's sizes (a sheaf 0.35 m, a sickle 0.35 m) assume a 1.9 m villager. The game draws its villager about 3.1 m tall (walker scale 1.1 on a 2.8 m model). The new models are sized against the game's villager, so their sizes in metres are about 1.7 times the audit's figures.
- **Not done:** contact sheets of the new models (`tools/render-contact-sheet.sh`) were not made for lack of time. The models were checked only in the staged pictures and in a quick scratch preview. `./check` was not run (the machine was reserved for timing tests until 09:51).

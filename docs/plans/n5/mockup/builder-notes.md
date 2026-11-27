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

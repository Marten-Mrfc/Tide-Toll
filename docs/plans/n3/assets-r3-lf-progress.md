# Asset round 3, track LF (life in the town): progress

Checkout `/home/user/tide-toll/build/wt/n3-art2`. Pictures: `/home/user/tide-toll/build/n3/assets-audit/r3-lf`.

## Done (code written, pictures pending unless said)

- **Free ground (item 5 base):** `game/scripts/town/TownSpace.cs` (new): cells taken by either age's town, from the plan
  only: water, plots +1 cell, roads (cells and drawn curve) +1 (queue spots stand on roads), river plots +1, the landing
  +5, every river link's cells and curve, 6 cells round each crossing's ends, 2.5 cells round every river point; and the
  "yard" ring (3 cells) round every cottage kept for its garden.
- **Trees (item 5):** `TownTrees.cs` keeps a tree only where its 3 x 3 cells are free and outside the yards.
- **Animals and birds (items 1, 2):** `game/scripts/town/TownLife.cs` (new node "Life" under MapOrigin, one line in
  Main.cs after Touches): 3 hens pecking by the coop garden, a cat asleep on a cottage step, a dog by the market looking
  round, 3 ducks drifting along a quiet stretch of river and bobbing, a heron in the shallows dipping its neck, 5 birds
  gliding in circles 15 m up over the town, a flock of 9 resting on the first field that lifts and wheels every 40 s.
  Models: `assets/src/models/animals.py` -> `animal-hen, -duck, -heron, -cat, -dog, -bird`.
- **Gardens (item 3):** `game/scripts/town/TownLife.Gardens.cs`: each cottage (both ages) gets one garden on its back side
  (then sides, then front), two cells out, on free ground, never on a touch spot or another garden: vegetable bed,
  washing line (cloths lift in the wind, shader), woodpile, bench, beehives, flower bed; the second cottage gets the hen
  coop. Pops in with its cottage. Models: `assets/src/models/gardens.py` -> `garden-washing-line`, `garden-vegetables`.
- **Meadow (item 4):** `assets/src/2d/ground.py`: the Cast-iron green shade blotches (the "holes") dropped; Bent grass
  tufts and Brick, Limewash, Reed and thatch flower specks added. `game/shaders/ground.gdshader`: a new trail is two thin
  ruts with grass between (band on the core wear), wearing in as before.
- Audit: subjects life-street, life-street-charter, life-hens, life-cat, life-dog, life-pasture, life-river, life-ducks
  (`AssetAudit.Town.cs`); the Life node is hidden for model pictures (`AssetAudit.cs`).

- **Item 6:** `assets/src/models/dressing.py` -> `dress-bunting` (5 m, eight flags fluttering by the same cloth shader,
  set beside the market as its "garden"), `dress-rocks` (four outcrops on quiet bank rows), `dress-gull` (on the first
  outcrop, not on a mooring post: the landing's posts are RiverLayer's).
- 01:41 first pictures read (r3-lf): gardens beside cottages, hens by the coop, cat on a step, dog, ducks and heron on
  the river, sheep in the pasture; the willow in front of the landing is gone; the crane's jib lies over the landing,
  not a roof. Animals were small at the normal view: scale raised (hens, ducks 2.2x, cat 2x, dog 1.8x, heron 1.6x);
  cat reworked grey with a clear head, dog in two lumps with paws.

- 02:06 second pictures read: meadow without the dark holes; new trail = two faint ruts (road-new-trail); bunting by
  the market stall; rocks with a gull; ducks larger; hens, cat, dog read. Faults found and fixed: a willow grew through
  a rock outcrop (trees now skip 2 cells round each outcrop: `TownLife.RockSpot` + `TownSpace.KeepClear`, asked by
  `TownTrees` before planting); the rocks' flat mud slab read as a pallet (dropped from dress-rocks).
- Tests while working (01:42, loaded): game-smoke, game-town, game-perf (1x mean 0.524 ms, 494 B/frame; 8x 0.700 ms,
  738 B; river 1x 0.607 ms, 1211 B/frame), game-save, game-river: PASS. Ledger merged (342 rows, 0 problems).

- 02:16 rocks retaken (life-rocks, town-view-river, life-river): the willow now stands clear of the outcrop, the gull
  sits on top; no slab.

- 02:25 lead's note of 01:58: `field.py`: the ploughed slab's sides are Bent grass (top still earth), so the
  sprouting field lies in a mown headland (field-sprouting/-ripe pictures retaken; the ripe field still shows a thin flat
  earth ring round the grain). Heron drawn 2.1x (2.3 m) in source: NOT built or pictured (the check's screenshot step
  was running with the built game).
- ./check (01:56 onward, load 30 to 45): every step PASS up to game-save, except game-motion's CPU gate only
  (120 fps, enter-on-lettering frame 77 12.32 ms, gate 6). The screenshot step was still running at 02:26 (2400 s
  limit); ledger checked alone: 342 rows, 0 problems.

## Left

- Screenshot step verdict; heron picture at 2.1x; the flock's lift was never caught mid-air in a picture; sheep are
  RiverLayer's and unchanged.

## How to resume

`tools/asset-audit.sh --only=<names> --out=/home/user/tide-toll/build/n3/assets-audit/r3-lf --skip=flat,sounds`

## After the review (fixer, from 02:27 UTC)

Changes (pictures: `/home/user/tide-toll/build/n3/assets-audit/r3-lf-fix`):

- Vegetable bed (`gardens.py`): light Trail earth soil, 8 cm low Packed earth edging, four rows of Bent grass and
  Meadow sprouts; no dark cabbages, no scarecrow, 146 tris.
- Rocks: trees kept 4 cells (was 2) round each outcrop (`TownLife.RockSpot`).
- Ducks (`animals.py`, `TownLife.cs`): Limewash breast, Packed earth back, green head, a pale ring ripple; 3x; a loose
  pair (two), not a line of three.
- Birds: Limewash body and wings with Ink tips, 3.6x (was 2x); gliders and the flock cast no shadow (the dark ground
  specks). New audit subject `life-flock`: the flock held 4.5 s into its lift (`TownLife.HoldFlock`, audit only).
- Heron: Limewash body and neck, Slate wings, Ink wing tips and crest, thicker legs; 2.1x (about 2.5 m drawn).
- Dog: lies at the market plot's back edge, beside the stall (not 2 cells out in the meadow); 2.4x; Limewash chest.
- Cat: ginger (Reed and thatch) with Brick ears, stripe and a curled tail tip; 2.6x.
- Gull: 2.5x, Slate wings with an Ink edge.
- Gardens 3 cells off every road (cells and curve; queue spots stand on roads): `TownSpace.NearRoad`.
- Bunting: poles 2.0 m (was 2.6), Packed earth (was Mud); the flag shader's line moved with it.
- Meadow (`ground.py`): pale blotches about a third less area (threshold 0.66 -> 0.70); flower specks twice the size.
- Ripe field (`field.py`): the grain fills the ploughed patch's own outline, so no earth ring.
- Second pass after the pictures: the bed's sprouts drew a grid of ink dots, so the bed is now a 5 cm Trail earth slab
  with Bent grass sides and four flat rows (2 cm, Meadow and Cast-iron green), soft ink, 2.6 m long (50 tris). The
  road rule took the hen coop away (no hens): the coop and the flower bed (the fallback kind when a woodpile or bench
  finds no room) are exempt from it, and the coop goes to the second cottage that has room. Heron moved 1 cell into
  the water (was on the bank's edge). Duck ripple: a broken ring of two arcs (16 segments), not a hard hoop. Birds 3.2x.
- Pictures read (02:35 to 02:54, `r3-lf-fix`, `-fix2`, `-fix3`): bed reads as a light earth bed with green rows;
  hens round their coop; ginger cat on its step; dog beside the stall (about 3 m off, on the stall's back side, not in
  the open meadow); bunting short and tied over the stall; ducks a clear pair with pale wakes; heron white with dark
  wings in the shallows; rocks clear of trees with the gull on top; gliders and the flock white birds with dark tips,
  no ground specks; the flock caught mid-lift (`life-flock`); ripe field without the earth ring.
- Not done: the woodpile right beside the road at the west cottage in town-view-level-8 is a simulation touch
  (`TownTouches`, at the simulation's touch spot), not a garden: not moved. Washing line in life-cat: no washing line
  is in the new life-cat picture (the gardens moved with the road rule), so not checked.
- 02:55 whole ./check started (log `build/n3/lf-fix-check.log`).

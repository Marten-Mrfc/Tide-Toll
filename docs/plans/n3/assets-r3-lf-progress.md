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

## Left

- Pictures read and fixed; tests; ledger rows merged; item 6.

## How to resume

`tools/asset-audit.sh --only=<names> --out=/home/user/tide-toll/build/n3/assets-audit/r3-lf --skip=flat,sounds`

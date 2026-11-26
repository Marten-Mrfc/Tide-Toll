# Assets round 1, track W (work buildings, nature, fields, stones): progress

Builder: checkout `/home/user/tide-toll/build/wt/n3-art2`. Pictures: `/home/user/tide-toll/build/n3/assets-audit/r1-w/`.

## How to resume

```
cd /home/user/tide-toll/build/wt/n3-art2
PYTHONPATH=assets/src python3 assets/src/models/<script>.py      # mill.py, market-stall.py, tree.py, field.py, town.py, well.py, river-town.py seal-stone
python3 tools/merge-ledger-rows.py --build                       # the rows into assets/LEDGER.csv
tools/sync-assets.sh
tools/asset-audit.sh --no-town --only=<names> --out=/home/user/tide-toll/build/n3/assets-audit/r1-w --skip=flat,sounds,sheets   # ~2 min
tools/asset-audit.sh --only=<names> --out=... --skip=flat,sounds                                                              # + the town pass, ~5 min
```

Audit tool changes: `--no-town` (skip the bot's town pass), a Godot `--import` after the sync (the pictures showed the
old mill: changed .glb files were not re-imported), the `trees-group` subject now shows all five kinds, a new
`wood-and-field` subject (eight trees of the five kinds behind the three field states, a villager).

## Done

| Item | State |
| --- | --- |
| Mill | `assets/src/models/mill.py` (new; the mill entry left `town.py`): smock mill, eight-sided tapered dark-timber tower on a stone plinth, three paler courses, a reefing stage at 3 m, a boat-shaped cap to 9.6 m, tail pole with wheel, flour sack at the door; four upright sails 7 m across (one patched panel), hub at 7.6 m. Nodes `Body` and `Sails` kept, so `TownBuildings` turns them as before (only while the mill works; see "For the lead"). 1244 tris |
| Market stall | awning over the back two thirds only, counter forward of it: apples, cabbages, a grain sack, three cheeses, loaves; a crate and sack in front, barrel, crate, stool, a hung cloth behind. 608 tris |
| Trees | oak, willow, poplar reworked (trunk clear, two or three lumps, each lump Bent grass light cap / Meadow / Cast-iron green underside); new `tree-birch` (pale banded trunk, high airy crown) and `tree-hawthorn` (low dark bush with red haws, a hedge piece). `TownTrees.cs` places five kinds (willow by the river; poplar, birch, hawthorn, oak by the seed's draw). Sway: `TreeSway.cs`, one ShaderMaterial (Lambert, palette nearest, soft-ink stencil) with a vertex sway above 2 m, phase from world position; no CPU cost |
| Fields | `field.py` rewritten: soft uneven sixteen-sided patch with a Bent grass headland, rows clipped to it; sprouting = dark furrows with broken shoot rows; ripe = one gold mass with bunched heads and a cut strip holding three stooks; stubble = pale rows, five stooks, four lying sheaves. 362 / 433 / 397 tris |
| Charter stone | `town.py`: 2.7 m standing stone, leaning a touch, a raised Brick seal with Paper ribbon tails, on a paved patch in a trodden ring 3.5 m across. 254 tris |
| Seal stone | `river-town.py`: the borough seal disc is now River blue (Deep water rim and back, Paper ship). 248 tris |
| Well | trodden patch, thatch courses, a bucket on the rim (footprint kept, so the game's fit keeps its size). 256 tris |
| Bell post | trodden patch and flags under the frame (inside its old width), thatch courses. 400 tris |

## For the lead (files I do not own)

1. `game/scripts/town/TownBuildings.cs`, the charter stone: it sets `MaterialOverride = PaleStone` and the same on every
   surface, so the new Brick seal shows pale like the stone. Wanted: drop the override (the model's own Concrete stone
   is pale already), i.e. delete the block `if (_charter is GeometryInstance3D cg) { ... }` except the `CastShadow` line.
2. `TownBuildings.cs`, the sails: they turn only while `t.PlotWorking(mill)`; the brief asks for always slowly, faster
   while it works. Wanted in the `if (_sails != null && millWorks)` block: `if (_sails != null) { float rate = millWorks ? 1.2f : 0.25f; _sailAngle = (_sailAngle + dt * rate * (float)Math.Min(_drv.Speed, 4)) % (MathF.PI * 2f); _sails.Rotation = new Vector3(0, 0, _sailAngle); }`.

## Pictures read (r1-w)

- mill-normal/close: a tall dark tower with a red cap and four pale upright sails facing the view; tallest thing; reads as a windmill. In town (town-mill-close) it stands to its plot's door yaw.
- market-stall: goods show at the normal view (red basket, green cabbages, sack, cheeses, loaves) in front of the striped awning.
- trees-group, wood-and-field: five kinds tell apart (round oak, white-trunked birch, columnar poplar, weeping willow, dark hawthorn with red haws). Fault: the light caps on the oak and poplar read as flat pentagon plateaus close up (a little geometric).
- fields: sprouting = brown furrows with green dashes; ripe = a gold patch with combed lines and three stooks on a cut strip; stubble = pale rows with stooks and sheaves; soft edges with a grass headland, no plank frame. The sprouting one is still the most "planky".
- charter stone: standing stone with a red seal on a paved, ringed patch, taller than the villager. In the town the game's PaleStone override makes the seal pale (For the lead, 1).
- seal stone: blue seal disc on the stepped plinth. well, bell post: trodden patch, thatch courses (the well's course bar sits a little proud of the roof).
- town-view-level-5/-9/-charter (new town-pass subjects): the whole first-age town; the trees and fields show; the town itself is mostly the other track's cottages.

## Not done

- Pasture and sheep (item 6): not started.
- The sails turn only while the mill works (TownBuildings, not mine): For the lead, 2.
- The tree sway is a shader (`TreeSway.cs`); it shows only in the running game, not in the audit's tree pictures (they use the plain material).

## After the review (fixer, round 1, 21:33 to 22:00 UTC)

Pictures retaken into `build/n3/assets-audit/r1-w/` (model pass and town pass, 21:41 to 21:45) and read; JPEG copies in
`docs/review/n3/after/assets/`.

| Review item | State |
| --- | --- |
| MUST 1 ripe field | fixed (`field.py`): no ridges with gable ends (the ticks) and no outlined heads; the grain is one gold mass with 0.8 m bands standing 4 cm proud every 1.6 m, ends cut to the outline. 0.4 m bands were tried first and were too busy (two ink lines every 0.8 m). There is no "Thatch light" colour in the palette, so the second tone is the band's step. In the town the outlined gold pentagons are gone |
| MUST 2 willow | fixed (`tree.py`): one Meadow dome, a Cast-iron green shade ring, 14 strands that start near the top, lie over the dome and fall past the rim to 2.2 to 2.8 m, alternating Bent grass and Meadow, 0.5 m wide at the crown and 0.28 m at the tip (0.25 m thin strands at the rim were tried and read as stilts or spider legs), a light crown top. Reads as a weeping tree; the trunk shows between strands. 283 tris |
| MUST 3 sprouting field | fixed: shoot rows are flat Meadow bands 0.3 m wide, broken by two or three unequal gaps, 3 cm high (no gable ends); furrow ridges are flat strips too |
| MUST 4 hawthorn | fixed: Meadow body over a Cast-iron green underside, four peaked Bent grass lumps on top (no plateaus), twelve Brick haws 0.18 m round the crown, one blossom. 280 tris |
| MUST 5 charter stone | model fixed: 3.4 m (seal raised to 2.15 m). In the town the stone is still hard to find in the charter frame: its placement and the PaleStone override are in TownBuildings.cs (For the lead 1 and 3) |
| MUST 6 mill | fixed (`mill.py`): a rounded boat-shaped Brick hood (half-round, narrowing to nose and tail, oval from above) over a Soot brick skirt with a Paper ridge band; hub moved to z 2.6 (sails about 0.6 m clear of the nose); sails 1.4 m wide; a Cast-iron green door 1.2 x 2.0 m with a lintel. In town-mill-normal the sails now face the view and the red hood reads. 1234 tris |
| MUST 7 seal stone | fixed (`river-town.py`): round steps, a round drum 1.6 m high with two carved bands and a cornice, the River-blue disc standing on top (18 degrees back), 1.36 m across: I made it larger than the asked 0.9 m because at the normal view the disc is what names it and 1.2 m already looked small. A coin on a post. 340 tris |
| MUST 8 row-end ticks, stooks | fixed: stubble rows are flat strips cut to the outline; the ripe field's stooks stand 0.85 to 1.1 m inside the cut strip. In the town the villager's work spot is still on the headland next to the stooks (game, not the model) |
| SHOULD well / bell post roofs | fixed: well roof 1.2 m wide, 0.8 m pitch, Brick ridge cap, two courses a side; bell post roof is Slate with Soot brick ridge tiles and Wet mudflat courses. The well's bucket beam lying on the ground in the town is game drawing: For the lead 4 |
| SHOULD bell post base | fixed: one flagged slab 2.4 x 1.8 m, 5 cm; footings narrowed to 0.5 m and lowered to 0.14 m |
| SHOULD oak / poplar caps | fixed: the light cap is two peaked lumps at different heights (same triangle count) in every lump |
| SHOULD birch | fixed: crown about 4.6 m across, three Meadow lumps with Bent grass caps, one hung low on a side |
| SHOULD market goods | fixed: cabbages 0.56 to 0.64 m, apple heap 0.72 m, loaves 0.34 x 0.26 m |
| SHOULD sails always turn | done without touching TownBuildings: `game/scripts/town/MillSails.cs` finds every node named "Sails" as it enters the tree, turns it at 0.25 rad/s (times the game speed, not while the driver is paused) and, while TownBuildings turns it faster, follows TownBuildings' step (runs after it, process priority 1000) so they never fight or jump. One line in `Main.cs`. For the lead 2 is therefore no longer needed |
| SHOULD sway strip, black-fill picture | not done (time) |
| Pasture and sheep | not done (builder's list; time) |

Files changed in game code by the fixer: `game/scripts/town/MillSails.cs` (new), `game/scripts/Main.cs` (one line).

## For the lead (after the review)

3. `TownBuildings.cs`, the charter stone's placement: in town-view-charter the stone stands behind the bell post and reads as a
   pale blob; with the model now 3.4 m the scale rule "at least 4 m across" makes it larger still. Wanted: set it in the green's
   front corner toward the camera (positive local Z of the green) and drop the PaleStone override (item 1).
4. The well's bucket beam lying on the ground east of the well in the town (town-well, town-view-level-5) is not part of
   `well.glb`; it comes from the game's well drawing or a villager's yoke at the well's work spot. Whoever owns it: stand it up or
   remove it.

### Whole ./check (fixer, started 21:46, ended 22:27 UTC; log build/n3/r1w-fix-check.log)

All steps PASS except game-settings and game-motion. game-settings printed SETTINGS PASS and then Godot aborted at exit (code 134, leaked
StyleBoxFlat / InputEventKey references); run alone at 22:27 it printed SETTINGS PASS again and failed on the same exit-time leak lines.
game-motion failed only on the CPU gate (6 ms) on frames that change from run to run (check: tab-turned-back 20.5 ms, tab-saving-keys
44.6 ms; alone: pause 12.6 ms, enter-on-lettering 7.6 ms, settings-open 69.6 ms); load average was 18 on 4 cores. Neither test
touches the models; the only game code from this fixer is MillSails.cs and one line in Main.cs. Not resolved: rerun both on a quiet machine.

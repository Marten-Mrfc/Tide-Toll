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

## Not done

See the final section of this file (updated at the end).

# Asset audit: progress

Builder of the asset audit, checkout `build/wt/assets`. Nothing in how the game looks is changed: only tools and a test scene.

## How to run

From the checkout (`/home/user/tide-toll/build/wt/assets`):

```
tools/asset-audit.sh                                  # everything into /home/user/tide-toll/build/n3/assets-audit/before
tools/asset-audit.sh --out=/home/user/tide-toll/build/n3/assets-audit/after-1
tools/asset-audit.sh --only=cottage-01,well --no-build   # retake some model / town pictures (index and sheets rebuilt)
tools/asset-audit.sh --skip=models                    # only textures, icons, sounds, index and sheets (no Godot window)
python3 tools/asset-audit/index.py DIR --jpeg=/home/user/tide-toll/docs/review/n3/before/assets   # JPEG copies
```

Files:

| File | What |
| --- | --- |
| `tools/asset-audit.sh` | the command (builds C#, syncs assets, runs the picture steps, one retry if Godot dies without a verdict) |
| `game/tests/asset-audit.tscn`, `AssetAudit.cs` | in the running game (ground, sun, ink, interface hidden, map trees hidden): every `assets/models/*.glb`, villagers with each cargo and Load step, the cottage at each extension width, a tree group |
| `game/tests/AssetAudit.Town.cs` | the same town played by the payback bot to the seal; roads by surface, plots in place, river at high and low water, landing, each crossing (drawbridge down and lifted), the book |
| `tools/asset-audit/flat.py` | textures (real size + enlarged) and icons (Godot's SVG renderer at 24, 48, 192 px, and on Ink) |
| `tools/asset-audit/sounds.py` | waveform + spectrogram per sound, `sounds.md` (length, peak, LUFS, cue table and code line) |
| `tools/asset-audit/index.py` | `assets.json` and `sheet-<kind>-<n>.png` (12 a sheet), `--jpeg=DIR` copies |

Pictures: `<name>-normal.png` (640 x 480, play zoom: the start follow view at 1920 x 1080 is 46 m high, kept at the same metres per pixel, so 20.4 m at 480 px; yaw 0, the game's tilt) and `<name>-close.png` (yaw 35 degrees, fills the picture). Names are stable: model names are the file names; code-built ones are fixed strings in the two .cs files.

## How the models are generated

Every model is a Python script under `assets/src/models/` (shared helpers in `assets/src/common/`, `_kit.py`). Rebuild one from the repository root:

```
PYTHONPATH=assets/src python3 assets/src/models/<script>.py        # some take names: town-props.py prop-bench, river-town.py sheep
tools/build-assets.sh models                                      # all model generators, then the ledger check
tools/render-contact-sheet.sh assets/models/<name>.glb --sides --save   # the guide's contact sheet
tools/sync-assets.sh                                              # copy into game/assets before the game sees it
```

| Script | Writes |
| --- | --- |
| `cottage.py` | cottage-01, -02, -03 (the game shows cottage-0{1+extensions}, widened 1.5x / 1.8x in `TownBuildings.cs`) |
| `villager.py` | villager, villager-cargo-grain, -wool, -timber, -cloth |
| `town.py` | mill, bell-post, plot-stakes, charter-stone, villager-cargo-water, villager-cargo-flour |
| `river-town.py` | pasture, sheep, weaver-cottage, crane, seal-stone, tide-post |
| `town-props.py` | prop-* (ten) |
| `tree.py` | tree-oak, tree-willow, tree-poplar |
| `field.py` | field-sprouting, field-ripe, field-stubble |
| `fence.py` | fence-wattle, fence-post, fence-gate |
| `ferry.py` | ferry-raft, ferry-post, ferry-rope |
| `drawbridge.py` | drawbridge-lift, drawbridge-span, drawbridge-end |
| `footbridge.py` | footbridge-span, footbridge-end |
| `barge.py`, `rowboat.py`, `ford-stones.py`, `keeper-hut.py`, `landing.py`, `market-stall.py`, `reeds.py`, `well.py` | the file of the same name |

## State

- 20:06 UTC: tools written; models pass (64 subjects) and flat and sound passes ran and were read; full run with the town pass under way.

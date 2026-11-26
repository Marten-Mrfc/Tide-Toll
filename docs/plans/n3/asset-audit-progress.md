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

- 20:06 UTC: tools written; models pass (64 subjects) and flat and sound passes ran and were read.
- 20:20 UTC: town pass added (bot stops at levels 5, 9, the charter, then the seal). Found and fixed: Main's follow view took the camera back whenever UserMoved was reset (all town and book pictures showed the whole map); the close framing cut fields (now uses the turned box); the scale villager hid small things in close-ups (now only in -normal).
- Findings from the index so far: 15 sounds not named by any cue table or game script (place-01..06, erase-01..06, boat-turn-away-01..03); models over the 500-triangle guide: drawbridge-lift 1188 (landmark limit 1,500), mill 638, barge 594; files not named in game/scripts: fence-gate, field-sprouting, reeds, villager-cargo-timber and 22 icons (tool-*, several ui-*).

- 20:40 UTC: final run done (about 8 minutes: 66 model subjects, then the town played to the seal with stops at levels 3, 5, 9 and the charter; `ASSET AUDIT PASS`). 95 subjects x 2 pictures, 9 textures, 53 icons, 151 sounds; 31 sheets read; 190 JPEG copies in `docs/review/n3/before/assets/` (8.8 MB). The command: `tools/asset-audit.sh --jpeg=/home/user/tide-toll/docs/review/n3/before/assets`.
- Seen on the sheets (faults of the game, left as they are): an Okabe orange ring round the mill and a new cottage in play (a state marker drawn on the models); world labels ("Grain 0 of 1998 loads", "Leaving, Cloth 256 of 398 loads", "The market, to buy") in the river and plot pictures; the table and the leather are one flat colour close up; the second bundle of Load step 2/3 sits on the back and cannot be seen from the play view; villager-cargo-timber has no use in code (no timber good).

## Left to do

- `road-worn` (a worn-in track): no road was worn in and still unpaved at any stop of the bot's run (the bot paves early); needs a stop before paving or a hand-played road.
- Boats loaded and empty as separate subjects; touches and plot stakes where the town puts them (only the market's stakes, `town-market-early`); the charter and seal stones in place.

## Not pictured (and why)

- "+ hand" markers: an interface label (`TownHud.Choice.cs:252`), not a 3D thing; belongs to the interface audit.
- Icons drawn in code: the tide dial (`TownHud.River.cs:105`) is drawn with DrawCircle/DrawArc in the interface; not pictured here.
- Boats loaded and empty as separate pictures: only the landing with whatever boats are moored at that moment (`landing-with-boats`, `landing-low-water`).
- Plot stakes and touches in place: pictured as model files (`plot-stakes`, `prop-*`) on plain ground, not where the town puts them.

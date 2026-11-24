# assets/src: generators and shared tooling

Everything in `assets/` that is made in code is rebuilt from a script in this folder. The rules (palette, scale, formats, naming, ledger, done checklist) are in `docs/asset-pipeline.md`; this page is how to use the tooling.

```
assets/src/
  common/          shared helpers (import as `common`): palette, glb, png, svg, audio, ledger, outline_screen.gdshader
  ledger-rows/     per-group ledger fragments, merged into assets/LEDGER.csv later (see its README)
  models/          generators for assets/models/*.glb            (one asset group per folder)
  textures/        generators for assets/textures/*.png
  icons/           SVG sources or generators for assets/icons/*.svg
  audio/           generators for assets/audio/{sfx,music,ambience}/*
  contact-sheets/  saved contact sheets (<name>.png), the guide's record copy
```

Setup: `tools/setup-linux.sh` (or just `python3 -m pip install -r tools/requirements-assets.txt`). The helpers need **numpy and scipy only**, plus `ffmpeg`, `godot` and `xvfb-run` on PATH. Do not import Pillow, matplotlib or cairosvg: their licences (MIT-CMU, Matplotlib/PSF, LGPL-3.0) are not on the ledger allow list, so the helpers avoid them. A new Python package needs a `kind tool` ledger row with the licence from its installed metadata.

## Commands

| What | Command (from the repository root) |
| --- | --- |
| Rebuild everything, then check the ledger | `tools/build-assets.sh` |
| Rebuild only some groups | `tools/build-assets.sh models` (also `textures icons audio ...`) |
| Run one generator | `python3 assets/src/models/well.py` |
| Rebuild the palette atlas | `python3 assets/src/common/palette.py` (reads the Palette table in `docs/asset-pipeline.md`; writes `assets/textures/palette.png` and `assets/src/common/palette.json`) |
| Contact sheet of a model | `tools/render-contact-sheet.sh assets/models/well.glb --sides` -> `build/contact-sheets/well.png` |
| Record copy of the sheet | add `--save` -> also `assets/src/contact-sheets/well.png` |
| Palette check of a model | `tools/render-contact-sheet.sh M.glb --unlit --noshadow --outline=none` then `PYTHONPATH=assets/src python3 -m common.png audit build/contact-sheets/M-unlit.png` |
| Palette check of a texture | `PYTHONPATH=assets/src python3 -m common.png audit assets/textures/x.png` |
| Icon lint / preview | `PYTHONPATH=assets/src python3 -m common.svg lint assets/icons/*.svg` and `... -m common.svg sheet build/icons.png assets/icons/*.svg` (24, 48, 96 px on Paper, rendered by Godot) |
| Sound check | `PYTHONPATH=assets/src python3 -c "from common import audio as A; print(A.check('assets/audio/sfx/x.wav'))"` |
| Ledger check incl. fragments | `python3 tools/check-ledger.py --fragments` |

`render-contact-sheet.sh` options: `--sides` (four low side views at yaw 0/90/180/270, wanted by the done checklist), `--view=14` (metres across a panel; 20-24 for a 16 m field), `--unlit`, `--noshadow`, `--outline=none`, `--line=1.5`, `--crease=0.45`, `--crease_soft=1.5` (creases on `ink="soft"` nodes), `--near=8`, `--focus_y=1.5`, `--panel=480`, `--tile`, `--out=PATH`. It prints the triangle count and bounding box Godot sees (`MODEL tris=... size=...`), and fails on a shader or script error. Renders take about 10 s and are serialised by a lock, so parallel builders can call it at the same time (they just wait).

## Writing a generator

1. Put it directly in your group's folder: `assets/src/<group>/<asset-name>.py`. Files starting with `_` are helpers, not generators; `tools/build-assets.sh` runs every other `*.py` there (and executable `*.sh`), in name order, from the repository root.
2. First lines: the command that runs it and what it writes, then the two-line bootstrap so `common` imports both by hand and from `build-assets.sh`:
   ```python
   # python3 assets/src/models/well.py  ->  assets/models/well.glb
   import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
   from common import glb, png, audio, svg, ledger, palette as P, asset_path
   ```
   Do not name a generator after a standard or common module (`wave.py`, `random.py`, `common.py`): its folder is first on the import path.
3. Deterministic: fixed seeds (`np.random.default_rng(SEED)`), no clock, no network. Running it twice must give identical bytes (`write_glb`, `write_png`, `write_wav` and `encode_ogg` are byte-stable).
4. Write outputs with `asset_path("models/well.glb")` (creates the folder). Scratch output (previews, WAVs behind an Ogg loop, spectrograms) goes to `build/`, never into `assets/`: every file under `assets/` outside `src/` and `licences/` needs a ledger row.
5. Register every output file with one `ledger.add(...)` call (next section).
6. Run it, look at the result (contact sheet, `png.tile` preview, spectrogram), then run `tools/build-assets.sh <group>`.

## Ledger rows for generated files

Every generated file gets one row. The helper writes the row for you and replaces it when the generator runs again. By default it goes to `build/ledger-rows/<group>.csv` (scratch, so rebuilding never rewrites a tracked file); `tools/build-assets.sh` clears that file before the group runs, checks it, and lists rows `assets/LEDGER.csv` lacks or has differently; merge them with `python3 tools/merge-ledger-rows.py --build --prune` (`--prune` drops ledger rows for files that no longer exist). Builders working in parallel set `ASSET_LEDGER_FRAGMENTS=1` (or pass `--ledger-fragments` to a generator) to write their group's tracked fragment, `assets/src/ledger-rows/<group>.csv` (ledger columns, no header), which the lead merges with plain `tools/merge-ledger-rows.py`. Nobody edits `assets/LEDGER.csv` by hand while builders work in parallel.

```python
ledger.add("models", out, kind="model", generator=__file__, used_in="Hamlet: well", seed=1, verified=False)
```

which writes (to the row file above):

```
assets/models/well.glb,model,generated by assets/src/models/well.py (seed 1); script written by Claude Opus 5.5,self-made,,Hamlet: well,2026-11-24,no
```

- `kind`: `model`, `texture`, `icon`, `font`, `sound`, `music` or `ambience`.
- `source`: `generated by assets/src/<group>/<script>.py` (that file must exist, or the check fails), the seed if there is one, and `script written by <who>` (default `Claude Opus 5.5`; set `writer=` or the `ASSET_SCRIPT_WRITER` environment variable if someone else wrote it). Add `note=` for anything else worth disclosing.
- `licence`: `self-made`. `url`: empty. `date_checked`: today.
- `verified`: `no` until every line of the done checklist in `docs/asset-pipeline.md` is ticked, then pass `verified=True`.
- Downloaded or hand-made files (a font, a hand-written SVG) get a row typed by hand into the same fragment, same columns.

Check: `python3 tools/check-ledger.py --fragments --build` (also run by `build-assets.sh`). Plain `python3 tools/check-ledger.py`, which `./check` runs, sees only `LEDGER.csv`, so it fails on your new files until the fragments are merged; that is expected while you work.

## The helpers

**`common.palette`**: `P.rgb("Meadow")`, `P.hex_of("thatch")`, `P.uv(name)`, `P.index(name)`, `P.nearest((r, g, b))`, `P.NAMES`, `P.STATE` (Okabe-Ito state colours, UI and icons only, never models). Names take the full palette name, an alias (`paper ink meadow bent trail packed mud river deep thatch reed brick slate soot iron concrete stone mudflat lime snow night`) or an index.

**`common.glb`** (models): `m = glb.Mesh()`, then `add_box(pos, (sx, sy, sz), colour, yaw=, top=, bottom=)` (colour can be a per-side dict `{'+x','-x','+y','-y','+z','-z','default'}`), `add_gable(pos, length, width, height, colour, gable=, ridge_axis='x'|'z', overhang=)`, `add_hip(...)`, `add_cylinder / add_cone / add_frustum(pos, r, h, colour, segments=)`, `add_sphere(centre, r, colour, rings=, segments=, squash=)`, `add_extrude(pos, [(x, z), ...] convex, height, colour, top=)`, `add_poly(points, colour, facing=)`, `append(other_mesh, pos, yaw)`; `tri_count()`, `bounds()`. `pos` is the centre of the part's base; yaw is radians about +Y. Write with `glb.write_glb(path, m, max_tris=500)` or `glb.write_glb(path, [("body", b), ("head", h), ("item", i)])` for separate nodes (villagers). It returns triangles, size and colours used; `glb.report(path, stats)` prints them. Pass `ink="soft"` for many-faceted organic models (trees, crops, reeds) so the ink pass inks their creases only above `glb.INK_SOFT` (see the pipeline guide, Model rules). Every face is one palette colour via the embedded atlas (material `Palette`, nearest sampler, no mipmaps); Godot imports it with no import script. Bottoms of parts standing on the ground are dropped automatically; pass `bottom=False` for parts that sit on other parts.

**Outline**: not modelled. The ink line is the screen-space depth edge pass, `common/outline_screen.gdshader` (the game should use this same file). It inks silhouettes and creases (ridges, wall/roof, wall/ground), not colour-only changes, so a door painted flush on a wall gets no line: make doors, shutters and windows stand out a little (0.1 to 0.15 m) if they should be outlined.

**`common.png`** (textures): `png.write_png(arr, path)`, `png.read_png(path)`, `png.tile_noise(size, cells, rng, octaves)` (seamless), `png.snap2(a, b, t, threshold)` (two palette colours only), `png.layer(img, colour, mask)`, `png.snap(img)` (nearest palette colour), `png.check_tiles(img)`, `png.palette_fraction(img)`, `png.audit(img)`, `png.tile(img, 2)`, `png.upscale(img, k)`.

**`common.svg`** (icons): `svg.lint(path, icon_style=False)` (viewBox, plain SVG, palette colours, round caps/joins; `icon_style=True` adds the house language: 2-unit Ink strokes, Paper or Ink fills, at least one Paper shape), `svg.rasterise(svg, png, size, bg="Paper")`, `svg.sheet(svgs, out_png)`.

**`common.audio`** (sound): voices `step_grass`, `step_dirt`, `bell`, `karplus_strong`, `bow`, `click(reverse=)`; noise `white`, `pink`, `brown`, `wobble`; `lowpass / highpass / bandpass`, `env_exp`, `env_adsr`, `fade`, `mix`, `pad`, `delay`, `pan`, `to_stereo`, `loop_crossfade(x, fade_s)` (sample-exact seamless loop), `normalize_peak(x, -6)`; `write_wav` (48 kHz 16-bit, mono or stereo), `encode_ogg(wav, ogg, stereo=True)`; `check(path)` (true peak, momentary-max and integrated LUFS, format, start/end clicks; `problems` lists what breaks the guide), `estimate_f0(x, lo, hi)`, `spectrogram_png(path, png)`, `NOTE["D4"]`, `LANES`.

## Example generators (tested)

Model:

```python
# python3 assets/src/models/well.py  ->  assets/models/well.glb (2.4 x 2.4 m, 3 m high, front +Z)
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
from common import glb, ledger, asset_path
m = glb.Mesh()
m.add_cylinder((0, 0, 0), 1.1, 0.9, "Concrete", segments=8)                      # stone ring
m.add_cylinder((0, 0.9, 0), 0.85, 0.02, "River", segments=8, cap_bottom=False)   # water
for x in (-0.95, 0.95): m.add_box((x, 0.9, 0), (0.2, 1.6, 0.2), "Mud")            # posts
m.add_gable((0, 2.5, 0), 2.2, 1.4, 0.5, "thatch", gable="Mud", overhang=0.1)
out = asset_path("models/well.glb"); print(glb.report(out, glb.write_glb(out, m, max_tris=300)))
ledger.add("models", out, kind="model", generator=__file__, used_in="Hamlet: well")
```

Texture:

```python
# python3 assets/src/textures/ground-meadow.py  ->  assets/textures/ground-meadow.png (512 x 512, tiling)
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import png, ledger, asset_path
SEED = 1; rng = np.random.default_rng(SEED)
n = png.tile_noise(512, 8, rng, octaves=3)                           # seamless value noise, 0..1
img = png.snap2("Meadow", "Bent grass", n + (rng.random(n.shape) - 0.5) * 0.3, 0.58)
png.layer(img, "Cast-iron green", (png.tile_noise(512, 64, rng) > 0.85) & (rng.random(n.shape) > 0.5))
out = png.write_png(img, asset_path("textures/ground-meadow.png"))
print(out, "seam (wrap, inner):", png.check_tiles(img), "on palette:", png.palette_fraction(img))
ledger.add("textures", out, kind="texture", generator=__file__, used_in="Ground: meadow", seed=SEED)
```

Sound:

```python
# python3 assets/src/audio/step-grass.py  ->  assets/audio/sfx/step-grass-01..08.wav (48 kHz 16-bit mono)
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import audio as A, ledger, asset_path
for i in range(1, 9):
    seed = 100 + i
    x = A.normalize_peak(A.step_grass(np.random.default_rng(seed)), -6)
    out = A.write_wav(asset_path(f"audio/sfx/step-grass-{i:02d}.wav"), x)
    r = A.check(out); print(out.name, r["true_peak_dbtp"], "dBTP", r["momentary_max_lufs"], "LUFS (M max)", r["problems"])
    ledger.add("audio", out, kind="sound", generator=__file__, used_in="Villagers: footsteps on grass", seed=seed)
```

## Previewing

- Models: `tools/render-contact-sheet.sh` (above). Look at the sheet: top view (roof reads?), 45-degree views from south, east and north, and with `--sides` the four low side views (no blank backs). Lit colours never match the palette exactly; check colours with `--unlit`.
- Textures: write `png.tile(img, 2)` to `build/` and look for seams; `png.check_tiles` gives the wrap difference against the inside difference.
- Icons: `svg.sheet(...)` to `build/`; judge them at 24 px.
- Audio: `A.spectrogram_png(path, "build/audio/<name>.png")` (waveform on top; spectrogram 0 to 12 kHz with grey lines every 1 kHz and Paper ticks at the 70/300 Hz and 1/3/6 kHz lane edges). Listen if you can; check `A.check(path)["problems"]` is empty.
- In the game: `tools/sync-assets.sh` mirrors `assets/` into `game/assets/`; scenes load `res://assets/models/well.glb`.

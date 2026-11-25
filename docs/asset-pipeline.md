# Asset pipeline

How every model, texture, icon, font and sound gets into Tide & Toll. Follow it without the design doc. Source: the Build Plan's five steps and the Theme and Art and Sound and Music tabs.

## The five steps

1. **Search free sources first.** CC0 is preferred: Kenney, Quaternius, Poly Haven, ambientCG, OpenGameArt (CC0 only), public-domain scans, and GitHub repositories with a clear licence file. Write down where you looked, even when nothing fits.
2. **Otherwise make it.** Build it in code (generated meshes, SVG, synthesised audio, shader code) or with a tool whose output you may sell. Approved AI tools need the owner's sign-off first, so none are used yet. Nothing is generated while the game runs.
3. **Fit it to the Living Map style.** Snap colours to the palette, meet the triangle budget, strip foreign textures, and check it from every angle (the contact sheet below).
4. **Log it.** Add a row to `assets/LEDGER.csv` before the file goes into the game. `./check` fails without one.
5. **Disclose.** The ledger feeds the credits and the Steam AI disclosure, so say honestly how each file was made.

**Tonight (Phase 0 and the first asset round):** kenney.nl, polyhaven.com, ambientcg.com, opengameart.org, quaternius.com, freesound.org and fonts.google.com are blocked from the build container. Do not work round the block. Raw files from public GitHub repositories, npm and PyPI are reachable (for example OFL fonts from `github.com/google/fonts`). Otherwise make the asset in code (see "Making assets in code").

## Living Map style in one paragraph

A town plan in ink and gouache on paper, built from chunky low-poly 3D seen from above. Flat colour, no photo textures, a thin warm-black ink outline (drawn later by one screen-space edge pass, so do not model outlines into meshes). Every model is finished on all four sides: backs get doors, windows or yards. Roofs carry identity from the top; facades matter when the camera tilts. Trees are round gouache canopies. Water is flat slate blue. One sun, from the south-east. No depth of field, tilt-shift, heavy bloom or haze. Readability comes first: shape, then colour, then motion. A colour never carries meaning alone.

## Palette

Starting values from the Art tab. Use these exact hex values (sRGB).

| Name | Hex | Use |
| --- | --- | --- |
| Paper | `#F2EAD8` | page, highlights |
| Ink | `#2B2620` | outlines, icon strokes, text |
| Meadow | `#7FA35B` | grass |
| Bent grass | `#A9B86E` | trampled grass |
| Trail earth | `#B89568` | trails |
| Packed earth | `#8C6A48` | tracks, yards |
| Mud | `#5E4A36` | mud, wet ground, wood shadow |
| River | `#5B7F95` | water |
| Deep water | `#3F5F75` | deep water |
| Reed and thatch | `#C9A55A` | thatch roofs, reeds, straw |
| Brick | `#A4553A` | brick, tiles |
| Slate roof | `#5E6B78` | slate roofs |
| Soot brick | `#7A4436` | old brick, dark wood |
| Cast-iron green | `#44604F` | ironwork, foliage shadow |
| Concrete | `#A9A69C` | stone, later eras |
| Wet mudflat | `#6F6450` | banks, ford stones in water |
| Limewash | `#E9E2CF` | walls |
| Snow | `#EEF2F4` | winter |
| Night tint | `#2E3A5C` | night grade only |

State colours (Okabe-Ito), always paired with a shape and a motion: flowing `#009E73` (circle), slow `#E69F00` (triangle), blocked `#D55E00` (diamond), boat or water info `#0072B2` (anchor), player preview `#56B4E9` (dotted outline), heritage `#CC79A7` (shield), money `#F0E442` (coin with ink outline). Never use these on models.

Models do not carry their own textures. They use one shared 256 x 256 palette atlas, `assets/textures/palette.png`: a 16 x 16 grid of 16 px swatches, swatch `i` at column `i % 16`, row `i / 16`, in the table order above (Paper is swatch 0). A face's UVs all point at the centre of one swatch. Until the atlas exists, use vertex colours or one material per palette colour, with the material named after the palette entry (for example `Meadow`).

## Scale and units

- 1 unit = 1 metre. **One map cell is 2 m.** The Phase 0 map is 128 x 128 cells (256 m); the game default is 512 x 384.
- +Y is up. North is -Z, east is +X. The sun comes from the south-east.
- People and boats are drawn about 1.5 times life size so they read from above; buildings are life size.
- At 1080p and default zoom a villager is 10 to 12 px tall, a house footprint 40 to 80 px, a boat 30 to 90 px.

| Asset | Footprint (m) | Height (m) | Triangles (max) |
| --- | --- | --- | --- |
| Villager (1.5x) | 1.0 x 1.0 | 2.6 | 300 |
| Cottage | 8 x 6 (4 x 3 cells) | 6 to the ridge | 500 |
| Well | 2.4 x 2.4 | 3 | 300 |
| Market stall | 4 x 3 | 3 | 400 |
| Field | 16 x 16 (8 x 8 cells), crops under 0.6 high | 0.6 | 500 |
| Fence segment | 2 long (one cell edge), posts at both ends | 1.0 | 100 |
| Gate | 2 long | 1.2 | 150 |
| Footbridge | 2 wide, spans in 2 m modules | rails 1.2 above the deck (deck 0.6 above the water, so 1.8 overall) | 300 per module |
| Ford stones | 2 x 2 (one cell) | 0.3 | 150 |
| Rowboat (1.5x) | 6.8 x 2.4 | 1.2 | 300 |
| Tree | canopy 4 to 7 across | 5 to 9 | 300 |
| Reed clump | 2 x 2 | 1.5 | 150 |

Landmarks may go to about 1,500 triangles. Everything else stays under 500.

The footbridge row measures its rails from the deck people walk on (corrected 2026-11-24, when the review read "1.2" as the overall height): villagers are drawn 1.5 times life size, so a 1.2 m rail is about waist height on them, and the deck stays 0.6 m above the water so masted boats cannot pass (Art tab).

## Model rules

- **Ink weight:** the outline pass inks creases above a threshold (0.45). Many-faceted organic models (tree crowns, crop rows, reeds) would carry far more ink than the houses at that setting, so they are written with `glb.write_glb(..., ink="soft")`: their glTF nodes carry extras `{"ink": "soft"}`, the renderer marks them in the stencil buffer and inks their creases only above 1.5 (`common.glb.INK_SOFT`). Silhouettes are inked as usual. See `assets/src/common/outline_screen.gdshader`.

- Low-poly, flat-shaded, chunky. No detail smaller than about 0.3 m: it will not survive the top view.
- **Origin** at the centre of the footprint, on the ground (y = 0). Snap footprints to whole cells.
- **Front** (door, bow, face) faces **+Z** (south), so the default tilted camera, which looks north, sees it.
- Readable from every angle: from the top (roof shape, colour), at 45 degrees, and from the side at four yaws (0, 90, 180, 270). No blank backs.
- Villagers have no rig. Separate the body into at most three parts (body, head, carried item) so a shader can bob and sway them.
- Apply all transforms before export. No cameras, lights or animations in the file.
- Format: **glTF 2.0 binary (`.glb`)**.

## Textures

- Ground and wear textures are drawn by shader code first (world-space, so nothing swims when the camera turns). When a bitmap is needed: PNG, sRGB, power-of-two size (512 x 512 for ground tiles, 256 x 256 for the palette atlas), seamlessly tiling, colours snapped to the palette.
- Keep the generator (shader, Python, Material Maker graph) under `assets/src/` (the ground tiles are in `assets/src/2d/ground.py`).
- **One exception to snapping: `assets/textures/wear-ramp.png`.** It is a lookup gradient for the wear shader, not a picture: wear (0 to 1) in, ground state out. Its five plateaus are exact palette colours (Meadow, Bent grass, Trail earth, Packed earth, Mud); only the narrow transitions between them (plus or minus 0.035 around each threshold, 63 colours in all) are interpolated in linear light, on purpose, so the ground changes smoothly instead of popping a whole stage when one footstep crosses a threshold. Nothing shows a ramp pixel as a surface colour: the shader uses it to choose and mix the ground textures, which are fully on the palette. Decided 2026-11-24 after the art review; the generator says the same.
- Before calling a ground texture done, lay it 4 x 4 and look for a repeating blob or a stripe that points one way (`assets/src/2d/previews/ground-tiled-4x4.png`). Brush marks go in every direction: the camera rotates, so a mark laid east-west shows as streaks.

## Icons

- SVG, `viewBox="0 0 24 24"`, drawn on a 24 px grid and readable at 24 px (problem icons 28 to 32 px).
- 2 px Ink (`#2B2620`) strokes with round caps and joins; fills from the palette, usually Paper.
- House drawing language (all 14 icons since 2026-11-24): crisp geometry, every element a **closed shape with a Paper fill inside a 2-unit Ink outline**, one stroke weight everywhere. Things that would be open lines (rails, sound waves, arrows) are closed bands at least about 3 units wide. Ink fill only for small accents inside a Paper shape. So the same file reads on paper (by its outline) and on ink or any dark ground (by its paper body) with no recolouring and no mask. `svg.lint(path, icon_style=True)` checks it.
- Look at every icon at 20, 40 and 80 px on Paper and on Ink, rendered from the real files (`assets/src/2d/previews/icons-preview.png`).
- Plain SVG only: no embedded bitmaps, no fonts (convert text to paths), no scripts.

## Fonts

- TTF or OTF under the SIL Open Font License 1.1 only. Save the font's `OFL.txt` as `assets/licences/<FontName>-OFL.txt` (it ships with the game).
- A good source tonight: raw files in `github.com/google/fonts` (the `ofl/` folder), which is reachable.

## Audio

- One-shots (footsteps, place, erase, bell, water hits): **WAV, 48 kHz, 16-bit, mono**. Make 6 to 8 variations of each (the game plays them round-robin with ±2 dB and ±50 cents on top).
- Loops and music (water bed, theme): **Ogg Vorbis, 48 kHz, stereo**, loop points at sample-exact boundaries.
- Loudness: peaks under -1 dBFS; aim the gameplay mix at about -20 LUFS. Leave headroom rather than normalising every file to the top.
- Frequency lanes: drone 70 to 300 Hz; bells 300 Hz to 3 kHz; footfall 1 to 6 kHz; UI about 2 to 4 kHz.
- The theme is in D. Music tempo never follows game speed. No stinger on every click, no alarms, no speech.
- More music, same composer: `assets/src/audio/_music.py` is the shared engine (the theme's pipe, lyre, frame drum, drone, mix, reverb and -20 LUFS level, copied from `theme-hamlet.py`, which stays as it is). Each piece is a small `music-*.py` generator holding its notes; `playlist-music.py` writes `assets/audio/music/playlist.json` (roles `age1`, `age-change`, `pause`; rotation order, theme first). Pieces are Vorbis quality 7 (the theme is 5) so the encoder's error at the loop join stays under the seam check; WAV masters go to `build/audio-music/`. Measure everything beside the theme with `PYTHONPATH=assets/src python3 assets/src/audio/_check_music.py` (loudness, spectrum, seam, mode of every note, shared melody runs of at most 4 notes).
- Incremental town sounds: voices in `assets/src/audio/_town.py` (quill, paper tick, chime bar, hand and chapel bell, saw, hammer, stones, gravel, wax seal, book cover; it reuses `_synth` and `_river` and changes neither). `town-tools.py`, `town-bells.py`, `town-ui.py` write mono WAVs into `assets/audio/sfx/`; `mill-loop.py` writes `mill-loop.ogg` there (stereo Vorbis q7, 6 s seamless, the guide's loop format); `town-zz-sounds-json.py` runs last (name order) and writes `assets/audio/sfx/town-sounds.json` (event, files, volume against the coin, loop, whether pitch may vary). Pitched ones are in D Dorian and marked `pitch_vary: false`. Levels follow coin (-24 LUFS momentary max) and place (-22); UI sits under them.

## Files and names

Lowercase, words joined by hyphens, a two-digit number for variations.

```
assets/
  LEDGER.csv              one row per file (below)
  models/                 cottage.glb, well.glb, rowboat.glb, tree-oak.glb, villager.glb
  textures/               palette.png, ground-meadow.png, wear-trail.png
  icons/                  tool-fence.svg, tool-gate.svg, tool-gravel.svg, tool-erase.svg
  fonts/                  <FontName>-Regular.ttf
  audio/sfx/              step-grass-01.wav ... step-grass-08.wav, bell-01.wav, place-01.wav
  audio/music/            theme-hamlet-draft.ogg, music-*.ogg (+ -score.json), playlist.json
  audio/ambience/         water-river-loop.ogg
  licences/               saved licence texts and pages (OFL.txt copies, CC0 page PDFs)
  src/                    generator code, prompts and contact sheets (not shipped)
    models/ textures/ icons/ audio/ contact-sheets/
```

Godot can only load files inside `game/`, so `tools/sync-assets.sh` mirrors the shipping folders into `game/assets/` (generated and gitignored; `./check` runs it). Edit `assets/`, never `game/assets/`. Scenes load assets as `res://assets/models/cottage.glb`.

## The ledger

`assets/LEDGER.csv` keeps its existing header:

```
item,kind,source,licence,url,used_in,date_checked,verified
```

| Column | What to write |
| --- | --- |
| `item` | the file's path from the repository root, for example `assets/models/cottage.glb` (tools and packages use their package name) |
| `kind` | `model`, `texture`, `icon`, `font`, `sound`, `music`, `ambience` (plus the existing `npm-dev-dependency`; add `tool` or `nuget` for software) |
| `source` | who made it and how. Downloaded: author and site. Made in code: `generated by assets/src/<path> (seed N); script written by <who>`. Edits: append `; edited: <what changed>` |
| `licence` | one of the allow-list tokens below, exactly |
| `url` | the download page or licence page (empty for self-made) |
| `used_in` | where the game uses it (`Hamlet: houses`, `UI: tool bar`) |
| `date_checked` | the day you checked the licence, `YYYY-MM-DD` |
| `verified` | `yes` once the checklist below is ticked, else `no` |

Quote a field that contains a comma. One row per file: eight footstep variations are eight rows.

**Licence tokens `./check` accepts:** `CC0` (preferred), `CC-BY-3.0` or `CC-BY-4.0` (put the credit line in `source`), `public domain`, `OFL-1.1` (fonts), `MIT`, `Apache-2.0`, `BSD-2-Clause`, `BSD-3-Clause`, `Zlib` (code; keep the notice), `Sonniss GDC`, and `self-made` (made by this project, by hand or in code).

**Never:** anything non-commercial (CC BY-NC and friends), no-derivatives, share-alike where the terms are unclear, Sampling+, BBC RemArc, "free for personal use", YouTube-only libraries, AI output from a tier that forbids commercial use, or any file whose origin or licence you cannot show. Delete such files; do not ship them.

For a `self-made` row, `source` must name the generator under `assets/src/` and that file must exist, or `./check` fails. For downloads, save the licence page into `assets/licences/` on the day.

**AI disclosure:** a file whose content came out of a generative AI model (image, 3D, audio or music generators) needs the tool, model version, prompt, seed and terms date in `source`, and is disclosed on Steam as pre-generated AI content. Code written by an AI coding agent that then builds an asset by rules (a mesh script, a synthesiser) is recorded as `script written by <agent and model>` so the disclosure report can decide; do not hide it.

Generators do not touch tracked ledger files on a normal rebuild: `ledger.add(...)` writes to `build/ledger-rows/<group>.csv` (scratch), `tools/build-assets.sh` lists the rows that `LEDGER.csv` lacks or has differently, and `python3 tools/merge-ledger-rows.py --build --prune` merges them and drops rows for files that no longer exist. Builders working in parallel set `ASSET_LEDGER_FRAGMENTS=1` to write the tracked fragments in `assets/src/ledger-rows/` instead.

Example rows:

```
assets/models/cottage.glb,model,"generated by assets/src/models/cottage.py (seed 1); script written by Claude Opus 5.5",self-made,,Hamlet: houses,2026-11-24,yes
assets/fonts/Alegreya-Regular.ttf,font,"Juan Pablo del Peral / Huerta Tipografica, via github.com/google/fonts",OFL-1.1,https://github.com/google/fonts/tree/main/ofl/alegreya,UI: headings,2026-11-24,yes
```

## Making assets in code

Keep every generator in `assets/src/`, deterministic (fixed seed, no clock), and runnable with one command from the repository root. Write the command at the top of the script.

- **Models:** a Python script that writes `.glb`, using `pygltflib`, `trimesh` or plain `struct` (PyPI is reachable), or Blender in background mode (`blender -b -P assets/src/models/cottage.py`; Blender is GPL but its output is yours). Build from boxes, prisms and low-segment cylinders; flat normals; vertex colours or palette swatch UVs.
- **Textures:** Python with `numpy` and `Pillow`, or a Godot shader. Snap every pixel to the palette, then add grain by mixing two neighbouring palette colours, not by inventing new ones.
- **Icons:** hand-written SVG (it is text; write it directly).
- **Audio:** Python with `numpy` (and `scipy` if needed) writing WAV with the standard `wave` module: filtered noise bursts for footsteps (grass: soft, high-passed; dirt: drier, more low-mid), additive partials with slow decay for the bell, a short filtered click for place and a reversed one for erase, low-passed noise with a slow random wobble for water. Use `oggenc` or `ffmpeg` (apt) for Ogg. Record the seed per variation.

## Contact sheet

Before calling a model done, render it from the top, at 45 degrees, and from the side at yaws 0, 90, 180 and 270, on Paper, and save the sheet as `assets/src/contact-sheets/<name>.png`. Look at it. A Godot scene or Blender script that renders the sheet is fine; keep it in `assets/src/`.

## Done checklist

Tick every line before `verified` becomes `yes`:

- [ ] Searched free sources first, or noted why not (blocked, nothing fits).
- [ ] Licence is on the allow list; licence page or OFL text saved in `assets/licences/` for downloads.
- [ ] File in the right folder, named lowercase-with-hyphens, in the right format (`.glb`, `.png`, `.svg`, `.ttf`/`.otf`, `.wav`, `.ogg`).
- [ ] Colours snapped to the palette; no foreign textures; state colours not used on models.
- [ ] Model: metres, origin at footprint centre on the ground, front facing +Z, within the triangle budget, transforms applied.
- [ ] Contact sheet saved and looked at: readable from the top, at 45 degrees and from four sides, no blank backs.
- [ ] Audio: right format, peaks under -1 dBFS, 6 to 8 variations for one-shots, no clicks at loop points.
- [ ] Generator script (for made-in-code assets) is in `assets/src/` and re-creates the file exactly.
- [ ] Ledger row added; `python3 tools/check-ledger.py` passes.

# Ink, line weight and sharpness: code map (n8 fact-finding)

Read-only lookup. Paths are relative to `/home/user/tide-toll`. Quotes are the deciding lines. "not confirmed" marks what could not be checked in code or in the documentation.

## 1. The ink pass

### Uniforms of `game/shaders/outline_screen.gdshader`

| Line | Uniform | Default in shader | Set by code |
| --- | --- | --- | --- |
| 10 | `depth_tex` (hint_depth_texture, filter_nearest) | n/a | n/a |
| 11 | `screen_tex` (hint_screen_texture, filter_nearest) | n/a | only read when `use_colour` = 1 |
| 12 | `ink` | `vec4(0.1683, 0.1441, 0.1236, 1.0)` (palette Ink #2B2620) | `m.SetShaderParameter("ink", Palette.Ink)` (`game/scripts/InkOutline.cs:41`) |
| 13 | `width_px` | `1.0` | `line * viewportPx.Y / 1080f` (`InkOutline.cs:72`) |
| 14 | `metres_per_px` | `0.05` | `cam.Size / viewportPx.Y` (`InkOutline.cs:71`) |
| 15 | `crease` | `0.45` | `0.45f` hard pass, `1.5f` soft pass (`InkOutline.cs:30`) |
| 16 | `use_colour` | `0.0` | not set (colour edges off) |
| 17 | `colour_edge` | `0.12` | not set |
| added | `ink_alpha` | `1.0` | injected by code (`InkOutline.cs:36`), set per frame on the soft pass only (`InkOutline.cs:76`) |

Fragment logic, `outline_screen.gdshader:28-39`:

- `int w = max(int(width_px + 0.5), 1);` so at 1080p `w` is 1 while `width_px` < 1.5 and 2 from 1.5 up.
- `float step_m = metres_per_px * float(w);`
- `float e = step(crease * step_m, max(lx, ly));` where `lx`, `ly` are the absolute second differences of view depth over `w` pixels.
- `ALPHA = e;` (line 46). The soft pass gets `ALPHA = e * ink_alpha;` (`InkOutline.cs:37`).

### How the width follows zoom (`game/scripts/InkOutline.cs`)

- Line 59: `public static float LinePx(float pxPerMetre) => Mathf.Clamp(Mathf.Remap(pxPerMetre, 4f, 12f, 1.0f, 1.6f), 1.0f, 1.8f);`
- Line 22: `SoftInk(pxPerMetre) => Mathf.Clamp(Mathf.Remap(pxPerMetre, 3.5f, 9f, 0.4f, 1f), 0.4f, 1f)`
- Line 67-68: `float pxPerM = viewportPx.Y / cam.Size; float line = LinePx(pxPerM * 1080f / viewportPx.Y);`
- So the inputs are the view's pixels per metre, normalised to 1080 px height. Width is `LinePx` (1.0 to 1.8 px at 1080p), and `width_px` is that value times `viewportPx.Y / 1080`.
- The doc comment (`InkOutline.cs:10`) says "about 1 px when the whole page is in view, 1.5 to 2 px at play zoom". The code range is 1.0 to 1.8 px; the play-zoom value was not computed (not confirmed).
- Soft ink strength (`InkOutline.cs:74-76`): full at about 9 px/m (1080p), 40 percent at 3.5 px/m or less.
- Early return (`InkOutline.cs:64`): `if (cam.Size == _lastSize && viewportPx.Y == _lastH) return;`. Materials are touched only when zoom or window height changes.

### Two passes (`InkOutline.Attach`, `InkOutline.cs:24-56`)

- `string[] cmp = { "compare_not_equal", "compare_equal" };` with `float[] crease = { 0.45f, 1.5f };`
- Replacement (line 36): `code.Replace("\n// STENCIL_MODE\n", $"\nstencil_mode read, {cmp[i]}, 1;\nuniform float ink_alpha = 1.0;\n")`
- Node names: `InkHard` (compare_not_equal, crease 0.45) and `InkSoft` (compare_equal, crease 1.5). Each is a 2x2 `QuadMesh`, `ExtraCullMargin = 16384f`, shadows off, `Position = (0,0,-1)`, child of the camera (`InkOutline.cs:43-53`, `cam.AddChild(this)` at line 55).
- Line 28 loads the shader with `GD.Load<Shader>("res://shaders/outline_screen.gdshader").Code`.

### Where `InkOutline.Update` is called

- Attach: `game/scripts/Main.cs:135-136` (`Ink = new InkOutline(); Ink.Attach(Rig.Camera);`).
- Update: `game/scripts/Main.cs:292`: `Ink.Update(Rig.Camera, vp);`, inside `Main._Process` (starts at line 281). `vp` is `GetViewport().GetVisibleRect().Size` (`Main.cs:289`).
- The legacy game has its own copy (`game/legacy/scripts/Main.cs:45, 150`); not compiled (`.gdignore`).

### Camera settings the ink depends on

- `game/scripts/CameraRig.cs:136`: `Size = 380f`.
- `CameraRig.cs:138-139`: `// Tight near/far: the ink pass reads depth differences, so precision matters (proof 4).` with `Near = Distance - 100f, Far = Distance + 100f`. The value of `Distance` was not read (not confirmed); the `InkOutline` doc comment says "200..400 m".

### Canonical copy versus game copy

- `assets/src/common/outline_screen.gdshader` and `game/shaders/outline_screen.gdshader` are NOT byte-identical. `diff` shows one difference: the game copy has an extra first line, `// Copy of assets/src/common/outline_screen.gdshader (the canonical file; keep them identical).` (`game/shaders/outline_screen.gdshader:1`). The shader body is identical.

## 2. Which models are soft ink, and how they are marked

### Marking mechanism

- Generator side: `assets/src/common/glb.py:231`: `INK_SOFT = 1.5     # crease threshold for ink="soft" models`. `write_glb(..., ink=None)` (line 234); `ink="soft"` marks nodes with glTF node extras `{"ink": "soft"}` (`glb.py:240-301`, line 300-301: `node["extras"] = {"ink": node_ink}`).
- Per-part marking: a part tuple may carry its own ink as third element (`glb.py:246`: `p[2] if len(p) > 2 and p[2] is not None else ink`).
- Godot side, `game/scripts/ModelLib.cs`:
  - `HasSoftInk(Node)` (lines 120-125): reads node meta `extras`, returns true if `"ink" == "soft"`.
  - `Collect` (lines 107-118): `soft |= HasSoftInk(n);`. A model is soft if any of its nodes is soft.
  - `Material(src, soft)` (lines 127-144): when soft, `StencilMode = Custom; StencilFlags = 2 /*write*/; StencilCompare = Always; StencilReference = 1`.
  - `IsSoft(model)` (lines 100-104) returns `StencilMode == Custom` on the first surface material.
  - All model materials use `TextureFilter = Nearest` (`ModelLib.cs:132`).
- Shader side: `game/shaders/tree.gdshader:8` `stencil_mode write, compare_always, 1;` and `game/shaders/villager.gdshader:7` the same. The villager shader is applied to the villager MultiMesh in `game/scripts/town/TownVillagers.cs:122` (`GD.Load<Shader>("res://shaders/villager.gdshader")`). Which materials get stencil through that path was not traced further (not confirmed).
- Hull shaders `cargo_hull.gdshader` and `walker_hull.gdshader` are inverted-hull inks (`ALBEDO = ink.rgb; ALPHA = 1.0`), not stencil writers. Their groups were not traced further.
- `building.gdshader` has no stencil (grep).

### Soft group (generator marks ink="soft")

| Generator | Models | Where marked |
| --- | --- | --- |
| `assets/src/models/tree.py` | tree-oak, tree-willow, tree-poplar, tree-birch, tree-hawthorn (the header lists five) | `tree.py:132` `ink="soft"` |
| `assets/src/models/field.py` | field-sprouting, field-ripe, field-stubble | `field.py:145` `ink="soft"` |
| `assets/src/models/reeds.py` | reeds-small, reeds (the header also names reeds-tall; the code list at lines 60-61 has two) | `reeds.py:65` |
| `assets/src/models/animals.py` | animal-hen, animal-duck, animal-heron, animal-cat, animal-dog, animal-bird (every model in the list) | `animals.py:163` |
| `assets/src/models/dressing.py` | dress-gull only | `dressing.py:74` |
| `assets/src/models/gardens.py` | garden-vegetables only | `gardens.py:59` |
| `assets/src/models/river-town.py` | pasture, sheep | `river-town.py:421` |
| `assets/src/models/town-props.py` | prop-apple-tree | `town-props.py:310-311` |
| `assets/src/models/town.py` | stake-nw, stake-ne, stake-sw, stake-se, cord-n, cord-s, cord-w, cord-e (per part, third tuple element) | `town.py:124-129`; `write_glb` at line 195 has no ink argument |
| `assets/src/models/work-scenes.py` | work-field-bed, work-row-ripe, work-row-green, work-row-shoots, work-row-stubble, work-row-swath, work-row-edge | `work-scenes.py:466-472`, passed at line 497 |
| `assets/src/models/_scene.py` | `hamlet-soft-ink` node, holding the `tree-`, `field-`, `reeds` nodes | `_scene.py:39-44, 103` |
| `assets/src/models/_river_scene.py` | `river-soft-ink` node, same name prefixes | `_river_scene.py:78, 87, 128` |

Stencil shaders: `tree.gdshader` (trees), `villager.gdshader` (villager bodies, through the dyed material).

### Hard group

All other models, with no ink extras: cottage, mill, market-stall, well, desk, drawbridge, footbridge, fence, ferry, barge, rowboat, landing, keeper-hut, ford-stones, and the other town-props, the non-soft work-scenes (`work-sheaf`, `work-stook`, `work-sickle`, `work-scarecrow`, `work-jug`, `work-wet-patch`, `work-bucket`, `work-pail`, `work-sack-grain`, `work-sack-flour`, `work-door-leaf`, `work-doorway`, `work-hoist`, `work-scales`, `work-apron`, `work-crate`, `work-pot`), and the villager models and their cargo files unless they go through the dyed villager material (not confirmed for each file).

## 3. Inner lines in the models

### Helpers in `assets/src/models/_kit.py`

- `beam(m, p0, p1, w, h, colour, up=(0,1,0), ends=True)` (line 15): square-section beam; `w` across the horizontal side, `h` across the vertical side (`_kit.py:16`).
- `loft` (39), `cap` (64), `ngon` (69), `jitter_ngon` (75), `hull2d` (82), `stone` (103), `tube` (113, round rod, `n` sides), `plate` (138, flat plate in Y-Z, `thickness` across X), `hull` (156).
- Mesh methods used for bands are in `assets/src/common/glb.py`: `add_box` (71), `add_frustum`, `add_gable` (104), `add_hip` (127), `add_extrude` (188), `add_poly` (66).

### `assets/src/models/cottage.py`

- Palette: `THATCH, COURSE, RIDGE, PATCH = "Reed and thatch", "Trail earth", "Packed earth", "Packed earth"` (line 20). Courses are coloured Trail earth (the shade tone).
- Course bar: `beam(m, T(-e, y, v), T(e, y, v), 0.2, 0.09, COURSE, up=up, ends=False)`, with `y = h - drop + f * rise + 0.05` (lines 65-71). So each bar is **0.20 m wide, 0.09 m tall**, centred 5 cm above the roof line at fraction `f`, on each slope (`s` = -1 and 1).
- Course fractions (line 62): main range `(0.17, 0.37, 0.57, 0.77)` (4 courses) by default; `(0.22, 0.47, 0.72)` (3 courses) when `courses=3`; any other range `(0.3, 0.62)` (2 courses).
- Hipped main ends (lines 72-79): for `f < 0.5` (so f = 0.17 and 0.37), two more bars per end, on both hipped ends, 0.20 x 0.09 m.
- Patch of older thatch (lines 80-84, `PATCH` colour), and a main-ridge sag (`if b.get("main")`, line 85).
- Corner posts: `add_box(..., (0.24, h - 0.3, 0.24), FRAME)` (line 40-ish), 0.24 m square.
- Bar counts per cottage kind, from the block calls (`cottage.py:138-171`):

| Kind | Main range | Wings (from the block calls) | Bars per roof, size 0 / 1 / 2 |
| --- | --- | --- | --- |
| a (hip-x, patch -0.9) | 4 x 2 slopes + 4 hip-end bars = 12 | once: hip-z, 2 x 2 = 4; twice: hip-z, 4 | 12 / 16 / 20 |
| b (gable-x, patch 1.2) | 4 x 2 = 8 | once: gable-x, 4; twice: gable-z (ext 1.2), 4 | 8 / 12 / 16 |
| c (gable-z, courses=3) | 3 x 2 = 6 | once: gable-z, 4; twice: gable-x, 4 | 6 / 10 / 14 |

Notes: variant a's size guards are at `cottage.py:136-155` (`if size >= 1`, `if size >= 2`). The size guards for b and c were not read (not confirmed); the counts above assume the same guards as a.

The style sheet's target, "Three to five courses on a roof", refers to the roof face, and the code has 4 courses per slope on the main range: the count is per slope, and each slope has the same number.

### `assets/src/models/mill.py`

- `BAND = "Packed earth"` (line 23). Tower tiers: a frustum band per tier, `add_frustum((0, a, 0), ..., 0.22, BAND, ...)` (line 68), so **0.22 m tall**, radius +0.08 m (the number of tiers was not read).
- Corner posts, four: `beam(m, p0, p1, 0.2, 0.2, BAND)` (line 75).
- Lintel: `on_face(m, Y0 + 1.95, 0.2, 1.45, BAND, k=2, proud=0.12)` (line 80). `on_face` default depth 0.08 (line 41).
- Sail ridge band: `beam(..., 0.5, 0.08, "Paper")` (line 116). Sail bars 0.11 x 0.09 (line 153-155).
- The header (line 7) says "weatherboard ... with three paler courses" (not read in code: not confirmed).

### `assets/src/models/market-stall.py`

- Side rails under the cloth: `beam(..., 0.16, 0.14, "Mud")` (line 23). Cloth sag 0.06 m on middle posts (line 30).
- Counter front board edge: `beam(..., 0.08, 0.2, "Trail earth")` (line 50). Counter top: `add_box(... (3.7, 0.16, 1.05) ...)` (line 49).

### `assets/src/models/well.py`

- Coping band: `add_cylinder(..., 1.2, 0.12, "Wet mudflat")` (line 19), 0.12 m tall.
- Roof: `add_gable((0, 2.23, 0), 2.3, 1.2, 0.8, "Reed and thatch", ...)` (line 29). Ridge cap `beam(..., 0.3, 0.16, "Brick")` (line 30).
- Courses: `beam(..., 0.08, 0.05, "Packed earth", up=(0,1,-sz))` (lines 32-34), two per side per the header (line 28, "two courses a side"); the loop's fractions were not read (not confirmed).

### `assets/src/models/field.py`

- No plank frame (header line 4). Inner lines are `strip()` bands; `strip(m, poly, z, w, y, h, colour, ...)` (line 45). Calls: `strip(m, poly, z, 1.0, 0.1, 0.04, ridge)` (line 74); `strip(..., 0.3, 0.14, 0.03, "Meadow", ...)` (line 87); ripe rows `strip(..., 0.8, 0.42, 0.04, "Packed earth", top="Reed and thatch", inset=0.15)` (line 115), comment "combed rows as 0.8 m bands standing 4 cm proud every 1.6 m" (lines 111-112); `strip(..., 0.55, 0.1, 0.04, "Reed and thatch", inset=0.35)` (line 129).
- Stooks: sheaf band `add_cylinder(... 0.46, 0.1 ...)` (line 96).

### `assets/src/models/villager.py`

- Belt: `loft(... centre=(0, 1.06, 0))`, "belt, proud so the ink finds it" (lines 38-39), radius 0.375 to 0.365.
- Pale collar frustum, 0.07 tall, `(0, 1.68, 0), 0.27, 0.21, 0.07` (line 41).
- Hood point: `beam(..., 0.22, 0.16, "Limewash")` (line 68). Brick band on the cap, 0.07 tall (line 81).
- Sack tie, 0.12 x 0.09 (line 99); seam `beam(..., 0.06, 0.03, "Mud")` (line 101); lying-sack band `tube(..., 0.32, band, n=6, r1=0.32)` (line 112).

### `assets/src/models/work-scenes.py`

- Row ridge, `ridge(m, rng, colour, h=0.12, w=0.95)` (line 77), a "low jittered band 2 m long" (lines 77-78); row ridge calls with `h=0.05` (line 176) and `h=0.08` (line 190).
- Crop tufts in two or four lines (`tufts` line 85; `crop` line 105; lines `(-0.22, 0.22)` and `(-0.33, -0.11, 0.11, 0.33)`).
- Stalk beams `0.09 x 0.08` (line 170).
- Door planks and the other items are described in the header (lines 35-36, "planks of different length") but the plank code was not read (not confirmed).

### Summary of inner-line sizes against the style sheet

| Item | Code | Style sheet (section 3) |
| --- | --- | --- |
| Cottage thatch course | 0.20 wide x 0.09 high, 5 cm above the roof line | 4 to 6 cm wide, 1 to 2 cm proud |
| Cottage course count | 2, 3 or 4 per slope (see table) | 3 to 5 courses on a roof |
| Mill tier band | 0.22 tall | (not stated) |
| Field row band (ripe) | 0.8 wide x 0.04 high, 4 cm proud | 4 to 6 cm wide (band width along the slope not stated) |

## 4. Scaling, filtering and anti-aliasing

### `game/project.godot` (whole file read)

- Lines 13-16: `[display]` `window/size/viewport_width=1280`, `window/size/viewport_height=720`. No `stretch`, `aspect` or `scale_mode` keys.
- Lines 22-28: `[importer_defaults]` `texture={"svg/scale": 4.0}` (SVG only).
- Lines 30-32: `[rendering]` `renderer/rendering_method="mobile"`. No `rendering/anti_aliasing/*`, no `rendering/scaling_3d/*`, no `rendering/textures/*` keys. All engine defaults apply.

### Code search for scaling and AA settings

Searched `game/scripts`, `game/tests`, `tools` and `project.godot` for `ContentScale`, `Scaling3D`, `Msaa`, `ScreenSpaceAA`, `Fxaa`, `Taa`, `Stretch`, `SetSize`, `WindowMode`, `content_scale`. Result: no code sets viewport MSAA, screen-space AA, 3D scaling, or a stretch mode. The only content-scale setter is the window's interface scale (below). `WindowMode`, `SetSize` and `Stretch` hits are settings enums, UI, and river models, not viewport settings.

### Settings pipeline

- Defaults (`game/scripts/settings/GameSettings.cs`): `WindowMode = Windowed` (line 88), `Resolution = 1280x720` (line 89), `InterfaceSize = 100` (line 92), `Vsync = true` (line 90), `FrameLimit = 0` (line 91). `ContentScale => InterfaceSize / 100f` (line 101).
- Resolution list (`game/scripts/settings/DisplaySeam.cs:12-15`): 1280x720, 1366x768, 1600x900, 1920x1080, 2560x1440, 3200x1800, 3840x2160. `Resolutions()` (lines 25-35) keeps those not larger than the screen, plus the stored size if it is not in the list.
- `ApplyWindow` (`DisplaySeam.cs:38-51`):
  - `Windowed`: `w.Mode = Window.ModeEnum.Windowed; w.Size = size;` and centred on the screen (`w.Position = ...`).
  - `Borderless`: `w.Mode = Window.ModeEnum.Fullscreen;` (desktop size; the Resolution setting is not used).
  - `Fullscreen`: `w.Mode = Window.ModeEnum.ExclusiveFullscreen;`.
- The resolution dropdown is disabled unless the mode is Windowed (`SettingsPages.SoundDisplay.cs:86`: `Disabled = shownMode != WindowMode.Windowed`; note "Used in windowed mode." at line 90).
- `ApplyContentScale` (`DisplaySeam.cs:60`): `w.ContentScaleFactor = scale;` with `scale = InterfaceSize / 100`. This is the only content-scale code. It is the interface scale (`AGENTS.md:83`: "The interface size is the window's content scale only").
- `SettingsApplier.ApplyAll` (`game/scripts/settings/SettingsApplier.cs:33-42`) and `OnChanged` (lines 48-54) re-apply the window on mode or resolution change.
- `Main.KeepWindow` (`Main.cs:43`): tests only, so the stored window size is not applied.

### Answer: is the picture stretched when the window is larger than the chosen resolution?

- No stretch is configured (`display/window/stretch/mode` not set, so the engine default applies; see section 6). The root viewport's size follows the window. In Windowed mode the window is set to the chosen resolution (`DisplaySeam.cs:44`), so the chosen resolution is the window size. If the window is then made larger by hand (not a game code path), the 3D picture follows the window size and is drawn at the window's pixel size, not stretched (inferred from the settings above; not confirmed by a run).
- `Ink.Update` reads the same visible rect (`Main.cs:289`), so line widths track the window height.
- The Display page's resolution is not a render scale. Interface scale changes only the Window's `ContentScaleFactor`.
- In Borderless and Fullscreen modes the window is the desktop (or exclusive mode) size; the chosen resolution is not used.

## 5. Ground and paper textures and filtering

### `game/shaders/ground.gdshader`

| Line | Sampler | Filter | Role |
| --- | --- | --- | --- |
| 10 | `wear_tex` | `filter_linear, repeat_disable` | wear data |
| 11 | `mask_tex` | `filter_nearest` | ford and crop mask |
| 12 | `river_tex` | `filter_linear` | river centre |
| 13 | `ramp_tex` | `source_color, filter_linear` | wear ramp |
| 14-18 | `g0`..`g4` | `source_color, filter_linear_mipmap, repeat_enable` | grass, trampled, trail, track, hollow way |
| 22 | `paper_tex` | `source_color, filter_linear_mipmap, repeat_enable` | paper grain |
| 23 | `water_tex` | `source_color, filter_linear_mipmap, repeat_enable` | water |

- Tiling: `tile1 = 28.0` m (line 29), `tile2 = 11.3` m (line 30). `two()` (line 75) blends texture at `p/tile1` and a rotated sample at `rot(p, 0.9)/tile2` with mix 0.45.
- `bank_px = 1.6` (line 70): ink bank line width in screen pixels.
- Uses `render_mode ... diffuse_lambert` (line 8) with a custom light.

### `game/shaders/paper.gdshader`

- Line 6: `paper_tex : source_color, filter_linear_mipmap, repeat_enable`.
- Line 13: `texture(paper_tex, wp / 3.1).r * 0.6 + texture(paper_tex, wp / 23.0).r * 0.4` (two scales).

### Import settings (`game/assets/textures/*.png.import`)

Checked `ground-grass.png.import` and `paper-page.png.import` (both identical in the lines read):

- `compress/mode=0` (lossless, line 18)
- `mipmaps/generate=false` (line 26), `mipmaps/limit=-1` (line 27)
- `process/fix_alpha_border=true` (line 34), `detect_3d/compress_to=1` (line 40)

The other ground files (`ground-trail`, `ground-track`, `ground-hollow-way`, `ground-trampled`) exist with `.import` files; their settings were not all read (not confirmed).

Observed: the shaders ask for `filter_linear_mipmap`, but the imports generate no mipmaps. So the mipmap levels are not stored for these textures (from the import file; a runtime check was not done).

### Other texture filters

- Model materials: `TextureFilter = Nearest` (`game/scripts/ModelLib.cs:132`).
- `game/scripts/BookBuilder.Desk.cs:40, 58`: `LinearWithMipmapsAnisotropic`.
- `game/scripts/UiKit.Styles.cs:181`: icons `LinearWithMipmaps`.

## 6. Engine facts (Godot 4.x, from the Godot docs via Context7, `/godotengine/godot-docs`)

Sources: `tutorials/rendering/renderers.rst`, `tutorials/3d/3d_antialiasing.rst`, `tutorials/2d/2d_antialiasing.rst`, `classes/class_projectsettings.rst`, `classes/class_viewport.rst`, `classes/class_renderingserver.rst`, `tutorials/rendering/multiple_resolutions.rst`, and the `class_visualshadernodetextureparameter.rst` enum text (all on github.com/godotengine/godot-docs, branch master).

### Anti-aliasing methods by renderer (renderers.rst, "Antialiasing" table)

| Method | Compatibility | Mobile | Forward+ |
| --- | --- | --- | --- |
| MSAA 3D | supported, no alpha to coverage | supported | supported |
| MSAA 2D | not supported | supported | supported |
| TAA | not supported | not supported | supported |
| FSR2 | not supported | not supported | supported |
| FXAA | not supported | supported | supported |
| SMAA | not supported | supported | supported |
| SSAA | supported | supported | supported |

- `3d_antialiasing.rst` (TAA section): TAA "is only available in the Forward+ renderer, not the Mobile or Compatibility renderers."
- FXAA is "only available in the Forward+ and Mobile renderers"; it "introduces a moderate amount of blur"; enabled with `Rendering > Anti Aliasing > Quality > Screen Space AA` set to `FXAA`.

### `rendering/anti_aliasing/quality/msaa_3d` (class_projectsettings.rst)

- Default `0`. "Sets the number of multisample antialiasing (MSAA) samples to use for 3D rendering (as a power of two)."
- "This has no effect on shader-induced aliasing or texture aliasing."
- Read only at project start; runtime: `Viewport.msaa_3d` or `RenderingServer.viewport_set_msaa_3d()`.

### `rendering/anti_aliasing/quality/screen_space_aa` (class_projectsettings.rst, class_viewport.rst, class_renderingserver.rst)

- Default `0`. Post-process blur of edges: "Screen-space AA methods are typically faster than MSAA and will smooth out specular aliasing, but tend to make scenes appear blurry."
- "The blurriness is partially counteracted by automatically using a negative mipmap LOD bias" (`rendering/textures/default_filters/texture_mipmap_bias`).
- "Only supported in the Forward+ and Mobile rendering methods."
- Value 1 is FXAA (`VIEWPORT_SCREEN_SPACE_AA_FXAA = 1`, class_renderingserver.rst).
- Runtime: `Viewport.screen_space_aa`.

### Does MSAA affect a screen-space shader pass that samples depth?

- Docs say MSAA does not change fragment shader aliasing (`2d_antialiasing.rst`: MSAA "will not affect ... aliasing caused by custom 2D shaders"; `msaa_3d` text: no effect on shader-induced aliasing).
- Whether a `hint_depth_texture` read (the ink pass's `texelFetch`) is affected by 3D MSAA was not found in the docs queried: **not confirmed**.

### `display/window/stretch/mode`, `aspect`, `scale_mode` (multiple_resolutions.rst, class_projectsettings.rst)

- `stretch/mode`: `"disabled"`: no stretching, one unit is one pixel; "Recommended for non-game applications." `"canvas_items"`: base size stretched to cover the screen; "3D is unaffected". `"viewport"`: root viewport set to the base size, then scaled to fit; "Recommended for games that use a pixel art aesthetic." The docs' stated default is `"disabled"` for the property; they say new projects from 4.7 get `canvas_items`, and this project file has no key (not confirmed which applies to this file).
- `stretch/aspect`: `"ignore"`, `"keep"` (letterbox), `"keep_width"`, `"keep_height"`, `"expand"`. Docs: `"expand"` is the 4.7 default for new projects. Has no effect with `stretch/mode` disabled.
- `scale_mode` (the stretch scale): with `disabled`, "Scale 2.0 ... each unit ... corresponds to 2x2 pixels". `canvas_items`: 2D elements are scaled and then multiplied by the scale. `viewport`: the viewport's resolution is divided by the scale. The runtime equivalents are `Window.content_scale_mode` and `Window.content_scale_factor` (multiple_resolutions.rst).
- Effect of `Window.content_scale_factor` (the game's interface size) on 3D under `disabled` was not confirmed.

### Texture mipmap filter (class_visualshadernodetextureparameter.rst)

- `FILTER_LINEAR_MIPMAP`: "blends between the nearest 4 pixels and between the nearest 2 mipmaps ... Use this for non-pixel art textures that may be viewed at a low scale ... as mipmaps are important to smooth out pixels that are smaller than on-screen pixels." So with no mipmaps stored, the mip blend has nothing to blend (consistent with section 5; the runtime result was not checked).

## 7. What the style sheet says about lines, and earlier notes on dark roofs

### `docs/plans/n3/style-sheet.md`, section 3 "Line" (lines 38-42)

- Line 40: "The ink line is drawn by the game (`InkOutline`: a screen pass on depth, 1.5 to 2 px at play zoom). Models are built for it: give every part that should have a line a real edge (a step of at least 6 cm or a crease of at least 30 degrees)."
- Line 41: "Inner lines (planks, thatch courses, timber framing, door boards) are thin bands of the shade tone or of Timber, 4 to 6 cm wide, standing 1 to 2 cm proud so the ink finds them. Three to five courses on a roof, not twenty."
- Line 42: "Nothing is perfectly straight or square: ridges sag 3 to 5 percent, walls lean up to 2 degrees, planks differ in length, posts tilt a little. Use the generator's seed so each model is the same every time it is built."

### Earlier notes on dark roofs and thatch courses

- `docs/review/n3/honest.md:21` and `docs/review/n3/closing.json:53`: "The gable-ended cottage reads dark from above, and every roof is the same thatch yellow."
- `docs/review/n3/manifest.json:3365` ("shows"): "from above its thatch courses stand close together and the roof reads dark, the fault you named for the old extended cottage. Every roof is still the same thatch."
- `docs/review/n3/list.md:147` (item 130): the same point, as a question: "Should the cottages get lighter roofs and a second roof colour?"
- `docs/plans/n3/assets-r2-bk-review.md:29` (pasture): proposed fix "three bands of Thatch shade, 0.08 m" for a shelter's visible courses. The "Thatch shade" name was not found in the palette read here (not confirmed).
- `docs/plans/n3/assets-r1-w-review.md:33`: hawthorn "reads dark, near black" with Cast-iron green; a bush, not a roof.
- `docs/plans/n3/assets-r1-w-progress.md:30-31, 48`: well and bell post "thatch courses", with the note that "the well's course bar sits a little proud of the roof".

### Relevant numbers side by side (for the owner's question about heavy black lines)

- Cottage course bars are 0.20 m wide (`cottage.py:70`), the style sheet asks for 4 to 6 cm. The bars use the Trail earth colour, and the ink is drawn at a depth crease of 0.45 (hard pass), so the bar's edges make the lines.
- The thatch roof reads dark from above in the reviews, which matches the course count per slope (4 on the main range) and the 0.20 m width.

## Things not confirmed

- Play-zoom line width in pixels (1.5 to 2 px in the style sheet; code range 1.0 to 1.8).
- `Distance` in `CameraRig` (near/far plane values).
- Which materials get the villager stencil through `TownVillagers.cs:122`.
- Whether MSAA 3D affects a depth-texture pass (docs did not say).
- Effect of `Window.content_scale_factor` on the 3D picture under stretch mode `disabled`.
- The engine's default stretch mode for this project file (no key in `project.godot`).
- Mipmap presence at run time for the ground textures (import says none).
- Number of tiers on the mill, the loop fractions in `well.py`, the size guards for cottage kinds b and c, and the plank code in `work-scenes.py` (door leaf).

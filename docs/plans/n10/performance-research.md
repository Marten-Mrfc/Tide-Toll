# Performance research (n10): "the app is slowing down a bit"

Research only; written from reading the code and the existing logs. Nothing was run or timed. Godot 4.7-stable, Mobile renderer, C#.
Line numbers are from the tree at the time of writing (`/home/user/tide-toll`, main checkout).

## 0. The short answer

- The CPU side is small and measured: `game-perf` says 0.76 ms a frame on average (town, 28 villagers) and 0.62 ms (river), worst frame 5.9 ms, against a 16.7 ms budget at 60 fps. Even if a laptop CPU is three times slower than this machine, the game's scripts use a tenth of a frame. **Unless the owner's PC is very weak, "slowing a bit" is a graphics (GPU) cost**, which nobody has ever measured. That includes the drawing the headless check cannot see (the engine's own culling, shadow pass, uploads, the draw submission itself).
- The biggest unmeasured GPU cost, by reading, is **not the ink pass the brief names but the ground shader** (`game/shaders/ground.gdshader`): one plane that covers most of the screen, about 55 to 60 texture reads and about 14 value-noise calls (4 hashes each) for every pixel, nothing skipped on plain grass. The ink pass is second (9 to 18 depth reads a pixel plus a screen copy and a depth copy). Both scale with the pixel count, so the same game that is fine at 1280 x 720 can be slow at 1440p or 4K or on an integrated GPU.
- The settings page offers vsync, a frame cap (30, 60, 120, none), window mode and size, interface size. It offers **no render-scale, no outline, no shadow and no quality setting**, and the window defaults to 1280 x 720 windowed (`GameSettings.cs:88-95`). If the owner plays fullscreen at 1440p or 4K, they pay 4x to 9x the 720p pixel cost with no way to turn it down.
- Cheapest first step: ask four questions (section 5c), ship a one-key readout (section 5b), and run the toggle matrix under xvfb (section 5a), which on a software rasteriser is pixel-bound and so gives a *relative* ranking of ground, ink, shadows and transparents (the absolute numbers mean nothing).

## 1. Map of what costs, frame by frame

Legend: C = constant, G = grows with the town, B = burst. Frame cost in ms is only given where the existing log measures it; the rest is my reading ("small" = a few dozen to a few hundred operations).

### 1.1 Simulation

| What | When | Cost shape | Class |
| --- | --- | --- | --- |
| `TownDriver._Process` (`TownDriver.cs:94`) steps `Town.Step()` from an accumulator at `TickHz = 10` (`TownRules.cs:12`), up to 200 steps a frame | each frame, 0 to 1 step at 1x (10 ticks a second), up to 8 or more at fast speeds | `Step` = `StepVillager` for each villager (O(V)), `StepRequests`, `StepHands`, `StepRiver` | G, linear in villagers |
| Once a second (`S.Tick % TickHz == 0`, `Town.cs:83`): `CheckAchievements`, `StepTouches`, `StepCrowding` | every 10th tick | small loops over achievements, touches, roads | C / G (roads) |
| Job choice, `ChooseJob` (`TownJobs.cs:192`): for each open workplace, `ExpectedWait` walks all villagers (`TownJobs.cs:172-189`); river `SiteWait` the same (`TownRiverJobs.cs:113`) | only when a villager picks a job (a villager every ~20 to 60 s) | O(workplaces x villagers), about 15 x 40 = 600 steps a choice; at most a few choices a second | G (quadratic in size only at the choice moment). **Not a suspect**: even 10 choices a second is ~6,000 loop steps, microseconds. The worry "villagers squared" is real in the O(V x W) sense but has no per-frame cost |
| Bot run to the charter in the test: 385 ms CPU for 18,735 ticks = **0.02 ms a tick** (`game-perf.log`) | | | so the simulation is about 0.2 ms of every real second at 1x |

Not measured: ticks at 40 and 100 villagers on the second age (the toolkit has `ticks --preset river --walkers 500`, section 5a).

### 1.2 `_Process` nodes (order of the scene; all run every frame at the display's frame rate)

| Node (file:line) | Each frame it does | Class |
| --- | --- | --- |
| `Main._Process` (`Main.cs:284`) | every 0.25 s `Follow()` (`TownExtent`: loops all plots and every point of every open road) while the camera follows the town; checks viewport size; `Ink.Update` (touches the shader only if zoom or height changed: good); sets `Villagers.WalkerScale` | C, plus a burst of O(plots + road points) at 4 Hz |
| `TownDriver` (above) | steps; drains events into a list | C |
| `CameraRig._Process` (`CameraRig.cs:362`) | eases size and pivot; `ApplyPitch` only on change; pans on keys | C |
| `TownGround._Process` (`TownGround.cs:206`) | sets `time_s` uniform; `UpdateFord` (loops river links, early-out); per road reveal; **`Upload()` ten times a second** (`UploadsPerSecond = 10`) or every frame while a road or lane is revealing: clears and re-fills a 128 x 128 wear (Rg8) and crowd (R8) image from every cell of every open road, runs the ease over 16,384 cells, `SetData` + `ImageTexture.Update` of two textures (about 48 KB). Paving changes (`UploadPaving`) upload two 256 x 256 R8 textures (only when a surface changes) | 10 Hz burst, grows with road cells; reveal frames every frame for about the reveal time |
| `TownBuildings._Process` (`TownBuildings.cs:331`) | loops event list; `Sync` only on level or building event; pop animation for plots popping; bell swing, charter stone | C; B on a level |
| `TownVillagers._Process` (`TownVillagers.cs:173`) | 3 passes over n villagers (n <= 40): follow/stand/line (`StandTarget`, `Transitions`), `Spread` (neighbour grid, `TownVillagers.Spread.cs:44`, 4 sub-passes: road order by insertion sort, targets, push over 3 x 3 grid cells, test measures over 5 x 5 cells), then `Pose`, `SetInstanceTransform` on **bodies, one head MultiMesh, shadows, up to 5 load MultiMeshes, frames, carts, tools** (about 13 MultiMeshes, each instance written with a managed call), `DrawProps`, `RingFrame` (hover ring and selected ring, material colour set while shown) | G, linear in n with a grid (no n squared left). `Array.Clear` of `_gridHead` (4,096 ints) and several `Array.Fill` each frame; the "measures for the tests" pass (step 4, lines ~160-185) runs in the shipped game too (a 5 x 5 neighbour search for every standing villager) and feeds only tests: a free quick win |
| `TownVillagers.Pick` via `TownHud.PeopleFrame` (`TownHud.People.cs:127`) | every frame: for every villager two `LocalToScreen` projections and a segment distance (`TownVillagers.Pick.cs:48`); only when the pointer is on the map | G, linear (40 x 2 projections: small) |
| `TownTouches._Process` (`TownTouches.cs:58`) | spawns a node per new touch; pops | B on a touch |
| `TownLife._Process` (`TownLife.cs:249`) | for each animal (hens, ducks, heron, gulls, cat, dog, gliders, flocks; about 15 to 30) sets rotation or position; `ProcessGardens` | C (each is a managed `Node3D` property write: about 2 interop calls an animal) |
| `WorkPlaces._Process` (`WorkPlaces.cs:95`) | `Well/Fields/Mill/Stall/Doors.Step(Dt)` (props at work: pails, rows, sacks) | G (with workplaces), small |
| `WorkPuffs._Process` (`WorkPuffs.cs:65`) | loops live puffs and splash rings, writes MultiMesh transform and colour per puff, sets both visible counts | G with activity (cap `Cap`) |
| `TownFeel._Process` (`TownFeel.cs:136`) | spawn budget 40 a frame, loop of at most 200 pieces (coins, rings, grit), 5 MultiMeshes | B around deliveries and levels |
| `RiverLayer._Process` (`RiverLayer.cs:357`) | returns at once in the first age; in the second: loops river plots and links each frame (`RiverLayer.cs:371-405`), `Pop`, raft and drawbridge | G in the second age, small |
| `RiverBoats._Process` (`RiverBoats.cs:186`) | for each boat: position, rotation, 4 `Visible` writes, label position, `BoatCargoText`, a `HashCode.Combine` signature (text only on change) | G with boats |
| `MillSails._Process` | each sail node | C |
| `TownHud._Process` (`TownHud.cs:148`) | runs 21 frame functions; see 1.3 | C + G |
| `TownSummary._Process` (`TownSummary.cs:85`) | every 0.5 s, **only while zoomed out to the book** (`Watched`): rebuilds the left page's `Label3D` texts (text only on change) | burst at 2 Hz, only in the book view |
| `TownAudio`, `RiverAudio`, `TownMusic` (`_Process`) | event cues; footsteps; mill and river loops by distance; music clock | C, small |
| `TownSaver._Process` (`TownSaver.cs:56`) | clock; **`SaveNow` every 2 minutes** (`AutosaveMinutes = 2`): `TownSave.Save` + `JsonSerializer.Serialize` + file write + rename, synchronous on the main thread, about 250 KB allocated | B every 2 min and on pause and quit |
| `PauseMenu._Process`, `BookCover._Process`, `SettingsPages._Process` | idle unless a motion runs | C (tiny) |
| Engine-side per frame (not visible to the check): `CpuParticles3D` x one per cottage smoke (`TownBuildings.Cottages.cs:105`, 5 puffs, `FixedFps = 10`), `TreeSway` vertex shader | | G with cottages |

### 1.3 `TownHud` frame functions (`TownHud.cs:148-170`), every frame

`StageFrame`, `Refresh` (ledger: text only when the number changes), `StepFloaters`, `ShopFrame` (**calls `Town.ShopItems` every frame**: builds about 8 to 14 `ShopItem` structs, each with `EstimateWorth` (which reads `CoinPerMinute`, a 60-slot ring sum) and `SuggestedPave` (loops all roads); then `Grouped`, a `HashCode` signature per row including two strings; rebuilds a row's text only if the signature changed; `TownHud.Shop.cs:227`), `PaveFrame` (returns early when neither Pave nor Extend mode is on, `TownHud.Pave.cs:228`; in Pave mode `AnyPavable` loops all roads each frame), `PeopleFrame` (pick + tag), `ModeFadeFrame`, `RequestFrame`, `HintFrame`, `TimersFrame` (hash signature), `BookFrame` (**counts every achievement every frame**: `TownSummary.Achieved(t)` / `RiverAchieved(t)` at `TownHud.Book.cs:281`, each reads the value of 16 to 30 achievements), `RiverFrame`, `ChoiceFrame`, `SecondRequestFrame`, `ZoneFrame` (sets positions of the ledger, bell, choice, tide), `MessagesFrame`, `LabelsFrame`.

`LabelsFrame` (`TownHud.Labels.cs:51`): every frame it gets the global rect of up to 14 cards (`GetGlobalRect`, `IsVisibleInTree`, `Modulate` each), then for each map tag does `IsInsideTree`, `GetParent`, `IsVisibleInTree`, `IsPositionBehind`, `UnprojectPosition`, a rect test against every card and every kept tag (tags x (cards + tags)), `Pill.Position`, and `if (l.Visible) l.Visible = false`. **Every 0.5 s `ScanTags`** (line 105) calls `FindChildren("W*", "Label3D", true, false)` on **three subtrees: `Boats`, `Buildings`, `River`**, i.e. walks every descendant node (all cottages' parts, stakes, props, boats' cargo) and returns a Godot array whose items are each wrapped in a managed object. This is the half-second beat in the brief: it grows with the number of nodes under those three owners (hundreds in the second age).

Everything else the HUD does is "text only on change" (signature hashes), which the allocation gates confirm: 542 to 1,216 bytes a frame.

### 1.4 What grows with the town and the second age

| Thing | Grows with | Effect |
| --- | --- | --- |
| Villagers (to about 40) | purchases | linear in CPU (above); also 1 body + 1 head + 1 shadow instance each, plus loads |
| Cottages (and extensions), fields, mill, well, market, stakes | levels | each is a separate `MeshInstance3D` (a house is 2: `house` + `accent`), casts shadows, and a cottage owns a `CpuParticles3D` (its own transparent draw); `ScanTags` walks them |
| Roads and wear | roads, time | `TownGround.Upload` loops all road cells at 10 Hz; every pixel's ground shader cost is the same whatever the wear (it never gets cheaper or dearer) |
| Touches (`prop-*.glb` as nodes) | time | one `MeshInstance3D` each, popping in |
| Boats (second age) | arrivals | each boat has hull, cargo meshes, `Label3D`, wake mesh (transparent) |
| Map tags | plots, boats | each is a `Label` with a style box in the interface, laid out each frame |
| Messages | events | queue is small, text on change |
| Second age | | `RiverLayer` per-frame loops, tide, reeds (205 `MultiMesh` instances, `RiverBanks`), lanes drawn into the ground, the ground shader's river/tide/mud branches switched on by uniforms (cheap uniform branches, no extra cost when off) |

Classification: nothing in the scripts grows faster than linearly in villagers or roads with a small constant; the only "growth that could be felt" is on the GPU side (more shadow casters and draw calls) and in the half-second tag walk.

## 2. The drawing

### 2.1 Settings and project defaults

- `game/project.godot` sets only the Mobile renderer, a 1280 x 720 design size and `config/features`. **Every rendering setting is the engine default**: no MSAA, no FXAA, no TAA, `scaling_3d` mode bilinear at scale 1.0, directional shadow atlas 4096, soft shadow filter "medium", default vsync. No `stretch/mode` is set (interface size is the window's content scale factor, `DisplaySeam.ApplyContentScale`).
- Settings (`GameSettings.cs`): window mode (windowed, borderless, exclusive fullscreen), resolution list 1280 x 720 up to the screen size (`DisplaySeam.CommonSizes`, up to 3840 x 2160), vsync (default on), frame limit {30, 60, 120, none} (default none, applied as `Engine.MaxFps`), interface size. **No quality or render-scale entry.** Because the game renders one 3D view at the window's pixel size, "resolution" is the only lever, and the interface scale does not reduce 3D cost.
- Anti-aliasing: none at all. The ink line is the edge treatment. So there is no MSAA cost to remove; offering 3D render scale below 1.0 would soften the one-pixel ink line (a look risk).

### 2.2 Counts from the code (where I could not count, I say so)

One `Camera3D` (orthographic, near/far 200..400 m, `CameraRig`), one `DirectionalLight3D` with shadows (`Main.cs:308`), **no other lights** (grep: no Omni/Spot), one `WorldEnvironment` (colour background, ambient colour, linear tonemap, no glow, no SSAO, no fog), **no SubViewport anywhere** (grep for `SubViewport` and `ViewportTexture`: none in `game/scripts`). The book, its pages and the settings spread are ordinary 3D meshes in the one view; their text is `Label3D` or 2D controls over the view. So "viewports within viewports" is nil: good.

Estimated draw calls in the main pass at the end of the first age (my count from the scene build; **not measured**, the real figure needs the rendering server's counter, section 5a):

| Group | Draws | Notes |
| --- | --- | --- |
| Ground plane | 1 | the heavy shader, section 2.3 |
| Book: pages, stack, cover, spine, desk props (inkwell, candle, seal, ribbon) | 10 to 20 | `BookBuilder*.cs`; `PaperMaterial` |
| Buildings | 25 to 45 | cottages are 2 draws (house + accent); mill, well, market stall, stakes (staked next plots), bell frame, charter stone, plus fences, props |
| Trees | 5 MultiMeshes, up to 260 instances, 184 to 284 triangles each (about 70,000 triangles) | `TownTrees.cs`; custom sway shader; cast shadows |
| Villagers | about 13 MultiMeshes (bodies, 3 head kinds, shadow discs, 5 load kinds, pack frames, carts, sickles, scythes) | capacity 200 instances each, only `n` visible; the shadow discs are alpha-blended |
| Touches, gardens, animals | 20 to 40 | each its own mesh node |
| Work props (pails, rows, stooks, sacks) | 10 to 20 | `FieldRows` MultiMeshes; `WellWorks` etc. |
| Smoke | one `CpuParticles3D` per cottage (about 8 to 16): each its own transparent draw | alpha, unshaded |
| Fx | `WorkPuffs` 2, `TownFeel` 5 MultiMeshes (only when drawn), hover and selected ring (visible rarely) | transparent |
| Ink pass | 2 full-screen quads | section 2.3 |
| Second age: river plots (weaver, pasture, quay), crane, boats (hull + cargo + wake), lanes (alpha meshes), reeds MultiMeshes, drawbridge, ferry | +40 to 80 | boats and wakes are transparent |

So I expect roughly **100 to 200 draws in the colour pass and a similar number again in the shadow pass** (every shadow caster, including every tree MultiMesh and every villager MultiMesh, is drawn again into the 4096 atlas each frame; Godot does not cache the directional shadow). That is fine for a desktop GPU or a modern integrated one; it is a modest cost on a weak laptop, and mostly a CPU-side submission cost inside the engine (not in the 0.76 ms the check sees).

Transparent layers (sorted back to front, drawn after the screen copy): villager shadow discs, smoke puffs, wakes, lanes, rings, `WorkPuffs`, `TownFeel`, the **two ink quads** (blend_mix, over everything). Overdraw of these is small except the ink quads, which touch every pixel.

Particles: only `CpuParticles3D` (one per cottage) plus the hand-made MultiMesh puffs; no GPU particles (so no compute or particle-shader compile).

### 2.3 Full-screen and large-area passes, cost per pixel

**Ground (`game/shaders/ground.gdshader`, 364 lines).** One plane (256 m page) filling most of the view; Lambert with a custom `light()` (shadow attenuation read per pixel). Per pixel, with every optional layer off (the normal case in the first age), I count from the code:

- `wear_tex` reads: 1 + (1 core) + 6 rings x 4 (`core`, `wn`, `spread`, `broad`) + 1 = **27 reads** of a 128 x 128 Rg8 texture (cache-friendly; they run on every pixel even on plain grass far from any road).
- tiled colour textures, each `two()` = 2 mipmapped reads: grass, flattened, earth, packed, hollow way, bed (x2), bank lip = **about 16 to 18**; plus ramp 1, mask 1 + 4 (field edge), crop 1, river row 1, water 1, paper 2 = **about 10 more**. Gravel, cobble, crowd, season, wet mud add 1 to 3 each when their uniforms are on (uniform branches: skipped when off).
- value noise: `warp` 4 calls, `stroke` 1, `n1 n2 n3` 3, river swirls 2, bank lip 3 to 4, plus several `hash12` for stones and shimmer: **about 14 `vnoise` = 56 `hash12`** plus several `sin`/`cos`/`fract` (the ring loop calls `cos`, `sin` 6 times).
- Result: **about 55 to 60 texture reads and a few hundred ALU operations a pixel**, for every pixel of the page, including pixels that end up plain grass and pixels that a building covers (the Mobile renderer has no depth pre-pass; with front-to-back sorting the buildings usually draw first and the ground is early-Z rejected under them, but the ground plane is one huge object and its sort position among the opaque objects is not something I could verify).
- Cost scale: 55 reads x 2.07 M pixels (1080p) = **about 115 M reads a frame**, 7 G reads a second at 60 fps. At 1440p (3.69 M) about 205 M a frame; at 4K (8.29 M) about **460 M a frame (27 G/s at 60 fps)**. A discrete GPU of the last five years takes this; an Intel UHD/Iris or AMD Vega integrated part has roughly 10 to 30 G reads a second of bilinear throughput in total, shared with the other passes and with the CPU's memory.
- Confidence that this is the largest single pass: medium-high (it is the only shader that is both full-screen and long). It has never been timed.

**Ink (`outline_screen.gdshader`, `InkOutline.cs`).** Two `MeshInstance3D` quads (`InkHard`, `InkSoft`), both full-screen, both `hint_depth_texture` and `hint_screen_texture` readers, distinguished by a stencil test (`compare_not_equal` / `compare_equal` against 1). Taps a pixel (from the shader's own comment and my reading of `fragment()`):

| Window | `width_px` (at the view's zoom) | depth reads | + colour read | pixels | reads a frame, one quad's worth per pixel |
| --- | --- | --- | --- | --- | --- |
| 1080p | `LinePx` 1.0 to 1.6 x 1 = 1.0 to 1.6 | 1 + 4 + 4 (step 2, only on non-silhouette pixels with a fraction) = 9 | 1 | 2.07 M | about 21 M |
| 1440p | 1.33 to 2.1 | 9 to 13 | 1 | 3.69 M | about 37 to 52 M |
| 4K (2160p) | 2.0 to 3.2 | 13 to 17 | 1 | 8.29 M | about 116 to 150 M |

Each depth read also does `inv_proj * vec4` (a mat4 multiply and a divide) in `vz()`, so each tap is about 20 ALU operations as well as a fetch: at 4K that is on the order of 2 to 3 GFLOP a frame. Two quads: if the hardware does the stencil test before the fragment shader (early stencil; normal when the shader writes neither depth nor discards, which this one does not), each pixel is shaded once in total (the quads split the pixels); if not, **twice** (a doubling I could not verify). In addition: reading the screen and depth textures in the Mobile renderer makes the engine **copy the colour buffer after the opaque pass and the depth buffer too** (the documentation says the screen is copied after the opaque geometry pass, before the transparent pass, `tutorials/shaders/screen-reading_shaders`; the depth copy is my reading of how `hint_depth_texture` works in the Mobile backend: not verified). Those copies are about 8 MB (1080p RGBA8) to 16 MB (RGBA16F) plus 8 MB (1080p) per frame of read and write bandwidth, 4x at 4K (a significant part of an integrated GPU's shared memory bandwidth).

Ink per frame is thus about a fifth to a half of the ground's fetch count at the same size, plus the copies. It is not likely the whole story; it is likely a third of it. It scales with the pixel count and (via `width_px >= 2`) with the window height: the step from 1080p to 1440p is a 1.8x jump in pixels and in taps per pixel at once (about 2.5x ink cost).

**Shadows.** One directional light, `Orthogonal` (single split), `DirectionalShadowMaxDistance = 700` (the camera far plane is 400, the page 256 m: the shadow map's resolution is spread over more depth range than needed, a quality not a speed question), `ShadowBlur 1.5`, atlas default 4096, soft filter medium (the engine default). Each frame renders every caster (trees about 70 k triangles in 5 draws, 25 to 45 buildings, villager and load MultiMeshes (the villager MultiMeshes do cast: `ModelLib.Multi` does not turn shadows off; only the shadow-disc MultiMesh does), props) into a 4096 x 4096 depth map, then every lit pixel (the ground shader's custom `light()` reads `ATTENUATION`; each building pixel too) takes a filtered shadow lookup (more taps with "soft medium" plus `ShadowBlur`). Unmeasured, probably the third GPU suspect.

**Other**: the book's paper material (`paper.gdshader`, 19 lines): cheap. `tree.gdshader`/`TreeSway`: vertex-only extras. `rain.gdshader` (40 lines): not found referenced by the game scripts (`grep` shows no `.cs` use), so no rain cost I could find. `river_sign.gdshader`, `cargo`, `wake`, `walker` etc.: small.

### 2.4 Shader and pipeline compilation at first sight

Godot 4.7 (documentation, `tutorials/performance/pipeline_compilations` and `rendering/jitter_stutter`, read through Context7): since 4.4 the Forward+ and Mobile renderers use **ubershaders** and precompile pipelines at load time (mesh loading and node-add "Surface" step); only "Draw"-category compiles (a case the engine did not anticipate) cause stutter in play; "Specialization" compiles happen in the background and cannot stutter. The page also says that material overrides on nodes are *not* precompiled at mesh load (only on node add), that enabling features at run time (MSAA, VoxelGI) recompiles, and that **2D canvas items are not precompiled**.

What this game does that touches those rules:

- Buildings, props and touches are instanced when they first appear (`TownBuildings.Sync`, `TownTouches`, `RiverLayer.MakePlot`, `RiverBoats.Build`), with `MaterialOverride` (`AccentMaterial(...)` new `StandardMaterial3D` per accent colour, `PaleStakes`, `PaleStone`, `TreeSway`, the ink `ShaderMaterial` built from a patched copy of the shader). The first appearance of each *combination* on a machine with a cold pipeline cache can hitch in the node-add frame; the same combination later is cached (the Vulkan pipeline cache lives in the user data folder). The patched ink shaders (two `new Shader { Code = ... }` instances) are compiled when the camera is built: a start-up cost, not a play-time one.
- The first look at a *new kind* (a hat, a boat, the quay, the drawbridge, reed beds, the `wake`, `cargo` shaders) is the case to expect: the shader is compiled when the first node using it enters the tree, not when the town starts. Under the new ubershader system this should be a small, one-off hitch (no more than a few frames on a driver without cached pipelines), most noticeable on the *first* run on a machine (clean cache) and after driver updates.
- 2D: the interface's controls, `Label`s with new font sizes, style boxes: canvas pipelines compile at first draw (no precompilation); the settings spread builds 22 MB at start-up (`game-perf.log`: "Settings 22554816"), mostly at start. These are one-time.
- I could not verify what the Windows export actually does at first run (shader cache size, whether the first minutes are the stutter). A "shader warm-up behind the closed cover" (instance one of every model and material for a frame before the first view) is the standard remedy, and the book's cover is a natural curtain (`PauseMenu` already has `WarmUp` at `PauseMenu.cs:247`: read what it covers first).

## 3. The suspects, ranked

Confidence is my estimate that this is a *real, felt* part of "slowing down a bit" for an ordinary owner PC.

**1. The ground shader's per-pixel work (about 55 reads, about 56 hashes a pixel), whole page, every frame.** Confidence 45 percent that it is the largest GPU cost, 25 percent that it alone explains the report. Evidence: `ground.gdshader:~95-300` (27 `wear_tex` reads in a 6-ring loop with `sin/cos`, 5 two-sample material blends, noise, river, bank lip, shimmer all evaluated for every pixel). Feels like: a steady low frame rate, the same everywhere, worse with a bigger window or fullscreen, no stutters; zoomed out (more ground in view, all of it shaded) it gets *no better*. Remedy: (a) an hour: add a one-texel "near wear" mask (a dilated max of the wear grid, computed in `TownGround.Upload` next to the existing arrays: the ring reads reach `2.2 cells + warp` from a pixel, so dilate by 4 cells) and skip the 24 ring reads, the `broad` and `core` mixing and `stroke` where it is zero (most of the page is plain grass, far from any road): saves about 25 of the 27 `wear_tex` reads and the ring's trig on most pixels with no change to the look (the look in "near" pixels is the same code); (b) half a day: move the river, bank lip, shimmer and mud blocks behind a cheap `inside_or_near_river` test (the river's x per row is already known: `abs(lp.x - cx) < half_w + 14` otherwise skip): saves about 10 reads and 6 noises on most pixels, same look; (c) a day, higher risk: bake the ground into a texture when the wear changes (cheapest of all at run time, but a 256 m page at the needed detail is a large texture, and the look at high zoom would change). Do (a) and (b) first; both are branch-coherent across large screen areas so the GPU really skips the work.

**2. Resolution multiplier: no render-scale or quality setting, and the ink step at 1440p and above.** Confidence 40 percent that the owner is at 1440p, 4K or a high-DPI laptop panel and that this is what turns a fine game into "a bit slow". Evidence: `DisplaySeam.CommonSizes` offers up to 3840 x 2160; no 3D scale; `InkOutline.Update` raises taps from 9 to 13 at 1440p and above (n8's own note, `docs/plans/n8/ink-progress.md`: "needs a look on a real graphics card"). Feels like: steady low frame rate that scales with window size; a smooth game in a small window and a heavy one fullscreen. Remedy: a settings entry "Drawing detail" (3D render scale 100/85/70 percent through `Viewport.Scaling3D*`, ~2 hours; risk to the look: the ink line and the paper detail soften, so offer it only as a choice, never default), and an "Outline: full / light" option which sets `max_step` so the outer step never exceeds 1 (taps 5 instead of 9 to 17; ~1 hour; risk: the outline weight no longer grows with the window, i.e. it gets relatively thinner at high resolution: that is the old, accepted look).

**3. The ink pass as such (two full-screen quads, depth + colour copies, 9 to 17 depth reads with a mat4 unproject each).** Confidence 30 percent. Evidence: `outline_screen.gdshader:48-70`, `InkOutline.cs`. Feels like the same steady low rate; costs more with large windows, and may be worse on integrated GPUs because of the bandwidth of the copies. Remedies: (a) compute view depth from the depth value with two scalars instead of a full `inv_proj * vec4` per tap (the camera is orthographic: view z is linear in depth, `z = near + d * (far - near)`; a half hour, same look, saves about 20 ALU a tap, valid only if the pass stays orthographic: it does, `CameraRig`); (b) `texelFetch` four depth taps as one `textureGather` (needs a pair of formats; half a day; risk low); (c) skip the pass by a stencil-or-depth early-out where all neighbours are the ground (1 + 4 reads then): the ground plane has a known depth; test `abs(zc - ground_z) < eps && d2 flat`: risky, a day. And verify whether early stencil really splits the two quads (section 5a, toggle `InkSoft` alone).

**4. Shadow pass: 4096 atlas, 700 m range, everything casts, soft filter.** Confidence 30 percent as a GPU cost, 15 percent as the main one. Evidence: `Main.cs:308-317`, `ModelLib.Multi` (no `CastShadow = Off`), `TownTrees` (260 trees, 70 k triangles into the map every frame). Feels like: a small constant tax, rising with the number of buildings and trees (the trees are constant: placed at the start). Remedies: `DirectionalShadowMaxDistance` from 700 to about 350 (the camera's far plane is 400 and the page is 256 m: sharper shadows and the same cost, an hour including a look at the pictures); trees cast no shadow when the view is wide (`TownTrees`, the 5 MultiMeshes: `CastShadow = Off` above a zoom size, or always: trees' shadows on the grass are part of the look, ask); smaller atlas (2048) as a "drawing detail" step, a visible softening; load and head MultiMeshes off the shadow pass (small win, tiny look change). Risk to the look: low to medium (shadows are in the book's picture). Measure first (section 5a).

**5. A hitch every half second or two minutes: tag scan, autosave, first-sight compiles.** Confidence 35 percent that the owner's "slowing" is partly hitches. Evidence: `ScanTags` `FindChildren` over three whole subtrees every 0.5 s (`TownHud.Labels.cs:113`), whose size grows with the town (buildings, boats, river parts); autosave every 2 minutes writes the whole save on the main thread (`TownSaver.cs:100`, about 250 KB allocated, one frame: expect a 5 to 30 ms hitch on a laptop SSD, longer on a spinning or a busy disc); new buildings, hats and boats compile their pipelines at first sight (section 2.4); the 35 to 54 KB interface spikes in `docs/incremental-log.md` are too small to matter (less than 0.1 ms of garbage collection). Feels like: regular small stutters at the half-second beat (barely felt) and a clear hitch every two minutes and at each level or new building; "it was smooth, then it gets worse as the town grows" (grows because `FindChildren` walks more nodes). Remedies: replace the scan with an event list (the owners register and unregister their `Label3D` when they make or free them: 1 to 2 hours, no look change, and `Hud` already gets `TagsChanged()`); move `JsonSerializer.Serialize` + write to a worker thread with a snapshot taken on the main thread (half a day; risk: the state copy must be taken on the main thread; or simply serialise to a `byte[]` there and write it elsewhere: 2 hours); warm-up of materials behind the cover (half a day; risk: none to the look).

**6. CPU in the scripts (the 0.76 ms).** Confidence 5 percent as the cause on a normal PC; 15 percent on a very weak laptop with the power saver. Evidence: `game-perf.log`; `TownVillagers._Process` (4 passes, 13 MultiMeshes of managed `SetInstanceTransform` calls), `ShopFrame` building the shop list every frame, `BookFrame` counting achievements every frame, `LabelsFrame` rect tests, `Pick` projecting every villager every frame. Together a few tenths of a millisecond; each worth doing as a hygiene fix (section 6), none worth doing for performance alone.

**7. Draw-call and node count (100 to 200 draws, +100 to 200 for shadows, about 15 transparent smoke emitters).** Confidence 10 percent on a desktop PC, 20 percent on an old laptop. Remedy: one `MultiMesh` for all smoke instead of a `CpuParticles3D` a cottage (half a day, small look risk: the `Preprocess` timing and curve); merge the 2 draws of a cottage (house + accent) is not possible while the accent needs its own material.

**8. Memory and start-up**: the settings pages build 22 MB (`Settings 22554816` in the log) at start-up, a one-off (a slow start, not a slow game); the .NET garbage collector sees the rest as small. Confidence 10 percent that the owner means this ("the app is slow" can mean "it started slowly"): ask.

Order of work if no measurements are possible: 5 (cheap hygiene), 1a, 2 (Outline light + Drawing detail) as options, then 4.

## 4. How things would feel, in one line each

- Steady low frame rate that scales with window size: suspects 1, 2, 3 (GPU, pixel-bound).
- Regular tiny hitch about twice a second: `ScanTags` (5, small).
- A clear hitch every two minutes: autosave (5).
- A hitch the first time something new appears, only the first time on a machine: pipeline compile (5).
- Gets slower as the town grows with no change in window size: shadows and draw count (4, 7), `ScanTags` (5); not the simulation.
- Slow only when zoomed out: not predicted by any suspect (the ground is shaded the same either way; the book view adds `TownSummary` text rebuilds at 2 Hz). If the owner says so, look at the interface (`Label3D` rebuilds, the tag layer) and at the left page.
- Slow only with the book open or paused: the cover's lettering and the settings spread (2D over 3D) are the changed parts; `PauseMenu.Layout` runs each frame while the cover is visible.

## 5. The measurements

### 5a. What can be measured here, with no graphics card (xvfb, software Vulkan)

Under a software rasteriser the **counts** are exact (draws, objects, primitives come from the rendering server, not the GPU) and the **times are pixel-bound on the CPU**, so the *ratio* between toggles is a crude but useful ranking of per-pixel cost (ground against ink against shadow); the absolute numbers mean nothing. Run on a quiet machine (the heavy-run lock), one scene `perf-draw.tscn` (new, in `game/tests`, a copy of `screenshot.tscn`'s setup), 1920 x 1080, the end-of-age bot town and the river town, 300 frames after 60 warm-up frames, report the mean and the 95th percentile of the frame in ms and these counters each variant:

```
RenderingServer.GetRenderingInfo(RenderingInfo.TotalDrawCallsInFrame)
RenderingServer.GetRenderingInfo(RenderingInfo.TotalObjectsInFrame)
RenderingServer.GetRenderingInfo(RenderingInfo.TotalPrimitivesInFrame)
Performance.GetMonitor(Performance.Monitor.TimeProcess), RenderVideoMemUsed, ObjectNodeCount
```

Variants (each a one-line toggle; the node paths are in `Main.cs`): (1) baseline; (2) `Main.Ink.InkVisible = false` (both quads); (3) only `InkSoft` hidden (tells whether the stencil splits the work); (4) the sun's `ShadowEnabled = false`; (5) trees hidden (`_trees.Visible = false`); (6) villagers `Visible = false`; (7) the ground `MaterialOverride` set to a flat `StandardMaterial3D` (the ground shader's whole cost); (8) the same view zoomed out (`Rig.LookAt` at `MaxSize`) and at the closest zoom; (9) window 1280 x 720, 1920 x 1080, 2560 x 1440 (`--resolution`): the scaling of time with pixels, which says "pixel-bound" or "not"; (10) `Viewport.Scaling3D = 0.5`; (11) the river town (second age) against the first-age town; (12) `Main.Villagers` and `Hud` `ProcessMode = Disabled` one at a time (CPU share of each outside the check). The ratios tell which of ground, ink and shadow dominates; the draws and primitives say whether the count of objects is a concern at all (I expect fewer than 300 draws and 150 k triangles).

CPU a frame per node: a probe wrapper in a test (a `Stopwatch` around each of the 20 `_Process` calls through `ProcessMode` toggling or by setting `SetProcess(false)` on one node at a time and comparing the mean) over the same scene run headless, as `game-perf` already does for the total. The check's worst-frame report (5.9 ms) names the *node before the probe*: extend it to name the node that took the longest.

Simulation tick cost at 40 and 100 villagers: `dotnet run --project tools/TideToll.Tools -c Release -- ticks --preset river --walkers 40` and `--walkers 100` (and 500, the existing bench); read the mean and the p99 ms a tick and the "worst ticks with what ran in them". Expected: about 0.02 ms a tick at 28 villagers, linear.

Bytes a frame and GC: the existing `game-perf` already gates them; add `GC.CollectionCount(0/1/2)` before and after the run (a `Gen2` count above 0 in a 60 s run means a heap-growth pause).

### 5b. What only the owner's PC can tell: a small in-game readout

A key (proposal: F3, `tt_debug`, not shown in the keys list; off by default, remembered) that shows a small paper-coloured box top-left (a `Label` in the `Hud` root, updated twice a second, never per frame):

```
fps 58   frame 17.2 ms  (95th 31 ms, worst 74 ms)
sim 0.3 ms   interface 0.4 ms   scripts 0.9 ms   drawing (CPU) 2.1 ms   GPU 14.6 ms
draws 187   objects 241   triangles 212 k   villagers 31   nodes 3,420
window 2560 x 1440   scale 100%   vsync on   cap none   gpu: <adapter name>
```

How to get each number (all in Godot 4.7 C#, no new dependency):
- fps and frame ms: `Engine.GetFramesPerSecond()` and a ring of the last 120 `delta`s (95th percentile and worst).
- scripts' CPU: a `Stopwatch` timestamp (`Time.GetTicksUsec()`) in `TownDriver._Process` (process priority -100, the first) and in a tiny node with priority +100 (the last): the difference is every node's `_Process`; `sim` = a timestamp around the `Town.Step` loop; `interface` = a timestamp around `TownHud._Process`.
- drawing CPU and GPU: `RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true)` once, then `RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid)` and `...Gpu(rid)` (milliseconds per frame, available on Vulkan; the GPU number is the one that answers the question: if it is above the frame's budget, the game is GPU-bound).
- draws, objects, triangles: `RenderingServer.GetRenderingInfo(...)` as above, or `Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)`.
- adapter and driver: `RenderingServer.GetVideoAdapterName()`, `GetVideoAdapterVendor()`, `GetRenderingDevice()?.GetDeviceName()`: this alone answers "what card".

Also a `--perf-log` start-up argument (or the same key held for 3 s) that writes 60 s of those numbers once a second to `user://perf-readout.csv` plus the window size, so the owner can send one file instead of reading numbers to us. Size: about half a day with a test (a text-only box; must not allocate per frame: build the string twice a second only; a gate in `game-perf` that it costs under 0.05 ms when on). Risk: none to the look (off by default).

### 5c. Questions to ask the owner now

1. What PC is it: a laptop or a desktop, and what graphics (Windows: Settings > System > Display > Advanced display, or Task Manager > Performance > GPU)? Is the laptop on the charger and in "best performance"?
2. How big is the game window or screen: windowed or fullscreen, and what resolution is the monitor (1080p, 1440p, 4K), and what is set in the game's Display settings (window size, vsync, frame limit)?
3. When does it slow: from the first minute, or only after the town grows, or in the second age? Only zoomed out, only with the book open, or everywhere? Is it a steady "everything is a little slow" or little jolts (every second, every two minutes, when something new appears)?
4. A one-minute test that tells most: set the window to 1280 x 720 and look at the game; does it feel clearly better? (If yes: it is pixel cost; ask which resolution is where it is fine.) And does the game feel better on a small window on the same machine?

(Optionally ask for a screenshot of the Windows Task Manager's GPU tab while playing: the "3D" bar at 90 to 100 percent confirms the GPU as the limit.)

## 6. Quick wins, safe whatever the cause

| # | Change | Where | Size | Look risk |
| --- | --- | --- | --- | --- |
| Q1 | Do not scan the scene tree every 0.5 s: owners (`TownBuildings`, `RiverLayer`, `RiverBoats`) register their `Label3D` when made or freed; `ScanTags` reads that list | `TownHud.Labels.cs:105-130` (`FindChildren("W*","Label3D",true,false)` over `Boats`, `Buildings`, `River`) | 1 to 2 h | none |
| Q2 | Add a settings entry **Outline: full / light** (`max_step` = 1 on the hard quad: 5 depth reads instead of 9 to 17, and no second step) | `InkOutline.cs:41` (`max_step` set once) and a row in `SettingsPages.SoundDisplay.cs` | 1 to 2 h | line thinner at large windows (the pre-n8 look) |
| Q3 | Add **Drawing detail** (3D render scale 100, 85, 70 percent): `GetViewport().Scaling3DScale` with `Scaling3DMode = Bilinear` (FSR 1 is also available in the Mobile renderer; unverified here) | `SettingsApplier`/`DisplaySeam` (a new `ApplyRenderScale`), `GameSettings.cs` | 2 to 3 h | softer picture and ink at below 100 |
| Q4 | Ground shader early-outs: skip the wear rings where a dilated "near wear" mask is zero, and the river/bank/shimmer block away from the river | `ground.gdshader:~95-120` and the river block, `TownGround.Upload` (a third texture), `TownGround.cs:302` | 3 to 5 h | none if the dilation covers the ring reach (about 4 cells); check with the existing screenshots |
| Q5 | Ink `vz()`: for an orthographic camera view depth is linear in the depth value, so no `inv_proj * vec4` a tap | `outline_screen.gdshader:vz()`, both copies (canonical in `assets/src/common/`) | 30 to 60 min | none (compare pictures bit for bit) |
| Q6 | `DirectionalShadowMaxDistance` 700 to about 350 (the page is 256 m; far plane 400) | `Main.cs:316` | 30 min plus a look | shadows sharper, none lost (check the book edge) |
| Q7 | Autosave: serialise to a `byte[]` on the main thread, write and rename on a worker; or at least never within a frame that also moves the camera or opens a page | `TownSaver.cs:88-100` | 2 h | none |
| Q8 | Shop list every frame: build `ShopItems` and the signature only when `Town.Tick`, coin (floored) or the interface version changed, not each frame | `TownHud.Shop.cs:227-262`, `TownShop.cs:84` | 1 h | none |
| Q9 | Achievement count every frame: count once a second (the value only changes at ticks) | `TownHud.Book.cs:281` | 20 min | none |
| Q10 | The test-only "measures" pass in `Spread` (neighbour search over 5 x 5 cells for each standing villager) and the `Array.Clear` of the 4,096-cell grid: run only when a test asks | `TownVillagers.Spread.cs:~157-185`, `:117` (`_gridHead` fill) | 30 min | none |
| Q11 | `LabelsFrame`: take the 14 cards' rectangles only when a card is visible or moved, and the tag layout only when the camera or a tag changed | `TownHud.Labels.cs:70-75` | 1 to 2 h | none |
| Q12 | Frame cap: a 60 limit as default with vsync on, so a 144 Hz or uncapped machine does not run the ground and ink at 144 fps for nothing (the default today is "none" with vsync on, which is fine on a 60 Hz monitor and a high-heat load on a 144 Hz one) | `GameSettings.cs:91` | 10 min | none |
| Q13 | `Pick` projects every villager each frame: skip while the pointer has not moved and the camera and villagers are still | `TownVillagers.Pick.cs:48`, `TownHud.People.cs:127` | 1 h | none |
| Q14 | Warm-up of all pipelines behind the closed cover at start (one of every model and material for one frame), and when a level is reached (the next level's buildings) | `PauseMenu.cs:247` (`WarmUp`: read first) | half a day | none |
| Q15 | One `MultiMesh` for all chimney smoke instead of one `CpuParticles3D` per cottage | `TownBuildings.Cottages.cs:105` | half a day | small (timing, count) |

## 7. Limits

**Read but not measured:** everything in sections 1 to 3. The only numbers that exist are `game-perf.log` (CPU in the scripts, bytes a frame, headless, no drawing) and the build's logs. All the counts in 2.2 are estimates from the code; the pixel and tap counts in 2.3 are arithmetic from the shader source, not from a profiler; I could not say how a given GPU or the Mobile renderer's driver schedules the two ink quads (early stencil), whether the screen copy is RGBA8 or RGBA16F, or the real order of the opaque draws.

**Documentation checked (Godot 4.7-stable, through Context7, the `docs.godotengine.org/en/stable` pages):** ubershaders and pipeline precompilation (Forward+ and Mobile, since 4.4), what is and is not precompiled, `hint_screen_texture` copies in 3D after the opaque pass, `Viewport.scaling_3d_*`, `msaa_3d`. **Not verified:** that the Windows export uses Vulkan (the default I believe; D3D12 is optional), the exact cost of the depth copy for `hint_depth_texture` in the Mobile backend, FSR 1 availability and look in the Mobile renderer at 4.7, the shadow filter's tap count at "soft medium", the `ViewportGetMeasuredRenderTime*` behaviour on this driver (it needs `ViewportSetMeasureRenderTime(rid, true)`; I believe it works with Vulkan, unconfirmed).

**Not read (or only skimmed):** `PauseMenu.Motion/Carry/Tail`, `BookBuilder*` (the book's own meshes), `TownHud.Request/Choice/Extend/Pave/Tip/ShopRows/ShopTip` frame code beyond the entry points, `TownLife.Gardens`, `WellWorks/MillWorks/StallTable/FieldRows/CottageDoors` per-frame bodies (taken as small from the 0.76 ms), `RiverLayer.Drawbridge/Landing`, `TownMusic` and the audio mixer (audio runs on its own thread; a slow audio device can still cause crackle, not frame time), the `sim/` code except `Step`, `ChooseJob`, `ExpectedWait`, `SiteWait`, shop and worth, the other shaders (`building`, `cargo`, `wake`, `river_sign`, `walker`, `villager`, `ghost`, `route`, `ground_mark`), the asset import settings (texture sizes and mipmaps of the ground's five tiles and the palette atlas: if they are large (2048 or 4096) and not compressed (`texture_format/s3tc_bptc=true` is on in the export) the 25 tiled reads are also bandwidth), the export preset's driver and the installer, the tests (`game/tests`).

**Not possible here:** any frame time on a real GPU, power or thermal behaviour (a laptop's clocks drop after minutes: "slow after a while" can be the machine, not the game), the owner's driver, antivirus or capture software, or Windows' own scaling (a 150 percent display scale makes the window larger in pixels than the setting says).

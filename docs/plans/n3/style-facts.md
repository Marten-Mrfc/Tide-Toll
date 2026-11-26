# Style facts (measured, asset reviewer 1)

Facts only. Sources: `docs/asset-pipeline.md`, `game/scripts/Palette.cs`, `InkOutline.cs`, `CameraRig.cs`, `Main.cs`, `game/shaders/*.gdshader`, `assets.json` (build/n3/assets-audit/before). Pixel claims from code are marked "code"; "picture" means read off the 1920x1080 pictures.

## Palette in use
`game/scripts/Palette.cs` holds 15 colours: Paper #F2EAD8, Ink #2B2620, Meadow #7FA35B, BentGrass #A9B86E, TrailEarth #B89568, PackedEarth #8C6A48, Mud #5E4A36, River #5B7F95, DeepWater #3F5F75, Thatch #C9A55A, Brick #A4553A, SootBrick #7A4436, SlateRoof #5E6B78, CastIronGreen #44604F, Limewash #E9E2CF. `docs/asset-pipeline.md` lists 19 (adds Concrete #A9A69C, Wet mudflat #6F6450, Snow #EEF2F4, Night tint #2E3A5C): these four are not in Palette.cs.

Models: every glb samples the shared palette atlas (`assets/textures/palette.png`); the model generators contain only palette hex (one #7FA35C is a one-digit near-Meadow). So the models themselves are on palette. Off-palette colour comes from code:
- `game/scripts/town/TownVillagers.cs:30-31` clothes: #B8433A, #3F78B5, #D9A21E, #4E8F4A, #8A5AA8, #E8E0CC (the doc forbids state colours on models; #3F78B5 and #D9A21E are near Okabe-Ito blue/orange). Hats/hoods hat0..2 in `walker.gdshader` (set from C#).
- `game/scripts/town/TownBuildings.cs:212,215` StakeColour #E6D5AE, CharterStoneColour #F0EADB.
- `game/shaders/ground.gdshader`: ice #AFC4CE, rain mud #4F3E2F, wet #7F8F99, puddle glint #D5E2E8, gravel wash #E3D9BF, gravel stone #77796F, heat ramp heat0..4 (#F2ECCB, #F0C35A, #E0742C, #B02A2E, #4C1036; overlay only), ice path #F7F7F2.
- `game/shaders/building.gdshader`: per-cottage `thatch_tone` instance colour replaces Thatch (fresh, weathered, grey).
- `game/shaders/tree.gdshader`: each tree tone x 0.92..1.06 of atlas colour.
- `game/scripts/town/PauseMenu.cs:18-20` Gilt #D9B45A, #F4D98C, #A8873F (menu, not the town).
- Sun colour #FFF4E0 (`Main.cs:291`). Background Mud darkened 30%.
- Book: leather red-brown and gold line, table near-black brown (BookBuilder/BookCover; picture: table about #3A2A1E, leather about #4A2318, gold about #8A7A2A). Not named palette entries.
- Palette textures: ground textures are painted from the palette (`assets/src/2d/ground.py`); the wear ramp is the one documented exception.

## Outline
- One screen-space depth-edge pass (`shaders/outline_screen.gdshader`, `InkOutline.cs`): second difference of view depth over a step of `width_px`; fires only at silhouettes and creases. No colour edges (use_colour 0). Models carry no outline geometry.
- Colour: Palette.Ink #2B2620, full alpha.
- Two passes: hard (crease 0.45) and soft (crease 1.5, stencil-marked trees, fields, reeds, villagers), soft alpha 0.4 to 1.0 with zoom (`SoftInk`: 0.4 at 3.5 px/m, 1.0 at 9 px/m).
- Step width: `LinePx` = 1.0 to 1.8 px (1.0 at 4 px/m, 1.6 at 12 px/m, capped 1.8). The shader says the line is about 2x the step (code), so about 2 px zoomed out, about 3 px at play zoom, about 3.6 px at the closest. At play zoom (46 m of height at 1080p = 23.5 px/m) the step is 1.6 px (code). Picture: lines at 1920x1080 play zoom look 2 to 3 px; at the audit's -close pictures they stay 2 to 3 px while the model grows 3x, so a model's ink does not scale with it.
- Walkers are drawn in the transparent pass and are not in the depth texture: they get a thinner hull outline (`walker_hull.gdshader`), not the screen pass.
- Ground: river bank line `bank_px` 1.6 px (ground.gdshader:70); crop edge ink mixed 0.6; cobble joints inked.
- Camera needs near/far 200..400 m for the depth maths.

## Proportions (metres; model size from assets.json, as drawn in the game)
- Villager: 1.05 x 1.09 footprint, 2.86 high (doc says 2.6). Drawn with `WalkerScale` = clamp(15 / px per metre, 1.1, 3): 1.1 at play zoom, so about 3.1 m high, 1.15 m wide; up to 3x (8.6 m) zoomed out. Picture: 11 to 14 px tall at 1080p play zoom. Carried loads are scaled 1.7x (water 1.45x) on top.
- Loads on the head: grain 1.28 x 4.28 total, flour 4.5 total, wool 3.91, cloth 3.68, water yoke 3.0 wide; load-step-3 stack 2.24 x 5.76 x 2.95 (twice the villager's height).
- Cottage 01/02: 8 x 6, ridge 5.84 / 5.59. Cottage 03 (extension lean-to base): 5.93 x 4, ridge 5.63. Extended cottage as drawn: one extension x1.5 wide = 12 x 6 (cottage-extension-1) ; two extensions x1.8 on cottage-03 = 10.66 x 4, so the second-stage house is 2 m shallower than the first (`TownBuildings.cs:101`, non-uniform scale on X only; windows and doors stretch).
- Weaver 8.1 x 6 x 5.39. Keeper hut 3.38 x 3.47 x 4.75. Well 2.36 x 2.22 x 3.0. Market stall 4 x 3 x 2.93. Mill 8.17 x 5.7 x 9.7 (sails included; the tower body is about 3 m across). Bell post 3.3 x 1.7 x 3.74. Charter stone 2.09 x 1.64 x 1.9. Seal stone 3.8 x 3.8 x 3.67.
- Trees: oak 6.06 x 5.68 x 7.4; poplar 2.9 x 2.9 x 8.95; willow 4.76 x 4.87 x 5.4; apple 2.6 x 3.3. Reeds 2 x 2 x 1.4. Sheep 1 x 2 x 1.0 (a real sheep is about 1.2 m long).
- Boats: rowboat 2.4 x 6.65 x 1.4; barge 5.5 x 12.7 (mast to 10.2 high). Landing 3.2 x 6.5 x 2.7. Ferry raft 3.95 x 6. Footbridge span 2 x 2.16, 2.6 high. Drawbridge lift 7.2 x 10.1 x 8.1.
- Fields 16 x 16 (8 x 8 cells), crop 0.37 to 0.58 high; in the game scaled x0.96 to the plot. Pasture 16 x 16, fence 1.2 high. Fence segment 2.2 x 1.0 x 0.22.
- Map cell 2 m. Map 256 m, 128 x 128 cells.

## Level of detail
- Triangles: villager 226; cottage 268 to 316; keeper hut 294; well 168; stall 378; weaver 486; mill 638 (over the 500 guide); bell post 336; charter stone 121; seal stone 248; oak 264; poplar 108; willow 224; reeds 108; sheep 144; field 130 to 250; pasture 493; rowboat 182; barge 594; drawbridge lift 1188; trees-group 968. Props 20 to 148. Total of everything on screen is far under any budget: the town is not limited by triangles.
- Smallest feature: the guide says none under 0.3 m; found: fence post 0.26 m wide, ferry rope 0.13 m, window panes and door frames on cottages about 0.3 to 0.5 m (visible in `-close` pictures, gone at play zoom where 1 px is 0.04 m but ink is 3 px wide).
- Flat shaded, no textures on models, no normal maps, no vertex noise; every facet is one atlas swatch (nearest filtered).
- Triangles are not the problem: shapes are simple boxes and prisms.

## Shading and shadow
- One sun: `Main.cs:287`, rotation (-48, 45, 0), colour #FFF4E0, energy 0.9, shadow on, ortho shadow map, blur 1.5, max distance 700. Sun from the south-east (doc).
- Ambient: colour Paper, energy 0.4. Linear tonemap. No bloom, DOF or fog.
- Models: Lambert (`diffuse_lambert, specular_disabled`), roughness 1. So a roof face is lit by one angle: a thatch roof is two flat orange tones, no gradient.
- Trees: normals bent toward a squashed sphere (`tree.gdshader`, roundness 0.85) so a crown shades as a ball.
- Villagers: a soft ink disc shadow pushed north-west (`walker_shadow.gdshader`, alpha 0.22, push 0.35 m per unit scale); no real shadow map for them.
- Buildings and trees cast shadow-map shadows onto the ground; the ground uses a custom `light()` so unshadowed ground keeps its texture colour exactly.
- Camera: orthographic. Tilt 56 degrees at play zoom (`TiltDegrees`), flattening to straight down from 300 m view height (`TiltFlatSize`) and full tilt below 70 m. Yaw is rotatable.

## Paper and ink of the ground
- ONE plane for the 256 m page (`ground.gdshader`, 326 lines). Five gouache textures (grass, trampled, trail, track, hollow way) blended by the sim's wear value through `wear-ramp.png`, sampled in map metres at two scales: 28 m and 11.3 m (rotated, not a multiple), plus a paper grain (0.07) on top.
- Picture: grass is Meadow with pale BentGrass patches and many small dark-green flecks and larger dark blotches (3 to 8 px at play zoom, 15 to 40 px close up). The blotches have no ink edge, so they read as dirt holes or shadow.
- Trails are soft brown patches with a blurred edge; gravel a pale cream ribbon with dots; cobbles a grey strip with ink joints and an ink edge (the one ground surface drawn in the ink language).
- River: flat River with a darker Deep centre, an ink bank line 1.6 px, faint wave streaks (`texture-water-river.png`), tidal flat band; no foam, no shimmer, no reflections.
- Fields are drawn in the shader too (rows, ink edge) on the map ground, and as field models in the town (130 tri) laid on top.
- Page and book: `paper.gdshader` gives the page exactly Paper where no shadow falls, with grain 0.05 from two paper textures (3.1 m and 23 m); shadow tint (0.72, 0.75, 0.86).
- No ink hatching, no ink dots, no brush-edge on any ground surface except cobbles and the river bank.

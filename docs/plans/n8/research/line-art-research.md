# Line art research: too much ink, and soft lines

Written 2026-11-27 for the lead. Nothing in the game was changed, built or run.
Source status is marked on every outside claim: READ (I opened the page), SUMMARY (I saw only a search summary, not the page), MEMORY (stated from what I remember, not checked). In this session many sites did not open and I did not manage to fetch any full page, so almost nothing outside is "READ". Treat every outside claim as a lead to check, not a citation. The reading of the game's own pictures and shader is my own work and is firm.

## 1. The pictures, read as an art director

What I looked at: the owner's picture, `u11.jpg` (play view, 1920 x 1080), `cottage-c2-normal.jpg`, `mill-normal.jpg`, `villagers-group-normal.jpg`, and the shader. (I did not open u13, u32, the close-ups or the staged board; "everywhere" is judged from u11 and the model sheets, so the lead should glance at those.)

### Where there is too much line

Rule of thumb from what I see: ink is right where it draws the outside of a shape against grass, and wrong wherever two lines are closer together than about three times the line width. Then the gap fills and the object turns into a black block with a few coloured slits.

1. **Thatch roofs (worst).** In the owner's picture every course is a heavy black stripe; the roof reads as black bars on yellow, not yellow thatch. In u11 every cottage and barn, about 60 to 80 px across, has the same barred look from far away. The reason is in the shader: each course is a small step proud of the roof, a step makes two inked pixels-bands (one where it rises, one where it falls), and the band is 2 to 4 px wide. A course 4 to 6 cm wide at about 5 cm per pixel is about 1 px of detail wearing 4 to 8 px of ink. The line is wider than the thing it describes.
2. **Small buildings as a whole.** Owner's cottage is about 100 x 80 px. My estimate of the share of its pixels that are ink: roof 55 to 65 percent, whole model about 45 to 55 percent. In u11 the smaller cottages and sheds, 50 to 70 px wide, are about 50 percent or more ink; they read as dark brown-black lumps with yellow flecks.
3. **Villagers (and small props).** A villager is about 12 to 16 px tall at the play view. A 2 to 4 px outline all round, plus lines at the hat, arms and legs, takes about 35 to 45 percent of the figure (`villagers-group-normal.jpg` is zoomed, so a little less there, perhaps 25 percent). Groups of villagers merge into one dark clump. Carts, crates and baskets (the small dark rectangles in u11) are almost solid ink: about 50 to 70 percent.
4. **Trees.** Low-poly facets produce a line at every facet change; the shader has a "soft ink" mode for this (higher crease threshold), and in u11 the trees look acceptable: about 10 to 15 percent. Not a main problem.
5. **Where it is fine.** The large mill (`mill-normal.jpg`): about 12 to 18 percent, the sails and silhouette read clearly. `cottage-c2-normal.jpg` (a larger view of a cottage, about 190 px wide): about 12 to 18 percent, and it looks like a drawing: the outline round the roof, a line along the ridge, thin lines for the gable. This shows the same shader is fine when the model is big on screen. The fields (u11, the golden plot): about 2 to 3 percent, one outline and one or two rows, fine. Paths have no line at all and read well (soft colour edge only).

So the "everywhere" is: every model that is smaller than about 120 px at the play view and carries modelled detail (thatch, boards, window frames, people). The size of the line is fixed in pixels; the amount of detail is fixed in centimetres; the smaller the model on screen, the more of it is line.

### What a hand-drawn map does instead (MEMORY, general illustration practice)
An illustrator of a map inks the **outline of each building once** (a slightly heavier, slightly broken line), perhaps **one line for the ridge and one where roof meets wall**, and nothing else. Thatch is suggested with **a few short ticks or a colour change**, not with lines for each course. Windows and doors are small coloured shapes, or one short tick. People are a **dot of head and a blob of body** with a very thin or no outline. Fields get a **colour and a few strokes of row**, not a boundary line for each row. Trees get an outline round the whole crown and one or two inner strokes. Everything else is colour: a darker brown for the underside of a roof, a lighter one for the lit side.

## 2. Line weight in illustration and in games

- **Hierarchy: silhouette heaviest, form lines lighter, surface detail lightest or none.** SUMMARY (search results quoting several art-teaching sites): outer edge drawn boldly, interior detail thinner, and a single weight for everything loses the separation between the object and its background. Silhouette is "the frame"; keep the heaviest line for the focal area. A three-weight scheme (thin for interior detail and texture, medium for contour and main interior forms, thick for overlaps and foreground emphasis) is described by a digital-drawing guide. Pages (not opened): https://www.proko.com/course-lesson/demo-hierarchy-of-importance-line-weight , https://medium.com/technical-illustration/line-rendering-use-of-variable-line-weights-fa580665849 , https://sessions.edu/notes-on-design/line-control-saying-a-lot-with-a-little-part-2 , https://users.cs.northwestern.edu/~ago820/NPRcourse1/node5.html (Northwestern NPR course notes, mentioned as a source for "bold outside, thinner inside").
- **Steps between weights.** SUMMARY: architectural line weights follow ISO 128 steps (0.18, 0.25, 0.35, 0.5, 0.7 mm), a ratio of about 1.4 each (https://portico-architecture.squarespace.com/journal//line-weight-for-architects). Useful as a ratio: inner line about 0.5 to 0.7 of the silhouette, surface lines about 0.35 or none.
- **Weight against the size of the thing on screen.** MEMORY and general principle: a line should be a fixed fraction of the thing it describes (a few percent of its size), not a fixed number of pixels. In print, small figures are drawn with a finer pen. As things shrink on screen, line weight should fall until it hits a floor of about 1 px (below that it reads as grey, not ink), and **detail lines are dropped, not thinned**. This is the rule the game now breaks: the pixel width is the same for a 400 px mill and a 14 px villager.
- **When detail lines should vanish.** MEMORY: a detail line stays only if the gap it leaves on each side is at least twice the line's width. Below that, drop the line and keep the colour change.
- **Colour holds (coloured lines against black).** MEMORY: animation and comics use "colour holds" or "colour lines": inner lines drawn in a darker shade of the local colour (brown on yellow, dark green on green), keeping black for the outside. This is exactly what the current style sheet already says for inner lines ("bands of the shade tone or of Timber"), but the shader inks them black anyway, because it sees only depth. The sheet's intent and the shader's behaviour disagree; that is the core of the problem. (Cannot give a URL I opened; general term, search "colour hold lines animation".)
- **Broken and tapering lines.** MEMORY: illustrators let the line thin or break where light hits an edge (top and left edges in a lit scene) and in corners and far from the focus; this is the hand-drawn map's chief charm and also reduces ink. Games that do it: Sable, via textured/variable-width lines (see below), and Wind Waker-like looks via textured hull lines. In a screen pass the cheap equivalents are: ink only on the shadow side, vary the threshold with a noise texture, or fade the line when the depth jump is small.

## 3. How games and techniques keep outlines under control

Honest note: for the games below I did not manage to open any developer source. What follows is mostly MEMORY and search summaries; where I could confirm something I say so.

### Games

- **Sable.** SUMMARY: a forum answer (https://redlib.hbubli.cc/r/howdidtheycodeit/comments/17k126c/how_did_they_make_the_outline_shader_in_sable , a mirror of a Reddit thread) says Sable combines two line kinds, "polygon lines and then normal lines", and points to a GDC talk "The Art of Sable" which I could not open. MEMORY: the look is Moebius ink: thin, fairly even lines, with big flat areas left uncoloured by line; inner lines are sparse; and a lot of the detail is in textures. I cannot confirm "inner lines by distance, per-object control" from a source. Godot imitations exist: https://80.lv/articles/have-a-look-at-this-bright-godot-shader-inspired-by-sable , https://godotshaders.com/shader/mobius-outline-hatching/ , https://blog.maximeheckel.com/posts/moebius-style-post-processing (all SUMMARY).
- **Return of the Obra Dinn.** MEMORY: 1-bit, edges from a screen pass, dithered shading; lines are thin (about 1 px at the game's resolution) and there is no black fill from lines because the dither carries the shading. Lucas Pope's devlog on TIGSource has the details (I could not open it; see https://www.tigsource.com/tag/lucas-pope/ and https://www.pcgamer.com/lucas-pope-on-the-challenge-of-creating-obra-dinn-1-bit-aesthetic , SUMMARY only). The lesson for us: 1 px lines, and they work only because the render is high resolution.
- **Borderlands.** SUMMARY (Gearbox quotes in search results; https://80.lv/articles/how-borderlands-devs-created-franchise-s-signature-art-style , https://gamespot.com/articles/behind-borderlands-11th-hour-style-change/1100-6253257/ , https://gdcvault.com/play/1012220/Borderlands-and-the-11th-Hour): hand-drawn textures with ink strokes, scanned and coloured, **plus** one procedural element, a Sobel edge pass that draws the black outline around characters and environment; they say they did many tweaks to it. So: drawn inner lines live in the texture, the pass does the silhouette only. This is the closest known recipe to what we need.
- **Wind Waker / Breath of the Wild.** I found nothing sourced. MEMORY: Wind Waker has little black outlining, mostly texture-based shading and soft shapes; BotW has almost no outline, relying on colour and light. Their lesson, if the memory is right: the flat-colour toon look does not need black lines to read.
- **Okami, Valkyria Chronicles, Hades, Tunic, Lethal League, Kentucky Route Zero, Manifold Garden, Mini Motorways, Townscaper, Dorfromantik.** Not researched at source. MEMORY only, with low confidence: Okami uses ink-brush strokes with variable width over a paper texture; Valkyria Chronicles has a sketch-pencil look with lines mostly on silhouettes and a paper overlay; Hades has hand-painted outlines baked into character art with a thin dark rim and a colour-held look; Tunic uses mostly colour and light, thin outline on some things; Manifold Garden uses edge detection as the look, but on huge flat-colour architecture where each line is long and detail is sparse; Mini Motorways and Townscaper have no outlines at all and read by **colour value contrast, soft shadow/ambient occlusion, and simple shapes**, which works because every object is a clean block against a near-uniform background; Dorfromantik reads by colour and small tile-edge shading. Verify before citing.

### Techniques (strengths and faults) - mostly MEMORY and standard graphics practice
| Method | Strength | Fault, for us |
|---|---|---|
| Edge detection on depth (ours) | Cheap, works in Godot Mobile (no normal buffer), no model prep | Inks any small relief (thatch courses, planks) as thick black; width fixed in pixels; hard, aliased edge |
| On normals | Catches creases without relief, even on flat-depth surfaces | Not available in Mobile renderer; lights up on every low-poly facet |
| On colour | Lines where colours meet (doors painted on walls) | Inks shadows, grass patches and texture noise; needs a threshold per scene |
| On object ids | Clean silhouettes between objects, per-object control, no inner noise | Needs an id buffer (extra pass or stencil); no inner lines at all (which is what we want most) |
| Inverted hull | Even outer line, can vary width per vertex, per object; colour per material; cheap | Outer silhouette only; breaks at hard edges unless normals smoothed; thickness in world units unless corrected |
| Lines drawn in textures or vertex colours | Full control, coloured holds, tapers, hand-drawn feel; scale with object and fall away as texture mips | Texture work per model; texel density limits crispness |
| SDF / distance-field lines | Resolution-independent, crisp, width can be animated | Needs a distance field per model or per screen |
| Per-object/material masks (stencil) | Turn inner lines off or soft per kind of thing; the shader already does this for "soft ink" | Needs author discipline and extra pass |
| Width in pixels vs world units | Pixels: line steady on screen. World units: line scales with the object so the weight ratio stays the same under zoom | Pixels: small things drown (our problem). World units: far things go sub-pixel and shimmer |
| Fade inner lines with distance/size | Directly solves "detail gets too heavy when small" | Needs a rule for what is "inner" (depth difference small vs large) |
| Anti-aliasing the edge line | Lines look drawn, not stair-stepped | Edge pass runs on a hard 0/1 mask; needs a soft step or MSAA/FXAA on the line |

A plain way to make a depth line lighter without a new pass (idea from the shader, not from a source): size the threshold by the **jump**, not by the second difference alone. A silhouette is a jump of metres in depth; a thatch course is 1 to 2 cm. Raise `crease` so that 1 to 2 cm relief gives no line, and the ink goes away from thatch while every silhouette stays. That is one number, testable in a minute.

## 4. What "blur" can come from (checklist for engineers)

1. **The line is hard, then smoothed afterwards.** The edge shader writes ALPHA 0 or 1 with `step`, and the shader runs after the 3D picture. If anything (FXAA, TAA, an upscaler, bilinear scaling) runs after the ink, the stair-steps become soft 2 to 3 px smears. Check order: is ink drawn before or after the scale and anti-aliasing?
2. **Render scale below 100 percent, or the window not equal to the viewport.** If the 3D is rendered at a lower size and scaled up to 1920 x 1080 (Godot `scaling_3d` or a SubViewport), every ink pixel is magnified and blurred. The owner's picture itself looks like a small crop enlarged (365 x 267), which would add blur of its own; compare with a native 1920 x 1080 screenshot before trusting what the owner's crop shows. Our shader samples with `texelFetch` on the depth buffer, so the ink grid is the render grid.
3. **Screen anti-aliasing filters.** MSAA in Godot does not apply to the depth-based pass (the depth is resolved before; edge pixels of the pass get no AA). FXAA or TAA run on the final image and blur thin lines; TAA can also shimmer thin lines.
4. **Texture filtering.** Ground and cloth textures with mipmaps and anisotropic filtering blur small drawn detail in the distance; ink drawn in textures (if we move to that) would blur the same way. `filter_nearest` in the shader is right; the world textures are separate.
5. **Lines wider than the detail they describe.** A thatch course 1 px wide with a 4 px line on each side fills in; the black merges, which reads as "smear" rather than "stripe". The width is `2 x w` where `w = int(1.5 * height/1080 * zoom + 0.5)`: at `w = 2` the line is about 4 px. That is a design choice in the shader file (the comment says "about 2x this wide"), and it is the biggest cause of the heavy look. Also, rounding `w` to an integer means a zoom change flips between 1, 2, 3 px with jumps instead of a smooth change.
6. **Dark shadows next to ink.** Soft cast shadows and the dark soft patches on the ground (visible in the model sheets) sit next to ink lines and make the line edge appear to bleed.
7. **Post-processing order and colour.** Ink is `#2B2620`, nearly black; any tone-mapping, bloom or vignette after it can lift and smear it.

How to tell which one it is, without changing anything much: take a screenshot at exactly 1920 x 1080 with all post filters off and inspect a single line at 800 percent. A hard 1 px stair-step = the filters/scaling are the blur. A hard black band 3 to 4 px wide = it is width, not blur.

## 5. Rules for this game: section 3 "Line", rewritten

Proposed text for `docs/plans/n3/style-sheet.md`:

> ## 3. Line
>
> The map is a coloured picture first. Ink is for the outside of things and for a very few main inner edges. Everything else is a darker or lighter band of colour.
>
> **What gets ink (drawn by the game):**
> - The silhouette of every model against the ground: always, the heaviest line.
> - The roof ridge, and the line where a roof meets its wall: one line each.
> - The base of a building where it meets the ground: only if the model is bigger than about 120 px at the play view.
> - Nothing else. In particular no ink on thatch courses, planks, tiles, window and door frames, shutters, row furrows, ripples, fur, folds of cloth, fingers.
>
> **What gets a band of colour instead (made by the modeller):**
> - Thatch courses, planks, timber framing, shingles, door boards, furrows: a band of a darker shade of the surface's own colour (or Timber), 4 to 6 cm wide, **flush with the surface (no step)**. Not raised: a raised step is read as a crease and inked. The surface is flat; the colour does the drawing. This replaces "standing 1 to 2 cm proud so the ink finds them".
> - Shadow side of a roof, underside of eaves, gaps between objects: a darker shade, not black.
>
> **Weights (at the play view, 1080 px high):**
> - Silhouette: 2 px.
> - Main inner line (ridge, roof to wall): 1 px, in a colour darker than the surface (not full ink) where the shader can do it; otherwise 1 px of ink.
> - Surface detail: none. No part of a model may be a line only.
> - A line may never be closer than 3 line widths to another line. If two lines would be, drop the smaller.
>
> **Counts at the play view:**
> - A roof: at most 1 ridge line, at most 3 coloured courses on a side, 0 ink courses.
> - A wall: 0 ink lines; at most a door and one window, as coloured shapes with no frame line.
> - A field: one outline for the plot, no ink per row; rows are two-tone colour, at most 5 per plot.
> - A villager: silhouette only, 1 px or thinner; head, body and carried item read by colour; no inner lines.
> - A tree: crown silhouette, plus at most 2 inner lines.
> - A prop smaller than 24 px (crate, basket, bird): silhouette 1 px, no inner lines.
>
> **Zoom:**
> - Play view: as above.
> - Whole page (zoomed out): silhouettes thin to 1 px, nothing inside; buildings read by colour and roof shape. Lines never go below 1 px and never fade to grey (a grey line is blur).
> - Close-up (zoomed in): the weight grows to at most 3 px for silhouettes and 1.5 px for the main inner lines, and the detail bands of colour may appear as lines in a darker shade of colour (not black), up to 8 courses on a roof. The close-up is where a drawn look can have more lines; ink is still never denser than the play-view test below.
>
> **Nothing is perfectly straight or square** (keep): ridges sag 3 to 5 percent, walls lean up to 2 degrees, planks differ in length, posts tilt. Use the generator's seed.
>
> **The test (every model, at the play view, 1920 x 1080, as rendered by the game):** count pixels that are ink-coloured and divide by the pixels of the model (inside its silhouette including the line). Pass if the share is **at most 15 percent for buildings and large props, at most 25 percent for a villager, at most 20 percent for trees and other small things**; fail at 30 percent. Second test, by eye: squint, or blur the picture to a quarter of its size: the roof must still read as roof-coloured, not as black. Third: no two inked pixels-bands closer than 3 pixels of colour between them.

I suggest these numbers from the pictures: the models that look good now (mill, larger cottage) are at about 12 to 18 percent, the ones the owner dislikes are 45 to 60 percent, so a limit near 15 to 20 percent for buildings sits between them. These are estimates by eye from JPEGs, not counts; the lead should have the number measured with a small script on the existing screenshots before the limit is frozen (a measurement of 20 images would settle the limits).

## 6. Recommendation: the three changes that help most

1. **Stop inking small relief: raise the crease threshold or make the crease threshold depend on the size of the step, and drop the "proud edge" rule from the style sheet.** Make the modellers' bands flush with the surface and coloured; let ink find only silhouettes, ridges and wall/roof joins. Smallest change in the shader (`crease` up, perhaps `width_px` down); the biggest effect in the picture (thatch stops being black bars). Risk: thatch and boards become flat colour; fix by keeping the coloured darker bands (so it is still drawn, just not in black), and by checking that houses still separate from the grass (the silhouette line is still there; the grass is similar in value to the thatch, so the silhouette is what separates them, which is why it must stay).
2. **Make line width follow the size of the thing on screen: width 1 px for small models and people, 2 px for houses, 3 px at close-up; drop inner lines altogether under about 60 px.** Use the existing soft-ink stencil path per kind (it already exists): villagers, props and small buildings get "soft" or "silhouette only". Add a per-object width or a distance-based threshold. Risk: small things lose some of their "stickiness" and a villager group can read paler against the grass; the answer is to keep the 1 px silhouette and give them a darker clothing colour.
3. **Make the line soft in the right way and not in the wrong way: apply an anti-aliased edge (a soft step of 1 px on the mask), draw it before any scaling/FXAA, and render at 100 percent scale; then take the line colour from the surface (a darker shade) for inner lines.** First, find out whether the blur is from the scaling or filters (checklist 4): the owner's crop looks enlarged. Risk: a soft line can look smeared if it is wider than 2 px; the full black outline can look pasted-on if inner lines are tinted, so keep the silhouette black and only tint the inner ones. If the lead can afford the work later: draw the inner lines (thatch, planks) into the textures or vertex colours as coloured lines, like Borderlands (SUMMARY above), and keep the pass for silhouettes only. That is the most durable fix and the most work.

Overall risk to watch: a bland look. The cure is not more lines but a **little colour contrast between neighbouring shapes**, plus the broken line (thin out on the lit sides) so that what remains looks drawn.

## What I could not check
- No outside page was fetched in full; Sable, Obra Dinn, Okami, Hades and the rest are from memory or search summaries (see each).
- Percentages of ink are my estimates by eye from the JPEGs, not pixel counts.
- I did not read the u13, u32, close-up or staged-board pictures, and I did not run anything.

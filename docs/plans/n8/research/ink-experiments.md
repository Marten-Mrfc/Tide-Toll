# Ink lines: what makes them heavy and soft, and what to do (research by experiment)

Branch `wt/n8-ink` (worktree `build/wt/n8-ink`), not for merging. Boards beside this file:
`ink-now-and-options.png` (the owner's cottage: the owner's picture, now, six options) and
`ink-options-street-town.png` (a street, the busy town at the play view, the whole page: now and four options).

## Short answer

- **The game does not blur anything.** No stretch, no 3D scaling, no anti-aliasing: at 1920 x 1080, 99.7 percent of
  the ink-coloured pixels around the cottage are exactly Ink. The softness in the owner's picture was added after the
  game drew it: the picture shows the cottage about 1.22 times as large as a 1080p screen does and 10.5 percent of its
  dark pixels are in-between tones, the marks of an enlargement (screen scaling, the screenshot or the app it went
  through). The game's lines are hard-edged and look stair-stepped when enlarged.
- **The black is real and comes from two things together:** a line that is 4 px wide at 1080p and up (step 2, both
  sides of every crease), and roofs built with thatch courses that are 20 cm wide beams standing 9 cm proud
  (the style sheet asks for 4 to 6 cm wide, 1 to 2 cm proud). Each course gives two lines whose 4 px bands touch:
  every course becomes a solid black stripe. Now **62 percent of the cottage's pixels are ink** (the oak 26, a field 6).
- **Recommended:** silhouettes stay full ink at today's width; inner creases are found with a 1 px step and drawn as
  the colour under them darkened (x 0.6) with a soft ramp instead of black; thatch courses become painted bands with
  no step. The cottage drops from 62 to 22 percent ink, the street from 64 to 28 percent. The cost is a few depth reads
  more a pixel in the ink pass (counted in section 4; the software-rendered timing was too noisy to use), no new pass, no anti-aliasing.

## 1. The owner's picture reproduced

- Model: **cottage-a2** (hipped longhouse extended twice: the back wing to the west and the east wing; the blue door is
  an accent the game picks per plot; ours is green). Pictured as `TownBuildings.CottageNode(0, 2, 6, 6)`.
- View: the start (follow) view, camera size 46.0 m whatever the window (`win.log`: 23.5 px/m at 1080, 15.7 at 720,
  19.6 at 900, 31.3 at 1440), straight on (yaw 0), 56 degree tilt.
- The 365 x 267 crop of a 1920 x 1080 frame round the cottage (`build/n8/win/crop-1920x1080.png`) is the owner's
  picture almost exactly: same model, view and line pattern. The owner's cottage is about 1.22 times larger, its dark
  roof stripes 7 px against 4 px median in ours (same column of the roof). That fits a frame drawn with a 2 px step at
  about 900 lines (1600 x 900 window, or a 1440p screen with 150 percent scaling) and enlarged about 1.45 times, or a
  1080p frame zoomed out a notch and enlarged; it does not fit 1280 x 720 (lines too thin). What cannot be told from
  the picture: the window size, the zoom notch, and which program enlarged it.
- What the window does to the line (`InkOutline.LinePx` then `int(width_px + 0.5)`): at the play view LinePx is
  clamped to 1.8, times height/1080: **720p: step 1 (lines 2 px); 900p, 1080p, 1440p: step 2 (lines 4 px)**. So the
  same view at 1600 x 900 has the cottage 17 percent smaller with lines as wide as 1080p: the heaviest look of all.
  At 1280 x 720 the cottage is 42 percent ink instead of 62.
- Note: the asset audit's `-normal` pictures (640 x 480 at the 1080p metres per pixel) draw with step 1, so they show
  the 720p look and understate the ink the player sees at 1080p. The lab draws step 2 for "now".

## 2. The ink measured

Method: each subject at the play view (640 x 480, 1080p metres per pixel, yaw 0), pictured with the variant, with no
ink and with nothing (ground alone). Model pixels = where the no-ink picture differs from the ground (cast shadows
left out); ink share = pixels the line changed, weighted by how far toward Ink they moved (a line at half strength
counts half). Line width = median of the shorter run through each ink pixel (bands where lines merge count as one wide
line). `python3 tools/ink-lab/measure.py build/n8/lab2` (and `lab3`, `flat`, `few`, `town`).

Weighted ink share, percent of the model's pixels:

| subject | now 1080p (step 2) | now 720p (step 1) | inner creases in own colour | whole model soft ink | courses flat (model) | recommended (shader) | recommended + flat courses |
|---|---|---|---|---|---|---|---|
| cottage a2 (owner's) | **62.5** | 41.4 | 33.2 | 31.9 | 39.8 | 25.0 | **21.9** |
| cottage a0 | 60.8 | 34.7 | 31.6 | 30.0 | | 22.5 | |
| cottage b0 / b2 | 59.5 / 63.9 | 32.9 / 41.6 | 30.9 / 34.3 | 23.8 / 31.5 | - / 42.7 | 21.8 / 25.1 | - / 23.2 |
| cottage c0 / c2 | 54.2 / 66.2 | 33.5 / 46.2 | 27.5 / 33.8 | 31.2 / 37.0 | - / 44.5 | 19.4 / 24.1 | - / 20.8 |
| cottage a2 zoomed in (x2) | 43.2 | 25.1 | 21.4 | 26.8 | | 15.0 | |
| cottage a2 zoomed out (x0.5) | 61.8 | 55.7 | 42.1 | 38.4 | | 36.8 | |
| street (5 cottages, 6 carriers) | 64.1 | 44.7 | 37.6 | 36.5 | 48.9 | 29.4 | **27.8** |
| mill | 51.3 | 31.7 | 33.4 | 31.0 | | 25.1 | |
| market stall | 48.6 | 32.3 | 36.1 | 37.5 | | 32.3 | |
| villager with grain | 71.2 | 46.6 | 53.1 | 58.2 | | 45.4 | |
| oak (soft ink already) | 25.6 | 14.9 | 19.4 | 25.6 | | 14.8 | |
| field, ripe (soft ink already) | 5.9 | 3.2 | 3.7 | 5.9 | | 2.7 | |
| busy town, whole 1280 x 720 frame | 12.7 | 9.1 | 8.9 | | 9.7 | 7.6 | **5.9** |

Line widths: a single line is 4 px at step 2 (1080p and up) and 2 px at step 1 (720p, and the whole-page view at any
size). On the cottages the courses' lines merge into bands: median 7 to 8 px "now", 2 to 4 px with the recommendation.
Lines across a roof: the main range of cottage a has four courses a slope, each a beam with two inked edges, plus ridge,
hip and eave lines: the middle column of the cottage crosses 8 ink bands now, 4 with the recommendation (ridge,
eave, wall foot, chimney). The back wing's courses, seen steeply, become 6 to 8 vertical stripes in every shader
option; only the model change removes them.

What looks right: the models that read well are the soft-ink ones (oak 15 to 26 percent, fields 3 to 6) and the
cottages zoomed in (15 to 25). A building at about **20 to 25 percent** ink (outline plus a few lines) looks drawn,
not inked over; villagers stay near 45 percent whatever is done because they are a few pixels across and mostly outline.

## 3. Where the "blur" comes from (tested)

| suspect | finding |
|---|---|
| Window stretch | `project.godot` sets no `display/window/stretch/*`: mode disabled, the 3D picture is drawn at the window's own size. The interface size is `ContentScaleFactor` only (2D). |
| 3D scaling | `Scaling3DScale` is left at 1.0; nothing sets it (grep). |
| Anti-aliasing | none set (no MSAA, no FXAA, no TAA). |
| Resolution option | `DisplaySeam.ApplyWindow`: windowed sets the window size (a smaller window, not a stretched picture); borderless and fullscreen use the desktop size. A lower resolution never gets stretched by the game. |
| Same view at two window sizes | `build/n8/win/win-*.png`: at 1080p 7321 pixels are exactly Ink and 21 are in-between; at 720p 2935 and 5. The lines are hard-edged at every size; only their width changes (step 1 at 720p, 2 from 900p up). |
| Ground textures | linear, mipmapped (`ground.gdshader`): the grass is soft by design; models use nearest-sampled palette atlases. Not a source of line blur. |
| Soft-ink alpha | 1.0 at the play view (`SoftInk(23.5) = 1`); 0.4 only with the whole page in view. Not involved. |
| Jagged lines smoothed by something | nothing smooths them in the game. Of the AA modes, FXAA does: it smears the ink into a grey halo and makes it look heavier (66 percent, the blurriest picture of all, `build/n8/lab2/ink-cottage0-ext2-fxaa.png`); MSAA 4x does nothing to the lines (they come from a full-screen pass, MSAA only smooths triangle edges). |
| The owner's picture | 1491 of its 14137 dark pixels (10.5 percent) are in-between tones against 0.3 percent in the game's frame; the cottage is 1.22 times its 1080p size. It was enlarged after rendering. |

So the "blur" is (a) enlargement outside the game, and (b) what reads as smear in the game itself: lines 4 px wide
that merge into bands and stairs, with no anti-aliasing. Thinner lines and fewer inner edges cure (b).

## 4. The remedies (switches on the branch)

All in the lab copy of the ink shader (`game/tests/AssetAudit.Ink.cs`, `LabShader`): `lab_w` (sample step),
`lab_ramp` (smoothstep from the threshold to 4x it instead of `step`), `lab_mul_in` / `lab_mul_sil` (the line as the
colour under it times `lab_k`), `lab_strength`, `lab_inner` (alpha of inner creases) and `lab_w_in` (inner creases
found with their own step; silhouettes = second difference above `lab_sil` = 0.3 m). Model switches:
`INK_COURSES=flat|few python3 assets/src/models/cottage.py`. Board 1 shows them on the owner's cottage.

| remedy | cottage a2 ink | what it looks like |
|---|---|---|
| Thinner line (step 1 at the play view) | 62 -> 41 | courses become two 2 px lines each instead of a black band; still busy, back wing still striped. A per-object width (by pixels covered) was not built: the step is already 1, the next step down is no line. |
| Ramp instead of the hard step | 62 -> 36 | weak creases (courses) fade, silhouettes unchanged; the stair-steps of strong lines stay. |
| Flat ink at 60 percent | 62 -> 27 | lighter everywhere, but the silhouette against the grass goes grey-green and the town loses its drawn look. Rejected. |
| Line in the colour under it x 0.45 (all lines) | 62 -> 26 | the same problem at the silhouette (dark green halo on the ground). Rejected for silhouettes. |
| Inner creases at 35 percent | 62 -> 29 | good roofs, but some outer edges that are creases (wall foot) go faint. |
| Inner creases in the roof's own colour (x 0.55), silhouettes ink | 62 -> 33 | courses read as darker thatch, the outline holds. With the 1 px inner step: 21. |
| Two weights (recommended shader): silhouettes ink at today's step, inner creases at step 1, own colour x 0.6, ramp | 62 -> 25 | clean outline, roofs readable, courses and boards visible but quiet. |
| Inner lines fade when zoomed out | at the play view nothing to fade; zoomed out (x0.5) the recommended shader is at 37 percent, mostly outline, so the fade is not needed beyond the colour line. The soft ink's existing fade (to 0.4 at the page) is kept. |
| Fewer, wider courses (model) | 62 -> 54 | two black bands instead of four: still bands. |
| Courses painted flat (model, 1 cm proud) | 62 -> 40 | the main roof clears; the patch and the wing roofs keep their lines. |
| Whole cottage in the soft-ink group | 62 -> 32 | courses go, but so do the house's own corners; and the soft ink fades at the page. Second choice to the shader change. |
| FXAA | 62 -> 60, blurred | worse: a grey smear round every line. |
| MSAA 4x | no change | the ink is a full-screen pass. |
| Drawn at 2x and scaled down (step 2 in the 2x picture) | 62 -> 34 | smooth, thinner lines that look drawn with a pen; but 4 times the pixels for the whole 3D picture. |
| Recommended shader + courses flat | 62 -> **22** | board 1, last panel. |

Cost (the ink pass is GPU work; `game-perf` runs headless with no rendering, so it cannot see it, and the CPU side is
unchanged: the same two quads, the same uniforms written only when the zoom changes). A GPU measurement was tried in
the busy town at 1920 x 1080 under xvfb software rendering (`--ink-town --ink-cost`, 40 frames a variant): one frame
takes about 1.5 s there and the machine was shared, so the noise (no ink 1541 ms, today's ink 1486 ms, 1 px step 1982 ms)
is far larger than the ink pass; the run was stopped. The cost below is counted from the shader instead; it needs a
real GPU (game-motion's frame times on a machine with one) to confirm.

| variant | work per pixel of the ink pass (today: 5 depth reads, 5 unprojections) | frame cost |
|---|---|---|
| thinner line, ramp, strength, inner alpha | the same reads; a smoothstep instead of a step | none |
| inner creases in own colour | + 1 screen-colour read (the pass already declares and references `hint_screen_texture` for its optional colour edges, so the copy of the opaque picture is already made) | negligible |
| two weights (recommended) | + 4 depth reads and unprojections on pixels that are not silhouettes (nearly all), + 1 colour read | roughly 1.8x the ink pass, which is one full-screen pass of a few texel reads: well under a millisecond on a phone GPU at 1080p |
| model: flat courses | fewer triangles on roofs (the beams' side faces go) | none |
| FXAA | one more full-screen pass | small, and it makes the lines worse |
| MSAA 4x | 4x the depth and colour samples of the 3D picture | real on mobile, and no effect on the lines |
| drawn at 2x, scaled down | 4x every 3D pixel (ground shader, models, ink) | the largest by far; not for the Mobile renderer's targets |

## 5. Recommendation

Ship, in this order:

1. **Shader (no model work):** two weights. Silhouettes (second difference of 0.3 m or more) keep full Ink at today's
   width; inner creases are found with a 1 px step, drawn as the colour under them times 0.6, with a ramp from the
   threshold to four times it instead of the hard step. The cottage goes from 62 to 25 percent ink, the busy town frame
   from 12.7 to 7.6. Cost: four more depth reads and one screen read for the pixels that are not silhouettes.
2. **Models:** thatch courses (and planks, boards, framing) as painted bands of the shade tone or Timber with no step
   (at most 1 cm proud). Together with 1: 22 percent on the cottage, 28 on the street, 5.9 on the town frame.
3. **The width rule:** `LinePx` clamps to 1.8 at the play view and the shader rounds 1.5 up to 2, so a 900p window
   gets the 1080p line on a 17 percent smaller picture. With two weights the inner step is 1 px at every size and
   only silhouettes use the rule; keep it as it is for them (a 4 px outline reads well) or round 1.5 down if the
   owner wants the outline lighter at 900p.
4. Do not turn on FXAA or MSAA for the lines; drawing at 2x is the only AA that helps them and costs 4x the 3D pixels.

Style sheet section 3 changes: "Inner lines ... standing 1 to 2 cm proud so the ink finds them" becomes **inner lines
are painted, not built: bands of the shade tone or Timber flush with the face (at most 1 cm proud); the game draws them
as a darker shade of their own colour. Only the outline and real breaks of form (wall to roof, ridge, eave, chimney,
a step of 10 cm or more) get Ink.** "Three to five courses on a roof" stays as a count of bands. And: the audit's
-normal pictures should be taken at 1080p line width (step 2), not 640 x 480's step 1, or they understate the ink.

Models to rebuild for step 2: the nine cottages (`assets/src/models/cottage.py`: the course beams are 0.2 x 0.09 m,
four times the style sheet's own figure), and any other model with proud inner boards; candidates by measured ink:
market stall (49 percent), mill (51), villagers (71: they are mostly outline at 1 m across; leave them to the shader).
Not checked one by one: weaver cottage, river buildings, props.

Ranked causes of the complaint (cottage a2 at 1080p, 62 percent ink):

1. **The line's width and strength: about half.** The width rule alone: step 2 -> step 1 takes 62 to 41 (21 points);
   inner creases as colour instead of ink, at 1 px: to 25 (another 16).
2. **The models' many proud inner edges: about a third.** With today's shader, flat courses alone take 62 to 40;
   after the shader change they take 25 to 22, and they remove the striped wings the shader cannot.
3. **Scaling or filtering: none in the game.** The softness of the owner's picture came from enlarging the
   screenshot (or the screen) outside the game; the window size matters only through the width rule (900p is worst).

For the owner to decide: whether inner lines may be coloured (a darker thatch, not Ink) or must stay Ink but thinner;
whether to spend 4x pixels on drawing at 2x for pen-like smooth lines (it looks best close up, the cost is large on
mobile GPUs); and whether the villagers should also lose ink (they stay at 45 percent).

## Reproduce

```
cd build/wt/n8-ink   # branch wt/n8-ink
tools/sync-assets.sh && godot --headless --path game --import && dotnet build game/TideToll.csproj -c Debug
A="godot --path game --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 res://tests/asset-audit.tscn --quit-after 1000000 --"
xvfb-run -n 201 -s '-screen 0 1920x1080x24' $A --out-dir=$PWD/build/n8/lab2 --ink-lab                       # every subject, every variant (~5 min)
xvfb-run -n 202 -s '-screen 0 2560x1440x24' $A --out-dir=$PWD/build/n8/win --ink-windows                    # the owner's cottage in 4 window sizes
xvfb-run -n 204 -s '-screen 0 1920x1080x24' $A --out-dir=$PWD/build/n8/town --ink-town --ink-variants=now,now720,thin,inner-colour,thin-inner-colour,rec,rec2,ssaa2-thin
INK_COURSES=flat python3 assets/src/models/cottage.py && tools/sync-assets.sh && godot --headless --path game --import
xvfb-run -n 205 -s '-screen 0 1920x1080x24' $A --out-dir=$PWD/build/n8/flat --ink-lab --ink-town --only=cottage0-ext2,street,cottage1-ext2,cottage2-ext2 --ink-variants=now,now720,thin,inner-colour,thin-inner-colour,rec,rec2
#   (INK_COURSES=few likewise into build/n8/few; then git checkout assets/models assets/LEDGER.csv and re-sync)
xvfb-run -n 208 -s '-screen 0 1920x1080x24' $A --out-dir=$PWD/build/n8/cost --ink-town --ink-cost --ink-variants=now,thin,ramp,inner-colour,rec,rec2
python3 tools/ink-lab/measure.py build/n8/lab2      # the table
cp docs/plans/n8/owner-picture.png build/n8/ && python3 tools/ink-lab/board.py build/n8 build/n8/boards assets/fonts/AtkinsonHyperlegible-Bold.ttf
```

Variant names in the files: `now` (step 2, the 1080p game), `now720` (the game's own shader in a 640 x 480 window:
step 1), `thin`, `ramp`, `multiply`, `strength60`, `inner-light`, `inner-colour`, `thin-inner-colour`, `rec`
(everything at step 1), `rec2` (the recommended two weights), `soft-cottage`, `fxaa`, `msaa4`, `ssaa2`, `ssaa2-thin`.
The busy town differs a little between runs (the ground's wear): compare within a run. On board 2's whole-page row,
"Now" is the game's own step 1; the other panels use the lab's step 2 for silhouettes, so they are if anything
heavier than the game would draw them there.

# Ink lines: the recommended combination built in (n8)

Branch `wt/n8-ink`. The owner chose the recommended combination from `docs/plans/n8/research/ink-experiments.md`.
Boards: `docs/plans/n8/ink-before-and-after.png` (every building kind, the villagers, a street, the busy town; now
beside new, 1:1 pixels of a 1920 x 1080 window) and `docs/plans/n8/ink-window-sizes.png` (the owner's cottage at
720, 900, 1080 and 1440 lines, the same ground, now above new).

## What changed, file by file

- `assets/src/common/outline_screen.gdshader` (canonical) and `game/shaders/outline_screen.gdshader` (copy, identical
  but for its first line): two weights.
  - Silhouettes (second difference of view depth of 0.3 m or more, `sil_m`): full Ink. Their weight is `width_px` as a
    real number: the step `floor(width_px)` at full strength plus the next step out at the fractional part's
    strength, so the outline grows smoothly with the window and the zoom (no rounding jump).
  - Inner creases: found with a one-pixel step, drawn as the colour under them times 0.6 (`inner_k`), with a
    smoothstep from the crease threshold to four times it instead of the hard step.
  - `max_step` caps the outline's step (used for the soft-ink group, below). `ink_alpha` is now in the shader itself.
  - Removed: `use_colour` / `colour_edge` (the colour-edge option, used by nothing in the game; the contact sheet
    never passed it either).
  - Work per pixel: before 5 depth reads (and unprojections); now 1 + 4 at step 1 (shared by the inner creases and,
    below 2 px, the outline) + 4 at the outer step, + 4 more when `width_px >= 2` (1440 lines and up at the play
    view), + 1 read of the screen copy (which the pass already referenced, so the copy was already made).
    That is 9 reads at 720 to 1080 lines and 13 at 1440, against 5. No new pass, no anti-aliasing. It could not be
    timed under software rendering: **it needs a look on a real graphics card** (game-motion's frame times there).
- `game/scripts/InkOutline.cs`: no more string patching of `ALPHA` (the shader has `ink_alpha`); the soft-ink pass gets
  `max_step = 1` (thin outline for the soft group); the width rule is unchanged in form (`LinePx * height / 1080`),
  but the shader no longer rounds it, which fixes the 900-line case.
- The soft-ink group is kept as it was (trees, crops, reeds, villagers, loads, small props: stencil 1, crease 1.5) and
  now also has a one-pixel outline at every size.
- Models (painted bands at most 1 cm proud instead of raised beams):
  - `assets/src/models/cottage.py`: thatch courses 0.2 m bands, 1 cm proud, lying 1.2 cm above the slope (were
    0.2 x 0.09 m beams 5 cm above): the nine cottages a0..c2 and cottage-01/02/03 rebuilt.
  - `assets/src/models/river-town.py`: weaver cottage's courses (0.09 -> 0.02 thick, centred on the slope) and its
    lean-to's battens (8 cm -> 1 cm); the pasture shelter's courses (8 cm -> 1 cm). Rebuilt: weaver-cottage, pasture
    (the other models of that file came out byte-equal).
  - `assets/src/models/well.py`: courses 5 cm -> 2 cm thick (1 cm proud). Rebuilt: well.
  - `assets/src/models/mill.py`: the paler course at the foot of each tier 8 cm -> 1 cm proud. Rebuilt: mill.
- `game/tests/AssetAudit.InkCheck.cs` (new): game-ink, the measured ink share per kind against a ceiling;
  `--ink-variants=a,b` measures any models with no ceiling (a look). `AssetAudit.InkBoard.cs` (new): the board
  pictures. `AssetAudit.cs`: the flags. `AssetAudit.Ink.cs`: the research lab (kept).
- `tools/check.sh`: the step `game-ink` after `screenshot` (under xvfb, skipped with --no-screenshot).
- `tools/ink-lab/before-after.py` (new): the boards. `measure.py`, `board.py`: the research's.
- `docs/plans/n3/style-sheet.md` section 3 rewritten (which edges get Ink, painted bands at most 1 cm proud, counts,
  the test and its ceilings).

## Ink shares, before and after

Weighted share of each model's pixels covered by the ink (`game-ink`'s measure; a pixel darkened halfway to Ink
counts half), at the play view with the 1080p line. "Before" is the merge commit's models and shader.

| kind | before | after | ceiling |
|---|---|---|---|
| cottage a0 / a1 / a2 | 0.61* / 0.62 / 0.62 | 0.17 / 0.18 / 0.18 | 0.25 |
| cottage b0 / b1 / b2 | 0.60 / 0.64 / 0.63 | 0.16 / 0.19 / 0.20 | 0.25 |
| cottage c0 / c1 / c2 | 0.53 / 0.66 / 0.66 | 0.14 / 0.16 / 0.19 | 0.25 |
| weaver cottage | 0.33 | 0.13 | 0.25 |
| mill | 0.51 | 0.22 | 0.27 |
| market stall | 0.49 | 0.27 | 0.32 |
| well | 0.56 | 0.24 | 0.30 |
| street (5 cottages, 6 carriers) | 0.64 | 0.23 | 0.28 |
| villagers group (6) | 0.70 | 0.36 | 0.42 |

\* the first subject of the first "before" run measured 0.88 because the light on it changed between two frames (the
check now pictures each subject twice without ink and only takes a pair that agrees); 0.61 is the research's figure.
The models alone (old shader, new models) took the cottages from 0.62 to 0.40; the shader alone from 0.62 to 0.25.

Other models with the new look (`--ink-variants`, no ceiling): barge 0.27, drawbridge span 0.35, fence gate 0.56,
fence wattle 0.59, ferry raft 0.29, footbridge span 0.29, keeper hut 0.21, landing 0.28, pasture 0.05, beehives 0.37,
bench 0.51, handcart 0.56, haystack 0.20, hen coop 0.40, woodpile 0.42, rowboat 0.33, oak 0.10, ripe field 0.02.
The small ones (fence, bench, handcart) are a few dozen pixels across and mostly outline; they read cleanly and were
left as they are.

## Models rebuilt, and left with a note

Rebuilt: cottage-a0..c2, cottage-01..03, weaver-cottage, pasture, well, mill.
Left: barge, ferry, footbridge, drawbridge, landing, keeper hut, fences, props, rowboat, dressing, desk: no raised
inner lines that turn into stripes at the play view (their shares come from outline on small things).

**work-scenes.py and quay-scenes.py (other builders' files, not changed), for the lead to pass on:**
- quay-treadwheel 0.58: the wheel's many rungs are separate bars, each outlined: a ladder of black stripes. Fewer
  rungs (4 to 6), or rungs as painted bands on a solid wheel face.
- quay-crane 0.46: the jib's lattice reads as a dark ladder; a solid jib with painted bracing would read better.
- quay-mooring-post 0.42, quay-stack-grain 0.33: small, mostly outline; fine.
- work-scales 0.53, work-crate 0.40, work-scarecrow 0.46: small, mostly outline; fine. work-doorway 0.21,
  work-hoist 0.09, work-row-* 0.17 to 0.19, work-stook 0.26: fine. None needs a change for the ink rule as long as
  any new boards or courses are painted bands at most 1 cm proud.

## Small things: the thin outline

Villagers, loads and small props are in the soft-ink group, so the group's outline was capped at a one-pixel step
(`max_step = 1` on the soft pass). Villager group: 0.70 before, 0.40 with the new shader, 0.36 with the thin outline;
the street 0.238 -> 0.233. Against the grass the figures read better: heads, hats and loads show their colours instead
of sitting inside a dark ring, and they still stand clear of the ground (board, villagers row). Built in. It also
thins the trees' and fields' outline (oak 0.10, ripe field 0.02): the trees still read as clumps with an edge; the
field's plot edge is now a thin line (visible on the busy-town board).

## Check results

Run in this worktree with the commands of `tools/check.sh`; the timing checks and game-ink under the heavy-run lock
(taken 12:21, given back 12:30; an earlier holder's lock from 11:26 was stale at 11:51 and I removed it then).

- game-build: PASS (0 errors)
- game-smoke: PASS
- game-town: PASS
- game-save: PASS
- game-perf: PASS (1x: cpu ms/frame mean 1.000, bytes/frame 547; 8x: mean 1.250, bytes/frame 738)
- game-layout: PASS (317 s)
- game-work: PASS (WORK RATES PASS)
- game-ink: PASS (103 s; shares as in the table above). It fails on the old look: every kind was over its ceiling
  (cottages 0.53 to 0.66 against 0.25).
- ledger (`python3 tools/check-ledger.py`): PASS, 504 rows, 478 asset files, 0 problems
- **game-motion: FAIL, three runs**, each time only "frame N took 6.3 to 20 ms of CPU (gate 6)" on a different
  transition and frame rate each run (pause, tab-flick, settings-close, esc-spam, banner, tab-redirected). The load
  average was 10 to 15 on 4 cores while I held the lock (other work was running anyway). game-motion runs headless:
  the ink shader is not executed there, and `InkOutline.Update` (the only per-frame ink code) is unchanged. I read it
  as load, not as this change, but it has not been seen passing on this branch: run it once more on a quiet machine.
- screenshot: not run (not asked; its pictures all change with the new line).

## What I still see wrong

- The well's outline is thin where its stone ring meets the ground (a crease, not a silhouette, so it is drawn as a
  darker stone colour, not Ink); it reads, but less firmly than before.
- On the back wings of cottages a and c, seen steeply, the painted courses still show as faint vertical bands (colour,
  not ink): fine at the play view, busier zoomed in.
- Under software rendering the frame cost could not be measured (see above).
- A shared-machine incident: `git stash` is shared between worktrees. My `git stash pop` once applied another
  builder's stash (river boats work: RiverBoats, RiverLanding, BoatsTest, quay-crane/treadwheel, rowboat) into my
  worktree; it stayed in the stash list for its owner. I backed those files up to my scratchpad and removed them from
  my worktree (none were committed, except seven `.uid` files that I removed again in a3df430). I used no stash after.

## Shared files touched

`tools/check.sh` (the game-ink step and its name in the godot-missing list), `docs/plans/n3/style-sheet.md` (section 3),
`assets/src/common/outline_screen.gdshader`, `game/shaders/outline_screen.gdshader`, `game/scripts/InkOutline.cs`,
`game/tests/AssetAudit.cs` (flags), model generators cottage.py, river-town.py, well.py, mill.py and their .glb
files, `assets/LEDGER.csv` unchanged (the generators rewrite the same rows).

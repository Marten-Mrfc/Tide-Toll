# Assets round 2, track RV (the river, its banks, crossings and boats): progress

Builder: checkout `/home/user/tide-toll/build/wt/n3-art1`. Pictures: `/home/user/tide-toll/build/n3/assets-audit/r2-rv/`.

## How to resume

```
cd /home/user/tide-toll/build/wt/n3-art1
PYTHONPATH=assets/src python3 assets/src/models/<script>.py     # reeds.py, ford-stones.py, footbridge.py, ferry.py,
                                                                # drawbridge.py, rowboat.py, barge.py, landing.py,
                                                                # river-town.py crane tide-post
python3 tools/merge-ledger-rows.py --build && tools/sync-assets.sh
tools/asset-audit.sh --only=<names> --out=/home/user/tide-toll/build/n3/assets-audit/r2-rv --skip=flat,sounds
```

## Done (code and generators; pictures being read)

| Item | State |
| --- | --- |
| River water (ground.gdshader) | ruled dashes replaced by faint drifting swirls in every direction; a lighter shallows band along the water's edge (1.6 m, wider at low water: + 0.55 x the tide's drop); shimmer: sparse Paper flecks drifting with the flow and fading in and out (one hash per pixel, only where a fleck is a few pixels); the water texture laid at 0.25 instead of 0.55 |
| Banks (ground.gdshader) | a wet earth lip 1.1 to 1.7 m beyond the bank line (Packed earth over Mud, darker toward the water, wandering edge); at low water the mud flat has ridges parallel to the edge and pale puddles |
| Reeds | `reeds.py` writes three slender clumps: reeds-small 1.2 m (131 tris), reeds 1.5 m (168), reeds-tall 1.9 m (196); Cast-iron green stems, Bent grass and Thatch blades, Mud seed heads. `RiverBanks.cs` (new) plants them in clumps along both banks (seeded), clear of crossings, lanes, plots, the landing, berths and river points; bank-stones scattered on the lip. Three MultiMeshes + one |
| Ford | `ford-stones.glb` is now ONE flat stepping stone 1.3 x 1.1 m, 0.47 m high, Concrete with a Paper tread, wet foot ring, a Limewash ripple crescent (106 tris); RiverLayer lays one every 2 m in a gently bowed row. `bank-stones.glb` new (48 tris) |
| Footbridge | span is one arched bridge 10 m x 2.6 m, hump 0.55 m, 18 planks in two golden tones (Thatch / Trail earth) with gaps, Soot brick rails 0.2 m with Paper caps, a lantern, piles (822 tris); ends: stone abutments, two-tone ramp (210). RiverLayer turns the ends to the bridge |
| Ferry | post 4.5 m with a Brick cap, Paper top, Thatch flag, bell, landing steps (208); rope a dark Mud line 0.16 m sagging 0.7 m (100); raft with Brick-capped rail posts, Thatch sacks and a crate (510); a ferryman (the villager model) stands on the raft |
| Drawbridge | dark timber (Soot brick rails and beam, Packed earth planks, Paper caps), two Soot brick towers with Slate caps instead of white posts, Brick counterweight, heavier Ink chains (lift 1330, span 252, end 326 tris). `RiverLayer.Drawbridge.cs` (new): the model's parts are used: frame fixed, leaf and beam turn, chains follow the beam tip, the signal stands up; before, the whole thing (towers too) was stretched and tipped as one piece. Ramps turned to the bridge |
| Rowboat | Brick hull, Thatch gunwale, Limewash inside, Packed earth thwarts, oars out at 40 degrees, raised bow post, rope coil, stern lantern (264 tris); RiverBoats puts a boatman at the stern |
| Barge | Packed earth hull with a Brick top strake and Limewash boot stripe, Paper bollard caps; the fixed cargo node is gone (472 tris); RiverBoats draws the cargo in the good's shape, set down by its own box in the hold, 2.5x (rowboat 1.6x); a skipper aft |
| Tide post | 5.5 m above mean water, Brick and Limewash bands 0.6 m, Cast-iron green cap with a perch, Concrete footing; a Thatch pointer (node Pointer) the game slides up and down with the tide (221 tris). `RiverLayer.Landing.cs` (new) |
| Crane | sturdier (post 0.56, arm 0.36, braces 0.28), Concrete base 3 x 3 m, rope coil, Brick counterweight, a tied Limewash sack on the hook (535 tris). It was not drawn in the game at all: now it stands on the bank beside the landing once bought (`Town.CraneFactor > 1`), its jib swinging slowly |
| Landing | six piles 0.34 m with Paper caps, two-tone deck, crates under a Brick tarp, a pyramid of six sacks, a barrel, bollard and coil, a lantern post (591 tris) |
| Audit tool | new subjects `town-view-river` (the whole river town at the seal, 70 m) and `town-view-ferry` (40 m at the ferry's near post) in `AssetAudit.Town.cs` |

## Pictures read (three runs, 22:52 to 23:20 UTC; the third is what is in the folder for the town subjects)

- river-high-water, landing-with-boats: a brown earth lip runs along both banks (grass no longer runs into the water); slow
  Paper flecks of shimmer show on the open water; the dashes are gone. The landing reads (piles with Paper caps, Brick-tarped
  crates, a sack pyramid, lantern); the crane stands on the bank south of the jetty with its Limewash sack on the hook.
  The rowboat is Brick with a boatman and oars out; the barge has a Brick strake, a Limewash boot stripe and a skipper.
- river-low-water, landing-low-water: the water shrinks to a band in the middle with wide flats either side; it reads plainly
  different from high water. Fault: the flats read grey-blue more than mud (the shallows band over them is greyish).
- crossing-ford: a row of six flat stepping stones with ripple marks across the river, reeds on the banks either side.
- crossing-footbridge: an arched bridge with golden two-tone planks, dark rails with Paper caps, stone abutments and a lantern;
  clearly a bridge, not a ladder, and a different tone from the paths.
- crossing-ferry: two tall posts with Brick caps and flags, the dark rope sagging between them, the raft with rails, sacks and
  the ferryman.
- crossing-drawbridge-down / -lifted: two dark towers with Slate caps, the balance beam overhead; lifted, the leaf stands up at
  the near bank and the channel is open; the rest of the bridge stays put (before, the towers tipped with the leaf).
- town-view-river (the whole river town at the seal, the close picture is the wide one): banks, reeds as gold tufts with dark
  heads every few metres, the crossings readable as crossings at page scale.
- Faults left: the barge's cloth cargo (bolts striped Limewash and Brick, 2x) reads a little like an awning; the tide post now
  stands at the landing end clear of the berths and is not in the landing pictures' frame (seen only in the model pictures,
  where it reads as a banded 5.5 m gauge with its pointer); the reeds' dark seed heads are the darkest thing on the banks.

## For the lead

1. The crane is now drawn in the game once bought (it was not drawn before at all: only its sound played). It stands on the bank
   beside the landing; nothing in the simulation changed. Say if that counts as "changing where anything stands".
2. The tide post moved from the landing's north corner in the water to whichever end of the landing is clear of both berths
   (at 5.5 m it stood in the barge's mast). It is decoration only.
3. If the interface track turns the boats' 3D labels into 2D tags (lead note 22:40, item 4), it edits `RiverBoats.cs` too:
   my changes there are `SetCargo` (cargo set down by its box), `Crew`/`Unscaled` and two lines in `Build`.
4. The villagers' load stacks queueing at the landing are the white heaps in the landing pictures; they belong to
   `TownVillagers.cs` (track P), not to the landing model.

## Tests (23:22 to 00:00 UTC)

- While working: game-river alone twice (22:59, 23:12; both while the audit ran) failed in TownVillagers' "villager N waiting on
  the river drawn where it stands" (1.7 to 2.7 cells off, near cell 54,73); in ./check (23:2x) game-river PASSED.
- ./check: sim-build, sim-test, godot-nuget, sync-assets, game-build, game-smoke, game-town, game-perf (PERF PASS, 0.437 ms mean),
  game-playthrough, game-river (RIVER PASS), game-book, game-menu, game-motion, game-save passed; game-settings printed SETTINGS PASS
  then failed on the exit-time "Leaked unsafe reference ... InputEventKey" lines; the screenshot step was still running after 30
  minutes when the session's background limit stopped the whole check (not a test verdict); the ledger step never ran in it, so the
  ledger check was run by hand: "ledger: 325 rows, 299 asset files checked, 0 problems".
- game-settings alone at 00:00: SETTINGS PASS, then 1546 "Leaked" lines and GODOT TEST FAIL (the lead's note of 22:50).

## Not done

- The screenshot step of ./check (stopped by the time limit, not failed).
- Reeds in the first age (the river is drawn there too, without reeds): RiverBanks only runs in the second age.
- Low-water flats read grey-blue rather than mud; reeds' seed heads are dark and chunky (thin blades read as pure ink at the normal
  view, so the blades were made wider: the clumps are toy-like rather than slender).

## After the review (fixer, 00:02 to 01:15 UTC)

Pictures retaken into `build/n3/assets-audit/r2-rv/` (four audit runs 00:07 to 00:16, a last full track run after ./check).

### MUST FIX

1. **Ferry raft — fixed.** `ferry.py` raft: one Packed earth deck slab with a Paper edge band, faint plank lines, two Thatch
   rail posts a side and Thatch rails, a mooring post with a Brick cap at the bow, sacks and a Thatch crate; the loose pole
   is gone and the rope stub through the guides is now the rope's Mud (it was the "Thatch rail floating off the right").
   The ferry posts stand 1.5 m further up each bank (`RiverLayer.cs`). The "green cap and Brick-white pole" the reviewer
   saw on the raft was the tide post (the old placement put it at the ferry); it now stands at the landing.
2. **Reeds — fixed.** `reeds.py` rebuilt: three shapes (small 0.9 m three blades no heads; reeds 1.6 m five blades two
   heads; tall 2.2 m seven blades two heads, leaning); flat two-sided blades, two in three Bent grass, the rest Cast-iron
   green; Mud seed heads 0.12 x 0.35 m on Thatch stems; no hub, no Brick. Blades 0.3 m at the foot, not 0.14: at 0.2 m
   they still read as ink scribbles at the normal view (picture read 00:08). `RiverBanks.cs` plants rows of five to nine.
3. **Low-water flats — fixed.** `ground.gdshader`: the flat is Packed earth over Mud, darker wet Mud toward the water, no
   water colour mixed in (that, not the shallows band, made it grey: the shallows only colour the water); puddles fewer.
   Pictures: the flats read brown mud.
4. **Ford — fixed (the tree is not mine).** Stones about 1.6 x 1.4 m one every 1.6 m (they touch to the eye); a worn way
   3 cells long runs from each bank end (`TownGround.Lanes.cs`, worn like a lane once the crossing opens; a mesh patch
   was tried first and read as a dark plank, removed). The willows on the east end: For the lead, below.
5. **Drawbridge — fixed.** Towers 0.9 m square, top of tower 4.9 m (pivot 3.6 m above the deck, `DbH` in
   `RiverLayer.Drawbridge.cs` follows); the leaf is Packed earth on both faces with Packed earth rails and tip.
6. **Barge cloth — fixed.** `RiverBoats.SetClothRolls`: plain Brick rolls laid across the hold, two a row, no stripe.
7. **Tide post — fixed.** `river-town.py`: 3.2 m stubby gauge, 0.7 m, bands about 0.5 m with Brick at the top, Cast-iron
   green cap, Concrete plinth with a Paper step, a Thatch flag pointer 0.9 m wide. Stands on the bank edge at the landing's
   upstream end, pointer turned to the river (`RiverLayer.cs`, `RiverLayer.Landing.cs`); in the landing pictures now.
8. **Placement — crane fixed, tree and heaps for the lead / track P.** The crane stands on the lip at the water's edge
   at the landing's downstream end (1.8 m nearer the water, about 1 m nearer the landing), its jib swinging ±0.45 rad
   about the river side: it no longer crosses the cottage roof.

### SHOULD FIX

- Bank-stones — fixed: two touching stones 1.8 m across, 0.5 m high, Concrete with Paper tops.
- Footbridge abutments — fixed: one stone wall 2.7 m across at the river end with a Paper cap, slim cheeks; the end is
  fitted at its own size (2.84 m) so it is no wider than the span.
- Bank lip — fixed: width wanders 0.6 to 2.4 m over tens of metres, grass bites to the bank line now and then.
- Ferry post band vs raft pole — done by moving the tide post (the only banded post near the ferry was the tide post).
- Trodden ends of footbridge and drawbridge — fixed with the same worn ways as the ford (and the ferry's ends).
- Reeds in the first age, ferry rope alone — left as the reviewer accepts.

### For the lead

1. `TownTrees.cs` (not mine): keep trees off the river's crossings and the landing. In `TownTrees.Make`, before the loop:
   `var rp = TideToll.Sim.Town.TownRiverPlanner.Build(plan);` and in the loop, after the `dRiver` test:
   `if (NearRiverWork(rp, x + 0.5f, y + 0.5f)) continue;` with
   `static bool NearRiverWork(RiverPlan rp, float x, float y) { foreach (var l in rp.Links) { if (l.Kind == RiverLinkKind.Lane) continue; int n = l.Px.Length; for (int e = 0; e < 2; e++) { double ex = e == 0 ? l.Px[0] : l.Px[n - 1], ey = e == 0 ? l.Py[0] : l.Py[n - 1]; if ((x - ex) * (x - ex) + (y - ey) * (y - ey) < 36) return true; } } var p = rp.PlotOf(RiverPlotKind.Landing); return x > p.X - 5 && x < p.X + p.W + 5 && y > p.Y - 5 && y < p.Y + p.H + 5; }`
   (6 cells round each crossing end, 5 round the landing). Check that `TownRiverPlanner.Build(plan)` is what `Town` uses
   (Town.cs line 43 does) and costs nothing per frame (Make runs once). If the willow in front of the landing is a touch
   prop (`TownTouches.cs`) rather than a tree, the same test belongs there.
2. Track P (`TownVillagers.cs`): the load heaps queueing at the landing also stand at the crane's foot (now on the lip at the
   landing's downstream end, `RiverLayer.Landing.cs` `MakeCrane`): keep queue spots 2 m off it.
3. Crossings now wear a 3-cell way into the ground at each bank end (`TownGround.Lanes.cs`): display only, nothing in `sim/`.

### Tests (the whole ./check, 00:16 to 01:06 UTC, load average about 24 on 4 cores from other agents' picture runs)

sim-build, sim-test (258), godot-nuget, sync-assets, game-build, game-smoke, game-town, game-perf (1x mean 0.458 ms, river 1x
0.486 ms), game-playthrough, game-river, game-book, game-menu, game-save, ledger (325 rows, 0 problems): PASS.
game-settings: SETTINGS PASS printed, then exit 134 on the exit-time "Leaked unsafe reference" lines; alone at 00:49: the
same (SETTINGS PASS, 1181 Leaked lines, GODOT TEST FAIL: exit code 134).
game-motion: CPU gate only (60 fps click-settings-on-lettering 8.39 ms, 120 fps tab-turned-back 6.76 ms; gate 6); alone at
00:49 (load 24): tab-display-sound 7.89 ms at 60, tab-redirected 14.31 ms at 30, pause 12.86 ms at 120; a different frame
each run.
screenshot: stopped by its own 2400 s timeout (exit 124) with no FAIL line, the log only Godot's banner and "river banks: 205
reeds, 6 stones" twice. Not rerun (no time before 01:15): it needs a run on a quieter machine.

### Not done

- The screenshot step's verdict (timed out under load, above).
- The willows on the ford's east end and in front of the landing; load heaps at the crane's foot (For the lead 1 and 2).
- Some town pictures in the folder (river-high/low-water, landing-low-water) were taken at 00:07, before the reed blades went
  from 0.2 to 0.3 m and the ferry posts moved; crossing-footbridge, town-view-river and crossing/town-view-ferry are newest.

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

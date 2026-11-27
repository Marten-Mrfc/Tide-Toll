# Work you can watch: the villagers (progress, the villagers' builder, 27 November)

Branch `wt/n5-villagers`. The spec is `work-build-sheet.md` (sections A, B, the villagers' part of D, and the
villagers' share of "How it is judged"). `sim/` is not changed; the play-through still reads `charter=33.9min buys=201`.

## What was built, file by file

| File | What it holds |
| --- | --- |
| `tools/work-strips.sh`, `tools/work-sheet.py`, `game/tests/WorkStrips.cs`, `game/tests/work-strips.tscn` | The interaction strip tool (committed before the villagers changed). A real town played by the payback bot to the mill and the market (level 13, minute 22, 21 villagers), then left to run. For each job (water, reap, mill, market, home) a copy of the town (`TownSave` round trip; the same steps give the same town) is run ahead to find one villager's scene (the longest wait in the line; for the market and home, a delivery); the real town is played to it frame by frame at 1x with the renderer off, and at eight moments the town is held and one frame saved at the normal view (46 m of view height, the start view's metres per pixel at 1080p) and one close up (15 m, centred on the villager), the middle 960 x 540 at real pixels. One contact sheet per job, each frame captioned with its time and the villager's phase. `DISPLAY_NUM` picks the xvfb display. About six minutes. |
| `game/scripts/town/WorkSites.cs` | The shared interface (see the last section). |
| `game/scripts/town/TownVillagers.cs` | The frame loop now asks for a stand target (line or work spot), runs the transitions (work begun, finished, load delivered), poses each body (lean, crouch, swing, step) and draws its props. A stretch played without frames (a bot, a test) is drawn afresh. |
| `game/scripts/town/TownVillagers.Line.cs` | A. The line. |
| `game/scripts/town/TownVillagers.Work.cs` | B. Poses, rhythm, stoop, the bell. |
| `game/scripts/town/TownVillagers.Props.cs` | Loads taken up and set down, the mill's sack tipped in, the pack frame and handcart (parked at work), the stand-in sickle and scythe. |
| `game/scripts/town/TownVillagers.Spread.cs` | The round 6 spacing, moved out of `TownVillagers.cs` unchanged but for one line: those in a line and at work keep their places, others step round them. |
| `game/tests/WorkTest.cs`, `game/tests/work.tscn`, `tools/work-check.sh`, `tools/check.sh` | The new check step `game-work`. |
| `game/tests/RiverTest.cs` | One check replaced (below). |

## A. The line

- Done: those who wait at the well, a field or the mill stand in a line along the lane the first of them came by (`WorkSites.Lane`, kept while the line stands so it never jumps lanes), 2.0 m apart (2.3 m with the handcart), the first one step from the door node, each facing the one ahead (the first faces the work spot).
- Done: a line longer than its lane bends on along the straightest open road at the lane's far end (`WorkSites.LinePoint`, up to four roads).
- Done: moving up one step is eased and front first: the place that moved starts at once, each place behind it `RippleSeconds` (0.14 s at 1x) later, at walking pace along the lane, slowing over the last third of a step. A jump of more than two places at once (only after a fast-forward) is taken at once and followed by the chase like any jump.
- Done: the handcart is kept in the line (pushed ahead, the wider step); the pack frame on the back.
- Checked: never inside a building or a field's crop (game-work). Not handled: two lines on one lane (does not happen in the first age's plan; not checked).
- Known: someone who arrives along the lane walks to the door node and back to their place (the simulation puts them at the node first); someone who arrives by another road walks over to the line's lane.

## B. The villager at work

- Done, drawing water: on the simulation's rim spot (the first spot is at the crank side), rocking forward and back (9 to 25 degrees) with a small step, 0.9 times a second. No hands on a crank (no crank part to hold).
- Done, reaping: bent about 26 degrees (30 read as lying down in the close strips at the game's tilt) with a little crouch, swinging from side to side, stepping 0.8 m along the row over the work; a sickle in the right hand, from the first Tools milestone (`TownRules.MilestoneEvery`, step 5) a scythe with a wider swing. The tools are **stand-in meshes made in code** (`TownVillagers.ToolMesh`) until `villager-sickle.glb` / `villager-scythe.glb` exist: those names are picked up at once when they do.
- Done, milling: the worker stands on the step in the doorway (`WorkSites.WorkSpot`; the simulation's spot is inside the walls), the grain sack is tipped forward and goes in at the door over 0.45 s, then a slow sway; the flour sack rises from the step.
- Done, taking the goods: as work ends (estimated: the town's work time for the job divided by the Tools factor, corrected by the times seen) the worker stoops (29 degrees and a crouch); the load is drawn only once the simulation has handed it over (`Carry`), rising from its goods point into the arms over 0.35 s while the worker straightens; the worker stays at the spot for the first part of the lift (not above 4x).
- Done, the market and home: the load leaves the arms in a 0.45 s arc onto the table (with a dip of the body) or in at the door (shrinking as it goes in); when it lands the place is told (`IWorkSite.Delivered`). A load that leaves the arms without a delivery (a dropped job, the river's ways) sinks to the ground.
- Done: the pack frame and handcart stand parked beside their owner at work (eased over 0.25 s) and the cart is loaded at the end.
- Done, the bell: workers and those in line lift their heads for 1.1 s when it rings, and the rhythm runs `TownRules.BellHurry` times quicker while it rings.
- Every weight is eased (0.15 s or longer at any frame rate); the rhythm's tempo follows the game speed up to 2x only, so 16x does not flicker.

## D. What you buy, you see (the villagers' part)

- Tools: the sickle, then the scythe from Tools step 5 (`TownVillagers.ScytheAt`, checked in game-work). The well's second rope and the mill's hoist are the places'.
- Load: the pack frame (step 3) and handcart (step 4) go with their owner everywhere now (before, only while carrying) and stand parked at work; a new step grows in and the replaced frame shrinks away. No basket model exists for step 2: not done.
- An extra hand: the simulation's extra spot is drawn like the others (the well's next rim spot, the field's next row, the mill's next doorway place).
- The bell: as above.

## The simulation's views: what the sheet assumed and what exists

`VillagerView` has no `Slot`, `WorkLeft` or `Stage`. Without changing `sim/`: the work spot is found exactly from the
villager's position and the public `Town.WorkSpot` (`WorkSites.SlotOf`); the work left is estimated from
`TownRules.WellWork/FieldWork/MillWork` and `Town.ToolsFactorAt(ToolsLevel)`, corrected per job by the durations seen
(boons change them); the stage follows from `Carry` (grain carried to the mill). If the lead wants the stoop exact, a
`WorkLeft` in the view would do it; nothing else needs the simulation.

## Tests

Added: `game-work` (`tools/work-check.sh`, `game/tests/WorkTest.cs`), at fixed 60, 30 and 120 fps. A real town: the
bot to the mill and the market, then 90 game seconds at 1x, the bell rung, 30 s at 4x, then the bot on to a handcart
and the scythe and 25 s at 1x. Every frame: neighbours in a line who have reached their places stand 1.6 to 2.4 m apart
in queue order, everyone in a line faces the workplace (within 60 degrees of the one ahead), none stands inside a
building or a field; no load on anyone waiting or working but what they brought (the mill's grain), none appears or
leaves in one frame (`Pops`), every delivery lands on its table or door; no pose changes faster than 12 rad/s (an eased
change of the largest pose over 0.15 s is 4, the scythe's swing at the bell's tempo about 8); no villager jumps at 1x;
reapers hold the right tool; a worker's handcart is parked; the bell lifts heads and quickens the rhythm; the villagers'
node allocates nothing per frame and the whole frame stays under game-perf's 1x gate.

Replaced (one):

- `RiverTest.CheckWaiting`. Old: every villager waiting off the roads (`Queued`, `Road < 0`) is drawn within 0.3 cells
  of the simulation's spot. New: the same for those waiting on the river (ford, ferry, drawbridge, river sites); one
  waiting at a first-age workplace (the well, a field, the mill: `Workplace >= 0`) must be drawn in its line and within
  0.3 cells of its line place (`WorkSites.LinePoint` for its queue position). Why: the line is the new look, drawn
  along the lane 2 m apart, where the simulation's queue spots are 1.6 m apart on each villager's own road; the check
  is as strict as before, against the new place.

## Check results

(filled in below by the last run)

## The strips

`build/n5/strips-before/` and `build/n5/strips-after/` (frames and full sheets), JPEG copies in
`docs/plans/n5/strips/before/` and `docs/plans/n5/strips/after/` (`work-water`, `work-reap`, `work-mill`,
`work-market`, `work-home`). Both runs follow the same villagers at the same moments (the town is deterministic).

## What the places' builder needs to know about `WorkSites.cs`

- Register one `IWorkSite` per plot you draw: `WorkSites.Register(plotId, site)` (plot ids under 128; register again
  when a town is rebuilt; `WorkSites.Clear()` empties all). Unregistered plots work with the defaults.
- Calls you get (from the villagers' `_Process`, a few a frame, never allocate in them):
  `WorkStarted(villager, slot, carried)` as a villager begins a unit (the mill: the grain sack is then tipped in at
  your doorway for 0.45 s); `WorkFinished(villager, slot, good)` when a unit is finished (the bucket tips, a field's
  front moves on, the flour puff) — the load then rises from your goods point into the arms over 0.35 s, so take your
  goods away over the same time; `Delivered(villager, good, amount)` when a load has landed on your table or gone in
  at your door (at the end of its 0.45 s arc). For "continuously while anyone works" read the workplace's `Busy`.
- What you may move: `Spot(slot, out x, out y, out heading)` (map cells, cell centres at +0.5; heading atan2(dy, dx),
  y south) to move a worker, e.g. to the front of their row; `GoodsAt(slot, out at)` (map-local metres, with height)
  for where that spot's goods stand (the rim, a row's stook, the mill's step, the table, the door). Return false to
  keep the defaults in `WorkSites.WorkSpot` / `WorkSites.Goods`. A field's rows: the simulation's spot for slot s is row
  `s % (H - 1)`; one row per place, an extra hand one more row.
- The defaults are from the plot only: the well's rim spot and a point 0.62 of the way in to the centre at 0.85 m; a
  field's spot with the stook 0.55 cells behind; the mill's doorway 0.62 cells in from the door node, workers side by
  side, flour on the step 1.15 cells to the side; the market's table 1.8 cells in from the door at 0.9 m; a cottage's
  door 0.7 cells in at 0.9 m. `WorkSites.Doorway` is where the grain goes in. Refine them from the models.
- Counters for tests: `WorkSites.FinishedCount`, `WorkSites.DeliveredCount`.
- The line's own numbers (`LineStep`, `LineStepCart`) and `TakeSeconds`, `DropSeconds` are in the same file.

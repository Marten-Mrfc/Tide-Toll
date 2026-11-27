# Boats, the landing and the crane: the build sheet (the lead, 27 November)

The owner on the boards: "Okay start all of them implement them." The design is `boats-design.md`; the builder of the boards left what a builder must know in `mockup/notes.md`; the prototype of the course is `tools/mockup/boat_course.py`; thirteen quay models exist (`assets/src/models/quay-scenes.py`). This sheet adds the decisions and the tests.

## Decisions (the lead's, for the owner)

- **The simulation does not change.** Rules, prices, timing, berths, the crane's effect and the save stay. The simulation's place of a boat along the river stays the truth; the drawing follows it on a course of its own.
- **The river is not widened** (it is 8 m; widening it would change every town's map). So: a moored boat ships its oars; a boat passing a moored barge keeps the middle of what is left; the two lanes are as far apart as the river allows.
- **The drawn berths stand a barge length apart** along the quay (about 9 m), although the simulation's are 6 m apart: a drawn boat may lie up to 1.5 cells from the simulation's berth.
- **How far the drawn boat may be from the simulation's:** across the river about a cell; along it one boat length while sailing, two cells in the S to the quay, and up to five cells in the turn after leaving, caught up within six seconds. It is never ahead of the simulation.
- **Signs replace the text over the water.** The floating label goes; each berth's sign shows the good (its icon), a bar of how much is aboard, and the bar's colour for the time left; a request boat's sign carries the skipper's mark. The words stay available in a tooltip on the sign and in the ledger's request.

## What is built

1. **The course** (`RiverBoats.cs` and new files beside it): the designed course of section A of the design, ported from the prototype: a smooth middle line, two lanes, eased speed, the bow along the course with a limit on how fast it turns, a little lean in the bends. Holding station below the drawbridge instead of a dead stop.
2. **Alive on the water:** bob and roll; a rowboat's oars sweep and the hull surges a little with each stroke (the oars become separate parts of the model if they are not); a barge's two sweeps; a V wake that grows with speed and fades in and out; a loaded boat lower in the water.
3. **Coming in, lying, going away, waiting** (section B): the S, the mooring line to the bollard, the gangplank; riding on the line, lower at low water; line in, push off, the turning arc, away down the far lane, starting the preparations when leaving can be seen coming (patience nearly out, or one load short of full); waiting boats at mooring posts out of the lanes. A full boat flies a pennant; one that gave up rides high.
4. **The quay** in the landing's plot instead of the jetty, with the yard, a stack per kind of good, the bollards and ladders, the tide post where it is.
5. **Loading you can see:** each load a villager brings is set down on its stack and goes aboard: by the crane when the town has one (a lift per load: net up from the stack, swing over the hold, down, back; the treadwheel turning while it works, still when there is nothing to load; when loads come faster than a swing, more in each net: never behind, never empty), by hand across the gangplank when it has none. The hold fills as now. Each crane step bought shows on the crane.
6. **Villagers at the landing** wait in a line and set their goods down at the yard, through the interface the villagers already have (`game/scripts/town/WorkSites.cs`: register the landing as a site so a delivery's arc lands on the stack), as far as that interface carries it; say what it does not carry.

## Limits

`docs/plans/builders-rules.md` holds the rules for everyone. In addition: nothing in the first age changes (the play-through still reads `charter=33.9min buys=201`); `game-river` passes with no check changed except those that only fitted the floating label, each written down.

## How it is judged

1. **Boat strips** (a new tool like `tools/work-strips.sh`): twelve frames of one boat's visit and of one boat that has to wait, at the normal view and close up, on the game before this work and after it.
2. **A new check step `game-boats`**, on a real second age, sampled every frame at 30, 60 and 120 frames a second: no boat moves or turns more than a set amount between two frames (the prototype's bounds: about a third of a cell and ten degrees a tick); no two hulls overlap; no hull touches the quay, a bank or a crossing that is down; the drawn boat is never ahead of the simulation and within the distances above; the crane's lifts add up to the loads taken aboard; nothing changes in one frame; the boats' and the landing's nodes allocate nothing per frame; the frame stays inside `game-perf`'s gates.
3. The strips are read by a reviewer who did not build them; then the whole check on a clean copy and a Windows build.

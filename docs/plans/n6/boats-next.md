# Boats: where they stand and how to finish them (the lead, 27 November, 19:35 UTC)

**The boats are stopped for today. Nothing of them is merged.** Eight rounds went into one question, who gives way to whom, and the answer kept failing for a reason that was only measured at the end: the river is too narrow for the boats as they are drawn. This page is for whoever takes it up next.

## What exists (branch `wt/n6-boats-build`, checkout `build/wt/n6-boats-build`; notes `docs/plans/n6/boats-progress.md` there, rounds 1 to 8)

- **The last whole state is round 6, commit `43b596b`.** On it every check passes except the boats' own: `game-river`, `game-playthrough` (`charter=33.9min buys=201`), smoke, town, perf, save, layout, work, people, ink, motion pass; `sim.tests` 298 of 303.
- **What that state shows:** boats on a smooth course with way gathered and lost, oars, bob and wake; coming alongside, lying moored, going out astern and swinging round; the quay with yard, stacks and signs; the crane lifting per load; mooring posts below the drawbridge; one boat under way at the landing at a time (a rule of the simulation, proved tick by tick); the leaf up before a hull passes and villagers kept off.
- **What fails on it:** `game-boats` (97 frames at 60 a second of two barges' hulls overlapping while they pass each other under way on slanted water; 10 frames of a barge's corner 0.07 cells into a bank), and `RigidHullsNeverTouch` on 5 of 10 runs (the same passing; a barge passing a rowboat at a post; a rowboat passing a barge outside the reach).
- **Rounds 7 and 8 (commits `8472356`, `91840f4`, `d074a70`) are a broken intermediate state:** round 7 tried one boat under way on the whole river (cannot hold: a barge cannot pass a barge moored at a three-cell quay); round 8 went back to round 6's rule with the barge's beam at 1.0 and a static proof (`Town.Proof`, `PlaceRoom`, `PassRoom`), which still fails on 8 of 10 seeds; the tests there are not rewired and the drawing is not adapted.

## What was learned (the numbers are in the progress page)

1. **A drawing that decides who waits cannot agree with a simulation that does not know it** (rounds 1 to 4). The rule belongs in the simulation; the drawing only follows. That part is done and right.
2. **The river is 4 cells across the map, 3 by the quay on six seeds of ten, and less square to the flow where it runs aslant; its banks are stair steps.** A barge was 4.25 by 1.44 cells. Two rigid barges with the drawing's room need about 3.2 cells square to the flow; a moored barge and a passing one at the quay need 3.7.
3. **The lanes were laid off the river's smoothed middle, which is not the middle of the water** at the quay and in the lowest 25 rows toward the mouth; that is where the proof is short by up to 0.6 cells even with a barge 1.0 wide.
4. **Proof first.** The static proof (every place and every row, both kinds of boat, rigid hulls, both banks) says in seconds what a watch of the game says in minutes and only sometimes. Nothing should be built on the drawing again until that proof passes on seeds 1 to 10 (better: 1 to 100, as the plan's own test does).

## The rule to try next (the lead's proposal; check it with the proof before any drawing)

- **Nobody passes anybody under way.** Two rights of way: the landing reach (as now) and the lower river from the gate to the mouth; at most one boat under way in each. A boat coming in holds the lower river to the gate, lies at the gate, and takes the reach when it is free; a boat going out takes the reach and the lower river together, passes the boat lying at the gate, and leaves. Going out before coming in. So the only things that must fit are a mover past a boat that lies still: at a berth, at a post, at the gate.
- **The mover's line is laid off the banks, not off a middle:** along the stretch where boats lie, hard against the far bank (the drawing's margin off it); elsewhere down the water's own middle.
- **The places are chosen by the proof:** the gate and the posts on the first rows where a lying boat of either kind and a mover of either kind clear each other and both banks.
- **The barge is slimmer:** 1.0 cell of beam (a long river barge), in the simulation and as a rebuilt model (not a squeezed one). At a three-cell quay: 0.35 + 1.0 + 0.14 for the moored barge, 1.0 + 0.15 for the mover, 0.2 of room: 2.84 of 3.
- **If the proof still fails on a seed,** the choice is between a rowboat-only upper berth on that map, a shorter barge, and moving the quay to four-cell water (which changes the plan and old saves); that choice is the owner's.
- Then, and only then: the drawing follows the simulation's places and line; `game-boats` whole at 30, 60 and 120; the strips; `bands` (the tolls' `TollStep` if their band is thin); a reviewer who did not build it.

## What the lead got wrong

- Told the builders at the start to leave the simulation alone, which cost four rounds.
- Then changed the rule three times (one right of way, the gate on straight water, one mover on the whole river) without asking for the static proof first; the proof was the builder's idea in round 6 and settled in ten minutes what three rounds had not.

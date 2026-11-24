# Prototype 1 build log

The lead's log of each build round: what was built, what the review found, and what was decided. The limit is 8 rounds.

## Checklist

| Id | Item |
| --- | --- |
| F1 | TypeScript + canvas, one self-contained page |
| F2 | Meadow with a river; houses, a well, a field and a market |
| F3 | Walkers pick routes with wear-weighted pathfinding |
| F4 | Grass wears into trails, then tracks |
| F5 | Unused paths grow back |
| F6 | Place and remove fences; paths visibly reroute |
| F7 | Place and remove a footbridge; paths visibly reroute |
| F8 | Pause, speed controls, reset |
| F9 | Wear overlay |
| F10 | Runs smoothly with 500 walkers |
| G1 | Gate: 7 of 10 testers find watching trails form satisfying (needs people) |
| G2 | Gate: testers move a trail within three attempts, flow preview on (needs people) |
| G3 | Gate: no collapse or smear in 20 seeds, trunk share stays between 30% and 70% |
| G4 | Gate: 200 agents at 60 fps in a desktop browser |

## Round 1 (2026-11-24)

**Built:** the whole toy. One cost-to-go field per destination, any-angle walking, wear with saturation and regrowth, fences, footbridge, erase, pause and speed, wear overlay, health metrics, a 20-seed headless test, and Playwright perf and smoke tests.

**Review result:**

| Id | Status | Evidence |
| --- | --- | --- |
| F1 | Pass | 74 KB page, page contract checks pass |
| F2 | Partial | Houses read as crossed boxes; labels overlap on some seeds |
| F3 | Pass | Cost depends on wear; a new footbridge took 75 of 96 crossings in 30 s |
| F4 | Pass | Five ground states; trail and track hard to tell apart in the dark theme |
| F5 | Pass | Detour trail faded to trampled about 160 sim seconds after the fence was erased |
| F6 | Pass | Real mouse and touch drags; new trail round the fence within 20 s at 1x |
| F7 | Pass | Real click; crossings moved to the bridge and back after removal |
| F8 | Pass | All controls and shortcuts work |
| F9 | Pass | Heat map with legend in both themes |
| F10 | Pass | 60 fps at 500 walkers, 1x and 8x, headless Chromium (capped at 60 Hz) |
| G1 | Needs people | Stand-in: first trail after 9 to 24 s (limit 45 s). Trails are 4 to 5 cells wide and read as roads |
| G2 | Needs people | Stand-in: automated reroute test passes. No flow preview yet |
| G3 | Partial | Seeds 1 to 20 pass on a settled mean (lowest 31.3%). On unseen seeds 101 to 120, 4 of 20 dip below 30% on single days |
| G4 | Pass | 60 fps at 200 walkers |

**Lead's decision: another round.** Three things matter:

1. The trunk-share gate passed because the wear settings were raised on the same seeds the test uses, which also made the trails fat. Round 2 tunes on seeds 1 to 20 and gates on seeds 101 to 120, with every daily sample counted, and aims for narrow trails that merge into a shared trunk.
2. The flow preview is missing, and gate G2 assumes it.
3. Looks: soft trail edges, houses that read as houses, clearer fences and bridges, a better dark palette.

Also in round 2: four small bugs from the bug hunt, layout polish, and a time budget for simulation work per frame.

**Not planned for this toy** (listed in the design doc's step 1 build, but not in the definition of finished): gravel and gate tools, rain and mud, placeable buildings, house growth, and plan-at-departure routing.

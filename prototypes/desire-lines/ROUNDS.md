# Prototype 1 build log

The lead's log of each build round: what was built, what the review found, and what was decided. The toy's rounds count toward the overnight build's limit of 20 rounds (see `docs/overnight-log.md`).

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
| F11 | Gravel and gate tools (added in round 3) |
| F12 | Rain and mud, house growth, sound made in code (added in round 4) |

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

**Added to the plan later that evening** (the overnight brief asks for the rest of the design doc's step 1 build list): gravel and gate tools, rain and mud, house growth, and sound made in code.

## Round 2 (2026-11-24)

**Built:** a new trail model (worn ground only gets cheap where several households share it, and busy lanes attract walkers, so house paths merge into a trunk), the flow preview with ghost routes and a readout, soft-edged trail drawing, cottages with roofs, clearer fences and a wider footbridge, the four bug fixes, a time budget for simulation work, and a gate test that runs on seeds the model was not tuned on.

**Review result:**

| Id | Status | Evidence |
| --- | --- | --- |
| F1 | Pass | 103 KB page, page contract checks pass |
| F2 | Pass | Cottages read as houses; labels keep clear of buildings |
| F3 | Pass | Reroute and bridge tests pass; a new bridge took 58 of 94 crossings in 30 s |
| F4 | Pass | Ground states are distinct in the light theme; track and hollow way are too alike in the dark theme |
| F5 | Pass | Trails fade after a fence or bridge is removed |
| F6 | Pass | 49 newly worn cells formed within 20 s, all within 3 cells of the previewed route |
| F7 | Partial | Works with a mouse. At 400 px a touch press was cancelled because the map jumped 20 px when the readout appeared |
| F8 | Pass | All controls and shortcuts work |
| F9 | Pass | Heat map with legend in both themes |
| F10 | Pass | 60 fps at 500 walkers at 1x and 8x |
| G1 | Needs people | Stand-in: first trail in 8 to 12 s (limit 45 s); trunk about 3 cells wide at the median, soft edges, paths merge |
| G2 | Needs people | Stand-in: reroute test passes; the preview is honest (90 of 90 predicted points had new trail within 3 cells) |
| G3 | Fail | Held-out seeds 101 to 120: 19 of 20 stay in band every day; seed 115 dips to 28.6% on 3 days. Unseen seeds 201 to 220 and 301 to 320 pass 40 of 40 |
| G4 | Pass | 60 fps at 200 walkers |

**Lead's decision: another round.**

1. G3 needs real headroom, not a different seed set. Round 3 retunes on every set looked at so far (1 to 20, 101 to 120, 201 to 220) with the footbridge test as a second goal, then judges the gate on fresh seeds 401 to 420. The older sets must also stay in band.
2. The map must not move when the status text changes, and touch must work at phone width.
3. Gravel and gate tools join the kit. Sound, rain and mud, and house growth follow in round 4.
4. Polish from the review: fences drawn as rails, ghost routes that show only what changes, a clearer dark palette, a less road-like track colour.

## Round 3 (2026-11-25)

**Built:** a retuned trail model with real room in the trunk-share gate and working footbridges (worn ground is cheaper; walkers on long detours and one villager in ten ignore most of the discount, so new shortcuts still get found), a flow preview that models both kinds of walker, gravel and gate tools, fences drawn as rails, clearer ghost routes, a new dark palette, softer worn earth, a fixed-height status bar so the map never moves, touch input that works at phone width, and all five bug-hunt fixes. The route search is about twice as fast, which pays for the extra work.

**Review result:**

| Id | Status | Evidence |
| --- | --- | --- |
| F1 | Pass | 130 KB page, page contract checks pass |
| F2 | Pass | Houses, well, field, market and ford are distinct and labelled |
| F3 | Pass | Routes respond to ground, wear and traffic |
| F4 | Pass | Ground states are distinct in both themes |
| F5 | Pass | With no walkers, track cells fall from wear 84 to 14 in 24 days |
| F6 | Pass | Mouse and touch drags; a new trail forms round the fence end within 20 s |
| F7 | Pass | Works with mouse and with touch at 400 and 360 px; the map does not move |
| F8 | Pass | All controls and shortcuts work |
| F9 | Pass | Heat map with legend in both themes |
| F10 | Pass | 60 fps at 500 walkers at 1x and 8x on an idle machine; stalls up to 100 ms appear when the machine is busy or throttled 4x |
| F11 | Partial | Both tools work, but a gate cannot be placed on most of a diagonal fence, and gravel only pulls a trail that is within about 2 cells |
| G1 | Needs people | Stand-in: first trail in 8 to 13 s; trunk about 3 cells wide; trails merge. Mature trails still read a little like dirt roads |
| G2 | Needs people | Stand-in: reroute test passes; previews leave the simulation untouched; 77% to 84% of predicted detour points get a new trail |
| G3 | Pass | All five seed sets pass with every daily reading counted, including fresh seeds 401 to 420 (lowest 34.9%). The reviewer's own run on seeds 501 to 520: lowest 33.5%, highest 54.7%, mean 44.1% |
| G4 | Pass | 60 fps at 200 walkers |

Footbridges: every bridge that saves 8% or more of the walk takes at least half the crossings by the third day. On maps where the bridge saves under 5% it takes 24% to 45%.

**Lead's decision: accepted and republished. One more build round, then a polish round.**

1. Fix the two tool defects: a gate must go on any fence cell, and gravel must either reach further or say plainly when it is too far from any route to matter.
2. Add the rest of the build list: rain and mud, house growth, and sound made in code (off until the Sound button is pressed).
3. From the bug hunt: a second finger must not disturb a drag or a press; a fence over gravel; a gate on a lone post; the phone test must run at a real phone layout.
4. Polish: diagonal fences as straight rails, gravel as a clearer strip, trunks darker than side trails, 40 px speed buttons on phones, small wording fixes, and smoother frames at 8x on slow machines.
5. From round 4 on, the gate is also judged with rain and house growth switched on, on seeds nobody has looked at (601 to 620).

## Round 4 (2026-11-25)

**Built:** gates on fences at any angle, gravel that pulls a trail from several cells away, rain and mud, house growth by the frontage rules (one cottage every 2 days up to 16 houses and 248 villagers), sound made in code and silent until the Sound button is pressed (footsteps by ground type, river, rain, a dawn bell, tool sounds, the 8-bar theme), each touch gesture bound to its own finger, a phone test that runs at a true 400 and 360 px layout, straight rails for diagonal fences, darker trunks and lighter side trails. Two container restarts interrupted the builders; the work was picked up from the tree each time.

**Review result:**

| Id | Status | Evidence |
| --- | --- | --- |
| F1 | Pass | 216 KB page, page contract checks pass, no audio before the Sound press |
| F2 to F10 | Pass | `npm run check` (84 checks), smoke 3 of 3, perf 3 of 3 on an idle machine |
| F11 | Pass | A gate carries walkers at 0, 30, 45, 60 and 90 degrees. Gravel 3 cells from a trunk pulls footsteps on 16 of 16 sites; at 5 cells on 4 of 17. The gravel preview said "No change to routes" on 4 sites where gravel did pull walkers |
| F12 | Partial | Rain, mud, growth and sound all work. But mud is on screen 45% of the time and every worn cell turns to mud and dries at once, so rain reads as a slowdown and hides the worn-ground colours. Sound levels are unheard; the river bed measures very quiet |
| G1 | Needs people | Stand-in: first trail in 8 to 12 s; trunk about 3 cells wide; darker trunks, lighter side trails, pale fringes |
| G2 | Needs people | Stand-in: reroute and preview isolation tests pass, also with rain and growth on |
| G3 | Pass | The five old seed sets are bit-identical to round 3 with rain and growth off (lowest 34.0%). With rain and growth on: seeds 601 to 620 lowest 35.1%, and the reviewer's own seeds 701 to 720 lowest 36.8%, highest 59.0%, mean 46.0% |
| G4 | Pass | 60 fps at 200 walkers, also in rain with sound on |

Not smooth yet: 500 walkers at 8x on a CPU throttled 4 times gives 33 fps with stalls up to 133 ms. On an unthrottled machine it holds 60 fps.

**Lead's decision: accepted and republished (version 4). One last polish round.**

1. Mud: the design doc gives two lengths for autumn mud (3 days in "Weather and mud", 2 days in the season table). Use 2. Keep the doc's rain chances. Make mud read as wet trail rather than a different ground, and let it dry visibly from the least-worn cells first (drawing only).
2. Bugs: audio nodes are never released (a leak over a long session); a stray bell when sound is switched back on after a day change; mud should update at once after a tool is used.
3. Sound levels: raise the river and rain beds, which measure far below the footsteps; keep the step voices to the stated 8 a beat.
4. The dawn flash must not make the Time line unreadable in the dark theme.
5. The gravel preview must not say "No change" where gravel does pull walkers; the README should say gravel reaches about 3 cells, sometimes 5.
6. Smoother frames at 8x with 500 walkers on slow machines, without changing results.
7. A last look at both themes and phone width, and a last full check.

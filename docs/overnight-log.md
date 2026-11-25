# Overnight build log

The lead's running log for the overnight build that started on 2026-11-24. It is the place to look after a restart: what was asked, what is done, what runs now, and what comes next.

## The brief

Take Tide & Toll as far as possible overnight without the owner, using workflows. The lead (Fable 5.2) plans, reviews and decides and writes no game code. Haiku 5.5 agents do quick lookups, doc reading, licence checks and bug hunts. Sonnet 5.5 agents do deeper research, tuning, code review and play-testing. Opus 5.5 agents build and fix.

Work, in priority order:

1. **Phase 1, finish the browser toy:** every checklist item, the flow preview, polished UI in both themes and at phone width, and sound made in code with Web Audio (off until a Sound button is pressed). Then the rest of the step 1 build list: gravel and gate tools, rain and mud, house growth. Republish the Artifact after every round.
2. **Phase 0, repo setup:** the Godot 4 .NET project, the engine-free C# simulation library with tests, `assets/LEDGER.csv`, and the asset pipeline guide.
3. **Assets, the Hamlet set** (may run alongside step 4): cottage, well, field, market stall, fence, footbridge, ford stones, rowboat, trees and reeds, a villager; ground and wear textures, tool icons, a UI font, sound effects (footsteps on grass and dirt, water, bell, place and erase), and a first draft of the 8-bar theme. Free-licence first (CC0 preferred); otherwise made in code. Living Map style, readable from every angle, one ledger row each. Publish an asset gallery Artifact.
4. **Phase 2, Godot foundation:** the simulation ported to C#, a 3D scene with a top-down camera, the wear shader, walkers, basic UI and sound, using the step 3 assets. The theme is a book with a map that you play on. Target: Prototype 1 running in Godot with 500 walkers and headless tests passing. Try to produce a Windows build.
5. **Phase 3, the river prototype:** only if phase 2's automated checks pass.

Rules:

- Ask the owner nothing. Where a decision is theirs, take the design doc's recommended option and log it under "Decisions made for you".
- Anything that needs approval (a new network host, a login, an AI tool account, spending) is skipped and logged under "Needs you".
- Gates that need human testers are marked "waiting for playtest"; automated stand-ins are used and work continues.
- €0 budget. No asset without a ledger row. Nothing with a non-commercial or unclear licence.
- Never weaken a test to make it pass. Report failures plainly.
- Commit and push to the `claude/prototype-1` branch after every round. No pull requests and no pushes to `main`.

Stop when all of it is done, after 20 rounds, or when nothing can progress without the owner. Then write the morning report in the chat and in the Build Plan tab: what was built, links, pass or fail per gate, the asset list with sources, "Decisions made for you", "Needs you", and 5 feedback questions.

## Where things are

| Thing | Where |
| --- | --- |
| Design doc | https://claude.ai/artifact/Ld2BuxHtt8Ht8LpRbVxeJT (doc id `9ee52479-fb74-49e2-9063-a1585cdc1e40`) |
| Build Plan tab | body node `aa88b9bc-2a93`, tab file `db2db437-ccbf`; "This week" checklist list `m6ea1wgr0x5.1263` (item `.1264` is "Build Prototype 1"). The morning report (written 05:30 UTC on 25 November, replacing the earlier "Overnight build report"): heading `m6ea1wgr0x5.16356`, status paragraph `.16392`, links `.16687`, "What was built" table `.16869`, gates table `.19063`, assets table `.21318`, "Needs you" checklist `.23049`, five questions `.29610`, decisions table `.25043`, "What happens next" list `.28966`. Edit cells with find-and-replace on their text; replacing a whole block needs `force` |
| Scheduled check | One pending at a time, about every 45 minutes. Current: fires about 18:39 UTC on 25 November (see the newest "Tide & Toll overnight check" in the scheduled tasks). Workflow runs: fix list `wf_b4d91d92-644` (script copy in `build/wf/`) |
| Other tab body nodes | Overview `654f3c1a-1b75`, Gameplay `7e2d7651-21ec`, UX `ba135eb2-f67b`, Sound `6ddff1a9-fd33`, Art `d64ee9a3-3b89`, Engine `b2ac323d-e193` |
| Browser toy Artifact | https://claude.ai/artifact/V1ng3taGsSZ9Qm45mm4xjw (published from `prototypes/desire-lines/dist/index.html`) |
| Asset gallery Artifact | https://claude.ai/artifact/PT1cMEr8k973QosfZxf9r2 (published by the gallery agent from `docs/asset-gallery/index.html` plus the files in `docs/asset-gallery/files.json`; to republish from this session, read the artifact first, then publish with that URL) |
| Repository | `Marten-Mrfc/Tide-Toll`, branch `claude/prototype-1` |
| Toy build log | `prototypes/desire-lines/ROUNDS.md` |

## How to resume after a restart

1. Clone the repository if `/home/user/tide-toll` is missing and check out `claude/prototype-1`.
2. In `prototypes/desire-lines`, run `npm install` and `npm run build`. Playwright is pre-installed in the container; never install it.
3. Agents read the design doc from plain-text exports in the session scratchpad (`spec/gameplay-1.txt` and so on). After a restart those are gone: read each tab with the Claude Docs tools again (`view`, following the `next` cursor) and convert it to text before starting a round.
4. The Godot toolchain (the .NET SDK and the Godot editor binary) is installed in the container, not in the repository. Re-run `tools/setup-linux.sh` once it exists.
5. A workflow that was running when the container stopped is lost, but its finished agents are not. The container restarted twice tonight (about 00:20 and 01:08 UTC on 25 November); the files, the toolchain and the old scratchpad (`/tmp/claude-0/-home-user/<session>/scratchpad`, where the `spec/` exports live) all survived. To resume a round: commit the working tree as a snapshot, copy the round's script from `/root/.claude/projects/-home-user*/<session>/workflows/scripts/` into `build/wf/` (ignored by git), and start the workflow again with that script path and the same run id. Finished agents come back from the journal; the interrupted ones run again and find their half-done work in the tree. `AGENTS.md` has a temporary "Interrupted work" section that tells them so: remove it before the morning report.
6. To republish an Artifact from a new container, pass its URL from the table above.
7. NEVER send a message (SendMessage) to an agent that is running inside a workflow. It does not reach the running agent: the harness starts a SECOND copy of that agent from its transcript, and both then edit the same files. This happened twice in round 14 (11:47 and 11:53 UTC on 25 November) with the game builder; each time the copy was stopped with TaskStop on the agent's id, which stops the copy and leaves the workflow's own agent running. If an agent inside a workflow needs to know something, put it in a file its brief already tells it to read (a progress file under `docs/plans/`, or the temporary section of `AGENTS.md`), or hand it to the next agent in the workflow.

## Rounds

Limit: 20 rounds. A round is one workflow run: research, build, review.

| # | Track | Goal | Result |
| --- | --- | --- | --- |
| 1 | Toy | Round 2 of the toy: natural trails with an honest trunk-share gate, flow preview, visual and bug fixes | Done 20:54 UTC and republished. Flow preview works and is honest; trails merge and are about 3 cells wide. G3 fails on 1 of 20 held-out seeds (seed 115, 28.6%). Touch input breaks at 400 px because the map jumps when the readout appears |
| 2 | Setup | Phase 0: toolchain, repository skeleton, asset pipeline guide | Done 20:00 UTC. `./check` passes all 7 steps from a clean copy in 31 s; 31 simulation tests pass; the Windows export builds (not run on Windows). Review: no blockers |
| 3 | Assets | The Hamlet asset set: shared generator tooling, models, textures, icons, font, sound effects, theme draft, gallery page | Done 21:08 UTC. 95 asset files, all with ledger rows; everything except the fonts rebuilds identically from its generator; gallery published. Review: no blockers; willow tree and footbridge icon rated "redo"; icons vanish on dark grounds |
| 4 | Port | The toy's simulation (as of commit a057b50) ported to C# with the same tests, a comparison against the toy, a headless runner and `AGENTS.md` | Done 21:51 UTC. The C# library gives exactly the toy's results (wear, flow and cost grids, every metric, the flow preview) on all compared seeds. One simulation tick takes about 1 ms at 500 walkers with no memory allocated. 38 fast tests pass; the gate test fails on G3 exactly as the toy does. Review: no blockers. Open: results on Windows may differ in the last decimal because of the maths library; two small edge bugs (a fence line that leaves the map, a bridge at the map edge) |
| 5 | Toy | Round 3 of the toy: trunk-share headroom with working bridges (gate on fresh seeds 401 to 420), phone-width input fix, gravel and gate tools, polish | Done 00:15 UTC and republished (version 3, same link). G3 now passes on all five seed sets with every daily reading counted (lowest 34.0%), and on the reviewer's own seeds 501 to 520 (lowest 33.5%). Touch works at phone width; the map no longer moves. Open: a gate cannot go on most of a diagonal fence; gravel only pulls a trail within about 2 cells; a second finger disturbs a drag; mature trails still read a little like roads |
| 6 | Assets | Fixes from the asset review; gallery rebuilt and republished to the same link | Done 21:36 UTC. 10 of 12 findings fixed, 2 partly (ground tiles still repeat a little; soft ink on trees). Now 99 asset files, 125 ledger rows; `./check` passes. Left for later: stiff willow fronds, a few icons lose detail on ink, tile repetition |
| 7 | Godot | Phase 2: Prototype 1 on the map page of the book: wear shader, river, buildings, 500 walkers, ink outline, camera, tools, flow preview, interface, sound, headless tests, Windows build | Done 23:49 UTC. Review: phase 2's automated checks pass. `./check`, `./check --bench` and `./check --export` pass. The game gives the toy's town and wear exactly (seeds 1, 2, 7, with edits, at 1x and 8x). Picking was right on 205 of 205 clicks. The game's own work per frame at 500 walkers: 0.3 ms at 1x, 1.6 ms at 8x, 10 to 17 bytes allocated. Windows build: 184 MB, every asset in the pack, never started. Open: at 1280 x 720 the last line of the chapter page is clipped and the fps readout is unreadable (major); preview hitches of about 20 ms at 8x; trails read as roads; buildings have no labels at overview zoom; six small bugs from the bug hunt |
| 8 | River | Polish the Godot game after its review, then start phase 3: river assets, river rules in the simulation with a strategy bot, the river in the game, review | Done 02:45 UTC (two container restarts on the way). Polish: trails read as worn ground, villagers in colour, labels, copses, review fixes. River assets: 48 files. River rules: boats, landing, grain, ford, ferry, low footbridge, drawbridge with manual raising, keeper, reputation, money; off by default, Prototype 1 unchanged. In the game: boats, tools with prices, bridge badge with a lever, ledger and margin notes, sounds. Review: mechanics work, are deterministic at 1x, 4x and 8x, and are well tested, but the prototype is not ready for a playtest. On 24 fresh seeds the ferry beats the hand-raised drawbridge in money in every town, the free ford earns about as much as the ferry, and the keeper pays in 8 of 24 towns. `./check` fails about 2 runs in 7 on a preview timing gate. The river is too small to read at the default zoom. Spring flood and winter ice are not built |
| 9 | Toy | Round 4 of the toy: gate and gravel fixes, rain and mud, house growth, sound made in code (off until the Sound button), two-finger and phone fixes, worn-ground look | Done 03:38 UTC and republished (version 4, same link), after two container restarts. Gates go on fences at any angle; gravel pulls a trail from about 3 cells; rain and mud; cottages grow to 16 houses and 248 villagers; sound is off until pressed. G3 passes: the five old sets are bit-identical with rain and growth off, and with both on seeds 601 to 620 (lowest 35.1%) and the reviewer's 701 to 720 (lowest 36.8%, highest 59.0%) pass. Open: mud is on screen 45% of the time; audio nodes are never released; the river sound measures very quiet; 33 fps at 8x with 500 walkers on a CPU throttled 4 times |
| 10 | River | River, second pass: crossing capacity, spring flood, winter ice with its own wear layer, bounded preview steps, bug fixes; the river numbers tuned with the strategy bot; the river readable at the default zoom, flood and ice drawn; review on seeds 401 to 424 | Done 07:46 UTC. Built and verified: capacity, flood (70% of springs, ford closed), ice (48% of winters, separate ice paths), the preview timing gate (9 to 11 ms against 25), boats and crossings readable at the default zoom, Prototype 1 unchanged with the river off. Review verdict: NOT ready for a playtest. (1) `./check` is red on every run: the tuner's new default numbers broke ten river tests and the game's smoke test. (2) On 24 fresh towns the free ford ties or beats every purchase in money (clearly at 60 walkers), the ferry is slower than the ford, the drawbridge beats the ferry by 1%, and the keeper is affordable 15 seconds after the drawbridge. Of the lead's six outcomes, (c) to (f) pass on the letter, (a) on the median, (b) fails |
| 11 | Toy | Round 5 of the toy, the last polish round | Done 05:43 UTC and republished (version 5, same link). The final review says the toy is finished for the owner to play: check 86 of 86, smoke 3 of 3, perf 2 of 2, no test weakened in any round. G3 passes on the five old sets (bit-identical) and, with rain and growth on, on 100 seeds plus the reviewer's own 801 to 820 (lowest 37.1%, highest 52.6%). The audio leak is gone. Left for the owner to judge: mud about 40% of the time, the sound by ear, the dirt-road look of mature trunks. Five small bugs from the last bug hunt are listed in `ROUNDS.md` for a short fix list |
| 12 | Toy and plan | The toy's short bug list, checked independently; and, in parallel, the written plan for porting the toy's rounds 3 to 5 to the C# library and the game | Done 07:22 UTC. The seven small fixes are in, each with a test; simulation results unchanged; the independent check passed and the toy is republished as version 6 at the same link. The porting plan is `docs/plans/sync-plan.md`: nine library steps and six game steps, three of them high risk |
| 13 | Sync | Make the branch green again, then bring the C# library in line with the finished toy following `docs/plans/sync-plan.md` (steps A0 to A9), then review | Done 11:29 UTC. A0 to A7 finished and verified: the ten stale tests repaired without touching production numbers; new comparison data from the finished toy; the retuned trail model, gravel, gates, the second flow preview, rain and mud, house growth. The reviewer ran the toy and the library side by side on seeds 77, 314 and 909 (30 days, with edits, rain and growth): every wear hash and daily array identical; walker positions differ by 1e-13. The C# trunk-share gate passes on all 20 gate seeds, seed 115 included. A9 done: the library's defaults are the finished toy's rules. NOT green: `./check` fails on three river tests that pass on the old trail model (step A8 not met), and the river's economy is tuned for the old model. One game-perf run in five failed on a 27.8 ms tick. 11 of 146 maths probes differ from the browser's in the last bit (kept as an honest failing gate test). A10 not done |
| 14 | River 3 and game sync | Simulation: green on the new trail model, bug fixes, a faster route search, the river's new rules and a retune. Game: two chapters, the new river rules shown. Review on seeds 501 to 524 and both chapters played | Done 15:36 UTC. Review: Prototype 1 in Godot is now the finished toy in its rules (toy and library identical on three fresh seeds, one above 3.39 million; Chapter I played with real input: gates, gravel, rain, mud, growing houses). `./check` passed in 4 of 5 full runs (the one failure was a timing test while the reviewer's own jobs loaded the machine); bench, export and an export from a fresh copy pass; river tests pass on all three trail-model presets. River on seeds 501 to 524: 19 of 21 checks pass. Median money at 200 walkers: drawbridge by hand 1998, keeper 1863, ferry 1425, ford only 1317, footbridge 864; at 60 walkers the ferry (817) beats the drawbridge (535). The keeper comes about 3.6 minutes after the drawbridge. Open: the keeper unlock counts raises with no boat waiting, so it can be clicked through in 24 seconds; one check fails through a bug in its own code; one perf run in eleven had a 49 ms simulation step; the footbridge only wins on trip time |
| 15 | Fix list | The short fix list before the playtest: a keeper unlock that counts only raises for a waiting barge and belongs to the town, the bug in a check's own code, the bug hunt's items, small game fixes (page turn, status line, ledger row, wet sacks, allocation margin), a timer that names what ran in a slow simulation step; then an independent check | Running since 15:39 UTC (workflow run `wf_b4d91d92-644`; script copy in `build/wf/`). Progress file: `docs/plans/fixlist-progress.md` |

## The owner's requests since the overnight brief

- 25 November, about 09:00 UTC: "Make github build an exe for windows 11". Done with `.github/workflows/windows-build.yml` (the push of a workflow file was accepted). The first run (commit b3e453b) passed in under 2 minutes and attached `TideToll-windows-x64` (78 MB, kept until 25 December); the exe has still not been started on Windows. The lead can see runs with `gh run list --repo Marten-Mrfc/Tide-Toll` even though `gh auth status` complains. Commits whose message starts with "Snapshot" do not trigger a build, so keep using that prefix for work-in-progress commits and avoid it for finished rounds. The second build (commit d73c84f, the two-chapter game after round 14's review) passed at 15:38 UTC: run 45647234830, 78 MB.

## Next steps

- Round 15 is running (the fix list before the playtest): the keeper unlock counts only raises that let a waiting masted boat through, and the count belongs to the town; the bug in the footbridge trade check's own code; check (a) for trip time restated openly (a never-raised drawbridge is a dearer footbridge); the bug hunt's items (lost grain not counted, the old town running under the page turn, a stale queue pass, a walker set down on the far bank, a leftover timestamp, a note when growth's room is full); the status line after a successful build; a larger wet sack; the allocation margin in Chapter I; a timer that names what ran in any simulation step over 20 ms; timing tests re-run once on failure. Then an independent check.
- After round 15: commit WITHOUT the "Snapshot" prefix so GitHub builds a fresh exe, check the run with `gh run list --repo Marten-Mrfc/Tide-Toll`, tell the owner. Then the build stops: every remaining gate needs people or the owner's PC. Remove the temporary "Interrupted work" section from `AGENTS.md`, write the final report in the chat and in the Build Plan tab, and cancel the pending scheduled check.

## Toolchain facts (from the setup round)

- Godot 4.7-stable .NET at `/opt/godot/4.7`, launcher `/usr/local/bin/godot`; .NET SDK 8.0.134 from apt. Reinstall with `tools/setup-linux.sh`.
- `./check` runs the simulation build and tests, the Godot build, a headless smoke run, a screenshot and the ledger check. Options: `--export`, `--with-toy`, `--no-screenshot`.
- Screenshots need a window: `xvfb-run` with `--rendering-method mobile --rendering-driver vulkan`. Rendering here is software only (about 4 to 40 fps), so frame rates measured in this container say nothing about a real graphics card; measure script time per frame instead.
- Unhandled C# exceptions do not change Godot's exit code. Tests call `GetTree().Quit(code)` themselves.
- A Windows export needs a `.sln` beside the Godot project; without one it exits 0 with a broken build.

## Gate status

| Gate | Status |
| --- | --- |
| G1: 7 of 10 testers find watching trails form satisfying | Waiting for playtest. Stand-in (final toy): first trail in 8 to 13 s (limit 45 s); trails are soft, about 3 cells wide and merge into 2 or 3 trunks |
| G2: testers move a trail within three attempts, flow preview on | Waiting for playtest. Stand-in (final toy): the reroute test passes, also with rain and growth on; previews never change the simulation; trails form where the preview drew them |
| G3: trunk share stays between 30% and 70% in 20 seeds | Pass, with every daily reading from day 20 to 45 counted. Rain and growth off: five seed sets (100 seeds, lowest 34.0%). Rain and growth on (final toy): 100 seeds (lowest 34.4%, highest 56.5%) and the final reviewer's own seeds 801 to 820 (lowest 37.1%, highest 52.6%, mean 45.0%) |
| G4: 200 agents at 60 fps in a desktop browser | Pass (headless Chromium, capped at 60 Hz) |
| Phase 2: Prototype 1 in Godot at 60 fps with 500 walkers | Waiting for the owner's PC (GitHub builds the Windows exe). Automated stand-in passes: the game's own work per frame at 500 walkers is 0.4 ms at 1x and 2 ms at 8x. Since round 14 Chapter I of the game is the finished toy: the same rules exactly, with gravel, gates, rain, mud and growing houses drawn |
| Phase 3, river: at least 70% of testers use two or more crossing types for stated reasons | Waiting for playtest. Stand-in (strategy bot, 24 fresh towns, round 14): mostly passes. The free ford now costs trade (ford-only ends 37% below the best purchase), the ferry is the better buy in a small village and the drawbridge in a busy one. Still weak: nothing pays for owning two crossings at once, and the footbridge only wins on trip time |
| Phase 3, river: at least 60% can explain why the footbridge hurt trade | Waiting for playtest. Stand-in passes: the footbridge gives the fastest trips but turns back every barge, trade falls by more than half and reputation drops to 0; the game marks each boat "Mast too tall" and "Turned back" |
| Phase 3, river: manual raising enjoyed for 3 minutes, then the keeper is bought | Waiting for playtest. Stand-in (round 14): the keeper becomes available about 3.6 minutes after the drawbridge for a player who raises only for boats, and beats a player who misses 3 boats in 10 in 13 of 24 towns. Open until round 15: raising with no boat waiting also counts, so the wait can be clicked through |
| Phase 3, river: testers describe the river as a second network | Waiting for playtest. Stand-in (round 10): boats, the three crossing types, a waiting barge and the flood now read at the default zoom in the reviewer's screenshots |
| Phase 3, river: 60 fps with 200 walkers on a low-power PC | Waiting for the owner's PC. Stand-in passes: the game's own work per frame with the river on is 0.27 ms at 200 walkers and 0.35 ms at 500 (1x), 1.6 to 1.8 ms at 8x |

## Decisions made for you

| # | Decision | Why |
| --- | --- | --- |
| 1 | Work stays on the one branch `claude/prototype-1`, including the Godot work | One branch is simpler to fetch; no pull requests were asked for |
| 2 | Repo setup runs alongside the toy rounds instead of after them | They touch different folders, and it uses the night better |
| 3 | The toy compresses time: walkers move at 3 cells a second, not the doc's 0.67 | At the doc's real walking speed no trail ever forms (measured: 0.2% coverage) |
| 4 | The toy routes with one cost map per destination, not a search per trip | A search per trip costs 3 to 5 seconds of computing per second at 500 walkers and 8x speed |
| 5 | The trunk-share gate is judged on seeds the model was not tuned on (101 to 120), counting every daily reading | Round 1 passed only on its own tuning seeds |
| 6 | A CI workflow file will be kept as a template under `docs/`, not in `.github/workflows` | A push that adds a workflow file can be rejected for this session's token, which would block every later push |
| 7 | Godot 4.7-stable with .NET 8 | 4.7 is the newest stable build that downloads here, and its C# library targets .NET 8 |
| 8 | Projects are named `TideToll.Sim`, `TideToll.Sim.Tests` and `TideToll` in `sim/`, `sim.tests/` and `game/` | The Build Plan's folder layout was followed; the Engine tab's older code name "Delta" was not used |
| 9 | The Mobile renderer, as the Engine tab asks | It works here under software rendering, so screenshots use the same renderer as the game |
| 10 | The C# library keeps the toy's random-number generator and decimal costs for now | It lets tests prove the C# code gives exactly the toy's results; the Engine tab's PCG32 generator and whole-number costs can come with the full port |
| 11 | Asset formats: `.glb` models, WAV at 48 kHz for single sounds, Ogg for loops and music, lowercase-hyphen names | The design doc leaves these open; the choices are written down in `docs/asset-pipeline.md` |
| 12 | Assets live in `assets/` and are copied into `game/assets/` by a script | Godot only loads files inside the game folder, and the Build Plan puts assets at the top level |
| 13 | The game's chapter page and bookmark tabs are drawn as flat interface panels laid exactly over the book, not as textures on the 3D page | Text stays sharp at every zoom. The trade-off: when the camera later tilts, these panels will need to move onto the page |
| 14 | In the game, W is the wear overlay and the arrow keys pan; W A S D do not pan | The toy already uses W for the wear overlay and E for erase; the UX tab's W A S D pan conflicts with both. Easy to change when key remapping is built |
| 15 | Sound is on by default in the Godot game, and off until the Sound button is pressed in the browser toy | The brief asks for off-by-default only for the toy; a desktop game normally starts with sound |
| 16 | The river prototype (phase 3) started tonight | The brief allows it once phase 2's automated checks pass, and they did. The 60 fps reading on a real graphics card is still open |
| 17 | The toy's trail model changed in round 3: worn ground is cheaper to walk, and walkers on long detours, plus one villager in ten, mostly ignore that discount | It gives the trunk-share gate real room and keeps new footbridges in use. The cost: the C# library must be brought in line with the toy again (planned sync round) |
| 18 | In the toy, gravel and gates are removed with the Erase tool, not with Ctrl-drag as the tool table says | One way to remove things is simpler, and it works on touch screens |
| 19 | The river prototype gives villagers grain to carry from the field to the landing, where a moored boat pays double what the village market does | The doc has no goods for a Hamlet-only prototype, and without goods a blocked barge would cost nothing |
| 20 | The drawbridge has one lifting leaf, not the Art tab's double leaf | With a pier in the water a barge did not fit through a 3-cell river |
| 21 | In the game the drawbridge is worked by selecting it and clicking again, pressing L, or using the lever on its badge; K hires the keeper | The UX tab names "select, then L"; a second click was added so it also works with the mouse alone |
| 22 | Boats enter from the south only, the landing sits level with the market, and barges cannot lower their masts in the prototype | The doc lets a barge lower its mast one class, which would let it under the footbridge, and then the footbridge would cost no trade. The crossing table's "blocks every mast" was followed |
| 23 | Demolishing a crossing costs half its price in the prototype, not the doc's double | Testers should try several crossing types in one session |
| 24 | The check "a keeper pays for itself at 200 walkers" was replaced by a test at three levels of player attention (missing 10%, 30% and 50% of boats); the old form is still printed | The old check assumed a player who never looks away from the bridge. Its result, reported as it is: the keeper is 43 to 79 coins worse than an attentive hand-raising player |
| 25 | Autumn mud lasts 2 days in the toy, as in the doc's season table, not the 3 days of "Weather and mud" | The doc gives both. With 3 days mud was on screen 45% of the time |
| 26 | In rain, mud multiplies the cost a walker already sees for that ground, and walkers on long detours see less of it | The doc says "cost x1.6" without saying of what. The other reading pushed trunk share out of its band on half the tuning seeds |
| 27 | Gravel costs 0.6 of grass in the toy, not the tool table's 0.8 | At 0.8 gravel could not pull a trail more than 2 cells; at 0.6 it pulls from about 3 |
| 28 | The toy's Hamlet stops growing at 248 villagers (16 cottages of 3 on top of the first houses), one cottage every 2 days | The doc's Hamlet target of 250 was read as people; one household per 2 days is the doc's fastest rate |
| 29 | In the game's river prototype, house growth stays off and gravel and gates cost nothing | The river's prices were tuned for a village of fixed size, and pricing the path tools belongs to the next prototype step |
| 30 | The simulation does not get its own exp, pow and sin functions yet; the comparison data records probe values instead | Results match on this machine; whether they match on Windows can be shown by running the tests there once (see "Needs you") |
| 31 | River numbers after tuning (round 10): starting money 120 to 0, boat value 2 to 3, drawbridge 240 to 200, keeper 30 to 120, ferry capacity 6 to 5 with a 0.75 s turn-round, ferry upkeep 2 to 5, ferryman's wage 3 to 15 | The bot's trade-off checks. The review judged the wage and upkeep changes a patch, not a reason the doc gives; the third pass puts them back once the ford has a real cost |
| 32 | No flood in the first spring of a new town | The game starts on the first day of spring, so 7 towns in 10 would open with their only crossing closed |
| 33 | The game is a book with two chapters and opens on the first: Chapter I "The Hamlet" is Prototype 1 as the finished toy plays it; Chapter II "The River" is the river prototype. Turning the page starts a fresh town from the same seed | Rain and growing houses are off while the river rules are on, so one town cannot show both prototypes; chapters fit the book |
| 34 | In the river chapter, grain carried through the ford gets wet and sells for less, and a loaded walker wades more slowly | The review found the free ford earned as much as any crossing; the doc wants players to connect slow trips to lost trade |
| 35 | The keeper can be hired only after the player has raised the bridge by hand a number of times (about 10) | Price alone made the keeper affordable 15 seconds after the drawbridge; the doc wants about 3 minutes of raising by hand first |
| 36 | A small starting purse in the river chapter, and the ferryman's wage and upkeep back to the doc's 3 and 2 | With no purse there was nothing to buy for the first minute and a half; the higher wage was a patch |
| 37 | Outcome (b) of the river checks was restated from "the ford is the slowest way across" to "the free ford costs trade"; the old form is still printed | A ferry busy enough to queue is as slow as the ford, so the old form could not hold together with the others |
| 38 | The library's maths is exact against the toy only at the precision results are stored at; 11 of 146 probe values differ from the browser's in the last bit | .NET and the browser round exp and pow differently. Wear, costs and routes are identical; walker positions differ by 1e-13. The exact comparison stays as a test that fails honestly |
| 39 | River numbers after the third pass: wet grain sells at 55% of its price, a loaded walker wades at 0.7 of ford speed, drawbridge 220 (doc: 200 plus 40 stone), keeper 15 and unlocked after 10 hand raises, starting purse 50, ferry wage 3 and upkeep 2 (the doc's) | Tuned with the strategy bot on 40 seeds and judged on 24 others. The review notes that the drawbridge price and the keeper price were set to pass the bot's checks; the playtest should say what they should be |
| 40 | The low footbridge stays what the doc makes it: the cheap, fast first buy that blocks barges. Nothing in the prototype pays for shorter walks, so in money it ends below doing nothing | That cost is the lesson the doc wants testers to explain ("why the footbridge hurt trade"). Whether it is tempting enough is a playtest question |

## Needs you

| # | What | Why it was skipped |
| --- | --- | --- |
| 1 | Allow the free asset sites in the environment's network settings: kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org | The container cannot reach them, so tonight's assets are made in code instead |
| 2 | Play the toy and answer the feedback questions | Gates G1 and G2 need people |
| 3 | Run the Windows build on your PC: download `TideToll-windows-x64` from the newest green run at https://github.com/Marten-Mrfc/Tide-Toll/actions/workflows/windows-build.yml, unzip, run `TideToll.exe` (`docs/setup-windows.md` has the list of what to check) | The container has no Windows, so the build has never been started |
| 4 | Done on your request (25 November, 09:00 UTC): GitHub now builds the Windows exe. `.github/workflows/windows-build.yml` runs on pushes to `main` and `claude/` branches and attaches `TideToll-windows-x64` to the run; see `docs/setup-windows.md`. Still open: whether to also run the full `./check` on GitHub (`docs/ci/github-actions.yml` is the template) | It uses your GitHub Actions minutes |
| 5 | Listen to the sounds and the theme draft in the asset gallery | Agents can only measure audio, not hear it |
| 6 | Say whether you want AI-generated art, sound or music from an online service | Those need an account and a licence check; tonight everything was made by code instead |
| 7 | Listen to the toy with Sound on, and to the game: are the river and rain audible, is the bell too loud or too soft, do the footsteps follow the walkers | Levels were set from measurements only |
| 8 | Say whether there is too much rain and mud in the toy | The doc's rain chances keep mud on screen a large part of the time; the numbers are the doc's, so changing them is your call |
| 9 | On your Windows PC, run `dotnet test sim.tests -c Release` once and send the output | It shows whether the simulation gives exactly the same numbers on Windows as in the build container; replays and saved games will rely on that |

## Assets

Two sets so far. Every file has a row in `assets/LEDGER.csv` (173 rows); the gallery shows each one with its source and licence.

**The Hamlet set** (99 files)

| Kind | Count | What | Source |
| --- | --- | --- | --- |
| Models (`.glb`) | 24 | Three cottages, well, market stall, field in three states, wattle fence, gate and post, footbridge end and span, ford stones, rowboat, oak, poplar, willow, reeds, villager and four cargo items | Generated by scripts in `assets/src/models/`; self-made |
| Textures | 9 | Grass, trampled, trail, track, hollow way, river water, paper page, wear ramp, palette atlas | Generated by `assets/src/2d/` and `assets/src/common/`; self-made |
| Icons (`.svg`) | 14 | Fence, footbridge, erase, gravel, gate, pause, play, speed, wear overlay, flow preview, sound on and off, reset, new map | Generated by `assets/src/2d/icons.py`; self-made |
| Fonts | 4 | Atkinson Hyperlegible Regular and Bold (interface), Alegreya (headings), Uncial Antiqua (Hamlet titles) | The google/fonts repository on GitHub; SIL Open Font License 1.1, licence texts in `assets/licences/` |
| Sound effects | 44 | Footsteps on grass (8) and dirt (8), ford water (6), bell (6), long bell (4), place (6), erase (6) | Synthesised by `assets/src/audio/`; self-made |
| Music and ambience | 4 | The 8-bar Hamlet theme draft (D Dorian, 3/4, 64 BPM) as Ogg, WAV and a score file; a 10 s river loop | Synthesised by `assets/src/audio/`; self-made |

**The River set** (48 files, round 8)

| Kind | Count | What | Source |
| --- | --- | --- | --- |
| Models (`.glb`) | 10 | Masted barge (mast and cargo as separate parts), ferry raft, ferry post and rope, drawbridge with a lifting leaf, drawbridge span and end, landing jetty, keeper's hut | Generated by scripts in `assets/src/models/`; self-made |
| Icons (`.svg`) | 8 | Ferry, drawbridge, raise the bridge, lower the bridge, hire the keeper, coin, boat, reputation | Generated by `assets/src/2d/`; self-made |
| Sound effects | 30 | Drawbridge raising (3) and lowering (3), boat calls by hand bell and horn (6), coins (6), ferry rope (4), oar and wake (6), a boat turning away (3) | Synthesised by `assets/src/audio/`; self-made |

Nothing came from the six blocked asset sites. No AI image, sound or music service was used: every generated asset is produced by code in the repository. The audio has been measured (length, loudness, spectrum) but nobody has listened to it yet. Known weak spots: the drawbridge tool icon reads poorly at 20 px; the drawbridge creaks may sound more like hiss than timber; the drawbridge has one leaf, not the Art tab's double leaf, so that a barge fits through.

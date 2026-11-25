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
| Build Plan tab | body node `aa88b9bc-2a93`, tab file `db2db437-ccbf`; "This week" checklist list `m6ea1wgr0x5.1263` (item `.1264` is "Build Prototype 1"); the "Overnight build report" section: heading `m6ea1wgr0x5.8044`, status line `.8067`, status table `.9778`, gates table `.10555`, "Needs you" checklist `.9340` |
| Scheduled check | One pending at a time, about every 45 minutes. Current: fires about 01:34 UTC on 25 November (see the newest "Tide & Toll overnight check" in the scheduled tasks). Workflow runs: river round `wf_6e0a829d-c37`, toy round 4 `wf_70be681e-d06` |
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
5. A workflow that was running when the container stopped is lost, but its finished agents are not. The container restarted twice tonight (about 00:20 and 01:08 UTC on 25 November); the files, the toolchain and the old scratchpad (`/tmp/claude-0/-home-user/<session>/scratchpad`, where the `spec/` exports live) all survived. To resume a round: commit the working tree as a snapshot, copy the round's script from `/root/.claude/projects/-home-user*/<session>/workflows/scripts/` into `build/wf/` (ignored by git), and start the workflow again with that script path and the same run id. Finished agents come back from the journal; the interrupted ones run again and find their half-done work in the tree. `AGENTS.md` has a temporary "Interrupted work" section that tells them so: remove it when rounds 8 and 9 are done.
6. To republish an Artifact from a new container, pass its URL from the table above.

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
| 8 | River | Polish the Godot game after its review (clipped text, trails that read as worn ground, villagers, labels, trees, bookmarks, six bugs), then start phase 3: river assets, river rules in the simulation (boats, ferry, low footbridge, drawbridge with keeper, trade, reputation, money) with a strategy bot, the river in the game, review | Running (workflow run `wf_6e0a829d-c37`). Started about 23:55 UTC; stopped by a container restart at about 00:20 and resumed at 00:25. Already done: the river rules spec, the look and sound spec, and the river asset set (10 models, 8 icons, 30 sounds, 48 new ledger rows, gallery republished as version 3). The game polish finished at 00:45 and passed `./check` (worn-ground trails, villagers in colour, labels, copses, review fixes). A second restart at 01:08 stopped the river rules builder when it was nearly done (78 fast tests passing); resumed at 01:10. Still to come: the river in the game, then the review |
| 9 | Toy | Round 4 of the toy: gate and gravel fixes, rain and mud, house growth, sound made in code (off until the Sound button), two-finger and phone fixes, worn-ground look | Running (workflow run `wf_70be681e-d06`). Started about 00:19 UTC; stopped by the container restart and resumed at 00:26 with its two spec sheets kept. The sound design finished. The second restart at 01:08 stopped the rules builder when it was nearly done; resumed at 01:10 |

## Next steps

- When toy round 4 finishes: read the review, build the page, have a fork of the lead read the whole built page and republish it to the same link, update `ROUNDS.md`, commit, push.
- Toy round 5: final polish in both themes and at phone width, and a last full check. Then tick "Build Prototype 1" in the Build Plan.
- When round 8 (the river round) finishes: read the review and the strategy table, look at the screenshots, check the asset gallery was republished, update the gates, commit, push. Spring flood and winter ice are the likely leftovers for a second river round.
- After toy rounds 3 and 4: a sync round that ports the toy's newer features (gravel, gate, rain, house growth, the model retune) to the C# library and the Godot game, regenerates the comparison data, and closes the port review's open points (own maths functions so Windows gives the same results, read-only views, a tighter speed budget, a comparison that covers edits).
- Have the morning report ready in the chat and in the Build Plan tab by about 06:00 UTC, even if rounds are still running; update it if later rounds land.

## Toolchain facts (from the setup round)

- Godot 4.7-stable .NET at `/opt/godot/4.7`, launcher `/usr/local/bin/godot`; .NET SDK 8.0.134 from apt. Reinstall with `tools/setup-linux.sh`.
- `./check` runs the simulation build and tests, the Godot build, a headless smoke run, a screenshot and the ledger check. Options: `--export`, `--with-toy`, `--no-screenshot`.
- Screenshots need a window: `xvfb-run` with `--rendering-method mobile --rendering-driver vulkan`. Rendering here is software only (about 4 to 40 fps), so frame rates measured in this container say nothing about a real graphics card; measure script time per frame instead.
- Unhandled C# exceptions do not change Godot's exit code. Tests call `GetTree().Quit(code)` themselves.
- A Windows export needs a `.sln` beside the Godot project; without one it exits 0 with a broken build.

## Gate status

| Gate | Status |
| --- | --- |
| G1: 7 of 10 testers find watching trails form satisfying | Waiting for playtest. Stand-in after toy round 3: first trail in 8 to 13 s (limit 45 s); trails are soft, about 3 cells wide and merge into a trunk |
| G2: testers move a trail within three attempts, flow preview on | Waiting for playtest. Stand-in after toy round 3: the reroute test passes; previews never change the simulation; 77% to 84% of the points on a predicted detour get a new trail |
| G3: trunk share stays between 30% and 70% in 20 seeds | Pass after toy round 3, with every daily reading from day 20 to 45 counted: five seed sets (100 seeds, lowest 34.0%) and the reviewer's own 20 seeds (lowest 33.5%, highest 54.7%). From round 4 it is also judged with rain and house growth on |
| G4: 200 agents at 60 fps in a desktop browser | Pass (headless Chromium, capped at 60 Hz) |
| Phase 2: Prototype 1 in Godot at 60 fps with 500 walkers | Waiting for the owner's PC. Automated stand-in passes (round 7): the game's own work per frame at 500 walkers is 0.3 ms at 1x and 1.6 ms at 8x, with walkers in one batch and nothing created per frame. Drawing speed cannot be measured here (no graphics card) |

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

## Needs you

| # | What | Why it was skipped |
| --- | --- | --- |
| 1 | Allow the free asset sites in the environment's network settings: kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org | The container cannot reach them, so tonight's assets are made in code instead |
| 2 | Play the toy and answer the feedback questions | Gates G1 and G2 need people |
| 3 | Run the Windows build on your PC (`tools/export-windows.sh` makes it; `docs/setup-windows.md` has the steps to open the project in Godot) | The container has no Windows, so the build was made but never started |
| 4 | Decide whether to switch on automated builds: copy `docs/ci/github-actions.yml` to `.github/workflows/` | It uses your GitHub Actions minutes, and this session may not be allowed to push workflow files |
| 5 | Listen to the sounds and the theme draft in the asset gallery | Agents can only measure audio, not hear it |
| 6 | Say whether you want AI-generated art, sound or music from an online service | Those need an account and a licence check; tonight everything was made by code instead |

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

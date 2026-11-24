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
| Build Plan tab | body node `aa88b9bc-2a93`, tab file `db2db437-ccbf`; "This week" checklist list `m6ea1wgr0x5.1263` |
| Other tab body nodes | Overview `654f3c1a-1b75`, Gameplay `7e2d7651-21ec`, UX `ba135eb2-f67b`, Sound `6ddff1a9-fd33`, Art `d64ee9a3-3b89`, Engine `b2ac323d-e193` |
| Browser toy Artifact | https://claude.ai/artifact/V1ng3taGsSZ9Qm45mm4xjw (published from `prototypes/desire-lines/dist/index.html`) |
| Asset gallery Artifact | not published yet |
| Repository | `Marten-Mrfc/Tide-Toll`, branch `claude/prototype-1` |
| Toy build log | `prototypes/desire-lines/ROUNDS.md` |

## How to resume after a restart

1. Clone the repository if `/home/user/tide-toll` is missing and check out `claude/prototype-1`.
2. In `prototypes/desire-lines`, run `npm install` and `npm run build`. Playwright is pre-installed in the container; never install it.
3. Agents read the design doc from plain-text exports in the session scratchpad (`spec/gameplay-1.txt` and so on). After a restart those are gone: read each tab with the Claude Docs tools again (`view`, following the `next` cursor) and convert it to text before starting a round.
4. The Godot toolchain (the .NET SDK and the Godot editor binary) is installed in the container, not in the repository. Re-run `tools/setup-linux.sh` once it exists.
5. A workflow that was running when the container stopped is lost. Check `git status`, keep what builds and passes, and re-run the round.
6. To republish an Artifact from a new container, pass its URL from the table above.

## Rounds

Limit: 20 rounds. A round is one workflow run: research, build, review.

| # | Track | Goal | Result |
| --- | --- | --- | --- |
| 1 | Toy | Round 2 of the toy: natural trails with an honest trunk-share gate, flow preview, visual and bug fixes | Running since 19:00 UTC |
| 2 | Setup | Phase 0: toolchain, repository skeleton, asset pipeline guide | Starting 19:50 UTC |

## Next steps

- When toy round 2 finishes: review it, read the changes, republish the toy, commit, push, update `ROUNDS.md`.
- Toy round 3: sound in code (Web Audio, off until the Sound button), gravel and gate tools, and whatever the round 2 review lists.
- Toy round 4: rain and mud, house growth, final polish in both themes and at phone width.
- After the setup round: the Hamlet asset set and gallery, then the C# port of the simulation, then the Godot scene.

## Gate status

| Gate | Status |
| --- | --- |
| G1: 7 of 10 testers find watching trails form satisfying | Waiting for playtest. Stand-in after round 1: first trail in 9 to 24 s (limit 45 s) |
| G2: testers move a trail within three attempts, flow preview on | Waiting for playtest. Stand-in after round 1: automated reroute test passes; flow preview in progress |
| G3: trunk share stays between 30% and 70% in 20 seeds | Not passed yet. Round 1 passed only on the seeds it was tuned on |
| G4: 200 agents at 60 fps in a desktop browser | Pass (headless Chromium, capped at 60 Hz) |
| Phase 2: Prototype 1 in Godot at 60 fps with 500 walkers | Not started |

## Decisions made for you

| # | Decision | Why |
| --- | --- | --- |
| 1 | Work stays on the one branch `claude/prototype-1`, including the Godot work | One branch is simpler to fetch; no pull requests were asked for |
| 2 | Repo setup runs alongside the toy rounds instead of after them | They touch different folders, and it uses the night better |
| 3 | The toy compresses time: walkers move at 3 cells a second, not the doc's 0.67 | At the doc's real walking speed no trail ever forms (measured: 0.2% coverage) |
| 4 | The toy routes with one cost map per destination, not a search per trip | A search per trip costs 3 to 5 seconds of computing per second at 500 walkers and 8x speed |
| 5 | The trunk-share gate is judged on seeds the model was not tuned on (101 to 120), counting every daily reading | Round 1 passed only on its own tuning seeds |
| 6 | A CI workflow file will be kept as a template under `docs/`, not in `.github/workflows` | A push that adds a workflow file can be rejected for this session's token, which would block every later push |

## Needs you

| # | What | Why it was skipped |
| --- | --- | --- |
| 1 | Allow the free asset sites in the environment's network settings: kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org | The container cannot reach them, so tonight's assets are made in code instead |
| 2 | Play the toy and answer the feedback questions | Gates G1 and G2 need people |

## Assets

None yet beyond the build tools in `assets/LEDGER.csv`.

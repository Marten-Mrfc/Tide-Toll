# Incremental rebuild: the lead's log

The second overnight build (25 to 26 November 2026). The first build's log is `docs/overnight-log.md`; its rules about resuming still hold and the important ones are repeated here. All times are UTC, read from `date -u`.

## The brief (from the owner, word for word)

Use workflows to rebuild Tide & Toll as an incremental town game overnight, without me. Build until 07:00 Amsterdam time (06:00 UTC on 26 November), then show me a lot of screenshots. I will answer yes or no per item and say continue.

What changes
- The current walker system goes. Villagers no longer wander and wear paths anywhere, and the fence, gate and gravel tools stop being the game.
- New core loop, in the spirit of Incremental Traffic (our own version, not a copy of its maps, names or look): every finished trip earns coin; coin buys upgrades; upgrades reach milestones; each milestone hands the player the next piece of town automatically.
- Start small and logical: 1 house, 1 villager, 1 road. Villagers, houses, roads and fields are added step by step. Each level inside an age gives more houses and roads, and now and then an extra field or building. Enough levels ring in the next age.
- Roads are given by the stage, never drawn by the player. A new road appears as a faint worn trail and visibly wears in as villagers use it; a surface upgrade paves it.
- Upgrades to start with: more villagers, walking speed, load size, road surface, new buildings. Add milestones and achievements so there is always a next thing to reach.

The book
- Most of the time the player is zoomed in on the map. The book is only visible when fully zoomed out.
- Pausing closes the book: the closed cover is the pause menu (resume, settings, new town, quit).
- A full settings screen as pages of the book: music and sound volumes, display (window mode, resolution, interface size), keys, and saving.

Music
- The current background theme is right. Add at least three more pieces in exactly that style, and one variation for the age change. Make them in code as before, with ledger rows.

Keep
- The engine set-up, the tests, the one-command check, the Windows build on GitHub, the Living Map art style and the existing assets. The river, boats and crossings come back as the second age.

Order of work (do not stop between steps)
1. The core loop: 1 house, 1 road, 1 villager, coin per trip, upgrades, milestones that add houses and roads.
2. The book behaviour and the menus (zoom out to see the book, pause closes it, settings pages).
3. A full first age: about 30 minutes of levels, a second field, the market, achievements, saving and loading.
4. The extra music.
5. If there is time left: the second age with the river.

Roles: you (Fable 5.2) lead, plan, review and decide; don't write game code. Haiku 5.5: quick lookups, doc reading, licence checks, bug hunts. Sonnet 5.5: research, balancing, code review, play-testing. Opus 5.5: building and fixing.

Rules
- Don't ask me anything. Where a decision is mine, take your recommended option and log it under "Decisions made for you". If something needs my approval (a new network host, a login, spending), skip it and log it under "Needs you".
- Update the design doc first (https://claude.ai/artifact/Ld2BuxHtt8Ht8LpRbVxeJT): rewrite the Gameplay, UX and Build Plan tabs for the new direction, and keep the old desire-line design in a clearly marked "earlier direction" section.
- Commit and push to Marten-Mrfc/Tide-Toll on the claude/prototype-1 branch after every round. No pull requests, no pushes to main. Start work-in-progress commit messages with "Snapshot" so GitHub only builds an exe for finished rounds.
- Make the loop survive a container restart: keep exactly one scheduled check pending, and resume interrupted rounds from the log.
- Never send a message to an agent inside a running workflow; put what it needs in a file its brief reads.
- €0 budget. No asset without a ledger row. Nothing with a non-commercial or unclear licence.
- Never weaken a test to make it pass. Report failures plainly. Tests that only covered the old walker system may be retired, but say which and why.
- After each round, judge fun honestly: play it with real input and say how many decisions a player makes per minute and how long until there is nothing left to do.

Stopping
- Start no new round after 05:00 UTC, so everything is reviewed, pushed and built by 06:00 UTC. Cancel the scheduled check when you stop.
- Then publish a screenshot review page as an Artifact and post the same list here. At least 30 numbered screenshots at 1920 x 1080, taken from the final build, in play order: the first minute (1 house, 1 villager), each upgrade bought, each milestone and what it added, a road wearing in and then paved, the town at the end of the age, the age change, every tool tab and readout, the zoomed-in view, zooming out until the book appears, the closed book as the pause menu, every settings page, the achievements, and anything that still looks wrong. Under each screenshot: one line saying what it shows, and one yes-or-no question for me.
- End with: the link to the newest Windows build, what is fun and what is not in your honest view, "Decisions made for you", "Needs you", and the list of music pieces to listen to.

## Where things are

- Design brief for builders: `docs/plans/incremental-design.md` (the lead's; change it only on purpose).
- Design doc for the owner: Claude Docs id `9ee52479-fb74-49e2-9063-a1585cdc1e40`. Tab body nodes: Build Plan `aa88b9bc-2a93`, Overview `654f3c1a-1b75`, Gameplay `7e2d7651-21ec`, UX `ba135eb2-f67b`, Sound `6ddff1a9-fd33`, Art `d64ee9a3-3b89`, Engine `b2ac323d-e193`.
- New simulation: `sim/Town/` (tests in `sim.tests/Town/`), interface note `docs/plans/town-sim-api.md`, pacing tables `docs/plans/town-pacing.md`.
- Agents' progress files: `docs/plans/*-progress.md`. Reports: `docs/plans/` (named per round below).
- Workflow scripts, copied for resuming: `build/wf/` (not in git).
- Play-test checkouts (git worktrees at a snapshot commit): `build/pt/` (not in git).

## How to resume after a restart

1. `date -u`. If it is past 05:00 UTC on 26 November, start no new round: go to "Stopping" in the brief.
2. `git status` and `git log --oneline | head`. Commit anything uncommitted with a message starting "Snapshot" and push (`git -c push.negotiate=true push -q -u origin claude/prototype-1`).
3. Read "Rounds" and "Next steps" below. If a workflow was running, resume it: `Workflow({scriptPath: "/home/user/tide-toll/build/wf/<file>.js", resumeFromRunId: "<id below>"})`. Finished agents come back from the cache; interrupted ones run again and continue from their progress files.
4. Never use SendMessage on an agent inside a running workflow: it starts a second copy. What an agent needs goes in a file its brief reads.
5. Keep exactly one scheduled check pending (`send_later`, 45 minutes). The one-shot switches itself off after firing: arm a new one each time, and delete the pending one before arming another. Cancel it when the build stops.
6. Workflows run two agents at a time on this machine.

## Scheduled check

- Pending: `trig_01PEJwqyTfVijCKX1qo1iHHx`, fires 21:07 UTC.

## Rounds

| Round | What | Workflow run | Started | Ended | Result |
| --- | --- | --- | --- | --- | --- |
| 1 | Core loop: town simulation, design doc, assets, music, the playable core, balancing, bug hunt | (filled in at launch) | 20:35 | | |

## Next steps

1. Round 1 workflow is running or about to start (see the table). When it returns: read the reports in `docs/plans/`, look at the screenshots, run `./check`, write the result here, commit and push.
2. Round 2: the book behaviour and menus (zoom out to the book, pause closes it, settings pages) by one Opus builder in `game/`; beside it a Sonnet play-test of round 1 in a worktree under `build/pt/`, then a Haiku bug hunt. The builder starts by fixing what round 1's reports list.
3. Round 3: the full first age in the game (about 30 minutes, second field, market, mill, achievements page, saving and loading), the music playlist wired in, a screenshot tour scene for the final review.
4. Round 4, only if started before 05:00 UTC: the second age with the river, or polish if the first age is not yet good.
5. Stopping: full `./check`, a finished-round commit so GitHub builds the exe, at least 30 screenshots at 1920 x 1080, the review page as an Artifact, the report, cancel the scheduled check. Also update the README text inside `.github/workflows/windows-build.yml` (it still describes the two old chapters).

## Decisions made for you

| # | Decision | Why |
| --- | --- | --- |
| 1 | The design doc is rewritten at the very start of round 1, beside the first builder, from the same brief | The doc is updated first, without the build waiting for it |
| 2 | New text goes at the top of the Gameplay, UX and Build Plan tabs; everything older stays below a heading "Earlier direction". Nothing is deleted. A short note at the top of the Overview tab says the direction changed | You asked to keep the old design; a note on the Overview stops the tabs contradicting each other |
| 3 | Levels are reached by buying upgrades: every purchase adds renown to the level bar | Your wording: coin buys upgrades, upgrades reach milestones |
| 4 | Roads have a comfortable number of walkers; above it they slow down. Workplaces have a few places and villagers queue | Without some pushback "more villagers" is always the right answer and there is nothing to decide |
| 5 | Paving is bought per road by clicking a worn-in road on the map | It keeps one decision on the map itself now that roads are no longer drawn |
| 6 | A town bell the player can ring for a short hurry, with a rest time | Something to do with your hands while saving up |
| 7 | The town is laid out from a seed; seed 1 is the default town, "New town" takes a new seed | Replay value for free, and tests can check 100 towns |
| 8 | The old simulation and its fast tests stay and keep running. The old game scenes move to `game/legacy/` and leave the build; their game tests are retired and listed in `docs/plans/retired-tests.md` | The second age may reuse the river code; the old scenes cannot run beside the new game |
| 9 | No earnings while the game is closed; it continues where it stopped | Simplest honest behaviour for a first version |
| 10 | A few new models and icons are made in code tonight (mill, bell, flour sack, interface icons), with ledger rows | The mill and the new buttons have no art yet |

## Needs you

(nothing yet)

## Retired tests

(filled in when round 1 retires the old game tests)

## Music pieces

(filled in when the music agent reports)

## Fun, judged after each round

(filled in after each round)

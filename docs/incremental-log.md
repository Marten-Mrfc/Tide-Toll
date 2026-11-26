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
- Separate build checkouts (git worktrees): `build/wt/` (not in git). `build/wt/settings` holds the settings pages until round 2 copies them in. If it is lost, re-create it with `git worktree add --detach build/wt/settings <commit>`, run `tools/sync-assets.sh` in it, and resume the workflow.
- Workflows cap at two agents each; a second workflow may run beside the first (three agents at once is the most this machine should carry).

## How to resume after a restart

1. `date -u`. If it is past 05:00 UTC on 26 November, start no new round: go to "Stopping" in the brief.
2. `git status` and `git log --oneline | head`. Commit anything uncommitted with a message starting "Snapshot" and push (`git -c push.negotiate=true push -q -u origin claude/prototype-1`).
3. Read "Rounds" and "Next steps" below. If a workflow was running, resume it: `Workflow({scriptPath: "/home/user/tide-toll/build/wf/<file>.js", resumeFromRunId: "<id below>"})`. Finished agents come back from the cache; interrupted ones run again and continue from their progress files.
4. Never use SendMessage on an agent inside a running workflow: it starts a second copy. What an agent needs goes in a file its brief reads.
5. Keep exactly one scheduled check pending (`send_later`, 45 minutes). The one-shot switches itself off after firing: arm a new one each time, and delete the pending one before arming another. Cancel it when the build stops.
6. Workflows run two agents at a time on this machine.
7. GitHub cancels a Windows build that is still running when the next push arrives (snapshot pushes too). After pushing a finished round, wait until its run has completed (`gh run list --repo Marten-Mrfc/Tide-Toll --limit 3`, about two minutes) before pushing anything else.

## Windows builds

| Round | Commit | Run | Result |
| --- | --- | --- | --- |
| 1 | `55543fc` | https://github.com/Marten-Mrfc/Tide-Toll/actions/runs/45705589636 | success, artifact `TideToll-windows-x64` |
| 2 | `9298da5` | https://github.com/Marten-Mrfc/Tide-Toll/actions/runs/45716021068 | success, artifact `TideToll-windows-x64` |
| 3 | `3e7906f` | https://github.com/Marten-Mrfc/Tide-Toll/actions/runs/45726474639 | success, artifact `TideToll-windows-x64` |
| 5 | `d54ebaf` | https://github.com/Marten-Mrfc/Tide-Toll/actions/runs/45751507941 | success, artifact `TideToll-windows-x64` (both ages; before round 6's fixes) |

## Scheduled check

- Pending: `trig_015BhNwiP85coBqMC31GHPoz`, fires 04:45 UTC on 26 November. (Earlier ones fired and were handled.) Cancel it with `delete_trigger` once the final report is posted.

## Rounds

| Round | What | Workflow run | Started | Ended | Result |
| --- | --- | --- | --- | --- | --- |
| 1 | Core loop: town simulation, design doc, assets, music, the playable core, balancing, bug hunt | `wf_2cd4c43f-ce4`, script `build/wf/r1.js` | 20:32 | 21:33 | DONE. `./check --export` passed at 21:32 (sim 179 tests, game-smoke, game-town with real input to level 4, game-perf 0.11 ms a frame, five screenshots, ledger 253 rows, Windows export). Design doc 20:34, models and icons 20:42, music 20:55, simulation 20:56, first balancing 21:05, bug hunt 21:20 (nothing serious), game core 21:32 |
| 1b | Settings screen as pages of the book, built on its own in the separate checkout `build/wt/settings` (new files only; handover note `build/wt/settings/docs/plans/settings-handover.md`); round 2's builder copies it in | `wf_4a702972-214`, script `build/wf/settings.js` | 20:35 | 20:54 | Done, test passes three times in a row; copied into the main checkout at 20:55 (`game/scripts/settings/`, `game/tests/Settings*`), handover `docs/plans/settings-handover.md`. Not yet opened from a pause menu |
| 1c | Research: how incremental games pace their loop, as checkable rules (`docs/plans/incremental-research.md`) | `wf_424f4e35-7c9`, script `build/wf/research.js` | 20:39 | 20:41 | Done. Search found nothing on Incremental Traffic itself; sourced facts are cost growth of about 1.07 to 1.15 per level with roughly linear income; four more checkable targets added to the design brief |
| 1d | Rules v2 of the simulation in the separate checkout `build/wt/sim2` (local branch `wt/sim2`, committed there by the lead), and the missing sound effects | `wf_ed8099c3-fab`, script `build/wf/rules2.js` | 21:10 | 21:32 | Sounds DONE (44 effects, a mill loop, `assets/audio/sfx/town-sounds.json`, 46 ledger rows). Rules v2 built and documented but NOT green: 6 of 10 pacing tests fail (late minutes with 1 purchase instead of 2; a few seeds a little outside the bell, request and age bands), so it is not merged and the second balancing was skipped. Round 2 continues it (v2.1) |
| 2a | Main checkout, one agent after the other: (1) look and camera; (2) pause closes the book, the cover menu, settings joined, saving | `wf_c7da1efb-015`, script `build/wf/r2a.js` | 21:36 | 22:44 | DONE. `./check --export` passed at 22:44 with 16 steps (new: `game-book`, `game-settings`, `game-menu`, `game-save`). Part 1 at 22:01: villagers readable, tilt, narrow roads, continuous zoom to the book, summary page. Part 2 at 22:44: Esc closes the book, the cover is the menu (Resume, Settings, New town, Quit), settings pages open from it, the game autosaves and continues, a damaged save is set aside. Not built: interface labels still show default keys after a rebind; the music rotation |
| 2b | Rules v2.1 in `build/wt/sim2`, then balancing; and a real-input play-test of round 1 in `build/pt/r1` | `wf_364eac87-5f9`, script `build/wf/r2b.js` | 21:36 | 22:15 | Play-test DONE (`docs/plans/r1-playtest.md`). Rules v2.1 built (steps unlock with levels, adaptive requests, smooth roads) but 4 pacing tests still red (16 misses, down from 96), so the balancer was skipped; committed on `wt/sim2` as `1c98fbe`; continued as 2c |
| 2c | Rules v2.2 in `build/wt/sim2`, then the balancer | `wf_952633fe-0b0`, script `build/wf/r2c.js` | 22:17 | 22:43 | DONE and green: 203 fast tests pass, no pacing misses on seeds 1 to 10. Committed on `wt/sim2` as `98130e6`. Balance report `docs/plans/town-balance-r2.md`: about 158 purchases an age, no kind above 26 percent (speed was 80, now 16), 3 to 5 kinds within reach at once |
| 2d | Art and sounds for the second age (`docs/plans/age2-design.md`). Main checkout, `assets/` only | `wf_cccbed52-22a`, script `build/wf/age2-assets.js` | 22:19 | 22:32 | DONE: 6 models (pasture, sheep, weaver's cottage, crane, seal stone, tide post), 14 icons, 23 sounds and `assets/audio/sfx/river-sounds.json`, all with ledger rows |
| 3s | In `build/wt/age2` (local branch `wt/age2`): rules v3, then the simulation of the second age, then a balancer | `wf_e793e2e2-16b`, script `build/wf/age2-sim.js` | 22:45 | 23:55 | Built, NOT green, not merged: rules v3 (choices of two, the charter drive, worth shown, extra hands) and the whole second age (landing, tide, boats, wool and cloth, ford, ferry, footbridge, drawbridge and toll, 12 levels, the seal) work and are documented, but 6 pacing tests are red (2 in the first age, 4 in the second). Committed on `wt/age2` as `a791595`. Balance report `build/wt/age2/docs/plans/town-balance-r3.md`. Continued as 3g |
| 3g | Green pass in `build/wt/age2`; beside it ten small props | `wf_58f9760a-725`, script `build/wf/green.js` | 23:57 | 00:42 | Props DONE. Green pass: 5 of the 6 red pacing tests now pass (touches on a steady beat, tracks that go on, age lengths tied to income, the game builders' wishes); 1 first-age test still red (3 of 40 runs: a minute with 1 purchase instead of 2, twice, and 3 instead of 4, once); riverside villagers living in their own cottages not built. Committed on `wt/age2` as `ab58a04`. Continued as 3h |
| 3h | Green pass 2 in `build/wt/age2` | `wf_5867e33c-56b`, script `build/wf/green2.js` | 00:43 | 01:02 | DONE and green: the whole fast suite passes three times in a row (249 tests). A cheap ladder ("Regulars": each makes every delivery pay 0.4 percent more) keeps something always on sale in the first age. Riverside villagers living in their own cottages left out on purpose. Honest note: on ten further towns that are not tests (seeds 11 to 20), 8 of 80 runs miss a target a little (65 to 90 seconds with nothing affordable instead of 60; landing share 0.31 and 0.33 instead of a third; one minute with 3 purchases instead of 4), so the margins are thin. Committed on `wt/age2` as `a8dea76` and merged into `claude/prototype-1` at 01:03 |
| 3a | Main checkout (rules v2.2 merged at 22:46 as `b24db6d`): (1) the game learns rules v2 and fixes `docs/plans/r3-review.md`; (2) sounds, music rotation, achievements page, charter moment, a whole-age real-input check. Beside it a play-test of round 2 and a bug hunt | `wf_5f563f02-5ca`, script `build/wf/r3a.js` | 22:47 | 00:02 | DONE. Part 1 passed `./check` at 23:37. Part 2's `./check --export` at about 00:00 passed every step except `ledger`, which failed only because another agent's ten props had no ledger rows yet (they have them now: 307 rows, 0 problems). The lead re-runs the whole check on a clean copy of the commit before pushing it as a finished round. New step `game-playthrough`: a whole age by real input only, charter at minute 30.9 with 161 purchases. Play-test of round 2: `docs/plans/r2-playtest.md`. Bug hunt: `docs/plans/r3-game-bugs.md` |
| 4 | (a) Main checkout: fixes, part A of `docs/plans/r4-review.md`. (b) In `build/wt/game2`: the second age in the game | `wf_56580305-56c`, script `build/wf/r4.js` | 00:06 | 01:07 | (a) DONE: all 19 fixes, each with a test; `./check` passed (17 steps). (b) Built: ringing in, the landing, boats with cargo, the tide in the water and on a dial, wool and cloth, ford stones, ferry with riders and queues, footbridge, drawbridge, the seal, a river group in the shop, new step `game-river`; every game step passed there except `game-perf` (the first-age town allocated 3442 bytes a frame against a gate of 1536; cause not found). Committed on `wt/game2` as `1788800`. All three lines (fixes, river game, finished simulation) merged into `claude/prototype-1` at 01:08 with no conflicts (`ff2f40b`); the merged tree builds but has NOT passed `./check` as a whole: that is round 5's first job. No finished-round commit for round 4 |
| 5 | Main checkout: the merged game made whole. Beside it in `build/wt/age2`: river polish in the simulation; and a ledger and licence check | `wf_f8eda5b0-d25`, script `build/wf/r5.js` | 01:10 | 03:49 | DONE. Game: `./check --export` passed with 18 steps at 03:49 (sixth full run; the earlier ones failed on the builder's own new lines or ran into the screenshot step's time cap, which was raised from 1200 to 2400 seconds: a time limit, not a gate). Fixed from the merge: a drag over a card that paved a road; 3442 bytes a frame (now 599; two places built new names every frame); river pictures taken with the book closed; pausing in the second age. Added: the choice-of-two card (click or Z / X), the charter drive card, worth on every shop line, milestones, extra hands with a marker, Regulars, small touches drawn, both ages on the summary and achievements pages, short boat labels, lanes worn into the ground like roads. Left: the seal's page still says "not in the game yet"; a stake label can lie over the landing; a new lane barely shows until worn. River polish: DONE and green (257 tests three times), on `wt/age2` as `f549b1a`, merged into `wt/world`. Ledger check: clean (`docs/plans/ledger-check.md`) |
| 5t | The screenshot tour in `build/wt/tour` (new files only: `game/tests/Tour*.cs`, `game/tests/tour.tscn`, `tools/tour.sh`; plan `docs/review/shots-plan.json`; handover `build/wt/tour/docs/plans/tour-handover.md`) | `wf_ace0e2e2-4d1`, script `build/wf/tour.js` | 01:12 | 03:08 | DONE: `tools/tour.sh` plays one town (seed 1) from the first minute to the borough seal in about 16 minutes and writes `build/tour/<id>.png` and `build/tour/shots.json`; 47 of 50 pictures made in its checkout (the other 3 are things round 5 added afterwards). Options `--only=id,id`, `--no-build`, `--notes-only`. Godot crashed (segfault) in 2 of 8 runs on this machine's software renderer; the script retakes missing pictures once after a crash. The agent's notes on what looks wrong in each picture became round 6's fix list, `docs/plans/r6-review.md`. Its files still have to be copied into the main checkout (they are already in `build/wt/ui`) |
| 6u | Round 6, interface half, in `build/wt/ui` (local branch `wt/ui`) | `wf_2d0d7730-bef`, script `build/wf/r6u.js` | 03:11 | 03:43 | DONE: all 11 items of part U with tests; the four interface test scenes pass; 17 of 18 pictures retaken and read; 8 later fixes are covered by tests only. Committed on `wt/ui` as `731ff82` |
| 6w | Round 6, world half, in `build/wt/world` (local branch `wt/world`, with the river polish merged and the tour's files) | `wf_64381090-7de`, script `build/wf/r6w.js` | 03:13 | 03:31 | PARTLY: game tests pass against the river polish (boat labels in loads); W1 (charter stone visible), W2 (the grey rectangle across the river gone; ford shallows only once the ford is open) and W12 (crowding hint waits 8 seconds) done; W3 to W11 not started because the agent misjudged its time. One assertion changed in `RiverTest` (now requires that people cross the ford before the ferry; no longer requires waiting at high water, since the ford-only stretch can fall at low water). Continued as 6w2 |
| 6w2 | World half, second pass, same checkout | `wf_5f5e5ada-224`, script `build/wf/r6w2.js` | 03:31 | 03:58 | DONE in part: people drawn apart and kept out of buildings (W3), a new trail plainly visible (W5), bigger loads drawn bigger (W6), a second lean-to for two extensions (W7), a strong red for a crowded road (half of W8), the summary page stays until the cover is down (W10), a pale charter stone; all game test scenes pass; no assertion changed. Not done: the Pave highlight's colour and the page wipe at the age change (both in interface files it could not touch), the crash (W11). The ferry raft (W4) needed no change: a new picture shows it in mid-river. Committed on `wt/world` as `8339aeb` |
| 6 | Merge of 6u (`133de27`) and of 6w with the river polish and the tour's files (`c7973d5`) into `claude/prototype-1`, both without conflicts; the eight game test scenes passed on the first merge. Final verification on a clean copy `build/ck/final`: `./check --export` (log `build/ck-final.log`, last line `exit 0` when passed), then `tools/tour.sh` for the review pictures | (lead) | 03:59 | | |

## Next steps

No new round is started any more. What is left, in order:

1. `build/ck-final.log` ends with `exit 0`: commit the final finished round ("Round 6: ..."), push that commit alone (`git push origin <commit>:refs/heads/claude/prototype-1`), wait for the GitHub build, record it under "Windows builds". If it ends with a failure: say so here, look at the failing step, and decide whether the round 5 build (`d54ebaf`, verified and built) is the one to hand over.
2. Run `tools/tour.sh` in the main checkout (about 16 minutes; it retakes missing pictures once after a crash). Then a Sonnet agent looks at every picture in `build/tour/` and rewrites the `shows` and `wrong` notes in `build/tour/shots.json` (the notes in `game/tests/TourNotes.cs` describe the build before round 6 and are stale).
3. Write `docs/review/closing.json` (intro, facts, the Windows build link, what is fun and what is not, what still looks wrong, decisions, needs you, music). `python3 docs/review/make_review_json.py`, `python3 docs/review/build_review.py`, publish `build/review/index.html` as an Artifact with the files in `build/review/files.json`. Post the list (`build/review/list.md`) in the chat.
4. Write the morning report into the design doc's Build Plan tab under the heading "Morning report" (doc id `9ee52479-fb74-49e2-9063-a1585cdc1e40`, Build Plan body node `aa88b9bc-2a93`; the placeholder line reads "Will be written here by 07:00 Amsterdam time on 26 November.").
5. Post the final report in the chat and cancel the scheduled check (`delete_trigger`).

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
| 12 | Rules v2 after the first balance report: walking speed and the other upgrade tracks are finite (about 20 clear steps, not 170 taps); new tracks for working speed, the value of each good, and the bell; cottages can be extended by clicking them; unpaved roads clog sooner; about 16 levels instead of 12; timed requests ("12 Grain within 90 seconds") as goals a minute or two away; one line of advice at a time | The first numbers made 80 percent of all purchases the same cheap speed upgrade: a tap-fest with one right answer |
| 13 | New sound effects are made in code for buying, levels, gifts, paving, requests, the book and the interface, with ledger rows | The new loop had no sounds of its own, and feedback at the moment of earning is most of what makes such a game pleasant |
| 14 | The pause piece "Still Pages" stays 2 dB quieter than the other music, as its composer chose | A paused book should be quieter |
| 15 | Zoomed in, the camera tilts so buildings and villagers show their sides, and flattens to straight down as you zoom out to the page and the book | From straight above, villagers are dark dots and every cottage is the same square roof; the first screenshots showed it plainly. You can say no to this in the review |
| 16 | Roads are smoothed into curves in the town plan, and paved roads are drawn narrower | The first plan's lanes had sharp zigzags and the cobbles looked as wide as the cottages |
| 17 | Requests adapt: each one met makes the next a little harder, each one missed a little easier | A fixed difficulty was too easy on some towns and too hard on others |
| 18 | Upgrade steps unlock with town levels (a few steps of each track per level) instead of all being for sale from the start | Otherwise the tracks are bought out by minute 15 and the last ten minutes have little to buy |
| 19 | The worth of the bell and the share of requests met are measured as averages over ten towns, with wide limits for any single town | A single run has only about a dozen requests, so one run is too noisy to hold to a narrow band |
| 20 | The purchase-rate target (4 a minute early, 2 a minute later) is checked on the three players the design brief names (cheapest, best payback, random). The saving bot, added later, waits on purpose, so it gets its own checks instead: it buys every kind of thing, finishes in 25 to 40 minutes, and never goes 90 seconds without buying | A floor on purchases per minute measures nothing for a player whose plan is to wait; the three original players must still meet it on every town with no exceptions |
| 21 | The bell's worth is measured on frozen towns (same town, five minutes, no purchases, with and without ringing) | Two whole games with and without the bell buy different things, and that noise was larger than the bell |
| 22 | The second age is designed (`docs/plans/age2-design.md`): a landing where boats buy goods, a tide that decides when boats come and when the ford can be walked, wool and cloth from the far bank, and crossings that arrive in order (ford, ferry, footbridge, drawbridge with a toll), ending with the borough seal | You asked for the river, boats and crossings to come back as the second age; this is how they fit a game where things are given by levels |
| 23 | Rules v3 for the first age: at about four levels the town chooses one of two boons and keeps it for good; the last stretch before the charter is a "charter drive" request instead of a wait; every shop item says how much coin a minute it would add; a workplace where people keep queueing offers an extra place | The second balance report: every player ends up buying the same 158 things, so the only choice is the order. Choices of two make each town the player's own |
| 24 | Small touches: as the town prospers, woodpiles, washing lines, flower beds, hen coops, benches, beehives, haystacks, apple trees, handcarts and signposts appear by themselves beside cottages and lanes. They cost nothing and do nothing | The late levels left up to five minutes with nothing new on the map; numbers alone could not fix that, and a town that dresses itself is pleasant to watch |
| 25 | The upgrade tracks of the first age go on through the second age, a few steps per level | Otherwise players run out of cheap things to buy and wait |
| 26 | Touches also arrive on a timer: if nothing new has appeared for about three minutes, one comes anyway | The builder found that tying them to coin alone still left gaps of four to five minutes on some towns |
| 27 | "Regulars": a cheap purchase that is always on sale in the first age; each regular customer makes every delivery pay 0.4 percent more | The last red pacing test needed something small to buy in every minute; numbers alone only moved the gaps around. If it feels like filler when you play, say so in the review |
| 28 | Villagers housed in the cottages by the river and on the far bank still walk home to a first-age cottage | Making them live by the river safely in every state of the tide could not be built and tested tonight; the builder left it out rather than half done |
| 29 | Round 4 and round 5 have no Windows build of their own: three lines of work were merged in round 4 and made whole in round 5 and 6, and only a tree that passed the whole check is pushed as a finished round | You asked for an exe only for finished rounds |
| 30 | The ford is used on about half of the towns, including the default one; on the others villagers find other work until the ferry comes | Making it certain on every town broke another target (the landing's share of income); the builder kept the tested state |
| 11 | The settings screen is built at the same time as the core loop, in a separate checkout, and joined in round 2 | It does not depend on the new simulation, so it need not wait |

## Needs you

1. **Play it on a real Windows machine.** Nothing was run on Windows or on a real graphics card tonight: every picture and timing comes from a software renderer in a cloud machine. Frame rate, sound, window modes and resolutions are untested on real hardware.
2. **Listen to the music and the sounds.** Six music pieces and about 70 new sound effects were made and checked by measurement only. Nobody has heard them.
3. **Watch for a crash.** Godot crashed with a segfault in 2 of 8 runs of the screenshot tour on this machine's software renderer (once just after the book closed for a pause, once at the age-change page). It never happened in a check run. If the exe crashes on your machine at those moments, that is this.
4. **Free asset sites are blocked from this machine** (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org), so every asset was made in code instead of first looking for a free one, as you asked. If you want that search, those hosts have to be allowed in the environment's network settings.
5. **The exe is not signed**, so Windows SmartScreen will warn on first start ("More info", then "Run anyway").

## Retired tests

Round 1 retired the game tests that only covered the old game, with their scenes (moved to `game/legacy/`, out of the build). The full table, test by test with the reason, is `docs/plans/retired-tests.md`. In short:

- `game-smoke` (old), `game-tools`, `game-ui`: the old world view, the fence, footbridge and erase tools, the tool tabs. Gone because the player no longer draws or builds paths. Replaced by `game-smoke` and `game-town` on the new town.
- `game-river`, `game-river-rules`, `game-chapter`, `game-hamlet`: the two chapters and the river rules in the game. Gone because there are no chapters; the river returns as the second age on the new town.
- `game-perf` (old) and its re-run, and the four old screenshot steps: replaced by a new `game-perf` and new town screenshots. The old 64-bytes-per-frame allocation gate was NOT carried over (the new interface allocates about 2.5 KB a frame); that is a real loss of a check and is on the fix list.
- All simulation tests of the old desire-line and river code still run in `./check` (159 tests) beside the new town tests.

## Music pieces

All made in code by `assets/src/audio/music-*.py` with the theme's own instruments, mix and loudness (shared engine `assets/src/audio/_music.py`); ledger rows added; nobody has listened to them yet.

| Title | File (in `assets/audio/music/`) | Role | Key, tempo | Length |
| --- | --- | --- | --- | --- |
| Tide & Toll theme (the existing one) | `theme-hamlet-draft.ogg` | first-age rotation, first | D Dorian, 64 BPM | 45 s loop |
| Ferry Morning | `music-ferry-morning.ogg` | first-age rotation | D Mixolydian, 68 BPM | 74 s loop |
| The Long Field | `music-long-field.ogg` | first-age rotation | A Aeolian, 64 BPM | 79 s loop |
| Low Meadow | `music-low-meadow.ogg` | first-age rotation | D Aeolian, 60 BPM | 72 s loop |
| Tide and Toll, Rising | `music-theme-rising.ogg` | the age change | D Dorian, 64 BPM | 49 s, real ending |
| Still Pages | `music-still-pages.ogg` | the paused book | D Dorian, 60 BPM | 60 s loop, 2 dB quieter on purpose |

The game reads `assets/audio/music/playlist.json`. Not yet wired into the game (round 2 or 3).

## Fun, judged after each round

### Round 1 (21:35 UTC)

Honest verdict: it works and it is not yet fun.

- Real input: the `game-town` test plays with injected mouse and key events through the interface, but only to level 4. The numbers for the whole age below come from bots playing the simulation directly, not from input through the game. A real-input play of the whole age is running as part of round 2.
- Decisions a minute (the balancer's estimate from bot logs; a speed tap does not count as a decision): 1.5 to 2 in minutes 0 to 10, about 1 in minutes 10 to 20, about 0.5 in minutes 20 to 30.
- Until nothing is left to do: the charter comes at 26 to 36 minutes; after it there is nothing but the speed upgrade.
- What is dull: 80 percent of all purchases are the same cheap speed upgrade (172 levels of it by the end); choices have one right answer; the dear upgrades and the mill are hardly ever worth buying; crowding almost never happens; the last gifts are five minutes apart.
- What looks wrong: villagers are tiny dark dots from above; cobbled roads are as wide as cottages; lanes have sharp zigzags; the finished town fills only the middle third of the screen.
- What is good: the first minute is exactly one cottage, one well, one trail and one villager; the trail visibly wears in; staked plots show where the next gift will stand; the interface says what comes next.

Addendum at 22:16, from the real-input play of the whole age (`docs/plans/r1-playtest.md`; every action a click or a key through the interface, seeds 1 and 7):

- Purchases a minute: 11.9 in minutes 0 to 10, 4.7 in 10 to 20, 3.6 in 20 to 30; 70 percent of them the speed upgrade (180 of 258).
- Decisions a minute without counting the bell (which is always "ring it"): about 2 in minutes 0 to 10 and 10 to 20, about 1 in 20 to 30.
- Charter at 33.6 and 34.2 minutes. After it: the mill and the yoke if not yet bought, a little paving, and speed without end.
- Longest stretch with nothing to do: 19 to 20 seconds.
- The play-tester's verdict: a curious player keeps going after 5 minutes because new things keep arriving; after 15 minutes probably not, because play is "wait, tap speed, ring the bell". The dullest stretch is minutes 10 to 30.
- A real bug found by playing: only 24 of about 115 Pave clicks paved a road; the click often lands on a neighbouring road or a road that cannot be paved. On the round 3 fix list as the first item.

### Round 2 (22:45 UTC)

Honest verdict: it now looks and behaves like the book you asked for, and it plays exactly as dully as round 1, because this build still has the first rules. The second rule set is finished and tested but joins the game in round 3.

- Real input in this round: the new checks drive the game with injected wheel, mouse and key events: zoom out to the book and back, a Pave click while tilted, Esc to the cover, the cover menu by keyboard alone, the settings pages, New town, save and continue. Nobody has yet played this build through a whole age; a real-input play-test of it starts now (`build/pt/r2`).
- Decisions a minute and time until nothing is left: unchanged from round 1 (about 2 a minute early, about 1 late, charter at about 34 minutes), since the rules are the same.
- From the bots on the second rule set (not yet in the game): about 158 purchases an age, no kind above 26 percent, 3 to 5 kinds within reach at once; the balancer's estimate for a human is 2 to 3 decisions a minute in minutes 0 to 10, 1.5 to 2 in 10 to 20, about 1 in 20 to 30. Still dull there: everyone ends up buying the same things, only the order differs; most steps show only in the coin figure; nothing is left after the charter.
- What is good: the tilted view shows cottages, the mill and people as things with sides; the zoom out to the open book with the town's name and summary is one smooth move; the closed cover as the pause menu looks like the cover of this book.
- What still looks wrong: lanes zigzag (smooth roads arrive with the new rules); staked plots look like an editor's boxes; coin numbers are small and grey; the left page of the book is mostly empty.

### Round 3 (00:05 UTC, 26 November)

Honest verdict: this is the first build that plays like a game. It is pleasant for about twenty minutes and thin in the last ten.

- Real input: the new check `game-playthrough` plays the whole first age using only injected clicks and keys through the interface, like a sensible person (it points at a road or a cottage until the tip names it, then clicks).
- Purchases a minute: 6.1 in minutes 0 to 10, 5.2 in 10 to 20, 4.5 in 20 to 30. 161 purchases in all: no longer one button (speed is 16 percent of purchases in the bots' runs; it was 80).
- Decisions a minute (a proxy: looks at which two or more kinds of thing could be bought): 2.0 in minutes 0 to 10, 1.9 in 10 to 20, 0.8 in 20 to 30. Plus 24 roads paved and 14 cottages extended by clicking on the map, and 73 bell rings.
- Until nothing is left: the charter at minute 30.9. After it the shop is bought out; the game says honestly that the first age is complete and the river comes next.
- What is better: villagers in six colours on curved lanes; a grouped shop with a bar toward each price; requests from townsfolk ("185 Water within 85 s") that give something to aim for each minute; a hint line with a marker; a banner for each level; coin numbers you can read; every event has its own sound; the music rotates through the four pieces and changes on pause and at the age change; achievements have a page in the book.
- What is still dull: everyone ends up buying the same things, only the order differs (rules v3 adds choices of two, not yet in the game); the last ten minutes have under one decision a minute; steps of 3 to 4 percent are felt only in the coin figure.
- What is still wrong: game keys act under the closed cover; deleting the save leaves a save; stray marks beside the numbers on the summary page; an extended cottage looks like a plain one; staked plots still look like an editor's boxes. All on round 4's list (`docs/plans/r4-review.md`).

### Round 4 (01:10 UTC)

No finished-round build for round 4: its three lines of work each passed alone, and the merged whole is being made green in round 5. What can be said honestly now:

- The 19 faults from the play-test of round 2 and the bug hunt are fixed, each with a test that would have caught it (keys under the closed cover, clicks through cards, Delete leaving a save, stray marks on the summary page, and the rest).
- The second age exists in the game for the first time: I looked at the builder's screenshots. The ferry with riders and a queue on the bank, a barge loading at the landing, the tide dial, sheep on the far bank and the river group in the shop all read well. The new lanes are blocky tan strips with square corners and look worse than the first age's roads; boat labels overlap the quay; in the full screenshot run the river shots showed the closed cover instead of the river (a fault in the shot order, and perhaps a real one when pausing in the second age). All three are in round 5's brief.
- With rules v3 in the simulation, the whole-age real-input play of the first age (before the game shows the choices) gave 6.9, 6.2 and 4.3 purchases a minute for minutes 0 to 10, 10 to 20 and 20 to 30, and 3.3, 3.0 and 1.3 decisions a minute by the proxy; the charter at minute 35.2 with 201 purchases. Better than round 3 (2.0, 1.9, 0.8), mostly because more kinds of thing are within reach at once.
- Nobody has played the second age with real input yet. The bots finish it in 22.5 to 33.8 minutes on the ten test towns.

### Round 5 (03:50 UTC)

Honest verdict: both ages are now in one game that passes its whole check, and the first age is the best it has been. The second age has only been played by bots and by the screenshot tour.

- Real input, first age (the whole-age play through the interface, seed 1): purchases a minute 7.0, 6.5 and 4.1 for minutes 0 to 10, 10 to 20 and 20 to 30; decisions a minute by the proxy 3.8, 3.2 and 1.7; the charter at minute 33.4 with 201 purchases, 24 roads paved and 14 cottages extended by clicking the map, 4 boons chosen (2 by click, 2 by key), the charter drive met, 17 small touches and 14 milestones along the way.
- Until nothing is left: the first age ends at about 33 minutes and leads straight into the second; the bots finish the second in 22 to 34 minutes on the ten test towns. After the borough seal a page says the second age is complete; a few upgrade steps may still be on sale.
- Real input, second age: only the ring-in, a pause and resume, and the pictures of the tour. No whole second age has been played through the interface by an agent or a person. That is the biggest gap in what I can honestly claim.
- What is better than round 3: choosing one of two boons four times an age; a last push for the charter instead of a wait; every shop line says what it is worth; the town dresses itself with small touches; no more dead minutes in the first twenty.
- What is still thin: minutes 20 to 30 of the first age (under 2 decisions a minute); most steps are 3 to 4 percent and felt only in the coin figure; the cheap "Regulars" ladder may read as filler; in the second age the landing earns three quarters or more of all coin, so the first age's market matters little.

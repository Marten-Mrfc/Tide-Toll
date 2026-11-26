# Night 3 (26 to 27 November 2026): one designed game

Every agent of this night reads this file first, then `AGENTS.md`, then its own brief. The lead (the session that started you) plans, reviews and decides; builders build; reviewers judge from pictures.

## What the owner said after playing the Windows build (commit `b797857`)

- The game is fun to play. **Do not change how it plays** unless your brief says so: no change to prices, timings, levels, what a purchase does, the simulation in `sim/`, or the save format (old saves must still load).
- The interface, the models and the popups feel off. **The interface layout is the worst: it feels chaotic.**

## Goal of the night

Keep the play as it is. Make the interface calm and ordered, make popups behave as one system, and make the town beautiful to look at.

## The night's documents (all in `docs/plans/n3/`)

| File | What |
| --- | --- |
| `brief.md` | this file |
| `lead-notes.md` | the lead's notes to agents at work, newest first. **Read it again whenever your brief says "check the lead's notes"**; nobody can send you a message |
| `ui-inventory.md`, `message-inventory.md` | every interface element and every message the game shows, from the code |
| `audit-ui-*.md` | reviewers' findings on the interface pictures |
| `layout-sheet.md` | the one layout the interface is built to (zones, grid, sizes, styles) |
| `message-system.md` | the one message system (kinds, places, priorities, lengths of stay) |
| `style-sheet.md` | the art style every asset is checked against |
| `asset-ranking.md` | every asset, worst first |
| `*-progress.md` | each builder's own progress file: what is done, what is left, how to resume. Update it after every finished part, so that another agent can take over if you stop |

## Rules for every agent

- **No git commands that change anything** (no commit, add, stash, checkout, reset, merge, worktree). The lead commits. Work only in the checkout your brief names.
- **Time:** read the clock with `date -u` and keep to the clock deadlines in your brief. Never judge elapsed time by feel. What you have not reached by the deadline is left out and named in your final answer.
- **Never weaken a test to make it pass.** Report failures plainly. A test that only fits the old layout may be replaced by the equal test for the new one; say which and why.
- **Judge looks from pictures, not from code.** After building something that is seen, take the picture and READ it (the Read tool shows images), fix what you see, take it again.
- 0 euro. Everything is made in code. No asset without a row in `assets/LEDGER.csv`. Nothing with a non-commercial or unclear licence. No downloads of assets.
- Files under about 400 lines, one system per file (`AGENTS.md`).
- The game's text is plain British English, short sentences, no jargon.

## The machine

- 4 cores, no graphics card. Pictures need `xvfb-run` with the Mobile renderer on software Vulkan, about one frame a second: see how `tools/tour.sh`, `tools/motion-strips.sh` and the screenshot step of `tools/check.sh` do it. Measure CPU per frame headless; never judge by frames per second.
- **Other agents work at the same time in other checkouts.** A test that measures time can fail under load: run it again alone before you conclude anything, and say so.
- The whole `./check` takes about 20 minutes (the screenshot step is the slow one). Do not run it in a loop: run the steps your change touches while you work, and the whole check once at the end.
- Godot has crashed in a few screenshot runs on this renderer (cause unknown). If a picture run dies without a failed check, run it once more before reporting.

## Final answers

Your final answer goes to the lead, not to the owner. Plain facts: what is done and what is not, the printed results of the tests you ran, where the pictures are and what you saw in them (faults included), every existing assertion you changed with the reason, and anything you would do next.

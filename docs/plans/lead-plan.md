# The lead's plan for rounds 2 to 4 and the final review

Notes for the lead (and for a lead who resumes after a restart). Builders get their own briefs; this file is where those briefs come from.

## Round 2: the book behaviour and the menus

Launch as soon as round 1's game core is reviewed and committed. Before launching: commit a snapshot, create a play-test checkout (`git worktree add --detach build/pt/r1 HEAD`, `tools/sync-assets.sh` in it, copy `game/.godot` in, build once), and write `docs/plans/r1-review.md` (what round 1's reports and the lead's look at the screenshots say must be fixed first).

Agents (two at a time per workflow):

- **A, Opus, main checkout, about 100 minutes.**
  1. Fix what `docs/plans/r1-review.md` lists.
  2. Join the settings pages: copy the new files named in `build/wt/settings/docs/plans/settings-handover.md` into the main checkout, repeat its listed changes to existing files, add its test as the check step `game-settings`, move the game's own key handling onto the InputMap actions of `GameInput.cs`, and make the game obey the settings (interface size, coin numbers, follow the town, autosave interval, volumes).
  3. Zoom: one continuous zoom from close on the town, to the whole page, to the whole book on the table. The interface is full while zoomed in, thins to a slim strip as the page comes into view, and when the book is in full view the left page shows the town's summary as a page of the book (town name, age and level, villagers, cottages, roads and how many are paved, coin earned in all, trips, time played, next gift, achievements n of m). Wheel and plus/minus zoom, Home returns to the town. The simulation keeps running while zoomed out. One margin note early on: "Scroll out to see the whole book".
  4. Pause: Esc or the pause control. The camera pulls back, the book closes with an animated cover, the simulation stops, the music softens. The closed cover is the menu: title, town name, Resume, Settings, New town, Quit, usable with mouse and keyboard. Resume opens the book and returns the camera to where it was. Settings opens the book at the settings pages and Back returns to the cover. New town asks once, then starts from a new seed. Quit saves and quits.
  5. Saving: the game saves itself (interval from the settings, and on pause and quit) through the simulation's save, continues the saved town on start, puts a damaged save aside and starts a new town with a note, and supplies `ISaveActions` to the Saving page. Town names come from the seed.
  6. Tests: `game-book` (zoom out with wheel events until the book is in view; Esc pauses and the simulation does not advance; the cover menu by keyboard alone; Settings and Back; New town gives another seed), `game-save` (play, save, restart the scene, same state; damaged save handled), screenshots of each zoom step, the cover menu, the settings opened from the cover, the summary page.
- **P, Sonnet, play-test checkout `build/pt/r1`, about 30 minutes.** Plays round 1 with real input (injected mouse and key events through the interface, headless at high speed for the numbers; xvfb screenshots for the look). Reports decisions per minute for minutes 0 to 10, 10 to 20, 20 to 30, the time until nothing is left to do, the dullest stretches, every moment the interface did not say what to do next, and what looks wrong in the screenshots. Writes `docs/plans/r1-playtest.md` in the main checkout. A reads that file again before its final check.
- **H, Haiku, read-only, about 20 minutes.** Bug hunt in `game/scripts/` of the new town game. Writes `docs/plans/r1-game-bugs.md`.
- **S, Opus, simulation only, if round 1's balance report asks for rule changes.** Applies the accepted rule changes in `sim/Town/` with tests. Otherwise this slot starts the second age's rules (below).

## Round 3: the full first age

- **Opus, main checkout:** the whole age plays well from level 1 to the charter: every gift has its moment, offers, perks, the bell, the achievements page (left page of the book or a bookmark), achievement notes, the charter and the "ring in the next age" moment with the age-change music, the music playlist from `assets/audio/music/playlist.json` with gentle cross-fades, the pause piece while the book is closed, and what round 2's play-test lists.
- **Sonnet, play-test checkout of round 2:** as before, plus the book and menus.
- **Opus, the screenshot tour:** a test scene `game/tests/tour.tscn` that plays the default town with the simulation's bot at high speed and writes the numbered screenshots listed below at 1920 x 1080 into `build/tour/`. Deterministic, re-runnable, one command (`./check --tour` or a tool script).
- **Haiku:** licence and ledger check of every new asset row; bug hunt of the round's code.

## Round 4 (only if started before 05:00 UTC)

The second age, The River: in the simulation, boats that buy goods at a landing, the ford, ferry and bridge as gifts on the far bank, a second set of levels; in the game, the river assets and layers brought back from `game/legacy/`. If the first age is not yet good, polish it instead and say so.

## The final review page

At least 30 numbered screenshots at 1920 x 1080 from the final build, in play order. Each has one line saying what it shows and one yes-or-no question. Planned list (adjust to what exists):

1. The first minute: one cottage, the well, one faint trail, one villager.
2. The first delivery: the coin number floating up.
3. The interface at rest: coin, coin per minute, level bar, next gift, bookmarks, bell.
4. The first upgrade affordable: its bookmark lit.
5. Villager bought: two villagers on the trail.
6. Pace bought.
7. Load bought: the bigger load on a villager.
8. Level 2: the gift moment, a cottage and its lane.
9. A new road as a faint trail, close up.
10. The same road half worn.
11. The same road worn in.
12. Pave mode: worn-in roads lit, the price on the hovered road.
13. The road paved with gravel.
14. A road in cobbles.
15. The field arrives: the first grain trips.
16. A queue at the well.
17. The second bucket bought: the queue gone.
18. The market arrives.
19. The shortcut that closes a loop.
20. The bell rung: everyone hurrying.
21. The second field.
22. The mill and the first flour.
23. A perk earned.
24. An achievement note.
25. The achievements page.
26. The town at the end of the age.
27. The charter and the age change.
28. The second age, if built.
29. Zooming out: the whole town.
30. Zooming out: the page.
31. Zooming out: the whole book with the summary page.
32. Pausing: the book closing.
33. The closed cover as the pause menu.
34. Settings: Sound.
35. Settings: Display.
36. Settings: Keys, a rebind in progress.
37. Settings: Saving.
38. New town: the question.
39. A saved town loaded again.
40. The interface at 1280 x 720.
41 and on: anything that still looks wrong.

The page is an Artifact (load the artifact-design skill first; images as JPEG through `files`; one image per row with its number, line and question; a summary table at the top). The same list goes in the chat. Then: the newest Windows build link, what is fun and what is not, "Decisions made for you", "Needs you", the music list.

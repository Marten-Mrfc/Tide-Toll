# The message system (round L2 of night 3): progress

Builder of the message system, checkout `build/wt/n3-ui` (branch wt/n3-ui). Spec: `message-system.md` version 2.

## Where things live (game/scripts)

| What | File |
| --- | --- |
| A zone's queue: priority order, stay, crowd stay, cap (drop lowest), wait limit (overflow), eased in/out by `BookMotion`, gap 0.15 s, click away, a message taken back by the choice/page/zoom waits again first | `MessageQueue.cs` |
| An eased on/off value (BookMotion, continuous when turned mid-way) for things that follow the town's state | `MessageFader.cs` |
| The two large queues wired to the interface: Message zone (banners, trouble, one at a time, wait while choice/page/zoom, 12 s then a note), note section (one at a time, two lines, 90 chars), sound on show | `town/TownHud.Messages.cs` (API: `Large`, `Banner`, `Trouble`, `Note(text, strong, prio, sound)`) |
| The line: refusal 2.5 s > mode text > first-minute hint > town hint; card fade 0.2 s; text change is a cross-fade of two labels of one fixed size | `town/TownHud.Line.cs` (`Refuse(text)`, `LineKindNow`) |
| The first minute's four hints, each once ever (`settings.cfg` `[hints] hints_seen`, a bit each); `GameSettings.ShowHintsAgain()` | `town/TownHud.FirstMinute.cs`, `settings/GameSettings.cs` |
| Large sounds on their own player, at least 0.6 s apart (queue of 4) | `town/TownAudio.cs` (`Large(id)`, `LeastLargeGap`) |
| Tests | `game/tests/MotionTest.Messages.cs` (registered in `MotionTest.Run`) |

## Status

### 00:15 UTC

- 1 One message system: done. `MessageQueue` per zone; Message zone: trouble 4, charter/seal/new age 3 (charter and seal 5 s), drive 2, level 1 (3.2 s; 2.2 s with two or more waiting); waits while the choice, a page, a page turn or the zoom holds the zone (one showing is taken back and waits first); 12 s waiting becomes a note; cap 6 (lowest becomes a note). A choice level: the choice opens with "Level N:" in its heading and the gift is a note (no banner). The choice and a banner never show together: a banner showing leaves first (0.22 s), then the choice arrives. Banners and notes are clicked away (trouble only so). Trouble: `TownSaver`/`Main` failures now go to `Hud.Trouble` (brick card, until clicked).
- 2 Notes: done. One at a time, two lines, cut at 90 characters, priorities achievement 3, building/milestone/boon/choice-level gift 2, rest 1; 3.2 s, 2 s with four or more waiting, more than six waiting drops the lowest. Section 2 table: extended, paved, villager, "New to buy", worn in (Pave row pulses), request asked/met/missed, boat request (logged only), choice open, drive met: no note. Boon chosen: a note. Refusals and the scroll hint: the line.
- 3 Goal and strip: the goal's content fades in and out (0.22 s, 8 up); "Done: +N" moss and "Too late" brick stay 1.6 s (from L1). The drive met shows "Done" in moss in the strip for 1.6 s, then the strip fades. "Ring in the next age" fades in and out. NOT done: a second request in the strip in one line, a third waiting (the town and a skipper's request still share the goal section as before).
- 4 Line: done (`TownHud.Line.cs`).
- 5 On the map: coin numbers merge within 0.5 s at one building, at most 8, eased fade, and fade with Pave/Extend (0.15 s) instead of one frame; tags eased (0.15 s), scanned every 0.5 s of real time and in the three frames after a level, a building, a boat arriving or leaving; pointer tip and "Suggested" fade 0.15 s (`TownHud.ModeFade.cs`). The hint ring shows only while the line shows the town's hint.
- 6 Motion: banner, choice, note, line (card and cross-fade), goal content, drive strip, next-age button, coin numbers, tags, tip, suggest: eased BookMotion timelines. Not yet: "+ hand" markers, the tide dial, the slim purse's Visible, the Home and achievements buttons, the lit-band mesh of Pave/Extend (3D).
- 7 Sound: done for large sounds (level-reached, gift-arrives, request-met, charter-granted) on a player of their own, 0.6 s apart, played when their message appears; achievement plays when its note appears. The peal and the seal (RiverAudio, not mine) unchanged.
- 8 First minute: done (`TownHud.FirstMinute.cs`, `[hints] hints_seen` in settings.cfg; `GameSettings.ShowHintsAgain()` exists but no button: the settings pages are the next round's).
- 10 Tests: `MotionTest.Messages.cs` (banner, notes queue order/stay/gap, crowd, click away, line and cross-fade, the busiest moment, the large sounds' gap); game-layout rule 5 fails above loud 3 / quiet 4; `CheckMessages` (every message inside its zone).
- 11: per-checkout test profiles (`TownTestBase.ProfileFor`: user:// is shared by every checkout).

### 00:20 UTC

- 3 Goal: a skipper's request while the town's own holds the goal section goes to the strip in one line ("Skipper Alder: 3 of 12 Wool, 40 s", fades 0.22 s), after the next-age button and the drive (`TownHud.Strip.cs`). Simplification: the town's request always holds the goal section (as before); a third request is not shown.
- 6: "+ hand" markers fade (0.15 s, eased).
- 9 Tooltips: the play tooltip now covers achievements (page cells), the two boons, the goal, the strip and the note sections; engine `TooltipText` removed from the achievements, boons and hand markers. Not pictured yet.
- 11: shop names are set in caption size before they are cut (`ShopRow.PlaceStep`); the tour pauses first when the game plays before the settings states u21 to u24 (`UiAudit.Book.cs`). Not done: the half row at 1280 x 720 with 150 percent pictured again.
- Strips: `tools/motion-strips.sh --messages-only` (`MotionStrips.Messages.cs`, interface clocks held by `TownHud.StripStep`): msg-banner, msg-notes, msg-line, msg-request, msg-coin, msg-choice. Shop rows arriving/leaving: not made (L1's fade/slide, untouched).

### Tests changed (old assertion -> new assertion, why)

| Test | Old | New | Why |
| --- | --- | --- | --- |
| TownTest.cs level gifts | every level has a "Level N:" banner | at a choice level a "Level N:" note and the choice's heading starts "Level N:" | a decision is never delayed by a banner (brief item 1) |
| TownTest.cs CheckHint | a hint with a road or plot shows the ring | the ring shows while the line shows the town's hint; otherwise it is not at full | the line's priority: a refusal, a mode, a first-minute hint come first (message-system 1) |
| TownTest.cs Extend | no coin number visible at once when the mode starts | none within 30 frames | the numbers fade out over 0.15 s (nothing in one frame) |
| TownTest.V3 BoonByClick | banner text contains the boon; the card gone after 3 frames | the note log has "A boon for good: <boon>"; the card's opacity has started down, gone within 120 frames | message-system 2 (a boon is a note); the card leaves by its 0.22 s fade |
| TownTest.V3 choices | the card visible 3 frames after the choice opens | visible within 60 frames | one large message at a time: a banner showing leaves first (0.22 s) |
| TownTest.V3 key pick | banner text contains the boon | note log has it | as above |
| TownTest.V3 drive met | the card gone and a note "charter drive is met" | the strip says "Done" (or the next-age button holds the strip); no such note; the strip goes within 400 frames | message-system 2: the strip says it in its place for 1.6 s |
| LayoutRules rule 5 | reported only | fails above loud 3 or quiet 4 | brief item 10 |
| LayoutRules (new) | - | every message inside its zone (`CheckMessages`) | message-system 5 |
| BookTest scroll hint | unchanged; `TownHud.ScrollHint` is now the sheet's words "Scroll out to see the whole page, and the book." and goes to the line | | message-system 4 |
| TownTestBase.ProfileFor | `user://tests/<marker>/` | `user://tests/co-<hash of the checkout path>/<marker>/` | user:// is shared by every checkout (brief item 11) |

### For the lead (changes in files I may not edit)

1. `game/scripts/town/RiverAudio.cs` (asset builders' file): the seal's sound plays on the `Seal` event (around line 108). For "sound follows the message" it should be asked for by the banner instead: delete the cue there and in `TownHud.River.cs` pass `"seal-granted"` as the `sound` argument of `Large("The borough seal", ...)` (and add `"seal-granted"` to `TownHud.IsLargeSound`). The peal of the new age (TownAudio.NextAgeRung) is left as it is: it belongs to the page turn.
2. `RiverBoats.cs`, `RiverLayer.cs` (`StakeLabel`), `TownBuildings.cs` (stake label): create the "What"/"Wants" `Label3D`s with `Visible = false`. The interface draws them as screen tags; today a new 3D word is visible until the next tag scan (now at most 0.5 s of real time, and the three frames after a level, a building or a boat event).
3. (Withdrawn: a first strip showed no banner; the cause was the strip's own clock hold, not the zoom. Banners now show while the interface is at least half in, `FullAlpha >= 0.5`.)

### Results so far (00:24 UTC, machine load 15 to 23)

- game-town: TOWN PASS (after the assertion changes above); game-smoke, game-save, game-book, game-menu, game-settings: PASS.
- game-playthrough: PLAY PASS charter=33.9min buys=201 paves=24 extends=14 bells=76 clicks=204 keys=102 (1 click refused on a moving row, as before).
- game-motion alone at 60 fps: MOTION PASS, with the message lines (banner, notes order three/two/one, crowd, click away, line cross-fade, choice, busiest moment at most 1 large / 1 note / 1 line). motion-check.sh (with my builds running beside it): 120 fps PASS; 60 fps one CPU frame 8.31 ms (line), 30 fps one 240 ms frame in the book's settings (load). To rerun when quiet.
- Strips: msg-banner (read: eased arrival over 4 frames, fade and slide from above, stays, leaves eased), msg-notes (read: the first note arrives, is clicked away, leaves; the strip ended before the second arrived: step lengthened).

### The strips, read (00:27 UTC; `build/wt/n3-ui/build/motion/msg-*.png`)

- msg-banner: frames 2-5 the banner fades in and slides 12 down from above, eased; 6-8 at rest in the Message zone; 9-11 it leaves eased after a click; nothing else moves.
- msg-notes: the first note fades and slides in from the left inside the note section, is clicked away and fades out; the strip ended inside the gap before the second (the step after the click is now 0.09 s, not yet retaken).
- msg-line: "Your villager fetches water..." (first-minute hint 1) cross-fades to "30 short." (frames 2-4, both texts half-seen in the same fixed card), then to "The bell rests 31 s." (8-10). The card keeps its size.
- msg-request: the goal content fades in with "41 Water within 120 s" (2-5); after the bot's run it says "Done: +33" for its stay while level 4's choice arrives in the Message zone (6-8) and level 4's gift is a note ("Level 4: A cottage, a lane and a second bucket for the well.") - the busiest moment as the game makes it: one large message, one note, the goal, one line. The goal then fades out (11-12).
- msg-choice: FAULT of the strip, not the game: the choice was already open (the request strip's bot run reached level 4), so frames 1-8 show it at rest; 9-10 show it leaving eased after Z, with "A boon for good: Wider lanes. ..." arriving as a note (cut at 90 characters with an ellipsis: two lines). Reordered (the choice strip now runs before the request strip), not yet retaken.
- msg-coin: coin numbers are hard to see at 640 x 360 (a small "+8" over the well in frames 8-12); the choice stayed open over this strip (same cause). The stuck line text "The bell rests 31 s." in the later strips came from the held clock (a refusal's 2.5 s never ran out); `ClearMessagesNow` now clears the refusal too.

### 00:45 UTC

- game-layout run 1 (after the queue): 14 failures, all mine, fixed: (a) rule 1-zone, the banner card stood up to 12 above its zone while arriving: the card now stays in its zone and fades, its words come 8 down from above inside the card; the choice card fades in place; (b) rule 3-text, the fixture's longest note took three lines: notes are now cut at a word with an ellipsis to fit two lines (`FitLines`), and the fixture writes the note as the section sets it (`hud.NoteFit`, changed test, listed below). Run 2 showed the title sliding above the zone (12 units): now 8. Rule 5 (the budget) did not fail: most loud 2, quiet 4.
- A level banner still waiting is dropped when a newer level's banner comes (the tour at one frame a second showed "Level 6" at level 9: the queue was behind).
- Tour pictures in `build/n3/ui-audit/l2-wip/` (read): u04 level-2 banner in the Message zone, the note "Achievement: First coin.", the first-minute hint in the line, nothing over the town; u05 a banner one level behind (the 1 fps tour; now dropped when stale) with the goal, a note and the scroll hint; u06 the choice with "Level 4: choose one boon..." in its heading, no banner, an achievement note, the scroll hint; u11 a level-6 banner at level 9 (stale, see above), goal, note, the town's hint, coin numbers, the "A field, to buy" tag. u35: not taken yet when the check started.
- `./check` started 00:43.

| Test | Old | New | Why |
| --- | --- | --- | --- |
| LayoutFixture note | the longest note written raw into the label (it had 3 lines) | written through `TownHud.NoteFit` (cut at a word to two lines) and checked to fit | message-system 1: a note is two lines at most |
- u35 (read, taken with the build of 00:26, before the stale-banner drop): "Level 17" banner at level 24 (the same staleness, now dropped), an achievement note, the tide dial, the line with the town's hint and its thin ring around the mill, screen tags "Wool 20 of 35", "Full, leaving", "The footbridge, at level 25"; no 3D words.

### ./check of 00:43 (load average 15 to 28; other checkouts' checks and picture runs at the same time)

sim-build, sim-test, game-build, game-smoke, game-town, game-perf (PERF PASS, town 1x 504 B/frame, 8x 735; river 1x 1211, 8x 1565), game-playthrough (charter 33.9 min, 201 purchases, 1 click refused on a moving row), game-river, game-layout (LAYOUT PASS states=33 canvases=8 checks=66238 failures=0 loud<=2 quiet<=4 made=36 notmade=3), game-book, game-menu, game-save: pass.
game-settings: FAIL "sound settings not kept" in the check; alone at 01:02 "went back after 11.0 s, not 10" (a timing check of the settings pages, under the screenshot step's load); alone at 01:03 SETTINGS PASS. (The settings scene has no interface: the hints are not written there. `SetHintSeen` now reads the file before it writes, so a second store of the file is never overwritten.)
game-motion: FAIL on the CPU gate only, in the book's transitions before the message part runs: "pause: frame 42 took 8.62 ms" (60), "pause: frame 20 took 6.31 ms" (30), "tab-sound-display: frame 21 took 6.32 ms" (120). The message part passed alone at 60 fps at 00:09 (MOTION PASS); at 120 fps in the motion-check of 00:09 it passed too (MOTION PASS ... fps=120). The rates comparison of the msg-* lines was never reached with all three rates passing: to rerun on a quiet machine.
screenshot: still running at 01:05.

### Left (in the order of the brief)

- 3: a third request waiting (not shown at all); the skipper's request takes the goal section only when no town request runs (as before).
- 6: the lit-band mesh of Pave/Extend (3D) still flips with the mode; the tide dial and the slim purse flip `Visible` only at no opacity (behind the page-turn leaf / at full fade) and were left.
- 9: the new tooltips are not pictured.
- 10: the tour states never pictured (a "not yet" row's tooltip, the bell's tooltip, a row pressed, the centre button disabled, a group with everything finished): not made. Coin numbers, tags and tips have no motion-test channel of their own (they are eased in code; the strips show coin numbers only small).
- 11: the half row at 1280 x 720 with 150 percent: not pictured again.
- Strips: retake msg-notes and msg-choice after the reorder (`tools/motion-strips.sh --messages-only`, about 8 minutes); shop rows arriving and leaving: no strip.

## After the review (the L2 fixer, from 01:41 UTC)

Reviews read: `l2-review-1.md`, `l2-review-2.md`, `lead-notes.md` (no note for the L2 fixer). Checkout `build/wt/n3-ui`.

### Changes

| Finding | What changed | Where |
| --- | --- | --- |
| R2 must 2 (root cause of R1 M1, R1 S1, S2, S6) | The messages' clocks (stays, gaps, waits, the refusal, the drive's "Done", the first-minute hints) count real time, at most 0.5 s a frame (`MessageQueue.Clock`); they were counted in `BookMotion.MaxStep` (1/20 s) steps, so at 1 frame a second a 3.2 s banner stayed about a minute. Easing still goes through `BookMotion` (unchanged). At 30, 60 and 120 fps nothing changes. | `MessageQueue.cs`, `TownHud.Line.cs`, `TownHud.Choice.cs`, `TownHud.FirstMinute.cs` |
| R1 M1, R2 should 2, 3 | A hint's age counts only while the line shows it (a refusal or a mode in front does not use it up); no hint starts while the choice, the achievements page or an age page is open; hint 3 is tied to the first level banner SHOWING and given up 60 s after it; hint 2 pulses the cheapest affordable row ("the row pulses") | `TownHud.FirstMinute.cs`, `TownHud.Messages.cs` |
| R1 M2, R2 must 1 | A large message is checked against the town when it is about to show and while it shows (`MessageQueue.Stale`): a level banner whose level is below the town's, a drive banner once the drive is no longer running, "A new age" once the town is a level further, are dropped | `MessageQueue.cs`, `TownHud.Messages.cs`, `TownHud.Book.cs` |
| R1 M4 | A new coin number is placed beside a live one it would lie on (offset chosen once, so nothing jumps) | `TownHud.cs` (`FloatClearOf`) |
| R2 should 5 | A coin number under a showing banner or choice card fades with it | `TownHud.cs` (`StepFloaters`) |
| R2 should 6 | "Waiting for a berth" is never brick (brick is for missed and trouble) | `TownHud.Request.cs`, `TownHud.River.cs` |
| R2 should 8 | The hint ring is half as thick (2.5 percent of its radius) and 60 percent opaque | `TownHud.Request.cs` |
| R1 S4 | "A new age" says what changes: "The town turns to the river: a landing, boats and crossings." | `TownHud.Book.cs` |
| R1 M6, R2 must 5 | The tour has four tooltip states: u42 a shop row not yet affordable, u43 the bell, u44 the achievements button, u45 a boon of the choice | `game/tests/UiAudit.Tips.cs`, `UiAudit.Age1.cs` |
| (working aid) | `motion.tscn -- --messages-only` runs only the message part of game-motion (./check still runs everything) | `game/tests/MotionTest.cs` |
| (tour) | The tooltip states move the window's real pointer (`Input.WarpMouse`): the play tooltip reads the pointer's place from the window, not from the events the tour sends | `game/tests/UiAudit.Tips.cs` |

### Disagreed or left (and why)

- R1 M3 (a large message covers a roof; the line lies over a stake frame): the Message zone and the line are fixed zones of the sheet; moving the camera when a message comes would change the follow view (how the town is seen while playing). The banner leaves after 3.2 s. Left; for the lead if wanted: the follow view could frame the town inside `HudZones.Middle`.
- R2 must 3 (hint 4's words): `message-system.md` section 4 gives the words "Scroll out to see the whole page, and the book." The sheet is the specification: left. For the lead: the reviewer's "Scroll the wheel down to zoom out and see the whole town." is plainer.
- R2 should 1 (page 5 to 6 px from the rails at 1280): the sheet's gap is 8 sheet units; at 1280 x 720 the scale is about 0.78, so 8 units are 6 px. As the sheet says.
- R2 should 4 (slashed zero): Atkinson Hyperlegible has no plain zero (its only alternate, `zero.tf`, is slashed too; checked with fontTools). A font change is under `assets/` (not mine).
- R2 should 7 (coin sign in "Reward: 385 and renown", "Pays 29 a unit"): needs the coin icon inline in a text line; not done.
- R1 S3 (achievement icon in the note): not done. R2 should 9 (a coin number near a tag), R2 should 10 beyond the pulse: not done.
- R1 M5 (strips retaken): the message strips were started twice (01:50 and before) and produced no frame in 16 minutes under a load of 40 to 50 with two tours beside them; stopped at 02:05 to leave time for ./check. Not retaken.
- Hint showing when the choice opens (R2 should 2): fixed at 02:10 after b-u06 showed hint 3 under the choice: the first-minute hint is held out of the line while the choice or a page is open (its time does not run) and comes back after (`TownHud.Line.cs`). Retake: `d-1280x720-100/u06`.

### Pictures after the fixes (read)

`build/n3/ui-audit/l2-fix/`: `1280x720-100/` (u01 to u13, the build with the 0.25 s cap), `b-1280x720-100/` (u01, u02, u03, u04, u06, u11, u42, u45; final clocks), `c-1920x1080-100/` (u01, u42, u44, u02; with the pointer warp).

- 1280 b-u02: the line says hint 2 "You can afford your first upgrade..." with three pills gold (it was hint 1 in every early picture before).
- 1280 b-u04: the Level 2 banner at level 2; hint 2 still in the line (the tour bought within a few frames at 1 fps: the hint stays its least 4 s on screen).
- 1280 b-u03: level 2, hint 3 "Each level gives the town something new..." right after the first banner (before: first seen in the second age).
- 1280 u13 (stale fix in): level 15, the banner is the charter drive's, which is running (Charter 0 of 10 in the strip); no stale "Level 12". The hint ring is a thin faint circle around the field. Coin numbers "+36" and "+99" apart.
- 1280 b-u11: level 13, the choice open, the request in brick (22 s left), no coin number under the choice.
- 1280 b-u06: the choice open; hint 3 in the line (see above, not fixed).
- 1920 c-u42: the tooltip of the Villager row beside the shop, three lines; FAULT: its text is cut "pays for itself in 50 s...." (ellipsis after a full stop) and says "+5.4 coin a minute" (the word coin).
- 1920 c-u44: the achievements button's tooltip "Achievements [Tab]: 0 of 24." above the navigation, inside the window. Fine.
- 1280 d-u06 (after the hint hold): the choice open, the line empty (no hint under the decision); the milestone note in the ledger. Fixed.
- 1280 d-u45: FAULT: the tooltip of boon A ("Wider lanes [Z]: Every road carries 50 % more people... while you...") lies over boon B's half of the card and its text is cut with an ellipsis. The tooltip should go below the choice card (it avoids the card only when its owner is outside it: `HudZones.TipRect`) and the text should fit (three lines). Not fixed (time).
- u43 (the bell) with the pointer warp: not taken (time).

### ./check of 02:10 (after all fixes; load 37 to 50, other checkouts' runs beside it)

sim-build, sim-test, godot-nuget, sync-assets, game-build, game-smoke: pass. game-town: TOWN PASS (hints=16 markers=12). game-perf: PERF PASS. game-playthrough: PLAY PASS charter=33.9min buys=201 paves=24 extends=14 bells=76 clicks=204 keys=102 (as before the fixes). game-river: RIVER PASS. game-layout: LAYOUT PASS states=36 canvases=8 checks=71910 failures=0 loud<=2 quiet<=3 made=40 notmade=4. game-book, game-settings, game-menu, game-save: PASS.
game-motion: FAIL on the CPU gate only, at 60 fps in a book transition: "tab-redirected: frame 17 took 8.57 ms of CPU (gate 6)". At 30 fps MOTION PASS (cpu-max 1.51 ms) and at 120 fps MOTION PASS (cpu-max 1.25 ms), both with every message line (banner, notes, note-clicked, line-cross, choice, busiest moment: at most 1 large, 1 note, 1 line over 2706 frames). Not rerun alone: the screenshot step was still running at 02:40 (the final-answer deadline).
screenshot: still running at 02:40.

### Left

- Rerun game-motion alone on a quiet machine (`tools/motion-check.sh`); see the screenshot step's result in `build/check-logs/screenshot.log`.
- The message strips (`tools/motion-strips.sh --messages-only`): not retaken.
- The boon tooltip over the other boon (d-u45) and the cut tooltip texts (c-u42): `HudZones.TipRect` / `TownHud.Tip.cs`.
- u43 (the bell's tooltip) not pictured; the second-age states u30 to u35 not pictured.
- The coin sign in the goal's reward lines; the achievement's icon in its note; a third request waiting (builder's list); the lit band of Pave/Extend.

## Rate fix and medals (two-fix builder, 04:00 UTC)

- `MessageQueue.Step` keeps the queue's own timeline: a phase (arrival, stay, leave, gap) that ends inside a frame hands the rest of that frame to the next phase, so the next message is due from when the one before was due to end. Before, each of the four ends lost up to a frame (a row of six notes: 20.400 s at 30 fps, 20.117 s at 120; now 20.133 / 20.100, queue alone).
- `MotionTest.Messages.cs`: "at rest" now means both queues in `Phase.Idle` (past the gap); before, the next case began inside the last one's gap by a rate-dependent amount (note-clicked's dismiss then fell 0.35 s into the hold at 120 fps, 0.47 s at 30). `--cpu-info` now also applies to the message part.
- msg-* lengths at 30 / 60 / 120 fps after the fix: banner 3.667 / 3.650 / 3.642, notes 11.133 / 11.117 / 11.100, crowd 19.967 / 19.967 / 19.950, note-clicked 4.600 / 4.617 / 4.608, line 3.700 / 3.700 / 3.692, busiest 22.533 / 22.517 / 22.500.
- An achievement's note shows its medal (`TownHud.NoteMedal`, `_noteIcon`) at the left of the text; the achievements page draws each medal at the left of its cell (`TownHud.AchIcon`), dimmed until done.

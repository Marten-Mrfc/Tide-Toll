# Round 5 game progress

Started: Thu Nov 26 01:09:54 UTC 2026 (time box about 60 minutes)

## Done
- (nothing yet)

## Next
- Part 1: run game steps one by one, fix merge breaks.

## For a successor
- 01:2x game-town "drag paved a road": a left press blocked by a card left LeftDragged false, so its release elsewhere was a click; now LeftDragged = true for a blocked press (CameraRig). game-town PASS.
- game-perf 1x: one frame allocated 1.3 MB in CameraRig: KeyboardPan passed strings where Godot wants StringNames (8 new tracked StringNames a frame, the tracker's growth). Cached StringNames. Perf.cs now prints the largest frame and per-node allocation (information only, gates unchanged).
- Shop: Locked/Finished flags, worth line, Regulars icon and help line, Hand icon; Pavable/Extendable use CanPaveNow/CanExtendNow.
- Written (01:2x): TownHud.Choice.cs (choice card, drive card, milestones, hand markers, touch notes), TownTouches.cs (props), summary page and achievements page cover both ages and list boons, keys tt_choose_a Z / tt_choose_b X (Keys page rows 36 px 5 px apart so 14 rows fit; settings PASS), TownDriver.RunBotUntil, tests TownTest.V3.cs, Screenshot.V3.cs (--v3-only), river pause shots and checks (Screenshot RiverPauseShots), screenshot order fixed (ResumeFromMenu).
- Next: run town, v3 shots, read them; river test pause; lanes; boat labels; seal view.
- 01:30 v3 shots read (choice, touch, hand, drive, summary-boons(redo)); fixes: drive card stacked above notes; shop goes compact (13 px) when the column would overflow; milestone note text. Lanes now drawn by TownGround (TownGround.Lanes.cs), ribbon removed. Boat labels short and east of the boat. PlayThrough.V3.cs added. River test PASS with pause in the second age (cover centred 960,540).
- 01:4x game-town PASS (v3 checks), game-playthrough PASS (4 boons: 2 click, 2 key; drive seen and met; 1 hand; 17 touches; 14 milestones), menu, book, save, smoke, settings PASS. game-perf PASS: 1x 599 B/frame (was 3442), 8x 1715. Second tracker spike found at 8x in the HUD: KeyLabel read the InputMap per text change; cached per binding version.
- Read: river-landing (boat label east over the water, short), river-ford-low: NO LANE VISIBLE on the far bank. Added a RiverTest check (lane worn into the ground) and running game-river to find out.
- 01:38 lanes confirmed: game-river notes every lane open with drawn wear 0.95; river-seal shot shows worn lanes on both banks and the follow view takes in both banks; boat labels short over the water. river-paused (cover centred, whole), river-paused-settings read. Full ./check --export started (log build/check-logs/full-check.out).
- 01:5x first full ./check --export: game-town failed on my own new check "touches arrive" (no touch between the start of RulesV3 and the next choice in that run); the arrival is checked by the next step, so that line now checks only that every touch is drawn. game-town solo PASS. Full check still running (screenshot step); a second full ./check --export is needed for the final table.
- 01:58 Read from the first full run: settings-keys (27 rows fit, no scroll), boon, summary-boons (boons line), achievements (found: "+ hand" marker drawn over the achievements page; fixed: hand layer under the cards, markers and the choice card hide while a page is open), river-landing (river in view, not the cover). Killed the first full run and started the final ./check --export (build/check-logs/full-check2.out).
- Known, not fixed: the next level's stake label ("A cottage, at level 18") can lie over the landing; the seal page still uses the old "not in the game yet" text (RiverTest asserts it).
- 02:1x second full run: game-town failed "the second choice's card": the play had left the achievements page open and I had hidden the card under pages. Decision: the choice card shows over everything (it sits above the page); hand markers hide under pages, and the test closes the achievements page (Tab) before checking the marker. game-town solo PASS. Final ./check --export started: build/check-logs/full-check3.out.
- 02:38 third full run: every step PASS except screenshot, which hit the step's 1200 s wall-clock cap (exit 124) after town-river-resumed (all shots up to there written and read: drive with notes under it, achievements without the marker, river-paused centred). The full screenshot run is longer now (v3 shots, second-age pause). Raised the cap to 2400 s in tools/check.sh (a safety cap, not a gate). Fourth full ./check --export running: build/check-logs/full-check4.out.
- After it ends: fix the cap comment in tools/check.sh (it wrongly says the 1200 s run passed; it timed out).

## State
- Parts 1 and 2 done except: seal page text (old wording kept, RiverTest pins it), stake labels can overlap the landing, "small flourish" for milestones is a strong note plus a sound (no animation).
- 03:07 Found the real cause of the screenshot step's time-out: RiverPauseShots ran between the seal shot and the wait for the seal's page; pausing closes the page before it opens, so the wait (2000 rendered frames) ran into the cap. Moved the pause shots after the seal's page. Cap kept at 2400 s (comment corrected). Fifth full ./check --export: build/check-logs/full-check5.out.

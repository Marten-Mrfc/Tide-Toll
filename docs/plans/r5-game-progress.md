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

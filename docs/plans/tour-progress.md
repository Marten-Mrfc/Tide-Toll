# Tour progress

Started 2026-11-26 01:12 UTC (date -u). Time box ~60 min.

- [ ] read plan + tests

- 01:17 code written (Tour.cs, Tour.Age1.cs, Tour.Roads.cs, Tour.Book.cs, Tour.River.cs, TourNotes.cs, tour.tscn, tools/tour.sh); builds. First whole run started.
- 01:30 run 1: 739 s, failed at Resume after h05 (clicked Back at 150 % interface: control rect not in window pixels). Fixed (Esc), b05 (second extension needs a later level: Fast), d05 (pave by click before the bot paves), Calm (banner up to 30 s), e02 wider view. Run 2 started.
- 01:47 run 2: first age and menus all fine (b05, d05 now right); failed: the second age page did not open within 30 s after N (run came straight after h05 put the interface back to 100 %). Moved book and settings shots after the seal (plan order). Run 3 started.
- 01:59 run 3 killed at f02 by editing tools/tour.sh while bash ran it (now one block). b04/b05 missing in run 3: real-time waits make the town drift between runs; Extend now falls back to the bot. f02: N opens the page if it does not open itself. Added --notes-only. Run 4 (final, clean) started.
- 02:14 run 4: Godot segfaulted (dmesg) after g05, 843 s; run 3 had also segfaulted (at f02), not the tour.sh edit. tour.sh now retakes the missing shots once after a crash. Ferry condition relaxed (f06 missing in run 4). Run 5 (final, clean) started.
- 02:15 run 5 stopped by me at the start: run 4 showed f04 (ford) taken after the seal (its condition first held then). River conditions now stop at the seal (Early). Run 6 (final, clean) started.
- 02:31 run 6 PASSED clean: 926 s, 45/50 (c04, c06, e01 not in the game; f06, f07 missed: the ford waited until level 27). Kept in scratchpad. Ford waits shortened, ferry and drawbridge relaxed; run 7 started.
- 02:31 run 7 stopped: run 6 showed f08/f09 under the seal page (the seal came during an earlier wait). Esc closes an open age page before each river shot; f10 opens the page again with N if needed. Run 8 started.
- 02:47 run 8 (clean, whole): PASS, 933 s, 46/50; b05 not made (second extension click did not land). Extend retry loop added; retaking b04,b05 with --only.
- 03:08 retake b04+b05 (--only, 382 s) PASS: 47/50. --notes-only now headless (xvfb hang next to another agent run). Notes reviewed for all made shots; handover written. Done. Ended about 03:08.

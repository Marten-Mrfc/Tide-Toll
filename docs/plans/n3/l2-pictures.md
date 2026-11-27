# L2 pictures (partial, deadline 01:42 UTC)

Folders (build/n3/ui-audit/), made with the n3-ui checkout at the build of the message round:
- l2-1920x1080-100: 23 pictures at the time of reports (u01..u19, u22..u25 coming); the tour was still running (about 1 state in 90 s on a load of 20+), NOT finished. Remaining states u26..u41 (second age, tooltips) not made.
- l2-1280x720-100: 5 pictures (u03, u06, u11, u13, u15) from a shortened run in parallel (--only list: u03,u06,u11,u13,u15,u33,u35); u33 and u35 not made yet.
- Both runs were still going at the deadline and may add pictures after this; re-run ui-audit-report.py on the folders for the final count.

Not made: motion strips (tools/motion-strips.sh not run: no time). The new tooltip states and the 1280 states other than the 5 above. Run 2 was a shortened parallel run (display 98), not the full tour.
Strip sheets that exist (OLD, 00:25, from build/wt/n3-ui/build/motion/): msg-banner, msg-choice, msg-coin, msg-line, msg-notes, msg-request. Read: banner, notes, request (the 00:25 build, before the stale-banner drop and the reorder).

Pictures read: 1920: u06 (choice card in Message zone, heading "Level 4: choose one boon", scroll hint, fine), u11 (Level 12 banner at level 12, request card, "The mill, at level 13" tag, fine), u15 (achievements page, fine; shop shows "2 to buy now"); 1280: u13 (fault: banner "Level 12" at level 15, stale 3 levels), u15 (achievements page fine). Sheets: msg-banner (banner fades in and out in the Message zone, fine), msg-notes (note appears bottom of left card), msg-request (request card, then choice cards, fine).

Worst lines of report.md:
- 1920: largest overlaps u22 and u24 Label#0 x MapTitle (9714 px2), u19 AchValues0 x AchNames1 (9692), AchNames0 x AchNames1 (8666) (u19 is the closed cover: book print; likely 3D print labels). Middle third not free in u01, u02, u05, u07, u08 (hover tip, Suggest tag), u09, u10. Smallest text 15 px.
- 1280: u15 Label#2 x AchievementsPage (1029 px2), u11 Label#3 x Choice (1008), Label#3 x Worth (979); smallest text 12 px; covered share 27 to 36 percent (u15 100, page).
- Fault seen: stale level banner at 1280 u13 (banner 3 levels behind) although the queue drops stale ones in the 1920 run.

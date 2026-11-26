# L1 picture tour (checkout build/wt/n3-ui), finished 22:40 UTC

All runs passed (UIAUDIT PASS, no failed check, no crash, no retake needed). Reports: `<folder>/report.md`.

| Folder (under /home/user/tide-toll/build/n3/ui-audit/) | Pictures | Run |
| --- | --- | --- |
| l1-1920x1080-100 | 35 | all states, 846 s |
| l1-1280x720-100 | 35 | all states, 623 s |
| l1-1920x1080-80, l1-1920x1080-150, l1-1280x720-80, l1-1280x720-150 | 6 each (u03 u06 u11 u13 u33 u35) | run 3; the four ran at the same time (own xvfb displays) |

## Not made
- u32 (ferry + request): "the seal came first". u34 (drawbridge + toll): "no drawbridge before the seal". Both sizes. u37 is retired (no seal drive exists).
- So 35 of 37 asked states per full run (log says made=37: it counts u32/u34 slots oddly; there are 35 pngs).

## What the pictures show (READ: run 1 u01 u06 u11 u15 u23 u35; run 2 u03 u06 u13 u22)
Right state: u01, u06 (boon choice open), u11 (level 11 banner + request running), u15 (achievements page), u03, u13 (1280), u06 (1280).
Wrong or doubtful:
- u23 (1920, Keys) and u22 (1280, Display) do NOT show the settings page. u23 shows the book at page 1/2 (stats + map); u22 shows the same spread mid page-turn with settings buttons peeking at the right edge. The settings wait/turn staging is not reliable in this tour; u21..u25 pictures are suspect at both sizes.
- u35 ("busiest moment, second age") is taken at level 28 with the borough seal banner, not mid-age. It is the seal moment.
- u15 (1920): the achievements page is see-through (map and houses show through its text, the heading is unreadable), against the "pages opaque" note in layout-progress.

## Worst lines of report.md
1920x1080-100: overlaps are the scrim over pages/shop (u15 555840 px2, u39, u31, u37, u38) and book/settings states (u15 256 pairs, u25 166, u31 164, u39 204, u37 132, u38 128; u21/u23 23/51). Cut-offs: Ledger "Next:" line trimmed (u05, u10); "The charter drive" name 70x40 box, text 122 (u13-u15); "A fourth hand at the mill" (u16, u30, u31, u33) and "Better boat prices" (u30, u31, u33, u36) trimmed in shop rows; book compass N and folio outside the window (u17, u40). Moves: age 1: 92 things change rectangle in 17 groups (shop groups and rows shift down as groups appear: Roads, Town, Work, Row1000 and pills move between u01-u16); age 2: 90 things in 15 groups. Middle third not free in nearly every state (map labels, hover tip, "What" plot labels); free only in u04, u05, u06.
1280x720-100: same pattern. Overlaps: u15 266 pairs, u39 207, u25 176, u31 139, u37 126, u38 124, u11 10, u05 2. Cut-offs: Ledger "Next:" (u05, u10, box 232 vs text 292), "The charter drive" (u13-u15), book N and folio (u17, u40), u36: book plot tag "The drawbridge, at level 27" outside the window. Moves: age 1: 109 things in 22 groups; age 2: 92 in 13.
Run 3 (scale): covered share of the window grows with scale: 80 percent 11-19 percent (1920) / 15-25 (1280); 150 percent 32-47 (1920) / 38-59 (1280).
- 1920 80: one cut-off (u13 charter drive 56x32 vs 97; u33 "Better boat prices"); overlaps only u06 (3 pairs).
- 1920 150: u13 charter drive 105x60 vs 183; u33 "Next: The ford and a lane..." 372 vs 403 and "Better boat prices"; overlaps u06 5, u11 1.
- 1280 80: u13 charter drive; u33 "A fourth hand at the mill"; overlaps u06 3, u11 3.
- 1280 150 (worst): overlaps u06 42 pairs, u11 17, u33 29, u35 38; u33 shop row Better boat prices (row 15000) and its price pill lie outside the window (y 680 of 720; the shop runs off the bottom); "Extend a cottage" trimmed (u06, u33); "Next:" trimmed (u13, u33); charter drive trimmed.

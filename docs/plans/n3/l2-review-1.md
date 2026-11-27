# L2 review 1: messages, from the motion strips and the tour states

Reviewer 1, written 01:4x UTC. No code changed. Judged only from pictures.

## What I could and could not see (read this first)

- Strips: only the six OLD sheets of 00:25 exist in `build/wt/n3-ui/build/motion/` (msg-banner, msg-choice, msg-coin, msg-line, msg-notes, msg-request). None was retaken after the reorder or the stale-banner drop. I read all six. msg-coin is unreadable (coins are 3 px at 640 x 360). msg-choice and msg-coin show the choice already open in frames 1 to 8 (builder's own note: strip fault), so the choice's ARRIVAL is not pictured at all.
- Tour states 1920 (`l2-1920x1080-100`): u01 to u06, u09 to u11, u13, u14, u16 read. **u30 to u35 do not exist** (folder ends at u25). The new tooltip and pressed states do not exist in either folder. 1280: u13 and u33 read (u33 is the only second-age message picture).
- So the second-age moments, every tooltip, the page-open-while-a-message-comes moment (inventory moment 6) and the Pave-with-banner moment (moment 5) are NOT pictured. Nobody can say they work.
- The tour runs at about 1 frame a second, so lengths of stay in single pictures are unreliable. Where a fault below depends on that I say so.

## 1. MUST FIX, worst first

**M1. The first-minute hints do not teach; hint 1 never leaves and hints 2 and 3 never show.**
Evidence: u01 (0 coin), u02 (7 coin, "Achievement: First coin." in the note, shop says "3 to buy now"), u03, u04, u09 (level 2, 1 to 8 coin, three rows affordable and gold): the line still reads "Your villager fetches water and carries it home. Every trip earns coin." The sheet says hint 1 ends at the first coin (12 s at most), then hint 2 "You can afford your first upgrade. Click it in the shop, or press its key." must show while the row pulses. Hint 2 appears in NO picture, though u02 and u09 have three affordable rows. Hint 3 (after the first level banner) does not show in u03, u04, u09 (level 2 banner up, line still hint 1); it shows first in u33 1280, second age, level 18, under "A new age" (wrong place: it is about levels, it says "once ever after the first level banner"). msg-line strip: hint 1 for frames 1 to 3 then a refusal "30 short." takes over, then "The bell rests 31 s." and the hints are gone, never returning (right, once ever, but they were cut by refusals before teaching anything).
Change: end hint 1 on the first coin (or 12 s), start hint 2 the moment a row is affordable and keep it until the first buy; start hint 3 when the first level banner has left. Test with the tour's own clock: u02 must show hint 2, u03 to u04 hint 3. Make a refusal not consume a hint: a hint shown less than 4 s is shown again.

**M2. A stale banner stays up: wrong level, wrong moment.**
Evidence: 1920 u13 and 1280 u13 and u14 (level 15, banner "Level 12, A lane that joins the south cottages", still at half fade in u14); 1920 u05 (level 3, banner "Level 2"); 1920 u09 and u03/u04 are right (level 2). The builder said the queue drops stale ones; at 1280 and at 1920 u13 it still does not. Worse, **u16 (level 16, the charter already given: purse says "Level 16, the last", the strip holds the button "Ring in the next age [N]") shows the banner "The charter drive: One last push: send 353 Flour for the charter."** That drive is over; the banner lies about the state.
Change: when a banner is about to be shown (not when queued) compare it to the town now: a level banner whose level is below the current level, a drive banner after the charter is granted, are dropped (or become a note if the player never saw anything of that level). Test: scripted run that rings 3 levels in 2 s must show one banner, the newest.

**M3. A large message covers the thing it is about / things on the map.**
Evidence: u06: the choice card (y 16 to 215) sits on a building's roof at x 915, y 215 (a plot or house is half under it); u13: the Level 12 banner covers the roof of the house at the top (x 1035, y 90 to 110); u14: the same house. The sheet allows the choice in the middle, but covering a building that just changed is the exact thing the owner objected to. Also u03, u04, u09: the line (y 1024 to 1064) lies across the bottom edge of an unnamed plot's stake frame (x 520 to 890): its tag is missing and the frame runs under the line.
Change: the camera's framing of the plot/house must keep the live subject clear of zones; at least do not frame plots under the Message zone or the Line. Cheaper: when a new plot appears under a zone, nudge the camera on arrival (the town already glides) by the overlap.

**M4. Coin numbers pile into unreadable overlaps.**
Evidence: u11: "+1,632" (faded) and "+14" sit on each other at (850, 215); u14: "+199" and "+99" overlap at (1280, 535) ("+199" over "+99"). The spec says same-building deliveries within 0.5 s merge into one; these are two different numbers within about 20 px, not merged and not pushed apart.
Change: when a new number would lie within 24 px of a live one, offset it by 20 px sideways, or add it into the older one if that one is under 0.6 s old; check also that the number that rises 24 px in 1.4 s does not cross another one.

**M5. The note and the strip have no motion picture; their eased arrival is unproven.**
Evidence: msg-notes (00:25 build): frame 2 shows a faint text under the sections, frames 3 to 6 at rest at the bottom of the left card, 7 fading, 8 gone. The slide from the left is not visible at this size; the second note never arrives (builder says the strip ended in the gap). msg-request: frame 12 shows the goal fading out while the Done line "Level 4: A cottage..." (a note) stays in the note section: OK. But `Done: +33` is shown in frames 6 to 11 at the same time as the choice card AND a note AND the line "The bell rests 31 s." That is 1 large + 1 note + goal + line: allowed, but it is 4 things at once in the first 4 levels.
Change: retake msg-notes with three notes in a row (order, gap, crowd stay) and msg-choice with the arrival in frame 1 (choice closed). A larger strip (crop the ledger at 2x) is needed for the note's slide; at 640 wide it cannot be judged. Until then these are untested by eye.

**M6. Second-age message pictures are missing (u30 to u35) and the tooltips are not pictured.**
This is not a bug found but a gap that blocks sign-off: tooltips (the owner's rule: beside the owner, 0.25 s), pressed states and the "not yet" row's tooltip have never been seen by anyone. Do not call the message round done before u30 to u35, one tooltip on a shop row, the bell, a navigation button, an achievement, and one pressed row are read. 1280 u33 is the only second-age picture and it is read below.

## 2. SHOULD FIX

- S1. Notes linger: "Achievement: First coin." is in the ledger in u02, u03, u04, u09 (four pictures from one achievement; 3.2 s at 1 fps would be 3 pictures; check at real time). In u13 1920 "Well trodden" is still there as the banner fades. If this is the tour clock only, say so; if real, the note stay is longer than 3.2 s.
- S2. The first banner and the first hint arrive together (u03: "Level 2" banner top, hint 1 bottom, goal empty, note "First coin"): four messages in the first 30 s. The first minute should have two.
- S3. The note's first line "Achievement: First coin." is wording from the code; a plain "First coin." with the icon is shorter. The sheet says the achievement has its icon: none shows in any picture (u02, u04, u13): plain text only.
- S4. The "A new age" banner (u33) and the line "Each level gives the town something new..." arrive together at 1280 and the banner's subtitle "The bell rings in the next age." is a repeat of the title. One caption line that says what changes (the river, the ford) is better.
- S5. The ledger note section is blank for most pictures (u01, u03, u10, u11, u14) with the strip too: fine as paper, but in u16 the Ring button sits on the line where the note should start; check that a note arriving then does not look attached to the button.
- S6. Hint 4 "Scroll out to see the whole page, and the book." shows at level 3 and 4 (u05, u06, u10). Sheet: after 75 s of play and never after the player has zoomed. At 1 fps tour this may be the tour clock; but u10 (level 8, 5,700 renown, about 20 minutes of play) still shows it: it must have left after 8 s. Real fault unless the tour holds the clock.
- S7. The "Done" or "Too late" moss and brick states cannot be told apart from plain text at a glance in u10: the request line "373 Water within 21 s" is brick-coloured (quarter time left) and the goal card has no other change. Fine, but add the shrinking bar colour to match.
- S8. The pause of the town: the line `Your villager fetches water...` is wider than 560 (680 to 1240) and is long for its card; fine but the line card and the banner share the same width and the same paper colour family; add the quiet variant (no border) as in the sheet: it already is quiet in the pictures, good.

## 3. Per kind

| Kind | In its place? | One at a time? | Readable? | Eased? |
| --- | --- | --- | --- | --- |
| Large (banner, choice) | Yes, top centre, 560 wide, 96 high (u03, u10, u16) | Yes in every picture (never two) | Yes; choice heading "Level 4: choose one boon" clear; but stale (M2) and covers map (M3) | Banner: yes, 4 frames fade, 12 down then out, 4 frames (msg-banner 3 to 5, 9 to 11). Choice: arrival NOT pictured; leave: frames 9 to 11 of msg-choice fade (yes) |
| Note | Yes, bottom section of the ledger | Yes | Yes, but plain text, no icon (S3) | Leave seen fading (msg-notes 7, u14); arrival slide not visible at strip size (M5) |
| Goal | Yes, ledger goal section | Yes | Yes; brick when late | Fade seen in msg-request 2 to 5 and 11 to 12 |
| Strip | Yes (u14 Charter pips, u16 Ring button) | One | Yes; drive banner contradicts it in u16 (M2) | Not pictured |
| Line | Yes, bottom centre | Yes | Yes; but the hints are wrong in time (M1) | Cross-fade seen: msg-line 3 to 4 (two texts half-visible in one fixed card): good |
| Coin number | Yes, at the buildings | Not merged: overlaps (M4) | Small but fine at 1920 | Fade in/out not seen at the strip's size |
| Tag | Yes (plot names, "Flour 10 of 33", "+ hand") | Several at once, no touches seen except plot tag missing under the line (M3) | Yes | Not pictured |
| Tip (pointer price) | u07, u08 exist but not in my list | not read | not read | not read |
| Tooltip | not pictured at all (M6) | - | - | - |
| Page | Not in my list (u15, read by the builder: fine) | - | - | - |

## 4. The first minute

Quoted from the pictures and the sheet:
1. "Your villager fetches water and carries it home. Every trip earns coin." Plain, true, good words. It stays too long: still showing at 7 coin, "First coin" achievement, three rows gold (u02) and at level 2 (u09). It should end at the first coin.
2. "You can afford your first upgrade. Click it in the shop, or press its key." Never seen in any picture. The shop pulse is also not visible: the gold pills in u02 are solid wheat, with no pulse in a still. This is the hint that teaches the game; it is the one missing.
3. "Each level gives the town something new. The purse shows what comes next." First seen in u33, second age, level 18. Wrong time (M1).
4. "Scroll out to see the whole page, and the book." Seen at levels 3, 4, 8: too early and too long (S6), though the words are plain.
Verdict: the words are plain; the timing does not teach. A new player sees hint 1, then a stale line, then refusals.

## 5. What is good

- One large message at a time in every picture; no banner and choice together (u03 to u16).
- The banner's and the line's cross-fade are eased and the card never changes size (msg-banner, msg-line).
- The ledger holds its size with the goal, strip and note coming and going; nothing else moved (u02 to u16).
- The purse, shop and the pills are calm and aligned; the pills show the price in every state (u02, u16).
- The 1280 u33 picture is clean: banner, ledger, tide, tags, line without any overlap.

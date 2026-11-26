# Layout review 1 (1920 x 1080, interface 100 percent, both ages)

Reviewer 1. Judged from the 35 pictures in `build/n3/ui-audit/l1-1920x1080-100/`, against `layout-sheet.md` (version 2) and the BEFORE pictures in `build/n3/ui-audit/1920x1080-100/`. No code changed. Read at 22:41 to 22:55 UTC.

Short verdict: much calmer than before. The old stacks of notes on the left and the 11 one-line cards on the right are gone; the ledger, the shop, the bell and navigation sit still and look like one family. What is left to fix is mostly at the edges: a cut-off strip name, a progress bar that shows the wrong thing, the old pointer tips and map tags, the see-through pages, and a tour that cannot show tooltips or the settings.

## 1. MUST FIX (worst first)

1. **The goal bar shows time left, not the count (sheet 3, Goal).** u05: "Delivered 2 of 34" and the bar is 96 percent full (283 of 296 px). u10: "Delivered 351 of 373" and the bar is 17 percent full and brick red, so a request that is nearly met looks like a failing one. u35: "Loaded 105 of 3279" with a 24 percent bar. Also u11, u16, u30, u33, u36, u37, u38. The sheet says the bar belongs to the count ("27 of 117", river; moss when met). Change: bar width = count / total; keep time only in the "within 116 s" text; brick only for the "under a quarter of its time left" tint of the "within" text, not of the bar. If the bar must also show time, add a second thin line, not the same bar.
2. **Charter drive name is cut: "The cha..." (u13, u14, u15, u16 strip area; sheet 3, Strip).** The strip's name box is about 70 px wide, the text needs 122. The sheet says "Charter", ten pips, "4 of 10". Change: label text "Charter" (7 letters fits), box at least 72 units, pips after it; test with the longest text. The strip also looks crammed next to the note below it (u13: strip and note are two rows 40 px apart with the same bold look).
3. **Pointer tips and map tags are still the old style (sheet 4 Card/Tag, sheet 5, sheet 4 "never the word coin").** u07: "Suggested: 40 coin" and "Extend this cottage: 40 coin. Rooms 2 to 3."; u08: "Gravel for this road: 18 coin. Click to pave."; u36: "Suggested: 74 coin", "Gravel for this road: 74 coin". They are bold black on a white box with a dark border: a fourth card look and a fifth text style next to the ledger and the shop. Map tags are another look (bold outlined white text, "A cottage, at level 2", "The ford, at level 19") and the boat label "Flour 0 of 26 loads" (u33) is about 11 px. Change: pointer tip and tags use the quiet card (paper 88 percent, caption), coin sign instead of "coin", one tag style. Progress says tags as screen text are not done: this is the one thing that is still the old interface in every play picture.
4. **Tags are cut by map things and by the line (sheet 5: a tag that touches a card fades out).** u30, u31: "A cottage, at lev|el 18" is cut by a post and a building; u33 "The ford, at level 19" runs under the river bank; u05 the plot frame touches the bottom of the banner (frame at y 115, banner ends y 110); u03, u04, u09, u12: the line card (bottom centre) sits on top of the plot frame and its label. Change: the tag fade rule of sheet 5 (also against the line and banner), and keep plot frames out of the Message and Line rectangles by moving the tag, not the zone.
5. **No picture shows a tooltip, a pressed state or a disabled Home (owner's rules: tooltips, hover/pressed/disabled).** The only hover is the shop row in u09. The tour has no state for: a shop row tooltip (with "pays for itself in", "30 coin short, about 8 s"), the bell tooltip, a nav tooltip, the ledger tooltip, a locked group heading tooltip. I cannot judge `TipRect` at all. Change: add audit states for each tooltip owner (left of a shop row, right of the ledger and timers, above a nav button) before the owner sees it. Also: Home should show the disabled look when the view is already home; in u01 to u16 it looks like the other buttons.
6. **Pages and the age page are see-through in four of seven pictures (u15, u31, u38, u39; sheet 2 and lead's note "pages opaque").** The map and the houses show through the text; the heading is unreadable ("Achievements, 22 of 24" is drawn over a house, "The second age: The River" over a road). Only u37 is opaque. L1 pictures says the tour takes them mid-fade at 1 fps. Either way: (a) retake after the fade has finished (wait for the fade to reach 1.0, not 900 ms), (b) cap the fade so that a slow frame cannot leave a page half-drawn for a second, (c) the scrim: in u37 the map is not visibly dimmed at all (compare u36 and u37), the sheet says a page dims the map only.
7. **The pause cover's question is not the modal card (sheet 2, Modal 480 wide, card style).** u20 shows "Start a new town? This town will be lost." as loose gold text on the brown cover with two outlined buttons, while u25 (settings clash) uses a paper card with its own buttons. Two systems for one job. The pause cover itself (u19, u20: gold serif on brown, 40 px lettering) is a third look. Change: u20 gets the same paper modal card as "That key is taken". Keep the cover as the cover.
8. **Settings pictures u21 to u24 do not show the settings (they show the book page 1 and 2 four times).** Same state, no information. The settings page, the "Keep this?" card and "Delete the save?" card are not judged by this review. Fix the staging; the lead has this in the notes already (L1 pictures).

## 2. SHOULD FIX

- **Shop names are cut in the row although the row has room to spare in the key column (u16, u30, u31, u33, u36):** "A fourth hand at…", "Better boat pric…". The sheet allows an ellipsis but the longest real text is exactly these. Change: show a shorter row name ("Fourth mill hand", "Boat prices") with the full name in the tooltip, or let the pill be 88 for every row.
- **Not-yet pills are too faint to read (all play pictures).** "1,974", "2,307", "22,198" are muted 60 percent on paper at 15 px: the price is the most useful number in the shop. Use ink at 70 percent (the sheet's rule for muted text that carries a number) and keep the 3 px wheat line. Same for line 2 ("+3.7 a minute · 30 short"): readable but light.
- **Slashed zero everywhere (the numbers read "6Ø", "2ØØ", "+Ø.1 a minute", "Ø of 24")**: u01, u02, u05, u06, u10, u11, all. It looks like the empty-set sign or an 8 at 15 px and makes the purse and prices look like a typo. Use the font's plain zero (turn the slashed-zero feature off) for all numbers.
- **The renown bar does not match its number.** u10 "3,515 of 5,700" with a 2 px bar; u05 "154 of 300" with a 27 percent bar (progress inside the level, number is the total). Pre-existing, but the sheet puts both on one line. Either show "n of m" for the level only or draw the bar for the total.
- **Book opening and page-turn pictures (u17, u40) overlap:** the slim purse chip ("980 · Level 16 of 16") lies over the left page's title "Dunstead" (title cut by it); the compass "N" is half outside the window (u40, y 0), the page number "2" is hidden under navigation (u40, x 1820 y 1072). Sheet 2: with the book in view only navigation stays. Hide the slim purse as soon as the book starts, and keep the book's content inside the window while it turns. In the settled book (u18, u41) the nav row also lies on the book's lower edge and the dark wood; give navigation a quiet card behind it there.
- **Navigation buttons say keys, not names (u01 to u41).** "N", "Home", "Esc" over icons, and the Achievements button has an inner key box that the other three do not. "N" for the age page means nothing without the tooltip (see must 5). "Home" is 40 px wide inside a 48 box: tight. Use the same key-cap place in all four (top-right corner, caption) and put the name in the tooltip only.
- **Tide text contradicts itself:** "Low, falling / low in 0:22" (u35, u37, u38, u39), "High, rising / high in 0:31" (u36). Say "Low now, rising in 0:22" or "Falling, low in 0:22".
- **Wheat count looks loud although the sheet counts it as one.** u08: four solid blobs at once (Extend 40, Pace 52, "On" ink, Regulars 29), u02 three. The sheet says the group counts once, so the check passes, but the picture is the busiest part of a calm screen. Suggest: only the wheat fill on the pills that are bought most cheaply per coin, or the pill stays paper with a wheat border and only a pulse once; or count each pill (the sheet's limit then needs 2 rows, not 3).
- **Shop grows and the groups below slide when a row arrives (u05 to u07, Roads from y 490 to 550, Town from y 578 to 638).** Allowed by the sheet but it is the "92 things change rectangle" the audit report counts, and it is the owner's rule 2. Judge in motion; if it still feels restless, reserve the next row's room (an empty ghost row line) or make the Work group heading do the sliding only.
- **Pave pill, blocked: a dashed empty box with a tiny "–" (u01, u10, u13, u16, u31 to u41).** Reads as a hole. Show "no road" in the box, or hide the pill and keep the reason in line 2.
- **Ledger and shop cards let the map show through** (u12 and u13: faint house outlines at x 190-310, y 270-330 and x 140-300, y 150-230 inside the ledger; u02 and u12 in the shop). Make the card opaque or 98 percent so the ledger reads as paper.
- **Left of the purse the number sits tight:** coin sign and amount are at 30 px, the amount aligns left, but the income line "+8,161 a minute" is body ink with the same weight as "Level 15 of 16": the purse has no second most important number, as the sheet wants (ink, bold or a size up).
- **The ledger when empty (u01 to u04, u09, u12) is a 336 px card with three lines in it.** It is calm and faint, but at the start it is the biggest thing on the screen for the least content; consider letting the purse alone show until the first request and then growing downward (the ledger being a fixed size is the sheet's wish, so this is a call for the lead).
- **Purse hint line is cut ("Next: A cottage, a lane and a second bucke…", u05; "A second field to buy, its track and co…", u10)** which the sheet allows; the hint is the sheet's "what the next level gives". Rewrite long ones to under 40 characters.
- **Two Level-2 labels while locked (u01 to u02):** bell tile shows "Level 2" for a locked bell. Fine, but it is the only locked thing shown outside the shop headings; either make it faint like the folded text or hide until the town is at level 2.
- **Bell ringing in age 2 (u35, u37, u38, u39) shows "10 s" with no bar** while u36 and u09 show the bar. Check the bar at full.

## 3. Per state

Number = things asking for attention under sheet 7 (loud: wheat pill group counted once, banner, choice, "On", Ring in the next age; quiet: note, line, strip, goal under a quarter of its time). My count first, then verdict.

| id | calmer? | loud / quiet |
| --- | --- | --- |
| u01 | much calmer; ledger nearly empty, hint line empty (first minute is next round) | 0 / 0 |
| u02 | calmer; 3 wheat pills at once | 1 / 1 (note) |
| u03 | calmer; line sits on a plot frame | 1 / 2 (note, line) |
| u04 | calmer; banner + note + line together | 1 / 2 |
| u05 | much calmer (banner, goal, note, line: stacked before, ordered now); the plot frame touches the banner | 1 / 2 |
| u06 | much calmer; choice is clear, 2 cards of equal weight, keys Z X | 1 / 2 |
| u07 | calmer; pointer tip and tag are old style and say "coin" | 2 / 2 (+ tip) |
| u08 | calmer; four blobs on the shop at once | 2 / 2 (+ tip) |
| u09 | calmer; hover row border clearly readable | 1 / 2 |
| u10 | calmer; red goal bar looks like a failure for a nearly met request (must 1) | 0 / 3 (note, goal low on time, line) |
| u11 | much calmer than the 28-card original (stack of 4 notes gone) | 1 / 3 |
| u12 | calmer; ledger shows ghost houses through the paper | 1 / 2 |
| u13 | much calmer; "The cha…" cut (must 2) | 0 / 3 (strip, note, line) |
| u14 | calmer; "Too late for the miller" stays as old-style plain note | 1 / 3 |
| u15 | page unreadable (must 6) | 1 + page / 3 |
| u16 | calmer; the banner is half faded; "Next: Ring in the next age" repeats the button below it | 2 / 3 |
| u17 | worse than needed: purse chip over the book title | n/a |
| u18 | same as before, calm; nav sits on the book edge | 0 / 0 |
| u19, u20 | unchanged old look, not part of the layout system (must 7) | n/a |
| u21 to u24 | not judged (must 8) | n/a |
| u25 | the modal card is right; it covers "Restore defaults" by 5 px | n/a |
| u30 | calmer; tag cut by a post; extra empty row in the shop under "Extend a cottage" | 1 / 3 |
| u31 | page unreadable (must 6) | 1 + page / 3 |
| u33 | calmer; bold two-line note "Milestone! Master tools. A little extra on top." squeezed to the note's bottom edge; boat tag tiny | 1 / 3 |
| u35 | much calmer (banner is clear and alone at the top); red goal bar | 1 / 2 |
| u36 | calmer; the shop reaches y 987, 31 px above the nav row: the limit for the 56 rows is here | 1 / 3 (+ tip) |
| u37 | calm; the age page is opaque and readable but a long wall of text in body size, uses "coin" | page / 2 |
| u38, u39 | page unreadable (must 6) | page / 2 to 3 |
| u40 | worse: compass and page number cut, purse over title | n/a |
| u41 | calm | 0 / 0 |

## 4. For the next round (messages)

- **Old-style messages seen, still in the old look:** the pointer tips and "Suggested" tags with "coin" (u07, u08, u36); bold accent notes in the note section ("Market stall is open." u10, u11; "Milestone! Master tools. A little extra on top." wrapping to two lines, u33): the note box was made for plain text, and bold fills it. Failure and request notes ("Too late for the miller. Maybe next time." u14 to u16, "The innkeeper asks for 1026 Water." u30, u31) share the same plain note with the same weight as "Achievement: Hamlet." so they cannot be told apart.
- **Notes stay too long and never leave:** "Achievement: First coin." u02 to u04; "A road has worn in. It can be paved now." u05 and u06 and u10; "Too late for the miller" u14 to u16 (three states). The note section is never blank, so it never reads as news.
- **The line is always on:** the town's hint stays up in every state ("A road is crowded. Pave it so more can walk it." u03, u04, u09; "The Basket is ready. Every trip carries one more." u05, u06; "The Yoke is ready" u10, u11; "A queue at the field. Better tools would help." u13 to u16; "A queue at the mill..." u30 to u33). The line card sits over the map and over plot frames. Messages need a stay time and a "seen" flag so one hint is shown once.
- **First minute:** u01 has no hint at all and an empty ledger; the first-minute hints belong into the line (sheet 3).
- **Banners and notes at once:** u04 banner + note + line; u11 banner + hint + note; u16 faded charter banner + goal + button + note + line. The queue and priority rules must hide the note while a banner shows.
- **The banner at u16 ("The charter") is half transparent** (fade); the banner and the choice share the Message zone: check the swap when both are due.
- **Age page text** (u37, u38): about 14 lines of body text with the word "coin" and numbers like "1,193,518 coin"; the message round should give this one page a short form and the coin sign.
- **Words:** "coin" with amounts in notes, tips, tags, age page; sheet 4 says coin sign only.
- **The choice's header line** ("Level 4: choose one boon, for good. The town runs on.", u06) is a serif single line, not a title; fine but different from the banner's title.
- **Pause question (u20) and "Keep this?" use the old gold text**; they move into the modal card (must 7).

## 5. What is good

- The ledger as one ruled sheet with a stable purse, goal, strip and note: the left side no longer jumps (u10 to u16, u30 to u37 the same boxes at the same places).
- The shop: group headings that say what is folded away ("Basket at level 3 · +1 more", "1 done"), no locked rows, three clear pill states, the key cap column and the icon column line up in both ages.
- The middle of the map is free in every play picture except for map tags and the hint ring, and the bottom-left bell/tide and bottom-right navigation do not change places between the ages.
- The choice of two (u06) and the banners (u04, u05, u11, u35) now sit in one place and read at once.
- The strip button "Ring in the next age [N]" (u16) has a lasting place and is the one accent in the ledger.

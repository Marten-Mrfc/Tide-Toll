# L1 layout review 2 (small window and interface sizes)

Reviewer 2, from pictures only. Read: all 24 pictures of the four 1920 / 1280 size sets (u03 u06 u11 u13 u33 u35 at 80 and 150), and 20 of the 35 pictures of l1-1280x720-100 (u01 u05 u06 u10 u11 u13 u15 u16 u17 u20 u22 u25 u30 u31 u33 u35 u36 u37 u38 u41). Not opened for lack of time: u02 u03 u04 u07 u08 u09 u12 u14 u18 u19 u21 u23 u24 u39 u40 at 1280 x 720 / 100. BEFORE pictures opened: 1280x720-100 u06 and u35 only. No picture shows a tooltip, so section 6 of the sheet cannot be judged at all.

Overall: far calmer than before. The sheet's zones hold in every play picture; the right rail, ledger, bell, tide and navigation never move between the ages. The faults are at the edges: the book and menu states, the shop at 150 percent in 1280 x 720, truncated labels, and the translucent pages.

## 1. MUST FIX (worst first)

1. **The slim purse is drawn over the book and covers the book's title.** u17 (1280/100): the "882 . Level 16 of 16" strip sits at x 12 to 260, y 12 to 42, over "Dunstead" (title cut in half). Sheet 2: with the book in view only navigation stays. Change: hide the slim purse (and everything but navigation) while the book is in view; test it by picture, the progress note says it is done.
2. **Book does not leave room for navigation and runs off the window.** u17 (1280/100): the compass "N" is cut off at the top edge (y 5), the page folio at the bottom edge is cut, and the Achievements / N / Home / Esc buttons (y 670 to 705) lie over the bottom edge of the map frame and the binding. u38 and u22: the book's bottom edge (y 686) is under the navigation row. Change: the book gets a Page-zone-sized rectangle that ends at y H-72 (above the navigation row), with the compass inside it; or navigation drops out while the book is open. Keep one rule for both ages (u17 first age and u38 second age place the book differently: 55 px margin vs 12 px, different size, different map title position).
3. **Pause question wraps into its own buttons.** u20 (1280/100): "Start a new town? This town will be lost." wraps to a second line "lost." that is drawn over the "Start a new town" button. Sheet 2: one modal card, 480 wide, card style. Change: build this question on the modal card (title, text with room for 3 lines, buttons below, 32 high, same Button style); the buttons here are two different looks (one dim, one bright) and no "On/primary" logic. Also the dark leather cover is a third visual language: at minimum put the question on a paper modal card.
3b. **Settings: "That key is taken" dialog covers the key table** (u25, 1280/100): it sits over "Take the right boon" and the "Restore" button, and the Sound / Display / Keys / Saving tabs are cut by the right window edge (x 1147 to 1265, text starts at the edge). Change: same modal card, centred on the window, scrim behind; tabs inside the window.
4. **Shop runs off the bottom at 1280 x 720 / 150 percent and cuts a row in half.** u33 and u06 (1280/150): the last visible row ("Town", then a row) is cut by the card bottom with the pill half-visible; no fade or edge cue; the only hint is a small "↓" next to "3 to buy now". The sheet allows scroll at 150 but not a half row: Change: clip at a row boundary (snap the shop height to whole rows, 40 high rows at 150, no row ever half shown), draw a visible fade or a "more" line at the bottom, and let the key row for hidden items stay in the tooltip. Also at 150 the age-2 shop (u33, u35) cannot show "The river" group at all (u35 shows "The river" heading cut as a sliver at y 612): it is the heading of the group the age is about and it is invisible. Fold finished groups first, or drop the group headings' air at this size.
5. **"The charter drive" strip label is cut to "The cha..." at 1280/100 and 1920/150, 1280/150.** u13 (all sizes), u10 (1280/100 shows "The cha..." with 10 pips and "0 of 10"). Sheet 3 says Charter, ten small pips, "4 of 10". Change: the label is the word "Charter" (7 letters), not the whole name; pips shrink before the name does.
6. **Translucent pages show the map through their text.** u15 (achievements), u31 and u37 (second-age page), u38 (age end page) at 1280/100: pages are see-through, rows run into map houses and boats, the title "The second age / The River" is drawn twice on top of each other (u31), and page texts are unreadable. Sheet 2 and progress note 21:20 say pages are opaque. If this is the picture being taken mid-fade at 1 fps (the builder's own guess), the tour must wait for the fade to end: no state of the next round can be judged until the page pictures are taken with the fade finished. If it is a real bug, set the page card to 100 percent paper. Either way do not ship this; take the pictures again.
7. **Old-style tip pair at the pointer in the Pave mode.** u36 (1280/100): two tips side by side ("Suggested: 64 coin" tag and "Gravel for this road: 64 coin. Click to pave.") plus the line at the bottom ("Pave: click a lit road..."): three texts about the same thing, and both use the word "coin" after an amount (sheet 4: coin sign, never the word "coin"). Also the pointer's tip is drawn over the line zone's neighbourhood and over the tag. Change: the tag goes (the tip says it), the tip uses the coin sign, one text.
8. **Shop pill for a row you cannot use shows only a dash.** u05, u06, u10 etc. (all sizes, both ages): "Pave a road" shows a dashed pill with "–" and nothing else. Sheet 3: the pill always shows the price with the coin sign, blocked is paper, muted text, dashed border; and for Pave "from N". Change: "from 18" is fine when there is a road to pave later; when none can ever be paved yet show a coin sign with "from 18" muted (or the cheapest price) and put "No worn-in road to pave" on line 2 (it already is at 56 rows; at 40 rows it is lost, so keep it in the pill's title). The empty dashed box also reads as an empty text field.

## 2. SHOULD FIX

- **Purse bar touches its text.** u01, u05 (1280/100) and u06 (1280/150): the renown bar sits directly under "Level 7 of 16" with 0 px air; the bar looks like an underline. Sheet 3 has line 3 above the bar: leave 4 px. The purse reads cramped at 100 percent in 1280, while the ledger below it is 60 percent empty ruled paper in the first age (u01, u03, u06 at every size): the empty ruled lines are the biggest empty area on screen at 150 percent. Move 8 units of height from the empty sections into purse spacing or make the ledger height follow its content between two fixed heights (sheet says fixed: then give the purse the air).
- **Note section has three looks.** "A road has worn in..." (ink, regular), "Milestone! Water: a fair name. A little extra on top." (ink, bold, wraps to a second line), "Achievement: Chartered." (muted), "Too late for the innkeeper..." (faded, u35 1920/150). Sheet 3: the note is one style, two lines, 90 chars. One style, no bold for part of the message, and the milestone text is two sentences of 60 chars that only just fits.
- **Bell label and key cap crowd.** u05, u16, u33 (1280/100 and 150): "Ring" with the "Space" cap packed against it, "Ring" baseline higher than the cap; at 150 the cap touches the word. Cap box needs 8 px air. In the first minute the button reads "Level 2" (u01) with no icon colour change: it is the locked state, fine, but it is the only locked button on screen and the text is not a button style.
- **Navigation key caps overlap the buttons' lower edge.** "Home" and "Esc" caps (all sizes, 1920/80 u06 worst) sit on the border of the 48 x 48 button; "13 of 24" and "Tab" are two boxes inside one box. Move caps inside with 4 px air; hover/pressed states cannot be judged.
- **Solid wheat pills are a wall when rich.** u36 (9 solid pills plus an "On" pill), u38, u06 1920/150 (8 pills): the sheet counts the group once, but to the eye nothing stands out. Consider solid only for the cheapest 3 affordable rows or a lighter wheat tint for the rest; the pulse needs something to pulse against.
- **Group heading right text is inconsistent.** "1 later" (u13 1920/150), "1 done" (u35), "1 to buy now" in the heading, "Extend a cottage at level 5" (u01) and "Louder bell at level 14" (u11): sheet says "1 done . Yoke at level 7". Pick one grammar; "later" is not in the sheet.
- **Shop rows lose the worth line at 40 high.** u06 and u33 at 1280/150 and 1920/80 variants: the "short" state and the worth line disappear, leaving only dim pills; the affordable bar is still there. Acceptable per sheet 3, but then the tooltip is the only place for it and no picture proves a tooltip exists.
- **Row name ellipsis.** "Extend a cotta..." (u06, u33 1280/150), "Better boat pri..." (1920/150 u33), "A fourth hand at..." (1280/80 u33), "Better boat pric..." (u33 1280/80): the name is cut with the ellipsis (allowed) but the step number is dropped before the name is shortened; the sheet says optional captions go first, and the names are the longest of the real texts: let the pill shrink to 88 at 1280 and keep the full names.
- **Ledger "Next:" line is cut** with an ellipsis at 150 (u13, u33: "The ford and a lane on the far..."). Allowed by the sheet; the full text must be in the tooltip, which I could not see.
- **Hover/pressed/disabled states.** No picture shows a hovered or pressed row or button. The affordable state is only seen solid wheat; the "not yet" growing line is visible but 2 px high and very faint at 1280 (u05, u10).
- **Tags still old style on the map.** "A cottage, at level 8" is large white outlined map text at 1920/80 (u06) and tiny (6 px) at 1280/150 (u06, u11: "A cottage, at level 8" is unreadable), while "Flour 2 of 33 loads" boat tag is a different tiny font (u33). Not in this round (item 8 left), but the tags touch the ledger/shop at times: u17 tag "The mill, to buy" and "A cottage, at level 14" near the shop edge (u11).

## 3. Per state: calmer than before? things asking for attention now

Calmer = clearly yes in all unless noted. Count = loud groups (wheat pill group, accent banner or choice, On pill) / quiet things (note, line, strip, goal) as I saw them.

| State | Calmer? | Loud / quiet |
| --- | --- | --- |
| u01 (first minute, 1280/100) | yes, much (no hint card, no three slips) | 0 / 3 (note empty, line none, ledger) |
| u05 (level 3 banner + request, 1280) | yes | 2 (banner, pill group of 0) / 3 (goal, note, line) |
| u06 (boon choice, 1280 at 80, 100, 150, 1920 at 80, 150) | yes; the choice sits in the Message zone and covers only the top of the middle | 2 (choice, 8 solid pills at 150) / 3 (goal empty, note, line) |
| u10 (1280/100, level 8) | yes | 0 / 3 (goal, note, line) |
| u11 (level 13 choice, 80, 100, 150) | yes | 1 (choice) / 3 (goal, note, line) |
| u13 (level 15, strip, 1280 at 80, 100, 150, 1920 at 80, 150) | yes; the strip label cut | 0 / 4 (goal, strip, note, line) |
| u15 (achievements page, 1280/100) | not calmer: translucent page full of text over a busy map | 0 / 3 |
| u16 (charter banner, "Ring in the next age" accent button, 1280/100) | yes, but a banner, the accent button, a pulsing ring and floating coin numbers at once | 2 (banner, accent button) / 4 |
| u17 (book, 1280/100) | not calmer, slim purse and nav on the book | 0 / 1 |
| u20 (new town? question) | not calmer, text over button | 1 / 0 |
| u22 (book mid page-turn) | staging only; settings tabs cut at the edge | 0 / 0 |
| u25 (keys + "That key is taken") | not calmer, dialog over table | 1 / 0 |
| u30 (age 2 start, 1280/100) | yes | 1 (solid pills) / 3 (note, tide, line) |
| u31, u37 (second-age page) | not calmer, text on the map | 1 / 3 |
| u33 (age 2, 1280 at 80, 100, 150; 1920 at 80, 150) | yes; at 1280/150 shop cut | 1 / 3 (goal, note, line) |
| u35 (borough seal banner, 1280 at 80, 100, 150; 1920 at 80, 150) | yes (one banner plus request and note) | 1 (banner) / 4 (goal, note, tide, line) |
| u36 (Pave mode, 1280/100) | not calmer: two tips plus line | 2 (On pill, wheat wall) / 3 |
| u38 (age end card, 1280/100) | card good, translucency at u41 | 1 / 2 |
| u41 (book second age, 1280/100) | book text tiny (columns at 6 px) | 0 / 0 |

Not opened: the rest of the 1280/100 set; I cannot give a verdict for them.

## 4. For the next round (messages)

- Old-style messages still shown as plain slips in the note section: "A road has worn in. It can be paved now." (stays for the whole of u03 to u06, on every size: a note must not last through several states), "A new villager moves in." (u03 1280/150 stays in u06/u11 pictures: stale), "Milestone! Water: a fair name. A little extra on top." (bold, two lines), "Achievement: Chartered." (muted), "Too late for the innkeeper. Maybe next time." (fades, in u35), "New to buy: A third hand at the mill.", "A cottage is extended: room for one more." Make one note kind with one style and a stay time.
- The line says "The Basket is ready. Every trip carries one more." in u03 to u06 while the player has 4 coin and the basket costs 200: the line lies. Also "The Yoke is ready" in u11 (level 13) when Yoke is already bought. "A queue at the field/well/mill. Better tools would help." is repeated in 9 pictures: priority and stay time needed, and it is the line that most often sits at the bottom while a pointer tip also shows.
- Banners are two kinds today: the accent banner (u05 "Level 3", u35 "The borough seal", u16 "The charter" fading) and the choice. The "The charter" banner (u16) is half-faded and shows in the same place as the choice would: queue them, as sheet 3 says.
- Level gifts: u05's banner says "The first field and its track." where the ledger's goal also says "Request from the innkeeper": two big things for the same moment.
- Failed or refused clicks, the "Suggested: 64 coin" tag, the pointer tip and the line text in Pave mode: one text each, no word "coin" after a number ("Gravel for this road: 64 coin").
- "Too late for X" (missed request) belongs in the goal section ("Too late" in brick for 1.6 s, sheet 3), not in the note.
- Texts with the word "coin" after a number seen: achievements page ("Earn 1,000 coin in all"), age end page ("963,912 coin"), tips; the sheet forbids it in the shop and purse only; decide for pages.

## 5. What is good

- The ledger and the shop are calm cards; the right rail has the same order in both ages and the same place for Pave, Louder bell and Regulars; nothing in the bottom corners moves between ages except the tide that joins the bell.
- The choice of two (u06 at all sizes, u11) opens in the Message zone, grows downward, keeps icon, worth line and keys Z and X, and leaves the middle of the map free.
- Pills show a price in three readable states; the growing line under "not yet" works.
- Rows at 100 percent have a clean two-line layout with key cap, icon, name, step and worth; "+3.7 a minute . 30 short" is exactly the information the owner asked for.
- At 80 and 150 percent the zones hold their places with no overlap in the sets I read (mechanical overlaps in report.md are the scrim and the book).

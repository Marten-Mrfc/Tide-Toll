# Layout critique 1: the player's eye (against layout sheet version 1)

Critic 1, 26 Nov 2026, 20:30 UTC. Read: the sheet, m1 to m9, mock-up notes, c05 and f08 (present), ui-inventory, message-inventory, message-system. The audit files `audit-ui-*.md` did not exist yet and the `ui-audit` report was not read. I also read `GameInput.cs` to check keys. Nothing was built or changed.

How I counted "asks for attention" (something with an accent or gold fill, a moving bar, a pulse, a counter that changes, new text): m1 at rest 8 (goal card, note, 3 solid gold pills, 4 part-gold pills read as near-gold, "3 to buy now", the orange ring on the map, the bell). m2 about 12 (adds the gold banner, the running bell bar, the line, the drive pips, 4 coin numbers, 2 tags). m3 6. m6 (1280) about 10. The sheet's test 5 passes all of these only because "the affordable pills together count as one". That is the weakest part of the design (must-change 8).

---

## 1. MUST CHANGE, worst first

**M1. The canvas rule cannot hold at 1280 x 720, and the smallest text becomes unreadable.**
- Sheet line 7 says "at 150 percent (and at 1280 x 720 with 150 percent) it is 1280 x 720". That is only true for a 1920 x 1080 window. A 1280 x 720 window at 150 percent has a canvas of 853 x 480 if the setting divides the window, or about 1094 x 615 if the window's own `ScaleFor` (0.78) is also applied. Both are below the 1280 x 720 the sheet promises ("every canvas from 1280 x 720"). Two rails (672) leave 181 px of map at 853 wide. The shop rail (H-96 = 384) holds about 6 rows.
- Real text size is not in the sheet at all. Caption is 14 canvas px. At 1280 x 720 and 100 percent that is about 11 real px (14 x 0.78). At 80 percent it is about 9 real px. The caption holds the income, the level, every "+3.7 coin a minute" and every reason. The inventory notes `FontPx` has a floor of 12 today; the sheet's fixed boxes ignore it, and that is how the old UI got "cut" text.
- Rule: (a) write a table of the 6 window and setting combinations (1920 and 1280, 80, 100, 150) with the exact canvas size and the real size of caption, body and title; (b) the effective interface size is clamped so the canvas is never smaller than 1280 x 720 (cap = min(setting, W/1280, H/720)) and caption is never under 12 real px (raise the lowest setting for small windows, do not shrink boxes); (c) tests 1 to 3 run on the table, not on the words.

**M2. The mock-up invents keys that the game does not have, and one that does something else.**
- m3 shows the boon buttons as "Choose [1]" and "Choose [2]". In the game the boons are **Z and X**; **1 is Villager and 2 is Pace** (GameInput.cs:31-32, 45-46). A player who trusts the picture buys a Villager.
- m5 and m6 give the river rows the keys **Q, W, E**. The game has no river keys (f08 shows none). **W is "move up"; E is the second key of "Extend a cottage"** (GameInput.cs:21, 34). Printing them would be a lie; binding them would break scrolling and Extend.
- Rule: a key cap is drawn only from `GameInput` (the bound key, reprinted when bindings change). Rows without a key draw no cap but keep the column, so names stay aligned. Boon buttons print Z and X. If the lead wants river keys, that is a new binding (a play change; add to the settings list first, avoid W, E, A, S, D, P, N, Z, X, F, G, H).

**M3. "Ring in the next age" has no place that lasts.**
- The sheet puts "the ring-in-the-next-age prompt" in the Message zone, which shows 72 high for 3.2 s. message-system.md puts it in the Goal zone. The old button ("Ring in the next age [N]", then "The river age: read its page again") stays until pressed. This is the biggest decision of the first age, and it can be missed (the charter banner also fights the level-16 banner; inventory B2).
- Rule: one persistent slot, **x 16, y H-120, 320 x 48** (above the bell, nothing else may use it; empty and invisible before the charter), carrying the button with its [N]. The Message zone carries only the one-off banner "The charter". Delete the Goal-zone version. In the second age the same slot holds nothing (the nav "age page" button covers "read again").

**M4. The price pill hides what the player needs to compare, and its four fills are noise.**
- m1: Villager shows "No room" where the price was; the old row said "54 coin: No room: wait for a cottage". The player can no longer see the price of anything blocked, and cannot compare it with the purse. "Need 30 more" moved to the tooltip.
- The saving-up fill (92, 45, 38, 31 percent gold) sits next to solid gold. In m1 the 92 percent pill ("52") is almost the same as the affordable "30". The one signal the owner asked for (can I buy it now) is blurred. The column of different gold widths is the busiest thing in the rail.
- Rule: the pill always shows the price (coin sign + number), in three states only: **affordable** (solid wheat, ink bold), **not yet** (paper, muted text, a 3 px wheat underline growing from the left, like the old 4 px price bar), **blocked** (paper, muted text, a dashed border). The reason ("No room", "No worn road", "Level 7") goes in line 2 after the income: "+27 a minute · No room". Use the coin sign instead of the word "coin" in line 2 and everywhere ("+27 a minute"): it is 5 characters shorter and fixes mock notes 6 (cut names). The tooltip still carries "Need 30 more, about 8 s".

**M5. The shop rail is a full-height cream slab even when it is two thirds empty, and at 1280 it still scrolls.**
- m1, m3, m7: 10 rows and a blank cream wall down to y 1000. It hides map for nothing (the map is the first thing the eye should find) and looks like an app sidebar, not a page. m5 and m6: the rail covers the river's east bank and the landing (the second age's whole point).
- m6: the last row is cut in half and a 4 px thumb is the only sign. The rows are 584 high at 40 px, 20 over.
- Rule: (a) the shop card is **as tall as its content** (top fixed at y 16; only its empty bottom edge moves; no zone moves) up to H-96; (b) **merge the fold line into the group heading**: "Roads   3 done · 1 at level 9" (right-aligned caption). This removes 3 lines x 24 = 72 px: the second age late then fits at 40 px rows (512 of 564) and needs no scroll, so the scroll rule stays only as a safety net; (c) under a canvas width of 1600, the rails are **272** wide, row icons are dropped, and the free middle grows from 592 to 688 (+16 percent); the town is visible between the rails; (d) the rail scroll must keep "6 to buy now" honest: if an affordable row is out of sight, the header count shows a small down mark.

**M6. Tooltips are placed for the shop only.**
- "Left of the shop rail" is right for the rows. For the bell, the tide (bottom left), the purse lines and the Achievements/centre/pause buttons it puts the explanation up to 1300 px from the pointer, on the other side of the map. A player hovering the bell sees nothing happen. Mock note 10: it also covers the banner at 1280.
- Rule: the tooltip opens beside the thing: shop rows to its left (as now); anything in the left rail or the timers to the right of its zone (x 344, same top, inside the middle, allowed while hovering); navigation buttons above the button, right-aligned to the rail. Never over the Message zone when a banner is up: it moves down instead.

**M7. The notes strip cannot hold the notes it is given.**
- 40 high, one line, 296 wide at 14 px holds about 44 characters. The failed-save notes (N22, N23) are 90 to 110 characters and strong; "Achievement: <name>" and "Milestone! <name>. A little extra on top." are 40 to 50. The present game gives these 2 to 3 lines. A save failure must not be cut or last 3.2 s.
- Rule: the note is 2 lines high (**56**, y 280 to 336; move nothing else: the drive strip sits above it) with a hard limit of 90 characters; texts longer than that are rewritten in the message pass, not cut with a dots; a failed save stays until clicked (message-system already says so). The reserved note space and the goal space are inside the left sheet (see S1) so no hole shows.

**M8. Test 5 ("at most five things ask for attention") is gamed by its own definition.**
- It counts all affordable pills as one, and does not count tags, coin numbers or the line. m2 has 12. A test that passes while the picture is busy will not make the game calm.
- Rule: write the budget by tier. **Loud** (solid gold pill, accent banner, ink-filled "On", a pulse, a running bar): at most **3** at once, and the pills together count 1 only if no more than 4 are loud; the banner and the choice count 1 and may not coexist (M9). **Quiet** (a note, the line, the drive strip, tags, coin numbers): at most 4 at once, coin numbers and tags excluded because they are the map. The test lists what it counted, per state, in its report.

**M9. Two large messages want the same place and the sheet has no order.**
- Levels 4, 7, 10 and 13 raise the banner ("Level 7: ...") and the choice together (inventory section 3, moment 1). Both belong to the one Message zone. The choice stays until picked and is 168 high; the banner is 72 high. The banner carries the gift of the level just reached; the purse's "Next" line already shows the *next* gift, so the banner's text is lost if the choice wins.
- Rule: the banner always shows first for its 3.2 s; the choice grows out of the same top edge after it (72 to 168, downward only), the queue order written in the sheet (charter, seal, new age, drive, level, choice). Add the Message zone to the list of things allowed into the middle (mock note 8): y 96 to 184 only, 560 wide, centred. While a choice is open nothing else may enter that rectangle (not the line, not a tip).

---

## 2. SHOULD CHANGE

**S1. The left side is four equal paper cards stacked: the same weight for the purse, the goal and a one-line note.** In m1 and m2 the eye finds three boxes with the same border before it finds the map. Draw the left as **one sheet** (one border, one paper) with ruled sections: purse, rule, goal, rule, drive strip, rule, note. Empty reserved sections show blank paper, not a gap. This removes the hole at y 224 to 280 in m1 (drive strip reserved but empty), makes it look like a page and not a dashboard, and halves the borders on screen. The note may keep its quiet variant inside the sheet. Same idea for the right: one rail sheet. Navigation and timers stay separate small cards (they are controls).

**S2. Income is the second most important number and is the smallest.** m1 "+296 a minute" is caption size and muted, right of the big number. In an incremental game income is what the player watches. Put it body size, ink, directly under or beside the amount; keep the muted style for "1,312 of 2,200 renown".

**S3. "Level" means two things in one picture.** "Level 6 of 16" (the town) and "Villager level 9" (the item) sit 100 px apart; "1 more at level 7" is the town again. Rename the item's number ("no. 9" or "x9" in the row, "level" kept for the town only) or colour it as a count. At least do it for the unlock text: "2 more when the town reaches level 7".

**S4. Muted text is 60 percent ink: about 4.0 to 1 on paper (my estimate), for 14 px text.** The rows' "+3.7 coin a minute", "level 9" and every reason are the comparison data of the shop. Use 70 percent (about 5 to 1) for text that carries a number; keep 60 percent for captions without one.

**S5. Slashed zeros in every number** (m1 "2,2Ø0", "level 1Ø", "+4.Ø"). Atkinson's zero is crossed. In a book-like game, in the purse and on price pills, it reads as code. Use the serif's lining figures for numbers (Alegreya has tabular lining), or the sans without the slash if the font has the alternate. Decide once in the type row.

**S6. The look is a generic app card.** Crisp 2 px ink rectangles, 4 px corner, flat cream; the map's buildings have a heavy hand-wobbled outline (3 to 4 px) and the book pages have paper grain. Rules: borders use the same outline pen as the map's art (or a slight irregular line from the existing page-edge code); paper uses the book's grain at low strength; group headings and the purse's "Level" in the serif; numbers in the serif (S5). Nothing else is needed: no new asset, no new colour.

**S7. Row icons cost 28 px of the name column and carry no information in the mock** (every icon is the same stand-in chevron, so the picture cannot show whether 24 px reads). Keep them only if the finished icons are tested at 24 px in a picture (name column of 132 is already too narrow, mock note 6); under 1600 canvas width drop them (M5).

**S8. Coin numbers at 16 are specks (mock note 11): use 20**, with the ink edge; keep the six-at-once cap. Also keep the settings option "coin numbers on/off": the sheet does not say it still works.

**S9. Nav buttons are unreadable as icons.** Achievements, the age page and the centre button are three small chevrons/dots in m1; only Pause is plain. Give Achievements its trophy and count (as now), the age page its leaf, centre its target, and label the age page when something new is on it. Keys only in tooltips is fine for Tab, N, Home; **the bell key Space should stay on the button** ("Ring [Space]", mock note 3): the bell is pressed hundreds of times and Space is the whole point.

**S10. Prices over 6 digits.** A pill of 96 holds about 7 characters with the coin sign. The second age already reaches 18,639; a longer game reaches 1,000,000. Write the rule: pill grows to 104 (and name column shrinks) or the number is cut to "1.2M" above 999,999 (the tooltip keeps the whole number).

**S11. Mock notes 1 to 7 and 9 stand** (purse 112 high, goal 104, bell/tide text, drive strip, row names, purse line); they were written against this sheet and I did not repeat them. Mock 12 (shop ends at content) is M5.

**S12. The affordable-now state and the focus ring.** The sheet names "river blue" for focus; UiKit has #56B4E9 (mock notes). Pick one and put the colour names in the sheet (wheat, moss and river blue are not Palette names). Gold on cream has a poor contrast for the pill's text only if the text is muted: affordable text must be solid ink (it is, in m1: keep it).

**S13. "Pulse once" on a row that becomes affordable** means up to 6 rows may pulse in one frame after a bell or a big delivery. State: at most 2 pulses per second, one at a time, in shop order.

---

## 3. Elements or messages with NO place, or two places

No place in the sheet:
- **Hint ring** on the map (orange ring at the building the hint is about, inventory 2.1, message C6). It is the loudest thing in m1 and the sheet never mentions it. It must exist (it points at the thing the line talks about) but needs its rules: quiet colour, shown only while the line shows it, never under a zone (tag rules).
- **"Back to the town [Home]"** (inventory 2.3): the centre button replaces it, but the old one appeared only when the view had moved. Say the centre button is always there and say what it looks like when the view is already home (disabled).
- **"+ hand" markers** (inventory 2.4, L3): message-system lists them as tags; the layout sheet's section 5 tags do not. Add.
- **"Suggested: n coin" tag** and the lit road bands: message-system says tag; layout sheet silent. Add.
- **The load row, the building offers (keys F, G, H) and the bell track:** the sheet says "People, Work, Roads, Town": say which group each of Load, "Market stall" and offers belongs to (m1 puts Market stall in Town and has no F/G/H; if offers move, "nothing moves when something appears" needs a place for the first new offer).
- **The page-turn leaf** over the whole screen when ringing in the age: it ignores every zone; say it is above everything and the interface is not rebuilt under it.
- **The pause cover, settings, the "Keep this?" card, "Start a new town?":** the sheet says they use the same styles but gives them no zones. The card styles must include a modal card (centred, 480 wide) used by all of them.
- **The skipper's request when a town request is running** (today invisible, C3). The sheet's "one current goal" keeps that. Free place: the **drive strip is never used in the second age** (the drive is first-age only), so the second request fits there at no cost. Cheap, no play change; take it.

Two places:
- **Ring in the next age:** Message zone (sheet) and Goal zone (message-system). See M3.
- **Hints:** "No hints here" in the purse, hints in the Line, and the hint ring on the map: OK, but the sheet's Line says "otherwise the current hint" while message-system makes four first-minute hints and refusals also use the Line. One zone, three kinds: add the priority (refusal over mode text over hint) in the sheet, not only in message-system.
- **Achievements:** nav button count, page title count, note on unlock. Fine, but the count must come from one number.
- **"Next" gift:** purse line and the banner text of the same level. Fine only with M9's order.
- **Tide:** the dial in the timers zone (sheet) and "before the tide turns" in the skipper's request: ok. The sheet's text "High water, falling" is cut in m5/m6 (mock 4): "Falling, low in 1:14" fits.

---

## 4. Where the design changes how the game plays

1. **Price of a blocked item is hidden** (M4). Old: "54 coin: No room". New: "No room". Info lost: what it will cost.
2. **"Need n more" only on hover** (old: on the row). One extra action to read it for each item. Keep the exact number in the saving-up state: pill text shows the price; add "n more" to line 2 only when the item is the cheapest not-yet-affordable one (one number, no clutter), or accept the loss and list it as a decision.
3. **Locked items lose their names**: "2 more at level 7" against the old "Yoke: level 7". The player used to see what is coming. Say the next one's name: "Yoke at level 7, and 1 more".
4. **Finished items lose their names** ("3 done"). Intended; but then the shop no longer shows the player which tracks are finished. Fine if the tooltip of the heading lists them (one hover, same as the old click count: none). Say it.
5. **Boon choice loses** the per-option icon, the "+n coin a minute now" line (the number the player compares), "for good" and "The town runs on". Restore the coin line: it is the information needed to choose; keep the buttons at Z and X (M2).
6. **Pave line drops "Hold Shift to pave several"** (m3 text "Pave: click a worn road. Esc to stop."). That is the only place the Shift rule was told. Keep it.
7. **Boat tags**: "a boat's tag shows for the boat at the berth; other boats show theirs on hover" (sheet 5). Today every boat shows "3 of 6 Wool, waiting". That is an extra hover per boat to read the second age's main information. Show all, shorter ("Wool 3/6"), and hide only a tag that touches a zone or another tag.
8. **Keys no longer visible**: Space (bell), Tab, Esc, N, Home move into tooltips; "4 or P" becomes "4"; Extend's second key E and Pave's P are not shown anywhere. Keys do still work. Counting clicks: no change. Counting hovers: one more to learn a key. Keep key caps for the shop rows and the bell; print the second key on hover.
9. **Coin numbers**: 0.9 s instead of 1.8 s, six at most, merged within 0.5 s. A delivery's own amount is no longer readable for the player who counts deliveries. The 1.8 s (halve the rise, not the stay) and merging only the *same* building are the safer values. Not a play change, but the numbers are the game's main feedback.
10. **Zoomed out:** "Message" is not in the list of what fades at the Page stage (sheet section 2). The old game keeps the choice card over the book ("never missed"). Say the Message zone stays at Page and Book while a choice is open. A banner raised while zoomed out is lost today (inventory section 1); the queue should hold it until the Town stage.
11. **Refusals leave the note stack for the Line** (message-system): faster, no loss. Check "The bell rests 31 s" stays when the pointer is on the bell (tooltip) as well.
12. **Scroll wheel:** over the rail it scrolls the rail, over the map it zooms. When the rail is at its end, the wheel must not zoom the map (a player flicking the rail zooms out to the book by accident). State it.

---

## 5. Keep

- One grid, fixed zones, nothing resizes: the single best change; it answers the owner's complaint.
- The row design (name, income line, price at the right, whole row is the target) and the fold lines: far calmer than three-line bookmarks.
- Purse with a fixed four lines and "Next" cut to one line; tooltip order (what it does, what it is worth, price, why not, key).
- Messages by kind with one place each (message-system); refusals in the Line; coin numbers and tags as map things.
- The mock-up itself: it found 12 faults in its own sheet, and its own check (`__report`) is the right base for the `game-layout` step.

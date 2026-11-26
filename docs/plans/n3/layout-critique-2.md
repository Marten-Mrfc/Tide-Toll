# Layout critique 2: edge cases and buildability (against layout sheet version 1, 20:20 UTC)

Critic 2, written 20:15 to 20:30 UTC. No code changed. Read: brief, sheet, m1 to m9, mockup/notes.md, ui-inventory.md, message-inventory.md, message-system.md, `UiKit.cs`, `TownHud.Shop.cs`, `CameraRig._UnhandledInput`, `GameInput.cs`. The `audit-ui-*.md` files and the mechanical report did not exist yet. Canvas px unless said. Numbers I computed myself are marked (calc).

The sheet's idea (fixed zones, one set of styles) holds. The faults below are in the numbers, in four places where the sheet contradicts the game's code or itself, and in the information the new shop and choice card drop.

---

## 1. MUST CHANGE, worst first

### M1. The canvas rule is wrong, and at 1280 x 720 with 150 percent no layout exists (sheet section 1 and section 7)

- Sheet line 7 says "the canvas is the window divided by `UiKit.ScaleFor`". The code does something else (ui-inventory section 0): `_s = ScaleFor(real window height)` (clamped 0.78 to 2.5), and the interface size setting is the window's content scale, which shrinks the visible rect. The two multiply.
- A 1280 x 720 **window**: height ratio 0.667 is clamped to 0.78. At 100 percent the visible rect is 1280 x 720 and `_s` is 0.78. At 150 percent the visible rect is **853 x 480** (calc: 1280 / 1.5), still with `_s` 0.78. At 80 percent it is 1600 x 900.
- The sheet's "1280 x 720 at 150 percent" is really the 1920 x 1080 window at 150 percent. The mock-up (m6) only draws that. **A 1280 x 720 window has never been drawn.**
- On 853 x 480 the two rails alone take 672 (sheet: 2 x 336), leaving 181 px. Rail 320 x (H-96) = 384 high leaves room for about 6 rows. Nothing in the sheet or the mock-up is correct there.
- At 80 percent in a 1920 x 1080 window, caption 14 canvas px is 11.2 real px, under the project's 12 px floor (`FontPx` floor, UX tab). The floor then makes caption 14 into 15 canvas px, 7 percent wider than every width the mock-up measured. Every "fits by 5 px" in the notes (purse 287 of 292, bell, tide, drive strip) fails at 80 percent.
- **Rule to write:** (a) `_s` is always 1 and the canvas is `window / interface-size`, with ScaleFor dropped for windows under 1920 wide, or (b) keep ScaleFor but state the three multipliers in one table. Then **clamp the interface size so that the canvas is never under 1280 x 720**: a 1280 x 720 window offers 80 and 100 percent only, and the 150 percent entry is greyed with "needs a bigger window". Setting `ui-size 150` at 1280 x 720 then equals 100 percent. That is the only way the owner's "nothing is cut off at 1280 x 720 at 150 percent" can be true, because there is no layout on 853 x 480. Also set caption to 15 (not 14) so that 80 percent reaches 12 real px, and measure every width at 15.
- Add the real windows to the tests: 1280 x 720, 1920 x 1080, 2560 x 1440, 3840 x 2160, 1366 x 768 (canvas 1366 x 768 is not a multiple of 8, see S1).

### M2. The shop row is wider than its column: the sheet's own fix (level on line 2) does not fit either (section 3 Shop; mock-up notes 6)

- Name column (calc): 296 inner minus key 20, icon 24, pill 96, three 4 gaps, padding 8 = 136 (132 in the notes, 124 with the scroll thumb). The notes propose "level 14 · +359 coin a minute" on line 2. That is 29 characters, about 203 px at caption 14 (calc, 7 px a character; the notes measured "+1,424 coin a minute" at 136 for 20 characters). "level 36 · +1,472 coin a minute" is about 218 px. Neither fits 136.
- And the pill: "◎ 1,234,567" (second age, coin runs into millions: the purse shows 524,743 renown at level 24 of 28) is about 100 px at body bold, over 96 minus padding.
- **Rule:** the row is **56 high in two lines, with the pill only on line 1**: line 1 `[key][icon] Name ........ [pill 32 high]`; line 2 starts under the name and runs the full width under the pill: `level 36 · +1,472 a minute` (about 180 px, room for 252). Drop the word "coin" in line 2 and use the coin sign, per the sheet's own "coin amounts carry the coin sign". The pill grows to 104 (24 less name room is no longer a problem since the name has the whole of line 1: 296 - 20 - 24 - 104 - 12 - 8 = 128 for the name, which holds "Water price" and "Flour price" at body 16 (about 90 px); the longest name today is a building such as "Market stall" or "A bigger raft", under 110 px).
- Pill text longer than 104 (a million-plus price) uses the compact number rule in M5.
- Shop height at 56: late second age, 11 rows (616) + 5 headings + 3 fold lines. See M3 for the heading/fold merge that makes it fit 1080 and 720.

### M3. The shop cannot show everything at 720 high without scrolling, and scrolling breaks "see and compare everything with the same clicks" (mock-up notes 9; sheet "Fitting")

- m6: at 40 px rows the late second age is 20 px over; the last row ("A bigger raft") is half hidden and the player must scroll. Today the shop shrinks type in two steps (`_compactShop`, `_tightShop`) and **always shows every row**. The scroll is an extra action and hides affordable rows (the "3 to buy now" count in the header cannot point at them).
- At 56 rows (M2) it is worse at 720.
- **Rule:** (a) **fold lines live on the heading line**, right-aligned in caption: `Work ........ 1 done · 1 more at level 7`. This removes three 24 px fold lines (72 px) and the separate "N done" slot. (b) A **compact tier under canvas height 800**: rows 40, one line, the second line dropped (income and level go to the tooltip; this is exactly what today's `_tightShop` does, so no new loss in kind). Calc for the m6 case: 11 x 40 + 5 headings x 24 = 560, against 564 inside the rail. It fits with 4 px; tighten headings to 20 (calc 540) to leave air. (c) Keep the scroll only as the failure case, not as the 720 solution, and test that no state of `ui-audit.sh` scrolls at 1280 x 720 100 percent.
- If scroll stays at all: the wheel over the rail must scroll only when the rail overflows. Today the wheel over the map zooms (`CameraRig._UnhandledInput`, wheel handler); the rail is 17 percent to 52 percent of the width. A `ScrollContainer` that always eats the wheel kills zoom (and the "Scroll out to see the whole book" hint) while the pointer is on the rail. And at the end of its range a ScrollContainer lets the wheel fall through, so the map zooms after a scroll. Rule: over the rail, wheel scrolls if `content > rail`, else it is passed to the camera; at the ends it is still consumed.

### M4. The choice of two loses what the player compares, and its keys collide with the shop (m3; sheet "Message"; mock-up notes "Choice")

- m3 prints "Choose [1]" and "Choose [2]". **1 and 2 are the shop's buy keys** (`tt_buy_1` Villager, `tt_buy_2` Pace). The code's keys are Z and X (`tt_choose_a/b`). Printing 1 and 2 is a changed key; if built, pressing 1 would either choose a boon or buy a villager. **Keep Z and X**, printed in the key cap.
- Today's half shows icon 44, name, text wrapped at 196, and **"+n coin a minute now"** (the worth, what lets you compare the two boons) and a tooltip (`Town.BoonText`). m3 shows name, one short line, button. The worth line and the icon are gone.
- Height (calc): padding 24 + border 4 + title (20) 26 + gap 8 + half: name 20, text 2 lines of 14 = 36, worth 18, button 32, gaps 12 = 118 + the half's own padding 16 = 134 -> total about **196**, not 168. At 1280 the card is 560 wide (m3: halves 270 each); the longest boon text wraps to 3 lines.
- It also sits at y 16 to 184 and enters the middle by 88 (mock-up notes 8). It is a decision that blocks nothing (the town runs on), so the cleanest fix is to give the choice its **own rule in the middle list**: allowed while open, and **drawn at 90 percent so the map shows through**, or lower the middle's top limit to 200 only while it is open. Do not cut it to 80.
- "Choice over the book": message-inventory C1 says the choice card **stays over the book** at Page and Book stage "never missed". The sheet says at the Book stage only navigation stays. Say which. Recommend: the choice, and only it, stays at every stage, keys Z/X working.

### M5. Numbers and names have no limit, so "no text is cut off" cannot hold (sheet sections 3 and 7, test 3)

- Purse: "Level 24 of 28" + "524,743 of 535,458 renown" is 287 of 292 (the notes). One more digit overflows, and the second age runs past that. Large coin number at 30 bold: about 17 px a digit, so 12 characters (a ten-digit amount) plus "+49,050 a minute" (caption, about 110) exceeds 292.
- Goal: "180 cloth at the landing within 170 s" is already about 285 of 296 (m5). A bigger count ("1,200 flour at the landing within 170 s") or a longer skipper name ("The skipper Old Tam asks") overflows.
- Banner: captions run to 85 characters (B2 "The charter: The town is granted its charter. Ring in the next age when you are ready."). One caption line at 532 wide holds about 76 characters (calc at 7 px). B2, B3 and the boon banner B4 (variable) overflow, and the banner is 72 high, which holds one title and one caption line only.
- Note: one line, 296 wide, holds about 42 characters. N13 "Milestone! <name>. A little extra on top." is about 45 and clips. N22/N23 (save failures, 90 to 100 characters, strong, "until clicked") cannot fit one line of 40.
- **Rule:** write one function `Fit` for the whole HUD: (1) numbers from 1,000,000 are written "2.4 million" (exact value in the tooltip); renown in the purse drops the word "renown" under 8 px of slack ("524,743 / 535,458") and shows the word only in the tooltip; (2) every Label has `ClipText` with `OverrunBehavior = Ellipsis`, a fixed `CustomMinimumSize`, and a test that **feeds the longest real string of each message** (the inventories list every one; the longest per kind goes into a fixture) at all canvases; (3) banner height 96 with a caption of up to two lines (title 26 + 2 x 18 + 28 = 90, calc), and the Message zone's top band grows to 112 for the banner, so the middle starts at y 112 (the choice rule above covers the rest); (4) the note zone is **two lines (56 high)** and a failed-save note moves to the Message zone as a banner kind, since it must stay until clicked and does not fit a note; (5) the goal text drops "at the landing" from the bold line (the caption says "Old Tam asks, at the landing").

### M6. "Ring in the next age" (key N) has no place (message-inventory C11; sheet silent; message-system puts it in the Goal zone)

- This is the one action that ends the first age. The sheet's four navigation buttons are Achievements, this age's page, centre, Pause. None is it. The Goal zone holds a request, and requests keep running after the charter.
- **Rule:** after the charter the **drive strip (x 16, y 232, 320 x 40)** is free (the drive is met). It becomes "Ring in the next age [N]" as a 32-high button inside the strip, accent fill (it is the 1 thing that asks). It stays until rung. In the second age it is the "The second age is complete / borough" strip's place. The sheet and the message system must say it in one line; today they disagree.

### M7. Rows that fold or appear shift under the pointer, and the sheet says nothing moves (sheet intro and section 3 "Folding"; message-system section 3 "rows slide")

- A purchase that finishes a track (Pace at its last step, Load) removes the row; every row below slides up 48. A new row (unlock at a level) opens a gap in the middle of a group; everything below slides down. A player who clicks the same row repeatedly (buying Pace again and again, 6 purchases a minute on average, message-inventory section 2) will find the **next row under the pointer** and buy it by the next click. That is a changed play (an unwanted purchase) and the "nothing moves" rule is false for the shop's content.
- **Rule:** (a) say plainly in the sheet that the zone's rectangle is fixed, the rows inside it can slide; (b) a row that moved in the last 0.3 s ignores clicks (its pill shows the disabled look for that time); (c) **while the pointer is over the shop, rows leave and enter but the rows below do not slide until the pointer leaves or 1.5 s pass**, so the row under the pointer is the row the player aimed at. (d) A finished row leaves with its heading's "done" count going up by one in the same 0.2 s.

### M8. The tooltip zone is too far from three of the four rails and cannot be a Godot tooltip (sheet section 2 "Tooltip" and section 6)

- The zone is at the shop rail. The sheet gives a tooltip to the purse lines, the goal, the bell and the tide: all on the **left**. The pointer is at x 100 and the tooltip opens at x W-640 (calc 1280 at 1920): the eye must cross the map. The bell tooltip would appear at top right, away from the bell.
- Godot's `TooltipText` places a popup at the pointer; it cannot be pinned to a zone. A zone tooltip means a **custom Control**, mouse enter/exit on every owner, and a replacement for every `TooltipText` in the game (achievement cells Book.cs:43/97, Home, boons, "+ hand" markers, tide, Pause: about 10 places). The delay is the project setting `gui/timers/tooltip_delay_sec` (global; the sheet's 0.25 s has to be a timer in the new control).
- **Rule:** the tooltip opens **beside the thing**: for a left-rail owner, to its right (x 344); for a right-rail owner, to its left (the sheet's rule); for the bottom row, above it. Same width 296, one `Tooltip` control, placed by one function `TipRect(ownerRect, canvas)` that is unit-tested for every owner at every canvas (inside the window, never over the owner). State that the tooltip needs the mouse: see M9.

### M9. Keyboard focus does not exist in play, and enabling it would break the bell (sheet sections 4 and 6; the brief's "keyboard-only play")

- Every shop row, the bell and the HUD buttons are built with `FocusMode = None` (`TownHud.Shop.cs:152`, `Banner.cs:105`, `Choice.cs:73`, `TownHud.cs:114`) on purpose. Tab is the Achievements key, WASD and the arrows pan the camera, +/- zoom, Space rings the bell. There is no key that moves focus, so the sheet's "river-blue focus ring" and "tooltip at once on keyboard focus" are never reached. If focus were turned on, a clicked row would keep focus and **Space (the bell) would also press that row** (`ui_accept`).
- **Rule:** drop the focus ring and "tooltip on focus" from the play HUD; keep `FocusMode = None` there and say so. Keyboard play is the printed keys (a key cap on every row and button). Focus stays in the pause menu, settings and pages, where it exists. Where a tooltip is the only place a fact lives (M10), a keyboard player cannot read it. That is today's state, but the sheet must not claim otherwise.

### M10. Facts that were visible and are now only in a tooltip (see also section 4)

- The pill drops "**Need n more**" (today's line, updating each second) and the bar; in the new row "saving up" shows only a growing fill. The number is in the tooltip. A player now needs a hover to know whether the wait is 3 s or 3 min.
- Locked rows fold into "2 more at level 7": the **names** of what comes (today "Name: level 9") are gone, and two items at different levels are lumped under one level ("2 more at level 7" in m1 counts items unlocking at different levels? the sheet does not say).
- Done items today read "Name, level 5: done"; now "3 done" with no names.
- The purse's "Next: <gift>" is cut to one line with an ellipsis (message-system section 3); today it wraps and tells the whole gift.
- The bell's "Ringing: 6 s left, **then rests 40 s**" becomes "Ringing, 7 s": the rest time is lost until the bell is resting.
- **Rule:** (a) the pill in the saving-up state shows the short amount, not the price, once the fill is past half: "needs 4" (the price stays on line 2 as `52 · +3.7 a minute`, see M2) or show both as `◎ 52` with a 12-high caption "4 short" under it in the 48 row (pill 28 + caption 14); (b) the fold text names the next one: `Roads ... 2 done · Pave a road at level 9`, then `+1 more` only when there are several; (c) the purse's Next line may be one line at body 16 with the ellipsis, and the full text in the purse tooltip **and** on the Page; (d) the bell reads "Ringing 7 s, then rests 40" in the tooltip and the bar's second colour for the rest time.

### M11. Invented keys in the mock-up, which the sheet will be built from (m1, m5)

- m5 prints river rows with keys **Q, W, E**. The river rows have **no key today** (`ActionFor` returns "" for them). W is `tt_move_up` and E is the second Extend key (`tt_buy_5`). Building it would add keys that clash with camera and Extend. Either print nothing on river rows (today) or choose keys not in `GameInput` and add them to the settings list (27 actions) and the rebind page. The sheet says "the same thing is always in the same place": a blank key slot keeps its width.
- m1 shows "Market stall [3]". Buildings use F, G, H in shown order (`tt_offer_1..3`), and 3 is Load. Fix the mock-up before the builder copies it.

---

## 2. SHOULD CHANGE

- **S1. The 8 px grid is false for real canvases.** H = 1350 (80 percent) or 768 (1366 x 768) or a window of 1000 / 1.5 = 666.7 is not a multiple of 8, so `H-64`, `H-96`, `W-336` land off the grid and half pixels blur 1 px borders. Say: the grid is a design unit; edges anchor with integer offsets (`Floor`), tests check the formulas not "% 8".
- **S2. Left rail dead space and hidden tags.** Goal (128 to 224), drive strip (232 to 272) and note (280 to 320) are always reserved: the block 320 x 304 is invisible most of the time (m1 early game) and, if tags hide under any zone, stake labels vanish under empty air. Rule: a tag is hidden only by a **visible** card. Say so in section 5.
- **S3. The shop card is opaque and full height even when two thirds empty** (notes 12; m1, m7): 17 percent of the width of a 1080 window and 52 percent of a 720 one is hidden map. Make the card height = content (top fixed, bottom edge moves), min 120. The hit area follows.
- **S4. Narrow rails for small canvases** (notes 13): under canvas width 1500 rails 280; under 1280 not reachable once M1 clamps. At 1280 the free middle is 592 (46 percent); 280 rails give 656.
- **S5. The bell and tide row (notes 3, 4) fix:** the sheet's rule (bell 156, tide 156) cannot hold "Ring the bell [Space]" (165) and "High water, falling" (162). Use bell 128 (bell icon, "Ring", key cap; while ringing "7 s" with the bar), tide 184. 128 + 8 + 184 = 320. The tide's text is `High water · falling` with an arrow glyph; "lowest in 1:14" on line 2. Keys only on the cap, full sentence in the tooltip.
- **S6. Purse 112, goal 104, note 56** (notes 1, 2, and M5). The left rail then runs: purse 16 to 128, goal 136 to 240, drive strip 248 to 288, note 296 to 352. At 720 high the bell at 656 is clear by 304.
- **S7. Page size and content.** m8 shows 12 achievements; the game has about 24 plus the river's 16. 24 entries in 2 columns x 12 rows x 56 = 672, over 560 (and over H-96 = 624 at 720). Today's page is 1120 wide with progress bars and descriptions in each cell; m8 dropped the bars and the description. Rule: Page `min(960, W-64)`, **three** columns of 8 rows x 48 (384 + title 48 + close 48 + padding 28 = 508, calc), the progress bar kept (a 4 px bar under the name), description in the tooltip (as today).
- **S8. Zoom stages are keyed wrong.** The sheet says rails fade "at the Page stage". The code fades the full interface by `_fullA` (1 up to 0.82 of Slim, 0 from 0.94 of Slim), i.e. before Page starts, and banners/notes made while hidden are lost (inventory section 1). The new message queue must hold banners while `_fullA < 0.5`. Say "keyed to `_fullA`, not to Stage" and that the one-line purse is visible while `_fullA < 1`.
- **S9. Count of "five things asking for attention" is undefined (test 5).** A running bar, a motion or an accent fill counts one each: at a choice level with a request running, a banner due, the bell ringing in the second age: choice (1), goal bar (2), bell bar (3), tide needle (4, always moving), affordable pills (5), note (6), line (7). Define: clocks (bars, needle) do not count; counted are the choice, the banner, the note, the line, the pulse, the accent pill group, the goal when under 25 percent time. Otherwise the test is red from day one or means nothing.
- **S10. Fonts and styles the sheet cannot hold.** (a) "Four sizes" fails for the pause lettering (96, 44, 34), the age page title (40 Uncial), settings (22 to 44) and the `Label3D` map labels; test 6 will fail on them. Add a fifth "display" size (Uncial 40 and the pause sizes) outside the HUD and exempt `Label3D`. (b) "Tabular figures" needs the OpenType `tnum` feature in Atkinson Hyperlegible; not checked. If missing, say "right-aligned, fixed-width cell" instead. (c) The theme's tooltip is 15; the sheet's caption is 14/15 (S1 of M1).
- **S11. Icons.** The sheet says PNGs `assets/icons/shop-<key>.png` at 48 x 48; the game loads SVGs (`IconOr` builds `res://assets/icons/{n}.svg`, `ModelLib.MipTexture`). Either state SVG, or write the PNG path and change `IconOr`. The LEDGER rule (a row for every asset) then applies to about 40 new icons.
- **S12. Pave's pill has no price.** Pave and Extend cost per road or cottage, not a fixed price ("Cobbles for this road: 24"). The sheet's four pill states fit Extend (m1 shows 125) but not Pave: say "From 21" (cheapest paveable road) or "Pick" when affordable.
- **S13. The tooltip overlaps the banner at 1280** (notes 10), with M8 this is solved by placing beside the owner. Add the rule "a tooltip is never drawn over a visible Message zone; it is placed the other side of the rail".
- **S14. Shop and navigation gap.** Shop ends at H-80, navigation starts at H-64: 16, not the 8 the sheet states. Use shop H-88.
- **S15. Scrim.** 30 percent ink scrim "over map and rails": say whether it blocks clicks. Today the rails keep working while a page is open and the town runs on. If the scrim blocks, buying while the achievements page is open is lost (see section 4).
- **S16. Rebuild on resize.** `Build()` frees and makes everything in one frame at a size change; with zones in one `Layout(W, H)` this becomes cheap, but the new rows (animated) and the tooltip (custom) need the same state restored. Say "layout is recomputed, nodes are kept".

### Buildability notes (Godot 4 Controls), not faults but traps

- **A row cannot be a theme Button with text.** Row = `Button` with empty text and children (key, icon, labels, pill) set `MouseFilter = Ignore`, so the whole row is the target and hover/pressed/disabled come from one StyleBox. The pill's growing gold fill is a child `ColorRect` clipped to the pill (a StyleBox cannot do it).
- **Sliding rows (0.2 s) cannot use `VBoxContainer`**: a container re-sorts at once. Own layout: a `Control` with child rects set by `Layout()` and a Tween on `Position`. Keep the Round-5 rule (no strings per frame): rebuild text only on a changed signature, animate only `Position` and `Modulate`.
- **Stable sizes:** a `Label` grows its container when its text changes. Every Label needs `CustomMinimumSize`, `ClipText = true`, `AutowrapMode = Off` (except message captions with a fixed line count), and `PanelContainer` padding set by StyleBox only. The test "no zone changes size" then reads `Size`, not `GetMinimumSize`.
- **Card variants.** Today there are 7 border widths and 8 margins (inventory 4.2). The theme must be one `UiKit.MakeTheme` with three card StyleBoxes and five button states; `UiKit.Tab` (the tucked bookmark with square left edge) disappears. The Home, close and page buttons (all `Tab`) must be rebuilt, not re-skinned.
- **"On" for Pave/Extend** reads `PaveMode`/`ExtendMode`; E and P stop the mode too, and the "On" pill replaces today's "Pick a road. P to stop" (the stop key must stay in the Line zone text).

---

## 3. In the inventories with NO place, or two places in the sheet

**No place in the sheet**

| Element | Note |
| --- | --- |
| "Ring in the next age" button [N], and "Read its page again" (C11, inventory 2.3) | M6: the drive strip after the charter. "Read its page again" is the nav button "this age's page" |
| Hint ring on the map (C6) | A 3D torus the hint line points at. Add to section 5; hidden when under a visible card |
| "+ hand" markers (L3) and "Suggested: n coin" tag (L5) | Named in the message system as tags; the sheet's section 5 lists only stakes and boats. Add both |
| Home [Home] "Back to the town" | Becomes the nav "centre the view" button. State it, and that it is always shown (today only when the view moved) so its tooltip says "Back to the town [Home]" |
| Second request (a skipper's and a town request together) | Message system: "the strip takes the second request in one line". The sheet's strip is only the drive. Define the strip's second use: `Old Tam: 100 of 180 cloth · 170 s` with a 4 px bar, 40 high |
| Save-failure notes N22, N23 (strong, until clicked, 90 to 100 characters) | M5: a banner kind in the Message zone |
| Page-turn leaf (P5) and the pause lettering / "New town?" question (S1) | Not zones. Say: full screen, above all zones, outside the layout tests |
| Age pages P2 to P4 (820 wide, Uncial 40 title, text 19 serif at 740) | Sheet says "Achievements, the age pages" in the Page zone 720 x 560, but the age text needs about 820 x 400 and a display size (S10) |
| Settings, "Keep this?" confirmation (S2) | Sheet claims the styles apply; the "Keep this?" card is a HUD-level popup over the book. Give it the Message zone or the Page zone, one line |
| Banner "Level n" when the gift lies under it (`BannerClearOf` sends it to the bottom today) | Dies with the fixed zone; confirm the gift's map spot can lie under the Message zone and the tag/coin number rules hide it |
| Slim strip when the view is zoomed (C8) | Section 2 "Zoomed out": it moves from bottom left to the purse place (top left). It no longer shares a corner with the bell; confirm |
| N17 to N20 (the answer to a click that cannot be done) | Line zone ("30 coin short"); the shop reason strings must fit the pill (S12) |
| Tide dial text "highest in 2:05" / rising | In the tide zone: the sheet shows "lowest in 1:14" only; rising tides read "highest in" |

**Two places**

| Element | Places |
| --- | --- |
| The key/next-age/page | The sheet's nav "this age's page" and the message system's "Ring in the next age" in Goal |
| A note (the same fact) | The sheet's Note zone, the Line zone ("30 coin short", mode text) and the Message zone (a banner that waited 12 s becomes a note) can all say one event; give each event one home in a table (the message system has it, the layout sheet cites none) |
| Price/need | The pill, line 2 and the tooltip (M10) |
| Level | Purse "Level 6 of 16", the page "Won in the first age", the "unlock at level N" lines (three phrasings: "Opens at level 7" in the tooltip, "2 more at level 7" in the fold line, "Unlocks at level 9" in the sim). Pick one phrase; the sim's string is the source |

---

## 4. Where the design changes how the game plays

1. **Choice keys 1 and 2** (m3) instead of Z and X: a changed key and a clash with Villager and Pace (M4).
2. **River keys Q, W, E** in m5: new keys; W moves the camera, E is Extend (M11).
3. **"Need n more"** gone from the row; knowing the wait needs a hover (an extra action) (M10).
4. **Names of locked and finished purchases** gone from the shop (M10). Before: see what comes next without leaving the shop.
5. **The choice's worth line** ("+n coin a minute now") and icon are missing in the mock-up: the comparison of the two boons is lost (M4).
6. **Scroll in the shop at 720** (and the scroll rail eating the wheel): rows out of sight, extra scrolling, zoom lost over the rail (M3).
7. **Clicking a row repeatedly** can buy the row that slides under the pointer (M7).
8. **Tooltips only by mouse** and far from the left rail: the same as today in kind, but the sheet promises a keyboard path that does not exist (M8, M9).
9. **The bell's rest time** hidden while ringing; the purse's "Next" text cut (M10).
10. **Scrim over the rails** while a page is open: if it blocks clicks, the player can no longer buy or ring while the achievements page is open (today allowed, the town runs on) (S15). Decision for the lead: keep the rails live (scrim `MouseFilter = Ignore` over them, still dims).
11. **Notes that stop being notes** (message system section 2): the "road has worn in" note becomes only the pill changing. The player who looks at the map and not the shop at that moment loses the hint to pave. Compensate: the pill's one pulse plus the Line zone text "A road has worn in" once (2.5 s).
12. **Home** key and button: the sheet always shows the centre button; the Home *key* must still work with the view not moved (no-op).
13. **The skipper's request** now shows (strip) when today it is invisible behind a town request. That is a gain; say it is a decided change.
14. **The bottom-left "slim strip"** moves to the top-left (a visible move when zoomed out) and the pause button no longer jumps; both fine, but the sheet says "Page stage" where the code uses `_fullA` (S8).

---

## 5. Keep

- One zone per job, fixed rectangles, a single `Layout(W, H)`; this kills the four writers of the left column (`ColumnFrame` and three others).
- The 56-high two-line row once M2 is applied (key, icon, name, pill) with the pill as the one state carrier and the whole row as the target.
- Folding of locked and finished rows, once the fold text names the next item and sits on the heading line.
- The tooltip's content order (name, effect, worth, price, why/when, key) and the `game-layout` test idea (every state, both ages, three scales); add the fixtures of M5.
- The message queue's lengths and priorities in `message-system.md`, with the Message zone's choice rule (M4) and the note's two lines (M5).

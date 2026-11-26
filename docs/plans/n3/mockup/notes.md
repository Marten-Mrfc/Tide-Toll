# Layout mock-up: notes (n3, against layout sheet version 1)

Files: `mockup.html` (one page; `?w=&h=&age=&s=&f=` picks canvas, age, state and flags such as `banner,hint,drive,coins,tags,ringing,choice,pave`), `m1.png` to `m9.png` (1920 x 1080 device px, canvas scaled by 1920 / W as the game does), `map-age1.png`, `map-age2.png` (tour pictures c05 and f08 with the old cards covered by patches of nearby grass; the right strip x 1590+ is tiled grass, so the river's east bank is gone in m5 and m6). The page logs every text box that overflows, every zone collision and anything in the middle (`window.__report()`); the numbers below come from that and from measuring the shipped fonts.

## What did not work when drawn

Canvas px throughout. Atkinson Hyperlegible for the sans, Alegreya for titles.

1. **Purse does not hold its four lines.** Card 104 high, minus border 2 x 2 and padding 12 x 2, leaves 76. The lines need 84: large number 34 (30 px type), body 20, bar 8 with 2 above and below, caption 18. **8 px short.** Fix: purse 112 high (moves goal to y 136, drive 240, note 288), or padding 8 inside the purse.
2. **Goal card is 4 px short.** Inner 68 (96 - 4 - 24); caption 18 + body 20 + bar line 16 (bar with "27 of 117" beside it) + caption 18 = 72. Fix: goal 104 high, or padding 10.
3. **The bell does not fit "Ring the bell [Space]".** Text and key cap measure 145; with padding 8 x 2 and border 2 x 2 the button needs 165, the zone is 156. **9 px short**; the key cap overlaps the text in every picture. Widening does not help: bell 165 + gap 8 + tide 162 (item 4) = 335, more than the 320 rail. Fix: put the key only in the tooltip (bell text then needs 105 + 20 = 125), or "Ring [Space]".
4. **The tide does not fit "High water, falling".** Dial 24 + gap 8 + text 110 + padding and border 20 = 162 > 156. **6 px short**; "falling" is cut in m5 and m6.
5. **Drive strip does not fit its name, ten pips and "4 of 10" on one line.** Needs 304 inside; inside is 292 (320 - 4 - 24). "The charter drive" is cut to "The charter dri" in m2. Fix: pips 8 px, or the count only ("4 of 10") without the word "drive".
6. **Shop row names are cut in the second age.** The name column is 132 wide (296 inner - key 20 - icon 24 - pill 96 - three 4 gaps - row padding 8), 124 when the scroll thumb shows. "Water price level 14" needs 139, "Grain price level 15" 134, "Flour price level 36" 131, "+1,424 coin a minute" and "+1,472 coin a minute" 136. At 100 percent two names and two income lines are cut; at 1280 x 720 three names and two income lines. Fix: pill 88 wide, or level moved to the second line ("level 14 · +359 coin a minute") which is how I would do it.
7. **Purse second line at the second age is 5 px from touching:** "Level 24 of 28" + "524,743 of 535,458 renown" measure 287 of the 292 inside. It fits, but with no air; one more digit of renown will not fit.
8. **The choice of two (168 high) enters the middle.** Message zone y 16 to 184, the middle starts at y 96. The sheet's section 2 list of what may enter the middle does not name the message zone. Either allow the choice there (it is shown on purpose and for a short time) or cap the choice at 80 high, which would not hold a title and two options.
9. **1280 x 720 (m6): the shop does not fit even at 40 px rows.** Rail 624 high; inside it header 32 and scroll area 564. Second age late: 5 group headings, 11 rows, 3 fold lines = 664 at 48 px rows, **584 at 40 px rows, 20 px over**. The rail scrolls; the 4 px thumb shows; the last row ("A bigger raft") is half hidden. Nothing collides there: purse to note end at y 320, timers start at y 656; message zone x 360 to 920 (width min(560, 576) = 560) sits between rails ending at 336 and starting at 944, 24 px clear each side; line at y 664 to 704 does not touch the timers (they end x 336).
10. **The tooltip zone overlaps the message zone at 1280 x 720:** tooltip x 640 to 936, message x 360 to 920. A tooltip shown while a banner is up covers part of the banner. The sheet calls the tooltip an exception, so this is allowed, but worth knowing.
11. **Coin numbers at body size (16, bold) are small** against the map at 1920 x 1080 (m2): they read as specks next to buildings. 20 would read.
13. At 1280 x 720 the two rails take 2 x 336 = 672 of 1280 canvas px (52 percent of the width); the free middle is 592 x 560 (x 344 to 936, y 96 to 656). In m6 the river and the landing sit under the shop rail. The layout holds, but the town is squeezed; consider a narrower rail (272) under 1600 canvas width.
12. In the first age at rest (m1, m7) the rail is two thirds empty below the shop; at 2400 x 1350 more so. Not a fault, but the shop card could end at its content and leave the map visible (I kept the full height the sheet gives).

## Choices where the sheet was silent

- **Colours:** Palette has no "wheat" or "moss". Wheat = `Palette.Thatch` #C9A55A; moss = `Palette.CastIronGreen` #44604F; river blue = `Palette.River` #5B7F95; brick = `Palette.Brick`; ink #2B2620; paper #F2EAD8; hover = `UiKit.PaperHover` #F8F2E4. The sheet should name these. The focus ring sheet says river blue; UiKit's Focus is #56B4E9 (not shown in any picture).
- **Saving-up fill:** wheat at 55 percent over paper, the price text muted on top.
- **"Cannot now" pill:** paper fill, 1 px muted border, muted text, reason instead of price.
- **Row look:** rows have no fill of their own inside the rail card; hover is the lighter fill and a 3 px ink edge (the button hover style).
- **Group headings:** body size in the serif, a 1 px muted rule under them, "2 done" caption on the right. Fold lines are indented to the name column.
- **Shop header "3 to buy now":** caption, not muted.
- **Bell at rest** shows "Ring the bell [Space]" with no bar; ringing shows the river-blue bar running down (m2); resting would show the same bar, disabled look.
- **Navigation:** Achievements 1 wide button (icon + "11 of 24"), then three 48 x 48 icon buttons, 8 apart, right-aligned. Icons are stand-ins (no `assets/icons/shop-*.png` exist yet): a wheat or paper rounded square with an ink chevron.
- **Coin sign:** a small drawn coin (wheat disc, ink edge) before every amount, also inside pills.
- **Banner text:** title (serif 20) plus one caption line; accent fill.
- **Choice:** title plus two side-by-side option boxes, each with a 32 high "Choose [1]" button.
- **Tooltip:** padding 8 x 12, caption size except the name line (body, bold); top level with the hovered row; x = W - 640.
- **Pointer's price tip** (m3): tooltip style, one line "Pave this road: 21", placed 24 px right of and 40 px above the pointer.
- **Tags:** paper pill at 85 percent, caption, padding 1 x 8, fully round ends.
- **Page (m8):** 720 x 560, title left, count right, two columns of 48 high entries, Close button bottom right. Scrim over everything else including the rails.
- **Page zoom (m9):** the purse as a 320 x 40 quiet card: coin left, level right. The map is not drawn zoomed out (same picture as m1).
- **Where the sheet asked for "two saving up with different fills"** I show four (92, 45, 38 and 31 percent) to reach a realistic first-age shop of ten rows plus three fold lines; three affordable, two "No room", one "No worn road".
- **Second-age purse "Next"** line: "Next: the footbridge" (from f08).

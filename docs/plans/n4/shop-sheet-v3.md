# The right side, version 3 (the lead, 27 November, after the owner played `e02da4e`)

The owner's words: "Now fix the right ui. This still looks bugged and awful." This sheet replaces, in `docs/plans/n3/layout-sheet.md`, the paragraph **Shop** and the line **Navigation** of section 3, the pill and key cap lines of section 4 as far as they concern the shop, and what section 6 says about a shop row's tooltip. Everything else in that sheet stands: the zones and their rectangles, the four type sizes, the colours, the attention budget, the rules for rows that come and go.

The target is drawn: `mockup/shop-before-and-planned.png` (three game states, now beside planned), `mockup/shop-tooltip-and-hover.png`, `mockup/zero.png`. Where a picture and this page differ, this page is right.

## What is wrong now (seen in the final build's pictures, `build/n3/final/`)

1. **The zeros are slashed.** "10,697" reads as "1θ,697" in every price, in the purse and in the key cap "0". (Its own job: `font-brief.md`.)
2. **The "not yet" pill has a wheat line that sticks out** under its rounded corner; the "blocked" pill has a dashed rectangle drawn over a rounded pill, so its corners poke out like brackets.
3. **The key caps run 1, 5, 2, 3, 6, 7, 8, 4, F, 0 down the left edge**, like a numbered list out of order; rows without a key leave a hole in that column.
4. **Two icons touch the key cap** (Pace's speed lines, the bell's rings).
5. **One name is smaller than the others** ("Better boat prices", and "Extend a cottage" in the small window), because a name that does not fit is shrunk row by row.
6. **Rows differ in height of content**: some have a second line and some have none (Extend a cottage and Regulars when affordable), which leaves gaps.
7. **Headings stand with nothing under them** ("Town … Louder bell at level 23" directly above "The river"), and every heading carries grey notes on its right ("Basket at level 3 · +1 more", "1 done", "8 done").
8. **Compact rows look like another interface**: bold names, no icons.
9. **The navigation buttons hold a key cap box that fills their lower half** and collides with the button's own border; "Home" touches both sides of its box.
10. **The tooltip is one run-on sentence**: "Price: 4. 4 short. Key: 1."
11. Prices start at the left of the pill, so they do not line up as numbers.

## The shop

**The sheet.** One card as now (zone, width R and margins unchanged), as tall as its content, at most H-104.

- **Header** (32): "Shop" (title, serif); on the right "3 to buy" (caption, muted) while anything is affordable, with the small down mark when one of them is scrolled out of sight (as now).
- **Groups, for display** (the simulation does not change): the rows are grouped and ordered so that **the keys read upward down the list**: 1 2 3, 4 5 (F G H), 6 7 8 9, 0.

| Group | Rows, in this order |
| --- | --- |
| People | Villager (1), Pace (2), Load (3: Basket, Yoke, Handcart) |
| Building | Pave a road (4), Extend a cottage (5), the building offers (F, G, H) |
| Work | Tools (6), the prices of goods (7, 8, 9) |
| Town | Louder bell (0), then every other first-age row that has no key (Regulars and the like) |
| The river | every second-age row, in the simulation's order |

  A row of a kind not named here goes to Town (first age) or The river (second age). Within a group: rows with a key in key order, then rows without.
- **Group heading** (28): the name (body, serif) and a hairline (ink at 16 percent) from the name to the right edge. Nothing else on it. A group without rows has no heading.
- **Foot** (one line, caption, muted, under a hairline): only while something is still locked. "Later: Basket at level 3" names the locked purchase with the lowest level (the first in list order if several, then "+1 more"); when its level is not known, "Later: Extend a cottage" (on both rails). It says "Later:", not "Next:": the ledger's "Next:" line says what the next level gives. While rows are scrolled out of sight below, the foot's place says "2 more below" with the small down mark instead. The foot fades in only once the card's edge has arrived and fades out before its room is given back, so it never lies over a row. Its tooltip lists every locked purchase with its level, and the finished ones. This one line replaces all the notes on the headings; "done" counts are not shown on the sheet.

**A row** (48 tall with 4 between, a pitch of 52, always two lines; the whole row is the click target):

```
[icon]  Name  9                          (  o    10,697 )
 [key]  +874 a minute
```

- **Icon** (24) at the left. **Under it the key cap** (small, muted, boxed; only a key that `GameInput` binds to that row; a row without a key has nothing there). No key column beside the icon.
- **Name** (body, regular weight) and after it the step number (caption, muted; dropped whole if it does not fit, as now).
  - A name is **never set smaller than the other names**. On the 320 rail every name of the whole catalogue fits its box at body size (the box is wider now that the key column is gone); a test proves it with the fixture's longest names. On the 272 rail (interface size 150 percent) either every name is body or every name is caption, decided once from the whole catalogue, not from the rows in view. A name never ends in an ellipsis.
- **Line 2** (caption) is never empty and says one thing:
  - what one more is worth: "+874 a minute";
  - if the purchase has no rate, what it does in a few words ("Room for two more");
  - if it is blocked, why, in a few words ("No room in the cottages", "No worn road yet");
  - if its click mode is on, what to click ("Click a lit cottage", "Click a lit road").
  - "30 short" is no longer on the row: the pill shows how far the coin has come, and the tooltip has the number. Every line-2 text fits the row at caption size on both rails (test with the fixture); texts that do not are rewritten shorter, never cut.
- **The pill** (96 x 32; 88 wide on the 272 rail), right-aligned on line 1. The coin sign sits at its left; **the price is right-aligned with tabular digits**, so prices line up as a column. Four states and nothing else:

| State | Fill | Border | Text |
| --- | --- | --- | --- |
| Affordable | solid wheat | none | ink, bold |
| Not yet | paper, with a **wheat wash (wheat at 30 percent, its leading edge fading out over 8) filling the pill from the left** as far as coin / price | muted, 1 | muted number, regular |
| Blocked | paper | ink at 30 percent, 1 | disabled ink; the coin sign dimmed; with no price to show (Pave without a worn road) a centred en dash |
| On | ink | none | "On", paper, bold, centred |

  - The wash is clipped to the pill's rounded outline: **nothing of a pill is ever drawn outside its outline**. No line under a pill, no dashed frame (`DashFrame` goes).
  - Pave reads "from 12", centred, without the coin sign (as now).
  - A price is never set smaller in one row than in another. The largest price of a finished second age (fixture) fits at body size, bold, with at least 4 between the coin sign and the digits; if it would not, widen the pill (up to 104) or shorten large numbers sooner ("123k" from 100,000), the same way in every row.
  - A blocked row shows its icon and name in the disabled look as well.
- **States of the row:** at rest no fill. **Hover:** a band of ink at 9 percent with corner 4 across the whole row. **Pressed:** ink at 18 percent, content 1 down, the pill one step darker. **Blocked:** no band (the tooltip still shows). The one pulse when a row becomes affordable is a short lightening of the pill's fill (0.4 s), never a change of size.
- **Compact rows** (40 tall, used when the tall rows do not fit; tall again only when they would fit with two rows to spare, so one row near the limit does not flip the list; the switch eases over 0.2 s like every other change: heights and places slide, line 2 and the key caps fade): the same row in one line: icon (20), name in the same size and weight as in tall rows, pill (28 tall). No second line, no key cap. It must look like the same interface.
- **When rows or headings come and go** the rules of sheet v2 stand (0.2 s, the pointer holds the slide, a row that just moved takes no click), and the sheet's bottom edge and the foot line move with the same ease: no frame shows a gap, an overlap or a jump.

**A shop row's tooltip** is a small card (296 wide), not a sentence:

```
Tools 2                                   [6]
Better tools: every villager works a little
faster at the well, the field and the mill.
---------------------------------------------
Worth   +1.3 a minute, pays back in 44 s
Price   o 57
Short   o 5, about 4 s
```

- Title: name and step (body, bold); the key cap or caps at its right.
- One sentence of what it does (caption, ink).
- A hairline, then facts in two columns (label muted, value ink): **Worth**; **Price**; **Short** (only when not affordable: how much and about how long); **Why** (only when blocked or locked: the reason, "Opens at level 7").

## Navigation (bottom right)

Four buttons, same places and sizes as now (Achievements 112 wide, then three 48 x 48, 48 tall, 8 apart). Each shows **its icon centred (26) and nothing else**; Achievements shows its icon and "2 of 16" (body, bold). **No key cap inside a navigation button.** The tooltip names the button and its key ("Achievements · Tab", "The age's page · N", "Centre the view · Home", "Pause · Esc"). Disabled look: border 1 at 40 percent ink, icon at 40 percent.

## Tests (`game-layout` and the picture tour)

The checks of sheet v2 section 8 stay. Changed or added for this sheet (say in the progress file which old assertion each replaces, and why):

1. Down the visible list the number keys ascend (1 to 9, then 0), with F, G, H between 5 and 6.
2. Every tall row has a second line that is not empty; every row's name has the same font and size as every other row's name in that layout; no name ends in an ellipsis; the fixture's longest names and largest prices fit.
3. No group heading without a row under it; headings carry no notes; at most one foot line.
4. Every part of a pill lies inside the pill's rectangle and the pill clips to its outline; no dashed frame exists; the four states are the only ones.
5. Compact rows use the same name font as tall rows and show their icon.
6. A navigation button holds no key cap; its icon is centred within 1.
7. A shop row's tooltip has a title, a sentence and its fact lines; it holds no text "Key:".

The picture tour (`tools/ui-audit.sh`) gets three more states: the pointer resting on an affordable row (hover band and tooltip), the mouse button held on that row (pressed), and the pointer on a blocked row.

# The layout sheet (version 2, the lead, 20:20 UTC, after the mock-up and two critiques)

One layout for the whole game. Every thing on screen has one zone, every zone has one job, and a zone never moves. The owner's rules: the middle of the map stays free; nothing moves when something else appears; at most a handful of things ask for attention at once; finished and locked items fold away; the same thing is always in the same place in both ages. And: **how the game plays does not change**: a player can still see and compare everything they could before, with the same clicks and the same keys.

Version 1 was drawn (`mockup/m1.png` to `m9.png`, `mockup/notes.md`) and criticised (`layout-critique-1.md`, `-2.md`). What changed is listed at the end. The pictures show version 1: where they differ from this page, this page is right.

## 1. Units, canvases, grid

- **One scale for everything.** `s = UiKit.ScaleFor(window height)` scales boxes and type alike (no separate floor for fonts: that is how text came to be cut). All numbers on this page are **sheet units**; one unit is `s` canvas pixels. The **sheet canvas** is the visible rectangle divided by `s`.

| Window | Interface size | Sheet canvas (W x H) | Caption in real pixels |
| --- | --- | --- | --- |
| 1920 x 1080 | 80 / 100 / 150 | 2400 x 1350 / 1920 x 1080 / 1280 x 720 | 12 / 15 / 22 |
| 1280 x 720 (s 0.78) | 80 / 100 / 150 | 2051 x 1154 / 1641 x 923 / 1094 x 615 | 9 / 12 / 18 |

- The layout holds on every canvas from **1094 x 615** to 2400 x 1350, and on 1366 x 768 and 2560 x 1440 windows.
- **Grid:** 8 (4 inside a row). It is a design unit: edges are anchored with whole-pixel offsets from the window's edges. Margin to the window edge 16, gap between zones 8.
- **Rail width R:** 320 when W is 1600 or more, else 272.
- Order of importance: 1 the map, 2 the purse, 3 what can be bought now, 4 the goal, 5 the one large message, 6 the bell, 7 the note, 8 navigation.

## 2. The zones

| Zone | Job | Rectangle | Shows |
| --- | --- | --- | --- |
| **Ledger** (left) | One paper sheet with four ruled sections, always the same size: **purse** (112 high: money, income, level, progress, what the next level gives), **goal** (104: the one current request), **strip** (40: one line for a second goal, see section 3), **note** (56: small news, two lines, one at a time) | x 16, y 16, R wide, 336 high (sections divided by a 1 px muted rule and 8 of air) | Always. An empty section is blank paper with faint ruled lines, like an account book. Nothing else may use it |
| **Timers** (bottom left) | Things on a clock: the bell (a button), the tide (second age) | bell x 16, y H-64, 128 x 48; tide x 152, y H-64, R-136 wide | Bell always; the tide joins in the second age and moves nothing |
| **Shop** (right) | Everything that can be bought, and the two click modes | x W-16-R, y 16, R wide, **as tall as its rows**, at most H-104 | Always |
| **Navigation** (bottom right) | Achievements, this age's page, centre the view, pause | right-aligned at x W-16, y H-64, 48 high: Achievements 112 wide, then three 48 x 48 | Always, both ages, same order |
| **Message** (top centre) | The one large message at a time | centred, y 16, width min(560, W-2R-80); a banner is 96 high; the choice of two is up to 200 high | Only while a message shows |
| **Line** (bottom centre) | One line of guidance | centred, y H-56, width min(560, W-2R-80), 40 high | Only while there is something to say |
| **Tooltip** | What the thing under the pointer does and is worth | **beside its owner**: left of a shop row, right of the ledger and timers, above a navigation button; 296 wide; one function `TipRect(owner, canvas)` keeps it inside the window and off its owner and off a showing message | Only on hover |
| **Page** | Achievements, the age pages | centred between the rails when that leaves 640 or more, else centred on the window; min(960, W-64) x min(600, H-96) | Only while open |

- **The middle** is x 16+R+8 to W-16-R-8, y 120 to H-64. At rest nothing of the interface is in it. Only these may enter: the choice of two while it is open (down to y 216), a tooltip while hovering, a page the player opened, the pointer's price tip in a click mode, and the map's own tags, coin numbers and hint ring (section 5).
- A page dims the map only. The rails stay lit and usable wherever the page does not cover them; the town runs on; the buy keys work.
- **Zoomed out** (keyed to the interface's present fade, not to the stage): shop, timers, line and the ledger's goal, strip and note fade out; the purse stays as one line (coin, level) in its place; navigation stays. With the book in view only navigation stays. **The choice of two stays at every zoom** (as today). Banners raised while zoomed out wait until the interface is back.
- With the book closed (pause) no zone shows. The pause cover's question, the settings' "Keep this?" and "Delete the save?" use one **modal card** (480 wide, centred, card style).
- The page-turn leaf at the age change lies over everything; the interface under it is not rebuilt, only its content changes while covered.

## 3. What is in each zone

**Purse:** line 1 the coin sign and the amount (large number); line 2 the income "+296 a minute" (body, ink: it is the second most important number); line 3 "Level 6 of 16" (body) and, right-aligned, "1,312 of 2,200" (caption, muted; dropped, not cut, if it does not fit; always in the tooltip) over the renown bar; line 4 "Next: a cottage and its lane" (caption, one line with an ellipsis; the whole text in the tooltip and on the age page).

**Goal:** who asks (caption); what and by when (body, bold); the bar with "27 of 117" (river); the reward (caption). Met: moss fill and "Done: +160" for 1.6 s. Missed: "Too late" in brick for 1.6 s.

**Strip** (one line; in this order of priority): after the charter, the button **"Ring in the next age [N]"** (accent fill; it stays until rung); the charter drive ("Charter", ten small pips, "4 of 10"); a second request running at the same time as the one in the goal section (a skipper's while the town's runs, or the reverse), in one line with its count and time.

**Note:** two lines at most, 90 characters at most (longer texts are rewritten, never cut).

**Shop:** a heading line ("Shop", title; on the right "3 to buy now" in caption when anything is affordable, with a small down mark if one of them is scrolled out of sight). Then the simulation's groups in their order (People, Work, Roads, Town, and in the second age The river at the end). A new row enters at the end of its group.

- **Group heading** (28 high): the name (body, serif); right-aligned in caption what is folded away: "1 done · Yoke at level 7" (the next locked one by name, then "+1 more"). A locked purchase is not a row. A finished purchase leaves and the count of done goes up; the heading's tooltip lists the finished ones.
- **Row** (56 high, two lines; the whole row is the click target):

```
[key] [icon]  Name  9                          [ price pill ]
              +3.7 a minute · 30 short
```

  - Line 1: the key cap (20 wide; **only the key bound in `GameInput`**, a row without a key keeps the empty column), the icon (24), the name (body), the step number in muted caption (dropped if it does not fit), the pill.
  - Line 2 (caption, full width under the name): what one more is worth, then the state: nothing when affordable; "30 short" when saving up; the reason when blocked ("No room", "No worn road").
  - **The pill** (96 x 32; 88 wide when R is 272) always shows the price with the coin sign, in three states: **affordable** (solid wheat, ink bold); **not yet** (paper, muted text, a 3 px wheat line under it growing from the left as coin comes in); **blocked** (paper, muted text, a dashed border). Pave shows the cheapest road's price with "from"; a click mode that is on shows "On" (ink fill, paper text). From a million up a pill writes "1.24M" (the tooltip has the whole number).
- **Fitting:** rows are 56 when everything fits the rail; otherwise all rows are 40 with one line (key, name in caption bold, pill; no icon; line 2 only in the tooltip); otherwise the rail scrolls. No state of the audit may need the scroll at 1920 x 1080 or at 1280 x 720 with 100 percent.
- **The wheel:** over the shop it scrolls the shop only if the rows overflow (and is swallowed at the ends); otherwise it zooms the map as everywhere else.
- **Rows that come and go:** a row fades in or out over 0.2 s and the rows below slide to their place over 0.2 s. While the pointer is over the shop the rows below do not slide until the pointer leaves or 1.5 s pass, and a row that moved in the last 0.3 s takes no click: the row under the pointer is always the row the player aimed at.
- A row that has just become affordable pulses once (0.4 s), one row at a time, at most two a second.

**Timers:** the bell: icon, "Ring", the key cap [Space]; while ringing "7 s" over a bar running down; while resting "rests 14 s" over a bar filling, disabled look. Its tooltip gives the whole sentence ("Ringing 7 s, then rests 40 s"). The tide: the dial, "High, falling", "low in 1:14".

**Navigation:** Achievements (its icon and "11 of 24"), this age's page, centre the view (disabled look when the view is already home), pause. Each shows its key in a small cap and has a tooltip with its name.

**Message:** a banner has a title (title size) and up to two caption lines. The choice of two keeps everything it has today: per option the icon, the name, what it does, "+n coin a minute now", and its key **Z or X**. Order when both are due: the banner first, then the choice opens in the same place and grows downward. While a choice is open nothing else may enter its rectangle.

**Line:** in this order of priority: the answer to a click that could not be done (2.5 s); what the click mode in use expects ("Pave: click a worn road. Hold Shift to pave several. P or Esc to stop."); a first-minute hint; the town's hint.

## 4. One set of styles (`UiKit`), used everywhere

| Thing | Style |
| --- | --- |
| Type | Two families, the ones shipped: the serif for titles and group headings, the sans for everything else. Four sizes in play: **caption 15, body 17, title 21, large number 30** (bold). Outside play a fifth, display, for the pause lettering and page titles. Labels on the map (3D) are outside this rule. Numbers are right-aligned in fixed-width cells; coin amounts always carry the coin sign and never the word "coin" |
| Colours | Named in `UiKit`, taken from `Palette`: ink, paper, **wheat** = Thatch (money, affordable, accent), **river** = River (time, requests), **moss** = Cast-iron green (done), **brick** = Brick (a warning). Muted text is ink at 60 percent; muted text that carries a number is ink at 70 percent |
| Card | Paper at 96 percent, ink border 2, corner 4, padding 12. Variants: **quiet** (no border, 88 percent: the line, tags) and **accent** (wheat fill: banners; brick border for trouble). The ledger and the shop are one card each |
| Button | Height 48 (bell, navigation), 56 or 40 (rows), 32 (inside cards and pages). **Normal** paper, ink border 2. **Hover** lighter fill, border 3. **Pressed** darker fill, content 1 down. **Disabled** 45 percent ink, border 1. **On** ink fill, paper text. In play nothing takes keyboard focus (as today: Space is the bell); on the pause cover, in settings and on pages the focus ring is river, 2 outside |
| Bar | 8 high, corner 4, ink border 1. Wheat for renown, river for time and requests, moss for done |
| Key cap | Caption in a small box, muted, always at the same place in its row or button |
| Tag (on the map) | Caption, quiet card as a pill |
| Coin number (on the map) | 20, bold, wheat with an ink edge |
| Icons | 24 on screen, ink line with one flat colour, loaded with `TownHud.IconOr(name, stand-in)` from `assets/icons/shop-<key>.svg`, `ach-<key>.svg`, `msg-<kind>.svg`, `nav-<name>.svg` (the asset track draws them; until then the present stand-ins show) |

Text that does not fit its box is never cut in the middle of a word without a sign: names end in an ellipsis, optional captions are dropped whole, and the tooltip always has the whole text. Every label has a fixed box, so no control changes size because its text did.

## 5. Things on the map itself

- **Coin numbers:** at the building of the delivery, rising 24 over 1.4 s, fading in over 0.1 s and out over the last 0.5 s. Deliveries at the same building within half a second show as one number. At most eight at once. The setting that turns them off stays.
- **Tags:** a staked plot's name, a boat's cargo ("Wool 3 of 6"), "+ hand", "Suggested: 24". One style. A tag that would touch a **visible** card or another tag fades out and stays out for at least half a second.
- **The hint ring** around the thing the line's hint is about: wheat, thin, shown only while that hint shows, fading in and out.
- **The pointer's tip** in a click mode: tooltip style, beside the pointer, clear of the target.

## 6. Tooltips

One control for the whole game (not the engine's own), shown after 0.25 s of hover, fading in 0.12 s. Every shop row and group heading, the bell, the tide, each navigation button, each section of the ledger, each achievement and each option of a choice has one. A shop row's says, in this order: the name and step; what it does in one sentence; what one more is worth ("+3.7 coin a minute; pays for itself in 14 s"); the price; if it cannot be bought, why and when ("Opens when the town reaches level 7", "30 coin short, about 8 s"); its keys (both, where there are two).

## 7. Calm: the attention budget

- **Loud** (the group of solid wheat pills; an accent banner or the choice, never both; an "On" pill; a pulse): at most three at once.
- **Quiet** (a note, the line, the strip, a goal with under a quarter of its time left): at most four at once.
- Clocks (bars, the tide's needle), tags and coin numbers are not counted: they are the map and its clocks.

## 8. Tests (a new check step, `game-layout`)

For every state of `tools/ui-audit.sh`, in both ages, on the six canvases of section 1 and on 1366 x 768 and 2560 x 1440 windows at 100 percent:

1. Every zone's rectangle is the one this sheet gives for that canvas, in every state.
2. No two zones overlap (the tooltip, the choice and a page as stated).
3. Nothing is cut off: every control lies inside the window and inside its zone; no text is wider or taller than its box. The check is also run with **the longest real text of every kind** (longest name, largest numbers of a finished second age, longest banner, note, request and hint), kept in a fixture.
4. The middle is free except for what section 2 allows.
5. The attention budget of section 7 holds; the report lists what was counted.
6. Only the styles of section 4 are on screen: every font size one of the four, every card one of the three variants.
7. Locked purchases are not rows; finished ones are gone; no key cap shows a key that `GameInput` does not bind to that action.
8. `TipRect` for every owner on every canvas: inside the window, not over its owner.

## 9. What changed from version 1 (and why)

| Change | From |
| --- | --- |
| One scale for boxes and type, a table of real canvases down to 1094 x 615, caption 15 | both critics: the canvas rule was wrong and the smallest text too small |
| The left side is one ledger sheet with fixed sections instead of four cards and holes | critic 1 (four equal boxes; reserved but empty space) |
| The shop is as tall as its rows; fold lines moved into the group headings and name what comes next; rows 56 or 40 by fit; narrower rails on narrow canvases | both critics, mock-up notes 6, 9, 12, 13 |
| The pill always shows the price; "30 short" and the reason are on line 2 | both critics: a blocked price and "need n more" were hidden |
| "Ring in the next age" has a lasting place (the strip) | both critics |
| Keys only from `GameInput` (Z and X for the choice; none for river rows); no keyboard focus in play | both critics: the mock-up invented keys |
| Tooltips open beside their owner | both critics |
| The note is two lines; a failed save is a banner that stays until clicked | both critics |
| Banner 96 high with two caption lines; the choice keeps its worth line and icons, may enter the middle, stays at every zoom | both critics |
| Rows do not slide under the pointer | critic 2 |
| The attention budget is in tiers and lists what it counts | both critics |
| Purse 112, goal 104, bell 128 with "Ring [Space]", tide shorter text, coin numbers 20 and 1.4 s | mock-up notes 1 to 5, 11; critic 1 |

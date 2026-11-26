# The layout sheet (version 1, the lead, 20:20 UTC)

One layout for the whole game. Every thing on screen has one zone, every zone has one job, and a zone never moves. The owner's rules: the middle of the map stays free; nothing moves when something else appears; at most a handful of things ask for attention at once; finished and locked items fold away; the same thing is always in the same place in both ages.

## 1. The canvas and the grid

- Everything is laid out on the **canvas**: the window divided by the interface scale (`UiKit.ScaleFor`). At 1920 x 1080 and 100 percent the canvas is 1920 x 1080; at 150 percent (and at 1280 x 720 with 150 percent) it is 1280 x 720; at 80 percent it is 2400 x 1350. The layout must hold on every canvas from 1280 x 720 to 2400 x 1350.
- **Grid:** 8 px. Every rectangle's position and size is a multiple of 8 (4 inside a row).
- **Margin** to the window edge: 16. **Gap** between zones: 8. **Rail width:** 320 (both rails).
- Order of importance (what the eye should find first): 1 the map, 2 the purse, 3 what can be bought now, 4 the goal, 5 the one large message, 6 the bell, 7 the note, 8 navigation.

## 2. The zones (W and H are the canvas size)

| Zone | Job (one each) | Rectangle | Always there? |
| --- | --- | --- | --- |
| **Purse** | Money, income, level, progress to the next level, what the next level gives | x 16, y 16, 320 x 104 | Yes, both ages |
| **Goal** | The one current goal: a request (townsfolk or skipper), with its bar, time and reward. Under it one strip for the charter or seal drive | x 16, y 128, 320 x 96; drive strip x 16, y 232, 320 x 40 | The space is always kept; the card shows only while there is a goal. Nothing else may use the space |
| **Note** | Small news, one line, one at a time: an achievement, a milestone, a small touch, a save that failed | x 16, y 280, 320 x 40 | The space is always kept; one note at a time, the rest wait their turn |
| **Timers** | Things on a clock that the player acts on or waits for: the bell (left), the tide (right of it, second age only) | bell x 16, y H-64, 156 x 48; tide x 180, y H-64, 156 x 48 | Bell always; the tide joins in the second age without moving the bell |
| **Shop** | Everything that can be bought, and the two click modes (Pave, Extend) | x W-336, y 16, 320 x (H-96) | Yes, both ages |
| **Navigation** | Achievements, this age's page, centre the view, pause | x W-336, y H-64, 320 x 48 | Yes, both ages, same four buttons in the same order |
| **Message** | The one large message at a time: a level banner, the choice of two, the ring-in-the-next-age prompt | centred, y 16, width min(560, W-704), height 72 (banner) or 168 (choice) | Only while a message shows |
| **Line** | One line of guidance: what the mode in use expects ("Pave: click a worn road. Esc to stop."), otherwise the current hint | centred, y H-56, width min(560, W-704), height 40 | Only while there is something to say |
| **Tooltip** | What the thing under the pointer (or in keyboard focus) does and is worth | left of the shop rail: x W-336-8-296, 296 wide, top level with the row, kept inside the window | Only on hover or focus |
| **Page** | Achievements, the age pages: one at a time | centred, min(720, W-64) x min(560, H-96), over a 30 percent ink scrim that dims map and rails | Only while open |

- **The middle** is the canvas minus the rails (x 344 to W-344) and minus the top 96 and the bottom 64 px. At rest nothing of the interface is in the middle. Only these may enter it: the tooltip while hovering, a page (the player opened it), the pointer's price tip in a click mode, map labels and coin numbers (section 5).
- **Zoomed out:** at the Page stage the rails, goal, note, timers and line fade out; the purse shrinks to one line (coin, level) in its own place; navigation stays. At the Book stage only navigation stays (the book's left page is the summary).
- While the book is closed (pause) no zone shows.

## 3. What is in each zone

**Purse** (four lines, fixed): the coin sign and the amount (number style "large"), income "+296 a minute" right-aligned (caption); "Level 6 of 16" (body) with "1,312 of 2,200 renown" right-aligned (caption); the renown bar; "Next: a cottage and its lane" (caption). No hints here.

**Goal:** who asks (caption); what and by when ("117 grain within 78 s", body, bold); the bar with "27 of 117" (timed colour); the reward (caption). The drive strip: its name, ten pips, "4 of 10".

**Shop:** a header ("Shop", title style; on the right "3 to buy now" when anything is affordable). Then the groups in a fixed order: People, Work, Roads, Town, and in the second age The river, added at the end so that nothing above it moves. Each purchase is one **row**, 48 high:

```
[key] [icon]  Name                level 9   [ price pill ]
              +3.7 coin a minute
```

- The price pill (96 x 32, right-aligned) is the button's state: **affordable** (gold fill, ink text), **saving up** (paper fill, a gold fill growing from the left as coin comes in, muted text), **cannot now** with the short reason instead of a price ("No room", "No worn road"), **mode on** for Pave and Extend ("On", ink fill). The whole row is the click target.
- **Folding:** a locked purchase is not a row. Each group ends with one muted line, 24 high: "2 more at level 7". A finished purchase leaves; the group's heading shows "3 done" in caption. A group with nothing left folds to its heading.
- **Fitting:** if the rows do not fit the rail, rows go to 40 high; if they still do not fit, the rail scrolls (the wheel over the rail scrolls the rail, not the map; a 4 px thumb shows where you are). Keys keep working for rows scrolled out of sight.
- A row that has just become affordable pulses once (0.4 s); nothing in the shop moves or blinks otherwise.

**Timers:** the bell is a button: "Ring the bell [Space]"; while ringing "Ringing, 7 s" with a bar running down; while resting "Rests, 14 s" with a bar filling, disabled look. The tide: the dial, "High water, falling", "lowest in 1:14".

**Navigation:** four buttons of equal height, right-aligned in the rail: Achievements (icon and "11 of 24"), this age's page (icon), centre the view (icon), Pause (icon). Each has a tooltip with its name and key.

## 4. One set of styles (`UiKit`), used everywhere, also on pages, the pause cover's question and the settings

| Thing | Style |
| --- | --- |
| Type | Two families only, the ones already shipped: the book's serif for titles, the interface sans for everything else. Four sizes: caption 14, body 16, title 20 (serif), large number 30 (sans, bold). Numbers use tabular figures; coin amounts always carry the coin sign |
| Card | Paper fill at 96 percent, ink border 2 px, corner 4, padding 12. Variants: **quiet** (no border, 88 percent, for the note and the line) and **accent** (wheat fill, for the banner) |
| Button | Height 48 (rows, bell) or 32 (inside cards and pages). **Normal** paper with ink border. **Hover** lighter fill, border 3 px. **Pressed** darker fill, content 1 px down. **Disabled** 45 percent ink, thin border. **Affordable** wheat fill. **Focus** (keyboard) a river-blue ring 2 px outside. **On** (a mode) ink fill, paper text |
| Bar | 8 px high, corner 4, ink border 1 px. Wheat for renown and saving up, river blue for time and requests, moss for done |
| Key cap | Caption size in a small box, muted; always at the same place in a row or button |
| Tag (labels on the map) | Caption size, paper pill at 85 percent, ink text, no border |
| Coin number (on the map) | Body size, bold, wheat with an ink edge |
| Colours | Only named colours of `Palette`: ink, paper, wheat (money, affordable), river blue (time), moss (done), brick (a warning). Muted text is ink at 60 percent |
| Icons | 24 x 24 on screen, ink line with one flat colour, drawn from 48 x 48 files named `assets/icons/shop-<key>.png`, `ach-<key>.png`, `msg-<kind>.png`, loaded with `TownHud.IconOr(name, stand-in)` so a missing file shows the present stand-in |

## 5. Things on the map itself

- **Coin numbers:** appear at the building where the delivery was made, rise 24 px and fade over 0.9 s. At most six at once; deliveries at one building within half a second show as one number. Never drawn under or over a zone.
- **Tags** (a staked plot's "A cottage, at level 7", a boat's cargo): one tag style. A tag that would lie under a zone or under another tag is hidden. A boat's tag shows for the boat at the berth; other boats show theirs on hover.
- **The pointer's tip** in Pave and Extend mode: tooltip style, beside the pointer, clear of the target.

## 6. Tooltips

Every row of the shop, the bell, the tide, the four navigation buttons and the purse's lines have one. A shop tooltip says, in this order: the name and level; what it does in one sentence; what it is worth ("+3.7 coin a minute; pays for itself in 14 s"); the price; if it cannot be bought, why and when ("Opens at level 7", "Need 30 more coin, about 8 s"); the key. It appears after 0.25 s of hover or at once on keyboard focus, in the tooltip zone, and never covers the row it explains.

## 7. Tests (a new check step, `game-layout`)

For every state of `tools/ui-audit.sh`, in both ages, at 1920 x 1080 and 1280 x 720, at 80, 100 and 150 percent:

1. Every zone's rectangle is the one this sheet gives for that canvas, in every state: nothing moved because something else appeared.
2. No two zones overlap (the tooltip and a page are the stated exceptions).
3. Nothing is cut off: every control lies inside the window and inside its zone, and no text is wider or taller than its box.
4. The middle is free except for what section 2 allows.
5. At most five things ask for attention at once (an accent fill, a running bar or a motion counts as one each; the affordable pills together count as one).
6. Only the styles of section 4 are in use: every font size on screen is one of the four, every card one of the three variants.
7. Locked purchases are not rows; finished ones are gone.

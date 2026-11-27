# Review of the right side (shop and navigation), n4

Reviewer: judged from pictures only. BEFORE = `build/n3/final/1920x1080-100/`; AFTER = `build/n4/merged/` where it exists, else `build/n4/shop-round1/shop-*/`. Pair crops (before left, after right) and zooms are in `build/n4/review/`. Merged pictures existed only for u01-u08 and u42-45 at 1080, u06/u13 at 1080-150 and u03/u13 at 720 when I looked; everything else is round 1, so I judged zeros as fixed.

## Verdict

The owner would not now say "bugged". It is a different, calm sheet: headings with hairlines, one name size, one pill shape, keys stacked under icons, a card tooltip, clean navigation buttons. "Awful" is mostly gone. What is left is a handful of finish faults that a person who looks closely at the pills will call sloppy: the wash edge cuts through the price digits, affordable pills are not the same width as the others, a line of text sits against a pill's corner, and the compact and 150 percent layouts drop information (key, rate) without saying so. Nothing needs a redesign. Fix 1 to 4 and I would call it finished.

## 1. The eleven faults

1. Zeros slashed: fixed in merged (not judged further).
2. Pill outlines: GONE. "Not yet" pill is clipped to its capsule, "blocked" is a plain thin capsule with a dash (`pair-u13.png`, x 590-694, y 240-272 of the right half). No dashed frame, no line under pills.
3. Key order: GONE. 1 2 3 / 4 5 F / 6 7 8 / 0 reads upward (`pair-u07.png`, `pair-u08.png`). The key is no longer a column, so rows without keys leave no hole. Caveat: compact rows and the 150 percent layout show no keys at all (see finding 5).
4. Icons touching key caps: GONE. Icon and key cap are a stacked pair, same 20 px width and same left edge (x 404-424 in `pair-u13.png` right half); bell and speed lines clear.
5. One name smaller than the rest: GONE. "Extend a cottage", "Better boat prices" are body size everywhere incl. 720 (`s-720-u33.png`) and 150 (`s-150-u13.png`).
6. Uneven row content: GONE at 100/80 percent and 720: every tall row has a second line and all are 60 apart. PARTLY at 150 percent and in compact rows, where the second line is dropped by design.
7. Empty headings and grey notes: GONE. Headings are name plus hairline only (`pair-u13.png`). But the notes have moved into a foot line that sometimes names a level and sometimes not (finding 9). Headings with a single row ("Town: Regulars", "The river: Better boat prices", `pair-u33.png`) are legal but look like over-sorting.
8. Compact rows look like another interface: MOSTLY GONE. Same name font, weight and icon, same pill (`pair-u32.png`). They still lack key cap and rate (finding 5).
9. Navigation key cap boxes: GONE. Icon-only buttons, centred, no collision (`pair-u13.png`, y 1017-1063). Side effect: the keys (Tab, N, Home, Esc) are shown nowhere any more except in a tooltip I could not see (no u43/u44 nav shot in merged was checked).
10. Run-on tooltip: GONE. Card with title, key cap, sentence, hairline, Worth/Price/Short (`t-u42.png`) and Why for blocked (`w-u48.png`).
11. Prices not aligned: GONE for the digits (right-aligned column at x~683 in `pair-u13.png`), but see finding 3 for pill widths.

## 2. Findings, worst first

### Must fix

**A1. The wash edge runs through the price digits.** `shop-round1/shop-1080/u13.png`, crop x 1564-1920: Pace pill (2,108 at about x 1780-1850, y 156-180), Extend a cottage (1,629, y 304-328), Tools (1,974, y 392-416); also `merged/720/u13.png` Pace 2,108 and `merged/1080/u05.png` Tools 30. Zoom `review/z-u13-pills.png`. The vertical edge of the wheat fill cuts a digit in half, which reads as a rendering error (a "1" or "9" half on tan, half on paper). Also contrast: muted text (123,116,106) on wash (222,200,153) is about 3:1, below what a small number should have. Instead: keep the wash but draw the number in full ink (or paper-dark) on both halves, or confine the wash to a thin bar along the pill's bottom, or cap the wash visually so it never ends under the digit area (digits area is the right 55 percent of the pill; start the wash from the coin and end it before the digits, scaling inside the left 40 percent). Simplest honest fix: ink-coloured digits plus a lighter wash (about 30 percent wheat).

**A2. Affordable pills are wider than the others, so the right edge jumps.** `merged/1080/u08.png` (right-half crop of `review/pair-u08.png`): Louder bell pill starts at x 586, all other pills at 590, its right edge also not on the common line; Regulars at 150 percent (`s-150-u13.png`) starts x 396 and runs to x 536, past the 530 edge of every other pill (6 px). A growing pill is exactly the kind of "looks bugged" the owner saw before. Fix: one fixed width per rail; for 6-digit prices use shorter number form, not a wider pill.

**A3. Line 2 collides with the pill corner.** `merged/1080/u05.png`, Villager blocked: "No room in the cottages" ends at x 226 (crop), pill starts at x 222, line 2 top is 1 px under the pill's bottom (y 122-123). Same in `pair-u08.png`. Text under a pill reads as touching it. Fix: shorten ("No room: no cottage"), or reserve line 2's width to end 12 px before the pill, or move to 2 lines.

### Should fix

**B4. Hover, pressed and rest are nearly the same.** `shop-round1/shop-1080/u46.png` vs `u47.png` (Flour price row, y 566-620 in `review/hov.png`): hover band is a very pale tan; pressed is only slightly darker and the 1 px content shift is invisible. On an affordable row the band is the same tone as the wheat pill so it merges. A player cannot see "I am holding the button". Fix: pressed at 18-20 percent and pill darkens one step; hover band 9-10 percent.

**B5. Compact rows and the 150 percent layout silently lose the key and the rate.** `shop-round1/shop-1080/u32.png` (x 1564+, y 90-780), `s-150-u13.png`, `s-720-150-u13.png`. Keys 1-9 vanish, which a player who learned "6" under Tools in the first age will notice; the "+85 a minute" line vanishes, so they can no longer compare purchases. Compact at 1080 100 percent also leaves 280 px empty beneath the sheet (u32 sheet ends y~795 of 1080), so there was room. Fix: use tall rows until the height truly fails, or put the key cap in the compact row (left of the name in muted small box) and show the rate in the tooltip only with a hint.

**B6. Tooltip touches the sheet.** `merged/1080/u42.png` card right edge x 1584, sheet left edge x 1584 (zero gap); same in `shop-round1/.../u46.png` (card right 1583, sheet 1585). It covers nothing, which is good, but a card butting a card looks glued. Leave 8-12 px. Also in u46 "Worth +78 a minute, pays back in / 16 min" wraps with the second line indented under the value, while Price has no gap: set a fixed value column and allow the wrap, or write "pays back in 16 min" on its own fact line.

**B7. Foot line wording differs by layout.** "Next: Basket at level 3 · +1 more" (u42), "Next: Extend a cottage at level 5" (u45), "Next: Extend a cottage at a later level" (`s-80-u13.png`, `merged/720/u13.png`), "Next: Extend a cottage" with no level (`s-150-u13.png`, `s-720-150-u13.png`). Looks like four different texts for one thing; "at a later level" is a placeholder that reads like a bug. Show the level always (and in the 150 percent layout shorten the name instead).

**B8. Blocked rows with no second line in compact mode look broken.** `u32` right: "Pave a road" greyed with a dash pill and nothing saying why. In tall mode "No worn road yet" explains it. Put the reason in the tooltip only if you must, but a first-timer sees a dead row.

### What a first-time player will not understand

**B9. Boxed number under icon.** Never explained; the tooltip shows it again at the right of the title, which helps only after hover. First minute (u01-u05) the player sees "1, 2, 3, 4" boxes with no hint they are keys. A small word "Key" in the first row's tooltip, or "Press 1" in the line of the first tooltip, is enough.

**B10. The filling pill.** The wash is nice but nothing says "the tan is how close you are". At u03 several pills are 5 to 95 percent filled with no legend. The tooltip's "Short ◎4" helps. I would make the header say "Coins 12 of 18" on hover of any not-yet pill, or accept the wash as self-evident (it largely is).

**B11. "from 18", "-", "On".** "from 18" for Pave and the dash in a blocked pill are not explained at row level: line 2 explains the dash ("No worn road yet") but "from" is only guessable. Fine for a game that teaches by play, but write "18 and up" or give the tooltip a Price line "from 18, grows with road length".

**B12. Foot line "Next: ... at level N".** Clear to a player who has seen levels; the first-minute player has no level in view on the right. Acceptable, low priority.

### Could fix

**C13. Hairlines.** Right-side hairlines (about 16 percent ink) are fainter than the ledger's rule lines on the left (`review/left-top.png`: Next:/Request/Charter rules read at roughly 25-30 percent). Match them.

**C14. Single-row groups.** "Town" with only Regulars, "The river" with one row (u33): a heading and hairline for one row costs 28 px at 720 where space is tight. Merge into one heading when a group has one row and no key, or keep.

**C15. Nav buttons: pause is always heavy.** `pair-u13.png` y 1017-1063: pause has a thick ink border and dark icon while book and centre-view are pale; at rest this reads as "pressed". Either all four share a border weight when enabled, or pause is the only one with a state change when paused.

## 3. Other sizes

- 1280 x 720 (`s-720-u33.png`, `merged-mix.png`): holds. Tall rows fit; sheet ends y 603 against nav top at 670 (67 px clear). Same wash/digit problem (A1). Foot text wording differs (B7).
- 150 percent (`s-150-u13.png`, `s-150-u06.png`, `s-720-150-u13.png`): holds without overlap, names all one size, nothing cut. But everything is compact: no keys, no rates (B5), and Regulars' pill wider (A2). At 720 150 percent the "Next: Extend a cottage" line has no level.
- 80 percent (`s-80-u13.png`): holds; text is small but legible; hairlines, pills and key caps scale together. The key cap and line 2 are the smallest text on the screen and are tiny at 80 percent; acceptable.

## 4. Left against right

Closer than before, but the right side is now a bit different from the left. Shared: paper card, 2 px ink border, same corner, same muted caption colour, wheat fill for progress (ledger bar) and for pills, same sans for numbers, bell chip and nav buttons use the same border. Differences that show: (a) the right side uses a serif for "Shop" and group names, while the ledger top (`left-top.png`) uses bold sans for the purse and "Charter" and muted sans for the rest; (b) the ledger rule lines are stronger than the shop hairlines (C13); (c) the ledger fills with a flat, dark-outlined bar and the shop with a clipped wash that has no outline of its own, so the two "progress" ideas look related but not identical; (d) the bell chip on the left shows its key as a boxed "Space" next to the name (u03 `left-u03.png`), while nav buttons on the right carry no key at all. None is a fault; the serif title is the one I would test, because the purse is the loudest thing on the screen and is sans. I would not call either side the odd one out; the right is slightly more refined.

## 5. Would the owner say "bugged and awful"?

No for "awful"; the structure is orderly now. A careful owner would still point at "the price cut in half by the tan" (A1) and "the Regulars pill is fatter than the others" (A2) and call those bugs. Fix A1-A3 and B4-B7 and I would say it is finished.

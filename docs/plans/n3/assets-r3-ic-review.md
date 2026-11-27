# Asset round 3, track IC (icons): review

Reviewer: judged from the pictures only (sheets in `build/n3/assets-audit/r3-ic/`, game shots in `build/n3/ui-audit/r3-ic/` and `r3-ic-1280x720/`). Read at 02:32 UTC. Nothing was changed.

General verdict: the new set is a real step up. One ink line, round ends, flat palette, a coin badge for prices: it looks like one hand drew it, and the shop rows in the game read well at 24 px. Two things hold it back. First, most of the new icons are not asked for by the game yet, so the player mostly sees the old ones. Second, the medals, the stone icons and a few "same drawing, other name" pairs fail the 24 px test.

## 1. Per group

### Shop (purchases, 34 files)

Read at 24 without the label: shop-villager, shop-hand, shop-pace (boot with speed lines), shop-bell, shop-belltrack (bell with arcs), shop-building-mill, shop-building-stall, shop-building-bucket, shop-building-field, shop-extend, shop-pave, shop-regulars, shop-value-water, shop-value-grain, shop-value-wool (sheep), shop-boatprices, shop-ferry, shop-raft, shop-crane, shop-nextage, shop-rope, shop-load-basket, shop-load-handcart, shop-load-yoke.

Do not read, or only half:
- **shop-load and shop-load-basket are the same picture** (doubled). Fix: delete `shop-load` and make the game ask `shop-load-basket`, or draw `shop-load` as one sack in the arms.
- **shop-building and shop-extend are the same red house**, one with a plus, one with a box beside it. At 24 you cannot say which is "buy a building" and which is "extend a cottage". Fix: shop-extend keeps the house with the side wing, drawn wider (wing as wide as the house, lower roof line). shop-building becomes a plain house on a plot outline, plus kept only as a small badge at the top right, which is the current drawing, so move the plus to a clear 10 px circle and thicken it.
- **shop-value-cloth** is a striped drum: it reads as a barrel. Fix: a rolled bolt on its side with a visible spiral end and a loose flap of cloth, in Brick, no stripes on the body (stripes are what make it a barrel).
- **shop-value-flour** is a brown jar or vase with horns. Flour is meant to be a pale sack with a dark band (style sheet section 6). Fix: Limewash fill, tied neck, one dark band across the belly, no horns. Right now it also has the same sack silhouette as boon-stronger-backs, boon-millers-guild and ach-purse-1k; a pale sack with a band separates it.
- **shop-winch** looks like a radiator or a mangle. Fix: drum with a crank handle at one side and the rope end hanging, standing on two legs (the legs are fine); drop the vertical stripes, wind two rope turns instead.
- **shop-tools** (a mallet or flag on a stick) is hard at 24. Fix: draw a hammer head upright and heavy (wide block with a flat face) over a crossed saw or pick.
- **shop-toll**, **shop-berth**: pole-and-bar for toll is fine at 48, at 24 it is three thin strokes. Fix: thicker beam, add a coin hanging at the end. Berth (post with a rope loop) reads as a lamp post; add a moored boat bow beside the post.
- **The coin badge on every shop-value-*** (and boatprices): at 48 fine, at 24 it is a smudge, at 16 (1280 x 720) the ear of grain and its coin become one blob. The shop row already has a coin inside the price pill, so the badge says the same thing twice. Fix: in the game use the bare good drawing for the row; keep the badge only on the sheets if wanted.

### Boons (8)

Read: boon-deeper-well, boon-market-right (scales), boon-longer-rows, boon-third-room (house, three windows), boon-faster-hands.
Weak:
- **boon-faster-hands** is shop-hand plus the speed lines of shop-pace: a near twin of two shop icons. Acceptable, but at 24 the lines are thin; make three lines, heavier.
- **boon-stronger-backs** and **boon-millers-guild** are the same sack, one with an arrow and one with a cog. At 24 the cog is a dot-in-a-ring and reads as a coin. Fix: millers-guild = the mill (sails) in a small shield or with a sack at its foot; stronger-backs = a back with a load (person bent under a sack) or a sack with a large up arrow already there.
- **boon-wider-lanes** (brown trapezoid, two black triangles) reads as an hourglass or a speaker. Fix: two parallel road lines with outward arrows between them, in Trail earth.

### Perks and milestones (6)

Read: perk-good-neighbours (two houses), perk-masons-eye (set square on stone), perk-light-feet (feather, reads as a quill, so it says "writing", not "light feet"; fix: a feather beside a boot, or a boot with wing).
Do not read:
- **milestone-pace, -tools, -value are gravestones.** An arched stone with a flat base and a name plate is an RIP stone, and a message about progress should not look like a grave. Fix: replace the arch with a roadside milestone (a short squat stone with a flat slanted top, or a flag on a pole standing on a small mound) and keep the glyph (boot, hammer, coin) as a badge on it. The same fault is **msg-milestone** (grey stone with two lines).

### Achievements, first age (20 medals, glyph in Paper on wheat)

All sit in one scalloped medal, so they read as "achievement" at once. That is good. The trouble is the glyph: at 24 the drawing inside is about 10 px, and the second ink ring plus the scallop eat the room.

Read at 24: ach-bell-10, ach-charter, ach-cobbles, ach-first-coin, ach-flour-50 (loaf), ach-grain-200, ach-handcart, ach-market (stall), ach-mill, ach-pace-10, ach-purse-1k, ach-rate-300, ach-treasury-20k, ach-water-200, ach-worn-5, ach-trips-100 (footprints), ach-villagers-5.
Do not read:
- **ach-all-paved** is a plus sign: it reads as a hospital cross. Fix: a grid of paving slabs, or a road crossing filled solid with the slab lines, no cross shape.
- **ach-first-pave and ach-trips-1000** are the same drawing (a cone of rubble with dots). Fix: first-pave = one slab being laid with a trowel; trips-1000 = a stack of footprints or a long line of steps in rows.
- **ach-villagers-5 and ach-villagers-10** differ only by a second small house behind; at 24 the same. Fix: 5 = one person with four dots; or five heads in a row against ten heads in two rows (count shapes).
- **ach-level-3, -6, -9**: a shovel on a plot, a circular arrow, a lollipop tree. They do not say "level" or even say a number of levels, and the circular arrow looks like "reload". Fix: one family: a sprout (3), a young tree (6), a full tree (9); or the same flag (msg-level) with 3, 6, 9 pennants.
- **ach-first-pave vs ach-first-coin vs ach-cobbles** are fine apart; but ach-first-coin (a ring in a ring) is the least rich medal and reads as a button; add the coin's stamp (a small mark or a sheaf).

### Achievements, river (16 medals, glyph in Paper on River blue)

Read: ach-first-fleece (sheep), ach-skippers-friend (cap), ach-toll-keeper, ach-the-crane, ach-ferryman (ferry), ach-wet-feet (boot on waves), ach-two-berths (two posts), ach-full-hold (bowl of balls).
Do not read:
- **ach-ten-holds and ach-fifty-holds** (two bowls and four bowls): at 24 you cannot count the bowls and they look like trapezoids. Fix: show a hold with a number of crates as a tally (10 = one crate stack of ten dots in rows; 50 = five such rows) or use a filled hold for 10 and a heaped, flag-topped hold for 50.
- **ach-raise-the-bridge**: a diagonal bar with two squares, no bridge. Fix: a drawbridge deck tilted up on its chains between two towers.
- **ach-dry-shoes**: boot on an arch is a boot on a pot. Fix: boot on a flat plank bridge with the waves beneath, not touching; wet-feet then has the boot in the waves. Today the two are the same boot and differ by a little wave line at the base.
- **ach-borough** is a cog in a sun-like ring, which is the seal (msg-seal) and the gear idea together; it reads as "settings". Fix: a row of three roofs under one flag, or the town charter seal on a roof.
- **ach-weavers-pride**: the same striped drum as value-cloth, reads as a barrel. Same fix as cloth.
- **ach-first-mooring** and **ach-two-berths** are both a post: the first has a boat, the second doubles the post; OK, but the post is the weakest icon of the river set (a pale cylinder). Fix: a post with a rope loop and a hand's-width cleat.
- **ach-river-trade** (coin over a bowl) and **ach-full-hold** (balls in a bowl): both a bowl; at 24 the same shape. Fix: river-trade = a boat with a coin on the sail.

### Messages (13)

Read: msg-achievement (rosette), msg-boon (star), msg-building (door), msg-hint (lantern), msg-level (flag), msg-perk (horseshoe: luck), msg-touch (flower), msg-trouble (triangle), msg-charter (scroll and seal), msg-age.
Do not read:
- **msg-drive** (the charter drive): a scroll with a black blob in it. Reads as a smudge. Fix: scroll with a row of ten small ticks, the first few filled (it is a bar in tenths).
- **msg-seal** is a red cog or sun: the same as ach-borough and similar to the rosette. Fix: a round seal pressed in wax: circle with a pressed star or the river wave, and a ribbon tail at the bottom (so it is not a gear).
- **msg-milestone**: gravestone, see above.
- **msg-age** is the same open book with a feather as **shop-nextage**. Intentional, but it is two names for one picture; say so in the ledger or give the message a bookmark ribbon.
- **msg-achievement == nav-achievements**: the same rosette; fine, it is meant.

### Navigation (7)

Read: nav-achievements, nav-bell, nav-page (book), nav-pause, nav-home (crosshair: reads as "target" or "aim", not as "home"; fix: a house on a cross, or a house roof over the crosshair).
Do not read:
- **nav-tide-high and nav-tide-low**: a block of water with a post and tick marks. At 24 the two differ only in the water level behind the post; you cannot tell them apart without a label. Fix: put a large arrow on each (up for high, down for low) and move the water line clearly (nearly to the top / to the foot of the post).

### Older icons still in the set (`ui-*`, `good-*`, `tool-*`, `up-*`)

The old 24-unit set is a pale outline style, no colour, thinner line. It is in the game in places (HUD coin everywhere, tide dial, the pause/play family). Beside the new set they look like another game. They should be redone in the new style or hidden; the coin (`ui-coin`) in particular is on every row and is the old line drawing.

## 2. In the game

Shop at level 8 (u10, 1920 x 1080): the icons sit at about 20 px, left of the name, one size, one column, aligned with the row text. Villager, Extend, Pace, Tools, Water, Grain, Pave, Regulars, Louder bell are findable at once and the colour per kind helps. That is the best part. Problems:
- **The Yoke row shows a basket** (`shop-load`), the Louder bell row shows the plain bell where the track upgrade should show the bell with arcs, and the buildings would all show one house. The new drawings exist but the code asks for the old names.
- **The achievements page (u15) has no icons at all**: names, lines and bars only. The new 40 medals appear nowhere in the game. A medal beside each name, 24 px, is the whole point of the work.
- **Choices, notes and banners** (msg-*, boon-*, perk-*, milestone-*) do not show the new icons: `BoonIcon` asks the old shared names, and no code asks for a message icon.
- **The tide dial** (u33) shows a plain dial/clock stand-in, not `nav-tide-high`/`nav-tide-low`.
- **At 1280 x 720 (16 px icons)** the grain ear and its coin merge into a smudge; the water drop and coin are a blue dot. The bell is clear, the pace boot is clear, the hand reads; the sacks (flour, loads) are fine; coin badges are not. See the value-* fix.
- "Better boat pric..." is cut off in the river rows; its boat icon is clear but tiny. Not an icon fault, but the name needs a shorter text or the row two lines.
- Locked and finished rows: `ui-lock` and `ui-check` are still the old line style and unused; a locked row shows no lock in the new style.
- Nothing is doubled in a row and no stand-in shows in the shop rows I read. Stand-ins do show (old clock, old coin) in the tide dial and the coin rows.

## 3. MUST FIX (worst first, at most eight)

1. Wire the new names into the game: `shop-belltrack`, `shop-building-<offer>`, `shop-load-<step>`, `boon-*`, `nav-tide-*`, and `msg-*` for notes and banners. The Yoke row showing a basket is plain wrong.
2. Put the medals on the achievements page (`ach-*` beside each name at 24 px). Right now the 40 medals are invisible.
3. Make the medal glyph bigger at 24: drop the inner ring, thin the scallop, let the glyph fill about 70 percent of the medal. At 10 px most of the 40 cannot be told apart.
4. Fix the twins and the meaningless ones: ach-first-pave vs ach-trips-1000, ach-villagers-5 vs -10, ach-ten-holds vs -fifty-holds, ach-all-paved (a medical cross), ach-level-3/6/9, ach-raise-the-bridge, ach-dry-shoes vs ach-wet-feet, shop-load vs shop-load-basket, shop-building vs shop-extend.
5. Remove the coin badge from the value icons shown in the shop rows (the price pill has the coin); at 16 to 24 px it is a smudge.
6. Redraw the barrel and the jar: value-cloth and ach-weavers-pride (a drum), value-flour (a vase). Cloth is a bolt with a spiral end; flour a pale sack with a dark band.
7. Replace the gravestone on milestone-pace/-tools/-value and msg-milestone with a roadside stone or a flag on a mound.
8. Redraw msg-drive (a black blob), msg-seal (a gear) and nav-tide-high/low (add arrows).

## SHOULD FIX

- nav-home: a crosshair is not "home"; add a roof.
- boon-wider-lanes, boon-millers-guild, boon-stronger-backs, perk-light-feet: see group notes.
- shop-winch, shop-tools, shop-toll, shop-berth: thicker, simpler shapes at 24.
- ach-borough (a gear), ach-first-coin (a plain ring), ach-first-mooring (a post).
- Redo or retire the old `ui-*` set (coin, lock, check, pause, play, speed, tide, boat); they are visibly another style beside the new icons.
- Wire `ui-lock` and `ui-check` (new style) for locked and finished shop rows.
- Shorten "Better boat prices" or give the river rows room.
- Check the whole sheet at 16 px too: the game scales to 1280 x 720 and that is where the thin parts (ear and coin, wave lines, tick marks) disappear.

## 4. Better, same or worse (one word per group)

- Shop: better
- Boons: better
- Perks and milestones: worse (gravestones)
- Achievements, first age: better (in the sheets; not in the game)
- Achievements, river: better (in the sheets; not in the game)
- Messages: better (not in the game)
- Navigation: same
- Older set: worse (now it clashes)

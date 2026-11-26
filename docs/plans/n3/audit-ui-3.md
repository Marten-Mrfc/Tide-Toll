# Interface audit 3: the small window (1280 x 720, interface 100 %)

Reviewer 3. No code changed. Judged from the 34 pictures in `build/n3/ui-audit/1280x720-100` (all read), the mechanical report there, `ui-inventory.md`, and a few 1920 x 1080 pictures for comparison (u06, u13, u20, u35, u36). u32, u34 and u37 were never made (the tour could not stage them); no judgement on them.

How I counted "things asking for attention": every separate card or block that is on screen at that moment. The shop column counts as ONE (its 6 to 14 bookmarks are one more problem, see section 3). Every note slip, floating number, map label, pointer tip and the mode's coloured bands count separately. The numbers are my count from the picture, not the report's "cards/groups" column.

Two things about the small window that explain most of what follows:

- The interface is built for 1080 pixels high and shrunk to 0.78. All text under about 17 px becomes 12 px (a floor). Five sizes become one, so the ledger, the shop, the notes, the bell and the buttons all look equally "small and grey". The chrome does NOT shrink as much as the window: the two columns (left 258 + right 218 = 476 px) take 37 % of the 1280 width and leave a map strip of 780 px; at 1920 they take 32 %.
- The map is zoomed by the same camera, so the town fills the same fraction of the map but the columns are a bigger share of the screen. Anything the town grows into on the right (the mill at x 1030 in u06 and u07, the plot frame in u06, the shop-covered cottages in u30) goes under the shop column.

---

## 1. Per picture

Tags: [competes] [overlaps] [cut off] [jumps] [no clear place] [unreadable] [inconsistent style] [covers the map's middle] [covers what it is about].

**u01 First minute, level 1.** 7 things (ledger, shop, bell, achievements, pause, 2 stake labels).
- [inconsistent style] The bell sits bottom left as a greyed card "Comes at level 2": a locked thing shown in the corner of the screen as a full card, while the shop hides most locked things in one-line rows. Locked things appear in two different forms.
- [no clear place] "Extend a cottage: level 5" is a faded row in the shop between "Villager" and "Work": a goal five levels away shown on the first minute. Same for "Basket", "Tools" (level 3).
- [unreadable] Ledger "Renown 0 of 20" and "0 a minute" are 12 px faded grey on cream; the slashed zeros of the font read as "0" with a stroke, cheap and uneven next to the plain bold "0 coin".
- Good: the empty map is calm. This is the calmest picture of the set and it is still two full columns.

**u02 First purchase affordable.** 9 (u01 + note "Achievement: First coin." + floating "+7").
- [competes] The first note (full card, same cream as ledger) is stuck to the ledger bottom, 6 px away: reads as part of the ledger.
- [jumps] The shop re-ordered: "Pave a road" went from a bright affordable card to a faded one and "Water price" grew a third line; the column grew by 16 px in one frame, everything below moved.
- [competes] The "+7" floater is gold with a dark outline, the only gold thing on screen; money is gold only here and plain ink everywhere else.

**u03 Early shop (level 2).** 11 (ledger with hint, 3 notes, shop, bell, achievements, pause, "+3", hint ring, stake label).
- [competes] Ledger + three notes make a left stack of four cream cards reaching y 260 of 720: 1/3 of the screen height, all at the same weight. Three notes (new to buy, road worn in, villager moves in) say three different things at one volume.
- [cut off] The stake label "A field, at level 3" at the bottom edge (y 711 of 720): its baseline touches the window edge. Same in u04, u09.
- [covers what it is about] The field's stake frame (370 to 600, 560 to 680) is directly above the bell card; the label under the frame is almost under the bell column line.
- [no clear place] The hint ("A road is crowded. Pave it so more can walk it.") is the last line inside the ledger, indistinguishable from "Next: ..." above it. The ring on the map for the hint is an orange circle with no link to the text.

**u04 Level 2 banner.** 11.
- [competes] The banner (centre top, gold, serif 28 head) is the only "loud" card; but it appears 450 px from the left column that still shows three notes and a hint: three messages in three places at once (banner centre, notes left, hint in the ledger).
- [jumps] The banner appears in one frame; the old notes stay below it, not cleared.

**u05 Hint and request card (level 3).** 11 + request.
- [competes] Request card, 3 notes, ledger hint, banner: a left column 380 px tall of five cream cards plus the banner at top centre. Which one is "the thing to do now"? The request (blue bar) is not marked in any way above the notes.
- [overlaps] Banner bottom (y 80) touches the stake frame and label "A cottage, at level 4" directly under it (frame 630 to 700, 82 to 140, label at 160): the banner points at nothing, it just sits on the map's top middle. 289 px2 in the report.
- [jumps] The shop is 4 px wider and shifted left (starts x 1047 not 1051) because "No room: wait for a cottage" is longer than the column: the column edge moves between pictures. Right edge also differs from the achievements/pause line.
- [inconsistent style] The request card title is serif 12 px, its second line bold, its third normal, its last faded: four type treatments in a 97 px card.

**u06 Choice of two boons (level 4).** 10.
- [overlaps] Choice card (417 to 863, y 11 to 142) lies over the level banner (454 to 825, y 14 to 80): the banner head "Level 4" and its gift line show through the choice card's heading line; the choice's own head is drawn over the banner text. 24,486 px2 in the report. A mess exactly in the one moment the owner has to decide.
- [covers the map's middle] The choice card hangs at the top centre and hides the upper middle of the map; the town grows there.
- [covers what it is about] The stake frame at x 990 to 1100 (a plot to buy, the thing the player is working towards) is half under the shop column and the shop's "Roads" and "Town" heads.
- [jumps] This is the only picture where the shop bookmarks have TWO lines instead of three (the "+n coin a minute" line is dropped, `_tightShop`): the column shrank from y 502 to y 497 total but every bookmark lost a line, i.e. the shop changes its content to make room for something on the other side of the screen (the achievements button). At 1920 the same state shows 3 lines (compare). A third of the information disappears for a reason the player cannot see.
- [inconsistent style] The three notes now have two looks: gold "strong" slips and a cream one in between.

**u07 Extend mode, pointer on a cottage.** 12.
- [competes] Mode banner (top centre, black text on cream, 18 bold), tip "Extend this cottage: 40 coin. Rooms 2 to 3." (right of pointer), tag "Suggested: 40 coin" (above), a blue square on the cottage, and the shop bookmark "Extend a cottage [5]: Pick a cottage. E to stop" all say the same thing. Five voices for one action.
- [covers what it is about] The tip lies over the grass to the right of the cottage and reaches x 963, nearly the shop column (1051); the "Suggested" tag covers the cottage's roof line.
- [covers the map's middle] The mill at the right edge (x 1030) is hidden by the shop column.
- [competes] The ledger hint ("Every cottage is full. Extend one for another villager") and the notes still show: the old message system stays while the mode asks for attention.

**u08 Pave mode, pointer on a road.** 9.
- [competes] "Suggested: 21 coin" (tag) and "Gravel for this road: 18 coin. Click to pave." (tip) show two different prices for one road, 21 and 18 (the tag is for another road, the tip for the hovered one). Pure confusion even if it is correct.
- [covers what it is about] The tag sits on four walkers; the tip is on top of the well.
- [no clear place] Banner at the top centre, shop line "Pave a road: 21 coin: Pick a road. P to stop" at the right: two places for the mode's state.

**u09 Bell ringing.** 11.
- [jumps] The bell card widens from 132 to 215 px ("Ringing: 8 s left, then rests 32 s"): the one card in the bottom-left corner changes width every time its text changes (also u10 "Rests 3 s", u35 222 px).
- [competes] Notes fading out (three ghost cards at 40 % alpha) while the ledger and hint are full strength: a half-visible card is neither gone nor there.
- [no clear place] The bell has no ring or sign on the map; its state is only at the bottom left, 600 px from where the town is.

**u10 Level 8 at rest.** 12.
- [competes] Request card (373 Water within 16 s) + three yellow "strong" notes ("Milestone! Grain...", "Milestone! Sharp tools...", "A choice of two boons is open at the top.") = four loud cards stacked in the left column down to y 457. The one note that matters (a choice is open!) is the last and looks like the two milestones.
- [overlaps] The "+911" floater sits over the road at the middle; fine, but it is 27 px gold next to nothing readable around it. "+12" top left over the barn.
- [cut off] Nothing cut; but the shop's "Roads" and "Town" heads are partly under the river and trees (the heads are drawn without a backing, text on the map).

**u11 Busiest moment, level 11.** 16 (ledger, request, 3 notes, banner, shop, 3 bottom, 6 floaters).
- [competes] Six floaters ("+2,325", "+1,782", "+1,833", "+192", "+3,720", "+1,381") of 27 px gold: the biggest and boldest type on the screen is the money flying about, louder than the ledger's money (26 bold). The numbers are bigger than anything else.
- [overlaps] "+192" sits on the cottage on the left and "+3,720" sits on the well and the road crossing: they cover what they are about (the deliveries). Request card is green (met), the notes under it are cream: a third colour.
- [unreadable] The shop heads "Roads" and "Town" are cut by trees behind them ("Roads" shows as "Roa:s").

**u12 Open building offer and plots, with Home.** 9 + Home.
- [no clear place] "Back to the town [Home]" appears bottom centre (x 560 to 720): a fourth action place at the bottom, with a style (tucked tab, left edge flat) used nowhere else. It sits 400 px from the bell and 300 from achievements.
- [overlaps] The "+5" floater overlaps the ledger corner in the report (1,156 px2); in the picture it is just right of it.
- [competes] Stake label "The market, to buy" is plain black text on the map in a smaller weight than a bookmark; nothing links it to the shop's "Market stall [F]" row.

**u13 Level 15 at rest.** 11.
- [competes] Drive card (the charter drive, bar in ten parts) + three notes in the left stack; the drive card is gold, the notes cream and one gold. The charter drive is the goal of the age and it is the same size as a note.
- [no clear place] The drive is under the ledger where the request card used to be: the "current goal" slot is the same slot for request and drive, but the ledger's own "Next: The town charter and a last lane" is also a goal (a third goal in the same stack).
- [inconsistent style] Shop row "Louder bell, level 5: done" is one line of 12 px faded: "done" rows and "locked" rows look the same, so "done" looks broken.

**u14 Charter drive.** 12.
- [jumps] Notes fade to ghosts under the drive card.
- [competes] The "+40,221" floater at 27 px.

**u15 Achievements page open.** 8 + 24 cells (page 872 x 590 over x 204 to 1076, y 66 to 658).
- [covers the map's middle] The page covers 74 % of the window area including the whole map middle. It is semi-transparent: the town shows through the text and the floaters ("+58", "+40,221") and rings glow through the cells. Text on moving background.
- [overlaps] The page's left edge (x 204) cuts the ledger across (ledger runs to 270): "Next: The town charter and a last lane" and the hint are half over the page, the first achievement cells are under the ledger and drive card: "Town treasury", "Busy lanes" are half hidden ("(blank)reasury", "anes"). The ledger and the drive card are NOT dimmed or hidden; they are in front of the page's left column of cells. 6,612 px2 in the report, more in the picture.
- [competes] The shop column and the bell, achievements and pause buttons are all still there at full strength behind a "modal".
- [cut off] The first cells in the left column of the page ("Town treasury", "Busy lanes") are lost under the left stack.
- [inconsistent style] "Close [Tab]" is a tucked tab button (left flat), while "Pause [Esc]" and "Achievements" are full cards.

**u16 Charter moment.** 16.
- [competes] Request + three notes + banner + 3 floaters + "+ hand" marker on the mill + next-age card + bell + ring: the busiest first-age picture at rest.
- [overlaps] The banner is mid-fade (washed out) and the label "Ring in the next age when you are ready" is half over a tree top; the banner is at 380 to 898 wide, nearly 40 % of the window, for one sentence.
- [jumps] The next-age card (12, 614, 149 x 44) appears ABOVE the bell: the bell is pushed no where, but a new card slots in above it, and the achievements button sits right of the bell at the same row. Bottom-left now has two cards, bottom-right two cards, bottom-centre Home.
- [no clear place] The ledger says "Ring in the next age" (in place of "Next: ...") AND the card below says "Ring in the next age [N]": same message twice, 600 px apart.
- [inconsistent style] "+ hand" marker is a small tooltip-like card on the mill, a third card look (gold, border 1.5, margin 4).

**u17 Page stage (first age).** 4 (slim strip, pause, left page, map page).
- [jumps] The slim strip "980 coin   Level 16 of 16" (bold 14) replaces the ledger; the pause button moved from bottom right to the right of the slim strip (x 209): navigation jumps 1000 px in one frame. The coin shows 980 while u16 shows 634 and the page rows say 108,867 "Coin earned": three different money numbers for one town (partly timing, but the page prints "Coin earned" next to a slim "coin" with no label for the difference).
- [unreadable] The left page rows are 12 px light grey on cream: "Achievements, 23 of 24" list with "Done" in 12 px at 40 % ink. The list of 24 achievements is a printed wall in a size that cannot be read.
- [cut off] "A Plan of the Hamlet" and the compass N are at the very top edge of the window (y 6 to 40), the compass touches the top; the right page runs off the right edge (page number "2" cut at the bottom right).

**u18 Book stage.** 4. Calm. [unreadable] page text is 8 to 11 px effective (the whole book is shown smaller than in u17). The slim strip "980 coin Level 16 of 16" is the only chrome and lies over the book's lower-left corner (it overlaps the page frame).

**u19 Pause cover.** 1. Calm, large type (96 title, 44 buttons). [inconsistent style] This is a third look: brown leather, gold serif; nothing else in the game looks like it. A good screen, but the player gets there from a cream-card game.

**u20 New town question.** 3 (question + 2 buttons).
- [overlaps] The question wraps to two lines at 1280 and its second line "lost." runs through the button "Start a new town" (the text is drawn over the button's top edge). At 1920 it is one line and clean: the pause pages are only laid out for 1080. [cut off] The big frame has 300 px of empty brown below the buttons: the content sits in the top half.
- [inconsistent style] The "Start a new town" button is the dim one (the dangerous one), "Keep this town" is lit: good, but both are the same size.

**u21 Settings: Sound.** 
- Not a settings picture: it is the same picture as u17 / u18's book (left page the town, right page the map). The tour did not reach the Sound page (the cover/settings transition was captured too early). I cannot judge Sound.

**u22 Settings: Display.** Identical to u21: also the book. No judgement possible.

**u23 Settings: Keys.** 5 blocks (two pages, the 4 tabs, "Back").
- [cut off] The left page's list is clipped at "Buy pace" by a scroll area with a visible scroll bar; the next row is half drawn at the bottom of the list. The list continues on the right page with no scroll bar and no sign that left-down leads to right-up (reading order is not left page then right page).
- [inconsistent style] Buttons are bold 12 px key caps with black outline, nothing like the shop's bookmarks; tabs are coloured (lilac, blue, cream, tan) leaning out of the book at the right: pastel, in a brown/cream/gold game.
- [no clear place] "Restore defaults" is under the right page's list, "Back" under the left page: two exits.

**u24 Settings: Saving.** Captured mid page-turn: the right page is half turned with the Saving page's text showing through. Not a state to judge; the picture is a tool problem (the leaf was caught moving).

**u25 Key clash dialog.** 
- [covers what it is about] The dialog "That key is taken" sits over the key list: it hides "Take the right boon" and "Restore defaults", the rows it is about are covered.
- [inconsistent style] The dialog is cream on a darkened page with a blue focus frame (sky blue, the one blue-outlined button in the game); a fourth look next to the cover's brown and the shop's cream cards.

**u30 Second age, first minute.** 14 (ledger, request, tide, shop, next-age, bell, achievements, pause, 2 floaters, "+ hand", ring, 2 stake labels).
- [overlaps] The tide card (278 to 482, y 11 to 56) is built right against the ledger (270) with an 8 px gap: it reads as a part of the ledger that grew a wing. It lies exactly where the level banner will land (see u35).
- [unreadable] Two stake labels "A cottage, at level 16" east of the town are 6 to 7 px tall (x 850 to 940, y 340 and 514): not readable at all at this size.
- [no clear place] "The river age [N] Read its page again" (bottom left, above the bell) is a button for something already read: it takes a place in the bottom-left corner for the whole age. Achievements say "0 of 16 on the river" (24 vs 16 and the label changes length: the achievements card is 148 px wide in all states, so the text is crushed).
- [competes] The shop has a fifth head "The river" with one bookmark ("Better boat prices") under "Town": the column grows to 14 rows to y 529.
- [covers the map's middle] The tide card sits at the top-left of the middle third (x 278 to 482), not the middle itself; fine.
- Good: the river and the shop column are on opposite sides now; the town is the clear centre.

**u31 River age's page (open).** 7 (page, ledger, request, tide, shop, bell, pause).
- [covers the map's middle] 640 x 400 page in the exact middle: it hides the town, but it is the right place for a page. But the ledger, the request card, the tide card and the whole shop column stay fully visible and lit beside it: the page is not a modal. [competes] Request card says "within 70 s" and the timer runs behind the page while the player reads.
- [unreadable] Page text is 14 px serif on a semi-transparent cream: the town's road lines and floater "+1,049" show through the lines of text.
- [inconsistent style] The page is 640 wide, the achievements page 872, the cover 600: three modal widths.
- Achievements and next-age buttons are hidden while the page shows, but the bell stays: inconsistent set of what hides.

**u33 Shop with the river group (level 18).** 16.
- [competes] Request (innkeeper, 200 of 1026) AND a strong note "The rowboat Garth wants 160 Flour before the tide turns" (a skipper's request as a NOTE, gold) AND the tide dial. Two requests (innkeeper's and the rowboat's) at once, one as a card with a bar, one as a note slip that will disappear in 7 s: the player's most urgent goal is the one that vanishes.
- [unreadable] The boat label "Flour 0 of 26 loads" at the river (x 920, y 316) is 5 px tall. "The ford, at level 19" label at the bottom is 6 px, thin, black on a green field: unreadable.
- [no clear place] The ford, the boats and the tide are at the right of the map, 130 px from the shop column; the tide card that is about them is at the top left. The boat label and the dial are 700 px apart.

**u35 Busiest moment (second age, level 28).** 15 (ledger, request, tide, banner, 3 notes, shop, next-age, bell, achievements, pause, 3 big floaters + others).
- [overlaps] The banner (450 to 830) and the tide card (278 to 482) overlap by 32 px: the tide card's text "falling, lowest in 0:50" is touching the banner's border, the last digits crowd it. The report found this as the tour's second kind of overlap. At 1920 they do not touch (tide ends x 621, banner starts 715): a layout that is right at 1080 and wrong at 720.
- [competes] Request card (skipper Hob), three notes, banner, tide card, bell with long text ("Ringing: 14 s left, ready again after") = nine voices in the top 420 px of the screen.
- [competes] Floaters "+1,030,324" (47 px wide ranges, over the river and the boat, the largest thing on the screen), "+245,113", "+51,750": numbers that dwarf the town; the delivery numbers are bigger than the level banner's head.
- [unreadable] Boat label over the boat at the river "Grain 0 of 2822 loads" (x 880, y 358) is 5 px. The "A road is crowded..." hint is the same as in the first age's early hint (stale hint at level 28).
- [inconsistent style] The request card is now "Request from the skipper Hob ... Loaded 0 of 2822, leaves in 65 s ... Pays 24 coin a unit, more when filled" (another wording: loaded/leaves/pays) while the innkeeper's says delivered/within/reward. A different layout to learn for the same kind of thing.
- [no clear place] A note "The rowboat Hob wants 2822 Grain before the tide turns" duplicates the request card right above it. The same fact twice 130 px apart.

**u36 Pave mode, second age.** 14.
- [overlaps] The mode banner "Pave: click a lit road..." (435 to 845, y 12 to 41) lies UNDER the tide card (278 to 482): the word "Pave:" is hidden behind the card (only "click a lit road" shows). At 1920 they are 70 px apart. A banner that tells the player what mode they are in is cut in half by another card.
- [overlaps] Camera is far in (the town is huge, 5 times the size of u35): walkers and white granary boxes under the tip and tag; the "Suggested: 108 coin" tag and "Gravel for this road: 108 coin" are on top of the figures.
- [no clear place] The mode appears 3 times: banner, tip, shop bookmark "Pave a road [4 or P]: 108 coin: Pick a road. P to stop".
- [competes] The note "A road has worn in. It can be paved now." sits below the request card in the left stack; the ledger hint says "Pave it so more can walk it". Three texts all saying "pave".

**u38 Borough seal and its page.** 6 (page fading, ledger, tide, shop, bell, pause).
- [unreadable] The "The second age is complete" page is caught at 40 % alpha: the page text and the town are mixed; I cannot read the text and the title is half-transparent over the map. (Tool timing; the real page is probably fine but the fade shows how little backing the page has.)
- [no clear place] The achievements button and next-age button are gone while the page shows; the bell remains.
- [competes] The ledger has changed to a smaller card ("The borough seal is yours" and no hint), the request card is gone: the left column is now only 115 px tall: a long way from busy. Calm picture, but it shows the opposite problem: when nothing is asked, the screen is half empty at the left with two full-width columns anyway.

**u39 Achievements page (second age).** 8 + 16 cells.
- [covers the map's middle] 872 x 430 page over x 204 to 1076, y 146 to 578: the same width as the first age's page but 160 px shorter and not at the same height: the page moves between ages (first age y 66, second y 146).
- [overlaps] The page's left edge at 204 cuts the ledger corner and the tide card is left of it (278 to 482): the tide card is partly under the page's top-left (the page begins at y 146, the tide ends at y 56: no, they do not touch); but the page's left cells are over the town. The ledger (to 270) overlaps the page by 66 px (x 204 to 270, y 146 to 126): only a corner. 
- [competes] The title "Achievements of the river, 13 of 16. The first age: 23 of 24." is one long serif line with two counts: it reads as a sentence, not a heading.
- [inconsistent style] "Close [Tab]" tab-style button again while the page buttons around are cards.

**u40 Page stage (second age).** 3 (slim strip, pause, book). Same faults as u17: [jumps] pause beside the strip, [unreadable] two columns of 36 achievements in 8 to 9 px grey print, the third column ("Achievements of both ages, 36 of 40") is a single wall; [cut off] heading "The Town on the River" at the top edge, compass cut at the top.

**u41 Book stage (second age).** 3. [unreadable] the achievement lists are 6 to 7 px: the left page's list of 40 is only a texture. Slim strip over the page corner.

---

## 2. Where each kind of information sits (1280 x 720)

Key: L = left column (ledger at top, then request/drive, then notes), R = right column (shop), TC = top centre, BL = bottom left, BR = bottom right, BC = bottom centre. Positions in pixels at 1280 x 720 (x, y). "Jumps" means: not in the same place across states or both ages.

| Kind | Where in age 1 | Where in age 2 | Same in all states and both ages? |
| --- | --- | --- | --- |
| Money and income | L, top of the ledger (coin 26 px bold, rate 12 px faded, y 11 to 44). Also gold floaters over the map (27 px). Page stage: slim strip BL (x 12, y 684). Prices in the shop R. The book's page prints "Coin earned" | same ledger; floaters bigger numbers; slim strip | **NOT the same**: five places (ledger, floaters, slim strip, shop prices, book rows), two different looks (plain bold vs gold outline), and the numbers differ (the strip says 980, the ledger 634, the book 108,867) |
| Level and progress to next | L, ledger: "Level n of 16" serif 12, bar 12 px, "Renown a of b" faded, "Next: ..." gift line. Banner TC when levels change. Slim strip BL at page stage | same, "of 28"; at the seal the bar stays full and the "Next" becomes "The borough seal is yours" | **NOT the same**: two systems (ledger lines; banner) and a card that is a different size at each state (ledger 153 to 173 px high, 126 when no hint) |
| Things to buy | R column: People / Work / Roads / Town (age 2: + The river). Bell BL (x 12, y 664). Next-age card BL above the bell (y 614). Achievements BR. Stake frames and labels on the map ("to buy") | same; R column grows to 14 rows | **NOT the same**: the shop column changes length (6 to 14 rows) and content (2 or 3 lines per row), and "to buy" appears also as map labels (6 px in age 2), as notes ("New to buy: ...") and as a "+ hand" marker on the mill |
| Current goal (request, drive) | L, under the ledger (y 170). The request card (innkeeper, miller, baker, skipper) and the drive card use the same slot; the ledger's "Next:" and "Hint:" are two more goals in the ledger | same slot; skipper's request has a different card text; the skipper's wish also arrives as a note | **NOT the same**: the slot moves down by 20 to 60 px whenever the hint appears or goes; the drive card is the same size as a note; in age 2 a goal can be in the card, in a note, or on a boat label |
| Messages: banner | TC (y 14 to 80), 370 to 520 wide | TC (y 14 to 80), collides with the tide card at 1280 | **NOT the same**: same slot as the choice card (u06) and the mode banner (u07, u08, u36): up to three things claim TC |
| Messages: notes | L, below the last card of the stack (y 170 to 460), up to three, cream or gold | same | **NOT the same**: their top edge moves whenever the stack above grows (request, drive, hint) |
| Messages: hints | inside the ledger, last paragraph (y 120 to 160); also a ring on the map | same, hints stale at level 28 | **NOT the same**: appears and disappears and pushes everything under it |
| Time-limited: the bell | BL (y 664 to 708, width 132 to 222) | BL, same, but with the next-age card above | **NOT the same**: its width changes with its text; in age 2 it moves up by nothing but gets a neighbour |
| Time-limited: the tide | does not exist | top left beside the ledger (x 278, y 11 to 56) | age 2 only; **collides with banner (u35) and mode banner (u36)** |
| Time-limited: request timers, boat leaves | L card bars (request) | request card bars, boat labels (6 px) on the map | **NOT the same**: two places and one is unreadable |
| Modes: Pave and Extend | TC banner (y 14), tip and tag at the pointer, shop row R, bands on the map | same | **NOT the same**: four places for one fact; the banner can be hidden by the tide card (u36) |
| Navigation: achievements | BR (x 1029 to 1177, y 666 to 708); page opens over the middle | BR; label changes to "on the river"; page again in the middle but at a different height | consistent place, but hidden at the age page and at the page stage |
| Navigation: pause | BR (x 1185 to 1268, y 684 to 708) | same | **NOT the same**: jumps beside the slim strip at the Page/Book stage (u17, u18, u40, u41) |
| Navigation: home | BC (x 560 to 720, y 684 to 708), only when the view is moved (u12) | same | place used by nothing else; appears only sometimes |
| Navigation: next age | BL above the bell (y 614) after the charter (u16) | BL, "The river age [N] Read its page again", then "The second age [N]" | present for the whole second age; a button for "read it again" occupies the corner |

---

## 3. The ten worst layout problems, worst first

1. **Four loud corners and two full-height columns at once (everything, worst in u05, u10, u11, u16, u33, u35).** Left stack of ledger + request + three notes reaches down to y 410 to 460 and the shop is up to 14 rows tall: 37 % of the width is chrome at 1280. A calm layout would show one thing that is "the goal now" at one place, keep the rest folded into a single quiet strip, and give the map the middle two thirds.
2. **Top centre has up to three claimants and no rule (u06, u07, u08, u35, u36).** The choice card lies over the banner; the mode banner lies under the tide card; the level banner crowds the tide card. A calm layout would give the top centre to one "message" slot, queue the rest, and keep the tide with the shop or the ledger.
3. **The left stack moves under your eye (u02 to u05, u09, u10, u13).** The hint, the request card, the drive card and each new note push everything under them down by 20 to 120 px in one frame and notes are inserted on top. A calm layout would give each of ledger, goal and messages a fixed slot with a fixed height and never move anything that was already there.
4. **Messages say the same thing in several places (u07, u08, u16, u33, u35, u36).** Mode: banner + tip + tag + shop row. Next age: ledger + card. Skipper: request card + note. Hint: ledger text + ring + note. A calm layout would give every fact one place and one wording, and tie the others to it by position only.
5. **The shop changes shape with the screen and the state (u05, u06, u02, u11).** Rows are 2 or 3 lines, locked rows are 1 line, "done" looks like "locked", the column shifts 4 px, shrinks to hide a line to avoid a button 400 px away, and rebuilds whole when something is unlocked. A calm layout would give every row one fixed height and show the same three facts (name and key, price and state, gain) in the same order.
6. **Modals are not modal (u15, u31, u38, u39, u25).** The achievements page, the age pages and the key dialog lie over a fully lit interface: the ledger and the drive card cut across the page's first column, the town and floaters show through the text, and timers run behind. A calm layout would dim everything but the page, give all pages one width and one place, and keep the map out of the text.
7. **Navigation jumps and has no home (u16, u17, u40; Home in u12).** Pause moves 1000 px at the Page stage; Home appears in a place nothing else uses; the next-age card shares the corner with the bell; achievements, bell, next-age and pause are in four different bottom corners. A calm layout would put all navigation in one strip that never moves.
8. **Text is the same small 12 px grey everywhere at 1280, and on the map it is 5 to 7 px (u30, u33, u35, all age-2 map labels).** The floor of 12 collapses five sizes into one so nothing ranks; boat labels, ford labels and cottage labels are unreadable. A calm layout would have three text sizes with real gaps, and map labels that are never smaller than the smallest card text.
9. **Money is loudest where it matters least (u10, u11, u35).** Floating gold numbers of 27 px with outlines and up to a million are larger than the ledger's coin line; the ledger, the strip, the book and the shop show money five ways. A calm layout would show one money style, put the big number in the ledger, and keep floaters small and quiet.
10. **The pause pages and the book are laid out for 1080 only (u20, u23, u24, u25; u17/u18, u40/u41).** The question text runs through its button, the key list is cut by a scroll area, the tabs are pastel, the page stage is cut at the top edge and the print is 6 to 9 px. A calm layout would be built from one grid that scales with the window and never prints under 12 px.

---

## 4. What already works and must be kept

- **The map is the hero when nothing is asked (u01, u12, u30, u38).** Two quiet columns and an open middle; the town reads at once. Keep the clear middle third.
- **The ledger's content and order** (coin, level, bar, renown, next gift): a clear top-left anchor that every state uses. Keep it at the top left.
- **The shop column on the right edge, grouped under People / Work / Roads / Town**, with the key in each row and "Need n more" with a small bar: it tells the player what to buy and how close it is. Keep the grouping, the keys and the price bar.
- **Greyed and faded for "not yet", cream and dark for "can do now"**: the first look tells you what you can buy (u02, u03).
- **The request card's time bar** (blue, orange when low) and the green flash for met: clear and kind.
- **The one gold card for the banner and the strong notes** is a good sign for "this is a moment": keep gold for moments only (but only one at a time).
- **The slim strip at the Page and Book stages** is a simple honest little card. Keep it, but keep the pause button where it was.
- **The pause cover (u19)**: calm, large, readable, one clear focus ring on "Resume". Keep. The dim "Start a new town" vs lit "Keep this town" in u20 is correct design.
- **The book pages themselves (u17 to u18, u40, u41)**: the printed plan and the summary rows are beautiful and calm; only the size of the print needs to grow.
- **Gold ring on the map for the hint and the hand marker** tie the hint to a place: keep rings on the map, drop the duplicate text.
- **Atkinson Hyperlegible for numbers and Alegreya for headings**: the pairing works (the slashed zeros are consistent).
- **Settings key list (u23)**: tidy two-column rows with key caps, clear "None" fade.

---

## 5. Styles

Counted from the 34 pictures (and cross-checked with `ui-inventory.md`, section 4).

**Card looks: 8 you can tell apart at a glance.**
1. Ledger / request / drive: cream, 1.5 px dark border, no shadow (ledger and request are the same; the drive card is gold-tinted with a 2 px border).
2. Note slip: cream, 1 px border, 26 px high; strong note: gold (Pressed) with 2 px border, 51 px high.
3. Banner: gold, 2.5 px border, serif head 28 plus bold line.
4. Choice card: cream with two "boon" halves with a heading serif line, plus a pulse.
5. Slim strip: cream, 1.2 px border, tiny.
6. Tide card and pave banner: cream, 1.2 to 2 px border, nearly like the ledger but a different margin.
7. Map tip and "Suggested" tag: opaque cream, 1.5 px border, 5 px margin; "+ hand": gold, 4 px margin.
8. Pages and dialogs: big semi-transparent cream (achievements 872 px, age page 640 px), and the leather cover (brown, gold) as a ninth.
Also: five paper opacities (0.94, 0.95, 0.96, 0.97, 0.985) and seven border widths (0.6 to 2.5): many near-identical cards that differ by 0.01 in alpha or 0.5 px in border and so look "almost the same but not" instead of the same.

**Button looks: 5.**
1. Shop bookmark (full card, icon, three lines; hover brighter; disabled faded).
2. Bell, next-age, achievements (full cards, two lines, icon; same family as 1).
3. Pause (small full card, one line, no icon).
4. Tucked tab: "Back to the town [Home]", "Close [Tab]", "Keep playing [Esc]" (flat left edge).
5. Settings: key caps (bold, black outline), pastel side tabs (lilac, blue, cream, tan), and the cover's lettering buttons (gold text, outline only on focus), plus the dialog's blue-framed buttons ("Swap keys"): three more.

**Text sizes (visible, at 1280 x 720): about 9 tiers, only about 5 distinguishable.**
- 27 px gold (floaters), 26 px bold coin (ledger), 28 serif (banner head, scaled to about 24), 19 serif (level, request title; at 1280 about 14 to 15), 12 px floor for everything from 13 to 16 at 1080 (rate, renown, hint, notes, bookmarks, heads, bell, achievements, pause, tide, tips, tags), about 8 to 11 px effective for the book's list, 5 to 7 px for 3D map labels (boat, ford, cottage), 40 and 96 for the cover. At 1080 the HUD has 13 sizes; at 720 the five between 13 and 17 become one or two.

**Number styles: 6.**
1. Gold outlined float "+12" and "+1,030,324" (27 px).
2. Plain bold coin in the ledger ("1,115 coin") with a faded rate ("8,161 a minute").
3. Coin inside text: "4,651 coin: Need 3,535 more" (regular, 12 px).
4. Level as serif "Level 15 of 16" and "of 28".
5. Slim strip "980 coin   Level 16 of 16" (bold, one line, thousands comma, no "a minute").
6. Book rows right-aligned printed numbers ("108,867", "4222" with no comma, "32 min", "70 %") in serif: "Trips 4222" has no thousands separator while "Coin earned 108,867" has: inconsistent. Also the key letters "[Space] [N] [1]" in square brackets, and timers in three styles ("Rests 9 s", "within 55 s", "lowest in 1:54").

Number glyph note: the slashed zero ("0" with a bar) shows in coin, renown and prices; it reads as a "θ" at 12 px ("1,6θθ coin", "40 coin" with a slashed 0), and the floaters use a different 0: two zeros in one screen.

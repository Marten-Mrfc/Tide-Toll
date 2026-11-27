# Book builder (round L3, night 3): progress

Checkout: `/home/user/tide-toll/build/wt/n3-book` (branch wt/n3-book). Resume from here.

## Item 1, speed carried over (built; game-motion being proven)
- `BookMotion.Carry(v, a, d, t)`: the quintic Hermite terms for a start speed and acceleration (v d u(1-u)^3(1+3u) + a d^2 u^2(1-u)^3 / 2); nothing left at t = d. `CarrySpeed`, `CarryAccel`, `SmootherSpeed`, `SmootherAccel`.
- `PauseMenu.Carry.cs` (new): each part (interface, lettering, question, spread, board, models, camera log size, camera pivot) measures its speed and acceleration every frame; a transition that takes over (`turning`) adds the carry to every part's curve, over max(0.35 s, the part's window end), never past the new length. A board turned round while it swings gets a swing of at least 0.5 s (`CarryBoard`).
- `SettingsPages.Turn.cs`: a turn is a segment (start share, speed, acceleration, length). Turned back mid-way: the same leaf, from where it is with its speed reversed, over 0.36 s x (0.9 + 0.3 x share made). A third section chosen: the turn is re-planned from where it is at its speed to land in 1/1.3 of its time left (was: the rate jumped 1.3 x).
- `MotionTest.Checks.cs`: the eased-ends and speed-change checks now also run on the impatient cases (one line); the speed-change check skips the frames where one turn's leaf hands over to the next (each turn's leaf is its own, as the step check already did).

## Item 2, the leaf (built; strips to read)
- `PageLeaf.cs` rewritten: one shader face (trapezoid: the free edge 4.5 % taller as it stands up), front = the page under it read from a BackBufferCopy (`SettingsPages` places `UnderLeaf` between the section being left, now drawn whole, and the section coming), squeezed toward the spine; back = book paper with ruled lines every 24 and a gutter shade; curl shade and highlight along the free edge; shadow strips beyond the edge. Lift = sin of the leaf's angle (arc about the spine).
- The age-change leaf (TownHud, not mine) uses the same PageLeaf (Opaque: both faces paper with lines).

- Strips (00:40): the front carries the page squeezed, the back is ruled paper. Two faults seen and fixed: (1) clipping sections use the back buffer themselves, so the face is now hosted beside the sections (`PageLeaf.Face` reparented into the spread's frame; `OrderForLeaf` puts a BackBufferCopy and the face right before the section coming while the front shows, after it while the back shows); (2) sections had no paper of their own, so the page going (drawn whole) showed through the page coming: each section now carries a pages-only SpreadArt ("Paper").

## Item 3, paper tone
- Measured at the spread view (strips 00:39): 3D page 237.4,229.7,211.9 vs UiKit.Paper 242,234,216 (4.6 apart). `PageLeaf.BookPaper` = 237,230,212 is the one tone of the leaf, the 2D settings pages (`SettingsWidgets` SpreadArt), the age leaf (PageLeaf Opaque) and the modal card. `MotionStrips.PaperCheck`: the 3D page's foot margin before the leaf vs the settings page at rest ("STRIPS paper" lines, bound 4 of 255).

## Item 5, modal card (built)
- `settings/ModalCard.cs`: 480 wide, card style in BookPaper, title, text with three lines' room, buttons in a row below (StyleButton, 32 high, the safe choice bold). The pause question (`PauseMenu`: scrim over the cover, card centred on the window; `QuestionText` is title + text = the old `Question`) and the settings slips (`BuildSlip`: centred on the window; the clash card tries the centre first, then the old places clear of its rows).
- Spread in the game (not alone): margin max(40, 40 s), foot room max(margin, 80 s) for navigation, ribbon room 170 s. Alone (settings test) unchanged (a shorter page scrolled the key under test out of view).

## Item 6, the book in one place (built, pictures running)
- `Main.SetZoomLimits`: the zoom's outer end is the settings spread's view (`PauseMenu.SpreadView`): `Rig.MaxSize` and `Rig.OuterCentre` (CameraRig: the outer-zoom settle and FrameBook use it). game-book passes unchanged.
- Not done: the Page stage's compass/folio and the slim purse over the page title (TownHud rule untouched).

## Pictures (01:18)
- Strips build/motion (third run): STRIPS PASS; paper 0.4/0.5 of 255; table 0.0. tab-forward reads as a leaf (front squeezed, back ruled, new page clean under it). esc-twice: the camera keeps moving outward after the second Esc, then comes back (a visible overshoot to the page).
- ui-audit 1920 (`build/n3/ui-audit/l3-wip`): u18 and u41 book stage: one place, margins >= 66, navigation under the book, no purse. u21 settings with margins, ribbons inside. u25 clash card lay over "Restore defaults" (fixed after the picture: the card also keeps clear of the page's buttons; not retaken). u17/u40 page stage: compass cut at the top, purse over the left page title, navigation over the folio: NOT fixed. u19 caught the book still closing (tour wait under load), u20 not made.
- Pivot check (MotionTest.Checks): the speed-change check of the camera pivot uses the pivot's vector (its distance from the first frame bends where a turned-round path passes near that point: click-settings-on-lettering failed on that artefact).

## Tour
- `UiAudit.Book.cs`: pauses again before the settings states if the u20 question's Esc resumed the game (the layout fixer's proposed fix).

## Not yet
- Item 4 (no new game-motion check for the age change), item 7.
- game-motion: every run under load (load 18 to 26) has failed only on the CPU gate; the shape checks on the impatient cases passed through tab-turned-back and tab-redirected in the runs that got there.

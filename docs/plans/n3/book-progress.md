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

## Final state (01:41)
- ./check (started 01:18, load 20 to 26): game-build, smoke, town, perf, playthrough, river, layout, book, settings, menu passed; game-motion FAILED at 60, 30 and 120 fps on the CPU gate only (9.26, 11.36, 33.08 ms single frames); save and screenshot still running at 01:41.
- game-motion alone, ~90 retries tonight: every one stopped on the CPU gate (6.0 to 1168 ms single frames). Impatient cases reached and passing their new shape checks before the CPU stop: esc-during-close, esc-during-open, back-during-settings-open, tab-turned-back, tab-redirected, tab-flick (and the staged ones). click-settings-on-lettering failed once on the pivot artefact (check changed to the vector, not rerun to there); the cases after it never reached.
- ui-audit 1280 x 720 (`build/n3/ui-audit/l3-wip-1280x720`): u20 the question on the modal card, clear of its buttons; u25 the clash card centred, clear of its rows and of Restore defaults; u23 the key list still cut by its scroll area (a half row at the foot of the left page); u18 the book in its one place with margins and navigation under it.
- Next: the page stage (compass, folio, purse over the title, navigation over the folio), the key list's scroll area, item 4 (age-change pops: the map tag "The market, to buy" vanished in one step as the achievements page opened, in TownHud's code), item 7.

## After the review (fixer, 01:43 to 02:55 UTC)
Review: `l3-review.md`. What was changed (all in the checkout `build/wt/n3-book`):
- MUST 2 (blank leaf, text pops in): `PageLeaf.cs` `BackAlpha`: the leaf's back (and its shadow) fades out as it lands
  (free edge from 60 % to 100 % of the page's width from the spine, smoothstep; about the last quarter of a turn's time),
  so the page that lands shows through before the leaf lies flat. Not for the age leaf (Opaque) or the front.
- MUST 4 (question card text lags its paper): `PauseMenu.cs`: the card is drawn into a `CanvasGroup` ("QuestionGroup")
  under the scrim, so paper, text and buttons fade as one picture (one alpha); the scrim still fades with `_ask`.
- MUST 3 (clash card, two places): `SettingsPages.KeysSaving.cs` `PlaceClashSlip`: one rule at every size: centred on
  the window; only if that lies on a row it names or on Restore defaults/Back, it moves straight up or down from the
  centre to the nearest clear place. (Kept the test that it covers no row it names: disagreed with "cover the rows".)
- SHOULD (two fonts on the card): `ModalCard.cs`: body in the plain sans, both buttons bold sans at the same size, no
  gilt shadow.
- MUST 1 (age leaf a wipe): its path is in `TownHud.River.cs` (not mine). In `PageLeaf.cs` only: for Opaque the curl
  is 150 s wide (was 56), the shadow 110 s (was 44), and the leaf darkens toward its free edge (shade 0.14 x lift).
  Turning it about a spine and carrying the age title need `TownHud.River.PlaceWipe`: left for the messages builder.
- MUST 5 / item 6 (purse over the page title): `TownHud.cs` StageFrame, the one allowed change: the one-line purse
  fades out over sizes SlimSize to 1.08 SlimSize and is hidden beyond (all of the page stage at PlaySize). The tour's
  u17 wait (`UiAudit.Book.cs` line 22) no longer waits for the slim strip. Compass, folio, navigation and the left
  page's margin at the page stage: NOT fixed (needs the page stage's framing, PlaySize, and the tests built on it).
- MUST 6 (esc twice overshoot): not changed (see final answer).
- Strips retaken 01:46 to 02:11 (`build/motion/frames`; interim sheets in the fixer's scratchpad): tab-forward and
  tab-back frames 9 to 10 show the new page coming through the fading leaf, frame 11 and 12 the same page: no cut from
  ruled paper to text. question: frame 4 now shows faint text and buttons with the card, frame 9 the same going out
  (one picture); the card's mid-fade tone over the dark cover is still grey-brown (a light card at part opacity over
  red leather). age-page: a darker band now runs along the free edge (frames 5, 8); it is still a sweep, not a turn
  about a spine; frame 7 shows the shop and the bottom strip drawn OVER the leaf (TownHud order, not mine).
- u17 retaken (`build/n3/ui-audit/l3-fix/u17.png`): no purse over "Dunstead"; compass cut, navigation over the folio,
  left page without margin: unchanged.
- ./check started 02:12 UTC.

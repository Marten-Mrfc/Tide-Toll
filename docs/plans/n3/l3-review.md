# Round L3 (the book): review from pictures

Reviewer read: all 11 sheets in `build/wt/n3-book/build/motion/`, and in `build/n3/ui-audit/l3-wip/` u17, u18, u19, u21, u23, u25, u40, u41, plus the 1280 x 720 set u17, u18, u25 and u20 (`l3-wip-1280x720`). Before pictures: `l1-1920x1080-100` u18, u19, u25. Each sheet is read as a film strip (the frame numbers below are the numbers printed on the sheet).

Not seen: the pause cover at 1920 (`l3-wip/u19.png` is the book still closing, in the page stage, not the cover) and the question at 1920 (u20 was not made). The cover itself is only judged from `pause.png` and `question.png`. The progress file says u25 was "fixed, not retaken": it was not retaken, so that fix is unproven. The picture still shows the fault (see MUST FIX 3).

## 1. MUST FIX, worst first

1. **The age-change leaf is a wipe, not a leaf (`age-page.png`, frames 3 to 9).**
   - Frames 3 and 4: a flat ruled rectangle slides in from the right edge. It is full height, with a dead-straight vertical edge, no curl, no lift, no shadow and no squeeze.
   - Frames 5 to 7: it covers the screen as a plain ruled sheet. Frames 8 and 9: the same sheet slides off to the left.
   - This is exactly what the owner said not to do: "a turning leaf that looks like a leaf, not a wipe".
   - The settings leaf (`tab-forward.png`) does read as a leaf; the age leaf does not.
   - Change: drive the age change through the same trapezoid, curl, front and back faces and shadow as the settings leaf, with the angle swept over the whole screen width. Or, if the shader cannot run that wide, cut the sheet to a real turn about a spine at the screen's right edge.
   - Also: frames 6 and 7 are a full-screen blank. The town is gone for about 3 frames and then returns as the new age, with no content on the leaf. Put the age title and the new-age text on the leaf's front or back so it carries something.

2. **The page leaf lands as a blank ruled page and then the text pops in on the last frame (`tab-forward.png` frames 11 to 12; `tab-back.png` frames 11 to 12; `settings-open.png` frames 11 to 12; `settings-close.png` frames 2 and 3).**
   - Frames 8 to 11: the back of the leaf is a blank ruled page covering the whole left page (tab-forward) or the right page (tab-back).
   - Frame 12: the blank page is replaced at once by the real page ("Display" or "Sound, continued" or "Settings / Sound"). That is a 1-frame cut from ruled paper to full text, the biggest jump in all of the sheets.
   - Change: fade the page's content in under the leaf, over the last 25 per cent of the turn, as the leaf's own back goes transparent. Or put the new page's text on the leaf's back. The page that lands must already be there when the leaf is flat.
   - `tab-back.png` frame 9 also shows the Sound page already complete on the left while the right page is a clean ruled sheet with "On" buttons clipped at its edge (x about 380 to 415). The page is coming in half-finished.

3. **Key-clash card lies over "Restore defaults" at 1920 (`l3-wip/u25.png`).**
   - The card (x 1030 to 1510, y 700 to 928) cuts the button; you read "Restore def". In the before picture (`l1-1920x1080-100/u25.png`) the button was fully visible, so this is worse than before.
   - The progress file says this was fixed; the picture still shows it. Run it again.
   - At 1280 x 720 (`l3-wip-1280x720/u25.png`) the card sits in the middle of the window and covers the key buttons "Up", "Down" and "Num", and the row "A louder bell". The progress file says "clear of its rows"; it is not.
   - **The same card is in two different places at the two sizes**: low on the right page at 1920 and centred on the window at 1280. Decide one rule. One place: centred on the window, like the pause question. The scrim already greys the whole book, so covering the rows costs nothing.

4. **The modal card's text and its fill pop in out of step with the card (`question.png`, frames 3 to 5 and 8 to 9).**
   - Frame 4: a grey translucent block and two button ghosts, with no text. Frame 5: the text appears, still grey. Frame 6: full.
   - On the way out: frame 8 has text, frame 9 has the block and the button ghosts with no text.
   - Text, buttons and card must be one modulate (one alpha for the whole card). Right now the card is a box that fills and then its text arrives; the owner said "no cards that pop in".
   - Also in frame 5 the card is a grey-brown, not paper: it passes through a tone that is not the book's paper.

5. **Page stage (u17, u40, 1920 and 1280): not fixed, and the same faults in every picture.**
   - The purse badge ("835 . Level 16 of 16") lies over the left page's title: "Dunstead" is cut in half.
   - The compass "N" is cut off at the top of the picture.
   - The navigation (Tab, N, Home, Esc) lies over the folio; the page number "2" is cut at the bottom edge.
   - The left page has no margin: its text starts at x 5 to 20 and runs off the left edge. Rows "First coin", "Villagers" and "market right" are cut.
   - These are three overlaps and a cut-off in the one stage the player sits in most often. The progress file lists them as not fixed. Fix them before anything else on the layout.

6. **Esc twice: the camera goes on moving outward after the second Esc and then comes back (progress file, "esc-twice").**
   - The builder saw this and left it. It is a visible overshoot: a stagger the owner will see.
   - Change: when the second Esc turns the motion round, hand the speed over (the carry code is already there for this) so the camera slows and turns without overshoot. Or make the second Esc a no-op while the first is still moving out.

## 2. SHOULD FIX

- **`settings-close.png` frames 1 to 2:** in frame 1 the left page is the "Sound" page; in frame 2 it is the Dunstead page with a ruled leaf standing at the spine. The front of the leaf (the Sound page, squeezed) is never seen. Only the ruled back shows, so the leaf seems to appear from nowhere. Check that the front is drawn on the first frames of the close, as it is in `settings-open.png` frame 10 (the map squeezed on the front).
- **`pause.png` frame 8 to 9:** the white page-block strip to the right of the cover (x 1835 to 1860 in frame 8) is gone at once in frame 9. Let it slide under the cover, or fade it.
- **`settings-open.png` frame 9:** dark ghost tab shapes appear at the right edge of the book before the leaf turns, and are brighter than they should be. They should fade in with the new page, after the leaf has landed, not before.
- **Book stage not centred:** at 1920 the book sits 83 px left of centre, with an empty right side where the ribbons are not drawn (u18). The same at 1280 (40 px left margin, 172 px right). It is "one place", which is the point, but in the book stage the book looks pushed left. Either draw the ribbons' room as a plain strip, or move the book to the centre in the book stage and the settings stage together by an equal amount.
- **Buttons of the question card are in two fonts** (`l3-wip-1280x720/u20.png`): "Start a new town" is small serif, "Keep this town" bold sans, and the body "This town will be lost." is bold sans. The title is serif. Pick one for the card: serif title, one sans body, both buttons the same style and size.
- **1280 x 720 book stage (u18):** the left page's text is 8 to 10 px high (achievements list, boons line). It cannot be read. Raise the minimum size of the book's page text at this window, or drop the achievements list from the left page at 1280.
- **Key list at 1280 (progress file: "u23 still cut by its scroll area", `u25` at 1280):** the left page's key list stops with a half row at the foot ("1" row cut by the scrollbar). A scroll area with a half row at the edge looks like an error; snap the scroll to whole rows or leave room for one more row.
- **The paper:** the leaf back's ruled paper looks right next to the pages (no visible step in `tab-forward.png` frames 7 to 11). The measured difference of 0.4 to 0.5 of 255 is fine. The cover-to-book scrim greys the page to 170 (`u25`), so a modal over the book shows a different paper from the pause question over the cover; accept this, but note it.

## 3. Per sheet: smooth or not, does the leaf read as a leaf

- `tab-forward.png`: leaf motion smooth from frame 3 to 11, then a pop at frame 12 (text on the landing page). Leaf: yes, a real leaf (squeezed front, ruled back, curl at the free edge, shadow). The back is too blank.
- `tab-back.png`: smooth, with the same pop at frame 12. Frame 9 shows an unfinished page. Leaf: yes.
- `settings-open.png`: the zoom and the book opening are smooth (frames 1 to 9); the leaf in frame 10 reads well (map on the front); pop at frame 12. Leaf: yes.
- `settings-close.png`: not smooth at frames 1 to 2 (the front of the leaf is missing, the page jumps). Frames 3 to 8 smooth, the book closes to the cover. Leaf: only the back is seen; it reads as a ruled sheet, less as a leaf than the others.
- `pause.png`: smooth. The zoom out of the town to the page to the closed cover is continuous, and the HUD fades. One pop: the page block at the right of the cover (frame 8 to 9).
- `resume.png`: smooth; the reverse of the pause and the HUD fades back at the end. No pop seen. No leaf (the book opens by the cover).
- `question.png`: not smooth: the text arrives a frame after the card, and leaves a frame before it (MUST FIX 4). The cover fades are smooth.
- `age-page.png`: smooth in motion, but it is a wipe, not a leaf (MUST FIX 1). Frames 6 and 7 are a full-screen blank.
- `age-panel.png`: smooth; the panel fades in whole (frames 3 to 5) and out (frames 9 to 11), text with the card. This is how the question card should fade.
- `achievements.png`: smooth; the card fades in and out as one (frames 3 to 4 and 9 to 10). No pop. Fine.

## 4. Modal cards and the zoomed-out book, both window sizes

- **Pause question, 1280 (u20):** centred on the cover and on the window, nothing overlapping, nothing cut off. Good. 1920 not made.
- **Key-clash card:** overlapping at both sizes and in two different places (MUST FIX 3).
- **Zoomed-out book, 1920 (u18, u41):** in one place, margins at least 66 px, navigation under the book (about 47 px clear), no purse over it. The book is off-centre to the left (SHOULD FIX). u41 (second age) is the same.
- **Zoomed-out book, 1280 (u18):** one place, margins 40 px left, 172 px right, navigation 25 px under the book. Nothing overlapping. The text is too small (SHOULD FIX).
- **Page stage (u17, u40), both sizes:** purse over the title, compass cut, navigation over the folio, left page cut at the screen edge (MUST FIX 5). At 1280 the left page is cut even more.
- **Settings pages (u21, u23, 1920):** nothing overlapping; ribbons inside the window; the key list fits on both pages at 1920. At 1280 the left list is cut by the scroll area.

## 5. What is good

- The settings leaf is a real leaf: a squeezed front, ruled back, curl and shadow, and it was seen turning back and forth cleanly (`tab-forward.png`, `tab-back.png`).
- The paper matches: the leaf, the 2D pages and the modal card are one tone, and no step is visible at the leaf's edge.
- Pause and resume are smooth from the town to the closed cover and back (zoom, HUD and title all continuous).
- The age panel and the achievements card fade in and out as one object.
- The zoomed-out book is in one place at both sizes, with navigation clear under it.

# The lead's notes to agents at work (newest first)

Agents: read this file again whenever your brief says "check the lead's notes". A note names who it is for.

## 21:25 UTC, for the L1 picture runner and the L1 fixer: the tour's staging is repaired elsewhere

The seven states the tour missed or staged wrongly (u21 to u24, u32, u34, u35; u37 retired) were repaired and retaken on the unchanged game in another checkout: `/home/user/tide-toll/build/wt/n3-before` (local branch `wt/n3-before`, commit `0828955`). What each wait is now is written at the end of `ui-audit-progress.md` ("Staging repairs (before pictures)"). The changed files there are `game/tests/UiAudit.Book.cs`, `UiAudit.Age2.cs`, `UiAudit.Age1.cs`, `UiAudit.cs` and `tools/ui-audit.sh` (`git -C /home/user/tide-toll/build/wt/n3-before show 0828955` shows the exact change). **Fixer: take these repairs over into the tour in your checkout** (by hand: your copies differ because of the new layout), so that the after pictures show the same states as the before pictures. Also note from that run: by the second age the towns at 1920 x 1080 and at 1280 x 720 differ (the game runs in real time between states), so before and after are compared at the same window size only.

## 21:10 UTC, for the layout builder and the L1 fixer: from your own picture `l1-wip/u10.png`

It is already much calmer than before. Three things to change:

1. **Shop rows have no frame at rest.** In the picture every row is a box with a heavy ink border and every pill has another: with eight affordable rows the shop is the loudest thing on screen. The shop sheet is the card; rows inside it are divided by air only (sheet section 4: a button's border belongs to hover, 3, and pressed). An affordable pill is solid wheat with no ink border (the fill is the signal); a "not yet" or blocked pill has a 1 px muted border.
2. **Coin numbers need their ink edge and their size** (20, bold): "+8" at the top of the picture can hardly be seen on the grass.
3. **Empty ledger sections:** draw the faint ruled lines (ink at about 8 percent, one every 24) so that blank paper reads as an account book and not as a card that failed to load.

## 21:05 UTC, for the layout builder and the L1 fixer (interface track)

The three audit reviewers have finished (`audit-ui-1.md`, `-2.md`, `-3.md`; each has "the ten worst problems" in its section 3 and "what must be kept" in section 4). They confirm the sheet. What they add to it:

1. **Pages are opaque.** Today the achievements page and the age pages are see-through sheets with the town and coin numbers showing through the text (u15, u31, u38, u39). A page is solid paper (100 percent), the map behind it is dimmed, and its close button sits at its bottom right corner.
2. **Tags are real screen text.** Today's map labels are 3D labels that shrink to 5 to 7 px at 1280 x 720 (boat, ford and cottage labels in the second age). A tag is drawn at caption size on screen at every zoom at which the full interface shows (sheet section 5), never smaller.
3. **Gold means one thing: "this is yours to take now"** (an affordable pill, the banner of a moment, the Ring in the next age button). Nothing else is gold: not the strong notes, not a request that is met (that is moss), not a boat's demand.
4. **Pave's pill shows the cheapest road that can be paved now** ("from"). In u08 the tip at the pointer said 18 while the shop said 21: no tip may show less than the pill.
5. **Navigation never changes corner.** Today Pause jumps from the bottom right to the bottom left at the page stage (u17, u40). Sheet section 2 already says navigation stays: make sure of it at every zoom.
6. **The audit tour itself needs four repairs** (you maintain it now): u21 to u24 (the settings pages) were taken before the leaf had turned, so four of five pictures show the book, not the settings: wait for the settings to be at rest. u32 (ferry, tide, a skipper's request) and u34 (drawbridge and toll at rest) were never made: the wait for "a request" never held for a skipper's request (the first builder's fix is described in `ui-audit-progress.md`). u35 (busiest moment of the second age) was taken at the seal instead of mid-age. u37 does not exist in the game (no drive in the second age): drop it from the list.
7. For the next rounds, not for you (listed so you do not start on them): the pause cover's question running through its button at 1280 x 720, the settings' key list cut by its scroll area and the "That key is taken" box lying on "Restore defaults" (u20, u23, u25); the page stage cutting the compass and the folio, and the book spread standing in three different places (u17, u18, u21 to u24, u40).

## 20:45 UTC, for asset tracks P and W (builders if you read this, and both fixers)

1. Two reviewers ranked every asset: `asset-ranking-1.md` and `asset-ranking-2.md` (merged order in `asset-ranking.md`). For each asset you rework, read its row there: the last column is a concrete rework note (shapes, sizes in metres, palette names). `style-facts.md` has the measured facts (palette in use, outline, sizes, triangle counts).
2. The asset audit tool was finished after your checkout was branched. The copy of `game/tests/AssetAudit.Town.cs` in your checkout predates two fixes: the town and book pictures let the follow view take the camera back to the whole map (so they showed the wrong thing), and the map's own trees stood in front of model subjects. The finished file is `/home/user/tide-toll/build/wt/assets/game/tests/AssetAudit.Town.cs` (and `AssetAudit.cs`, `tools/asset-audit.sh` beside it). Take those fixes over into your copy by hand, keeping your own additions, so that your town pictures are right and the later merge is simple.
3. Track P: the villagers' clothes colours at `TownVillagers.cs:30` are outside the palette; the style sheet's five tunic colours replace them. Loads must not stand higher than about 1 m over the head (today's stacks reach 5.8 m).
4. Track W: the charter stone's pale colour (#F0EADB) is outside the palette. Plot stakes are the heaviest ink on screen ("a prison yard"): if you have time after your list, make them four paper-capped stakes and a pale rope at half the ink weight.

## 19:58 UTC, for everyone

The night's order: interface audit and asset audit first; then the layout sheet and the style sheet; then building.

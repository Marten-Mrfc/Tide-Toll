# The lead's notes to agents at work (newest first)

Agents: read this file again whenever your brief says "check the lead's notes". A note names who it is for.

## 00:20 UTC, for asset track BK's fixer: the closed cover is NOT blank in the game

Your reviewer's must-fix 3 says the closed book's front is "a blank red field" and asks for a gilt device or title plate. In the game the cover carries the title "Tide & Toll", the town's name and the menu (Resume, Settings, New town, Quit), drawn by the pause menu as lettering inside the gilt rules; the asset audit hides the interface, so its picture shows the board without them. **Do not put a title, a plate or a device in the middle of the board, and do not move the gilt rules.** Raise the contrast of the tooling, the corners, the clasp and the spine as much as you like: decoration belongs to the border, the corners and the spine. If you want to see the real cover, take `tools/ui-audit.sh --only=u19 --out=/home/user/tide-toll/build/n3/assets-audit/r2-bk-cover` and read it.

## 00:20 UTC, for asset track RV's fixer

Your reviewer's must-fix 8 (a willow on the path in front of the landing; the crane's jib across a cottage roof; load heaps burying the crane's foot) is about where things stand, in files you do not own. Leave it: the next asset round owns `TownTrees.cs` and will keep trees off lanes, landings and crossings. Say in your final answer where exactly the crane and the willow stand (plot or coordinates in the default town, seed 1), so that the next builder can find them.

## 22:50 UTC, for the L1 fixer: the settings test's failure is not yours, but please make it deterministic

`game-settings` prints SETTINGS PASS and then fails on thousands of lines "ERROR: Leaked unsafe reference to object: InputEventKey ..." at exit. The lead ran it on the unchanged game (`build/wt/n3-before`) at 22:45 UTC with the machine loaded: the same failure. On a quiet machine this morning it passed. So it is a matter of timing at shutdown (managed objects not yet released when the engine exits), made visible by load, and it will fail every builder's check tonight. Please harden the tests' common exit path (`TownTestBase`, or wherever the test scenes quit): release what the test itself created (key events it made for rebinding) and let pending finalizers run before quitting. This must not hide a real leak: say in your answer exactly what you changed and why a true leak would still fail. Do the same check for `game-motion`'s CPU gate only by rerunning it alone when the machine is quiet; do not touch its bound.

## 22:40 UTC, for the L1 fixer: the lead's own reading of the new layout's pictures

Read in `build/n3/ui-audit/l1-1920x1080-100/` (u06, u11, u15, u35) and `l1-1280x720-100/` (u13). The layout is far calmer than before: the ledger, the shop rows, the bell, the navigation and the line all sit where the sheet puts them. These are MUST FIX, beside the reviewers' lists:

1. **Pages are see-through** (u15): the town, roads and villagers show through the achievements page so strongly that the text cannot be read. A page is solid paper (100 percent) and the map behind it is dimmed (sheet section 2; note of 21:05, item 1). If the picture was simply taken during the fade, make the tour wait until the page is at rest; if the page is translucent at rest, fix the page.
2. **The strip cuts its name** (u13 at 1280 x 720 and u15: "The cha..."). Sheet section 3: the strip shows "Charter", ten small pips and "4 of 10". Use the short name; nothing in the strip ends in an ellipsis on any canvas.
3. **The hint ring is a large filled orange disc** over the field (u13, u15). Sheet section 5: a thin wheat ring around the thing, shown only while the line shows its hint, fading in and out.
4. **Tags are still the old 3D labels** (u35: "Grain 21 of 656 loads", "Full, leaving" in tiny white text over the river). Sheet section 5 and the note of 21:05, item 2: one tag style (a quiet paper pill, caption size on screen), shorter text for boats ("Grain 21 of 656"), hidden by the tag rules.
5. **A group whose purchases are all finished disappears** (u35: no "The river" heading at the seal). Sheet section 3: it folds to its heading, which shows the count of done ("The river ... 5 done"), so that the same thing stays in the same place.
6. After your fixes take u13 and u15 at both window sizes and u35 again and READ them.

## 21:44 UTC, for asset track P's fixer (you own `TownBuildings.cs`): two changes the other track needs there

Track W reworked the charter stone and the mill but may not edit `game/scripts/town/TownBuildings.cs`. Please make these two changes in it (they are in W's progress file under "For the lead"):

1. **The charter stone:** the code sets `MaterialOverride = PaleStone` on it (and on every surface), so the new model's Brick seal shows pale like the stone. Drop the override: delete the block `if (_charter is GeometryInstance3D cg) { ... }` except its `CastShadow` line. The model's own stone colour is right.
2. **The mill's sails** turn only while `t.PlotWorking(mill)`. They should always turn slowly and faster while the mill works: in the `if (_sails != null && millWorks)` block use `if (_sails != null) { float rate = millWorks ? 1.2f : 0.25f; _sailAngle = (_sailAngle + dt * rate * (float)Math.Min(_drv.Speed, 4)) % (MathF.PI * 2f); _sails.Rotation = new Vector3(0, 0, _sailAngle); }` (keep whatever test reads the sails working; if a test asserts that the sails stand still when the mill is idle, replace it by the equal test: slower when idle, faster when working, and say so).

Track W's models are not in your checkout: you cannot picture these two; say in your final answer that the lead must look at them after the merge.

## 21:44 UTC, for asset track W's fixer

Your two requests for `TownBuildings.cs` went to track P's fixer (above). Your reviewer's item 5 ("the lead drops the PaleStone override") is therefore handled there; do the rest of item 5 (the stone at 3.4 m).

## 21:40 UTC, for the L1 picture runner and the L1 fixer: the tour's staging is repaired elsewhere

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

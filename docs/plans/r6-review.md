# What round 6 must fix (the lead's list, the last round of the night)

Source: the screenshot tour of both ages. Its pictures are in `/home/user/tide-toll/build/wt/tour/build/tour/<id>.png` with the notes of the agent who looked at each in `/home/user/tide-toll/build/wt/tour/build/tour/shots.json` (field `wrong`). Those pictures were taken one build ago: round 5 has since added the choice card, the charter drive, worth on shop lines, hand markers, touches, lanes drawn like roads, shorter boat labels and both ages on the book's pages. Look at the picture named with each item, check whether the fault is still there in the present build, fix it, take the picture again and READ it. The owner will judge the game from exactly these pictures in a few hours.

Two agents work on this list at the same time in different checkouts, one on each part. Stay in your part's files so the two merge cleanly: do not rename, move or reformat anything.

## Part W: the world (drawing of the town, the river and the book)

Files: `game/scripts/town/TownBuildings.cs`, `TownVillagers.cs`, `TownGround*.cs`, `TownTouches.cs`, `River*.cs`, `BookCover.cs`, `game/scripts/BookBuilder.cs`, `CameraRig.cs`, `Main.cs`, shaders, and their tests. One small simulation change is allowed (item W12).

1. **No charter stone is seen on the bell green** at the charter or afterwards (`e02-town`, `e03-charter`): the moment is only a banner. The stone must stand there, large enough to see at the normal view, from the charter on, and the seal stone likewise in the second age.
2. **A grey see-through rectangle lies across the river** in the first age and at the ford's row (`c07-mill`, `e02-town`, `f04-ford`). Find what draws it and remove it; the ford is its stones and the shallows, and only once the ford exists.
3. **People are drawn through things**: queued villagers through the mill's body and sails (`c07-mill`), through the landing's crates (`f03-landing`), and through each other when a road is crowded (`d06-crowded`). Queues stand beside the building along the lane, never inside a model; on a crowded road people spread a little across the road's width so a crowd reads as a crowd.
4. **The ferry's raft is drawn at the far post while the simulation has it in mid-river** (`f06-ferry`). The raft and its riders are drawn where the simulation says, every frame.
5. **A new road is almost invisible** (`a01-first-minute`). A just-given trail must be faint but plainly there at the normal view.
6. **A bigger load looks the same** (`b02-pace-and-load`). Each Load step shows on the villager: a fuller basket, a second bundle, a handcart-sized pack.
7. **One and two extensions look alike, and extended cottages read as dark boxes from above** (`b05-extended`, `e02-town`). Make the three sizes plainly different at the normal view, and lighten what reads as a dark box.
8. **Pave mode's highlight is a pale blue that is hard to see on worn earth** (`d03-pave-mode`), and the crowded road's tint is weak (`d06-crowded`). Stronger, clearly different colours for "can be paved" and "crowded".
9. **The page wipe at the age change** shows the old view's cards and labels through a pale wash on the right half (`f01-ring`). The turning page covers what is behind it.
10. **The closing book shows only the right page and the board** (`g05-closing`): the left page with the summary should be there until the cover comes down.
11. **Godot crashed twice during tour runs** on this machine's software renderer (after `g05-closing` and at `f02-age-page`). If you can find a cause in the game (a node freed while in use, a texture or mesh released while still drawn, something done during the book's closing or the page turn), fix it; if you find nothing in ten minutes, say so and move on.
12. **The hint says "A road is crowded. Pave it" with two villagers on the map** (`c01-level-banner`). The hint should speak of crowding only when people are really slowed for a while. This is in `sim/Town/` (the hint): a small change with a test is allowed.

## Part U: the interface (cards, labels, numbers, pages of settings)

Files: `game/scripts/town/TownHud*.cs`, `TownSummary.cs`, `PauseMenu.cs`, `game/scripts/settings/**`, `UiKit.cs`, and their tests.

1. **Coin numbers are drawn twice, with a faded copy above each** (`b03-shop-lines`), are small, and sit on villagers' heads, on cottage doors and on the lit cottage in Extend mode (`a02-first-coin`, `b04-extend-mode`, `b05-extended`). One number per delivery, clear of the building and the person, never over the thing a mode is pointing at.
2. **The hint line is cut off by the request card** (`b03-shop-lines`). The left column (ledger, hint, request or drive, notes) is laid out so nothing overlaps or is cut at any moment, in both ages, at interface sizes 100 and 150 percent.
3. **A level's banner covers what the picture is about** (`b08-hand`, `f03-landing`, `f06-ferry`, `f07-drawbridge`), and stays a long time. Banners are shorter-lived and sit where they cover neither the gift nor the middle of the view.
4. **Labels lie under or over cards and each other**: a plot label under the shop card at the right edge (`c02-staked-plots`), through the edge of a page card (`f02-age-page`), over the river and the landing (`f06-ferry`); boat cargo labels over each other, over the bridge and the keeper's hut (`f05-pasture`, `f07-drawbridge`). Labels keep clear of cards (move or hide near them), never overlap each other, and never show through a page.
5. **In the second age the shop covers the river and its river group is squeezed into the bottom right** (`f08-river-shop`). The shop must stay readable with all its groups: collapse finished and locked lines, fold groups, or scroll inside the column, so that the map's middle stays free.
6. **At the page stage the left page's row names are cut by the screen edge and the slim strip is drawn over the map's title** (`g03-zoom-2`).
7. **At interface size 150 percent the shop cards overlap the achievements card and the group headings sit under the cards** (`h05-big`). Everything fits at 80, 100 and 150 percent at 1920 x 1080 and at 1280 x 720.
8. **While the bell rings its card says nothing** about the time left or the rest to come (`b07-bell`).
9. **The key clash box covers the very rows it names** (`h03-keys`). Place it clear of them.
10. **After the borough seal the left page of the book** must say the second age, count the river cottages, and list both ages' achievements (`g04-book`): round 5 worked on this; check that it is right now.
11. The price tip in Pave and Extend modes is drawn over villagers and the cottage roof (`d03-pave-mode`, `b04-extend-mode`): place it beside the pointer, clear of the target.

## For both

- Each fix that can be checked by a test gets one, in the test scene where it belongs.
- Take your pictures with the quick modes of the screenshot step or with `tools/tour.sh --only=<id,id>` once the tour's files are in your checkout (they are new files: `game/tests/Tour*.cs`, `game/tests/tour.tscn`, `tools/tour.sh`, `docs/plans/tour-handover.md`).
- If time runs short, do the items in the order given and say exactly which are left.

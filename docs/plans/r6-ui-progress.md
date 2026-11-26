# Round 6 UI progress

Started: Thu Nov 26 03:11:22 UTC 2026 (time box 40 min)

## Done

## Notes
- 03:19 code for all 11 items in (see below); tour retake of the 18 shots started (build/tour).

## Decisions (per item)
1. Coin numbers: a delivery within 1.2 s at the same spot adds to the rising number (no faded copy); font 24 to 30; anchored 9 m up and 1.5 cells north of the door, 44 px higher on screen; hidden while Pave or Extend mode is on. (TownHud.cs)
2. Left column: ColumnFrame() stacks ledger, request, drive, notes once a frame, last, from minimum sizes; the ledger shrinks when the hint goes. (TownHud.cs)
3. Banner: 6 s to 3.5 s, smaller type; top edge, or bottom edge when the gift is under the top place; never just below the gift (middle). (TownHud.Banner.cs, TownHud.cs)
4. Map labels (stake "What", boat "Wants"): new TownHud.Labels.cs fades (Transparency) any that touches a card or a label already kept (boats first); all fade while a page is open. The owners' files (TownBuildings, RiverLayer, RiverBoats) are not mine, so labels are faded, not moved. Not handled: labels over the bridge or the keeper's hut (3D things, not cards).
5. Shop: finished and locked lines fold to one short line, smaller (13) with a narrow icon.
6. Page stage: the summary's row names start at x -80 (was -112), achievements columns -84..-4 and 8..114 (was -114..-6, 6..114); the slim strip (and the pause mark that follows it) sits top left. The camera framing is not mine (CameraRig).
7. 150 %: the shop's room ends at the achievements card (was screen bottom - 80); first step smaller bookmarks (existing), second step drops the worth lines (worth moves to the tooltip).
8. Bell card while ringing: "Ringing: N s left, then rests M s".
9. Key clash slip: placed where it crosses neither named action's rows (right page foot, then page middles, bottoms, tops); re-placed 0.02 s later once the page is laid out. (SettingsPages.KeysSaving.cs)
10. Book after the seal: age line follows InRiverAge; new row "River cottages"; the next line says the seal ("The borough seal is the town's. Both ages are complete.") or the river's next gift, not "The river comes next"; achievements list both ages (first age, a line "On the river N of M", the river's), type set smaller when a column has more than 12 rows. (TownSummary.cs)
11. Price tip: beside the pointer past the hovered cottage's screen box (right, or left near the edge); in Pave mode above-right of the pointer. (TownHud.Extend.cs, TownHud.Pave.cs)

## Pictures read (tour retake 03:23-03:42, build of about 03:23; later fixes not in these pictures)
- a02: one "+3", bigger, clear of villager and buildings. b03: column stacked, hint whole; a faded second "+31" still showed (merge window was 1.2 s): fixed after (merge while any number shows at the spot), not retaken.
- b04: tip beside the cottage, no coin number; the boon choice card lay over the Extend banner: fixed after (card under the mode banner), not retaken.
- b07, f05, f06, h05: bell card "Ringing: N s left, then rests M s"; "rests 0 s" reads badly: now "ready again after", not retaken.
- b08: banner smaller, top, gift marker clear. c02: plot label clear of the shop.
- d03: Pave tip clashed with "Suggested" label: fixed after (tip goes under the pointer), not retaken.
- f02: stake labels showed beside the age page: the river layer's labels were missed (cached null owner) and Transparency does not hide a Label3D: fixed after (Visible, owners re-taken), not retaken.
- f08: shop compact, all groups including the river group in the right column, river and middle free.
- g03/g04: second age, River cottages 4, seal line, both ages' achievements (36 of 40). Faults seen and fixed after, not retaken: 11 rows ran into the seal line (rows now set at 450 in the river age), boons wrapped into the heading (wider), slim strip top left covered the town's name (now bottom left), achievements type small (min 400).
- h03: clash slip at the right page's foot, clear of every row. h05: shop above the achievements card, headings visible.
- b05 not made: "the cottage has 1 extensions after two clicks" (the known flaky second click).
## Tests (final build 03:42): game-settings, game-book, game-menu, game-town PASS.

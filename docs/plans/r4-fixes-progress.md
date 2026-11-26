# Round 4 fixes progress (part A of r4-review.md)

Started 2026-11-26 00:05 UTC. Time box ~45 min.

## Done
- read the brief and the reports.

## Next
- A1..A19 in order.
- 00:20 code for A1, A2, A3, A5, A6, A7, A8, A9, A10 written and building (tests next). A4 was already done in round 3 (tt_next_age N in GameInput.cs, on the Keys page, clash check by the shared store); a test is added.
- 00:33 A15 (CS8604) and A16 (tilt 50 deg, eased out from MinSize) done. Tests written: MenuTest.KeysUnderTheCover (A1, A2) and the New town clean check (A8), TownTest.CardsStopClicks (A3), SaveTest (A5 Delete by clicks, A6 autosave clock, A7 pause then unclean end, A9 failed write/read), Smoke (A10 slow frame, log caps), BookTest (A16 first notches tilt). Running them.
- Next: A4 test (Keys page lists tt_next_age, clash), A11-A14, A17-A19.
- smoke, save, menu, settings passed with the new tests (menu after making the Extend setup pause the town). book passed with the A16 checks.
- A4 test (SettingsTest.Keys), A11 (Alegreya summary printed from a plain mipmapped atlas, not MSDF), A12 (pause control a whole card, beside the slim strip at page/book), A13 (both question choices framed), A14 (Keys rows 6 px apart, columns 13/12, whole-row scroll windows) written; tests in BookTest, MenuTest, SettingsTest.Keyboard. Running settings and book.
- Next: A17 extended cottages, A18 banner clear of gift, A19 pale stakes; then town test, screenshots, full ./check.
- 00:18 (machine clock) settings PASS with A14 checks. A17 (lean-to, yard, woodpile on extended cottages), A18 (banner moves below the gift when the gift is under its place), A19 (pale stakes material) written with checks in TownTest. town test and town-quick screenshots running.
- Left: read screenshots (extend, level, summary, keys, question), rerun town, full ./check, AGENTS/README notes, sim-wishes if any.
- Screenshots read: extend (lean-to shows beside the extended cottage; stakes pale in the core but the ink outline pass keeps their thin posts and cord dark: not fixable without art or an outline mask), keys (13/12 rows, no scroll), new-town question (both framed). AGENTS.md has a round 4 section.
- town test: fixed my own test (PlaceUnder must keep the view still: UserMoved). Rerunning; age-only shots running.
- town PASS (cards=ok bring=ok). Shots read: summary (clean numbers, pause control beside the slim strip, page numbers clear), level-3 (banner clear of the field), keys, question, extend. All 19 items done. Running the full ./check (log in scratchpad check.log).
- 00:3x full ./check PASSED (all 17 steps). End-of-age shot read: twice-extended cottages show the wattle yard as a dark bracket beside the house; the lean-to reads in the extend shot, less clearly at the end of the age (the main model is bigger there).

## State: finished (part A, 19 items). A successor only needs to verify.
- Art missing: no chimney model (A17 used a lean-to = cottage-01 turned side on, the wattle fence and prop-woodpile instead). Pale stakes (A19): the material is pale, but the screen-space ink outline keeps thin posts and the cord dark; a thicker pale stakes model or an outline mask would be needed.
- Changed assertion: SaveTest used to call LoadLast() after DeleteSave and expect success (it loaded the new town saved at once); with A5 there is no save after Delete, so it now expects LoadLast() to fail, and LoadLast() succeeds after the next pause saves.

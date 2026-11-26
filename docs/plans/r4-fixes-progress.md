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

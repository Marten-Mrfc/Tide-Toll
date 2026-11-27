# Asset round 3, track IC (icons): progress

Checkout: `/home/user/tide-toll/build/wt/n3-art1`. Spec: `style-sheet.md` section 8.

## How to resume

- Generator: `python3 assets/src/2d/icons-n3.py [prefix ...]` writes `assets/icons/{shop,boon,perk,milestone,ach,msg,nav}-*.svg` (106 files) and their rows in `assets/LEDGER.csv`; then `tools/sync-assets.sh`. It sets `sys.dont_write_bytecode` (no `__pycache__` from it).
- Vocabulary: `assets/src/2d/iconkit/draw.py` (48 x 48, Ink line 3, round ends, one palette colour; `group`/`place` scale a glyph and keep the line at 3), `glyphs.py` (shared drawings), sets in `shop.py` (shop, steps, offers, boons, perks, milestones), `ach.py` (40 medals), `msg.py` (messages, navigation, tide).
- Contact sheets: `python3 tools/asset-audit/icon_sheets.py DIR` (new; Godot's own SVG renderer, grouped by kind, 24+48 and 192, plus all at 24 side by side) -> `/home/user/tide-toll/build/n3/assets-audit/r3-ic/`.

## Done

- [x] 1. Generator and vocabulary (01:27 UTC).
- [x] 2. Every purchase: shop-villager, hand, pace, load (+ load-basket, load-yoke, load-handcart), tools, value-water/grain/flour/wool/cloth, regulars, pave, extend, bell, belltrack, building (+ building-bucket, -stall, -field, -mill), nextage, ferry, raft, rope, berth, crane, boatprices, toll, winch.
- [x] 3. Boons (8: boon-wider-lanes, -stronger-backs, -deeper-well, -longer-rows, -third-room, -faster-hands, -market-right, -millers-guild), perks (perk-good-neighbours, -light-feet, -masons-eye), milestones (milestone-pace, -tools, -value).
- [x] 4. 40 achievements in one scalloped medal (wheat: first age; river blue: second age), glyph in Paper.
- [x] 5. Messages (msg-level, charter, drive, boon, age, seal, achievement, building, milestone, perk, touch, trouble, hint), navigation (nav-achievements, page, home, pause, bell), tide (nav-tide-high, nav-tide-low).

## For the lead (names the code does not ask for yet; files exist under these names)

The interface today asks only for `shop-<kind>`, `shop-value-<good>`, `shop-bell` and `nav-*`. Wishes, exact:

1. `TownHud.ShopIconName`: BellTrack should be `"shop-belltrack"` (today Bell and BellTrack both ask `shop-bell`, so the louder bell shows the plain bell). Building should be `"shop-building-" + offer` with offer from `OfferKind` (SecondBucket -> `bucket`, MarketStall -> `stall`, SecondField -> `field`, Mill -> `mill`); today all four ask `shop-building`. Load should be `"shop-load-" + TownRules.LoadNames[step].ToLowerInvariant()` (`basket`, `yoke`, `handcart`); today all ask `shop-load` (a basket).
2. `TownHud.BoonIcon`: `"boon-" + kebab(Boon)` (`boon-wider-lanes`, `boon-stronger-backs`, `boon-deeper-well`, `boon-longer-rows`, `boon-third-room`, `boon-faster-hands`, `boon-market-right`, `boon-millers-guild`); today it asks the old shared names.
3. Achievements: `"ach-" + Id.Replace('_', '-')` for the first age (`ach-first-coin` ... `ach-rate-300`), and for the river `"ach-" + Name` in lower case, apostrophes dropped, spaces as `-` (`ach-first-mooring`, `ach-weavers-pride`, `ach-skippers-friend`, `ach-raise-the-bridge`, ...). Nothing asks for them today.
4. Notes and banners by kind: `msg-level`, `msg-charter`, `msg-drive`, `msg-boon`, `msg-age`, `msg-seal`, `msg-achievement` (or the achievement's own `ach-*`), `msg-building`, `msg-milestone` (or `milestone-pace|tools|value`), `msg-perk` (or `perk-*`), `msg-touch`, `msg-trouble`, `msg-hint`. Nothing asks for them today.
5. Tide dial: `TownHud.River.cs` asks `ui-tide-high` / `ui-tide-low` (old 24-unit set); the new ones are `nav-tide-high` / `nav-tide-low`.
6. The achievements page (`TownHud.Book.cs` / `TownHud.Pages.cs`) draws no icon per achievement today (u15: names, lines and bars only). To show the medals: `IconOr(achKey, "ui-achievement")` beside each name, 24 on screen.
7. Locked / finished rows: `ui-lock` and `ui-check` exist (old style) and are still unused.

## Pictures

- Sheets: `/home/user/tide-toll/build/n3/assets-audit/r3-ic/` (`icons-<group>-24-48.png`, `icons-<group>-192.png`, `icons-all-24.png`).
- In the game: `/home/user/tide-toll/build/n3/ui-audit/r3-ic/` (1920 x 1080) and `r3-ic-1280x720/`. Note: new SVGs need `godot --headless --path game --import` after `tools/sync-assets.sh` before a picture run, or `IconOr` finds no file and shows the stand-in (my first run at 01:30 showed only old icons for this reason).
- Read: u10 (shop at level 8): Villager, Extend, Pace, Yoke (shows the basket: wish 1), Tools (hammer), Water price (drop + coin), Grain price (ear + coin), Pave, Louder bell (plain bell: wish 1), Regulars, all new; nav-bell, nav-achievements, nav-page, nav-home, nav-pause new. u15: achievements page has no icons (wish 6). u03 (level 2): Villager, Pace, Water price, Pave, Louder bell, Regulars new. u33 (level 18, river): Flour price (sack + coin), "Better boat prices" (boat + coin) new. u10 at 1280 x 720: the icons are about 16 px there; Grain price's ear and coin blur into one smudge, the coin badge on the price lines is barely a dot.

## Still open

- u39 at 1920 x 1080 not taken: the picture run (u03..u33 made) was stopped by the session's 30-minute limit for a background job before it reached the seal; u39 exists at 1280 x 720 only. There it was taken mid-transition (the seal page over the river's achievements page, both see-through): staging of the tour, not icons.
- The icons are not yet asked for by name for steps, offers, the louder bell, boons, achievements, messages and the tide (For the lead 1-6): the files exist, the code must ask.
- At 1280 x 720 (icons about 16 px) the price lines' coin badge is very small.
- ./check at 02:31 UTC (started 01:44, machine load about 49): sim-build, sim-test, game-build, game-smoke, game-town, game-perf, game-playthrough, game-river, game-layout, game-book, game-settings, game-menu, game-save passed; game-motion FAILED on the CPU gate only ("pause: frame 30 took 8.58 ms of CPU (gate 6)" at 60, "frame 32 took 12.79 ms" at 30, "frame 11 took 10.52 ms" at 120); screenshot and ledger steps still running at the deadline (log: /home/user/tide-toll/build/n3/r3-ic-check.log). `python3 tools/check-ledger.py`: 437 rows, 411 files, 0 problems.

## After the review (fixer, 02:33 to 03:35 UTC)

Generator: new shapes in `assets/src/2d/iconkit/glyphs2.py`; `shop.py`, `ach.py`, `msg.py` use them; `msg.py` has `ALIASES` + `today()`, which write the new drawings also under the names the interface asks for today (group "Today's names" in `icons-n3.py`). Run as before: `python3 assets/src/2d/icons-n3.py && tools/sync-assets.sh && godot --headless --path game --import`. 119 icons, 119 ledger rows. Sheets after: `/home/user/tide-toll/build/n3/assets-audit/r3-ic-after/`.

Must fix:
1. Wire the new names into the game: game code, not mine (brief). Done instead: files under today's names: `ui-tide-high`/`ui-tide-low` (the dial asks these) = the new tide icons; `TownHud.BoonIcon`'s eight names (`up-pave`, `up-pace`, `good-water`, `good-grain`, `ui-extend`, `ui-tools`, `ui-value`, `good-flour`) = the eight new boons (those names are asked only by `BoonIcon` and as never-shown shop stand-ins); `shop-load` redrawn as a neutral load (three bundles), so the Yoke row no longer shows a basket; `shop-building` neutral (house on a plot, plus badge). Bell/BellTrack both ask `shop-bell`: one name, one picture; stays the plain bell. Wishes below.
2. Medals on the achievements page: game code; wish below.
3. Medal glyph bigger: done (glyph box 32 -> 38 of 48, scallop thinner: 18 points, 23.5/22.4).
4. Twins: done: first-pave (slab and trowel) / trips-1000 (rows of footprints); villagers-5 (three in a row) / -10 (two rows, 3+4); ten-holds (stacked crates) / fifty-holds (heap with a flag) / full-hold (sacks) / river-trade (boat with a coin on the sail); all-paved (square of slabs on the diagonal, no cross); level-3/6/9 (sprout, young tree, full tree); raise-the-bridge (two towers, leaves raised on chains); dry-shoes (boot on a plank over water) / wet-feet (boot standing in water); shop-load / shop-load-basket; shop-building / shop-extend (wing as wide as the house, lower roof).
5. Coin badge removed from shop-value-* (bare good); boatprices: boat with a coin on the sail instead of a badge.
6. Cloth (shop-value-cloth, ach-weavers-pride): rolled bolt, spiral end, loose flap, no stripes. Flour: pale (Limewash) sack, tied neck, dark band, no horns.
7. Milestones (milestone-pace/tools/value, msg-milestone): squat roadside stone with a slanted top on a mound, glyph on its face (msg-milestone: an arrow).
8. msg-drive: scroll with four boxes, two filled (ten ticks do not fit at 24). msg-seal: wax blob with a pressed star and two ribbon tails. nav-tide-high/low: big arrow up/down, water near the post's top / at its foot.

Should fix: nav-home (roof over the crosshair), boon-wider-lanes (road with arrows outwards), boon-millers-guild (mill with a flour sack), boon-stronger-backs (bigger arrow), boon-faster-hands (lines longer), perk-light-feet (boot with a wing), shop-winch (drum, two rope turns, crank, legs), shop-tools (heavy hammer over a saw), shop-toll (heavy bar, hanging coin), shop-berth (post with rope loop, a boat's bow), ach-borough (three roofs under a flag), ach-first-coin (square-holed coin), ach-first-mooring (post with rope and cleat), msg-age (bookmark ribbon, so it differs from shop-nextage): done. Old set: `ui-coin` (every price), `ui-lock`, `ui-check` redrawn in the new style under their names. Not done: the rest of the old `ui-*` set (pause, play, speed, boat, ...), wiring lock/check (game code), the "Better boat prices" label (game text), a 16 px sheet.

### For the lead (after the review)

8. The old generators also write some of these names: `icons-town.py` (`up-pave`, `good-water`, `good-grain`, `good-flour`, `ui-lock`, `ui-check`, ...), `icons-river.py` (`ui-coin`), `icons-age2.py` (`ui-tide-high`, `ui-tide-low`). Rerunning them would put the old drawings back; run `icons-n3.py` last, or drop those names from them once the interface asks for the new names (then delete the aliases in `iconkit/msg.py` `ALIASES`).
9. Wishes 1-7 above stand, exactly as written. With wish 2 (`BoonIcon` -> `boon-*`) and wish 5 (`nav-tide-*`) done, the aliases can go. Wish 1 matters most: `ShopIconName` BellTrack -> `shop-belltrack`, Load -> `shop-load-<basket|yoke|handcart>`, Building -> `shop-building-<bucket|stall|field|mill>`. Wish 6: the achievements page, `IconOr(achKey, "ui-achievement")` at 24 beside each name.
10. Wish 7, exact: a locked shop row `IconOr("ui-lock", ...)`, a finished one `IconOr("ui-check", ...)` (both now new style).

### Pictures after the fixes (read)

- Sheets: `/home/user/tide-toll/build/n3/assets-audit/r3-ic-after/` (JPEG copies: `docs/review/n3/after/assets/icons-*.jpg`).
- Game (1920 x 1080): `/home/user/tide-toll/build/n3/ui-audit/r3-ic-after/` u06, u10, u33 (u32 not made by the tour this run). u06: the choice shows the new Wider lanes and Stronger backs icons (through the alias names). u10: Yoke row now shows the three bundles, not a basket; Second bucket shows the house on a plot with the plus; every price pill shows the new square-holed coin; Home shows the roof over the crosshair. u33: Flour price is the pale sack with the dark band; Better boat prices is the boat with a coin on its sail.
- Reviewer wrong on one point: the tide card is a dial drawn by code (`TownHud.River.cs` `DrawDial`); its `_tideIcon` (ui-tide-high/low) is built with `Visible = false` and never shown. Not a stand-in. The new tide files are ready if the interface wants them.

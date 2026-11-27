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
- Read: u10 (shop at level 8): Villager, Extend, Pace, Yoke (shows the basket: wish 1), Tools (hammer), Water price (drop + coin), Grain price (ear + coin), Pave, Louder bell (plain bell: wish 1), Regulars, all new; nav-bell, nav-achievements, nav-page, nav-home, nav-pause new. u15: achievements page has no icons (wish 6).

## Still open

- (filled in at the end)

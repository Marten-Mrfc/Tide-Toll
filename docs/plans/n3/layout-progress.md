# The layout (round L1 of night 3): progress

Builder of the layout, checkout `build/wt/n3-ui` (branch wt/n3-ui). Spec: `layout-sheet.md` version 2.

## Where things live (game/scripts)

| What | File, class | Stable names (node paths under `Hud/Root`) |
| --- | --- | --- |
| Styles: four type sizes, colours, card variants, button states, bar, key cap, `U(s, units)`, `Type(s, size)` | `UiKit.Styles.cs` (`UiKit` is now partial) | style boxes carry meta `ui_card` = Card / Quiet / Accent / Trouble / Button* / Pill |
| Zones (pure functions) | `HudZones.cs` (`HudZones(canvasPx, s)`: Ledger, Purse, Goal, Strip, Note, Bell, Tide, Shop(content), ShopMax, NavButton(0..3), Nav, Message(h), Banner, Line, Middle, Page, Modal, TipRect) | |
| Ledger: one sheet, four sections | `town/TownHud.Ledger.cs` | `Ledger`, `Ledger/Purse` (Coin, Rate, Level, Renown, LevelBar, Next), `Ledger/Goal/Request` (Who, Ask, Done, Bar, Reward), `Ledger/Strip` (NextAge, Drive), `Ledger/NoteSection/Note`; zoomed out: `Slim` |
| Shop | `town/TownHud.Shop.cs` (data, keys, row content), `town/TownHud.ShopRows.cs` (card, fit, slide, hold, pulse, wheel, folds), `town/ShopRow.cs` (one row, pill, `DashFrame`) | `Shop`, `Shop/Title`, `Shop/ToBuy`, `Shop/Body/Group<Name>` (Name, Folded), `Shop/Body/Row<id>` (Key, Icon, Name, Step, Line2, Pill/Price) with id = kind*1000 + index+1 (Pave, Extend: kind*1000) |
| Timers and navigation | `town/TownHud.Timers.cs` | `Bell` (State, Key, Bar), `Tide`, `Achievements`, `AgePage`, `HomeMark`, `PauseMark` |
| Tooltip | `town/TownHud.Tip.cs` | `Tooltip` |
| Message and Line zones | `town/TownHud.Zones.cs` (`ZoneFrame`, `LineFrame`), `TownHud.Banner.cs`, `TownHud.Choice.cs` | `Banner`, `Choice` (BoonA, BoonB), `Line` |

## Status

(updated after each item)

### 20:58 UTC

- 1 UiKit styles: done (`UiKit.Styles.cs`). Play interface sizes come from `UiKit.U`/`UiKit.Type`; old `UiKit.Card`/`Tab`/`FontPx` remain for the book, pause and settings (not changed).
- 2 HudZones: done (`HudZones.cs`).
- 3 Ledger: done (purse complete; goal = request card content; strip = Ring in the next age, else the charter drive in one line with ten pips; note = one at a time, FIFO queue of at most 12, a waiting note shortens the shown one to 4 s).
- 4 Shop: done (heading, groups with folds, 56/40 rows by fit, scroll, pill states, wheel rule, fade/slide, pointer hold per change, 0.3 s click calm, pulse). Building offers take F, G, H in the order of the rows (locked offers are no rows).
- 5 Timers and navigation: done. 6 Tooltip: done (rows, heads, bell, tide, nav, purse; engine TooltipText removed there).
- 7 Message and Line: done (banner Message zone, choice Message zone 200 high, a banner raised while the choice is open or zoomed out waits (newest wins); mode text and hint in the Line).
- 8 Pages: card style, Page zone width, 3 columns when wide.
- 9 Zoomed out: slim purse in the purse's place; book in view: only navigation.
- 10 game-layout: `game/tests/LayoutTest.cs` (subclass of UiAudit: same state ids), `LayoutRules.cs`, `LayoutFixture.cs`, `layout.tscn`; step in `tools/check.sh` after game-town. The HUD is rebuilt per canvas by `TownHud.ForceCanvas(canvas, s)`.
- Left: run whole checks; pictures.

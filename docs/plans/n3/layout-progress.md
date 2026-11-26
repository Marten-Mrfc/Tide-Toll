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

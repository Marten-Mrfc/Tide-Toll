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

### 21:20 UTC: lead's notes of 21:05 and 21:10 applied

- Rows have no frame at rest (`UiKit.StyleRow`: air; hover border 3; pressed darker); affordable pill solid wheat, no border; not-yet and blocked 1 px muted (blocked dashed).
- Coin numbers 20 bold wheat with a 6-unit ink edge. Faint ruled lines in the ledger (ink 8 percent, every 24; `RuledLines` in TownHud.Ledger.cs).
- Pages opaque (paper 100 percent), close button at the bottom right, a scrim (`Scrim`) under every zone dims the map only while a page shows.
- Gold: the drive's pips and achievement progress are river; done is moss.
- Pave's pill: the cheapest road that can be paved now ("from"); the word takes the coin's place in the pill.
- Navigation never changes corner (it stays bottom right at every zoom). Not done from the notes: tags as screen text (item 8, map), the audit tour's four repairs (u21-u24 wait, u32/u34 skipper request, u35 mid-age, drop u37).

### Tests changed (old assertion -> new assertion, why)

| Test | Old | New | Why |
| --- | --- | --- | --- |
| Smoke.cs | `CoinText.EndsWith(" coin")` | `CoinText == Money(floor(Coin))` | the purse shows the amount beside the coin sign, never the word "coin" (sheet 4) |
| TownTest.cs BellByKey | bell button text contains " s left, then rests " | `BellTipText` contains it | the bell shows "7 s" over a bar; the whole sentence is its tooltip (sheet 3) |
| TownTest.cs LayoutChecks | ledger and request card do not overlap | request rect is the ledger's goal section (`HudZones.Goal`) and inside the ledger | the request is a section of the ledger now (sheet 2) |
| TownTest.cs CheckKeysUnique | keys parsed from "[k]" in the bookmark text | `RowKey(i)` unique | rows carry the key in a key cap, no text button |
| TownTest.cs CheckRequestCard | reward contains "N coin" | contains "Reward: N" | no word "coin" with amounts (sheet 4) |
| TownTest.cs level gifts | banner never covers its gift | banner rect is the Message zone | a zone never moves (sheet 2, critique 2 "dies with the fixed zone") |
| TownTest.cs ClickMark, TownTest.V3 hand | click at once | wait `RowCalm(i)` (at most 2 s) then click | a row that moved in the last 0.3 s takes no click (sheet 3) |
| TownTest.V3 hand | `mark.Text` contains the hand's name | `MarkText(mark)` contains it | rows have no button text |
| TownTest.Pave CardsStopClicks | `Root/Notes` slip counts as a card | `Root/Ledger/NoteSection` counts as a card | notes are the ledger's note section |
| BookTest | slim strip shows over the book; slim text contains "N coin" | at the book only navigation shows (slim hidden, pause shown); slim text contains N and the level | sheet 2 "with the book in view only navigation stays"; no word "coin" |
| RiverTest | tide text starts "High water"/"Low water", contains "rising, highest in"/"falling, lowest in" | starts "High,"/"Low,", contains "rising\nhigh in"/"falling\nlow in" | the tide's shorter text (sheet 3) to fit the timers zone |
| PlayThrough | click a lit bookmark at once; a click that buys nothing is a MISS | scroll the shop to the row if it is out of sight, bring the pointer, wait until the row is still (at most 2 s; if still moving, look again), click; a click refused because the row moved under it in that moment is counted and printed as REFUSED, not MISS | the sheet's rule that a row that just moved takes no click; everything else still a MISS |
| UiAudit.cs | Marker property | `AuditMarker` virtual; `StateHook`, `AfterTour` hooks | game-layout runs the same tour with the same state ids |

### 21:53 UTC: results (final for L1)

./check run at 21:22 (machine load average 18 to 35):
sim-build, sim-test ok; game-build ok; game-smoke PASS; game-town PASS; game-perf PASS (town 1x 489 B/frame, 8x 748; river 1x 1184, 8x 1511, all under the old figures 656/2014/1361/2954); game-playthrough PASS charter 33.9 min, 201 purchases (equal to before), 1 click refused on a moving row; game-river PASS; game-layout PASS (31 states x 8 canvases, 59,979 checks, 0 failures; budget at most loud 2, quiet 2); game-book PASS; game-menu PASS; game-save PASS;
game-settings: SETTINGS PASS printed, then Godot exits with "Leaked unsafe reference" errors (code 134); the main checkout without these changes fails the same way on this machine now (1528 such lines): environmental.
game-motion: 60 and 30 fps pass; 120 fps failed one CPU gate (tab-saving-keys frame 8.47 ms, gate 6) under load; the book and settings were not changed. Rerun alone when the machine is quiet.
screenshot and ledger: still running at 21:53.

Next: the tour's staging repairs from n3-before (lead's note 21:40) into this tour; tags as screen text (sheet 5); message queues and motion (next round); page pictures taken mid-fade at 1 fps (UiAudit waits 900 ms real time; the fade is real time with a per-frame step cap).

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

## After the review (L1 fixer, 22:43 to 23:35 UTC)

Read: brief, AGENTS.md, layout-sheet v2, l1-pictures, l1-review-1, l1-review-2, lead-notes (22:40, 22:50 and the older notes). Pictures of the fixes: `build/n3/ui-audit/l1-fix/<res>-<scale>[b]/`.

### Fixed (code, then the picture of a state that showed the fault)

| Item | Change | Files |
| --- | --- | --- |
| Lead 1, R1 must 6, R2 must 6: pages see-through | The page card was opaque at rest; the tour pictured it mid-fade (1 frame a second). `Shot` now waits until an open page's fade is at 1. Picture: 1920 u15 opaque, map dimmed behind | `tests/UiAudit.cs` |
| Lead 2, R1 must 2, R2 must 5: "The cha..." | The strip says "Charter", ten pips, "1 of 10"; the whole name stays in the drive's ask (tests read `DriveCardText`) | `TownHud.Choice.cs` |
| Lead 3: hint ring a filled disc | A flat thin wheat ring (own mesh, 5 percent of its radius), just outside the plot, fading in and out over 0.4 s with its hint; gentle 3 percent breathing | `TownHud.Request.cs`, `TownHud.cs` |
| Lead 4, R1 must 3 and 4: tags are 3D labels | The plots' "What" and boats' "Wants" Label3Ds are kept as data but never drawn; each is drawn as a screen tag (quiet paper pill, caption, every zoom with the full interface); boats say "Grain 21 of 656" (no "loads"); a tag touching a visible card (now including the line) or another tag, or leaving the window, fades out and stays out at least 0.5 s | `TownHud.Labels.cs` |
| Lead 5: a finished group disappears | Group headings read the simulation's whole list (`_raw`), so "The river" keeps its heading with "N done" when all its steps are finished or locked | `TownHud.ShopRows.cs` |
| R1 must 1: goal bar shows time | The bar is the count (delivered or loaded of the amount), river; time only tints the "within" text (skipper: the "Loaded" line) brick in its last quarter. `RequestTimeBar` still reports the time share (layout budget) | `TownHud.Request.cs`, `TownHud.River.cs` |
| R1 must 3, R2 must 7: tips say "coin", bold card | Pointer tip and "Suggested" tag: quiet pill, regular caption, the coin icon before the amount ("Extend this cottage: (c) 40. Rooms 2 to 3.") | `TownHud.CoinTip.cs` (new), `TownHud.Pave.cs`, `TownHud.Extend.cs` |
| R2 must 8, R1 should: Pave pill a dashed "–" | A blocked Pave pill says "no road" (line 2 keeps the reason) | `TownHud.Shop.cs` |
| R2 must 4: half row at 1280/150 | When the shop scrolls, the scroll stops at a heading or row top, the card ends under the last whole row, rows and headings not wholly inside are not drawn; the wheel steps one row (42) | `TownHud.ShopRows.cs` |
| R1 must 8 (and lead 21:40): tour staging | The n3-before repairs (0828955) taken over: Age1, Book, UiAudit.cs and ui-audit.sh by patch; Age2 by hand (u32 skipper card, u35 mid-age 20..24 with a request, u34 drawbridge, u37 retired) | `tests/UiAudit*.cs`, `tools/ui-audit.sh` |
| R1 must 5 (part): Home not disabled-looking | A disabled navigation button dims its icon and key cap to 45 percent (only on change) | `TownHud.Timers.cs` |
| Lead 22:50: settings leak at exit | See below | `tests/TestInput.cs`, `TownTestBase.cs`, `SettingsTest*.cs`, `SettingsShot.cs` |
| R2 should: heading grammar "1 later" | the narrow fallback is "+N more" (the sheet's words) | `TownHud.ShopRows.cs` |
| R2 should: renown bar touches "Level" | purse lines moved up 2 to 3 units: 5 of air over the bar | `TownHud.Ledger.cs` |

### The settings test's exit (lead 22:50)

All test scenes now leave through `TestInput.QuitClean(node, code)`: GC.Collect, WaitForPendingFinalizers, GC.Collect, one frame, twice; then Quit. Key events the tests make are disposed after use (`using`). This only releases wrappers that nothing holds; an object still referenced by managed code (a true leak) survives the collection and is still reported as "Leaked unsafe reference", which godot-test.sh still fails on. game-settings alone at 22:48 (load about 5): SETTINGS PASS, exit 0.

### Tests changed

| Test | Old | New | Why |
| --- | --- | --- | --- |
| TownTest.cs CheckRequestCard | `RequestTimeBar` equals time left share | `RequestCountBar` equals delivered/amount, and `RequestTimeBar` (now the time share itself) equals time left | sheet 3: the bar is the count; the time check is kept |
| TownTest.cs Pave hover | tip contains "coin" | tip contains `Money(PavePrice(road))` | the tip carries the coin sign, never the word |
| Should (sheet 4): not-yet prices and line 2 too faint | price in a not-yet or blocked pill and row line 2 are ink 70 percent (`MutedNumber`: muted text that carries a number) | `ShopRow.cs` |

### Not fixed, disagreed, or for the next round

- **Settings pictures u21 to u23 (R1 must 8):** the n3-before staging is in, but in an `--only` run at 1280 x 720 the cover was not open when the settings loop began ("state Playing": the u20 question's Esc had resumed the game). Proposed fix, not made (the whole check had started): before the settings loop, if `Main.Menu.Now == Playing`, `Main.Menu.Pause()` and wait for the cover. u24 and the "Keep this?" / "Delete the save?" cards are therefore still not judged.
- **Tags, one gap:** at 1280 x 720 u35 two river stakes ("A cottage, at level 24", staked at the level-up 1.2 s before) still showed as the old 3D words. Cause: the tags are scanned every 30 frames, so at the tour's 1 frame a second a fresh Label3D draws for up to 30 s (0.5 s in play). Fix for the next agent: scan by real time (0.5 s), or the owners (`RiverLayer.StakeLabel`, `RiverBoats`, `TownBuildings` stake label: track P owns that file) create their labels with `Visible = false`.
- **Hint ring size:** a thin ring now, but at radius 0.75 of the plot's longer side it is larger than needed at 1920 (u13 passes under the line card); 0.5 times the plot's diagonal would hug it.
- **Disagreed:** R2 must 7 "the Suggested tag goes": the sheet (section 5) lists "Suggested: 24" as a tag and it tells the player which road the town suggests; kept, with the coin sign and the existing no-overlap rule against the tip. R1 should "cards 98 percent": the sheet says paper 96 percent. R1 should "slashed zero": Atkinson Hyperlegible's zero is slashed by design and has no plain alternate; changing the font is the lead's call.
- **Next rounds (the lead's note 21:05 item 7):** the pause cover's question and the settings' clash card as modal cards (R1 must 7, R2 must 3 and 3b), the book's placement, compass and folio and navigation over the book (R2 must 2), tooltips in the tour (R1 must 5, rest), notes and their stay times, the line always on, "coin" in notes and pages, tide wording, the age page text.

### Reverted after the check

- R2 must 1 (slim purse over the book): the fade by the map's left edge made game-book fail ("the interface thins to the slim strip on the way out": the slim must show while the full interface is out, and by then the left page is already under it). Reverted at 23:32; game-book alone after the revert: BOOK PASS. Not fixed: it needs the slim placed or faded against the summary page's title, measured, not guessed.

### ./check at 23:11 (load 12 to 36, other agents' checks at the same time)

sim-build, sim-test, game-build, game-smoke, game-town, game-perf PASS (town 1x 644 B/frame, river 1x 1361), game-playthrough PASS (charter 33.9 min, 201 purchases, 1 click refused on a moving row), game-river, game-layout PASS (32 states x 8 canvases, 0 failures), game-settings PASS (exit clean), game-menu, game-save; game-book FAIL (the slim change, reverted, then BOOK PASS alone); game-motion FAIL at 120 fps only (CPU gate 6 ms: 8.30 and 8.40 ms on single frames under load; 60 and 30 fps pass; the book and settings motion untouched); screenshot still running at 23:49.

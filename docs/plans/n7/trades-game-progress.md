# Trades, step 2, parts 2 and 3: the interface and the figures (progress)

Branch `wt/n7-trades-game` (worktree `build/wt/n7-trades-game`), made from the names commit `a2ce2a5`; `wt/n7-trades` (part 1, the simulation) merged in at `2d2bac4` (the rules and the tuning). Brief: `trades-build-sheet.md` parts 2 and 3, `game-trades`, `game-layout`'s knowledge of the cards, the pictures.

## Names missed or wrong in the simulation (for the lead)

- In the names commit the Training row's `ShopItem.Worth` was 0, so line 2 showed the stand-in. The rules commit gives a worth; the row now reads "+241 a minute" by itself. The stand-in "Better at their trades" was too long for the 272 rail (S2-line2 at 1920x1080@150 and 1280x720@150); it is now "Better at trades" (shown only while there is no worth).
- Whether plus or minus can move anyone: the interface first copied the simulation's rule; the other builder then added `CanRaiseTrade` / `CanLowerTrade` (commit `e885ad2`), which the stepper now uses (minus also stays disabled at the last member, as the sheet says).
- `GiveVillagerBackToTown` exists now (`e885ad2`) but the sheet gives it no place on the card, so the interface does not offer it: a villager whose cap you clicked stays yours for good (switching "Town fills trades" off and on does not clear choices made one by one). A small "Leave it to the town" on the villager card would be the place; not built.

## Built, file by file

**Part 3, the figures**

- `game/scripts/town/TownVillagers.Heads.cs` (new): six head MultiMeshes (`villager-head-helper-mud/-earth/-soot`, `-kerchief`, `-strawhat`, `-cap`) and one for the miller's apron (`villager-apron`), all soft-ink copies, never the tunic shader. `HeadOf(i, trade)`: a Helper wears hair only, its tone by its number (i % 3); a trade its headwear. A change of trade is a shrink into the neck and a grow out of it over `HeadSwapSeconds` (0.3 s), the kind swapped at the smallest point; a change back half way turns the motion round from where it is; a fast-forward (the drawing's `_seen` cleared) snaps. The apron shrinks with the cap about the waist. No allocation (structs only).
- `TownVillagers.cs`: the tunic list is Brick, Packed earth, Cast-iron green, Mud, Soot brick; the old three heads and `HeadOf(i)` are gone; `Figure(..., trade)` draws a trade's head and the apron (the asset audit and the ink check use it).
- `TownVillagers.TradeRings.cs` (new): the hovered Trades line's members ringed on the map (one MultiMesh, the hover ring's look); fades 0.1 s, a change of trade fades the old rings out first.
- Ink (`game/tests/AssetAudit.InkCheck.cs`): `villagers-group` is now drawn by the game's own heads in a mixed group (two Helpers, a carrier, two reapers, a miller with apron); a warm-up subject (the villagers' group, not counted, printed as `INK warm-up`) is measured before the list. On the first subject reading high: with the new heads the first subject now reads the same as later (0.349 alone with `--only=villagers-group`, 0.349 as warm-up and again in the list), so I could not reproduce the 0.53; the cause was not found. The old heads' materials (hood, hat) compiling on first sight is the likeliest cause; the warm-up guards against it either way.

**Part 2, the interface**

- `TownHud.Trades.cs` (new): the Trades card in `HudZones.Person` (156 high), shown from `TradesShown` on while no villager's card is open; crossfades with the villager's card over 0.2 s (`MessageFader`), and fades with the interface (`_fullA`). Heading "Trades" (title, serif), the switch "Town fills trades" (caption, muted, a small paper box with an ink check that fades 0.15 s). One line of 28 per open trade (Carriers, Reapers, Millers) and the Helpers last; a trade that opens fades in and the Helpers slide down over 0.2 s. Hovering a line: the band and its members ringed. `SetOpenCard` is told the open card every frame.
- `TownHud.TradesParts.cs` (new): the line (icon 24, name body, the count right-aligned tabular in a 28 cell: muted regular when the town decides it, ink bold when you set it, its digits fade out and in over 0.2 s when it changes), the stepper's pills (drawn 32 x 24, hit areas 40 x 28 units; rest, hover, pressed with the bar 1 down, disabled; bars drawn, not glyphs), plus disabled when nobody can be taken (`CanRaiseTrade`), minus at the last member or when nobody can leave (`CanLowerTrade`); a click acts on what was shown 0.3 s ago. Tooltips in the shop tip's form, made when the pointer comes and frozen while it shows: a sentence, then Members / places, Waiting now / away now, Bonus (when above 0), "You set this count. The town leaves it alone."; disabled pills say why; the switch says what it does.
- `TradeMark.cs` (new): the check and the corner fold, drawn in code.
- `TownHud.PeopleTrade.cs` (new): the villager's card: title "Kit · Carrier" (name bold, trade muted), line 2 "Likes carrying water · by the well lane" (home dropped whole when it does not fit; "Lives by ..." while the like's trade is not open), the Trade line: caption "Trade" and caps 40 x 32 at x 60, y 112 (current: darker paper, ink border 2; liked: ink fold 8; hover: light paper, ink border 1), one-line tooltips ("Reaper: likes this work, +5%"); a click sets the trade. Map tags: "Kit, carrier" on hover; the first three villagers the town gives a trade (TradeChanged by the town) show their tag for 2 s (fades 0.12 s), again after a new town.
- `TownHud.People.cs`: the card is 156 (`HudZones.PersonH`), the leaning line is hidden for good (the like is on line 2), lines at 8, 33, 56, 79, caps at 112; fade 0.2 s; the hover tag names the trade; the Trades card stops the pick.
- `TownHud.FirstMinute.cs`: the fifth first hint "Every villager has a trade. Change them on the card at the left, or leave it to the town." once ever (settings `hints_seen` bit 4), when the Trades card has fully shown; done when you change a trade (4 s at least, 10 s at most), in the line like every first hint.
- `TownHud.Tip.cs`: the cards' tooltip owners; the purse's tooltip in the second age gains "Word of the river spreads: each level asks a little less as time goes by." (while a level is left).
- `TownHud.ShopText.cs`: the Training row's tooltip "Every villager works N% faster at its own trade and its goods fetch N% more." with N from `TownRules.TradeStep` (read, not copied); "Training" in the row-name catalogue (S2).
- `TownHud.cs`: the Trades card stops the map (`OverInterface`), trade events, a new town resets the tags.

## Tests

- `game/tests/TradesTest.cs`, `TradesTest.Cards.cs`, `trades.tscn`, `tools/trades-check.sh` (game-trades at 60, 30, 120): see the class summary. Counts are set through the calls and handed back to the town so the stepper can move anyone.
- `game/tests/TradesShots.cs`, `trades-shots.tscn`, `tools/trades-shots.sh`: the pictures (below).
- `game-layout`: `LayoutRules` knows the zone "Trades" (= `HudZones.Person`); `LayoutTest` checks the Trades card in every other state (the villager's card in the others), and runs the longest-text fixture with each card on every canvas; `LayoutFixture` holds the Trades card's texts and the card's new lines; "Training" is in the row-name catalogue.

### Tests replaced (old assertion, new assertion, why)

| Test | Old | New | Why |
| --- | --- | --- | --- |
| `PeopleTest` name tag | `PersonTagText == VillagerName(who)` | `== VillagerName(who) + ", " + TradeOne[trade]` | the sheet: the tag says "Joss, reaper" |
| `PeopleTest` card text | starts with `name + "|"`, contains "Lives ", contains "A good hand" exactly when `LeaningCounts`, contains "trip" | starts with `name + " · "`, contains "|Likes " or "|Lives ", never "A good hand", contains "trip" | the title names the trade; the leaning line is replaced by "Likes ..." on line 2 |
| `LayoutFixture` | "Person/Leaning" "A good hand at the harvest"; "Person/Home" "Lives by the well lane" | no Leaning (label hidden for good); "Person/Home" "Likes carrying water" (the card drops the home when the pair does not fit), plus the title's trade and the Trades card's texts | the card's lines changed |
| `LayoutTest` | the villager's card in every state | the Trades card in every other state, the villager's card in the rest; the fixture with each card | the two cards share the zone; both must meet every canvas |

## Results (18:10 UTC, after merging `wt/n7-trades` up to `6e52b5f`, under the heavy-run lock)

- game build: `Build succeeded. 0 Warning(s) 0 Error(s)` (`dotnet build game/TideToll.csproj -c Debug`)
- game-trades: `TRADES PASS bytes=611 tags=3 fps=60 villagers=21 headstep=0.17 alphastep=0.17 frames=1451`; 30 fps `bytes=786 headstep=0.32 alphastep=0.33`; 120 fps `bytes=518 headstep=0.08 alphastep=0.08`; `TRADES RATES PASS`
- game-people: `PEOPLE RATES PASS` (60: `picked=21/21 passpairs=15165 sidefails=0 flicker=0 bytes=686 villagerbytes=0`)
- game-work: `WORK RATES PASS`
- game-layout: `LAYOUT PASS ... states=36 canvases=8 checks=91817 failures=0 loud<=2 quiet<=3` (the first run failed S2-line2 twice on the Training stand-in line; fixed)
- game-ink: `ASSET AUDIT PASS ... villagers-group=0.349` (mixed headwear, game heads), warm-up 0.349
- game-motion: **fails**, three times in a row, at all three rates: `FAIL: banner: frame 112 allocated 29840 B (gate 8192; first hints shown 29, now 2)` (30 fps; 60: frame 221; 120: frame 439). The frame is the one after the test's banner leaves: the first hint 2 ("Each level gives the town something new...") starts there, and `GameSettings.SetHintSeen` writes the settings file (about 30 KB in one frame). With the trades hint switched off the same failure stays (`first hints shown 13, now 2`), so it is not the trades hint; on the main branch the motion test passed (people-progress), so something since (most likely the merged simulation's pacing changing which first hints have shown before the banner) puts hint 2 into the measured window. Not verified on `wt/n7-trades` alone. Earlier runs also failed CPU gates once each (6.8-15.8 ms against 6) while the machine's load was 6-8; those did not repeat. The motion test's failure message now also prints the first hints shown (a diagnostic only).

## Pictures (real frames, `build/n7/trades/`, not committed)

`tools/trades-shots.sh` (xvfb, Mobile renderer): `trades-card`, `trades-hover` (a line hovered, its members ringed), `trades-tip`, `trades-plus-hover`, `trades-villager`, `trades-cap-tip`, `trades-crossfade`, `trades-count-strip`, `trades-street-before/after(-close)`, each `-1920x1080`, `-1280x720` and `-1280x720-150` (interface 150 percent, the 272 rail); `*-whole-*` the whole window; `board-beside-game-1920x1080.png` (the board's planned screen beside the game's); `game-columns.png` (the two cards at each size).

## Not done

- The Helpers' caption "work where it pays best" does not fit on any canvas (154 px of caption against 134 on the 320 rail at 1080, less elsewhere): it is dropped whole (layout rule) and the Helpers' line tooltip says it.
- The Training row is not separately tested in the late first age at 1280 x 720 with compact rows (review 21); game-layout's catalogue and line-2 checks cover its name and stand-in line on every canvas, and `ShopSamples` cover compact rows in general.
- No place on the villager's card to give a villager back to the town (above).

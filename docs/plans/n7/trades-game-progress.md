# Trades, step 2, parts 2 and 3: the interface and the figures (progress)

Branch `wt/n7-trades-game` (worktree `build/wt/n7-trades-game`), made from the names commit `a2ce2a5`; `wt/n7-trades` (part 1, the simulation) merged in at `2d2bac4` (the rules and the tuning). Brief: `trades-build-sheet.md` parts 2 and 3, `game-trades`, `game-layout`'s knowledge of the cards, the pictures.

## Names missed or wrong in the simulation (for the lead)

- In the names commit the Training row's `ShopItem.Worth` was 0, so line 2 showed the stand-in. The rules commit gives a worth; the row now reads "+241 a minute" by itself. The stand-in "Better at their trades" was too long for the 272 rail (S2-line2 at 1920x1080@150 and 1280x720@150); it is now "Better at trades" (shown only while there is no worth).
- Whether plus or minus can move anyone: the interface first copied the simulation's rule; the other builder then added `CanRaiseTrade` / `CanLowerTrade` (commit `e885ad2`), which the stepper now uses (minus also stays disabled at the last member, as the sheet says).
- `GiveVillagerBackToTown` (`e885ad2`): on the lead's word, a click on the villager's current cap hands it back to the town when it was set by you; the cap's tooltip says so ("Reaper. Set by you: click to give it back to the town" / "Reaper. The town's choice").
- `TrainingStepBonus`: the Training row's tooltip reads it when the tip is made ("Every villager works 4% faster at its own trade and its goods fetch 4% more."; the plain sentence when all training is done).

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
- `TownHud.PeopleTrade.cs` (new): the villager's card: title "Kit · Carrier" (name bold, trade muted), line 2 "Likes carrying water · by the well lane" (home dropped whole when it does not fit; "Lives by ..." while the like's trade is not open), the Trade line: caption "Trade" and caps 40 x 32 at x 60, y 112 (current: darker paper, ink border 2; liked: ink fold 8; hover: light paper, ink border 1), one-line tooltips ("Reaper: likes this work, +5%"; the current cap says whose choice it is); a click sets the trade, a click on the current cap set by you gives the villager back to the town. Tooltips of the cards' parts stand 8 right of the card, not of the part. Map tags: "Kit, carrier" on hover; the first three villagers the town gives a trade (TradeChanged by the town) show their tag for 2 s (fades 0.12 s), again after a new town.
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

## Results (18:30 UTC, after merging `wt/n7-trades` up to `4300f3f`, under the heavy-run lock in takes of at most 15 minutes)

- game build: `Build succeeded. 0 Warning(s) 0 Error(s)` (`dotnet build game/TideToll.csproj -c Debug`)
- game-trades: `TRADES PASS bytes=606 tags=3 fps=60 villagers=21 headstep=0.17 alphastep=0.17 frames=1447`; 30 fps `bytes=782 headstep=0.32 alphastep=0.33 frames=711`; 120 fps `bytes=519 headstep=0.08 alphastep=0.08 frames=2924`; `TRADES RATES PASS`
- game-people: `PEOPLE RATES PASS` (60: `picked=21/21 passpairs=15165 sidefails=0 flicker=0 bytes=686 villagerbytes=0`)
- game-work: `WORK RATES PASS`
- game-layout: `LAYOUT PASS ... states=36 canvases=8 checks=92169 failures=0 loud<=2 quiet<=3 made=42 notmade=6 level=28` (a first run failed S2-line2 twice on the Training stand-in line, fixed; one run at 18:26 failed the shop's compact switch timing, 189 ms against 200, with the machine's load at 7: the next run passed)
- game-ink: `ASSET AUDIT PASS ... villagers-group=0.349` (mixed headwear, game heads), warm-up 0.349
- game-motion: **fails** in every run since the rules merge (last 18:30: 60 fps `esc-during-return: frame 35 took 6.27 ms of CPU (gate 6)`, 30 fps the banner below): `FAIL: banner: frame 112 allocated 29840 B (gate 8192; first hints shown 29, now 2)` (30 fps; 60: frame 221; 120: frame 439). The frame is the one after the test's banner leaves: the first hint 2 ("Each level gives the town something new...") starts there, and `GameSettings.SetHintSeen` writes the settings file (about 30 KB in one frame). With the trades hint switched off the same failure stays (`first hints shown 13, now 2`), so it is not the trades hint; on the main branch the motion test passed (people-progress), so something since (most likely the merged simulation's pacing changing which first hints have shown before the banner) puts hint 2 into the measured window. Not verified on `wt/n7-trades` alone. Earlier runs also failed CPU gates once each (6.8-15.8 ms against 6) while the machine's load was 6-8; those did not repeat. The motion test's failure message now also prints the first hints shown (a diagnostic only).

## Pictures (real frames, `build/n7/trades/`, not committed; composite made by a scratch script with PIL)

`tools/trades-shots.sh` (xvfb, Mobile renderer): `trades-card`, `trades-hover` (a line hovered, its members ringed), `trades-tip`, `trades-plus-hover`, `trades-villager`, `trades-cap-tip`, `trades-crossfade`, `trades-count-strip`, `trades-street-before/after(-close)`, each `-1920x1080`, `-1280x720` and `-1280x720-150` (interface 150 percent, the 272 rail); `*-whole-*` the whole window; `board-beside-game-1920x1080.png` (the board's planned screen beside the game's); `game-columns.png` (the two cards at each size).

What I saw in them (18:58 run): the Trades card reads at all three sizes with nothing cut (two-figure counts clear of the plus); a hovered line has its band and its members wear thin wheat rings on the map; tooltips stand beside the card; the villager's card shows "Hal · Carrier", "Likes milling · by the well lane", the current cap darker with the ink border and the liked cap's fold; the crossfade frame shows both cards at about half (as designed, 0.2 s); in the count strip the changed digits fade out and in and the count turns bold; at the play view the straw hats read at once (Brick band), the kerchief is a blue head, the miller a white crown in a red ring with the apron, Helpers dark hair; "before" (everyone a Helper) is a street of bare heads. Weak points: a carrier and a Helper still differ by head colour only; the auto tags ("Wenna, reaper") show three at once when the town's first filling lands in one batch. Not mine but seen: at 1280 x 720 with interface 150 the first-minute hint's line ("Your villager fetches water ... coin.") wraps to a second line below its card.

## Not done

- The Helpers' caption "work where it pays best" does not fit on any canvas (154 px of caption against 134 on the 320 rail at 1080, less elsewhere): it is dropped whole (layout rule) and the Helpers' line tooltip says it.
- The Training row is not separately tested in the late first age at 1280 x 720 with compact rows (review 21); game-layout's catalogue and line-2 checks cover its name and stand-in line on every canvas, and `ShopSamples` cover compact rows in general.
- No place on the villager's card to give a villager back to the town (above).

## A lock mistake (18:43-18:47 UTC)

My picture script for take B waited 8 minutes for the heavy-run lock and then, by a bug in the script (the wait loop fell through instead of stopping), started its runs **without the lock** at 18:43, beside the boats builder's strip run that held it. I stopped my runs at 18:47 by hand. The lock folder disappeared at 18:47:01: that may have been my script's last line (`rmdir`) removing the boats builder's lock, or that builder giving it back; I cannot tell which. Any timing check of the boats builder's between 18:43 and 18:47, or after 18:47 while it believed it held the lock, is worth one more run. The later takes wait strictly (no run without the lock).

## Fix round (19:00-20:05 UTC)

- **No file written in a frame of play because a hint was seen.** `GameSettings.SetHintSeen` keeps the bit in memory and sets `HintsUnsaved`; `SaveHintsIfChanged` (read the file, merge the bits, write) runs with the town's next save (`TownSaver.SaveNow`: the autosave, the pause's save, the quit; also when the town's save is refused because the save on disk could not be read). Any other `Save` clears the flag. Searched for other writers during play: every other setter (`SetVolume`, `SetFrameLimit`, `SetInterfaceSize`, `SetWindowMode`, `SetResolution`, `SetKey`, `RestoreDefaultKeys`, `SetAutosaveMinutes`, `ShowHintsAgain`) is called only from the settings pages (`SettingsPages.*`), and `SettingsRuntime` writes nothing; nothing else writes `settings.cfg` by itself during play. Test (game-trades): the frame the trades' hint appears in leaves `settings.cfg` unchanged and allocates under 8 KB; after `SaveNow` the file's `hints_seen` has the bit.
- **The first three tags of the town's filling** come one after another, 0.5 s apart (`AutoTagGap`), and follow the map tags' rules: never cut by the window, never on a card (the map tags' card list, the Trades and villager cards), a kept map tag or within 40 px of a showing tooltip; none for a villager under the interface. A blocked tag fades out. Test: the tags start at least 0.4 s apart.
- **The line holds its text on every canvas**: `TownHud.FitLine` measures the text when it changes (and the old text during a cross-fade) and grows the card upward from the zone's bottom edge for two lines. game-layout: the Line zone may be taller, bottom-aligned; the fixture's line text is the longer of the longest mode help and the longest first hint (the trades' hint, 87 characters), applied through `FitLineFor`.
- **Counts**: the town's are in the captions' muted ink (`UiKit.Muted`, was `MutedNumber`), regular; yours ink bold; the Helpers' line by the same rule.
- **The trade line's tooltip**: "Members 13", "Work places 5 (the others are on the road)", then "Waiting now" and "Away now (at another trade's work)" only when above 0.
- **Honest pictures**: no count set by hand except in the count strip; the earlier first hints are marked seen so the line shows the trades' hint (`trades-hint-whole`); the street at two moments of the town's own filling (`--street=8`, `--street=15`).
- **The kerchief and the cap 15 percent larger**: tried in the drawing (`TownVillagers.WearScale`, about the neck; the models untouched) with before-and-after frames on a real street (`trades-street-after*` against `trades-street-larger*`); decision below.

## Left from the review (can wait)

From `docs/plans/n7/trades-game-review.md`:
- 7a the liked cap's fold: made 10 units in this round (it was a speck at 720). 7b the Helper cap's tooltip already reads "Helper: no trade, works where it pays best" (the frame showed the reaper's cap hovered).
- 8 the crossfade's mid-frame: check it in play at 30 fps and at 150; if it flickers, fade the outgoing card faster (0.1 s) than the incoming. Not done.
- 11 the hover rings are thin and few at 720 and at 150: scale ring radius and width with the view's zoom, a little ink in the colour so it shows on wheat. Not done.
- The Helpers' caption "work where it pays best" fits nowhere: accepted as dropped (the Helpers line's tooltip says it).

### Fix round results (under the lock, takes under 15 minutes)

- game-motion, run 1 (19:22): 60 fps `FAIL: notes: frame 98 took 6.32 ms of CPU (gate 6)`; 30 fps `MOTION PASS impatient=ok pages-align=0.00px fps=30 transitions=13 alloc-max=6000B cpu-max=4.90ms`; 120 fps `FAIL: tab-keys-saving: frame 36 took 6.81 ms of CPU (gate 6)` (frame time, load 4-7). No allocation failure.
- game-motion, run 2 (19:25): 60 `MOTION PASS ... fps=60 transitions=13 alloc-max=6000B cpu-max=1.74ms`; 30 `... cpu-max=2.64ms`; 120 `... cpu-max=4.33ms`.
- game-motion, run 3 (19:47): 60 `alloc-max=6000B cpu-max=1.85ms`; 30 `cpu-max=1.57ms`; 120 `cpu-max=1.54ms`; all `MOTION PASS`.
- game-trades: `TRADES PASS hintframe=2744B bytes=616 tags=3 tagstarts=0.05/0.53/1.05 fps=60 ... headstep=0.17 alphastep=0.17`; 30 `hintframe=2696B tagstarts=0.10/0.59/1.06`; 120 `hintframe=2848B tagstarts=0.03/0.53/1.03`; `TRADES RATES PASS` (with the cap's hand-back check, the hint's file check and the tags' spacing).
- game-layout: `LAYOUT PASS ... states=36 canvases=8 checks=92063 failures=0 loud<=2 quiet<=3` (with the longest first hint in the line on every canvas).
- game-people: `PEOPLE RATES PASS`.
- The trial of a 15 percent larger kerchief and cap: **not kept** (`WearScale` stays 1, the models untouched). On the real streets (`trades-street-after*` against `trades-street-larger*`, levels 8, 13 and 15) the larger kerchiefs read a little bigger but also make the heads look swollen; not clearly better. game-ink was not rerun (nothing drawn changed).
- The town's own mix in the frames: level 8: 8 carriers, 6 reapers, 0 millers; level 13: 6, 13, 2; level 15: 5, 14, 7.
- The tooltip's "Work places 5 (the others are on the road)" wraps to a second line at 1280 x 720 at 150 (the tip box grows; never cut).

### Pictures in the repository

`docs/plans/n7/after/` (JPEG, quality 88): `board-beside-game-1920x1080`, `game-columns` (1920 x 1080 and 1280 x 720 at 150; **1280 x 720 at 100 was not retaken this round**), `trades-card-whole-1920x1080`, `trades-hint-whole-1920x1080` (the trades' hint in its place), `trades-tip-*`, `trades-hover(-whole)-1920x1080` (rings), `trades-count-strip-1920x1080`, `trades-cap-tip-1920x1080`, and the streets `trades-street-{before,after,larger}(-close)-1920x1080[-level8|-level15]` (no suffix: level 13).

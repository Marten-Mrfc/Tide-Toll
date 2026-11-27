# Trades, step 2: the build sheet (the lead, 27 November)

The design is `trades-design.md`; the board is `mockup/trades-now-and-planned.png` and `mockup/hats-round2.png` (with `mockup/notes.md`); the review is `trades-review.md`. **Where they differ from this sheet, this sheet is right**: it holds the lead's decisions on the review. It is built on the main branch after the retune (`retune-progress.md`) is merged: read that page's last section ("what the trades step should know") first.

The owner's sentence this must answer on the screen: "They all start doing the same task. That is not the idea. I would like each villager to have a role and that it can be assigned."

## Decisions on the review (what changed against the design page)

| Review | Decision |
| --- | --- |
| 1 minus looks dead | The stepper sets **your count** for that trade. The town never moves anyone into or out of a trade you set. A count the town decides is shown in the muted number colour, regular weight; a count you set in ink, bold (the shop's language for "not yet" and "yours to buy"). Switching "Town fills trades" off and on again hands every trade back to the town |
| 2 the Hand wears a hat | The unassigned villager is a **Helper** and wears hair only (three dark tones). A hat always means a trade. A test fails if a Helper's head is a trade's head or a hood |
| 3 the screen does not say it | The villager's card is titled "Joss · Reaper"; the map tag says "Joss, reaper"; **hovering a trade's line rings its members on the map**; the first three villagers the town gives a trade show their tag for 2 seconds. The hint stays in the hints' own place (one place per kind of message) |
| 4 "Hands" | **Helpers**, a fourth line in the same grid (icon, name, count in the count column, no stepper, caption "work where it pays best" in the stepper's place); no foot line |
| 5 too loud | The switch is quiet: a small paper box with an ink check when on, caption "Town fills trades". The current trade's cap is the darker paper with an ink border of 2, not wheat (wheat means coin) |
| 6, 7, 8, 13 hats, tunics, icons | As rebuilt in the board's round 2; the tunic list follows that round's recommendation |
| 9 the Train row | One row for all trades: **"Training"** with its step number; line 2 is what one more is worth, "+312 a minute", like every other row; the tooltip says what it does ("Every villager works 3% faster at its own trade and its goods fetch 3% more"). One level in the save, ten steps |
| 10 the swap | Both cards 156 high, a crossfade of 0.2 s in the same rectangle. The town never changes the trade of the villager whose card is open |
| 11, 12 the line | The plus and minus have a hit area of 40 x 28 units (the drawn pill stays 32 x 24); the tests measure the hit area. No status on the line: the count only. Places, waiting and away are in the tooltip |
| 14 | The Helper is last on both cards |
| 15, 16 | "Likes reaping" (carrying water, reaping, milling; later shearing, weaving, rowing). The liked cap carries an ink corner fold (8 units), not a dot. The card's second line: "Likes reaping · by the well lane"; the home is dropped whole when the line does not fit; a like whose trade is not open yet is not shown |
| 17, 18 | "Town fills trades"; the trade line's tooltip in the shop tip's form (a sentence, then facts), its numbers frozen while it shows |
| 19, 22 | The Trades card fades with the ledger's lower sections when the interface fades; it fades in over 0.2 s; a count that changes fades its digits over 0.2 s; a click acts on what was shown 0.3 s ago, as the shop's rows do |
| 20 | Step 3's matter (the second age's trades) |
| 21 | The Training row is tested in the late first age at 1280 x 720 with compact rows |
| 23 | A tuning target below: villagers with a trade spend at least 70 percent of their working trips at it |
| 26 | Not taken: a click on the empty map closes the villager's card, as built in step 1 |

Names on the cards: **Carriers, Reapers, Millers, Helpers** (full names in tooltips: "Water carriers"). One villager: "carrier", "reaper", "miller", "helper".

**The town gives every villager a trade** (decided after the review: the owner asked for a role for each villager, and Helpers left to the best-pay rule are the old herd). With "Town fills trades" on there are no Helpers unless you make some; the Helpers line shows 0.

## Part 1: the simulation (commit it alone, first; the game is built against these names)

```csharp
public enum Trade : byte { None = 0, Water = 1, Grain = 2, Flour = 3 }   // appended only; Wool, Cloth, Boat come with step 3

// VillagerState:  Trade Trade;  bool TradeByPlayer;
// TownState:      bool TownFillsTrades = true;  bool[] TradeCountByPlayer (per trade);  int Training;

town.TradeOpen(Trade t)                 // its workplace exists (Water from the start, Grain with the first field, Flour with the mill)
town.TradesShown                        // true from the first field on
town.TradeInfo(Trade t)                 // TradeView { Open, Members, Places, Away, Waiting, ByPlayer, WorkBonus, WorthBonus }
town.Helpers                            // villagers with no trade
VillagerView.Trade, .TradeByPlayer, .Likes   // Likes: the trade it likes (None while that trade is not open)
town.TradeOfJob(JobKind j)

town.SetTradeCount(Trade t, int n)      // yours from now on; the game picks who moves (below); false if nothing changed
town.SetVillagerTrade(int id, Trade t)  // that villager is yours from now on
town.SetTownFillsTrades(bool on)        // on: every trade's count is the town's again (villagers set one by one stay set)

TownEventKind.TradeChanged              // Villager, Index = new trade, a flag: by the town or by the player
ShopItemKind.Training = 22              // one row, ten steps
```

1. **The choice of work:** in `Consider` and `RConsider` a job of the villager's own trade counts `TradeLean` times its rate (start 1.5; tune between 1.25 and 2). A Helper weighs every job at 1. Keep it a field read, not a delegate (hot path).
2. **The reward:** at its own trade a villager's work time is divided by `1 + TradeWork + Training x TradeStep`, and a delivery of its trade's good is worth that much more with `TradeWorth` and the same step; a villager in the trade it likes gets `TradeLikes` more on both. The bots' estimate of a purchase's worth knows the Training row.
3. **The town fills trades** (while `TownFillsTrades`): a villager with no trade, not set by the player, takes one within a second: the open trade, not set by the player, whose members earned most a head over the last minute (a smoothed figure kept at each delivery); the one who likes that trade goes first, then the lowest number. And it rebalances: at most one villager every `TradeMoveEvery` seconds (start 20) moves from the trade whose members earn least a head to the one whose members earn most, when the gap is over `TradeMoveGap` (start 20 percent); never a villager set by the player, never the one whose card is open (the game says which), never into or out of a trade whose count the player set, and a trade keeps at least one member while it is open.
4. **Your count:** `SetTradeCount` up takes Helpers first (the one who likes the trade first), then a member of the largest town-decided trade; down gives members to the Helpers (the one who likes it least, never one set on its card while another is left). `SetVillagerTrade` sets one.
5. **Determinism and cost:** same seed and same calls at the same ticks, same town; by index, no sets or dictionaries; no allocation in the tick (`InvariantTests.TickAndStatsAllocateNothing` with trades on and a rebalance inside the measured window).
6. **The save:** the fields above with safe defaults (an old save loads with every villager a Helper and the town filling: within a second each they take trades, so an old town's pace from then on differs a little: say so in the notes). Keep versions 2 and 3 if the defaults allow, as the Regulars fields did. A test: with `TownFillsTrades` off and every villager a Helper, a run is tick for tick the run before this step (pin a hash from the main branch before you change a rule).
7. **The `people` tool** gains the trades: per age and bot the share of time with more than three quarters of the villagers on one job, the share of a trade's trips made at its own trade, hat changes a minute, and options to play "careful" (keeps counts at the mix the best-pay rule would choose and trains) and "careless" (everybody a carrier; or everybody a Helper with the town's filling off).

## Part 2: the interface

**The Trades card** (`HudZones.Person`, 156 high, shown when `TradesShown` and no villager is selected): heading "Trades" (title, serif) with the switch at the right; one line of 28 per open trade and the Helpers last: icon 24, name (body), the stepper at the right (minus, the count in a fixed cell with tabular digits right-aligned, plus). Hover on a line: a band as the shop's rows, and its members ringed on the map. States of the pills as the shop's (rest, hover, pressed, disabled). Plus is disabled when there is nobody to take; minus at nought and at the last member of an open trade; a disabled pill's tooltip says why. Tooltip per line in the shop tip's form: "Reapers bring in the grain." then Members, Places, Waiting now, Away now, Bonus ("work +20%, goods +10%"), and "You set this count. The town leaves it alone." when so.

**The villager's card** (156 high): title "Joss · Reaper" (name bold, trade muted); line 2 the like and the home; lines 3 and 4 as now; the last line "Trade" and one cap of 40 x 32 per open trade, the Helper last: current = darker paper with an ink border of 2; the liked one with the corner fold; each with a one-line tooltip ("Reaper: likes this work, +5%"). A click sets the trade.

**The shop:** the row "Training" in the People group after Load, no key; it appears with the Trades card. Rules S1 to S9 hold; its name and line 2 are in the layout fixture.

**On the map:** the hover tag "Joss, reaper"; the tag for 2 seconds on the first three villagers the town assigns; one hint, once, when the card first shows: "Every villager has a trade. Change them on the card at the left, or leave it to the town."

**Rules:** nothing in one frame (0.15 s or longer, at 30, 60 and 120 frames a second); no allocation per frame; text made only when what it says changes; nothing overlaps or is cut at 1920 x 1080 and 1280 x 720, at interface sizes 80, 100 and 150 percent (the fixture holds the longest names); the two cards never show at once; the middle of the map stays free.

## Part 3: the figures

- A villager wears its trade's headwear (the models of the board's round 2; the miller its apron too); a Helper hair only. A change of trade changes the head with a short fade or a small puff, never in one frame.
- The tunic list as the board's round 2 recommends, so no tunic takes a hat's colour.
- `game-ink` passes with a group in mixed headwear; no allocation per frame in `TownVillagers`.

## Part 4: the tuning (the bands do not move; reasons and tables per constant, as the retune did)

1. **Left alone** (the town fills, nobody touches a trade, the bots buy Training like any row): every pacing test passes with the margins the retune left; the first age by real input within a minute and ten purchases of the figure recorded after the retune.
2. **Careful** against left alone: at most about 8 percent sooner to the charter.
3. **Careless:** at most about 15 percent later, and never an idle villager.
4. **Seen:** villagers with a trade make at least 70 percent of their trips at it; the share of time with more than three quarters of the villagers on one job well under today's; hat changes by the town no more than about three a minute. Give the figures.
5. Order: `TradeLean` (how much they stay), then the two bonuses so that point 1 holds, then Training's price and step so its payback sits among the other rows'.

## Tests and pictures

- `sim.tests`: the lean (a member leaves its trade only above the factor), the reward, the filling and the rebalance (never the player's, never more than one per interval), your count and one villager's trade, determinism, no allocation, old saves, the pinned run with trades off.
- A check step `game-trades` (at 30, 60 and 120 frames a second): the card shows and hides with the villager's card by a crossfade; plus and minus move the count and a villager's head; the hit areas; the hover rings; the Training row buys; no Helper wears a hat; nothing changes in one frame; bytes per frame.
- `game-layout` knows both cards and the Training row on every canvas.
- Pictures (real frames): the board's states again, now from the game, beside the board; a strip of a count being changed; a street at the play view before and after; each at 1920 x 1080 and 1280 x 720. A reviewer who did not build it reads them before it is merged.

## What to deliver

A progress page `docs/plans/n7/trades-progress.md` written as you go (what is built file by file, the tuning tables, the tests replaced with old and new assertion and why, what is not done); the exact result lines of `sim.tests`, `game-trades`, `game-people`, `game-work`, `game-layout`, `game-playthrough`, `game-river`, `game-motion`, `game-ink`, `game-save`; the pictures.

# Trades, step 3, part 1: the second age's trades in the simulation (progress)

Branch `wt/n7-trades2`. Brief: `docs/plans/n7/trades2-build-sheet.md`, "The three trades" and "The simulation" (items 1 to 6), and the `sim.tests`.

## The names commit (the second builder starts here)

**Commit: NAMES_COMMIT** ("Snapshot: trades 2, the names"). In that commit the three new trades are named and open with their places, and the views and calls take them, but the town does not fill them and no job belongs to them yet (`TradeOfJob` and `TradeOfLeaning` as before), so nobody is in them unless a call puts someone there. Every test passes; the pin with trades off, the new pin of the first age with trades on, and a pin of both ages with trades on (recorded before any change) are identical.

### The names as built

```csharp
public enum Trade : byte { None = 0, Water = 1, Grain = 2, Flour = 3, Wool = 4, Cloth = 5, Boat = 6 }   // appended
// Wool: shepherds, Cloth: weavers, Boat: porters (grain and flour to the landing)

Town.TradeCount = 7                    // including None (index 0, the Helpers); every array by trade has 7 entries
Town.FirstAgeTrades = 4                // the first age's trades are 1..3; the second age's 4..6

town.TradeOpen(Trade.Wool)             // the pasture given (second age only)
town.TradeOpen(Trade.Cloth)            // the weaver's cottage given
town.TradeOpen(Trade.Boat)             // the landing given
town.TradeInfo(t).Places               // Wool: TownRulesRiver.PasturePlaces (3), Cloth: WeaverPlaces (2), Boat: 0 (the landing has no work places)
```

Everything else is step 2's names, now taking the three new values: `TradeInfo`, `SetTradeCount`, `SetVillagerTrade`, `CanRaiseTrade`, `CanLowerTrade`, `TradeEarnPerHead`, `VillagerView.Trade` / `.Likes`, `TownEventKind.TradeChanged` (Index / Amount up to 6), `TownState.TradeCountByPlayer`, `TradeWant`, `TradeEarn`, `TradeAt` (length 7).

On the cards (the game's stand-ins in `TownHud.Trades.cs`, `TownHud.PeopleTrade.cs`, `TownHud.TradesParts.cs`, by `(int)Trade`): "Shepherds", "Weavers", "Porters"; one villager "shepherd", "weaver", "porter"; tooltips "Shepherds bring in the wool.", "Weavers make the cloth.", "Porters carry grain and flour to the boats."; icon names `trade-wool`, `trade-cloth`, `trade-boat` (stand-in: the coin while missing).

Saves: a save from before has four entries by trade; it loads with the three new ones at their defaults (nobody in them, their counts the town's, no earnings). Versions 2 and 3 kept.

### Game code touched (only so nothing indexes past the end with seven trades)

- `game/scripts/town/TownHud.Trades.cs`: `TradeNames`, `TradeFullNames`, `TradeOne`, `TradeIcons` grown to seven (the sheet's names). `TradeOrder` (the card's lines) unchanged: the second builder's.
- `game/scripts/town/TownHud.PeopleTrade.cs`: `TradeTitle` grown to seven. The caps (four) unchanged.
- `game/scripts/town/TownHud.TradesParts.cs`: `TradeSentences` grown to seven (the sheet's tooltips).
- `game/scripts/town/TownVillagers.Heads.cs`: `HeadOf` gives the second age's trades a Helper's hair (there are six heads; their headwear is the figures' builder's).

## The pins (recorded before any change)

`sim.tests/Town/TownTradesPinTests.cs`:
- `WithTradesOffARunIsTheRunBeforeTrades` (as step 2: trades off, both ages, hashes from `70922b8`).
- `WithTradesOnTheFirstAgeIsAsBeforeTheSecondAgesTrades` (new): seeds 1 to 3, payback and cheapest, trades on as shipped, the first age to the ring; every 10 ticks coin, renown, level, purchases, trade changes and every villager's job, phase, trade and position. Recorded at `db85993` before any change.
- `WithTradesOnBothAgesAreAsBeforeStep3` (names commit only): the same run as the trades-off pin with trades on, both ages (8 minutes of the second). Recorded at `db85993`. The rules change the second age, so the rules commit replaces it (below, "Tests replaced").

# Trades, step 2, part 1: the simulation and its tuning (progress)

Branch `wt/n7-trades`. Brief: `docs/plans/n7/trades-build-sheet.md` part 1 (the simulation), part 4 (the tuning) and the `sim.tests` of "Tests and pictures".

## The names commit (the second builder starts here)

**Commit `a2ce2a5`** ("Snapshot: trades, the names").  In that commit trades do nothing yet: the lean is 1, every bonus 0, the town does not fill trades yet (the rules come in the next commits), the Training row is listed but locked ("Comes with the first trade") while no villager has a trade. Every test passes and the pinned run is identical.

### The names as built

```csharp
public enum Trade : byte { None = 0, Water = 1, Grain = 2, Flour = 3 }    // sim/Town/TownTrades.cs; None = a Helper

// VillagerState:  Trade Trade;  bool TradeByPlayer;                         (set on its card by the player)
// TownState:      bool TownFillsTrades = true;  bool[] TradeCountByPlayer (by Trade, index 0 = the Helpers);  int Training;
//                 int[] TradeWant (the count the player set);  double[] TradeEarn (members' coin, about the last minute);
//                 double TradeMoveAt;  long TradeChanges, TradeChangesByTown   (counters)

public struct TradeView { bool Open; int Members, Places, Away, Waiting; bool ByPlayer; double WorkBonus, WorthBonus; }

town.TradeOpen(Trade t)                 // its workplace exists (Water from the start, Grain with the first field, Flour with the mill)
town.TradesShown                        // true from the first field on
town.TradeInfo(Trade t)                 // TradeView; TradeInfo(Trade.None) is the Helpers' line (Places 0, no bonus)
town.Helpers                            // villagers with no trade
town.TownFillsTrades                    // the switch as it stands
town.VillagerLikes(int i)               // the trade villager i likes (None while that trade is not open)
VillagerView.Trade, .TradeByPlayer, .Likes
Town.TradeOfJob(JobKind j)              // static; wool and cloth jobs: None (their trades come with step 3)
Town.TradeOfLeaning(Leaning l)          // static
town.TradeWorkBonus, town.TradeWorthBonus   // fractions, Training included, without the like
town.TradeEarnPerHead(Trade t)          // coin a head the trade's members earned lately (the figure the town fills by)   [rules commit]

town.SetTradeCount(Trade t, int n)      // yours from now on (Trade.None: the Helpers' count); false if nothing changed
town.SetVillagerTrade(int id, Trade t)  // that villager is yours from now on
town.SetTownFillsTrades(bool on)        // on: every count is the town's again (villagers set one by one stay set)
town.SetOpenCard(int id)                // the game says whose card is open (-1 none); the town never moves that villager

TownEventKind.TradeChanged = 33         // Villager, Index = (int)new trade, Amount = (int)old trade, Level = 1 by the player / 0 by the town, X, Y
ShopItemKind.Training = 22              // one row, ten steps (TownRules.TrainingMax); town.Training, town.TrainingPrice,
                                        // town.TrainingLockReason, town.BuyTraining(); listed from TradesShown on, after Load
```

Reasons on the Training row: "Comes with the first trade" (locked: nobody has a trade), "All training done" (finished).

### Game code touched (only what the shop needs to list the row)

- `game/scripts/town/TownHud.Shop.cs`: `Training` in the People group, rank 35 (after Load, no key), a track (its step number shows).
- `game/scripts/town/TownHud.ShopText.cs`: its line "Better at their trades" and help sentence (stand-ins; the interface builder writes the real ones).
- `sim/Town/TownPacing.cs`, `TownRiverPacing.cs`: `ByKind` arrays sized 23 (Training = 22).

## The pinned run

`sim.tests/Town/TownTradesPinTests.cs`: seeds 1 to 3, the payback and the cheapest bot, the first age to the charter, rung in, 8 minutes of the second age; a SHA-256 of coin, renown, level, purchases and every villager's job, phase and position every 10 ticks. Recorded on the main branch (`70922b8`) before any change (the same test run on `git archive` of it passes); with `SetTownFillsTrades(false)` at the start (every villager a Helper) the branch must give the same hashes.


## From the game, round 3 part 1 (r3-game1)

- `ShopItem` could carry `Locked` and `Finished` flags. The game now reads them from `Reason` text ("Unlocks at level", "Comes with", "... reached", "at its best"); a reworded reason would silently change the shop's look.
- `CanPave(r)` and `CanExtend(p)` ignore coin; the game adds `Coin >= PavePrice(r)` / `ExtendPrice` itself so that what cannot be bought now does not light up. A `CanPaveNow` / `CanExtendNow` would keep that rule in one place.
- `Hint` builds a new string each read while a Load step is fresh (`$"The {name} is ready..."`); a cached string per step would keep the game's per-frame allocation at zero.
- At the charter with every extension on sale bought, `ShopItems` lists Extend with "Unlocks at a later level" (there is none), while `Hint` still says "Every cottage is full. Extend one for another villager." with a `HintPlot` that cannot be extended. The game shows the item as finished at the last level; the hint should not point at an extension that cannot be bought.

## From the game, round 3 part 2 (r3-game2)

- `Town.OfferOf(OfferKind)` is internal; the game wants "is the mill (or the stall) bought" for the first age's page and finds it through `PlotWorking` of the plot of that kind. A public `OfferBoughtOf(OfferKind)` would say it directly.
- Achievement descriptions print numbers without a thousands separator ("Earn 20000 coin in all."); the game prints coin as "20,000" everywhere else.
- `RingNextAge` emits `NextAge` and nothing more; the game builds the whole moment (peal, music, page). When the second age exists the game needs a way to start it (or to know it is not there: today it says so in words).

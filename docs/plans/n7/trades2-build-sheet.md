# Trades, step 3: the second age's trades (the lead, 28 November)

The owner: "Fix the boats and the second ages trades."

Step 2 gave every villager a trade in the first age (carrier, reaper, miller). In the second age the wool and cloth work has no trade yet, so villagers leave their trade for it (trips at the own trade 69 to 74 percent there) and the town is more than half reapers. This step adds the second age's trades. It builds on `trades-build-sheet.md` (its decisions stand) and on what is merged (`docs/plans/n7/trades-progress.md`, `trades-game-progress.md`).

## The three trades

| Trade | On the cards | Its work (jobs) | Opens with | Likes |
| --- | --- | --- | --- | --- |
| Shepherd | Shepherds | wool, to the market or the landing (`WoolMarket`, `WoolBoat`) | the pasture | the villagers whose leaning is wool |
| Weaver | Weavers | cloth, to the market or the landing (`ClothMarket`, `ClothBoat`) | the weaver's cottage | the villagers whose leaning is cloth |
| Porter | Porters | grain and flour to the landing (`GrainBoat`, `FlourBoat`) | the landing | nobody |

- **Porter** is the "boatman's hand" of the proposal under a name that does not collide with the extra hands. It takes the boat work away from the reapers and millers, who keep the home and market work: the largest trade gets smaller, which the review of step 2 asked for.
- One villager: "shepherd", "weaver", "porter". Tooltips: "Shepherds bring in the wool.", "Weavers make the cloth.", "Porters carry grain and flour to the boats."
- The lean, the reward, the like and Training are step 2's, the same constants, for all six trades. Training stays one row for all.

## The simulation (commit the names alone, first)

```csharp
public enum Trade : byte { None = 0, Water = 1, Grain = 2, Flour = 3, Wool = 4, Cloth = 5, Boat = 6 }   // appended
```

1. `TradeOfJob`: wool and cloth jobs to their trades; `GrainBoat` and `FlourBoat` to `Boat`. `TradeOfLeaning`: wool and cloth. `TradeOpen`: the pasture, the weaver's cottage, the landing given. Every array by trade grows to seven; saves from before load with the new entries at their defaults and keep versions 2 and 3 if the defaults allow.
2. **The town's filling** takes the new trades in as it does the old (a member at once when a trade opens; floors; then by the coin a head of each trade's work). The floors: the pasture and the weaver's cottage have places; the porters' floor is yours to set by measuring (the landing has no work places: by berths, or by the share of boat deliveries).
3. **Old saves and towns in the second age** take the new trades within seconds, as old saves did with step 2.
4. **The `people --mix` tool** shows six trades. **Targets for the second age** (seeds 1 to 10, four bots; give the tables before and after): no trade holds more than about 40 percent of the town on average; trips at the own trade at least 80 percent; hat changes by the town no more than about three a minute; the share of time with more than three quarters of the town on one job no higher than now (26/19/24/22 percent).
5. **Pacing:** every band test passes; the bands do not move; reasons per constant; `bands` before and after for both ages. The first age must come out exactly as now (nothing of this opens before the ring-in): show it. The boats are being rebuilt at the same time and move the second age too: tune last, say what room you leave, expect one more pass after both are merged.
6. Tests: the new jobs' trades, the opening, the filling, your count and one villager for the new trades, old saves, no allocation, the pinned run with trades off unchanged.

## The interface

- **The Trades card with seven lines** (six trades and the Helpers) is 244 high and fits the left column everywhere except the smallest canvases (1280 x 720 with the interface at 150 percent has 183). So the card has **two forms, like the shop's rows:**
  - **Full:** as now, a line of 28 per trade, only the open trades and the Helpers; the card as tall as its lines, growing downward as trades open (it never moves anything else; the villager's card takes the same height when it replaces it).
  - **Compact** (when the full card does not fit its room): two columns of cells, each the trade's icon, the count and the two pills, no name (the name is in the tooltip); the Helpers' cell last. Same states, same hit areas, same rules for bold and muted counts.
  - The switch between them only when the canvas or the number of open trades changes, never while the pointer is on the card.
- **The villager's card:** up to seven caps on the Trade line. When they do not fit with the word "Trade" before them, the word goes and the caps take the line; if they still do not fit, the caps narrow to no less than 32 units. The title, the like and the rest as now.
- **Tags, tooltips, the hover rings, the hint** work for the new trades without new rules. No new hint.
- **Not in this step:** pips on the workplaces and "who works here" on a click.

## The figures and icons

- Three headwears, each a real model made in code with its ledger row, told apart from the three of step 2 and from a Helper's hair **from above at the play view**, on every tunic colour, alone and under a load: proposals to be shown on a board before the game uses them (the hat-by-tunic grid as in `mockup/hats-round2.png`). The palette is the style sheet's; a hat's colour must not be a tunic's (the tunic list may lose a colour for it: say which).
- Three icons in the shop's family at 24, told apart at 19 pixels.

## Tests and pictures

- `sim.tests` as above; `game-trades` covers the new trades, the compact card and the seven caps; `game-layout` knows the taller card and the compact form on every canvas; `game-ink` with a group in all six headwears.
- **Every check step that watches the town in the second age is run with the trades in before reporting** (`game-river`, `game-work-places`, `game-town`, `game-playthrough`, `game-perf`, `game-motion`, `game-people`, `game-work`): yesterday three whole checks each failed on a step nobody had run.
- Pictures from the real game at 1920 x 1080, 1280 x 720 and 1280 x 720 at 150 percent: the full and the compact card, the villager's card with seven caps, a second-age street before and after at two moments, with the town's own mix. A reviewer who did not build it reads them before the merge.

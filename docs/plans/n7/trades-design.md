# Trades: a role for every villager, and how you give it (the lead, 27 November)

The owner: "I would like each villager to have a role and that it can be assigned. Like I would like to add that as a full system."

**After the board and its review (16:09 UTC) the build sheet `trades-build-sheet.md` holds the decisions; where this page differs from it, the sheet is right.** The main changes: the unassigned villager is a Helper and wears no hat; the town gives every villager a trade; plus and minus set your own count, which the town then leaves alone; one Training row for all trades; no status on a trade's line.

Step 1 made villagers people (a name, a card on a click, bodies that keep apart). The retune (`retune-sheet.md`) makes them spread by themselves. This page is step 2: the trades of the first age. Step 3 (the second age's trades and who works where) is sketched at the end. The research behind it: `villager-roles.md`, `research/design-options.md`, `research/sim-roles-experiments.md`.

## What the player gets

1. **Three trades in the first age**, each open when its workplace exists:

| Trade | Its work | Opens with | Seen on the figure |
| --- | --- | --- | --- |
| Water carrier | water from the well | the well (from the start; shown from the first field on) | a blue kerchief |
| Reaper | grain, home or to the market | the first field | a wide straw hat |
| Miller | flour: field, mill, market | the mill | a white flat cap and a dusty apron |
| Hand | whatever pays best (today's rule) | always | bare head or hood, as now |

   The camera looks down on the town, so **the head is what you see**: one headwear per trade, in one flat colour each, readable at the normal view. A villager working away from its trade keeps its hat (that is how you see a reaper helping at the well).

2. **A trade is a lean, not a wall.** A villager with a trade takes its own work unless other work pays clearly more (a factor, `TradeLean`, to be tuned between 1.5 and 2). When its own workplace is full it helps elsewhere and comes back by the same arithmetic. Nobody ever stands idle because of a trade.

3. **A trade gives something back.** At its own trade a villager **works faster** (seen in the work's rhythm) and **its goods fetch a little more** (the craftsman's price). Without that, the research measured, any assignment only costs: 5 to 11 percent of the age.

4. **Train a trade** (one new row in the shop's People group, "Train", which names the trade it suggests, as Pave suggests a road): each step raises that trade's two bonuses a little; ten steps a trade, a milestone at 5 and 10.

5. **A villager likes one kind of work** (it has, since step 1: a pure function of the town's seed). In the trade it likes it gets a small extra. The card says "Likes the sickle". This is the reason to look at one villager and give that one a trade by hand; nobody has to.

6. **The town fills trades by itself** ("Keep trades filled", on by default): when a trade opens, a villager arrives or a workplace gets another place, an unassigned Hand takes the open place, the one who likes that work first. It never changes a villager the player has set. A player who never touches trades loses nothing against today and sees a town in hats, spread over its work.

## Where it is on the screen

The assignment is free, so it is **not in the shop** (which is full: the late first age already needs compact rows at 1280 x 720). It lives with the people, in the left column under the ledger, in the zone the villager's card uses (`HudZones.Person`). One zone, two faces:

**The Trades card** (shown from the first field on, when nothing is selected; 156 high):

```
Trades                              Keep filled [On]
[kerchief]  Water carriers            ( - )  1  ( + )
[straw hat] Reapers      1 helping    ( - )  4  ( + )
[cap]       Millers                   ( - )  2  ( + )
Hands 5 · take whatever pays best
```

- A heading line (the card's title in the serif, as the shop's; on its right the switch: caption "Keep filled" and the small On/Off pill of the shop's "On" state).
- One line per open trade, 28 high: the trade's icon (20), its name (body), then in caption, muted, only when true: "1 helping" (members working away from the trade right now) or "2 waiting" (members in a line), then the stepper at the right: minus, the count in tabular digits, plus. Minus moves one member to the Hands (the game picks: one the player did not set, the one who likes it least); plus takes a Hand (the one who likes it most). Plus is disabled with no Hand left, minus at nought. Hover, pressed and disabled states as the shop's pills.
- A foot line in caption, muted: "Hands 5 · take whatever pays best".
- The tooltip of a trade line (the tooltip family, `TipRect`): "Reapers: 4 villagers for 4 places at the fields. Work 20% faster and sell grain for 10% more. Trained 3 of 10." All figures real.
- A trade not open yet has no line. Before the first field there is no card.
- Nothing on the card moves when a count changes; the lines never reorder.

**The villager's card** (on a click, in the same zone, in place of the Trades card; the cross or a click on the map brings the Trades card back; 152 high):

```
Joss                                              x
Lives by the well lane · likes the sickle
On the way to the well
160 trips · earned (o) 1,921
Trade  [hand] [kerchief] [straw hat*] [cap]
```

- The new last line: "Trade" and one small icon cap per open trade plus the Hand, the current one filled (wheat), the others outlined; the one the villager likes carries a small dot. A click sets the trade (and marks the villager as set by the player). Each cap has a tooltip ("Reaper: likes this work, +5%").
- The hover tag on the map says the trade after the name: "Joss, reaper".

**In the shop:** one row, in the People group after Load: icon (the suggested trade's), "Train the reapers 3", line 2 "Reapers work and sell 4% better", the price pill. No key. It appears when the first trade besides the water carrier is open.

**One hint**, once, when the Trades card first appears: "Your villagers take up trades. Change them on the card at the left, or leave it to the town."

## The simulation

- **Data:** per villager a trade (a byte: None, Water, Grain, Flour; Wool, Cloth and Boat later) and whether the player set it; per trade a training level; one switch for "keep filled". Old saves load with every villager a Hand and the switch on; they then fill by the rule (so an old town's pace from then on can differ a little: said in the notes). With the switch off and every villager a Hand the simulation is tick for tick what it was (a test pins it).
- **The choice of work** (`ChooseJob`): a job of the villager's own trade counts `TradeLean` times its rate; nothing else changes. The wait estimates (with the retune's fixes on) do the spreading inside a trade.
- **The reward:** in its own trade a villager's work time is divided by `1 + TradeWork + level x TradeWorkStep` and a delivery of its trade's good is worth `1 + TradeWorth + level x TradeWorthStep` (plus `TradeLikes` on both when it likes the trade). The bots' estimates of what a purchase is worth must know the Train row.
- **Filling:** when a place opens or a Hand appears: the trade with the most open places per member, the Hand who likes it most, lowest number first. At most one change a second. Deterministic: by index, no sets.
- **Calls and views** in the style of `docs/plans/town-sim-api.md`: set a trade's count, set one villager's trade, the switch; per trade: open, members, places, helping, waiting, level, the two bonuses now; per villager: trade, set by the player, likes.
- **No allocation in the tick; same seed and same calls, same town.**

## The tuning (the bands do not move)

1. **Leave it alone** (keep filled on, nobody touches a trade, the bots buy Train like any other row): every pacing test passes, with the margins the retune left; the first age by real input within a minute and ten purchases of the figure recorded after the retune.
2. **A careful player** (a bot that keeps trades at the mix the best-job rule would choose, and trains): at most about 8 percent faster to the charter than leaving it alone. Trades are a pleasure, not a duty.
3. **A careless player** (everybody a water carrier; or all trades emptied and the switch off): at most about 15 percent slower, and never an idle villager.
4. **Seen:** with trades filled, the share of time with more than three quarters of the villagers on one job falls well below today's (first age 26 percent with the payback bot); give the figure.
5. `TradeLean` first (it sets how much people stay at their trade), then the two bonuses so that point 1 holds, then the Train row's price and steps so its payback sits among the other rows'. Reasons and tables per constant, as in the retune.

## The assets (each made in code, each with a ledger row)

- Three headwears for the figure: kerchief (blue), wide straw hat, flat cap (white) with a dusty apron for the miller; checked from the normal view, from above, and under `game-ink`'s ceilings for villagers.
- Icons in the shop's family: the three trades (kerchief, straw hat, flat cap), the Hand, "Train".

## What it must not do

- No villager idle because of a trade; no hard cap that refuses a click without saying why.
- Nothing in the middle of the map; no new zone; the left column's two cards never show at once; nothing overlaps at 1280 x 720 or at the interface sizes 80 and 150 percent (the card is at most 156 high: at 150 percent the column has 164).
- Nothing changes in one frame; no allocation per frame; the shop's rules S1 to S9 hold for the new row.
- No change to prices, boats, tolls or keys; the save format's version only if a default cannot be safe.

## Step 3, sketched (after step 2 is played)

Shepherd, weaver and boatman's hand in the second age (the Trades card grows to six lines only there; if that is too tall at 150 percent the card scrolls like the shop); headwear for each; a workplace's tag shows who works there (pips), and a click on a workplace lists them.

## How it will be shown and judged

1. Before building: one board, **now beside planned**: the left column today (the ledger and the villager's card) beside the Trades card and the villager's card with its trade line, at 1920 x 1080 and at 1280 x 720; the shop's People group with the Train row; and a street of figures today beside the same street in hats, at the normal view and close.
2. A reviewer who did not draw it criticises it against the layout sheet and the style sheet; the fixes go into the build sheet.
3. After building: real frames of the same states, a strip of a count being changed, the checks, and a Windows build.

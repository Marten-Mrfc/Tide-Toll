# Villagers as people: the build sheet for step 1 (the lead, 27 November)

The owner on `villager-roles.md`: "Okay start all of them implement them." The seven decisions are taken as recommended there: step 1 first; the two blind spots fixed; a trade is a lean; a trade gives something back; assign by count and on the card; a name and a card for every villager; bodies as a look only. Walkers keep to the right. This sheet is step 1, "people you can point at, who spread by themselves". Step 2 (trades) follows on top of it and is not in this sheet.

Read first: `villager-roles.md`; `research/code-map.md` (where everything is); `research/bodies-and-picking.md` (the design for picking and bodies, with its build order: follow it); `research/sim-roles-experiments.md` (the blind spots, measured, and its part 3; its prototype is on branch `wt/n7-roles`, for reading only); `docs/plans/n5/villagers-progress.md` (the line and the poses just built: your work stands on top of them and must not break the check step `game-work`).

## Part 1: the simulation (this step may change how the town plays; say by how much)

1. **The two blind spots.** A villager choosing work counts, for the mill's wait, the villagers who chose flour and have not reached the mill yet; and for the pasture and the weaver, those on their way there. (`ExpectedWait`, `SiteWait`.)
2. **A leaning.** Each villager has, from the town's seed and its number, a small natural leaning toward one kind of work: its rate for that work counts a little more when it chooses (start at 10 percent; tune so the play-through stays near where it is). Two villagers at the same door then need not choose alike. No random numbers at play time; the same seed gives the same town.
3. **A name** for each villager, a pure function of seed and number, from a list of plain old-world given names written for this game (no real people, no trademarks), with a by-name if two share one ("Wat the younger").
4. **Two counters** per villager that no rule reads: trips made, coin earned.
5. **The views** give all of it to the game: name, leaning, trips, earned, and also what the villagers' builder had to estimate: the work spot's `Slot`, the work `Stage` and the seconds of work left (`WorkLeft`).
6. **The save:** the counters are saved; old saves (versions 2 and 3) load with the counters at nought and the rest derived. A migration test.
7. **Pacing.** The play-through by real input reads `charter=33.9min buys=201` today and the bots have their own figures (`tools/TideToll.Tools`, `town`). After parts 1.1 and 1.2 measure again. The charter must stay within a minute and the purchases within ten of today's; if not, make the leaning smaller before anything else. Record the new figures; the pinned figure in `game/tests/PlayThrough.cs` and `tools/check.sh` is then replaced by the new one, written down with the reason. Also look at the oddity the research found: in the second age a poorer town reaches the seal sooner, because the levels there follow the town's best coin per minute. Say what you find; do not change it without saying.
8. Tests in `sim.tests` that pin which job a villager takes are replaced by the equal test for the new rule, each written down.

## Part 2: pointing at a villager

As `research/bodies-and-picking.md` recommends: a pick in code, in screen space, no physics; the nearest figure within a generous radius wins and the hover sticks to it; a ring under the feet at once and a name tag after the tooltip's delay; a click selects and **docks a card in the left column under the ledger** (a new zone in the layout: the ledger's width, about 120 tall, the card style of the interface): the name, where they live, their leaning in plain words ("a good hand with water"), what they carry, trips made, coin earned. A click elsewhere or on its cross closes it. Off in the Pave and Extend modes, over the interface, and while paused; a click on the interface never picks. The selected villager keeps its ring while the card is open.

The layout check (`game-layout`) learns the new zone: it overlaps nothing on any of its canvases, and it moves nothing else.

## Part 3: bodies, in the drawing

One pass in place of today's `Spread`, as the research designs it: walkers keep to the right by the way they go, with a wider offset on a paved road; a follower keeps a figure's distance behind the one ahead (bounded, eased); people standing at the market, a cottage or the landing take spots of their own; a bounded push for what is left that never puts anyone in a building or in crop; a neighbour grid instead of comparing every pair. The line of the first-age workplaces (`TownVillagers.Line.cs`) stays as it is. The number of villagers that can be drawn rises from 96 to 200.

## Limits and judging

- `docs/plans/builders-rules.md` holds the rules for everyone.
- A new check step `game-people`: a pick of a projected villager returns that villager, also in a crowd; the hover does not flicker between two neighbours; no pick in the modes, over the interface or while paused; two walkers going opposite ways on a road are on opposite sides; a follower is never closer than a set distance for longer than a second; no two standing people share a spot; nothing changes in one frame; nothing allocated per frame by the villagers or the pick; the frame inside `game-perf`'s gates.
- Pictures from the real game: the hover ring and name, the card, a busy road before and after (the strip tool `tools/work-strips.sh` shows how), read by you before you report.
- The order of worth if time runs short: part 1 with its figures; the pick, ring and card; lanes; following distance; standing spots.

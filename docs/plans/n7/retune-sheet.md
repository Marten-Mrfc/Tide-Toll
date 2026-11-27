# Spreading on: retune the ages (the lead, 27 November)

Step 1 of the role system ("villagers as people") is merged with every behaviour switch **off**: the town plays exactly as before. The fixes that make villagers spread are built and wait behind constants, because each of them tips a pacing test by a hair. This job switches them on and brings the pacing back by tuning. It is the part of the owner's wish ("they all start doing the same task. That is not the idea") that actually changes what villagers do.

Read first: `docs/plans/n7/people-progress.md` (the section "What ships in step 1" and everything it collected about the second age: which fix tips which test, on which seed and bot, by how much), `docs/plans/n7/research/sim-roles-experiments.md` (why they herd; the measurements), `docs/plans/n7/villager-roles.md` (the proposal), `docs/plans/incremental-design.md` and `docs/plans/age2-design.md` (how the ages are paced and why), and the pacing tests themselves (`sim.tests/Town/*Pacing*`, `TownRiverPacingTests`).

## What is switched on

| Constant | What it does | Measured effect on herding (share of time with more than three quarters of the villagers on one job) |
| --- | --- | --- |
| `TownRules.MillWaitCountsOnTheWay` and `...InSecondAge` | A villager choosing flour counts those who chose flour and have not reached the mill yet | first age 26 to 22 percent (payback bot) |
| `TownRules.SiteWaitCountsOnTheWay` | The same for the pasture and the weaver | second age 55 to 41 percent (payback), 46 to 32 (cheapest) |
| `TownRules.LeanBonus` | A small natural leaning per villager | little; adds variance from seed to seed. Leave at 0 unless you can show it helps without cost; trades (the next step) will give it meaning |

## The rule that must be fixed first

In the second age a level costs so many minutes of the town's **own best** income (`LevelRate = max(LevelRateMin, rate at the ring, MaxCoinPerMinute)` in `sim/Town/TownRiver.cs`). So a town that earns less at its best reaches the seal sooner: across the bots' runs the correlation between coin a minute and the seal's minute is about nought, and a spread town (lower peaks) seals a minute sooner than a herding one. That rewards playing worse, and it is why spreading tips the second age's tests. Replace it by a rule under which earning more never makes the age longer and earning less never makes it shorter (for example: the cost of each level follows a fixed curve, anchored on the income at the ring-in and a set growth per level, not on the town's later peaks). Show, with the bots, that a richer run now seals no later than a poorer one on the same seed.

## The tuning

- **The bands are the design and do not move.** Every test of `sim.tests` passes when you are done, including the pacing bands of both ages for every bot and seed.
- **Tune, do not search for a pass.** For each constant you change: the reason, the old and new value, and what it does to the means per bot. Prefer few constants. Start by restoring the mean charter minute and the mean seal minute per bot to what they are on the main branch today (give the "before" table first).
- **Leave room.** Today several tests pass by a hair (the people builder's notes list four that tip with a first-age change alone: a second age of 19.6 minutes against a floor of 20; 3.4 minutes from drawbridge to seal against 3.5; a share of 0.33 against a third; 61 seconds with nothing affordable against 60). After your tuning, list for every band test the run nearest its edge and its margin, and aim for no margin under about three percent of the band's value. If a band cannot be given room without moving it, say which and why; do not move it.
- **The first age by real input** (`game-playthrough`) reads `charter=33.9min buys=201` today. It may move by at most a minute and ten purchases; record the new figure, which then replaces the pinned one in the test's notes and in `tools/check.sh` if it is pinned there, written down with the reason.
- **Nothing else about play changes:** prices, what boats want and pay, the crane, the tolls' meaning. The save format stays; a town saved before still loads and plays on (its pace from then on may differ: say so).

## What to deliver

1. The tables: per bot and seed, both ages, before (the main branch) and after: charter minute, purchases, seal minute, coin a minute, herding share.
2. Every band test's nearest run and margin, before and after.
3. The constants changed and the new level-cost rule, in `docs/plans/n7/retune-progress.md` and at the constants.
4. All of `sim.tests` passing; `game-playthrough`, `game-river`, `game-town`, `game-work`, `game-work-places`, `game-people`, `game-save` passing (under the heavy-run lock where `docs/plans/builders-rules.md` says so).
5. What trades (the next step) should know: where a trade's factor would enter, and how much room the bands now leave for it.

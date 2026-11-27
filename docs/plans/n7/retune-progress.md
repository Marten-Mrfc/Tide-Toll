# Spreading on: the ages retuned (n7, 27 November)

Branch `wt/n7-retune`. Brief: `docs/plans/n7/retune-sheet.md`. Everything here is measured on seeds 1 to 10 with the four bots, as the pacing tests play them.

## Where it stands (read this first)

- **Done.** The second age's level rule is replaced (below), the three blind-spot fixes are on, `LeanBonus` stays 0, four constants are tuned. `sim.tests`: **276 of 276 pass** (271 before; one test replaced, five added). Every second-age band has at least 5 % room; the first age's nearest real band is the charter floor at 3.2 %.
- **Game checks** (each run alone with the commands of `tools/check.sh`; the timing ones under the heavy-run lock): game-town, game-save, game-playthrough, game-river, game-work, game-work-places, game-people, game-perf all pass (result lines at the end).
- **The play-through by real input is unchanged: `charter=33.9min buys=201`** (the first age's only change is the mill fix, which does not move seed 1's run). No pinned figure had to change.
- How to measure: `dotnet run --project tools/TideToll.Tools -c Release -- bands [--bell] [--set Name=v,LevelRenown[10]=13] [--millfix 0|1|2] [--sitefix 0|1] [--lean x]` prints every run and, for every band test of `TownPacingTests` and `TownRiverPacingTests`, the run nearest the edge and its margin (about 17 s for 80 runs; `--bell` adds the bell and paving tests). `bands --demo` plays richer and poorer copies of one town from its ring; `bands --rates` prints how the best coin rate grows through the second age. `people --bot four` gives herding for all four bots. The "before" figures come from the main branch's code (`git archive HEAD` of `b608c2c` into `build/main-src/`, built with these two tool files): its `bands` output is identical to this branch's before the change.

## The rule that was fixed first: a level's cost

**Old rule** (green pass to n7): each second-age level's renown was fixed when the level before it was reached, at `LevelRate x (minutes between the two levels)`, `LevelRate = max(LevelRateMin, rate at the ring, the town's best coin rate so far)`. A town whose best minute is lower has cheaper levels, so the seal came at about the same minute whatever the town earned; a herding town (higher peaks) paid more; the spread town sealed sooner.

**New rule** (`Town.RiverLevelRenown`, `TownRulesRiver.LevelCurve`):

    renown of river level i = RenownBase + round( RenownScale x LevelCurve(i) x exp(-LevelEase x minutes since the ring) )
    LevelCurve(i) = LevelMinutesScale x sum over j = 1..i of (LevelRenown[j] - LevelRenown[j-1]) x LevelGrowth^(j-1)

- `RenownBase` is the renown at the ring, `RenownScale` the best coin rate at the ring (at least `LevelRateMin`, 2000): both fixed when the age is rung in, as before.
- The curve stands for the income growth the old rule followed: under the old rule the best rate when each level was reached was 1.00, 1.01, 1.14, 1.51, 1.96, 2.14, 2.39, 2.80, 3.36, 4.57 and 5.61 times the ring's (mean of all 40 runs; about x1.19 a level).
- The ease: the renown above the ring falls by `LevelEase` a minute, the same for every town. It is what keeps the age inside its band. A fixed curve alone passes each run's own income growth straight into its seal minute: second-age income ran from 1.0 to 3.3 times the ring rate across the runs, and at the growth that restores the mean seal minute the buyers sealed **17.6 to 40.2** minutes after the ring (band 20 to 35). With the ease, the seal minute follows the logarithm of the town's income instead of the income: **21.0 to 31.4**.
- **Why it cannot reward earning less:** the renown a level needs depends only on the ring and the clock, never on what the town earns or its peaks after the ring. Renown comes from purchases (and requests), so a town that earns more can buy more and reaches each level no later. Tested: `TownRiverLevelRuleTests` (below).
- `RiverState.LevelAt` keeps each level's renown before the ease, fixed when the level before is reached (the same mechanism, now on the curve).
- **Old saves:** a second-age save from before keeps the levels it had fixed (old rule values) and the later levels follow the curve from the last one; `RiverState.EaseFrom` is -1 in such a save and is set the first time it is stepped, so loading does not jump levels. Its pace from then on differs from what it would have been: the ease starts at the load, so it reaches its remaining levels sooner than a town rung in now would at the same point. First-age saves and saves from before the green pass are unaffected (the latter use `RenownBase + RenownScale x LevelCurve(i)`, eased from the load). The save format is unchanged apart from the one new field (missing: -1), the version stays 3. Test: `TheSwitchesAreOnAndAnOldSecondAgeSavePlaysOn`.
- **What the player sees:** `NextLevelRenown` (the ledger's "renown X of Y" and the level bar) now falls slowly while the town waits (about 6 % a minute). The renown held never falls.

### Shown with the bots: a richer run seals no later than a poorer one

`bands --demo`: each seed is played by the payback bot to the ring once; from that moment the same town plays on with what boats pay at 0.8, 1 and 1.25 times its value, so the runs differ only in what they earn after the ring. Within each seed, the correlation between coin a minute and the seal minute: **old rule +0.06, new rule -0.99**. With boats paying 1.25 times, the seal comes earlier on 9 of 10 seeds (mean -1.51 minutes; old rule: 6 of 10, mean -0.27); with 0.8 times, later on 8 of 10 (mean +1.53; old rule: mean -0.51, i.e. the poorer run sealed sooner). The exceptions are runs whose measured income did not follow the price (the bot bought other things: seed 6 x1.25 earned less than x1; seed 7 x0.8 earned more), or differ by 2 seconds (seed 10, incomes within 0.3 %).

```
old rule (main branch)
DEMO seed | coin/min 2nd x0.8 / x1 / x1.25 | seal min x0.8 / x1 / x1.25 | richer never later
DEMO   1 |   64763 /   75768 /   70369 | 26.12 / 27.96 / 25.13 | yes
DEMO   2 |   30149 /   37609 /   48812 | 22.88 / 25.04 / 25.47 | NO
DEMO   3 |   21588 /   27889 /   36900 | 23.30 / 22.96 / 21.34 | yes
DEMO   4 |   52177 /   68998 /   89086 | 21.82 / 23.38 / 27.98 | NO
DEMO   5 |   65310 /   76926 /  123174 | 26.44 / 25.88 / 23.13 | yes
DEMO   6 |   32002 /   34335 /   35612 | 26.07 / 24.51 / 25.70 | NO
DEMO   7 |   48940 /   56456 /   43750 | 27.79 / 27.21 / 27.57 | yes
DEMO   8 |   49305 /   59127 /   61458 | 27.89 / 26.07 / 25.07 | yes
DEMO   9 |   20347 /   26028 /   28722 | 25.98 / 25.06 / 24.79 | yes
DEMO  10 |   36456 /   37764 /   49502 | 25.93 / 31.27 / 30.48 | NO
DEMO richer never later on 6 of 10 seeds

new rule
DEMO seed | coin/min 2nd x0.8 / x1 / x1.25 | seal min x0.8 / x1 / x1.25 | richer never later
DEMO   1 |   44020 /   56601 /   72195 | 23.40 / 21.07 / 18.82 | yes
DEMO   2 |   32730 /   39304 /   50449 | 26.45 / 24.56 / 22.61 | yes
DEMO   3 |   23764 /   34524 /   40379 | 30.07 / 26.16 / 24.77 | yes
DEMO   4 |   48913 /   51934 /   80232 | 23.25 / 22.84 / 18.65 | yes
DEMO   5 |   56060 /   65468 /   71956 | 22.49 / 21.00 / 20.18 | yes
DEMO   6 |   26050 /   36383 /   35860 | 30.49 / 26.98 / 27.12 | yes
DEMO   7 |   41626 /   40904 /   48130 | 25.03 / 25.29 / 23.64 | yes
DEMO   8 |   43509 /   44739 /   48211 | 29.38 / 29.33 / 28.31 | yes
DEMO   9 |   21141 /   25420 /   29289 | 31.92 / 29.91 / 28.38 | yes
DEMO  10 |   43092 /   43232 /   45194 | 23.48 / 23.52 / 23.05 | NO
DEMO richer never later on 9 of 10 seeds
```

## What is switched on

| constant | before | after | why |
| --- | --- | --- | --- |
| `TownRules.MillWaitCountsOnTheWay` | false | true | the brief: villagers spread over their work |
| `TownRules.MillWaitCountsOnTheWayInSecondAge` | false | true | the same |
| `TownRules.SiteWaitCountsOnTheWay` | false | true | the same |
| `TownRules.LeanBonus` | 0 | 0 (unchanged) | measured at 0.10 with everything else as shipped: second-age herding falls further (payback 44 to 33 %), but the first age's charter floor fails (`seed 7 saver 24.68 min`, floor 25) and the charter ceiling thins to 2.7 % (seed 2 random 38.91). Not at no cost, so left at 0 for trades. |

## The constants changed (all in `TownRulesRiver`, with these reasons at the constants)

| constant | old | new | reason, and what it does to the means |
| --- | --- | --- | --- |
| `LevelGrowth` (new) | (none: the town's own best rate) | 1.4 | stands for the income growth through the age. With the ease (levels about 2.4 minutes apart, x0.87 a level) it gives about x1.21 a level, the old rule's measured mean growth (x1.19). Growth 1.10 / 1.15 / 1.19 / 1.23 without an ease gave mean seals (payback) 17.3 / 21.8 / 25.0 / 28.8 with the spread above. |
| `LevelEase` (new) | 0 | 0.06 a minute | compresses the spread of the seal minute (above). 0.03 (at growth 1.19, scale 1.6): buyers 17.8 to 29.1 and a last stretch too short; 0.07 (growth 1.4, scale 1.2): buyers 20.5 to 31.9 (floor 2.7 %) and toll payback 0.84. Tried before the ease: renown that grows by itself with time (a fixed amount a minute): at 0.5 to 1.5 level-minutes a minute it did not hold back the slow seed (seed 9 still 41 to 52 minutes), because the costs grow faster than a fixed amount; dropped. |
| `LevelMinutesScale` | 1 (unused) | 1.05 | brings the mean seal minute back: 0.9 gave payback / cheapest 23.5 / 26.9; 1.05 gives 25.1 / 27.6 (main: 25.9 / 26.6). |
| `LevelRenown[10]` (level 27, the drawbridge) | 14.00 | 13.00 | the ease lowers the seal's renown by about a quarter while the town works through the last stretch, which shortened the drawbridge-to-seal stretch (mean 6.5 to 5.3 minutes) and with it the toll steps' payback (all runs 1.07 to 0.91, under its floor of 1.0). 13.00 gives the stretch back (mean 6.5 minutes) and the payback (1.16). |
| `TownRiver.LevelRate` (code) | `max(LevelRateMin, RenownScale, best rate so far)` | removed; `RenownScale` only | the rule itself. |

Order of the work: the rule first with the switches on (growth only, no ease: the spread above), then the ease, then the scale for the mean, then the drawbridge level for the one band left short (toll payback). Nothing was chosen by whether a test passed; each step answers one measured figure. The mean seal of the four bots together is 26.4 minutes after the ring (main 26.1); per bot the richer bots now seal a little sooner and the poorer ones a little later, as the rule intends (payback 25.93 to 25.07, cheapest 26.55 to 27.64, random 25.69 to 26.88, saver 26.19 to 25.95).

Mean level minutes after the ring (all 40 runs), levels 17 to 28: before 0.0 0.5 1.0 1.5 2.3 4.5 6.8 9.8 13.1 17.0 19.6 26.1; after 0.0 0.5 1.1 1.8 3.2 6.1 8.8 11.9 15.3 18.9 19.9 26.4. The middle of the age comes 1.5 to 2 minutes later (the ferry's level at 6.1 instead of 4.5 minutes), so the ford alone lasts longer (shortest 3.3 minutes instead of 2.2; ford crossings before the ferry, all runs, 460 to 1367; villager-seconds queued at the ford at high water 1551 to 9797; the longest wait at a crossing 138 to 141 s of 240). Play-wise: a longer stretch on the ford before the ferry. Nothing else about play changed (prices, boats, crane, tolls).

## The tables: before (main branch) and after, per bot and seed

From `people --seeds 1-10 --river --bot four` (first age to the charter, then the second age to the seal; herd = share of 10-second samples with more than three quarters of the villagers on one job; seal minutes after the ring). Means:

- before: MEAN Payback: charter 29.70 min (10 of 10), buys 185.8, herd 1st  26 % | seal 25.93 min (10), buys 2nd 209.0, coin/min 2nd 50090, herd 2nd  55 %
- before: MEAN Cheapest: charter 36.75 min (10 of 10), buys 205.0, herd 1st  17 % | seal 26.55 min (10), buys 2nd 150.1, coin/min 2nd 19980, herd 2nd  46 %
- before: MEAN Random: charter 35.36 min (10 of 10), buys 204.2, herd 1st  16 % | seal 25.69 min (10), buys 2nd 165.7, coin/min 2nd 25126, herd 2nd  45 %
- before: MEAN Saver: charter 27.28 min (10 of 10), buys 171.4, herd 1st  27 % | seal 26.19 min (10), buys 2nd 185.2, coin/min 2nd 39556, herd 2nd  52 %
- after: MEAN Payback: charter 29.30 min (10 of 10), buys 185.9, herd 1st  22 % | seal 25.07 min (10), buys 2nd 197.2, coin/min 2nd 43855, herd 2nd  44 %
- after: MEAN Cheapest: charter 36.71 min (10 of 10), buys 205.1, herd 1st  16 % | seal 27.64 min (10), buys 2nd 169.8, coin/min 2nd 24197, herd 2nd  33 %
- after: MEAN Random: charter 35.25 min (10 of 10), buys 204.2, herd 1st  15 % | seal 26.88 min (10), buys 2nd 180.4, coin/min 2nd 28548, herd 2nd  35 %
- after: MEAN Saver: charter 27.08 min (10 of 10), buys 171.2, herd 1st  22 % | seal 25.95 min (10), buys 2nd 181.1, coin/min 2nd 38331, herd 2nd  40 %

Herding, headline (first age; second age), before to after: payback 26 to 22 %; 55 to 44 %. Cheapest 17 to 16 %; 46 to 33 %. Random 16 to 15 %; 45 to 35 %. Saver 27 to 22 %; 52 to 40 %. Charter minutes (means): payback 29.70 to 29.30, cheapest 36.75 to 36.71, random 35.36 to 35.25, saver 27.28 to 27.08. Seal minutes after the ring: payback 25.93 to 25.07, cheapest 26.55 to 27.64, random 25.69 to 26.88, saver 26.19 to 25.95.

(The second age's herding is a little higher than the people builder's 41 % with both fixes, because the new level rule plays a different age.)

| seed | bot | charter min | buys 1st | herd 1st | seal min | buys 2nd | coin/min 2nd | herd 2nd |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | payback | 31.7 → 31.2 | 191 → 190 | 32 % → 25 % | 28.0 → 21.1 | 263 → 210 | 75767 → 56601 | 71 % → 55 % |
| 2 | payback | 30.3 → 30.1 | 183 → 183 | 17 % → 16 % | 25.0 → 24.6 | 183 → 191 | 37609 → 39304 | 51 % → 28 % |
| 3 | payback | 31.3 → 29.9 | 186 → 185 | 20 % → 16 % | 23.0 → 26.2 | 148 → 176 | 27889 → 34524 | 66 % → 48 % |
| 4 | payback | 30.7 → 29.5 | 188 → 187 | 34 % → 25 % | 23.4 → 22.8 | 235 → 201 | 68994 → 51980 | 60 % → 55 % |
| 5 | payback | 27.5 → 27.3 | 184 → 186 | 29 % → 27 % | 25.9 → 21.0 | 254 → 225 | 76933 → 65468 | 53 % → 60 % |
| 6 | payback | 29.5 → 29.8 | 185 → 187 | 16 % → 15 % | 24.5 → 27.0 | 176 → 198 | 34335 → 36383 | 55 % → 57 % |
| 7 | payback | 29.4 → 28.5 | 185 → 183 | 30 % → 23 % | 27.2 → 25.3 | 241 → 193 | 56456 → 40903 | 55 % → 36 % |
| 8 | payback | 28.3 → 28.3 | 186 → 186 | 29 % → 28 % | 26.1 → 29.3 | 239 → 223 | 59123 → 44739 | 53 % → 44 % |
| 9 | payback | 28.6 → 28.7 | 185 → 187 | 18 % → 19 % | 25.1 → 29.9 | 145 → 162 | 26028 → 25418 | 40 % → 16 % |
| 10 | payback | 29.6 → 29.6 | 185 → 185 | 34 % → 22 % | 31.3 → 23.5 | 206 → 193 | 37764 → 43232 | 44 % → 48 % |
| 1 | cheapest | 37.3 → 37.2 | 208 → 208 | 21 % → 18 % | 29.0 → 28.0 | 169 → 169 | 22838 → 23204 | 45 % → 43 % |
| 2 | cheapest | 38.5 → 38.3 | 206 → 206 | 21 % → 19 % | 25.3 → 27.2 | 165 → 171 | 24458 → 24121 | 49 % → 28 % |
| 3 | cheapest | 37.5 → 37.5 | 201 → 201 | 6 % → 6 % | 24.1 → 30.0 | 119 → 155 | 14082 → 18268 | 34 % → 27 % |
| 4 | cheapest | 36.2 → 36.1 | 206 → 206 | 19 % → 18 % | 27.5 → 26.9 | 158 → 162 | 21635 → 22577 | 41 % → 32 % |
| 5 | cheapest | 36.3 → 36.3 | 206 → 206 | 17 % → 17 % | 31.1 → 24.7 | 185 → 189 | 24690 → 32985 | 49 % → 44 % |
| 6 | cheapest | 36.5 → 36.4 | 205 → 205 | 14 % → 14 % | 25.3 → 29.0 | 127 → 163 | 15018 → 20905 | 39 % → 35 % |
| 7 | cheapest | 34.5 → 34.5 | 204 → 204 | 19 % → 19 % | 29.2 → 28.3 | 163 → 171 | 20730 → 23248 | 47 % → 31 % |
| 8 | cheapest | 37.4 → 37.5 | 206 → 207 | 24 % → 21 % | 27.0 → 29.0 | 146 → 163 | 18280 → 21457 | 44 % → 20 % |
| 9 | cheapest | 37.0 → 37.0 | 204 → 204 | 12 % → 12 % | 26.1 → 29.9 | 109 → 162 | 10407 → 19099 | 50 % → 18 % |
| 10 | cheapest | 36.4 → 36.4 | 204 → 204 | 18 % → 18 % | 20.9 → 23.5 | 160 → 193 | 27658 → 36104 | 63 % → 55 % |
| 1 | random | 35.1 → 35.1 | 205 → 205 | 14 % → 13 % | 29.1 → 27.5 | 202 → 181 | 32785 → 27527 | 53 % → 32 % |
| 2 | random | 37.8 → 37.8 | 202 → 202 | 17 % → 17 % | 26.6 → 27.1 | 172 → 180 | 23838 → 25335 | 52 % → 32 % |
| 3 | random | 37.1 → 37.0 | 205 → 205 | 9 % → 8 % | 23.6 → 29.5 | 143 → 167 | 20318 → 23551 | 41 % → 22 % |
| 4 | random | 35.2 → 35.1 | 207 → 207 | 21 % → 19 % | 26.9 → 24.2 | 202 → 190 | 34111 → 34252 | 68 % → 46 % |
| 5 | random | 34.0 → 33.9 | 204 → 204 | 16 % → 16 % | 30.0 → 24.7 | 218 → 197 | 36379 → 35796 | 42 % → 34 % |
| 6 | random | 35.0 → 34.8 | 205 → 205 | 9 % → 7 % | 20.7 → 26.0 | 147 → 175 | 24219 → 28744 | 35 % → 29 % |
| 7 | random | 32.6 → 32.5 | 202 → 202 | 22 % → 22 % | 27.4 → 28.9 | 179 → 195 | 27267 → 29633 | 35 % → 31 % |
| 8 | random | 34.9 → 34.6 | 204 → 204 | 20 % → 17 % | 23.5 → 27.9 | 151 → 193 | 22349 → 30216 | 24 % → 36 % |
| 9 | random | 36.7 → 36.4 | 204 → 204 | 15 % → 13 % | 25.9 → 31.4 | 102 → 144 | 9860 → 15004 | 42 % → 44 % |
| 10 | random | 35.3 → 35.2 | 204 → 204 | 20 % → 20 % | 23.3 → 21.6 | 141 → 182 | 20135 → 35422 | 54 % → 48 % |
| 1 | saver | 27.6 → 27.3 | 172 → 172 | 42 % → 37 % | 25.5 → 21.3 | 232 → 182 | 60782 → 52773 | 63 % → 46 % |
| 2 | saver | 28.7 → 28.5 | 172 → 172 | 18 % → 15 % | 25.2 → 25.7 | 175 → 165 | 38386 → 34325 | 50 % → 38 % |
| 3 | saver | 27.6 → 26.9 | 172 → 171 | 19 % → 18 % | 22.6 → 27.4 | 146 → 173 | 29190 → 31202 | 58 % → 52 % |
| 4 | saver | 27.5 → 27.4 | 170 → 171 | 33 % → 23 % | 25.1 → 22.6 | 221 → 189 | 53554 → 47628 | 67 % → 55 % |
| 5 | saver | 26.6 → 26.4 | 170 → 170 | 29 % → 29 % | 27.3 → 24.5 | 197 → 185 | 40255 → 41851 | 35 % → 34 % |
| 6 | saver | 27.5 → 27.5 | 173 → 171 | 11 % → 8 % | 25.8 → 28.6 | 149 → 169 | 26819 → 29377 | 48 % → 33 % |
| 7 | saver | 26.2 → 25.8 | 171 → 167 | 26 % → 24 % | 26.4 → 27.5 | 163 → 181 | 30383 → 34390 | 63 % → 35 % |
| 8 | saver | 27.5 → 27.4 | 167 → 169 | 37 % → 26 % | 24.3 → 26.3 | 194 → 193 | 43263 → 39367 | 44 % → 49 % |
| 9 | saver | 26.4 → 26.9 | 171 → 173 | 19 % → 20 % | 28.0 → 30.2 | 143 → 172 | 21745 → 27133 | 45 % → 21 % |
| 10 | saver | 27.1 → 26.6 | 176 → 176 | 37 % → 27 % | 31.6 → 25.3 | 232 → 202 | 51186 → 45263 | 53 % → 42 % |

## Every band test: nearest run and margin, before and after

Margin = distance to the edge as a share of the band's value (negative: outside). From `bands --bell` (before: the main branch's code; after: this branch). Full output: `build/retune/bands-main.txt`, `bands-after.txt` (not committed; rerun the tool).

| band (test) | nearest run before | margin before | nearest run after | margin after |
| --- | --- | --- | --- | --- |
| 1 bell average 0.10-0.25 | 0.119 | 18.8 % | 0.118 | 18.3 % |
| 1 bell single 0.05-0.35 | seed 4 minute 10 0.065 | 29.3 % | seed 4 minute 10 0.065 | 29.3 % |
| 1 bots' spread <= 2 | seed 9 1.40 | 30.0 % | seed 3 1.39 | 30.4 % |
| 1 charter 25-40 min (ceiling) | seed 2 cheapest first 38.45 min | 3.9 % | seed 2 cheapest first 38.34 min | 4.1 % |
| 1 charter 25-40 min (floor) | seed 7 saver 26.21 min | 4.8 % | seed 7 saver 25.80 min | 3.2 % |
| 1 every kind, load step, building (misses) | seed 1 best payback first 0 | 0.0 %  thin | seed 1 best payback first 0 | 0.0 %  thin |
| 1 first coin <= 25 s | seed 5 cheapest first 16.5 s | 34.0 % | seed 5 cheapest first 16.5 s | 34.0 % |
| 1 first purchase <= 45 s | seed 5 best payback first 31.5 s | 30.0 % | seed 5 best payback first 31.5 s | 30.0 % |
| 1 map gap <= 210 s | seed 3 saver 165 s | 21.4 % | seed 3 saver 154 s | 26.7 % |
| 1 nothing affordable <= 60 s | seed 2 cheapest first 27 s | 55.0 % | seed 2 cheapest first 27 s | 55.0 % |
| 1 paving matters (seed 1) >= 15 % slower | cheapest first 54.4 vs 37.3 | 204.3 % | cheapest first 54.2 vs 37.2 | 206.4 % |
| 1 purchases a minute, early >= 4 | seed 1 cheapest first 4 in minute 1 | 0.0 %  thin | seed 1 cheapest first 4 in minute 1 | 0.0 %  thin |
| 1 purchases a minute, late >= 2 | seed 1 best payback first 2 in minute 27 | 0.0 %  thin | seed 2 random affordable 2 in minute 35 | 0.0 %  thin |
| 1 requests average 0.5-0.8 | cheapest first 0.738 | 7.8 % | cheapest first 0.738 | 7.7 % |
| 1 requests single run 0.3-0.95 | seed 4 best payback first 0.31 | 2.6 %  thin | seed 4 cheapest first 0.93 | 2.3 %  thin |
| 1 saver gap <= 90 s | seed 1 saver 58 s | 36.1 % | seed 1 saver 58 s | 36.1 % |
| 1 track share <= 0.4 | seed 2 random affordable 0.269 | 32.8 % | seed 2 random affordable 0.269 | 32.8 % |
| 2 age 20-35 min, buyers (ceiling) | seed 10 best payback first 31.27 min | 10.6 % | seed 9 random affordable 31.35 min | 10.4 % |
| 2 age 20-35 min, buyers (floor) | seed 6 random affordable 20.66 min | 3.3 % | seed 5 best payback first 21.00 min | 5.0 % |
| 2 crossing wait <= 240 s | seed 4 best payback first 138 s | 42.5 % | seed 8 cheapest first 141 s | 41.1 % |
| 2 drawbridge to seal >= 3.5 min | seed 10 cheapest first 3.77 min | 7.6 % | seed 3 best payback first 4.78 min | 36.7 % |
| 2 each crossing carries more | seed 1 cheapest first | 100.0 % | seed 1 cheapest first | 100.0 % |
| 2 ford crossings, all runs >= 300 | 460 | 53.3 % | 1367 | 355.7 % |
| 2 ford high-water wait, all runs >= 1000 | 1551 | 55.1 % | 9797 | 879.7 % |
| 2 ford only >= 2 min | seed 4 best payback first 2.16 min | 8.0 % | seed 5 random affordable 3.26 min | 63.0 % |
| 2 ford used on seeds >= 4 | 5 | 25.0 % | 10 | 150.0 % |
| 2 landing share >= 1/3 | seed 8 saver 0.339 | 1.6 %  thin | seed 9 random affordable 0.414 | 24.1 % |
| 2 map gap <= 210 s | seed 3 cheapest first 162 s | 22.9 % | seed 6 saver 170 s | 19.0 % |
| 2 nothing affordable <= 60 s | seed 2 random affordable 59 s | 1.7 %  thin | seed 6 cheapest first 44 s | 27.5 % |
| 2 purchases a minute >= 2 | seed 9 random affordable 3.94 | 96.8 % | seed 9 random affordable 4.59 | 129.7 % |
| 2 saver buys every new kind (misses) | seed 1 saver 0 | 0.0 %  thin | seed 1 saver 0 | 0.0 %  thin |
| 2 seal (every run) | seed 1 cheapest first | 100.0 % | seed 1 cheapest first | 100.0 % |
| 2 toll steps back, all runs >= 1.0 | spent 979554 earned 1044750 | 6.7 % | spent 983975 earned 1140750 | 15.9 % |
| 2 toll steps back, single run >= 0.35 | seed 6 random affordable spent 24599 earned 9750 | 13.2 % | seed 2 random affordable spent 24599 earned 15750 | 82.9 % |
| 2 tolls before seal >= 2 | seed 6 random affordable 2 | 0.0 %  thin | seed 2 random affordable 3 | 50.0 % |


Not a real margin ("0.0 %" on a count): "every kind, load step, building", "saver buys every new kind" and "each crossing carries more" are pass/fail (all pass); "purchases a minute" early >= 4 and late >= 2 are whole numbers of purchases and sit at exactly the floor on the main branch too (seed 1 cheapest, minute 1, untouched by any of this); "requests single run" moves in steps of one request (13 of 14 = 0.93 against 0.95; before 0.31 against 0.30); "tolls before seal" is a count (now 3 at least).

Below about 3 %: none of the real bands of the second age (nearest: the age's floor, 5.0 %, seed 5 payback 21.0 minutes). First age: the charter floor 3.2 % (seed 7 saver 25.80 minutes; main 26.21, 4.8 %): the mill fix makes the saver's first age a little faster; no constant of the first age was changed (the brief: the first age moves only by the fixes). The landing-share and nothing-affordable bands of the second age, thin on the main branch (1.6 % and 1.7 %), now have 24 % and 27 %.

## Tests replaced and added

Replaced (`sim.tests/Town/TownPeopleTests.cs`):

| old | new | why |
| --- | --- | --- |
| `TheSecondAgePlaysAsBeforeFromASavedTown`: asserts all three switches off and `LeanBonus` 0, and that the v3 save played on 10 minutes by the payback bot hashes to `C2A8E835...AAB8285` (the main branch's output) | `TheSwitchesAreOnAndAnOldSecondAgeSavePlaysOn`: asserts the three switches on and `LeanBonus` 0; the same v3 save loads (its `EaseFrom` unset), does not jump a level on its first step (same level, same next-level renown), keeps the level renown it had fixed, reaches a further level within the 10 minutes, and saves, loads and plays on exactly | the old test pinned "the output equals the main branch's", which only fits the old rule; the new one checks what the change promises for old saves |
| `FewerSpellsOfTheWholeTownOnOneJob`: switched the fixes on for its towns (`WaitFixesHere = true`) because they shipped off | the same assertion (under half the second-age samples herd, seeds 1-3, payback) on the town as shipped | the fixes now ship on: the test measures what ships |

The two switch tests (`...WhenSwitchedOn(bool on)`) are unchanged (they set the switch per town both ways); only their summaries now say "on as shipped".

Added (`sim.tests/Town/TownRiverLevelRuleTests.cs`):
- `ALevelsRenownDoesNotFollowWhatTheTownEarnsAfterTheRing`: two copies of one second-age town (seed 2, 4 minutes after the ring), one with four times the best coin rate and three times the coin: every level not reached needs the same renown in both, now and every 30 s for 2 minutes. (Under the old rule the copy with the higher rate has dearer levels.)
- `ALevelsRenownOnlyEasesWithTime`: the renown of the next level never rises while the town waits for it, and stays above the renown at the ring (seed 4 to the seal).
- `ARicherRunSealsNoLaterThanAPoorerOne` (seeds 1, 2, 3): the same town from its ring, the same bot; a copy that keeps 25 % more of what it earns seals no later, one that loses 25 % no sooner.

No band, target or test of the pacing suites was moved, loosened, skipped or special-cased.

## Check steps (result lines)

- `dotnet test sim.tests -c Release --filter "Category!=Gate&Category!=Perf"`: `Passed!  - Failed:     0, Passed:   276, Skipped:     0, Total:   276` (before: 271 of 271).
- game-build: 0 errors, 0 warnings.
- game-town: `TOWN PASS faint=0.40 maxstep=0.009 worn=0.95 paved=0 boonclick=Wider lanes extended=0 level=6 bought=50 ...`
- game-save: `SAVE PASS autosave-clock=0.66/0.66s level 2 paved 1 villagers 4`
- game-playthrough (lock): `PLAY PASS charter=33.9min buys=201 paves=24 extends=14 bells=76 clicks=204 keys=102`
- game-river (lock): `RIVER PASS ... fordcrossings=91 checks=350 waiting=63 boats=743 cargo=402 fordwait=26/961t raft=280 lift=9/270t tide=153/153 level=28 villagers=34`
- game-work (lock): `WORK RATES PASS` (60 fps `pairs=622 gap=1.98-2.30m ... villagerbytes=2`)
- game-work-places (lock): `WORK PLACES PASS` at 60, 30 and 120 fps
- game-people (lock): `PEOPLE RATES PASS` (60 fps `picked=21/21 shared=0 passpairs=9790 sidefails=0 ... villagerbytes=1 pickbytes=0`)
- game-perf (lock): `PERF PASS`; river 34 villagers 1x mean 0.745 ms, 1175 B/frame (gate 1536); 8x 0.812 ms, 1448 B/frame.

## What trades (the next step) should know

- **Where a trade's factor enters:** `Town.Leaned` (`sim/Town/TownPeople.cs`), called from `Consider` (first age, `TownJobs.cs`) and `RConsider` (second age, `TownRiverJobs.cs`): today `rate x (1 + LeanBonus)` for the villager's own kind of work; a trade replaces the hashed leaning (`_choosing`) by a stored trade and the bonus by the trade's factor. A trade that makes villagers work faster or carry more would go where work time and loads are read (`WorkTime`, `ToolsFactor`, the loads in `Deliver*`).
- **The level rule no longer rewards a poorer setup:** a trade setup that earns more seals sooner, one that earns less later. The size of it: 25 % more income after the ring moved the seal about 1.5 minutes sooner (the demo); the seal follows roughly the log of the income.
- **Room the bands leave:** second age: the floor (20 minutes) has 1.0 minute on its nearest run (seed 5 payback 21.0), the ceiling (35) 3.6 minutes (seed 9 random 31.35). So a trade that lifts the payback bot's second-age income by much more than 15 % would take seed 5 below the floor; tune with `LevelMinutesScale` (a factor k on it moves every seal by about ln(k) / 0.06 minutes) rather than the bands. Drawbridge to seal: 1.3 minutes above 3.5. First age: the charter floor has 0.8 minutes (seed 7 saver 25.80) and the ceiling 1.7 minutes (seed 2 cheapest 38.34); the leaning at 0.10 already takes seed 7 saver under 25, so a first-age trade that speeds the saver needs the first age retuned (`TownRules.CharterMinutes` or the level table) and is not covered by this retune.
- The first age's charter still uses the old kind of rule (`CharterMinutes x` the town's best rate, `Town.LevelRenownOf`): a poorer first age reaches its last level relatively sooner. Not changed here (the brief covered the second age); trades will meet it in the first age.

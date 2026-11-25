# Town pacing (first age, rules v3)

Generated on 25 November 2026 (rules v3, `docs/plans/town-rules3-progress.md`) with the numbers in `sim/Town/TownRules.cs`:
`town --seeds 1-10 --misses --averages --v3`. The rules v2.2 report follows further down, unchanged.

## What rules v3 adds

1. Choices of two at levels 4, 7, 10, 13 (lanes or backs, well or rows, a third room or faster hands, market right or miller's guild).
2. The charter drive after level 15: deliver an amount of the town's best good, paid in renown as it arrives (65 % of what is left for the charter).
3. Worth on every shop item; a milestone at every fifth step of Pace, Tools and each value track.
4. Extra hands offered at a workplace where a queue of 2 or more stays 30 s (2 per workplace at most).

Re-tune: those four made the town about 25 % faster, so goods pay less (Water 1.8, Grain 2.4/3.2, Flour 6.5), early prices are a little lower (Villager 4.5, Pace 1.8, Basket 200), Villager, Pace and Tools prices grow faster (1.32, 1.4, 1.38), and levels 9 to 16 need more renown (charter 140000).

## Result (seeds 1 to 10): NOT all targets met (4 misses)

charter minute ; requests met ; boons taken (A/B for choices 0 to 3) ; last gap / longest of the three gaps before (minutes)

    seed | cheapest first | best payback first | random affordable | saver
       1 | 33.2 ; req 8/14 ; AAAA ; 3.5/4.3 | 26.0 ; req 7/11 ; BABB ; 1.7/3.2 | 31.4 ; req 9/13 ; BBAB ; 3.4/3.5 | 25.7 ; req 8/11 ; BAAA ; 3.0/2.3
       2 | 33.0 ; req 11/13 ; AAAA ; 2.8/3.8 | 28.2 ; req 5/12 ; BABB ; 2.3/2.8 | 30.3 ; req 9/12 ; BABA ; 2.5/2.7 | 26.2 ; req 9/10 ; BAAA ; 2.3/2.6
       3 | 32.6 ; req 10/13 ; AAAA ; 2.9/3.8 | 29.0 ; req 6/12 ; AAAA ; 2.8/3.8 | 30.1 ; req 8/12 ; AABA ; 2.4/2.3 | 25.1 ; req 5/10 ; AABA ; 2.2/2.7
       4 | 31.2 ; req 10/12 ; AAAA ; 2.5/3.8 | 26.0 ; req 7/10 ; AABB ; 1.7/2.6 | 33.8 ; req 9/13 ; BAAB ; 3.5/4.5 | 24.1 ; req 6/10 ; BABB ; 1.7/2.5
       5 | 31.6 ; req 10/12 ; AAAA ; 2.9/3.8 | 26.7 ; req 8/11 ; BAAA ; 2.8/3.2 | 30.3 ; req 8/12 ; BBAA ; 3.7/3.3 | 25.3 ; req 9/10 ; AAAA ; 2.7/2.7
       6 | 31.4 ; req 9/13 ; AAAA ; 2.6/2.8 | 27.8 ; req 6/12 ; BBAA ; 2.3/2.8 | 28.4 ; req 7/12 ; BABA ; 1.6/2.8 | 25.9 ; req 6/11 ; BBAA ; 1.6/3.4
       7 | 29.8 ; req 11/12 ; AAAA ; 2.9/2.7 | 27.5 ; req 8/12 ; BAAB ; 2.6/3.3 | 27.0 ; req 7/12 ; ABBB ; 1.1/2.8 | 23.3 ; req 7/10 ; BBAA ; 2.2/2.6
       8 | 33.3 ; req 11/13 ; AAAA ; 3.0/4.4 | 27.5 ; req 10/11 ; BAAB ; 1.8/3.0 | 32.3 ; req 8/13 ; BBAA ; 2.4/4.4 | 26.6 ; req 8/10 ; BAAA ; 3.3/3.7
       9 | 30.9 ; req 9/12 ; AAAA ; 3.1/2.9 | 25.3 ; req 6/11 ; BBBA ; 2.0/2.4 | 29.5 ; req 6/13 ; BABA ; 2.9/2.5 | 25.0 ; req 5/11 ; BBBA ; 2.1/3.5
      10 | 32.0 ; req 11/13 ; AAAA ; 3.0/3.7 | 25.2 ; req 8/10 ; BABB ; 1.2/2.5 | 30.3 ; req 10/13 ; BAAA ; 2.4/3.3 | 25.2 ; req 5/11 ; BABB ; 2.0/2.7

    MISSED seed 4 saver: charter at 24.1 min, outside 25-40
    MISSED seed 7 saver: charter at 23.3 min, outside 25-40
    MISSED seed 10 cheapest first: 1 purchases in minute 32 < 2
    MISSED seed 10 best payback first: 3 purchases in minute 8 < 4
    bell (frozen payback towns at minutes 10,20): average +13.0 % (comparisons 6.6 to 20.8)
    cheapest first: requests met 79 % on average (runs 57 to 92); best payback first: 64 % (runs 42 to 91)

- Last gap against the longest of the three before it, mean over seeds 1 to 10: cheapest 2.92 / 3.60, payback 2.12 / 2.96, random 2.59 / 3.21, saver 2.31 / 2.87 minutes (v2.2: the last gap was the longest, 3.7 to 3.9).
- Payback boon picks on seeds 1 to 10 (side A of 10): choice 0 2, choice 1 8, choice 2 5, choice 3 4. Choices 0 and 1 sit at the limit of 8.
- Why the saver is early: by minute 19 to 20 it has bought everything on sale and waits on levels; more renown per level only makes it wait with nothing to buy (tried: 'nothing affordable' and 'no purchase for 90 s' misses appear). It needs more to buy in levels 12 to 15, not higher thresholds. Not done in the time box.

# Rules v2.2 report (kept for comparison)

Generated on 25 November 2026 (rules v2.2, `docs/plans/town-rules22-progress.md`) with the numbers in `sim/Town/TownRules.cs`:
`town --seeds 1-10 --misses --averages`. Targets are `TownTargets` in the same file; `sim.tests/Town/TownPacingTests.cs` checks them on seeds 1 to 10 with all four bots. The per-seed tables further down (seed 1, every bot) are from rules v2.1 and were not regenerated.

How the bots play: they think every 0.5 game seconds, buy at most one thing per think, ring the bell whenever it is ready, and stop buying at the charter. Cheapest first buys the cheapest affordable item; best payback first buys the item with the best estimated extra coin per minute per coin, saving for it only when it is under 20 s of income away; random affordable buys a random affordable item; the saver picks the best payback among everything on sale, affordable or not, and waits for it when it is under 40 s of income away.

## The lead's decisions in v2.2

1. The purchases-per-minute floor (4 a minute in minutes 1 to 10, 2 after) holds for cheapest, payback and random on every seed. The saver waits on purpose, so it is out of that check; its own checks: every kind, every Load step and every building before the charter, charter in 25 to 40 minutes, and never more than 90 s without a purchase after minute 2.
2. The bell is measured on frozen towns: the payback town at minutes 10 and 20 (through the save), 5 minutes with no purchases, bell rung whenever ready against never, delivery coin compared. Average over the 20 comparisons 10 to 25 percent, each 5 to 35.
3. Requests start easy (`RequestEaseIn` 0.4, 0.6, 0.8 for the first three) and adapt; share met by cheapest and payback 50 to 80 percent on average, each run 30 to 95.
4. The saver reaches the charter at 25 minutes or later on every seed.

## Result (seeds 1 to 10): all targets met

charter minute ; longest nothing-affordable spell (s) ; requests met

    seed | cheapest first | best payback first | random affordable | saver
       1 | 30.4 ; 36 ; req 9/13 | 27.0 ; 38 ; req 9/11 | 31.1 ; 44 ; req 8/13 | 27.1 ; 22 ; req 7/11
       2 | 32.0 ; 36 ; req 6/14 | 29.6 ; 40 ; req 6/12 | 32.4 ; 46 ; req 6/14 | 27.0 ; 27 ; req 6/11
       3 | 30.5 ; 39 ; req 7/13 | 28.5 ; 42 ; req 6/12 | 31.1 ; 48 ; req 7/13 | 26.5 ; 23 ; req 6/10
       4 | 31.4 ; 48 ; req 7/12 | 27.2 ; 36 ; req 9/11 | 30.7 ; 40 ; req 7/12 | 25.3 ; 16 ; req 9/10
       5 | 30.2 ; 35 ; req 8/11 | 26.5 ; 34 ; req 9/10 | 29.4 ; 34 ; req 9/11 | 26.2 ; 20 ; req 8/10
       6 | 31.5 ; 34 ; req 8/13 | 28.6 ; 40 ; req 7/12 | 31.5 ; 35 ; req 8/13 | 25.7 ; 16 ; req 8/11
       7 | 30.6 ; 34 ; req 9/13 | 27.0 ; 46 ; req 9/12 | 30.9 ; 34 ; req 10/13 | 26.1 ; 22 ; req 7/12
       8 | 29.4 ; 42 ; req 8/12 | 26.2 ; 42 ; req 10/11 | 29.4 ; 47 ; req 9/12 | 25.1 ; 17 ; req 8/10
       9 | 31.5 ; 34 ; req 7/13 | 28.4 ; 48 ; req 7/12 | 30.8 ; 45 ; req 6/13 | 26.4 ; 24 ; req 8/11
      10 | 31.4 ; 31 ; req 10/13 | 28.2 ; 45 ; req 7/12 | 32.7 ; 40 ; req 9/14 | 26.4 ; 22 ; req 7/11
    bell (frozen payback towns at minutes 10,20): average +14.6 % (comparisons 9.1 to 20.2), per seed 13.1/17.4 10.3/18.4 11.9/19.0 9.8/15.6 11.8/16.6 9.1/16.2 11.8/16.9 12.0/19.9 11.1/20.2 11.3/20.1
    cheapest first: requests met 63 % on average (runs 43 to 77)
    best payback first: requests met 69 % on average (runs 50 to 91)
    misses: 0 (purchases 0, nothing affordable 0, age 0, map 0)

Seeds 11 to 20 (no test, for information): one miss, the saver on seed 18 reaches the charter at 24.8 minutes. The saver's margin on seeds 1 to 10 is thin (25.1 minutes on seed 8).

## Seeds 1 to 10 (rules v2.1, for comparison)

`town --seeds 1-10 --misses --averages`: charter minute ; longest nothing-affordable spell (s) ; requests met.

    seed | cheapest first | best payback first | random affordable | saver
       1 | 31.4 ; 38 ; req 7/13 | 27.4 ; 26 ; req 6/11 | 31.3 ; 43 ; req 7/13 | 24.9 ; 17 ; req 5/10
       2 | 32.9 ; 38 ; req 6/14 | 30.0 ; 50 ; req 3/13 | 33.5 ; 37 ; req 6/14 | 27.1 ; 18 ; req 4/11
       3 | 30.6 ; 26 ; req 7/12 | 26.3 ; 20 ; req 5/10 | 30.6 ; 36 ; req 7/12 | 25.3 ; 16 ; req 5/9
       4 | 32.8 ; 37 ; req 6/13 | 26.5 ; 18 ; req 7/10 | 32.9 ; 39 ; req 6/13 | 24.6 ; 16 ; req 7/9
       5 | 31.1 ; 28 ; req 6/12 | 26.2 ; 22 ; req 6/10 | 28.9 ; 29 ; req 7/11 | 25.4 ; 19 ; req 6/10
       6 | 32.0 ; 42 ; req 7/14 | 26.6 ; 21 ; req 6/11 | 31.9 ; 44 ; req 7/13 | 26.0 ; 21 ; req 6/11
       7 | 30.4 ; 35 ; req 7/13 | 27.1 ; 24 ; req 6/12 | 30.1 ; 32 ; req 8/12 | 24.2 ; 16 ; req 7/10
       8 | 30.7 ; 33 ; req 9/13 | 26.5 ; 22 ; req 8/11 | 30.1 ; 34 ; req 9/13 | 24.4 ; 18 ; req 6/10
       9 | 31.7 ; 33 ; req 6/13 | 26.4 ; 20 ; req 6/11 | 31.6 ; 42 ; req 6/13 | 25.7 ; 18 ; req 6/11
      10 | 32.9 ; 43 ; req 6/14 | 27.1 ; 22 ; req 6/11 | 30.8 ; 36 ; req 6/13 | 25.1 ; 16 ; req 5/10

Misses as printed:

    MISSED seed 1 saver: 3 purchases in minute 6 < 4
    MISSED seed 1 saver: charter at 24.9 min, outside 25-40
    MISSED seed 2 saver: 3 purchases in minute 8 < 4
    MISSED seed 4 saver: 3 purchases in minute 6 < 4
    MISSED seed 4 saver: charter at 24.6 min, outside 25-40
    MISSED seed 6 best payback first: 3 purchases in minute 7 < 4
    MISSED seed 6 saver: 3 purchases in minute 6 < 4
    MISSED seed 7 saver: 2 purchases in minute 7 < 4
    MISSED seed 7 saver: charter at 24.2 min, outside 25-40
    MISSED seed 8 saver: 3 purchases in minute 8 < 4
    MISSED seed 8 saver: charter at 24.4 min, outside 25-40
    MISSED seed 9 saver: 3 purchases in minute 6 < 4
    MISSED seed 9 saver: 1 purchases in minute 19 < 2
    MISSED seed 10 saver: 2 purchases in minute 6 < 4
    cheapest first: bell average +10.6 % (runs 6.7 to 16.8), per seed 8.3 11.7 13.2 7.8 11.6 9.3 6.7 9.9 16.8 10.6
    best payback first: bell average +14.9 % (runs -2.6 to 36.1), per seed 17.3 -2.6 36.1 11.7 10.6 14.1 14.2 8.0 23.0 16.9
    saver: bell average +25.1 % (runs 12.9 to 48.3), per seed 48.3 16.8 25.9 12.9 29.2 22.3 30.0 21.2 22.7 21.6
    misses: 16 (purchases 12, nothing affordable 0, age 4, map 0)

## Bell, paving and requests (seed 1)

`town --extras --seed 1`:

    seed 1 cheapest first: charter 31.4, never paving 47.4 (51 % slower), bell +8.3 %, requests met 7/13
    seed 1 best payback first: charter 27.4, never paving 42.4 (55 % slower), bell +17.3 %, requests met 6/11
    seed 1 random affordable: charter 31.3, never paving 45.8 (46 % slower), bell +11.4 %, requests met 7/13
    seed 1 saver: charter 24.9, never paving 41.0 (64 % slower), bell +48.3 %, requests met 5/10

## Seed 1, every bot

### Seed 1, bot: cheapest first

| minute | coin | coin/min | purchases | level | villagers |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 3 | 16 | 4 | 1 | 2 |
| 2 | 9 | 45 | 5 | 2 | 4 |
| 3 | 17 | 89 | 5 | 3 | 4 |
| 4 | 2 | 174 | 6 | 4 | 4 |
| 5 | 18 | 230 | 8 | 4 | 6 |
| 6 | 10 | 218 | 4 | 5 | 6 |
| 7 | 21 | 282 | 5 | 5 | 7 |
| 8 | 68 | 332 | 4 | 6 | 8 |
| 9 | 24 | 522 | 9 | 7 | 10 |
| 10 | 43 | 575 | 6 | 7 | 12 |
| 11 | 68 | 793 | 5 | 8 | 13 |
| 12 | 51 | 878 | 6 | 8 | 15 |
| 13 | 190 | 871 | 5 | 8 | 16 |
| 14 | 156 | 1466 | 7 | 9 | 16 |
| 15 | 361 | 1529 | 8 | 10 | 17 |
| 16 | 8 | 1966 | 6 | 10 | 18 |
| 17 | 448 | 1786 | 4 | 11 | 19 |
| 18 | 65 | 2040 | 4 | 11 | 21 |
| 19 | 643 | 2060 | 2 | 11 | 21 |
| 20 | 144 | 2404 | 5 | 12 | 22 |
| 21 | 65 | 2268 | 4 | 12 | 22 |
| 22 | 894 | 2661 | 3 | 12 | 23 |
| 23 | 186 | 3455 | 4 | 13 | 23 |
| 24 | 586 | 3302 | 3 | 13 | 23 |
| 25 | 275 | 4524 | 3 | 13 | 24 |
| 26 | 349 | 4231 | 11 | 14 | 24 |
| 27 | 1258 | 5426 | 3 | 14 | 24 |
| 28 | 445 | 5825 | 6 | 15 | 24 |
| 29 | 114 | 5843 | 3 | 15 | 25 |
| 30 | 1202 | 5575 | 2 | 15 | 25 |
| 31 | 172 | 6382 | 3 | 15 | 26 |
| 32 | 3326 | 5851 | 1 | 16 | 26 |

- first coin 16.1 s, first purchase 16.5 s, purchases 154
- longest spell with nothing affordable 37.5 s (from 1456 s)
- fewest purchases in a minute: 4 in minutes 1-10 (minute 1), 2 after (minute 19)
- level reached (minute): L2 1.4 L3 2.7 L4 4.0 L5 5.8 L6 7.8 L7 9.0 L8 10.8 L9 13.1 L10 14.6 L11 16.6 L12 19.6 L13 22.2 L14 25.0 L15 27.4 L16 31.4
- charter at minute 31.4
- purchases by kind before the charter: Villager 25, Pace 24, Load 2, Pave 24, Building 4, Tools 16, Value 40, Extend 14, BellTrack 4
- longest gap with nothing new on the map after minute 2: 3.1 min (from minute 27.4); requests met 7 of 13

### Seed 1, bot: best payback first

| minute | coin | coin/min | purchases | level | villagers |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 6 | 24 | 5 | 1 | 2 |
| 2 | 8 | 54 | 5 | 2 | 4 |
| 3 | 8 | 110 | 6 | 3 | 4 |
| 4 | 15 | 177 | 6 | 4 | 6 |
| 5 | 15 | 233 | 9 | 5 | 6 |
| 6 | 11 | 248 | 5 | 5 | 7 |
| 7 | 30 | 355 | 5 | 6 | 8 |
| 8 | 43 | 413 | 5 | 6 | 9 |
| 9 | 26 | 609 | 5 | 7 | 11 |
| 10 | 118 | 982 | 8 | 7 | 13 |
| 11 | 153 | 1446 | 9 | 8 | 15 |
| 12 | 152 | 1438 | 11 | 9 | 16 |
| 13 | 4 | 1583 | 8 | 10 | 17 |
| 14 | 23 | 1874 | 5 | 10 | 17 |
| 15 | 99 | 1870 | 6 | 11 | 19 |
| 16 | 520 | 2559 | 4 | 11 | 20 |
| 17 | 156 | 3194 | 5 | 12 | 21 |
| 18 | 1000 | 3971 | 4 | 12 | 22 |
| 19 | 247 | 4119 | 5 | 13 | 23 |
| 20 | 531 | 4061 | 8 | 13 | 23 |
| 21 | 759 | 4784 | 4 | 13 | 23 |
| 22 | 823 | 4391 | 5 | 14 | 24 |
| 23 | 1351 | 4825 | 3 | 14 | 24 |
| 24 | 1289 | 5056 | 3 | 14 | 24 |
| 25 | 913 | 5632 | 2 | 15 | 25 |
| 26 | 435 | 6698 | 3 | 15 | 27 |
| 27 | 153 | 8531 | 5 | 15 | 27 |
| 28 | 6919 | 8580 | 2 | 16 | 27 |

- first coin 16.1 s, first purchase 30.5 s, purchases 151
- longest spell with nothing affordable 26.5 s (from 1449 s)
- fewest purchases in a minute: 5 in minutes 1-10 (minute 1), 2 after (minute 25)
- level reached (minute): L2 1.2 L3 2.5 L4 3.8 L5 4.6 L6 6.6 L7 8.9 L8 10.0 L9 11.3 L10 13.0 L11 14.3 L12 16.4 L13 18.6 L14 21.2 L15 24.9 L16 27.4
- charter at minute 27.4
- purchases by kind before the charter: Villager 26, Pace 24, Load 3, Pave 22, Building 4, Tools 15, Value 38, Extend 13, BellTrack 5
- longest gap with nothing new on the map after minute 2: 2.5 min (from minute 21.2); requests met 6 of 11

### Seed 1, bot: random affordable

| minute | coin | coin/min | purchases | level | villagers |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 6 | 24 | 5 | 1 | 2 |
| 2 | 16 | 54 | 5 | 2 | 4 |
| 3 | 23 | 97 | 5 | 3 | 4 |
| 4 | 0 | 174 | 7 | 4 | 6 |
| 5 | 39 | 233 | 9 | 5 | 7 |
| 6 | 4 | 266 | 5 | 5 | 7 |
| 7 | 32 | 371 | 5 | 6 | 8 |
| 8 | 108 | 421 | 5 | 6 | 9 |
| 9 | 70 | 633 | 8 | 7 | 12 |
| 10 | 109 | 678 | 4 | 7 | 12 |
| 11 | 150 | 897 | 6 | 8 | 15 |
| 12 | 41 | 877 | 7 | 8 | 16 |
| 13 | 65 | 1127 | 5 | 9 | 16 |
| 14 | 236 | 1654 | 9 | 9 | 16 |
| 15 | 227 | 1603 | 5 | 10 | 17 |
| 16 | 116 | 2029 | 6 | 11 | 19 |
| 17 | 492 | 1811 | 3 | 11 | 20 |
| 18 | 698 | 2288 | 3 | 11 | 21 |
| 19 | 27 | 2021 | 4 | 12 | 22 |
| 20 | 629 | 2429 | 4 | 12 | 22 |
| 21 | 886 | 2480 | 3 | 12 | 23 |
| 22 | 722 | 2752 | 4 | 13 | 23 |
| 23 | 71 | 3503 | 4 | 13 | 23 |
| 24 | 1442 | 3603 | 2 | 13 | 24 |
| 25 | 22 | 4237 | 9 | 14 | 24 |
| 26 | 1327 | 4794 | 4 | 14 | 24 |
| 27 | 953 | 5994 | 4 | 14 | 24 |
| 28 | 1130 | 5621 | 5 | 15 | 25 |
| 29 | 1306 | 5893 | 3 | 15 | 25 |
| 30 | 2253 | 5348 | 2 | 15 | 25 |
| 31 | 652 | 5722 | 3 | 15 | 26 |
| 32 | 4114 | 6110 | 1 | 16 | 26 |

- first coin 16.1 s, first purchase 16.5 s, purchases 154
- longest spell with nothing affordable 43.0 s (from 1418 s)
- fewest purchases in a minute: 4 in minutes 1-10 (minute 10), 2 after (minute 24)
- level reached (minute): L2 1.2 L3 2.6 L4 3.9 L5 4.9 L6 6.7 L7 8.5 L8 10.3 L9 12.6 L10 14.0 L11 15.9 L12 18.8 L13 21.4 L14 24.4 L15 27.2 L16 31.3
- charter at minute 31.3
- purchases by kind before the charter: Villager 25, Pace 24, Load 2, Pave 24, Building 4, Tools 16, Value 40, Extend 14, BellTrack 4
- longest gap with nothing new on the map after minute 2: 3.2 min (from minute 27.2); requests met 7 of 13

### Seed 1, bot: saver

| minute | coin | coin/min | purchases | level | villagers |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 6 | 24 | 5 | 1 | 2 |
| 2 | 8 | 54 | 5 | 2 | 4 |
| 3 | 8 | 110 | 6 | 3 | 4 |
| 4 | 15 | 177 | 6 | 4 | 6 |
| 5 | 229 | 231 | 5 | 4 | 6 |
| 6 | 14 | 295 | 3 | 5 | 6 |
| 7 | 4 | 521 | 9 | 6 | 10 |
| 8 | 3 | 816 | 11 | 7 | 13 |
| 9 | 342 | 1307 | 13 | 8 | 15 |
| 10 | 99 | 1275 | 9 | 9 | 16 |
| 11 | 1026 | 1686 | 7 | 9 | 16 |
| 12 | 1579 | 1667 | 4 | 10 | 16 |
| 13 | 366 | 2543 | 8 | 10 | 18 |
| 14 | 467 | 3586 | 9 | 11 | 20 |
| 15 | 438 | 3627 | 6 | 12 | 21 |
| 16 | 771 | 3938 | 5 | 12 | 22 |
| 17 | 3650 | 4052 | 2 | 12 | 22 |
| 18 | 272 | 5047 | 4 | 13 | 23 |
| 19 | 991 | 4976 | 9 | 14 | 24 |
| 20 | 611 | 5799 | 4 | 14 | 26 |
| 21 | 497 | 5931 | 4 | 14 | 26 |
| 22 | 593 | 6220 | 5 | 15 | 26 |
| 23 | 119 | 6597 | 4 | 15 | 27 |
| 24 | 1160 | 7022 | 4 | 15 | 27 |
| 25 | 610 | 7797 | 3 | 16 | 28 |

- first coin 16.1 s, first purchase 30.5 s, purchases 150
- longest spell with nothing affordable 17.0 s (from 1395 s)
- fewest purchases in a minute: 3 in minutes 1-10 (minute 6), 2 after (minute 17)
- level reached (minute): L2 1.2 L3 2.5 L4 3.8 L5 5.3 L6 6.5 L7 7.7 L8 8.8 L9 9.5 L10 11.5 L11 13.0 L12 14.9 L13 17.1 L14 18.5 L15 21.4 L16 24.9
- charter at minute 24.9
- purchases by kind before the charter: Villager 26, Pace 24, Load 3, Pave 22, Building 4, Tools 14, Value 37, Extend 14, BellTrack 5
- longest gap with nothing new on the map after minute 2: 2.6 min (from minute 18.9); requests met 5 of 10

### Targets

- seed 1 saver: 3 purchases in minute 6 < 4
- seed 1 saver: charter at 24.9 min, outside 25-40

## The second age, The River (2026-11-25, town-age2-progress.md)

Made with `dotnet run --project tools/TideToll.Tools -c Release -- town --river --seeds 1-10` (first age played by the same bot to the charter, then rung in; times in minutes after the ring; coin by source earned in the second age: landing = boats and the landing store, toll, river market = Wool and Cloth at the market, town = every other delivery). Crossing columns: people crossed (by any crossing) per minute while that crossing was the newest: use, which follows demand. The tested crossing target is capacity (`Town.CrossingCapacity`), printed per run below the table. Targets in `TownRiverTargets`; a MISS names each one missed.

| seed | bot | rung (min) | age (min) | buys/min | longest nothing affordable (s) | longest map gap (s) | landing share at mid | ford/ferry/foot/draw per min | longest crossing wait (s) | boats come/filled/short | coin landing / toll / river market / town |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | cheapest first | 33.2 | 21.4 | 3.37 | 56 | 157 | 0.94 | 0.0/17.8/55.4/65.8 | 50 | 17/9/6 | 240906 / 1200 / 6447 / 30497 |
| 1 | best payback first | 26.0 | 19.0 | 4.74 | 42 | 129 | 0.95 | 0.0/3.2/63.0/77.3 | 7 | 14/8/6 | 438392 / 300 / 5975 / 43478 | MISS: age 19.0
| 1 | random affordable | 31.4 | 16.0 | 4.38 | 40 | 150 | 0.94 | 0.0/4.6/78.9/62.2 | 8 | 14/8/3 | 236759 / 1350 / 6372 / 18425 | MISS: age 16.0
| 1 | saver | 25.7 | 25.4 | 3.04 | 38 | 216 | 0.80 | 0.0/39.5/56.5/95.0 | 46 | 22/8/11 | 262553 / 900 / 1276 / 60045 | MISS: map gap
| 2 | cheapest first | 33.0 | 22.9 | 3.15 | 54 | 133 | 0.81 | 14.2/33.0/73.3/84.9 | 33 | 18/9/8 | 253176 / 600 / 0 / 21740 |
| 2 | best payback first | 28.3 | 21.0 | 3.76 | 44 | 161 | 0.92 | 0.0/43.8/76.2/95.8 | 43 | 20/9/8 | 293446 / 1125 / 4473 / 36939 |
| 2 | random affordable | 30.4 | 24.0 | 3.38 | 46 | 177 | 0.94 | 0.0/34.9/63.5/50.0 | 48 | 23/11/9 | 321701 / 1350 / 4221 / 25049 |
| 2 | saver | 26.2 | 27.8 | 3.10 | 47 | 238 | 0.76 | 5.9/43.6/55.7/75.4 | 30 | 23/8/12 | 278339 / 750 / 4082 / 69926 | MISS: map gap
| 3 | cheapest first | 32.6 | 32.4 | 2.16 | 67 | 279 | 0.62 | 0.0/21.1/42.2/43.5 | 40 | 26/4/19 | 191665 / 1350 / 1837 / 60715 | MISS: nothing affordable, map gap
| 3 | best payback first | 29.0 | 29.3 | 2.46 | 53 | 302 | 0.54 | 1.7/34.8/39.1/80.4 | 36 | 23/5/15 | 214194 / 1050 / 312 / 63999 | MISS: map gap
| 3 | random affordable | 30.1 | 36.5 | 2.33 | 65 | 322 | 0.75 | 0.0/25.1/43.6/56.0 | 54 | 32/11/19 | 313815 / 1575 / 1636 / 61934 | MISS: age 36.5, nothing affordable, map gap
| 3 | saver | 25.1 | 31.0 | 2.65 | 51 | 291 | 0.63 | 0.0/16.3/52.2/75.1 | 28 | 23/9/14 | 307748 / 0 / 0 / 74208 | MISS: map gap
| 4 | cheapest first | 31.2 | 28.5 | 2.60 | 56 | 156 | 0.79 | 0.0/19.4/26.7/59.9 | 47 | 23/9/11 | 205870 / 1350 / 2413 / 73711 |
| 4 | best payback first | 26.1 | 25.5 | 3.65 | 60 | 203 | 0.92 | 0.0/32.0/34.8/69.2 | 37 | 20/11/7 | 405025 / 1125 / 4795 / 87939 |
| 4 | random affordable | 33.8 | 19.0 | 3.68 | 47 | 162 | 0.87 | 0.0/24.4/25.4/1.3 | 72 | 16/10/5 | 205363 / 300 / 4674 / 55500 | MISS: age 19.0
| 4 | saver | 24.1 | 23.5 | 3.87 | 38 | 157 | 0.60 | 0.0/41.7/14.4/90.9 | 42 | 20/8/9 | 371474 / 1200 / 0 / 93965 |
| 5 | cheapest first | 31.6 | 24.8 | 2.86 | 71 | 168 | 0.95 | 0.0/30.6/62.2/67.2 | 66 | 20/9/8 | 221576 / 1350 / 8654 / 34278 | MISS: nothing affordable
| 5 | best payback first | 26.7 | 24.3 | 3.00 | 52 | 181 | 0.88 | 0.0/30.1/61.4/94.6 | 27 | 20/7/10 | 264109 / 1425 / 3943 / 36471 |
| 5 | random affordable | 30.3 | 22.0 | 3.14 | 52 | 249 | 0.82 | 0.0/21.2/66.1/92.9 | 61 | 17/7/10 | 216978 / 1050 / 4188 / 36761 | MISS: map gap
| 5 | saver | 25.3 | 21.8 | 3.39 | 27 | 202 | 0.88 | 0.0/42.3/68.2/109.3 | 31 | 17/8/7 | 289074 / 600 / 0 / 20581 |
| 6 | cheapest first | 31.4 | 29.3 | 2.46 | 63 | 195 | 0.93 | 0.0/32.5/55.4/48.1 | 63 | 23/10/11 | 238349 / 1350 / 5310 / 27583 | MISS: nothing affordable
| 6 | best payback first | 27.8 | 24.3 | 3.45 | 57 | 178 | 0.74 | 0.0/45.6/81.7/89.0 | 18 | 20/9/8 | 344198 / 1425 / 1660 / 27109 |
| 6 | random affordable | 28.4 | 29.6 | 2.77 | 60 | 237 | 0.80 | 0.0/39.2/42.8/53.8 | 36 | 22/10/11 | 304861 / 900 / 4591 / 48448 | MISS: map gap
| 6 | saver | 25.9 | 28.3 | 3.08 | 37 | 159 | 0.79 | 0.0/52.9/74.0/46.7 | 16 | 22/9/10 | 364291 / 825 / 599 / 36383 |
| 7 | cheapest first | 29.8 | 31.1 | 2.22 | 69 | 269 | 0.94 | 4.8/25.9/51.4/88.1 | 47 | 29/11/17 | 253914 / 1350 / 0 / 9502 | MISS: nothing affordable, map gap
| 7 | best payback first | 27.5 | 22.9 | 3.50 | 46 | 231 | 0.94 | 0.0/38.7/44.4/59.9 | 33 | 19/6/11 | 317227 / 900 / 0 / 39203 | MISS: map gap
| 7 | random affordable | 27.0 | 32.7 | 2.82 | 68 | 354 | 0.91 | 0.0/23.9/50.8/56.9 | 45 | 28/8/17 | 357537 / 2775 / 9383 / 102498 | MISS: nothing affordable, map gap
| 7 | saver | 23.3 | 28.0 | 2.93 | 46 | 218 | 0.86 | 0.0/18.0/49.8/25.8 | 12 | 27/10/14 | 346073 / 1125 / 2155 / 31706 | MISS: map gap
| 8 | cheapest first | 33.3 | 24.0 | 2.75 | 56 | 196 | 0.73 | 0.0/24.8/67.3/99.4 | 39 | 18/5/11 | 199246 / 750 / 163 / 32251 |
| 8 | best payback first | 27.5 | 30.0 | 2.87 | 55 | 306 | 0.52 | 0.0/45.0/71.6/80.4 | 48 | 23/5/16 | 330335 / 1050 / 0 / 68359 | MISS: map gap
| 8 | random affordable | 32.3 | 26.9 | 2.82 | 57 | 273 | 0.90 | 0.0/34.3/57.3/94.4 | 49 | 20/9/11 | 261060 / 0 / 2542 / 51679 | MISS: map gap
| 8 | saver | 26.6 | 19.1 | 3.56 | 27 | 146 | 0.70 | 0.0/18.3/87.9/96.1 | 20 | 14/7/7 | 296862 / 0 / 0 / 17521 |
| 9 | cheapest first | 31.0 | 33.8 | 2.10 | 67 | 286 | 0.73 | 0.0/17.0/32.8/46.8 | 40 | 26/9/15 | 229563 / 1125 / 1343 / 41021 | MISS: nothing affordable, map gap
| 9 | best payback first | 25.3 | 32.9 | 2.52 | 54 | 271 | 0.56 | 4.3/18.6/29.2/45.1 | 135 | 26/9/14 | 309664 / 1350 / 863 / 61824 | MISS: map gap
| 9 | random affordable | 29.5 | 32.2 | 2.48 | 63 | 342 | 0.74 | 0.0/17.4/43.0/54.1 | 51 | 25/9/14 | 284144 / 1050 / 2015 / 56780 | MISS: nothing affordable, map gap
| 9 | saver | 25.0 | 31.8 | 2.48 | 36 | 225 | 0.23 | 1.3/18.7/36.2/46.7 | 19 | 28/7/18 | 306719 / 1275 / 160 / 100904 | MISS: map gap, landing share
| 10 | cheapest first | 32.0 | 31.8 | 2.17 | 58 | 208 | 0.89 | 0.0/18.3/38.9/67.1 | 69 | 29/8/18 | 218397 / 2025 / 8256 / 23443 |
| 10 | best payback first | 25.2 | 26.1 | 3.48 | 37 | 258 | 0.73 | 0.0/25.2/36.6/119.7 | 64 | 21/8/12 | 454155 / 300 / 2136 / 34606 | MISS: map gap
| 10 | random affordable | 30.3 | 26.5 | 2.76 | 45 | 262 | 0.82 | 1.2/16.2/65.4/0.0 | 33 | 24/10/12 | 268124 / 975 / 0 / 20527 | MISS: map gap
| 10 | saver | 25.2 | 26.3 | 3.28 | 27 | 169 | 0.67 | 0.0/17.0/39.2/75.9 | 52 | 21/8/12 | 379714 / 1125 / 7334 / 63175 |

Level times (minutes after the ring), levels 17 to 28:
- seed 1 cheapest first: 0.0 0.9 1.3 2.2 2.8 4.0 6.9 9.1 13.2 15.8 17.9 21.4 | capacity at the seal (ford/ferry/foot/draw a minute): 16/74/213/242 | bought: Ferry 1 Berth 0 Crane 1 BoatPrices 20 RiverValue 29 Raft 2 Rope 3 Toll 2 Winch 4
- seed 1 best payback first: 0.0 0.8 1.7 2.3 2.9 4.2 6.2 8.5 10.0 12.7 15.7 19.0 | capacity at the seal (ford/ferry/foot/draw a minute): 17/137/223/252 | bought: Ferry 1 Berth 1 Crane 5 BoatPrices 20 RiverValue 30 Raft 5 Rope 5 Toll 3 Winch 4
- seed 1 random affordable: 0.0 0.7 1.3 1.9 2.4 3.7 6.2 8.0 9.4 11.9 13.4 16.0 | capacity at the seal (ford/ferry/foot/draw a minute): 17/74/223/252 | bought: Ferry 1 Berth 0 Crane 2 BoatPrices 19 RiverValue 27 Raft 2 Rope 3 Toll 2 Winch 4
- seed 1 saver: 0.0 0.6 1.3 2.6 3.1 6.2 9.7 13.2 15.2 18.8 22.1 25.4 | capacity at the seal (ford/ferry/foot/draw a minute): 17/96/223/252 | bought: Ferry 1 Berth 1 Crane 5 BoatPrices 20 RiverValue 20 Raft 3 Rope 4 Toll 0 Winch 5
- seed 2 cheapest first: 0.0 1.1 1.9 2.5 3.7 5.9 9.5 11.3 14.4 16.6 18.8 22.9 | capacity at the seal (ford/ferry/foot/draw a minute): 16/74/213/242 | bought: Ferry 1 Berth 0 Crane 1 BoatPrices 19 RiverValue 29 Raft 2 Rope 3 Toll 2 Winch 4
- seed 2 best payback first: 0.0 0.8 1.3 2.2 2.5 3.6 6.2 9.3 11.6 13.3 16.5 21.0 | capacity at the seal (ford/ferry/foot/draw a minute): 17/96/223/252 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 20 RiverValue 28 Raft 3 Rope 4 Toll 2 Winch 4
- seed 2 random affordable: 0.0 0.8 1.6 2.4 3.0 4.5 7.8 11.0 13.3 17.2 21.5 24.0 | capacity at the seal (ford/ferry/foot/draw a minute): 17/80/223/252 | bought: Ferry 1 Berth 1 Crane 2 BoatPrices 20 RiverValue 32 Raft 2 Rope 4 Toll 3 Winch 5
- seed 2 saver: 0.0 0.6 1.5 2.6 3.5 5.9 8.2 10.3 14.3 17.6 23.8 27.8 | capacity at the seal (ford/ferry/foot/draw a minute): 17/119/223/252 | bought: Ferry 1 Berth 1 Crane 4 BoatPrices 20 RiverValue 24 Raft 4 Rope 5 Toll 2 Winch 4
- seed 3 cheapest first: 0.0 0.7 0.8 2.6 4.2 7.7 10.8 15.4 20.1 24.3 28.6 32.4 | capacity at the seal (ford/ferry/foot/draw a minute): 20/83/266/302 | bought: Ferry 1 Berth 0 Crane 1 BoatPrices 19 RiverValue 27 Raft 2 Rope 3 Toll 2 Winch 4
- seed 3 best payback first: 0.0 0.8 1.1 1.9 3.2 6.5 9.4 12.4 17.4 21.4 25.0 29.3 | capacity at the seal (ford/ferry/foot/draw a minute): 20/83/266/302 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 19 RiverValue 25 Raft 2 Rope 3 Toll 2 Winch 4
- seed 3 random affordable: 0.0 0.2 1.3 2.5 5.8 10.4 14.0 17.7 24.0 28.6 32.6 36.5 | capacity at the seal (ford/ferry/foot/draw a minute): 20/106/266/302 | bought: Ferry 1 Berth 1 Crane 2 BoatPrices 20 RiverValue 34 Raft 3 Rope 4 Toll 3 Winch 5
- seed 3 saver: 0.0 1.1 2.5 3.1 6.3 10.5 14.3 16.8 19.6 24.5 28.0 31.0 | capacity at the seal (ford/ferry/foot/draw a minute): 20/131/266/302 | bought: Ferry 1 Berth 1 Crane 5 BoatPrices 20 RiverValue 25 Raft 4 Rope 5 Toll 2 Winch 4
- seed 4 cheapest first: 0.0 0.8 1.9 2.9 3.8 6.5 9.9 13.7 17.0 19.2 24.8 28.5 | capacity at the seal (ford/ferry/foot/draw a minute): 16/74/213/242 | bought: Ferry 1 Berth 0 Crane 1 BoatPrices 19 RiverValue 29 Raft 2 Rope 3 Toll 2 Winch 4
- seed 4 best payback first: 0.0 0.9 1.7 2.7 3.5 5.2 8.3 10.2 13.4 16.8 21.1 25.5 | capacity at the seal (ford/ferry/foot/draw a minute): 16/119/213/242 | bought: Ferry 1 Berth 1 Crane 5 BoatPrices 20 RiverValue 33 Raft 4 Rope 5 Toll 3 Winch 5
- seed 4 random affordable: 0.0 0.7 1.2 1.9 2.4 3.7 5.9 8.6 10.2 12.9 15.9 19.0 | capacity at the seal (ford/ferry/foot/draw a minute): 17/74/223/252 | bought: Ferry 1 Berth 1 Crane 1 BoatPrices 19 RiverValue 28 Raft 2 Rope 3 Toll 2 Winch 3
- seed 4 saver: 0.0 1.1 2.1 2.7 4.2 6.1 8.8 10.9 13.5 15.9 19.5 23.5 | capacity at the seal (ford/ferry/foot/draw a minute): 17/137/223/252 | bought: Ferry 1 Berth 1 Crane 5 BoatPrices 20 RiverValue 30 Raft 5 Rope 5 Toll 2 Winch 5
- seed 5 cheapest first: 0.0 0.7 1.5 2.6 3.4 5.4 7.3 11.1 16.1 17.8 21.5 24.8 | capacity at the seal (ford/ferry/foot/draw a minute): 20/83/266/302 | bought: Ferry 1 Berth 0 Crane 1 BoatPrices 19 RiverValue 29 Raft 2 Rope 3 Toll 2 Winch 4
- seed 5 best payback first: 0.0 0.7 1.5 2.2 2.8 5.7 8.6 10.7 13.5 17.0 19.8 24.3 | capacity at the seal (ford/ferry/foot/draw a minute): 21/106/278/316 | bought: Ferry 1 Berth 1 Crane 4 BoatPrices 19 RiverValue 25 Raft 3 Rope 4 Toll 2 Winch 3
- seed 5 random affordable: 0.0 0.6 1.2 1.7 2.5 4.5 7.3 10.4 12.3 16.4 18.5 22.0 | capacity at the seal (ford/ferry/foot/draw a minute): 21/83/278/316 | bought: Ferry 1 Berth 0 Crane 1 BoatPrices 20 RiverValue 27 Raft 2 Rope 3 Toll 2 Winch 3
- seed 5 saver: 0.0 0.9 1.7 2.5 3.7 6.6 9.2 10.8 13.7 16.0 17.5 21.8 | capacity at the seal (ford/ferry/foot/draw a minute): 20/112/259/294 | bought: Ferry 1 Berth 1 Crane 4 BoatPrices 20 RiverValue 22 Raft 3 Rope 5 Toll 1 Winch 3
- seed 6 cheapest first: 0.0 1.3 1.9 3.7 4.5 6.2 9.9 14.1 17.9 21.1 24.9 29.3 | capacity at the seal (ford/ferry/foot/draw a minute): 20/83/266/302 | bought: Ferry 1 Berth 0 Crane 1 BoatPrices 19 RiverValue 29 Raft 2 Rope 3 Toll 2 Winch 4
- seed 6 best payback first: 0.0 0.8 1.7 2.6 5.0 7.8 9.6 11.2 14.2 17.1 20.6 24.3 | capacity at the seal (ford/ferry/foot/draw a minute): 21/112/278/316 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 20 RiverValue 30 Raft 3 Rope 5 Toll 3 Winch 4
- seed 6 random affordable: 0.0 0.9 2.0 3.0 6.1 8.1 10.2 13.8 17.7 20.9 25.9 29.6 | capacity at the seal (ford/ferry/foot/draw a minute): 21/112/278/316 | bought: Ferry 1 Berth 1 Crane 1 BoatPrices 20 RiverValue 32 Raft 3 Rope 5 Toll 3 Winch 4
- seed 6 saver: 0.0 0.4 1.7 2.9 5.5 8.2 9.6 13.3 17.0 20.0 24.3 28.3 | capacity at the seal (ford/ferry/foot/draw a minute): 21/131/278/316 | bought: Ferry 1 Berth 1 Crane 5 BoatPrices 20 RiverValue 26 Raft 4 Rope 5 Toll 2 Winch 4
- seed 7 cheapest first: 0.0 1.1 1.7 2.5 3.4 5.6 10.3 14.7 17.3 21.6 27.2 31.1 | capacity at the seal (ford/ferry/foot/draw a minute): 20/83/266/302 | bought: Ferry 1 Berth 0 Crane 1 BoatPrices 19 RiverValue 28 Raft 2 Rope 3 Toll 2 Winch 4
- seed 7 best payback first: 0.0 0.0 0.8 1.5 2.4 3.6 5.0 8.0 11.9 15.1 18.9 22.9 | capacity at the seal (ford/ferry/foot/draw a minute): 21/106/278/316 | bought: Ferry 1 Berth 1 Crane 4 BoatPrices 20 RiverValue 28 Raft 3 Rope 4 Toll 2 Winch 4
- seed 7 random affordable: 0.0 0.9 1.6 2.5 3.2 4.8 8.9 12.7 16.2 22.9 26.5 32.7 | capacity at the seal (ford/ferry/foot/draw a minute): 20/112/266/302 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 20 RiverValue 36 Raft 3 Rope 5 Toll 5 Winch 6
- seed 7 saver: 0.0 0.1 1.0 2.4 3.6 5.6 9.4 13.4 16.1 19.6 24.5 28.0 | capacity at the seal (ford/ferry/foot/draw a minute): 21/131/278/316 | bought: Ferry 1 Berth 1 Crane 5 BoatPrices 20 RiverValue 24 Raft 4 Rope 5 Toll 1 Winch 4
- seed 8 cheapest first: 0.0 1.0 1.9 3.0 4.2 7.5 10.6 13.6 16.1 19.0 20.7 24.0 | capacity at the seal (ford/ferry/foot/draw a minute): 16/74/213/242 | bought: Ferry 1 Berth 0 Crane 1 BoatPrices 18 RiverValue 25 Raft 2 Rope 3 Toll 2 Winch 3
- seed 8 best payback first: 0.0 1.1 2.6 3.2 5.7 7.9 12.0 14.2 17.1 19.8 26.6 30.0 | capacity at the seal (ford/ferry/foot/draw a minute): 17/102/223/252 | bought: Ferry 1 Berth 1 Crane 4 BoatPrices 20 RiverValue 30 Raft 3 Rope 5 Toll 2 Winch 4
- seed 8 random affordable: 0.0 1.1 2.0 3.5 4.1 7.2 9.8 12.7 17.3 20.3 24.7 26.9 | capacity at the seal (ford/ferry/foot/draw a minute): 17/85/223/252 | bought: Ferry 1 Berth 0 Crane 2 BoatPrices 20 RiverValue 29 Raft 2 Rope 5 Toll 2 Winch 4
- seed 8 saver: 0.0 0.8 1.8 2.8 5.3 6.8 8.3 9.3 11.7 13.6 17.2 19.1 | capacity at the seal (ford/ferry/foot/draw a minute): 17/119/217/246 | bought: Ferry 1 Berth 1 Crane 5 BoatPrices 20 RiverValue 17 Raft 4 Rope 5 Toll 0 Winch 3
- seed 9 cheapest first: 0.0 1.1 2.8 4.5 5.5 7.2 11.8 16.5 21.9 26.6 30.2 33.8 | capacity at the seal (ford/ferry/foot/draw a minute): 20/83/266/302 | bought: Ferry 1 Berth 0 Crane 1 BoatPrices 19 RiverValue 28 Raft 2 Rope 3 Toll 2 Winch 4
- seed 9 best payback first: 0.0 0.5 1.4 3.6 5.8 10.3 13.0 16.9 19.2 24.3 28.8 32.9 | capacity at the seal (ford/ferry/foot/draw a minute): 21/106/278/316 | bought: Ferry 1 Berth 1 Crane 4 BoatPrices 20 RiverValue 31 Raft 3 Rope 4 Toll 3 Winch 4
- seed 9 random affordable: 0.0 0.8 2.5 4.3 5.3 6.8 10.2 14.8 20.5 25.0 29.6 32.2 | capacity at the seal (ford/ferry/foot/draw a minute): 21/89/278/316 | bought: Ferry 1 Berth 1 Crane 1 BoatPrices 20 RiverValue 32 Raft 2 Rope 4 Toll 3 Winch 5
- seed 9 saver: 0.0 0.6 1.1 2.0 4.0 7.1 11.4 16.1 18.9 23.9 27.6 31.8 | capacity at the seal (ford/ferry/foot/draw a minute): 21/131/278/316 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 22 Raft 4 Rope 5 Toll 1 Winch 5
- seed 10 cheapest first: 0.0 1.1 1.8 2.8 4.4 7.0 10.9 14.4 18.0 22.1 26.0 31.8 | capacity at the seal (ford/ferry/foot/draw a minute): 20/83/266/302 | bought: Ferry 1 Berth 0 Crane 1 BoatPrices 18 RiverValue 27 Raft 2 Rope 3 Toll 2 Winch 4
- seed 10 best payback first: 0.0 0.9 1.9 2.4 4.3 7.1 10.8 13.6 17.9 20.9 23.7 26.1 | capacity at the seal (ford/ferry/foot/draw a minute): 21/131/278/316 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 31 Raft 4 Rope 5 Toll 3 Winch 5
- seed 10 random affordable: 0.0 0.7 1.6 2.8 3.6 6.0 8.8 11.3 15.0 19.3 23.2 26.5 | capacity at the seal (ford/ferry/foot/draw a minute): 21/83/278/316 | bought: Ferry 1 Berth 1 Crane 1 BoatPrices 19 RiverValue 29 Raft 2 Rope 3 Toll 2 Winch 4
- seed 10 saver: 0.0 0.9 1.6 2.2 4.1 6.9 9.6 12.4 15.2 18.7 22.0 26.3 | capacity at the seal (ford/ferry/foot/draw a minute): 21/149/278/316 | bought: Ferry 1 Berth 1 Crane 5 BoatPrices 20 RiverValue 25 Raft 5 Rope 5 Toll 2 Winch 6

misses: 33 (target checks over all 40 runs; what is red and why: town-age2-progress.md)

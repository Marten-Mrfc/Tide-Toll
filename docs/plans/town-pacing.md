# Town pacing (first age, rules v2.2)

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

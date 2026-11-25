# Town pacing (first age, rules v2)

Generated on 25 November 2026 (rules v2, `docs/plans/town-rules2-progress.md`) with the numbers in `sim/Town/TownRules.cs`:
`town --seeds 1-10`, `town --extras --seed 1` and `town --seed 1 --markdown`. Targets are `TownTargets` in the same file; `sim.tests/Town/TownPacingTests.cs` checks them on seeds 1 to 10 with all four bots.

How the bots play: they think every 0.5 game seconds, buy at most one thing per think, ring the bell whenever it is ready, and stop buying at the charter. Cheapest first buys the cheapest affordable item; best payback first buys the item with the best estimated extra coin per minute per coin, saving for it only when it is under 20 s of income away; random affordable buys a random affordable item (its own seeded stream); the saver (new) picks the best payback among everything unlocked, affordable or not, and waits for it when it is under 40 s of income away, else buys the best affordable meanwhile. "Nothing affordable" counts every moment no shop item could be bought. "Something new on the map" is a level gift, a building opened, a road paved or a cottage extended.

## Targets not met (honest)

- **Purchases keep coming**: 39 misses on seeds 1 to 10. Late in the age (minutes 16 to 30) every bot has single minutes with 1 purchase (target 2): by then Pace, Tools and the value tracks are finished and what is left (villagers, extensions, Handcart, the mill, the last paves) costs one to two minutes of income. Early, the saver (and on a few seeds the payback and random bots) has a minute 5 to 8 with 2 or 3 purchases (target 4).
- **Age 25 to 40 for every bot**: the saver finishes at 24.3 (seed 4) and 24.7 (seed 10) minutes; all other 38 runs are inside (24.3 to 32.7 overall; spread 1.3 at most, target 2).
- **Nothing affordable over 60 s**: once, seed 2 payback, 64 s.
- **Something new on the map every 3.5 min**: 4 misses: the saver on seeds 1, 9 and 10 (3.6 to 3.7 minutes) and the payback bot on seed 1 (3.5 rounded, just over), once each around minute 18 to 22 (the gap before level 13, the mill).
- **Bell 10 to 25 percent** (seed 1, coin earned in 20 minutes, ringing whenever ready against never): cheapest +10.9, payback +15.5, saver +29.5 (over by 4.5 points). The random bot (+27.1) is not part of the test: its random picks diverge with and without the bell.
- **Requests met by a normal bot, half to four fifths**: the cheapest and payback bots miss the band on 6 of 20 runs (4 of 13 to 11 of 11).

Met: first coin 14.9 s, first purchase 15 to 28.5 s; no track above 40 percent of any bot's purchases (largest: Villager and Value about 19 percent); the saver and payback bots buy every kind, every Load step and all four buildings before the charter (seed 1 to 10); never paving makes the charter 57 to 74 percent later on seed 1 (target at least 15).

## Seeds 1 to 10

`town --seeds 1-10`: charter minute ; longest spell with nothing affordable (s).

    seed | cheapest first | best payback first | random affordable | saver | (charter minute; longest nothing-affordable s)
       1 | 32.1 ; 55 | 27.9 ; 48 | 29.8 ; 58 | 26.0 ; 27
       2 | 32.6 ; 50 | 29.5 ; 64 | 32.1 ; 49 | 27.2 ; 26
       3 | 30.7 ; 48 | 27.8 ; 56 | 30.7 ; 48 | 25.4 ; 24
       4 | 32.3 ; 56 | 26.3 ; 30 | 32.0 ; 54 | 24.3 ; 22
       5 | 29.9 ; 45 | 27.1 ; 40 | 29.4 ; 46 | 26.5 ; 26
       6 | 31.8 ; 47 | 30.1 ; 54 | 32.7 ; 54 | 26.1 ; 24
       7 | 30.3 ; 46 | 26.4 ; 52 | 29.9 ; 50 | 26.4 ; 30
       8 | 30.9 ; 54 | 27.1 ; 32 | 30.7 ; 50 | 25.0 ; 22
       9 | 31.2 ; 50 | 28.9 ; 53 | 31.5 ; 44 | 26.2 ; 26
      10 | 31.6 ; 50 | 27.2 ; 54 | 31.3 ; 44 | 24.7 ; 23

## Bell, paving and requests (seed 1)

`town --extras --seed 1`:

    seed 1 cheapest first: charter 32.1, never paving 50.2 (57 % slower), bell +10.9 %, requests met 7/14
    seed 1 best payback first: charter 27.9, never paving 48.5 (74 % slower), bell +15.5 %, requests met 6/12
    seed 1 random affordable: charter 29.8, never paving 50.0 (68 % slower), bell +27.1 %, requests met 9/13
    seed 1 saver: charter 26.0, never paving 44.4 (71 % slower), bell +29.5 %, requests met 6/11

## Seed 1, every bot

### Seed 1, bot: cheapest first

| minute | coin | coin/min | purchases | level | villagers |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 2 | 16 | 4 | 1 | 2 |
| 2 | 2 | 74 | 7 | 2 | 4 |
| 3 | 4 | 151 | 6 | 3 | 4 |
| 4 | 39 | 238 | 7 | 4 | 6 |
| 5 | 70 | 261 | 4 | 4 | 6 |
| 6 | 102 | 384 | 6 | 5 | 8 |
| 7 | 9 | 427 | 6 | 6 | 9 |
| 8 | 60 | 704 | 5 | 7 | 10 |
| 9 | 92 | 819 | 6 | 7 | 12 |
| 10 | 53 | 1023 | 5 | 8 | 15 |
| 11 | 204 | 1214 | 9 | 9 | 15 |
| 12 | 334 | 1500 | 6 | 9 | 15 |
| 13 | 318 | 1865 | 5 | 9 | 16 |
| 14 | 214 | 1859 | 4 | 10 | 17 |
| 15 | 344 | 1995 | 3 | 10 | 18 |
| 16 | 509 | 1941 | 3 | 11 | 19 |
| 17 | 672 | 2067 | 2 | 11 | 19 |
| 18 | 434 | 2358 | 3 | 11 | 20 |
| 19 | 1312 | 2590 | 3 | 12 | 20 |
| 20 | 586 | 3397 | 3 | 12 | 20 |
| 21 | 267 | 3229 | 3 | 12 | 21 |
| 22 | 1788 | 3405 | 1 | 13 | 21 |
| 23 | 1650 | 4872 | 3 | 13 | 22 |
| 24 | 793 | 4539 | 2 | 13 | 23 |
| 25 | 2431 | 5333 | 2 | 14 | 23 |
| 26 | 315 | 4511 | 5 | 14 | 23 |
| 27 | 2306 | 6661 | 6 | 14 | 23 |
| 28 | 2163 | 5846 | 2 | 15 | 23 |
| 29 | 900 | 5563 | 2 | 15 | 23 |
| 30 | 3765 | 6365 | 1 | 15 | 24 |
| 31 | 4919 | 5900 | 1 | 15 | 25 |
| 32 | 6020 | 7072 | 1 | 15 | 25 |
| 33 | 9580 | 10070 | 1 | 16 | 26 |

- first coin 14.9 s, first purchase 15.0 s, purchases 127
- longest spell with nothing affordable 55.0 s (from 1816 s)
- fewest purchases in a minute: 4 in minutes 1-10 (minute 1), 1 after (minute 22)
- level reached (minute): L2 1.0 L3 2.2 L4 3.5 L5 5.0 L6 6.6 L7 7.9 L8 9.6 L9 10.6 L10 13.1 L11 15.4 L12 18.4 L13 21.5 L14 24.4 L15 27.7 L16 32.1
- charter at minute 32.1
- purchases by kind before the charter: Villager 24, Pace 20, Load 3, Pave 22, Building 4, Tools 10, Value 24, Extend 14, BellTrack 5
- longest gap with nothing new on the map after minute 2: 2.9 min (from minute 21.5); requests met 7 of 14

### Seed 1, bot: best payback first

| minute | coin | coin/min | purchases | level | villagers |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 10 | 24 | 4 | 1 | 2 |
| 2 | 18 | 72 | 6 | 2 | 4 |
| 3 | 32 | 177 | 7 | 3 | 4 |
| 4 | 121 | 272 | 9 | 5 | 6 |
| 5 | 21 | 255 | 5 | 5 | 7 |
| 6 | 86 | 450 | 6 | 6 | 8 |
| 7 | 42 | 507 | 4 | 6 | 9 |
| 8 | 136 | 1283 | 12 | 8 | 15 |
| 9 | 251 | 1301 | 7 | 8 | 15 |
| 10 | 153 | 1828 | 10 | 9 | 16 |
| 11 | 87 | 1789 | 6 | 10 | 16 |
| 12 | 358 | 2423 | 5 | 10 | 17 |
| 13 | 244 | 2878 | 6 | 11 | 18 |
| 14 | 738 | 2671 | 3 | 11 | 19 |
| 15 | 829 | 2823 | 3 | 11 | 19 |
| 16 | 370 | 3008 | 3 | 12 | 20 |
| 17 | 1081 | 4423 | 3 | 12 | 20 |
| 18 | 2707 | 4396 | 3 | 13 | 21 |
| 19 | 539 | 4277 | 8 | 13 | 21 |
| 20 | 649 | 5249 | 3 | 13 | 21 |
| 21 | 1970 | 5249 | 2 | 14 | 22 |
| 22 | 101 | 5793 | 3 | 14 | 23 |
| 23 | 2812 | 5153 | 1 | 14 | 23 |
| 24 | 155 | 6213 | 3 | 14 | 23 |
| 25 | 3312 | 6150 | 1 | 14 | 23 |
| 26 | 3429 | 7080 | 2 | 15 | 23 |
| 27 | 4096 | 7921 | 2 | 15 | 24 |
| 28 | 1381 | 8830 | 2 | 16 | 26 |

- first coin 14.9 s, first purchase 28.5 s, purchases 129
- longest spell with nothing affordable 47.5 s (from 1626 s)
- fewest purchases in a minute: 4 in minutes 1-10 (minute 1), 1 after (minute 23)
- level reached (minute): L2 1.1 L3 2.0 L4 3.2 L5 4.0 L6 5.6 L7 7.2 L8 8.0 L9 9.1 L10 10.5 L11 12.5 L12 15.6 L13 17.4 L14 20.6 L15 25.0 L16 27.9
- charter at minute 27.9
- purchases by kind before the charter: Villager 24, Pace 20, Load 3, Pave 24, Building 4, Tools 10, Value 24, Extend 14, BellTrack 5
- longest gap with nothing new on the map after minute 2: 3.5 min (from minute 21.5); requests met 6 of 12

### Seed 1, bot: random affordable

| minute | coin | coin/min | purchases | level | villagers |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 3 | 24 | 5 | 2 | 2 |
| 2 | 11 | 78 | 6 | 2 | 4 |
| 3 | 7 | 176 | 7 | 3 | 4 |
| 4 | 41 | 269 | 7 | 4 | 6 |
| 5 | 42 | 282 | 7 | 5 | 7 |
| 6 | 7 | 443 | 7 | 6 | 9 |
| 7 | 109 | 570 | 4 | 6 | 10 |
| 8 | 92 | 874 | 7 | 7 | 12 |
| 9 | 208 | 954 | 6 | 8 | 15 |
| 10 | 47 | 1143 | 7 | 9 | 15 |
| 11 | 47 | 1701 | 9 | 9 | 16 |
| 12 | 445 | 1630 | 3 | 10 | 16 |
| 13 | 329 | 2126 | 5 | 10 | 18 |
| 14 | 195 | 2110 | 6 | 11 | 18 |
| 15 | 322 | 2468 | 3 | 11 | 19 |
| 16 | 557 | 3031 | 3 | 12 | 19 |
| 17 | 256 | 2782 | 3 | 12 | 20 |
| 18 | 609 | 3395 | 3 | 12 | 20 |
| 19 | 391 | 3009 | 2 | 13 | 21 |
| 20 | 309 | 3766 | 2 | 13 | 21 |
| 21 | 1785 | 4846 | 2 | 13 | 22 |
| 22 | 1636 | 4594 | 2 | 13 | 23 |
| 23 | 1543 | 5455 | 2 | 14 | 23 |
| 24 | 1709 | 4710 | 2 | 14 | 23 |
| 25 | 1040 | 5680 | 8 | 14 | 23 |
| 26 | 2504 | 5347 | 2 | 14 | 23 |
| 27 | 1997 | 7187 | 3 | 15 | 23 |
| 28 | 4643 | 6159 | 1 | 15 | 24 |
| 29 | 5992 | 6152 | 1 | 15 | 25 |
| 30 | 1915 | 8523 | 2 | 16 | 26 |

- first coin 14.9 s, first purchase 15.0 s, purchases 127
- longest spell with nothing affordable 57.5 s (from 1683 s)
- fewest purchases in a minute: 4 in minutes 1-10 (minute 7), 1 after (minute 28)
- level reached (minute): L2 0.9 L3 2.0 L4 3.2 L5 4.2 L6 5.5 L7 7.1 L8 8.5 L9 9.8 L10 11.5 L11 13.7 L12 15.8 L13 18.9 L14 22.3 L15 26.1 L16 29.8
- charter at minute 29.8
- purchases by kind before the charter: Villager 24, Pace 20, Load 3, Pave 22, Building 4, Tools 10, Value 24, Extend 14, BellTrack 5
- longest gap with nothing new on the map after minute 2: 2.8 min (from minute 19.4); requests met 9 of 13

### Seed 1, bot: saver

| minute | coin | coin/min | purchases | level | villagers |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 10 | 24 | 4 | 1 | 2 |
| 2 | 18 | 72 | 6 | 2 | 4 |
| 3 | 32 | 177 | 7 | 3 | 4 |
| 4 | 181 | 272 | 8 | 4 | 6 |
| 5 | 13 | 282 | 2 | 5 | 6 |
| 6 | 156 | 687 | 11 | 6 | 10 |
| 7 | 49 | 920 | 11 | 7 | 13 |
| 8 | 259 | 1522 | 7 | 8 | 15 |
| 9 | 20 | 1502 | 9 | 9 | 15 |
| 10 | 22 | 1678 | 7 | 9 | 16 |
| 11 | 1270 | 2033 | 5 | 10 | 16 |
| 12 | 322 | 2529 | 4 | 11 | 17 |
| 13 | 242 | 3807 | 6 | 11 | 20 |
| 14 | 3374 | 4049 | 5 | 12 | 20 |
| 15 | 1115 | 3672 | 1 | 12 | 20 |
| 16 | 284 | 5287 | 5 | 13 | 21 |
| 17 | 1878 | 5397 | 6 | 13 | 21 |
| 18 | 1231 | 5937 | 5 | 13 | 22 |
| 19 | 834 | 5978 | 2 | 14 | 24 |
| 20 | 3398 | 6193 | 2 | 14 | 24 |
| 21 | 1584 | 6631 | 3 | 14 | 25 |
| 22 | 1445 | 6813 | 3 | 15 | 25 |
| 23 | 3379 | 6815 | 2 | 15 | 25 |
| 24 | 907 | 7424 | 2 | 15 | 26 |
| 25 | 713 | 8187 | 3 | 15 | 26 |
| 26 | 215 | 8436 | 1 | 16 | 27 |

- first coin 14.9 s, first purchase 28.5 s, purchases 127
- longest spell with nothing affordable 27.0 s (from 1496 s)
- fewest purchases in a minute: 2 in minutes 1-10 (minute 5), 1 after (minute 15)
- level reached (minute): L2 1.1 L3 2.0 L4 3.2 L5 4.5 L6 5.4 L7 6.1 L8 7.3 L9 8.4 L10 10.2 L11 11.9 L12 13.8 L13 15.3 L14 18.2 L15 21.8 L16 26.0
- charter at minute 26.0
- purchases by kind before the charter: Villager 25, Pace 20, Load 3, Pave 22, Building 4, Tools 10, Value 24, Extend 13, BellTrack 5
- longest gap with nothing new on the map after minute 2: 3.6 min (from minute 18.2); requests met 6 of 11

### Targets

- seed 1 cheapest first: 1 purchases in minute 22 < 2
- seed 1 best payback first: 1 purchases in minute 23 < 2
- seed 1 random affordable: 1 purchases in minute 28 < 2
- seed 1 saver: 2 purchases in minute 5 < 4
- seed 1 saver: 1 purchases in minute 15 < 2
- seed 1 best payback first: nothing new on the map for 3.5 min (from minute 21.5) > 3.5
- seed 1 saver: nothing new on the map for 3.6 min (from minute 18.2) > 3.5

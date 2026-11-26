# Town pacing

## Green pass 2 (current rules; 26 November 2026)

These tables are the current numbers. The "Green pass" section below and every older section are history.

What changed: Regulars (sim/Town/TownRegulars.cs), the first age's ladder of small steps. Each regular makes every delivery pay 0.4 % more, for good. The ladder starts again at every level at 0.15 minutes of the town's best coin rate when the level was reached (at least 20 a minute), 1.35 times dearer for each one won at that level. So a buy-when-you-can player always finds something cheap within the minute, and no other track is bought out for it. Not sold in the second age (whatever was won stays).

Tried and dropped: Regulars on sale in the second age too (at 0.15, 0.04 and 0.02 of the best rate): boats pay in lumps that swell the best rate, and the cheapest bot drained its coin into the ladder; spells with nothing affordable got longer (62 to 82 s), not shorter. Lowering the second age's TrackStart (0.2, 0.15) did not help either.

### First age, seeds 1 to 10 (tool: town --seeds 1-10 --misses --v3)

Columns per bot (cheapest first, best payback first, random affordable, saver): charter minute; longest spell with nothing affordable (s); requests met/asked; boons; last gap / longest of the three before (minutes); hands bought; regulars won.

```
   1 | 37.3 ; 24 ; req 11/16 ; AAAA ; gap 3.6/4.7 ; hands 2 ; regs 52 | 31.7 ; 21 ; req 7/14 ; AAAB ; gap 3.0/4.2 ; hands 3 ; regs 32 | 35.1 ; 22 ; req 9/15 ; ABAB ; gap 2.5/4.3 ; hands 3 ; regs 49 | 27.6 ; 16 ; req 8/12 ; BAAB ; gap 3.0/4.1 ; hands 3 ; regs 15
   2 | 38.5 ; 27 ; req 9/16 ; AAAA ; gap 3.0/5.1 ; hands 2 ; regs 55 | 30.3 ; 18 ; req 7/13 ; BBAA ; gap 2.7/3.1 ; hands 1 ; regs 30 | 37.8 ; 24 ; req 9/16 ; AAAA ; gap 2.5/5.0 ; hands 2 ; regs 54 | 28.7 ; 17 ; req 8/12 ; BAAA ; gap 3.2/2.9 ; hands 2 ; regs 16
   3 | 37.5 ; 26 ; req 9/15 ; AAAA ; gap 3.2/4.6 ; hands 2 ; regs 51 | 31.3 ; 18 ; req 7/13 ; BAAA ; gap 3.5/4.4 ; hands 2 ; regs 30 | 37.1 ; 24 ; req 7/15 ; AABA ; gap 2.7/4.0 ; hands 2 ; regs 51 | 27.6 ; 16 ; req 5/11 ; BABA ; gap 3.7/3.3 ; hands 2 ; regs 13
   4 | 36.2 ; 26 ; req 12/14 ; AAAA ; gap 3.8/4.5 ; hands 2 ; regs 51 | 30.7 ; 17 ; req 4/13 ; AABB ; gap 3.5/3.9 ; hands 1 ; regs 28 | 35.2 ; 24 ; req 8/14 ; BABB ; gap 4.0/3.8 ; hands 1 ; regs 51 | 27.5 ; 17 ; req 9/11 ; BAAB ; gap 3.0/3.3 ; hands 2 ; regs 12
   5 | 36.3 ; 26 ; req 12/14 ; AAAA ; gap 2.9/4.6 ; hands 2 ; regs 54 | 27.5 ; 17 ; req 7/11 ; BABB ; gap 3.3/3.4 ; hands 1 ; regs 26 | 34.0 ; 21 ; req 6/14 ; BBBA ; gap 3.2/3.5 ; hands 0 ; regs 49 | 26.6 ; 17 ; req 6/10 ; BABA ; gap 3.6/3.2 ; hands 1 ; regs 12
   6 | 36.5 ; 26 ; req 11/15 ; AAAA ; gap 2.8/4.6 ; hands 2 ; regs 52 | 29.5 ; 16 ; req 8/12 ; BBAA ; gap 2.9/3.3 ; hands 1 ; regs 29 | 35.0 ; 22 ; req 9/15 ; BBAB ; gap 3.2/4.0 ; hands 2 ; regs 50 | 27.5 ; 16 ; req 7/11 ; BBAA ; gap 2.2/3.1 ; hands 2 ; regs 18
   7 | 34.5 ; 24 ; req 12/14 ; AAAA ; gap 2.6/4.3 ; hands 3 ; regs 50 | 29.4 ; 19 ; req 8/13 ; BAAB ; gap 2.4/4.4 ; hands 3 ; regs 29 | 32.6 ; 22 ; req 8/13 ; AABA ; gap 2.0/3.6 ; hands 2 ; regs 47 | 26.2 ; 16 ; req 6/12 ; BBAA ; gap 3.4/2.9 ; hands 2 ; regs 12
   8 | 37.4 ; 26 ; req 12/15 ; AAAA ; gap 3.2/4.8 ; hands 2 ; regs 53 | 28.3 ; 18 ; req 7/12 ; BABB ; gap 2.0/3.3 ; hands 2 ; regs 28 | 34.9 ; 22 ; req 7/15 ; BBBB ; gap 3.1/4.3 ; hands 0 ; regs 49 | 27.5 ; 16 ; req 9/11 ; BAAB ; gap 2.5/3.4 ; hands 4 ; regs 14
   9 | 37.0 ; 24 ; req 11/15 ; AAAA ; gap 3.0/4.2 ; hands 2 ; regs 53 | 28.6 ; 16 ; req 7/12 ; BBAA ; gap 3.0/2.9 ; hands 1 ; regs 30 | 36.7 ; 24 ; req 11/15 ; BAAA ; gap 3.4/4.1 ; hands 2 ; regs 50 | 26.4 ; 16 ; req 6/11 ; BBBA ; gap 2.8/3.2 ; hands 1 ; regs 13
  10 | 36.4 ; 25 ; req 11/16 ; AAAA ; gap 2.9/4.8 ; hands 2 ; regs 52 | 29.6 ; 19 ; req 8/13 ; AAAB ; gap 3.4/3.3 ; hands 2 ; regs 28 | 35.3 ; 26 ; req 12/15 ; BAAB ; gap 2.7/4.5 ; hands 2 ; regs 52 | 27.1 ; 16 ; req 6/12 ; BABB ; gap 3.7/2.5 ; hands 2 ; regs 13
misses: 0 (purchases 0, nothing affordable 0, age 0, map 0)
```

No miss: TownPacingTests.PurchasesKeepComing is green (was 3 misses). Charter 26.2 to 38.5 minutes (target 25 to 40; was 25.0 to 35.3). Longest spell with nothing affordable 27 s (limit 60; was 40 to 60). Regulars won: 47 to 55 (cheapest, random), 26 to 32 (payback), 12 to 18 (saver).

Seeds 11 to 20 (not tests): MISSED seed 16 best payback first: 3 purchases in minute 9 < 4; misses: 1 (purchases 1, nothing affordable 0, age 0, map 0). Charter 25.4 to 37.5 minutes, nothing affordable at most 28 s.

### Second age, seeds 1 to 10 (tool: town --river --seeds 1-10)

No miss. Age 22.5 to 33.8 minutes, longest spell with nothing affordable 58 s (limit 60: still thin; it moved between 56 and 69 s with small changes to the Regulars numbers, so it is luck of the boats' lumps, not structure), longest map gap 170 s (limit 210), landing share at the middle at least 0.34 (target 0.333).

| seed | bot | rung (min) | age (min) | buys/min | longest nothing affordable (s) | longest map gap (s) | landing share at mid | ford/ferry/foot/draw per min | longest crossing wait (s) | boats come/filled/short | coin landing / toll / river market / town |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | cheapest first | 37.3 | 29.6 | 5.81 | 53 | 105 | 0.80 | 0.0/23.9/53.8/63.2 | 106 | 26/14/10 | 590687 / 28500 / 32672 / 95825 |
| 1 | best payback first | 31.7 | 28.6 | 8.96 | 17 | 79 | 0.90 | 0.0/27.2/62.1/150.5 | 33 | 26/14/9 | 1605382 / 53250 / 164805 / 204269 |
| 1 | random affordable | 35.1 | 28.2 | 6.85 | 39 | 127 | 0.71 | 0.0/26.9/62.9/76.9 | 110 | 24/11/12 | 655856 / 37500 / 55671 / 158225 |
| 1 | saver | 27.6 | 24.6 | 8.80 | 17 | 105 | 0.68 | 0.0/31.4/79.2/81.6 | 41 | 22/11/8 | 1121111 / 44250 / 125043 / 147828 |
| 2 | cheapest first | 38.5 | 25.6 | 6.40 | 58 | 101 | 0.82 | 10.1/24.6/77.7/96.7 | 62 | 24/10/12 | 540791 / 20250 / 6694 / 63076 |
| 2 | best payback first | 30.3 | 26.0 | 8.04 | 25 | 137 | 0.80 | 0.0/41.4/84.8/131.9 | 72 | 25/16/8 | 989189 / 27000 / 38709 / 95300 |
| 2 | random affordable | 37.8 | 27.5 | 6.50 | 52 | 170 | 0.86 | 0.0/32.4/83.1/103.4 | 118 | 26/11/12 | 632988 / 19500 / 6572 / 47513 |
| 2 | saver | 28.7 | 27.9 | 7.50 | 19 | 100 | 0.81 | 0.0/58.7/98.7/96.4 | 62 | 29/17/9 | 1107083 / 49500 / 48849 / 41676 |
| 3 | cheapest first | 37.5 | 24.0 | 4.78 | 39 | 102 | 0.95 | 0.0/25.6/49.6/45.5 | 52 | 23/9/11 | 226016 / 17250 / 9830 / 75319 |
| 3 | best payback first | 31.3 | 25.8 | 5.65 | 33 | 90 | 0.87 | 0.0/49.3/54.2/94.9 | 30 | 24/14/8 | 544007 / 24750 / 3183 / 40205 |
| 3 | random affordable | 37.1 | 25.4 | 6.02 | 39 | 101 | 0.90 | 0.0/19.0/50.1/34.4 | 49 | 21/11/8 | 419421 / 21000 / 0 / 116898 |
| 3 | saver | 27.6 | 24.9 | 5.79 | 17 | 118 | 0.94 | 0.0/38.0/50.9/50.2 | 42 | 23/15/5 | 568096 / 27000 / 1224 / 78821 |
| 4 | cheapest first | 36.2 | 27.5 | 5.78 | 44 | 113 | 0.78 | 0.0/22.6/33.0/97.7 | 99 | 25/13/9 | 457016 / 33750 / 14459 / 99249 |
| 4 | best payback first | 30.7 | 24.5 | 9.61 | 21 | 106 | 0.86 | 0.0/54.9/73.9/151.2 | 91 | 23/15/5 | 1237066 / 27000 / 102895 / 175488 |
| 4 | random affordable | 35.2 | 25.1 | 9.15 | 30 | 126 | 0.67 | 0.0/36.5/49.6/141.2 | 154 | 25/14/8 | 1087439 / 42000 / 15582 / 127401 |
| 4 | saver | 27.5 | 26.4 | 8.14 | 15 | 116 | 0.57 | 0.0/51.3/56.9/169.2 | 124 | 23/12/9 | 1063901 / 27000 / 66055 / 147526 |
| 5 | cheapest first | 36.3 | 24.2 | 6.69 | 28 | 102 | 0.95 | 0.0/29.5/73.6/54.4 | 119 | 23/16/5 | 517067 / 22500 / 43176 / 54538 |
| 5 | best payback first | 27.5 | 26.2 | 9.76 | 27 | 124 | 0.81 | 0.0/29.0/108.3/165.7 | 30 | 22/13/7 | 1711328 / 27000 / 94637 / 222408 |
| 5 | random affordable | 34.0 | 29.2 | 7.06 | 29 | 107 | 0.92 | 0.0/38.1/75.7/92.0 | 83 | 27/14/10 | 836957 / 53250 / 42862 / 78309 |
| 5 | saver | 26.6 | 27.4 | 7.81 | 22 | 95 | 0.80 | 0.0/55.5/111.3/142.6 | 29 | 26/16/7 | 1127767 / 36000 / 81583 / 69565 |
| 6 | cheapest first | 36.5 | 23.9 | 5.37 | 38 | 125 | 0.82 | 5.9/21.4/74.1/60.0 | 47 | 20/12/5 | 283141 / 12000 / 20655 / 82763 |
| 6 | best payback first | 29.5 | 24.4 | 7.67 | 16 | 107 | 0.81 | 0.0/33.7/118.0/58.8 | 45 | 20/10/8 | 894735 / 33750 / 1633 / 24111 |
| 6 | random affordable | 35.0 | 24.2 | 7.33 | 42 | 118 | 0.62 | 0.0/34.1/68.9/73.1 | 104 | 20/12/5 | 521529 / 27000 / 36510 / 141983 |
| 6 | saver | 27.5 | 28.6 | 5.46 | 22 | 113 | 0.75 | 2.5/42.4/50.3/88.8 | 37 | 25/14/9 | 544042 / 44250 / 47208 / 109967 |
| 7 | cheapest first | 34.6 | 31.3 | 5.85 | 42 | 136 | 0.77 | 0.0/33.8/40.6/62.7 | 73 | 26/13/12 | 576887 / 30750 / 49964 / 121743 |
| 7 | best payback first | 29.4 | 26.3 | 7.00 | 19 | 98 | 0.87 | 0.0/44.1/58.1/100.1 | 66 | 22/10/12 | 749369 / 27000 / 53012 / 181401 |
| 7 | random affordable | 32.6 | 31.3 | 5.56 | 41 | 165 | 0.67 | 0.0/23.3/39.7/64.8 | 52 | 26/8/17 | 473789 / 27750 / 30649 / 189483 |
| 7 | saver | 26.2 | 25.4 | 6.05 | 17 | 104 | 0.65 | 0.0/33.7/18.9/67.7 | 51 | 23/15/5 | 640980 / 27000 / 6368 / 78976 |
| 8 | cheapest first | 37.4 | 30.7 | 5.12 | 50 | 112 | 0.89 | 0.0/18.9/57.0/66.7 | 117 | 23/14/9 | 469976 / 20250 / 27567 / 78589 |
| 8 | best payback first | 28.3 | 31.1 | 7.64 | 20 | 99 | 0.42 | 0.0/35.3/97.3/99.4 | 32 | 24/10/13 | 1265896 / 38250 / 94303 / 264249 |
| 8 | random affordable | 34.9 | 27.9 | 6.34 | 33 | 99 | 0.58 | 0.0/25.4/73.2/64.5 | 59 | 23/7/13 | 464812 / 40500 / 29931 / 195096 |
| 8 | saver | 27.5 | 27.2 | 7.08 | 17 | 120 | 0.34 | 0.0/33.9/67.2/73.4 | 67 | 21/8/12 | 548027 / 41250 / 184762 / 255804 |
| 9 | cheapest first | 37.0 | 31.5 | 3.30 | 48 | 98 | 0.60 | 1.2/5.7/51.5/52.8 | 101 | 27/4/21 | 168808 / 15000 / 6618 / 66134 |
| 9 | best payback first | 28.6 | 29.3 | 4.82 | 32 | 141 | 0.63 | 0.6/10.5/44.5/43.6 | 33 | 23/8/13 | 447968 / 49500 / 13720 / 70507 |
| 9 | random affordable | 36.7 | 27.0 | 3.63 | 45 | 120 | 0.65 | 3.9/0.9/19.6/20.2 | 9 | 20/3/16 | 134866 / 9000 / 4788 / 95573 |
| 9 | saver | 26.4 | 29.5 | 3.90 | 31 | 133 | 0.55 | 0.2/11.5/26.6/38.1 | 82 | 23/5/15 | 311628 / 27000 / 1638 / 81509 |
| 10 | cheapest first | 36.4 | 23.8 | 6.63 | 29 | 95 | 0.93 | 0.0/23.2/55.3/87.1 | 57 | 23/14/6 | 455942 / 45000 / 40775 / 44570 |
| 10 | best payback first | 29.7 | 33.0 | 7.55 | 24 | 122 | 0.83 | 0.0/34.7/91.2/128.6 | 90 | 29/13/14 | 1313761 / 69750 / 288412 / 123860 |
| 10 | random affordable | 35.3 | 22.5 | 7.37 | 35 | 122 | 0.85 | 0.0/16.9/36.5/73.6 | 60 | 19/13/4 | 504536 / 18750 / 17449 / 89901 |
| 10 | saver | 27.1 | 33.8 | 7.04 | 22 | 130 | 0.95 | 8.5/27.7/88.4/124.8 | 7 | 29/10/18 | 1325082 / 27000 / 228243 / 214513 |

Seeds 11 to 20 (not tests), 7 misses of 40 runs:

```
| 11 | cheapest first | 36.5 | 29.7 | 5.97 | 65 | 170 | 0.75 | 8.9/19.0/71.7/88.0 | 30 | 27/16/9 | 573583 / 48750 / 35621 / 75508 | MISS: nothing affordable
| 14 | cheapest first | 37.5 | 22.0 | 4.85 | 38 | 88 | 0.33 | 0.0/11.3/39.7/39.9 | 71 | 20/8/11 | 183525 / 19500 / 6533 / 82485 | MISS: landing share
| 16 | cheapest first | 36.2 | 25.2 | 4.45 | 90 | 145 | 0.92 | 0.0/14.5/47.2/59.6 | 65 | 21/12/8 | 221281 / 15750 / 6198 / 68913 | MISS: nothing affordable
| 17 | cheapest first | 37.2 | 25.5 | 5.33 | 67 | 169 | 0.91 | 0.0/16.0/47.2/45.0 | 44 | 23/11/9 | 365770 / 18750 / 10218 / 46337 | MISS: nothing affordable
| 18 | cheapest first | 35.1 | 25.3 | 6.20 | 65 | 153 | 0.94 | 0.0/13.6/59.2/37.3 | 111 | 26/13/10 | 437514 / 27000 / 20660 / 66385 | MISS: nothing affordable
| 19 | random affordable | 33.1 | 27.5 | 5.92 | 77 | 119 | 0.86 | 11.6/24.5/56.3/64.9 | 35 | 23/11/9 | 554258 / 31500 / 366 / 41820 | MISS: nothing affordable
| 20 | random affordable | 35.0 | 22.3 | 6.51 | 38 | 170 | 0.31 | 4.2/23.9/33.9/76.4 | 97 | 20/7/12 | 349366 / 11250 / 0 / 187545 | MISS: landing share
misses: 7
```

So the second age's "nothing affordable" (60 s) and landing share (1/3) targets hold on seeds 1 to 10 by a small margin and miss on 7 of 40 runs of seeds 11 to 20: these two are not yet structural.

Not built: villagers living in the riverside and far-bank cottages (decision 6). It needs new river itineraries home, resting by the river and crossing safety in every state of the tide and the crossings, tested through the whole age; left out entirely rather than half done.

## Green pass (history: before Regulars; 26 November 2026)

History: these were the numbers before green pass 2. Every section further down is from older rules (v2 to v3 and the first second-age build) and is kept as history only; read none of its numbers as current.

### First age, seeds 1 to 10 (tool: town --seeds 1-10 --misses --v3)

Columns per bot (cheapest first, best payback first, random affordable, saver): charter minute; purchases-per-minute detail as the tool prints it; requests met/asked; boons; last gap / longest of the three before (minutes); hands bought.

```
1 | 33.7 ; 40 ; req 10/14 ; AAAA ; gap 4.5/3.2 ; hands 2 | 27.6 ; 26 ; req 7/12 ; BABB ; gap 2.2/3.9 ; hands 2 | 32.8 ; 31 ; req 8/14 ; BBAB ; gap 3.6/4.2 ; hands 2 | 27.1 ; 16 ; req 9/11 ; BAAA ; gap 3.5/3.1 ; hands 2
   2 | 35.3 ; 37 ; req 12/14 ; AAAA ; gap 3.3/4.5 ; hands 2 | 30.3 ; 25 ; req 5/13 ; BABB ; gap 3.1/3.3 ; hands 1 | 32.2 ; 38 ; req 8/13 ; BABA ; gap 3.4/3.2 ; hands 1 | 28.1 ; 19 ; req 10/11 ; BAAA ; gap 3.0/3.2 ; hands 2
   3 | 33.4 ; 32 ; req 11/13 ; AAAA ; gap 2.9/4.5 ; hands 2 | 30.0 ; 25 ; req 8/12 ; AAAA ; gap 3.8/2.9 ; hands 3 | 32.6 ; 28 ; req 8/13 ; AABA ; gap 2.7/4.5 ; hands 2 | 27.5 ; 16 ; req 5/11 ; AABA ; gap 3.4/3.7 ; hands 2
   4 | 32.8 ; 37 ; req 11/13 ; AAAA ; gap 3.2/3.8 ; hands 2 | 28.6 ; 25 ; req 7/11 ; AABB ; gap 3.1/3.0 ; hands 2 | 34.0 ; 32 ; req 10/13 ; BAAB ; gap 3.8/3.9 ; hands 2 | 26.8 ; 16 ; req 6/11 ; BABB ; gap 3.1/3.1 ; hands 2
   5 | 33.5 ; 36 ; req 10/13 ; AAAA ; gap 3.3/4.5 ; hands 3 | 28.5 ; 26 ; req 9/11 ; BAAA ; gap 3.5/3.7 ; hands 2 | 31.5 ; 31 ; req 8/12 ; BBAA ; gap 3.6/4.0 ; hands 1 | 26.6 ; 18 ; req 9/10 ; AAAA ; gap 3.2/3.1 ; hands 2
   6 | 33.4 ; 35 ; req 10/14 ; AAAA ; gap 3.5/3.7 ; hands 2 | 29.9 ; 26 ; req 7/12 ; BBAA ; gap 3.5/3.3 ; hands 1 | 31.4 ; 32 ; req 8/13 ; BABA ; gap 3.8/3.1 ; hands 2 | 29.1 ; 16 ; req 7/12 ; BBAA ; gap 3.5/3.9 ; hands 1
   7 | 33.5 ; 34 ; req 10/14 ; AAAA ; gap 3.9/4.7 ; hands 2 | 28.2 ; 26 ; req 8/12 ; BAAB ; gap 2.1/4.2 ; hands 4 | 30.1 ; 30 ; req 7/13 ; ABBB ; gap 3.3/3.2 ; hands 2 | 26.2 ; 16 ; req 6/12 ; BBAA ; gap 4.1/3.5 ; hands 2
   8 | 34.8 ; 37 ; req 11/14 ; AAAA ; gap 3.3/4.6 ; hands 2 | 29.4 ; 28 ; req 11/12 ; BAAB ; gap 2.7/3.6 ; hands 3 | 33.2 ; 34 ; req 9/14 ; BBAA ; gap 3.2/3.9 ; hands 1 | 27.9 ; 18 ; req 8/12 ; BAAA ; gap 3.4/3.7 ; hands 2
   9 | 33.9 ; 32 ; req 9/14 ; AAAA ; gap 4.1/4.1 ; hands 2 | 27.5 ; 25 ; req 6/11 ; BBBA ; gap 3.9/2.5 ; hands 1 | 32.5 ; 32 ; req 6/14 ; BABA ; gap 4.0/3.1 ; hands 1 | 26.2 ; 16 ; req 6/11 ; BBBA ; gap 2.8/3.5 ; hands 1
  10 | 34.5 ; 37 ; req 10/15 ; AAAA ; gap 3.5/4.6 ; hands 2 | 28.1 ; 25 ; req 8/12 ; BABB ; gap 3.7/2.6 ; hands 2 | 32.9 ; 34 ; req 11/14 ; BAAA ; gap 3.9/3.5 ; hands 2 | 27.7 ; 18 ; req 5/12 ; BABB ; gap 3.7/3.0 ; hands 2
  MISSED seed 8 best payback first: 1 purchases in minute 24 < 2
  MISSED seed 10 cheapest first: 1 purchases in minute 34 < 2
  MISSED seed 10 best payback first: 3 purchases in minute 8 < 4
misses: 3 (purchases 3, nothing affordable 0, age 0, map 0)
```

Still red: TownPacingTests.PurchasesKeepComing, 3 runs of 40 (seed 8 best payback: 1 purchase in minute 24, target 2; seed 10 cheapest: 1 purchase in minute 34, target 2; seed 10 best payback: 3 purchases in minute 8, target 4). Every other first-age target holds; charter 25.0 to 35.3 minutes for all four bots.

### Second age, seeds 1 to 10 (tool: town --river --seeds 1-10)

No miss on the tool's checks. Ranges over the 40 runs: age 22.6 to 32.3 minutes (the three buyers), 3.60 to 8.74 purchases a minute, longest spell with nothing affordable 9 to 60 s, longest map gap 76 to 170 s, landing share at the middle 0.34 to 0.95. Touches 16 to 17 at the ring and 33 to 43 at the seal.

| seed | bot | rung (min) | age (min) | buys/min | longest nothing affordable (s) | longest map gap (s) | landing share at mid | ford/ferry/foot/draw per min | longest crossing wait (s) | boats come/filled/short | coin landing / toll / river market / town |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | cheapest first | 33.7 | 29.4 | 6.00 | 40 | 156 | 0.77 | 0.0/24.5/66.1/57.8 | 107 | 26/12/12 | 655719 / 34500 / 18593 / 75525 |
| 1 | best payback first | 27.7 | 29.4 | 8.24 | 27 | 113 | 0.75 | 0.0/36.2/79.8/136.7 | 55 | 26/14/9 | 1115642 / 27000 / 193554 / 288956 |
| 1 | random affordable | 32.8 | 26.1 | 7.00 | 36 | 115 | 0.75 | 1.5/26.9/48.2/119.0 | 106 | 22/12/9 | 699487 / 21000 / 39149 / 135447 |
| 1 | saver | 27.1 | 28.7 | 5.90 | 22 | 156 | 0.75 | 0.0/36.1/60.2/101.1 | 110 | 26/16/7 | 654983 / 48000 / 100346 / 121702 |
| 2 | cheapest first | 35.3 | 26.1 | 6.60 | 44 | 123 | 0.82 | 0.0/33.8/74.6/103.6 | 116 | 25/13/9 | 632730 / 29250 / 11739 / 58272 |
| 2 | best payback first | 30.3 | 28.3 | 8.30 | 33 | 170 | 0.81 | 0.0/32.7/79.7/120.5 | 77 | 26/14/9 | 1069481 / 54000 / 140077 / 270182 |
| 2 | random affordable | 32.2 | 28.3 | 6.79 | 40 | 105 | 0.81 | 0.0/38.5/76.0/101.9 | 96 | 26/14/9 | 758189 / 42000 / 27045 / 90213 |
| 2 | saver | 28.2 | 25.9 | 5.60 | 25 | 94 | 0.74 | 0.0/37.9/91.3/109.2 | 85 | 23/13/9 | 601894 / 22500 / 24533 / 98090 |
| 3 | cheapest first | 33.4 | 26.7 | 4.77 | 33 | 115 | 0.94 | 0.0/25.0/49.3/65.3 | 40 | 21/10/8 | 266431 / 24750 / 16541 / 105758 |
| 3 | best payback first | 30.0 | 24.4 | 5.45 | 22 | 83 | 0.95 | 0.0/36.0/62.0/52.6 | 34 | 22/15/4 | 483852 / 27000 / 3845 / 56148 |
| 3 | random affordable | 32.6 | 24.3 | 4.94 | 38 | 119 | 0.93 | 0.0/24.2/44.1/61.7 | 31 | 20/6/11 | 229196 / 18000 / 8691 / 131877 |
| 3 | saver | 27.5 | 28.1 | 5.55 | 29 | 87 | 0.93 | 0.0/29.1/36.1/90.3 | 48 | 26/15/8 | 621901 / 44250 / 8076 / 69562 |
| 4 | cheapest first | 32.8 | 30.4 | 5.52 | 36 | 122 | 0.79 | 0.0/33.1/32.1/84.1 | 159 | 26/14/9 | 497788 / 50250 / 26799 / 121025 |
| 4 | best payback first | 28.6 | 24.5 | 8.74 | 29 | 119 | 0.74 | 0.0/35.9/54.3/120.5 | 76 | 23/12/8 | 934339 / 27000 / 46600 / 229713 |
| 4 | random affordable | 34.0 | 30.8 | 6.91 | 26 | 170 | 0.92 | 0.0/24.8/53.6/80.0 | 142 | 26/17/9 | 905473 / 44250 / 47637 / 168326 |
| 4 | saver | 26.8 | 31.7 | 7.97 | 21 | 126 | 0.74 | 0.0/45.4/76.6/124.0 | 84 | 29/14/12 | 1749763 / 81000 / 81528 / 249609 |
| 5 | cheapest first | 33.5 | 28.1 | 6.30 | 38 | 131 | 0.91 | 0.0/35.3/72.7/84.2 | 80 | 26/14/9 | 615882 / 43500 / 34332 / 60549 |
| 5 | best payback first | 28.6 | 30.1 | 7.60 | 27 | 154 | 0.89 | 0.0/36.6/99.2/108.0 | 62 | 26/18/7 | 1282122 / 54000 / 76375 / 44711 |
| 5 | random affordable | 31.5 | 29.1 | 7.12 | 32 | 153 | 0.87 | 0.0/36.6/84.4/54.8 | 73 | 27/18/7 | 910635 / 52500 / 27081 / 65907 |
| 5 | saver | 26.7 | 29.0 | 6.45 | 9 | 119 | 0.82 | 0.0/45.6/81.9/89.1 | 44 | 27/18/6 | 836962 / 63000 / 133135 / 99150 |
| 6 | cheapest first | 33.4 | 32.3 | 5.24 | 40 | 114 | 0.78 | 0.0/26.9/66.0/44.8 | 68 | 26/12/11 | 553869 / 44250 / 49929 / 52944 |
| 6 | best payback first | 29.9 | 23.1 | 6.70 | 16 | 114 | 0.74 | 0.0/26.6/100.8/102.3 | 38 | 19/13/4 | 607292 / 25500 / 2673 / 81859 |
| 6 | random affordable | 31.4 | 22.6 | 8.04 | 28 | 149 | 0.94 | 0.0/23.9/72.5/47.6 | 61 | 18/15/2 | 699822 / 18000 / 22189 / 63388 |
| 6 | saver | 29.1 | 24.6 | 5.29 | 12 | 76 | 0.77 | 0.0/31.6/108.2/48.7 | 28 | 20/13/4 | 498961 / 23250 / 745 / 58699 |
| 7 | cheapest first | 33.6 | 29.6 | 5.28 | 37 | 110 | 0.86 | 0.0/32.9/40.8/79.0 | 94 | 25/11/11 | 446340 / 33750 / 42623 / 92115 |
| 7 | best payback first | 28.3 | 26.3 | 7.57 | 30 | 86 | 0.91 | 0.0/45.1/67.2/100.6 | 67 | 22/12/10 | 820711 / 24000 / 67482 / 170088 |
| 7 | random affordable | 30.2 | 26.9 | 7.65 | 38 | 117 | 0.86 | 0.0/29.7/20.7/92.5 | 100 | 22/12/9 | 770786 / 30000 / 27746 / 258758 |
| 7 | saver | 26.2 | 30.9 | 5.73 | 33 | 112 | 0.62 | 0.0/28.7/47.9/90.4 | 44 | 25/12/12 | 768853 / 27000 / 30051 / 80898 |
| 8 | cheapest first | 34.8 | 29.0 | 5.83 | 37 | 170 | 0.87 | 0.0/19.8/70.1/84.4 | 84 | 23/12/8 | 538977 / 41250 / 22029 / 80526 |
| 8 | best payback first | 29.4 | 28.6 | 6.37 | 36 | 102 | 0.34 | 0.0/37.1/68.9/72.4 | 59 | 23/7/13 | 559210 / 64500 / 104729 / 204284 |
| 8 | random affordable | 33.2 | 28.2 | 5.90 | 38 | 131 | 0.89 | 0.0/22.8/62.5/90.1 | 78 | 23/15/5 | 537862 / 42750 / 30258 / 71812 |
| 8 | saver | 28.0 | 30.9 | 4.59 | 26 | 121 | 0.76 | 0.0/27.0/67.1/78.7 | 57 | 23/13/9 | 502630 / 47250 / 11998 / 97113 |
| 9 | cheapest first | 34.0 | 27.7 | 3.61 | 37 | 107 | 0.68 | 0.0/6.9/17.7/30.2 | 36 | 22/4/15 | 153543 / 15000 / 5925 / 91121 |
| 9 | best payback first | 27.5 | 30.2 | 4.14 | 60 | 117 | 0.81 | 4.2/19.6/17.4/53.1 | 155 | 23/7/13 | 334598 / 25500 / 2053 / 59008 |
| 9 | random affordable | 32.5 | 27.7 | 4.62 | 36 | 86 | 0.58 | 0.0/7.7/48.6/23.0 | 29 | 21/5/15 | 301054 / 21750 / 6351 / 79343 |
| 9 | saver | 26.2 | 29.2 | 3.60 | 41 | 79 | 0.63 | 0.0/16.1/25.9/28.4 | 55 | 23/9/11 | 297579 / 30750 / 578 / 78916 |
| 10 | cheapest first | 34.5 | 26.6 | 5.37 | 34 | 101 | 0.86 | 0.0/21.9/39.1/72.9 | 61 | 21/10/10 | 415200 / 19500 / 21303 / 52808 |
| 10 | best payback first | 28.1 | 27.9 | 7.60 | 24 | 125 | 0.85 | 0.0/28.2/59.7/157.3 | 77 | 27/13/11 | 915190 / 45000 / 100176 / 139660 |
| 10 | random affordable | 33.0 | 24.8 | 5.21 | 38 | 102 | 0.90 | 0.0/19.2/35.6/87.5 | 73 | 20/9/8 | 296591 / 19500 / 30036 / 75871 |
| 10 | saver | 27.7 | 30.6 | 7.10 | 10 | 100 | 0.88 | 0.0/14.6/67.6/124.7 | 74 | 26/13/13 | 947944 / 43500 / 201316 / 247880 |

Level times (minutes after the ring), levels 17 to 28:
- seed 1 cheapest first: 0.0 0.6 0.9 1.2 1.7 3.7 7.9 12.7 14.7 20.0 24.9 29.4 | capacity at the seal (ford/ferry/foot/draw a minute): 17/96/217/245 | touches at ring/seal/spots 17/41/60 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 20 RiverValue 39 Raft 3 Rope 4 Toll 7 Winch 5
- seed 1 best payback first: 0.0 0.5 1.0 1.3 1.7 3.1 8.6 11.9 13.8 19.2 24.7 29.4 | capacity at the seal (ford/ferry/foot/draw a minute): 24/137/308/349 | touches at ring/seal/spots 16/40/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 40 Raft 5 Rope 5 Toll 8 Winch 6
- seed 1 random affordable: 0.0 0.5 0.8 1.1 1.6 3.3 7.3 11.9 14.1 17.2 21.4 26.1 | capacity at the seal (ford/ferry/foot/draw a minute): 17/112/226/256 | touches at ring/seal/spots 17/41/60 | bought: Ferry 1 Berth 1 Crane 4 BoatPrices 20 RiverValue 40 Raft 4 Rope 4 Toll 7 Winch 5
- seed 1 saver: 0.0 0.6 0.9 1.4 2.0 4.6 8.0 9.7 13.5 16.9 22.4 28.7 | capacity at the seal (ford/ferry/foot/draw a minute): 22/137/284/322 | touches at ring/seal/spots 15/38/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 34 Raft 5 Rope 5 Toll 8 Winch 6
- seed 2 cheapest first: 0.0 0.5 0.9 1.3 1.7 3.2 7.8 12.3 15.7 19.9 22.7 26.1 | capacity at the seal (ford/ferry/foot/draw a minute): 17/96/217/245 | touches at ring/seal/spots 18/40/60 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 20 RiverValue 38 Raft 3 Rope 4 Toll 6 Winch 5
- seed 2 best payback first: 0.0 0.5 0.7 1.1 1.5 2.7 7.2 9.6 12.6 16.3 21.5 28.3 | capacity at the seal (ford/ferry/foot/draw a minute): 24/137/312/353 | touches at ring/seal/spots 17/41/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 40 Raft 5 Rope 5 Toll 8 Winch 6
- seed 2 random affordable: 0.0 0.5 0.8 1.2 1.6 3.1 6.9 11.4 14.8 19.7 22.0 28.3 | capacity at the seal (ford/ferry/foot/draw a minute): 21/96/271/307 | touches at ring/seal/spots 17/42/60 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 20 RiverValue 40 Raft 3 Rope 4 Toll 7 Winch 6
- seed 2 saver: 0.0 0.7 0.9 1.2 1.9 3.8 7.3 9.5 13.4 17.6 22.1 25.9 | capacity at the seal (ford/ferry/foot/draw a minute): 17/137/226/256 | touches at ring/seal/spots 16/37/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 37 Raft 5 Rope 5 Toll 8 Winch 6
- seed 3 cheapest first: 0.0 0.6 1.1 1.6 2.2 3.7 5.3 7.7 12.2 17.3 22.0 26.7 | capacity at the seal (ford/ferry/foot/draw a minute): 20/83/266/302 | touches at ring/seal/spots 17/37/60 | bought: Ferry 1 Berth 1 Crane 2 BoatPrices 20 RiverValue 32 Raft 2 Rope 3 Toll 5 Winch 3
- seed 3 best payback first: 0.0 0.5 1.0 1.7 2.4 3.5 5.4 7.8 11.2 13.5 19.8 24.4 | capacity at the seal (ford/ferry/foot/draw a minute): 20/149/266/302 | touches at ring/seal/spots 16/35/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 30 Raft 5 Rope 5 Toll 8 Winch 6
- seed 3 random affordable: 0.0 0.5 0.9 1.6 2.5 4.3 5.7 8.3 10.9 15.8 21.0 24.3 | capacity at the seal (ford/ferry/foot/draw a minute): 21/83/271/307 | touches at ring/seal/spots 17/36/60 | bought: Ferry 1 Berth 1 Crane 2 BoatPrices 20 RiverValue 31 Raft 2 Rope 3 Toll 4 Winch 4
- seed 3 saver: 0.0 0.5 1.2 1.6 2.7 3.7 5.6 9.2 14.5 18.6 23.1 28.1 | capacity at the seal (ford/ferry/foot/draw a minute): 21/149/271/307 | touches at ring/seal/spots 16/37/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 35 Raft 5 Rope 5 Toll 8 Winch 6
- seed 4 cheapest first: 0.0 0.5 0.8 1.2 1.7 3.1 5.5 9.0 13.3 16.3 21.3 30.4 | capacity at the seal (ford/ferry/foot/draw a minute): 17/96/217/245 | touches at ring/seal/spots 17/42/60 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 20 RiverValue 37 Raft 3 Rope 4 Toll 6 Winch 5
- seed 4 best payback first: 0.0 0.4 1.1 1.2 2.1 3.2 6.1 9.0 11.9 14.6 20.0 24.5 | capacity at the seal (ford/ferry/foot/draw a minute): 21/137/279/316 | touches at ring/seal/spots 16/37/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 40 Raft 5 Rope 5 Toll 8 Winch 6
- seed 4 random affordable: 0.0 0.5 0.7 1.1 1.6 2.7 6.3 9.8 12.4 13.8 22.1 30.8 | capacity at the seal (ford/ferry/foot/draw a minute): 22/137/288/326 | touches at ring/seal/spots 17/42/60 | bought: Ferry 1 Berth 1 Crane 4 BoatPrices 20 RiverValue 40 Raft 5 Rope 5 Toll 8 Winch 6
- seed 4 saver: 0.0 0.5 1.1 1.2 2.0 3.2 6.6 9.1 11.7 15.4 21.5 31.7 | capacity at the seal (ford/ferry/foot/draw a minute): 26/137/336/381 | touches at ring/seal/spots 16/42/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 40 Raft 5 Rope 5 Toll 8 Winch 6
- seed 5 cheapest first: 0.0 0.1 0.6 0.9 1.4 3.3 5.8 8.2 12.7 17.2 20.6 28.1 | capacity at the seal (ford/ferry/foot/draw a minute): 21/106/271/307 | touches at ring/seal/spots 17/40/60 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 20 RiverValue 38 Raft 3 Rope 4 Toll 7 Winch 5
- seed 5 best payback first: 0.0 0.5 0.8 1.3 2.1 3.9 5.8 7.4 10.4 14.5 21.9 30.1 | capacity at the seal (ford/ferry/foot/draw a minute): 28/149/369/418 | touches at ring/seal/spots 16/41/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 40 Raft 5 Rope 5 Toll 8 Winch 6
- seed 5 random affordable: 0.0 0.6 0.8 1.3 2.3 3.9 5.8 7.3 13.6 17.8 21.3 29.1 | capacity at the seal (ford/ferry/foot/draw a minute): 26/131/343/389 | touches at ring/seal/spots 17/41/60 | bought: Ferry 1 Berth 1 Crane 4 BoatPrices 20 RiverValue 39 Raft 4 Rope 5 Toll 7 Winch 5
- seed 5 saver: 0.0 0.7 0.8 1.4 2.3 3.9 5.0 6.6 10.1 15.2 21.1 29.0 | capacity at the seal (ford/ferry/foot/draw a minute): 27/149/353/400 | touches at ring/seal/spots 15/40/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 24 Raft 5 Rope 5 Toll 8 Winch 6
- seed 6 cheapest first: 0.0 0.6 1.0 1.5 2.3 3.8 6.0 6.9 12.2 20.1 24.0 32.3 | capacity at the seal (ford/ferry/foot/draw a minute): 21/106/271/307 | touches at ring/seal/spots 17/44/60 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 20 RiverValue 37 Raft 3 Rope 4 Toll 6 Winch 5
- seed 6 best payback first: 0.0 0.7 1.1 1.7 2.6 4.7 6.9 9.0 12.5 16.1 18.5 23.1 | capacity at the seal (ford/ferry/foot/draw a minute): 22/149/283/321 | touches at ring/seal/spots 16/36/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 39 Raft 5 Rope 5 Toll 8 Winch 6
- seed 6 random affordable: 0.0 0.2 0.7 1.4 2.2 3.4 5.2 6.9 8.4 13.4 19.2 22.6 | capacity at the seal (ford/ferry/foot/draw a minute): 25/142/330/373 | touches at ring/seal/spots 17/35/60 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 20 RiverValue 37 Raft 5 Rope 4 Toll 6 Winch 6
- seed 6 saver: 0.0 0.7 1.4 1.9 2.8 4.9 7.9 10.2 13.1 16.0 20.9 24.6 | capacity at the seal (ford/ferry/foot/draw a minute): 21/149/278/316 | touches at ring/seal/spots 16/36/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 34 Raft 5 Rope 5 Toll 8 Winch 6
- seed 7 cheapest first: 0.0 0.5 0.8 1.2 1.6 2.8 5.3 9.2 14.6 19.4 23.3 29.6 | capacity at the seal (ford/ferry/foot/draw a minute): 21/106/271/307 | touches at ring/seal/spots 17/39/60 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 20 RiverValue 37 Raft 3 Rope 4 Toll 6 Winch 5
- seed 7 best payback first: 0.0 0.5 0.8 1.2 1.6 2.6 4.8 7.6 12.2 17.0 22.2 26.3 | capacity at the seal (ford/ferry/foot/draw a minute): 27/149/355/402 | touches at ring/seal/spots 16/38/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 39 Raft 5 Rope 5 Toll 8 Winch 6
- seed 7 random affordable: 0.0 0.4 0.8 1.1 1.7 2.7 5.4 9.7 12.5 17.4 20.9 26.9 | capacity at the seal (ford/ferry/foot/draw a minute): 26/149/340/385 | touches at ring/seal/spots 17/38/60 | bought: Ferry 1 Berth 1 Crane 4 BoatPrices 20 RiverValue 40 Raft 5 Rope 5 Toll 8 Winch 6
- seed 7 saver: 0.0 0.6 1.0 1.2 2.0 4.0 5.9 8.1 12.6 17.8 24.9 30.9 | capacity at the seal (ford/ferry/foot/draw a minute): 27/149/355/402 | touches at ring/seal/spots 15/40/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 37 Raft 5 Rope 5 Toll 8 Winch 6
- seed 8 cheapest first: 0.0 0.6 1.0 1.5 2.2 3.7 6.9 9.4 12.7 16.6 22.9 29.0 | capacity at the seal (ford/ferry/foot/draw a minute): 17/96/217/245 | touches at ring/seal/spots 18/42/60 | bought: Ferry 1 Berth 1 Crane 3 BoatPrices 20 RiverValue 37 Raft 3 Rope 4 Toll 6 Winch 5
- seed 8 best payback first: 0.0 0.7 1.1 1.8 2.7 4.3 6.4 8.3 11.2 13.1 19.3 28.6 | capacity at the seal (ford/ferry/foot/draw a minute): 21/137/278/315 | touches at ring/seal/spots 16/41/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 39 Raft 5 Rope 5 Toll 8 Winch 6
- seed 8 random affordable: 0.0 0.7 1.0 1.5 2.2 3.4 6.4 8.7 10.4 14.8 21.2 28.2 | capacity at the seal (ford/ferry/foot/draw a minute): 17/128/226/256 | touches at ring/seal/spots 17/41/60 | bought: Ferry 1 Berth 1 Crane 2 BoatPrices 20 RiverValue 35 Raft 5 Rope 4 Toll 6 Winch 6
- seed 8 saver: 0.0 0.3 1.0 2.2 2.3 3.9 6.9 9.1 12.0 15.9 22.6 30.9 | capacity at the seal (ford/ferry/foot/draw a minute): 17/137/226/256 | touches at ring/seal/spots 15/40/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 33 Raft 5 Rope 5 Toll 8 Winch 6
- seed 9 cheapest first: 0.0 0.5 1.4 2.6 3.9 6.9 8.4 10.4 13.6 17.5 22.6 27.7 | capacity at the seal (ford/ferry/foot/draw a minute): 20/61/259/294 | touches at ring/seal/spots 17/39/60 | bought: Ferry 1 Berth 0 Crane 1 BoatPrices 19 RiverValue 25 Raft 1 Rope 2 Toll 4 Winch 2
- seed 9 best payback first: 0.0 1.1 2.2 3.6 4.5 8.5 11.9 15.5 18.3 20.9 25.3 30.2 | capacity at the seal (ford/ferry/foot/draw a minute): 21/89/278/316 | touches at ring/seal/spots 16/39/60 | bought: Ferry 1 Berth 1 Crane 5 BoatPrices 20 RiverValue 25 Raft 2 Rope 4 Toll 8 Winch 4
- seed 9 random affordable: 0.0 0.7 1.7 2.8 4.6 7.3 8.9 11.3 14.3 16.4 22.6 27.7 | capacity at the seal (ford/ferry/foot/draw a minute): 21/89/278/316 | touches at ring/seal/spots 17/39/60 | bought: Ferry 1 Berth 1 Crane 2 BoatPrices 20 RiverValue 30 Raft 2 Rope 4 Toll 4 Winch 3
- seed 9 saver: 0.0 0.8 2.1 3.1 4.5 7.3 9.8 12.3 16.0 18.6 23.8 29.2 | capacity at the seal (ford/ferry/foot/draw a minute): 21/112/278/316 | touches at ring/seal/spots 15/39/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 10 Raft 3 Rope 5 Toll 8 Winch 6
- seed 10 cheapest first: 0.0 0.5 0.8 1.2 1.9 3.6 6.4 9.8 13.8 19.0 21.6 26.6 | capacity at the seal (ford/ferry/foot/draw a minute): 20/83/266/302 | touches at ring/seal/spots 17/39/60 | bought: Ferry 1 Berth 1 Crane 2 BoatPrices 20 RiverValue 35 Raft 2 Rope 3 Toll 5 Winch 4
- seed 10 best payback first: 0.0 0.4 0.9 1.1 2.5 4.0 6.8 8.1 12.5 16.7 20.6 27.9 | capacity at the seal (ford/ferry/foot/draw a minute): 28/149/364/413 | touches at ring/seal/spots 16/39/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 40 Raft 5 Rope 5 Toll 8 Winch 6
- seed 10 random affordable: 0.0 0.1 0.5 0.8 1.5 3.0 5.1 8.4 13.1 17.4 21.3 24.8 | capacity at the seal (ford/ferry/foot/draw a minute): 21/83/278/316 | touches at ring/seal/spots 17/37/60 | bought: Ferry 1 Berth 1 Crane 2 BoatPrices 20 RiverValue 33 Raft 2 Rope 3 Toll 5 Winch 3
- seed 10 saver: 0.0 0.5 1.0 1.1 2.5 4.0 6.6 8.3 14.1 17.6 21.5 30.6 | capacity at the seal (ford/ferry/foot/draw a minute): 29/149/385/437 | touches at ring/seal/spots 16/43/60 | bought: Ferry 1 Berth 1 Crane 6 BoatPrices 20 RiverValue 40 Raft 5 Rope 5 Toll 8 Winch 6
misses: 0

## History (older rules)

 (first age, rules v3)

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

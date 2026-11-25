# Town balance, round 1 (first age)

Written 25 November 2026. Seeds 1, 2, 3, 7, 42, the three bots of `TownBot`, `town` command and a scratch harness (outside the repo) that reads the same simulation. Only numbers in `sim/Town/TownRules.cs` changed. Fast tests: 179 passed after the final numbers.

## What changed

| Number | Before | After | Why |
| --- | --- | --- | --- |
| `PaceDecay` | 0.98 | 0.97 | Pace steps are bigger (first level +4.5 % instead of +3 %), so a Pace click is felt and the cap of +150 % is reached after about 100 levels instead of 150. |
| `BellHurry` | 1.5 | 1.3 | Bell was worth +40 to 46 percent of coin earned in 20 minutes (target 10 to 25). Now +12 to 15 percent. |
| `BellRest` | 30 | 40 | Same reason; the bell is ready every 46 s instead of every 36 s. |
| `LevelRenown` | 0, 30, 80, 180, 350, 700, 1700, 3600, 7000, 12500, 22000, 35000 | 0, 20, 100, 390, 880, 1650, 2800, 4600, 7400, 11400, 17200, 25500 | Spreads the gifts. Before, six levels came in the first 9 minutes and then gaps grew to 7 minutes. Now gaps grow from 1 to about 5 minutes. |

Tried and put back: Pace price growth 1.045 to 1.1 and base 3 to 4 (fewer, dearer Pace levels). It cuts Pace to 65 to 75 percent of purchases and makes the age 30 minutes for every bot, but it breaks the "4 purchases a minute in minutes 1 to 10" and "2 a minute later" targets in `TownPacingTests` (3 in minute 9, 1 in the last minutes) and lifts the longest empty spell to 40 to 47 s. Cheaper Load and Mill prices only made the payback bot finish under 25 minutes (23 to 24.8). Neither was kept.

## Before and after (five seeds)

Charter minute, by seed (cheapest first / best payback / random affordable):

| seed | before | after |
| ---: | --- | --- |
| 1 | 37.4 / 26.9 / 35.5 | 34.8 / 26.5 / 32.9 |
| 2 | 38.3 / 27.4 / 36.1 | 35.7 / 27.5 / 33.8 |
| 3 | 37.5 / 26.7 / 35.5 | 34.5 / 26.6 / 33.4 |
| 7 | 38.9 / 26.1 / 35.0 | 34.2 / 26.2 / 32.3 |
| 42 | 38.3 / 27.6 / 41.6 | 36.3 / 26.6 / 34.6 |

Slowest bot over fastest: 1.4 to 1.5 before (seed 42 random: 1.5), 1.3 after. All runs are inside 25 to 40 minutes after (before: seed 42 random was 41.6).

Longest spell with nothing affordable (seconds): before 31 to 34 / 22 to 24 / 31 to 33, after 28 to 32 / 22 to 24 / 28 to 32. Under 60 in every run.

Mean level time in minutes (cheapest first), gap to the previous level in brackets:

| level | before | after |
| ---: | --- | --- |
| 2 | 1.3 (1.3) | 0.9 (0.9) |
| 3 | 2.5 (1.2) | 2.8 (1.9) |
| 4 | 3.8 (1.3) | 4.8 (2.0) |
| 5 | 4.7 (0.9) | 6.7 (1.9) |
| 6 | 6.2 (1.5) | 9.2 (2.5) |
| 7 | 8.9 (2.7) | 12.2 (3.0) |
| 8 | 12.6 (3.7) | 15.4 (3.2) |
| 9 | 17.8 (5.2) | 19.8 (4.4) |
| 10 | 23.9 (6.1) | 24.9 (5.1) |
| 11 | 30.5 (6.6) | 29.6 (4.7) |
| 12 | 37.4 (6.9) | 34.8 (5.2) |

Bell: coin earned in 20 minutes with the bell rung whenever ready against never (BellHurry 1): before +46 percent (cheapest), +41 (payback); after +13.5, +15, +13 (cheapest, payback, random). Target 10 to 25.

Purchases by track over five seeds, all bots alike: Pace 80 percent before and 81 after, Pave 11 to 12, Villager 6, Building 1 to 2, Load under 1.

Purchases per minute, seed 1 cheapest first, after: 10, 11, 10, 16, 17, 13, 12, 8, 7, 7 in minutes 1 to 10; 5 to 8 in minutes 11 to 20; 2 to 4 after. Every bot meets every target of `TownTargets` on all five seeds.

Crowding and queues: crowded roads (above comfort) in 1 to 2 percent of seconds, peak 1.4 to 2.0 times comfort; someone is queueing at a workplace 25 to 30 percent of the time, longest queue 4 to 6.

## What a player is doing

- Minute 1: first coin at 12 s and first purchase at 12 to 24 s. The player taps Pace and Villager, one purchase every 5 s. Level 2 (cottage, bell green) arrives at about 0.9 minutes. Fine.
- Minute 5: 5 to 7 purchases a minute, still mostly Pace taps. One real choice a minute (villager when a cottage has room, second bucket at 70, a pave click). Level 4 to 5 arrives.
- Minute 15: coin per minute is 600 to 1200 depending on the bot; 4 to 6 purchases a minute, nearly all Pace. Real choices: pave the suggested road (12 of them at 5 to 15 minutes), Basket, stall, second field. This is the slackest stretch for the cheapest and random bots, whose income sits at 300 to 700 coin a minute from minute 8 to 24.
- Minute 30: 2 to 4 purchases a minute, 90 percent Pace, plus the bell every 46 s. Gifts are 5 minutes apart. The player waits for the last levels.

## Decisions per minute for a human (estimate)

A decision is a purchase where a second option was worth considering, a pave click, a bell ring that mattered, or a choice to save. Pace taps do not count.

| minutes | purchases per minute | decisions per minute |
| --- | ---: | ---: |
| 0 to 10 | 7 to 12 | 1.5 to 2 (villager, bucket, stall, first paves, bell) |
| 10 to 20 | 4 to 6 | 1 (pave, field, Basket, villager) |
| 20 to 30 | 2 to 4 | 0.5 (bell, mill, Yoke) |

These are guesses from bot logs, not from a person.

## Where it is dull, and how long until nothing is left

1. Pace is 80 percent of everything bought. A bot cannot be told that, but a person will feel it: the age is a Pace tapper with 20 other things around it. About 170 Pace levels per run is too many; on the dearer curves I tried it was still 75 to 125.
2. Choices have one right answer. Pace is cheap and Villager, Basket, bucket, stall, field and every pave pay back faster than their price, so the only question is order. Cheapest-first and random finish within 6 percent of each other.
3. Yoke (1500) and Handcart (5000) are never bought by any bot (only the Basket, one per run), and the cheapest and random bots never buy the Mill (900); the payback bot buys all four buildings. The bots do not save, so Pace eats the coin first. A human who saves reaches them; nothing in the numbers tells a player they matter. They are the only real "save up" decisions in the age and they are 1 percent of purchases.
4. Income plateaus: cheapest first goes from 305 coin a minute at minute 7 to 760 at minute 24 on seed 1. Housing is the cap (cottages arrive with levels, 6 villagers from minute 7 to 12) and levels need renown, which here needs coin. So the player waits on levels while buying Pace.
5. Gifts: beats of 1 to 3 minutes until level 7 (about minute 12), then 3 to 5 minutes. Late gifts (levels 9 to 12) are too far apart for the design's "something new every 2 to 3 minutes". Number changes cannot fix it without breaking the age length: faster late gifts bring the payback bot under 25 minutes.
6. Crowding and queues rarely decide anything. Road crowding is on 1 to 2 percent of seconds. Queues exist (25 to 30 percent of the time) but the bots never respond to them.

How long until nothing is left: the charter comes at 26 to 27 minutes for the best-payback bot and 33 to 36 for the others (seed 42 cheapest first 36.3). A human who buys sensibly should land at 28 to 33 minutes. After the charter there is only Pace and the next-age button; before it, from minute 24 on the only items worth reading are Pace and the last pave or two.

## Proposals (rule changes, ranked by fun per hour of building)

1. Cap Pace at about 15 to 20 levels, each a clear step (+6 %), and make renown from Pace small. This removes the Pace tap-fest and forces coin into villagers, loads, buildings, paves. Cheap: one number plus a rule in the shop.
2. Give every track its own reason to wait: Load levels unlocked by level (Basket at level 3, Yoke at 6, Handcart at 9), so a player saves with a target on screen, and a "saving for" marker in the shop. Medium.
3. Make housing and workplaces the real limit: 2 villagers per cottage and 1 to 2 places per workplace already queue 25 to 30 percent of the time; add a visible "queue at the well: buy the second bucket" hint and make the second field and stall change what villagers do. Small.
4. Make crowding matter: lower comfort per cell on tracks (0.22 to about 0.15) so unpaved roads clog with 10 villagers and the pave click is a real fix. Small, but it is a number change, so it would belong in round 2 once the game shows crowding.
5. Rework the late levels: split levels 9 to 12 into smaller ones (add levels 13 and 14 as gifts, or give a mid-level perk or achievement every 2 to 3 minutes), so the last 10 minutes have a beat. Medium.

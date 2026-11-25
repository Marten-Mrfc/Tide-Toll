# Town balance, round 2 (first age, rules v2.2)

Written 25 November 2026, 22:35 to 22:45 UTC. Seeds 1, 2, 3, 7, 42, all four bots (cheapest first, best payback first, random affordable, saver). One number changed. Fast suite after the change: 203 passed, town tests 51 passed.

## What changed

| Number | Before | After | Why |
| --- | --- | --- | --- |
| `LevelRenown[14]` (level 15) | 62000 | 66000 | The last gap was 4.2 to 4.6 minutes (level 15 to the charter) against 2.4 to 2.6 before it. Now the last two gaps are 3.2 and 3.7. The charter minute does not move (level 16 still needs 102000), so the saver margin is untouched. |

Tried and put back: 72000. It moved the long gap one level earlier (4.0 minutes from level 14 to 15) and the charter was unchanged. 66000 is the flattest.

Tool change (tools project only): `town --choices` prints, per ten-minute window, how many kinds of purchase were within reach, and the share of seconds with a crowded road and with a queue.

## Before and after (five seeds, mean level minute, cheapest first)

| level | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| before | 10.6 | 12.6 | 14.4 | 16.5 | 18.9 | 21.3 | 23.9 | 26.6 | 30.8 |
| after | 10.6 | 12.6 | 14.4 | 16.5 | 18.9 | 21.3 | 23.9 | 27.1 | 30.8 |

Gap to the previous level, last three (minutes): before 2.6, 2.6, 4.2 (payback 2.7, 2.3, 4.3; random 2.7, 2.5, 4.5; saver 2.0, 2.6, 4.6). After 2.6, 3.2, 3.7 (payback 2.7, 2.8, 3.8; random 2.7, 3.1, 3.7; saver 2.0, 3.1, 3.9).
Longest gap with nothing new on the map: worst bot over five seeds 4.1 minutes before, 2.9 after (cheapest first 4.1 to 2.1).

Charter minutes, after (seeds 1, 2, 3, 7, 42): cheapest 30.4 32.0 30.5 30.6 30.3; payback 27.0 29.7 28.5 27.0 26.7; random 30.9 32.4 31.1 30.9 29.0; saver 27.1 27.1 25.9 26.0 26.5. All inside 25 to 40. Slowest over fastest bot on a seed: 1.12 to 1.25. Longest spell with nothing affordable: 33 to 48 s for the three spenders, 19 to 27 s for the saver (under the 60 s target).

## Purchases by kind before the charter (mean over five seeds, after)

| bot | Villager | Pace | Load | Pave | Building | Tools | Value | Extend | Bell track | total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| cheapest first | 26 | 25 | 2.8 | 22.4 | 4 | 17.2 | 41.8 | 14 | 5 | 158 |
| best payback first | 27 | 25 | 3 | 22.4 | 4 | 16.6 | 41 | 14 | 5 | 158 |
| random affordable | 26 | 25 | 2.8 | 23.2 | 4 | 17.2 | 42 | 14 | 5 | 159 |
| saver | 27 | 25 | 3 | 22.8 | 4 | 17 | 41 | 14 | 5 | 159 |

## Judgement against the first reports

- Is one track most of what is bought? No. Largest is Value at 26 percent (all three goods together), Pace 16 percent (was 80). Every bot ends with the same mix because every track is finite and is bought out by the charter. That is also the weakness: the mix says nothing about the player.
- Does every purchase change something visible or countable? Mostly. Villager (a walker), Extend (a cottage gains a person), Load, buildings, Pave (a road changes surface), the bell track (a number on the bell) can be seen or counted. Pace (3 percent a level), Tools (3 percent), Value (4 percent) and the bell steps are 100 of the 158 purchases and each one is a small number change. A player feels them only in the coin per minute figure. Not fixable by numbers alone without shortening the age.
- Real choices? In all four bots and all windows 3.4 to 4.7 kinds are within reach (coin in hand plus 30 s of income) at the same moment, and two or more kinds in 81 to 100 percent of samples (minutes 0 to 10: 3.7 to 4.0 kinds; 10 to 20: 3.7 to 4.7; 20 to 30: 3.4 to 3.6; after 30: 2.6). The count is of kinds, not of kinds worth weighing: many are the same cheap price class and the right answer is "all of them, soonest first". Payback and saver finish 3 to 5 minutes before cheapest and random, so order matters a little (12 to 25 percent), but no purchase excludes another.
- Do the dear things get bought? Yes. Yoke, Handcart, second field and mill are bought by every bot before the charter (Load 2.8 of 3 for cheapest and random: on seed 7 the Handcart comes after the charter). They are bought because the shop empties, not because anyone chose them.
- Crowding and queues? Crowding now matters early: a road is above comfort in 37 to 44 percent of seconds in minutes 0 to 10, 12 to 21 percent in minutes 10 to 20, 1 to 3 percent after (paving and Extend fix it). Never paving was 46 to 64 percent slower to the charter on seed 1 in the v2.1 table of `town-pacing.md` (target at least 15 percent); not re-measured here. A queue exists at some workplace in 8 percent of seconds in minutes 0 to 10, 63 to 78 percent in 10 to 20, 98 to 100 percent after minute 20, so late a queue is a constant, not an event, and it decides nothing the player can act on except Tools and places.
- Beat of gifts: levels arrive every 1.1 to 2.0 minutes until level 11, 2.0 to 2.7 for levels 12 to 15, then 3.7 to 3.9 for the charter. Steady until the last stretch; the last gap is still the longest.
- Requests: the bots meet 43 to 91 percent per run (6 of 14 to 10 of 11). A bot answers because the good is already what the town makes; the request does not change what it buys. For a person the 1.5 times price while a request runs says "send more of this", but only when Value or the good's workplace is already open. Little evidence they steer play; they do give a short goal each 70 s from level 3.
- Waiting: no stretch over 48 s where nothing is affordable for the spenders. The player never has more than about 50 s of pure waiting while the bell rests (40 s) and a request runs. After the last purchase is available, the wait for the charter is real (see below).
- After the charter: nothing. Pace 25 of 25, Tools 18, Value 14, Extend 14, Bell 5, Load 3 and the buildings are bought out before the charter; the shop is empty except Pave of the last roads.

## Decisions per minute for a human (estimate)

A decision means choosing between two or more things within reach that are not just "the cheapest next step", plus a pave click or a request that changes what is made.

| minutes | purchases per minute (bots) | decisions per minute | what the decisions are |
| --- | ---: | ---: | --- |
| 0 to 10 | 5 to 8 | 2 to 3 | villager or Pace, second bucket vs stall, first paves (crowding is real here), Basket, which good to raise, bell timing |
| 10 to 20 | 4 to 8 | 1.5 to 2 | pave vs extend vs value step, second field, Tools, requests, bell track |
| 20 to 30 | 3 to 5 | 1 | mill, Yoke, Handcart, Flour price, saving for dear steps; the rest is clicking the next step |

These are estimates from bot logs and the counts above, not from a person. A human buys slower than a bot (about half as many purchases a minute), so decisions per minute are lower and the choices are fewer per minute but each is more real.

## How long until nothing is left

Charter at 26 to 27 minutes for payback and saver, 30 to 32 for cheapest and random. A human who buys sensibly: 28 to 33 minutes. After the charter nothing is left to do; from about 3 minutes before it, the shop holds the last steps and the player waits for them to become affordable (1 to 3 purchases a minute, coin up to 5000 unspent).

## Further rule changes, ranked by fun per hour of building

1. Make some purchases exclusive: a choice of two (for example cobbles on the mill road or a second field track, or a Water price path against a Grain price path, only one tops out). The mix becomes the player's. Small to medium: a flag per pair and shop text.
2. Make the last 5 minutes an event: after level 15 a "charter drive" request that asks for a big amount of Flour in 3 minutes (reward = the charter). Replaces the wait. Small.
3. Make step sizes show: group Pace, Tools, Value into "levels of 5" with a visible change at each fifth step (a perk-like name and a note), and show "+x coin/min" in the shop line. Small.
4. Queue decisions: a visible queue at a workplace gives a "Hire a second hand here" offer (more places for coin), so a long queue asks for a purchase. Small to medium.
5. A second goal after the charter: let the age continue with a "fine tune" track (a few expensive steps) so the post-charter minutes are not empty. Medium.

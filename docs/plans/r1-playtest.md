# Round 1 playtest (real input, whole first age)

Played 25 November 2026, 21:36 to about 22:08 UTC, in `/home/user/tide-toll/build/pt/r1`. Files: `game/tests/PlayThrough.cs`, `game/tests/playthrough.tscn`. Raw logs: `/tmp/pt1/s1.log` (seed 1), `/tmp/pt1/s7.log` (seed 7), `/tmp/pt1/s1-v1.log` and `s1-v2.log` (earlier versions of the bot, see section 3), screenshots in `/tmp/pt1/shots/`.

## The player and its rules

Every action is a real input event: a click on a bookmark, a number key (every third purchase goes by key: 1, 2, 3, 5), P and then a click on a road, the bell by Space or by a click (alternating). Nothing calls the simulation's buy methods. It looks at the interface about once per game second (the game is paused while it clicks, so a click never lands late).

Buying rule: at each look, buy the first affordable item in this order: building offer, villager, load, pace. Repeat (re-reading the shop) up to 6 times per look. Pave when the Pave bookmark says "Pave a road" and the price is affordable (press P, wait for the view to stop easing, move the pointer onto the lit road until the price tip names the suggested road, click). If the road is off screen it presses Minus to zoom out (up to 4 times). Rings the bell whenever it is ready. After a failed pave it waits 20 s before trying again. It never saves up for a dear item: it spends on Pace whenever Pace is affordable (a sensible but not careful player).

Run: headless, speed 24, window 1920 x 1080, seed 1 and seed 7, until 4 game minutes after the charter, then N pressed. Both runs ended with the pass marker and no error or warning line.

## 1. Purchases and decisions per minute

A purchase is one successful buy click or key (pave clicks that paved are included). Decisions: "two kinds" = a look where two or more affordable things of different kinds existed (counted at each buy it led to), "pave" = a pave click that paved, "bell" = a bell ring.

| Seed, minutes | Purchases | per min | Decisions | per min | two kinds | pave | bell |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1, 0 to 10 | 119 | 11.9 | 47 | 4.7 | 16 | 4 | 27 |
| 1, 10 to 20 | 47 | 4.7 | 43 | 4.3 | 4 | 9 | 30 |
| 1, 20 to 30 | 36 | 3.6 | 38 | 3.8 | 2 | 7 | 29 |
| 7, 0 to 10 | 119 | 11.9 | 50 | 5.0 | 19 | 4 | 27 |
| 7, 10 to 20 | 44 | 4.4 | 37 | 3.7 | 2 | 5 | 30 |
| 7, 20 to 30 | 36 | 3.6 | 39 | 3.9 | 2 | 8 | 29 |

Read this honestly:
- The purchase counts are inflated by Pace. Over the whole age seed 1 bought 180 Pace, 13 Villager, 1 Basket (Load), 24 Pave, 4 buildings (second bucket 531 s, market stall 833 s, second field 1523 s, mill 2187 s). Pace is 70 percent of all purchases. Minutes 0 to 3 are 14, 20, 23 purchases a minute, all of them the same Pace button at 2 to 5 coin.
- Real choice is rare. The bell is 27 to 30 of the "decisions" in each window (it is ready about every 36 s and the right move is always "ring it"). Without the bell, decisions are about 2 a minute at 10 to 20 and about 1 a minute at 20 to 30. After minute 10 a "two kinds" moment is almost always Pace plus Pave.
- Seeds 1 and 7 play almost identically (same level times within 20 s, same purchase mix).

## 2. Levels, idle time, charter, what is left

Level times, seed 1 (seed 7 in brackets), game seconds:

| Level | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 (charter) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Time | 42 (42) | 106 (105) | 198 (199) | 307 (301) | 457 (450) | 636 (628) | 819 (812) | 1122 (1105) | 1397 (1403) | 1682 (1699) | 2014 (2053) |

- Charter at 33.6 minutes (seed 1) and 34.2 minutes (seed 7). In the brief's 25 to 40 minute window.
- Gaps between levels grow from about 60 s to about 330 s (level 11 to 12). The slowest stretches are level 8 to 9 (303 s), 10 to 11 (285 s) and 11 to 12 (332 s).
- First coin about 12 s (first look that saw 4 coin was at 12.4 s and a Pace was bought then); first purchase 12.8 s. Inside the targets.
- Longest stretch with nothing affordable and the bell resting: 19 s (seed 1, at 2124 s) and 20 s (seed 7). The target of 60 s is met by a wide margin, but only because Pace is always cheap and the bell comes back every 36 s. Longest gap between two purchases: 37 s (seed 1, near the end), 38 s (seed 7).
- Purchases per minute never fall under the targets (4 a minute for the first 10 minutes: 11.9; 2 a minute after: 3.6 at worst in 20 to 30; 2.3 to 2.7 after minute 30).
- After the charter (240 s played, seed 1): the "Ring in the next age [N]" button appears. Pressing N works (no error; the town keeps running). Still on the shop: Villager 626 ("No room: wait for a cottage", villagers are capped at 14, no more cottages come), Pace level 180 at 977 coin, Yoke (second Load) 1,500 coin, Pave (gravel 35 coin on roads not yet paved). The mill (900 coin) was bought 174 s after the charter, because the bot kept spending on Pace. So what is left is: the mill if you saved for the wrong things, the Yoke, paving, and Pace without end. 22 of 24 achievements unlocked (I did not log which two are missing).
- Money is always spent at once on the cheapest track, so the dear items (Basket 300, Second field 400, Mill 900, Yoke 1,500) are bought only when Pace is briefly out of reach. A player who saves would see these as real goals; a player who taps Pace never meets them until late. The interface shows the "Need N more" line for them, which does help.

## 3. Interface that was unclear, and inputs that did not do what a player expects

Most serious first.

1. **Pave clicks often do the wrong thing or nothing.** With Pave mode on and the pointer on the lit suggested road, the price tip names that road, but the click then said "Already cobbled." (60 times in seed 1, 51 in seed 7) or "Cobbles come later." (19 and 21) and paved nothing. Over about 115 pave attempts per run, only 24 paved. A first version of the bot re-moved the pointer before pressing, and 2,266 clicks failed in one run (the tip named one road and the click acted on a neighbouring, already paved road within 1.5 cells). The final bot presses at the spot it hovered and it still fails about two times in three. The camera does not move during the click (checked: 0 to 1 px). I did not find the cause in the time box. Where roads share a corridor near a junction, the road that gets the click is not the one the tip names. A player would feel it as "I clicked the blue road and it said Already cobbled". Treat as a real bug until shown to be an artefact of injected events. The check in `PlayThrough.DoPave` logs each case with `diag:` lines.
2. **The suggested road is often off screen, and the Pave bookmark does not say where.** The bookmark reads "Pave [4 or P] 21 coin: Pave a road". The view follows the town as it grows and the first roads (the ones that wear in first) slide off the top edge (road middle at y = -200 px at 1920 x 1080). The "Busiest road" tag is anchored to the road so it is off screen as well. The bot hit this case 12 times in each run and had to press Minus to find the road.
3. **Zooming out to reach a road switches off the follow camera for good.** After Minus the log shows `Following False` from then on. The town then keeps growing off to the side (see the minute 5 screenshot, where the whole village sits in the bottom quarter of the screen). I did not find a visible control that brings the view back (Home toggles the book view, so I did not use it).
4. **Pave mode stays on after a successful pave** (23 of 24 times). The banner says "Esc or P to stop", so it is written down, but a player who just paved expects the mode to end, and a second click on a neighbour road then gives "Already cobbled." or "Cobbles come later." The bookmark text while on is "Pick a road. P to stop".
5. **The Pave bookmark looks the same ("Pave a road") whether the one road it points to can be clicked or is off screen**; it is enabled when coin is lacking too (it reads "Pave a road" at 21 coin with 7 coin in hand; the bot only pressed it when it could pay).
6. **Notes pile up.** "Perk: Light feet." appeared 35 times in a run (a perk every five Pace levels, and Pace levels come every few seconds). The note stack at the top left is five slips at once and the level gift note ("Level 2: A cottage, a lane and the bell green.") sits among them for the same 7 s as the others, so it is easy to miss. Level changes are shown in the ledger ("Next: ...", the bar), which is clear.
7. **The only thing a player sees for the first 40 s is one trail and a button that costs 2 coin.** The first minute is clear enough: "Pace, level 0 [2] 2 coin: Need 2 more" tells the next step. But a player cannot tell Pace is by far the best buy: 13 Pace in the first minute.
8. **Villager "No room: wait for a cottage"** is a clear message, but it holds from about 240 s to the end in long stretches (Villager is disabled for most of the age: 14 villagers in the whole age).
9. Keys 1, 2, 3, 5 all worked every time (about a third of all purchases, no failures). The bell by Space and by click both worked every time. Bookmark clicks never missed. The bell bookmark text ("Rests 18 s") and the "Need N more" texts are clear.
10. The bookmark list shifts: when a building offer arrives, the Pave bookmark is not at a fixed place (Pave is item 4, a new offer is item 5, the second offer item 6). Both offers are labelled `[5]`, so the key 5 buys only the first one, and a click is needed for the second (the log shows two "[5]" bookmarks at once: "Second bucket [5]" and "Market stall [5]").

## 4. Errors and warnings printed

- None in either play-through (both pass `tools/godot-test.sh`, which fails on any error or warning line apart from the allowed exit-time leak lines).
- Build: `warning CS0108: 'TownHud.Scale' hides inherited member 'CanvasLayer.Scale'` (game/scripts/town/TownHud.cs line 44), on every build.
- The checkout as handed over had no Godot import sidecars for the assets, so the first run failed with "No loader found for resource: res://assets/textures/paper-page.png" until I ran `godot --headless --path game --import` (5 s). `./check` does this itself, so it only bites someone who runs a scene by hand.
- `pkill` of my own background runs was refused by the sandbox, so two screenshot runs ran side by side for a while; no effect on results.

## 5. The look (1920 x 1080, xvfb, mobile renderer, seed 1)

Shots taken at game minute 1 and minute 5 only (`/tmp/pt1/shots/seed1-min1.png`, `seed1-min5.png`). The minute 15 and charter shots were not finished inside the time box: the rendered run is slow under software Vulkan (minute 1 took about 2 real minutes to reach, minute 5 about 15). A second run with `--shots=/tmp/pt1/shots2` was still going when I wrote this; if `/tmp/pt1/shots2/seed1-min15.png` and `seed1-charter.png` exist, they are from the same bot and nobody has read them. Command line (same as tools/check.sh with the playthrough scene): `xvfb-run -a -s '-screen 0 1920x1080x24' godot --path game --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 res://tests/playthrough.tscn -- --seed=1 --speed=8 --shots=DIR`.

What I read in the two images, most serious first:
1. **Villagers are tiny dark blobs** (about 10 to 15 px at 1920 x 1080 against a 60 px cottage). At minute 1 two blobs on the trail are all there is to watch. Worse than it sounds when the town is a single trail: the thing you are paying for is hard to see.
2. **At minute 5 the town sits in the bottom quarter of the screen** and three quarters of the view is empty meadow, because zooming out stopped the follow camera (see 3.3). Even without that, only 2 cottages, a well and 2 short roads are in view at level 4, so the screen mostly shows grass and trees.
3. **Gravel road is a white speckled slab** much wider and brighter than the trails; it reads as a pavement slab, not as a road, and clashes with the soft earth trails around it. (The brief noted cobbles are too wide; gravel is already wide.)
4. **The empty plot at minute 1 is a black rectangle outline** with four corner pegs; it reads as a game editor box, not a building site, and nothing says what it is for. The bell green is not visible.
5. The ledger text: "Renown 37 of 100" and "300" use a font with slashed zeros that looks like "100" with a stroke through it. Legible, but odd. The "+4" coin floaters are small and grey at minute 1 (hard to read on grass).
6. The bookmark panel on the right is clear and readable; disabled bookmarks are greyed but the text is still readable. The panel overlaps the river at minute 5 (harmless). "Upgrades" caption is very small.
7. Interface layout otherwise fine at 1920 x 1080: ledger top left, bookmarks top right, bell bottom left, no overlaps.

## 6. Honest opinion

- After 5 minutes: yes, a curious player keeps going, but mostly because new things keep arriving (levels 2 to 5 come every 1 to 2 minutes) and Pace is fun to tap; after 15 minutes: probably not, since the play is "wait, click Pace, ring the bell" with about 2 real choices a minute.
- The dullest stretch is minutes 10 to 30: a level every 3 to 5 minutes, purchases are Pace almost only, and the one real new verb (Pave) is the one that is hard to use (sections 3.1 to 3.3).
- The first 3 minutes work (a purchase every 3 seconds) but they teach one button; they do not teach saving up.
- Walking speed being most of what is bought is worse than it sounds: 70 percent of purchases, and the player never has to choose between tracks until minute 20.
- The one change that would help most: make Pace not the cheap default (a few dear steps like Load) AND fix paving so the click lands on the road the tip names and the camera shows the suggested road; then the rest of the shop (basket, villagers, buildings) becomes the thing you save for.

## Villagers too small, cobbled roads too wide, speed upgrade

All three are as bad as they sound, none is milder. Villagers: see look item 1. Cobbles: I saw gravel only in my shots (the cobbles gift arrives at level 8, 819 s, and the bot paved cobbles from 1100 s on); gravel is already wide. Walking speed: 180 Pace of 258 purchases (70 percent).

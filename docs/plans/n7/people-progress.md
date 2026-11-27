# Villagers as people, step 1: progress (the people builder, 27 November)

## What ships in step 1 (final, the lead's decision)

**The simulation plays exactly as the main branch does.** Every behaviour switch is off. The `people` output is identical to the "before" file for every seed and both bots in both ages, the play-through reads `charter=33.9min buys=201`, and `sim.tests` passes 271 of 271.

**In:**
- **People in the simulation:**
  - names (a pure function of seed and number, with a by-name when one repeats);
  - two counters no rule reads (`Trips`, `Earned`);
  - the views (`VillagerView`: `Dir`, `Stage`, `Slot`, `WorkLeft`, `Leaning`, `Trips`, `Earned`, `RiverHome`, `OnRiver`; `town.VillagerName`);
  - old saves (v2 and v3) load with the counters at 0.
- **Pointing at a villager:** the screen-space pick, the hover ring, the name tag after the tooltip's delay, the card docked under the ledger (the Person zone; it hugs its four lines). The card does not show a leaning while the leaning does nothing.
- **The bodies pass, in the drawing only:** keep right by direction, following distance, standing spots, a bounded push on a neighbour grid, 200 villagers drawn.

**Built and switched off**, each reachable by its constant and documented there, for the builder who retunes the ages:

| switch (as shipped) | what it does | herding, payback / cheapest bot, seeds 1-10 (first age; second age) | tests it tips (seed, bot, margin) |
| --- | --- | --- | --- |
| none on (as shipped, = main) | | 26 % / 17 %; 55 % / 46 % | none |
| `TownRules.MillWaitCountsOnTheWay` (false) | the mill's wait counts flour pickers still at or on the way to the field, first age | 22 % / 16 %; 51 % / 45 % (the second age only moves because the first age ends in another town) | `TheSecondAgeTakes20To35MinutesForTheBuyers` seed 6 random 19.6 min (0.4 short of 20); `BoatsPayTollsBeforeTheSeal` seed 6 random 3.4 min from drawbridge to seal (0.1 short of 3.5); `TheLandingEarnsAThirdOfAllCoinByTheMiddleOfTheAge` seed 8 saver 0.33 (under 0.01 short); `NeverMoreThan60SecondsWithNothingAffordable` seed 10 payback 61 s (1 s over) |
| plus `MillWaitCountsOnTheWayInSecondAge` (false) | the same in the second age | 22 % / 16 %; 52 % / 36 % | `BoatsPayTollsBeforeTheSeal` seed 2 payback 3.4 min (0.1 short); `NeverMoreThan60SecondsWithNothingAffordable` seed 10 payback 67 s (7 s over) |
| plus `SiteWaitCountsOnTheWay` (false) | the pasture's and the weaver's waits count those on their way there | 22 % / 16 %; 41 % / 32 % | `TheSecondAgeTakes20To35MinutesForTheBuyers` seed 8 random 19.4 min (0.6 short); `TollStepsPayBackBeforeTheSeal` seed 8 random spent 24599, earned 2250 |
| plus `TownRulesRiver.LevelMinutesScale` 1.0023 (1) | every second-age level costs that much more (restores both bots' mean seal minute) | as the row above | seed 8 random 19.4 min and seed 6 random 19.9 min (floor 20); seed 6 random 3.4 min from drawbridge to seal; seed 8 random toll steps |
| `TownRules.LeanBonus` 0.10 (0), with all three fixes | a villager's own kind of work counts 10 % more | 19 % / 16 %; 42 % / 29 % | `EveryBotFinishesTheAgeInTime` seed 7 saver charter 24.7 min (floor 25); `BoatsPayTollsBeforeTheSeal` seed 10 payback 3.4 min; the play-through `charter=35.6min buys=200` (1.7 min over, seed 1) |
| `LeanBonus` 0.10, the mill's fix only (both ages) | | 19 % / 16 %; 50 % / 37 % | `EveryBotFinishesTheAgeInTime` seed 7 saver 24.7 min; `BoatsPayTollsBeforeTheSeal` seed 7 cheapest 3.4 min |

What the retuner needs to know:
- Every change of behaviour moves which single runs sit on the second age's band edges (the random bot at the 20-minute floor, the drawbridge-to-seal minutes, a minute with nothing affordable).
- A second-age level costs minutes of the town's best coin a minute (`TownRiver.cs` `LevelRate`), so a town that earns less, or spreads more, seals as soon or sooner.
- Measure any setting with `people --seeds 1-10 --river [--millfix 0|1|2] [--sitefix 0|1] [--lean x]`; `people --replay save.json --minutes n` compares rule sets from one saved town.
- Tests switch the fixes per town (`Town.WaitFixesHere`), never globally.

Branch `wt/n7-people`. The spec is `people-build-sheet.md`. Commits: `4b91e0a` part 1 (the simulation, alone), `e6f57f0` parts 2 and 3, then fixes and this page.

## What was built, file by file

| File | What it holds |
| --- | --- |
| `sim/Town/TownPeople.cs` (new) | `Leaning` (None, Water, Grain, Flour, Wool, Cloth); `Town.LeaningOf(seed, id)` and `LeaningOfJob(job)`; the name table (80 plain given names written for the game, 12 by-names); `Town.VillagerNameOf(seed, id)` (a pure function: a by-name such as "Ada the younger" when an earlier villager has the same given name), `town.VillagerName(i)` (kept once made, so asking again allocates nothing); `Leaned` (a villager's own kind of work counts `LeanBonus` more). |
| `sim/Town/TownJobs.cs` | Blind spot 1: `ExpectedWait` at the mill also counts flour pickers still at the field or on their way to it (`Stage 0`, Flour or FlourBoat). `ChooseJob` sets the chooser's leaning; `Consider` weighs it. `Deliver` counts `Trips` and `Earned`. |
| `sim/Town/TownRiverJobs.cs` | Blind spot 2: `SiteWait` also counts villagers whose itinerary still has work at that site ahead of them (`BoundForSite`: the pasture for wool and cloth jobs, the weaver for cloth jobs). `RConsider` weighs the leaning. `DeliverLanding` and `DeliverMarket` count trips and coin. |
| `sim/Town/TownState.cs` | `VillagerState.Trips`, `Earned` (no rule reads them). |
| `sim/Town/TownRules.cs` | `LeanBonus = 0.10`. |
| `sim/Town/TownViews.cs` | `VillagerView` adds `Dir, Stage, Slot, WorkLeft, Leaning, Trips, Earned, RiverHome, OnRiver`. |
| `sim.tests/Town/TownPeopleTests.cs` (new), `sim.tests/Town/data/*.json` | 10 tests (below); a v2 and a v3 save written by the code **before** the change (seed 3, the payback bot). |
| `tools/TideToll.Tools/PeopleCommand.cs` (new), `Program.cs` (one line) | `people --seeds 1-10 --river [--lean x]`: charter, seal, purchases, coin a minute and the herding share per age, both bots. |
| `game/scripts/town/TownVillagers.Pick.cs` (new) | The pick: each figure is the segment from its drawn feet to its head on screen; the nearest within 14 px x scale wins, ties (2 px) go to the figure in front, a hovered figure is held until the pointer is 1.6 radii away or another is 5 px nearer. The hover ring and the selected ring (wheat annulus on the ground, fades 0.1 / 0.12 s). |
| `game/scripts/town/TownHud.People.cs` (new) | Hover (the system pointer, as the shop reads it), the name tag after the tooltip's 0.25 s (map tag style), click (a left release that did not drag) selects, the card in the new Person zone (name; "Lives by the well lane"; "A good hand with water"; what it is doing and carrying; trips and coin earned), the cross and a click on the empty map close it. Off in Pave and Extend, over the interface (`OverInterface`, `GuiGetHoveredControl`), while paused, with a page open and zoomed out. Text is made only when what it says changes. |
| `game/scripts/HudZones.cs` | `Person`: x 16, y 360 (under the ledger), the rail's width, 124 high. |
| `game/scripts/town/TownHud.cs`, `TownHud.Shop.cs` | Wiring: build, frame, input, `OverInterface` includes the card, a new town clears the selection. |
| `game/scripts/town/TownVillagers.cs` | Capacity 96 to 200; rings; the stand target's result kept (`_stood`); the bodies pass in place of `Spread` (the round 6 one only for the before pictures). |
| `game/scripts/town/TownVillagers.Spread.cs` | Rewritten: the bodies pass (part 3). |
| `game/scripts/town/TownVillagers.OldSpread.cs` (new) | Round 6's spread, kept only so the pictures can show before and after (`OldSpread`, never on in the game or a check). |
| `game/tests/PeopleTest.cs`, `people.tscn`, `tools/people-check.sh` (new) | The check step `game-people`. |
| `game/tests/PeopleShots.cs`, `people-shots.tscn`, `tools/people-shots.sh` (new) | The pictures. |
| `game/tests/LayoutRules.cs`, `LayoutTest.cs`, `LayoutFixture.cs` | game-layout knows the Person zone: the card is open in every state on every canvas, with the longest texts in the fixture. |
| `game/tests/PlayThrough.cs` | One line: `--lean=x` for measuring (the game's own value is untouched without it). |
| `tools/check.sh` | The step `game-people` (and its name in the no-Godot list). |
| `docs/plans/town-sim-api.md` | A short section on the new views. |
| `docs/plans/n7/people/` | The pacing tables before and after, the pictures. |

## The sheet, part by part

### Part 1, the simulation: done

1. The two blind spots: done (above). Measured, both bots, seeds 1 to 10, both ages: herding falls clearly (table).
2. The leaning: done, 10 percent, from seed and number (a hash; no random stream). Five kinds, about a fifth of the town each; in the first age two of the five (sheep, loom) have no work yet, so about 60 percent of the villagers lean toward a first-age job.
3. The name: done (pinned in a test: seed 1 gives Amice, Wenna, Ada, Joss).
4. Two counters: done; the sum of every villager's trips is the town's trips (tested in both ages).
5. The views: done.
6. The save: the counters are saved as fields; old saves (v2 and v3, kept as test data, written by the code before the change) load with them at nought, names and leanings derived; the version numbers stay 2 and 3 (the defaults are safe, as with the Regulars fields). Test: `OldSavesLoadWithCountersAtNought`.
7. Pacing: below.
8. No test in `sim.tests` pinned which job a villager takes (the code map's list was checked): none replaced.

### Part 2, pointing at a villager: done

Pick, ring, name tag, the card in its zone, the cross, a click elsewhere, off in the modes / over the interface / paused, the layout check taught the zone. Not done: the keys to step through villagers (V / Shift+V) and "Follow" (research ideas, not in the sheet); Esc still pauses with the card open (the owner's question 5 in `bodies-and-picking.md`).

### Part 3, bodies: done, in the research's order

- Lanes by direction: walkers keep right of the way they go, 0.16 / 0.22 / 0.30 cells on track / gravel / cobbles (times WalkerScale/2, clamped 0.8 to 1.25), along the curve's normal, eased.
- Following distance: per road and way, the walkers sorted by progress (insertion sort, no allocation); each is drawn at least 1.1 m x WalkerScale behind the one ahead as drawn, lagging at most 1.5 gaps, eased.
- Neighbour grid: 64 x 64 hashed buckets of max(personal radius, 0.5) cells; pushes and the measures look at 3 x 3 (5 x 5 for measures) buckets only.
- Standing spots: those standing about (idle at home or at a node, the river's waits; not in a line, not at work) take a spot on a sunflower spiral round where the simulation puts them, by rank among those at the same place.
- The bounded push for what is left: walkers only across their road (at most 0.08 cell, so it never flips a side), others at most 0.4 cell and never into a building (`OutOfBuildings` for everyone not walking a road). The line and the work spots are untouched.
- 200 villagers drawn (Capacity). Not measured: a 200-villager town's frame (no such town in a check; game-perf's end-of-age town has about 25 to 30).

## Pacing, before and after

Headless runner, `people --seeds 1-10 --river`, means (full tables in `docs/plans/n7/people/pacing-*.txt`). "Herd" is the share of 10-second samples (4 or more villagers) with more than three quarters of the villagers on one job.

| | payback before | payback after | cheapest before | cheapest after |
| --- | --- | --- | --- | --- |
| first age: charter (min) | 29.70 | 29.25 | 36.75 | 36.75 |
| first age: purchases | 185.8 | 184.8 | 205.0 | 204.9 |
| first age: herd | 26 % | 19 % | 17 % | 16 % |
| second age: seal (min after the ring) | 25.93 | 24.50 | 26.55 | 25.50 |
| second age: purchases | 209.0 | 197.7 | 150.1 | 147.7 |
| second age: coin a minute | 50,090 | 45,561 | 19,980 | 19,862 |
| second age: herd | 55 % | 42 % | 46 % | 29 % |

(The research's 24 and 63 percent were seeds 1 to 6 / 1 to 3, payback; on seeds 1 to 3 payback the second age's herding here goes 63 to 39 percent.)

Leaning tried (payback / cheapest, seeds 1 to 10, mean charter; herd first / second age, payback): 0: 29.30 / 36.71, 22 % / 41 %; 0.05: 29.54 / 36.79, 19 % / 42 %; 0.10: 29.25 / 36.75, 19 % / 42 %; 0.20: 30.01 / 36.91, 17 % / 37 %.

**The play-through by real input** (seed 1, game-playthrough): before `charter=33.9min buys=201`. After, **`charter=35.6min buys=200`**: purchases within ten, the charter 1.7 minutes later, outside the minute. Per the sheet I made the leaning smaller: 0.05 gives 35.2 / 200, 0.16 gives 35.1 / 200; only no leaning at all gives 33.9 / 201 (the blind-spot fix alone does not move this run). Other seeds of the same play-through, leaning 0 against 0.10: seed 2 37.0 / 200 against 37.5 / 203; seed 3 33.0 / 197 against 33.1 / 200; seed 4 35.5 / 201 against 34.6 / 201. So the leaning does not slow a human-like player in general (+0.5, +0.1, -0.9 minutes), and the bots are not slower; seed 1's run is one trajectory that happens to lose 1.7 minutes. I kept 0.10 and say plainly that the brief's rule ("within a minute") is missed on seed 1; the lead may prefer 0 (no leaning, the blind spots only) or accept. The new pinned figure is **`charter=35.6min buys=200`** (only notes print it: neither `PlayThrough.cs` nor `check.sh` asserts it).

### The second age reaching its seal sooner when the town is poorer

Confirmed and measured, not changed. The second age's level thresholds are set in minutes of the town's best income: each step is `LevelRate x (RenownAbove step)` with `LevelRate = max(LevelRateMin, the rate at the ring, S.MaxCoinPerMinute)` (`TownRiver.cs` lines 38 to 56). So the seal comes after about the same number of minutes whatever the town earns: before this change the cheapest bot earns 60 percent less a minute than the payback bot (19,980 against 50,090) and seals at the same minute (26.6 against 25.9); across the 20 runs the correlation between coin a minute and the seal minute is -0.02 (after: -0.25). A town whose best minute is high (a herd on the best boat job) raises every later threshold; spreading the town lowers its peak, so after this change the payback town earns 9 percent less in the second age and seals 1.4 minutes **sooner**. This is what tipped two band tests (below). A careless or spread-out town is not punished in the second age; with trades (step 2) a role setup that earns less would also shorten the age.

## Tests

Added (`sim.tests/Town/TownPeopleTests.cs`): names and leanings pure functions of seed and number (pinned names, every kind at least 25 of 200); a shared given name gets a by-name (200 names unique); the view gives name, leaning, counters, slot, stage, work left and Dir; the counters add up over both ages; a leaning counts its own kind of work 10 percent more (and the bonus is small); the mill's wait counts flour pickers at the field (checked against the formula at 50 moments); the pasture's and weaver's waits count those on the way (40 moments); herding in the second age under half the time (seeds 1 to 3); old v2 and v3 saves load with the counters at nought, play on, save and load exactly.

Replaced: none. No test was weakened.

**Failing, honestly** (fast suite: 266 of 268 pass):

- `TownPacingTests.EveryBotFinishesTheAgeInTime`: `seed 7 saver: charter at 24.7 min, outside 25-40` (the town is faster).
- `TownRiverPacingTests.BoatsPayTollsBeforeTheSeal`: `seed 10 best payback first: 3 tolls in 3.4 minutes` (floor 3.5 minutes from the drawbridge to the seal: the seal comes sooner, see the oddity).

Every leaning from 0 to 0.2 misses one to five of these band edges (each value moves different single runs across the edges; with the blind spots alone, leaning 0: `seed 8 random: 19.4 minutes` (floor 20) and a toll-step payback). 0.16 passes both pacing suites but then `TownRulesV3Tests.ThePaybackBotDoesNotAlwaysPickTheSameSide` fails (`choice 0: side A 1, side B 9 of 10`, from 0.12 up). I kept 0.10 (the sheet's value, the bots' charter and purchases closest to before) rather than fish for a value; the lead decides whether the bands or the leaning move.

## Check steps (result lines)

Run one by one with the commands of `tools/check.sh`, each heavy one under the lock (taken and given back per step), on the final code unless said:

- simulation tests (`dotnet test sim.tests -c Release --filter "Category!=Gate&Category!=Perf"`): **Failed: 2, Passed: 266** (the two band misses above; part 1's code, unchanged since).
- game-build: 0 errors.
- game-smoke: `SMOKE PASS renderer=mobile game_s=30.0 coin=4 trips=1 uploads=75`
- game-town: `TOWN PASS faint=0.40 maxstep=0.008 worn=0.95 paved=0 boonclick=Wider lanes extended=0 level=6 bought=48 Pace:11,Villager:6,Regulars:14,Pave: ...`
- game-perf: `PERF PASS`; town 28 villagers 1x mean 0.473 ms, 562 B/frame; 8x mean 0.966 ms, 751 B/frame; river 36 villagers 1x 0.573 ms / 1216 B, 8x 0.857 ms / 1664 B.
- game-playthrough: `PLAY PASS charter=35.6min buys=200 paves=24 extends=14 bells=80 clicks=207 keys=101`.
- game-river: `RIVER PASS ... checks=386 waiting=105 boats=856 cargo=496 ... level=28 villagers=35`.
- game-layout: `LAYOUT PASS ... states=36 canvases=8 checks=81624 failures=0 loud<=2 quiet<=3` (the card open in every state; the first run failed 208 times on the card's cross, 40 px wide in a 28 px place: fixed).
- game-motion: **FAILS**, `FAIL: banner: frame 221 allocated 29832 B (gate 8192)` at 60 fps (same bytes on every run; at 30 and 120 fps CPU-time failures under load). Not found: with the villagers' hover and pick switched off, and with round 6's spread in place of the bodies pass, it still allocates the same; the HUD's `_Input` override I had added did allocate per event and was removed. The other builders' last game-motion runs pass, so it is very likely mine or my branch's base; unresolved at the deadline.
- game-work: `WORK RATES PASS` (60: pairs=946 gap=1.98-2.31m, villagerbytes=0; 30 and 120 the same).
- game-save: `SAVE PASS autosave-clock=0.63/0.63s level 2 paved 1 villagers 4`
- game-people: `PEOPLE RATES PASS`; 60 fps: `villagers=21 picked=21/21 shared=0 passpairs=7938 sidefails=0 follow=0.77m/0.00s spot=99.00m/0.00s flicker=0 bytes=540 villagerbytes=0 pickbytes=0 pave=none r=14px`. Weak spots of the check: no two people stood about at one place during its minute (`spot=99`: idle is about 0 percent of the first age, so the standing-spot rule went unexercised), no road was pavable at the moment it tried Pave (`pave=none`: the Pave case was checked by `PersonTarget` only when the mode could open; Extend was checked), 21 villagers (no 200-villager town).

## Pictures (looked at full size)

`docs/plans/n7/people/` (JPEG; the PNGs in `build/n7/people/`), made by `tools/people-shots.sh` (xvfb, Mobile renderer, 1920 x 1080, seed 2 at level 13 with 21 villagers, the town held still):

- `people-hover.jpg`, `people-hover-close.jpg`: the wheat ring under Bram's feet and the name tag "Bram" above his head. Reads well; the ring is thin at the normal view and partly under the figure's own shadow.
- `people-card.jpg`, `people-card-close.jpg`: the card under the ledger ("Bram / Lives by the well lane / A good hand at the mill / Carrying grain to the market"), the selected ring on the road. In this picture the fourth line (trips and coin) is missing: the label was clipped to nothing by the coin sign's resize; fixed after the picture (`ClipText` off), and the next picture run (`people-card*.jpg` as kept here) shows all five lines: "32 trips · earned (coin) 796".
- `people-road-before*.jpg` / `people-road-after*.jpg`: the busiest road (to the mill), the same people held still. After, the walkers stand to the right of their way and a little more evenly spaced; the difference is modest, because at that moment everyone on that road walks the same way (no one to pass), so the picture shows following and keep-right more than passing. What I still see wrong: at the normal view figures are about 2.4 m wide on a 1 m road (the research's owner question 2), so a person on the right of a gravel road still overhangs its edge; and a figure drawn behind its simulated place (the follow lag) carries its load a little after the coin number appears at the door.

## Shared files touched

`tools/check.sh` (the `game-people` step and its name in the no-Godot list), `docs/plans/town-sim-api.md` (one section appended), `game/scripts/HudZones.cs` (the Person zone), `game/tests/LayoutRules.cs`, `LayoutTest.cs`, `LayoutFixture.cs` (the zone), `game/tests/PlayThrough.cs` (the `--lean=` argument), `tools/TideToll.Tools/Program.cs` (one line). Not touched: `Main.cs`, `AGENTS.md`, `WorkSites.cs`, `River*.cs`, `work/*`, the line and the poses.

## What step 2 (trades) needs from this

- `Leaning` and `LeaningOfJob` are the trade's natural keys (Water, Grain, Flour, then Wool, Cloth): a trade can reuse the enum (append Hand / boatman), and `Leaned` is where a trade's factor goes (a lean of 1.25 instead of 1.10, from a stored `Trade` instead of the hash).
- `_choosing` is set once per `ChooseJob`; a trade filter or a weight for the river jobs plugs into `Consider` / `RConsider` the same way.
- The card has room for a trade line (124 high now; a trade chooser needs about 32 more: the zone can grow to about 180 before it meets the bell on the smallest canvas, 1094 x 615 sheet units) and the pick gives the id to assign by person.
- `Trips` and `Earned` are per villager already; trade skill or "earned in trade" can count in the same places (`Deliver`, `DeliverLanding`, `DeliverMarket`).
- The second-age thresholds (above) must be dealt with before trades are tuned, or a poorer role setup is rewarded with a shorter age.
- The save: a stored `Trade` per villager is a field like the counters (missing: Hand).

## Follow-up (the lead's three points, 27 November afternoon)

### 2. The pacing: three settings measured

New switch `TownRules.SiteWaitCountsOnTheWay` (the second age's fix; the mill's fix stays on in every setting). `people --seeds 1-10 --river --lean x --sitefix 0|1`; herding is payback / cheapest. Tests: the suites that move (`TownPacingTests`, `TownRiverPacingTests`, `TownRulesV3Tests`, `TownPeopleTests`; the rest of `sim.tests` passed in every earlier run). The play-through is the first age only, so the second-age switch does not touch it.

| setting | `sim.tests` failing (name: message) | play-through (seed 1) | herd first age | herd second age |
| --- | --- | --- | --- | --- |
| (i) bonus 0, both fixes | `TheSecondAgeTakes20To35MinutesForTheBuyers`: seed 8 random affordable 19.4 minutes (floor 20, 0.6 short); `TollStepsPayBackBeforeTheSeal`: seed 8 random affordable spent 24599, earned 2250 (the same short run) | `charter=33.9min buys=201` | 22 % / 16 % | 41 % / 32 % |
| (ii) bonus 0, the mill's fix only | `BoatsPayTollsBeforeTheSeal`: seed 2 best payback 3 tolls in 3.4 minutes (floor 3.5); `NeverMoreThan60SecondsWithNothingAffordable`: seed 10 best payback 67 s from 5.1 min (7 s over) | `charter=33.9min buys=201` | 22 % / 16 % | 52 % / 36 % |
| (iii) bonus 0.10, the mill's fix only | `EveryBotFinishesTheAgeInTime`: seed 7 saver charter at 24.7 min (floor 25, 0.3 short); `BoatsPayTollsBeforeTheSeal`: seed 7 cheapest first 3 tolls in 3.4 minutes (floor 3.5) | `charter=35.6min buys=200` | 19 % / 16 % | 50 % / 37 % |
| before (for comparison) | none | `charter=33.9min buys=201` | 26 % / 17 % | 55 % / 46 % |

**None of the three passes every test**, so by the lead's rule nothing is chosen by me: I did not hunt for a value. Every miss is one run at a band's edge, and every one of them is in or because of the second age except (iii)'s saver charter. Even (ii) (bonus 0, only the mill's first-age fix) moves the second age's runs across two edges, because the mill's wait is also used by the second age's flour boat job. The branch is left at (i) (`LeanBonus = 0`, both fixes on): it keeps the play-through exactly as before and spreads the town most of the three; both its misses are one seed's random bot sealing in 19.4 minutes, the poorer-town-sooner effect. The lead decides between (i), (ii), (iii) or a band.

The card says a leaning only while it counts (`town.LeaningCounts`, the bonus above 0): with the bonus at 0 the card has four lines (name, home, doing, counts). Names and leanings are still made (the leaning waits for step 2's trades).

### 1. game-motion

See "Check steps (follow-up)" below for the result on the merged code. What was found before the merge: the allocation (29832 B in frame 221 of the banner's recording, the same on every run) stays with the villagers' hover and pick switched off and with round 6's spread in place of the bodies pass; a probe without the HUD's `_Input` override stopped earlier on a CPU gate (machine load), so it did not reach the banner. The bisect on my own commits (`50bb865`, `4b91e0a`, `6b2ca95`) was started twice and stopped both times while waiting more than 25 minutes for the heavy-run lock (another builder's chained picture runs held it from 13:27), so it is not done.

### 3. Pictures

The ring now lies just outside the soft shadow (radii 0.98 to 1.16, selected 0.96 to 1.22, in the wheat accent). New pictures of the final code: see below.

### Check steps (follow-up, on the merged code, setting (i))

`claude/prototype-1` merged once (`2c14096`); the one conflict was the name list at the end of `tools/check.sh` (every step and name kept, `game-people` added).

- simulation tests: **Failed 2, Passed 266**: `TheSecondAgeTakes20To35MinutesForTheBuyers` (seed 8 random 19.4 minutes) and `TollStepsPayBackBeforeTheSeal` (seed 8 random), setting (i)'s two.
- game-build: 0 errors. game-smoke: `SMOKE PASS renderer=mobile game_s=30.0 coin=4 trips=1 uploads=75`. game-town: `TOWN PASS faint=0.40 maxstep=0.008 worn=0.95 ...`. game-save: `SAVE PASS autosave-clock=0.63/0.63s level 2 paved 1 villagers 4`.
- game-perf: `PERF PASS`. game-playthrough: **`PLAY PASS charter=33.9min buys=201 paves=24 extends=14 bells=76 clicks=204 keys=102`**. game-river: `RIVER PASS ... checks=419 ... level=28 villagers=36`.
- game-motion: **the allocation gate passes now**: `MOTION PASS ... fps=60 transitions=13 alloc-max=5672B cpu-max=4.74ms`, and 30 fps `alloc-max=5672B cpu-max=5.06ms`; at 120 fps `FAIL: esc-during-return: frame 30 took 7.55 ms of CPU (gate 6)` (the processor-time gate, machine load). The banner's 29832 B is gone after the merge; I did not find it in my own code (see above), so I cannot name the cause: either the main branch fixed it, or something of mine that the merge changed. The bisect remains undone.
- game-work: `WORK RATES PASS` (60: pairs=622 gap=1.98-2.30m villagerbytes=2). game-work-places: `WORK PLACES PASS` at 60, 30 and 120 fps.
- game-layout: **FAIL**, 9 failures, all `S1-keycap u35 1280x720@80: row '...' key cap ... is not under its icon` (shop rows at one canvas in one state); nothing about the Person zone. Not looked into: the shop rows are not my code, and the main branch's shop changed in the merge; no other worktree has a game-layout log to compare.
- game-people: see the last lines of this page.
- game-ink: see the last lines of this page.
- game-people: `PEOPLE RATES PASS`; 60 fps `picked=21/21 shared=0 passpairs=9790 sidefails=0 follow=1.15m/0.00s spot=99.00m/0.00s flicker=0 bytes=613 villagerbytes=1 pickbytes=0`. Before that it failed once at 60 and 120 fps on the merged town ("the hover does not flicker between neighbours (1 times)"). Fixed in the hover: it does not go back within 0.4 s to the villager it has just left, and under a still pointer a figure that steps just out of reach keeps the hover for up to 0.4 s.
- game-ink: `ASSET AUDIT PASS play1080=46.0 play640=20.4 walker=1.10 ... villagers-group=0.357`.

### Pictures of the final code (looked at full size; `docs/plans/n7/people/*.jpg`)

- `people-hover.jpg`: Joss with the wider wheat ring just outside his shadow and the tag "Joss". It reads at the normal view now.
- `people-card*.jpg`: the card has four lines with the bonus at 0 (Joss / Lives by the well lane / On the way to the well / 160 trips · earned (coin) 1,921). The cross sits in the top right corner, inside the card. With four lines there is about 20 units of empty paper at the card's foot.
- `people-road-before*.jpg` and `people-road-after*.jpg`: a moment when two walkers pass each other on the lane west of the well. After, they hold opposite sides of the cobbles; the before picture has them nearer the middle. Still a modest difference at the normal view, where a figure is wider than the road.

The pictures were taken just before the last hover fix, which does not change what they show.

### Second follow-up (the lead's decision: setting (i), a retune of the second age)

**1. The retune, stopped by the rule.** The one constant is new: `TownRulesRiver.LevelMinutesScale`, a factor on every second-age level's minutes (`LevelRenown`), applied where the river plan is built. Old value: 1 (there was no factor).

Mean seal minutes after the ring, seeds 1 to 10:

| | payback | cheapest |
| --- | --- | --- |
| before the blind-spot fixes | 25.93 | 26.55 |
| after them, setting (i) | 25.64 | 26.72 |
| with the scale 1.0023 | 25.88 | 26.81 |

With setting (i) the second age barely moved. The 4 to 6 percent in the lead's note came from the leaning setting (0.10). Restoring both means together asks for (25.93 + 26.55) / (25.64 + 26.72) = 1.0023. With 1.0023 the whole `sim.tests` run still failed 3 tests (265 passed). All three misses are the random bot, which is not in the two means:
- `TheSecondAgeTakes20To35MinutesForTheBuyers`: seed 8 random affordable, 19.4 minutes (0.6 short of 20); seed 6 random affordable, 19.9 minutes (0.1 short).
- `BoatsPayTollsBeforeTheSeal`: seed 6 random affordable, 3 tolls in 3.4 minutes (0.1 short of 3.5).
- `TollStepsPayBackBeforeTheSeal`: seed 8 random affordable, spent 24599, earned 2250.

So the retune stopped there, as the rule says. The constant stays in the code at 1, with these numbers in its comment. The branch is setting (i) exactly, and its two misses are the ones in the table above (seed 8 random).

**2. The shop's key cap.** `ShopRow.PlaceKey` now places the cap from the icon's rectangle as placed. The cap's centre is the icon's centre, its top is never above the icon's foot (`max(row height - 20, icon bottom)`), and its width includes the label's own minimum. The failing case was vertical: the cap's top at y 89 sat above the icon's foot at 90 while the row blended between compact and tall (`S1-keycap` also checks "under"). The rule's tolerance is unchanged.

**3. The card's foot.** With the leaning line hidden the card is 105 units tall instead of 124; it stays in its zone, which is still 124. The zone rule now reads like the shop's: the zone's position and width, and the card's height up to the zone's. `game-people` checks the foot is 16 units of paper or less.

**Check steps on the final code** (`91fad22`, setting (i); heavy steps under the lock, taken and given back per step):

- simulation tests: **Failed 2, Passed 266**. `TheSecondAgeTakes20To35MinutesForTheBuyers`: seed 8 random affordable, 19.4 minutes. `TollStepsPayBackBeforeTheSeal`: seed 8 random affordable, spent 24599, earned 2250.
- game-build: 0 errors. game-town: `TOWN PASS faint=0.40 maxstep=0.008 worn=0.95 ...`. game-save: `SAVE PASS autosave-clock=0.63/0.63s level 2 paved 1 villagers 4`.
- game-layout, three runs:
  - Runs 1 and 2: `LAYOUT PASS ... failures=0`.
  - Run 3, and its rerun: no layout rule failed (36 states checked, no `LAYOUT FAIL` line). Both times it stopped on the shop's real-time switch check: `FAIL: the switch eases over the slide's time (switch to compact: 28 frames over 186 ms ...)` and then `... 187 ms ...`, where 200 ms is needed. That check times the shop's own 0.2 s slide by the wall clock (`Time.GetTicksMsec`, frames not fixed) and passed in runs 1 and 2 at 191 and 207 ms.
- game-people: `PEOPLE RATES PASS` (60 fps: `picked=21/21 shared=0 passpairs=9790 sidefails=0 follow=1.15m/0.00s flicker=0 bytes=613 villagerbytes=1 pickbytes=0`; 30 and 120 fps the same).
- game-playthrough: `PLAY PASS charter=33.9min buys=201 paves=24 extends=14 bells=76 clicks=204 keys=102`.
- game-perf: `PERF PASS`. game-river: `RIVER PASS ... checks=419 ... level=28 villagers=36`.
- game-motion: `MOTION RATES PASS` (60 fps: `alloc-max=5672B cpu-max=3.10ms`; 30: 2.66 ms; 120: 3.30 ms).
- game-work: `WORK RATES PASS` (60 fps: pairs=622, gap 1.98-2.30 m). game-work-places: `WORK PLACES PASS` at 60, 30 and 120 fps.
- game-ink: `ASSET AUDIT PASS play1080=46.0 play640=20.4 walker=1.10 ...`.

### Third follow-up: what ships, plainly

**On:**
- the mill's blind-spot fix, in the first age only (`TownRules.MillWaitCountsOnTheWay = true`);
- names, the two counters, the views, the save, the pick, the card, the bodies.

**Off:**
- the mill's fix in the second age (`MillWaitCountsOnTheWayInSecondAge = false`);
- the pasture and weaver fix (`SiteWaitCountsOnTheWay = false`);
- the leaning (`LeanBonus = 0`; the card does not show the leaning);
- the second-age level scale (`TownRulesRiver.LevelMinutesScale = 1`).

**Herding measured for exactly this** (`people --seeds 1-10 --river`), first age, payback / cheapest bot:
- before 26 % / 17 %, after 22 % / 16 %;
- charter 29.70 to 29.30 min (payback), 36.75 to 36.71 min (cheapest);
- purchases 185.8 to 185.9, 205.0 to 205.1.

**Is the second age as before?**
- **The second age's rules are.** With every switch off, the `people` figures are byte-identical to the "before" file, both ages, every seed and bot.
- A second-age town saved by the old code, played on for 10 minutes by the payback bot, gives the same save as with every switch off: SHA-256 `C2A8E835...AAB8285`. Pinned in `TheSecondAgePlaysAsBeforeFromASavedTown`. With the mill's fix on in the second age it differs.
- **The second-age figures of a whole run do not stay the same.** The first age now ends in a different town (another charter minute, other purchases), and the second age plays on from there. With the shipped switches, mean seal and coin a minute per bot:

  | | before | shipped |
  | --- | --- | --- |
  | payback: seal / coin a minute | 25.93 min / 50,090 | 25.99 min / 46,238 |
  | cheapest: seal / coin a minute | 26.55 min / 19,980 | 27.20 min / 20,798 |
  | herding, second age (payback / cheapest) | 55 % / 46 % | 51 % / 45 % |

**`sim.tests` with exactly this: 4 fail, 265 pass of 269** (one test is new). Per the rule, stopped here. All four are `TownRiverPacingTests`, each one run just past its band's edge:
- `TheSecondAgeTakes20To35MinutesForTheBuyers`: seed 6, random affordable bot, 19.6 minutes (floor 20; 0.4 short).
- `BoatsPayTollsBeforeTheSeal`: seed 6, random affordable bot, 2 tolls in 3.4 minutes (drawbridge-to-seal floor 3.5 minutes; 0.1 short).
- `TheLandingEarnsAThirdOfAllCoinByTheMiddleOfTheAge`: seed 8, saver bot, landing share 0.33 (floor 1/3; under 0.01 short).
- `NeverMoreThan60SecondsWithNothingAffordable`: seed 10, best payback first, 61 s with nothing affordable from minute 5.2 (limit 60; 1 s over).

Any change to the first age moves which single second-age runs sit on these edges.

**For the builder who retunes the second age** (everything measured here):
- Which tests tipped, on which seed and bot, and by how much, under every setting tried:
  - leaning 0.10 with both fixes: seed 7 saver charter 24.7 min, seed 10 payback 3.4 min from drawbridge to seal;
  - setting (i): seed 8 random 19.4 min, and the toll steps on the same run;
  - (ii): seed 2 payback 3.4 min, seed 10 payback 67 s with nothing affordable;
  - (iii): seed 7 saver 24.7 min, seed 7 cheapest 3.4 min;
  - (i) with the level scale 1.0023: seeds 6 and 8 random 19.9 and 19.4 min, seed 6 random 3.4 min;
  - as shipped: the four above.
- The levels cost minutes of the town's best coin a minute (`LevelRate`), so a town that earns less seals as soon. The random bot is the one most often at the 20-minute floor.
- The switches above turn the second age's spreading back on: second-age herding 55 % to 41 % (payback) with both fixes.
- `people --millfix 0|1|2 --sitefix 0|1 --lean x` measures any setting; `people --replay save --minutes n` compares rule sets from one saved town.

**The shop's slide check** (`game/tests/LayoutTest.Shop.cs`):
- Old measure: wall-clock milliseconds (`Time.GetTicksMsec`) from the frame before the switch to the last switching frame, plus the longest wall-clock gap between frames.
- New measure: the sum of the frames' own times (`GetProcessDeltaTime`, the clock the slide runs on) over the same frames, plus the longest frame time.
- Bound unchanged: the slide's 0.2 s, less up to one frame.
- Why: on a busy machine the wall clock read 186 to 187 ms for a slide that runs 0.2 s of frame time.

`claude/prototype-1` merged again (one commit of documents).

**Check steps as shipped** (heavy steps under the lock, taken and given back per step):

- simulation tests: **Failed 4, Passed 265** (the four listed above).
- game-build: 0 errors.
- game-smoke: `SMOKE PASS renderer=mobile game_s=30.0 coin=4 trips=1 uploads=74`.
- game-town: `TOWN PASS faint=0.40 maxstep=0.021 worn=0.95 ...`.
- game-save: `SAVE PASS autosave-clock=0.64/0.64s level 2 paved 1 villagers 4`.
- game-layout, three runs, all `LAYOUT PASS ... failures=0`, the slide check passing. Slide times (to compact / to tall): 194 / 199 ms, 196 / 196 ms, 192 / 196 ms of frame time.
- game-playthrough: `PLAY PASS charter=33.9min buys=201 paves=24 extends=14 bells=76 clicks=204 keys=102`.
- game-people: `PEOPLE RATES PASS`. At 60 fps: `picked=21/21 sidefails=0 follow=1.15m/0.00s flicker=0 villagerbytes=1 pickbytes=0`.
- game-perf: `PERF PASS`.
- game-river: `RIVER PASS ...`.
- game-motion:
  - First run: 60 fps `FAIL: choice: frame 820 took 7.81 ms of CPU (gate 6)` (the processor-time gate); 30 fps passed (`alloc-max=5672B cpu-max=1.52ms`); 120 fps passed (`cpu-max=4.36ms`).
  - Rerun under the lock: `MOTION RATES PASS`, at 60 / 30 / 120 fps `alloc-max=5672B`, `cpu-max` 1.88 / 2.02 / 2.06 ms.
- game-work: `WORK RATES PASS`.
- game-work-places: `WORK PLACES PASS` at 60, 30 and 120 fps.
- game-ink: `ASSET AUDIT PASS play1080=46.0 play640=20.4 walker=1.10 ...`.

During the third layout run the lock folder was removed by someone else while I held it. My give-back found it gone ("rmdir: failed to remove ... No such file or directory"). The run itself finished normally.

# Tide & Toll, the incremental town: design brief

Written by the lead on 25 November 2026 for the builders of the second overnight build. It replaces the desire-line game as the direction. Where this brief is silent, choose the simplest thing that fits it and write the choice down in your report.

## The game in one paragraph

A town that grows one path at a time. You start with one cottage, one villager and one faint trail to the well. Every finished trip earns coin. Coin buys upgrades. Every upgrade bought adds renown to the town's level bar, and each level hands you the next piece of town by itself: a cottage, a road, now and then a field or a building. Enough levels ring in the next age. It is our own game in the spirit of incremental traffic games: no maps, names, numbers or art are taken from any other game.

## The loop

- **Trips.** A villager leaves home, walks the roads to a source, works there for a moment, carries the load to where it is wanted, and walks home. On delivery the town earns `load x value of the good x bonuses`.
- **Jobs in the first age**, each opened by a building: water (well to home), grain (field to home, or to the market once it stands, at a better price), flour (field to mill to market, late in the age). A villager takes the best-paying job that has a free place; every workplace has a small number of places, so buildings matter.
- **Roads.** Villagers walk only on roads. Roads are given by the levels and are never drawn by the player. A new road is a faint worn trail and is slow. It wears in as villagers use it, visibly, until it is a firm track at full speed. A worn-in road can then be paved (gravel, later cobbles): faster and it carries more people.
- **Crowding.** A road carries only so many walkers comfortably; beyond that they slow down and bunch up. So more villagers is not always better: paving, a new road from the next level, or a second workplace relieves it. This is the pushback that makes choices matter.

## What the player buys

| Upgrade | What it does | Notes |
| --- | --- | --- |
| Villager | One more villager | Needs room: each cottage houses a fixed number |
| Pace | Everyone walks a little faster | Many small levels |
| Load | Everyone carries one more unit per trip | Few, dear levels (basket, yoke, handcart) |
| Pave | Paves ONE road: the player clicks a worn-in road on the map | Price by length; the busiest road is suggested |
| Buildings | Offered by levels: a second bucket at the well, a market stall, a second field, the mill | Bought, then placed by the game on its planned plot with its road |
| The bell | Ring it: everyone hurries for a few seconds, then it rests | The one thing to do with your hands while saving up |

Every fifth level of a track gives a small named perk. Costs rise geometrically.

## Levels, milestones, ages, achievements

- The level bar fills with **renown**, and renown comes from buying upgrades: every purchase adds some, dearer purchases add more. So coin buys upgrades, upgrades reach levels, and each level hands over the next piece of town. About 12 levels in the first age, about 30 minutes for a new player.
- Each level's gift comes from the town plan: level 1 is the start (cottage, well, one road, one villager); early levels add a cottage and its lane; then the field; the market; a shortcut road that closes a loop; more cottages; the second field as a purchase; the mill; the last level grants the charter and the button that rings in the next age.
- The next gift is always shown ("Next: a cottage and a lane").
- About 24 achievements in the first age (first coin, 100 trips, first paved road, every road paved, 10 villagers, ring the bell 10 times, each level, and so on).
- Age II, The River, reuses the river, boats and crossings assets: boats buy goods at a landing, crossings arrive as gifts. Only if time allows.

## Targets the numbers must meet (the balancing bot checks them)

- First coin within 25 seconds; first purchase within 45 seconds.
- At least 4 purchases a minute in the first 10 minutes and at least 2 a minute after that.
- Never more than 60 seconds in which nothing is affordable.
- The first age takes 25 to 40 minutes for a sensible player, and there is always a next thing to reach until the charter.
- Three bot players (cheapest first, best payback first, random) all finish the age; none is more than twice as fast as another.

Further targets for the balancer (from the research; not yet tests):

- No upgrade track is the best buy more than about 60 percent of the time (the best-payback bot reports each track's share).
- Ringing the bell whenever it is ready earns 10 to 25 percent more per minute than never ringing it.
- Adding villagers without paving lowers income per villager; paving the suggested road raises it.
- Something new appears on the map at least every 2 to 3 minutes (a gift, a building or a road).

If `docs/plans/incremental-research.md` exists, balancers and play-testers read it too: it lists pacing rules from other incremental games and ten recommendations for ours.

## The town plan

The terrain comes from the existing map generator (meadow and river, by seed). A planner lays out, from the seed alone, every plot and road for all levels of the first age on the west bank, with room kept by the river for the second age: roads with gentle curves, no road through a plot, later roads closing loops, everything reachable. Tests check the plan on 100 seeds. Seed 1 is the default town and must look good in screenshots.

## The book

- The player is normally zoomed in on the town. The view follows the town as it grows until the player moves the camera.
- Zooming all the way out reveals the page, then the whole book on the table, in one continuous zoom. The left page then shows the town's summary.
- While zoomed in, a small book-styled interface sits on screen: coin and coin per minute, the level bar with the next gift, the upgrade bookmarks with prices, the bell, and margin notes for events.
- Pausing (Esc) closes the book. The closed cover is the pause menu: Resume, Settings, New town, Quit.
- Settings are pages of the book: Sound (master, music, ambience, effects, interface), Display (window mode, resolution, interface size), Keys (rebinding), Saving (autosave, save now, load, delete).
- The game saves by itself and continues where it stopped.

## What stays and what goes

- Stays: the engine set-up, `./check`, the Windows build on GitHub, the Living Map art, every asset, the ground shader (roads are painted into its wear layer), the book, the camera, the ink outline, the sounds.
- New simulation code lives in `sim/Town/` (namespace `TideToll.Sim.Town`), engine-free and deterministic like the rest: a town plan, a road graph, villagers on the graph, the economy, a player bot.
- The desire-line simulation and the river rules stay in `sim/` untouched as the earlier direction. Their fast tests keep running in `./check`: they cost seconds and guard code the second age may reuse.
- The old game scenes and scripts (the two chapters, the fence, gate, gravel and crossing tools) move to `game/legacy/` and leave the build; shared pieces are reused. The old game tests that only covered those scenes are retired with them and listed, each with its reason, in `docs/plans/retired-tests.md`.
- Goods are named Water, Grain and Flour everywhere (simulation, models, interface).
- The browser toy is finished and is not touched.

## How fun is judged after every round

Play with real input. Count decisions per minute (purchases, pave clicks, bell rings that mattered). Time how long until nothing is left to do. Say plainly what is boring.

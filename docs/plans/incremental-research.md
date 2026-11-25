# Incremental games: research for Tide & Toll

Written 25 November 2026. Web search worked but was thin. One search for "Incremental Traffic" found no page about it, so nothing here describes that game. Facts come from the sources at the end and from general knowledge. Anything not checked is marked "unverified". We copy no map, name, number, art or text from any game.

## 1. How traffic-style incremental games structure the loop

General shape (from what is known about the genre, partly unverified for any one title):
- A unit makes a trip on a road. A finished trip pays coin.
- Coin buys upgrade tracks. Typical tracks: more units, speed, load, road quality.
- Some purchases or counts unlock the next road, district or building.
- A stage is a set of unlocks. A stage ends with a bigger step: a new area or a reset.
- The player's main picture is the traffic itself, so the world shows the numbers.

Rules for us:
- R1.1 Every upgrade track must change what trips look like, not only a number.
- R1.2 Income comes from trips, so the trips must be on screen.
- R1.3 The next unlock is always named and always in reach.
- R1.4 A stage has a clear end that the player can see coming.

What players praise (partly unverified; seen for road and idle games such as Mini Motorways and Traffic Loop in search results):
- Calm, readable systems that look alive.
- Meaningful choices at upgrade time (Mini Motorways reviews say choosing upgrades adds real thought).
- A world that visibly grows.

What players complain about (mostly from general idle-game forums, unverified for traffic games):
- Waiting with nothing to do.
- Jams that feel random and unfixable.
- One thing that is always best to buy.
- Walls where progress stops until a long timer ends.
- Big numbers with no visible meaning.

## 2. Numbers known to work

Checked in the Pecorella articles and the Idle Idol postmortem:
- Costs grow exponentially: cost = base x rate ^ owned. Rates seen: 1.07 to 1.15 per level. AdVenture Capitalist uses 1.07 to 1.15 across its businesses. Derivative Clicker and Idle Idol use 1.1.
- Income per level grows linearly or slowly, so cost always catches up. This is on purpose: it makes each level take a bit longer.
- Lower tiers must stay worth buying after higher tiers appear. This is a known balance problem.
- Do not expose all mechanics in the first session. AdVenture Capitalist made the first purchase obvious for that reason.
- Active play should earn more than leaving it idle.
- Prestige has no agreed timing. Some designers call it a crutch. Later prestiges can give less than earlier ones.

Not verified by search (general knowledge, treat as starting values for our bot to test, not facts):
- First coin: under 10 to 25 seconds. Our brief says 25.
- First purchase: 30 to 45 seconds. Our brief says 45.
- Purchases per minute early: 4 to 8. Later: 2 to 4.
- Cost rate for cheap tracks (villager, pace): 1.12 to 1.18. For dear tracks (load): 1.3 to 1.6 with few levels.
- Income should roughly double each time the cost of the next track step has risen 1.5 to 2 times, so payback of a typical buy stays 20 to 90 seconds early and 2 to 5 minutes late.
- Something new (a building, a road, a perk) every 2 to 3 minutes; a visible small change every minute.
- First stage: 25 to 40 minutes for a new player, as in our brief. A shorter first stage (10 to 15 minutes) gets players to the second mechanic sooner.
- Second mechanic: introduce at 20 to 40 percent of the first stage. Our crowding and paving rule is the candidate. The bell is the first.
- Prestige in a first version: not worth it. Our age change hands over new town and works as the reward. A small carry-over (a keepsake or a perk) can be added later. Unverified opinion, supported by "prestige is a crutch" views and by the cost of tuning it.

## 3. What makes them satisfying to watch and to play

General design knowledge, unverified for specific titles except where noted:
- Feedback at the moment of earning: a coin pop, a sound and a small rise at the spot where the trip ends. A faster first reward with satisfying visuals hooks players (forum view, unverified).
- Goals at three distances:
  - Seconds: the next coin or trip.
  - Minutes: the next affordable upgrade.
  - Session: the next level gift and the age.
- Bars that are always nearly full: the level bar should show a visible step with every purchase, and the cheapest next buy should be about 10 to 30 seconds away.
- Small active inputs: the bell, clicking a road to pave it, choosing what to buy. Active play should pay a bit better than idling.
- Visible change in the world: new cottages, worn trails turning into tracks, more walkers, faster walking.

## 4. Usual ways they fail

- Dead time: a stretch where nothing is affordable and nothing can be done. Our limit is 60 seconds.
- One always-right buy: the bot "best payback" always picks the same track. Fix by making tracks compete (crowding, caps, rising costs).
- Walls: a step that costs far more than the last with no new income. Smooth the curve.
- Numbers without meaning: "+12 percent" with nothing to see. Pair each number with a picture.
- Nothing to do with your hands: only waiting. Give the bell and the paving choice.
- No visible difference after buying: a purchase that changes nothing on screen.
- Skipped tiers: unlocks that come so fast or are so weak that players never use them.
- Idle beats active: leaving it running should never be the best plan.

## 5. Ten recommendations for our game, ranked

1. First coin by 25 seconds and first purchase by 45 seconds; a bot measures the time of each in 100 seeds.
2. Never more than 60 seconds with nothing affordable; a bot logs the longest gap in coin-able purchases over a full age.
3. Every purchase must change the screen (a new walker, a faster pace, a bigger load on the back); a person buys each track once and checks that the change is visible within 5 seconds.
4. Keep at least 4 purchases a minute in the first 10 minutes and 2 a minute after; a bot counts purchases per minute for all three strategies.
5. No track is the best buy more than about 60 percent of the time; a best-payback bot reports the share of each track in its purchases.
6. Show the next level gift at all times and keep the level bar moving on every purchase; a person checks that the bar and the line "Next: ..." are always on screen.
7. The bell must beat idling: the active bot, which rings it whenever ready, must earn 10 to 25 percent more per minute than a bot that never rings; and a person must notice the effect.
8. Crowding and paving must make a real choice: the bot measures that adding villagers without paving lowers income per villager, and that paving the suggested road raises it.
9. Each level gives something new on screen at least every 2 to 3 minutes; a bot logs the time between level gifts and between any new building or road.
10. Skip prestige in version one; the age change is the carry-over, and the three bots finish the first age in 25 to 40 minutes with the slowest no more than twice the fastest.

## How to use this file

- Treat every number marked "unverified" as a starting value. Tune it with the three bots.
- If a bot target in the design brief and a number here disagree, the brief wins.

## Sources

- Pecorella, "The Math of Idle Games, Part I" https://gamedeveloper.com/design/the-math-of-idle-games-part-i
- Pecorella, "The Math of Idle Games, Part II" https://www.gamedeveloper.com/game-platforms/the-math-of-idle-games-part-ii
- Pecorella, "The Math of Idle Games, Part III" https://gamedeveloper.com/design/the-math-of-idle-games-part-iii
- GameAnalytics article (math and balance of idle games) https://gameanalytics.com/?p=6634
- "Balancing tips: how we managed math on Idle Idol" https://gamedeveloper.com/design/balancing-tips-how-we-managed-math-on-idle-idol
- "Idle Game Design Lessons From developing AdVenture Capitalist" https://www.globalgamesforum.com/ctv/idle-game-design-lessons-from-developing-adventure-capitalist
- Eric Guan, "Idle Game Design Principles" https://ericguan.substack.com/p/idle-game-design-principles
- "10 rules" (Subpixel, opinion on prestige) https://www.patreon.com/Subpixel/posts/10-rules-for-160530914
- Game bootcamp module 34, idle incremental https://seanking11.github.io/gamebootcamp/modules/34-idle-incremental
- Mini Motorways review, Twinfinite https://twinfinite.net/reviews/mini-motorways-review/
- Traffic Loop listing https://www.crazygames.com/de/game/traffic-loop
- Search found nothing for "Incremental Traffic" itself: unverified.

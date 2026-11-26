
## 04:32 UTC: game-town "a hand is offered at a queued workplace" (agent)
- Cause: the test's own Payback bots (Pave stretch, RulesV3) bought every extra hand the simulation offered before the hand check looked. In the failing clean-copy run: level 16, time 30647 s, the mill (the only workplace that queues) at hands 2 = HandMax, the other workplaces had no queue. How many offers came up and were bought depended on how much game time real-time frames let pass. Not a fault of the game or the simulation.
- Change: `TownBot.NoHands` test switch (same shape as NoPave; sim/Town/TownBots.cs); the Pave-stretch and RulesV3 bots set it. The hand is now always the first one, bought by the click (note `hand0=0`). Failure message of that check now prints level, time and each workplace's queue/hands/offer. No assertion weakened.
- Results: game-town 3/3 PASS main, 3/3 PASS clean copy (files copied); game-smoke, game-playthrough, game-river PASS; sim fast tests 258/258.

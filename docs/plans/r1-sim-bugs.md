# Round 1: bug hunt in sim/Town (report)

Date: 25 November 2026, started 21:08 UTC. Scope: `sim/Town/**`, read in full, plus experiments in `build/hunt/r1/` (not in git).

## What was run

1. Invariant scan: seeds 1 to 10, each with the three bots (cheapest, payback, random), 30 game minutes each (18000 ticks per run, 90 runs). Checked every tick: walker count per road against `RoadInfo.Walkers`, `Busy` and queue length per workplace against the villagers' phases, walking `Dist` inside the road, idle time, queued-with-a-free-place time. Result: 0 mismatches. Longest idle with no job: 0.1 s. Longest queued-with-a-free-place: 1.1 s (seed 10, random bot).
2. Save and load determinism: seed 1, payback bot, saved at tick 3000 and continued 3000 ticks from the loaded copy, every tick compared (coin, renown, trips, level, every villager's position and phase). Identical. A second run (seed 4, random bot) differed from tick 210 only because the random bot's RNG is not part of the save (the harness restarted it); that is a harness artefact, not a sim bug.
3. Read-only review of every file in `sim/Town/`.

## Findings (most serious first)

### 1. Free place left empty while a queue exists (low)
- `sim/Town/TownJobs.cs` lines 259-266 (`Arrive`: a villager queues whenever `QueueLen > 0`, even if `Busy < Places`) and lines 290-298 (`FinishWork`: only the head of the queue is started).
- Trigger: a queue forms at the well (1 place), then the player buys the second bucket (offer 0, level 4, 70 coin). `RefreshWorkplaces` raises `Places` to 2. `Busy` is 1 and the second place stays empty until the queue has drained, one villager per finished job. A second place is never filled while the queue is non-empty.
- Measured: the invariant scan saw queued villagers with a free place for up to 1.1 s. Effect on throughput is small in these runs.
- Sure: read from the code; the 1.1 s measurement shows it occurs. Not run as a dedicated scenario.
- Fix idea (not applied): in `FinishWork`, and after `RefreshWorkplaces` raises `Places`, start queued villagers while `Busy < Places`.

### 2. Renown rounding uses banker's rounding on fractional prices (low)
- `sim/Town/TownShop.cs` line 295: `RenownFor` uses `Math.Round(price * RenownPerCoin)`, which rounds half to even. Villager prices (`5 * 1.45^n`, not rounded) and Pace prices (`2 * 1.035^n`) are fractional: villager 1 costs 7.25 and gives 7 renown; villager 2 costs 10.5125 and gives 11; a price of 2.5 gives 2, and 3.5 gives 4.
- Renown is neither created nor lost in total over time in any way that compounds; each purchase is off by at most 0.5. Deterministic, so not a desync.
- Sure: read from the code and the arithmetic above.

### 3. Order-dependent route choice inside one tick (low)
- `sim/Town/TownWalk.cs` lines 77-91 (`TravelTime`): the all-pairs table is cached per tick (`_pairTick`) and built at the first call in a tick. `ChooseJob` for a villager later in that tick uses the walker counts from the start of the tick, while `SetRoute` (line 99) runs a fresh Dijkstra with the current counts. A villager can therefore pick a job by one set of travel times and walk a path chosen with another.
- Deterministic (same order every run), so it does not break the determinism rule; it is a small inconsistency.
- Sure: read from the code. Not measured.

### 4. Rest of the tick is dropped on arrival (nit)
- `sim/Town/TownWalk.cs` lines 147-156 and 159: when a villager reaches its target node part way through a tick, the unused time of that tick is discarded (`budget` is not carried into the next leg or the next phase). Each trip loses up to 0.1 s. Not a stall. Deterministic.
- Sure: read from the code.

### 5. Goods vanish silently when a route fails (latent)
- `sim/Town/TownJobs.cs` lines 305, 312 and 316-320 (`DropJobCarry`): if `SetRoute` fails after a job is finished, the carried Water, Grain or Flour is dropped with no coin, no `Delivery` event and no count. Not reachable now: `ChooseJob` only picks jobs with a finite `TravelTime` both ways and roads never close. It would become reachable if roads could close.
- Sure: read from the code.

### 6. Hard-coded offer ids (latent)
- `sim/Town/Town.cs` line 139 (`S.OfferBought[0]` for the second bucket) and `sim/Town/TownAchievements.cs` lines 50-51 (`OfferBought(1)` for the market, `OfferBought(3)` for the mill). They match `TownPlanner.cs` lines 162-167 today. Reordering the offers in the planner silently breaks the well's place count and two achievements.
- Sure: read from the code.

### 7. Fixed-size arrays with no bounds check (latent)
- `sim/Town/TownState.cs` line 124: `VillagerState.Path` has 48 slots; `sim/Town/TownWalk.cs` line 105 writes without a check. Safe with 17 roads (a shortest path is a simple path). A plan or `--set` tuning giving a longer path throws `IndexOutOfRangeException`.
- `sim/Town/TownState.cs` line 140: `WorkplaceState.Queue` has 64 slots. Villagers are capped at 2 per given cottage, 14 at most in this plan. `--set PerCottage=...` above about 9 could overflow it.
- Sure: read from the code. Not triggered.

### 8. Harness-only: bot state is outside the save (not a sim bug)
- `TownBot` keeps its own RNG (`TownBot.cs` line 19). `TownSave` does not store it, so a random bot resumed from a save makes different choices. Expected by design; the game does not use the bot. Listed so nobody reads the earlier divergence as a save bug.

## Checked and found fine

- Pay and affordability: `Pay` uses `S.Coin < price`, so buying with exactly the price is accepted and the coin goes to 0. Purchases without coin are refused in every `Buy*` method. Buying a villager with no room is refused before paying (`BuyVillager`).
- Housing: `HousingRoom = Housing - Villagers.Length`; villager count cannot exceed given cottages x 2.
- Level-ups: `ApplyLevel` runs once per level (`S.Level` advances inside it); `LevelReached` and `Charter` are emitted once; loading a save does not replay gifts.
- Events: `OfferAvailable`, `BellRung`, `RoadPaved`, `Purchase` are emitted once per action; `PerkEarned` only at multiples of 5. Achievements are emitted once (`S.Achieved`).
- Division by zero: road `Length` can be 0 only if two doors share a cell (not seen); `Comfort` has a minimum of 2; `Places` is at least 1; `Math.Max(0.1, seconds)` guards the job cost. Level 1 has cottage c1 and the well, so `AddVillager` always finds a home.
- Coin per minute: ring of 60 seconds, cleared per second; `Time = Tick * 0.1` is exact for whole seconds.
- Saves: all state including the queues and paths is saved; `_pairTick` is not, and resets correctly.

## Not done

- No dedicated scenario for finding 1 (only the 1.1 s measurement).
- No check of `TownPlanner` road crossings on 100 seeds (the tests already do this).
- Throwaway harness: `/home/user/tide-toll/build/hunt/r1/Program.cs` (currently the save/load experiment; the invariant scan was an earlier version of the same file).

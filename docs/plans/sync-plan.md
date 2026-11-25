# Porting plan: bring sim/ and game/ in line with the finished toy (rounds 3 to 5)

From `diff -ru` of toy-r2 (a057b50) against the final toy, and a read of sim/, sim.tests/ and game/. The repository was not edited.

**One fact shapes the plan.** I ran the final toy with round-2 values (wearPerStep 2.4, sideShare 0.3, minSurface 0.6, fordCostMul 3, explorerShare 0, detourCap 0, bridgeFlow 0) and rain and growth off. On seeds 1 to 3, 200 walkers, 20 days it matched toy-r2 bit for bit in wear, cost and tripRate. So every change in rounds 3 to 5 is a parameter value or sits behind a switch. So the port can be a refactor with legacy defaults and existing tests unchanged, then features on one golden section at a time.

## 1. What changed in the toy

"Off" means rain, growth, gravel, gates, explorers, detour cap and bridge flow all off.

| Where | Change | Changes off-results? |
| --- | --- | --- |
| params.ts | wearPerStep 2.4 to 2.2, sideShare 0.3 to 0.4, minSurface 0.6 to 0.45, fordCostMul 3 to 4. New: explorerShare 0.1, frugalDisc 0.3, detourCap 1.25, bridgeFlow 1; gravel (cost 0.6, speed 1.15, floor 0.45); gate (cost 1.5, speed 0.4); rain (chance 0.3/0.15/0.4/0 by season, mud 2 days, wetUp 3 s, dry 7.5 s, mud cost 1.6/1.7/1.8, mudReplace 0, speed 0.7); growth (every 30 s, target 250, household 3, frontage 50, clear 25, floor 50, radius 6) | Yes, the four retuned values only |
| world.ts costs | `surf` becomes `grounds(c, gravel)` with two multipliers: ordinary `max(minSurface, f*g)`, frugal `1 + (that - 1) * 0.3`. `cellCost` becomes `cellCosts(c, ov)` filling both views in one pass (`cost`, `costX`, `fields`, `fieldsX`). Bridge deck `0.8 * max(minSurface, flow)`. Ov is a bit set (Block, Open, Bridge, CloseBridge, Gravel, Ungravel, Gate, Ungate, GatePair). Mud multiplies the dry cost, blended by wetness; frugal sees 30% of it. A gate adds 1.5 (0.75 a cell in a pair) after crowding | No |
| world.ts walkers | `explorer = hash2(id, 77) < 0.1` (no RNG draw); per trip `frugal = explorer or overDetourCap` (straight distance over 4 and ordinary `routeLength` over 1.25 times it). Frugal aims on `fieldsX` and `costX`, taste 0, same step noise. `speedOn`: gravel x1.15, gate x0.4, mud toward x0.7 by wetness | No |
| world.ts tick order | 1 time, metrics, occupancy, tripRate decay. 2 weather, then `updateMud` if a shower started or mud must clear. 3 `refreshFields` (one building a tick, destinations first, now two Dijkstras each). 4 walkers in id order. 5 decay each sim second, then `updateMud`. 6 growth every 30 s. Every edit runs `updateMud` before rebuilding costs | No |
| world.ts state and tools | Added: gate, gatePair, gravel, costX, fieldsX, mud, weather, grown, fronted, nextHouseTime, growthRng, Walker.explorer/frugal; buildings, fields and tripRate grow. Decay keeps gravel at wear 25 and fronted cells at 50. New placeGravel (a fence takes gravel up), gravelSnap, placeGate, removeGate, gateTarget, `addHouse`; erase takes gates and gravel | No |
| weather.ts | Own stream `mulberry32(((seed * 2654435761) ^ 0x5eed) >>> 0)`, two draws a day even when catching up; plain double arithmetic | No |
| growth.ts | 3x3 plot on a frontage cell at wear 50 or more, scan order, score `3*wear/100 + access + near (hypot <= 6, max 3) - mud - crowding`, stable sort, first 8 tried for reachability; new villagers draw from `mulberry32(((seed * 40503) ^ 0x6f7d) >>> 0)` (timer, then taste) | No |
| gates.ts | Pure `gatePassage`, `gateCellsFor`, `gateTarget` (nearest fence within 2 cells), `gateGroup` | No |
| path.ts | Bucket queue (same values as the heap), `routeLength`, `lineCost(limit)`, `chooseAim(memo)`: speed only | No |
| preview.ts | Both walker kinds: `f` and `fX` per destination, ordinary and explorer groups per house, `outOfReach` shortcut, MIN_RATE 0.5 to 0.2, `meanCostRatio`, gravel `note`, new wording, rebuild when buildings grow | Readout and thresholds, yes |
| metrics.ts, mapgen, rng, grid | `trailWidth` added (report only); the rest identical | No |

## 2. Where it lands in sim/, and the river conflicts

| Toy item | C# home | Meeting the river hooks |
| --- | --- | --- |
| Params | SimParams.cs: new properties, `RainOn`, `GrowthOn`; presets `Legacy` (round 2) and `Toy`; `Default` flips last | `Default with { River = ... }` inherits it, so river tests shift when it flips |
| Two cost views | World.cs `CellCost` becomes `CellCosts` writing two doubles; `BuildCost(out, outX, from, to)`; `_fieldsX`; new WorldGround.cs (files stay under 400 lines) | **Conflict A.** Ferry, drawbridge, ice and flood-wade costs are expected waits, not worn ground: use them identically in both views, as the toy does for ford and crops. Ice's wear discount follows land (frugal 30%) |
| Flags | `CostOverride` becomes `[Flags] ushort`; `RiverCellCost` compares `ov == Block`: change to mask tests | `WhatIf.Crossing` cost goes into both preview arrays |
| Rain mud | RainWeather.cs, `UpdateMud` in `GroundChanged()` (shared by `Edited()` and the river's `CostsChanged()`) | **Conflict B.** `BandCostMul` already applies 1.6/1.7/1.8 to flood-band mud. Where both apply take the larger, not the product; no rain mud on a band cell while `_flooded`; gravel is exempt from flood mud, not from wading |
| Gravel, fence gates | `_gravel`, `_gate`, `_gatePair`, FenceGates.cs (RiverGates.cs owns that name) | **Conflict C.** `CanFence` already bars crossing ends, so no fence gate lands there. Refuse gravel on crossing ends, ferry stages (`_crossAt`) and the landing. Price under the river's money rules: lead decides |
| Growth | Growth.cs; buildings, fields, tripRate pre-sized; `Buildings`, `Houses` become counted views | **Conflict D.** Plots must avoid `_band`, crossing ends, landing and bridge approaches. Each house adds `HearthTax` and may be charged `FloodRepairCost`: the economy tuned on 6 to 8 houses would see 24. Resolution: growth off while River is on, until retuned |
| Preview v2 | FlowPreview.cs split in two; keep heap `DijkstraRun` (slices stay bit-identical) | River's boat lines stay after the toy text |
| Tick | Weather before `RefreshFields`; growth after decay, before `RiverSettle` (tick 300 is both a day end and the first arrival) | `RiverStep` stays after `RefreshFields` |

Capacity: at most `min(HamletTarget, MaxWalkers) / 3 + 1` new houses (84 extreme). A field is 64 KB, so about 90 buildings x 2 views is 12 MB; allocate only when `GrowthOn`. `_tripRate` keeps a fixed stride; `TripRatePerDay` stays the public view. The plot search must not allocate: keep the best 8 in a fixed array, inserting after every plot of equal or higher score (exact, since the toy only tries the first 8 of a stable sort), and preallocate the flood-fill scratch. Name the new stream `_rain`. GateTables, ToyChecks and tools/TideToll.Tools must pass rain and growth explicitly: the gate runs with both off, the live gate with both on.

## 3. Exactness traps

- **Round every stored value:** wear, flow, hollowHold, occ, tripRate, cost, costX, fields, fieldsX, both LUTs, noise are float32: `(float)(double expr)`, first operand of a float sum cast to double; `flow[c]++` is `(float)(flow + 1.0)`.
- **Keep operation order:** `1 + (max(minSurface, f*g) - 1) * frugalDisc`; mud `dry + (muddy - dry) * wet` then `1 + (gc0 - 1) * frugalDisc`; gate cost after crowding. A reordered sum changes bits.
- **Bucket queue:** do not port it for exactness. Its values equal the heap's, so `CostField.Dijkstra` and `DijkstraRun` already give them. Tie order matters only with `goals`: unsettled cells then hold different tentative values, so goldens hash doors and traced routes for goal-limited fields, never the whole array. Port it later for speed, with a property test against the heap.
- **Sorts:** the toy's sort is stable. Plot score (descending, ties in scan order), preview rate and `gateTarget` need a stable or total order; `Array.Sort` is not stable. Use insertion order.
- **Iteration:** JS `Map` keeps insertion order (`walkersOf`, `blocked`): use ordered lists.
- **Seeds and hashes:** `hash2` is identical. Stream seeds: `(uint)((long)seed * 2654435761L) ^ 0x5eedu`, `(uint)(seed * 40503) ^ 0x6f7du`. The toy multiplies in doubles, exact only below about 3.4 million; `NextSeed` stays under 99,992.
- **Time:** `time += 0.1` accumulates identically; never use `ticks * dt`.
- **Integers:** `(c / N) | 0` equals `/` for non-negatives; `here` (Uint16) wraps in both while C# overflow checks stay off.
- **Hypot and rounding:** keep `JsMath.Hypot` for the plot test (`<= 6` is borderline), `overDetourCap`, `lineCost`, `traceRoute`; `**2` is `x*x`; use `JsMath.Round`.

**Math.Exp, Pow, Sin.** On results paths in the final toy: `pow` in the wear LUT (801 values) and five decay factors (constant arguments); `exp` in the flow LUT (1,025 values), the tripRate decay (one constant), Weidmann speed (argument depends on `here`, an integer: about 20 distinct values) and crowd cost (continuous `occ`, per cell per rebuild, stored to float32); `sin` only in mapgen (stored to float32 `riverX`). `atan2` and `cos` appear only in report-only `trailWidth`. .NET calls the platform libm and V8 uses fdlibm, so the last bit may differ on another OS or CPU; on this machine ten 45-day traces match today. A portable implementation is not needed to match the toy in tests here. It is needed only if replays must agree across machines. Plan: route every call through `JsMath.Exp/Pow/Sin` (delegating to Math at first), put probes and LUT hashes in the goldens, run the golden tests once on Windows and ARM. If a probe fails, tabulate the LUTs and Weidmann speed from the goldens; only the crowd `exp` and mapgen `sin` then need an fdlibm port (about 150 lines).

## 4. The golden plan

One exporter (`export.mjs`, run on the final toy, with a legacy switch applying round-2 values), one file.

| Section | Content |
| --- | --- |
| params, probes | all PARAMS; LUT FNVs; exact doubles of the five `pow`, the `exp` constant, 40 `exp` and some `sin` samples |
| maps, rng, hash, line4, dijkstra | as now (maps unchanged); add a low-cost Dijkstra and `routeLength` |
| traces OFF | seeds 1 to 8 at 200, seeds 1 and 2 at 500, 45 days, both off; per day wear, flow, cost, costX, positions |
| traces LIVE | seeds 1 to 3 at 200, seed 1 at 500, both on; per day also mud FNV, rain, wetness, mudUntil, houses, walkers; the full `grown` list |
| weather alone | seeds 1 to 50, 120 days, FNV per tick |
| plots | seeds 1 and 2, days 3 and 9: best 8 plots (x, y, door, front, exact score), the chosen one, the reachability result |
| edits at fixed ticks | one LIVE world: fence (300), gravel (450), gates in fences at 0, 30, 45, 60, 90 degrees (600), bridge (750), erase (900), `startRain` (1000), `stopRain(true)` (1200); per 150 ticks hashes of wear, cost, costX, gravel, gate, gatePair, fence, mud, version; `gateTarget`, `gravelSnap`, refusal strings |
| previews | block, bridge, enclose plus gravel (far, on, near), gate, ungate, in a LIVE world with grown houses; per change rate, explorers, frugal, costRatio, route FNVs; `meanCostRatio`, `note`, text |
| table | seeds 101 to 120 OFF, 601 to 620 LIVE (mud and rain time) |

Size about 250 to 400 KB with compact arrays (now 102 KB). A 45-day LIVE trace takes 12 s in node; about 45 runs, so 10 to 15 minutes quiet.

**Regenerate, never edit:** ToyParityTests, GateTests and ToyComparison, RngGoldenTests (add the two streams), CostFieldTests (values unchanged), the baseline. **Re-port by hand, one for one, from the toy's own checks:** ToyChecks and ToyPreviewChecks (`sim.test.ts` plus `rules.test.ts` hold 69 checks; the C# ports about 15). **Expectations from toy output only:** PreviewSlicingTests (more steps), InvariantTests (add growth, rain).

**RiverOffBaselineTests** hold C# hashes of the round-2 model. Re-record honestly: the exporter records, for seeds 1 to 3 at 200 walkers, 20 days, both off, the toy's day-20 wear FNV and the next three walker-stream draws (read by a cast; the generator state is private); C# compares `new Mulberry32(w.RngState)` draws. Keep the old three rows run on `SimParams.Legacy`: they prove the river hooks stay invisible. Add a family for the live defaults. Never record from C# output.

## 5. The game, by value

1. **Houses arriving.** TownLayer rebuilds only on `WorldReplaced`. Add `World.BuildingCount` and a `HouseBuilt` event through SimDriver, and `TownLayer.AddHouse` (cottage-01..03 exist). Scenery, MapTags and GroundMarks need the same call.
2. **Gravel and gate tools.** Icons (tool-gravel.svg, tool-gate.svg) and the model (fence-gate.glb) exist with ledger rows. Hooks: `Tool` enum and ToolTabs `Tab`; ToolController (brush with `GravelSnap`, click with `GateTarget` and its refusal strings); ToolGhost, ToolTargets; FenceLines and EditLayer (gate cut into the rail at any angle); Erase. Draw gravel in MapGround through a third channel of `mask_tex` (RG8 to RGB8) with a rim and grit in ground.gdshader.
3. **Rain and mud.** Expose a `Weather` view (rain, wetness, raining, rainStart, mudUntil) and a `Mud` span. MapGround uploads a mud texture beside `season_tex` and a wetness uniform; the shader darkens worn cells and dries them from the least worn over the last day. Rain streaks as a screen overlay. Merge the flood band's `mud` uniform (Conflict B).
4. **Chapter page and wording.** ChapterPage near line 235: "Dry", "Rain, day N", "Mud until day N"; "Houses: N" with "newest on day N". Previews: "Reroutes about 20 walkers a day (+13% longer on average)", "Blocks the Well for 3 houses", the three gravel notes, "Saves under 5% of the walk, so many will keep to their old way", tool hints, gate refusals. FlowPreviewer already uses `Begin/Step(int)`; only `Describe` changes.
5. **Sound.** assets/audio has no rain bed, gravel or mud steps, gate cue. Reuse place-0 for the gate; rain needs a new asset with a ledger row or a generated stream in AudioDirector. Last.

## 6. Work order

**Builder A, library.** Each step ends with `dotnet test sim.tests` (the allowed filters) green and a note of the first golden section that differs.

| Step | Work | Proof | Risk |
| --- | --- | --- | --- |
| A1 | Exporter v2 (node only); run on toy-r2 and check the legacy sections equal today's JSON byte for byte; run on the final toy | byte-equal; file under 1 MB; time noted | low |
| A2 | Refactor on `Legacy`: flags, `CellCosts`, `BuildCost(out, outX)`, fieldsX behind `UseFrugal`, Walker fields, capacity plumbing, params | whole existing suite unchanged, old goldens and baselines included | medium |
| A3 | Trail model: `Toy` preset, frugal view, explorers, detour cap, bridge flow, `LineCost(limit)`, `RouteLength` | OFF traces day by day, 101-120 table exact, LUT and probe tests | **high** |
| A4 | Gravel, fence gates, speed, decay floor, erase, preview flags | edits-at-ticks; gate-angle checks re-ported | medium |
| A5 | Preview v2 and `Describe` | previews; sliced equals one-go | **high** |
| A6 | Weather, mud, update order, edits | weather-alone; LIVE traces, growth off | medium |
| A7 | Growth: capacity, plots, allocation-free search | plots; LIVE traces; allocation test at 200 walkers (today's runs 500, where growth is idle) | **high** |
| A8 | River pass: Conflicts A to D, growth off with River, economy and bot compared | River*Tests green on `Legacy` and `Toy`; economy shift reported, not retuned | **high** (tuning drift) |
| A9 | Flip `Default`, new baselines, `./check --gate`, `--bench`, sim/README | mean under 5 ms at 500 walkers with two fields a building | medium |
| A10 | Optional: bucket queue, `outOfReach` | test against the heap | low |

**Builder B, game** (after A9): B1 sim views and `HouseBuilt`; B2 houses; B3 gravel and gate tools; B4 mud and rain; B5 chapter page and wording; B6 sound.

Size: library about 1,200 new lines plus 600 of tests; game about 800 plus shader.

## Decisions for the lead

1. Flip `Default` only at A9 (river agents stay on `Legacy` until then)?
2. Growth off while the river rules are on?
3. Must replays match across machines (portable exp, pow, sin)?
4. Gravel and gates free under the river's money?
5. Keep the legacy section in the exporter (recommended)?

# Retired game tests and check steps

Retired on 25 November 2026 with the desire-line game (the two chapters and the fence, gate, gravel and crossing tools), when the incremental town became the game (docs/plans/incremental-design.md). Their scenes and scripts are kept for reference under `game/legacy/` (a `.gdignore` hides it from Godot, `<Compile Remove="legacy/**" />` keeps it out of the C# build). Nothing here guarded code that still ships: every test below loaded the old `Main` scene or its tools, which no longer exist in the build. The simulation's own tests (`sim.tests`, including the river rules and the desire-line simulation) are untouched and still run in `./check`.

## Check steps (tools/check.sh)

| Step | What it covered | Why it goes |
| --- | --- | --- |
| game-smoke (old) | The old world view: scene tree complete, walkers move and wear grows, a fence and a bridge placed and erased through debug commands, picking at two zooms, scenery off doors and the ford, reset and new map | Replaced by a new game-smoke on the town scene; fences, bridges, wear grid and new map are gone |
| game-tools | Fence drag, footbridge, erase, toolbar and cancel by real input; the flow preview's isolation (same wear hash with and without preview) | The player no longer draws or builds paths: no tools, no flow preview |
| game-river | Ferry and drawbridge placed with a ghost, refusals, raising and lowering by click, key and lever, the keeper, boats over four days | The river chapter is retired; Age II (The River) will be rebuilt on the town |
| game-river-rules | Wet grain at the ford, the keeper earned by use, the starting purse, flood band edge, turn-back notes, queues, ledger after a jump | Same: Chapter II's rules are not part of the town game |
| game-chapter | Opening on Chapter I, the chapter bookmark and C turning the page, Chapter II having the river rules, reset inside a chapter | There are no chapters any more |
| game-hamlet | Chapter I's living town: a grown cottage drawn (rising, yard, page count), rain, wet ground, mud and drying | House growth and rain belong to the desire-line simulation; the town grows by levels now (checked by game-town) |
| game-ui | Every old control (tool tabs, speed, sound, overlay) exists, has a tooltip and label, toggles by mouse; every old shortcut; focus; text sizes | Those controls are gone; the town's interface is exercised by real input in game-town |
| game-perf (old) | CPU ms per frame with 500 desire-line walkers at 1x and 8x in both chapters, flow preview recomputing, 64 bytes per frame allocation gate | Replaced by a new game-perf with the end-of-age town at the same time budget (6 ms at 1x, 10 ms at 8x mean); the allocation gate is not carried over (the town interface builds its strings once a frame; the simulation's own test checks Step allocates nothing) |
| game-perf-rerun | One re-run of the old perf step when only its worst-frame gate failed | The new perf step gates the mean only, so no re-run is needed |
| screenshot, screenshot-1080 | The whole book, the map page after about 20 days, wear and preview close-ups, river views, at 1280x720 and 1920x1080 | Replaced by the town screenshots (town-*-1920x1080.png) |
| screenshot-hamlet, screenshot-hamlet-1080 | Chapter I at both sizes: day 1, page turn, gate and gravel strip, day 30, rain and mud | Chapter I is retired |

## Test files (moved to game/legacy/tests/)

| File | What it covered | Why it goes |
| --- | --- | --- |
| Smoke.cs, smoke.tscn | The old game-smoke (above) | Replaced by tests/Smoke.cs on the town |
| ToolsTest.cs, ToolsTest.Paths.cs, tools.tscn | game-tools | No tools |
| RiverTest.cs, RiverTest.Seasons.cs, river.tscn | game-river, including flood and ice drawing | River chapter retired |
| RiverRulesTest.cs, river-rules.tscn | game-river-rules | River chapter retired |
| ChapterTest.cs, chapter.tscn | game-chapter | No chapters |
| HamletTest.cs, hamlet.tscn | game-hamlet | Chapter I retired |
| UiTest.cs, ui.tscn | game-ui | Old interface retired |
| Perf.cs, perf.tscn | Old game-perf | Replaced by tests/Perf.cs on the town |
| Screenshot.cs, Screenshot.Hamlet.cs, Screenshot.River.cs, Screenshot.RiverRules.cs, screenshot.tscn | The four old screenshot steps | Replaced by tests/Screenshot.cs on the town |

Kept: `tests/TestInput.cs` (the real-input helper, now used by the town tests) and the settings tests (`settings.tscn`, `SettingsTest*.cs`, `SettingsFakes.cs`, `SettingsShot.cs`, `settings-shot.tscn`), which round 2 adds to `./check`.

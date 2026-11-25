# Game core progress

Started: 2026-11-25 20:56 UTC (time box ~100 min, until ~22:36 UTC)

## Done
- Step 1: old scenes/scripts/tests moved to game/legacy/ (.gdignore; `<Compile Remove="legacy/**"/>` in game/TideToll.csproj).
  Kept: BookBuilder, CameraRig, InkOutline, ModelLib, Palette, UiKit (Tuck const moved in from ToolTabs), GravelStrip, settings/**, tests/TestInput + settings tests.
- New code in game/scripts/town/: TownDriver, TownGround (+ cobbles/crowding in shaders/ground.gdshader), TownBuildings, TownVillagers,
  TownTrees, TownHud (+ .Shop, .Pave), TownAudio; game/scripts/Main.cs rewritten; game/main.tscn new. Builds.

- Tests written and passing alone: game/tests/{Smoke,TownTest,Perf,Screenshot,TownTestBase}.cs + smoke/town/perf/screenshot.tscn.
- tools/check.sh: old game steps replaced by game-smoke, game-town, game-perf, screenshot (1920x1080).

- ./check passed once in full (21:18 UTC). retired-tests.md, AGENTS.md, README.md, game/README.md, workflow note written.
- After that: trail drawn a bit stronger (TrailWear 0.4), repeated notes merged, second bucket drawn beside the well,
  game-town extended to the first building purchase (passes, level 4).

- Final ./check --export at about 21:35 UTC: CHECK PASSED (all steps, export OK). Log: build/check-export2.log.

## Status
FINISHED. Not done / not tested: villagers waiting in line are drawn at the simulation's queue spots but no test
asserts it; Load and Pave-to-cobbles purchases are not reached in game-town; no menus, book zoom-out or settings join
(next round by the brief).

## Decisions
- AudioDirector was tied to the old Main/SimDriver/tools: retired; TownAudio reuses its buses and rules with the same files.
- MapGround was tied to the old SimDriver: retired; TownGround reuses the same shader and GravelStrip.
- A level's gift resumes camera follow ("the camera makes room").
- Settings runtime is not joined yet (round 2); Main calls GameInput.Register() so the InputMap actions exist.
- Sounds: at 21:2x another agent added town cues (buy, level-up, gift, pave-gravel, pave-cobbles, achievement, charter, with ledger rows);
  TownAudio uses them where the files exist, else Hamlet stand-ins (place, bell-long, coin). Nothing required is missing.
  Not used yet: ui-hover/ui-press/ui-tab, book-open/close, page-turn, extend, cannot, request-*, mill-loop.ogg (menus and later rounds).
- First ./check --export run: godot-import step FAILED with no error line while another agent was adding sound files;
  import re-run alone exits 0. Re-running the full check.

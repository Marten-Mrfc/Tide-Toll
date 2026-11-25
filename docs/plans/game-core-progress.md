# Game core progress

Started: 2026-11-25 20:56 UTC (time box ~100 min, until ~22:36 UTC)

## Done
- Step 1: old scenes/scripts/tests moved to game/legacy/ (.gdignore; `<Compile Remove="legacy/**"/>` in game/TideToll.csproj).
  Kept: BookBuilder, CameraRig, InkOutline, ModelLib, Palette, UiKit (Tuck const moved in from ToolTabs), GravelStrip, settings/**, tests/TestInput + settings tests.
- New code in game/scripts/town/: TownDriver, TownGround (+ cobbles/crowding in shaders/ground.gdshader), TownBuildings, TownVillagers,
  TownTrees, TownHud (+ .Shop, .Pave), TownAudio; game/scripts/Main.cs rewritten; game/main.tscn new. Builds.

## Next
- run the scene headless, write tests (smoke, town, perf, screenshot), check.sh, docs.

## Decisions
- AudioDirector was tied to the old Main/SimDriver/tools: retired; TownAudio reuses its buses and rules with the same files.
- MapGround was tied to the old SimDriver: retired; TownGround reuses the same shader and GravelStrip.
- A level's gift resumes camera follow ("the camera makes room").
- Settings runtime is not joined yet (round 2); Main calls GameInput.Register() so the InputMap actions exist.
- Missing sounds: no dedicated purchase sound (uses place-0N.wav), no dedicated level-up sound (uses bell-long-*.wav).

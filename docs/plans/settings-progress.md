# Settings pages: progress

Started 2026-11-25 20:35 UTC (time box about 75 minutes, so aim to finish by 21:50 UTC).

## Decisions
- Settings file is written in ConfigFile syntax but read by our own lenient parser (no Godot parse errors are printed for a damaged file; bad lines fall back to defaults).
- Tests use their own input helper (game/tests/SettingsInput.cs) because tests/TestInput.cs references Main, which is retired.
- UiKit.MakeTheme is used for the look; it still references ToolTabs.Tuck (a constant) - integrator must keep that constant alive.

## Done
- see the steps below

## Next
- GameInput, GameSettings, DisplaySeam, SettingsPages, test, screenshots, handover.

## Step 1 (about 20:45 UTC)
- Done: GameInput, GameSettings, DisplaySeam, SettingsApplier, SettingsRuntime, ISaveActions, SettingsWidgets, SettingsLook, SettingsPages (+ .SoundDisplay, .KeysSaving). Builds.
- Next: game/tests/settings.tscn + SettingsTest*.cs, then screenshot scene, then handover.

## Step 2 (about 20:48 UTC)
- Done: tests (game/tests/settings.tscn, SettingsTest.cs, SettingsTest.Keys.cs, SettingsTest.Keyboard.cs, SettingsFakes.cs) pass: SETTINGS PASS.
- Screenshot scene game/tests/settings-shot.tscn + tools/settings-shots.sh work.
- Fixes found by the test: a click at a slider's far end gave 99 (VolumeSlider snaps the last 3 percent); ChoiceButton rebuilt the page before accepting the event (engine error).
- Next: look at screenshots, polish, handover, 3 runs + full build.

## Step 3: finished (20:54 UTC)
- Everything is done. Handover: docs/plans/settings-handover.md.
- Polish after looking at the screenshots: Sound split over both pages, rows stack under their names when the column is narrow, slip padding, clash text names the key the other action gets, the Display choices show the trial value while "Keep this?" is open, key names fit ("Plus", "Minus", empty slot "None" in faded ink).
- Final: test passed 3 of 3 runs, game build 0 warnings 0 errors, 9 screenshots written by tools/settings-shots.sh.
- A successor only needs to verify: run the test command and tools/settings-shots.sh from the handover.

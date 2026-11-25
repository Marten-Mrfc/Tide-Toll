# Settings pages: handover

Built in the worktree `build/wt/settings`. Every file is new; **no existing file was changed** (no project.godot, no bus layout: buses are made in code).

## New files (paths from the repository root)

Game code, namespace `TideToll.Game.Settings`:
- `game/scripts/settings/GameInput.cs`: the action table (InputMap names, English names, two default keys each), `Register()`, `Apply(bindings)`.
- `game/scripts/settings/GameSettings.cs`: the store; `user://settings.cfg`; `Changed` event; defaults; lenient loading.
- `game/scripts/settings/SettingsApplier.cs`: puts the store into effect (bus volumes, window through the seam, InputMap) and follows every change.
- `game/scripts/settings/SettingsRuntime.cs`: the start-up node (load, apply, follow, mute when the window is not active).
- `game/scripts/settings/DisplaySeam.cs`: every call that touches the real window (window mode and size, VSync, frame limit, content scale).
- `game/scripts/settings/ISaveActions.cs`: what the Saving page needs from the game.
- `game/scripts/settings/SettingsPages.cs`, `SettingsPages.SoundDisplay.cs`, `SettingsPages.KeysSaving.cs`: the screen.
- `game/scripts/settings/SettingsLook.cs`: its theme (built from UiKit colours, fonts, Card, FocusBox, Px).
- `game/scripts/settings/SettingsWidgets.cs`: `ChoiceButton`, `SwitchButton`, `VolumeSlider`, `SpreadArt` (table, cover, pages).
- each `.cs` has its `.cs.uid` beside it.

Tests and tools:
- `game/tests/settings.tscn`, `game/tests/SettingsTest.cs`, `SettingsTest.Keys.cs`, `SettingsTest.Keyboard.cs`: the test.
- `game/tests/SettingsFakes.cs`: `FakeSaves` (ISaveActions) and `RecordingDisplay` (DisplaySeam).
- `game/tests/settings-shot.tscn`, `game/tests/SettingsShot.cs`: one screenshot per run.
- `tools/settings-shots.sh`: all the screenshots.

Dependencies: Godot, `UiKit` (colours, `UiFont`, `UiBold`, `Serif`, `Display`, `Card`, `FocusBox`, `Text`, `Rule`, `Px`, `FontPx`, `ScaleFor`), `Palette`, the fonts, `assets/audio/sfx/coin-01.wav` (test sound). Not `UiKit.MakeTheme`, `UiKit.Tab` or `UiKit.Icon` (those reach into `ToolTabs` and `ModelLib`). The tests do not use `tests/TestInput.cs` (it references `Main`); they carry their own input helpers. Note: `UiKit.cs` itself still references `ToolTabs.Tuck` and `ModelLib.MipTexture`, so whoever retires those must keep UiKit compiling.

## Start-up (main scene, first thing)

```csharp
var settings = new TideToll.Game.Settings.SettingsRuntime { Name = "Settings" };
AddChild(settings);   // _EnterTree: registers the actions, loads user://settings.cfg, applies it; GameSettings.Current is set
```

## Opening the screen from the pause menu

```csharp
var pages = new SettingsPages(settingsRuntime, saveActions);   // saveActions: the game's ISaveActions, or null
pages.Closed += () => { pages.QueueFree(); /* show the pause cover again, give it focus */ };
canvasLayer.AddChild(pages);   // fills the screen, takes the mouse, puts focus on the Sound ribbon
```

Put it on a CanvasLayer above the game interface. It stops the mouse; it handles Esc itself (`_Input`, marks it handled), so the pause menu must not also react to that same Esc press. Create it only in code (its parameterless constructor exists only for Godot).

## Events

- `SettingsPages.Closed`: Back or Esc (Esc first cancels a rebind or closes an open slip). A pending "Keep this?" is reverted on close.
- `GameSettings.Changed(string key)`: after every change (already saved). Keys: `volume:Master`, `volume:Music`, `volume:Ambience`, `volume:Effects`, `volume:Interface`, `mute_inactive`, `window_mode`, `resolution`, `vsync`, `frame_limit`, `interface_size`, `coin_numbers`, `follow_town`, `autosave_minutes`, `keys` (constants `GameSettings.K...`). The applier already handles sound, window, frame rate, content scale and keys.

## What the game reads (from `GameSettings.Current`)

- `AutosaveMinutes`: 0 (off), 1, 2 or 5. Re-read it on `Changed("autosave_minutes")` and restart the autosave timer.
- `ShowCoinNumbers`, `FollowTown`: bools; read when needed or on `Changed`.
- `InterfaceSize` (80 to 150) / `ContentScale` (0.8 to 1.5): already applied as `Window.ContentScaleFactor`. The game's own interface should NOT multiply by it again; build in canvas units from the window's real height (`UiKit.ScaleFor(GetWindow().Size.Y)`), as `SettingsPages` does, or the size is counted twice.
- Volumes: buses `Master`, `Music`, `Ambience`, `Effects`, `Interface` are created if missing and owned by the settings. The old `AudioDirector` volume slider (it sets the Master bus) must go, or it fights the store.

## ISaveActions (the game implements it)

```csharp
public interface ISaveActions {
    bool HasSave { get; }              // a save on disk
    DateTime? LastSaveTime { get; }    // local time, null when none
    string FolderPath { get; }         // ProjectSettings.GlobalizePath of the save folder
    bool SaveNow();
    bool LoadLast();                   // the page then shows "The last save is loaded."; the pause menu decides whether to close
    void DeleteSave();                 // only called after the player confirms
}
```

## InputMap actions (`GameInput.Actions`; two keys each, physical keys)

`tt_move_left` A / Left, `tt_move_right` D / Right, `tt_move_up` W / Up, `tt_move_down` S / Down, `tt_zoom_in` = (the plus key) / Num +, `tt_zoom_out` - / Num -, `tt_home_view` Home, `tt_pause` Esc, `tt_bell` Space, `tt_pave` P, `tt_buy_1` 1 (villager), `tt_buy_2` 2 (pace), `tt_buy_3` 3 (load), `tt_buy_4` 4 (pave), `tt_buy_5` 5 (building), `tt_achievements` Tab. Stored in `[keys]` of the settings file; applied at start-up and on every change. A clash offers "Swap keys" or "Cancel". The settings screen itself uses Godot's `ui_*` actions and Esc (`ui_cancel`), so a rebound `tt_pause` never locks the player in.

## Commands (from the repository root)

```
dotnet build game/TideToll.csproj -c Debug
tools/godot-test.sh 'SETTINGS PASS' timeout 300 godot --headless --path game --audio-driver Dummy res://tests/settings.tscn --quit-after 20000
tools/settings-shots.sh     # needs xvfb-run; writes build/screenshots/settings-*.png
```

The test takes about 15 seconds (it waits out the real 10-second "Keep this?" fallback). For `tools/check.sh`, add next to game-ui:

```
step game-settings tools/godot-test.sh 'SETTINGS PASS' timeout 300 "$GODOT" --headless --path game --audio-driver Dummy res://tests/settings.tscn --quit-after 20000
```

## Decisions made without the owner

- Defaults: Master 80, Music 70, Ambience 70, Effects 80, Interface 80; mute when inactive off; Windowed 1280 x 720; VSync on; no frame limit; interface 100 percent; coin numbers on; follow the town on; autosave every 2 minutes.
- Volume curve: square law (50 is about -12 dB); 0 mutes the bus.
- Borderless full screen is Godot's `Fullscreen` mode, Full screen is `ExclusiveFullscreen`. Resolution is only used in windowed mode (the choice is faded otherwise).
- The file is in ConfigFile syntax but read by our own parser, so a damaged file prints nothing; each bad line falls back to its default, the rest is kept.
- During "Keep this?" the trial is applied through the seam but not saved; only Keep writes it.

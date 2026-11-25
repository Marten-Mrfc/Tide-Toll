# R3 game bug hunt (code reading only)

Started 23:52 UTC, finished about 23:58 UTC. Read-only: no build, no run, no repository files changed except this report.
Line numbers are from the tree as read at 23:52 to 23:58 and may shift while the builder works. Each quote is the line as read.

## Findings, most serious first

### 1. P and bookmark keys switch Pave or Extend mode while the book is closed, in Settings and while opening
- File: game/scripts/town/TownHud.Shop.cs, `if (Is(e, "tt_pave")) SetPaveMode(!PaveMode);` (about line 356). Also `Activate` (about lines 168-169): `if (it.Kind == ShopItemKind.Pave) { _main.Audio.UiPress(); SetPaveMode(!PaveMode); return; }`.
- What goes wrong: `_UnhandledInput` in the HUD never checks `Menu.Now`. Pause() hides the HUD (`_main.Hud.Visible = false`), but a hidden CanvasLayer still gets unhandled key input. Pave mode then starts under the closed cover. `SetPaveMode(true)` calls `BringIntoView`, which moves the camera (TownHud.Pave.cs, `rig.LookAt(local.Value, ...)`). `PauseMenu.Resume()` (PauseMenu.cs, `Resume`) never clears PaveMode, so Pave mode stays on after the book opens, with its banner and click handling. The same happens on the Settings pages (state Settings, HUD hidden, SettingsPages._Input does not consume letter keys outside capture).
- Trigger: press Esc (pause), press P (or the Pave bookmark key), then Resume or Back. Or press P while on Settings. Repeat with E for Extend.
- Confidence: high for the code path. Medium-high that Godot delivers unhandled input to a hidden CanvasLayer (I believe it does; not tested).

### 2. Extend mode survives Pause, and clicks on the table round the closed book buy an extension
- File: game/scripts/town/PauseMenu.cs, `if (_main.Hud.PaveMode) _main.Hud.SetPaveMode(false);` (line 158). Pause() clears Pave mode only; there is no matching call for ExtendMode.
- File: game/scripts/town/TownHud.Extend.cs, `if (_drv.Town.Extend(p) && (!mb.ShiftPressed || !AnyExtendable())) SetExtendMode(false);` (line 111). ExtendInput checks neither `Rig.InputLocked` nor `Menu.IsPaused`.
- What goes wrong: the cover Control (`_cover`, PauseMenu.cs) covers only the closed cover's rectangle. A left click outside it reaches `_UnhandledInput`, so ExtendInput calls `Town.Extend(p)` (coin is spent) while the game is paused. Extend mode also stays on after Resume.
- Trigger: open Extend mode (E or the bookmark), click the bottom-right Pause mark (TownHud.cs, `PauseMark.Pressed += () => _main.Menu.Pause();`, line 115), then click a cottage on the table beside the closed book (or a cottage visible past the cover edge). Esc is not a trigger because TakesEsc blocks it while Extend is on.
- Confidence: high for the code path. The purchase while paused depends on the cottage being under the click outside the cover; plausible at the zoom used in Cover state.

### 3. Clicks on the ledger, notes and slim strip pass through to Pave and Extend targeting; BringIntoView is not clear of the interface
- Files: game/scripts/town/TownHud.cs. Ledger card: `card.Position` ... `MouseFilter = Control.MouseFilterEnum.Ignore` (line 124). Notes: `_notes = new VBoxContainer { Name = "Notes", MouseFilter = Control.MouseFilterEnum.Ignore };` (line 101). Slips: `var slip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };` (Banner/Note, line 297). Slim strip: `_slim = new PanelContainer { Name = "Slim", MouseFilter = Control.MouseFilterEnum.Ignore, ...}` (line 237).
- Because these Controls ignore the mouse, a click on them is not consumed and reaches `PaveInput` / `ExtendInput` (TownHud.Shop.cs, lines 366-367). In Pave mode a click on a card paves the road under it (PaveTarget uses PickLocal from the screen point and ignores the UI). In Extend mode it extends the cottage under it.
- Related: TownHud.Pave.cs, `BringIntoView` says "clear of the interface" (line 115) but checks only a 40 px margin of the window (`float m = UiKit.Px(_s, 40);`, line 122). The ledger is 362 px wide (Main.cs, `float left = 362f * s`), so a suggested road can be brought into view under the ledger or notes.
- Trigger: enter Pave mode when a road runs under the ledger or the margin notes (top left), then click the card. The road is paved without a click on the map.
- Confidence: high.

### 4. The pause-time save stores the closed-book zoom and "moved" flag
- File: game/scripts/town/PauseMenu.cs, Pause(): `rig.LookAtFree(centre, size, instant: false);` ... `rig.UserMoved = true;` ... `_main.Saver.SaveNow();` (line 172) then `Now = State.Closing;` (line 173).
- File: game/scripts/Main.cs, `ViewToKeep()`: `Menu != null && Menu.IsPaused ? Menu.Kept : (Rig.PivotLocal, Rig.TargetSize, Rig.UserMoved);` (lines 143-144).
- What goes wrong: `SaveNow` runs while `Now` is still Playing, so `IsPaused` is false and the save uses the rig values. `TargetSize` was just set to the closed-book size, and `UserMoved` was set to true. The saved camera is therefore the whole-book zoom with the old pivot. A normal quit saves through `Kept` and is correct, so only a pause followed by an unclean end (crash, kill, power loss) is affected: on the next start the view opens zoomed out to the whole book, not where the player was.
- Trigger: Esc to pause, wait for the cover, kill the process. Restart and compare the view with the one before the pause.
- Fix direction (not made): set `Now = State.Closing` before SaveNow, or save `Kept`.
- Confidence: high on the ordering. Medium on the visible result (not run).

### 5. Perk notes are silenced for the rest of the session after New town
- File: game/scripts/town/TownHud.cs, `case TownEventKind.PerkEarned: if (_perksSeen.Add(e.Text ?? "")) Note("Perk: " + ...` (line 351), with `private readonly HashSet<string> _perksSeen = new();` (line 361). Nothing clears it: the only other use is the declaration. Build() and Adopt() do not clear it either.
- What goes wrong: a perk earned in one town never shows its note again in a later town started by New town (Main.NewTown, or DeleteSave). Each perk's note is lost; the perk itself still works.
- Trigger: earn a perk, start a New town, earn the same perk.
- Confidence: high.

### 6. A damaged-looking read moves a good save aside; a failed save is silent
- File: game/scripts/town/TownSaver.cs, `string text = FileAccess.GetFileAsString(SavePath);` (line 111) followed by `catch (Exception) { SetAside = ...; DirAccess.RenameAbsolute(SavePath, SetAside); ...}` (lines 122-126). `FileAccess.GetFileAsString` returns an empty string on a read error (it does not throw), so a transient read failure gives `Deserialize("")`, an exception, and the good file is renamed aside and a new town starts. The file is kept, not lost, but the player sees a new town.
- File: game/scripts/town/TownSaver.cs, `if (DirAccess.RenameAbsolute(tmp, SavePath) != Error.Ok) return false;` (line 89). A failed rename returns false with no note, leaves `town.json.tmp` behind, and the autosave silently does nothing. I could not confirm from here whether Godot's rename overwrites an existing target on Windows; if it does not, every save after the first would fail this way on that platform.
- Also (line 119-120): `_main.RestoreView(...)` runs after `_main.Driver.Adopt(town)`; an exception in RestoreView takes the catch path and renames a valid save aside although the town was already adopted.
- Confidence: medium (the GetFileAsString behaviour and Windows rename semantics are from memory of Godot 4, not checked).

### 7. Hint marker keeps the previous town's indices for up to 4 s after New town
- File: game/scripts/town/TownHud.Request.cs, `if (h.Length > 0 && h == _hintShown) { ...; _hintRoad = road; _hintPlot = plot; }` (line 156) and `if (_marker == null) return;` ... `_marker.Visible = on;` (lines 165-167) with `at = PlotCentre(t.Plan.Plots[_hintPlot])` (line 176) and `var rd = t.Plan.Roads[_hintRoad];` (line 172).
- What goes wrong: `_hintRoad` and `_hintPlot` are only refreshed when the hint text is unchanged or changes after HintHold (4 s). After New town, the old indices can be used against the new plan for up to 4 s. If a new plan has fewer roads or plots, that is an IndexOutOfRangeException in `_process`; otherwise the marker sits on the wrong place.
- Trigger: have a hint showing, start New town within 4 s of the hint last changing.
- Confidence: low-medium (the exception depends on the plan sizes, not checked).

### 8. A click that cancels key capture also reaches the GUI
- File: game/scripts/settings/SettingsPages.cs, `else if (e is InputEventMouseButton mb && mb.Pressed) CancelCapture();` (line 340). This branch does not call `SetInputAsHandled()`, unlike the key branches.
- What goes wrong: the same press goes on to the Button under the pointer. Clicking the slot being captured starts a new capture of it, and clicking another button also activates it.
- Confidence: medium.

### 9. Game time is lost on slow frames at high speed
- File: game/scripts/town/TownDriver.cs, `_acc += Math.Min(delta, 0.25) * Speed;` (line 84) and `if (steps == MaxStepsPerFrame) _acc = 0;` (line 92). A frame longer than 0.25 s counts as 0.25 s, and a frame that hits the 200-step cap discards the rest of the accumulator. Game progress per real second therefore depends on the frame rate once frames are slow (busy machine), and the amount depends on Speed. At Speed 1 the effect is only on long hitches. Tests run at Speed up to 64 and are the case that is most affected.
- Confidence: medium that the result depends on frame time (by design, but it is not the same as a fixed-step result). Impact on normal play is low.

### 10. Notes and banner logs grow without the cap used for notes
- File: game/scripts/town/TownHud.Banner.cs, `BannerLog.Add(line); NoteLog.Add(line);` (lines 49-50), and `public readonly System.Collections.Generic.List<string> BannerLog = new();` (line 20). `NoteLog` is trimmed to 200 only in `Note()` (TownHud.cs, `if (NoteLog.Count > 200) NoteLog.RemoveAt(0);`, line 314). Banners bypass that trim, and `BannerLog` has no cap.
- What goes wrong: growth only per level and per charter (a few entries per game), so this is minor memory growth, and the trimmed list loses its "last 200" meaning.
- Confidence: high that it grows; low impact.

### 11. A request flash from the old town carries into the new one
- File: game/scripts/town/TownHud.Request.cs, `_reqFlash` (lines 98, 100, 111). New town (Main.NewTown, Driver.Reset) does not reset `_reqFlash`, so a "Done" or "Time ran out" card can show for up to 1.5 s in the new town, and `RequestFrame` shows it with the old text.
- Confidence: medium (not run).

## Checked and found no problem (short)
- Main._Process level-gift loop (Main.cs) reads Driver.Events, which Driver clears each frame: no replay.
- TownSaver.TryContinue drains load events, so a loaded town does not replay gift notes.
- Pave click does not consume the mouse-up, by design (the camera needs it); the comment in TownHud.Pave.cs explains it.
- Audio players are created once in Init (TownAudio.cs `Player(...)`), so no growth there.
- Floater ring buffer (TownHud.cs, `MaxFloaters`) is bounded.
- Esc order: PauseMenu's `_UnhandledInput` sees Esc first (added after the HUD), and it leaves Esc to the HUD while TakesEsc holds; no double handling found.
- Settings: Captured() handles key conflicts through Store.FindKey; the capture path consumes all keys.
- PauseMenu state changes (Closing, Cover, Asking, Opening) match the Keep-this and New-town flow as read; the only gap is finding 1 and 2.

Not read in depth (time box): CameraRig.cs (except input and LookAt), BookBuilder.cs, TownSummary.cs, GameSettings.cs, SettingsPages.KeysSaving.cs, SettingsPages.SoundDisplay.cs, DisplaySeam.cs, TownHud.Book.cs beyond the achievements and age pages.

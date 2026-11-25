# Round 2 part 2: menus, settings, saving

Started 2026-11-25 22:01 UTC. Time box about 40 minutes.

## Done
- 22:06 code written, builds: game/scripts/town/TownSaver.cs (save/load/autosave, ISaveActions), BookCover.cs (3D front
  board swing), PauseMenu.cs (cover menu, question, settings host); Main joins SettingsRuntime (Profile folder),
  continues the save, close button saves; CameraRig.InputLocked; HUD scale from window height, coin numbers setting,
  PauseMark button, Esc via tt_pause; TownAudio: ui/book sounds, Still Pages while paused, no bus creation of its own.
- tests/TownTestBase: each test uses user://tests/<marker>/ (wiped first).

## Next
- run existing tests; write tests/MenuTest.cs (menu.tscn), SaveTest.cs (save.tscn); screenshots; check.sh steps.
- 22:2x tests written and passing alone: tests/MenuTest.cs + menu.tscn (MENU PASS), tests/SaveTest.cs + save.tscn
  (SAVE PASS). Screenshot.cs: menu shots (book-half-closed, cover-menu, settings-{sound,display,keys,saving},
  new-town-question); `-- --menu-only` runs only those (quick look). Looked at them: cover reads as this book's cover.
- check.sh: game-settings, game-menu, game-save steps added.
- Main.KeepWindow (tests): the stored window size is not applied, so --resolution 1920x1080 holds.

- 22:31 first full ./check --export PASSED (all 16 steps). AGENTS.md and README.md updated.
- After it: MenuTest gained "Esc in Pave mode only leaves Pave mode" (first try failed because the test had no
  pavable road; the bot now plays until one exists). Final ./check --export re-run 22:35-22:44: CHECK PASSED (16 steps). FINISHED.

## Decisions
- The cover lettering is 2D (gilt Labels/Buttons, tooled frame drawn) laid over the 3D board's screen rect each frame;
  the 3D board (BookCover) carries gilt rules and an endpaper. Closing hides the left page and the left half of the
  base board at once (the front board lies there, endpaper up) and swings it over the spine; the town's models hide
  once the board is 80 % down (trees stood through it).
- Camera for the closed book uses CameraRig.LookAtFree (the pan limits would centre the open book instead).
- Save file: user://town.json = { Game: 1, CamX, CamZ, CamSize, UserMoved, Town: TownSave json }, written via .tmp +
  rename. Events queued by TownSave.Load are dropped (a loaded level is not news; it reset the follow camera).
- New town seed: System.Random, never equal to the current seed. Esc on the question keeps the town; focus starts on
  "Keep this town".
- Pause music: the rotation (the theme now and then) waits; "Still Pages" loops on the Music bus while paused.
- Tests: Main.Profile = user://tests/<marker>/ (wiped first) and Main.KeepWindow.

## Known gaps
- HUD labels still print default keys ("[Space]", "[4 or P]", "Pause [Esc]") after a rebind.
- "The view follows the town" is obeyed (Main._Process) but no test asserts it.
- The interface-size 3D cover rect assumes Camera3D.UnprojectPosition is in visible-rect units (seen right at 100 %;
  not screenshotted at 150 %).

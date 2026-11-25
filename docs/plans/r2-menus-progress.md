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

## Next
- full ./check --export; AGENTS.md, README.md notes.

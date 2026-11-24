# game: the Godot project

Purpose: rendering, camera, UI and audio. Godot 4.7 .NET, C# only, Mobile renderer. The simulation lives in `../sim` (project reference), so the game never owns game rules.

| Path | What it holds |
| --- | --- |
| `project.godot`, `TideToll.csproj`, `TideToll.sln` | project files (the `.sln` is needed for exports) |
| `main.tscn`, `scripts/Main.cs` | main scene: the open book on a table, built in code |
| `scripts/BookBuilder.cs` | the book, pages, map plane (one vertex per cell corner) and ink frame |
| `scripts/CameraRig.cs` | pivot, yaw, pitch, orthographic camera; locked top-down for now |
| `scripts/Hud.cs`, `scripts/Palette.cs` | title card and fps label; the Living Map palette |
| `tests/smoke.tscn` | headless smoke test (120 frames, checks nodes and calls the sim) |
| `tests/screenshot.tscn` | saves a PNG of the main scene (needs a window; xvfb on Linux) |
| `export_presets.cfg` | Windows Desktop preset (`tools/export-windows.sh`) |
| `assets/` | generated copy of `../assets` (`tools/sync-assets.sh`); never edit, gitignored |

Run from the repository root: `./check`, or `godot --path game` to play.

# game: the Godot project

Purpose: rendering, camera, interface and sound for the incremental town. Godot 4.7 .NET, C# only, Mobile renderer. The rules live in `../sim/Town` (project reference, always built optimised; interface in `docs/plans/town-sim-api.md`): the game reads the town's views and changes it only through the player actions.

## Files

| Path | What it holds |
| --- | --- |
| `main.tscn`, `scripts/Main.cs` | the main scene: the book on the table, the town on the right page, the camera following the town's extent until the player moves it (a gift makes it follow again; Home too) |
| `scripts/town/TownDriver.cs` | owns the `Town`; fixed steps at `TownRules.TickHz` from an accumulator, interpolation `Alpha`, `Speed` multiplier, pause; drains events once a frame into `Events` |
| `scripts/town/TownGround.cs` | the ground plane with `shaders/ground.gdshader`: roads painted into the wear layer (a new road draws itself in over 1.6 s as a faint trail and firms up as it wears in), gravel and cobble strips along the road lines, a warm wash on crowded roads |
| `scripts/town/TownBuildings.cs` | plots with the Hamlet models (cottages, well, fields, market stall, mill with turning sails, bell post with a swinging bell, charter stone); gifts pop in; the next level's plots and unbought offers are staked out (`plot-stakes.glb`); named stand-ins when a model file is missing |
| `scripts/town/TownVillagers.cs` | villagers (one MultiMesh) at interpolated, eased positions facing the way they walk, with their load (water yoke, grain or flour sack) |
| `scripts/town/TownTrees.cs` | seeded trees round the town, never in the town's window or on water |
| `scripts/town/TownHud*.cs` | the interface: ledger card (coin, coin a minute, level bar, renown, next gift), margin notes, floating "+N", upgrade bookmarks with key, price and Buy or the reason, the bell, Pave mode (lit roads, hover price, click to pave, the busiest suggested), the next age button |
| `scripts/town/TownAudio.cs` | coin, purchase, level, bell and step sounds, the river loop and the theme, from the existing files |
| `scripts/BookBuilder.cs`, `scripts/CameraRig.cs`, `scripts/InkOutline.cs`, `scripts/ModelLib.cs`, `scripts/Palette.cs`, `scripts/UiKit.cs`, `scripts/GravelStrip.cs` | shared pieces kept from the first game |
| `scripts/settings/` | the settings pages (finished, not joined to the game yet: round 2) |
| `tests/` | `smoke`, `town`, `perf`, `screenshot` scenes on `TownTestBase`; `TestInput.cs`; the settings tests |
| `legacy/` | the retired desire-line game (two chapters, tools); `.gdignore` and `<Compile Remove>` keep it out |

## Keys

1 villager, 2 pace, 3 load, 4 or P Pave mode, 5 building, Space bell, N next age (after the charter), Esc leaves Pave mode, WASD or arrows move, wheel or + and - zoom, Home follows the town. The keys are the InputMap actions of `scripts/settings/GameInput.cs`.

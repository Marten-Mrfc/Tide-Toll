# Running Tide & Toll on Windows

There are two ways: run the exported build (a few minutes, nothing to install), or open the project in the Godot editor (about 15 minutes, most of it downloads). Then go through "What to check on your PC" at the end.

## A. Run the exported build

The build is made on Linux by `tools/export-windows.sh` (or `./check --export`) into `build/windows/`. It is not zipped. Copy the whole folder to the PC (a USB stick or a shared folder); it holds:

| File | Size (this build) | What it is |
| --- | --- | --- |
| `TideToll.exe` | 105 MB | the game (Godot 4.7 .NET runtime) |
| `TideToll.pck` | 3.5 MB | the scenes, shaders, models, textures, icons, fonts and sounds (with the river set) |
| `data_TideToll_windows_x86_64\` | 78 MB, 187 files | the game's C# code and the .NET runtime it needs |

Keep the three side by side and double-click `TideToll.exe`. Nothing needs installing: the .NET runtime ships inside the data folder. Windows SmartScreen may warn that the app is unrecognised (it is not signed): choose **More info**, then **Run anyway**. If it closes at once, run it from PowerShell (`.\TideToll.exe`) to see the message, and check that the data folder is next to the `.exe`.

To see frame times, run `.\TideToll.exe --print-fps` from PowerShell; the game also shows fps and simulation ms per tick in the bottom-right corner.

## B. Open the project in the Godot editor

### 1. Install the .NET 8 SDK

1. Open https://dotnet.microsoft.com/download/dotnet/8.0 and download the **SDK** installer for **Windows x64** (not the runtime).
2. Run it and accept the defaults.
3. Open a new PowerShell window and run `dotnet --list-sdks`. You should see a line starting with `8.0`.

(Or, in PowerShell: `winget install Microsoft.DotNet.SDK.8`.)

### 2. Install Godot 4.7 .NET

1. Open https://github.com/godotengine/godot/releases/tag/4.7-stable and download `Godot_v4.7-stable_mono_win64.zip`. It must be the **mono** (.NET) build; the plain one cannot run C#.
2. Unzip it somewhere permanent, for example `C:\Tools\Godot_v4.7-stable_mono_win64\`. Keep the `GodotSharp` folder next to the `.exe`.
3. Optional: pin `Godot_v4.7-stable_mono_win64.exe` to Start.

### 3. Get the code

Install Git for Windows (https://git-scm.com/download/win) if you have not, then in PowerShell:

```
git clone https://github.com/Marten-Mrfc/Tide-Toll.git
cd Tide-Toll
git checkout <the claude/ branch named in the morning report>
```

### 4. Copy the assets into the Godot project

Godot only sees files inside `game\`, so the shipping assets are copied there. From the repository folder in PowerShell:

```
foreach ($d in "models","textures","icons","fonts","audio") { if (Test-Path "assets\$d") { New-Item -ItemType Directory -Force "game\assets\$d" | Out-Null; Copy-Item -Recurse -Force "assets\$d\*" "game\assets\$d\" } }
```

(Git Bash users can run `tools/sync-assets.sh` instead.) Re-run it after pulling new assets. The game needs them: without this step the map has no models, the interface no icons and there is no sound.

### 5. Open and play

1. Start Godot. In the Project Manager choose **Import**, browse to the `game` folder of the repository and pick `project.godot`, then **Import and Edit**.
2. The first open imports files and registers Godot's own NuGet packages. Wait until the progress bar at the bottom finishes.
3. Press **Build** (the hammer, top right) once, then **Play** (F5).

You should see an open book on a table: the chapter page on the left (how to play, the village facts, path health), the hamlet's map on the right with villagers walking, and paper bookmarks on the right edge for time, tools and views. Press Home to see the whole book.

To build the Windows export yourself from the editor: **Project > Export**, pick **Windows Desktop**, **Export Project** (the export templates for 4.7 .NET must be installed: **Editor > Manage Export Templates > Download**).

### 6. Run the checks (optional)

The automated checks run in Git Bash with `./check`. On Windows set the Godot path first:

```
export GODOT="/c/Tools/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64_console.exe"
./check --no-screenshot
```

The screenshot steps need a Linux virtual display, so skip them on Windows; the rest runs, including the tools, interface and performance tests (which play no sound). Simulation tests alone: `dotnet test TideToll.sln`.

## If something goes wrong

- **"The SDK 'Godot.NET.Sdk/4.7.0' specified could not be found"**: open the project once in the Godot editor (step 5), which registers the package folder, then build again.
- **Scripts do nothing or "Cannot instantiate C# script"**: press Build in the editor, or run `dotnet build game\TideToll.csproj`.
- **Wrong Godot**: Help > About must say `4.7.stable.mono`.

## What to check on your PC

The Linux machine that builds the game has no graphics card and no speakers: it renders in software at a few frames a second and its audio is a dummy. These are the things only your PC can answer. Use the exported build or the editor's Play; note the answers (and a screenshot, F12 is not bound yet: use Win+Shift+S).

1. **60 fps with 500 walkers.** Press Home to get the map page, drag the **Walkers** slider (bottom bookmark) to 500, press 4 for 8x and wait two in-game days. Read the corner: it should say about 60 fps (or your monitor's refresh rate) and the sim a few ms per tick. Then try 1x, the whole-book view (Home), the wear overlay (W) and a fence drag with the flow preview. Note the lowest fps you see. (The automated stand-in measured under 2 ms of CPU per frame at 8x; the graphics side is unmeasured.)
2. **Sharp text.** At your normal window size, and full screen if you can (resize the window: the interface rebuilds at the new size), the chapter page, the bookmarks, tooltips (hover a bookmark) and the preview readout by the pointer should be crisp, not blurry or jagged. The map title and folios are on the 3D page and soften when zoomed out; that is expected.
3. **Sound plays.** Sound is on by default. You should hear the river (louder when zoomed in near it, from the side it is on), soft footsteps where crowds walk (grass or dirt), a small bell at each dawn (the chapter page says "Dawn bell" at the same moment), a wooden knock when a fence or bridge is placed, a pull and rustle on erase, a low muted knock when you click where no footbridge fits, and now and then the theme, quietly, with minutes of silence between. M or the Sound bookmark mutes everything; the Volume slider sets the level. Tell us if anything is too loud, too busy or missing.
4. **Mouse and keys.** Fence (F): drag on the map, release to place. Footbridge (B), Ferry (Y), Drawbridge (D): hover the river, click. Erase (E): click or drag over them. Esc or a right click puts the tool down. Space pauses, 1 to 4 set the speed, the wheel zooms at the cursor, the arrows or a middle drag pan. Tab moves a sky-blue focus frame over the bookmarks, Enter presses one.
5. **The river (Prototype 1, step 2).** Boats now come up the river from the south edge to the landing (the jetty by the market) and buy grain the villagers carry from the field. Things to try:
   - **Tell the boats apart.** A rowboat is short with no mast; a barge is twice as long with a white sail cross-bar and a pennant, and carries cargo once it has bought grain. Each leaves a pale V-shaped wake.
   - **Build each crossing** from the bookmarks, each showing its price: Footbridge (B, 50c), Ferry (Y, 40c), Drawbridge (D, 240c). Hover the river to see the cells and the flow preview ("Blocks barges: about N boats a day will turn back"); a cross and a reason in words where it cannot go or cannot be paid for. The price on a bookmark goes faint with "short" when the purse is too small. You start with 120c, so the drawbridge has to be earned.
   - **A footbridge below the landing** stops barges: the barge waits with a patience ring (an hourglass inside a blue arc that runs down and turns orange), "Mast too tall" floats up, and when the ring runs out it turns back: a vermillion diamond on the water, "Turned back", and a margin note on the chapter page. The ledger's reputation drops.
   - **Work the drawbridge by hand.** With no tool in hand, click the bridge to select it (its badge gets a blue frame), then click it again, press L, or click Raise on its badge. The leaf rises, the signal disc's arm stands up (blue: the boats' turn), the barriers drop and walkers bunch on both banks; it lowers itself once the barges are through (or L / Lower). Ctrl+1 to 9 work drawbridge 1 to 9 without selecting it. After a raise, "+N boats through; M walkers waited" floats up. Is it fun for a few minutes before you want help?
   - **Hire the keeper** (K, or Keeper on the badge, 30c): the keeper's hut and keeper appear, and the bridge raises itself for each barge ("The keeper raised the bridge for a barge").
   - **The ferry**: the raft slides along its rope six walkers at a time; walkers queue at the stages. Every boat passes.
   - **Erase** (E) takes a crossing down for half its price; the hover says what it costs.
   - **The chapter page's ledger**: coin, income today, reputation and boats served and turned back today, with the last few events as margin notes. Coins float up by the landing when a boat pays.
   - **Sound**: the drawbridge's winch and creak, a hand bell (rowboat) or horn (barge) when a boat is held or reaches the landing, coins, the ferry's rope, oars near rowboats when zoomed in, and a dull knock when a boat turns back. Tell us if any of them is too loud, too busy or does not fit.
   - `-- --no-river` on the command line plays without the river rules (Prototype 1 as before).
6. **Anything odd**: flicker, missing icons or models, a stutter when the preview computes, text cut off at your window size.

# Running Tide & Toll on Windows

There are two ways: run the exported build (a few minutes, nothing to install), or open the project in the Godot editor (about 15 minutes, most of it downloads). Then go through "What to check on your PC" at the end.

## A. Run the exported build

### Download it from GitHub

GitHub builds the Windows 64-bit game (Windows 10 and 11) by itself: the workflow `.github/workflows/windows-build.yml` runs on every push to `main` or a `claude/` branch that touches the game, the simulation, the assets or the tools. Work-in-progress commits (message starting with "Snapshot") are skipped.

1. Open https://github.com/Marten-Mrfc/Tide-Toll/actions/workflows/windows-build.yml (sign in to GitHub first).
2. Click the newest run with a green tick.
3. At the bottom of the run page, under **Artifacts**, click **TideToll-windows-x64** to download a zip (about 70 MB). GitHub keeps it for 30 days.
4. Unzip all of it into one folder and double-click `TideToll.exe`. A `README.txt` in the zip says which commit it was built from.

Once the workflow file is on the default branch, the same page also has a **Run workflow** button to build any branch by hand. A private repository uses the account's free Actions minutes: about 4 minutes a run once the Godot download is cached.

### Or build it yourself on Linux

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
   - **Tell the boats apart at the default zoom** (Home for the map page, no zooming). Boats are drawn larger there, as villagers are: a rowboat is short with no mast; a barge is about twice as long with a white sail cross-bar, and carries cargo once it has bought grain. Each under way leaves a pale V-shaped wake. Without zooming in, can you tell which boats are barges, and that a barge is waiting (a round patience ring beside it)?
   - **Tell the three crossings apart at the default zoom.** The ferry: a dotted rope between two posts with the square raft on it. The footbridge: a plank deck with a rail each side. The drawbridge: a plank deck with a dark gantry across it at the west end and a round signal disc beside it (bar flat: walkers' turn; bar upright and blue: boats' turn); while it is raised, the part over the water opens. Could someone who has never seen the game name each one?
   - **Build each crossing** from the bookmarks, each showing its price: Footbridge (B), Ferry (Y), Drawbridge (D). Hover the river to see the cells and the flow preview ("Blocks barges: about N boats a day will turn back"); a cross and a reason in words where it cannot go or cannot be paid for. The price on a bookmark goes faint with "short" when the purse is too small. The drawbridge has to be earned.
   - **A footbridge below the landing** stops barges: the barge waits with a patience ring (an hourglass inside a blue arc that runs down and turns orange), "Mast too tall" floats up, and when the ring runs out it turns back: a vermillion diamond on the water, "Turned back", and a margin note on the chapter page. The ledger's reputation drops.
   - **Work the drawbridge by hand.** With no tool in hand, click the bridge to select it (its badge gets a blue frame), then click it again, press L, or click Raise on its badge. The leaf rises, the signal disc's arm stands up (blue: the boats' turn), the barriers drop and walkers bunch on both banks; it lowers itself once the barges are through (or L / Lower). Ctrl+1 to 9 work drawbridge 1 to 9 without selecting it. After a raise, "+N boats through; M walkers waited" floats up. Is it fun for a few minutes before you want help?
   - **Hire the keeper** (K, or Keeper on the badge; its price is on the button): the keeper's hut and keeper appear, and the bridge raises itself for each barge ("The keeper raised the bridge for a barge").
   - **The ferry**: the raft slides along its rope six walkers at a time; walkers queue at the stages. Every boat passes.
   - **Erase** (E) takes a crossing down for half its price; the hover says what it costs.
   - **The chapter page's ledger**: coin, income today, reputation, boats served and turned back today, the river ("Open water", "Spring flood, day N", "Mud on the banks", "Frozen since day N"), grain brought to the landing today and dropped on the way, and who waited to cross today ("drawbridge 3 of 40": 3 of the 40 who crossed there waited), with the last few events as margin notes. Coins float up by the landing when a boat pays; notes float up beside the river, never over a boat or a crossing.
   - **The spring flood.** The game starts on the first day of spring, and most towns flood then (or wait for the next spring: a year is 16 in-game days). The water spreads over the band beside the river over a couple of seconds as a pale wash; the ford gets a vermillion bar and a "Ford closed" tag (at every zoom); a margin note and the ledger's "Spring flood, day N" say so; villagers wade the band slowly and use the bridges and the ferry. Hover the footbridge tool next to the ford: the reason says the ford is closed by the flood. After a day or two the water goes down, the tag goes, and worn paths in the band turn to mud until summer. Does the flood read at a glance? Is the wash too faint or too strong?
   - **Winter ice.** In winter the river may freeze (about one winter in two): the water goes pale and still with ink cracks, boats stop where they are, villagers walk straight across, and their paths show as whiter scratched trails on the ice; the river's sound drops almost to nothing. At the first day of spring the ice breaks up and the paths on it vanish. Do the ice paths read as paths? Is the silence welcome or odd?
   - **Sound**: the drawbridge's winch and creak, a hand bell (rowboat) or horn (barge) when a boat is held or reaches the landing, coins, the ferry's rope, oars near rowboats when zoomed in, and a dull knock when a boat turns back. Tell us if any of them is too loud, too busy or does not fit.
   - `-- --no-river` on the command line plays without the river rules (Prototype 1 as before).
6. **Anything odd**: flicker, missing icons or models, a stutter when the preview computes, text cut off at your window size, a bridge's badge covering a place name or the bridge, notes stacking over boats.

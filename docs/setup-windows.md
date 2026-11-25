# Running Tide & Toll on Windows

There are two ways: run the exported build (a few minutes, nothing to install), or open the project in the Godot editor (about 15 minutes, most of it downloads). Then go through "What to check on your PC" at the end.

## A. Run the exported build

### Download it from GitHub

GitHub builds the Windows 64-bit game (Windows 10 and 11) by itself: the workflow `.github/workflows/windows-build.yml` runs on every push to `main` or a `claude/` branch, except work-in-progress commits (message starting with "Snapshot"), which are skipped.

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
5. **Two chapters.** The game is a book and opens on **Chapter I, "The Hamlet"**: Prototype 1 as the browser toy plays it, paths only, no boats and no coin. Turn the page with the brick-red ribbon at the top of the bookmarks ("Chapter II ›") or the C key: a leaf turns over the book and **Chapter II, "The River"** opens on a fresh town from the same seed (the ribbon then says "‹ Chapter I"). Does the page turn read as turning a page? Is the ribbon easy to find?
   - **Chapter I.** Fence (F), Gate (T: hover a fence and click; a staircase corner takes a two-cell gate), Gravel (G: brush a strip, it snaps to a trail), Footbridge (B, free here), Erase (E). Rain now and then darkens the map and turns worn trails to mud for a day or two (streaks over the page, a soft rain sound, wetter footsteps; the chapter page says "Rain, day N", "Mud until day N" or "Dry"). New cottages rise beside busy trails with two red rings and a badge; the page says "Houses: N, newest on day N". Does the town feel alive? Do you notice the mud and steer round it?
6. **Chapter II: the river.** Boats come up the river from the south edge to the landing (the jetty by the market) and buy grain the villagers carry from the field. Fences, gates, gravel and erase cost nothing here; the crossings cost coin. Things to try:
   - **The opening purse.** You start with a small purse (50c with today's rules). The crossing bookmarks say at once what it can buy ("40c · can buy" in ink) and what is still to save ("200c · save 150c", fainter). Is it clear in the first minute what you can buy? Does anything look broken or disabled while the purse is short?
   - **The ford costs trade.** With no crossing, villagers wade the ford. Grain carried through it gets wet: watch a villager wading with a sack on the head (zoom in on the ford): a wet sack is darker and runs with water. At the landing a note floats up, "wet grain: -3c", and the chapter page's line "The ford" says how many sacks came wet today and what the ford has cost in all. The Ford tag on the map says "free · wet grain sells at half price". Without being told, would you connect the ford with lost trade?
   - **The flood line.** A faint dotted line runs along both banks from the first day: where the spring flood will reach. Do you notice it? Does it read as a flood line?
   - **Tell the boats apart at the default zoom** (Home for the map page). A rowboat is short with no mast; a barge is about twice as long with a white sail cross-bar. Each under way leaves a pale wake. Can you tell a waiting barge (a round patience ring beside it) without zooming in? The Ford tag gets out of the way while a boat passes under it.
   - **Tell the three crossings apart at the default zoom.** The ferry: a dotted rope between two posts with the square raft on it. The footbridge: a plank deck with a rail each side. The drawbridge: a deck with a dark gantry and a round signal disc (bar flat: walkers' turn; upright and blue: boats' turn).
   - **Build each crossing** from the bookmarks: Footbridge (B), Ferry (Y), Drawbridge (D). Hover the river for the cells and the flow preview; a cross and a reason in words where it cannot go or cannot be paid for.
   - **A footbridge below the landing** stops barges: the barge waits with a patience ring, "Mast too tall" floats up, and when the ring runs out it turns back ("Turned back", a margin note; several at once become one note, "Turned back ×3"). Queued barges lie nose to tail or rafted side by side, never on top of each other.
   - **Work the drawbridge by hand** (click it to select, click again, L, or Raise on its badge; Ctrl+1 to 9). The badge says how many more raises by hand it takes to earn a keeper ("Raise by hand 10 more times to earn a keeper"); the Keeper button stays locked until then and says why. At the last raise a bell rings, a note floats up, the button glows and the chapter page says a keeper will come. Then hire it (K or the button; price on the button): the hut appears and the bridge raises itself for each barge. Does earning the keeper feel like a reward, or a chore? (About ten raises: roughly a raise an in-game day.)
   - **The ferry**: the raft carries five walkers at a time; walkers queue at the stages; every boat passes.
   - **The chapter page.** Chapter II's How to play explains what boats want, what each crossing does to boats and to walkers, and what the ford costs. The ledger: coin, income today, reputation, boats today, the river's state, grain today, the ford's cost, who waited to cross. Fast-forward (4 for 8x) over a dawn: the "today" lines start again at that dawn.
   - **The spring flood and winter ice.** As before: the wash over the band to the dotted line, the ford closed ("Ford closed" tag), mud after it; in winter the river may freeze and villagers walk across.
   - **Sound**: the winch, bells and horns, coins, the ferry's rope, oars, the turn-away knock, and a higher bell when a keeper is earned.
   - `-- --chapter 2` on the command line opens on Chapter II; `-- --no-river` (or `-- --chapter 1`) on Chapter I.
7. **Anything odd**: flicker, missing icons or models, a stutter when the preview computes, text cut off at your window size, a bridge's badge covering a place name or the bridge, notes stacking over boats.

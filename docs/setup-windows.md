# Running Tide & Toll on Windows

These steps open the Godot project and press Play. They take about 15 minutes, most of it downloads.

## 1. Install the .NET 8 SDK

1. Open https://dotnet.microsoft.com/download/dotnet/8.0 and download the **SDK** installer for **Windows x64** (not the runtime).
2. Run it and accept the defaults.
3. Open a new PowerShell window and run `dotnet --list-sdks`. You should see a line starting with `8.0`.

(Or, in PowerShell: `winget install Microsoft.DotNet.SDK.8`.)

## 2. Install Godot 4.7 .NET

1. Open https://github.com/godotengine/godot/releases/tag/4.7-stable and download `Godot_v4.7-stable_mono_win64.zip`. It must be the **mono** (.NET) build; the plain one cannot run C#.
2. Unzip it somewhere permanent, for example `C:\Tools\Godot_v4.7-stable_mono_win64\`. Keep the `GodotSharp` folder next to the `.exe`.
3. Optional: pin `Godot_v4.7-stable_mono_win64.exe` to Start.

## 3. Get the code

Install Git for Windows (https://git-scm.com/download/win) if you have not, then in PowerShell:

```
git clone https://github.com/Marten-Mrfc/Tide-Toll.git
cd Tide-Toll
git checkout <the claude/ branch named in the morning report>
```

## 4. Copy the assets into the Godot project

Godot only sees files inside `game\`, so the shipping assets are copied there. From the repository folder in PowerShell:

```
foreach ($d in "models","textures","icons","fonts","audio") { if (Test-Path "assets\$d") { New-Item -ItemType Directory -Force "game\assets\$d" | Out-Null; Copy-Item -Recurse -Force "assets\$d\*" "game\assets\$d\" } }
```

(Git Bash users can run `tools/sync-assets.sh` instead.) Re-run it after pulling new assets. In Phase 0 there are none yet, so you can skip this step for now.

## 5. Open and play

1. Start Godot. In the Project Manager choose **Import**, browse to the `game` folder of the repository and pick `project.godot`, then **Import and Edit**.
2. The first open imports files and registers Godot's own NuGet packages. Wait until the progress bar at the bottom finishes.
3. Press **Build** (the hammer, top right) once, then **Play** (F5).

You should see an open book on a table: chapter text on the left page, a blank map with an ink frame and a compass on the right.

## 6. Run the checks (optional)

The automated checks run in Git Bash with `./check`. On Windows set the Godot path first:

```
export GODOT="/c/Tools/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64_console.exe"
./check --no-screenshot
```

The screenshot step needs a Linux virtual display, so skip it on Windows; the rest runs. Simulation tests alone: `dotnet test TideToll.sln`.

## If something goes wrong

- **"The SDK 'Godot.NET.Sdk/4.7.0' specified could not be found"**: open the project once in the Godot editor (step 5), which registers the package folder, then build again.
- **Scripts do nothing or "Cannot instantiate C# script"**: press Build in the editor, or run `dotnet build game\TideToll.csproj`.
- **Wrong Godot**: Help > About must say `4.7.stable.mono`.

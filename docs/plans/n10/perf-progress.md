# n10 performance: progress (builder's notes)

Brief: measure what can be measured here, give the owner a readout to measure the rest, make the wins that do not
change the look. Research: `docs/plans/n10/performance-research.md`. Matrix tables: `docs/plans/n10/perf-measures.md`.

## Part A: the readout (F3)

**Built.** `game/scripts/PerfReadout.cs` (the box, the numbers, the clocks) and `PerfReadout.Log.cs` (the file).
Added to `Main` after the pause menu. Remembered in `settings.cfg` as `[debug] readout=true|false`
(`GameSettings.ShowReadout`); F3 is its own InputMap action `tt_readout`, not in `GameInput.Actions`, so it is not
on the Keys page and cannot be rebound.

**Where it sits.** The left rail, the rail's width (R), its foot 8 units above the bell (layout sheet section 2:
the gap between the ledger's foot at y 352 and the timers at H-64). At 1920 x 1080 and 1280 x 720 (100 percent) the
box lies clear of the ledger, the villager card (y 360 to 484), the bell, the tide, the shop, navigation, the message
zone, the line and the middle; the test checks exactly that. At the smallest canvas of the sheet (1280 x 720 at
150 percent, 1094 x 615 units) it would reach up over the villager card's place when both show: it is a debugging
box, off by default.

**What the owner does.**
1. Start the game, press **F3**. A small paper box appears at the left, above the bell. F3 again hides it; the game
   remembers whether it shows.
2. Play as usual where it feels slow, for a minute.
3. To send us the numbers: **hold F3 for three seconds**. The box says "recording 0 of 60 s"; after a minute it says
   "saved perf-readout.csv". The file is in `%APPDATA%\Godot\app_userdata\Tide & Toll\perf-readout.csv` (paste that
   into the Explorer's address bar). Or start the game with `-- --perf-log` (e.g. a shortcut's target
   `"Tide & Toll.exe" -- --perf-log`) and it records the first minute. Send the file (and a photo of the box).

**What each number means** (one line each):

| Shown | Meaning |
| --- | --- |
| fps | frames drawn in the last second (60 is smooth on a 60 Hz screen) |
| frame ms | average real time from one frame to the next over the last 120 frames (16.7 = 60 fps) |
| 95th / worst | the slowest 5 percent of those frames, and the single slowest: stutters show here, not in the average |
| sim | milliseconds a frame the town's simulation took |
| interface | milliseconds a frame the interface (ledger, shop, tags) took |
| scripts | milliseconds a frame all the game's own code took, the simulation and the interface included |
| drawing cpu | milliseconds a frame the engine's processor side spent preparing the picture |
| gpu | milliseconds a frame the graphics card spent drawing: if this is near or above the frame ms, the card is the limit |
| draws / objects / triangles | how much the card is asked to draw each frame |
| villagers | the town's size, to compare readings |
| W x H, vsync, cap | the window's size in pixels, whether vsync is on, the frame limit (none or 30/60/120) |
| last line | the graphics card's name and maker, the driver (vulkan) and renderer (mobile) |

The CSV has one row a second with the same numbers (plus the 3D scale), the half second before each row.

**Cost.** Hidden: the node does not process, the two probes round the interface do not process and the
simulation's timestamps are skipped (one bool test a frame), so nothing runs or is allocated for it. Shown: see the
test's result below. The text is rebuilt twice a second with one `StringBuilder` (no per-frame strings).

**Test** `game-readout` (`game/tests/ReadoutTest.cs`, `readout.tscn`, a step in `tools/check.sh` under xvfb):
(results below)

## Part B: the matrix

`game/tests/PerfDraw.cs` (`perf-draw.tscn`) and `tools/perf-matrix.sh` (one process per group, the lock taken per
group). (results in perf-measures.md)

## Part C: the wins

(below as they are proven)

## Not done / not in this round

(at the end)

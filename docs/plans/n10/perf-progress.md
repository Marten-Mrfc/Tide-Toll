# n10 performance: progress (builder's notes)

Brief: measure what can be measured here, give the owner a readout to measure the rest, make the wins that do not
change the look. Research: `docs/plans/n10/performance-research.md`. Matrix tables: `docs/plans/n10/perf-measures.md`.

## Part A: the readout (F3)

**Built.** `game/scripts/PerfReadout.cs` (the box, the numbers, the clocks) and `PerfReadout.Log.cs` (the file).
Added to `Main` after the pause menu. Remembered in `settings.cfg` as `[debug] readout=true|false`
(`GameSettings.ShowReadout`); F3 is its own InputMap action `tt_readout`, not in `GameInput.Actions`, so it is not
on the Keys page and cannot be rebound. **The file is never written in the frame of the key press** (the lead's
note at 19:00: a settings write in a frame of play is a spike): F3 changes the store in memory
(`ReadoutPending`), and `TownSaver.SaveNow` writes it with the next save on the main thread (pause, quit, new town,
the Saving page; not the background autosave). The test checks both halves.

Where the numbers come from: frame times are the real time between the readout's own frames (the engine's delta is
smoothed or capped and would hide a hitch); "sim" is a timestamp pair round `TownDriver`'s step loop; "interface" two
probe nodes placed just before and after `TownHud` in `Main`'s children; "scripts" from `TownDriver` (priority -100,
the first) to the readout (priority 100000, the last); drawing CPU and GPU are
`RenderingServer.ViewportGetMeasuredRenderTimeCpu/Gpu` with `ViewportSetMeasureRenderTime` on only while it shows;
draws, objects, triangles are `RenderingServer.GetRenderingInfo(TotalDrawCalls/Objects/PrimitivesInFrame)`; the
adapter `RenderingServer.GetVideoAdapterName/Vendor` plus the driver and method names, read once.

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

**Test** `game-readout` (`game/tests/ReadoutTest.cs`, `readout.tscn`, a step in `tools/check.sh` under xvfb,
about 2.5 minutes): the `--perf-log` argument parsed; off by default and not processing (no frame code runs: counted);
F3 shows it; the store remembers it, the file only after the next save; about two updates a second (one a frame when
a frame is longer than half a second, as under llvmpipe); every number present and sane (fps, frame mean, p95 and
worst in order, sim and interface within scripts, drawing CPU and GPU, draws > 10, objects > 10, triangles > 1000,
villagers and window equal to the game's, the adapter's name in the box); its cost; holding F3 3 s starts the file,
releasing keeps it, three rows, the box says "recording", F3 hides it and ends the file; the CSV has the header and
rows with a positive frame time; the box clear of the ledger, the villager card, bell, tide, shop, navigation, the
message zone, the line and the middle at 1280 x 720 and 1920 x 1080.

Cost, measured (llvmpipe, 1920 x 1080): the frame work without the update 5.5 us and 0 bytes a frame; one update
(text, place) 507 us and 581 bytes; so at 60 fps **22 us and 20 bytes a frame on average** (gates 50 us and 300
bytes). Under llvmpipe (2 to 7 fps) the update falls on every frame or two, so the raw average there is 0.51 ms and
581 bytes; the gate is computed for 60 fps from the two measured parts and both are printed. The update itself is
half a millisecond twice a second (most of it the label's text layout).

Seen under xvfb: `fps 2  frame 133 ms ... drawing cpu 0.42  gpu 526 ms  draws 147  objects 650  triangles 119.5k`,
adapter `llvmpipe (LLVM 20.1.2, 256 bits) (Unknown), vulkan mobile` (a new town; GPU ms on llvmpipe is the software
rasteriser's time, not a card's).

Places at 100 percent: 1280 x 720: x 12, y 492, 250 x 172 (the bell at y 670, the villager card ends at 378);
1920 x 1080: x 16, y 822, 320 x 186 (the bell at 1016, the card ends at 484).

## Part B: the matrix

`game/tests/PerfDraw.cs` (`perf-draw.tscn`) and `tools/perf-matrix.sh` (one process per group, the lock taken per
group). Tables and the three sentences: `docs/plans/n10/perf-measures.md`. In short: the ground shader is about 69
percent of a frame under llvmpipe (flat ground 0.31), shadows 17 to 19 percent, the ink within the noise; the time
follows the pixels (720p 0.51, 1440p 1.64, 3D scale 50 percent 0.37); the draws (360 and 538) do not matter.
`perf-draw -- --shots` takes the pictures for the proofs (time stopped at a fixed frame with `--fixed-fps 60`, the
animals and chimney smoke hidden because they move at random: with them two runs differed by about 800 pixels,
without them two runs are identical, 0 pixels, in all three views); `-- --gc=60` counts the collector.

## Part C: the wins

**Proof method.** `perf-draw -- --shots` before (the tree before Part C) and after, three views (the follow view,
the whole book, the closest zoom) of the end-of-age town and of the sealed river town at 1920 x 1080, compared pixel
for pixel (`build/n10/imgdiff.py`); two "before" runs first, to show the pictures are deterministic (0 pixels).

**Q5, the ink pass's depth without a matrix a tap** (`game/shaders/outline_screen.gdshader` and the canonical
`assets/src/common/outline_screen.gdshader`, identical apart from the copy's first line). `vz()` took
`inv_proj * vec4(ndc, d, 1)` (a mat4 product and a divide) for each of the 9 to 17 depth taps a pixel. The z and w
rows of a camera's inverse projection do not depend on the screen position, so the fragment takes four numbers once
(`zk(INV_PROJECTION_MATRIX)`: ip[2].z, ip[3].z, ip[2].w, ip[3].w) and each tap is `-(a d + b) / (c d + e)`: two
multiply-adds and a divide. Exact for the orthographic camera the game uses (w = 1) and for a symmetric perspective.
Proof: with Q4 below, all six views identical (0 pixels); and the asset audit's ink-check pictures (game-ink, 640 x 480 each, with and without the ink and the
ground): every one identical except the cottages and the street, where 300 to 1,300 pixels differ in a box of
about 25 x 30 pixels round a chimney, **equally in the pictures drawn without any ink**: the chimney smoke, which is
random (looked at side by side: the puff's shape differs, nothing else). game-ink passes with its ceilings
untouched; its shares move by at most 0.002 (the smoke).

**Q4, the ground shader's skips** (`game/shaders/ground.gdshader`, `game/scripts/town/TownGround.Near.cs`, two lines in
`TownGround.cs`). A "near wear" mask: an R8 128 x 128 texture, 255 where any cell within 5 cells has wear, built
from the very bytes of the wear texture each time it is uploaded (two sliding-window passes, no allocation,
uploaded only when it changes), read with nearest filtering at the pixel's cell. Every wear read of the shader lies
within 4 cells of the pixel's cell (warp under 0.66 cell, ring 2.2 cells, the bilinear footprint 1 cell), so where
the mask is 0 they all read 0 and the results are known exactly: core, spread, broad 0, stroke 0.5. Then the fringe,
bare earth, ruts, packed earth, hollow way and ramp weigh exactly 0, so the colour is the meadow's (`two(g0)`), and
the warp noise (which only moves wear, gravel and cobble lookups, all on worn cells) is not needed. Skipped there:
27 wear reads and the ring's trigonometry, 4 + 3 + 1 value noises, 8 mipmapped tile reads (g1 to g4), the ramp.
The crop block (rows, a tile read, 4 mask reads) is skipped off the crop cells (`mask.g` 0, its only weight); its tile
read takes its derivatives outside the branch (`textureGrad`). The river block (water, swirls, shallows, shimmer,
ford, ice, tidal flat, bank lip, the bank's ink line: about 7 tile reads and 4 noises) is skipped beyond 3 m plus
the ink line's width from the bank, where all of its weights are exactly 0; the derivatives it uses (`fwidth(sw)`,
`fwidth(lp.x)`) are taken before the branch. Unset, the mask reads white (`hint_default_white`): nothing skipped.
Proof: the six views before and after, **identical, 0 pixels differ** (also after each of the three steps). Gain:
under llvmpipe the ground's own cost (baseline minus flat ground) fell 11 percent in the first age's follow view and
25 percent in the river town's (the whole frame 6 and 16 percent), with a little more drawn after the merge; at
720p and 1440p the frame is 2 to 3 percent faster (the noise is about 5 percent). llvmpipe skips a branch only when
all its pixels in a block agree; a card skips per wave, so the gain there should be larger where most of the screen
is plain grass, but only the owner's readout (GPU ms at the same view before and after this build) can say how much.
The CPU price: two passes over the 16,384 cells (no allocation) per wear upload, ten a second.

**Q1, the labels registered by their owners** (`TownBuildings.Words`, `TownHud.Labels.cs` `ScanTags` and `AddTag`).
The buildings add each staked plot's `Label3D` to a list when they make it (and clear it on a rebuild); the scan reads
that list, in the same order as the tree search gave (creation order), so the tags and their priority are unchanged.
**The boats and the river stay on the tree search** (`RiverBoats.cs`, `RiverLayer.cs` are not mine this round): in
the first age all tags now come without a tree walk; in the second the river's and the boats' subtrees are still
walked twice a second. Checked by game-layout and game-town's tags (see the checks below).

**Q7, the autosave's writing off the main thread** (`TownSaver.cs`, `TownSaver.Background.cs`). The autosave makes
the town's save and the file's bytes on the main thread exactly as before (`JsonSerializer.SerializeToUtf8Bytes`,
the same bytes `StoreString(Serialize(...))` wrote), then a worker writes the `.tmp` and renames it over the save; the
main thread takes the result in a later frame (Saves, or the failure's note). Every other save (pause, quit, new
town, the Saving page), load and delete first waits for a write in flight. Test (game-save): a main-thread save and
a worker save of the same paused town are **byte for byte identical** (9,379 bytes), the reload that follows loads
the worker's file to the same town (the existing state comparison), and a worker write that fails (a folder where the
.tmp goes) reaches the main thread as a failure.

**Readout's choice written with the next save** (the lead's note): see Part A. After the main branch's merge (the
trades builder's `SaveHintsIfChanged`, hints kept in memory and written with the town's save) there is **one path**:
`GameSettings.SaveHintsIfChanged` writes whatever is kept in memory (the hints seen and the readout's F3 choice), the
file read first as before; `Save()` clears both flags. `TownSaver.SaveNow` calls it after a save on the main thread
succeeds (as the trades builder made it) and, for the autosave on a worker, on the main thread in the autosave's own
frame before the worker starts (the store belongs to the main thread; the worker only writes the town's bytes).
So a hint's frame and an F3 frame write no file; the bits are on disk after the next save of either kind.

**Q9, the achievements counted each frame** (`TownHud.Book.cs`, free after the trades round): `BookFrame` read every
achievement's value every frame (and a second-age achievement builds a string, `"river" + i`). Achievements unlock
only inside a step (`CheckAchievements`, `CheckRiverAchievements`), so the count is taken again only when the tick,
the town object or the age changes: the same number in every frame.

**Q10, the villagers' test-only measures** (`TownVillagers.Spread.cs`, free after the trades round): the neighbour
search for the standing, spot and follow gaps (5 x 5 grid cells for each standing villager) ran in the game every
frame and fed only `TownTest` and `PeopleTest`. It now runs when `TownVillagers.MeasureGaps` is set, which
`TownTestBase` sets for every test scene, so every test reads exactly what it read before; the game skips it.

## Hitches found by the same pattern (a file written or read in a frame of play)

Searched `game/scripts` for `FileAccess.Open`, `Save()`, `SetHintSeen`, `File.*`, `GD.Load`, `ResourceLoader`:

- `GameSettings.SetHintSeen` (from `TownHud.FirstMinute.cs`): reads and writes `settings.cfg` in a frame of play
  (about 30 KB that frame). Being changed by another builder this round (not touched here).
- `GameSettings.SetShowReadout` (mine): was the same pattern in the first commit; now kept in memory and written with
  the next save (above).
- Every other `GameSettings` setter (`Commit` then `Save`) writes the file at once, but they are called only from the
  settings pages, i.e. while the book is closed and the town is paused: not a frame of play. `GameSettings.Read`
  allocates the whole file's text and splits it (when the pages open and for the hints).
- The autosave (`TownSaver.SaveNow` every 2 minutes): now written on a worker (Q7). The town's serialisation
  (`TownSave.Save`, in `sim/`) and the JSON bytes are still made on the main thread in that frame.
- `TownSaver.LastSaveTime` / `HasSave` (`FileAccess.GetModifiedTime`, `FileExists`): read only by the Saving page.
- `ModelLib.Instance/Pick` (`GD.Load<PackedScene>` per model) and `ModelLib.MipTexture`: a model's scene is read
  from disk the first time a new kind appears in play (a level's new building, a boat's first arrival); later loads
  come from the resource cache. With the pipeline compile of that first sight, this is the "first time something new
  appears" hitch of the research (section 2.4); a warm-up behind the cover (Q14) would read them at start.
- `TownVillagers.cs:130` loads `villager.gdshader` (forbidden file; at start). Sounds and music load at start.

## Not done / not in this round

- **Q6, the shadow's range (700 to 350 m): left out.** Tried as a variant (`perf-draw --shot-variant=shadow350`): the
  three views are pixel-identical to 700 m, so with this orthographic camera (near 200, far 400) the shadow map
  covers the same depth either way and nothing is gained; and the book standing up for the pause (`CameraRig.SetDeep`
  widens the depth range) could lose shadows past 350 m. Nothing to win, a small risk: not changed.
- **Q12, a 60 frame cap by default: left out.** With vsync on (the default) the frame rate is already held to the
  screen's refresh; a 60 cap on a 144 Hz screen gives uneven frame pacing (60 does not divide 144), a visible
  judder, i.e. a change to how the game looks in motion; and an owner who has ever opened the settings has
  `frame_limit=0` saved in `settings.cfg`, so a new default would not reach them. What it would need: a cap equal to
  the screen's refresh rate when vsync is off, or "60 / match the screen" in the settings page (a settings-page
  change, which is the next round's), and the settings test's walk through the frame limits rewritten for the new
  default.
- **Settings entries for drawing detail (3D render scale 100/85/70) and outline quality (full/light)**: change the
  settings page (`SettingsPages.SoundDisplay.cs`, `GameSettings`, `SettingsApplier`/`DisplaySeam`), so not this round.
  The matrix says render scale is the biggest single lever (50 percent: 0.37 of the time); the outline one would gain
  little by these numbers (the ink pass is within the noise here). Needs: the two rows, the store keys, the applier
  (`Viewport.Scaling3DScale`, `InkOutline` `max_step`), settings-test rows, a picture check at 85 and 70 percent.
- **The wins in the forbidden files**: the shop list built each frame (`TownHud.Shop.cs`), the villager pick each
  frame (`TownVillagers.Pick.cs`, `TownHud.People.cs`), the bodies pass's test-only measures (`TownVillagers.Spread.cs`),
  the label layout's per-frame card rectangles (Q11, partly in `TownHud.Labels.cs` but its cards list names forbidden
  owners' controls). Each needs only its owner's file; none changes the look.
- **Q1 for the boats and the river**: `RiverBoats.cs` and `RiverLayer.cs` are forbidden, so their `Label3D`s are
  still found by `FindChildren` twice a second; the buildings' (all of the first age's tags) now come from a list.
  Needs: `RiverBoats.Build` and `RiverLayer.MakePlot` to add their words to a list as `TownBuildings.Words` does.
- **Warm-up of pipelines** (Q14) and **one MultiMesh for the chimney smoke** (Q15): not started.
- `./check` as a whole was not run (the lock); the checks run one by one are listed below.

## The checks run (branch with the main branch merged, 20:27 to 20:45 UTC, each under the lock)

Logs in `build/n10/final/`.

- `game-perf` PASS: town 28 villagers 1x mean 0.741 ms worst 2.18 (gate 6), 505 bytes/frame (gate 1536); 8x mean
  0.934 worst 2.13, 915 bytes; river 35 villagers 1x 0.848 / 3.07, 699 bytes; 8x 0.859 / 3.80, 1538 bytes (gate 6144).
- `game-motion` (tools/motion-check.sh) PASS at 60, 30 and 120 fps (cpu max 1.41, 2.79, 2.96 ms). An earlier run at
  19:52, right after my perf run and with others busy, failed on two single frames (6.01 ms at 60 fps against the 6 ms
  gate, 8.33 ms at 120); run again under the lock with nothing else running it passed.
- `game-trades` PASS at 60/30/120 (hintframe 2576 / 2696 / 2848 B: the hint's frame writes no file).
- `game-people` PASS at 60/30/120 (follow 0.12 m, the gap measures still taken for the tests).
- `game-readout` PASS: cost at 60 fps 24.0 us and 20 bytes a frame (gates 50 us, 300 bytes); csv 3 rows; boxes
  1280x720 at 12,492 250x172 and 1920x1080 at 16,822 320x186.
- `game-ink` PASS with its ceilings untouched: cottage-a0 0.168 ... mill 0.221, market-stall 0.274, well 0.240,
  street 0.231, villagers-group 0.349 (the same as before within the chimney smoke's noise).
- `game-smoke` PASS, `game-save` PASS (background save 9,946 bytes, identical), `game-settings` PASS.
- `game-layout` PASS (states 36, canvases 8, checks 923; 309 s).
- `game-town`: the first run (20:31, in the take with the others) failed once: "a hand is offered at a queued
  workplace" (TownTest.V3: the bot played four game hours without a hand offered); run twice more under the lock it
  passed both times. Its town comes from real input over frames, so the state at that point varies; nothing in this
  branch touches the simulation. Worth a look by whoever owns that check if it recurs.
- Not run: `./check` as a whole, `game-playthrough`, `game-river`, `game-book`, `game-menu`, `game-work`,
  `game-work-places`, the screenshots (the lead's whole check of the main branch ran meanwhile; it holds Part A).

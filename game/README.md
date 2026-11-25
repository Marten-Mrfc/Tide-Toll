# game: the Godot project

Purpose: rendering, camera, UI and audio. Godot 4.7 .NET, C# only, Mobile renderer. The simulation lives in `../sim` (project reference, always built optimised), so the game never owns game rules: it reads the `World` through read-only spans and changes it only through commands.

Today it plays Prototype 1 on the map page of the book: the same town as the browser toy for the same seed, villagers wearing trails into the page, the player placing and erasing fences and footbridges with a flow preview of the routes that would change, a wear overlay, pause and four speeds, reset and new map, 50 to 500 walkers, and basic sound, in the Living Map style with the Hamlet assets. The left page is the chapter page (how to play, the village facts, path health, the legend and the status line); the controls are paper bookmarks out of the map page's edge.

With the river rules on (the default; `-- --no-river` turns them off) it also plays the river prototype (Prototype 1, step 2): rowboats and masted barges on the simulation's schedule sail up to the landing, wait at crossings they cannot pass with their patience running down, and turn back when it runs out; the player buys a footbridge (50c), ferry (40c) or drawbridge (240c) from priced bookmarks, works the drawbridge by hand or hires its keeper, and the chapter page keeps the ledger and a chronicle of margin notes. See "The river" below.

## Files

| Path | What it holds |
| --- | --- |
| `project.godot`, `TideToll.csproj`, `TideToll.sln` | project files (the `.sln` is needed for exports) |
| `main.tscn`, `scripts/Main.cs` | main scene; wires the driver, the map layers, the camera, the ink pass and the HUD |
| `scripts/SimDriver.cs` | owns the `World`; fixed 10 Hz tick with an accumulator, speeds 1/2/4/8x, pause, per-frame time budget, commands |
| `scripts/MapGround.cs` | the ground plane: wear (eased) and hollow flags uploaded 5 times a second, river, ford, crop mask, wear overlay |
| `scripts/TownLayer.cs`, `shaders/building.gdshader` | buildings from the simulation's list: cottages (each with its own thatch tone), well, market stalls, field corner posts |
| `scripts/GroundMarks.cs`, `shaders/ground_mark.gdshader` | one mesh printed on the ground round the town: soft drop shadows under the cottages and the well, inked yard outlines (house plots, the well's round yard, the market square) with a gap at each door |
| `scripts/MapTags.cs` | paper tags naming the Well, Market, Field and Ford (Alegreya, an ink thread to the place); shown at overview zoom, faded out below 170 m of view, gone at 120 m |
| `scripts/Scenery.cs`, `shaders/tree.gdshader` | seeded decoration: trees in copses round the map edge (a few small copses and lone trees inside, all clear of doors, water, the ford and likely paths), reeds, rowboat, ford stones (MultiMesh per model, grown if a seed needs more); crowns shaded as round clumps |
| `scripts/EditLayer.cs`, `scripts/FenceLines.cs` | fences (wattle and posts, straightened runs) and footbridges, rebuilt on each edit |
| `scripts/WalkerLayer.cs` | every villager in one MultiMesh, interpolated between ticks, one buffer upload per frame |
| `scripts/InkOutline.cs` | the screen-space ink pass (two camera quads: normal and soft-ink stencil) |
| `scripts/CameraRig.cs` | pivot / yaw / pitch / orthographic camera: pan, zoom, limits, book and play views, picking |
| `scripts/BookBuilder.cs` | the book on the table: curved pages with gutter shading, page-stack edges, ink frame, compass, labels |
| `scripts/ModelLib.cs` | loads the .glb models as merged meshes with Living Map materials (soft ink from glTF extras), mipmapped textures |
| `scripts/ToolController.cs`, `ToolController.Ghost.cs` | the three tools from real pointer input (fence drag, footbridge hover and click, erase click and drag), cancel by Esc or right click, tool events for sound; the `.Ghost` part turns the tool state into ghost pieces and preview requests |
| `scripts/ToolTargets.cs` | read-only questions to the world: why no footbridge fits at a cell, which cells the eraser's brush would remove |
| `scripts/ToolGhost.cs`, `shaders/ghost.gdshader` | the tool ghost: one MultiMesh of cell quads (marching dotted outline = will be built, hatched with a cross = cannot, hatched = will be removed, dotted ring = eraser) |
| `scripts/FlowPreviewer.cs` | the toy's previewer: throttled requests, `FlowPredictor.Begin/Step` spread over frames in what the simulation left of a 6 ms budget (none when the ticks took over 4 ms), refresh once a second |
| `scripts/RouteLayer.cs`, `shaders/route.gdshader` | the flow preview on the map: dashed faint "now", solid sky-blue "after" with arrow heads, blocked routes dotted with a ring and cross; one mesh with pooled upload arrays, pixel widths; the dash march wraps at 45 s (a multiple of both dash periods) |
| `scripts/GameUi.cs` | the interface (node `Hud`): lays the chapter page and the tabs over the book every frame, preview readout by the pointer, the fps and ms-per-tick readout on a small ink plate 12 px in from the bottom-right corner, keyboard shortcuts, rebuilt on resize (the old root leaves the tree first) |
| `scripts/ChapterPage.cs` | the left page: title, how to play, village facts, with the river the ledger (coin, income today, reputation in words, boats served and turned back today) and four margin notes, path health with the trunk-share band gauge, ground or heat legend, status line; every line at least 12 px inside the page (it folds the key reminder, then the how-to paragraph, away when the page is too short, as at 1280 x 720); folds into a slip when zoomed in |
| `scripts/ToolTabs.cs` | the bookmark tabs: pause, 1x 2x 4x 8x, fence, footbridge, ferry and drawbridge (with the river: each with its price on a second line, faint with "short" when the purse cannot pay, the tooltip saying by how much), erase, flow preview, wear overlay, sound and volume, reset, new map, walkers 50 to 500; tucked 9 px under the right page's edge, which `PageLip` draws over them (edge rule, a stack line and the page's soft shadow), so they read as bookmarks in the book |
| `scripts/UiKit.cs` | interface look: paper and ink styles, fonts (and `PrintFont`, an MSDF copy for text printed on the book: map title in Uncial Antiqua, folios and compass in Alegreya), scale from the window height, icons |
| `scripts/RiverLayer.cs`, `shaders/river_sign.gdshader`, `shaders/wake.gdshader` | the river's traffic: boats from a pool keyed by boat id (rowboat with a grain bundle when loaded; barge hull, mast and cargo node), interpolated between ticks along the river's bend, queues drawn nose to tail, jumps (mooring, turning back) eased out; V-shaped wakes (one MultiMesh); signs drawn over everything at a fixed pixel size (one MultiMesh): the patience ring round an hourglass or a crossed-out mast, the vermillion turn-back diamond; the landing jetty |
| `scripts/CrossingLayer.cs` | ferries (posts, rope, dotted crossing line, the raft sliding with the crossing's progress) and drawbridges (lift with leaf, beam, chains and signal arm from the model's nodes, fixed spans, ramps with barriers, keeper's hut and keeper once hired, a signal disc readable from above), rebuilt on edits, animated per frame from the crossing's state |
| `scripts/ChapterPage.Ledger.cs` | the chapter page's ledger and chronicle (river rules on) |
| `scripts/RiverFeed.cs` | drains the river's events once a frame after the ticks and hands them on (`Event`); writes the notable ones as margin notes ("Day 6: A barge turned back at the footbridge: its mast is too tall.") and the drawbridge's cycle summary (`CycleNote`) |
| `scripts/BridgeBadges.cs` | a badge over every drawbridge: state glyph with progress ring and state in words, boats and walkers waiting, the lever (Raise / Lower, L) and the keeper (price, K; greyed with the shortfall; "Keeper on duty"); compact at overview zoom, hidden in the whole-book view |
| `scripts/RiverFloats.cs` | notes floating up from the map and fading: coins by the landing, "Turned back", "Mast too tall", the cycle summary, "Keeper hired" |
| `scripts/ToolController.River.cs` | the crossing tools (ghost, preview, price and reason), selecting a drawbridge, the lever, the keeper, L, K, Ctrl+1..9 |
| `scripts/AudioDirector.River.cs` | the river's sounds from the feed's events: winch and creak, boat calls, coins, ferry rope, oars, turn-away knock |
| `scripts/AudioDirector.cs` | buses, river level by view, footsteps per chunk on a beat with a 12-voice pool, dawn bell, tool sounds, the theme with long silences |
| `scripts/Palette.cs` | the Living Map palette |
| `shaders/ground.gdshader` | wear ground, river, ford, crop rows, paper grain, wear heat overlay |
| `shaders/paper.gdshader` | book paper (exactly Paper where unshadowed, with grain and baked gutter shading) |
| `shaders/walker.gdshader`, `shaders/walker_hull.gdshader`, `shaders/walker_shadow.gdshader` | villager bob and sway and outfits (six palette tunics, three hats, per villager from the instance data); thin inverted-hull ink (downward faces left out); a soft shadow disc per villager from the same instance buffer |
| `shaders/outline_screen.gdshader` | copy of `assets/src/common/outline_screen.gdshader` |
| `tests/smoke.tscn` | headless smoke test (`Smoke.cs`) |
| `tests/perf.tscn` | headless CPU-per-frame and allocation test, 500 walkers at 1x and 8x (`Perf.cs`) |
| `tests/screenshot.tscn` | rendered screenshots under xvfb (`Screenshot.cs`, river views in `Screenshot.River.cs`) |
| `tests/river.tscn` | headless river test through real input (`RiverTest.cs`) |
| `export_presets.cfg` | Windows Desktop preset (`tools/export-windows.sh`) |
| `assets/` | generated copy of `../assets` (`tools/sync-assets.sh`); never edit, gitignored |

## Scene tree (built in code at startup)

```
Main (Main.cs)
  Environment, Sun                      one sun from the south-east; linear tonemap
  Table/ Top, Seam0..10, Inkwell
  Book/
    Cover, Ribbon
    LeftPage/  Paper, Stack, Folio      (the chapter page's words are the interface's ChapterPage, drawn over it)
    RightPage/ Paper, Stack, FrameInner, FrameOuter, Compass, CompassN, MapTitle, Folio
      MapOrigin                         the map's north-west corner; map-local metres, x east, z south, y up
        Ground (MapGround)              one plane, one draw call
        Marks (GroundMarks)             one mesh: drop shadows and yard outlines
        Town (TownLayer)/ House3/Cottage, Well0/Well, Market2/StallBackW.., Field1/Post0..3
        Scenery/ Trees0..2, Reeds, FordStones (MultiMesh), Rowboat
        Edits (EditLayer)/ Fences/{Wattle, Posts} (MultiMesh), Bridges/Bridge0/{EndW, EndE, Span0..}   (footbridges only)
        Crossings (CrossingLayer)/ FerryLines (MultiMesh: ferry lines, signal discs), Ferry3/{PostW, PostE, Rope, Raft},
                                  Drawbridge4/{Lift/{Frame, Leaf, Beam, Chains, Signal}, Span3.., EndW/{Ramp, Barrier}, EndE, Keeper/{Hut, Keeper}}
        River (RiverLayer)/ Boat0..31/{Rowboat/{Hull, Cargo}, Barge/{Hull, Mast, Cargo}}, Wakes, Signs (MultiMesh), Landing/Jetty
        Walkers (WalkerLayer)/ Shadows  one MultiMesh, up to 500, and their shadow discs (a second MultiMesh)
        Ghost (ToolGhost)               one MultiMesh, the tool ghost
        Routes (RouteLayer)             one mesh, the flow preview
  Sim (SimDriver)                       processed first each frame (priority -100)
  CameraRig/ Yaw/ Pitch/ Camera/ Ink/{InkHard, InkSoft}
  RiverFeed                             drains river events after the ticks (priority -90)
  Tools (ToolController)                pointer input, ghost, FlowPreviewer
  Audio (AudioDirector, Node2D)         Step0..11, River (2D players at screen positions), Theme, Bell, Tool, Boat0..3, Bridge0..1, Coin
  Hud (GameUi, CanvasLayer)/ Root/ MapTags/{Well, Market, Field, Ford}, RiverFloats/Float0..7, BridgeBadges/Bridge4,
                                        Chapter (ChapterPage), Tabs (ToolTabs), PageLip,
                                        PreviewTip, Fps/Text
```

Units: 1 unit = 1 m, one cell = 2 m, the map is 128 x 128 cells. Cell (x, y) covers map-local x in [2x, 2x + 2) and z in [2y, 2y + 2); `Main.CellToWorld(x, y)` gives a cell centre in world space.

## Playing

| Input | Does |
| --- | --- |
| F, B, E or the tabs | pick up the fence, footbridge or eraser (again puts it down) |
| Y, D or the tabs (river) | pick up the ferry or the drawbridge; hover the river for the ghost, price and preview, click to build |
| click a drawbridge (no tool) | select it (its badge gets a blue frame); click it again to raise or lower it |
| L, K | raise or lower the selected drawbridge (else the first); hire its keeper |
| Ctrl+1 to 9 | raise or lower drawbridge 1 to 9 without selecting it |
| left drag (fence) | draw a fence line; the ghost and flow preview follow; release places it |
| hover, click (footbridge) | shows the bridge that would be built or says why none fits; press and release builds it |
| click or drag (erase) | removes fences in a 3 x 3 brush and whole bridges and ferries (a crossing costs half its price; the hover says how much) |
| Esc, right click | put the tool down (Esc also leaves the keyboard focus) |
| Space, 1 2 3 4 | pause and resume; speed 1x 2x 4x 8x |
| P, W, M | flow preview (on by default), wear overlay, sound (on by default) |
| R, N | reset (same seed), new map (the toy's next seed) |
| Home | the whole book or the map page |
| arrows, middle or right drag, wheel, + - Page Up Page Down | pan and zoom |
| Tab, Shift+Tab, Enter | move the keyboard focus over the tabs (a sky-blue frame), press the focused tab |

The camera pans with the arrows only: W is the wear overlay and E the eraser in Prototype 1 (the UX tab's W A S D pan and Q/E rotation come back with rebinding).

## The river

`Main.River` (a `RiverRules`, default `RiverRules.Default`) is turned into `Main.Params.River` at start; tests change it (for example a fuller purse) before adding the scene; `-- --no-river` sets it to null and the game plays Prototype 1 as before.

- Boats are drawn from `RiverView.Boats` every frame. The simulation's boat is a point at its bow row; the hull is drawn behind it, so a held barge stops short of the bridge. The simulation queues boats 2 rows apart; they are drawn nose to tail. Moored boats lie either side of the jetty. Boats grow to 1.6x at overview zoom.
- A raised leaf seen from straight above is only a dark edge, so the state is carried by shape on the map: the signal disc's arm (flat = walkers' turn, upright and blue = boats' turn), the open water under the lift, the barriers, the walkers bunched at the banks (waiting walkers are drawn spread round their stopping point), the badge's glyph and words.
- Riders are drawn on the raft as it is drawn (the sim moves them stage to stage, which is longer than the raft's travel).
- Events go through `RiverFeed` (one drain a frame). Strings are built only for notable events (margin notes, floats), never per frame.

## Hooks for tests and the next builder

Everything hangs off the `Main` node (`GetTree().CurrentScene as Main`, or the node itself):

- **Simulation** `Main.Sim` (`SimDriver`): `World` (read-only spans, see `sim/README.md`), `Seed`, `WalkerCount`, `Paused`, `Speed`, `Alpha`, `AnimTime`, `MsPerTick`, `TicksPerSecond`, `SimMsThisFrame`, `BudgetHits`.
  - Time: `SetPaused(bool)`, `TogglePause()`, `SetSpeed(1|2|4|8)`, `SetWalkers(n)` (0..500), `Reset()` (same seed), `Reset(seed)`, `NewMap()` (the toy's next-seed rule, `SimDriver.NextSeed`).
  - Commands (use these, not `World` directly, so the views hear about the edit at once): `PlaceFence(cells)`, `PlaceFenceLine(x0, y0, x1, y1)`, `PlaceBridge(cx, cy)` (returns the `Bridge` or null), `Erase(cells)`, `StepTicks(n)` (fast-forward). Ghosts can ask `World.CanFence`, `World.BridgeCellsAt(x, y, list)`, and the flow preview `new FlowPredictor(World)`.
  - Events (plain C# events, no allocation): `WorldReplaced` (reset or new map: rebuild anything drawn from the map), `Edited(version)` (fence, bridge, erase), `TimeChanged` (pause or speed). Unsubscribe in `_ExitTree`.
- **Picking** `Main.Rig.PickCell(screenPos, out cx, out cy)` (false off the map), `PickLocal(screenPos, out mapLocalMetres)`, `LocalToScreen(mapLocal)`. A general ray-plane test in map space: right at any pan, zoom, yaw or tilt.
- **Camera** `Main.Rig`: `FrameBook()`, `FramePlay()` (the map frame, title and compass all in view: `PlaySize`, `PlayCentreZ`), `ToggleView()`, `LookAt(mapLocal, size)`, `ZoomAt(screen, factor)`, `PanLocal(delta)`, `InPlayView`; limits `MinSize`, `MaxSize`, `PanMargin`. It handles its own input in `_UnhandledInput`, so UI controls get events first: middle or right drag pans (the tools treat a right click without a drag as "cancel"), the arrows pan (not while a control has keyboard focus), the wheel zooms at the cursor and the point under it stays put wherever the pan limits allow it (Shift+wheel is left free for tool variants), + / - and Page Up / Page Down zoom, Home switches between the whole book and the map page. Q/E and Shift+Home are reserved for rotation later.
- **Overlay** `Main.Ground.WearOverlay = true` lays the wear heat map over the page (eased).
- **Town marks and tags** `Main.Marks` (`Shadows`, `Yards`), `Main.Ui.Tags` (`Alpha`, `Tag(i)`: Well, Market, Field, Ford), `Main.Scenery` (`TreeCells`, `Copses`, `Dropped`).
- **Ink** `Main.Ink.InkVisible`; walkers ink themselves (hull) so they stay crisp. Soft ink (trees, reeds, crops) thins from full strength at play zoom to 40% with the whole page in view (`InkOutline.SoftInk`). A zero-height window leaves every width as it was.
- **Drawing on the map**: add nodes under `Main.MapOrigin` in map-local metres (the flow-preview ribbon mesh from the rendering proofs goes there; ground is y = 0, put lines a few centimetres above it).
- **The chapter page**: `Main.Ui.Chapter`, laid over `Main.LeftPageRect` (world x/z) each frame; `Main.RightPageRect` places the tabs.
- **Tools** `Main.Tools` (`ToolController`): `Tool`, `SetTool(t)`, `ToggleTool(t)`, `PreviewEnabled`, `PreviewSkips`, `Previewer` (`Current`, `Text`, `Settled`, `Completed`, `Steps`, `StepMs`, `LastFrameMs`, `AllocBytes`), `Routes` (`Builds`, `BuildBytes`), `Ghost.Pieces`, `Routes.Routes`, `Pointer`, `PointerOverUi`, `Gesturing`, `LastMessage`, `BridgeReason(x, y)`; events `Message`, `ToolChanged`, `Placed`, `Erased`, `Refused`.
- **Interface** `Main.Ui` (`GameUi`, node `Hud`): `Tags`, `Controls` (every tab and slider by name: Pause, Speed1/2/4/8, Fence, Footbridge, Erase, FlowPreview, WearOverlay, Sound, Volume, Reset, NewMap, Walkers), `SetOverlay`, `SetPreview`, `SetSound`, `Reset`, `NewMap`, `Overlay`, `UiScale`, `Rebuilds`, `Chapter` (`Slip`, `PageRect`, `Folded`, `MinInset`). `Rig.PlayRoomPx` is the room the tabs need right of the page; the book and map views shift the book left by it.
- **River** `Main.Boats` (`RiverLayer`: `Drawn`, `Rings`, `Marks`, `Wakes`, `LandingAt`, `BoatPosition(id, out at)`, `MarkTurnBack(at)`, `BoatScale`), `Main.Crossings` (`CrossingLayer`: `Count`, `NodeFor(id)`, `LeafDegrees(id)`, `KeeperShown(id)`, `RaftAt(id, out at)`, `Signals`), `Main.Feed` (`RiverFeed`: `Drain()`, `Drained`, `NotesWritten`, `Note(i)`, events `Event`, `CycleNote`, `Chronicle`), `Main.Ui.Badges` (`Count`, `LeverButton(id)`, `KeeperButton(id)`, `Card(id)`, `StateText(id)`), `Main.Ui.Floats` (`Started`, `Coins`, `LastText`), `Main.Ui.Tabs.PriceAffordable(name)`, the chapter page's `Column/Coin/Value`, `Column/Chronicle/Note0..3`. Tools: `Tool.Ferry`, `Tool.Drawbridge`, `SelectedBridge`, `Select(id)`, `PullLever(id)`, `HireKeeper(id)`, `RiverKey(key, ctrl)`, events `Lever`, `SelectionChanged`. Driver: `Sim.PlaceCrossing(type, cx, cy)`, `Sim.SetBridge(id, raise)`, `Sim.HireKeeper(id)`, `Sim.LastLeverTick`, `Sim.Params`.
- **Sound** `Main.Audio` (`AudioDirector`): `SetEnabled`, `SetVolume`, `Enabled`, `Volume`, `Silent` (headless or `--audio-driver Dummy`: cues are counted, nothing plays), cue counters `Steps`, `Splashes`, `Bells`, `Places`, `Erases`, `Refusals`, `Themes`, `Beats`, and for the river `BridgeCues`, `Calls`, `CoinCues`, `Ropes`, `OarCues`, `TurnAways`; event `Bell` (the chapter page shows "Dawn bell" as its visual twin).

## Look and performance notes

- Ground ("the paths are the art"): the wear texture is read, never changed, through a lookup warped by noise (under half a cell, so no cell squares or vector strokes show), at three widths: a softened core, a wide spread and a broad average. The spread draws a wide, pale fringe of bent grass with strokes along the walking direction (the direction is the wear's slope, from the same samples). Bare soil is blended by height: the grass has a noise height and wear takes it down from the lowest tufts up, so a lightly used trail is a broken line of light, mottled soil through grass and only the middle of a busy one is solid. The darker packed brown is kept for trails both well worn and wide, and the hollow-way texture for the simulation's hollow flag. Paper grain on top. Lit with a custom `light()` so unshadowed ground is exactly the texture colour and shadows of houses and trees still fall on it. The field is drawn as crop rows in the shader (trampled to earth where trails cross); it reads better than the 16 m field model at the simulation's 18 x 12 m footprint.
- Villagers: a tunic in one of six palette colours (no greens, which vanish into the meadow), a cape a shade darker, a smaller head as a dark hood, straw hat or soot cap, a pale pack behind as the facing cue, a soft shadow disc toward the north-west, and an ink hull of 1.2 px up close thinning to 0.8 px at overview zoom.
- Town: each cottage has a soft drop shadow, an inked yard outline with a gap at its door and one of four thatch tones; the well has a round yard and the market a square. Paper tags name the Well, Market, Field and Ford at overview zoom (as the toy does) and fade out zoomed in.
- Trees: copses of 5 to 12 round the map's edge, a few small ones and lone trees inside, from the seed; crowns drawn 18% wider and 8% flatter and shaded as one ball each (round clumps from above), each a slightly different green.
- Draw calls in the play view: about 100 on seed 1 (ground 1, marks 1, walkers 3 with the hull and shadows, trees 3, reeds 1, ford stones 1, a mesh per building piece, ink 2, plus the book).
- Per frame the game allocates nothing on the managed heap except the interface's text, rebuilt about twice a second and only where a value changed (10 to 17 bytes per frame on average with 500 walkers; 0 with `-- --no-hud`). A finished flow preview allocates its result and route mesh (a few kilobytes), which is not steady state.
- Interface: screen-space Controls (crisper than a SubViewport on the page), sized from one scale factor (window height / 1080, at least 0.78, fonts never under 12 px) and rebuilt on resize. Icons are the SVGs imported at 4x (`project.godot`, `importer_defaults`) and mipmapped at load.
- Sound: footsteps are never one per villager: a beat (eighth notes at 100 bpm; quarter notes at 8x, thinned at 2x and 4x) picks the 3 busiest 16 x 16-cell chunks on screen, from a pool of 12 voices placed at the chunk's screen position (2D panning), grass or dirt by the wear under the walkers, a ford splash when they wade; +-2 dB and +-50 cents, no file twice in a row, beats 2 and 4 3 dB lower. The river loop follows the share of the river in view and the zoom. A bell at each dawn (at most every 6 s), place / erase knocks, a low muted knock when a tool is refused, the theme at -14 dB with gaps of 2 to 6 minutes.

## Tests

From the repository root, `./check` runs them all (logs in `build/check-logs/`, screenshots in `build/screenshots/`). One at a time:

```
dotnet build game/TideToll.csproj -c Debug
tools/godot-test.sh 'SMOKE PASS' godot --headless --path game --audio-driver Dummy res://tests/smoke.tscn
tools/godot-test.sh 'TOOLS PASS' godot --headless --path game --audio-driver Dummy res://tests/tools.tscn
tools/godot-test.sh 'UI PASS'    godot --headless --path game --audio-driver Dummy res://tests/ui.tscn
tools/godot-test.sh 'PERF PASS'  godot --headless --path game --audio-driver Dummy --fixed-fps 60 res://tests/perf.tscn
godot --headless --path game --fixed-fps 60 res://tests/perf.tscn -- --walkers=200      # or --frames=N, --no-hud
xvfb-run -a -s '-screen 0 1280x720x24' godot --path game --rendering-method mobile --rendering-driver vulkan \
  --resolution 1280x720 res://tests/screenshot.tscn -- --out-dir=$PWD/build/screenshots
xvfb-run -a -s '-screen 0 1920x1080x24' godot --path game --rendering-method mobile --rendering-driver vulkan \
  --resolution 1920x1080 res://tests/screenshot.tscn -- --out-dir=$PWD/build/screenshots --ui-only
```

- **Smoke** (headless): the scene tree is complete; the place tags show in the play view and fade out at 60 m; the dash march wraps at a multiple of both dash periods; a zero-height window gives no infinite widths; scenery drops nothing, trees stand in at least 6 copses, every house has a shadow and a yard; wheel zoom from the play view at six off-centre points (four notches in, four out, through the camera's own wheel handler) keeps the point under the cursor within 5 cm; the frame loop runs ticks; after 300 more ticks walkers have moved, wear is above zero and the walker layer draws every walker that is out; a fence line and a footbridge placed through the commands appear as nodes and go again on erase; picking returns the right cell for 30 known screen points at two zooms and nothing off the map; zoom at the cursor keeps the point under it; pan and zoom stop at their limits; trees avoid doors, water and the ford; reset and new map rebuild.
- **Tools** (headless, real input through `Input.ParseInputEvent`): a diagonal fence drag fences exactly the simulation's 4-connected line, with a ghost of the same cells during the drag; the footbridge tool gives a reason off the river and builds the bridge it showed; erase removes by click, by drag and a whole bridge; a click on a tab over the map places nothing, a drag released over a tab is cancelled, a right click and Esc put the tool down; preview isolation: seed 3, the same ticks with and without previews running between them, the wear grid, hollow flags, random stream and tick count hash the same.
- **Interface** (headless): every control exists (with the river also the Ferry and Drawbridge tabs, each with its price line; Y and D pick up their tools), is on screen, has a tooltip, a label and keyboard focus, and changes its state by a real click; every shortcut; Tab gives a visible focus frame, Space still pauses with a tab focused, Enter presses it; the chapter page shows the facts; no text under 12 px at 1280 x 720; rebuilt at scale 1 at 1920 x 1080; at both sizes, in the whole-book view and in the play view, every visible label lies inside its page (12 px in from the page's edges), panel or tab, and on screen.
- **River** (headless, real input, 330c to start): Y and D pick up the ferry and drawbridge tools; off the river a click builds nothing and the status says why; on the river the ghost shows the crossing's cells and the drawbridge's preview speaks of barges; press and release builds it and charges its price; a second drawbridge with too little coin builds nothing, says "Not enough coin: the drawbridge costs 240c, you have ...", greys the tab's price and sounds the refusal; a click selects the drawbridge, a second click raises it (leaf drawn at 80 degrees, badge "Up"), L lowers it (leaf at 0), the badge's lever raises it, Ctrl+1 lowers it; the badge's keeper button hires the keeper (30c, hut shown, button disabled) and the keeper raises for a barge; four in-game days: river events, notes, a boat served, coins floating, coin cues, the ledger's coin equal to the purse, the chronicle's first line equal to the feed's; erase removes the ferry and the drawbridge (views and badge gone, half the price paid).
- **Perf** (headless, `--fixed-fps 60`): seed 1, 500 walkers, the river rules on with a ferry north of the landing and a drawbridge with its keeper south of it (600c to start), 12 days in, then 600 frames at 1x and at 8x with the interface and sound code running (sound counts cues but plays nothing headless), and a third run at 8x with a fence being dragged so the flow preview keeps recomputing. Prints CPU ms per frame (mean, 95th percentile, worst; simulation, views, tools, interface, sound, no rendering), managed bytes per frame and gen-0 collections. Fails above a 6 ms mean at 1x, 10 ms at 8x, or 64 bytes per frame; the preview run instead has its own gates: no frame at 25 ms or more, and at most 48 KB allocated per finished preview (about 21 KB measured: the predictor's result and the readout; the route mesh's upload arrays are reserved for 12,288 vertices up front, so they do not grow mid-play). Any exception quits with code 1.
- **Screenshot** (xvfb, Mobile renderer on lavapipe): `book`, `map` (about 20 in-game days at 8x; must contain trail-coloured pixels), `wear` (overlay) and `preview` (a fence dragged across the busiest trail by real mouse events, held until the preview settles; fails if it never shows) as `NAME-1280x720.png` and, from a second run with `--ui-only`, `NAME-1920x1080.png`; then `close`, `field`, `market`, `fence`, `bridge`, `bridge-hover` and `erase-hover`. Both runs end with the river views (`Screenshot.River.cs`, also alone with `-- --river-only`), seed 1 with 1200c: `river-waiting` (a barge waiting at the lowered drawbridge, its patience ring), `river-raised` (raised by L, walkers bunched), `river-ferry`, `river-blocked` (a barge held below a footbridge, crossed-out mast), `river-turnback` (the diamond, "Turned back"), `river-crossings` (ferry, drawbridge and footbridge in one view with a rowboat and a barge), `river-boats` (a rowboat moored at the landing, close), `river-map` (the play view) and `river-ledger` (the whole book with the ledger and chronicle); each scene is checked in the simulation before it is taken. `-- --ground-only` instead writes `ground-d20-overview`, `ground-d20-close`, `ground-d45-overview` and `ground-d45-close` (seed 1, 200 walkers) for judging the worn ground. Fails on a blank image. Software rendering runs at a few frames a second here, so the days are fast-forwarded through the driver.
- `tools/godot-test.sh MARKER CMD...` passes only when the command exits 0, prints MARKER and prints no error or warning line (Godot only logs unhandled C# exceptions). The only lines allowed, by exact text, are Godot's exit-time leak counts ("N resources still in use at exit", "N RID allocations ... were leaked at exit", "N ObjectDB instances were leaked at exit"): the C# wrappers of cached Godot objects are finalised after Godot prints them. If "invalid UID" warnings appear after the asset set changes, re-import (`godot --headless --path game --import`) so the `.glb.import` files point at the current palette atlases.

Run the game with `godot --path game`.

# Round 4, the river (game2): handover for the merge

New files (the river lives here):
- game/scripts/town/RiverLayer.cs: river plots (landing, cottages both banks, pasture with grazing sheep, weaver), stakes
  with labels for the next level's plots and crossings, lanes, crossings (ford stones, ferry posts, rope and raft,
  footbridge, drawbridge with its leaf on DrawbridgeAngle, keeper's hut), the tide (shader uniform tide_drop, tide post),
  the seal stone, and `RiverLayer.Widen` for the follow view.
- game/scripts/town/RiverBoats.cs: boats at the simulation's positions, cargo on deck, "Good N of M" label, wake.
- game/scripts/town/RiverAudio.cs: river sounds (river-sounds.json plus boat-call, oar-splash, ferry-rope,
  drawbridge-raise/lower, water-ford by file name).
- game/scripts/town/TownHud.River.cs: tide dial, page turn at the ring, the right page's title, the second age's page,
  the seal's page, skippers' requests in the request card, landing and toll coin numbers.
- game/tests/RiverTest.cs + game/tests/river.tscn (game-river), game/tests/Perf.River.cs (second-age perf town),
  game/tests/Screenshot.River.cs (-- --river-only and at the end of the full screenshot run).

Existing files changed (each small and local):
- game/scripts/Main.cs: three properties and three lines creating RiverLayer, RiverBoats, RiverAudio; one line in
  TownExtent calling RiverLayer.Widen (the view widens to the riverside).
- game/scripts/town/TownHud.cs: BuildRiverParts() in Build, RiverFrame() in _Process, `default: RiverEvent(e)` in OnEvent.
- game/scripts/town/TownHud.Book.cs: NextAgePressed calls RiverRung(); OpenAgePage opens the river page once in the
  second age (the old "first age ... not in the game yet" page is replaced by "The second age: The River").
- game/scripts/town/TownHud.Request.cs: a skipper's request shows in the request card when no town request runs;
  the river hint goes first on the hint line; GoodName knows Wool and Cloth.
- game/scripts/town/TownHud.Shop.cs: ShopGroup.River ("The river") with the river kinds, their icons and tooltips,
  Grouped loops over all groups, river steps that are locked or finished are left out (RiverHidden) so the bookmarks
  fit; the Ring bookmark reads "The river age" / "The second age" after the ring and the seal.
- game/scripts/town/TownVillagers.cs: loads array 6 wide, Wool and Cloth load models.
- game/shaders/ground.gdshader: uniform tide_drop (0 = unchanged): the water's edge draws back, wet mud between.
- game/tests/Perf.cs: keeps the charter town's save and calls RiverPerf at the end.
- game/tests/Screenshot.cs: --river-only mode, and RiverShots after the menu shots (new town, seed 1).
- tools/check.sh: step game-river; help text mentions --river-only.

Changed assertions in existing tests:
- game/tests/PlayThrough.cs line ~71: the page after the ring was "first age is complete ... river"; it is now the
  second age's page ("The second age: The River") because the brief replaces that page (item 1).
- game/tests/SaveTest.cs line ~87: the version-1 save is made by replacing the version the file was actually written
  with: a first-age town writes TownSave.FirstAgeVersion (2) since the second age's sim merge; the test looked for
  CurrentVersion (now 3) and could not find it. Same check, the right number.
- game/tests/Screenshot.River.cs Look(): waits for the bot's level events before moving the camera.
Not done: summary page (TownSummary) and achievements page do not list the second age yet; the river page text and
the seal page cover both ages' numbers instead. The screenshot named town-first-age now shows the second age's page.

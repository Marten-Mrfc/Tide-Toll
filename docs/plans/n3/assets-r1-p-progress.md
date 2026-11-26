# Assets round 1, track P (people and dwellings): progress

Checkout `build/wt/n3-art1`. Pictures: `build/n3/assets-audit/r1-p/`.

## Done

1. **Villager** (20:36 UTC). `assets/src/models/villager.py` writes `villager.glb` (nodes body, head: legs, shoes, tunic
   with belt, two arms bent forward, hands; head with hair), `villager-head-hood.glb`, `villager-head-hat.glb`.
   `game/shaders/villager.gdshader` dyes only the tunic faces (palette cell of Brick) to the instance colour.
   `TownVillagers.cs`: five tunic colours (Brick, River, Cast-iron green, Thatch, Soot brick) by `TunicOf(i)`, three heads
   by `HeadOf(i)` (three head MultiMeshes), lean and sway while walking (eased back to upright when standing).
2. **Loads** (20:36). Six cargo files in the body's frame (water yoke and pails, grain sack, flour sack with band, timber
   log on the shoulder, wool bale tied twice, cloth bolt), `villager-packframe.glb`, `villager-handcart.glb`.
   `TownVillagers.LoadPlace(g, level, k)`: level 0 one load; 1 a second on the back; 2 pack frame with a load on its
   shelf; 3 handcart with two loads. water and flour moved out of `town.py`.
3. **Cottages** (20:36). `assets/src/models/cottage.py` writes `cottage-{a,b,c}{0,1,2}.glb` (nodes house, accent, empty
   Smoke / Smoke2 at chimney tops) and cottage-01/02/03 (= a0, b0, c0, for the river layer and stand-ins).
   `TownBuildings.Cottages.cs`: variant and accent from the cottage's place among the plan's cottages; one scale for all
   sizes (largest model fills 0.95 of the plot as the extended cottage did; plain stays inside the old 0.74); widening
   and lean-tos removed from `TownBuildings.cs`; Marker3D "Smoke"/"Smoke2" under each cottage.
   `common/glb.py`: a part with mesh None is written as an empty node.
4. Audit: subjects `street`, `cottages-all`, `villagers-group` added; `cottage-extension-1/2` and `villager-carrying-*`,
   `villager-load-step-*` now built from the game's own code (`TownBuildings.CottageNode`, `TownVillagers.Figure`).

5. Ledger rows merged into assets/LEDGER.csv (`python3 tools/merge-ledger-rows.py --build`: 13 added, 10 replaced).
6. Tests (20:50): game-perf PASS, game-smoke PASS, game-town PASS, game-save PASS, game-river PASS.
   game-town's round-4 check "the extended cottage has no lean-to" replaced by the equal check for the new models
   (`TownBuildings.DrawnModel(p)` is the once-extended model of its variant, footprint > 1.2x the plain one's).
7. Audit town pass: stops at level 5, 8, 9, charter; new pictures `town-view-level-8`, `town-view-charter`.
   `AssetAudit.Bounds` now takes nested node transforms (close framing of the code-built cottages).
8. ./check 20:54 to 21:34 UTC: every step up to screenshot passed except game-settings (crash, exit 139; rerun alone during the screenshot step: timing fail 11.6 s vs 10) and game-motion (CPU per frame 6.6 to 13 ms vs gate 6 under load); the run was stopped by the 40-minute limit inside the screenshot step. To do: rerun game-settings, tools/motion-check.sh and the screenshot step alone.

## Left / how to resume

- Weaver's cottage and keeper's hut not started (item 4).
- Loads are still small at the normal view (readable by colour more than shape); the street picture crops its end cottages.

## After the review (fixer, 21:36 to 22:20 UTC)

Review: `assets-r1-p-review.md`. Pictures retaken into `build/n3/assets-audit/r1-p/` (reimport with
`godot --headless --path game --import` after a generator run, or the audit pictures the old models).

MUST FIX
1. Grain: now a squat hessian sack (Packed earth, never Thatch), gathered to a Soot-brick tie, Thatch grain at its
   ear, a Mud seam down the front (`villager.py` `sack_upright`). Fixed.
2. Wool: a ragged bale on the left shoulder (a roll and three lumps, Limewash) with two Thatch ties standing 6 cm
   proud. Fixed (still a few pixels at the normal view, like every load).
3. Flour: a Limewash sack lying across both arms, one Mud band, a tied neck and ear (`sack_lying`). Fixed.
4. Cloth: the bolt is Limewash in three broad Brick stripes with Paper ends, so it never equals a tunic. Fixed in
   the model (no game change).
5. Roofs: four courses a face on the main range lying on the slope (three on c, two on wings), courses on the
   hipped ends of a, a ridge cap sagging 4 percent, a darker thatch patch on the near face (a, b), a bigger Soot-brick
   chimney cap with a Brick pot, a flower box in the accent colour under the front window (a, b). All within 900
   triangles (`cottage.py`). Fixed.
6. Seams: b2's cross wing roof now runs back into the main roof (`ext=1.2`, no floating gable); c2's back cross
   range ridge dropped 0.3 m; a valley board between c's two ranges. Fixed (c's join is still busy close up).
7. Hat brim 0.48 m drawn (0.34 m life) with a darker underside; the back load moved 0.15 m lower and 0.12 m to the
   side (`TownVillagers.LoadPlace`). Fixed.
8. Street: the street's normal picture is drawn 20 percent further out (`AssetAudit.cs`, only that subject): all
   five cottages whole. Fixed.

SHOULD FIX
- Smoke: a CPU particle puff column (5 puffs, 10 updates a second, Limewash fading to Paper) on every cottage's first
  chimney (`TownBuildings.Cottages.cs` `SmokePuffs`). Fixed.
- Flower box: done (with 5).
- Weaver's cottage: Brick-tiled lean-to roof, a chimney, two thatch courses a face (`river-town.py`). Partly.
- Collar: a pale Limewash collar on every tunic (helps the green tunic against Mud). Fixed.
- Yoke 0.17 m thick drawn (0.12 m life), ropes 0.08 m. Fixed.
- Skin against Trail earth: not fixed: the palette has no other skin tone; a palette change is the lead's call.
- Black-fill pictures in the audit tool: not done (time).
- Keeper's hut: not touched (its ranking note is minor).

./check 21:52 to (unfinished at 22:37 UTC, machine load 10 to 14 on 4 cores): sim-build, sim-test, godot-nuget,
sync-assets, game-build, game-smoke, game-town, game-perf (PERF PASS), game-playthrough, game-river, game-book,
game-menu, game-save passed; game-settings printed SETTINGS PASS then aborted at exit (exit 134, leaked StyleBoxFlat
and InputEventKey references; rerun alone 22:26: SETTINGS PASS, then the same leak lines fail the error filter: not
this track's code); game-motion failed on CPU per frame under load (6.33 ms and 6.79 ms against the gate 6); the
screenshot step was still running after 37 minutes. To do: rerun game-motion and the screenshot step on a quiet machine.
After pictures as JPEG (quality 85): docs/review/n3/after/assets/ (74 files, every before name of this track covered).

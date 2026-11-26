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
8. ./check started 20:54 UTC (log /tmp/claude-0/t/check.log).

## Left / how to resume

- Weaver's cottage and keeper's hut not started (item 4).
- Loads are still small at the normal view (readable by colour more than shape); the street picture crops its end cottages.

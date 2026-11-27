# Work you can watch: the places (progress, 27 November)

Builder of the places (branch `wt/n5-places`). The build sheet is `work-build-sheet.md`; this is what was built against
its section C, the places' part of D, and the places' share of "How it is judged".

## What was built, file by file

All new code is under `game/scripts/town/work/`; one node, `WorkPlaces`, is added by `Main` under `MapOrigin`
(after `TownLife`), as `Main.Work`.

| File | What it does |
| --- | --- |
| `WorkPlaces.cs` | Owns the five places and a puff pool. The events (`UnitFinished`, `Delivered`, `GoodsTaken`), the positions the villagers need, and the one switch `WorkPlaces.DetectFromSim` (see "Joining"). `OnPlot(plot, local)` maps a point in a building's model frame (its Body) to map metres. `Ease`/`Smooth` helpers. The places' clocks stand while the town is paused. |
| `WorkPuffs.cs` | One pool of 48 translucent puffs and rings (two MultiMeshes, no ink line round them): flour at the mill, chimney puffs, splash rings at the well. |
| `FieldRows.cs` | The field as rows of the row pieces (`work-row-ripe/-green/-shoots/-stubble/-swath/-edge`), all fields in one MultiMesh per state, stooks in one more (`work-stook`). Fronts, the regrowth clock, stooks laid and taken up, the "lived in" rebuild from the plot's number. |
| `MillWorks.cs` | Doorway and plank door (`work-doorway`, `work-door-leaf`), sails' speed tied to the work, flour puffs, grain sacks (waiting) and flour sacks (just ground) on the step, the hoist (`work-hoist`) from Tools 10. |
| `WellWorks.cs` | The bucket (`work-bucket`, its Rope node stretched to the windlass), a turning crank, the bucket rising while anyone draws and tipping at each unit (splash rings), two full pails (`work-pail`) on the rim, the wet patch (`work-wet-patch`) darkening with use and drying when idle, the second rope and bucket from Tools 5. |
| `StallTable.cs` | Every load delivered to the market arcs onto the table by kind (pot, grain sack, flour sack, crate), eight places, sold and sinking away after 4.5 s; a stallholder in the apron (`work-apron`) at the table's end, turning to whoever comes near. |
| `CottageDoors.cs` | A door leaf in the cottage's own door colour on each cottage's door (position by variant, from `cottage.py`), opening as one of its people walks within 2.6 cells and closing behind them; a chimney puff at each delivery home. |
| `TownBuildings.cs` (changed) | The field plot now draws `work-field-bed` (the bed under the rows) instead of `field-ripe`; the mill's sails are no longer turned here (MillWorks does it); new `BuiltRoot(p)`, `BuiltBody(p)`, `MillSailsNode`. `SailAngle` reads the sails' drawn angle. |
| `Main.cs` (changed) | Adds `Work` (`WorkPlaces`). |
| `game/tests/WorkPlacesTest.cs`, `work-places.tscn`, `tools/work-places-check.sh`, `tools/check.sh` | The new check step `game-work-places`. |
| `game/tests/AssetAudit.NowWork.cs` (changed) | `--now-work` also photographs the market (once loads stand on its table) and a cottage (its door open for one of its people), and a busy field three more times a few seconds of play apart. |

## Section C, place by place

- **A field: done.** Seven rows on an 18 x 12 m field (six to eight by size; at least seven, see below), seven pieces a row (six on the outer rows), ragged row ends. Each row is cut up to a front (`work-row-edge`, turned so the standing crop is ahead); behind it the stretch lies as swath, then stubble, shoots, green, and gold again on a fixed clock of game time (2 / 7 / 13 / 22 s). Each unit finished moves that row's front one piece on and lays a stook where it passed; the next sheaf carried off takes up the older stook (one always stands). Work spot s works row `RowOf(rows, s)`: second row from the front, then back across the field. A field has at most four places (two and two extra hands), so with seven rows the front row and the two back rows are never worked: they alone are 19 of 47 pieces (0.40), so a field is never under a third gold however hard it is worked (the test's lowest: 0.45 meant, 0.41 drawn). A piece changing state shrinks into the ridge as the new state grows (0.35 s). A field just bought grows in row by row; a loaded town shows its fields part cut at once, from the plot's number.
  - **What the old field's states meant:** the game drew only `field-ripe`, always (`TownBuildings.Model`); `field-sprouting` and `field-stubble` were not used for fields (`field-stubble` is only the pasture's stand-in). So "ripe" meant "a field never runs out and is always ready". The new field keeps that: three rows always stand gold, and a staked field (not yet bought) is still the stakes.
  - At the fastest work (Tools 20, both field boons, the bell) a worked row's front cuts into green crop before it is gold again; the rows never worked keep the field gold. A clock fast enough for those rows to ripen at that rate would show no regrowth at all at the start of the game.
- **The mill: done**, but the hoist's rope does not run (the hoist model carries its own rope and sack). Door open exactly while anyone works or waits there (eased 0.6 s); sails at 0.25 rad/s idle plus 0.55 for each at work, eased over 0.15 s; four flour puffs at each unit; grain sacks for those waiting (up to three); flour sacks for each unit just ground (up to three), taken up by those who leave; the hoist from Tools 10 ("Fine tools"), growing in.
- **The well: done**, but the crank is a small code-made arm on the model's own crank (no generated model for it). The second rope and bucket from Tools 5 ("Sharp tools"); they work half a turn apart.
- **The market stall: done.** The stall model's own goods stay (the quiet table, as today).
- **A cottage: door and chimney done; the water butt is not drawn.**
- **Section D:** sickle and scythe are the villagers'. The second rope at Tools 5 and the hoist at Tools 10 are written here and checked by the test (`hoist-at-tools`, `rope-at-tools`). An extra hand works one more row at a field (rows for spots 2 and 3) and a second spot at the well (the second bucket, when the rope is there).

## Tests

- **Replaced:** none. No game test asserted the old field model or the sails' old speed (`TownTest`'s `DrawnModel` check is for cottages only).
- **Added:** `game-work-places` (`WorkPlacesTest.cs`, run by `tools/work-places-check.sh`): a first age played by the town bot in the running game at speed 16 (the bot acting once a frame), sampled every frame, then up to two minutes more at speed 1 after the charter (at speed 16 a lone miller lasts only a few frames). At 60 fps the whole age with every check; at 30 and 120 fps the first two minutes of frames. It checks: a field never under a third gold (what is meant and what is drawn); fronts move only in a frame where the test itself, from the simulation's views, sees work finished at that field, and never more steps than units; the sails' drawn rate (per game speed) with 0 < 1 < 2 at work; the mill's door wanted exactly while someone works or waits there, fully open or shut after its swing; every market delivery received and shown on the table, none missed; a cottage's door fully open whenever its people have been near it for its swing; no field piece, bucket, mill door or cottage door changing faster than a 0.15 s ease allows in one frame; the places allocating in few frames (only when a place's nodes are built); the hoist and second rope at their Tools steps; a town adopted again shows its fields lived in, at once.

## Results of the check steps (exact commands of `tools/check.sh`)

See the table at the end (filled in when the last run is done).

## Pictures

`build/n5-places/now/` (taken with `--now-work`, real game, not staged): `work-now-{well,field,field-1,field-2,field-3,mill,market,cottage}-{close,normal}.png`.

## Joining: what the villagers' side calls and reads

All positions are map metres in `MapOrigin`'s space (what `TownVillagers` draws in). Reach them through `Main.Work`.

Calls (once joined, set `WorkPlaces.DetectFromSim = false`, or the places count each event twice):

- `UnitFinished(int workplace, int slot)`: a unit of work finished at a workplace (index into `Town.Workplace`) by the worker on the simulation's work spot `slot` (-1 if not known). `VillagerView` carries no `Slot`; `WorkPlaces.SlotOf(town, workplace, v.X, v.Y)` finds it from where the simulation put the worker (exactly on `Town.WorkSpot(plot, Slot)`), as this side does until joined. The well tips its bucket and fills the pails, a field's row front moves and lays a stook, the mill puffs and sets a flour sack on the step.
- `GoodsTaken(int workplace, int slot)`: that worker has taken up the goods (stooped and lifted). The pails leave the rim, the older stook on the row goes, the oldest flour sack goes. Until joined these leave 0.9 to 1.1 s after the unit; joined, they leave when this is called.
- `Delivered(int plot, Good good, Vector3 from)`: a load put down at the market (it arcs from `from`, the arms, onto the table) or brought home to a cottage (a chimney puff and the door opens). `from` is ignored for cottages.

Positions to read:

- Field: `FieldWorkSpot(plot, slot, out yaw)` (where the reaper of spot `slot` stands, just behind its row's front, facing the standing crop), `FieldStook(plot, slot)` (the stook on that row the sheaf is lifted from).
- Well: `WellCrank(plot, slot, out yaw)` (spot 0 at the crank, spot 1 on the other side), `WellPail(plot, 0 | 1)` (the two full pails on the rim).
- Mill: `MillStep(plot)`, `MillDoor(plot)`, `MillFlour(plot, 0..2)`.
- Market: `TableEdge(plot)`, `TablePlace(plot, 0..7)`.
- Cottage: `CottageDoor(plot)`.

Read-only states for the villagers' side if useful: `Main.Work.Mill.DoorOpen`, `Main.Work.Well.BucketHeight`.

## What still does not look right

(filled in from the last pictures)

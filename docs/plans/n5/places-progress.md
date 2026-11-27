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

## Results of the check steps (exact commands of `tools/check.sh`, after the join, 11:00 to 11:30 UTC)

| Step | Result |
| --- | --- |
| game-build | PASS, 0 warnings, 0 errors |
| game-smoke | PASS `renderer=mobile game_s=30.0 coin=4 trips=1 uploads=72 slow-frame=384steps` |
| game-town | PASS `faint=0.40 maxstep=0.014 worn=0.95 ... level=6 bought=48` |
| game-perf | PASS `1x: mean 0.911 ms (worst 8.68), 546 B/frame; 8x: mean 1.262 ms, 738 B/frame` (load average about 20 on 4 cores; on a quieter machine before the join: 0.543 / 0.738 ms, 544 / 724 B) |
| game-playthrough | PASS `charter=33.9min buys=201` |
| game-river | PASS |
| game-layout | PASS (the first run after the join failed S1-keycap on shop rows in u13, interface code this branch does not touch; the rerun passed unchanged) |
| game-motion | **FAIL on CPU time only**, a different transition each run (`pause: frame 2 took 7.21 ms`, `resume: frame 55 8.44 ms`, `esc-during-open: frame 5 8.74 ms`; gate 6 ms) with the load average at 18 to 26 on 4 cores. The same test run at the same moment in the villagers' checkout (no places) failed the same way (`tab-display-sound: frame 11 took 8.80 ms`). It has not yet passed in this branch: to be rerun on a quiet machine before it is believed either way. |
| game-save | PASS `autosave-clock=0.65/0.65s level 2 paved 1 villagers 4` |
| game-work | PASS `WORK RATES PASS pairs=535 gap=1.98-2.30m` |
| game-work-places | PASS at 60, 30 and 120 fps (60: `field-gold-min=0.45 drawn=0.41 front-bad=0 door-bad=0/3122 sails=0.280/0.800/1.639 market=1607 table=1606 missed=0 late=0 cottage-bad=0 one-frame piece=0.071 bucket=0.048 door=0.028 cottage=0.037 bound=0.111 alloc-frames=23 hoist-at-tools=12 rope-at-tools=5`) |
| ledger | PASS `506 rows, 480 asset files checked, 0 problems` |

## Pictures

Real game, not staged (`--now-work`): `build/n5-places/now3/` (after the join; `now/` and `now2/` are the places
alone): `work-now-{well,field,field-1,field-2,field-3,mill,market,cottage}-{close,normal}.png`. Interaction strips after
the places: `build/n5/strips-after-places/`, JPEG copies replacing the villagers-only ones in
`docs/plans/n5/strips/after/` (`work-water`, `-reap`, `-mill`, `-market`, `-home`).

## Joined to the villagers (done in this branch)

The villagers' part (`claude/prototype-1`, merge `4711f01`) was merged into `wt/n5-places` (merge commit "Snapshot: merge
the villagers' part"). One conflict, `tools/check.sh`, where both added a step: both steps kept (`game-work` after
`game-motion`, `game-work-places` after `game-save`) and both names in the "godot not found" list.

- `PlaceSite.cs` (new): one `IWorkSite` per well, field, mill, market and cottage plot, registered by `WorkPlaces`
  on every new town (`WorkSites.Clear()` then `Register`). `WorkPlaces.DetectFromSim` is now **false**: the places
  answer to the villagers' calls only; the old detection stays behind the switch for a build without them.
  - `WorkStarted`: the well's two pails stand on the rim (they fill as the bucket comes up); the mill's flour sack of
    that spot stands on the step while the grain is ground.
  - `WorkFinished`: the well's bucket tips (splash) and the pails leave the rim over `WorkSites.TakeSeconds`; a field's
    row front moves on, a stook is laid where it passed and the older one leaves over `TakeSeconds`; the mill puffs
    flour and that spot's flour sack leaves over `TakeSeconds`.
  - `Delivered`: the market puts the load on the table place the arc was aimed at (it drops the last bit and settles
    with a small overshoot, 25 percent larger than the board's goods, so the arrival reads at the normal view); a
    cottage's door opens and the chimney puffs.
  - `Spot`: a field's worker stands just behind the front of their row, facing the standing crop (heading 0). Other
    places keep the defaults.
  - `GoodsAt`: the stook being taken up on that row; the middle of the well's two pails; the mill's flour sack of that
    spot; the market's next free table place; a cottage's door.
- `villager-sickle.glb` and `villager-scythe.glb` (generator `work-scenes.py`, ledger rows): in the hand frame the
  villagers use (hand at the origin, handle up +Y, blade at the top curving forward +Z), pale (Concrete) blades, the
  sickle 0.8 m and the scythe 1.36 m in the villager model's frame, so they read at the normal view.
- The test `game-work-places` reads the joined path: the market check now allows the villager's 0.45 s arc (never more
  loads on the table than delivered, never fewer than were delivered a second before; none missed).

## Positions and calls (for reference)

All positions are map metres in `MapOrigin`'s space (what `TownVillagers` draws in). Reach them through `Main.Work`.

Direct calls on `Main.Work` (used only with `WorkPlaces.DetectFromSim = true`; joined, `PlaceSite` calls the places):

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

- **The mill's door faces away from the camera** in this town (the plan's door node is on the north side, and the line
  comes in from there), so the doorway, the door swinging, the sacks on the step and the flour puffs are behind the
  tower at the normal view; only the sails' speed and the hoist show. Moving the doorway to the south face would put
  it where no villager can reach it without walking through the mill (the simulation's door and lane are north), so it
  was not done: it needs the plan's door, which is the simulation's (not changed).
- **The well**: the pails and bucket now sit on the rim toward the camera and read close up; at the normal view they
  are small dark tubs, and the old second-bucket offer piece (a yoke beside the well) still stands there and confuses
  the picture. The wet patch is translucent and hardly shows. The crank is a small code-made arm.
- **A field**: reads as rows, with fronts, stubble, shoots, green and stooks moving between the three shots; the
  worked rows show a bare-looking cut stretch at high work rates, where the front cuts into green crop again (the
  three rows never worked stay gold). The whole field is dense gold tufts, heavier than the board's.
- **The market**: goods on the table read at the normal view now (larger, settling with a small overshoot); the stall
  model's own goods are still mixed in.
- **A cottage**: the door leaf opens and the dark inside shows; the water butt is not drawn.
- **Not done**: the hoist's running rope; the cottage's water butt; the mill's doorway on the camera's side (above).

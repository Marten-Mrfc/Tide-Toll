# Work you can watch: the build sheet (version 1, the lead, 27 November)

The owner on the idea (`work-you-can-watch.md`) and its board: "Okay when done implement it." This sheet says what is built, in what order, and how it is judged. Round 1 is the first age: the line, the well, a field, the mill, the market, a cottage. The second age's places (pasture, weaver, landing) follow in round 2 with the same rules.

## Limits

- **How the game plays does not change.** No new rule, timer, price or key; the save format stays. The simulation (`sim/`) is not changed; if a builder thinks it must be, they stop and say why. The play-through by real input must still read `charter=33.9min buys=201`.
- **Drawing only, from what the simulation already gives** through its views: a villager's `Phase`, `Job`, `Stage`, `Workplace`, `Slot`, `WorkLeft`, `Carry`, queue position (`QueuePosition`, `QueueSpot`), work spot (`WorkSpot`); a workplace's `Places`, `Busy`, `QueueLen`, `Hands`; the town's Tools and Load levels; the bell.
- **Light.** Transforms on the villagers already drawn, pooled props in multimeshes, a few nodes per place. No allocation per frame; `game-perf` and `game-motion` must pass untouched.
- **Never in one frame.** Every change of pose, prop or place is eased (0.15 s or longer), also when the game runs at 30 or 120 frames a second and when the speed is raised.
- **It must read at the normal view**, where a villager is about sixteen pixels tall. The large signs carry it; the small ones are for those who zoom in.
- **It must not suggest rules that do not exist.** A field never runs out, so it never looks bare. A queue is drawn as long as it is, never longer or shorter.
- The models are the ones made for the board (`docs/plans/n5/mockup/notes.md`, "Round 2"), made by the generators under `assets/src/models/`, each with its ledger row.

## The one design rule: places answer to the work done, villagers move in its rhythm

Work is short: 1.5 s at the well, 3 s at a field and at the mill at the start, and much shorter once Tools are bought. A scene that needs three seconds of acting per villager would fall apart late in the game. So:

- **A place keeps its own small picture state** (not saved; rebuilt from the plot's number on load) and moves it forward **each time a unit of work is finished there**, and continuously while anyone works. A busy place looks busy at any speed.
- **A villager at work shows a pose and a rhythm**, not a long act: a lean, a rock or swing at a fixed tempo, a tool in the hand; and at the end a short stoop as the goods are taken up.

## A. The line

- Villagers who wait at a workplace stand **in a line along the lane they came by**, about 2 m apart, each facing the workplace, the first nearest to it. With less room than the line needs it bends along the lane; it never runs through a building, a field's crop or another line.
- When a place comes free the line moves up **one step, eased, front first** (a ripple, not all at once).
- A villager with a handcart or pack frame keeps it; the spacing allows for the cart.
- The same line at the well, a field, the mill and, in round 2, the river places.

## B. The villager at work

| Job | Pose and rhythm | Tool or prop in the hand | Taking the goods |
| --- | --- | --- | --- |
| Drawing water | At the crank, rocking forward and back in time with the crank | none (hands on the crank) | Stoops to the rim; the yoke with full pails comes to the shoulders as the pails leave the rim |
| Reaping | Bent about 30 degrees, a swing from side to side, stepping slowly along the row with the cut | A sickle; from the Tools step named in the notes, a scythe with a wider swing | Stoops at the stook; the sheaf comes up into the arms as it leaves the stook |
| Milling | Carries the sack to the door, tips it at the doorway | The grain sack, then none | A flour sack is lifted from the step |
| Selling (the market) | Does not stop (the simulation does not); a dip of the body as it passes the table | The load leaves the arms in a short arc onto the table | Walks on with empty hands |
| Coming home | Does not stop; the door answers | | |

- A load is never seen before it has been taken up, and never vanishes from the arms without going somewhere (the table, the door).
- With a handcart, the cart stands beside its owner while they work and is loaded at the end.
- When the bell rings, workers lift their heads for a moment and the rhythm quickens for as long as it lasts.

## C. The places

- **The well.** The crank turns and the rope winds while anyone draws; the bucket rises and tips at each unit finished; two pails on the rim fill; a splash ring at each tip; the wet patch round the well darkens with use and dries slowly when the well stands idle.
- **A field.** The field is rows (six to eight, by its size). Each row is at every moment cut up to a front, growing behind an earlier cut, or ripe. Each unit of work finished **moves a front forward by a fixed step**; the workers stand at the fronts of their rows (one row per place; an extra hand, one more row). A cut stretch lies as stubble, then shows green shoots, then taller green, then gold, on a fixed clock that is fast enough that **at least a third of the field is always gold** however hard it is worked. A stook stands where a front has passed and is taken up by the next sheaf carried off.
- **The mill.** The door stands open while anyone is at work inside or at the door and closes (eased) when the mill is idle. The sails turn at an idle rate, and faster the more places are busy. A flour puff leaves the door at each unit finished. Grain sacks on one side of the step show how many are waiting (up to three); flour sacks on the other side show what was just ground and are taken up by those who leave. The hoist's rope runs while the mill works.
- **The market stall.** The table shows the last deliveries by kind (up to eight things); each thing delivered arrives in a short arc from the villager and is sold (it leaves, the coin hops as now) some seconds later. A stallholder stands at the table's end and turns to whoever comes. With nothing delivered for a while the table holds its few standing goods, as today.
- **A cottage.** The door opens as one of its people comes within a few steps and closes behind them; a puff from the chimney at each delivery home; a water butt by the door that fills with water brought home and sinks slowly.

## D. What you buy, you see

- **Tools:** sickle, then scythe; a second rope on the well; the hoist on the mill appears with its step. Which step shows what is written in the notes and in a test.
- **Load:** the basket, pack frame and handcart stand by their owner at work.
- **An extra hand** at a workplace: one more row, or one more spot, at work.
- **The bell:** heads up, quicker rhythm.

## E. Sounds (after the pictures are right)

At most four new ones, made in code like the others, each with a ledger row, quiet and thinned when the town is busy: the windlass, the sickle's swish, a sack set down, the door. Not before the scenes look right.

## How it is judged

1. **Interaction strips** (a new picture tool beside `tools/feel-strips.sh`): for each job, eight frames of one villager from joining the line to walking away, at the normal view and close up; taken on the build before this work ("before") and after. Read by a reviewer who did not build it; what they find is fixed before the round is called done.
2. **A new check step, `game-work`**, with real game states, not staged ones:
   - the line: waiting villagers are 1.6 to 2.4 m apart, face their workplace, stand outside buildings and crop, in queue order;
   - no load is drawn on a villager whose work is not finished, and no load vanishes: every delivery to the market shows on the table;
   - a field never shows less than a third gold over a whole first age, and its fronts move only when work is finished there;
   - the mill's sails are faster with two at work than with one than with none; the door is open exactly while it should be;
   - nothing changes in one frame (sampled every frame at 30, 60 and 120 frames a second);
   - allocation per frame stays inside `game-perf`'s gates.
3. The whole check on a clean copy; the play-through unchanged; then the Windows build.

## Who builds what

- **Villagers** (the line, the poses, tools, taking the goods, the parked cart, the bell): `game/scripts/town/TownVillagers*.cs` and new files beside them.
- **Places** (well, field, mill, market, cottage; their models and props): new files `game/scripts/town/Work*.cs` or one per place, the generators and the ledger.
- Both read the same small interface, written first by the places' builder and kept in one file: for a workplace, where its work spots, its line and its goods are in the drawing, and a call "a unit of work was finished here".
- Tests that only fit the old look (a villager standing exactly on the simulation's spot, for example) may be replaced by the equal test for the new look; each replacement is written down with its reason. No test is weakened.

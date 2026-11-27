# Work you can watch (an idea, the lead, 27 November)

The owner: "Next spend time on interaction. I mean the villagers their interactions with the buildings, fields etc. Right now this looks super basic."

## What happens now

The simulation knows a great deal about every villager: which job, which workplace, which work spot (`Slot`), how many seconds of work are left (`WorkLeft`), who waits in what order (`Queue`), what is carried, and which upgrades the town has. The picture shows almost none of it:

1. A villager walks up to the well, the field or the mill.
2. They stand straight and still for the work time. At the field they stand at its edge; at the mill and the well they are half hidden behind the building. Those who wait stand in a knot.
3. A load appears in their arms in one frame and they walk away.

Nothing at the workplace answers: the well's rope does not move, the field looks the same before and after a harvest, the mill's sails turn at one speed whether or not anyone grinds. At the market the goods vanish and a coin number rises.

## The idea: every job is a small scene in three beats

Each workplace plays the same three beats, timed by the simulation's own clocks. **Nothing about how the game plays changes**: no new rule, no new timer, the same work spots and queues. The drawing reads `Phase`, `Slot`, `WorkLeft` and the queue order, which it has today.

1. **Take a place.** Those who wait stand in a line along the lane, a step apart, facing the workplace. When a place comes free the whole line moves up one step. The worker goes to a spot that belongs to the job.
2. **Work.** The body moves in the rhythm of the job and the workplace answers, so progress can be seen from the normal view.
3. **Take the goods.** The product stands there first; the villager stoops, lifts it and leaves. No load appears from nothing.

| Place | Take a place | Work (the body, and the place's answer) | Take the goods |
| --- | --- | --- | --- |
| **The well** | A line along the lane; the worker steps to the windlass, empty pails set on the rim | Rocks at the crank; the handle turns, the rope winds, the bucket rises out of the shaft and tips; a splash ring; a wet ring of ground round the well that dries | The full pails stand on the rim; the villager shoulders the yoke |
| **A field** | Each worker takes a row of their own (two places, two rows; an extra hand, a third row) | Walks the row bent, swinging the sickle; behind them the gold turns to stubble, strip by strip; cut rows grow back green and then gold, so the field always shows all three states and never looks empty | A sheaf stands at the row's end; the villager lifts it |
| **The mill** | A line by the door; grain sacks set down on the step | Carries the sack in through the door (the door opens); while anything is being ground the sails turn faster, the hoist rope moves and a puff of flour comes from the door; at rest the sails idle | A flour sack slides out onto the step; the villager lifts it |
| **The market stall** | Walks to the front of the table | Sets the goods down; the table fills with what was last delivered (crates, pots, sacks) and empties as it sells; a stallholder behind the table takes it | The coin hops (as now); the villager turns for home with empty hands |
| **A cottage** | Comes to the door | The door opens, the villager steps in, a puff from the chimney; the water butt by the door fills when water comes home | Steps out again with empty pails |
| Second age | The pasture (shearing: the sheep is held, wool comes off, a shorn sheep trots away), the weaver (the shuttle moves, the roll of cloth grows), the landing (goods stacked on the quay by kind, the crane swings them aboard) follow the same three beats | | |

## What you buy, you see

- **Tools:** the tool in the hand changes with the steps (a sickle, then a scythe with a longer swing; a second rope on the well; a hoist on the mill).
- **Load:** the basket, yoke and handcart stand beside their owner while they work, and are loaded at the end.
- **An extra hand** at a workplace: one more row or spot visibly at work.
- **The bell:** when it rings, workers look up and the rhythm of the work quickens for as long as it lasts.

## What it must not do

- It must not suggest rules that do not exist. The field's cutting and regrowing is drawing only: a field never runs out, so it must never look bare.
- It must read at the normal view, where a villager is about sixteen pixels tall: the large signs carry it (the line, the cut strip, the sails' speed, the rising bucket, goods on the table), the small ones (a hand on a crank) are for those who zoom in.
- It must stay light: transforms on the villagers that are already drawn, a few pooled props, no allocation per frame. The per-frame cost test must pass.
- Pacing by real input must be unchanged (charter near minute 33.9, 201 purchases).

## How it would be judged

An "interaction strip" per job: eight frames of one villager from joining the line to walking away, at the normal view and close up, before and after, read by a reviewer who did not build it.

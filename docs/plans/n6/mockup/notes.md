# Boats, the landing and the crane: the mock-up boards (notes)

The design: `docs/plans/n6/boats-design.md`. These are pictures to say yes or no to. Nothing in how the game plays or draws changed: the only code is test and tool code (`game/tests/AssetAudit.Landing.cs`, `tools/mockup/*`) and new models that the game does not load yet.

## Where the pictures are

- `boats-journey-now-and-planned.png` (1648 x 2668): board 1, the journey. Top pair: one boat's whole visit, from the south edge to the berth and back out. Bottom pair: the same, enlarged on the landing's reach. "Now" is on the left in white and "planned" on the right in wheat, on the dark strip. Single panels: `journey-now.png`, `journey-planned.png`, `landing-reach-now.png`, `landing-reach-planned.png`.
- `landing-now-and-planned.png` (1648 x 1348): board 2, the landing. Top pair: close up. Bottom pair: the normal play distance. Single panels: `landing-{now,planned}-{close,normal}.png`.
- `track/river.json`, `track/boats.csv`: the real data behind board 1. This is seed 1's river plan, and every tick of boats 32, 33 and 34.

## Board 1: how it was made

- **The real track.** `tools/mockup/boat-track/` is a small console program that uses `sim/`. It plays seed 1 with the Cheapest bot (PlayOn) through the first age and into the second, and writes every boat's X, Y, heading and phase every tick. It also writes the river plan: water edges per row, `CourseX`, berths, waiting places, crossings and plots. It took 4 s for 41 boat visits. To run it: `dotnet run -c Release --project tools/mockup/boat-track -- 1 OUTDIR 40`.
- **The visits drawn.** Boat 33 is a rowboat. It went to berth 1, lay there 96 s, and left. Boat 34 is a rowboat that came 15 s later. It waited 57 s at waiting place 0, went to berth 0 when the barge (boat 32) left, lay 69 s, and left. The drawbridge had been built in that run. The ford, ferry, footbridge and drawbridge are drawn where the plan has them.
- **How the boats are drawn.** Seen from straight above, in the game's palette. Pale is a boat coming up and wheat is a boat going down. Brick is the boat that waited, and a mix of brick and pale is that boat going from its post to the berth. Moving boats are small boat shapes every quarter of a second. "Now" also draws every tick where the boat jumps sideways or its bow turns more than 17 degrees. A boat lying still is drawn once at full size (4.2 m by 1.6 m, 2.1 by 0.8 cells). The bow is the pointed end. The rings mark the four faults: the bend (top), and the slide in, the jump out and the about-turn (bottom).
- **Commands:**
  - `python3 tools/mockup/track_board.py docs/plans/n6/mockup/track 33 34 docs/plans/n6/mockup`
  - The prototype alone, tick by tick: `python3 tools/mockup/boat_course.py docs/plans/n6/mockup/track 33`

## What the real track shows (seed 1; 1 cell = 2 m; the sim runs at 10 ticks a second)

- **The river is 4 cells (8 m) wide all the way.** Its middle runs at x 70 by the landing and at x 53 at the bottom bend. Boats sail at 3 cells (6 m) a second, which is 0.3 cells a tick.
- **Bends: a sideways jump of 1.0 cell (2 m) in one tick, each time the boat crosses a row where the middle moves.** Over the whole river it happens 23 times going up and 23 times going down (most between rows 84 and 112), every 0.3 to 1.2 s. At each jump the bow swings 73 degrees in one tick (to -163 or -17 degrees), and it swings back 73 degrees in the next tick. That is 730 degrees a second, twice per jump.
- **Below the drawbridge, a dead stop.** The boat goes from 3 cells a second to 0 in one tick and holds 1.4 s coming up (at y 76.5). Going down it holds 1.5 s (at y 72.5). Then it is at full speed in the next tick.
- **Coming alongside: a diagonal slide.** Over the last 4 rows the boat slides 1 cell (2 m) sideways, from the middle (x 70) to the berth (x 69). In the tick it starts, its bow points to -162 degrees (nearly west, across the river). It then lies moored at -104 degrees, 14 degrees off up-river, for the whole stay, because the heading is just its last movement.
- **Leaving: a jump from the quay to mid-river, 1 cell (2 m) in one tick.** In the same two ticks the bow goes from -104 to 0 to +90 degrees, which is 194 degrees in 0.2 s (the about-turn). The boat then goes back down the same middle line it came up.
- **Boats pass through each other.** The waiting boat lies on the middle line at (70, 67.5), the line every other boat sails along. Over the 41 visits, 31 pairs of boats came within 1 cell of each other's centres, the closest 0.1 cell apart. A leaving boat sails through a waiting one (boats 4 and 5, 7 and 8), and a leaving boat meets an arriving one head-on (boats 0 and 1, 9 and 10).

## The prototype course (`tools/mockup/boat_course.py`)

It takes the simulation's ticks of one visit and gives where the designed boat is drawn at each of the same ticks. The simulation's y (how far up-river) stays the truth.

1. **The middle line** is `CourseX` smoothed with a Gaussian over 3 rows. It is a curve, not a staircase.
2. **Two lanes.** Coming up, the boat keeps 0.85 cells to the near (landing) side of the middle. Going down, it keeps 0.95 cells to the far side. By the waiting places, the up lane moves in to 0.3 cells so it can pass the boats lying in the lee of the near bank.
3. **Waiting boats.** Each lies at a mooring post 0.45 cells off the near bank's water, over its whole length, with its bow up-river. That is out of both lanes.
4. **One path per visit**, built from pieces:
   - up the lane;
   - the S in to the quay, over 4.5 rows, ending bow up-river with its side 0.75 cells off the water's edge (the quay's face); or the S in to a post, then from the post along the bank to the berth;
   - the push-off, 0.45 cells sideways with the bow still up-river;
   - a turning arc to starboard (bow north, then east, then south), drifting down with the stream;
   - the far lane down to the edge.

   Every point of the path carries the simulation's y it stands for, so each tick gives a target distance along the path.
5. **Speed like a boat.** The drawn boat runs toward that target. It reaches at most 3.6 cells a second, 1.6 in the turning arc, and less in bends (by curvature). It gathers way at 2.5 cells/s² and loses it at 4.0. It brakes ahead of slow stretches and is never ahead of the simulation, so it stops smoothly wherever the simulation stops dead. The bow turns at most 100 degrees a second.
6. **What it gives for these two visits:**
   - The largest step between two ticks is 0.36 cells, and the largest bow turn is 10 degrees (against 1.0 cell and 73 to 104 degrees now).
   - It falls behind the simulation along the path by up to 2.0 cells coming in (boat 33) and 2.3 cells (boat 34). That is about one rowboat's length.
   - In the turn it falls behind by up to 4.7 to 5.0 cells (see below). It catches up to 1.4 cells or less within 6 s.

## Board 2: how it was made

One run of the asset audit with the new `--landing` flag (`game/tests/AssetAudit.Landing.cs`) took about 2 minutes:

`xvfb-run -n 196 -s '-screen 0 1920x1080x24' godot --path game --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 res://tests/asset-audit.tscn --quit-after 1000000 -- --out-dir=DIR --only=none --landing`

then `python3 tools/mockup/landing_board.py DIR docs/plans/n6/mockup`.

The run goes like this:
1. The audit's own town (seed 1, Payback bot) plays to the charter, and the second age is rung in.
2. The town plays on, with the bot buying, until the crane stands and two boats lie at the berths at high water. That took 9,827 ticks, to level 26.
3. **Now** is that real frame, with nothing staged: the barge and a rowboat moored at today's short jetty, the crane, and villagers with handcarts in knots around the jetty. It is taken with the work scenes' camera (yaw 35 degrees, 24 m camera size, 1280 x 960), and from straight above at the play zoom.
4. **Planned:** on the same river, with the same camera, the game's landing, boats and villagers are hidden. The crane and the tide post are moved out of sight, because the river layer sets them visible every frame. The design is then staged where the landing was. After the pictures everything is put back.

### What the planned picture shows

- **The quay.** A timber quay on piles along the bank, 18 m (two berths) long. It has a string piece and a kerb, piles with pale caps, cross-braces, a bollard with a coil of line, and an iron ladder at each berth.
- **The rowboat** lies at the upper berth with its bow up-river, on two lines to the quay. Its gangplank is down and its hold is part full of grain sacks.
- **The barge** lies at the lower berth.
- **The yard behind the quay** has four stacks on pallets: grain sacks, flour sacks, wool bales and bolts of cloth. One villager is bent at the grain stack. Three wait in a line on the worn lane, each facing the yard and carrying flour, wool or cloth.
- **A sign at each berth**, leaning back so it reads from above. Each shows the good's icon in relief (a grain sack with ears, a banded flour sack), and a bar of how much is aboard with quarter ticks. The grain bar is 60 % full in green (plenty of time). The flour bar is 30 % full in thatch (some time).
- **The crane** stands on a stepped stone base between the berths. Its jib is swung toward the rowboat's hold, and a net of four sacks hangs from the hook. The treadwheel is beside it, and a villager stands on the quay by the crane.
- **On the water:** a rowboat waits at a mooring post on the near bank below the quay, and another rowboat passes down the far lane with a V wake.

### The new models (`assets/src/models/quay-scenes.py`; each has a ledger row; `python3 tools/check-ledger.py` passes with 504 rows)

| Model | What it is | Triangles |
| --- | --- | --- |
| `quay-deck` | The quay, 18 x 2.6 m, on piles, two berths, bollards, coils, ladders | 1140 |
| `quay-crane` | Stone base, turntable, post with struts, jib with stay and counterweight, sheave, rope, hook block and a net of four sacks (posed mid-swing) | 954 |
| `quay-treadwheel` | A wheel 3.1 m across, rims, spokes, treads, axle, two A-frames, footing | 774 |
| `quay-stack-grain`, `-flour` | Sacks lying 3-2-1 on a pallet (flour with a band) | 654 each |
| `quay-stack-wool` | Three lumpy tied fleece bales on a pallet | 303 |
| `quay-stack-cloth` | Five bolts of cloth in three colours on a pallet | 222 |
| `quay-sign-grain`, `-flour` | A lectern sign: two posts, a board leaning back 50 degrees, the icon, the bar | 234 / 142 |
| `quay-mooring-post` | Three lashed piles | 114 |
| `quay-gangplank` | A plank with cleats | 84 |
| `quay-line` | A sagging mooring line, stretched along X in the scene | 64 |
| `quay-wake` | A V of broken water and a furrow | 38 |

The boats are the game's own `rowboat.glb` and `barge.glb` at the game's lengths (4.2 m and 8.5 m). The sacks in the hold are `work-sack-grain.glb` (night 5).

## What does not read well yet

- **Board 1, the "now" track** goes up and down the same middle line, so coming up and going down lie on top of each other. That is the fault itself (fault 5), but it makes the "now" panels busy.
- **Board 1, the planned turning arc** is drawn as a fan of boats close together. It reads as "turns round", but the drawing is heavy.
- **Board 1, the waiting boat in "planned"** lies just behind the moored boat at berth 1, nearly touching it. The sim's waiting place 0 is 2 cells below berth 1.
- **Board 2, the net of sacks** on the crane's hook is small. Close up it sits over the rowboat at the quay's edge, but at the normal view it merges with the boat. The treadwheel stands still, and nobody walks in it: a posed villager inside it looked like a fallen figure and was taken out.
- **Board 2, the villager setting a sack down** at the grain stack is next to a cottage of the real town and reads weakly.
- **Board 2, the wake** is a scatter of white flakes. It reads as "moving" only beside the boat.
- **Board 2, the yard** has to fit between the real town's cottages: there is less room behind the landing than the design sheet assumes.
- **Board 2, the white rings** round the barge's bow in both "now" and "planned" are drawn by the game. They are not part of the design.
- **Board 2, the small black frame** left of the planned crane is a prop of the real town that was not hidden. It is also in the "now" frame.
- **Not done:**
  - No fresh landing frame with a delivery in mid-walk. The "now" frame shows the villagers standing about the jetty, which is what the game does.
  - No contact sheets of the new models.
  - `./check` was not run (the machine is shared). The game build passes, and the ledger check passes.

## What the builder of the real feature must know

- **Width.** The river is only 4 cells (8 m) wide along the whole reach (seed 1, and the planner's banks are the same everywhere). The game's models, at the game's lengths, are wide: the rowboat is 4.2 m long and 3.8 m across with its oars out (the hull itself about half that), and the barge is 8.5 m long and about 3.7 m across with its leeboards. The two lanes (0.85 and 0.95 cells, 1.7 and 1.9 m, from the middle) are room enough for hulls, but not for oars sweeping on both sides. In the staged picture the barge lies 4.3 m out from the bank, which leaves under 2 m of the far lane. Two ways to make room: widen the quay's reach of the river in the river plan, or ship oars by a moored boat and let a passing barge keep the middle.
- **Berths are too close for barges.** The simulation's berths are 3 rows (6 m) apart (`BerthY = yL - 0.5, yL + 2.5`), and a barge is 8.5 m long. The designed quay puts its berths 9 m apart (z -4.5 and +4.5 about the landing's row). So the drawn berth must be up to 1.5 cells from the simulation's berth, which is within a boat's length. The same holds for the waiting places (`WaitY` 2 cells apart, while a barge is 4.25 cells long). The posts need to be at least one barge length apart, so the drawn waiting place can stand up to about 4 cells from the simulation's. The rules do not care: berth and waiting slot are only indices.
- **How far the drawn boat may stand from the simulation's.**
  - Across the river: up to about 1 cell (the lanes and the quay face).
  - Along it: within one boat length (2.1 cells for a rowboat) while sailing, about 2 cells in the S.
  - In the turn after leaving: up to 5 cells. The simulation's leaving boat goes at full speed from the berth in the first tick, so no turn fits inside a boat's length.
  - There are two ways to keep it close: start the push-off before the simulation says Leaving, or let the turn lag and catch up.
- **When a boat will leave is partly known in advance.** A boat that runs out of patience leaves when `Patience` reaches 0, so the drawing can start line-in and the push-off about 1.5 s early. A boat that is filled leaves on the tick of its last load (`LandingDelivery` with that boat's index), which can be seen coming once `Loaded` is one load short.
- **What the simulation gives:**
  - Berths: `BerthX[k], BerthY[k]` (k = 0, 1). `BerthX` is the near bank's water edge plus 1 cell, which is a moored boat's centre, not the quay face.
  - Waiting places: `WaitY[0..2]` with x = `CourseX`, which is the middle of the river. In the design they move to posts in the lee of the near bank.
  - `CourseX[row]` is the middle of the water for each row. It changes by whole cells, which causes the jumps.
  - Heading is the last tick's movement, so it is meaningless when a boat stops (moored at -104 degrees).
  - The drawbridge holds come from `DrawRow` (arriving holds at `DrawRow + 2.5`, leaving at `DrawRow - 1.5`).
- **The prototype's numbers to reuse:** lane offsets 0.85 and 0.95 cells (0.3 by the posts); the S over 4.5 rows; push-off 0.45 cells; top speed 3.6 cells a second (1.6 in the arc); acceleration 2.5 and braking 4.0 cells/s²; the bow turns at most 100 degrees a second. These are first guesses, chosen so these two visits look right. The strips and tests in step 3 of the design should settle them.

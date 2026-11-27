# Bodies and picking: what "hitboxes" and "spreading" should mean for a villager (research, n7)

The owner: "They don't have hitboxes and they don't spread well. Like they all start doing the same task. That is not the idea. I would like each villager to have a role and that it can be assigned."

This paper is about the villager's **body in the world**: being pointed at and clicked, and not standing or walking inside other people. Roles themselves are in `design-options.md`; the code map is `code-map.md`; engine lookups are `engine-notes.md`. Nothing in the game or the simulation was changed. Measurements were made with the simulation alone, on a copy of `sim/` in `build/n7-bodies/` (no Godot runs).

## 0. In short

- **Picking:** a screen-space pick in code against each villager's drawn figure (distance from the pointer to the projected segment from feet to head, nearest wins, a radius of at least 12 px), off in Pave and Extend modes and over the interface, with a hover ring at once, a name tag after the tooltip's 0.25 s, a click that selects and docks a card, and a key that steps through villagers. No physics. It costs a few microseconds per pointer move at 200 villagers.
- **Bodies:** keep the simulation's rules as they are and give villagers bodies **in the drawing**, using one per-frame pass over a coarse grid: walkers keep to one side of the road by direction, a follower keeps a following distance along the road, and standing people take **reserved standing spots** per place. That pass replaces today's `Spread`. The measurements show why. People overlap mostly when they pass each other or walk glued behind someone, and briefly. Nobody overtakes (every walker on a road has the same speed). The present crowd rule slows walkers long before bodies would touch. So a body rule in the simulation would change every recorded result and add almost nothing to play. Real avoidance (RVO, ORCA, boids) is not wanted. On one-cell roads with a deliberate line at every workplace it jitters, it locks up, and it fights the line the build sheet asks for.
- **The one number that shapes everything:** at the normal view a villager is drawn about **3 times life size** (`Main.cs`: `WalkerScale = clamp(15 / pxPerM, 1.1, 3)`). That makes the figure about **2.4 m wide on a road whose paved strip is 0.9 to 1.2 m wide** (`TownGround`: `GravelHalf 0.22`, `CobbleHalf 0.30` cells; a cell is 2 m). At that view two people can never pass on a road without their figures touching. Bodies must be judged at the scale they are drawn, and "never touching" is only possible close in.

## 1. Today's overlapping, measured

### 1.1 How it was measured

`build/n7-bodies/measure/` compiles a copy of `sim/` (`simsrc/`) with a small program. It plays a first age to the charter with the shipped bots (best payback first and cheapest first, seeds 1, 2 and 3). Once a second it takes every pair of villagers and their **simulation positions** (`VillagerView.X/Y`, before the drawing's `Spread`), and sorts each close pair by what the two are doing and where they are. Overtakes are counted every tick. The full output is in `build/n7-bodies/overlap-first-age.txt`. A run takes about a second.

Distances are life-size metres (1 cell = 2 m): **0.5 m** means inside each other at any zoom, **1 m** means touching close in (`TownVillagers.Personal` is 1 m), and **2 m** means touching at the normal view (figure about 2.4 m wide) and is also the build sheet's line spacing.

### 1.2 Results (first age, six runs: 25 to 28 villagers, charter at 30 to 38 minutes)

| | seed 1 payback | seed 2 payback | seed 3 payback | seed 1 cheapest | seed 2 cheapest | seed 3 cheapest |
| --- | --- | --- | --- | --- | --- | --- |
| villagers walking / queued / working (share of time) | 69 / 9 / 22 % | 78 / 4 / 18 % | 69 / 9 / 22 % | 72 / 8 / 20 % | 76 / 6 / 19 % | 76 / 4 / 20 % |
| close pairs at any moment, < 0.5 m | 1.00 | 1.14 | 1.42 | 1.26 | 1.03 | 0.86 |
| close pairs at any moment, < 1 m | 2.16 | 2.49 | 2.71 | 2.46 | 2.22 | 1.92 |
| close pairs at any moment, < 2 m | 5.70 | 5.55 | 6.35 | 5.77 | 4.91 | 4.28 |
| of the < 1 m pairs: walkers passing (same road, opposite ways) | 37 % | 58 % | 35 % | 36 % | 46 % | 49 % |
| walkers following (same road, same way) | 13 % | 20 % | 24 % | 27 % | 23 % | 19 % |
| a walker through a line or a worker | 44 % | 16 % | 32 % | 30 % | 26 % | 24 % |
| walkers at a junction (different roads) | 5 % | 3 % | 4 % | 5 % | 5 % | 8 % |
| exactly on top of each other (< 0.1 m), pair-seconds over the age | 587 | 713 | 1246 | 1377 | 992 | 856 |
| of those, two in the same line on one spot (line longer than its road) | 25 | 63 | 260 | 71 | 19 | 5 |
| longest line (minute) | 11 (27.6) | 15 (29.1) | 13 (28.9) | 11 (35.4) | 11 (38.5) | 5 (29.6) |
| biggest knot: villagers within 1 m of one (minute) | 6 (27.6) | 7 (29.1) | 9 (28.9) | 7 (35.4) | 5 (19.6) | 5 (22.9) |
| overtakes on a road, per minute | 0.0 | 2.9 | 2.8 | 0.2 | 0.1 | 0.1 |
| road-seconds over the crowd rule's comfort | 0.6 % | 3.8 % | 3.0 % | 0.9 % | 1.1 % | 0.8 % |

How long a contact (< 1 m) lasts, sampled once a second: **passing, junction and walker-through-line contacts last under a second**, **followers stay glued for 3 to 4 s on average**, and two people on one line spot for 1.6 to 2.5 s.

Where the < 1 m contacts are (seed 1 payback, the others are alike): on a road 1873 pair-seconds, field 804, well 660, mill 348, market 266, bell green 111, cottage 44. Two working villagers were never closer than 1 m in any run, because work spots are distinct by slot. Idle villagers almost never meet (idle is 0 % of the time in the first age).

### 1.3 What that means

1. **At life size about 15 to 20 % of villagers touch someone at any moment, and at the normal view's drawn size about 35 to 45 % do.** That is the knot the owner sees. The drawing's `Spread` pushes pairs apart by up to 0.6 cells. That removes most exact overlaps standing still (`TownTest` checks `MinStandingGap`), but it does not separate walkers, because a push is eased at `dt * 8` and walkers pass in under a second.
2. **The persistent knot is followers, not passers.** Every walker on a road has the same speed (`RoadSpeed` depends only on the road, its walker count and town-wide factors). So two people who start a road together walk it glued together: they come off the same line, leave the same workplace in the same tick, or arrive at a node together. Overtakes are almost nil (0 to 3 a minute). The pictures show this: in `docs/review/n3/after/ui/u13.jpg` and `u32.jpg` the walkers between the well, the field and the mill are beads on a string.
3. **The line is on the road.** `QueuePoint` puts the line on the road the person came in by, 0.8 cells (1.6 m) apart, **on the road's centre line**. Every walker using that road walks through the line, which is the "walker through a line" share of 16 to 44 %. When the line is longer than the road, `back = min(road.Length, (pos + 1) * 0.8)` stands everyone beyond the end on one spot at the far node. That happened in every run, 5 to 260 pair-seconds an age. `u13.jpg` shows a line of about 20 at the field filling its whole lane. `u32.jpg` (level 22) shows a line of about 30 bent around a corner into a knot where the well, field and mill lanes meet.
4. **Knots peak at the end of the age** (the biggest knot and the longest line come in the last few minutes, 27 to 38 min). Crowded roads are rare (1 to 4 % of road-seconds), because paving raises comfort.
5. **The crowd rule is far stricter than bodies would be.** Comfort on a track is 0.1 walkers per cell (one person per 20 m, `ComfortPerCellTrack`), cobbles 0.5 (one per 4 m). Bodies with a 1 m headway in each of two lanes would allow about 4 per cell. A crowd is already slowed long before anyone could bump.
6. **Second age, not measured here:** river jobs walk river links (`RMode`), and the ferry and the drawbridge make people wait (`RWait`). `u32.jpg` shows a row of figures on the ferry at the crossing. The same spot system (section 4) applies there. A follow-up measure should extend the program to `RMode` walkers.

### 1.4 A body rule in the simulation, tried on the scratch copy

To see what a deterministic body rule does to play, the scratch copy of `Walk` got a **headway**: a walker never comes closer than h behind the walker ahead on the same road going the same way, and it never overtakes. Overtaking was already rare, so this tests "following distance as a rule". Code: `build/n7-bodies/simsrc/Town/TownWalk.cs`, guarded by `Town.Headway`. Run it with `dotnet run -c Release -- headway` in `measure/`.

| headway | charter, payback bots (seeds 1/2/3) | mean | charter, cheapest (1/2/3) | mean | walking ticks held back |
| --- | --- | --- | --- | --- | --- |
| none (today) | 31.68 / 30.33 / 31.27 | 31.09 | 37.33 / 38.45 / 37.49 | 37.76 | 0 |
| 1 m | 31.57 / 33.76 / 29.95 | 31.76 | 37.00 / 38.17 / 37.70 | 37.62 | 0.9 to 1.7 % |
| 2 m | 31.82 / 31.63 / 28.79 | 30.75 | 37.10 / 38.32 / 37.77 | 37.73 | 1.0 to 3.3 % |

The rule changes no mean (the charter moves by up to 3.4 minutes on a single seed, both ways, as small changes cascade through a run). It holds people back 1 to 3 % of the time. But **it changes every run**: the play-through's `charter=33.9min buys=201`, the pacing tables and every recorded first-age trace would have to be recorded again, and the build sheet now forbids touching `sim/`. A rule that changes everything recorded and nothing in play does not belong in the simulation.

## 2. Picking a villager

### 2.1 What exists

- The pointer becomes a map point through `CameraRig.PickLocal(screen, out local)` (a ray to the ground plane in map space; right at any tilt and zoom). A map point becomes a screen point through `LocalToScreen`.
- Pave uses `PaveTarget`: the nearest pavable road within 1.2 cells of the curve on the ground. Extend uses `ExtendTarget`: the ground footprint, or on screen the distance to the segment from a house's foot to its roof. **The second is the pattern a villager needs**, because a figure stands up out of the ground and a tilted camera sees its body above its feet.
- `OverInterface(p)` covers every card. Both pickers find nothing there, and `CameraRig.PressBlocked` starts no drag there. Left releases are never marked handled (the camera must see them).
- Pave and Extend act on the left **release** when `!Rig.LeftDragged`, so a drag of the map never buys anything. A villager click must do the same.
- One tooltip control (`TownHud.Tip.cs`): 0.25 s delay, 0.12 s fade-in, placed by `HudZones.TipRect`. Its text is made when the pointer reaches an owner and refreshed twice a second, never per frame. The layout sheet (`docs/plans/n3/layout-sheet.md` sections 4 to 6) allows only a few things into the middle of the screen: a tooltip while hovering, the map's own tags, and "the pointer's tip in a click mode: tooltip style, beside the pointer, clear of the target".
- `TownHud.CoversVillager` already projects every villager each frame (`LocalToScreen` of `v.X, 1, v.Y`), so there is a precedent for the cost.

### 2.2 The options

| | How | Cost at 40 / 200 villagers | Good | Bad |
| --- | --- | --- | --- | --- |
| **A. Screen-space, in code** | Project each villager's drawn feet and head to the screen. The distance from the pointer to that segment, minus a radius, picks; nearest wins | Projection through `Camera.UnprojectPosition` (an engine call) is estimated at 0.2 to 1 µs each: 8 to 40 µs at 40, 40 to 200 µs at 200 per pick. Projecting in C# with the frame's camera matrices is estimated at 10 to 20 ns each: under 5 µs at 200. Estimates, not measured (no Godot runs) | Matches what the player sees at any tilt. A fixed pixel radius makes a 16 px figure as easy to hit at every zoom. Same pattern as `ExtendTarget`. No nodes, no allocation | Must use the **drawn** position (after the body pass), not the simulation's, or the click misses what is seen |
| B. Ray against a capsule per villager, in code | `PickLocal`'s ray, then a ray-capsule test per villager in map space | About the same as A | Exact depth order | Hit size is in metres, so a far view gives a 3 px target unless the radius is scaled by zoom. That is A done the hard way |
| C. Physics areas | An `Area3D` with a capsule per villager, moved every frame, `InputRayPickable` or `IntersectRay` | 40 to 200 nodes and shapes moved every physics frame: a per-frame cost **not measured** (`engine-notes.md` section 2), plus a `Dictionary` per ray query (allocation, which `game-perf` gates) | Engine's own mouse-enter and exit signals | Per-frame cost and allocation for something used a few times a minute. Signals can arrive out of order for small fast shapes (the docs say so). Positions must still be written from the drawing. A whole new system |

**Recommendation: A**, projected in C# from the camera's matrices taken once per frame (or `LocalToScreen` while there are under 100 villagers, if simpler first). Pick on pointer motion, and once a frame while the pointer is still, since the view and the villagers move under it, as `PaveFrame` already does for roads.

### 2.3 Making a sixteen-pixel figure easy to hit

- **Target shape:** the segment from the drawn feet (`y = 0`) to the head (`y = ModelHeight * WalkerScale * 0.9`) on screen. The pick distance is the distance to that segment. A hit is a distance under `R = max(12 px, 0.6 * projected figure width) * ui scale`. At the normal view that is about a 24 px wide, 28 px tall pill around a 16 px figure. Close in, the figure itself is the target.
- **Nearest wins.** Ties within 2 px go to the figure in front (lower on the screen, so nearer the camera).
- **Stickiness (hysteresis):** once a villager is hovered, it stays hovered until the pointer is more than `1.5 R` from it, or another figure is at least 4 px nearer. The hover follows the person, not the spot. A walker moves about 6 px a second at the normal view at speed 1, and about 50 px a second at speed 8. Without stickiness the ring would flicker from person to person in a crowd.
- **Dwell:** the ring shows at once (it is the hit feedback). The name tag waits the tooltip's own 0.25 s (`TipDelay`) and fades in 0.12 s, so sweeping the pointer over a crowd does not flash names.
- **A crowd:** with bodies spread (section 3) the nearest-wins rule picks the person under the pointer. In a line, R is wider than the 2 m spacing at the normal view, so the nearest still wins and the ring makes the choice visible before the click. The scroll wheel zooms, as now. No cycling inside the crowd under the pointer is needed. The key does that (below).
- **Under the interface:** no pick when `OverInterface(p)`. Also no pick when `GetViewport().GuiGetHoveredControl() != null` (the engine-notes guard, for controls with `Pass` or `Ignore` filters). No pick while `PauseMenu.IsPaused` or while a book transition runs. A figure under a card can be reached by moving the view, or by the key.
- **Villagers inside buildings:** mill workers stand inside the footprint (`WorkSpot` default case), and the build sheet puts the miller at the door. They are picked where they are drawn. A worker hidden by a roof is reached by the key, or through the place (a later step: hovering the mill lists who works there).

### 2.4 Beside Pave and Extend

- **In a click mode the mode owns the map**: villager hover and click are off, and no ring shows. That is the simplest rule, and the one `design-options.md` proposes too. Entering a mode keeps the selection (the docked card stays) but hides the hover ring.
- **Outside a mode** a left release without a drag (`!Rig.LeftDragged`) that hits a villager selects it. A release that hits no villager on the open map clears the selection. A right click clears it too. The release is never marked handled.
- Priority if more clickable things come to the map later (a building's card, say): **villager before building before road**, because a villager is the smallest target and stands on the others.

### 2.5 Keyboard

- Two new InputMap actions in `settings/GameInput.cs`: `tt_next_villager` (V) and `tt_prev_villager` (Shift+V, or C if a modifier cannot be bound). Their key caps are printed from `GameInput.Bound/Tag` like every other key.
- The order is the **simulation's villager order** (id), which is stable and the same for everyone. It is not screen order, which changes as people walk. With a role filter later, the key steps through the villagers of the selected villager's role.
- Stepping selects the villager and **brings it into view only if it is off screen** (`Rig.LookAt` without zoom, like Pave's `ModeLook`). The camera never follows on its own. A "Follow" button on the card follows that villager until the player moves the view (`UserMoved`).
- Esc: decide whether the first Esc closes the card and a second one pauses (the usual pattern) or Esc always pauses (today's rule). This is a question for the owner (section 7).

### 2.6 What is drawn

- **Hover:** a thin wheat ring flat on the ground under the feet, in the hint ring's style (layout sheet section 5), radius `0.55 * WalkerScale` m, fading in over 0.1 s. No lift and no glow on the body: a lift would read as a jump in the simulation, and the build sheet's poses already move the body.
- **Selected:** the same ring, solid and a little thicker, with a soft ink outline. It moves with the villager every frame. The figure itself can be brightened by one float in `INSTANCE_CUSTOM` in `villager.gdshader` (the body already uses a `ShaderMaterial`; `UseCustomData` must be set before `InstanceCount`, `engine-notes.md` section 5). That is optional; the ring does the work.
- Cost: one small `MultiMeshInstance3D` with two instances (hover, selected), written each frame. No allocation.
- **The name tag on hover:** the map's tag style (layout sheet section 5), above the figure. It holds the name and one line of what the villager is doing ("Ada Thatcher, carrying flour to the market"). It is placed clear of the figure and fades out when it would touch a visible card. Its text is built when the hovered villager or its phase changes, never per frame.
- **The card when selected:** **docked in the left column under the request card** (where notes stack), not following the figure. A card that follows a walker moves all the time, covers the map and breaks the rule that the middle stays empty. A docked card fits the layout sheet: cards live at the edges, and only tooltips and tags enter the middle. The ring and a short wheat leader line, drawn only while the figure is on screen, tie the card to its person.

## 3. Bodies that do not pass through each other

### 3.1 The options judged

**1. In the drawing, improved (replacing `Spread`).** One pass per frame over the drawn positions, after interpolation and before the transforms are written:

- **Lanes by direction:** a walker is drawn offset to one side of its road, by direction and not by id, so two people meeting pass side by side. Keep left or keep right is a choice for the owner (section 7); left suits an English-feeling town. The offset grows with the road: a track 0.18 cells, gravel 0.24, cobbles 0.32, scaled up a little with `WalkerScale` but capped so a figure's feet stay on the strip. **Paving then shows: on cobbles two streams pass clear of each other, on a track they brush.** Corners: the offset follows the curve's normal (`Road.At` gives the heading), so a figure never cuts a corner through a house.
- **Following distance:** along each road and direction, the drawing keeps a drawn gap g (`1.2 m * WalkerScale`, about one figure) behind the one ahead. A follower is drawn at `min(sim distance, leader's drawn distance - g)` along the curve. The lag is at most g at walking speed, about 0.3 to 1 s, and it is eased away when the leader leaves the road. Followers entering together (the measured 3 to 4 s glued pairs) then show as two people one behind the other. If more than two would bunch, the third may step into the other half of the lane (side by side) when the opposite lane is empty: a crowd then fills the road's width.
- **Standing spots (section 4):** the line, the work spots and the yard spots come from one table per place. A standing person is never pushed. **The line stands at the road's side** (its lane offset), not on the centre line, so walkers pass beside it. That one change removes most of the "walker through a line" contacts.
- **A small separation for what is left:** pairs still inside a body's width (two lines meeting at a junction, a walker passing a worker at a door) get a push like today's, but only **along the road's normal for walkers** and **never into a building's footprint** (`OutOfBuildings` applies to everyone, not only those in a line). The push is clamped to the road's half width plus 0.1 cell.
- **Note for 200 villagers:** `TownVillagers.Capacity` is 96 today; villagers beyond that are not drawn (and so cannot be picked). It must rise with the second age's numbers.
- **Cost:** today's `Spread` is O(n²): about 1,600 pair tests at 40 and 40,000 at 200, every frame. The new pass buckets drawn positions in a coarse grid of one cell (flat `int[]` heads and next links, sized once), so it tests only neighbours: about 10 tests per villager. Lanes and following need the walkers of each road in order: sort by distance along the road with an insertion sort into a pre-sized per-road array (walkers on a road are few, 1 to 19 measured). Estimate: under 0.05 ms at 200 villagers. No allocation.
- **Play:** none. **Look:** lanes, gaps and lines that read as people. **Risk:** the drawing lags the simulation by up to one gap. Picking and tags must read the drawn position, which `TownVillagers.Shown(i)` already offers (it would add `_ox/_oy`).

**2. In the simulation, cheap and deterministic.**

- *Reserved standing spots and a lane offset in `Place`* write only the villager's X, Y and heading. Checked: no rule in `sim/Town` reads a villager's X or Y; they are output for drawing and saving. So spots and lane offsets can move into the simulation later **without changing any result** (the play-through number stays), as `design-options.md` step 1 proposes for the lane offset. That is worth doing once roles need "where does this person stand" in the simulation's own terms. It is not needed for the look.
- *Lanes and headway as a rule* (no overtaking inside a lane, a road's lanes from its surface, capacity = lanes x length / headway): measured above. With 1 to 2 m headway, people are held 1 to 3 % of the time, the pacing means do not move, and every recorded run changes. Replacing the comfort rule with lane capacity would be a real rules change. Comfort today allows one walker per 20 m on a track and one per 4 m on cobbles. Physical capacity is 10 to 40 times higher, so lanes would make crowds slow people **less**, and paving would matter less unless headways were set unrealistically long. The present rule is better as a game rule. The bodies should **show** it: when a road is over comfort its walkers are already slower, and the drawing fills the road side by side (above), so the player sees the crowding the rule charges for.

**3. Real local avoidance (boids separation, RVO, ORCA).**

- *In the simulation:* deterministic only if written by hand in fixed order with the toy's float rules. Cost about O(n k) with k neighbours a tick, affordable at 200. But on one-cell roads with a line at every door, ORCA's known failures are this game's normal case: agents meeting head-on in a corridor slow to a stop or oscillate, a line compresses and expands like a spring, and people step off the road to get past. It would also change every result (as the headway did) and move the line away from the spots the build sheet tests (1.6 to 2.4 m, in queue order).
- *In the drawing:* the drawn figures drift from the simulation's positions (picking, tags and coin numbers would follow the drift), and Godot's own avoidance is "not confirmed" deterministic or usable without a navigation mesh (`engine-notes.md` section 4).
- *Verdict:* no. Roads are lines and the game is about which line people walk. Lanes and order along the line give everything avoidance would, and they look calmer.

### 3.2 Recommendation

**Bodies live in the drawing: one "bodies" pass (lanes by direction, following distance, reserved standing spots, a bounded push that never enters a building), on a neighbour grid, replacing `Spread`.** The simulation keeps its crowd rule unchanged. Later, when roles need it, standing spots and the lane offset can move into `Place`, which changes no result. Build in this order:

1. lines at the road's side, using the spots table that `WorkSites.cs` already plans (fits the build sheet's section A; the build sheet's builder may already be doing the "2 m apart, bends along the lane, never through a building" part, so coordinate rather than duplicate);
2. lanes by direction;
3. following distance;
4. the grid in place of the O(n²) loop (needed before 200 villagers);
5. width-filling in a crowd.

## 4. Spreading at places

### 4.1 Where people stand today (`TownWalk.Place`, `WorkSpot`, `QueuePoint`)

| Where | Spot | Problem |
| --- | --- | --- |
| In a line | `QueuePoint`: on the arrival road's centre line, (pos + 1) x 0.8 cells from the door node, clamped to the road's length | On the walkers' path. Overflow beyond the road's end stacks on one spot (measured). 1.6 m spacing (the build sheet wants about 2 m) |
| Working, well | Rim, angle `a0 + slot * 2.3` rad | Fine (never measured closer than 1 m) |
| Working, field | Rows by slot, staggered | Fine |
| Working, mill and others | Inside the footprint beside the door, alternating sides 0.6 cells | Fine, but hidden inside the mill. The build sheet moves them to the door |
| Idle at home | `WorkSpot(home, v.Id)`: door and yard, 6 positions from `id % 2` and `id % 3` | Two people of one cottage whose ids differ by a multiple of 6 share a spot. Rare (cottage contacts 40 to 131 pair-seconds an age) |
| Idle at a node that is not home | The node's cell centre | Everyone idle there stacks (idle is about 0 % of the first age) |
| Selling at the market | Does not stop: walks through the market node | Overlaps at the market are walkers meeting at its node (45 to 880 pair-seconds); handled by lanes |
| River places (second age) | Crossings, the ferry, the landing (`TownRiverJobs`) | Not measured. The ferry row in `u32.jpg` reads as a white string |

### 4.2 A system of reserved standing spots

- **Per place, a small fixed table of spots** in the place's own frame, made once from its footprint, door and roads when the plot is given (or on load):
  - door spots (one or two, facing in);
  - work spots (today's `WorkSpot` by slot, plus the build sheet's poses);
  - **line spots for each road into the door**, at the road's side, 2 m apart, following the curve, continuing on the next road (or bending along the lane) when one road is too short, and never inside another plot's footprint or another line;
  - yard spots for a cottage (door, bench, butt, garden edge: 6 to 8);
  - at the market, two **pass-by spots** at the table's end, so a seller dips at the table, not at the node.
- **Reservation:** a spot has an owner (`int[]`, -1 free). People take spots in queue order for a line (position k takes line spot k), by slot for work, and by the lowest free yard spot for home, assigned by id when the person arrives, so the result is the same every time. A spot is freed when its owner leaves. **A line always shows its true length** (build sheet): when a line outgrows its spots, its tail continues along the next road, and is never stacked.
- **Where it lives:** in round 1, in the drawing, in `game/scripts/town/WorkSites.cs`, the interface the build sheet already has the villagers' builder write ("where its work spots, its line and its goods are in the drawing"). The line part of this system is section A of the build sheet. This paper adds the side-of-road placement, overflow onto the next road, yard spots and market pass-by spots. Later, if a role or a test needs the simulation to say where someone stands, the same table moves into `Place`, which changes no result.
- **Tests:** add to `game-work`'s line check: "no two standing villagers closer than 1.2 m x WalkerScale; no standing villager on a road's centre line within 0.15 cells; no line spot inside a plot". Add to `TownTest`'s W3: the per-frame minimum gap of **walkers passing** (lanes) is above 0.35 x figure width.

## 5. How picking and bodies meet roles

**The card (docked, left column, about 296 wide, the tooltip's width):**

1. **Name** (body, serif), the small figure in its tunic colour, and the cottage ("of the cottage on Mill Lane", or "Cottage 3").
2. **Role** with its tool icon, and the control to change it: "Role: Reaper [change]". It opens a short list of the roles open now. Locked roles show why ("Opens with the mill"). This control is the roles design's; the card is its place on the map.
3. **Now:** one line from `Phase`/`Job`/`Stage`/`Workplace`/`QueuePosition`: "Carrying grain to the mill", "Third in line at the well", "Reaping the east field".
4. **Carries:** the good and amount, with the Load level's icon.
5. **Earned and trips** this age: "214 coin, 37 trips". Counts only.
6. Buttons: **Follow**, **Next of this role** (the key), close.

**What the simulation must have:**

- **An id that stays the same.** `VillagerState.Id` is the index into `S.Villagers`, and villagers are only ever added, so an id is stable for a town's life and is saved. The game keeps the selection as an id, and does not keep it across a new town (`Driver.Replaced` clears it).
- **A name from the seed:** a pure function `Town.VillagerName(id)`, drawn from a fixed table with a hash of seed and id (like the skippers' names in `TownRequests`/`TownRiver`, which already hash seed and salt). It is not stored, so it needs no save change and changes no result. A test pins a few names per seed.
- **Earned and trips per villager:** two counters in `VillagerState`, written where a delivery is paid (`Emit` of `Delivery`). They are output only: no rule reads them, so results do not change. Saved: save format +1, and old saves load with 0.
- **Role:** a field in `VillagerState` and a player action `SetRole(id, role)` applied at a tick like `Buy`. This belongs to the roles design; the card only calls it.
- Nothing in picking or bodies needs more from the simulation than `VillagerView` gives today (`Id`, `Home`, `Phase`, `Job`, `Carry`, `Amount`, `Workplace`, `QueuePosition`, `Road`, `Dist`, `Heading`). The following distance needs the walking direction, which the drawing can take from `Heading` against the road's heading, or a `Dir` added to `VillagerView` (one line, output only).

## 6. Build plan, smallest first (each step ships alone)

| Step | What | `sim/` | Drawing and interface | Tests | Per-frame cost | Risk |
| --- | --- | --- | --- | --- | --- | --- |
| 1. Point at someone | Screen-space pick (2.2, 2.3), hover ring, name tag after 0.25 s; off in click modes and over cards | `Town.VillagerName(id)` (pure, no state) | `TownVillagers.Pick(screen)` on drawn positions; `TownHud.People.cs` for hover and tag; ring multimesh | `town.tscn`: hover a known villager by `LocalToScreen`, the tag names it; a click in Pave mode on a villager standing on a pavable road paves the road; no pick over the shop; no handled release (the map does not drag after a click) | Under 5 µs per pick at 200 (estimate); 2 transforms for the rings | Low |
| 2. Select and the card | Click selects, docked card (name, cottage, now, carries), right click or empty-map click clears, V / Shift+V step, Follow | none | Card in the left column; `GameInput` actions; text rebuilt only when a value changes | Card inside the column on the six canvases (`game-layout`); stepping goes through every villager; selection cleared on new town; allocation per frame unchanged (`game-perf`) | One card's text only on change | Low. Esc behaviour is the owner's choice |
| 3. Lines at the road's side | Line spots in `WorkSites.cs` at the lane's side, 2 m apart, overflowing onto the next road; never on a building or a crop | none | `WorkSites.cs` (shared with the build sheet's builders: do it with them or after them) | `game-work` line check plus "off the centre line" and "no two line spots within 1.6 m" | Spots computed once per place on change | Medium: two other builders work in this file now |
| 4. Lanes by direction | Walkers drawn to one side by direction; offset by surface (paving shows) | none (or `Dir` in `VillagerView`) | The bodies pass, part 1 | W3: passing walkers' minimum gap above 0.35 x figure width; `game-motion`: no jump when a walker turns at a node (the offset is eased across the node) | O(n) | Low |
| 5. Following distance, grid, width-filling | Followers one figure behind; a third steps alongside when the other lane is free; the grid replaces the O(n²) `Spread` | none | The bodies pass, part 2; `Spread` removed | W3 for followers; a perf check at 200 villagers (`perf.tscn` with a 200 villager town) | O(n) with about 10 neighbour tests each | Medium: a drawn lag behind the simulation; tags and coin numbers must use drawn positions |
| 6. Earned and trips | Counters on the card | Two output-only fields in `VillagerState`, save +1 | Card lines | Save round trip; the play-through still reads `charter=33.9min buys=201` | none | Low |
| 7. Roles on the card | The role line and its change control | Roles design's `SetRole` | Card control | The roles design's tests | none | Roles design's |
| (later) Spots into `Place` | Spots and lane offsets in the simulation, if roles or tests need them | `Place` only (positions), no rule | The drawing reads them | A test that hashes everything except positions is unchanged | none | Low; only if needed |

Steps 1 and 2 answer "hitboxes" in the first reading. Steps 3 to 5 answer the second. None changes how the game plays, and none touches `sim/` beyond a name function and two output-only counters.

## 7. What the owner must decide

1. **Bodies are a look, not a rule.** Is that right? This paper recommends that crowds keep slowing people by today's comfort rule, and that bodies are drawn, not simulated. The alternative, people physically blocking each other in the simulation, was measured: it changes nothing in pacing, changes every recorded run, and would need longer-than-real gaps to matter.
2. **How big a villager is at the normal view.** Today 3 times life size (2.4 m wide on a 1 m road), so at that view people passing on a road always touch. Either accept a brief touch at the far view, or cap `WalkerScale` at about 2 (figures about 11 px tall instead of 16) so lanes can separate them.
3. **Keep left or keep right.**
4. **The card's place:** docked in the left column (recommended) or floating beside the villager.
5. **Esc with a card open:** closes the card first, or always pauses.
6. **Names for everyone** (the card's first line), or "a villager of Cottage 3" (also asked in `design-options.md`).

## 8. Files

- `build/n7-bodies/measure/Program.cs` (the measure), `build/n7-bodies/simsrc/` (a copy of `sim/`; only `Town/TownWalk.cs` differs, the headway experiment), `build/n7-bodies/overlap-first-age.txt` (full output).
- Read: `game/scripts/town/TownVillagers.cs`, `TownHud.Pave.cs`, `TownHud.Extend.cs`, `TownHud.Tip.cs`, `TownHud.cs` (`OverInterface`), `game/scripts/CameraRig.cs`, `game/scripts/Main.cs` (`WalkerScale`), `game/scripts/town/TownGround.cs` (road widths), `sim/Town/TownWalk.cs`, `TownViews.cs`, `TownState.cs`, `TownRules.cs`, `docs/plans/n5/work-build-sheet.md`, `docs/plans/n3/layout-sheet.md`, `docs/plans/n7/research/engine-notes.md`, `design-options.md`, the pictures `docs/review/n3/after/ui/u13.jpg` and `u32.jpg`.
- Not done: no Godot run (shared machine), so picking and drawing costs are estimates; the second age was not measured.

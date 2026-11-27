# Boats, the landing and the crane: a design (the lead, 27 November)

The owner: "Next start work on the boats. I want the animation of them going through the river to be better and the dock + crane to work better. Work out a design etc. like this can be done a lot better design wise but also like use wise as the boats move terrible through the river."

## What happens now (read from `sim/Town/TownRiver.cs` and `game/scripts/town/RiverBoats.cs`, `RiverLayer.Landing.cs`)

1. **A boat zig-zags.** Its place across the river is the middle of the river *for the whole row of the map it is in*. Where the river bends, the boat jumps sideways once per row, three times a second, and its bow is turned to face each jump. Nothing smooths it: the picture shows the simulation's numbers as they are.
2. **One speed, dead stops.** Three cells a second everywhere; it stops in one tick at the berth, at its waiting place and below the drawbridge, and starts again at full speed.
3. **Coming alongside** it slides diagonally over the last four rows, then its bow snaps to point up-river in one tick.
4. **Leaving** it jumps from the quay to the middle of the river in one tick, its bow flips round in one tick, and it goes back the way it came.
5. **Boats pass through each other.** Those that wait lie on the same middle line that the next boat sails along.
6. **Nothing moves on the boat:** no oars, no bob, no lean; the wake switches on and off in one frame; cargo appears on board by itself.
7. **The crane swings slowly back and forth all the time**, whether or not anything is being loaded, with the same sack on its hook. It makes boats ask for more cargo (a rule), and shows nothing of it.
8. **The landing is a short jetty.** Villagers bring goods to it and stand in a knot; the goods vanish there. What a boat wants and how long it will wait is a line of text floating over the water.

## The design

**Rules stay as they are:** when boats come, what they want, what they pay, how long they wait, the berths, the crane's effect, the tolls. The simulation's own clock along the river (how far up-river a boat is) stays the truth. What changes is everything the eye sees, and where a boat lies across the river.

### A. The course: a boat follows the river like a boat

- **A smooth course** through the middle of the river, bend by bend (a curve through the row middles, not the row middles themselves), with the bow along the course and a little lean into each bend.
- **Two lanes.** Boats coming up keep to the landing's side, boats going down keep to the far side, and **waiting boats lie at mooring posts** out of both lanes, bow into the stream. Nobody passes through anybody.
- **Speed like a boat:** it gathers way and loses it over a boat's length or two, slows a little in the bends and for the drawbridge (holding station below it, bow working against the stream, instead of standing dead), and is never more than a boat's length from where the simulation has it.
- **Alive on the water:** a slow bob and a slight roll; a rowboat's oars sweep and the boat surges a little with each stroke; a barge is heavier and steadier, with two long sweeps; the wake is a V that grows with speed and fades in and out; a loaded boat sits lower than an empty one.

### B. Coming in, lying alongside, going away

- **Coming in:** off the lane on a long S, slowing, to lie alongside the quay bow up-river; a mooring line goes to the bollard; the gangplank comes down.
- **Lying moored:** it rides gently on its line, lower at low water (the quay's ladder shows), and its hold fills as it is loaded.
- **Going away:** line in, gangplank up, it pushes off sideways, the stream takes the bow and swings it round in an arc, and it gathers way down-river in the far lane. A boat that leaves full sits low and flies a pennant; a boat that gave up waiting leaves riding high.
- **Waiting for a berth:** at a mooring post, swinging a little with the stream; when a berth comes free it slips its line and comes in.

### C. The landing: a quay that works

- **A quay along the bank** as long as its berths, timber on piles, with a bollard and a ladder per berth and room behind it. It replaces the short jetty and keeps the landing's plot.
- **A place for everything:** villagers come along one lane and set their goods down in the **yard**, each kind on its own stack; they wait in a line, not a knot (the same line as at the well). The stacks show what is ready to go aboard.
- **A sign at each berth** instead of text over the water: the good the boat wants (the icon), how much is aboard of how much (a bar), and how long it will stay (the bar's colour, as the ledger's request does). A request boat's sign carries the skipper's mark. Readable from the normal view; the full text stays in the tooltip.
- **Without a crane** the goods go aboard by hand: each load crosses the gangplank in a short arc.

### D. The crane: it loads

- **The crane does the loading you see.** For each load that goes aboard it lifts a netful from the yard's stack, swings it over the hold, lowers it and swings back; its treadwheel turns while it works and stands still when there is nothing to load. When loads come faster than it can swing, it carries more in each net: it is never behind and never swings empty.
- **It stands between the berths** on a stone base at the quay's edge and reaches both.
- **Each crane step bought shows:** a longer jib, then a second sheave and a bigger net (the rule it stands for is "boats take more").

### E. What it must not do

- No rule, price, timing or key changes; the save format stays; the second-age play-through and the river bot's results are unchanged.
- Nothing in one frame; no allocation per frame; the per-frame cost tests pass untouched.
- It must read at the normal view: the lanes, the turn, the crane's swing, the signs and the stacks carry it.

## How it will be shown and judged

1. **The journey, now beside planned:** the real track of one boat's whole visit, taken from the simulation tick by tick and drawn as boat shapes on the real river, beside the same visit on the designed course.
2. **The landing, now beside planned:** a real frame of today's landing beside a staged still of the quay, the yard, the signs and the crane mid-swing, made with models built like the game's.
3. After the owner's yes: strips of a boat's visit (twelve frames) before and after; tests that no boat moves or turns more than a set amount between two frames, that no two boats overlap, that no boat touches the quay or a bank, that the crane's lifts equal the loads taken aboard; the whole check; a Windows build.

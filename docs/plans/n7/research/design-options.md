# Villager roles: four designs, criticised, and one recommendation

For the lead. Design thinking only; nothing was built, run or changed. Written 27 November from `incremental-design.md`, `town-sim-api.md`, `age2-design.md`, the n3/n4/n5/n6 sheets, `sim/Town/TownJobs.cs` (`ChooseJob`, `ExpectedWait`, `StartWork`), `TownState.cs` (`VillagerState`) and the pacing tables in `town-pacing.md`.

The owner: "They don't have hitboxes and they don't spread well. Like they all start doing the same task. That is not the idea. I would like each villager to have a role and that it can be assigned. Like I would like to add that as a full system."

## 0. What the code tells us before any design

Four facts shape every option below.

1. **Why they all do the same task.** `ChooseJob` is a pure function of where you stand and what the town looks like: every idle villager takes the job with the best coin per second. Villagers who finish at about the same moment, at the same door, compute the same answer. The only brake is `ExpectedWait`, and it counts only those already walking, queued or working, so it reacts one step late: a herd forms, queues at the best workplace, and the next arrivals scatter to the second best. The herd then moves together to the next job. So "they all do the same thing" is partly a bug of one rule that is too deterministic, and a role system built *on top of* that rule inherits it unless the rule itself gets a lean.
2. **Places are the real limit, not villagers.** Well 1 place (2 with the second bucket), field 2, mill 2, pasture 3, weaver 2, plus at most 2 extra hands per workplace. The pacing tables show 12 villagers at minute 10 and 22 at minute 20, but the first age has about 3 working workplaces. Most villagers at the end of the age are walking or standing in a line. This is the picture in `u11.jpg` and `u13.jpg`. A role system that lets the player say "6 reapers" when the fields have 4 places only makes a longer line. **Every design must show places next to counts** and must treat "more places" (hands, second field, second bucket) as the thing that makes a role count meaningful.
3. **The best assignment is nearly solved.** If places are the limit, the best use of N villagers is "fill every place, best-paying first". `ChooseJob` already does about that. A manual assignment screen therefore competes with an automatic answer that is nearly optimal. For the choice to be real it must be about something the automatic rule cannot know: a request or charter drive the player wants to hit, the tide, a boat the player wants to fill, a trade the player has invested in, or simply what the player wants to *see*.
4. **Nothing in the sim names a villager.** `VillagerState` has `Id`, `Home`, job fields, carry and position. No name, no skill, no history, no lateral offset. `VillagerView` exposes the same. A click card (name, home, role, carry, earned) needs new state in the sim (name by id from a seeded table, `Earned`, `Trips`), which must be saved.

A honest baseline, **Design 0, "fix the spread, add no roles"**: give every villager a deterministic aptitude (a leaning of plus or minus 10 to 20 percent per job kind, from the seed and the id) and add the herd's own pending choices to `ExpectedWait` in the same tick. This costs little, needs no interface and would already make the town look varied. It does not answer the owner, who asked for roles that can be assigned ("a full system"), but it is the floor under every design below, and each design keeps it as the *default behaviour of villagers without a role*.

## 1. Shared vocabulary and the hitbox, since all designs need them

**Role, trade, job.** A *job* is what exists today: one trip (water home, grain to market...), chosen per trip. A *role* (I use "trade" on the screen) is a standing leaning: a set of jobs a villager prefers, with a name, a tool in the hand, a place in the story. The difference is that a job lasts a minute and is chosen by arithmetic; a trade lasts until the player changes it and is chosen by a person.

**How a trade meets `ChooseJob` (the same in A, B, D).** A trade is a *weight*, not a wall. In `Consider`, a job of the villager's own trade scores `rate x 1`, jobs of other trades score `rate x OffTrade` (about 0.55; one number in `TownRules`). A villager with no trade ("Hand") uses `x 1` everywhere, so with nobody assigned the simulation is bit-identical to today (a test can assert it against the existing hashes). With a soft weight, "blocked" behaviour needs no new code: a reaper whose field has a queue of four will see another job beat 0.55 times the field's rate and go and help, then return by the same arithmetic. A hard filter would need explicit help-out rules and would freeze villagers in lines. (A "strict" toggle per trade, for players who want it, is a late option; see open questions.)

**Hitbox, two meanings, one rule for both.**
- *Pointing:* a villager is a pick target with a screen-space radius of about 14 px (the body is about 16 px tall at the normal view; a small body gets a larger target, as a button would). The nearest villager to the pointer wins within the radius; a villager beats a road or a building under the same pointer; a villager inside a building (at the door, working at the mill) is picked at the door spot. Hover shows a ring and the name; click opens the card. This must use `CameraRig.PickLocal`/`LocalToScreen` like `PaveTarget`, and must not mark the left release handled (AGENTS.md). In Pave and Extend click modes the villager pick is off, so the modes keep working.
- *Bodies:* soft separation, not collision. Every villager gets a **lane offset** in the simulation (`Place` adds a sideways offset of up to about 0.15 cell, from the id and the direction of travel: keep to your right, so two people meeting on a road pass each other instead of through each other), and every queue and work spot is already a distinct place (`QueueSpot`, `WorkSpot`). Do not make bodies block each other on roads: the crowding rule (comfort per cell, down to 0.35) already is the town's "bodies in the way", it is balanced and tested, and a hard block would deadlock single-file lanes and rewrite pacing. AGENTS.md says "no display fan-out": the offset must therefore live in `Place` (sim) and be deterministic, not be added by the drawing. It changes X, Y only, never an economic quantity.

**Card (the click, in every design).** A small paper card beside the villager, 232 wide, in the tooltip's family (uses `TipRect`), never in the middle band for more than the hover:
```
Aldith Marsh                      [trade icon]
Lives at the third cottage      Reaper, level 3
-----------------------------------------------
Carrying   2 Grain, to the market
Earned     o 1,240   (412 trips)
Likes      the sickle
[ Reaper v ]  [ Home ]            (design dependent)
```
"Likes" is the aptitude from Design 0. In A the card is read-only apart from one trade chooser; in B it is the main control.

**Roles per age, a common table** (all designs draw on it):

| Age | Trade | Jobs it prefers | Place it works | Tool in the hand (n5 sheet) |
| --- | --- | --- | --- | --- |
| I | Water carrier | Water | the well (1 to 2 places) | pails, yoke |
| I | Reaper | Grain home, Grain to market | fields (2 each, plus hands) | sickle, scythe |
| I | Miller | Flour (field, mill, market) | the mill (2) | sack hoist |
| II | Shepherd | Wool to market or boat | the pasture (3) | shears |
| II | Weaver | Cloth to market or boat | the weaver's cottage (2) | shuttle |
| II | Boatman's hand | Grain, Flour, Wool, Cloth to the landing | the landing, crossings | hand-truck, rope |

Three trades in the first age, six in all. Three is on purpose: more trades than workplaces is a list without content. The Miller's trip is field, mill, market in one go, so a Miller also harvests; in the card it reads "Miller (from the field)". A *Hand* (no trade) is not a role the player assigns but the name for "follows the best job", shown as "Hands" in lists.

---

## 2. Design A: Trades by count

The player does not see people, only **trades with a number**: "Water carriers 1, Reapers 3, Millers 0, Hands 4". Plus and minus move people between trades and Hands.

1. **What a role is.** One of the six trades above, a count per trade. Differs from a job: a standing preference that the player sets, applied to whichever villagers are free at that moment.
2. **Assignment.** A count per trade, plus and minus, in one panel. Who moves is chosen by the game: the idle villager nearest the trade's workplace, else the one with the lowest earnings in their old trade. No clicking of villagers. Nothing done: all villagers are Hands (today's behaviour plus Design 0's aptitude spread). A new villager joins the trade that is furthest below its *place count* (the places at the workplaces), once the first assignment exists; before that, a Hand.
3. **When blocked.** The soft weight: help elsewhere by arithmetic, come back. The panel shows "3 reapers, 1 helping elsewhere" so the player sees why.
4. **Growth.** Trade counts are not bought; the *trades* are unlocked by buildings (Reaper at the first field, Miller with the mill, Shepherd with the pasture) and the *panel* by level 3. Tools and Load stay global. One new shop row "Train" (suggested trade, like Pave's suggested road) raises a trade's level: +4 percent work speed and carry in that trade, 10 steps, a milestone at 5 and 10 (the repo's own rhythm). Buys per minute stay the same shape as today.
5. **Map.** Roles decide who walks where, in bulk. Reapers' fields and the market lane wear first and hardest; with trades set, the well lane and the mill lane wear on a schedule the player chose. Reasons to pave: you can pave the road your 4 reapers walk. The look of the town changes mildly: the same people on the same roads, a little more even.
6. **Spreading.** Counts force a distribution: by construction at most N follow each trade. Standing in the same spot is solved by queue spots plus the lane offset, not by roles.
7. **Click.** The card is read-only (name, trade, carry, earned) with a button "Show in People" that opens the panel row.
8. **Interface.** A **People page** in the shop's People group: one row "People" that opens a card, or, better, a *compact block inside the People group* once two trades exist:
```
People                         14 villagers
Water carriers   [-] 1 [+]   1 of 2 places
Reapers          [-] 4 [+]   4 of 4 places
Millers          [-] 0 [+]   0 of 2 places
Hands            6   (follow the best job)
```
   Four lines, caption size, on the shop's rail (320 wide). First minute: not present; the block appears with the first field (level 3), with the line "Name a trade." Someone who never opens it sees only the Hands behaviour and the new villager-card on click.
9. **Cost and risk.** Sim: one `Trade` byte per villager, a count request resolved at the next idle moment, weights in `Consider`, save version +1 (old saves load with all Hands, identical play). Pacing: unchanged when untouched; measure a bot that sets counts. Risk: a single best assignment (fill places, fastest-paying first) that the player finds in a minute, after which the panel is dead; two counts that only add a queue; and the block breaking the "handful of things ask for attention" rule if it keeps flashing.

**Three strongest objections.**
1. *It is a spreadsheet with no people in it.* It does not answer "each villager has a role". Met: only partly, by the card (read-only trade, name). It is honest to say the owner will say "this is not what I asked".
2. *The optimum is "fill every place" and is already automatic.* Met: only if the panel carries decisions the automatic rule cannot make (requests, drives, tide, boats), shown as *prompts on the row* ("A boat wants Flour: +1 miller?"), not as a duty.
3. *Counts above places are wasted and the player will do it by accident.* Met: each row shows "n of places" and plus is refused (greyed, reason "no free place at the mill") beyond places plus one; the place purchases (hands) then become the thing that unlocks a count.

---

## 3. Design B: A villager is someone

Each villager has a name, a cottage, a talent, a trade the player gives them, and a skill that grows. There are 20 to 30 of them by the end of the age. The unit of play is a person.

1. **What a role is.** The same six trades, but held by a named person with **skill levels per trade** from doing the work (reap 30 trips, become a Reaper 2). A *Master* of a trade (the best at it, level 5, a hat or a coloured apron) gives the trade a small town-wide bonus ("Master reaper Aldith: reapers carry 8 percent more"). A role differs from a job: it is part of who someone is, and it is kept when they change workplace.
2. **Assignment.** Click a villager on the map, or a row in a roster list, and pick a trade from the card. One at a time. Shift-click selects several to give them the same trade. Nothing done: new villagers arrive with their *talent*'s trade ("Aldith: good with the sickle"), a free default that spreads the town by itself.
3. **When blocked.** A person with a trade waits a patient time (shown as a small thought mark above them, a pail or sickle sign), then helps elsewhere by the soft weight; a master helps less readily (a stronger lean, `OffTrade` lower). A person can be told "stay" on the card (hard).
4. **Growth.** Skill by doing, per person per trade. Players buy *training* ("Send Aldith to learn the mill", a price) and *tools per trade* (the sickle line, the pail line), replacing global Tools in later steps. Masters are earned (level 5 and the town's top). Seven times a minute: little new, since the buys stay the same; what grows is care.
5. **Map.** The strongest effect on the look. A person lives at a cottage and walks home between trips, so each cottage's lane wears with its owner's routine; a master reaper wears the field lane harder; named people become *characters walking known paths*, which is the thing trails are for. Reason to pave: "Aldith walks that road eight times a minute".
6. **Spreading.** By person: talents differ, trades differ, skill differs, so they decide differently, and each has an identity the eye can follow. Different spots: lane offset and per-person work spot.
7. **Click.** The main control: card with trade chooser, skill bars, "Show on map", "Stay". Also hover names on all villagers while a key is held.
8. **Interface.** Two places: the card on the map, and a **People page** (a book page, like Achievements, opened from a navigation button, key P) listing all villagers as rows:
```
People (22)                              sort: trade v
Aldith Marsh   Reaper 3      Third cottage    o 1,240
Bram Holt      Water 2       First cottage    o  880
Cora Wick      Hand          Lane cottage     o  410   [ give a trade v ]
```
   First minute: with one villager, the first click on the person shows a card ("Your first villager. Click a trade."). A player who never opens it sees named people with default trades, and hover rings.
9. **Cost and risk.** The heaviest of the person-based options: name table, per-person per-trade skill (a 22 x 6 array, saved), earned and trips, master logic, a People page, a roster, card, pick. Save version +1; old saves give names by id and level 0 skills. Pacing: skill is a new income source; if a trade level adds 4 percent per level per person, the first age's 200 purchases shift; a balancer pass is needed. Risks: **forty villagers to manage** (22 in the first age, more in the second), micromanagement, a master system nobody cares about, and a roster that turns the calm map into a list.

**Three strongest objections.**
1. *22 to 40 people is too many to assign by hand in a game whose rhythm is 7 buys a minute.* Met: only by making all of it optional and automatic by talent, and the roster a place to *look*, not to work. If the player must touch people to do well, this fails the incremental genre.
2. *Per-person skill creates "my best reaper" optimisation and bookkeeping.* Met: skill advances only by doing, never by buying per person; the buy is per trade ("Train reapers"). Masters are a consequence, not a goal.
3. *It cannot be balanced: income depends on who is where.* Met: skill is capped small (plus 20 percent at the top), the sum of skills in a trade, not the individual, enters coin per second, and a bot can be run with a random roster.

---

## 4. Design C: Workplaces are staffed

The player does not think about people but about *places*. Each workplace has posts (its `Places`, plus bought hands), the player fills posts, and a villager belongs to a workplace until moved.

1. **What a role is.** A *post*: "Well keeper", "Field hand", "Miller", "Shepherd", "Weaver", "Landing hand", defined by the workplace. A role is the same as a place at a workplace; unlike a job it is held between trips. The Well's 1 place, the field's 2, the mill's 2 are all the staffing the game has.
2. **Assignment.** On the **workplace**: click the well, the field or the mill (the tag over each already exists for requests and hints) and get "Staff: 1 of 2 [-][+]". Plus brings the nearest unassigned villager. A villager can also be dragged to a workplace. Nothing done: the town staffs itself in order of pay (best-paying workplace first), a Hand fills any open post; unstaffed workplaces are staffed by whoever passes (today's behaviour).
3. **When blocked.** A staffed villager whose workplace is full or closed *waits at their post* for a short time, then fetches the nearest other need (the second-best job), then returns. The post is theirs, so they cannot be pushed out by the herd. This removes the herd entirely.
4. **Growth.** The place purchases are the growth: second bucket, extra hands, second field, a stall, the second berth are *new posts* that appear on a workplace tag. "A third hand at the mill" becomes "A third post at the mill". Tools stay global. Per-trade levels, if any, sit on the workplace (a *workplace level*: the well's "deepened" step). A master is the villager who has held the post longest (a hat).
5. **Map.** Posts make walking regular: the same person walks the same loop for minutes; so a few lanes wear in very fast and others stay faint until another post is staffed. The player's reason to pave: the road between a staffed field and a staffed market. Pleasing and most readable: the town has *shifts*.
6. **Spreading.** Exactly: no more people at a workplace than posts. The rest are "off duty" at their cottages or in a yard (a *yard* of idle people is a visible, non-queue place). Different spots: each post has a spot (`WorkSpot` already does).
7. **Click.** Click a villager: the card shows "Works at: the mill (post 2)" and "Move to..." with a short list of workplaces with open posts. Click a workplace: its staff list and open posts.
8. **Interface.** No new page. The **workplace tag** over each building (small, already used for hints) shows posts as pips: `● ● ○`; click opens a small card:
```
The mill        posts 2 of 2
 Aldith Marsh   Miller 3
 Bram Holt      Miller 1
 [ Add a post ]   o 4,000
Waiting: 2 in the line
```
   First minute: with 1 to 2 villagers there is nothing to staff. The first workplace tag with a pip appears with the first field; the hint line says "Click the field to put a hand on it."
9. **Cost and risk.** Sim: `Workplace` field per villager (already `v.Workplace` exists in the trip, but not held between trips), posts as a second count beside `Places`, reassigning logic, an idle yard, save +1. It changes the economy *more* than A or B, because a villager who stays at a post stops choosing the best job each trip, which is today's whole logic (the "best coin per second" rule becomes "post first"). Pacing needs a full re-balance: the first age's 200 purchases and minute 34 will not survive without it. Risks: roles fighting the best-job rule (the heart of the economy), idle yards of people with nothing to do (the town looks asleep), and a second-age mess (pasture across the ford, the weaver, the landing, crossings: who staffs a crossing?).

**Three strongest objections.**
1. *It throws out the one rule that makes the economy play itself.* The best-job rule is why you can do nothing. Met: only if posts are *soft* (a lean, as in A), which makes it Design A with a different front door.
2. *Posts cap the number of useful villagers, so villagers beyond the posts are dead weight and "more villagers" stops being a good buy.* The Villager upgrade is the game's first column. Met: by making off-duty people carry *errands* (the best job that no post covers), which is again the existing rule.
3. *The second age has workplaces that are not buildings (crossings, the tide, boats).* Met: poorly. A ferry post exists, a ford does not; "Boatman's hand at the landing" is a post, but boats come and go. This is the least extensible design.

---

## 5. Design D: Staged, light first (hybrid)

Start with Design 0's invisible spread, give the player trades by count in the middle of the first age, make people clickable from the first minute, put workplaces' posts on their tags as a read-only count, and add skills and masters only in the second age. Each stage can be bought, tested and left alone.

1. **What a role is.** The same six trades, introduced when their workplace exists. A *trade* holds a *level* (a town-wide skill, the "Train" row), never a per-person skill, until the second age, where **a villager can become a Master** of one trade (named, with a hat). Compared with a job: a standing lean (`OffTrade` weight) the player chooses or the town defaults.
2. **Assignment.** Three layers, each optional.
   - *None:* Hands with a talent each (Design 0). Plays itself.
   - *By count (A):* the People block in the shop's People group, from level 3.
   - *By person (B-lite):* the card on the map has one control, "Trade: [Reaper v]", for the player who wants this one to change. Setting a person's trade moves a count by one (the block updates). Plus/minus never name a person; the card does. Both write the same field.
   - *By rule (a switch):* "Keep trades filled" (default on): when a villager arrives or a place appears, the game gives the open place to the Hand nearest it. Off: manual.
3. **When blocked.** The soft weight (section 1). Trades *show* it: a villager helping elsewhere carries a small mark (the other trade's tool in their hand: the n5 sheet's tools-in-hand carry the information). A "Stay" is a late option for the second age.
4. **Growth.** A trade is unlocked by its building, its count by its places (hands), its level by the Train row (suggested trade, as Pave suggests a road), cap 10 steps, a milestone at 5 and 10. Tools and Load stay global in the first age and each becomes *per trade* from the second age ("Shears", "Handtruck"). Masters: in the second age, when a trade's level hits 5 the best member is named Master and wears a mark: small bonus, never a purchase. Per minute: unchanged for the first age; the new row adds at most one buy every 2 to 3 minutes.
5. **Map.** Trades carry the town's look: reapers' lanes, the miller's lane. Trails wear by *whoever walks them*, now steered by player counts, so a player who gives 4 reapers wears the field lane harder and sees why to pave it. Masters in the second age walk the longest routes (shepherd over the ford).
6. **Spreading.** Three causes, each of which helps: aptitude (different leanings), trades (counts), and places (posts shown). Different spots: lane offsets, work spots, queue spots.
7. **Click.** The card is the same everywhere, with different weight per stage: first age, read-only plus one chooser; second age, plus skill and master mark. Click the workplace: "2 of 2 posts", with the trades working there.
8. **Interface.**
   - First minute: unchanged screen. A villager gets a hover ring and name only *after the second villager arrives* (so the first minute stays calm). The first click gives the card, with "Likes the pail".
   - Level 3 (the field): the People block appears in the People group, two lines ("Water carriers 1", "Reapers 1") with plus and minus, then grows by a line per trade. One hint: "Name a trade for your villagers."
   - Workplace tags show pips read-only (open and filled posts).
   - The second age adds one row "Masters" to the People block and a line in the book's second-age page.
   - Never in the middle of the map except the card on a click and the hover.
   - A player who never opens it: Hands with talents, new villagers fill open places on their own ("Keep trades filled" on), hover names, a card on click. The game still plays itself, and *looks* spread.
9. **Cost and risk.** Sim: `Trade`, `Talent`, `Earned`, `Trips`, lane offset, soft weights, the fill rule; save +1 (old saves: all Hands, no change). Stage by stage, each with its own pacing check against 33.9 minutes and 201 purchases. Risks: it is the sum of A and a piece of B, so scope can grow (people wanting "more of C"); the *fill rule* is the real danger, because it makes the default assignment better or worse than today's and moves the charter minute; and three controls for one field (block, card, switch) is the thing the calm layout is against.

**Three strongest objections.**
1. *A hybrid avoids a choice.* A staged plan hides that the owner asked for "a full system". Met: the stages end in the full system for the second age (masters, per-trade tools, the card), and the first stage is something the owner sees in the next play session.
2. *Three controls for the same thing will contradict.* Met: one field (`Trade` per villager) and a count that is only a view of it; the block and the card write the same value, and the rule only fills *Hands*, never anyone the player has set.
3. *"Keep trades filled" can be exactly the "single best assignment" that empties the choice.* Met: it fills places only; what it leaves open is the request, drive, tide and boat decisions, and the Train row. If the bot finds that doing nothing wins by a clear margin, the fix is to make requests and boats pay for a deliberate trade shift (they already do 1.25 to 1.5 times).

---

## 6. Design E: Division of labour with stocks (the big one, for the record)

Roles that differ in *what they do*, not in what they prefer: reapers cut and leave sheaves in a **stack at the field** (a real stock), carters carry stacks to the mill or market, millers grind stock, stall-keepers sell. Each workplace has an output pile that fills and is cleared.

1. **Role:** a position in a chain: producer, carter, processor, seller. A job becomes *a link* of the chain. 2. **Assign:** by count per link ("2 reapers, 1 carter") or by rule; chains are visible as piles. 3. **Blocked:** the pile is full (reaper stops) or empty (miller waits): *it is clear who is the bottleneck, and the player can see it*. 4. **Growth:** bigger piles, carts (a carter role purchase), more carters; Load becomes the carter's. 5. **Map:** the strongest: carter lanes are the busy ones, field-to-mill and mill-to-market wear and need paving; a town of three kinds of walker. 6. **Spread:** by necessity, nobody can do two links at once. 7. **Click:** card shows the link and what the pile needs. 8. **Interface:** piles on the map; a row per link in the People block.
9. **Cost and risk:** it rewrites the economy: trips no longer end in coin (coin is earned when a pile is *sold*), stocks need saving, `ChooseJob`, `ExpectedWait`, the queue and `Deliver` all change, every pacing test and the second-age boats and landing (a landing store already exists, `StorePrice`) are affected. **Objections:** (1) a 3 to 5 week rewrite of the one thing that works; (2) a bad default means the town stalls (all reapers, nobody carries) and the do-nothing game dies unless a smart auto-assignment runs behind it; (3) it moves the charter minute and the 200 purchases and needs the whole balancing round again. Not recommended for the owner's next step; it is the design that would make roles *matter most*, and Design D's stages 1 to 3 do not close the door on it (a "carter" can later be a trade that takes from a pile).

---

## 7. Comparing the five on the nine questions

| | 0 (spread only) | A trades by count | B someone | C staffed | D staged | E stocks |
| --- | --- | --- | --- | --- | --- | --- |
| Answers "each villager a role, assignable" | no | in part | yes | yes (to a place) | yes, in stages | yes |
| Plays itself with no input | yes | yes | yes (talent default) | yes if soft | yes | needs a smart auto |
| Blocked work | none | soft weight | soft + stay | wait at post | soft | visible bottleneck |
| Growth | none | Train row | skills, masters | posts = hands | Train row, masters in age 2 | piles, carts |
| Map effect | small | medium | strong (identity) | strong (shifts) | medium to strong | strongest |
| Sim change | tiny | small | medium to large | large (rule change) | small, then medium | huge |
| Save | none | +1 | +1 | +1 | +1 | +1, state |
| Pacing risk | tiny | small | medium | large | small, staged | very large |
| Micromanagement risk | none | low | high | medium | low | medium |
| Fit with the calm layout | perfect | good (one block) | poor (a page) | good (tags) | good | medium |
| Second age | free | six trades | per-person | weak | six trades, masters | good |

## 8. Recommendation: Design D, built in four steps, with A as its spine

**Why D.** It is the only design that gives the owner what they wrote (a role per villager that can be assigned) without paying for Design B's roster or Design C's rewrite of the best-job rule. It keeps the property that matters most to an incremental game: with nothing assigned, the town plays itself and looks varied. It rests on one new idea ("a trade is a lean, not a wall"), so blocked work, helping, spreading and the second age all fall out of `ChooseJob`'s existing arithmetic and nothing fights it. And it is staged: each step is something a player can see and a bot can measure.

**Stages.**

1. **Step 1, the first buildable step: be someone, be in a different place.** No roles yet. Add to the sim: a deterministic name and *talent* per villager (a lean of plus or minus 15 percent per job kind, from seed and id), the *lane offset* in `Place`, `Trips` and `Earned` per villager. `ChooseJob` multiplies each job's rate by the villager's talent for that kind. Add the game's villager hit target, hover ring and name, and the **card** (read-only). With a test that the sim with all talents at 1 equals today's hashes, and a pacing check of the shipped talents (the charter must stay within about a minute of 33.9 and 201 purchases plus or minus 5 percent; if not, shrink the lean). This step alone answers "hitboxes" and a large part of "they all start doing the same task", and it is safe in old saves (they load with talents from the id).
2. **Step 2, trades by count, first age.** The `Trade` field, the soft weight, the People block (from level 3) with a "n of places" per trade and the Train row; the card gets its trade chooser, and "Keep trades filled" defaults on. Three trades. Measured by three bots plus a "set counts" bot; the shop's checks S1 to S7 and a new test for the block. This is Design A with B's card.
3. **Step 3, workplace tags with posts.** Read-only pips on the workplace tag, the click on a workplace showing who works there. It is where C's best idea (see who staffs what) lives without C's rule change.
4. **Step 4, the second age.** Shepherd, Weaver and Boatman's hand, per-trade tools in the second age, Masters (named, a mark, a small bonus), hints from the river (the boat and tide prompt "+1 weaver?" in the People block).

**What to measure at every step.** Charter minute and purchase count on seeds 1 to 10 with the three bots; the share of idle-in-line time (the "busy and late" pictures should have fewer people standing); the number of distinct jobs in progress at once (if "they all do the same" is solved this rises from about 1.3 to 2.5 or more; the simulation researcher can read the number now); no allocation per tick; the layout checks.

**What the player sees.** The first minute does not change. At the second villager they can hover and click people. At the first field they get a two-line People block and a hint. Later: a Train row once every few minutes, workplace pips, and in the second age masters. The middle of the map stays free; only a card (on a click) enters it.

## 9. Open questions only the owner can decide (each for a yes/no or a choice of two)

1. When a villager's trade has no free place, should they **help elsewhere on their own** (soft, recommended) or **stand and wait at their trade** (hard)?
2. Should the first step ship **before any assignment exists** (names, talents, click card, spread) so you can judge the feel first, **yes or no**?
3. Is a **name and a cottage for every villager** wanted (22 to 40 named people), **yes or no**; if no, the card shows "a villager of the third cottage" only.
4. Assign **by count in a block (plus and minus)** or **by person on the card first**? (The recommendation builds both; which one comes first?)
5. Should a villager with no trade given by you **take a trade by themselves** (their talent), **yes or no**, or stay a Hand until you act?
6. Should the game **fill open places for you** ("Keep trades filled", on by default), **yes or no**, and may it be switched off?
7. Are **trade levels** (a Train row) one town-wide skill per trade, or is skill **per person** that grows by doing? (The recommendation: town-wide in the first age, per person only for masters in the second.)
8. Do **Tools and Load become per trade** (sickle, yoke, handcart for reapers and carriers) in the first age already, **yes or no**? (The recommendation: no, from the second age.)
9. Is a **master of a trade** wanted in the first age, **yes or no**? (The recommendation: second age only.)
10. May villagers be **seen to pass each other on roads** (a lane offset) while still **not blocking each other** (the crowding slowdown stays the only "body in the way"), **yes or no**?
11. Is a longer line of idle villagers at a full workplace **a problem you want solved by more places** (hands), or **by sending the extra people home**?
12. Should **second-age trades** (shepherd, weaver, boatman's hand) arrive **with their buildings** or **after the first age's roles are judged** in play?
13. May the **pacing move** (charter at minute 34, 201 purchases) by **up to a minute** to make room for the new row, **yes or no**?

## 10. Limits of this paper

I did not run the simulation, so "about 1.3 distinct jobs in progress" and the shape of the herd are read from the code, not measured; the simulation researcher's numbers should replace them. I did not read the pictures `u11.jpg`, `u13.jpg`, `u32.jpg` or `cmp-g*.png` in this pass; statements about crowds come from the pacing tables (12 villagers at minute 10, 22 at minute 20) and the sim API. Percentages given for talents, trade levels and `OffTrade` are starting values for a balancer, not tuned numbers.

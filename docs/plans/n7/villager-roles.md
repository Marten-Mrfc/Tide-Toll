# Villagers with roles: what the research found and what I propose (the lead, 27 November)

The owner: "They don't have hitboxes and they don't spread well. Like they all start doing the same task. That is not the idea. I would like each villager to have a role and that it can be assigned. Like I would like to add that as a full system."

Six researchers reported (`research/`): the map of today's code, engine lookups, a survey of other games, design options, bodies and picking with measurements, and experiments in the real simulation. Nothing is built. This page is the proposal; the decisions that are the owner's are at the end.

## What happens today, measured

- **They do all do the same task.** In the real simulation more than three quarters of the villagers are on one job for 24 percent of the first age and 63 percent of the second. In the second age the whole town sits on one boat job for up to five and a half minutes, then flips to another (`research/plots/jobs-seed1-today.png`).
- **Why.** Every idle villager takes the job that pays most coin per second, counting the walk, the wait and the work. Only the expected wait pushes back, and it has **two blind spots**: villagers who chose flour and are still walking to the field do not count toward the mill's line, so ten can choose flour and each sees an empty mill; the same at the pasture and the weaver.
- **Waiting is not the problem.** Villagers spend 73 percent of their time walking, 19 working, 7 in a line, none idle.
- **They overlap.** At any moment 35 to 45 percent of villagers touch someone at the size they are drawn. Walkers on a road all have the same speed, so people who start together stay glued for the whole road; a line stands on the middle of the road, and when it is longer than its road the rest stack on one spot.
- **Nobody can be pointed at.** There is no body, name or card for a villager anywhere.

## The finding that decides the design

**Roles as walls make the town poorer.** Every way of giving fixed roles that was tried made the first age 15 to 26 percent longer, and 55 percent longer when villagers with no work for their role stand and wait. No fixed split came within 15 percent of today's rule. Today's rule already plays like a careful player, so a careful player with hard roles can only lose to it.

So a role system must do two things at once, or it is a chore: **a role must be a lean, not a wall** (a villager prefers its trade and helps elsewhere when its trade is full), and **a role must give something back** (or assigning can only cost).

## What I propose, in three steps

### Step 1: people you can point at, who spread by themselves (no assigning yet)

- **Fix the two blind spots**, so a villager choosing work sees the others already on their way. Measured: second-age herding falls from 63 to 42 percent; the first age gets about half a minute shorter.
- **Every villager is someone:** a name from the town's seed, a home, and a small natural leaning toward one kind of work (a few percent), so two villagers at the same door no longer make the same choice.
- **Point and click:** a ring under the feet on hover, a name after a moment; a click docks a small card in the left column (name, home, what they carry, trips made, coin earned). Off in the Pave and Extend modes and over the interface.
- **Bodies, in the drawing:** walkers keep to one side by direction, with more room on a paved road; a follower keeps a figure's distance; a line stands at the road's side and never stacks; standing people have their own spots. (The line is being built now with "work you can watch".)
- This step answers "hitboxes" and a good part of "the same task", and can be judged in play before anything is assigned.

### Step 2: trades you assign (first age)

- **Three trades:** water carrier, reaper, miller. A hat or tool shows the trade (the sickle is being made now).
- **A trade is a lean:** a villager with a trade prefers its work and takes other work only when that pays clearly more (measured: at a factor of 1.25 the age is 5 percent longer; at 2 it is 11 percent longer and herding falls to 8 percent of the time).
- **A trade gives something back:** a villager works faster at its own trade, and the trade can be trained further in the shop. This pays for the lean, and makes assigning worth doing. It means the pace of the age is tuned again.
- **Assigning:** by count in the shop's People group (plus and minus per trade, shown as "3 of 4 places" so a count never just makes a longer line), and for one villager on its card. The town fills trades by itself unless the player takes over ("keep trades filled", on by default), so a player who ignores it loses nothing.

### Step 3: the second age and the workplaces

- Three more trades (shepherd, weaver, boatman's hand), tools and loads per trade, and each workplace shows who works there.

## What I would not do

- Villagers that never leave their role (the measured tax, and idle people).
- A priority grid or a roster of forty people to manage (the survey's heaviest complaint in other games).
- Bodies as a rule in the simulation: tried, it holds walkers back 1 to 3 percent of the time and changes nothing but every recorded run. The crowding rule on roads is already stricter.
- Crowd-avoidance steering: on roads one cell wide with a line at every door it jitters and locks.

## Limits of this research

- The survey of other games rests on search summaries; the game wikis and review sites could not be opened from this session.
- The second age's overlapping was not measured; picking and drawing costs are estimates.
- The reward for a trade (step 2) was not prototyped: only the cost of the lean was measured. Its tuning is real work.
- One oddity found on the way: in the second age a poorer town reaches the seal sooner, because the levels there follow the town's best coin per minute. That needs its own look.

## The decisions that are the owner's

1. **Step 1 first, on its own**, so it can be felt in play before trades exist? (Recommended: yes.)
2. **Fix the two blind spots now?** It moves the recorded play-through (the charter about half a minute earlier in the bot's runs). (Recommended: yes.)
3. **A trade as a lean** (prefers its work, helps elsewhere when full; recommended) **or as a wall** (never does anything else; measured 15 to 26 percent slower, with idle people)?
4. **Should a trade give something back** (faster at its own work, trainable in the shop), which means tuning the pace of the ages again? (Recommended: yes. Without it roles can only cost.)
5. **Assign by count per trade** (recommended first), **by clicking each villager**, or both?
6. **A name and a card for every villager** (22 to 40 people by the end of an age)? (Recommended: yes.)
7. **Bodies as a look only** (recommended), or as a rule that slows people who are in each other's way?

# Villager roles: a survey of other games

Researched 2026-11-27 for the lead designer.

**Method and limits.** Every WebFetch failed (the session could not resolve timberborn.wiki.gg, rimworldwiki.com, wiki.hoodedhorse.com, adarkroom.fandom.com, www.gamepressure.com or www.banished-wiki.com; they would need to be allowed in the session's network settings). So every claim below rests on a web-search result summary, not on a page I read in full. A URL after a claim is the search hit that carried it. "From memory, not checked" marks what I could not find. Developer talks and design posts were mostly not reachable; most sources are wikis, guides, forum threads and reviews. No search query named this game or its owner.

---

## 1. Game by game

### Incremental and idle games

**A Dark Room.** The player moves villagers between jobs with up and down arrows, and can step by 1 or 10 (https://adarkroom.fandom.com/wiki/Outside_(Browser), https://adarkroom.fandom.com/wiki/Villager_occupations). A gatherer yields wood on a fixed timer; the Lodge lets gatherers be turned into hunters and trappers; trappers consume meat and make bait; tanners turn fur into leather at 5:1 (same wiki pages, plus https://www.arrpeegeez.com/2026/02/a-dark-room-walkthrough.html). So: **role counts, with plus and minus, no individual villager.** Nobody is named. Roles are unlocked by buildings. Complaint data: not found for this game.

**Kittens Game.** Jobs are assigned as counts. Each kitten has a skill that rises with experience, there is a "leader" kitten who benefits from promotion, and an auto-assign "manage jobs" option places kittens by skill (https://www.reddit.com/r/kittensgame via mirrors: https://axebps-redlib.hf.space/r/kittensgame/comments/13sjpw1/quick_question_about_leaders and https://cal1.lr.ggtyler.dev/r/kittensgame/comments/12eo48i/does_manage_jobs_really_do_anything). A commenter says that early the skill bonus is small and later all kittens are good at everything, so managing is unnecessary (second link). The Anarchy challenge removes the leader and only half the kittens work (https://axebps-redlib.hf.space/r/kittensgame/comments/13sjpw1/quick_question_about_leaders). I could not reach the official wiki, so exact rank and job lists are **from memory, not checked.** Lesson: individuals with skill exist, but the player ends up dealing in counts; skill differences wash out.

**Evolve Idle.** Searched twice, nothing found. Job assignment by counts per job (farmer, miner and so on) is **from memory, not checked.**

**Universal Paperclips.** No worker roles. The stage-two allocation is drones against factories, a ratio problem with a power limit (https://universalpaperclips.fandom.com/wiki/Stages). Useful only as an example of allocation as a single slider-like choice, not of villagers.

**Idle town or kingdom builders in general.** Not surveyed beyond the above; I found no specific source. Treat as a gap.

### Town and colony games

**Banished.** Professions are assigned in a window with two numbers per profession, current and limit; citizens with no profession are laborers; raising a profession lowers laborers by one; professioned citizens drop labor work when their work appears (https://www.gamepressure.com/banished/assigning-professions/z16083, https://www.banished-wiki.com/wiki/Job, https://w.atwiki.jp/banished/pages/147.html). Reviewers: the job-shuffling is "surprisingly fun" to one, micromanagement fades as spare laborers grow, but idle workers and jobs not done when needed are the main complaint; no direct control of citizens (https://www.hookedgamers.com/pc/banished/review/article-1203-2.html, https://www.pcgamer.com/banished-review, https://www.gamezebo.com/reviews/banished-review/; summarised in one search result, and I could not tell which review said which). Pattern: **role counts with an automatic fallback (laborer).**

**The Settlers series.** In Settlers III, carriers are the free basic settler, move goods automatically by need, and a tab controls labor distribution (https://Www.Wikipedia.org/wiki/The_Settlers_III, https://gog.com/forum/the_settlers_series/training_settlers). In New Allies, carriers become workers automatically when a building needs staff, while engineers are the controllable units (https://news.ubisoft.com/en-gb/article/54IHIBPSLbTyrtzQchvDQj/the-settlers-new-allies-out-now-on-pc, https://ubisoft.com/en-us/game/the-settlers/new-allies/news-updates/3zPXejpp2BWvZDzpDvLXcU/the-engineer-the-most-versatile-unit). Specialists need tools (tool-per-profession is **from memory, not checked**). Pattern: **automatic from need, with a small set of directly controlled specialists.**

**Manor Lords.** Families (1 to 3 people) are assigned from the building, with an add button (key E) and a subtract button (Q); unassigned families build by default; artisan households tied to their craft cannot be freely reassigned (https://game8.co/games/Manor-Lords/archives/452236, https://sportskeeda.com/esports/how-assign-workers-families-manor-lords, https://www.gamepressure.com/manor-lords/how-to-manage-villagers/zc1136a). Pattern: **staffing per workplace, plus a default job for the unassigned.**

**Timberborn.** Priority per building; with fewer workers than slots, the top-priority workplaces are filled first and the lowest can sit entirely empty; workers stand idle at the entrance when there is nothing to harvest; players ask for per-slot priorities and even spreading among equal priority (https://timberborn.wiki.gg/wiki/Workers, https://timberborn.featureupvote.com/suggestions/297296/comment/523493, https://timberborn.featureupvote.com/suggestions/222816/comment/346385). A mod adds "employ now" that takes workers from the lowest priority (https://thunderstore.io/c/timberborn/p/Hectare/EmploymentPriority/). Requests to manage individual beavers and choose home and job exist (https://timberborn.featureupvote.com/suggestions/202922/comment/309440). Pattern: **priority list per workplace, automatic fill.** The weakness shows clearly: fill order is winner-takes-all, not spread.

**RimWorld.** Work tab is a grid of colonists against work types; numbers 1 to 4 (1 first), or in standard mode tasks to the left rank higher; within a type, a second order applies; mods add finer priorities and warn that defaults exist "for a good reason" and changing them can deadlock (https://rimworldwiki.com/wiki/Work, https://rimworldwiki.com/wiki/Capable, https://github.com/MrClon/WorkTab). Each colonist has backstory, traits, skill levels, and passions that speed learning (https://rimworldwiki.com/wiki/Backstories, https://rimworldwiki.com/wiki/Colonists). Pattern: **per-villager priority grid.** Identity is a strong point: the wiki calls the game centred on its characters' stories.

**Dwarf Fortress.** Per-dwarf labor toggles, which are heavy enough that the third-party Dwarf Therapist (spreadsheet of dwarves against labors, custom professions, a "labor optimizer") is the standard way to manage them; the game also has an automatic labor mode (https://dwarffortresswiki.org/Utility:Dwarf_therapist, https://dwarffortresswiki.org/index.php/Dwarf_Therapist, https://dwarffortresswiki.org/DF2014:Labor, https://dwarffortresswiki.org/index.php/Job). Lesson: a per-person system gets so heavy that players built presets ("custom professions") to apply to many at once. Skills grow with use (the 2022 thread says this; not verified in detail).

**Foundation.** Every profession has its own clothes and tools and can be seen at work; villagers' footsteps wear dirt paths into grass; the player paints zones and builds a gathering hut, then assigns a few villagers as foragers (https://www.pcgamer.com/au/you-dont-have-to-place-roads-in-this-medieval-city-builder-thanks-to-your-citizens-feet/, https://www.digitec.ch/en/page/foundation-is-a-brilliantly-chaotic-and-creative-building-game-36583). Promotion runs through a Great Hall mandate, costs rise with rank, needs must be met, and since 1.9.4 promotion can be done from the villager's detail window (https://wiki.polymorph.games/foundation/Serf, https://www.polymorph.games/foundation/news/?p=717, https://foundation-game.fandom.com/wiki/Characters; my summary of version history comes from one search result). This is the closest match to this game: trails from feet, role visible on the body, promotion as a purchase.

**Kingdoms and Castles.** One forum reply says a job priority list opens with "J" and workers move to higher-priority jobs; workers may sit at their workstation indefinitely when idle (https://steamcommunity.com/app/569480/discussions/0/1480982971160033489, https://steamcommunity.com/app/569480/discussions/0/591758125124641511). Weak evidence (forum posts); the rest is **from memory, not checked.**

**Northgard.** A villager is placed into a building to take its job; workers can be converted back by right-clicking a house; reassignment any time (https://northgard.fandom.com/wiki/Buildings?oldid=7014, https://blog.hyperx.com/article/2726/northgard---how-to-build-your-first-settlement, https://www.gamepressure.com/northgard-game-guide/how-to-send-a-villager-to-work/zaab16). Pattern: **per-villager click assignment** (a small population makes it viable).

**Against the Storm.** Click open slots on a building to assign villagers or dismiss them to act as builders; comfort specialisations give +5 Resolve, proficiency specialisations give a 10% chance of double output; species have Resolve and Resilience; Resolve below 1 and villagers leave; Fox bonus needs 3 or more in one building (https://wiki.hoodedhorse.com/Against_the_Storm/Villagers, https://wiki.hoodedhorse.com/Against_the_Storm/Specialization, https://hoodedhorse.com/wiki/Against_the_Storm/Resolve). Pattern: **staffing per workplace, with a trait of the worker making some assignments better** (an assignment puzzle, not just a count).

**Anno 1800.** The player does not assign anyone. Workers come from houses (population tier), the workforce is a pool, and shortages cut building efficiency; housing upgrades are the lever (https://www.gamepressure.com/newsroom/how-to-fix-a-problem-with-insufficient-workforce-in-anno-1800/za520c, https://thenerdstash.com/how-to-get-more-workers-in-anno-1800, https://steamcommunity.com/app/916440/discussions/0/4626978782935270832). Pattern: **no assignment; pressure goes to the supply of workers.** Details of the global pool are mostly from memory.

**Others met in passing (not asked for).** Nova Roma: job priority, and workers quit low-priority jobs to fill higher-priority openings (https://blast.tv/gaming/news/nova-roma-most-important-feature). Infection Free Zone: add and remove workers one at a time or in bulk, with "0" and "max" buttons; unassigned citizens idle (https://primagames.com/?p=327162). Settle the World: arriving skilled workers are auto-assigned (https://theotheoderich.itch.io/settle-the-world/devlog/493637/automatic-job-assignment-for-newly-arrived-skilled-workers). Several itch.io comments ask for auto-assign and bulk assign because manual assignment is tedious (from the same search; individual posts not opened).

---

## 2. The patterns

| # | Pattern | Seen in | Strengths | Weaknesses | Suits |
|---|---|---|---|---|---|
| A | **Role counts** (plus/minus per role) | A Dark Room, Banished, Kittens Game | Tiny interface; scales to hundreds; fast; one decision per role | Villagers are interchangeable; nothing to see on the map; players must come back to rebalance | Idle games, big populations |
| B | **Staffing per workplace** (slots on the building) | Manor Lords, Against the Storm, Timberborn, Northgard | Visible and spatial; the building is the thing you look at; caps are natural (slot count) | Many buildings means many clicks; empty-slot chores | City builders with few, big buildings |
| C | **Per-villager assignment** | Northgard, RimWorld, Dwarf Fortress, Foundation (promotion) | Strongest identity; skill and passion matter | Heavy; DF needed a third-party spreadsheet; breaks at 40+ | Small casts (under about 15) or colony sims |
| D | **Priority lists / grids** | RimWorld, Timberborn, Kingdoms and Castles, Nova Roma | Say once, hold forever; robust to change; handles shortages | Opaque ("why is nobody hauling?"); deadlocks when edited; winner-takes-all fill (Timberborn) | Large sims with many job types |
| E | **Automatic with nudges** | Settlers carriers, Anno (workforce pool), Banished laborer fallback, Manor Lords unassigned builders, Kittens "manage jobs" | Nothing breaks when ignored; zero chores | Player feels no ownership; the default must be good or it is resented | Games where the town is watched, not run |

Observation across the board: successful systems pair a manual layer with an **automatic default for the unassigned** (Banished laborers, Manor Lords unassigned families building, Settlers carriers). Complaints cluster on idle workers and on assignments that quietly go stale.

---

## 3. What fits this game

The heart is watching the town and trails forming; the interface is calm; ages last about 34 minutes; about seven purchases a minute early. That is Foundation's feel with Kittens Game's economy. So: **E as the base, with A or B bought as upgrades, and a small dose of C (click a villager to see them).**

What would make roles a pleasure:

1. **A good automatic default that never breaks.** The existing best-job rule is the "laborer": everyone not given a role keeps doing it. Roles then are a *bias on the rule*, not a replacement. Banished and Manor Lords both hold this shape (links above).
2. **Roles as a purchase.** A role slot is bought and upgraded like Pace and Load, so assigning is one of the seven-a-minute choices, not a separate menu. Foundation makes promotion a cost-and-need purchase (https://wiki.polymorph.games/foundation/Serf).
3. **Roles change the map.** Foundation's profession clothes and tools (digitec link) and footstep paths (PC Gamer link) are the model: a role means a different tool in the hand and a different trail.
4. **Click a villager to meet them.** RimWorld's backstory, skills and passions are the strong version (https://rimworldwiki.com/wiki/Backstories); a cheap version is name, home, trade, one trait.
5. **Spreading by the roles themselves**, not by a hidden rule (section 5).

Traps, each with evidence:

- **Micromanagement in an idle game.** Banished reviewers find shuffling fun early and a chore later (hookedgamers, pcgamer links); DF needed Therapist; itch.io commenters ask for bulk and auto-assign. In a calm game, cap the number of things to tune and give a bulk action.
- **Dominant strategy.** Kittens Game: skill differences vanish so management is moot (reddit mirrors above). If one role always pays best, the choice is empty. Roles need diminishing returns per workplace or a need the player feels (the well runs dry, the mill waits for grain).
- **A role system that fights the best-job rule.** RimWorld's mod authors warn that editing defaults can deadlock (https://github.com/MrClon/WorkTab); Timberborn's strict priority leaves lower buildings empty (featureupvote link). If a role says "always the mill" while the rule says "the well pays more", the player sees a villager walking the wrong way. Make the role a **weight** that the rule honours, with a clear "idle if nothing to do, then fall back to best job" ending.
- **Idle villagers.** Timberborn workers stand at the entrance; Banished jobs go undone. Always give a role a fallback.

---

## 4. Roles and identity, cheaply

What the sources show costs little and adds most:

- **A name and a face on a click.** RimWorld backstory plus skills plus passions makes colonists characters (rimworldwiki links above). A tiny version: name, home cottage, current trade, one quirk.
- **A tool or hat that shows the role.** Foundation: profession-specific clothes and tools (digitec link); also an outfit overhaul in release notes (https://www.polymorph.games/foundation/news/category/release-notes/). Cost: one sprite variant per role.
- **A history counter.** Kittens Game and Foundation both attach experience or rank. A "trips made" or "loads carried" count costs one integer.
- **A home.** Everyone already has a cottage; showing it on click is nearly free.
- **A rank.** Foundation's promotion has a cost per rank (https://wiki.polymorph.games/foundation/Serf).

What it adds: attachment, reading the town at a glance, and a reason to look at the map, which is this game's core.

---

## 5. Spreading: how others stop everyone piling onto one job

- **Caps per workplace.** Slots on buildings: Manor Lords, Against the Storm, Timberborn (links above). Banished: a limit number per profession.
- **Priority, with its failure mode.** Timberborn fills the top priority first; the community asks for even spreading among equals and per-slot priorities (featureupvote link). Nova Roma lets workers quit low-priority jobs for a higher one (blast.tv link).
- **Ownership by the building.** Manor Lords families are attached to a building and cannot all pile on one (game8 link).
- **Distance.** Banished citizens work near their homes (review summary); Timberborn players ask for home-to-work proximity (https://timberborn.featureupvote.com/suggestions/222816/comment/346385).
- **Need-driven pull.** Settlers carriers turn into workers when a building needs staff (Ubisoft New Allies link).
- **Quotas.** Role counts are a quota by definition (A Dark Room, Banished).
- **Cooperation bonuses with caps**, such as Against the Storm's Foxes needing three in one building (hoodedhorse Villagers page).
- **Round-robin, reservations, hitboxes:** I found no source naming these in these games. Reservation of a workplace slot by one villager is **from memory, not checked** as how most of them do it.

For this game the cheapest honest tools are: a per-workplace cap that is itself an upgrade ("extra hands"); a role quota; and a mild crowding penalty in the best-job score (the n-th person at a workplace is worth less). This answers "they all start doing the same task" without a new menu.

---

## 6. One-page recommendation

### Design 1 (light): "Trades" as a bias

- **Player does:** buys a Trade (Carrier, Drawer, Tiller, Miller) from the shop like any upgrade; each Trade claims a share of the villagers, set with one plus and one minus per row.
- **Mechanics:** the best-job rule stays, but a villager with a Trade scores that trade's workplaces higher. Unassigned villagers use the old rule. Add a crowding penalty so a workplace's n-th worker is worth less.
- **Sees:** a hat or tool per trade; on click, name, home, trade.
- **Why it works:** pattern E plus a little A; Banished-style fallback; nothing breaks if ignored.
- **Cost:** low; a score tweak, a shop row, a sprite variant.
- **Main risk:** the bias feels invisible or too weak, so nothing changes; or one trade dominates (trap 2). Fix by tuning the penalty and making each trade need its own workplace.

### Design 2 (middle): "Hands at the workplace"

- **Player does:** each workplace has slots; "extra hands at a workplace" (already a purchase) becomes a slot the player can fill with a click on the building or on a villager.
- **Mechanics:** a filled slot reserves a villager for that workplace; unfilled slots are filled by the best-job rule. Per-workplace caps give spreading by construction. Trades from Design 1 optional.
- **Sees:** a small row of dots on each workplace; the assigned villager wears the trade; trails deepen on that route.
- **Why it works:** pattern B; spatial; mirrors Manor Lords and Against the Storm; where people walk is where the trail forms, so the choice shows on the map.
- **Cost:** medium; slot data, reservation, UI on buildings.
- **Main risk:** chores. Every new workplace in age two adds clicks. Mitigate with auto-fill on purchase and a bulk "tidy" action; keep the number of workplaces small (about 7).

### Design 3 (full): "Villagers with trades, promoted"

- **Player does:** click a villager, see name, home, trade, skill; promote or retrain them for a coin cost (Foundation's mandate model); assign trades by count or by villager.
- **Mechanics:** each villager has a trade and a skill that grows with work (Kittens Game, RimWorld); slots per workplace (Design 2); automatic fill for the unassigned; presets like Dwarf Therapist's custom professions for bulk change.
- **Sees:** distinct clothes per trade and rank, a short history line, per-villager trails if wanted.
- **Why it works:** strongest identity, patterns B plus C plus E.
- **Cost:** high; data model, UI, balance across two ages and about 40 villagers.
- **Main risk:** this is the micromanagement trap in an idle game; DF needed an external spreadsheet and Kittens Game's skills washed out. Without strong bulk tools and an excellent default it will be a chore.

**My pick:** build Design 1 first, because it is the only one that cannot break the town, then grow it into Design 2 using the existing "extra hands" purchase. Take only the click-to-see-who-they-are and the visible tool from Design 3. Skip skill growth until the first two are proved.

---

## 7. Gaps

- Evolve Idle, Kittens Game wiki details, Kingdoms and Castles, idle town builders in general: no good source found.
- No developer talks or postmortems were reachable; the evidence is wikis, guides, forum threads and review summaries via search.
- Review quotes were reported through search summaries; I could not attribute each to a single review.
- Numbers (priority 1 to 4, caps, costs) come from the sources above and may have changed with game versions.

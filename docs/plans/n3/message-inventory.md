# Message inventory (from the code, commit as checked out on 26 Nov 2026)

Written by a researcher, no code changed. Files are under `game/scripts/` unless named otherwise; "Hud.X" is `town/TownHud.X.cs`, "Audio" is `town/TownAudio.cs`. Sizes are at 1920x1080 and 100 percent. **(est.)** marks my own sum of the code's numbers or my estimate of how often something fires. The frequency estimates come from one bot run of the simulation, `dotnet run --project tools/TideToll.Tools -c Release -- town --seed 1 --bot payback`: charter at minute 31.7, 16 levels, 191 purchases, 24 roads paved, 14 cottages extended, 27 villagers, 14 requests asked and 7 met, 4 buildings bought, 3 extra hands, 32 regulars. A person plays slower (more minutes for the same events), so the rates below are an upper bound per minute. The events are those of `sim/Town/TownEvents.cs`.

## 0. The kinds and the numbers that govern them

| Kind | Mechanism | Time | Max at once |
| --- | --- | --- | --- |
| **Note** (margin slip) | `TownHud.Note` (TownHud.cs:376-401) | 7 s; fade over the last 1.2 s only (`NoteSeconds`, TownHud.cs:19, 419) | 3 (`MaxNotes`); a fourth deletes the oldest in one frame (392-397); the same text again only restarts the timer of the slip on screen (379-381) |
| **Banner** | `TownHud.Banner` (Banner.cs:67-78) | 3.5 s: fade in 0.3 s, out the last 1 s (`BannerSeconds`, TownHud.cs:19; Banner.cs:86) | 1: a new one **replaces** it in place |
| **Floating coin number** | `Float` (TownHud.cs:488-510) | 1.8 s (`FloatSeconds`), opaque to half way, then fades; rises 36 px | 32 (`MaxFloaters`), pool reused round-robin; deliveries within 2 cells of a rising number are added to it |
| **Card** (request, drive, choice, tide, slim strip) | a control whose `Visible` follows the town's state | as long as the state | one each |
| **Hint line** | `HintFrame` (Request.cs:150-183) | each text held 4 s at least (`HintHold`) | 1 |
| **Page** (achievements, age) | `ShowPage` (Pages.cs:40-46) | until closed; fade 0.22 s | 1 each (opening one closes the other, Book.cs:165, 219) |
| **Map label** | `Label3D` named `What` / `Wants` | as long as the plot is staked or the boat is there | many; hidden each frame when they touch a card or each other (Labels.cs:27-75) |
| **Mode text** (Pave/Extend banner, price tip, "Suggested" tag) | Pave.cs, Extend.cs | as long as the mode | 1 banner, 1 tip, 1 tag |
| **Tooltip** | Godot's own (`TooltipText`) | while hovered | 1 |

**No message can be dismissed by the player** except pages (Tab, Esc, the close button), the Pave/Extend mode (Esc, right click) and the choice card (picking a boon). Notes and banners cannot be clicked away, and the cards under them are mouse-blocking (`OverInterface`, TownHud.cs:358-368).

### The sound path (all messages)

`TownAudio._Process` (Audio:158-185) plays one cue per event through `Cue(id, player, ref counter)` (Audio:122-137), which refuses a cue that played less than its least gap ago (`SoundCues.GapFor`, SoundCues.cs:45-51: hover 0.07 s, press/tab/page-turn 0.05, purchase/paving/extend 0.09, cannot-afford 0.35, achievement 1.2, level-reached and gift-arrives 0.8, others 0.25). **Level-reached, gift-arrives, request-met, charter-granted, the bell and the peal all play on the one player `_bellP`**, so two of them in the same frame cut each other: the later `Play()` replaces the earlier stream. The `_ui` player carries purchase, hover, press, tab and every "moment" the HUD asks for (`Audio.Moment`). River cues have their own player and polyphony (RiverAudio.cs:76-92).

## 1. Every message

Columns: **trigger** (where raised); **place and size**; **stay and end**; **arrival and leave**; **sound**; **rate** (first age, est.). "One frame" means the thing is switched on or off with `Visible` or inserted into the tree with no fade.

### 1.1 Banners (top centre, one at a time)

Place for all: `y = 18` (or 70 under a Pave/Extend banner, or at the bottom `H - h - 70` for level banners whose gift lies under it, Banner.cs:90-99), centred, about 70 px tall and as wide as its text plus 44 px (the label does not wrap, Banner.cs:57-60), head 28 serif, text 18 bold, `Pressed` colour. Stay 3.5 s. Arrive: a fade in 0.3 s; leave: fade out in the last 1 s then `Visible = false`. **Replacement:** `Banner()` overwrites both labels and restarts `_bannerAge` at 0 (Banner.cs:69-71), so a banner on screen at full opacity **drops to opacity 0 in one frame and fades in again with the new text**.

| # | Message | Trigger | Sound | Rate |
| --- | --- | --- | --- | --- |
| B1 | "Level n" + gift text, e.g. "Level 5: A field ..." | `LevelReached`, level above 1 (TownHud.cs:429-431); camera follows (`BannerClearOf` puts it at the bottom if the gift lies under it) | level-reached on `_bellP` (Audio:172) | 15 in the first age (levels 2 to 16), 12 more in the second (17 to 28); about one every 2 min |
| B2 | "The charter: The town is granted its charter. Ring in the next age when you are ready." | `Charter` (TownHud.cs:441-444) plus the camera glides to the bell green | charter-granted on `_bellP` | once; raised in the same event batch as B1 of level 16 (Town.cs:129-138: LevelReached is emitted first), so **B1 is overwritten before a frame is drawn** |
| B3 | "The charter drive: One last push: send n Grain for the charter. Every load counts." | `DriveAsked` (Choice.cs:290-293); started in `ApplyLevel` of level 15 right after its LevelReached (Town.cs:128-133) | request-asked via `Moment` on `_ui` | once; **overwrites B1 of level 15 in the same batch** |
| B4 | "A boon for good: Wider lanes. <what it does>" | `BoonChosen` (Choice.cs:278-282) | level-reached via `Moment` on `_ui` | 4 (levels 4, 7, 10, 13) |
| B5 | "A new age: The bell rings in the next age." | the player rings in the next age (Book.cs:196) | `NextAgeRung` (peal of four long bells 0.7 s apart on `_bellP`, Audio:178-185) and page-turn | once |
| B6 | "The borough seal: The town is a borough. The second age is complete." | `Seal` (River.cs:184-187) | seal-granted (RiverAudio.cs:108) | once, second age |

**Collisions:** B1 with B2, B3, B4 (same frame or within 3.5 s). B1 and the **choice card (C1)** appear at the top centre at the same moment on levels 4, 7, 10, 13 (`ChoiceOffered` is emitted right after `LevelReached`, Town.cs:129-130), the card at `y = 14` drawn over the banner at `y = 18` (card added later, Choice.cs:56-60), so **the level banner lies behind the choice card** unless its gift point sends it to the bottom. The banner is not told about the card.

### 1.2 Notes (left column, under the cards)

Place: `x = 16`, under the last card of the left column (TownHud.cs:227), width about 334, 6 px apart, one slip about 38 px tall for one line, 57 for two **(est.)**; text 16 regular, or 18 bold on a `Pressed` slip with a 2 px border when strong. Stay 7 s. **Arrive: inserted at the top of the stack in one frame at full opacity; every older slip is pushed down by its height.** Leave: a fade over the last 1.2 s, then removed; **or removed in one frame when a fourth arrives (oldest first)**. Collision: stack (max 3), identical text restarts the old slip. A slip can cover **the thing it is about**: the left column holds the ledger, request and drive cards, and notes about requests, levels, offers and the bell are drawn right under them, never over; the notes cover nothing on the map's left edge except what lies under x 16 to 350, y 220 to 500 (est.), which at the town's start is the map itself. Notes hide stake labels that touch them (Labels.cs:47-49).

| # | Text | Trigger | Strong | Sound | Rate (first age, est.) |
| --- | --- | --- | --- | --- | --- |
| N1 | "A cottage is extended: room for one more." / "...extended again..." | `CottageExtended` (TownHud.cs:433) | no | cottage-extended | 14 |
| N2 | "<Name> is open." | `BuildingOpened` (434) | yes | gift-arrives on `_bellP` | about 12 to 20 |
| N3 | "New to buy: <Name>." | `OfferAvailable` (435) | no | none | about 12 to 20 |
| N4 | "A road has worn in. It can be paved now." | `RoadWornIn` (436) | no | none | about 24 to 30 |
| N5 | "A road is cobbled." / "A road is laid with gravel." | `RoadPaved` (437) | no | road-paved-* | 24 |
| N6 | "Perk: <name>." | `PerkEarned`, once per name per town (438) | no | none | up to about 10 |
| N7 | "Achievement: <name>." | `AchievementUnlocked` (439) | no | achievement (gap 1.2 s) | up to 25 |
| N8 | "A new villager moves in." | `VillagerArrived`, from the second villager (440) | no | none | 26 |
| N9 | "<Who> asks for 12 Grain." | `RequestAsked` (Request.cs:95) | no | request-asked | 14 |
| N10 | "<Who> is pleased: +n coin." | `RequestMet` (97) | yes | request-met on `_bellP` | 7 |
| N11 | "Too late for the miller. Maybe next time." | `RequestMissed` (100) | no | request-missed | 7 |
| N12 | "A choice of two boons is open at the top." | `ChoiceOffered` (Choice.cs:275) | yes | request-asked via `Moment` | 4 |
| N13 | "Milestone! <name>. A little extra on top." | `Milestone` (287) | yes | gift-arrives via `Moment` on `_ui` | about 15 |
| N14 | "The charter drive is met." | `DriveMet` (296) | yes | request-met via `Moment` | 1 |
| N15 | touch notes: "Someone has stacked a woodpile." etc., ten texts and a default (Choice.cs:310-323) | `TouchAdded`, one in four (302) | no | none | about 3 (a touch about every 3.5 min of income, `TownRules.TouchMinutes`) |
| N16 | "Scroll out to see the whole book." | game time 75 s, if the player has not zoomed out (TownHud.cs:329-330) | no | none | once per HUD build (`_scrollHintShown`) |
| N17 | "The bell comes at level 2." / "The bell is resting: 31 s." | the player rings a bell that cannot ring (Shop.cs:181) | no | none | on the player's clicks |
| N18 | the shop's reason + "." ("Not enough coin.", "Unlocks at level 9.") or "Not yet." | the player buys what cannot be bought (Shop.cs:196) | no | cannot-afford (gap 0.35 s) | on the player's clicks |
| N19 | "Paving costs 24 coin." / "No road is worn in enough to pave yet." | Pave mode refused (Pave.cs:81) | no | none | on the player's clicks |
| N20 | the extend lock reason / "Extending costs n coin." / "No cottage can be extended now." | Extend mode refused (Extend.cs:30) | no | none | on the player's clicks |
| N21 | a boat's request: "The barge Alder wants 12 Wool before the tide turns." | `BoatArrived` with `Value > 0` (River.cs:179-183; text from TownRiverShop.cs:148-153) | yes | boat-call (gap 1 s) | second age, several per tide |
| N22 | save notes: "The town could not be saved: the file could not be written." / "...could not be saved." / "The save on disk could not be read, so it is not written over. Restart the game to try it again." | `TownSaver.Failed` (TownSaver.cs:84-113, note shown once until a save works, line 124) | yes | none | on a failed save only |
| N23 | start notes: "The saved town could not be read from the disk, so a new town begins. The save is left as it was." / "...The old file is kept beside the save." | `Main` at start (Main.cs:131), `LoadLast` (TownSaver.cs:185) | yes | none | on a damaged save only |

**Estimate of notes per minute in a normal first age** (N1 to N16 added: 14 + 16 + 16 + 27 + 24 + 8 + 20 + 26 + 14 + 7 + 7 + 4 + 15 + 1 + 3 + 1): about 200 notes in about 32 minutes, **about 6 a minute, one every 10 s on average**, with a note alive 7 s. They are not even: a level-up raises N2, N3, N7 and N8 in one frame and the cap of three cuts the oldest at once. Player-made notes N17 to N20 come on top.

### 1.3 Cards that follow the town's state

| # | Message | Trigger and condition | Place and size | Stay and end | Arrival / leave | Collision | Covers what it is about? | Sound | Rate |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| C1 | **Choice of two** | `Town.ChoiceOpen`; `ChoiceOffered` at levels 4, 7, 10, 13 (Choice.cs:166-204) | top centre, `y = 14` or below the Pave banner, about 570 x 170 **(est.)** | until a boon is picked (click, Z or X); the town runs on | **one frame in, one frame out**; alpha pulses 0.94 to 1 | lies over B1 (see above); lies over the map's top strip, which is never an important thing by itself; the tide dial, if shown, ends 20 px or so before its left edge at 1080 and can reach under it at small windows **(not measured)** | no | press (ui-press) on pick; hover sound on each half | 4 |
| C2 | **Request card** | `Town.Request.Active` or a 1.5 s flash (Request.cs:104-148) | left column under the ledger, 330 wide, about 120 tall **(est.)** | until delivered, missed, or its 60 to 120 s run out | in one frame; met: green for 1.5 s then **gone in one frame**; missed: fades 1.5 s | stack with drive and notes | no | see N9 to N11 | 14 |
| C3 | **Skipper's request** (same card) | a boat with `Request` while no town request runs (River.cs:192-225) | same place | until the boat leaves | in one frame; swaps text with a town request that starts | the town request takes the card back | no | boat-call | second age |
| C4 | **Charter drive card** | `Town.Drive.Active` (Choice.cs:206-236) | under the request card | from level 15 until met | one frame in and out; bar in tenths | pushes the notes down | no | see B3, N14 | once |
| C5 | **Hint line** | `Town.RiverHint` or `Town.Hint` (Request.cs:150-183; strings in sim/Town/TownRequests.cs:164 on, TownRiverShop.cs:205-222) | last row of the ledger, 16 px | at least 4 s | one frame in and out; **the ledger grows and shrinks** | pushes everything under it | **the ring on the map (C6) points at the thing the line is about** | none | many; one per queue or full cottage, changes at most every 4 s |
| C6 | **Hint ring** | same as C5 when the hint names a road or plot | 3D torus at the road's middle or the plot, pulsing | while the hint shows | one frame | none | it marks the thing, can be hidden under the ledger or the shop column if the thing lies under them (the ring is a map object under the interface) | none | with C5 |
| C7 | **Tide dial** | second age (River.cs:149-169) | right of the ledger, `(356, 14)`, about 300 x 56 **(est.)** | always in the second age | one frame in, after the page-turn leaf covers the screen | none known | no | tide-turned plays from `RiverAudio` (gap 2 s) when the tide turns; **there is no note or banner for it, the dial text alone says it** | every tide |
| C8 | **Slim strip** | Page or Book stage | bottom left | while zoomed out | fades with the interface | the bell and strip share the corner during the cross-fade | no | page-turn on entering the Book stage | on zooming |
| C9 | **Bookmark text** (state line) | `ShopFrame` (Shop.cs:223-312) | in each bookmark | always | text swapped in one frame | n/a | no | none | continuous: "Need 12 more" changes each second |
| C10 | **Bell button text** | `BellFrame` (Shop.cs:398-432) | bottom left | always | one frame per second: "Rests 31 s", "Ringing: 6 s left, then rests 40 s" | n/a | no | bell sound on ring | continuous |
| C11 | **"Ring in the next age" button** | `CanRingNextAge` | above the bell | until rung | one frame in | n/a | no | none | once |

### 1.4 Floating coin numbers and labels over the map

| # | Message | Trigger | Place and size | Stay and end | Arrival / leave | Collision | Covers what it is about? | Sound | Rate |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| F1 | **"+12"** | `Delivery`, `LandingDelivery`, `TollPaid` if the setting is on (TownHud.cs:428; River.cs:177-178) | 34 bold gold, outline 6 px, anchored over the building (9 up, 1.5 cells north) then 44 px up, rising 36 px | 1.8 s | **full opacity from the first frame** (alpha `(1 - k) * 2` clamps to 1), fades in its second half; **hidden in one frame when Pave or Extend mode starts**; a second delivery within 2 cells restarts the first one's age at 0.3 s, so its alpha **jumps back to full** | merges per spot; 32 max | **yes: it is drawn above the building at the door's side and can cover the villagers queuing there and the stake label**; the left cards cover the numbers that land under them | one coin sound at most every 0.25 s (Audio:162-165) | one per delivery; with 27 villagers at the end of the age, several a second **(est.)** |
| L1 | **Stake label** "A cottage, at level 5" / ", to buy" | a plot staked (next level) or given and not bought | `Label3D` 38 px at 0.03, ink with paper outline, at the plot's front edge | until the plot is built (`Sync`, TownBuildings.cs:283-291) | appears with the stakes (the plot model pops in or is instant); text swaps in one frame; hidden in one frame by `LabelsFrame` when a card, a note or another label touches it | yes, hides itself under the interface | **it is the thing's own name and can be covered by the floaters and the notes** | none | one per staked plot, about 3 to 6 at a time |
| L2 | **Boat label** "3 of 6 Wool, waiting" / "Full, leaving" | second age, a boat shown (RiverBoats.cs:162-166) | `Label3D` 36 px at 0.026, over the water east of the boat | while the boat is there | text swap | hidden when touching a card | no | boat sounds | per boat |
| L3 | **"+ hand"** | `HandOffered(w)` (Choice.cs:238-267) | label 15 bold over the workplace | until bought | in one frame | none | it sits over the workplace | none | 3 |
| L4 | **Price tip** "Cobbles for this road: 24 coin. Click to pave." / "Extend this cottage: 60 coin. Rooms 2 to 3." | Pave or Extend mode with a hovered target (Pave.cs:233-243; Extend.cs:121-131) | 16 bold card beside the pointer, moved to dodge walkers (`TipBeside`) | while hovered | one frame | the "Suggested" tag if both near | it can cover the road or cottage only if no clear place is found | none | while the mode is on |
| L5 | **"Suggested: 24 coin"** | the town's suggestion is paveable or extendable | 15 bold card above the road's middle or the plot | while the mode is on | one frame | with the tip (tip is pushed under the pointer) | no | none | while the mode is on |
| L6 | **Pave / Extend banner** | `PaveMode`/`ExtendMode` (Pave.cs:218-229) | top centre `y = 14`, 18 bold card | while the mode is on | one frame | pushes B* to 70 and the choice card under it | no | ui-press on entering | on the player's action |

### 1.5 Pages

| # | Message | Trigger | Place and size | Stay and end | Arrival / leave | Collision | Sound | Rate |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| P1 | **Achievements page** | Tab or the button (Book.cs:160-170) | centred, 1120 wide, tall (2 columns of up to 16 rows plus the head) | until Tab, Esc, Close, or the stage changes | fade and rise 16 px over 0.22 s; turned round halfway it continues from where it is | closes the age page; ends Pave and Extend modes; **the choice card stays over it** ("starts below the card" comment, Choice.cs:169) | ui-tab on open and close | the player's choice |
| P2 | **First age page** "The first age is complete" | 3.2 s after ringing in (`AgePageDelay`, Book.cs:199, 242) or the "Read its page again" button | centred, 820 wide | until closed (Esc or "Keep playing") | fade and rise | closes the achievements page; replaced by P3 in the second age | page-turn | once, then on demand |
| P3 | **Second age page** "The second age: The River" | same button, in the river age (River.cs:246-257) | same | same | same | same | page-turn | once |
| P4 | **Borough page** "The second age is complete" | 3.2 s after the seal (River.cs:186, 233-244) | same | same | same | same | page-turn (the seal sound sits with B6) | once |
| P5 | **Page-turn leaf** | ringing in the next age (River.cs:120-127) | whole screen, 1.1 s | until done | eased; its shading grows and shrinks | draws over every card; the title and the tide dial change under it | page-turn | once |
| P6 | **Left page of the book** (summary, achievements, boons) | the book in view (`TownSummary`) | on the 3D page | while visible | text swaps in one frame (twice a second at most) | none | none | continuous |
| P7 | **Map title** "A Plan of the Hamlet" / "The Town on the River" | the age changes (River.cs:129-134) | 3D label | permanent | swapped under the leaf | none | none | once |

### 1.6 Pause and settings messages (shown only with the book closed)

| # | Message | Trigger | Sound |
| --- | --- | --- | --- |
| S1 | "Start a new town? This town will be lost." with Yes and No (PauseMenu.cs:21, 84) | New town on the cover | book / ui sounds |
| S2 | "Keep this? The window has changed. Keep it like this?" and "Going back in n seconds." (SettingsPages.KeysSaving.cs:248, SoundDisplay.cs:137-141) | a window mode or size change | none |
| S3 | "Delete the save? The town on disk is removed. This cannot be undone." (KeysSaving.cs:250) | Delete in Saving | none |
| S4 | "Saved." / "The game could not be saved." / "The last save is loaded." / "There is no save to load." / "The save is deleted." (KeysSaving.cs:159, 168, 355) | the buttons of the Saving page | none |
| S5 | "Press the new key. Esc cancels." / "Press a key" (KeysSaving.cs:42, 56) | rebinding | none |

## 2. Frequency in a normal first age (summary, est.)

| Per 32 minutes (payback bot, seed 1) | Count |
| --- | --- |
| Banners (B1 15, B3 1, B4 4, B2 1, B5 1; two of them overwritten unseen) | 22 raised, 20 seen |
| Notes N1 to N16 | about 200 (about 6 a minute) |
| Floating numbers | one per delivery; no count from the simulation was taken (the tool prints coin a minute: 22 at minute 1, 2,089 at minute 15, 13,311 at minute 32); a count of `Delivery` events from the sim would give the figure |
| Requests: card, 3 notes kinds, 2 sounds | 14 asked, 7 met, 7 missed |
| Purchases (no message at all, only the bookmark's text and a sound) | 191, about 6 a minute |
| Choice cards | 4 |
| Page openings by the player | 0 to a few |
| Hint changes | each at least 4 s apart, in practice every 10 to 60 s **(est.)** |
| Sounds | about 20 distinct cues, 3 players shared by many messages |

## 3. The moments with the most messages at once

1. **A level that is a choice level (4, 7, 10, 13)**, with a request running, in the first minutes of the level. On the screen together: ledger (with its hint), request card, up to three notes (N2 "is open", N3 "New to buy", N12 "A choice of two boons...", plus N7 achievement or N8 villager, which push each other out), the level banner **hidden behind** the choice card, the choice card, up to 32 floating numbers, the stake labels of the next level's plots, the "+ hand" markers, new bookmarks for the shop (the whole column rebuilt in one frame), and sounds from level-reached, gift-arrives, request-asked and achievement within the same frame, two of them on `_bellP`.
2. **Level 15 (charter drive) and level 16 (charter)**: B1 overwritten by B3, then B2 two minutes later; drive card appears in the left column (pushes the notes down), "Ring in the next age" button appears above the bell, the camera glides, `charter-granted` and `level-reached` share `_bellP`.
3. **Ringing in the next age**: banner B5, the page-turn leaf over the whole screen, the peal of four bells, the camera glide, 3.2 s later the age page P3 over the town (a second fade in), the tide dial appearing under the leaf.
4. **Second-age tide with a skipper's request and a town request together**: the town's request owns the card and the skipper's request is invisible until it ends, though its note N21 and boat label L2 show.
5. **Pave mode on while a level banner and a coin number are up**: Pave banner (`y = 14`), level banner pushed to `y = 70`, the choice card under both, tip and "Suggested" tag, lit bands; the coin numbers go away in one frame.
6. **Page open while a choice, a banner or a note comes**: the achievements page (centre) with the choice card above it, banner behind them, notes on the left.

## 4. Every place where something appears or disappears in one frame

Verified from the code (file:line):

1. **Notes appear on top of the stack at full opacity and push the others down** (TownHud.cs:389-390). **The oldest is deleted at once** when a fourth comes (392-397).
2. **Banner replaced:** text swapped and the opacity dropped to 0 in one frame while it was at 1 (Banner.cs:69-71, 86).
3. **Banner and notes made while the interface is zoomed out** are never seen (`_fullA <= 0.01`, Banner.cs:73; TownHud.cs:310).
4. **Floating numbers appear at full opacity** (TownHud.cs:506-508, 527), jump back to full opacity when a delivery adds to them (496), and **vanish in one frame when Pave or Extend mode starts** (520-523).
5. **Request card** appears in one frame (Request.cs:114-116); on a met request it **vanishes in one frame** after 1.5 s of green (113-117 with `_reqFlash`).
6. **Drive card** appears and goes in one frame (Choice.cs:210-211).
7. **Hint line** appears and goes in one frame (Request.cs:179-181); the ledger changes height with it, so the cards under it jump.
8. **Hint ring** `Visible` flips (Request.cs:168-170).
9. **Choice card** appears and goes in one frame (Choice.cs:170-172).
10. **Shop column rebuilt**: every bookmark and head freed and made again in one frame when the set of items changes (Shop.cs:228, 366-396); finished or locked steps fold to one line, so the column's height jumps; `_compactShop` and `_tightShop` change the type size of the whole column in one frame (305-311).
11. **Buttons that show and hide:** "Ring in the next age" (Shop.cs:419), the achievements button (Book.cs:265), Home (Banner.cs:115), the slim strip's `Visible` (TownHud.cs:320).
12. **Pause button jumps** between the bottom right and the slim strip (Book.cs:262-264).
13. **Pave and Extend banner, tip, "Suggested" tag, the lit-band mesh** flip with the mode (Pave.cs:222, 232-234).
14. **"+ hand" markers** `Visible` flips (Choice.cs:244, 266).
15. **Tide dial** `Visible` flips (River.cs:150); title text swapped under the leaf (River.cs:148).
16. **Map labels**: `LabelsFrame` hides or shows each label every frame (Labels.cs:69), so a label blinks as the camera moves or a note comes and goes; stake text swaps (TownBuildings.cs:290); boat label text swaps (RiverBoats.cs:162).
17. **Buildings pop in** over a short time (`_pop`, TownBuildings.cs:277) but stakes appear and disappear in one frame (`Visible`, 288-289).
18. **Summary page text** (TownSummary.cs:96-125) swaps in one frame, and the achievements block changes pixel size and position when the age or the count changes (153-175).
19. **Settings "Keep this?" card** appears when the window changes; the interface rebuilds in one frame at a size change (TownHud.cs:188-189).
20. **Bookmarks' text** and the **bell's text** change in one frame (Shop.cs:287, 408-414).
21. **Text-only changes in the ledger** ("coin" every time it changes, level line, next gift line, renown) swap in one frame; the next gift line may change its number of lines and so the ledger's height.

Eased (not in the list): the achievements and age pages (0.22 s), pause motion, page-turn leaf (1.1 s), slim strip and full interface cross-fade, banners' fade, request-missed fade, note fade out.

## 5. Number of separate places a message can appear (in play)

Counted from section 1: (1) note stack, (2) banner, (3) choice card, (4) request/skipper card, (5) drive card, (6) hint line, (7) hint ring on the map, (8) floating numbers, (9) stake labels, (10) boat labels, (11) "+ hand" markers, (12) Pave/Extend banner, (13) price tip, (14) "Suggested" tag, (15) tide dial, (16) bookmark state lines, (17) bell button, (18) next-age button, (19) achievements button count, (20) slim strip, (21) ledger lines (level, renown, next gift), (22) Home button, (23) achievements page, (24) age page, (25) page-turn leaf, (26) Godot tooltips, (27) left page summary on the book, (28) map title on the book. **28 places during play**, plus the pause lettering and question, and the settings pages' notes (3 more when paused). Only **three of the 28 have a rule about where they sit relative to the others** (banner vs Pave banner, choice card vs Pave banner, left column stack, tip vs tag); the rest are placed independently.

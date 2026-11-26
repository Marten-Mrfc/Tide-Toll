# Interface inventory (from the code, commit as checked out on 26 Nov 2026)

Written by a researcher, no code changed. All files are under `game/scripts/`. "TownHud.X" means `town/TownHud.X.cs`. Line numbers are of the file as read tonight. Where a size or a height is my own sum of the code's numbers and not measured on a picture, it is marked **(est.)**.

## 0. How the interface is built (needed to read the tables)

- **Two canvas layers.** `TownHud` is a `CanvasLayer`, Layer 5 (TownHud.cs:60). `PauseMenu` is Layer 20 (PauseMenu.cs:68). The 3D book, the map and every `Label3D` lie under both.
- **Everything of the town interface is hand-placed.** No anchors except two: `_root` is full rect (TownHud.cs:83) and `PauseMark` uses `BottomRight` preset once (TownHud.cs:116). Every other card is given a `Position` in canvas units from `UiKit.Px(_s, n)`, most of them every frame.
- **Scale `_s`.** `UiKit.ScaleFor(window height / 1080)`, clamped 0.78 to 2.5 (UiKit.cs:51). It is taken from the **real window height** (TownHud.cs:81). The "interface size" setting (80 to 150 percent, GameSettings.cs:23) is the window's **content scale** (DisplaySeam.cs:60): it shrinks the canvas (the visible rect `_built`) instead of enlarging `_s`. So at 150 percent in a 1920x1080 window the canvas is 1280x720 and every card is still built with `_s = 1.0`: the ledger (330 px) then takes 26 percent of the canvas width instead of 17 percent. The shop column code knows this and shrinks itself (section 3, Shop).
- **Window changes rebuild everything.** `_Process` calls `Build()` when the visible rect changes (TownHud.cs:188-189): all cards are freed and made again in one frame.
- **Fonts:** Atkinson Hyperlegible Regular/Bold (UI), Alegreya Variable (serif headings), Uncial Antiqua (display), UiKit.cs:27-31. `UiKit.FontPx(s, px)` never goes under 12 px (UiKit.cs:55).
- **One master fade.** `TownHud.Fade` (TownHud.cs:340-349) is the whole root's `Modulate`, driven by the pause motion.
- **Per-frame order** (TownHud.cs:186-206): Build check, events, `StageFrame`, `Refresh`, `StepNotes`, `StepFloaters`, `ShopFrame`, `PaveFrame`, `RequestFrame`, `HintFrame`, `BannerFrame`, `HomeFrame`, `BookFrame`, `RiverFrame`, `ChoiceFrame`, `ColumnFrame`, `LabelsFrame`. `ColumnFrame` is last of the layout steps, so it overwrites positions the earlier steps set for the left column (see rows Request, Drive, Notes).

## 1. Zoom stages (what Page and Book do to the interface)

`Main.Stage` (Main.cs:56-60): `Town` while `Camera.Size < SlimSize` (= 0.86 x PlaySize), `Page` from `SlimSize`, `Book` from `(PlaySize + MaxSize) / 2`.

`StageFrame` (TownHud.cs:299-331) computes `a = clamp((0.94 x Slim - size) / (0.12 x Slim), 0, 1)`: the "full interface" opacity `_fullA`. It is 1 up to size 0.82 x Slim and 0 from 0.94 x Slim, so the **full interface is already gone before the Page stage starts**. Parts in `_fullParts` (TownHud.cs:131) get `Modulate = a` and `Visible = a > 0.01`: **ledger (with its hint), notes, shop column, bell, "Ring in the next age" button, floaters layer, banner, achievements button**. Other parts check `_fullA` themselves.

| Element | Town stage | Page and Book stages |
| --- | --- | --- |
| Ledger, shop, bell, next-age button, achievements button, floaters | full | faded to 0 and hidden between 82 and 94 percent of Slim |
| Notes | shown | **hidden but still counting their 7 s**: a note made at Page or Book stage is never seen (TownHud.cs:310 sets `Visible` of the container; `StepNotes` still ages them) |
| Banner | shown | `Banner()` sets `Visible = _fullA > 0.01` (Banner.cs:73): a banner raised at Page or Book is lost; its 3.5 s run out unseen |
| Request card, drive card | shown | `show = ... && _fullA > 0.01` (Request.cs:114, Choice.cs:210): hidden |
| Tide dial (second age) | shown | hidden (`_fullA > 0.01`, River.cs:149) |
| Hint ring on the map | shown | hidden (Request.cs:168) |
| "+ hand" markers | shown | hidden (Choice.cs:242) |
| Choice of two card | shown | **still shown, over the book** (`show = t.ChoiceOpen`, Choice.cs:170; comment "never missed") |
| Pave / Extend banner, tip, "Suggested" tag, lit road bands | shown while the mode is on | not gated by stage (Pave.cs:221-233): mode stays on; the lit bands are 3D on the map |
| Achievements page | opens with Tab | `OpenAchievements` refuses under `_fullA < 0.5` (Book.cs:164); an open one is closed when `_fullA < 0.5` (Book.cs:269) |
| Age page | open | faded out the same way (Book.cs:269) |
| Slim strip (coin and level) | hidden | visible while `a < 1`, fades in as the rest fades out (TownHud.cs:320-328); bottom left, `(16, H - h - 12)` |
| Pause button | bottom right | **jumps** to the right of the slim strip once `_fullA < 0.5` (Book.cs:262-264), in one frame |
| Back to the town [Home] | shown when the view is moved | only at the Town stage (Banner.cs:114) |
| Map labels (stakes, boats) | shown | hidden only while a page is open; otherwise left to the camera (Labels.cs) |
| Left page of the book | not seen | `TownSummary` Label3Ds (section 4) carry the town's numbers and the achievements |
| Sound | none | `PageTurn()` plays when entering or leaving Book (TownHud.cs:317) |

## 2. Table of every element

Columns: **builds** (file and method); **place** (anchor, offset, size at 1920x1080, 100 percent, `_s = 1`); **visible when**; **shows**; **moves or resizes**; **Page/Book**; **second age**.

### 2.1 Left column (the ledger and what hangs under it)

| Element | Builds | Place | Visible when | Shows | Moves / resizes (what pushes it) | Page / Book | Second age |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Ledger card** | TownHud.cs `BuildLedger` 137-184 | top left, `(16, 14)`, min width 330, content margin 10, paper alpha 0.96, border 1.5, corner 3. Height about 145 px **(est.)** without a hint | Town stage (in `_fullParts`) | coin icon 30 px + "N coin" (26 bold) + "N a minute" (16, faded); rule; "Level n of max" (19 serif); level bar 12 px (Thatch fill); "Renown a of b" (15 faded); next gift (17, wraps at 310 px); hint line (see next row) | `ColumnFrame` calls `ResetSize` each frame (TownHud.cs:217); grows with wrapped next-gift text and with the hint. Text changes only when a value changes (`Refresh`, 241-266) | faded out, replaced by slim strip | same card; next-gift line becomes the river's gift |
| **Hint line** (inside the ledger) | Request.cs `BuildHintLine` 39-48 | last row of the ledger column, 16 px, wraps at 310 px, "Hint: ..." | when `Town.RiverHint` or `Town.Hint` is non-empty (HintFrame 150-183); each text held 4 s (`HintHold`) | one line of advice | **appears and goes in one frame; the ledger grows and shrinks by its height (about 20 to 60 px) and pushes the request card, drive card and notes** | with the ledger | river hint takes precedence (Request.cs:152) |
| **Hint ring** | Request.cs `BuildRequestCard` 61-86 (torus mesh in `MapOrigin`) | 3D ring on the road or plot the hint is about, pulses by 12 percent | hint has a road or plot, `_fullA > 0.01`, no Pave/Extend mode | pale orange ring (Warn, alpha 0.6, no depth test) | position and scale each frame; `Visible` flips in one frame | hidden | none for river hints (road and plot -1) |
| **Request card** | Request.cs `BuildRequestCard` 50-86 | `(16, ledger bottom + 8)` set in `RequestFrame` (line 117) **and again by `ColumnFrame`** (TownHud.cs:224); min width 330, margin 9, paper 0.96, border 1.5 | a town request is active, or its 1.5 s flash runs, and `_fullA > 0.01` | "Request from the miller" (18 serif), "12 Grain within 45 s" (17 bold), "Delivered a of b" (15), time bar 8 px (blue; orange under 25 percent), "Reward: n coin and renown" (15 faded) | appears and goes in one frame (met: stays 1.5 s green then **vanishes in one frame**; missed: fades 1.5 s grey). Pushed by the ledger's height. Pushes drive card and notes | hidden | the same card shows a skipper's request (`RiverRequestFrame`, River.cs:192-225) when no town request runs: "Request from the skipper X" |
| **Charter drive card** | Choice.cs `BuildChoiceParts` 109-138 | under the request card (or ledger), `x = 16`, min width 330, margin 9, fill `Pressed` mixed with paper, alpha 0.97, border 2 | `Town.Drive.Active` and `_fullA > 0.01` (DriveFrame 206-236). Active from level `MaxLevel - 1` | drive name (19 serif), "Send n Grain for the charter" (17 bold), 12 px bar in ten parts with ticks, "Sent a of b. Every load counts." (15) | appears and goes in one frame; the bar steps by tenths; pushed by ledger and request card | hidden | not shown in the river age (drive done) |
| **Notes** (margin slips) | TownHud.cs `Note` 376-401, container made at 105-109 | x 16, below the last card of the column (`ColumnFrame` 227: `y + 2`; `StepNotes` 405-407 sets `top + 10` first, **a second writer**). Slip width about 334 (label min 320 + margin 7), up to 3 slips, 6 px apart | `Note()` called; 7 s each (`NoteSeconds`) | text 16 regular (strong: 18 bold on a `Pressed` slip, border 2) | new slip is `MoveChild(0)`: **inserted on top in one frame, no fade in; all older slips drop by its height**. Beyond 3 the oldest is removed in one frame (392-397). Fade out only in the last 1.2 s | hidden but timers run (see section 1) | same; more strong notes for boats |
| **Slim strip** | TownHud.cs `BuildSlim` 282-293 | bottom left `(16, H - h - 12)`, paper 0.94, border 1.2, margin 6 (14 left/right) | `a < 1` (a Page or Book stage, or the cross-fade) | "1,234 coin   Level n of max" (18 bold) | its alpha is `1 - a`; **sits at the same corner as the bell button** (bell `(16, H - h - 16)`), so both show in the cross-fade | the only HUD card left | same |

### 2.2 Top of the screen

| Element | Builds | Place | Visible when | Shows | Moves / resizes | Page / Book | Second age |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Level / event banner** | Banner.cs `BuildBanner` 48-65 | top centre: `x = (W - w)/2`, `y = 18` (70 while a Pave/Extend banner shows), height about 70 **(est.)**, width = the text's width (the label does not wrap) plus 44; `Pressed` colour, alpha 0.97, border 2.5; head 28 serif, text 18 bold | `Banner()` called; 3.5 s; `_fullA > 0.01` | "Level 4" + gift; "The charter"; "A new age"; "A boon for good"; drive; "The borough seal" | `BannerFrame` 80-101. If its `_bannerAt` gift point would lie under it, it goes to the bottom `H - h - 70` (level banners only). **A second banner replaces the first in place: text swapped in one frame and the fade restarts from 0** | lost (see section 1) | same, plus the seal |
| **Choice of two card** | Choice.cs `BuildChoiceParts` 56-107 | top centre, `y = 14` (below the Pave banner when it shows, 196), width about 570, height about 170 **(est.)**: head 20 serif + two buttons 270 x 112, margin 10, border 2, alpha 0.97 and a pulse 0.94 to 1 | `Town.ChoiceOpen` (levels 4, 7, 10, 13; stays open until picked; the town runs on) | "Level n: choose one boon, for good. The town runs on."; per side icon 44, name + key [Z] / [X] (18 bold), text (15, wraps at 196), "+n coin a minute now" (14 faded) | **`y = 14` against the banner's `y = 18`: both centred at the top, the card is the later child and lies over the banner**. A tooltip on each half. Appears and goes in one frame | **stays** | no choice is offered in the river age: `TownRules.ChoiceLevels` is 4, 7, 10, 13 and the river's levels are 17 to 28 (TownRiver.cs:34) |
| **Pave / Extend banner** | Pave.cs `BuildPave` 53-58 | top centre `y = 14`, paper 0.95, border 2, margin 8, 18 bold; text "Pave: click a lit road. Hold Shift to pave several. Esc to stop." | `PaveMode || ExtendMode` | the mode's help | one frame; pushes the level banner down to 70 and the choice card to 8 px under itself | not gated | same |
| **Tide dial** | River.cs `BuildRiverParts` 49-70, `DrawDial` 103-117 | `(ledger right + 10, ledger top)` = about `(356, 14)`; icon 34, dial 40, text 16 bold two lines; paper 0.95, border 1.2, margin 7 | second age and `_fullA > 0.01` and not under the page-turn leaf (`PageCovering`) | "High water / rising, highest in 2:05"; dial with needle | `Visible` flips in one frame when the age is rung in; dial redraws every 10th step | hidden | **second age only** |
| **Page-turn leaf** | River.cs 74-77 and `PlaceWipe` 85-100, `PageLeaf.cs` | full screen, over every card (re-ordered to be the last child each frame) | 1.1 s after "Ring in the next age" | an opaque paper leaf sweeping right to left | eased (`BookMotion.Smoother`) | n/a | the change of age |

### 2.3 Right column and bottom

| Element | Builds | Place | Visible when | Shows | Moves / resizes | Page / Book | Second age |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Shop column** | Shop.cs `BuildShop` 126-146, `RebuildMarks` 366-396, `ShopFrame` 223-312 | `VBoxContainer` at `(W - 280 - 14, 10)` = `(1626, 10)`, width 280, bookmarks 270 min wide, 4 px apart. Group heads "People / Work / Roads / Town / The river" 17 serif, right aligned (14 when compact) | Town stage | one bookmark per `ShopItem` (Villager, Extend, Pace, Load, Tools, Value x up to 3, Pave, building offers, Bell track, Hand, Regulars; river steps) | **Set of items changes (a gift, a track finished, a unlock): all bookmarks are freed and made again in one frame** (`RebuildMarks`), so everything below a new head or bookmark jumps down. A folded line (locked or done) is one line of 13 px, so buying the last step of a track pulls the rest up. Room check against the achievements button: step 1 `_compactShop` (13 px text, heads 14), step 2 `_tightShop` (worth line dropped) (Shop.cs:305-311); never undone until the HUD is rebuilt | hidden | adds group "The river"; river locked and finished steps are left out (`RiverHidden` 221) |
| **Bookmark (one shop item)** | Shop.cs `MakeButton` 148-177 | 270 x (1 to 3 lines) card; margin 8; icon up to 26 px; text 15 (13 compact or folded) | per item | line 1 "Name, level n   [key]", line 2 "price coin: Buy / Need n more / Unlocks at level 9", line 3 "+n coin a minute" | text rebuilt only on a change of its signature (255-257); a price bar 4 px (Thatch) at its foot while coin is the one thing missing | with the column | river items use `ui-ferry`, `ui-boat`, ... icons |
| **Bell button** | Shop.cs `MakeButton` in `BuildShop` 139, `BellFrame` 398-432 | bottom left `(16, H - h - 16)`, width 250 min, two lines | Town stage; text "Comes at level 2" before level 2 | "The bell   [Space]" / "Ring it" / "Rests 31 s" / "Ringing: 6 s left, then rests 40 s" | disabled style while resting; changes text each second | hidden | same |
| **Ring in the next age button** | Shop.cs 142-145, 419-431 | `(16, bell top - h - 8)` | `CanRingNextAge || NextAgeRung`, Town stage, no age page | "Ring in the next age   [N]" / "The charter is yours"; later "The river age ... Read its page again" | appears in one frame, above the bell | hidden | label becomes "The second age" once the seal is held |
| **Achievements button** | Book.cs `BuildBookParts` 77-79, `BookFrame` 265-268 | `(PauseMark left - w - 10, H - h - 16)`, 15 px, two lines | Town stage, not while the age page is open | "Achievements   [Tab]" / "7 of 25" ("on the river" in the second age) | follows the pause button | hidden | counts the river's 16 |
| **Pause button** | TownHud.cs 114-127, Book.cs 260-264 | bottom right `(W - w - 16, H - h - 16)`, a full card, 16 px, "Pause  [Esc]" | always | closes the book | at Page and Book it **jumps** beside the slim strip | moves | same |
| **Back to the town [Home]** | Banner.cs `BuildHomeMark` 103-110, `HomeFrame` 112-120 | bottom centre `H - h - 16`, 16 px, theme button (bookmark look, tucked left edge) | view moved by the player, Town stage, not paused, no Pave/Extend mode | "Back to the town  [Home]" | one frame in and out | hidden | same |

### 2.4 Over the map

| Element | Builds | Place and size | Visible when | Shows | Moves / resizes | Page / Book | Second age |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Floating coin number** (32 labels in a pool) | TownHud.cs 86-99, `Float` 488-510, `StepFloaters` 512-529 | 34 bold gold `#F2C14E`, outline `#3A2A10` 6 px; centred over the delivery spot, starting 44 px above its anchor (cell x, height 9, cell y - 1.5) and rising 36 px in 1.8 s | each `Delivery` (and `LandingDelivery`, `TollPaid`) if the setting "coin numbers" is on | "+12" | a second delivery within 2 cells of a rising number adds to it and drops its age back to 0.3 s (alpha jumps back to 1). Opaque at once, fades over the second half. **Hidden in one frame when Pave or Extend mode starts** | with the layer | landing and toll numbers too |
| **Stake label** ("A cottage, at level 5" / ", to buy") | TownBuildings.cs `MakeStakes` 230-237, `StakeText` | `Label3D`, billboard, font 38, pixel size 0.03, ink with paper outline 14; at the plot's front edge, 1.6 up | the plot is staked (next level's plots; given offers not bought) | what stands there and when | text swapped in `Sync` (TownBuildings.cs:290); `LabelsFrame` hides it in one frame when a card or another label would touch it | hidden under pages only | `RiverLayer.StakeLabel` 210-216, "The landing, at level 3" |
| **Boat label** ("Wants", e.g. "3 of 6 Wool, waiting") | RiverBoats.cs 59-66, text 162-166 | `Label3D`, font 36, pixel size 0.026, 3.5 to 5.5 up, east of the boat | a boat is shown | cargo count, "Full, leaving" | text changes with each load | as stake labels | **second age only** |
| **"+ hand" marker** | Choice.cs `HandFrame` 238-267 | label with a card box, 15 bold, above the workplace (6 up) | `Town.HandOffered(w)` and Town stage, no page | an extra hand is on offer | placed every frame | hidden | same |
| **Pave and Extend lit bands, "Suggested: n coin" tag, price tip** | Pave.cs 51-52, 63-73, `PaveFrame` 218-278; Extend.cs | tip 16 bold and tag 15 bold, cards paper alpha 1 border 1.5 margin 5; bands are 3D, blue (Focus) alpha 0.42 to 0.85 | mode on | "Cobbles for this road: 24 coin. Click to pave." | tip follows the pointer (`TipBeside`, Extend.cs:171-195, moves to avoid walkers); tag follows its road | not gated | same |

### 2.5 Pages and pause

| Element | Builds | Place | Visible when | Shows | Motion | Page / Book | Second age |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Achievements page** | Book.cs `Page` 145-158, `BuildBookParts` 81-124 | centred, min width 1120, paper 0.985, border 2.5, margin 22 (34 sides); head 32 serif; two columns of 500 px cells (name 16 bold, state 15, description 13, bar 5 px) | Tab or its button, Town stage | achievements with progress | fade and rise 16 px in 0.22 s (Pages.cs 16-88) | closed under `_fullA < 0.5` | shows the river's 16 and a count of the first age in the heading |
| **Age page** | Book.cs 126-139 | centred, min width 820; title 40 Uncial; text 19 serif wraps at 740; button 18 | 3.2 s after ringing in (or the seal), or the button | "The first age is complete" / "The second age: The River" / "The second age is complete" | same fade | faded out | three texts (River.cs:228-261) |
| **Pause lettering** | PauseMenu.cs 70-100, 190-210, `Layout` 260-300 | over the closed book: title 96, name 40, buttons 44 (34 in the question), question text 40 | pause (Esc) | "Resume, Settings, New town, Quit"; "Start a new town? This town will be lost." | `BookMotion` timeline | n/a | n/a |
| **Settings pages** | settings/SettingsPages*.cs | on the 3D spread; heading 44 serif, "Settings" 22 Uncial, blurb 17, rows 19, notes 15 to 17, confirmation card 30/18/17 | from the pause menu | volume sliders, display, keys, saving | leaf turn | n/a | n/a |
| **Tooltips** | `UiKit.MakeTheme` 71-77 | Godot tooltip, paper card border 1.5, margin 8, 15 px | hover on bookmarks, achievement cells, bell, tide, boons, Home, Pause | the explanations (Shop.cs:340-362) | engine default delay | n/a | n/a |

### 2.6 Printed on the book (3D, not HUD)

`TownSummary.cs`: town name (Uncial), age and level line, eight rows (Villagers, Cottages, Roads, Paved roads, Coin earned, Trips, Time in the town, Achievements), next gift, boons held, achievements in two columns. Rewritten twice a second while `Watched`, only if a value changed (UpdateNow 96-125); **text swaps in one frame**, and in the second age three rows are added (River cottages, Boats filled, Tolls taken) and the pixel size of rows goes from 0.06 to 0.045 (line 110); achievements are re-set in size 400 to 520 by count (154). `BookBuilder.cs:159-210`: folios, "A Plan of the Hamlet" (swapped to "The Town on the River" under the leaf, River.cs:129-134), compass.

## 3. How elements move or push each other (summary)

1. **Left column stack** (ledger, request or skipper card, drive card, notes): `ColumnFrame` (TownHud.cs:213-229) re-stacks them each frame with an 8 px gap, from their combined minimum sizes. Three other places also set the same positions earlier in the frame (Request.cs:117, Choice.cs:215, TownHud.cs:405-407); the last writer wins, so the earlier ones are redundant and can disagree for the frames in which a size changes.
2. **What pushes the stack down:** hint appears (+20 to 60), request card appears (+about 120 **(est.)**), drive card appears (about 110 **(est.)**), a new note (its own height, 40 to 80 **(est.)**). Nothing animates; all jumps are single frames.
3. **Top centre has three claimants at nearly one place:** Pave/Extend banner (y 14), choice card (y 14, or under the Pave banner), level banner (y 18, or 70 under the Pave banner). Only the last two are told about the Pave banner; the choice card and the level banner are **not told about each other**.
4. **Bottom left has three:** bell (and next-age button above it), slim strip (`H - h - 12`), and at the Book stage the pause button beside the strip.
5. **Right column vs achievements button:** the shop measures room down to the achievements button and switches to compact and then to tight text (and so the column's content changes depending on a card at the other side of the screen).
6. **Map labels vs cards:** `LabelsFrame` hides any `What` / `Wants` label whose screen rect touches a card, a note slip, or another label, every frame (Labels.cs:27-75); labels are hidden whole, never moved, so a camera move or a new note makes labels blink out and in.

## 4. Styles in use

### 4.1 Font sizes (px at 1080, before scale)

Counted from every `UiKit.Text`, `FontPx`, `AddThemeFontSizeOverride` and theme default in the files named in the brief:

| Group | Sizes |
| --- | --- |
| Town HUD (plain screen during play) | **13, 14, 15, 16, 17, 18, 19, 20, 26, 28, 32, 34, 40** = 13 distinct |
| Pause lettering | 34, 40, 44, 96 |
| Settings | 15, 16, 17, 18, 19, 22, 25, 26, 30, 44 |
| 3D labels (pixel size, not px) | stake 38 at 0.03, boat 36 at 0.026, summary and map titles 400 to 1150 per 10 000 |
| **All distinct px sizes in the interface code** | **13, 14, 15, 16, 17, 18, 19, 20, 22, 25, 26, 28, 30, 32, 34, 40, 44, 96 = 18** |

Where they are used (HUD): 34 floater (TownHud.cs:93); 26 coin; 19 level, drive name, 19 age text; 17 next gift, request ask, drive ask, group head; 16 rate, hint, note, Home, tide, tip, achievement name; 15 renown, request lines, boon text, tag, bookmark default, achievements button; 14 boon worth, compact group head; 13 folded bookmark, compact bookmark, achievement description; 18 slim, banner text, strong note, request who, pave banner, boon name; 20 choice head; 28 banner head; 32 achievements head; 40 age title.

**At 1280x720, `_s = 0.78` and `FontPx` has a floor of 12:** 13, 14, 15 and 16 all become 12; 17 becomes 13; 18 becomes 14. Five tiers become one. At 1080 the HUD has 13 tiers, at 720 about 9.

### 4.2 Cards (`UiKit.Card`, UiKit.cs:118-125, corner always 3, antialiased)

| Where | Fill | Border px | Margin px |
| --- | --- | --- | --- |
| Ledger | Paper alpha **0.96** | 1.5 | 10 |
| Request card | Paper 0.96 (green mix 0.97 on met) | 1.5 | 9 |
| Drive card | Pressed/Paper mix 0.97 | 2 | 9 |
| Note | Paper **0.95** | 1 | 7 |
| Strong note | Pressed 0.95 | 2 | 7 |
| Banner | Pressed **0.97** | **2.5** | 8 (22 sides) |
| Choice card | Paper 0.97 | 2 | 10 |
| Boon half | Paper / PaperHover / Pressed | 1.5 / 2.5 / 2.5 | 8 |
| Slim strip | Paper **0.94** | 1.2 | 6 (14 sides) |
| Tide | Paper 0.95 | 1.2 | 7 |
| Pave banner | Paper 0.95 | 2 | 8 |
| Tip, Suggested tag | Paper 1.0 | 1.5 | 5 |
| Bookmark | Paper 1.0, hover PaperHover (border 2), pressed Pressed (2.5), disabled PaperDark (border 1, faded ink) | 1.5 | 8 (6 achievements button) |
| Pause button | same four states as a bookmark (2 pressed) | 1.5 | 12 / 5 |
| Pages | Paper 0.985 | 2.5 | 22 (34 sides) |
| "+ hand" | Pressed 0.95 | 1.5 | 4 |
| Theme button (Home, close buttons) | `UiKit.Tab`: paper, no left border, right corners 7, margin 19/10/5 | 1.5 | tucked 9 px |
| Bars | PaperDark with border 1 (0.6 on the achievements page), fill Thatch | 1 | 0 |

So: **5 paper alphas** (0.94, 0.95, 0.96, 0.97, 0.985) plus opaque, **7 border widths** (0.6, 1, 1.2, 1.5, 2, 2.5, plus 3 px focus frame), **8 content margins** (4, 5, 6, 7, 8, 9, 10, 12 and the page's 22/34), **3 corner radii** (3 card, 7 bookmark tab, 9 settings tab; 5 focus). Two button looks (full card bookmark, tucked tab) are used side by side: the pause button and the bookmarks are full cards, the Home and close buttons are tucked tabs.

### 4.3 Colours

| Meaning | Colour | Defined | Used for |
| --- | --- | --- | --- |
| Paper | `Palette.Paper` (#F2EAD8 per UiKit comment) | Palette.cs, UiKit.cs:17 | every card |
| Ink | `Palette.Ink` #2B2620 | same | text, borders |
| Faded ink | Ink 38 percent toward Paper | UiKit.cs:23 | rate, renown, descriptions, worth |
| PaperDark #E4D6B8 | UiKit.cs:19 | bar backs, disabled bookmark |
| PaperHover #F8F2E4 | UiKit.cs:20 | hover |
| Pressed #E9D49A | UiKit.cs:21 | pressed bookmark, banner, strong note, "+ hand", boon pressed |
| Focus #56B4E9 (sky blue) | UiKit.cs:22 | request bar, pave bands, keyboard focus frame |
| Good #009E73 | UiKit.cs:24 | only the met-request flash (mixed 70 percent with paper) |
| Warn #E69F00 | UiKit.cs:25 | request bar under 25 percent, hint ring |
| Thatch | `Palette.Thatch` | level bar, drive bar, achievement bars, shop price bar |
| **Money** | gold **#F2C14E** with outline #3A2A10, only on the floating "+N"; the ledger's coin is plain ink 26 bold; prices in bookmarks are plain ink text | TownHud.cs:93-95 | there is no single "money colour" |
| **"Can afford"** | no colour: an enabled bookmark (paper, ink) says "Buy"; the shop never turns green | Shop.cs:279, 288 | |
| **"Cannot afford"** | disabled style (PaperDark, faded border, ink 25 percent toward paper), text "Need n more" and a Thatch bar 4 px | Shop.cs:173-174, 242-250 | |
| **Locked** | disabled style plus whole bookmark `Modulate` alpha 0.75, 13 px, one folded line "Name: level 9" | Shop.cs:272-292 | same look as "done" ("Name, level 5: done") |

Colours written as literals outside `UiKit`: `#F2C14E`, `#3A2A10` (TownHud.cs:93, Choice.cs:252). Names for them do not exist.

## 5. Keys shown in the interface

All through `GameInput.Bound` / `Tag` (GameInput.cs:85-98), reprinted when `GameInput.Version` changes. Defaults (GameInput.cs:20-48):

| Printed where | Key |
| --- | --- |
| Bookmarks (`[key]`) | 1 Villager, 2 Pace, 3 Load, 4 Pave (printed "4 or P"), 5 Extend (printed 5; E only in "E to stop"), 6 Tools, 7 8 9 value tracks (Water, Grain, Flour), 0 bell track, F G H building offers in shown order |
| Bell | Space |
| Next age | N |
| Achievements button and page close | Tab |
| Pause button, "Keep playing", pave banner "Esc to stop" | Esc |
| Home | Home |
| Boon halves | Z (left), X (right) |
| Pave mode | P; Extend mode: E |
| Not printed anywhere in the HUD | A W S D / arrows (move), + - (zoom), Shift (hold to pave several: only in the Pave banner's words), mouse wheel, drag |
| Printed only in settings | the whole list of 27 actions |

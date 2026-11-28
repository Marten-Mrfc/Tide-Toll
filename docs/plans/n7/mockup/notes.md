# Trades mock-up: notes (n7 step 2, 27 November)

The board for `docs/plans/n7/trades-design.md`, before anything is built. Two pictures:

- `trades-now-and-planned.png`: the interface. Left half Now, right half Planned, for: the left column with nothing selected, a villager selected, the shop's People group, at 1920 x 1080 and at 1280 x 720 (true pixels, crops); then one whole 1920 x 1080 planned screen (also on its own: `trades-planned-whole-1920x1080.png`); then a panel with the stepper's states, a trade cap's states, the tooltips, and the two cards on the 272 rail (1280 x 720 at interface size 150 percent).
- `hats-now-and-planned.png`: four villagers (Hand, water carrier, reaper, miller) now and in the trades' headwear, at the play view (true pixels and 3 x) and close up.

`measures.json` holds every width measured while drawing (text width and the room it has, in pixels, per canvas).

## What is real and what is drawn

| Part | Real or drawn |
| --- | --- |
| Every "Now" crop, and the map, ledger, shop, bell and navigation under every "Planned" crop | **Real**: frames of the game taken by `tools/people-shots.sh` (`game/tests/PeopleShots.cs`) at 1920 x 1080 and at 1280 x 720, Mobile renderer under xvfb, this branch. One line was added to the test: it now also saves `people-none.png`, the same moment with the card closed (nothing selected) |
| The Trades card, the planned villager card, the Train row, the stepper and cap states, the tooltips | **Drawn** (`tools/mockup/trades_board.py`, `trades_board_main.py`) with the game's own fonts from `assets/fonts` (TideLegible Regular and Bold, Alegreya), the colours, radii, borders and sizes of `UiKit`, at the zone `HudZones.Person`, scaled as the game scales (`U(u) = round(u * s)`, `Type(size) = round(size * s)`) |
| The shop below the Train row | **Real pixels moved**: the rows from "Building" to the foot are the frame's own, moved down one row pitch; the band they leave is the card's own blank row (one pixel row inside the gap above the heading) repeated |
| Icons in the drawn parts | **Real files**: `assets/icons/trade-{hand,water,grain,flour}.svg`, `shop-train.svg` (new, `assets/src/2d/icons-trades.py`) and the game's `ui-coin.svg`, rasterised with cairosvg at their on-screen size |
| The figures in `hats-now-and-planned.png` | **Real**: rendered in the game (`game/tests/AssetAudit.Trades.cs`, run as `asset-audit.tscn -- --trades --no-town`) on the game's ground with its sun, camera tilt and ink pass. The planned heads are new models (below); the game itself does not load them yet: the test swaps the head mesh of `TownVillagers.Figure` and adds the apron |

Under the planned villager card the map is taken from `people-none.png` (the same moment, 30 frames later, card closed), so today's shorter card does not show through the planned card's 96 percent paper; the selection ring on the map is from the card frame.

Differences a drawing cannot avoid: PIL's text rasteriser is not Godot's (same font files and sizes; hinting and subpixel placement differ slightly); the planned card's alpha (96 percent paper) is composited over the frame's map, so what shows through is right.

## Numbers taken from the code

- Scale: `UiKit.ScaleFor(h) = clamp(h / 1080, 0.78, 2.5)`: 1.00 at 1080, **0.78 at 720**; interface sizes multiply it (80: 0.624, 150: 1.17 at 720, 1.5 at 1080).
- Rail (`HudZones.RailFor`): 320 when the sheet canvas is 1600 wide or more, else 272. 1280 x 720 at 100 is 1641 wide: rail 320 units = **250 px**.
- Type (`UiKit.Styles.cs`): caption 15, body 17, title 21, large 30 (at 0.78: 12, 13, 16, 23 px).
- Colours: Paper #F2EAD8, Ink #2B2620, Wheat = Thatch #C9A55A; Muted = Ink to Paper 0.4; MutedNumber 0.3; DisabledInk 0.55; PaperLight = Paper to white 0.45; PaperDarker = Paper to Ink 0.12; the pill's wash Wheat to Paper 0.7, fading over 8.
- Card (`CardBox`): paper at 96 percent, ink border U(2), corner U(4), padding 12.
- Zones (`HudZones`): margin 16, gap 8, ledger 336 high, so the Person zone starts at y 360 (1080: 360 px; 720: 281 px). Today's `PersonH` is 124 (105 when the leaning line is hidden, as in both frames).
- The villager's card today (`TownHud.People.cs`): name body bold at y 8 (24 high), home caption muted at y 33, now at 52, counts at 71 (tabular, muted, coin 14), the cross at the top right in title size, muted. The planned card keeps all of these and adds the Trade line at y 104.
- Shop rows (`ShopRow.cs`, measured on the frames to confirm): tall row 48 + 4 (pitch 52; 40 px at 720 because U(48) + U(4) = 37 + 3), icon 24 at (8, 4) of the row, name at 38, row inset 8 from the card, pill 104 x 32 (81 x 25 at 720), 6 from the row's right, corner 16, coin 16 at 9. The Train row is inserted at y 244 (190 at 720): the end of the Handcart row.
- Not-yet pill: paper, wash from the left, Muted border 1; price right-aligned, tabular, MutedNumber.

## What I drew, beyond the design's words (the design does not give these numbers)

- **The Trades card** (156 high, as the design): heading 28 at y 8 ("Trades", title, Alegreya; at the right "Keep filled" caption muted and an On pill 40 x 22, ink fill, paper bold caption); three lines of 28 from y 40 (icon 20 at 12, name body at 40, status caption MutedNumber 8 after the name, stepper at the right); a hairline at y 126 (ink 16 percent, as the shop's); the foot at y 128, 20 high, caption MutedNumber.
- **The stepper**: two pills 32 x 24 (fully round, as the shop's pills), a count cell 28 wide (body bold, tabular) between them, 2 apart. States as the shop's pills: rest paper with a Muted border 1; hover PaperLight, Ink border 1; pressed PaperDarker, content 1 down; disabled paper, ink-30-percent border 1, glyph DisabledInk. The minus and plus are drawn bars (10 long, 2 thick), not font glyphs: the font's minus sits off centre.
- **The Trade line** of the villager card: "Trade" caption muted, then four caps 40 x 32 (corner 4), 4 apart, from x 60: current = wheat fill, no border; others paper with a Muted border 1; hover PaperLight, Ink border 1; the liked one a dot (radius 3, Ink) in its top right corner. Card height 152 as the design.
- **The Train row** as a tall row in the "not yet" state (price 640 drawn, 55 percent of the way), icon `trade-grain` (the suggested trade's), no key cap (no key).
- **Tooltips**: the trade line's tooltip drawn twice: as written (title "Reapers" and the design's sentence) and, as a best fix, in the shop tip's form (a short sentence, then fact lines Members / Bonus / Trained), since the design asks for "the tooltip family". A cap's tooltip as a one-line tip.

## What did not fit when drawn at true size, and what I drew instead

Measured with the game's fonts (pixels; "room" is what the line leaves before the next element):

1. **"Water carriers" with a status does not fit its trade line on any canvas.** At 1920 x 1080: name 105 + 8 + "2 waiting" 60 = 173 against 164 of room; at 1280 x 720: 134 against 128. ("Reapers 1 helping", the design's example, fits: 128 of 164 and 100 of 128.) On the **272 rail** (interface 150 percent) no status fits at all: "Reapers 1 helping" 152 against 137 at 1280 x 720 / 150, and 193 against 174 at 1920 x 1080 / 150. Drawn: the status is dropped whole when it does not fit (the layout sheet's rule for optional captions) and is in the line's tooltip. The 272 panel on the board shows it. Possible design fix (not drawn): "Carriers" as the short name on the card (127 of 164 with "2 waiting").
2. **The Train row's name for the water carriers does not fit**: "Train the water carriers 10" is 194 against 150 at 1920 x 1080 (150 against 117 at 720). "Train the reapers 3" fits (127 + step). Best fix: "Train carriers 10" / "Train reapers 10" (120 of 150; 93 of 117). On the 272 rail even "Train reapers 10" is 3 to 5 px over (142 of 139 at 720 / 150; 182 of 177 at 1080 / 150): the step number is then dropped whole (shop sheet rule), or the 272 rail's names go to caption as the shop sheet already allows. Line 2 "Water carriers work and sell 10% better" is 260 of 266 at 1080 and 208 of 208 at 720 (on the edge), and over on the 272 rail (381 of 327); "Carriers work and sell 10% better" fits on the 320 rail and is 8 px over at 720 / 150 (264 of 256).
3. **The villager card's line 2 is at its limit**: "Lives by the well lane · likes the sickle" is 248 of 296 (198 of 232 at 720), but the longest pair, "Lives near the market · likes the millstone", is 273 of 296 on the 320 rail and over on the 272 rail (401 of 372 at 1080 / 150, 328 of 290 at 720 / 150). Drawn fix: when it does not fit, the home words lose "Lives": "Near the market · likes the millstone" (349 of 372; 285 of 290). The 272 panel shows it. ("likes the millstone" and "likes the sickle" are my words for Flour and Grain; the design gives only the sickle.)
4. **The stepper is small**: the line is 28 high, so the pills are 32 x 24 units (the sheet's buttons inside cards are 32 high; 32 would not fit a 28 line, and 32-unit lines would make the card 168, over the 164 the column has at 150 percent). In pixels: 32 x 24 at 1080, **25 x 19 at 1280 x 720**, **20 x 15 at 1280 x 720 with interface 80** (too small to hit comfortably). Recommendation for the build (not drawable): the hit area of each pill takes the whole line height and 4 units more each side (40 x 28 units: 25 x 17 px at 720 / 80). The trade caps are 40 x 32 (31 x 25 at 720; 25 x 20 at 720 / 80).
5. **The card heights fit**: Trades 156 and villager 152 units under the ledger; the column below the ledger has 648 units at 1080, 491 at 720, 183 at 720 / 150, 288 at 1080 / 150 (to the bell's gap). The Train row makes the shop 52 taller: its bottom moves from 678 to 730 (max 976) at 1080 and from 523 to 563 (max 639) at 720: no compact rows needed in this state.
6. The design's Trades card in the frame's state shows **three trades** because this town has a mill. Its whole column stays calm: one card, no wheat fills (the only filled thing is the On pill).

## The headwear (`hats-now-and-planned.png`)

New models in `assets/src/models/villager-trades.py` (same face, same 1.4 x drawn size and frame as `villager.py`'s heads; front +Z; node "head"):

| File | What | Triangles |
| --- | --- | --- |
| `villager-head-kerchief.glb` | water carrier: River kerchief tight over the crown, Deep water hem, knot and two tails at the back | 172 |
| `villager-head-strawhat.glb` | reaper: straw hat, brim 0.70 drawn (the Hand's hat today: 0.48), low cone crown, Mud band | 234 |
| `villager-head-cap.glb` | miller: flat cap, a Paper disc 0.50 across with a short Limewash peak, a Packed earth band under it | 196 |
| `villager-apron.glb` (node "apron", body frame) | miller's apron: Limewash bib and skirt, three Paper flour patches 1 cm proud, Packed earth tie | 96 |

Seen from the game's camera at the play view: the blue kerchief and the wide straw hat read at once by their headwear alone (the "empty-handed" strip); the miller's white cap reads as a white disc, with the apron showing below it.

Problems found by looking:

- **Today's Hands already wear a straw hat and a pale hood** (`TownVillagers.HeadOf(i) = i % 3`: hair, hood, straw hat). The design says the Hand stays "bare head or hood, as now". At the play view the Hand's straw hat (brim 0.48) is close to the reaper's (0.70), and the pale hood is close to the miller's white cap: both are a pale blob from above (see the Now strip, where the "miller" wears a hood). Drawn: the planned Hand is bare-headed. Recommendation: Hands get hair or the hood only, and the hood a colour no trade uses (Packed earth or the tunic's colour), or the miller's cap gets a coloured band that shows from above.
- The cap's band is hidden under the crown from above; a small Mud dot (the hair) shows through the middle of the cap at close range and reads as the cap's button; kept.
- The ink ceiling of `game-ink` (a group of six villagers under 0.42) was not run on the new heads.

## Not done

- The hover tag "Joss, reaper" and the one-time hint were not drawn.
- `game-ink` was not run on the new heads; `./check` was not run (no shipping code changed: only a test file and one test line).
- The ledger rows were added by `tools/merge-ledger-rows.py` on this branch (see the report).

## Round 2 (after `trades-review.md` findings 2, 6, 7, 8, 13, 25; the lead's decisions)

Picture: `hats-round2.png`: (1) Helper, carrier, reaper, miller now beside planned at the play view, 3 x and true size, and the three Helpers; (2) close up; (3) every headwear on every tunic colour; (4) the trades under their own load and under another's; (5) the icons at 24 and 19 px on paper and on wheat, and at 3 x. All figures are real frames (`game/tests/AssetAudit.Trades.cs`, `asset-audit.tscn -- --trades --no-town`); the tunic colour is set on the body's MultiMesh instance exactly as the game dyes it.

**What changed** (`assets/src/models/villager-trades.py`, `assets/src/2d/icons-trades.py`):

- **Helper** (was Hand): hair only. New `villager-head-helper-{mud,earth,soot}.glb` (108 triangles each): the hair-only head of `villager.py` in Mud, Packed earth or Soot brick. No hood, no straw hat, no Thatch, Limewash or River on its head.
- **Miller's cap**: the band is 0.40 across against the head's 0.36 (the head plus a tenth; it was 0.50); the crown's top is 2.58, over the face's 2.52, so nothing shows through it; from above a white crown (Paper, 0.33) sits inside a **Brick** ring (the band's top); a Packed earth peak 0.36 wide stands 0.13 out at the front. 196 triangles.
- **Apron**: a Brick tie 9 cm deep round the waist with two ends hanging at the hips; it shows beside a flour sack (section 4). 120 triangles.
- **Kerchief**: a Limewash hem, the cloth's folded front edge, 10 cm wide and 1.5 cm proud, over the front of the crown from ear to ear: from above a pale band across the blue. 192 triangles.
- **Straw hat**: the band is Brick (was Mud), 9 cm deep: a red ring on the straw from above.
- **Icons**: `trade-flour` has a Limewash crown, a Brick band and a Packed earth peak (its own colour on paper); `trade-hand` is renamed `trade-helper` and drawn in muted ink only (lines, hair and shoulders #7B746A = `UiKit.Muted`); ledger rows added, the old row pruned. At 19 px on paper and on wheat the four read apart: grey head (Helper), blue cap (carrier), wide yellow hat (reaper), white-over-red cap (miller). The carrier's and the Helper's are both small round heads: they differ by colour only (blue against grey).
- Note for the build: Brick on the heads and the apron is safe only because heads and props are not drawn with the tunic-dye shader (only the body's MultiMesh is, `TownVillagers.DyedBody`); the apron must stay a separate mesh too.

**Ink** (`game-ink`'s measure, `--ink-check --ink-variants=cottage-a0,villagers-group,villagers-trades-group`; the new subject is `villagers-trades-group` in `AssetAudit.Trades.cs`, hooked into `InkSubject`): six villagers in the same places, turns and loads as `villagers-group`, in mixed planned headwear (two Helpers, a carrier, two reapers, a miller with apron) and the proposed tunics: **0.349**. Today's group in the same run: 0.357. Ceiling 0.42. Took 37 s, no lock needed.

A finding on the check itself: measured as the **first** subject of a run (`--ink-check --only=villagers-group`, or first in `--ink-variants`), today's group reads **0.533** and fails its ceiling; second in the run it reads 0.357. The warm-up in `InkCheck` is not enough for the first subject (its own comment says the first subject once read 0.73). `./check` is not affected because cottages come first, but anyone running one subject alone gets a false failure.

**Tunics** (section 3; today's list `TownVillagers.Clothes` = Brick, River, Cast-iron green, Thatch, Soot brick):

- **River must go**: under the kerchief it makes one blue column; only the hem separates head from body.
- **Thatch must go**: under the straw hat, hat and body are one yellow mass; the Brick band and the outline still mark the hat, but the figure reads as one blob. It is also the miller's and the field's colour.
- **Packed earth** replaces one of them: it reads under every headwear.
- **Slate roof** (the review's second proposal) is **not good under the kerchief**: blue-grey under blue, nearly as weak as River. It is fine under the hat and the cap. I recommend **Mud** in its place, which reads under all three hats; its only weak pairing is a Helper with Mud hair, and a Helper is meant to look plain. If the lead prefers Slate roof, the carriers in it rely on the hem.
- Recommended list: **Brick, Packed earth, Cast-iron green, Mud, Soot brick**. I did not change the game's list.

**Under a load** (section 4): the carrier with flour, the reaper with pails and the miller with a grain basket all still read by the hat. The reaper's hat covers most of its own load from above. The miller carrying flour reads by the cap's red ring and the apron's red tie, not by white: the sack, cap and apron are all pale.

**Still weak**:

- The kerchief's hem sits low over the brow, so at close range it reads a little like a visor.
- The miller's peak covers the upper face from the game's tilt. At the play view it is a dark line under the cap.
- The Soot brick Helper hair reads reddish-brown on a Brick tunic; still no trade colour.
- At the play view the carrier and a Helper differ by the head's colour alone (blue against dark hair, both small round heads); the hem is 1 to 2 px there.

## Trades 2 (n7 step 3: shepherd, weaver, porter; 28 November, round 3 after the lead's choice)

Picture: `hats-trades2.png`, all real frames: `game/tests/AssetAudit.Trades2.cs`, run as `asset-audit.tscn -- --trades2 --no-town --out-dir=DIR` (xvfb, Mobile renderer, 1920 x 1080 play view), board by `tools/mockup/hats_trades2.py SHOTS OUT "ink note"` (each frame cropped where it differs from the empty ground frame). Parts: (a) Helper, carrier, reaper, miller, shepherd, weaver, porter at 3 x and true size, empty-handed and with their own good (wool, cloth, flour); (a') the dropped candidates the same way; (a'') carriers and porters in turn, turned four ways; (b) the chosen headwear, the shepherd's two variants and the carrier's kerchief on all five tunic colours of `TownVillagers.Clothes`; (c) a second-age street of twenty (Helpers 3, carriers 2, reapers 4, millers 2, shepherds 3, weavers 3, porters 3; a third carrying, random turns) at true size and 2 x, beside the same street with the first three trades only; (d) the seven trade icons at 24 and 19 px on paper and at 3 x.

**The chosen set** (`assets/src/models/villager-trades2.py`, `assets/src/2d/icons-trades2.py`; ledger rows for every file, `check-ledger.py` passes):

- **Shepherd: `villager-head-felthat.glb`** (210 triangles): a felt hat in one flat Slate roof tone, round crown, brim 0.56 against the straw hat's 0.70, no band. From above a grey disc, smaller than the reaper's yellow one. The variant with a Mud band (`villager-head-felthat-band`) looks the same from above (the band is hidden under the brim at the tilt): no band is simpler. The variant with the fleece cape as a body piece (`villager-cape.glb`) reads as a white collar under the hat, not as a second white head, but it adds white near the miller's and nothing the hat does not already carry: left out.
- **Weaver: `villager-head-stocking.glb`** (184): a Meadow stocking cap, the tail lying back over the crown, a Limewash tassel; at the tilt a green point over the head. Unchanged (the lead: right as it is).
- **Porter: `villager-head-fantail.glb`** (136): a Deep water fantail hood with a short peak and a fan 0.8 across at its Packed earth hem, sloping down behind at about 35 degrees. From the front a dark blue hood with a dark bar behind it; turned, a fan.
- **Icons**: `trade-wool` (a Slate roof felt hat, brim 32 of 48 against the reaper's 42), `trade-cloth` (green stocking cap), `trade-boat` (a Deep water fantail hood with its peak, Packed earth shoulders). At 19 px the seven read apart: grey head (Helper), all-blue figure (carrier), wide yellow hat, white over red cap, small grey hat on brown shoulders (shepherd), green cap, dark blue hood on brown shoulders (porter).

**Why the first choices went:**

- **The fleece hood and mantle** (`villager-head-fleece`, shepherd round 1-2): white is the miller's. At true size the shepherd and the miller were told apart only on a second look, and under a load (a wool bale against a flour sack, both pale) worse.
- **The large Slate fantail** (`villager-head-fantail-slate`, porter round 2): 1.0 across and flat, it stood over the head as a funnel from the front, and Slate is now the shepherd's.
- Earlier dropped: the Deep water cloth roll (`villager-head-turban`, a dark round blue head, the kerchief's shape), the Slate head pad (`villager-head-headpad`, too small, a grey hat with a dark middle). Kept for the board, rows marked dropped.

**Ink** (`game-ink`'s measure, `--ink-check --ink-variants=villagers-group,villagers-trades2-group`): a group of six in the six chosen headwears (carrier, reaper, miller with apron, shepherd, weaver, porter; same places, turns and loads as `villagers-group`): **0.360**, ceiling 0.42 (today's group in the same run 0.349).

**Tunics**: no change. No chosen headwear uses a tunic colour (Slate roof, Meadow, Deep water). Weakest pairings: the weaver's Meadow on Cast-iron green (the cap is much lighter; it reads); the porter's Deep water on Soot brick or Mud (dark on dark, the outline carries it).

**Still hard:**

- **Carrier and porter.** River and Deep water are both mid-to-dark blue at the play view: by colour alone they are close. They are told apart by shape: the porter's fan shows behind or beside the head whenever he is turned, and from the front as a dark bar behind the hood; the carrier's kerchief is round with its pale hem in front. From behind, at true size, the two are blue heads of nearly one size, and only the fan's straight hem tells them. I kept Deep water: the only darker tone that is neither a tunic's nor a Helper's hair is Ink, and an Ink hood would merge with the outline into a black head. If the lead wants more, the next steps are a Limewash hem on the fan (a pale bar behind the head) or a darker kerchief hem; neither tried.
- **Shepherd and reaper** share the brimmed-hat shape; they differ in colour (grey against yellow with a red ring) and size (0.56 against 0.70). In the crowd this reads at once.
- Icons: `trade-wool` and `trade-grain` are the same kind of picture (a brimmed hat) in two colours and sizes; `trade-boat` and `trade-water` differ by the shoulders' colour and the hood's shape more than by the blue.

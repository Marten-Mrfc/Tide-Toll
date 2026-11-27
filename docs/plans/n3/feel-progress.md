# Feel (night 3, last round part A): progress

Checkout: `/home/user/tide-toll/build/wt/n3-ui` (branch `wt/n3-ui`). Strips: `/home/user/tide-toll/build/n3/feel/`.

## Done

- **The system** `game/scripts/town/TownFeel.cs` (pool, drawing) and `TownFeel.Spawn.cs` (which event starts which reward, where, its sound). One pool of at most 200 pieces for all rewards together, drawn by four MultiMeshes (coin disc, puff, ring, grit stone); pieces packed each frame, nothing allocated per frame, nothing saved, the simulation only read. Every piece lives under a second of real time whatever the speed. Above 140 live pieces the frequent rewards (coins, trail dust, grit, ford rings) are skipped; above 200 everything is skipped (never queued). Node `Feel` under `MapOrigin`, added by one line in `Main.cs` (after the audio).
- **Sounds:** `assets/src/audio/feel-sounds.py` makes coin-hop (4), dust-puff (3), grit (3), ripple (3), bell-creak (2), gift-ring (2), levelled against the coin; ledger rows merged; listed in `town-sounds.json` (via `town-zz-sounds-json.py`) and in `SoundCues`' fallback table with their least gaps; `TownAudio.Feel(id)` plays them on the world player. Trail dust uses the puff's files at -4 dB with a 1.4 s least gap (`dust-puff-trail`).
- **The seal's sound follows its message:** `RiverAudio` no longer plays the seal on the `Seal` event; `TownAudio.Large("seal-granted")` (asked for by the banner) hands it to `RiverAudio.Large`, which plays it. If no banner asks for it within `RiverAudio.SealWait` (8 s) it plays then. `RiverTest`: the check "the seal sound" now waits up to 1200 frames for it (was: at once when the seal stone showed) - the equal test for "the sound follows the banner".
- **Strips:** `game/tests/FeelStrips.cs`, `feel-strips.tscn`, `tools/feel-strips.sh`, `tools/feel-sheet.py`.

## For the lead

1. Interface side of "the seal's sound follows its message" (`TownHud.River.cs`, `TownHud.Messages.cs`, not mine): in `TownHud.River.cs` line 182 pass `"seal-granted"` as the last argument of `Large("The borough seal", "The town is a borough. The second age is complete.", PrioMoment, BannerLongStay, "seal-granted")`, and add `or "seal-granted"` to `TownHud.IsLargeSound`. Until then the seal sounds 8 s after the event (RiverAudio's fallback).

## Tests so far

- game-perf before (02:47 UTC): 1x mean 0.820 ms, 538 B/frame; 8x mean 1.315 ms, 740 B/frame; river 1x 0.688 ms, 1225 B/frame; river 8x 1.834 ms, 1579 B/frame. PERF PASS.
- game-perf after (02:51, first version): 1x 0.860 ms, 538 B/frame; 8x 0.992 ms, 738 B/frame; river 1x 0.740 ms, 1211 B/frame; river 8x 0.906 ms, 1578 B/frame. PERF PASS.
- game-smoke, game-river (with the seal check waiting), game-town, game-save: PASS (02:52 to 03:05).
- First strip run (03:05): the view was the book's page view, not the play zoom (staging fault: `Rig.PlaySize` is the whole page); "most live 200, skipped 169" during the bot's long run (a burst of events in one frame). Fixed: the strips take the size the view has when it follows the town; a frame starts at most 40 new pieces (`TownFeel.FrameBudget`).
- Second and third strip runs (03:10, 03:15): nothing of the rewards moved and the puffs never showed: the strips' clock was set back to 0 before the nodes' frame ran (a test's frame wait resumes before the nodes' own frame). Fixed with a one-frame step (`TownFeel.StripNext`), as the message strips do. The pieces are now opaque flat palette colours with the ink edge (they leave by shrinking or sinking, never by fading), the coin 1.4 m and lying nearly flat (on edge it read as an ink blot from above).
- game-playthrough (03:13): PLAY PASS charter=33.9min buys=201 paves=24 extends=14 bells=76 clicks=204 keys=102.
- game-perf (03:21, current code): 1x mean 0.858 ms, 541 B/frame; 8x 1.112 ms, 738 B/frame; river 1x 1.055 ms, 1211 B/frame; river 8x 1.579 ms, 1579 B/frame. PERF PASS.

## The strips, read (run of 03:20; sheets in `build/n3/feel/`, 960 x 540 real pixels at the play zoom)

- feel-coin: a pale gold disc at the market's door path, rocks over (edge-on in frames 4 and 10), shrinks in 11, gone in 12. Readable (about 12 px), does not cover the building. Fault: the hop's height hardly shows from the steep view; it reads as a flip in place.
- feel-trail-dust: small pale puffs with an ink edge at the feet of the one walker on a new trail, in 9 of 12 frames; light, never over the villager.
- feel-extend: a cluster of pale ink-edged puffs at the cottage door, spreading and shrinking away by frame 12; the cottage stays clear.
- feel-flourish: a paper ring at the new villager's cottage door and sparks inside it (frames 1 to 6), gone by 7. Fault: the sparks read as dark specks (the ink edge dominates spheres this small) rather than gold.
- feel-bell: three paper rings spread round the bell post, the brightest in frames 8 and 11 lying over the post's base; the bell's own swing is not in the strip (the event was staged, the town was not really rung).
- feel-request: (sheet taken; the same flourish at the market).
- feel-pave (03:20 run): FAULT, fixed at 03:38: the grit sat in one clump at one end of the road (the code walked the grid's distances on the curve's points); now placed by `Road.At` along the curve. Retake started 03:40 into `build/n3/feel/retake/`.
- feel-gift (03:20 run): FAULT of the staging: the town's level 6 gift has no plot, so nothing showed; the strip now picks the latest level whose gift has a plot. In the retake.
- feel-landing (taken before the last fixes, landing code unchanged): a thin paper ring widening and sinking at the landing's edge on the bank, not on the water (the strip staged the sale at the landing's foot); faint at this zoom. A boat mooring rings at the boat itself (on the water); not staged.
- Retake (03:44, `build/n3/feel/retake/`): feel-pave: a line of small dark stones jumps along the whole road from end to end (frames 1 to 11), gone at 12; the road and the walkers stay seen. feel-gift: a ring widens inside the new market plot's frame (frames 1 to 8) and sinks away (9); it reads as ink until it is largest (frame 8 pale gold). Fault: both read darker than meant (the ink edge on thin pieces).
- feel-ford and a fresh feel-landing: the retake was still running at 03:46 (river part); not read.

## Left / next

- Sparks, grit and thin rings read as ink at the play zoom: thicker rings (inner radius 0.75) and larger sparks would let the colour show.
- The coin's hop hardly shows from the steep view: more travel up the screen.
- The landing's sale ring belongs on the water beside the landing, not on the bank.
- The whole ./check was started at 03:27 and was still running at 03:46 (see the final answer).

## New sounds

| File | Length | What it is for | When it plays |
| --- | --- | --- | --- |
| `coin-hop-01..04.wav` | 0.40 s | a small coin flicked up and landing on others (pitch varied 3 %) | as a delivery's coin lands; least gap 0.3 s |
| `dust-puff-01..03.wav` | 0.35 s | a breath of dry earth with a few grains | a cottage extended (with its saw and hammer cue); trail dust at -4 dB, least gap 1.4 s |
| `grit-01..03.wav` | 0.55 s | a handful of gravel grains scattering | a road paved (with the pave cue); least gap 0.5 s |
| `ripple-01..03.wav` | 0.50 s | a small lap of water and a bubble | a boat moors or casts off, goods sold at the landing (least gap 0.35 s); a ford stone crossed (`ripple-ford`, the same files, least gap 0.6 s) |
| `bell-creak-01..02.wav` | 0.45 s | the bell's yoke turning in its wooden frame | the bell rung (with the bell), each peal of the new age; least gap 0.6 s |
| `gift-ring-01..02.wav` | 0.90 s | a soft struck chime (A5, D6) | a level's gift arriving; a flourish (new villager, hand, tools, building, louder bell); least gap 0.9 s (its whole length: never cuts its own tail) |

All levelled against the coin (-24.2 LUFS): coin-hop -3 dB, dust-puff -4, grit -2, ripple -3, bell-creak -3, gift-ring -1 (measured within 0.1 dB, `town-sounds.json`). Generator `assets/src/audio/feel-sounds.py`, ledger rows in `assets/LEDGER.csv`.

## Rewards

| Action | What is seen | How long | Its sound |
| --- | --- | --- | --- |
| A delivery | a Thatch gold coin (1.6 m) hops from the door, about 1.5 m up the screen in the first 0.3 s, rocks over and falls back, shrinking away (the coin number is the interface's, as before); at most one every 0.2 s | 0.95 s | the coin clink (as before) and coin-hop as it lands |
| A purchase with a place | a broad light-Thatch band ring (inner radius 0.75) widening at the door and five light-Thatch sparks (0.9 m) rising: a new villager at its cottage door, a hand at its workplace, Tools at every open workplace, a building when it opens, the louder bell at the bell green; nothing for the others | 0.7 s | the purchase cue (as before) and gift-ring |
| A cottage extended | eight Limewash puffs in a half fan against the cottage's front, spreading away from the path, in two bursts | 0.6 to 0.75 s | cottage-extended (saw, two hammer taps) and dust-puff |
| A road paved | 12 earth-coloured grit stones (Packed and Trail earth, 0.25 m, unshaded) in a wave from one end of the road to the other, at most four up at once | 0.98 s | the pave cue and grit |
| Dust on a new trail | a small Trail-earth (light) puff at a walker's feet on a trail not yet worn in, chance 0.4 x (1 - wear) every 0.2 s per walker, none once worn in or paved | 0.6 s | dust-puff, quieter, at most every 1.4 s |
| The landing | two water rings (Paper, then Limewash-river) round the berth, wider than a boat (7 m across at their widest), where a boat moors or casts off and where goods are sold (at the berth nearest the landing; at most every 0.3 s) | 0.9 s (+0.25) | ripple |
| The ford | a broad ring at each stepping stone (cell) a wader crosses | 0.7 s | ripple-ford (least gap 0.6 s) |
| The bell | the bell swings in its frame (TownBuildings, as before); three Limewash rings (a fifth smaller than before) spread on the green; each of the four peals of the new age the same | 0.9 s + 0.44 | the bell (as before) and bell-creak |
| A level's gift | a light-Thatch band ring widening on each new plot of the level (up to six), at its widest a fifth smaller than the plot | 0.9 s | gift-ring (the level's own sound plays with its banner) |
| A request met | the flourish at the asker's building, an open one (the miller: the mill; the innkeeper and the baker: the market) | 0.7 s | request-met with its banner (as before) |

## After the review (the feel fixer, 03:46 to 04:25 UTC)

Code: `TownFeel.cs`, `TownFeel.Spawn.cs`, `SoundCues.cs`, `game/tests/FeelStrips.cs` (checkout `build/wt/n3-ui`). Strips: `build/n3/feel/fix/` (town) and `build/n3/feel/fix-river/` (landing, ford); frames in `frames/`, sheets `feel-NAME.png` once a run ends.

- **The root of "ink, not gold" and "cream, not gold":** the pieces' instance colours were palette sRGB values read as linear by the unshaded material, so every piece showed far paler than its palette colour (Thatch as cream), and the thin ones were then all ink edge. Now converted (`SrgbToLinear`) in `Draw`.
- MUST 1 landing: fixed. A sale's rings go on the water at the berth nearest the landing's point (a boat's mooring rings are at the boat, the same place). A ring smaller than a moored boat was hidden under it (seen in the first retake), so the two rings (Paper, then Limewash-river) grow round the boat to 7 m across, band about 0.5 m. Seen: two clear rings round the moored boat, frames 2 to 9.
- MUST 2 pave: fixed in code: 12 stones, Packed/Trail earth, 0.25 m, unshaded with the soft ink, a wave along the road (each 0.26 s, next 0.065 s later: at most four up). Strip: see the final answer.
- MUST 3 thin rings and sparks: fixed. A second ring mesh (`Bands`, inner radius 0.75) for flourish, gift, ford; light Thatch from frame 1; sparks 0.9 m; the small pieces (puffs, grit, bands) take the soft ink as trees do (stencil 1, ModelLib's way). Seen (flourish): a gold band ring and five gold sparks at the cottage door, frames 1 to 6.
- MUST 4 request: fixed both ways. The code puts it at an open market (or mill) door, not the plan's first plot of the kind; the strip now runs the town until a market is open (at level 6 the default town's market was still "to buy": that was the reviewer's bare plot).
- MUST 5 bell: the strip now really rings the town's bell (`Town.RingBell`, the town running) so the swing (TownBuildings: 0.45 rad, already larger than the reviewer's 3 degrees) is in the frames; rings Limewash and a fifth smaller. Not mine to change the swing (TownBuildings.cs).
- MUST 6 coin: fixed. Thatch gold, 1.6 m, carried up the screen (2 x its height: about 3 m at the top of the hop), least gap 0.2 s. Seen: a gold disc high above the market stall in frames 2 to 4.
- SHOULD trail dust: colour Trail earth light; size kept (0.8 to 1.1 m): the reviewer's 0.5 m would make it smaller than the 5 px specks it called "close to unseen".
- SHOULD extend: fixed. Limewash, a half fan against the cottage front facing away from the path, 0.45 to 0.6 s. Seen: puffs at the cottage's side, path clear.
- SHOULD bell rings: Limewash, a fifth smaller. SHOULD gift ring: at its widest a fifth smaller than the plot. SHOULD sounds: gift-ring least gap 0.9 s; the ford's stones use `ripple-ford` (same files, gap 0.6 s).
- SHOULD pool pressure: not taken in a thirty-villager sheet (no time). By the numbers: coins at most one per 0.2 s with a 0.95 s life, so at most 5 on screen; trail dust only for walkers on trails not worn in.
- Strips read (04:10 to 04:20): coin gold, high over the market stall; flourish gold band ring and gold sparks; extend Limewash puffs against the cottage, path clear; pave a wave of about four small earth stones moving along the road, road and walkers clear; bell really rung: the bell visibly turns in its frame between frames, Limewash rings round the post in frames 1 to 3 (partly under the frame); landing two rings round the moored boat on the water. Ford (first run): rings hid under the stepping stones; made wider (3.4 m) and retaken in `build/n3/feel/fix-ford/`.
- Tests on the final code (04:11 to 04:12 UTC, machine loaded by three strip runs): PLAY PASS charter=33.9min buys=201 paves=24 extends=14 bells=76 clicks=204 keys=102; RIVER PASS (fordcrossings=22 checks=436); SMOKE PASS; TOWN PASS; SAVE PASS; PERF PASS (town 1x 0.794 ms, 8x 0.807 ms; river 1x 0.732 ms, 8x 0.603 ms; 533 / 738 / 1211 / 1578 B/frame). The whole ./check was not run (the lead runs it on a clean copy, lead's note of 03:06).
- Retakes (04:13 to 04:17, `build/n3/feel/fix-request/`, `fix-ford/`): the market is no workplace, so "open" now means `Town.PlotWorking` (code and strip). Request: the gold flourish at the built market stall's door. Gift: the band ring is under the market stall (the strip ran the town until the market was built, so the level's plot is no longer empty; in the game the gift rings on the new empty plot): staging fault, not judged again. Ford: thin white rings round the middle stepping stones, still faint (the stones and a barge on the ford cover most of them).

## Left (for whoever comes next)

- The ford's rings are still faint at the play zoom: a ring beside the wader's feet rather than round the stone, or a broad band.
- Gift strip: stage it before the plot is built (take it right after `LevelReached`).
- Bell rings lie partly under the bell's frame; a ring starting at the frame's edge (radius 1.8 m) would show more.
- Pool pressure not proven in a thirty-villager sheet.

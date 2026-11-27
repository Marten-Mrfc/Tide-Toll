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

## New sounds

| File | Length | What it is for | When it plays |
| --- | --- | --- | --- |
| `coin-hop-01..04.wav` | 0.40 s | a small coin flicked up and landing on others (pitch varied 3 %) | as a delivery's coin lands; least gap 0.3 s |
| `dust-puff-01..03.wav` | 0.35 s | a breath of dry earth with a few grains | a cottage extended (with its saw and hammer cue); trail dust at -4 dB, least gap 1.4 s |
| `grit-01..03.wav` | 0.55 s | a handful of gravel grains scattering | a road paved (with the pave cue); least gap 0.5 s |
| `ripple-01..03.wav` | 0.50 s | a small lap of water and a bubble | a boat moors or casts off, goods sold at the landing, a ford stone crossed; least gap 0.35 s |
| `bell-creak-01..02.wav` | 0.45 s | the bell's yoke turning in its wooden frame | the bell rung (with the bell), each peal of the new age; least gap 0.6 s |
| `gift-ring-01..02.wav` | 0.90 s | a soft struck chime (A5, D6) | a level's gift arriving; a flourish (new villager, hand, tools, building, louder bell); least gap 0.4 s |

All levelled against the coin (-24.2 LUFS): coin-hop -3 dB, dust-puff -4, grit -2, ripple -3, bell-creak -3, gift-ring -1 (measured within 0.1 dB, `town-sounds.json`). Generator `assets/src/audio/feel-sounds.py`, ledger rows in `assets/LEDGER.csv`.

## Rewards

| Action | What is seen | How long | Its sound |
| --- | --- | --- | --- |
| A delivery | a pale gold coin (1.4 m) hops from the door, rocks over and falls back, shrinking away (the coin number is the interface's, as before); at most one every 0.08 s | 0.95 s | the coin clink (as before) and coin-hop as it lands |
| A purchase with a place | a thin paper ring widening at the door and five gold sparks rising: a new villager at its cottage door, a hand at its workplace, Tools at every open workplace, a building when it opens, the louder bell at the bell green; nothing for the others | 0.7 s | the purchase cue (as before) and gift-ring |
| A cottage extended | eight dust puffs spreading from the door in two bursts | 0.85 s | cottage-extended (saw, two hammer taps) and dust-puff |
| A road paved | 24 grit stones jumping along the road from one end to the other | 0.95 s | the pave cue and grit |
| Dust on a new trail | a small puff at a walker's feet on a trail not yet worn in, chance 0.4 x (1 - wear) every 0.2 s per walker, none once worn in or paved | 0.6 s | dust-puff, quieter, at most every 1.4 s |
| The landing | two water rings where a boat moors or casts off; one where goods are sold (at most every 0.3 s) | 0.9 s (+0.25) | ripple |
| The ford | a ring at each stepping stone (cell) a wader crosses | 0.7 s | ripple |
| The bell | the bell swings in its frame (TownBuildings, as before); three rings spread on the green; each of the four peals of the new age the same | 0.9 s + 0.44 | the bell (as before) and bell-creak |
| A level's gift | a gold ring widening on each new plot of the level (up to six) | 0.9 s | gift-ring (the level's own sound plays with its banner) |
| A request met | the flourish at the asker's building (the miller: the mill; the innkeeper and the baker: the market) | 0.7 s | request-met with its banner (as before) |

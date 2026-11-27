# Feel (night 3, last round part A): progress

Checkout: `/home/user/tide-toll/build/wt/n3-ui` (branch `wt/n3-ui`). Strips: `/home/user/tide-toll/build/n3/feel/`.

## Done

- **The system** `game/scripts/town/TownFeel.cs` (pool, drawing) and `TownFeel.Spawn.cs` (which event starts which reward, where, its sound). One pool of at most 200 pieces for all rewards together, drawn by four MultiMeshes (coin disc, puff, ring, grit stone); pieces packed each frame, nothing allocated per frame, nothing saved, the simulation only read. Every piece lives under a second of real time whatever the speed. Above 140 live pieces the frequent rewards (coins, trail dust, grit, ford rings) are skipped; above 200 everything is skipped (never queued). Node `Feel` under `MapOrigin`, added by one line in `Main.cs` (after the audio).
- **Sounds:** `assets/src/audio/feel-sounds.py` makes coin-hop (4), dust-puff (3), grit (3), ripple (3), bell-creak (2), gift-ring (2), levelled against the coin; ledger rows merged; listed in `town-sounds.json` (via `town-zz-sounds-json.py`) and in `SoundCues`' fallback table with their least gaps; `TownAudio.Feel(id)` plays them on the world player. Trail dust uses the puff's files at -4 dB with a 1.4 s least gap (`dust-puff-trail`).
- **The seal's sound follows its message:** `RiverAudio` no longer plays the seal on the `Seal` event; `TownAudio.Large("seal-granted")` (asked for by the banner) hands it to `RiverAudio.Large`, which plays it. If no banner asks for it within `RiverAudio.SealWait` (8 s) it plays then. `RiverTest`: the check "the seal sound" now waits up to 1200 frames for it (was: at once when the seal stone showed) - the equal test for "the sound follows the banner".
- **Strips:** `game/tests/FeelStrips.cs`, `feel-strips.tscn`, `tools/feel-strips.sh`, `tools/feel-sheet.py`.

## For the lead

1. Interface side of "the seal's sound follows its message" (`TownHud.River.cs`, `TownHud.Messages.cs`, not mine): in `TownHud.River.cs` line 182 pass `"seal-granted"` as the last argument of `Large("The borough seal", "The town is a borough. The second age is complete.", PrioMoment, BannerLongStay, "seal-granted")`, and add `or "seal-granted"` to `TownHud.IsLargeSound`. Until then the seal sounds 8 s after the event (RiverAudio's fallback).

# Last look (night 3, part B): progress

Checkout `build/wt/n3-book`. Newest at the bottom.

## Item 1: game-motion's impatient cases (03:10 UTC, done)

- Cause of esc-during-question: the question came in (lettering fading out over 0.16 s), Esc at 0.1 s turned the
  lettering round on the spot with its carried speed: it dipped, was clamped at 0 and jerked.
- Fix (`game/scripts/town/PauseMenu.Tail.cs`, new): a lettering or question fade taken over less than `TailMax`
  (0.12 s) before its end, and turned round, finishes its own eased fade (arrives at rest) and only then fades back on
  the new transition's curve (no carry for that part). With nothing of the question shown, the lettering comes back as
  soon as its fade ends (0.06 to 0.3 s instead of 0.13 to 0.3 s).
- esc-spam (never reached before) then failed: "camera size starts at speed 0.19 of its top speed" and, after the
  first fix, "lettering speed jumps 0.98". Fixes: (a) `TurnGap` 0.1 s: a turn-round (Esc while the book opens or
  closes) asked for in a transition's first 0.1 s is held until then (presses in between count: an even number
  cancels out); (b) Open taken over with the camera at rest waits as from rest (`CamGoing`, PauseMenu.Carry.cs).
- New working switches of game-motion (never used by ./check, the gate is unchanged): `--impatient-only` (the
  impatient cases alone) and `--cpu-info` (a frame over the CPU gate is printed as "MOTION CPU over the gate: ...",
  not fatal). Only `--messages-only` existed before.
- Impatient alone with --cpu-info: MOTION PASS at 60 and 120 fps, every case incl. keep-during-question,
  click-while-closing, esc-spam.

# Last pass (04:17 to 04:36 UTC): progress

Decisions written down, since the owner is asleep.

1. Crowded-road tint: done. TownGround.Upload now tints a road only once Town.CrowdedSeconds(r) has reached
   Town.CrowdHintSeconds (8 s, the same wait as the hint), so two villagers passing on a trail never tint it. The shader's
   wash is multiplicative over the earth (c * muted brick * 1.6) at 0.28 strength (round 6 was a straight mix at 0.8),
   colour #B06649-ish (0.69, 0.40, 0.29), so the trail / worn earth / gravel / cobble pattern shows through.
2. Charter stone: done (by reading, not by picture). In e03 the stone was the small green blob right of the bell frame:
   the override alone did not reach it and it sat half in the ground. Now: at least 4 m (0.55 of the green), its base
   lifted onto the green (model origin is not at its foot; the drop-in animation keeps that lift), the pale material
   set on every surface as well as the override, with a little emission so the grass light cannot turn it green.
3. Tour script: done by reading (no picture runs).
   - b02 / c03: new Tour.ClearCards (picks side 0 if a choice is open, waits until no choice card and no banner);
     b02 also records a Wrong if Pace or Load is not bought; c03 waits for a Grain carrier after clearing.
     Note: picking the level 4 boon at b02 can make c04 wait for the next choice.
   - d06: Tour.Crowded now also needs CrowdedSeconds >= CrowdHintSeconds; just before the shot the tour waits for such a
     road (30 s) and re-frames on it if it changed; a Wrong if none.
   - f01: the wipe starts at the ring itself, so the picture is now taken after the wipe has finished (no mid-wipe).
   - f06: after closing the page, waits (30 s) for the raft between 0.25 and 0.75 of the crossing, someone waiting and
     no banner, then frames the ferry at 30 m (was 40 m, which took in the footbridge's label).
   - g01..: Tour.PageGone (Esc if the age page is open, wait until gone) before every picture in the book section.
   - g03: waits for camera size at target, the play view reached, the cover still, strip shown and full UI faded.
   - TourNotes: f01 and d06 expectations updated to match.
4. If time: done (unverified by picture).
   - "Back to the town" placed on whole pixels (a half-pixel x lost the 1 px left border).
   - Pave/Extend price tip (TipBeside): if level with the pointer it covers a villager, it goes just above, else
     just below the target.

No assertion in any test was changed.

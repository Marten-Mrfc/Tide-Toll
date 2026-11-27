# The message system (round L2 of night 3): progress

Builder of the message system, checkout `build/wt/n3-ui` (branch wt/n3-ui). Spec: `message-system.md` version 2.

## Where things live (game/scripts)

| What | File |
| --- | --- |
| A zone's queue: priority order, stay, crowd stay, cap (drop lowest), wait limit (overflow), eased in/out by `BookMotion`, gap 0.15 s, click away, a message taken back by the choice/page/zoom waits again first | `MessageQueue.cs` |
| An eased on/off value (BookMotion, continuous when turned mid-way) for things that follow the town's state | `MessageFader.cs` |
| The two large queues wired to the interface: Message zone (banners, trouble, one at a time, wait while choice/page/zoom, 12 s then a note), note section (one at a time, two lines, 90 chars), sound on show | `town/TownHud.Messages.cs` (API: `Large`, `Banner`, `Trouble`, `Note(text, strong, prio, sound)`) |
| The line: refusal 2.5 s > mode text > first-minute hint > town hint; card fade 0.2 s; text change is a cross-fade of two labels of one fixed size | `town/TownHud.Line.cs` (`Refuse(text)`, `LineKindNow`) |
| The first minute's four hints, each once ever (`settings.cfg` `[hints] hints_seen`, a bit each); `GameSettings.ShowHintsAgain()` | `town/TownHud.FirstMinute.cs`, `settings/GameSettings.cs` |
| Large sounds on their own player, at least 0.6 s apart (queue of 4) | `town/TownAudio.cs` (`Large(id)`, `LeastLargeGap`) |
| Tests | `game/tests/MotionTest.Messages.cs` (registered in `MotionTest.Run`) |

## Status

### 00:15 UTC

- 1 One message system: done. `MessageQueue` per zone; Message zone: trouble 4, charter/seal/new age 3 (charter and seal 5 s), drive 2, level 1 (3.2 s; 2.2 s with two or more waiting); waits while the choice, a page, a page turn or the zoom holds the zone (one showing is taken back and waits first); 12 s waiting becomes a note; cap 6 (lowest becomes a note). A choice level: the choice opens with "Level N:" in its heading and the gift is a note (no banner). The choice and a banner never show together: a banner showing leaves first (0.22 s), then the choice arrives. Banners and notes are clicked away (trouble only so). Trouble: `TownSaver`/`Main` failures now go to `Hud.Trouble` (brick card, until clicked).
- 2 Notes: done. One at a time, two lines, cut at 90 characters, priorities achievement 3, building/milestone/boon/choice-level gift 2, rest 1; 3.2 s, 2 s with four or more waiting, more than six waiting drops the lowest. Section 2 table: extended, paved, villager, "New to buy", worn in (Pave row pulses), request asked/met/missed, boat request (logged only), choice open, drive met: no note. Boon chosen: a note. Refusals and the scroll hint: the line.
- 3 Goal and strip: the goal's content fades in and out (0.22 s, 8 up); "Done: +N" moss and "Too late" brick stay 1.6 s (from L1). The drive met shows "Done" in moss in the strip for 1.6 s, then the strip fades. "Ring in the next age" fades in and out. NOT done: a second request in the strip in one line, a third waiting (the town and a skipper's request still share the goal section as before).
- 4 Line: done (`TownHud.Line.cs`).
- 5 On the map: coin numbers merge within 0.5 s at one building, at most 8, eased fade, and fade with Pave/Extend (0.15 s) instead of one frame; tags eased (0.15 s), scanned every 0.5 s of real time and in the three frames after a level, a building, a boat arriving or leaving; pointer tip and "Suggested" fade 0.15 s (`TownHud.ModeFade.cs`). The hint ring shows only while the line shows the town's hint.
- 6 Motion: banner, choice, note, line (card and cross-fade), goal content, drive strip, next-age button, coin numbers, tags, tip, suggest: eased BookMotion timelines. Not yet: "+ hand" markers, the tide dial, the slim purse's Visible, the Home and achievements buttons, the lit-band mesh of Pave/Extend (3D).
- 7 Sound: done for large sounds (level-reached, gift-arrives, request-met, charter-granted) on a player of their own, 0.6 s apart, played when their message appears; achievement plays when its note appears. The peal and the seal (RiverAudio, not mine) unchanged.
- 8 First minute: done (`TownHud.FirstMinute.cs`, `[hints] hints_seen` in settings.cfg; `GameSettings.ShowHintsAgain()` exists but no button: the settings pages are the next round's).
- 10 Tests: `MotionTest.Messages.cs` (banner, notes queue order/stay/gap, crowd, click away, line and cross-fade, the busiest moment, the large sounds' gap); game-layout rule 5 fails above loud 3 / quiet 4; `CheckMessages` (every message inside its zone).
- 11: per-checkout test profiles (`TownTestBase.ProfileFor`: user:// is shared by every checkout).

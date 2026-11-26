# Interface audit tour: progress

Builder of the interface audit tour, night 3. Test and tool files only; no game code changed.

## How to run

```
tools/ui-audit.sh                                   # 1920x1080, interface 100 %, out build/n3/ui-audit/1920x1080-100
tools/ui-audit.sh --res=1280x720 --no-build         # out build/n3/ui-audit/1280x720-100
tools/ui-audit.sh --scale=150 --only=u10,u13        # retake some states (replays the town up to the last of them)
python3 tools/ui-audit-report.py build/n3/ui-audit/1920x1080-100   # writes report.md there
DISPLAY_NUM=97 tools/ui-audit.sh ...                # fixed xvfb display (two runs at once)
```

Files: `tools/ui-audit.sh`, `tools/ui-audit-report.py`, `game/tests/ui-audit.tscn`, `game/tests/UiAudit.cs` (frame,
helpers, pictures, shots.json), `UiAudit.Age1.cs` (u01..u16), `UiAudit.Book.cs` (u17..u25 and the second age's
u40, u41), `UiAudit.Age2.cs` (u30..u41), `UiAuditDump.cs` (the per-state JSON).

Per state: `<id>.png`, `<id>.json` (window, scale, age, level, stage, items: name, type, group, rect in window
pixels, opacity, text (60 chars), font_px, text_size, trims). `shots.json`: id, title, how (how it was staged, with
the facts at the moment of the picture), level, made.

Names are node paths below the window with Godot's automatic names replaced by `Type#n` among siblings (stable
across runs while the HUD builds its nodes in the same order). Group: nearest Panel/PanelContainer, else the first
ancestor under the CanvasLayer smaller than the window. 3D labels (plot, boat labels, book print): group `3d:<owner>`.

## The states (order of play)

First age (each state's "how" in shots.json says exactly what was done and what was on screen): u01 opening, u02 villager affordable (town idles), u04 level 2 banner, u03 shop at rest with an affordable
and a locked line, u09 bell by Space, u05 request with deliveries + hint, u06 choice open (bot), u07 Extend bookmark +
pointer on a cottage, u08 Pave bookmark + pointer on a road, u12 an open building offer framed at size 70, u10 level 8
at rest, u11 a level-up (9..13) with a request running, 1.2 s after, nothing cleared, u13 level 15 at rest, u14 drive
with deliveries, u15 Tab, u16 charter (+ page if it opens), u17/u18 page and book stage, u19 cover, u20 New town
question, u21..u24 settings sections, u25 Pave's key -> Space (clash; cancelled).
Second age (N rings it; the bot does not pave until u36): u31 page, u30 first minute, u33 shop level 18, u36 Pave
mode, u32 ferry (or level 22) + request (skipper's if one comes), u35 level-up with request, u34 drawbridge + toll, u37 (no seal drive exists: nearest, see shots.json),
u38 seal + page, u39 Tab, u40/u41 stages.

## Status

- 20:04 first whole runs. Fixed: settings pages could not reopen from the cover under load (waits, three tries);
  second age reordered (u32, u34 were lost to the seal because u36 waited for a pavable road to level 27).
- 20:22 both runs restarted on the fixed code (1920x1080 on xvfb :91, 1280x720 on :92, at once: own profile per run
  via --tag).
- 20:40 both whole runs passed (37 made, u32 and u34 not made: the waits for them ran into the seal; u37's
  "nearest" was taken after the seal and shows the seal page, so it is wrong). Fixed in code (bounded waits, u35 kept
  below MaxLevel-3, u37 taken at MaxLevel-1 or marked not made); retake `--only=u32,u35,u34,u37` started 20:42 at both
  sizes. If a retake did not finish, its states keep the whole run's pictures/entries.
- Reports: `build/n3/ui-audit/<res>-100/report.md`. JPEGs of the 1920 run: `docs/review/n3/before/ui/<id>.jpg`
  (`python3 tools/ui-audit-report.py build/n3/ui-audit/1920x1080-100 --jpeg=docs/review/n3/before/ui`).

## Left / known limits

- u16: the first age's page does not open by itself at the charter (only the banner); the picture is the charter
  banner moment. u37: no seal drive exists in the simulation.
- 3D labels under the closed cover (u19, u20) are still listed (the tool cannot see occlusion by the board).
- Rerun after the layout changes: same command; ids are stable; names are node paths (a renamed HUD node shows as a
  new name in the moves list).
- 20:56 the retake did not fix u32 or u34. Both ran into the seal again; the likely cause is that skippers' requests are
  not `Town.Request`, so `Request.Active` never held. u35 was retaken at the seal (level 28). u37 is now marked not made
  and its wrong picture is deleted. Made: 34 of 37 at both sizes. Next: stage u32 on the skipper's request card
  (`RequestCardText` holding "skipper") or on the ferry alone, and stage u34 on `CrossingExists(Drawbridge)` up to level
  26, then `--only=u32,u34,u35,u37`.

## Staging repairs (before pictures)

Repair builder, 21:05-21:50 UTC, checkout `build/wt/n3-before` (the game as the owner played it). Tour files only, no
game code. Changed: `game/tests/UiAudit.Book.cs` (settings loop in `MenusAndBook`, new `SpreadRestNow`, `SpreadAtRest`),
`game/tests/UiAudit.Age2.cs` (`SecondAgeLate` rewritten up to the seal; new `SkipperAsks`, `AnyRequest`,
`SkipperCardShown`; `IsSkipper` removed), `game/tests/UiAudit.Age1.cs` (`Busiest` takes an optional "request running"
predicate, stops at the seal and marks the state not made there), `game/tests/UiAudit.cs` (`Retired` = {u37}, dropped
from shots.json in `WriteShots`), `tools/ui-audit.sh` (crash-retake list skips u37). All paths under
`/home/user/tide-toll/build/wt/n3-before/`.

- **u21..u24 (settings Sound, Display, Keys, Saving).** Open: wait up to 60 s for the cover at rest (`CoverShown` and
  the pause motion not running); focus the cover's Settings and press Enter, up to three tries of 15 s each until
  `PauseMenu.Pages` is non-null; if still null and the menu is in `State.Cover`, call `PauseMenu.OpenSettings()` (the
  button's own action; the how-line says so). Then wait (up to 40 s) until the spread is at rest for three frames
  running: `PauseMenu.Now == State.Settings`, `PauseMenu.Motion.Running` false, `SettingsPages.Arrival >= 1` (the
  arriving leaf has turned), `SettingsPages.Turning` false and `TurnProgress >= 1`. Only then `ShowSection(section)`,
  and wait for the same rest condition again (the tab's page turn ended), then 500 ms and 3 frames, check it still
  holds, and take the picture; otherwise the state is marked not made with the values. After the picture, Esc and wait
  up to 60 s for the cover at rest. Cause of the old fault: the section was chosen while the arriving leaf was still
  turning (`ShowSection` then jumps without a turn) and the picture waited only 5 s for the motion, so under load the
  book still showed. Under tonight's load (load average about 40) Enter on the cover also did not open the settings
  within 3 x 10 s: hence the longer waits and the `OpenSettings()` fallback. In another checkout: if the settings
  spread has other names, wait on "menu in its settings state, its transition ended, arriving page fully down, no tab
  turn under way".
- **u32 (second age middle: ferry, tide dial, skipper's request).** A skipper's request is not `Town.Request`: it is a
  boat with `Town.Boat(i).Request` true whose phase is not Gone or Leaving (that is what the request card shows while no
  town request runs). Wait (bot at speed, never past the seal) for `CrossingExists(RiverLinkKind.Ferry)` and such a boat,
  or level 24; if that ended without, wait for the ferry alone. Then close pages, clear the choice, follow view, settle,
  calm (8 s), and wait up to 6 s for the request card text (`TownHud.RequestCardText`) to contain "skipper"; if not,
  go round again (four tries, stopping at level 24). Picture taken whenever the ferry stands and the seal has not come.
- **u35 (busiest moment, second age).** `Busiest(id, title, 20, 24, AnyRequest)`: level by level from max(level+1, 20)
  to 24, the picture 1.2 s after the first level-up at which a request of either kind runs (the town's
  `Request.Active` or a skipper asking, as above), or at the level-up to 24 if none; nothing cleared. Never at the
  seal (not made then). It runs right after u32 (the ferry comes at level 22, so in practice levels 23 and 24).
- **u34 (second age late at rest: drawbridge and toll).** Wait (never past the seal) for
  `CrossingExists(RiverLinkKind.Drawbridge)` (it comes at level 27 in this build, the last level before the seal);
  then up to 6 game minutes more for `Tolls > 0`; then close pages, clear the choice, follow view, settle, calm, picture.
  The old condition also accepted "level >= MaxLevel-2" but it was never reached because u32's fallback waited on a town
  request (`Request.Active`), which the second age hardly runs, until the seal.
- **u37** removed from the tour (`Retired` in `UiAudit.cs`; the old entry is dropped from an existing shots.json; the
  crash-retake list in `tools/ui-audit.sh` skips it). There is no drive in the second age.
- **Results (21:39).** Retaken at both sizes (u32, u34, u35 in a run started 21:05; u21..u24 again in runs started
  21:27 and 21:31 after the longer cover waits). 36 of 36 states made at both sizes, all seven pictures read:
  u21..u24 show the settings spread at rest on the right section; u32 level 22, ferry, tide dial, the card "Request
  from the skipper Isolde"; u34 level 27, drawbridge up river, a toll paid, a skipper's request; u35 level 24 banner
  1.2 s after the level-up with notes and coin numbers; at 1920 a skipper's request runs (Wenna), at 1280 none did at
  levels 23 or 24 (the tour fell back to the level-up to 24; the how-line says request running=False). The two sizes
  are different towns by the second age (real-time play between states). Reports rebuilt; JPEGs of the 1920 run in
  `docs/review/n3/before/ui/` (36, no u37).

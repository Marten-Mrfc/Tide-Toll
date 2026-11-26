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

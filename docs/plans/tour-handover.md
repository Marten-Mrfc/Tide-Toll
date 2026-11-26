# Screenshot tour: handover

The owner's review pictures, from the final build, by one command, in play order, at 1920 x 1080.

## New files (nothing existing was changed)

| File | What |
| --- | --- |
| `tools/tour.sh` | builds the game (Debug, as `./check`), empties `build/tour`, runs `res://tests/tour.tscn` under xvfb with the game's renderer exactly like the screenshot step of `tools/check.sh`, through `tools/godot-test.sh` (fails on any printed error or warning line, a failed check, or no `TOUR PASS`) |
| `game/tests/tour.tscn` | the scene (script `Tour.cs`) |
| `game/tests/Tour.cs` | frame (on `TownTestBase`: own profile folder `user://tests/tour-pass/`, the player's save and settings are never read or written), arguments, playing helpers, view helpers, picture saving, `shots.json` |
| `game/tests/Tour.Age1.cs` | a, b, c shots up to the stall and extending |
| `game/tests/Tour.Roads.cs` | the lane from trail to cobbles, the crowded road, touches, hands, shop lines, mill, drive, charter, achievements |
| `game/tests/Tour.Book.cs` | g (zoom to the book, pause, cover, New town) and h (settings pages, interface 150 %) |
| `game/tests/Tour.River.cs` | f (ring, age page, landing, ford, pasture and weaver, ferry, river shop, drawbridge, seal) |
| `game/tests/TourNotes.cs` | per shot: what the picture really shows ("shows") and the game's faults seen in it ("wrong"), from looking at the pictures; copied into `shots.json` |
| `docs/plans/tour-progress.md`, this file | |

## Command

```
tools/tour.sh                                   # whole tour, clean build/tour (15.5 min here, machine busy)
tools/tour.sh --only=d04-gravel,f07-drawbridge  # retake shots: replays the same town up to the last asked shot, keeps the other files and entries
tools/tour.sh --no-build                        # skip the dotnet build
tools/tour.sh --notes-only                      # keep the pictures, rewrite shows/wrong in shots.json from TourNotes.cs (headless, ~1 min)
```

Output: `build/tour/<id>.png` (1920 x 1080), `build/tour/shots.json` (the plan's structure; per shot `made`, `file`,
`shows` corrected, `wrong` where the picture shows a fault, `why` when not made; the questions are the lead's, unchanged),
`build/tour/tour.log` (one `TOUR SHOT id level= t=` or `TOUR NOT MADE id: why` line per shot), `build/tour/build.log`.
Plan read from `docs/review/shots-plan.json` (`--plan=` after `--` in the godot command to use another).

## How it plays

- One town, seed 1 (checked), 1920 x 1080 (checked). Between pictures the simulation's best-payback bot plays at once
  through `TownDriver.RunBot` (no frames), or the town runs by itself (`Town.Step`) while the tour waits for coin to buy
  something by a real click on its bookmark. For each picture the town runs at normal speed, the drawing catches up
  (road reveal, building pops), banners and notes go (unless the banner is the subject), the camera is placed with
  `CameraRig.LookAt` (as `Screenshot.cs`) on the subject.
- Played differently on purpose, honestly: the bot does not ring the bell until b07 (rung by Space) and does not pave
  until d05, so the lane can be followed from faint trail (d01) to worn (d02), Pave mode (d03, bookmark click + pointer),
  gravel (d04, click) and cobbles beside a gravel road and tracks (d05, clicks), and so an unpaved road crowds (d06).
  Villager, Basket, Pace, the stall, Extend, Pave are bought by real clicks; the bell, Tab, N, Esc, Enter by keys.
- Shots for things another builder is adding (c04 choice, e01 charter drive, b03 shop worth, b08 hand, c06 touch) are
  taken when the simulation has the thing AND the game shows it: the choice's boon name or "charter drive" in a visible
  interface label, a `Hand` item among the shop's shown items, a 3D node named after the touch kind (e.g. `Woodpile`,
  `WashingLine` in its name or scene file), a "/min" or "a minute" figure on a shop line. Otherwise "made": false with
  the reason. When the new art lands, check `Drawn3D` in `Tour.Roads.cs` still finds it by name.
- Order of the run: the first age (a, b, c, d, e in the order they happen), then the river (f, rung in by N), then the
  zoom, pause, New town and settings (g, h) on the sealed borough: the plan puts the book and settings last too.
  River pictures never wait past the seal (`Early`); an age page left open by the seal is closed with Esc first.
- Settings: only the interface size (h05, through the Display page, put back and checked) and a key clash (h03,
  cancelled with Esc, checked unchanged) are touched, in the tour's own profile.

## What the lead must know

- Run it in the main checkout as is: `tools/tour.sh`. It needs xvfb-run and Godot as `./check` does (`GODOT=`).
- It is not part of `./check`.
- `TourNotes.cs` holds my reading of the pictures of the last whole run. After game changes the pictures change: look
  again and update the notes (they are data, not checks).
- Repeatable only in what is shown, not tick for tick: same town (seed 1), same conditions for every picture, same
  purchases by click; but the composition waits (drawing catching up, banners going, about 1 to 30 s each) run the
  town at normal speed in REAL time, so the bot phases start from slightly different states and the town drifts
  (in one run the Extend shot came at level 5, in another at level 7; levels and villager places differ a little).
  Making it exact needs `--fixed-fps 60` and frame-counted waits, which is several times slower on this machine
  (the banner alone is 360 rendered frames); not done. Every shot waits on a condition, so the subject is there.
- Never edit `tools/tour.sh` while it runs (fixed now: the body is one block bash reads whole).
- Godot segfaulted twice in 8 runs (dmesg: in the renderer, machine loaded by other agents' xvfb runs). `tools/tour.sh`
  then retakes the shots with no picture yet once with `--only` (replaying the town). Two `xvfb-run -a` at once can
  also hang; `--notes-only` runs headless for that reason.
- Last result (run 8, clean folder, 933 s, exit 0): 46 of 50; b05 failed there (second Extend click missed) and was
  fixed and retaken with `--only=b04-extend-mode,b05-extended` (382 s): 47 of 50. Not made: c04 choice, c06 touch,
  e01 charter drive (simulation has them, game draws nothing yet). f06 and f07 are made but not quite what the plan
  asks (raft docked, not mid-river; no toll paid yet): see their `wrong`.
- Hooks I would have wanted: a way to know the HUD draws the choice / drive / touch (an id on the node), the bell card
  showing time left while ringing, a camera call that frames a rect clear of the interface cards
  (`Main.Follow` frames the whole town but the banner and shop cover its edges).

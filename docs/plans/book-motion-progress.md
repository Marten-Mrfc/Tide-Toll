# Book motion (round 7): progress

The owner asked for the book's animations (closing for the pause menu, opening, the settings and their tabs) to look
smooth, not staggered. This file records what is done, what is next and the decisions.

## Done

- `game/scripts/BookMotion.cs`: one timeline per transition (fixed length in seconds, real time, a frame steps it at most
  1/20 s, holdable by tests); curves: `Smoother` (smootherstep: no speed or acceleration at the ends), `Smooth`,
  `Land` (the board: gathers speed, touches with no speed at 0.8 of its window, lifts 1.2 percent and settles), `Mix`,
  `MixSize` (view height in log space).
- `PauseMenu.Motion.cs`: every menu change is one BookMotion timeline that every part reads (interface opacity, camera,
  board, models' sinking, lettering, question, settings spread). A transition starts from wherever every part is, so
  Esc, Back or a click in the middle of one turns it round smoothly (parts then start at once). Visible changes only at
  no opacity. The pause's save is written in the frame after the motion's last. The frame that takes the input writes
  the left page's summary and the clock starts in the next frame (no text change while the page turns with the board).
- `BookCover.cs`: the left half of the open book (left page, its page stack and the left half of the binding) turns
  over the spine as one rigid piece, hinged at 0.3 m; the binding is two halves from the start (no halving, lifting or
  endpaper swap); the town's models sink flat (MapOrigin y scale to 0.01) on their own eased window, done before the
  board is low; hidden only when shut and still.
- `CameraRig.cs`: `Place` (a staged move sets the view itself, no exponential chase), `TargetPivot`, `SetDeep` (the
  camera moved back along its axis with a wider depth range while the board stands up to 310 m high).
- `SettingsPages*.cs`: built once at start-up by PauseMenu with all four sections, each under a `SectionMask`
  (ClipChildren: hides drawing only, input unaffected); shown and hidden, never rebuilt on open or tab change (only a
  window or interface size change rebuilds; a section's own control rebuilds that section only). Tab change = a page
  turn (`SettingsPages.Turn.cs`, 0.36 s, smootherstep): the leaf's edge sweeps the spread (right to left forwards, left
  to right backwards), the old section ahead of it and the new one behind it, a `PageLeaf` washing and shading the
  lifted part, the ribbons sliding out and in, the page-turn sound once. Back to the section being left turns back from
  where it is; a third one waits (the current turn at 1.3 times pace). The chosen section takes clicks and focus at once.
- `PageLeaf.cs`: the shared leaf drawing (rectangles only). The age-change page (`TownHud.River.cs`) is an opaque leaf
  with a curled, shaded edge and its shadow; the right page's title, the river and the tide dial change under it once it
  covers the screen.
- `TownHud`: `Fade` (the whole interface's opacity); no page-turn sound from the zoom stage while paused; achievements
  and age pages close quietly once the interface has faded out.
- Warm-up (`PauseMenu.WarmUp`): two frames at start-up with the lettering, the question and all four settings sections
  laid out and drawn at no opacity (fonts rasterised at their sizes, controls laid out).
- Tests: `game/tests/MotionTest*.cs` (`motion.tscn`), `tools/motion-check.sh` (60, 30, 120 fps), the `game-motion` step
  in `tools/check.sh`. Frame strips: `game/tests/MotionStrips.cs` (`motion-strips.tscn`), `tools/motion-strips.sh`,
  `tools/motion-sheet.py`, sheets in `build/motion/`.

## Lengths and curves

| Transition | Length | Parts (seconds on its timeline) |
| --- | --- | --- |
| Pause (close) | 1.25 + the input frame | interface out 0-0.2; camera 0-0.8; board 0.35-1.05 (Land, touch 0.91 with the book-close sound); models sink 0.30-0.85; lettering in 0.95-1.25 with a gilt glow |
| Resume (open) | 0.95 | lettering out 0-0.2; board up 0.08-0.63 (book-open sound at its start); models rise 0.20-0.65; camera 0.2-0.9 to the kept view; interface in 0.7-0.95 |
| Cover to settings | 0.9 | lettering out 0-0.2; board up 0.08-0.63; camera 0.1-0.7 to the spread view (3D pages under the 2D pages); spread in 0.5-0.9 |
| Settings to cover | 1.05 | spread out 0-0.25; camera 0.1-0.65; board 0.15-0.85 (touch 0.71, sound); lettering 0.75-1.05 |
| Question in / out | 0.3 | lettering out 0-0.2 and question in 0.05-0.3 sliding up 28 px; out: question 0-0.2, lettering 0-0.3 |
| Tab change | 0.36 | leaf edge, sections and ribbons on one smootherstep |
| Age-change page | 1.1 | leaf in from the right, covering at half way, out to the left (smootherstep); shading grows and fades |

All windows use smootherstep unless named. Taken over mid-way, the parts start at once (camera 0-0.7, board 0-swing).

## Decisions

- The save after the motion, not off the main thread: it needs the town's state, and a frame at rest hides the cost.
- Tab redirect to a third section queues rather than swapping the incoming page (a swap would pop the revealed part).
- A click on the pages mid-turn reaches the chosen section's control (masks hide drawing only); the page turns on.
- The ribbon's colour (normal or pressed style) still changes at the click: it is the button's own press feedback.

## Existing tests changed (the behaviour they pinned was one of the jumps)

- `MenuTest.cs`: four "back on the cover" checks after Esc or Back now wait (`Until`) for the cover instead of checking
  three frames later (the return is a ~1 s closing of the book now). `RiverTest.Pause.cs`: the same, once.
- `RiverTest.cs`: the river's title is no longer set in the frame the age is rung (before the leaf covered anything); it
  now checks that the title and the river are not there before the leaf covers the screen, then waits for the title.
- `Screenshot.cs`, `Screenshot.V3.cs`, `Tour.Book.cs`: the half-closed shot holds the menu's timeline
  (`PauseMenu.HoldAt`) instead of `BookCover.Hold` + `Close`, and the settings and question shots wait for the motion to end.

## Also

- `PauseMenu.WarmUp` ends with a GC: building the spread at start-up leaves thousands of dead wrappers in Godot's tracker of
  C# objects; collected then, the tracker does not have to grow later in play (game-perf saw a 1.3 MB frame without it).

## Next

- The age page panel itself (and the achievements page) still open in one frame (out of this request's scope).

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

## Second pass (round 8)

- Settings from the cover: the board up (0.03-0.58) and the camera to the spread view (0.05-0.65) first; the settings
  pages then arrive by a leaf (0.684-0.984, 0.3 s; `SettingsPages.Arrive.cs`): the tab turn's `PageLeaf` and
  smootherstep, the settings drawn only behind its edge (right to left), the town's 3D pages ahead of it, the ribbons
  sliding out 40 px and fading in with it. No cross-fade, so the town's map and summary never show through. The way back
  turns the leaf back first (0-0.27; a tab page still turning lands first), then the camera (0.304-0.804), the board
  (0.324, touch 0.884) and the lettering (0.924-1.174, 0.25 s): 1.17 s.
- In the game the spread draws no table and no cover (`OwnTable` false): the 3D table and book stay on screen unchanged.
  Alone (the settings tests and shots) it still draws its own and keeps round 7's proportions. In the game the 2D pages have the 3D pages' proportions (two pages of
  `PageWidth` and the 2 m spine gap by `PageDepth`), so the spread view puts the 3D pages exactly under them (0.00 px at
  1920 x 1080 and 1280 x 720, interface 80, 100 and 150 percent). In a window narrower than 1.82 : 1 around the pages
  (1280 x 720) the pages are now a little shorter than before (width-limited), so a long column scrolls sooner.
- The ribbons' colour (tint to paper) and border follow their pull on the turn's curve: each ribbon has its own style
  boxes, the same for normal and pressed, so the click no longer changes the colour at once.
- Achievements and age pages (`TownHud.Pages.cs`): 0.22 s smootherstep in and out (opacity and a 16 px rise), visible
  only at no opacity (the frame that shows one draws it at 0 and its clock starts in the next), hidden only at 0; turned
  round from where they are (a part-way return takes the matching share of 0.22 s). The pause's quiet close (the
  interface already faded out) hides at once. The age page after the age-change leaf comes in the same way.
- The question: the one leaving goes out in 0-0.16, the one coming in 0.13-0.3: never both above 15 percent (0.002).
- Not done: carrying speed over when a transition takes over another mid-way (parts still restart from no speed).

## Lengths and curves

| Transition | Length | Parts (seconds on its timeline) |
| --- | --- | --- |
| Pause (close) | 1.25 + the input frame | interface out 0-0.2; camera 0-0.8; board 0.35-1.05 (Land, touch 0.91 with the book-close sound); models sink 0.30-0.85; lettering in 0.95-1.25 with a gilt glow |
| Resume (open) | 0.95 | lettering out 0-0.2; board up 0.08-0.63 (book-open sound at its start); models rise 0.20-0.65; camera 0.2-0.9 to the kept view; interface in 0.7-0.95 |
| Cover to settings | 0.984 | lettering out 0-0.2; board up 0.03-0.58; camera 0.05-0.65 to the spread view; leaf 0.684-0.984 (ribbons with it) |
| Settings to cover | 1.174 | leaf back 0-0.27; camera 0.304-0.804; board 0.324-1.024 (touch 0.884, sound); lettering 0.924-1.174 |
| Question in / out | 0.3 | lettering out 0-0.16, question in 0.13-0.3 sliding up 28 px; out: question 0-0.16, lettering 0.13-0.3 |
| Achievements / age page | 0.22 | opacity and a 16 px rise, smootherstep, in and out |
| Tab change | 0.36 | leaf edge, sections and ribbons on one smootherstep |
| Age-change page | 1.1 | leaf in from the right, covering at half way, out to the left (smootherstep); shading grows and fades |

All windows use smootherstep unless named. Taken over mid-way, the parts start at once (camera 0-0.7, board 0-swing).

## Decisions

- The save after the motion, not off the main thread: it needs the town's state, and a frame at rest hides the cost.
- Tab redirect to a third section queues rather than swapping the incoming page (a swap would pop the revealed part).
- A click on the pages mid-turn reaches the chosen section's control (masks hide drawing only); the page turns on.
- (Round 7: the ribbon's colour changed at the click. Round 8: it follows the turn.)

## Existing tests changed (the behaviour they pinned was one of the jumps)

- `MenuTest.cs`: four "back on the cover" checks after Esc or Back now wait (`Until`) for the cover instead of checking
  three frames later (the return is a ~1 s closing of the book now). `RiverTest.Pause.cs`: the same, once.
- `RiverTest.cs`: the river's title is no longer set in the frame the age is rung (before the leaf covered anything); it
  now checks that the title and the river are not there before the leaf covers the screen, then waits for the title.
- `Screenshot.cs`, `Screenshot.V3.cs`, `Tour.Book.cs`: the half-closed shot holds the menu's timeline
  (`PauseMenu.HoldAt`) instead of `BookCover.Hold` + `Close`, and the settings and question shots wait for the motion to end.

## Checks changed in round 8 (the design they pinned changed)

- `MotionTest.Checks.cs`, settings-open: "the spread arrives on the paper as the camera settles" (it started before the
  camera's last move) became "the leaf starts after the camera and the board are at rest", plus the leaf's length
  (0.3-0.42 s) and the settings shown exactly behind the leaf's edge on every frame (`LeafBehind`).
- settings-close: "the spread fades from the start" became "the leaf turns back from the start" plus "the camera and the
  board wait for the leaf" and `LeafBehind`.
- question in/out: "the lettering and the question cross" became "the one leaving goes first" and "never both above
  0.15". New: `RibbonEased` in every tab turn, `HudPages` and `PagesAlign` (`MotionTest.Round8.cs`). Bounds unchanged.
- `SettingsPages.SectionShown` is 0 while the spread is hidden (it reported the hidden spread's section as shown).

## Also

- `PauseMenu.WarmUp` ends with a GC: building the spread at start-up leaves thousands of dead wrappers in Godot's tracker of
  C# objects; collected then, the tracker does not have to grow later in play (game-perf saw a 1.3 MB frame without it).

## Next

- Carry a part's speed over when a transition takes over another mid-way (round 8's item 5, not done).

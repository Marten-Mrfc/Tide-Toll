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
- Full game-motion with --cpu-info at 60/30/120 (03:10): MOTION PASS at all three (shape checks all passed); CPU over
  the gate: 1 frame at 60 ("click-settings-on-lettering: frame 4 took 6.35 ms"), 1 at 30 ("tab-redirected: frame 13
  took 6.90 ms"), 0 at 120. Rate comparison of the END lines: every book transition the same; the message cases
  msg-notes, msg-crowd, msg-note-clicked, msg-busiest differ by 0.11 to 0.31 s between rates (bound 0.067): not
  caused by this round (message timing), left.

## Item 2: the page stage (03:30, done)
- `CameraRig.PlaySize` is now the whole open book with at least 44 px to every window edge and the navigation
  row's room (72 x interface scale) at the foot, capped at 0.96 of the book stage's size; `Clamp` settles the book to
  that place between the map-page size and PlaySize (the book a little high: margin above, navigation room below).
  The old size (MapSize x 1.2) is `MapViewSize` and keeps the tilt, the slim strip threshold, the summary's watch, the
  sound's zoom and the zoom-to-a-thing; `BookStageSize` follows PlaySize.
- Pictures: `build/n3/ui-audit/last-look/p-1920/u17.png` (whole book, margins 30 top / 115 sides / nav under the
  book, compass and folio whole); after the top-margin rule `b-1280/u17.png` (size 396, book 412: margins 44 top,
  78/80 sides; the navigation row touches the cover's bottom edge by about 2 px, folio clear).
- Fault left: the page stage is now only about 4 % closer than the book stage (both show the whole book).

## Item 3: tooltips (03:26, done)
- `TownHud.Tip.cs`: a RichTextLabel; the coin sign is the shop's coin icon inline (marker ¤ in the text), never the
  word; the box is measured from the text (and the label's own content height) and widens past five lines; never cut.
  A boon's tip lies under the choice card on its own side. Pictures `last-look/b-1920/u42.png` (5 lines, whole text,
  coin signs), `b-1280/u45.png` (under the card, the other boon readable).

## Item 4: cottages (03:35, code done; pictures below)
- `TownBuildings.Cottages.cs`: `CottageFill` = 0.86 / 0.91 / 0.95 of the plot's shorter side, the widest model of
  each size (`CottageSpan(ext)`).

## Item 5 (03:33, done): "What"/"Wants" Label3Ds created hidden (RiverBoats, RiverLayer stake label, TownBuildings);
  map tags fade with a page's own eased fade (`TownHud.Labels.cs`, `under`).

## Item 6 (03:35, done): the age leaf turns about the spine (left screen edge), on its own canvas layer (6) over the
  whole interface, "The Town on the River" on its back. Strip `build/motion/age-page.png` read. game-river PASS.

## Item 7 (03:40): (a) hint text changed; (b) icon names: shop-belltrack, shop-load-<step>, shop-building-<offer>,
  boon-*, nav-tide-high/low, each falling back to today's names; medals on the achievements page NOT done;
  (c) seal-granted passed with the seal's banner and added to IsLargeSound.

## Item 4 pictures (read): `build/n3/assets-audit/last-look/{before,after}/street-normal.png`: the plain cottage
  about 75 to 97 px wide at the play zoom, once-extended a little larger, twice-extended unchanged; shapes still tell
  them apart; nothing overlaps the villagers in front. `town-view-charter-normal.png`: no visible change (only a
  twice-extended cottage at the edge of that view).

## Final tests (03:44 to 03:50 UTC, this tree)
- game-layout: LAYOUT PASS states=36 canvases=8 checks=73519 failures=0. game-book: BOOK PASS. game-settings,
  game-menu, game-save, game-town, game-river: PASS. game-playthrough: PLAY PASS charter=33.9min buys=201.
  game-perf: PERF PASS. game-motion with --cpu-info at 60/30/120: MOTION PASS, 0 frames over the CPU gate.
- tools/motion-check.sh as ./check runs it (twice, load 5 to 8 from other checkouts): FAILED on the CPU gate only:
  run 1 "pause: frame 13 took 6.84 ms" (60), "line: frame 67 took 12.47 ms" (30), "tab-saving-keys: frame 29 took
  8.77 ms" (120); run 2 "click-mid-turn: frame 12 took 11.83 ms" (60), "note-clicked: frame 133 took 96.21 ms" (30),
  "notes: frame 464 took 6.69 ms" (120).
- Not done: item 7b's medals on the achievements page and message icons; item 8 (dropped by the lead); whole ./check.

# Round 2 play-test (real input, 1920 x 1080 under xvfb)

Played 25 November 2026, 22:47 to 23:55 UTC (box was 30 minutes; it overran because the machine ran at load 10 to 12 and one rendered frame took about a second, so every zoom notch took 15 to 60 s). Checkout `/home/user/tide-toll/build/pt/r2`. Scene: `game/tests/PlayR2.cs`, `game/tests/playr2.tscn` (real window seam, not in `./check`). Final run log: `/tmp/pt2/run.log` (settings, saving, quit, New town; shots `build/pt-shots/01..36`). Earlier runs: `/tmp/pt2/run2-first-half.log` (zoom, moving, lost town, cover menu; shots `build/pt-shots-run2/`), `/tmp/pt2/run1-partial.log` and `build/pt-shots-run1/`. All shots are in `/home/user/tide-toll/build/pt/r2/build/`.

What was played: new town at seed 1 (name Dunstead), looks at speed 16 with clicks on bookmarks, key 2 and bell clicks (game time 86 to 150 s, level 3 by the final run; the zoom run played to 4 min); zoom out to the book and back by wheel; left and right drag, arrow keys, D; dragging the town away; Esc, cover menu by keys and mouse; every settings page by mouse; quit by the menu and restart; New town; Delete. NOT played: a full ten minutes (the simulation is not the subject), Pave while tilted (game-book covers it), the in-game interface and cover at 80 and 150 percent (only the settings pages at those sizes), a keyboard-only walk of the settings pages (SettingsTest covers it).

## 1. What did not do what a player would expect

1. **Game keys still work on the closed cover.** `TownHud._UnhandledInput` (game/scripts/town/TownHud.Shop.cs, line 221) has no pause check. In the run, pressing 2, P and 1 on the cover made my check fail (purchases changed or Pave mode came on; I did not separate the two). Proven side effect: P switched Pave mode on under the closed book, so the next Esc only left Pave mode and did not pause; the player sees the book open with no pause. Same code path reaches the settings pages for any key that is not being captured. Fix: ignore these keys while `PauseMenu.IsPaused`.
2. **N is hard-coded as "Ring in the next age"** (same file, line 230) and is not on the Keys page. I bound the bell to N and no clash question came; N then rang the bell (the bell branch is first). A player who rebinds anything to N loses the next-age key silently. Not run to the end of the age; from code only.
3. **Nothing tells a player who lost the town how to get back.** After 9 left drags the view sits on blank paper and one corner of the map (`19-lost-town-dragged-away.png`; pivot (-24, -0.6), town centre at screen (3327, 3112)). Notes, bookmarks and the Pause label say nothing; the Home key appears only on Settings > Keys ("Back to the town"). Home does bring the town back (checked), and so does zooming out. The only hint in the interface is "Scroll out to see the whole book." (it appears once at about 75 s, and again after every restart).
4. **Delete the save leaves a save.** After Settings > Saving > Delete > Delete, the log shows a new town (new seed, level 1, music volume kept) but `HasSave` true and the Load button enabled: a save of the fresh town exists at once. A player who deletes expects "No save". (I did not find which call writes it.)
5. **Space on the cover resumes** (Resume has the focus on opening, Space activates the focused button). A player who taps Space for the bell habit gets the book open. Probably fine; noted because it also made my own script resume.
6. Autosave "Every minute": no save appeared in 91 s of real time (`Saver.Saves` stayed 2). Very likely an artefact: `TownSaver._clock` adds the frame delta and this machine drew about one frame a second, which the engine clamps. Not shown to be a bug; unverified, please re-test on a normal machine.

Worked as expected (checked by value): wheel zoom; left drag pans, right drag back returns to the start (0.00 m error); arrows and D pan; Home returns to the town view at the same size (47.9); Esc pauses and the tick stands still on the cover; Down x4 on the cover goes Settings, New town, Quit, Resume (wraps), Up goes back; mouse hover moves the focus; Resume by click and by Esc; the wheel does nothing on the cover; settings: Music slider click at 25 percent gives 25, the Right arrow gives 26, Master at the left end 0 mutes the bus, right end 100; Mute when inactive, coin numbers, follow, VSync (applied to the display server) and frame limit switch and apply; interface size 150 and 80 apply to the window content scale (1.5, 0.8) and come back to 100; resolution step opens "Keep this?" and resizes the real window to 1600 x 900, "Go back" restores 1920 x 1080; window mode step goes to full screen with "Keep this? Going back in 8 seconds", Keep stores Borderless, going back to Windowed works, an unanswered question goes back by itself after the countdown (tested with 3 s); Keys: Esc during capture cancels, rebinding the bell to N works in game and Space stops ringing (also after a restart), binding Pave to N raises "That key is taken ... Swap keys / Cancel" and Cancel leaves both keys; Saving: autosave Off, Save now writes the file, Load brings back exactly the state Save now wrote (compared tick, coin, level, villagers, every road), Delete asks and "Keep the save" keeps it; quit by the menu saves, restart gives the identical town state (string compare), the same camera (pivot and size), settings kept (bell key, music 26, master 80, mute, 1920 x 1080, windowed); New town asks, "Keep this town" keeps, "Start a new town" gives a new seed at level 1, new name (Barcombe), settings untouched, and a quit and restart brings back the new town. After restart the book comes up open and running, not on the cover (design choice, but note it: "continue" is immediate).

Notes: the first town is always seed 1, Dunstead, for every new player and every fresh profile (default 1280 x 720 window at first start). New town is random.

## 2. What looks wrong in the screenshots

(In `build/pt-shots-run2/` unless marked final run `build/pt-shots/`.)
- `14-book-whole.png`, `18-summary-page-closeup.png`: the summary page numbers carry stray marks right after the digits ("2:" for villagers and cottages, "62:" for trips) and a dot above some letters (Roads, Paved roads, Coin earned, Time in the town). It looks like the digit font's glyph or the ink outline leaving noise; it is on the lettering the player reads most. The book itself reads well as a book: two pages, gutter, page numbers, title in a manuscript face, a plan with a compass.
- `18-summary-page-closeup.png`, `14-book-whole.png`: the "Pause [Esc]" control (bottom right) sits over the corner of the book; in 18 it covers the right page number "2". Its white box has a clipped left edge in every shot.
- `19-lost-town-dragged-away.png`: the HUD (coin ledger, bookmarks) floats over a blank page with only a map corner at the bottom right. Legal, but see 1.3.
- `12-zoom-out-7-Town.png`, `13-zoom-out-8-Town.png` (run 2 numbering 12 and 13): half way out the left page is blank paper with the ledger on it and the map is a panel against the right half; the town is small and in the top third while the bookmarks cover the map's right edge. Readable, not broken.
- `08-settings-display-150.png`, `10-settings-keys-150.png` (final run): at 150 percent the pages fit. On Keys the list scrolls and the last visible row ("Zoom in") is cut by the list's bottom edge just above Back. Fine, but the cut is hard.
- `12-settings-display-80.png`: 80 percent is small but legible; the spread shrinks and floats in the middle of the window.
- `17-settings-display-keep-question-mode.png`: "Keep this?" is clear (Keep focused, Go back, countdown); the page behind is greyed.
- `24-settings-keys-clash.png`: the "That key is taken" slip covers the rows below Pave and half of the "Esc always ..." note. It is a modal, acceptable.
- `32-new-town-question-100.png`: only the focused choice has a frame. "Start a new town" looks like a caption, not a button, until the pointer is on it.
- `05-cover-menu-100.png` (final run), `23-cover-menu-100.png`: the cover looks like a book: leather, gilt double border, diamonds, title, town name, four items, Resume framed. "Dunstead" in dim gilt on brown is the weakest contrast but readable. The shot taken 15 frames after Esc already shows the closed cover (`22-pause-closing-early.png`), so I did not see the swing; the screenshot test has the half-closed shot.
- Tilt: at 56 degrees the town reads almost top-down (`01-start-town-t0.png`): roofs and the well show a side face, villagers are readable (about 30 px), but a player would call it "top down with a little depth". It hid nothing I needed; bookmarks and roads were all reachable.

## 3. The zoom, from numbers (cursor at (1400, 300), notch = factor 1.176 on view size)

Town size at Home 47.9; book at 380.6 (max); slim strip at 264; page stage from 264; book stage from 344.

| notch out | view size | pitch | stage and interface | cursor drift after settling |
| --- | --- | --- | --- | --- |
| 1 to 4 | 56.4, 66.3, 78.0, 91.8 | 56.0, 56.0, 56.0, 56.3 | Town, full | 0 px |
| 5 to 8 | 108.0, 127.0, 149.5, 175.8 | 57.5, 60.1, 64.2, 69.9 | Town, full | 0 px |
| 9 | 206.9 | 77.1 | Town, full | 0 px |
| 10 | 243.4 | 84.7 | Town, full alpha 0.66 with the slim strip already showing (cross-fade) | 0 px |
| 11 | 286.3 | 89.7 | Page, full 0, slim | 108.5 px |
| 12 | 336.8 | 90.0 | Page | 123.9 px |
| 13 | 380.6 | 90.0 | Book, slim strip over it | 79.3 px |

- **13 notches from the town to the book**, 13 back (book to size 46.0, about 4 percent under the Home size; the minimum 36 is 2 notches further). A 14th notch at the book does nothing.
- The town stays under the cursor to 0.0 px through notch 10 (while tilted and during the easing, maximum deviation 0.0 px in the 20 frames sampled after each notch). From notch 11 the book's edge limits the pan: the point under the cursor slides 79 to 124 px. Going back in, notch 1 ends 7.9 px off (42.5 px at worst during the ease), then 0 px.
- Tilt: nothing for the first 4 notches (the town stays at 56 degrees), then 0.8, 1.7, 2.8, 4.0, 5.1, 5.5, 4.1 degrees a notch. A notch inside the first four changes only the size; the "tilting" is concentrated in notches 6 to 11.
- The interface goes full, then thins at 243 (notch 10), then the book stage; both directions the same. Nothing jumped: size and pitch change monotonically (pitch never went back). I measured the total change of each notch over 20 frames, not per frame, so a one-frame jump inside a notch is not excluded (the largest total is 12.3 percent of size, 5.5 degrees, one notch). The camera stays "not following" after any wheel use until Home.
- Frame time cannot be judged here (about 1 s per frame under load).

## 4. Errors and warnings

- Printed by the game in the final run: only the exit-time ones `tools/godot-test.sh` allows (`ERROR: 4 resources still in use at exit`, `WARNING: 8 ObjectDB instances were leaked at exit`). No script error, no unhandled exception, no "Could not save the town".
- A checkout that has not been imported fails: `ERROR: No loader found for resource: res://assets/textures/paper-page.png`, followed by NullReferenceException in `Main._Process` every frame (first run, before `godot --headless --path game --import`). This checkout was "synced and built" but not imported; the real game would not hit it after an import. Worth knowing if a fresh clone ever skips the import.
- Build warnings (`dotnet build game/TideToll.csproj`): CS0108 `TownHud.Scale` hides `CanvasLayer.Scale` (TownHud.cs 45); CS8604 possible null in `TownVillagers.cs(65,48)`.

## 5. Opinion

1. It does feel like a game inside a book at both ends: the whole-book view, the closed gilt cover as the menu and the settings as an open spread are convincing. In between, the tilted town view is just a map with a ledger over it; the book is only a place you visit.
2. Weakest part: the small things around the edges: keys that act under the closed cover (P switched Pave mode on and ate the next Esc), the unadvertised Home key and the empty page after dragging, and the Delete that leaves a save.
3. The zoom is good: 13 even notches, the town holds still under the cursor until the book's edge takes over, and the interface thins at the right moment. The 4 notches at the start that change only size make the first scroll feel flat.
4. Settings, saving and continue were solid: every value I changed took effect and survived a restart, "Keep this?" reverts, Load returned exactly the saved state.
5. One change that would help most: make the HUD ignore keys while paused (and make Esc always pause or leave Pave mode, never silently), then show "Home: back to the town" as a small note or button whenever the town is off screen.

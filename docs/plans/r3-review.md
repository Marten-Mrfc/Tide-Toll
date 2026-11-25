# What round 3 must fix and add (the lead's list)

Sources: the real-input play-test of round 1 (`docs/plans/r1-playtest.md`, read it), the lead's look at round 2's screenshots, and the builders' own "not built" notes. Most serious first within each part.

## A. Things that are broken or mislead the player

1. **A Pave click can land on a different road than the one the price tip names**, or do nothing ("Already cobbled", "Cobbles come later"). In the play-test only 24 of about 115 pave attempts paved. Hovering and clicking must use one and the same picking, the click must act on exactly the road the tip names, and a road that cannot be paved must not light up or take the click. Test it with many roads close together at the end of the age, tilted and flat.
2. **The suggested road is often off screen and nothing says where.** Choosing Pave moves the view so the suggested road is in sight (and back again afterwards if the view was following the town).
3. **After zooming or dragging, the view never follows the town again and there is no visible way back.** Show a small "Back to the town [Home]" control whenever the view is not following.
4. **Pave mode stays on after a road is paved.** Leave Pave mode after a successful pave (holding Shift keeps it on for several).
5. **The Pave bookmark reads "Pave a road" when there is not enough coin.** It must show the same clear not-yet state and reason as the others.
6. **Two building offers share the key [5].** Every bookmark has its own key for as long as it is shown.
7. **Notes pile up and the important one drowns.** A level reached is a banner of its own (bigger, longer, with the gift named and the camera showing it); small notes merge and never show more than three; a perk is one line, once.
8. Build warning CS0108: `TownHud.Scale` hides `CanvasLayer.Scale`. Rename it.

## B. The look

1. Lanes zigzag. The simulation now gives smooth curves (`Road.Px`, `Road.Py`): draw those, never `Xs`/`Ys`, so villagers walk on the drawn road.
2. A staked plot reads as an editor's box. Pale wooden stakes, a light cord, and a small label saying what will stand there ("A cottage, at level 4").
3. Coin numbers at deliveries are small and grey. Make them gold with a dark edge, a little bigger, rising and fading.
4. Gravel is a bright speckled slab. It should be a soft pale path, clearly between a worn track and cobbles.
5. Loads are hard to see at the normal view. Bigger or brighter loads, so Water, Grain and Flour can be told apart at a glance.
6. Villagers are now readable but all look like the same pale pawn. Keep the six clothes colours stronger.
7. A track's worn earth stops at the door cell; it should reach the door like the paved strips do.
8. The half-faded interface overlaps the summary page during the zoom out. Fade it out before the left page comes into view.
9. The level-6 follow view sits left of the free space because staked plots count in the town's extent. Frame what is built, with the next plot just in view.
10. The left page of the book has a lot of empty paper: the achievements belong there (see C).

## C. What the game must learn from rules v2 (after the merge; `docs/plans/town-sim-api.md`, "Added in rules v2")

1. The shop is longer and data-driven: Villager, Pace, Load, Tools, a value track for each good, Extend a cottage, the bell track, Pave, building offers. Group it so it stays readable (for example People, Work, Roads, Buildings), show a step that is locked with its reason ("Unlocks at level 9"), show a finished track as finished, and show how near each unaffordable thing is (a bar filling toward its price).
2. Extending a cottage is a click on a cottage, like paving: a mode that lights the cottages that can be extended, with the price on hover and the suggested one tagged. An extended cottage looks bigger (the three cottage models can stand for the three sizes).
3. Requests: a card that shows who asks, for how much of which good, how much is delivered, the time left as a bar, and the reward; a clear moment and sound when it is met or missed (`request-asked`, `request-met`, `request-missed` in `assets/audio/sfx/town-sounds.json`).
4. The hint: one line of advice under the ledger, and where it points to a road or a plot, a gentle marker there.
5. Work spots: draw working and idle villagers where the simulation now puts them; remove the display-only fan-out.
6. Crowding now matters: a crowded road must be readable at a glance (people bunch up and slow down; a soft warm tint on the road), and the hint says paving would help.
7. Sounds from `assets/audio/sfx/town-sounds.json` for every event that has one, including `cannot`, `extend`, `ui-hover`, `ui-press`, `ui-tab`, `page-turn`, and the mill's loop near the mill.
8. Music: the rotation from `assets/audio/music/playlist.json` with gentle cross-fades and a pause between pieces; "Tide and Toll, Rising" when the next age is rung in.
9. Achievements: a page of the book (the left page when zoomed out, and the Tab key or a bookmark while zoomed in) listing all of them with progress; a note and the `achievement` sound when one unlocks.
10. The charter and "Ring in the next age": a real moment (the charter stone, the bell, the music), and until the second age exists, an honest page that says the first age is complete and what the town achieved.

## D. Checks

- `game-town` must buy at least one of every kind of shop item, extend a cottage by clicking it, pave to gravel and to cobbles, meet a request and miss one, and reach the charter at speed, all through real input.
- A real-input play of the whole age is kept as a check step (the play-tester's scene in `build/pt/r1/game/tests/PlayThrough.cs` is a starting point), reporting purchases and decisions a minute.

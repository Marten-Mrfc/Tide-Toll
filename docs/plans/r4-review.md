# What round 4 must fix and add (the lead's list)

Sources: the real-input play-test of round 2 (`docs/plans/r2-playtest.md`), the bug hunt of the game code (`docs/plans/r3-game-bugs.md`), the round 3 builders' own notes, and the lead's look at the screenshots. Read the two reports: this list is the order of work, not a replacement for them.

## Part A. Fixes (can be done before the simulation's next merge)

1. **Game keys act under the closed cover, on the settings pages and while the book opens.** P switched Pave mode on under the closed book and the next Esc then only left Pave mode instead of pausing. While the game is paused, closing, opening or showing settings, the town's interface takes no keys and no clicks at all. Esc always does one visible thing: leaves a mode if one is on (and the player can see it), otherwise pauses.
2. **Extend and Pave modes end when the game pauses**, and a click on the table beside the closed book does nothing.
3. **Clicks on the ledger, the notes, the request card and the slim strip must not reach the map.** Nothing behind an interface card can be paved, extended or dragged through it. A target brought into view by Pave or Extend must end up clear of the interface, not under a card.
4. **"Ring in the next age" has a hard-coded N key that is not on the Keys page.** Make it an action in `GameInput.cs` like the others, listed and rebindable, with a clash check.
5. **Deleting the save leaves a save.** After Delete the page says there is no save until the next autosave or pause of the new town; Load is not offered.
6. **Autosave counts frame time, which the engine clamps on slow frames.** Count real time.
7. **The pause-time save stores the closed-book zoom and the "moved" flag.** A town continued after quitting from the cover must open at the view the player had before pausing.
8. **After New town**: perk notes stay silenced for the session; the hint marker and a request flash carry over from the old town for some seconds. A new town starts clean.
9. **A save that fails to write is silent, and a good save can be moved aside on a failed read.** Say so in a note when a save could not be written; only set a file aside when it was read completely and is truly unreadable.
10. **Game time is lost on slow frames at high speed; note and banner logs grow without limit.** Fix both.
11. **The summary page's numbers carry stray marks** ("2:" after digits, dots above letters) at the book view. Clean lettering on the page the player reads most.
12. **The Pause control sits over the corner of the book** at the book view and covers the page number; its box has a clipped left edge. Move it clear and fix the box.
13. **The New town question**: both choices look like buttons before the pointer is on them.
14. **The Keys page** scrolls with 25 actions and cuts a row hard at the bottom. Balance the two columns so nothing scrolls at 100 percent, and end a scrolling list on a whole row at other sizes.
15. Build warning CS8604 in `TownVillagers.cs`.
16. **The first scroll feels flat**: the first four wheel notches change the size but not the tilt. Start a little lower (about 50 degrees at the closest view) and let the tilt change from the first notch, still reaching straight down at the full page.
17. **An extended cottage is hard to tell from a plain one.** Each extension must be visible at the normal view: a lean-to or a second gable, a clearly longer roof, a chimney, a fenced yard: use the three cottage models and placement, and say what is missing in art if it cannot be done with them.
18. **A level's banner can cover the very thing it announces.** Place it clear of the gift, or wait for the camera to settle first.
19. Staked plots still use the dark stakes model: the label helps; if `plot-stakes.glb` can be tinted pale in the game, do it.

## Part B. What the game must learn next (after the merge; `docs/plans/town-sim-api.md`, "Added in rules v3", "The second age", "Added in the green pass")

1. **Choices of two.** When a choice is open, a card with the two boons side by side, each with its name, one line on what it does and a small picture or icon; the town runs on behind it; one click or key picks; a clear moment and sound; the boons the town holds are listed on the summary page.
2. **The charter drive**: shown like a request, with its own name and a bar that fills in parts, as the last push of the age.
3. **What each purchase is worth**: every shop line shows "+N coin a minute" where the simulation gives a worth, so a player can compare.
4. **Milestones**: every fifth step of a track is announced with its name and a small flourish.
5. **Extra hands**: the offer of another place at a workplace where people queue appears in the shop and as a marker on that workplace.
6. **Small touches**: woodpiles, washing lines, flower beds, hen coops, benches, beehives, haystacks, apple trees, handcarts and signposts appear by themselves as the town prospers (`assets/models/prop-*.glb`); each pops in gently with a small note at most now and then, never a banner.
7. **The second age, The River** (`docs/plans/age2-design.md`): ringing in the next age is a real page-turning moment with "Tide and Toll, Rising"; the view widens to the riverside; the landing, boats on the river (rowboat and barge, mooring, loading, leaving), the tide in the water's edge and on a dial with the time to the turn, the ford with people waiting at high water, the ferry and its queues, the footbridge, the drawbridge lifting for boats with walkers waiting, the pasture with sheep, the weaver; wool and cloth as loads; the new purchases in the shop under their own group; skippers' requests; the borough seal as the end, with an honest "complete" page. Use the existing river models and the river sounds (`assets/audio/sfx/river-sounds.json`); the old river drawing code in `game/legacy/` may be mined for ideas but the new town is the base.
8. The summary page and the achievements page cover both ages; the right page's title changes from "A Plan of the Hamlet" to a title for the town on the river.

## Part C. Checks

- `game-town` and the whole-age real-input play (`game-playthrough`) cover: picking a boon by click and by key, the charter drive, an extra hand, touches arriving, ringing in the second age, a boat filled, the ford closed at high water, the ferry bought and used, the drawbridge lifting, the seal.
- A second whole-age real-input play for the second age, starting from a saved first-age town, with purchases and decisions a minute.
- Screenshots at 1920 x 1080 of every new thing, read and fixed.

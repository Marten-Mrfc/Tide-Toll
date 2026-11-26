# The message system (version 2, the lead, 20:25 UTC; zones as in `layout-sheet.md` version 2)

Everything the game tells the player goes through one system. Each kind of message has one place (a zone of `layout-sheet.md`), a priority and a length of stay. Messages wait their turn instead of stacking. Only one large one shows at a time. None covers the thing it is about. A delivery or a purchase never raises more than a coin number or a sound. Nothing arrives or leaves in a single frame.

The present state is in `message-inventory.md`: 28 separate places, about 200 notes in a 32-minute age (6 a minute, three stacked, each pushing the others), banners that overwrite each other unseen, cards that appear in one frame.

## 1. The kinds

| Kind | Zone | What goes there | Stay | When two come at once |
| --- | --- | --- | --- | --- |
| **Large** | Message (top centre) | A level reached; the charter drive begins; the charter; the new age; the borough seal; **the choice of two**; **trouble** (a save that failed or could not be read: brick border) | Banner 3.2 s (the charter and the seal 5 s; trouble until clicked). The choice stays until a boon is picked | A queue, highest priority first (trouble 4; charter, seal, new age 3; drive 2; level 1; the choice after the banner of its level). With two or more waiting, each stays 2.2 s. While the choice is open, or a page is open, or the interface is faded out by zooming, banners wait; one that has waited 12 s is shown as a note instead. The choice stays at every zoom |
| **Note** | The ledger's note section (two lines, 90 characters at most) | An achievement (with its icon); a building opened; a perk; a milestone; a boon chosen; one small touch in four; a banner that could not wait | 3.2 s | A queue, one at a time, highest priority first (achievement 3; building, milestone 2; the rest 1). With four or more waiting, each stays 2 s; beyond six waiting, the lowest are dropped |
| **Goal** | The ledger's goal section and strip | A request from the town or from a skipper (goal section). In the strip, in this order: after the charter the button "Ring in the next age [N]" until it is rung; the charter drive; a second request in one line | As long as it runs. Met: the section turns moss and says "Done: +160" for 1.6 s, then empties. Missed: "Too late" in brick for 1.6 s, then empties | The goal section keeps the request it has; a second one goes to the strip and stays there (nothing changes place while it runs). A third waits |
| **Line** | Line (bottom centre) | The answer to a click that could not be done ("30 short", "The bell rests 31 s"); what the click mode in use expects; a first-minute hint; the town's hint (with its ring on the map) | A refusal 2.5 s; a mode's line while the mode is on; a hint at least 4 s and as long as it is true | One at a time, in that order of priority. A change of text is a cross-fade |
| **Coin number** | On the map, at the building | A delivery, a landing sale, a toll | 1.4 s, rising 24 | Deliveries at the same building within half a second become one number. Eight at most. The setting that turns them off stays |
| **Tag** | On the map | A staked plot's name, a boat's cargo, "+ hand", "Suggested" | While true | A tag that would touch a zone or another tag fades out, and stays out for at least half a second (no blinking) |
| **Tip** | At the pointer | The price of what a click would do in Pave or Extend mode | While hovering | One |
| **Tooltip** | Beside its owner | What the thing under the pointer does and is worth | While hovering or focused, after 0.25 s | One |
| **Page** | Page (centre; the map dims, the rails stay usable) | Achievements; the age pages | Until closed | One; opening one closes the other |
| **State** | In its own control | A price pill, the bell's clock, the tide dial, the purse's lines | Always | Text changes in place; a control never changes size because its text did |

## 2. What no longer raises a message

These said what the player had just done or could already see. The thing itself shows it, with its sound:

| Was a note | Now |
| --- | --- |
| "A cottage is extended", "A road is cobbled", "A new villager moves in" | Nothing: the player did it and sees it (the purchase sound and the small reward on the map stay) |
| "New to buy: ..." | The new row fades into the shop and pulses once |
| "A road has worn in. It can be paved now." | The Pave row's pill changes from "No worn road" to its price and pulses once |
| "... asks for 12 Grain", "... is pleased", "Too late for ...", a boat's request, "The charter drive is met" | The goal card and strip say it in their own place |
| "A choice of two boons is open at the top" | The choice card is the message |
| "A boon for good: ..." (was a banner) | A note |
| The Pave and Extend banners, the hint inside the ledger, "Scroll out to see the whole book", the refusals | The line |

That brings a first age from about 200 notes to about 70 (two a minute).

## 3. Motion and sound

- Every arrival and leave is a `BookMotion` timeline, like the book's pages: large messages 0.22 s (fade and 12 px down from above), notes 0.2 s (fade and 8 px in from the left), the goal's and the strip's content 0.22 s (fade and 8 px up), the line 0.2 s, coin numbers fade in over 0.1 s, tags and tips 0.15 s, the tooltip 0.12 s. Between two messages in the same zone there is a gap of 0.15 s.
- A message can be clicked away (banners and notes); the next one follows.
- Rows of the shop that come or go fade over 0.2 s and the rows below slide to their place over 0.2 s. No zone changes size or place.
- The purse's "Next" line is always one line (cut with an ellipsis; the whole text is in its tooltip), so the purse never changes height.
- **Sound follows the message:** a message's sound plays when the message appears, not when the event fires, and two large sounds are at least 0.6 s apart (today four of them share one player and cut each other).

## 4. The first minute teaches itself

Four hints in the line, each shown once ever (remembered in the settings file, not in the save), each leaving as soon as the player has done what it says:

1. At the start: "Your villager fetches water and carries it home. Every trip earns coin." (until the first coin, 12 s at most)
2. When the first purchase can be afforded: "You can afford your first upgrade. Click it in the shop, or press its key." (the row pulses; until bought)
3. After the first level banner: "Each level gives the town something new. The purse shows what comes next." (6 s)
4. After 75 s of play, if the player has not zoomed out: "Scroll out to see the whole page, and the book." (until zoomed out, 8 s at most)

## 5. Tests

`game-motion` gains, for every kind: arrival and leave are continuous and eased (the same bounds as the book's transitions), nothing is switched on or off while it shows, the lengths of stay, one at a time per zone with the queue's order, the same lengths at 30, 60 and 120 frames a second, and no allocation or CPU spike per frame. A scripted "busiest moment" (a choice level with a request running, a banner due and three achievements) must show at most one large message, one note and one line at any frame. `game-layout` checks that no message lies outside its zone.

# Round 3 game part 1 progress

Started 2026-11-25 22:47 UTC (the machine clock; it runs oddly slow). Time box ~55 min.

## Done
- Baseline: all five game tests passed on rules v2.2 before any change (the old test only reached level 4).
- Shop (TownHud.Shop.cs): grouped People / Work / Roads / Town, own key per bookmark (1-9, 0, E for extend, F G H for offers; new actions in settings/GameInput.cs), locked and finished states, bar toward the price, Pave bookmark not-yet state.
- Pave (TownHud.Pave.cs): one picking for hover and click (PaveTarget, only roads payable and pavable now, on Road.Px/Py), hover recomputed each frame, leaves after a pave (Shift keeps it), brings the suggested road into view and gives the view back.
- Extend mode (TownHud.Extend.cs), request card + hint line + marker (TownHud.Request.cs), level banner + Home control (TownHud.Banner.cs), notes max 3, perks once, gold coin numbers, request/extend sounds.
- Villagers: display fan-out removed (work spots from the sim).
- TownHud.Scale renamed UiScale (CS0108).

## Next
- ground on Px/Py, cottages by extensions, stakes, crowding, loads, colours; tests; screenshots; ./check
- 23:07 game-town rewritten and PASSING: every kind bought by level 6-7 through clicks and keys, Extend by clicking a cottage, Pave by clicking, request card and hint checks, 30 end-of-age Pave clicks (15 tilted, 15 flat) each on the tip's road (30 of 30 meant).
- Found and fixed a real bug: the Pave click marked the mouse release handled, so the camera never saw it and the next mouse move dragged the map.
- Ground draws paving on Road.Px/Py; worn earth reaches the door; gravel softer; crowding tint stronger; cottages sized by extensions; staked plots labelled; follow view frames what is built; loads bigger; clothes stronger; interface fades before the left page.
- MenuTest: setup now runs the bot until a road can be paved AND paid for (Pave mode opens only then).
- 23:15 book, menu, save, settings, perf, smoke pass on their own; quick screenshots read (level 6, pave, extend, request); Pave band now one mitred strip (the per-segment quads left stripes); stake labels moved below the plot (they crossed the cord, then hid under the banner).
- Settings: Keys page rows 32 px when there are more than 18 actions, right column two rows shorter (25 actions now). Offer keys F, G, H (B is used as a free key by the settings test).
- SaveTest: added a check that a save from the first rules (town save version 1) is set aside.
- Running the full ./check.

## Not done (for the lead / the next agent)
- C7-C10 and D belong to the next agent. Request met/missed have a sound and a note (C3) but no other moment.
- B2 cord: the stakes model is unchanged (assets/ is not mine); only the label is new. Pale wooden stakes need an asset change.
- A2: Pave and Extend bring the target into view only when it is off screen (not merely under a card): a view that moves under a still pointer is worse.
- 23:25 full ./check PASSED (table in the final report). Screenshots read: first-minute, level-6, pave, extend, request, end-of-age, zoom-2, settings-keys.
- After it: stake label raised (its foot was clipped by the ground), Extend at the last level shown as finished ("Unlocks at a later level" never comes; sim wish written). Second full ./check running.
- Keys page: the left column scrolls (it did before with fewer rows too); the settings test's fit check passes. A two-column rebalance is left for the next agent if wanted.

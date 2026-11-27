# The right side, version 3: progress (branch `wt/n4-shop`)

Built to `docs/plans/n4/shop-sheet-v3.md` and its mock-up. The branch was fast-forwarded first to `da09a81` (the
sheet and the mock-up were committed after the worktree was cut). How the game plays is unchanged: same keys, clicks,
prices, save; `sim/` untouched.

## What changed, file by file

- `game/scripts/town/ShopRow.cs`: the row rebuilt. Icon (24) with its key cap under it, centred on it (no key column);
  name in one size for the whole shop, never resized row by row, never cut (`NoTrimming`); line 2 always present;
  the pill: four states only, coin sign at the left, price right-aligned in tabular figures, "On"/"from 12"/"–" centred.
  "Not yet" is paper with a wheat wash (wheat at half strength) drawn by `PillWash` as one polygon inside the pill's
  capsule (nothing outside the outline), under a separate 1-px frame; blocked is paper, frame ink 30 percent,
  disabled-ink price, coin at 40 percent, icon and name in the disabled look. `DashFrame` is gone. The row draws its own
  band (`SetBand`): hover ink 7 percent, pressed ink 14 percent with the content 1 down, none when blocked. Compact rows
  (40): icon 20, the same name font and size, pill 28 tall, no line 2, no key cap.
- `game/scripts/town/TownHud.Shop.cs`: display groups People / Building / Work / Town / The river (`GroupOf(kind,
  secondAge)`), inside a group by key (`KeyRank`, stable insertion sort, no allocation), so the keys read 1 2 3, 4 5 F G H,
  6 7 8 9, 0. Line 2 says one thing (click to make, why blocked, worth, or what it does); "N short" is off the row.
- `game/scripts/town/TownHud.ShopText.cs` (new, split from Shop.cs): row names, the name catalogue
  (`RowNameCatalogue`), short forms for the narrow rail (`ShortRowNames`: "Better boat prices" reads "Boat prices" only
  where even caption would not fit it), "Mill hand" for "A third hand at the mill" (the tooltip title keeps the whole
  name), line-2 texts (`BlockedLine`, `DoesLine`), pill money (`PillPrice`: whole up to 99,999, then "123k", "1.2M",
  never rounded up; `PaveFace`), tooltip sentences.
- `game/scripts/town/TownHud.ShopRows.cs`: the card. Header "Shop" / "3 to buy" (with the down mark when one is
  scrolled away). Headings are the name and a hairline (ink 16 percent) only, shown only while the group has a row, and
  fade and slide like rows. The card's bottom edge eases with the rows' slide (`ShopSlide`) and carries the foot. The
  heading notes ("1 done", "Basket at level 3 · +1 more") are gone. The name size and the pill width are decided once
  per layout from the whole catalogue (`NameSizeFor`, `DecidePillShort`): body on the 320 rail; on the 272 rail caption
  for every name. The pill is 96, or 104 on the 320 rail when "99,999" in bold tabular figures would not fit at 96 (it
  does not, so it is 104 there); 88 on the 272 rail, where prices shorten from 10,000 ("12k").
- `game/scripts/town/TownHud.ShopFoot.cs` (new): the foot line "Next: Basket at level 3 · +1 more" (lowest level, first
  in list order, "+N more" for the others at that level), only while something is locked; its tooltip lists every locked
  purchase with its level, then the finished ones. The rows' band each frame.
- `game/scripts/town/TownHud.ShopTip.cs` (new): the row tooltip card (296): title (name and step, body bold) with key
  cap(s) at the right (Pave: 4 and P), one sentence, hairline, facts in two columns: Worth (with "pays back in 44 s" /
  "18 min"), Price, Short (only when not affordable), Why (only when blocked).
- `game/scripts/town/TownHud.Tip.cs`: rows use the card; the plain tip shrinks to a one-line text's width; heading tips
  gone, the foot has one; navigation tips "Achievements · Tab", "The age's page · N", "Centre the view · Home",
  "Pause · Esc".
- `game/scripts/town/TownHud.Timers.cs`: navigation buttons hold their icon (26) centred and nothing else; Achievements
  its icon and "2 of 16" (body, bold). No key caps. Disabled: border 1 at 40 percent ink, icon at 40 percent.
- `game/scripts/UiKit.Styles.cs`: `TabRegular`/`TabBold`, font variations of `UiKit.UiFont`/`UiBold` with OpenType
  `tnum` (the font-loading lines of `UiKit.cs` are untouched); `StyleRow` is air in every state.
- `game/scripts/town/TownHud.River.cs` (one line): a skipper's "Loaded 460 of 3264, leaves in 100 s" takes the form
  "..., 100 s left" when it would not fit (game-layout found it 2 px too wide in u38 at 1280x720@150).
- `AGENTS.md`: the shop line names the new groups and files.

## The sheet, item by item

| Item | State |
| --- | --- |
| 1 slashed zeros | not this job (the font brief, other builder) |
| 2 pill line / dashed frame | done: wash clipped to the outline, no line under a pill, no `DashFrame` |
| 3 keys out of order, holes | done: groups and order by key; no key column |
| 4 icons touching the cap | done: the cap is under the icon, the icon alone at the left |
| 5 one name smaller | done: one size per layout from the catalogue, never cut |
| 6 rows without a second line | done: line 2 always says one thing |
| 7 headings without rows, notes | done: heading only with rows, name and hairline; one foot line |
| 8 compact rows look foreign | done: same name font and size, icon 20, pill 28 |
| 9 key cap in the navigation | done: icon centred, nothing else |
| 10 tooltip one sentence | done: the card |
| 11 prices start at the left | done: right-aligned, tabular figures |
| Header "3 to buy" | done |
| Hover / pressed / blocked band | done |
| Rows, headings, bottom edge and foot ease together | done (the foot rides the bottom edge; when the foot itself appears the card grows over 0.2 s while the foot stands at the bottom) |
| Navigation tooltips and disabled look | done |
| Tests 1 to 7 and three tour states | done (below) |

## Tests

Replaced (old assertion, new assertion, why):

- `LayoutFixture` prices: old "9.99M" / "from 9,999" written into every pill, set in caption when it did not fit; new:
  the widest price `PillPrice` can make (99,999 / 999,999 / 9,999,999 / 99,999,999, widest of them) laid out by the
  row itself (`SetPrice`; Pave through `PaveFace`) at body size, and it must fit. Why: the sheet forbids a smaller price.
- `LayoutFixture` line 2: old "+120 a minute · 49,999 short" (worth dropped when it did not fit); new: the longest
  line 2 "No room in the cottages", and `CheckCatalogue` measures every line-2 text the rows can show. Why: line 2 says
  one thing and no longer carries "short".
- `TownTest.V3` (extra hands): old: the row's text contains `T.HandName(w)`; new: it contains
  `TownHud.HandRowName(T.HandName(w))` ("Mill hand") and the row's tooltip title is `T.HandName(w)`. Why: the whole
  name does not fit a row at body size; it is the tooltip's title now.

Added (`game/tests/LayoutRules.Shop.cs`, run by game-layout on every state and canvas): S1 keys ascend down the
visible list and each cap sits under its icon; S2 every tall row has a non-empty line 2 without "short", every name in
the shop's one font and size, not cut, in the catalogue; with the fixture, every catalogue name fits its box (body on
the 320 rail) and every line-2 text fits; S3 no heading without a row, headings hold only name and hairline, one foot
line; S4 every pill part inside the pill, the pill clips, every wash point inside the capsule, no dashed frame anywhere,
the pill's look matches its state, prices at body size; S5 compact rows show their icon, no cap or line 2, same name
font; S6 no key cap in a navigation button, icon centred within 1; S7 every row's tooltip has a title, a sentence and
facts, and no "Key:". game-layout also prints the first three failures of every rule now. Tour (`UiAudit.Tips.cs`):
u46 pointer on an affordable row (band and card), u47 the button held on it (released off the row, nothing bought), u48
pointer on a blocked row.

Check steps run in the worktree (commands of `tools/check.sh`):

All passed (logs in `build/n4/logs/`, not committed). game-perf was run as well, as it gates per-frame allocation.

| Step | Result line |
| --- | --- |
| game-build | pass, 0 warnings, 0 errors |
| game-smoke | `SMOKE PASS renderer=mobile game_s=30.0 coin=4 trips=1 uploads=75 slow-frame=384steps` |
| game-town | `TOWN PASS faint=0.40 maxstep=0.009 worn=0.95 paved=0 boonclick=Wider lanes extended=0 level=6 bought=50 ...` |
| game-playthrough | `PLAY PASS charter=33.9min buys=201 paves=24 extends=14 bells=76 clicks=204 keys=102` (unchanged: 33.9 min, 201 purchases) |
| game-river | `RIVER PASS pause2=ok cover=960,540 lanes=...` |
| game-layout | `LAYOUT PASS states=36 canvases=8 checks=73325 failures=0 loud<=2 quiet<=3` |
| game-book | `BOOK PASS villager_px=39.9 pitch=53.0 size=46.0 paved=0 notches=14 town=Dunstead hint=1` |
| game-settings | `SETTINGS PASS sound display(18 seam calls) keys saving reopen damaged keyboard(181 keys) fits(...)` |
| game-menu | `MENU PASS pause-mark 48px->72px cover-keys=ok ...` |
| game-motion | `MOTION RATES PASS`; at 60 fps `MOTION PASS impatient=ok pages-align=0.00px transitions=13 alloc-max=5672B cpu-max=5.42ms` |
| game-save | `SAVE PASS autosave-clock=0.63/0.63s level 2 paved 1 villagers 4` |
| game-perf (extra) | `PERF PASS`; 1x bytes/frame 545 (gate 1536), 8x 738 (6144), river 1x 1224, 8x 1592 |
| ledger | `ledger: 465 rows, 439 asset files checked, 0 problems` |

On the way, game-layout failed twice and was fixed, not loosened: the Achievements count "40 of 40" was 2 px too wide
at 2560 x 1440 (box widened), and a skipper's "Loaded 460 of 3264, leaves in 100 s" 2 px too wide in u38 at
1280x720@150 (short form when it does not fit).

## Pictures

In the worktree, not committed: `build/n4/shop-1080/` (u01 u02 u03 u05 u07 u08 u10 u11 u13 u16 u30 u32 u33 u36 u42
to u48), `build/n4/shop-720/` (u13, u33), `build/n4/shop-1080-150/` (u06, u13), `build/n4/shop-720-150/` (u13),
`build/n4/shop-1080-80/` (u13). All looked at, right side at full size. Seen and fixed on the way: the count "22 of …"
cut, a tooltip sentence cut, "pays back in 1063 s" wrapping (now minutes), "10k" for 10,697 on the wide rail (pill
104 now, whole numbers to 99,999).

What I still see:
- The zeros are slashed (the font job; not touched here).
- The pill is 104 wide on the 320 rail, not 96: "99,999" in bold tabular figures does not fit 96 (the sheet allows up to
  104); the name box stays wide enough for every catalogue name at body.
- u32 (the longest list, 14 rows and 5 headings) goes compact at 1080: tall rows would need 1032 of the 976 the card
  may have. Nothing overlaps the navigation.
- When the foot line itself appears or goes (rare: the first locked purchase, the last one unlocked), the card's edge
  eases over 0.2 s while the foot already stands at the bottom, so for those frames the foot can cover the last row's
  lower part.
- On the narrow rail the foot "Next: Extend a cottage at a later level" does not fit and reads "Next: Extend a cottage".
- u46, u47 and u48 (hover, pressed, blocked) are pictures only: under game-layout's headless run the rows do not count
  as in view for the pointer, so those states are not made there (as u42 already was; u48 prints "no blocked row yet").
  The rules S1 to S7 still run on every other state.
- At 1280x720 at 150 the shop scrolls (Regulars below); whole rows only, as before.

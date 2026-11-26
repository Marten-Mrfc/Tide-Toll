# Assets round 2, track BK (the book, the table, the small things of the town): progress

Checkout `/home/user/tide-toll/build/wt/n3-art2`. Pictures: `/home/user/tide-toll/build/n3/assets-audit/r2-bk/`.

## The paper colour on screen (for the interface track)

`BookBuilder.PaperOnScreen` (game/scripts/BookBuilder.Desk.cs) = **#F2EAD8** (Palette.Paper). `paper.gdshader`'s custom
light() keeps a lit page exactly Paper (its grain averages out to it); only the gutter shade (vertex colour, down to
0.64 at the spine) and shadows darken it. The turning leaf should be #F2EAD8.

Print on the page: `BookBuilder.PageInk` = Ink #2B2620; `BookBuilder.PageInkSoft` = Ink mixed 30 percent toward Paper
(#7B756A), used for every line the summary printed in FadedInk (the "Done" / "0 of 200" column, "Next", the age line).

## How to resume

```
cd /home/user/tide-toll/build/wt/n3-art2
PYTHONPATH=assets/src python3 assets/src/2d/book-desk.py          # table-wood.png, book-leather.png
PYTHONPATH=assets/src python3 assets/src/models/desk.py           # desk-inkwell, -candle, -seal, -ribbon
PYTHONPATH=assets/src python3 assets/src/models/town-props.py prop-flower-bed prop-hen-coop prop-signpost prop-handcart
PYTHONPATH=assets/src python3 assets/src/models/town.py           # plot-stakes (and bell-post, charter-stone unchanged)
PYTHONPATH=assets/src python3 assets/src/models/fence.py; PYTHONPATH=assets/src python3 assets/src/models/keeper-hut.py
PYTHONPATH=assets/src python3 assets/src/models/river-town.py pasture sheep
python3 tools/merge-ledger-rows.py --build                        # rows into assets/LEDGER.csv (drop stale scratch rows first)
tools/asset-audit.sh --only=book-open,book-closed,book-desk,book-print --out=/home/user/tide-toll/build/n3/assets-audit/r2-bk --skip=flat,sounds,sheets   # ~3 min, no bot run
tools/asset-audit.sh --no-town --only=<models> --out=... --skip=flat,sounds,sheets
tools/asset-audit.sh --only=town-plot-stakes,town-charter-stone --out=... --skip=flat,sounds,sheets   # bot to the charter only
```

Audit tool additions: book-only `--only` lists skip the bot (the book is pictured on the new town at once); new
subjects `book-desk` (normal, `-close` at the ink pot, `-right-close` candle, `-seal-close`), `book-print` (the
summary's print at the pixel density of the game's 1920 x 1080 book view, and closer), `town-plot-stakes` (the first
staked plot in the town), `town-charter-stone` (the bell green after the charter); a town `--only` list that is all
first-age subjects stops after the charter.

## Done

| Item | State |
| --- | --- |
| 1 Table | `table-wood.png` (book-desk.py): five planks a tile in Packed earth tones, wandering grain bending round knots, two butt joints, Ink gaps, soft Trail earth wear; triplanar on the table top (800 x 500 m a tile), lifted 15 percent (lamp-lit). Desk things (desk.py, scaled 10x, cm units): ink pot and goose quill top left, candle in a brass chamberstick top right, seal stamp with wax stick and a pressed seal bottom right, a loose ribbon bottom left; all outside the open book and the shut cover. The old seams are gone; the old inkwell is replaced (node name "Inkwell" kept) |
| 2 Leather | `book-leather.png`: Soot brick with pebbled grain, edges rubbed toward Brick, blind-tooled double frame at 6 and 9 m (dotted between), a single blind line at 22 m with diamond fleurons; mapped once over each board (binding and front board). BookCover.Dress.cs: four Thatch corner pieces with rivets, a strap clasp with a Thatch catch at the fore-edge, five raised spine bands. Gilt rules untouched. The bookmark ribbon is a cloth strip with a fold shade that runs from the gutter over the page foot onto the table and ends in a swallowtail (node "Ribbon", same place) |
| 3 Paper | print darker (PageInkSoft for the faded lines, 3 px same-colour outline on Alegreya print up to 7.6 m em), gutter shade 0.74 -> 0.64 over a 40 m wide band (was 29 m). PaperOnScreen constant |
| 4 Plot stakes | slim pale stakes with Paper caps, a Brick pennant (NW) and cloth tag (SW), soft ink; the rope runs down from each lashing and lies flat on the ground as a Limewash line (no depth edge, so no black frame). TownBuildings fits the parts to the plot (stakes unstretched, ropes scaled along their side) |
| 5 Props | flower bed (stone-edged bed, leaf cushions, 14 round blossoms), hen coop (Thatch roof with ridge course, pen of posts and rails, three round hens), signpost (0.28 m post, 1.2 m arms with Brick tips, a lantern), handcart (Limewash sack with a tie, Brick wheels), wattle fence (woven withies over and under three stakes, 1.1 m), fence post (tapered top, Paper tip, lean), keeper's hut (a 0.6 m stone chimney, smaller bell-cote roof), sheep (fleece of three round lumps with Paper tops, Soot brick face and ear tabs, Mud legs), pasture (a thatched field shelter) |
| 6 Charter stone | TownBuildings: the PaleStone override is dropped for the real model (kept for the stand-in); `CharterPale` (PlayThrough's check) replaced by the equal check for the new model (see below) |

## Changed assertions

- PlayThrough "the charter stone is pale dressed stone": `TownBuildings.CharterPale` was "the PaleStone override is set".
  The override is what hid the Brick seal, so it is now: the stand-in under the pale override, or the charter-stone model
  with no override over its own Concrete stone (lighter than Meadow). The test line itself is unchanged.
- TownTest "the stakes are pale" is unchanged; the stakes' MaterialOverride is now the model's own (soft ink) palette
  material, whose albedo is white (the colours are the pale Trail earth / Paper / Limewash of the atlas).

## For the lead

- `RiverLayer.cs` (not mine) stakes river plots with the whole `plot-stakes` model scaled to the plot: with the new
  model the stakes are stretched a little but the rope still lies on the ground. Better: use the same fitting as
  `TownBuildings.FittedStakes` (could be made `internal static` and called from RiverLayer.MakeStakes).

## Pictures read (r2-bk, 22:53 to 23:21 UTC)

- book-desk-normal / book-open-normal: a warm mid-brown plank table with grain, knots and joints round the book (was a flat
  near-black); the ink pot and quill at the top left and the seal at the bottom right enter at the view's edges; the
  bookmark runs off the page foot onto the table. book-desk-close: a dark glass ink pot with a brass collar and a glint, a
  goose quill (smooth vane, dark bar near the tip). book-desk-right-close: brass chamberstick and candle (the flame is
  small from above). book-desk-seal-close: stamp, wax stick, pressed red seal.
- book-closed / book-leather: grained Soot brick leather, rubbed lighter edges, blind double frame with dotted line, inner
  blind line with diamond fleurons, brass corner pieces, clasp at the fore-edge, five spine bands; gilt rules as before.
  Faults: at the normal view the blind tooling is faint (the board still reads mostly as one red-brown field); the corner
  pieces' diagonal plate overhangs the board's corner a little.
- book-print (the summary at the 1920 x 1080 book view's density): names in Ink, values in PageInkSoft, all readable (the
  before picture's "Done" column was near the paper colour).
- town-plot-stakes: four pale capped stakes and a pale line round the plot; the black yard is gone (the rope on the ground
  still carries a thin ink edge on each side).
- town-charter-stone (after the charter, bell green): the 3.4 m stone with its red seal stands on its paved patch in front
  of the bell frame, facing the camera; found at a glance. Before: a pale blob behind the frame.
- Props (normal and close): flower bed reads as flowers on a stone-edged bed; hen coop thatched with a pen (the hens are
  still blocky close up); signpost stout with red tips and a lantern; handcart red wheels and a tied white sack; wattle
  shows the over-and-under weave (close up it can read as crossing rails); post tapered with a pale tip; keeper's hut has
  a stone chimney; sheep are three-lump white fleeces with a dark face; pasture has a thatched shelter. All props are
  small at the normal view (true size), as before.

## Tests while working (23:03 to 23:11 UTC)

game-book BOOK PASS, game-town TOWN PASS (includes "the stakes are pale"), game-menu MENU PASS, game-save SAVE PASS.
tools/motion-check.sh: every check passed except the CPU gate under load (60 fps question-in frame 8 14.5 ms; 30 fps
tab-keys-display 6.12 ms; 120 fps pause 7.5 ms; gate 6 ms; load average 22 to 50).

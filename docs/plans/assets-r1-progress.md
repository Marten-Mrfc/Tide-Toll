# assets-r1 progress

Started: Wed Nov 25 20:34:24 UTC 2026 (time box 45 min, until about 21:19 UTC). Last update: 20:42 UTC (date -u).
(Two earlier entries in this file carried guessed times, 20:55 and 21:05; they were wrong and are replaced by this one.)

## Status: ALL DONE

- Models, all from `assets/src/models/town.py` (one command: `PYTHONPATH=assets/src python3 assets/src/models/town.py`;
  build-assets.sh discovers it by itself, so the script needed no edit). Re-running it gives byte-identical files.
  - `mill.glb` 638 tris (budget 1500). Nodes `Body` and `Sails`; Sails origin = hub (0, 5.6, 2.25), spins about local Z.
    Base 6 x 6 m; sails reach 4.6 m from the hub (lowest point 1.0 m above ground), so the whole thing is 8.2 m wide.
  - `bell-post.glb` 336 tris (500). Nodes `Frame` and `Bell`; Bell origin = hanger (0, 2.72, 0), swing about local X.
    Footprint 4 x 2 m, 3.74 m high.
  - `plot-stakes.glb` 240 tris (300). For an 8 x 6 m plot. Nodes `stake-nw/ne/sw/se` (origin at the stake foot,
    translated to (+-4, 0, +-3)) and `cord-n/s` (8 m along local X) and `cord-w/e` (6 m along local Z), origin at the side
    middle. Other plot sizes: move the stakes, scale each cord along its own axis only.
  - `charter-stone.glb` 121 tris (300). 2 x 2 m, 1.9 m high, carved panel on the +Z face.
  - `villager-cargo-water.glb` 84 tris (100). Same form as the other cargo (one node, attach at (0, 2.6, 0)), but the
    buckets hang BELOW the origin to y = -0.72 and the yoke is 1.72 m wide.
  - `villager-cargo-flour.glb` 80 tris (100).
- Contact sheets (with side views) saved and looked at: `assets/src/contact-sheets/{mill,bell-post,plot-stakes,
  charter-stone,villager-cargo-water,villager-cargo-flour}.png`. The water sheet hides the buckets under the sheet's
  ground; `build/contact-sheets/villager-cargo-water-lifted.png` (preview glb in build/previews/) shows them.
  Changes after looking: bell roof narrowed so the bell shows from above; stakes and cord made thicker and paler.
- Icons, all from `assets/src/2d/icons-town.py` (reuses Icon from icons.py like icons-river.py): 17 SVGs, each passes
  `svg.lint(icon_style=True)`. Preview `assets/src/2d/previews/icons-town-preview.png` looked at on Paper and Ink;
  ui-keys and good-grain redrawn after the first look (accents merged, kernels too thin on Ink).
- Ledger: 23 rows appended to `assets/LEDGER.csv` (copied from build/ledger-rows/, same form as the river rows).
  `python3 tools/check-ledger.py` and `--fragments --build`: 0 problems.
- Gallery: `docs/asset-gallery/build.py` had hard-coded sections, so I added a "Town set" section (models + icons) and
  regenerated with `python3 docs/asset-gallery/build.py`. Existing media unchanged; 24 new media files.

## Not done / for a successor
- `tools/build-assets.sh` was not run as a whole (it rewrites every existing asset; other agents were working).
- `./check` not run by me (the Godot part belongs to the game agents).

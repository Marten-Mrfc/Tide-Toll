# assets-props progress

Started 2026-11-25 23:57:31 UTC. Time box 25 min. Finished 2026-11-26 about 00:03 UTC.

- [x] read docs/asset-pipeline.md and assets/src/models/river-town.py
- [x] generator assets/src/models/town-props.py (deterministic, no randomness); all 10 props under 150 tris
- [x] contact sheets rendered (--sides, view 5 m; haystack and apple tree 6.5 m) and looked at beside cottage-01
      Redone after looking: woodpile (square logs read as bricks -> hex logs with pale end grain), washing line
      (cloths invisible from above -> folded tent shape), hen coop (hens specks at cottage scale -> drawn 1.5x)
- [x] 10 ledger rows appended to assets/LEDGER.csv; python3 tools/check-ledger.py passes
- [x] gallery: "Town props" section in docs/asset-gallery/build.py, rebuilt (7 s)
- [x] docs/asset-pipeline.md: one "Town props" paragraph

Decisions: hens drawn 1.5x like sheep and villagers; apple tree written with ink="soft" like the other trees;
handcart shafts rest on the ground to the south (+Z); signpost arms are blank boards.
Known nits: haystack thatch cap shows small ink ticks at facet creases; apple windfall sits 0.05 m into the ground.

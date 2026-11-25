# assets-r1 progress

Started: Wed Nov 25 20:34:24 UTC 2026 (time box 45 min, until ~21:19 UTC)

## Done
- nothing yet

## Next
- read docs, existing generators

## For a successor

## 20:55 UTC
- Done: assets/src/models/town.py writes all six models (mill 638 tris, bell-post 336, plot-stakes 240, charter-stone 121, cargo-water 84, cargo-flour 80). build-assets.sh auto-discovers *.py in assets/src/models, so no script edit needed.
- Next: contact sheets, then icons (assets/src/2d/icons-town.py, reusing Icon from icons.py like icons-river.py), then ledger rows.

## 21:05 UTC
- Done: contact sheets saved in assets/src/contact-sheets/{mill,bell-post,plot-stakes,charter-stone,villager-cargo-water,villager-cargo-flour}.png and looked at; bell roof narrowed, stakes and cord made chunkier after the first look. The water sheet hides the buckets under the sheet's ground (they hang below the origin); build/contact-sheets/villager-cargo-water-lifted.png shows them.
- Next: icons (assets/src/2d/icons-town.py), ledger rows, check-ledger.

# Age 2 assets: progress

Started 2026-11-25 22:19 UTC (time box 30 min). Finished 22:27 UTC.

Generators: assets/src/models/river-town.py (6 models), assets/src/2d/icons-age2.py (14 icons). Both deterministic
(re-run gives identical bytes, checked with sha1sum).

## Status
- [x] models generator written and run (all within budget)
- [x] contact sheets rendered and looked at; sheep head enlarged after the first look; saved to assets/src/contact-sheets/
- [x] icons generator written, all 14 pass svg.lint(icon_style=True), preview looked at; good-wool redrawn twice
      (overlapping circles read as a face, then as a cog), ui-tide-high simplified (two waves + up arrow)
- [x] 20 ledger rows appended to assets/LEDGER.csv; python3 tools/check-ledger.py: 0 problems
- [x] gallery: new "Second age" section in docs/asset-gallery/build.py, regenerated (additions only)
- [x] docs/asset-pipeline.md: short "Second-age models and icons" note

## Decisions
- Sheep drawn 1.5x like villagers (1.0 x 1.0 x 2.0 m). Pasture 16 x 16 m with the gate gap at x = -1..1 on the south side.
- Seal stone 4 x 4 m (2 x 2 cells), 248 tris; budget 600 (a landmark, allowed up to 1500).
- Weaver cottage roof footprint 8.1 x 6.0 m (0.1 m over cottage-02's 8 m in x, accepted).
- Crane frame 4.2 x 3.0 m with the treadwheel; Jib at rest points +Z.

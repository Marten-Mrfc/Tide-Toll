# Second-age sound effects: progress

Started 2026-11-25 22:27 UTC (date -u). Time box 25 min (to about 22:52 UTC).

Plan (decided, nobody to ask):
- Voices in a new helper `assets/src/audio/_river2.py` (built on _synth, _river, _town; nothing existing changed).
- Generator `assets/src/audio/river-sfx.py` writes moor x3, cast-off x2, quay-drop x4, tide-turn x2, loom x3,
  sheep x3, crane x2, toll x3, seal x1 (seeds 1000-1099), via `_town.finish` (level, check, ledger row).
- `assets/src/audio/river-zz-sounds-json.py` writes `assets/audio/sfx/river-sounds.json` (same form as town-sounds.json).
- Ledger rows appended to assets/LEDGER.csv from build/ledger-rows/audio.csv (only my files).

Status:
- [x] helper + generator written
- [x] rendered, all files pass A.check
- [x] river-sounds.json
- [x] ledger rows + check-ledger
- [x] measurement table, determinism re-render
- [x] note in docs/asset-pipeline.md

Finished 2026-11-25 ~22:33 UTC. 23 WAVs + river-sounds.json written, 24 ledger rows appended to assets/LEDGER.csv,
check-ledger passes (also --build), every file passes A.check (TP under -1 dBTP, starts/ends at zero), a second
render is byte-identical (md5). Generator file is river-sfx.py (not river-sounds.py as first planned).
Decisions: river-sounds.json lists only the nine new events (older river sounds left out); volumes vs coin:
moor +1, cast-off +2, quay-drop 0, tide-turn +1, loom -1, sheep -2, crane 0, toll +1, seal +4.5 (charter is +4).
Tide-turn tuned A3 A3 D3 / D3 D3 A2; cast-off horn D4-A4 / A3-D4; seal hum D5 A4 E4 D4.

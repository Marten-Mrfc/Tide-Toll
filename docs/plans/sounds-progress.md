# Sounds progress (incremental town SFX)

Started 2026-11-25 21:10 UTC, time box 35 min. Status: FINISHED (about 21:20 UTC by `date -u`).

## Done
- Voices: assets/src/audio/_town.py (new; _synth.py and _river.py unchanged, so existing outputs unchanged).
- Generators (found by tools/build-assets.sh as they are; no change to the script was needed):
  town-tools.py (buy x4, cannot x2, extend x3, pave-gravel x3, pave-cobbles x3), town-bells.py (level-up x2,
  gift x3, achievement x2, request-asked x2, request-met x2, request-missed x1, charter x1), town-ui.py (ui-press x4,
  ui-hover x2, ui-tab x3, book-close x2, book-open x2, page-turn x3), mill-loop.py (mill-loop.ogg, 6.000 s stereo,
  seam checked on WAV and decoded Ogg), town-zz-sounds-json.py (town-sounds.json; named zz so it runs last).
- 46 rows appended to assets/LEDGER.csv (44 WAV, 1 Ogg, 1 JSON); python3 tools/check-ledger.py: 0 problems.
- Rendering checked deterministic (md5 equal over repeated runs).
- Short note in docs/asset-pipeline.md (Audio section).

## Decisions
- mill-loop is Ogg Vorbis stereo q7 (the guide's loop format), placed in sfx/ as the brief asked.
- volume_db in town-sounds.json = intended loudness against coin; files are already rendered at it (play at the
  coin's bus gain). The JSON generator fails if a file drifts more than 1 dB from its table value.
- Pitched sounds marked pitch_vary false (tuned in D Dorian).

## Successor
- To re-render only these: PYTHONPATH=assets/src python3 assets/src/audio/<gen>.py (run town-zz-sounds-json.py last).
  Generators write ledger rows to build/ledger-rows/audio.csv; LEDGER.csv rows were added by hand-merge of only these items.

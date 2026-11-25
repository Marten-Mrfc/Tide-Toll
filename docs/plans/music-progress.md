# Music progress

Started: Wed Nov 25 20:42:20 UTC 2026 (time box 50 min). Finished: 20:55 UTC. STATUS: ALL DONE (successor: verify only).

## Done
- assets/src/audio/_music.py: shared engine. The theme's pipe, lyre, frame drum, drone, gains, reverb (rt60 2.0,
  18 ms predelay, 28% wet), 40 Hz high-pass, humanising and level (-20 LUFS I, TP <= -2 dBTP) copied unchanged
  from theme-hamlet.py. theme-hamlet.py and the theme's files are untouched (git status shows no change).
- Pieces (generator -> assets/audio/music/<id>.ogg + <id>-score.json):
  - music-ferry-morning.py  Ferry Morning, D Mixolydian, 68 BPM, 28 bars, 74.12 s loop, seed 801
  - music-long-field.py     The Long Field, A Aeolian, 64 BPM, 28 bars, 78.75 s loop, seed 802
  - music-low-meadow.py     Low Meadow, D Aeolian, 60 BPM, 24 bars, 72.00 s loop, seed 813
  - music-theme-rising.py   Tide and Toll, Rising (age change), D Dorian, 64 BPM, 16 bars, 49.01 s, real ending
  - music-still-pages.py    Still Pages (pause), D Dorian, 60 BPM, 20 bars, 60.00 s loop, -22 LUFS on purpose
- playlist-music.py -> assets/audio/music/playlist.json (rotation: theme, Ferry Morning, The Long Field, Low Meadow).
- _check_music.py: measures all pieces beside the theme; passes. Rendering is deterministic (two full re-renders,
  identical sha256 of WAV masters, Oggs and scores).
- LEDGER.csv: 11 rows appended (5 Ogg, 5 score JSON, playlist). check-ledger: 0 problems.
- docs/asset-pipeline.md: note under Audio + layout line. tools/build-assets.sh needs no change (it discovers
  every non-underscore *.py in assets/src/audio in name order; playlist-music.py runs after music-*.py).

## Decisions
- WAV masters go to build/audio-music/, not assets/ (repo size). The game plays the Oggs.
- Vorbis quality 7 for the new pieces (theme is 5): at q5/q6 the encoder's independent error at both file ends
  (about 0.0013 each, opposite sign) sometimes pushed the decoded join's 2nd difference just over the theme's own
  seam rule (e.g. Long Field 0.0042 vs 0.0035 at q6). At q7 all pass. The WAV joins always passed.
- Low Meadow seed 803 -> 813 (803's q5 Ogg failed the same seam rule; kept 813).
- Melodies revised once each (Ferry bars 10-12, Long Field bar 10, Low Meadow bars 5, 9-12) to keep shared runs <= 4 notes.

## Next
- nothing. Possible later: in-game wiring (game/ is not mine), a listen by the owner.

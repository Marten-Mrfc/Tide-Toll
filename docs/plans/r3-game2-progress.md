# Round 3 game part 2 progress (sound, music, achievements, end of the age, playthrough)

Started 2026-11-25 23:37 UTC. Time box ~45 min.

## Done
- (nothing yet) read the brief.

## Next
- sounds, music, achievements page, charter moment + first-age page, keys as bound, game-playthrough step, screenshots, ./check --export
- 00:0x: written and building: SoundCues.cs (town-sounds.json table with fallback), TownAudio rewrite (every event its cue at coin gain + nothing, least gaps, Interface/Effects/Ambience buses, mill loop by distance and zoom, peal on next age), TownMusic.cs (rotation, fades, gap, Still Pages, Rising), TownHud.Book.cs (achievements page + bookmark, first-age page, Ring moment), charter stone arrival + bell peal (TownBuildings), keys as bound (GameInput.Bound/Tag/Version; new action tt_next_age N), Esc closes pages first (TownHud.TakesEsc).
- game-playthrough (game/tests/PlayThrough.cs, play.tscn, step in tools/check.sh) PASSED first run: seed 1, charter at minute 30.9, 161 purchases, 24 paved, 14 extended, 73 bells, 156 clicks, 102 keys, no MISS.
- Screenshots --age-only (new quick mode): charter, end-of-age, achievements, summary (achievements on left page), next-age, first-age. Read; fixed: charter stone hidden under the banner (view now glides to the bell green), summary list moved up/larger.
- game-town passes after the changes.
- Export: export_presets.cfg now includes playlist.json and town-sounds.json (code has the same tables as fallback).
## Next
- re-read age shots, menu/settings/save tests, sim-wishes, AGENTS/README, full ./check --export
- Age shots re-read after fixes (charter shows the bell green and stone; summary with the achievements list; first-age page). Settings and town tests pass. Docs: AGENTS.md, README.md, sim-wishes.md updated.
- Running ./check --export (log build/check-logs/, output in scratchpad check.log).
- 00:00 ./check --export: every step PASS except `ledger`, which fails on 10 untracked assets/models/prop-*.glb with no LEDGER.csv row. Those files were added by another agent; assets/ is not mine, so the lead must add the rows or drop the files.
- After the check: the Pave and Extend bookmark text "P to stop" / "E to stop" now uses the keys as bound (StopKey). game-town and game-menu pass again after that change. The full ./check was not run again.

## State: finished (brief done). A successor only needs to verify.
- Missing assets: none. Every event in town-sounds.json has its own file. A bell peal for the next age uses the existing bell-long-*.wav.
- Not done / judgement calls: "decisions a minute" counts the one-second looks where two or more kinds of item could be bought. The playthrough plays seed 1 only. The charter stone looks small at the charter view, because the model is small (assets/).

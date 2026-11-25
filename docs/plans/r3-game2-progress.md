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

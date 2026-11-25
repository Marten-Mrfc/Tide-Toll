# Doc rewrite progress

Started 2026-11-25 20:32 UTC (time box 25 minutes). All finished.

Done:
- Gameplay Systems tab (7e2d7651-21ec): new section "The game now: an incremental town" inserted after the title, then heading "Earlier direction: the desire-line game (kept for reference)" and the one sentence. Old blocks follow.
- UX & UI tab (ba135eb2-f67b): "How it plays and looks now", same Earlier direction heading.
- Build Plan tab (aa88b9bc-2a93): "Tonight's rebuild, 25 to 26 November 2026", with "Morning report" heading and the single line, same Earlier direction heading.
- Overview tab (654f3c1a-1b75): short note inserted after the date line.

Verified by re-reading and comparing block ids: no old block removed or reordered. Build Plan: all 372 old blocks present. Gameplay and UX: the read is capped (complete=false, about 255k characters), so the last 101 and 35 old blocks fall outside the read window; the missing ids are one contiguous tail run, consistent with the cap, not deletion.

For a successor: insert ops need full block ids (session id + clock, e.g. m6ea1wgr0x5.31), and several inserts after the same anchor read in reverse. Nothing else to do.

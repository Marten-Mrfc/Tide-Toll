#!/usr/bin/env bash
# game-people (n7, villagers as people): the pick, the ring, the card and the bodies (game/tests/PeopleTest.cs) on a real
# town, sampled every frame at fixed 60, 30 and 120 frames a second: picking in a crowd, no flicker, off in the modes,
# over the interface and paused; lanes by direction, following distance, standing spots; nothing in one frame; allocation.
# Used by tools/check.sh; logs in build/check-logs/people-*.log.
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot}
LOGS="$ROOT/build/check-logs"
mkdir -p "$LOGS"
fail=0
for fps in 60 30 120; do
  echo "-- game-people at $fps fps"
  if ! "$ROOT/tools/godot-test.sh" 'PEOPLE PASS' timeout 900 "$GODOT" --headless --path "$ROOT/game" --audio-driver Dummy --fixed-fps "$fps" \
      res://tests/people.tscn --quit-after 400000 -- --fps="$fps" >"$LOGS/people-$fps.log" 2>&1; then
    echo "   FAILED at $fps fps:"; grep -E "FAIL" "$LOGS/people-$fps.log" | head -5 | sed 's/^/   /'; fail=1
  fi
  grep -E '^PEOPLE PASS' "$LOGS/people-$fps.log" | sed 's/^/   /'
done
[ "$fail" = 0 ] && echo "PEOPLE RATES PASS"
exit $fail

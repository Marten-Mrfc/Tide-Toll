#!/usr/bin/env bash
# game-work-places (night 5, the places' share of "work you can watch"): game/tests/WorkPlacesTest.cs over a whole first
# age at 60 frames a second (every check), then the first two minutes again at 30 and 120 (nothing changes in one frame
# at any rate; the same checks on what was seen). Logs per rate in build/check-logs/work-places-*.log.
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GODOT="${GODOT:-godot}"
LOGS="$ROOT/build/check-logs"
mkdir -p "$LOGS"
fail=0
for fps in 60 30 120; do
  extra=""
  [ "$fps" != 60 ] && extra="--short"
  echo "-- game-work-places at $fps fps"
  if ! "$ROOT/tools/godot-test.sh" 'WORK PLACES PASS' timeout 1200 "$GODOT" --headless --path "$ROOT/game" --audio-driver Dummy --fixed-fps "$fps" \
      res://tests/work-places.tscn --quit-after 400000 -- --fps="$fps" $extra >"$LOGS/work-places-$fps.log" 2>&1; then
    echo "   FAILED at $fps fps:"; grep -E "FAIL" "$LOGS/work-places-$fps.log" | head -5 | sed 's/^/   /'; fail=1
  fi
  grep -o 'WORK PLACES PASS.*' "$LOGS/work-places-$fps.log" | head -1 | sed 's/^/   /'
done
exit $fail

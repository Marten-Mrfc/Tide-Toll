#!/usr/bin/env bash
# game-work (n5, work you can watch): the villagers' checks (game/tests/WorkTest.cs) on a real town, sampled every
# frame at fixed 60, 30 and 120 frames a second: the line (spacing, order, facing, outside buildings and crop), no
# load before the work is finished and none that pops or vanishes, every delivery landing, nothing changing in one
# frame, the tools, the parked cart, the bell, and allocation. Used by tools/check.sh; logs in build/check-logs/work-*.log.
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot}
LOGS="$ROOT/build/check-logs"
mkdir -p "$LOGS"
fail=0
for fps in 60 30 120; do
  echo "-- game-work at $fps fps"
  if ! "$ROOT/tools/godot-test.sh" 'WORK PASS' timeout 900 "$GODOT" --headless --path "$ROOT/game" --audio-driver Dummy --fixed-fps "$fps" \
      res://tests/work.tscn --quit-after 400000 -- --fps="$fps" >"$LOGS/work-$fps.log" 2>&1; then
    echo "   FAILED at $fps fps:"; grep -E "FAIL" "$LOGS/work-$fps.log" | head -5 | sed 's/^/   /'; fail=1
  fi
  grep -E '^WORK PASS' "$LOGS/work-$fps.log" | sed 's/^/   /'
done
[ "$fail" = 0 ] && echo "WORK RATES PASS"
exit $fail

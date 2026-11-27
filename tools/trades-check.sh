#!/usr/bin/env bash
# game-trades (n7 trades): the figures' trade heads, the Trades card and the villager's card (game/tests/TradesTest*.cs) on a real
# town, sampled every frame at fixed 60, 30 and 120 frames a second: no Helper in a hat, heads changing over frames, the
# crossfade of the two cards, the stepper and its hit areas, the hover rings, the Training row; allocation.
# Used by tools/check.sh; logs in build/check-logs/trades-*.log.
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot}
LOGS="$ROOT/build/check-logs"
mkdir -p "$LOGS"
fail=0
for fps in 60 30 120; do
  echo "-- game-trades at $fps fps"
  if ! "$ROOT/tools/godot-test.sh" 'TRADES PASS' timeout 900 "$GODOT" --headless --path "$ROOT/game" --audio-driver Dummy --fixed-fps "$fps" \
      res://tests/trades.tscn --quit-after 400000 -- --fps="$fps" >"$LOGS/trades-$fps.log" 2>&1; then
    echo "   FAILED at $fps fps:"; grep -E "FAIL" "$LOGS/trades-$fps.log" | head -5 | sed 's/^/   /'; fail=1
  fi
  grep -E '^TRADES PASS' "$LOGS/trades-$fps.log" | sed 's/^/   /'
done
[ "$fail" = 0 ] && echo "TRADES RATES PASS"
exit $fail

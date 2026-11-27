#!/usr/bin/env bash
# n7 trades: pictures from the real game (game/tests/TradesShots.cs): the Trades card, a line hovered with its members
# ringed, tooltips, the villager's card with its Trade caps, the crossfade, a strip of a count changed, a street before
# and after the trades' heads. Rendered as the screenshot step renders (xvfb, Mobile renderer on Vulkan).
#   tools/trades-shots.sh [OUT_DIR]   runs 1920x1080, 1280x720 and 1280x720 at interface size 150.
#   DISPLAY_NUM picks the virtual display (default: any free one).
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot}
OUT=${1:-"$ROOT/build/n7/trades"}
mkdir -p "$OUT"; OUT=$(cd "$OUT" && pwd)
if [ -n "${DISPLAY_NUM:-}" ]; then XVFB=(xvfb-run -n "$DISPLAY_NUM"); else XVFB=(xvfb-run -a); fi
fail=0
for run in "1920x1080 100" "1280x720 100" "1280x720 150"; do
  set -- $run
  "$ROOT/tools/godot-test.sh" 'TRADES SHOTS PASS' timeout 1200 "${XVFB[@]}" -s '-screen 0 1920x1080x24' "$GODOT" --path "$ROOT/game" \
    --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution "$1" --fixed-fps 60 \
    res://tests/trades-shots.tscn --quit-after 400000 -- --out-dir="$OUT" --size="$2" || fail=1
done
exit $fail

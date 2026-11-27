#!/usr/bin/env bash
# n7, villagers as people: pictures from the real game (game/tests/PeopleShots.cs): the hover ring and name tag, the
# card under the ledger, a busy road before (round 6's spread) and after (the bodies pass), at the normal view and close.
# Rendered as the screenshot step renders (xvfb, Mobile renderer on Vulkan, 1920 x 1080).
#   tools/people-shots.sh [OUT_DIR]        DISPLAY_NUM picks the virtual display (default: any free one).
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot}
OUT=${1:-"$ROOT/build/n7/people"}
mkdir -p "$OUT"; OUT=$(cd "$OUT" && pwd)
if [ -n "${DISPLAY_NUM:-}" ]; then XVFB=(xvfb-run -n "$DISPLAY_NUM"); else XVFB=(xvfb-run -a); fi
"$ROOT/tools/godot-test.sh" 'PEOPLE SHOTS PASS' timeout 1200 "${XVFB[@]}" -s '-screen 0 1920x1080x24' "$GODOT" --path "$ROOT/game" \
  --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 --fixed-fps 60 \
  res://tests/people-shots.tscn --quit-after 400000 -- --out-dir="$OUT"

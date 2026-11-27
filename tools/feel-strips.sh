#!/usr/bin/env bash
# Night 3 (feel): frame strips of each small reward on the map (game/tests/FeelStrips.cs), rendered as the screenshot
# step renders (xvfb, Mobile renderer on Vulkan, 1920 x 1080), twelve frames 0.08 s apart, the middle 960 x 540 of
# the screen at its real pixels; joined into one sheet per reward: OUT/feel-NAME.png.
#   tools/feel-strips.sh [OUT_DIR] [--river-only]
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot}
OUT=${1:-"$ROOT/build/n3/feel"}
shift || true
mkdir -p "$OUT/frames"
"$ROOT/tools/godot-test.sh" 'FEEL STRIPS PASS' timeout 2400 xvfb-run -a -s '-screen 0 1920x1080x24' "$GODOT" --path "$ROOT/game" \
  --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 \
  res://tests/feel-strips.tscn --quit-after 400000 -- --out-dir="$OUT" "$@" || exit 1
python3 "$ROOT/tools/feel-sheet.py" "$OUT"

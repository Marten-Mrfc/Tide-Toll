#!/usr/bin/env bash
# Frame strips of the book's transitions for human eyes: the game rendered as the screenshot step renders it (xvfb,
# the Mobile renderer on Vulkan, 1920 x 1080), each transition held at twelve even moments of its clock
# (game/tests/MotionStrips.cs), joined into one contact sheet per transition (tools/motion-sheet.py):
# build/motion/{pause,resume,settings-open,tab-forward,tab-back,settings-close,question,age-page}.png.
# Night 3, L2: and the message system's: msg-banner, msg-choice, msg-notes, msg-request, msg-line, msg-coin.
#   tools/motion-strips.sh [--age-only | --messages-only]
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot}
OUT="$ROOT/build/motion"
mkdir -p "$OUT/frames"
extra=()
[ "${1:-}" = "--age-only" ] && extra=(--age-only)
[ "${1:-}" = "--messages-only" ] && extra=(--messages-only)
"$ROOT/tools/godot-test.sh" 'STRIPS PASS' timeout 2400 xvfb-run -a -s '-screen 0 1920x1080x24' "$GODOT" --path "$ROOT/game" \
  --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 \
  res://tests/motion-strips.tscn --quit-after 400000 -- --out-dir="$OUT" "${extra[@]}" || exit 1
python3 "$ROOT/tools/motion-sheet.py" "$OUT"

#!/usr/bin/env bash
# Work you can watch (n5): interaction strips (game/tests/WorkStrips.cs). A real town played to the middle of the first
# age; for each job (water, reap, mill, market, home) one villager followed from joining the line to walking away:
# eight frames at the normal view and eight close up, rendered as the screenshot step renders (xvfb, Mobile renderer
# on Vulkan, 1920 x 1080, --fixed-fps 60), the middle 960 x 540 at real pixels; one contact sheet per job:
# OUT/work-JOB.png (and, with --jpeg-dir, a JPEG copy there).
#   tools/work-strips.sh [OUT_DIR] [--jpeg-dir=DIR] [--only=water,reap,...]
# DISPLAY_NUM picks the virtual display (default: any free one).
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot}
OUT=${1:-"$ROOT/build/n5/strips"}
shift || true
JPEG=""; ARGS=()
for a in "$@"; do
  case "$a" in
    --jpeg-dir=*) JPEG=${a#--jpeg-dir=} ;;
    *) ARGS+=("$a") ;;
  esac
done
mkdir -p "$OUT/frames"
OUT=$(cd "$OUT" && pwd)
[ -n "$JPEG" ] && mkdir -p "$JPEG" && JPEG=$(cd "$JPEG" && pwd)
rm -f "$OUT"/frames/work-*
if [ -n "${DISPLAY_NUM:-}" ]; then XVFB=(xvfb-run -n "$DISPLAY_NUM"); else XVFB=(xvfb-run -a); fi
"$ROOT/tools/godot-test.sh" 'WORK STRIPS PASS' timeout 3000 "${XVFB[@]}" -s '-screen 0 1920x1080x24' "$GODOT" --path "$ROOT/game" \
  --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 --fixed-fps 60 \
  res://tests/work-strips.tscn --quit-after 2000000 -- --out-dir="$OUT" "${ARGS[@]}" || exit 1
python3 "$ROOT/tools/work-sheet.py" "$OUT" ${JPEG:+"$JPEG"}

#!/usr/bin/env bash
# Renders a .glb to a contact sheet PNG: top view plus 45-degree tilts from the south, east and north, on Paper,
# lit by one sun from the south-east, with the screen-space ink outline (assets/src/common/outline_screen.gdshader).
# Uses Godot (Mobile renderer, Vulkan on Mesa lavapipe) inside xvfb. Takes about 10 s per sheet.
#
#   tools/render-contact-sheet.sh MODEL.glb [options]
#     --out=PATH      output PNG (default build/contact-sheets/<model>.png, or <model>-unlit.png with --unlit)
#     --sides         add a second row: low side views at yaw 0, 90, 180, 270 (the done checklist wants these)
#     --save          also copy the sheet to assets/src/contact-sheets/<model>.png (the guide's record copy)
#     --unlit         flat unshaded colours, for palette checks:  PYTHONPATH=assets/src python3 -m common.png audit OUT
#     --view=14       metres across one panel (same ortho scale in every panel; use 20-24 for a 16 m field)
#     --panel=480     panel size in px      --focus_y=1.5  camera target height
#     --outline=none  no ink line           --line=1.5  line step in px    --crease=0.45  crease threshold
#     --noshadow      sun without shadows   --tile  also save each panel as <out>-<panel>.png
# Prints the model's triangle count and bounding box (MODEL ...) and the sheet path. Exits non-zero on any
# failure, including a shader compile error (which Godot itself does not treat as fatal).
# Renders are serialised with a lock (one at a time on this machine), so parallel builders can call it freely.
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot}
PROJ="$ROOT/tools/contact-sheet"
SHADER="$ROOT/assets/src/common/outline_screen.gdshader"
[ $# -ge 1 ] && [ "${1:0:2}" != "--" ] || { sed -n '2,20p' "$0"; exit 2; }
MODEL=$(realpath "$1"); shift
[ -f "$MODEL" ] || { echo "no such model: $MODEL"; exit 2; }
NAME=$(basename "$MODEL" .glb)
OUT=""; SAVE=0; ARGS=()
for a in "$@"; do
  case "$a" in
    --out=*) OUT=$(realpath -m "${a#--out=}") ;;
    --save) SAVE=1 ;;
    --unlit) ARGS+=("$a"); NAME_SUFFIX=-unlit ;;
    *) ARGS+=("$a") ;;
  esac
done
OUT=${OUT:-$ROOT/build/contact-sheets/$NAME${NAME_SUFFIX:-}.png}
mkdir -p "$(dirname "$OUT")" "$ROOT/build/contact-sheets"
LOG="$ROOT/build/contact-sheets/.last-$NAME.log"
command -v xvfb-run >/dev/null || { echo "xvfb-run not installed (tools/setup-linux.sh)"; exit 1; }
command -v "$GODOT" >/dev/null || { echo "godot not found (tools/setup-linux.sh, or set GODOT=)"; exit 1; }

(
  flock -w 900 9 || { echo "timed out waiting for another contact-sheet render"; exit 1; }
  # A fresh clone has no .godot/ cache in the tool project; import it once.
  [ -d "$PROJ/.godot" ] || "$GODOT" --headless --path "$PROJ" --import >/dev/null 2>&1 || true
  rm -f "$OUT"
  timeout 300 xvfb-run -a -s '-screen 0 1280x720x24' "$GODOT" --path "$PROJ" \
    --rendering-method mobile --rendering-driver vulkan --resolution 960x480 \
    res://contact_sheet.tscn --quit-after 900 -- \
    --model="$MODEL" --out="$OUT" --shader="$SHADER" ${ARGS[@]+"${ARGS[@]}"} >"$LOG" 2>&1 || true
) 9>"$ROOT/build/contact-sheets/.render.lock"

grep -E "^MODEL " "$LOG" || true
if grep -qE "SHADER ERROR|SCRIPT ERROR|Parse Error|CONTACT SHEET FAIL" "$LOG" || ! grep -q "CONTACT SHEET OK" "$LOG" || [ ! -s "$OUT" ]; then
  echo "CONTACT SHEET FAILED for $MODEL; log: $LOG"
  grep -E "ERROR|FAIL|error" "$LOG" | head -20
  exit 1
fi
if [ "$SAVE" = 1 ]; then
  mkdir -p "$ROOT/assets/src/contact-sheets"
  cp "$OUT" "$ROOT/assets/src/contact-sheets/$(basename "$OUT")"
  echo "saved assets/src/contact-sheets/$(basename "$OUT")"
fi
echo "sheet: ${OUT#$ROOT/}"

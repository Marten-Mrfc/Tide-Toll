#!/usr/bin/env bash
# The interface audit tour: one town (seed 1) from the first minute to the borough seal, one picture per interface
# state (u01..u41, no u37) and per state a JSON of every visible interface control (rect in window pixels, text, opacity,
# group), under xvfb with the game's renderer (as tools/tour.sh).
#   tools/ui-audit.sh [--res=1920x1080] [--scale=80|100|150] [--only=u01,u07] [--out=DIR] [--no-build]
#   default DIR: build/n3/ui-audit/<res>-<scale>. Then: python3 tools/ui-audit-report.py DIR  (writes DIR/report.md)
# Fails on any printed error/warning line (tools/godot-test.sh), a failed check, or no UIAUDIT PASS line.
# Own profile folder (user://tests/co-<checkout hash>/uiaudit-pass/): the player's save and settings are never touched.
# Set DISPLAY_NUM=N to choose the xvfb display (two runs at once need different numbers).
set -uo pipefail
{
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"
GODOT=${GODOT:-godot}
RES=1920x1080; SCALE=100; ONLY=""; OUT=""; BUILD=1
for a in "$@"; do
  case "$a" in
    --res=*) RES=${a#--res=} ;;
    --scale=*) SCALE=${a#--scale=} ;;
    --only=*) ONLY="$a" ;;
    --out=*) OUT=${a#--out=} ;;
    --no-build) BUILD=0 ;;
    -h|--help) sed -n '2,9p' "$0"; exit 0 ;;
    *) echo "unknown option $a"; exit 2 ;;
  esac
done
case "$SCALE" in 80|100|150) ;; *) echo "--scale must be 80, 100 or 150"; exit 2 ;; esac
[ -n "$OUT" ] || OUT="$ROOT/build/n3/ui-audit/$RES-$SCALE"
case "$OUT" in /*) ;; *) OUT="$ROOT/$OUT" ;; esac
W=${RES%x*}; H=${RES#*x}
SW=$(( W > 1920 ? W : 1920 )); SH=$(( H > 1080 ? H : 1080 ))
if [ -z "$ONLY" ]; then rm -rf "$OUT"; fi
mkdir -p "$OUT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
if [ "$BUILD" = 1 ]; then
  dotnet build game/TideToll.csproj -c Debug >"$OUT/build.log" 2>&1 || { tail -n 30 "$OUT/build.log"; echo "UIAUDIT FAIL: build"; exit 1; }
fi
XV=(xvfb-run -a)
[ -n "${DISPLAY_NUM:-}" ] && XV=(xvfb-run -n "$DISPLAY_NUM")
t0=$SECONDS
run() {
  tools/godot-test.sh 'UIAUDIT PASS' timeout 2700 "${XV[@]}" -s "-screen 0 ${SW}x${SH}x24" "$GODOT" --path game \
    --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution "$RES" \
    res://tests/ui-audit.tscn --quit-after 1000000 -- --out-dir="$OUT" --scale="$SCALE" --tag="$RES-$SCALE" "$@" >"$OUT/audit.log" 2>&1
}
args=(); [ -n "$ONLY" ] && args+=("$ONLY")
run "${args[@]}"; code=$?
if [ "$code" != 0 ] && [ -z "$ONLY" ] && ! grep -q '^FAIL:' "$OUT/audit.log" && [ ! -f "$OUT/shots.json" ]; then
  missing=$(python3 -c 'import os,sys; ids=["u%02d"%i for i in [i for i in list(range(1,26))+list(range(30,42)) if i!=37]]; print(",".join(i for i in ids if not os.path.exists(os.path.join(sys.argv[1], i+".png"))))' "$OUT")
  echo "ui-audit: Godot stopped without a verdict (crash?); retaking: $missing"
  mv "$OUT/audit.log" "$OUT/audit-crashed.log"
  run --only="$missing"; code=$?
fi
grep -E '^UIAUDIT (SHOT|NOT MADE)|UIAUDIT PASS|FAIL' "$OUT/audit.log"
echo "ui-audit: exit $code in $((SECONDS - t0)) s; pictures and json in $OUT, log $OUT/audit.log"
exit $code
}

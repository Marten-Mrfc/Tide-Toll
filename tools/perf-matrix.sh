#!/usr/bin/env bash
# n10, the drawing's matrix (docs/plans/n10/performance-research.md section 5a): game/tests/perf-draw.tscn under xvfb
# with the Mobile renderer, one process per group, each run holding the heavy-run lock only for its own group
# (build/locks/heavy in the main checkout, waited for with 10 s naps). Lines "DRAW town variant WxH mean_ms p95_ms
# draws objects primitives" go to build/perf-matrix/<label>.txt; the times are only good for ratios within a group.
#   tools/perf-matrix.sh [--label NAME] [--frames N] [--display N] [--groups 1,2,3,4] [--no-lock]
# Groups: 1 = age1 1920x1080 baseline, noink, nosoft, noshadow, notrees; 2 = age1 1920x1080 baseline, novillagers,
# flatground, zoomout, closest, scale50; 3 = age1 baseline at 1280x720 and 2560x1440; 4 = river: baseline, noink,
# noshadow, flatground; 5 = age1 1920x1080: the Display page's drawing detail 85, 70, 50 and the light outline.
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"
GODOT=${GODOT:-godot}
LABEL=run; FRAMES=30; DISPLAY_N=205; SETS=1,2,3,4; LOCK=1
while [ $# -gt 0 ]; do
  case "$1" in
    --label) LABEL=$2; shift ;;
    --frames) FRAMES=$2; shift ;;
    --display) DISPLAY_N=$2; shift ;;
    --groups) SETS=$2; shift ;;
    --no-lock) LOCK=0 ;;
    *) echo "unknown option $1"; exit 2 ;;
  esac
  shift
done
OUT="$ROOT/build/perf-matrix"; mkdir -p "$OUT"
TXT="$OUT/$LABEL.txt"
LOCKDIR=$(cd "$ROOT" && git rev-parse --path-format=absolute --git-common-dir 2>/dev/null | sed 's#/.git$##')/build/locks/heavy

take() {
  [ "$LOCK" = 1 ] || return 0
  until mkdir "$LOCKDIR" 2>/dev/null; do python3 -c "import time; time.sleep(10)"; done
  echo "lock taken $(date -u +%T)"
}
give() { [ "$LOCK" = 1 ] && rmdir "$LOCKDIR" && echo "lock given back $(date -u +%T)"; return 0; }

# run TOWN WxH VARIANTS
run() {
  local town=$1 size=$2 variants=$3 log="$OUT/$LABEL-$1-$2-${3//,/-}.log"
  local w=${size%x*} h=${size#*x}
  take
  timeout 600 xvfb-run -n "$DISPLAY_N" -s "-screen 0 ${w}x${h}x24" "$GODOT" --path game --rendering-method mobile \
    --rendering-driver vulkan --audio-driver Dummy --resolution "$size" res://tests/perf-draw.tscn --quit-after 1000000 \
    -- --town="$town" --variants="$variants" --frames="$FRAMES" >"$log" 2>&1
  local rc=$?
  give
  grep '^DRAW' "$log" | tee -a "$TXT"
  grep -q 'PERF DRAW PASS' "$log" || { echo "perf-draw failed ($rc), see $log"; return 1; }
}

ok=0
for g in ${SETS//,/ }; do
  case $g in
    1) run age1 1920x1080 baseline,noink,nosoft,noshadow,notrees || ok=1 ;;
    2) run age1 1920x1080 baseline,novillagers,flatground,zoomout,closest,scale50 || ok=1 ;;
    3) run age1 1280x720 baseline || ok=1; run age1 2560x1440 baseline || ok=1 ;;
    4) run river 1920x1080 baseline,noink,noshadow,flatground || ok=1 ;;
    5) run age1 1920x1080 baseline,detail85,detail70,detail50,light || ok=1 ;;
  esac
done
echo "matrix lines in $TXT"
exit $ok

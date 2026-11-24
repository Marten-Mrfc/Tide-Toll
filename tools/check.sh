#!/usr/bin/env bash
# One command for everything that can be verified automatically. Exits non-zero if any step fails.
#   ./check                 sim build + tests, Godot C# build, headless smoke, screenshot, ledger
#   ./check --no-screenshot skip the rendered screenshot (no xvfb, or in a hurry)
#   ./check --with-toy      also run the browser toy's own checks (npm run check)
#   ./check --export        also build the Windows export (slow; needs export templates)
# Set GODOT=/path/to/godot to use a Godot binary that is not on PATH.
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"
GODOT=${GODOT:-godot}
LOGS="$ROOT/build/check-logs"
SHOTS="$ROOT/build/screenshots"
mkdir -p "$LOGS" "$SHOTS"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

WITH_TOY=0; EXPORT=0; SHOT=1
for a in "$@"; do
  case "$a" in
    --with-toy) WITH_TOY=1 ;;
    --export) EXPORT=1 ;;
    --no-screenshot) SHOT=0 ;;
    -h|--help) sed -n '2,7p' "$0"; exit 0 ;;
    *) echo "unknown option $a"; exit 2 ;;
  esac
done

NAMES=(); RESULTS=(); SECS=(); NOTES=()
FAILED=0
record() { NAMES+=("$1"); RESULTS+=("$2"); SECS+=("$3"); NOTES+=("$4"); [ "$2" = FAIL ] && FAILED=1; return 0; }

# step NAME COMMAND...: runs COMMAND with output in build/check-logs/NAME.log.
# On failure it prints the log's last lines so the failing test or error is named right here.
step() {
  local name=$1; shift
  local log="$LOGS/$name.log" t0=$SECONDS
  echo "-- $name"
  if "$@" >"$log" 2>&1; then
    record "$name" PASS $((SECONDS - t0)) ""
  else
    echo "   FAILED, last lines of $log:"
    grep -E "Failed |error |FAIL" "$log" | head -20 | sed 's/^/   /'
    tail -n 15 "$log" | sed 's/^/   | /'
    record "$name" FAIL $((SECONDS - t0)) "see build/check-logs/$name.log"
  fi
}
note() { [ "${RESULTS[-1]}" = PASS ] && NOTES[-1]="$1"; return 0; }
skip() { echo "-- $1 skipped: $2"; record "$1" SKIP 0 "$2"; }

have_godot() { command -v "$GODOT" >/dev/null 2>&1; }

step sim-build dotnet build TideToll.sln -c Debug
if [ "${RESULTS[-1]}" = PASS ]; then
  step sim-test dotnet test TideToll.sln --no-build -c Debug --logger "console;verbosity=normal"
  note "$(grep -o 'Passed: *[0-9]*' "$LOGS/sim-test.log" | head -1 | tr -s ' ') $(grep -o 'BENCH.*ms per pass' "$LOGS/sim-test.log" | head -1 | sed 's/BENCH //')"
else
  skip sim-test "sim did not build"
fi

if have_godot; then
  # A fresh clone has no .godot/ cache; import once so scenes and scripts resolve.
  [ -d game/.godot ] || step godot-import "$GODOT" --headless --path game --import
  step sync-assets tools/sync-assets.sh
  step game-build dotnet build game/TideToll.csproj -c Debug
  # --quit-after is only a safety cap; the smoke scene quits itself with 0 or 1.
  step game-smoke bash -c "timeout 300 '$GODOT' --headless --path game res://tests/smoke.tscn --quit-after 2000 && grep -q 'SMOKE PASS' '$LOGS/game-smoke.log'"
  note "$(grep -o 'SMOKE PASS.*renderer=[a-z_]*' "$LOGS/game-smoke.log" | head -1)"
  if [ "$SHOT" = 0 ]; then
    skip screenshot "--no-screenshot"
  elif ! command -v xvfb-run >/dev/null 2>&1; then
    skip screenshot "xvfb-run not installed (tools/setup-linux.sh)"
  else
    # The Mobile renderer, as the game uses, on Mesa lavapipe inside a virtual X display.
    step screenshot bash -c "timeout 300 xvfb-run -a -s '-screen 0 1280x720x24' '$GODOT' --path game --rendering-method mobile --rendering-driver vulkan --resolution 1280x720 res://tests/screenshot.tscn --quit-after 2000 -- --out='$SHOTS/main.png' && grep -q 'SCREENSHOT PASS' '$LOGS/screenshot.log'"
    note "build/screenshots/main.png"
  fi
else
  for s in game-build game-smoke screenshot; do
    echo "-- $s: godot not found (run tools/setup-linux.sh or set GODOT=)"
    record "$s" FAIL 0 "godot not found"
  done
fi

step ledger python3 tools/check-ledger.py
note "$(tail -n 1 "$LOGS/ledger.log")"

if [ "$WITH_TOY" = 1 ]; then
  step toy bash -c "cd prototypes/desire-lines && { [ -d node_modules ] || npm ci; } && npm run check"
fi
if [ "$EXPORT" = 1 ]; then
  step export-windows tools/export-windows.sh
  note "$(tail -n 1 "$LOGS/export-windows.log")"
fi

echo
printf '%-15s %-6s %5s  %s\n' STEP RESULT SECS NOTE
for i in "${!NAMES[@]}"; do
  printf '%-15s %-6s %5s  %s\n' "${NAMES[$i]}" "${RESULTS[$i]}" "${SECS[$i]}" "${NOTES[$i]}"
done
echo
if [ "$FAILED" = 0 ]; then echo "CHECK PASSED"; else echo "CHECK FAILED"; fi
exit $FAILED

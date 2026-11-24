#!/usr/bin/env bash
# One command for everything that can be verified automatically. Exits non-zero if any step fails.
#   ./check                 sim build + fast tests (Release), Godot C# build, headless smoke, screenshot, ledger
#   ./check --no-screenshot skip the rendered screenshot (no xvfb, or in a hurry)
#   ./check --gate          also run the long statistical runs: gate tables, toy comparison, Category=Gate tests (~6 min)
#   ./check --bench         also run the tick benchmark in Release (fails only above a generous budget)
#   ./check --with-toy      also run the browser toy's own checks (npm run check)
#   ./check --export        also build the Windows export (slow; needs export templates)
# Set GODOT=/path/to/godot to use a Godot binary that is not on PATH, GODOT_NUPKGS=/path to Godot's NuGet folder.
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"
GODOT=${GODOT:-godot}
LOGS="$ROOT/build/check-logs"
SHOTS="$ROOT/build/screenshots"
mkdir -p "$LOGS" "$SHOTS"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

WITH_TOY=0; EXPORT=0; SHOT=1; GATE=0; BENCH=0
for a in "$@"; do
  case "$a" in
    --with-toy) WITH_TOY=1 ;;
    --gate) GATE=1 ;;
    --bench) BENCH=1 ;;
    --export) EXPORT=1 ;;
    --no-screenshot) SHOT=0 ;;
    -h|--help) sed -n '2,9p' "$0"; exit 0 ;;
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

# Godot.NET.Sdk ships only inside the Godot download. Register its folder with NuGet when it is missing,
# so the game builds for any user (tools/setup-linux.sh does the same once, as root).
GODOT_SRC=godot-4.7-local
godot_nuget() {
  dotnet nuget list source 2>/dev/null | grep -q "$GODOT_SRC" && return 0
  local d bin
  bin=$(command -v "$GODOT" 2>/dev/null || true)
  for d in "${GODOT_NUPKGS:-}" "${bin:+$(dirname "$(readlink -f "$bin")")/GodotSharp/Tools/nupkgs}" /opt/godot/4.7/GodotSharp/Tools/nupkgs; do
    if [ -n "$d" ] && [ -f "$d/Godot.NET.Sdk.4.7.0.nupkg" ]; then
      dotnet nuget add source "$d" -n "$GODOT_SRC" && return 0
    fi
  done
  echo "Godot.NET.Sdk 4.7.0 not found: set GODOT_NUPKGS to Godot's GodotSharp/Tools/nupkgs folder"; return 1
}

# The simulation is built and tested in Release: the toy's checks time optimised code, and Release keeps
# the fast tier under a minute. The game build below compiles the library again in Debug.
step sim-build dotnet build TideToll.sln -c Release
if [ "${RESULTS[-1]}" = PASS ]; then
  step sim-test dotnet test TideToll.sln --no-build -c Release --filter "Category!=Gate&Category!=Perf" --logger "console;verbosity=normal"
  note "$(grep -o 'Passed: *[0-9]*' "$LOGS/sim-test.log" | head -1 | tr -s ' ') $(grep -o 'BENCH.*ms per pass' "$LOGS/sim-test.log" | head -1 | sed 's/BENCH //')"
  TOOLS="dotnet tools/TideToll.Tools/bin/Release/net8.0/TideToll.Tools.dll"
  if [ "$BENCH" = 1 ]; then
    step bench $TOOLS bench --budget-ms 5 --p99-budget-ms 25
    note "$(grep -o 'BENCH 500 walkers: [0-9.]* ms' "$LOGS/bench.log" | sed 's/BENCH //')"
    grep '^BENCH' "$LOGS/bench.log" | sed 's/^/   /'
    step bench-test dotnet test sim.tests --no-build -c Release --filter "Category=Perf" --logger "console;verbosity=normal"
  fi
  if [ "$GATE" = 1 ]; then
    # The toy's tables and gate checks, then the C# port against the toy's golden table.
    step gate-tables $TOOLS gate
    note "$(grep -E '^(ALL CHECKS PASSED|FAILED)' "$LOGS/gate-tables.log" | head -1)"
    sed -n '/^GATE G3/,$p' "$LOGS/gate-tables.log"
    step gate-tests dotnet test sim.tests --no-build -c Release --filter "Category=Gate" --logger "console;verbosity=normal"
    note "$(grep -o 'Failed: *[0-9]*, Passed: *[0-9]*' "$LOGS/gate-tests.log" | head -1 | tr -s ' ')"
  fi
else
  skip sim-test "sim did not build"
fi

if have_godot; then
  step godot-nuget godot_nuget
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

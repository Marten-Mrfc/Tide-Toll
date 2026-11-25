#!/usr/bin/env bash
# One command for everything that can be verified automatically. Exits non-zero if any step fails.
#   ./check                 sim build + fast tests (Release), Godot C# build, the town's headless smoke, play-through
#                           (real input) and perf tests, screenshots at 1920x1080, ledger
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
  step sync-assets tools/sync-assets.sh
  # Import when the cache is missing or an asset has no import sidecar yet (new assets, or changed import settings).
  needs_import() {
    [ -d game/.godot ] || return 0
    find game/assets -type f \( -name '*.glb' -o -name '*.png' -o -name '*.svg' -o -name '*.ttf' -o -name '*.wav' -o -name '*.ogg' \) \
      ! -name '*_palette.png' | while read -r f; do [ -f "$f.import" ] || { echo "$f"; break; }; done | grep -q .
  }
  if needs_import; then step godot-import "$GODOT" --headless --path game --import; fi
  step game-build dotnet build game/TideToll.csproj -c Debug
  # --quit-after is only a safety cap; the test scenes quit themselves with 0 or 1. tools/godot-test.sh also fails
  # a run that printed any error line, since Godot only logs unhandled C# exceptions.
  # The town scene loads with all its pieces and runs 30 game seconds (coin earned, villager drawn, ground uploaded).
  step game-smoke tools/godot-test.sh 'SMOKE PASS' timeout 300 "$GODOT" --headless --path game --audio-driver Dummy res://tests/smoke.tscn --quit-after 20000
  note "$(grep -o 'SMOKE PASS.*' "$LOGS/game-smoke.log" | head -1 | cut -c1-90)"
  # The town through real mouse and key events: the first upgrades bought by bookmark and key, then fast play by
  # clicking to level 3: every purchase changes the town, each gift arrives and is drawn, a road wears in visibly,
  # Pave mode lights the roads and a click paves one, the bell rings by key and by click; villagers never jump.
  step game-town tools/godot-test.sh 'TOWN PASS' timeout 600 "$GODOT" --headless --path game --audio-driver Dummy res://tests/town.tscn --quit-after 200000
  note "$(grep -o 'TOWN PASS.*' "$LOGS/game-town.log" | head -1 | sed 's/TOWN PASS //' | cut -c1-90)"
  # CPU time per frame with the end-of-age town (the simulation's best-payback bot plays to the charter), at 1x and 8x,
  # interface and sound running (no rendering headless); --fixed-fps 60 gives every frame 1/60 s. Fails above 6 ms (1x)
  # or 10 ms (8x) mean, the old game's budget, or above 1536 (1x) or 6144 (8x) managed bytes allocated per frame.
  step game-perf tools/godot-test.sh 'PERF PASS' timeout 600 "$GODOT" --headless --path game --audio-driver Dummy --fixed-fps 60 res://tests/perf.tscn --quit-after 20000
  note "$(grep -o 'PERF town [0-9]* villagers [0-9]x: cpu ms/frame mean [0-9.]*' "$LOGS/game-perf.log" | sed 's/PERF town [0-9]* villagers //; s/cpu ms\/frame mean/mean/' | tr '\n' ' ')ms"
  grep '^PERF' "$LOGS/game-perf.log" | sed 's/^/   /'
  # The zoom from the tilted town out to the whole book by wheel notches (pitch, interface stages, the summary page
  # against the simulation, Home), a Pave click while tilted and a villager's height on screen, at 1920 x 1080.
  step game-book tools/godot-test.sh 'BOOK PASS' timeout 600 "$GODOT" --headless --path game --audio-driver Dummy --fixed-fps 60 res://tests/book.tscn --quit-after 100000
  note "$(grep -o 'BOOK PASS.*' "$LOGS/game-book.log" | head -1 | sed 's/BOOK PASS //' | cut -c1-90)"
  if [ "$SHOT" = 0 ]; then
    skip screenshot "--no-screenshot"
  elif ! command -v xvfb-run >/dev/null 2>&1; then
    skip screenshot "xvfb-run not installed (tools/setup-linux.sh)"
  else
    # The Mobile renderer, as the game uses, on Mesa lavapipe inside a virtual X display, at 1920 x 1080: the first
    # minute, the first purchase, level 3, Pave mode and the end of the age (build/screenshots/town-*-1920x1080.png).
    step screenshot tools/godot-test.sh 'SCREENSHOT PASS' timeout 1200 xvfb-run -a -s '-screen 0 1920x1080x24' "$GODOT" --path game --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 res://tests/screenshot.tscn --quit-after 100000 -- --out-dir="$SHOTS"
    note "build/screenshots/town-{first-minute,first-purchase,level-3,pave,level-6,end-of-age,zoom-1..4,summary}-1920x1080.png"
  fi
else
  for s in game-build game-smoke game-town game-perf game-book screenshot; do
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

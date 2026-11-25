#!/usr/bin/env bash
# One command for everything that can be verified automatically. Exits non-zero if any step fails.
#   ./check                 sim build + fast tests (Release), Godot C# build, headless smoke, tools, river, interface and perf
#                           tests, screenshots at 1280x720 and 1920x1080, ledger
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
  step game-smoke tools/godot-test.sh 'SMOKE PASS' timeout 300 "$GODOT" --headless --path game --audio-driver Dummy res://tests/smoke.tscn --quit-after 2000
  note "$(grep -o 'SMOKE PASS.*renderer=[a-z_]*' "$LOGS/game-smoke.log" | head -1)"
  # The tools through real mouse and key events (fence drag, footbridge, erase, toolbar, cancel), and the flow
  # preview's isolation: the same seed with and without preview activity must end with the same wear hash.
  step game-tools tools/godot-test.sh 'TOOLS PASS' timeout 300 "$GODOT" --headless --path game --audio-driver Dummy res://tests/tools.tscn --quit-after 4000
  note "$(grep -o 'TOOLS PASS.*' "$LOGS/game-tools.log" | head -1 | cut -c1-90)"
  # The river through real input: ferry and drawbridge placed with a ghost and a reason where they cannot go, a purchase
  # refused for coin, the drawbridge raised and lowered by click, L, the badge's lever and Ctrl+1, the keeper hired,
  # both erased; then four in-game days with boats (events, chronicle, ledger, coins, sound cues).
  step game-river tools/godot-test.sh 'RIVER PASS' timeout 300 "$GODOT" --headless --path game --audio-driver Dummy res://tests/river.tscn --quit-after 20000
  note "$(grep -o 'RIVER PASS.*' "$LOGS/game-river.log" | head -1 | cut -c1-90)"
  # The river's third-pass rules as shown: wet grain at the ford, the keeper earned by use, the starting purse, the
  # flood band's edge, merged turn-back notes, the Ford tag and boats, queues never overlapping, the ledger after a jump.
  step game-river-rules tools/godot-test.sh 'RIVER RULES PASS' timeout 300 "$GODOT" --headless --path game --audio-driver Dummy res://tests/river-rules.tscn --quit-after 30000
  note "$(grep -o 'RIVER RULES PASS.*' "$LOGS/game-river-rules.log" | head -1 | cut -c1-90)"
  # The two chapters: the game opens on Chapter I (no river), the chapter bookmark and C turn the page (the leaf covers
  # the book while it is built again), Chapter II has the river rules, reset and new map stay inside the chapter.
  step game-chapter tools/godot-test.sh 'CHAPTER PASS' timeout 300 "$GODOT" --headless --path game --audio-driver Dummy res://tests/chapter.tscn --quit-after 8000
  note "$(grep -o 'CHAPTER PASS.*' "$LOGS/game-chapter.log" | head -1 | cut -c1-90)"
  # Chapter I's living town: a cottage arriving is drawn (rising, yard, mark, page count, walkers), and a forced shower
  # shows rain over the map, wet ground, mud and the weather line, then dries.
  step game-hamlet tools/godot-test.sh 'HAMLET PASS' timeout 300 "$GODOT" --headless --path game --audio-driver Dummy res://tests/hamlet.tscn --quit-after 40000
  note "$(grep -o 'HAMLET PASS.*' "$LOGS/game-hamlet.log" | head -1 | cut -c1-90)"
  # Every control exists, has a tooltip and label, toggles its state by mouse; every shortcut; focus; text sizes.
  step game-ui tools/godot-test.sh 'UI PASS' timeout 300 "$GODOT" --headless --path game --audio-driver Dummy res://tests/ui.tscn --quit-after 4000
  note "$(grep -o 'UI PASS.*' "$LOGS/game-ui.log" | head -1 | cut -c1-90)"
  # CPU time per frame with 500 walkers at 1x and 8x in Chapter II (river) then Chapter I (rain, growth at 200 walkers),
  # interface and audio code running (no rendering headless);
  # --fixed-fps 60 gives every frame 1/60 s of game time. Fails above 6 ms (1x) or 10 ms (8x) mean, or 64 bytes
  # allocated per frame. A third run at 8x drags a fence so the flow preview keeps recomputing (time gated only).
  # A rare slow step can push the preview run's worst frame over its gate. When that is the ONLY reason game-perf failed
  # (its "PERF FAIL REASONS:" line says just "worst-frame", and no error line), it is run once more: the first run stays
  # in the table as RETRY with its worst frame and any slow-step lines, and the re-run decides. Never silent.
  perf_failed_before=$FAILED
  step game-perf tools/godot-test.sh 'PERF PASS' timeout 600 "$GODOT" --headless --path game --audio-driver Dummy --fixed-fps 60 res://tests/perf.tscn --quit-after 20000
  note "$(grep -o 'PERF 500 walkers [0-9]x: cpu ms/frame mean [0-9.]*' "$LOGS/game-perf.log" | sed 's/PERF 500 walkers //; s/cpu ms\/frame mean/mean/' | tr '\n' ' ')ms"
  grep '^PERF' "$LOGS/game-perf.log" | sed 's/^/   /'
  if [ "${RESULTS[-1]}" = FAIL ] && grep -qx 'PERF FAIL REASONS: worst-frame' "$LOGS/game-perf.log" \
     && ! grep -q 'GODOT TEST FAIL: error or warning lines' "$LOGS/game-perf.log"; then
    worst=$(grep -o 'worst frame [0-9.]* ms (gate < [0-9]*) OVER' "$LOGS/game-perf.log" | head -1)
    slow=$(grep -c '^PERF slow step' "$LOGS/game-perf.log")
    echo "   game-perf failed ONLY on the worst-frame gate ($worst; $slow slow-step lines): running it once more"
    RESULTS[-1]=RETRY
    NOTES[-1]="failed only on the worst-frame gate: $worst, $slow slow-step line(s); re-run below (see build/check-logs/game-perf.log)"
    FAILED=$perf_failed_before
    step game-perf-rerun tools/godot-test.sh 'PERF PASS' timeout 600 "$GODOT" --headless --path game --audio-driver Dummy --fixed-fps 60 res://tests/perf.tscn --quit-after 20000
    note "RE-RUN after a worst-frame failure: $(grep -o 'worst frame [0-9.]* ms' "$LOGS/game-perf-rerun.log" | head -1)"
    grep '^PERF' "$LOGS/game-perf-rerun.log" | sed 's/^/   /'
  fi
  if [ "$SHOT" = 0 ]; then
    skip screenshot "--no-screenshot"
  elif ! command -v xvfb-run >/dev/null 2>&1; then
    skip screenshot "xvfb-run not installed (tools/setup-linux.sh)"
  else
    # The Mobile renderer, as the game uses, on Mesa lavapipe inside a virtual X display.
    # The whole book, then the map page after about 20 in-game days at 8x, plus close-ups (book.png, map.png, ...).
    step screenshot tools/godot-test.sh 'SCREENSHOT PASS' timeout 600 xvfb-run -a -s '-screen 0 1280x720x24' "$GODOT" --path game --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1280x720 res://tests/screenshot.tscn --quit-after 5000 -- --out-dir="$SHOTS"
    note "build/screenshots/{book,map,wear,preview,river-*}-1280x720.png + close-ups $(grep -o 'draw calls [0-9]*' "$LOGS/screenshot.log" | head -1)"
    # The interface views again at 1920x1080 (the interface is rebuilt at full scale).
    step screenshot-1080 tools/godot-test.sh 'SCREENSHOT PASS' timeout 600 xvfb-run -a -s '-screen 0 1920x1080x24' "$GODOT" --path game --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 res://tests/screenshot.tscn --quit-after 5000 -- --out-dir="$SHOTS" --ui-only
    note "build/screenshots/{book,map,wear,preview,river-*}-1920x1080.png"
    # Chapter I at both sizes: day 1, the page turn, a gate and a gravel strip, day 30 with grown houses and a new one
    # marked, rain and mud, the mud drying (hamlet-*.png, chapter-turn.png).
    step screenshot-hamlet tools/godot-test.sh 'SCREENSHOT PASS' timeout 1200 xvfb-run -a -s '-screen 0 1280x720x24' "$GODOT" --path game --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1280x720 res://tests/screenshot.tscn --quit-after 60000 -- --out-dir="$SHOTS" --hamlet-only
    note "build/screenshots/{hamlet-*,chapter-turn}-1280x720.png"
    step screenshot-hamlet-1080 tools/godot-test.sh 'SCREENSHOT PASS' timeout 1200 xvfb-run -a -s '-screen 0 1920x1080x24' "$GODOT" --path game --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 res://tests/screenshot.tscn --quit-after 60000 -- --out-dir="$SHOTS" --hamlet-only --ui-only
    note "build/screenshots/{hamlet-*,chapter-turn}-1920x1080.png"
  fi
else
  for s in game-build game-smoke game-tools game-river game-river-rules game-chapter game-hamlet game-ui game-perf screenshot; do
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
retried=0
for r in "${RESULTS[@]}"; do [ "$r" = RETRY ] && retried=1; done
if [ "$retried" = 1 ]; then echo "NOTE: game-perf failed once on its worst-frame gate only and was re-run (RETRY row above); the re-run's result counts."; fi
if [ "$FAILED" = 0 ]; then echo "CHECK PASSED"; else echo "CHECK FAILED"; fi
exit $FAILED

#!/usr/bin/env bash
# One command for everything that can be verified automatically. Exits non-zero if any step fails.
#   ./check                 sim build + fast tests (Release), Godot C# build, the town's headless smoke, game-town and the whole-age playthrough
#                           (real input) and perf tests, the menus and the book's motion, screenshots at 1920x1080, ledger
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
  # clicking to level 6 buying every kind of shop item (Extend by clicking a cottage, Pave by clicking a road), each
  # gift arrives and is drawn with its banner, a road wears in visibly, the bell by key and by click, the request card
  # and the hint line match the simulation; then the end of the age (the simulation's bot) and 30 Pave clicks, tilted
  # and flat, each on exactly the road the price tip named. Villagers never jump.
  step game-town tools/godot-test.sh 'TOWN PASS' timeout 600 "$GODOT" --headless --path game --audio-driver Dummy res://tests/town.tscn --quit-after 200000
  note "$(grep -o 'TOWN PASS.*' "$LOGS/game-town.log" | head -1 | sed 's/TOWN PASS //' | cut -c1-90)"
  # CPU time per frame with the end-of-age town (the simulation's best-payback bot plays to the charter), at 1x and 8x,
  # interface and sound running (no rendering headless); --fixed-fps 60 gives every frame 1/60 s. Fails above 6 ms (1x)
  # or 10 ms (8x) mean, the old game's budget, or above 1536 (1x) or 6144 (8x) managed bytes allocated per frame.
  step game-perf tools/godot-test.sh 'PERF PASS' timeout 600 "$GODOT" --headless --path game --audio-driver Dummy --fixed-fps 60 res://tests/perf.tscn --quit-after 20000
  note "$(grep -o 'PERF town [0-9]* villagers [0-9]x: cpu ms/frame mean [0-9.]*' "$LOGS/game-perf.log" | sed 's/PERF town [0-9]* villagers //; s/cpu ms\/frame mean/mean/' | tr '\n' ' ')ms"
  grep '^PERF' "$LOGS/game-perf.log" | sed 's/^/   /'
  # The whole first age through injected clicks and keys only, like a sensible person (1920 x 1080, 16x): bookmarks,
  # their keys, Pave and Extend by pointing until the tip names a road or cottage, the bell, then Ring in the next age
  # and the first age's page. Fails on any error, a Pave or Extend click that does not do what its tip said, a lit
  # bookmark that buys nothing, or a minute after the second with nothing to buy. Prints purchases and decisions a
  # minute per ten minutes and the charter's minute.
  step game-playthrough tools/godot-test.sh 'PLAY PASS' timeout 1500 "$GODOT" --headless --path game --audio-driver Dummy --fixed-fps 60 res://tests/play.tscn --quit-after 400000
  note "$(grep -o 'PLAY PASS charter=[0-9.]*min buys=[0-9]*' "$LOGS/game-playthrough.log" | head -1 | sed 's/PLAY PASS //')"
  grep '^PLAY ' "$LOGS/game-playthrough.log" | grep -v 'PLAY PASS' | sed 's/^/   /'
  # Round 4, the second age: a first-age town at its charter (the bot), real input rings in the next age (the bell,
  # Rising, the page turning, the river's title and page); then at speed with the bot buying: every river plot and
  # crossing drawn when given, boats where the simulation has them with their cargo, the tide dial, people waiting at
  # the ford at high water, the ferry's raft, the drawbridge's lift, the seal and its page; a save reloaded runs on exactly.
  step game-river tools/godot-test.sh 'RIVER PASS' timeout 900 "$GODOT" --headless --path game --audio-driver Dummy --fixed-fps 60 res://tests/river.tscn --quit-after 400000
  note "$(grep -o 'RIVER PASS.*' "$LOGS/game-river.log" | head -1 | sed 's/RIVER PASS //' | cut -c1-90)"
  # Night 3, the layout sheet (docs/plans/n3/layout-sheet.md, section 8): the interface audit tour's states (u01..u41)
  # each built on the sheet's eight canvases and held to the sheet: zone rectangles, no overlaps, nothing cut (also
  # with the longest texts), the middle free, only the sheet's styles, rows and key caps, TipRect; the attention budget
  # is counted and printed (it fails from the next round).
  step game-layout tools/godot-test.sh 'LAYOUT PASS' timeout 2400 "$GODOT" --headless --path game --audio-driver Dummy res://tests/layout.tscn --quit-after 1000000 -- --out-dir="$ROOT/build/layout"
  note "$(grep -o 'LAYOUT PASS.*' "$LOGS/game-layout.log" | head -1 | sed 's/LAYOUT PASS //' | cut -c1-90)"
  grep '^LAYOUT budget' "$LOGS/game-layout.log" | sed 's/^/   /'
  # The zoom from the tilted town out to the whole book by wheel notches (pitch, interface stages, the summary page
  # against the simulation, Home), a Pave click while tilted and a villager's height on screen, at 1920 x 1080.
  step game-book tools/godot-test.sh 'BOOK PASS' timeout 600 "$GODOT" --headless --path game --audio-driver Dummy --fixed-fps 60 res://tests/book.tscn --quit-after 100000
  note "$(grep -o 'BOOK PASS.*' "$LOGS/game-book.log" | head -1 | sed 's/BOOK PASS //' | cut -c1-90)"
  # The settings pages alone (their own test: store, applier, pages, keys, saving page with a fake, keyboard only).
  step game-settings tools/godot-test.sh 'SETTINGS PASS' timeout 300 "$GODOT" --headless --path game --audio-driver Dummy res://tests/settings.tscn --quit-after 20000
  # The pause menu with real input at 1920 x 1080: Esc closes the book and stops the tick, the cover menu by keyboard,
  # the settings pages and Back, Resume puts the camera back, settings take effect in the game (interface size, a
  # volume, coin numbers, a rebound key), New town asks and gives a new seed at level 1, the interface's pause control.
  step game-menu tools/godot-test.sh 'MENU PASS' timeout 600 "$GODOT" --headless --path game --audio-driver Dummy res://tests/menu.tscn --quit-after 100000
  note "$(grep -o 'MENU PASS.*' "$LOGS/game-menu.log" | head -1 | sed 's/MENU PASS //' | cut -c1-90)"
  # Round 7, the book's motion: every transition (pause, resume, cover to settings, each tab forwards and backwards,
  # settings back to the cover, the New town question, resume after the settings, and impatient Esc, Enter, clicks and
  # tab changes mid-way) through real input, sampled every frame at fixed 60 fps: continuity, eased ends, the parts'
  # overlaps and lengths, nothing on screen switched on or off, no allocation or CPU hitch, the save outside the motion,
  # the end states; then at 30 and 120 fps: the same lengths (two frames) and the same end states (tools/motion-check.sh).
  step game-motion env GODOT="$GODOT" tools/motion-check.sh
  note "$(grep -o 'MOTION RATES PASS' "$LOGS/game-motion.log" | head -1) $(grep -o 'alloc-max=[0-9]*B cpu-max=[0-9.]*ms' "$LOGS/motion-60.log" | head -1)"
  # n5, work you can watch: the villagers at work on a real town (the payback bot to the mill, then frame by frame at
  # 1x and 4x, the bell, later a handcart and the scythe), sampled every frame at 60, 30 and 120 fps: the line's spacing,
  # order and facing, outside buildings and crop; no load before the work is finished, none popping or vanishing, every
  # delivery landing; nothing changing in one frame; tools, parked cart, bell; allocation (tools/work-check.sh).
  step game-work env GODOT="$GODOT" tools/work-check.sh
  note "$(grep -o 'WORK RATES PASS' "$LOGS/game-work.log" | head -1) $(grep -o 'pairs=[0-9]* gap=[0-9.-]*m' "$LOGS/work-60.log" | head -1)"
  # Saving: the town at level 2+ with a paved road saved and the scene loaded again (same state, camera, runs on), a
  # damaged save and one from another version set aside with a note, the quit paths save, delete gives a new town.
  step game-save tools/godot-test.sh 'SAVE PASS' timeout 600 "$GODOT" --headless --path game --audio-driver Dummy res://tests/save.tscn --quit-after 100000
  note "$(grep -o 'SAVE PASS.*' "$LOGS/game-save.log" | head -1 | sed 's/SAVE PASS //' | cut -c1-90)"
  # Night 5, work you can watch (the places): a first age played in the running game and sampled every frame: a field
  # never under a third gold and its fronts moving only with work finished there, the mill's sails faster with more at
  # work, its door open exactly while it should be, every market delivery on the table, cottage doors opening for their
  # people, nothing changed in one frame (also at 30 and 120 fps), no allocation by the places (tools/work-places-check.sh).
  step game-work-places env GODOT="$GODOT" tools/work-places-check.sh
  note "$(grep -o 'WORK PLACES PASS.*' "$LOGS/work-places-60.log" | head -1 | sed 's/WORK PLACES PASS //' | cut -c1-90)"
  if [ "$SHOT" = 0 ]; then
    skip screenshot "--no-screenshot"
  elif ! command -v xvfb-run >/dev/null 2>&1; then
    skip screenshot "xvfb-run not installed (tools/setup-linux.sh)"
  else
    # The Mobile renderer, as the game uses, on Mesa lavapipe inside a virtual X display, at 1920 x 1080: the first
    # minute, the first purchase, level 3, Pave mode, level 6, Extend mode, a request, the end of the age, the zoom and
    # the menus (build/screenshots/town-*-1920x1080.png). Quick looks while working: add -- --menu-only (menus only) or
    # -- --town-quick (stops after the request shot) or -- --age-only (the charter, achievements, summary, next age, first-age page) or -- --river-only (the second age).
    # Round 5: also -- --v3-only (choice, boon, touch, hand, drive, summary with boons). The full run takes about 20 to 25
    # minutes on the shared 4-CPU machine with lavapipe (the river shots now render the riverside, not the closed cover),
    # so the safety cap is 2400 s (it was 1200 s).
    step screenshot tools/godot-test.sh 'SCREENSHOT PASS' timeout 2400 xvfb-run -a -s '-screen 0 1920x1080x24' "$GODOT" --path game --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 res://tests/screenshot.tscn --quit-after 100000 -- --out-dir="$SHOTS"
    note "build/screenshots/town-{first-minute,...,summary,book-half-closed,cover-menu,settings-*,new-town-question}-1920x1080.png"
  fi
  if [ "$SHOT" = 1 ] && command -v xvfb-run >/dev/null 2>&1; then
    # n8, the ink line (style sheet section 3): the cottage kinds, weaver cottage, mill, stall, well, a street and a
    # villager group at the play view (640 x 480 at the 1080p play view's metres per pixel and line width); the share
    # of each model's pixels the ink covers, against a ceiling per kind (AssetAudit.InkCheck.cs). About a minute.
    step game-ink tools/godot-test.sh 'ASSET AUDIT PASS' timeout 600 xvfb-run -a -s '-screen 0 1920x1080x24' "$GODOT" --path game --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 res://tests/asset-audit.tscn --quit-after 100000 -- --ink-check --out-dir="$ROOT/build/ink-check"
    note "$(grep -o '^INK [a-z0-9-]* share=[0-9.]*' "$LOGS/game-ink.log" | sed 's/^INK //; s/ share=/=/' | tr '\n' ' ')"
  else
    skip game-ink "--no-screenshot or no xvfb-run"
  fi
else
  for s in game-build game-smoke game-town game-layout game-playthrough game-river game-perf game-book game-settings game-menu game-motion game-work game-save game-work-places screenshot game-ink; do
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

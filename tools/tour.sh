#!/usr/bin/env bash
# The screenshot tour for the owner's review: one town (seed 1) played in play order from the first minute to the
# borough seal, one picture per shot of docs/review/shots-plan.json, at 1920 x 1080 under xvfb with the game's renderer
# (as the screenshot step of tools/check.sh). Writes build/tour/<id>.png and build/tour/shots.json.
#   tools/tour.sh                   the whole tour (empties build/tour first)
#   tools/tour.sh --only=a01-first-minute,d04-gravel   retake those shots (replays the town up to the last of them;
#                                   other pictures and their entries in shots.json are kept)
#   tools/tour.sh --no-build        skip the C# build
#   tools/tour.sh --notes-only      keep the pictures; write shots.json's shows/wrong again from game/tests/TourNotes.cs
# Fails (exit 1) on any printed error or warning line (tools/godot-test.sh), a failed check, or no TOUR PASS line.
# Log: build/tour/tour.log. Uses its own profile folder (user://tests/co-<checkout hash>/tour-pass/): the player's save and settings are
# never touched. Set GODOT=/path/to/godot to use a Godot binary not on PATH.
set -uo pipefail
# The body is one block so bash reads it whole first: editing this file while it runs cannot break a run.
{
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"
GODOT=${GODOT:-godot}
OUT="$ROOT/build/tour"
ONLY=""; BUILD=1
for a in "$@"; do
  case "$a" in
    --only=*|--notes-only) ONLY="$a" ;;
    --no-build) BUILD=0 ;;
    -h|--help) sed -n '2,12p' "$0"; exit 0 ;;
    *) echo "unknown option $a"; exit 2 ;;
  esac
done
command -v xvfb-run >/dev/null 2>&1 || { echo "xvfb-run not installed (tools/setup-linux.sh)"; exit 1; }
command -v "$GODOT" >/dev/null 2>&1 || { echo "godot not found (set GODOT=)"; exit 1; }
if [ -z "$ONLY" ]; then rm -rf "$OUT"; fi
mkdir -p "$OUT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
if [ "$BUILD" = 1 ]; then
  dotnet build game/TideToll.csproj -c Debug >"$OUT/build.log" 2>&1 || { tail -n 30 "$OUT/build.log"; echo "TOUR FAIL: build"; exit 1; }
fi
t0=$SECONDS
if [ "$ONLY" = --notes-only ]; then
  # No pictures are taken: headless, no xvfb (two xvfb-run -a at once can hang).
  tools/godot-test.sh 'TOUR PASS' timeout 600 "$GODOT" --headless --path game --audio-driver Dummy \
    res://tests/tour.tscn --quit-after 100000 -- --out-dir="$OUT" --notes-only >"$OUT/notes.log" 2>&1
  code=$?; grep -E 'TOUR PASS|FAIL' "$OUT/notes.log"; exit $code
fi
args=(--out-dir="$OUT")
[ -n "$ONLY" ] && args+=("$ONLY")
tools/godot-test.sh 'TOUR PASS' timeout 2400 xvfb-run -a -s '-screen 0 1920x1080x24' "$GODOT" --path game \
  --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 \
  res://tests/tour.tscn --quit-after 1000000 -- "${args[@]}" >"$OUT/tour.log" 2>&1
code=$?
# Godot itself sometimes crashes on this machine (segfault in the renderer under load: no FAIL line, no shots.json).
# Then retake once the shots that have no picture yet: the same town is replayed and only those are saved.
if [ "$code" != 0 ] && [ -z "$ONLY" ] && ! grep -q '^FAIL:' "$OUT/tour.log" && [ ! -f "$OUT/shots.json" ]; then
  missing=$(python3 -c 'import json,os,sys; p=json.load(open(sys.argv[1])); print(",".join(s["id"] for sec in p["sections"] for s in sec["shots"] if not os.path.exists(os.path.join(sys.argv[2], s["id"]+".png"))))' docs/review/shots-plan.json "$OUT")
  echo "tour: Godot stopped without a verdict (crash?); retaking: $missing"
  mv "$OUT/tour.log" "$OUT/tour-crashed.log"
  tools/godot-test.sh 'TOUR PASS' timeout 2400 xvfb-run -a -s '-screen 0 1920x1080x24' "$GODOT" --path game \
    --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 \
    res://tests/tour.tscn --quit-after 1000000 -- --out-dir="$OUT" --only="$missing" >"$OUT/tour.log" 2>&1
  code=$?
fi
grep -E '^TOUR (SHOT|NOT MADE)|TOUR PASS|FAIL' "$OUT/tour.log"
echo "tour: exit $code in $((SECONDS - t0)) s; pictures in $OUT, list in $OUT/shots.json, log $OUT/tour.log"
exit $code
}

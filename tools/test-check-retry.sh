#!/usr/bin/env bash
# Test of tools/check.sh's one re-run of game-perf (fix round, item 10): ./check runs with a stand-in Godot that passes
# every scene, except that the perf scene fails as each scenario says, and the summary must show what happened.
#   tools/test-check-retry.sh
# Scenarios: the worst-frame gate fails once, then passes (RETRY row, a game-perf-rerun PASS row, the NOTE line, CHECK
# PASSED); it fails on the mean (no re-run, CHECK FAILED); the worst-frame gate fails twice (re-run FAIL, CHECK FAILED).
# Runs the real sim build and tests and the real game build (about 2 minutes a scenario). Overwrites build/check-logs.
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT
REAL=$(command -v "${GODOT:-godot}")
cat >"$TMP/godot" <<EOF
#!/usr/bin/env bash
# Stand-in Godot: the import step and other calls go to the real one; scenes print their pass line.
case " \$* " in
  *" res://tests/perf.tscn "*)
    n=\$(cat "$TMP/perf-runs" 2>/dev/null || echo 0); n=\$((n + 1)); echo \$n >"$TMP/perf-runs"
    what=\$(echo "\$FAKE_PERF" | cut -d, -f\$n)
    echo "PERF 500 walkers 1x: cpu ms/frame mean 0.400 p95 1.0 worst 5.0 (budget mean 6; of which sim 0.2) | ok"
    case "\$what" in
      worst) echo "PERF preview gates: worst frame 27.500 ms (gate < 25) OVER; (sim 26.9 ms in 2 ticks)"
             echo "PERF slow step: tick 123 (day 13) took 24.0 ms (stand-in)"
             echo "PERF FAIL REASONS: worst-frame"; echo "PERF FAIL"; exit 1 ;;
      mean)  echo "PERF FAIL REASONS: mean"; echo "PERF FAIL"; exit 1 ;;
      *)     echo "PERF preview gates: worst frame 11.000 ms (gate < 25) ok"; echo "PERF PASS"; exit 0 ;;
    esac ;;
  *smoke.tscn*) echo "SMOKE PASS renderer=dummy" ;;
  *tools.tscn*) echo "TOOLS PASS" ;;
  *river-rules.tscn*) echo "RIVER RULES PASS" ;;
  *river.tscn*) echo "RIVER PASS" ;;
  *chapter.tscn*) echo "CHAPTER PASS" ;;
  *hamlet.tscn*) echo "HAMLET PASS" ;;
  *ui.tscn*) echo "UI PASS" ;;
  *) exec "$REAL" "\$@" ;;
esac
EOF
chmod +x "$TMP/godot"
fails=0
scenario() {
  local name=$1 perf=$2; shift 2
  rm -f "$TMP/perf-runs"
  FAKE_PERF=$perf GODOT="$TMP/godot" "$ROOT/tools/check.sh" --no-screenshot >"$TMP/out" 2>&1
  local code=$? ok=1
  for want in "$@"; do
    case "$want" in
      exit=*) [ "$code" = "${want#exit=}" ] || { echo "  $name: exit $code, expected ${want#exit=}"; ok=0; } ;;
      !*) ! grep -qE "${want#!}" "$TMP/out" || { echo "  $name: found '${want#!}'"; ok=0; } ;;
      *) grep -qE "$want" "$TMP/out" || { echo "  $name: missing '$want'"; ok=0; } ;;
    esac
  done
  if [ "$ok" = 1 ]; then echo "ok   $name"; else echo "FAIL $name"; sed -n '/^STEP/,$p' "$TMP/out" | sed 's/^/     | /'; fails=1; fi
}
scenario "worst-frame once, then pass" worst,pass exit=0 '^game-perf +RETRY +[0-9]+ +failed only on the worst-frame gate: worst frame 27.500 ms \(gate < 25\) OVER, 1 slow-step' \
  '^game-perf-rerun +PASS .*RE-RUN after a worst-frame failure' '^NOTE: game-perf failed once on its worst-frame gate only and was re-run' 'CHECK PASSED'
scenario "mean over budget: no re-run" mean,pass exit=1 '^game-perf +FAIL' '!game-perf-rerun' '!RETRY' 'CHECK FAILED'
scenario "worst-frame twice" worst,worst exit=1 '^game-perf +RETRY' '^game-perf-rerun +FAIL' 'CHECK FAILED'
if [ "$fails" = 0 ]; then echo "CHECK RETRY TEST PASS"; else echo "CHECK RETRY TEST FAIL"; fi
exit $fails

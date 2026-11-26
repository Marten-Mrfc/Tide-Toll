#!/usr/bin/env bash
# game-motion: the book's transitions sampled every frame (game/tests/MotionTest.cs) at fixed 60 frames a second (all
# the checks), then at 30 and 120: each transition must take the same time to within two frames (of 30 fps) and end in
# exactly the same state ("MOTION END" lines). Used by tools/check.sh; logs per rate in build/check-logs/motion-*.log.
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot}
LOGS="$ROOT/build/check-logs"
mkdir -p "$LOGS"
fail=0
for fps in 60 30 120; do
  echo "-- game-motion at $fps fps"
  if ! "$ROOT/tools/godot-test.sh" 'MOTION PASS' timeout 600 "$GODOT" --headless --path "$ROOT/game" --audio-driver Dummy --fixed-fps "$fps" \
      res://tests/motion.tscn --quit-after 200000 -- --fps="$fps" >"$LOGS/motion-$fps.log" 2>&1; then
    echo "   FAILED at $fps fps:"; grep -E "FAIL" "$LOGS/motion-$fps.log" | head -5 | sed 's/^/   /'; fail=1
  fi
  grep -E '^MOTION (PASS|bounds|worst|the )' "$LOGS/motion-$fps.log" | sed 's/^/   /'
done
[ "$fail" = 0 ] || exit 1
python3 - "$LOGS" <<'PY'
import re, sys
logs = sys.argv[1]
runs = {}
for fps in (30, 60, 120):
    ends = {}
    for line in open(f"{logs}/motion-{fps}.log", encoding="utf-8", errors="replace"):
        m = re.match(r"MOTION END (\S+) secs=([0-9.]+) state=(.*)$", line.strip())
        if m: ends[m.group(1)] = (float(m.group(2)), m.group(3))
    runs[fps] = ends
bad = 0
names = list(runs[60].keys())
tol = 2.0 / 30
for n in names:
    secs = {f: runs[f].get(n, (None, None))[0] for f in runs}
    states = {f: runs[f].get(n, (None, None))[1] for f in runs}
    if None in secs.values():
        print(f"MOTION RATES {n}: missing at some rate {secs}"); bad = 1; continue
    spread = max(secs.values()) - min(secs.values())
    same = len(set(states.values())) == 1
    ok = spread <= tol + 1e-6 and same
    print(f"MOTION RATES {n}: {secs[30]:.3f} / {secs[60]:.3f} / {secs[120]:.3f} s at 30 / 60 / 120 fps (spread {spread:.3f} s, bound {tol:.3f}); end state {'the same' if same else 'DIFFERS'}")
    if not same:
        for f in states: print(f"   {f}: {states[f]}")
    bad |= not ok
print("MOTION RATES PASS" if not bad else "MOTION RATES FAIL")
sys.exit(1 if bad else 0)
PY

#!/usr/bin/env bash
# Runs one Godot test scene and decides pass or fail from its output, because Godot's exit code alone cannot be
# trusted: unhandled C# exceptions are only logged. Used by tools/check.sh.
#   tools/godot-test.sh MARKER COMMAND...
# Passes when the command exits 0, prints a line containing MARKER, and prints no error line (Godot's "ERROR:",
# "SCRIPT ERROR:", "USER ERROR:", or an unhandled .NET exception). Exit-time resource-leak reports are ignored:
# they are Godot noise when a scene quits from code, not test failures.
set -uo pipefail
marker=$1; shift
out=$(mktemp)
trap 'rm -f "$out"' EXIT
"$@" >"$out" 2>&1
code=$?
cat "$out"
errors=$(grep -E '^(ERROR|SCRIPT ERROR|USER ERROR):|Unhandled exception|System\.[A-Za-z]*Exception' "$out" \
  | grep -vE 'leaked at exit|RID allocations|ObjectDB instances leaked|resources still in use at exit' || true)
if [ "$code" != 0 ]; then echo "GODOT TEST FAIL: exit code $code"; exit 1; fi
if [ -n "$errors" ]; then echo "GODOT TEST FAIL: error lines in the output:"; echo "$errors" | head -20; exit 1; fi
if ! grep -q "$marker" "$out"; then echo "GODOT TEST FAIL: no '$marker' line"; exit 1; fi
exit 0

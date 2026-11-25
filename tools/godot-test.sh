#!/usr/bin/env bash
# Runs one Godot test scene and decides pass or fail from its output, because Godot's exit code alone cannot be
# trusted: unhandled C# exceptions are only logged. Used by tools/check.sh.
#   tools/godot-test.sh MARKER COMMAND...
# Passes when the command exits 0, prints a line containing MARKER, and prints no error or warning line (Godot's
# "ERROR:", "SCRIPT ERROR:", "USER ERROR:", "WARNING:", "SHADER ERROR", or an unhandled .NET exception).
# Allowed, by exact text (numbers aside), only what cannot be fixed from game code: Godot's exit-time leak reports.
# The C# wrappers of Godot objects (cached meshes, textures, fonts) are released by the .NET finalizer, which runs
# after Godot prints them, so those counts are never zero when a C# scene quits. A leaked CanvasItem or node is NOT
# allowed: that is a real orphan (the chapter page once built three labels it never used).
set -uo pipefail
marker=$1; shift
out=$(mktemp)
trap 'rm -f "$out"' EXIT
"$@" >"$out" 2>&1
code=$?
cat "$out"
allowed='^ERROR: [0-9]+ resources still in use at exit \(run with --verbose for details\)\.$'
allowed+='|^ERROR: [0-9]+ RID allocations of type '"'"'[A-Za-z0-9_]+'"'"' were leaked at exit\.$'
allowed+='|^WARNING: [0-9]+ ObjectDB instances were leaked at exit \(run with `--verbose` for details\)\.$'
errors=$(grep -E '^(ERROR|SCRIPT ERROR|USER ERROR|WARNING|USER WARNING|SHADER ERROR):|SHADER ERROR|Unhandled exception|System\.[A-Za-z]*Exception' "$out" \
  | sed 's/\x1b\[[0-9;]*m//g' | grep -vE "$allowed" || true)
if [ "$code" != 0 ]; then echo "GODOT TEST FAIL: exit code $code"; exit 1; fi
if [ -n "$errors" ]; then echo "GODOT TEST FAIL: error or warning lines in the output:"; echo "$errors" | head -20; exit 1; fi
if ! grep -q "$marker" "$out"; then echo "GODOT TEST FAIL: no '$marker' line"; exit 1; fi
exit 0

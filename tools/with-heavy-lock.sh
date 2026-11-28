#!/usr/bin/env bash
# Runs one command while holding the heavy-run lock (docs/plans/builders-rules.md, rule 3), so that builders sharing the
# machine never take or give back the lock by hand:
#   /home/user/tide-toll/tools/with-heavy-lock.sh tools/boats-check.sh
#   /home/user/tide-toll/tools/with-heavy-lock.sh bash -c 'tools/a.sh && tools/b.sh'
# It waits until the lock is free (a lock older than 45 minutes is taken as dead), runs the command, and gives the lock
# back when the command ends, however it ends. It never removes a lock it did not take. Keep one take under 15 minutes
# (the whole ./check is the one exception). Exit code: the command's.
set -u
L=/home/user/tide-toll/build/locks/heavy
mkdir -p "$(dirname "$L")"
if [ "$#" -eq 0 ]; then echo "usage: with-heavy-lock.sh command [args...]" >&2; exit 2; fi
waited=0
until mkdir "$L" 2>/dev/null; do
  if [ -d "$L" ]; then
    made=$(stat -c %Y "$L" 2>/dev/null || date +%s)
    age=$(( $(date +%s) - made ))
    if [ "$age" -gt 2700 ]; then
      echo "with-heavy-lock: the lock is $((age / 60)) minutes old, its holder is taken as dead" >&2
      rmdir "$L" 2>/dev/null
      continue
    fi
  fi
  python3 -c "import time; time.sleep(2)"
  waited=$((waited + 2))
done
# From here on the lock is ours: give it back whenever this script ends.
trap 'rmdir "$L" 2>/dev/null' EXIT
trap 'exit 130' INT TERM
echo "with-heavy-lock: taken after ${waited}s at $(date -u +%H:%M:%S)" >&2
"$@"
code=$?
echo "with-heavy-lock: given back at $(date -u +%H:%M:%S) (exit $code)" >&2
exit $code

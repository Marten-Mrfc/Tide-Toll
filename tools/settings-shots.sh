#!/usr/bin/env bash
# Screenshots of the settings pages: the four sections at 1920 x 1080, Sound at 1280 x 720 and at 150 percent
# (at both sizes), and the Keep this? and key clash slips.
# Written to build/screenshots/settings-*.png. Needs xvfb-run.
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"
GODOT=${GODOT:-godot}
OUT="$ROOT/build/screenshots"
mkdir -p "$OUT"
fail=0
shot() { # WIDTH HEIGHT SECTION UI NAME [SLIP]
  tools/godot-test.sh 'SETTINGS SHOT PASS' timeout 300 xvfb-run -a -s "-screen 0 ${1}x${2}x24" "$GODOT" --path game \
    --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution "${1}x${2}" \
    res://tests/settings-shot.tscn --quit-after 3000 -- --out-dir="$OUT" --section="$3" --ui="$4" --name="$5" --slip="${6:-}" || fail=1
}
for s in Sound Display Keys Saving; do
  shot 1920 1080 "$s" 100 "settings-$(echo "$s" | tr 'A-Z' 'a-z')-1920x1080"
done
shot 1280 720 Sound 100 settings-sound-1280x720
shot 1920 1080 Sound 150 settings-sound-1920x1080-ui150
shot 1280 720 Sound 150 settings-sound-1280x720-ui150
shot 1920 1080 Display 100 settings-display-keep-1920x1080 keep
shot 1920 1080 Keys 100 settings-keys-clash-1920x1080 clash
exit $fail

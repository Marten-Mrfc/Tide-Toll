#!/usr/bin/env bash
# The asset audit: a picture of every asset as the player sees it and close up, an index, and contact sheets.
#   tools/asset-audit.sh [--only=name,name] [--out=DIR] [--no-build] [--skip=models,flat,sounds,sheets]
#     --no-town skips the town pass (the bot's run to the seal): model pictures only, about a minute.
#     DIR defaults to build/n3/assets-audit/before (in the main checkout when run from a worktree under build/wt).
# Models (every assets/models/*.glb, plus villagers with each cargo and Load step) are pictured inside the running
# game (game/tests/asset-audit.tscn, AssetAudit.cs) under xvfb with the Mobile renderer, as tools/tour.sh does:
#   <name>-normal.png  640 x 480, play zoom and tilt, on the game's ground, sun and ink, one villager beside it
#   <name>-close.png   640 x 480, filling the picture, three-quarter view at the game's tilt
# Textures and icons (tools/asset-audit/flat.py): <name>.png on Paper at real size and enlarged.
# Sounds (tools/asset-audit/sounds.py): sound-<name>.png waveform and spectrogram, and sounds.md.
# Then tools/asset-audit/index.py writes assets.json and sheet-<kind>-<n>.png (at most 12 assets a sheet).
# --only limits the model pictures to those names (the index and sheets are rebuilt from what is in DIR).
set -uo pipefail
{
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"
GODOT=${GODOT:-godot}
MAIN=$ROOT; case "$ROOT" in */build/wt/*) MAIN=${ROOT%%/build/wt/*} ;; esac
OUT="$MAIN/build/n3/assets-audit/before"
ONLY=""; BUILD=1; SKIP=""; NOTOWN=0
for a in "$@"; do
  case "$a" in
    --only=*) ONLY="$a" ;;
    --out=*) OUT=$(realpath -m "${a#--out=}") ;;
    --no-build) BUILD=0 ;;
    --no-town) NOTOWN=1 ;;
    --skip=*) SKIP="${a#--skip=}" ;;
    -h|--help) sed -n '2,13p' "$0"; exit 0 ;;
    *) echo "unknown option $a"; exit 2 ;;
  esac
done
mkdir -p "$OUT"
skip() { [[ ",$SKIP," == *",$1,"* ]]; }
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
if ! skip models; then
  tools/sync-assets.sh >/dev/null 2>&1 || true
  "$GODOT" --headless --path game --import >/dev/null 2>&1 || true   # changed .glb files are re-imported before the pictures
  if [ "$BUILD" = 1 ]; then
    dotnet build game/TideToll.csproj -c Debug >"$OUT/build.log" 2>&1 || { tail -n 30 "$OUT/build.log"; echo "AUDIT FAIL: build"; exit 1; }
  fi
  args=(--out-dir="$OUT"); [ -n "$ONLY" ] && args+=("$ONLY"); [ "$NOTOWN" = 1 ] && args+=(--no-town)
  for try in 1 2; do
    timeout 2400 xvfb-run -a -s '-screen 0 1920x1080x24' "$GODOT" --path game \
      --rendering-method mobile --rendering-driver vulkan --audio-driver Dummy --resolution 1920x1080 \
      res://tests/asset-audit.tscn --quit-after 1000000 -- "${args[@]}" >"$OUT/models.log" 2>&1
    grep -q 'ASSET AUDIT PASS' "$OUT/models.log" && break
    grep -q '^FAIL:' "$OUT/models.log" && { grep -A5 '^FAIL:' "$OUT/models.log"; exit 1; }
    echo "audit: Godot stopped without a verdict (crash?); running once more"
  done
  grep -E 'ASSET AUDIT PASS|FAIL' "$OUT/models.log"
  if [ -n "$ONLY" ] && [ -f "$OUT/models-only.jsonl" ]; then
    python3 - "$OUT" <<'EOF'
import json, sys, os
d = sys.argv[1]; full = os.path.join(d, "models.jsonl"); only = os.path.join(d, "models-only.jsonl")
rows = {}
for p in (full, only):
    if os.path.exists(p):
        for l in open(p):
            if l.strip(): r = json.loads(l); rows[r["name"]] = r
open(full, "w").write("".join(json.dumps(r) + "\n" for r in rows.values())); os.remove(only)
EOF
  fi
fi
skip flat || python3 tools/asset-audit/flat.py "$OUT" || echo "audit: flat pictures failed"
skip sounds || python3 tools/asset-audit/sounds.py "$OUT" || echo "audit: sound pictures failed"
skip sheets || python3 tools/asset-audit/index.py "$OUT" || { echo "AUDIT FAIL: index"; exit 1; }
echo "audit: $OUT"
}

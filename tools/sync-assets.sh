#!/usr/bin/env bash
# Mirrors the shipping asset folders from assets/ into game/assets/ so Godot can load them as
# res://assets/... (Godot only sees files inside its project folder). game/assets/ is generated
# and gitignored: edit assets/, never game/assets/. Generator code (assets/src) and the ledger stay out.
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
DST="$ROOT/game/assets"
mkdir -p "$DST"
for d in models textures icons fonts audio; do
  if [ -d "$ROOT/assets/$d" ]; then
    mkdir -p "$DST/$d"
    if command -v rsync >/dev/null; then
      # Keeps Godot's .import sidecars, so unchanged files are not imported again, and the palette atlas Godot
      # extracts from each .glb (<model>_palette.png; asset names never contain "_"), so models are not re-imported.
      rsync -a --delete --exclude '*.import' --exclude '*_palette.png' "$ROOT/assets/$d/" "$DST/$d/"
    else
      rm -rf "$DST/$d"; mkdir -p "$DST/$d"; cp -r "$ROOT/assets/$d/." "$DST/$d/"
    fi
  else
    rm -rf "$DST/$d"
  fi
done
echo "synced assets/ -> game/assets/ ($(find "$DST" -type f ! -name '*.import' | wc -l) files)"

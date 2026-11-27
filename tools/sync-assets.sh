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
      # No rsync (tools/setup-linux.sh installs it): same result by hand. Copy new or changed files, then delete
      # files that are gone from assets/, but never Godot's .import sidecars or extracted *_palette.png atlases
      # (a sidecar whose asset is gone is removed, as Godot would orphan it).
      (cd "$ROOT/assets/$d" && find . -type f -print0) | while IFS= read -r -d '' f; do
        if [ ! -f "$DST/$d/$f" ] || ! cmp -s "$ROOT/assets/$d/$f" "$DST/$d/$f"; then
          mkdir -p "$(dirname "$DST/$d/$f")"; cp -p "$ROOT/assets/$d/$f" "$DST/$d/$f"
        fi
      done
      (cd "$DST/$d" && find . -type f -print0) | while IFS= read -r -d '' f; do
        case "$f" in
          *_palette.png.import) ;;
          *.import) [ -f "$ROOT/assets/$d/${f%.import}" ] || rm -f "$DST/$d/$f" ;;
          *_palette.png) ;;
          *) [ -f "$ROOT/assets/$d/$f" ] || rm -f "$DST/$d/$f" ;;
        esac
      done
      find "$DST/$d" -mindepth 1 -type d -empty -delete
    fi
  else
    rm -rf "$DST/$d"
  fi
done
# The Atkinson Hyperlegible files are only the source of Tide Legible (assets/src/2d/fonts-plain-zero.py, plain
# zero); the game loads Tide Legible, so the originals are kept out of the game and of the export.
rm -f "$DST"/fonts/AtkinsonHyperlegible-*.ttf "$DST"/fonts/AtkinsonHyperlegible-*.ttf.import
# Icons are imported at 4x (game/project.godot, importer_defaults); drop sidecars from before that setting so
# Godot imports them again with it.
{ grep -l '^svg/scale=1.0' "$DST"/icons/*.svg.import 2>/dev/null || true; } | xargs -r rm -f
echo "synced assets/ -> game/assets/ ($(find "$DST" -type f ! -name '*.import' | wc -l) files)"

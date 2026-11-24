#!/usr/bin/env bash
# Builds a Windows x86_64 release of the game into build/windows/ from Linux (no wine needed).
# Needs the Godot 4.7 .NET export templates (tools/setup-linux.sh installs them).
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot}
OUT="$ROOT/build/windows"
TD="$HOME/.local/share/godot/export_templates/4.7.stable.mono"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
if [ ! -f "$TD/windows_release_x86_64.exe" ]; then
  echo "export templates missing in $TD (run tools/setup-linux.sh)"; exit 1
fi
rm -rf "$OUT"; mkdir -p "$OUT"
cd "$ROOT/game"
[ -d .godot ] || "$GODOT" --headless --path . --import
# Godot exits 0 even when the C# publish step fails, so the output is checked below.
"$GODOT" --headless --path . --export-release "Windows Desktop" "$OUT/TideToll.exe"
DATA="$OUT/data_TideToll_windows_x86_64"
fail() { echo "EXPORT FAIL: $1"; exit 1; }
[ -f "$OUT/TideToll.exe" ] || fail "no TideToll.exe"
[ -f "$OUT/TideToll.pck" ] || fail "no TideToll.pck"
[ -f "$DATA/TideToll.dll" ] || fail "no TideToll.dll in $DATA (C# publish failed)"
[ -f "$DATA/TideToll.Sim.dll" ] || fail "no TideToll.Sim.dll in $DATA"
file "$OUT/TideToll.exe" | grep -q "PE32+ executable" || fail "TideToll.exe is not a PE32+ executable"
echo "EXPORT OK build/windows: $(du -sh "$OUT" | cut -f1), $(find "$OUT" -type f | wc -l) files (not run: no Windows here)"

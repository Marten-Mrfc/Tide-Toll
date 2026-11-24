#!/usr/bin/env bash
# Installs everything ./check needs on a fresh Ubuntu 24.04 machine: the .NET 8 SDK,
# Godot 4.7 .NET, the Godot export templates, and Mesa/Vulkan/xvfb for screenshots; plus the asset
# tooling (tools/build-assets.sh): ffmpeg, rsync, and the Python packages in tools/requirements-assets.txt.
# Idempotent and non-interactive. Run as root (or with passwordless sudo).
#   tools/setup-linux.sh            everything
#   tools/setup-linux.sh --no-templates   skip the 1.2 GB export templates (no Windows export)
set -euo pipefail
GODOT_VERSION=4.7
REL=https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}-stable
GODOT_DIR=/opt/godot/${GODOT_VERSION}
GODOT_BIN=${GODOT_DIR}/Godot_v${GODOT_VERSION}-stable_mono_linux.x86_64
TEMPLATES=1
[ "${1:-}" = "--no-templates" ] && TEMPLATES=0

SUDO=""
[ "$(id -u)" -ne 0 ] && SUDO="sudo -n"
export DEBIAN_FRONTEND=noninteractive DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "== apt packages"
$SUDO apt-get update -y
# dotnet-sdk-8.0 because GodotSharp 4.7 targets net8.0. xvfb + Mesa (GL and Vulkan) let the
# screenshot test render with llvmpipe/lavapipe on a machine with no GPU.
$SUDO apt-get install -y --no-install-recommends \
  ca-certificates curl unzip file python3 dotnet-sdk-8.0 \
  xvfb xauth libgl1-mesa-dri mesa-vulkan-drivers libvulkan1 vulkan-tools \
  libasound2t64 libfontconfig1 libxcursor1 libxinerama1 libxi6 libxrandr2 libxkbcommon0
# Asset tooling: ffmpeg (Ogg Vorbis, loudness, PNG decode), rsync (tools/sync-assets.sh), Python packages.
$SUDO apt-get install -y --no-install-recommends ffmpeg rsync python3-pip python3-numpy python3-scipy

echo "== Python packages for assets/src (tools/requirements-assets.txt)"
REQ="$(cd "$(dirname "$0")" && pwd)/requirements-assets.txt"
if ! python3 -c "import numpy, scipy" 2>/dev/null; then
  # Ubuntu's system Python is "externally managed"; fall back to --break-system-packages there.
  $SUDO python3 -m pip install -r "$REQ" || $SUDO python3 -m pip install --break-system-packages -r "$REQ"
fi
python3 -c "import numpy, scipy; print('numpy', numpy.__version__, 'scipy', scipy.__version__)"

echo "== Godot ${GODOT_VERSION} .NET"
if [ ! -x "$GODOT_BIN" ]; then
  T=$(mktemp -d)
  curl -fsSL -o "$T/g.zip" "$REL/Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64.zip"
  unzip -q -o "$T/g.zip" -d "$T/x"
  $SUDO mkdir -p "$GODOT_DIR"
  $SUDO cp -r "$T/x/Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64/." "$GODOT_DIR/"
  $SUDO chmod +x "$GODOT_BIN"
  rm -rf "$T"
fi
printf '#!/bin/sh\nexec %s "$@"\n' "$GODOT_BIN" | $SUDO tee /usr/local/bin/godot >/dev/null
$SUDO chmod +x /usr/local/bin/godot

# Godot.NET.Sdk ships only inside the Godot download. The editor registers it with NuGet when it
# opens; a headless machine never opens the editor, so register it here (user-level NuGet config).
SRC_NAME=godot-${GODOT_VERSION}-local
if ! dotnet nuget list source 2>/dev/null | grep -q "$SRC_NAME"; then
  dotnet nuget add source "$GODOT_DIR/GodotSharp/Tools/nupkgs" -n "$SRC_NAME"
fi

if [ "$TEMPLATES" = 1 ]; then
  echo "== export templates"
  TD="$HOME/.local/share/godot/export_templates/${GODOT_VERSION}.stable.mono"
  if [ ! -f "$TD/windows_release_x86_64.exe" ]; then
    T=$(mktemp -d)
    curl -fsSL -o "$T/t.tpz" "$REL/Godot_v${GODOT_VERSION}-stable_mono_export_templates.tpz"
    unzip -q -o "$T/t.tpz" -d "$T/x"
    mkdir -p "$TD"
    cp -r "$T/x/templates/." "$TD/"
    rm -rf "$T"
  fi
fi

echo "== versions"
ffmpeg -version | head -n 1
rsync --version | head -n 1   # tools/sync-assets.sh keeps Godot's .import sidecars only with rsync (or its slower fallback)
dotnet --version
godot --version

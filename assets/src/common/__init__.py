"""Shared helpers for the asset generators under assets/src/ (see assets/src/README.md).

Import from a generator after putting assets/src on the path (build-assets.sh sets PYTHONPATH; the
two-line bootstrap in the README works when you run a script by hand):

    import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
    from common import palette as P, glb, png, audio, svg, ledger

Modules: palette (colours, atlas), glb (low-poly mesh builder + .glb writer), png (PNG write/read,
tiling noise, palette snapping and audits), svg (icon lint + rasterise via Godot), audio (synthesis,
WAV/Ogg, loudness/true peak, spectrogram), ledger (ledger fragment rows).
Dependencies: numpy and scipy only (tools/requirements-assets.txt), plus ffmpeg and Godot on PATH.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]      # repository root
ASSETS = ROOT / "assets"
SRC = ASSETS / "src"


def asset_path(rel: str) -> Path:
    """Path under assets/ (for example 'models/cottage.glb'); creates the parent folder."""
    p = ASSETS / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    return p


def rel(p) -> str:
    """Repository-relative POSIX path, as the ledger's `item` column wants it."""
    return Path(p).resolve().relative_to(ROOT).as_posix()

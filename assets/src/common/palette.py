"""Tide & Toll palette, read from the Palette table in docs/asset-pipeline.md (the single source of truth).

Rebuild the atlas and the JSON copy (run from the repository root):
    python3 assets/src/common/palette.py
writes assets/textures/palette.png (256 x 256, 16 x 16 grid of 16 px swatches, swatch i at column i % 16,
row i // 16, table order, Paper = 0) and assets/src/common/palette.json.

In code:
    from common import palette as P
    P.rgb("Meadow") -> (127, 163, 91)    P.hex_of("thatch") -> "C9A55A"    P.uv("Mud") -> (u, v) swatch centre
    P.index("Paper") -> 0                P.nearest("#7FA35C") -> "Meadow"
Names accept the full palette name, a short alias (below) or the swatch index.
STATE holds the Okabe-Ito state colours (UI and icons only, never on models).
"""
import json
import re
import sys
from pathlib import Path

if __package__ in (None, ""):            # run as a script: make `common` importable
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    __package__ = "common"

from . import ROOT, asset_path, rel  # noqa: E402

DOC = ROOT / "docs" / "asset-pipeline.md"
JSON_PATH = Path(__file__).with_name("palette.json")
ATLAS_REL = "textures/palette.png"
SWATCH = 16
ATLAS = 256


def _parse_doc(text: str):
    """Rows of the table under '## Palette': | Name | `#HEX` | Use |, plus the 'State colours' line."""
    sec = text.split("\n## Palette", 1)[1].split("\n## ", 1)[0]
    pal = []
    for line in sec.splitlines():
        m = re.match(r"\|\s*([^|`]+?)\s*\|\s*`#([0-9A-Fa-f]{6})`\s*\|\s*([^|]*?)\s*\|", line)
        if m:
            pal.append({"name": m.group(1), "hex": m.group(2).upper(), "use": m.group(3)})
    state = []
    sl = next((l for l in sec.splitlines() if l.startswith("State colours")), "")
    for m in re.finditer(r"([a-z][a-z ]*?)\s+`#([0-9A-Fa-f]{6})`\s+\(([^)]+)\)", sl):
        state.append({"name": m.group(1).strip(), "hex": m.group(2).upper(), "shape": m.group(3).strip()})
    if len(pal) < 10:
        raise SystemExit(f"palette: could not read the Palette table from {DOC}")
    return pal, state


PALETTE_ROWS, STATE = _parse_doc(DOC.read_text(encoding="utf-8"))
PALETTE = [(r["name"], r["hex"]) for r in PALETTE_ROWS]
NAMES = [n for n, _ in PALETTE]
INDEX = {n: i for i, n in enumerate(NAMES)}
_HEX = dict(PALETTE)
# Short aliases so generators stay readable. Unknown aliases simply fail with a KeyError.
ALIAS = {"paper": "Paper", "ink": "Ink", "meadow": "Meadow", "bent": "Bent grass", "trail": "Trail earth",
         "packed": "Packed earth", "earth": "Packed earth", "mud": "Mud", "river": "River", "water": "River",
         "deep": "Deep water", "thatch": "Reed and thatch", "reed": "Reed and thatch", "brick": "Brick",
         "slate": "Slate roof", "soot": "Soot brick", "iron": "Cast-iron green", "concrete": "Concrete",
         "stone": "Concrete", "mudflat": "Wet mudflat", "lime": "Limewash", "limewash": "Limewash",
         "snow": "Snow", "night": "Night tint"}
ALIAS = {k: v for k, v in ALIAS.items() if v in INDEX}


def resolve(name) -> str:
    """Palette name, alias (case-insensitive) or index -> canonical palette name."""
    if isinstance(name, int):
        return NAMES[name]
    if name in INDEX:
        return name
    low = name.lower()
    for n in NAMES:
        if n.lower() == low:
            return n
    return ALIAS[low]


def index(name) -> int: return INDEX[resolve(name)]
def hex_of(name) -> str: return _HEX[resolve(name)]
def rgb(name): h = hex_of(name); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))
def rgb01(name): return tuple(c / 255 for c in rgb(name))
def srgb_to_linear(c): return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
def linear01(name): return tuple(srgb_to_linear(c) for c in rgb01(name))


def uv(name):
    """UV of the centre of the swatch (glTF convention: v = 0 at the top of the image)."""
    i = index(name)
    return ((i % 16 + 0.5) / 16, (i // 16 + 0.5) / 16)


def hex_to_rgb(h: str):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def nearest(colour) -> str:
    """Nearest palette name to an (r, g, b) tuple or '#RRGGBB' string (plain RGB distance)."""
    c = hex_to_rgb(colour) if isinstance(colour, str) else tuple(colour)
    return min(NAMES, key=lambda n: sum((a - b) ** 2 for a, b in zip(c, rgb(n))))


def array(names=None):
    """numpy (N, 3) uint8 array of palette colours (all of them by default)."""
    import numpy as np
    return np.array([rgb(n) for n in (names or NAMES)], np.uint8)


def atlas_array():
    """256 x 256 x 3 uint8 atlas. Unused swatches are Paper."""
    import numpy as np
    a = np.empty((ATLAS, ATLAS, 3), np.uint8)
    a[:] = rgb("Paper")
    for i, n in enumerate(NAMES):
        x, y = (i % 16) * SWATCH, (i // 16) * SWATCH
        a[y:y + SWATCH, x:x + SWATCH] = rgb(n)
    return a


def atlas_png_bytes() -> bytes:
    from .png import encode_png
    return encode_png(atlas_array())


def build():
    """Write assets/textures/palette.png and assets/src/common/palette.json."""
    from .png import write_png
    out = asset_path(ATLAS_REL)
    write_png(atlas_array(), out)
    data = {"source": "docs/asset-pipeline.md (## Palette)", "atlas": f"assets/{ATLAS_REL}",
            "layout": {"size": ATLAS, "swatch": SWATCH, "columns": 16, "rule": "swatch i at column i % 16, row i // 16"},
            "palette": [{"index": i, **r, "uv": list(uv(r["name"]))} for i, r in enumerate(PALETTE_ROWS)],
            "state": STATE, "aliases": ALIAS}
    JSON_PATH.write_text(json.dumps(data, indent=1) + "\n", encoding="utf-8")
    return out


if __name__ == "__main__":
    p = build()
    print(f"palette: {len(NAMES)} colours, {len(STATE)} state colours -> {rel(p)}, {rel(JSON_PATH)}")

"""SVG icon helpers: lint against the icon rules and rasterise with Godot (the same SVG renderer the game uses).

    from common import svg
    svg.lint("assets/icons/tool-fence.svg") -> []            # list of problems; empty = OK
    svg.rasterise("assets/icons/tool-fence.svg", "build/icons/tool-fence-24.png", size=24)
    svg.sheet(["assets/icons/tool-fence.svg", ...], "build/icons/sheet.png", sizes=(24, 48, 96))

Shell: python3 -m common.svg lint FILE.svg...   |   python3 -m common.svg sheet OUT.png FILE.svg...
(with PYTHONPATH=assets/src). Rasterising runs Godot headless (tools/contact-sheet/svg_to_png.gd); cairosvg is
not used because its licence (LGPL-3.0) is not on the ledger allow list.
"""
import fcntl
import os
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

import numpy as np

from . import ROOT, palette as P

GODOT = os.environ.get("GODOT", "godot")
SHEET_PROJ = ROOT / "tools" / "contact-sheet"


def lint(path, icon_style=False):
    """Icon rules from docs/asset-pipeline.md: viewBox 0 0 24 24, plain SVG (no image/text/script/foreignObject/
    style links), colours from the palette or the state colours (plus none/currentColor), Ink strokes round.
    icon_style=True also checks the house drawing language (assets/src/2d/icons.py): every drawn element has a
    2-unit Ink stroke and a Paper or Ink fill (closed shapes, so the icon reads on dark grounds too), and at least
    one element is filled with Paper."""
    path = Path(path)
    probs = []
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as e:
        return [f"not well-formed XML: {e}"]
    if root.get("viewBox", "").split() != ["0", "0", "24", "24"]:
        probs.append(f"viewBox is {root.get('viewBox')!r}, want '0 0 24 24'")
    allowed = {h.upper() for _, h in P.PALETTE} | {s["hex"] for s in P.STATE}
    for el in root.iter():
        tag = el.tag.split("}")[-1]
        if tag in ("image", "text", "script", "foreignObject", "style", "font"):
            probs.append(f"<{tag}> is not allowed (plain shapes and paths only)")
        for k, v in el.attrib.items():
            if k.split("}")[-1] == "href" and not v.startswith("#"):
                probs.append(f"external reference {v!r}")
            if k.startswith("on"):
                probs.append(f"event attribute {k}")
        style = " ".join(f"{k}:{v};" for k, v in el.attrib.items()) + el.attrib.get("style", "")
        for h in re.findall(r"#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})\b", style):
            h = "".join(c * 2 for c in h) if len(h) == 3 else h
            if h.upper() not in allowed:
                probs.append(f"colour #{h} on <{tag}> is not in the palette (nearest: {P.nearest('#' + h)})")
        for m in re.findall(r"rgb\(|hsl\(", style):
            probs.append(f"use #RRGGBB palette hex, not {m}")
        if el.get("stroke") and el.get("stroke") != "none":
            if el.get("stroke-linecap", "round") != "round" or el.get("stroke-linejoin", "round") != "round":
                probs.append(f"<{tag}> stroke should have round caps and joins")
    if icon_style:
        ink, paper = "#" + P.hex_of("Ink"), "#" + P.hex_of("Paper")
        drawn = [el for el in root.iter() if el.tag.split("}")[-1] in ("path", "circle", "rect", "ellipse",
                                                                         "polygon", "polyline", "line")]
        for el in drawn:
            tag = el.tag.split("}")[-1]
            if (el.get("stroke") or "").upper() != ink.upper():
                probs.append(f"<{tag}> needs an Ink stroke ({ink})")
            if el.get("stroke-width") not in ("2", "2.0"):
                probs.append(f"<{tag}> stroke-width {el.get('stroke-width')!r}: the icon weight is 2")
            if (el.get("fill") or "").upper() not in (ink.upper(), paper.upper()):
                probs.append(f"<{tag}> fill {el.get('fill')!r}: closed shapes are filled Paper (or Ink accents)")
        if not any((el.get("fill") or "").upper() == paper.upper() for el in drawn):
            probs.append("no Paper-filled shape: the icon would vanish on a dark ground")
    return sorted(set(probs))


def rasterise(svg_path, png_path, size=24, bg=None):
    """Render an SVG at size x size px with Godot. bg = palette name/hex for a backdrop (default: transparent)."""
    return _godot_raster([(Path(svg_path), Path(png_path), size)], bg)


def _godot_raster(jobs, bg):
    with tempfile.NamedTemporaryFile("w", suffix=".txt", delete=False) as f:
        for s, p, n in jobs:
            p.parent.mkdir(parents=True, exist_ok=True)
            f.write(f"{s.resolve()}\t{p.resolve()}\t{n}\n")
        listfile = f.name
    bgarg = []
    if bg:
        bgarg = [f"--bg=#{P.hex_of(bg) if not str(bg).startswith('#') else str(bg)[1:]}"]
    lockdir = ROOT / "build" / "contact-sheets"
    lockdir.mkdir(parents=True, exist_ok=True)
    try:
        with open(lockdir / ".render.lock", "w") as lock:   # same lock as tools/render-contact-sheet.sh
            fcntl.flock(lock, fcntl.LOCK_EX)
            r = subprocess.run([GODOT, "--headless", "--path", str(SHEET_PROJ), "-s", "res://svg_to_png.gd",
                                "--",
                                f"--list={listfile}", *bgarg], capture_output=True, text=True, timeout=300)
    finally:
        os.unlink(listfile)
    if "SVG OK" not in r.stdout:
        raise RuntimeError(f"svg_to_png failed:\n{r.stdout[-2000:]}\n{r.stderr[-2000:]}")
    return [p for _, p, _ in jobs]


def sheet(svgs, out_png, sizes=(24, 48, 96), bg="Paper", gap=12):
    """One preview PNG: a row per icon, each at every size, on Paper. Look at the 24 px column."""
    from .png import read_png, write_png
    tmp = Path(tempfile.mkdtemp(prefix="icons-"))
    jobs = [(Path(s), tmp / f"{i}-{n}.png", n) for i, s in enumerate(svgs) for n in sizes]
    _godot_raster(jobs, bg)
    rowh = max(sizes) + gap
    W = sum(sizes) + gap * (len(sizes) + 1)
    img = np.empty((rowh * len(svgs) + gap, W, 3), np.uint8)
    img[:] = P.rgb(bg)
    for i, _ in enumerate(svgs):
        x = gap
        for n in sizes:
            a = read_png(tmp / f"{i}-{n}.png")
            y = gap + i * rowh + (max(sizes) - n) // 2
            img[y:y + n, x:x + n] = a
            x += n + gap
    for f in tmp.iterdir():
        f.unlink()
    tmp.rmdir()
    return write_png(img, out_png)


if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "lint":
        bad = 0
        for f in sys.argv[2:]:
            pr = lint(f)
            bad += bool(pr)
            print(f, "OK" if not pr else "\n  " + "\n  ".join(pr))
        sys.exit(1 if bad else 0)
    elif len(sys.argv) >= 4 and sys.argv[1] == "sheet":
        print(sheet(sys.argv[3:], sys.argv[2]))
    else:
        print(__doc__)

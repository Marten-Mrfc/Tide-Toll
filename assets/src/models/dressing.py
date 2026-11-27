# python3 assets/src/models/dressing.py [name ...]  ->  assets/models/dress-*.glb
# Night 3, round 3 (asset track LF): small touches placed by game/scripts/town/TownLife.cs.
#
#   dress-bunting  two thin Packed earth poles 5 m apart (x = -2.5..2.5), 2.0 m high, and a sagging line of eight triangular flags
#                  in Brick, Reed and thatch and River. The game flutters the flags (shader: everything below the line
#                  between the poles).
#   dress-rocks    a low outcrop of three to four stones on the river bank, 2.6 x 1.8 m, 0.9 m high, Concrete with
#                  Slate roof faces. Seeded.
#   dress-gull     a gull standing, 0.4 m: Limewash body, Slate roof wings with Ink tips, Reed and thatch bill and legs.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import random

from common import glb, ledger, asset_path
from _kit import beam, stone

NOTE = "made in code here; free asset sites blocked from the build container (checked 2026-11-25)"
SEED = 31


def bunting():
    m = glb.Mesh()
    for x in (-2.5, 2.5):
        m.add_cylinder((x, 0, 0), 0.06, 2.0, "Packed earth", segments=5)
    pts = [(-2.5 + 5 * i / 9, 1.92 - 0.3 * math.sin(math.pi * i / 9)) for i in range(10)]
    for a, b in zip(pts, pts[1:]):
        beam(m, (a[0], a[1], 0), (b[0], b[1], 0), 0.03, 0.03, "Paper", ends=False)
    cols = ["Brick", "Reed and thatch", "River"]
    for i in range(8):
        x0, y0 = pts[i + 1]
        x1 = x0 + 0.4
        y1 = y0 - 0.35 * math.sin(math.pi * 0.4 / 5) * 0 - 0.02
        tri = [(x0 - 0.2, y0, 0), (x0 + 0.2, y0 - 0.02, 0), (x0, y0 - 0.5, 0)]
        m.add_poly(tri, cols[i % 3], facing=(0, 0, 1))
        m.add_poly(list(reversed(tri)), cols[i % 3], facing=(0, 0, -1))
    return m


def rocks():
    rng = random.Random(SEED)
    m = glb.Mesh()
    for (x, z, r, h) in ((-0.5, 0.1, 0.8, 0.9), (0.55, -0.2, 0.6, 0.6), (0.2, 0.6, 0.45, 0.4), (-1.15, -0.55, 0.4, 0.3), (1.05, 0.55, 0.3, 0.2)):
        stone(m, rng, (x, 0, z), r, h, top="Concrete", side="Slate roof", n=6, amount=0.22, taper=0.7)
    return m


def gull():
    """Limewash all over with Slate roof wings edged in Ink, Reed and thatch bill and legs."""
    m = glb.Mesh()
    for x in (-0.04, 0.04):
        beam(m, (x, 0, 0), (x, 0.12, 0), 0.03, 0.03, "Reed and thatch")
    m.add_sphere((0, 0.2, -0.02), 0.11, "Limewash", rings=2, segments=6, squash=0.8)
    for sx in (-1, 1):
        beam(m, (sx * 0.08, 0.25, 0.04), (sx * 0.08, 0.21, -0.2), 0.04, 0.1, "Slate roof")
    beam(m, (0, 0.23, -0.16), (0, 0.2, -0.28), 0.14, 0.04, "Ink")                  # dark wing edge / tips
    m.add_sphere((0, 0.3, 0.08), 0.06, "Limewash", rings=2, segments=5)
    beam(m, (0, 0.3, 0.12), (0, 0.29, 0.2), 0.03, 0.03, "Reed and thatch")
    return m


BUILDS = [
    ("dress-bunting", bunting(), 120, "Town life: bunting beside the market stall"),
    ("dress-rocks", rocks(), 200, "Town life: rock outcrops on the river bank"),
    ("dress-gull", gull(), 140, "Town life: a gull at the landing"),
]

if __name__ == "__main__":
    only = [a for a in sys.argv[1:] if not a.startswith("--")]
    for name, parts, budget, used in BUILDS:
        if only and name not in only:
            continue
        out = asset_path(f"models/{name}.glb")
        print(glb.report(out, glb.write_glb(out, parts, max_tris=budget, ink="soft" if name == "dress-gull" else None)))
        ledger.add("models", out, kind="model", generator=__file__, seed=SEED if name == "dress-rocks" else None,
                   note=NOTE, used_in=used, verified=True)

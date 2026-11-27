# python3 assets/src/models/desk.py [name ...]  ->  assets/models/desk-*.glb
# Night 3, asset round 2 (track BK): the small things lying on the table round the book, at the edges of the book
# view (BookBuilder.AddDeskThings places them; never under the book or where the shut cover's lettering is read).
# Units: centimetres of a real desk (the game scales them by BookBuilder.DeskScale = 10, so a 30 cm page is 300 m).
# Palette colours only, one per face, details standing proud so the ink pass finds them. Deterministic.
#
#   desk-inkwell   a squat eight-sided ink pot (Ink glass, a Paper glint on the shoulder and a glint band) with a brass
#                  (Thatch) collar and the ink showing in its mouth; a goose quill lying beside it, nib to the pot:
#                  a Limewash shaft and a long vane in Paper and Limewash, barbs split near the tip.
#   desk-candle    a Thatch brass chamberstick (dish, ring handle) with a Limewash candle, a drip of wax, a black
#                  wick and a teardrop flame (Thatch, a Paper core on its front).
#   desk-seal      a wax seal stamp standing on its brass foot (turned Packed earth handle, a Mud band, a knob),
#                  a stick of Brick sealing wax and a pressed Brick seal lying beside it.
#   desk-ribbon    a loose Brick silk ribbon lying in two easy curves on the table, its ends cut in a swallowtail,
#                  with a Soot brick underside where it turns over.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import numpy as np

from common import glb, ledger, asset_path
from _kit import beam, tube

NOTE = "free asset sites blocked from the build container (checked 2026-11-25)"
BUDGET = 250


def inkwell():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 3.6, 3.9, 2.6, "Ink", segments=8, cap_top=False)
    m.add_frustum((0, 2.6, 0), 3.9, 2.0, 1.2, "Ink", segments=8, cap_top=False)
    # a glint of light on the glass shoulder, toward the sun (south-east)
    m.add_poly([(1.9, 3.58, 1.5), (2.7, 2.95, 2.1), (2.35, 2.92, 2.55), (1.55, 3.57, 1.9)], "Paper", facing=(0.5, 1, 0.5))
    m.add_frustum((0, 1.2, 0), 3.82, 3.82, 0.5, "Paper", segments=8, cap_top=False)      # glint band, proud 0.05
    m.add_frustum((0, 3.8, 0), 2.0, 1.8, 0.9, "Thatch", segments=8, cap_top=False)       # brass collar
    m.add_frustum((0, 4.7, 0), 1.8, 1.4, 0.15, "Thatch", segments=8, cap_colour="Ink")   # mouth, ink inside
    # the quill: lying on the table, nib at the pot's foot, feather out to the north-west
    nib = np.array((-3.2, 0.25, 1.6))
    d = np.array((-0.86, 0.0, -0.5)); d /= np.linalg.norm(d)
    s = np.array((-d[2], 0.0, d[0]))                     # across the shaft
    tube(m, nib, nib + d * 22, 0.22, "Limewash", n=5, r1=0.16)
    tube(m, nib - d * 0.9, nib, 0.05, "Ink", n=4, r1=0.22)  # the cut nib, inked
    up = np.array((0, 0.22, 0))
    # vane: one smooth convex outline a side (widest a third of the way up, tapering to the tip), the broad side
    # Limewash and the narrow side a warm grey (night 3 fix: the Slate roof bar near the tip, read as a label, is gone)
    ts = [5.0, 7.5, 10.5, 14.0, 17.5, 21.0, 24.0, 26.5, 27.5]
    def wid(t):
        return 2.5 * math.sin(math.pi * (t - 5.0) / 22.5) ** 0.7
    broad = [nib + d * t + up + s * wid(t) for t in ts] + [nib + d * t + up for t in reversed(ts)]
    narrow = [nib + d * t + up - s * wid(t) * 0.55 for t in ts] + [nib + d * t + up for t in reversed(ts)]
    m.add_poly(broad[:len(ts)] + [nib + d * ts[-1] + up, nib + d * ts[0] + up], "Limewash")
    m.add_poly(narrow[:len(ts)] + [nib + d * ts[-1] + up, nib + d * ts[0] + up], "Concrete")
    return m


def candle():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 5.2, 4.6, 0.5, "Thatch", segments=10, cap_colour="Thatch")
    m.add_frustum((0, 0.5, 0), 4.6, 4.8, 0.4, "Thatch", segments=10, cap_top=False)        # raised rim
    m.add_frustum((0, 0.5, 0), 1.6, 1.4, 1.6, "Thatch", segments=8)                          # socket
    # ring handle out to the east: a loop of four short rods
    pts = [(4.6, 0.7, -1.2), (7.2, 0.9, -1.2), (7.2, 0.9, 1.2), (4.6, 0.7, 1.2)]
    for a, b in zip(pts, pts[1:]):
        tube(m, a, b, 0.4, "Thatch", n=4)
    m.add_cylinder((0, 2.1, 0), 1.15, 9.0, "Limewash", segments=8, cap_colour="Paper")
    m.add_frustum((1.05, 7.0, 0.2), 0.35, 0.25, 4.0, "Paper", segments=4)                   # a wax drip
    tube(m, (0, 11.1, 0), (0, 11.8, 0), 0.12, "Ink", n=4)
    # night 3 fix: a teardrop flame 5 cm tall and 2.8 cm across (a round Thatch bulb and a tapering tip, a Paper core on its front), so a lit candle
    # is told from above as well as from the side
    # (after the pictures: the bulb is wider than the candle, 2.8 cm, and clear of its top, so it shows from above)
    m.add_sphere((0, 13.1, 0), 1.4, "Thatch", rings=2, segments=8, squash=0.95)
    m.add_frustum((0, 13.5, 0), 1.18, 0.0, 3.4, "Thatch", segments=8, cap_bottom=False)
    m.add_poly([(-0.5, 12.6, 1.42), (0.5, 12.6, 1.42), (0.0, 14.8, 0.78)], "Paper", facing=(0, 0, 1))   # bright core
    return m


def seal():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 1.5, 1.3, 0.9, "Thatch", segments=8, cap_colour="Thatch")      # brass foot
    m.add_frustum((0, 0.9, 0), 0.7, 0.55, 1.4, "Packed earth", segments=8, cap_top=False)
    m.add_frustum((0, 2.3, 0), 0.62, 0.62, 0.35, "Mud", segments=8, cap_top=False)          # band
    m.add_frustum((0, 2.65, 0), 0.55, 0.95, 3.0, "Packed earth", segments=8, cap_top=False)
    m.add_sphere((0, 6.2, 0), 1.0, "Packed earth", rings=3, segments=8)
    # sealing wax stick lying beside it, and a pressed seal
    beam(m, (2.6, 0.45, -3.5), (3.6, 0.45, 4.5), 0.9, 0.9, "Brick")
    m.add_frustum((-3.4, 0, 2.2), 1.9, 1.7, 0.35, "Brick", segments=9, cap_colour="Brick")
    m.add_frustum((-3.4, 0.35, 2.2), 1.05, 0.95, 0.18, "Soot brick", segments=7)            # the pressed boss
    return m


def ribbon():
    m = glb.Mesh()
    w, h = 1.6, 0.12
    n = 16
    pts = []
    for i in range(n + 1):
        t = i / n
        x = -14 + 28 * t
        z = 3.2 * math.sin(t * math.pi * 1.6) + 1.2 * math.sin(t * math.pi * 4.1)
        pts.append(np.array((x, h, z)))
    for i in range(n):
        a, b = pts[i], pts[i + 1]
        d = b - a; d /= np.linalg.norm(d)
        s = np.array((-d[2], 0, d[0])) * (w / 2)
        tone = "Soot brick" if i in (6, 7) else "Brick"      # where it turns over, the shaded back shows
        m.add_poly([a - s, b - s, b + s, a + s], tone)
        # thin sides so the ink finds an edge
        m.add_poly([a - s, b - s, b - s - np.array((0, h, 0)), a - s - np.array((0, h, 0))], "Soot brick", facing=-s)
    for end, sign in ((pts[0], -1), (pts[-1], 1)):
        d = np.array((sign * 1.0, 0, 0))
        s = np.array((0, 0, w / 2))
        tip = end + d * 2.2
        m.add_poly([end - s, end + d * 0.2, tip - s], "Brick")            # swallowtail: two points
        m.add_poly([end + s, tip + s, end + d * 0.2], "Brick")
    return m


BUILDS = [
    ("desk-inkwell", inkwell, "Book: ink pot and quill on the table (BookBuilder.AddDeskThings)"),
    ("desk-candle", candle, "Book: candle in a brass chamberstick on the table (BookBuilder.AddDeskThings)"),
    ("desk-seal", seal, "Book: wax seal stamp, sealing wax and a seal on the table (BookBuilder.AddDeskThings)"),
    ("desk-ribbon", ribbon, "Book: a loose silk ribbon on the table (BookBuilder.AddDeskThings)"),
]

if __name__ == "__main__":
    only = [a for a in sys.argv[1:] if not a.startswith("--")]
    for name, fn, used in BUILDS:
        if only and name not in only:
            continue
        mesh = fn()
        mesh.tris = [t for t in mesh.tris if np.linalg.norm(np.cross(t[1] - t[0], t[2] - t[0])) > 1e-9]
        out = asset_path(f"models/{name}.glb")
        print(glb.report(out, glb.write_glb(out, mesh, max_tris=BUDGET)))
        ledger.add("models", out, kind="model", generator=__file__, note=NOTE, used_in=used, verified=True)

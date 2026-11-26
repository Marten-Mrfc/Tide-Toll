# python3 assets/src/models/market-stall.py  ->  assets/models/market-stall.glb
# Hamlet market stall: a trestle counter under a striped cloth awning on four posts. 4 x 3 m footprint, 3 m high.
# Front (customer side, low edge of the awning) faces +Z.
# Night 3 rework (style sheet section 4, "goods on the table readable as shapes"): the awning only covers the back
# two thirds, so from the game's tilt the counter stands out in front of it and its goods show: a round basket of
# red apples, a heap of cabbages, a tied grain sack, a stack of three cheeses and a basket of loaves, each larger
# than 0.4 m and each in its own colour; a crate of apples and a sack lean against the front of the counter, and
# behind the stall a crate, a stool and a barrel, so the back is not blank. The striped awning is kept, with a
# scalloped valance and a sag in its front edge.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam

m = glb.Mesh()
W = 3.8
hx = W / 2
ZF, ZB = 0.25, -1.4                     # awning's front and back edges (posts stand under them)
YF, YB = 2.45, 2.95
for x in (-hx, hx):
    m.add_box((x, 0, ZF), (0.2, YF, 0.2), "Mud")
    m.add_box((x, 0, ZB), (0.2, YB, 0.2), "Mud")
    beam(m, (x, YF - 0.05, ZF), (x, YB - 0.05, ZB), 0.16, 0.14, "Mud")                    # side rail under the cloth
# awning: striped cloth, alternating Brick and Limewash strips running front to back; the middle strips sag a touch
strips = 5
for i in range(strips):
    x0 = -2.0 + 4.0 * i / strips
    x1 = -2.0 + 4.0 * (i + 1) / strips
    col = "Brick" if i % 2 == 0 else "Limewash"
    sag = 0.06 if i in (1, 2, 3) else 0.0
    y0, y1, z0, z1 = YF - sag, YB, ZF + 0.15, ZB - 0.1
    t = 0.08
    pts_top = [(x0, y0 + t, z0), (x1, y0 + t, z0), (x1, y1 + t, z1), (x0, y1 + t, z1)]
    pts_bot = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z1), (x0, y1, z1)]
    m.add_poly(pts_top, col, facing=(0, 1, 0.2))
    m.add_poly(pts_bot, col, facing=(0, -1, -0.2))
    m.add_poly([pts_bot[0], pts_bot[1], pts_top[1], pts_top[0]], col, facing=(0, 0, 1))
    m.add_poly([pts_bot[3], pts_bot[2], pts_top[2], pts_top[3]], col, facing=(0, 0, -1))
    if i == 0:
        m.add_poly([pts_bot[0], pts_bot[3], pts_top[3], pts_top[0]], col, facing=(-1, 0, 0))
    if i == strips - 1:
        m.add_poly([pts_bot[1], pts_bot[2], pts_top[2], pts_top[1]], col, facing=(1, 0, 0))
    # scalloped valance off the front edge
    m.add_box(((x0 + x1) / 2, y0 - 0.32, ZF + 0.14), (0.78, 0.32, 0.06), col, bottom=col)
# counter: trestle legs and a thick plank top, standing forward of the awning's edge so its goods show from above
CZ, CY = 0.95, 0.9
for x in (-1.5, 1.5):
    m.add_box((x, 0, CZ), (0.2, CY, 0.8), "Mud")
m.add_box((0, CY, CZ), (3.7, 0.16, 1.05), "Packed earth", bottom="Packed earth")
beam(m, (-1.85, CY + 0.08, CZ + 0.53), (1.85, CY + 0.08, CZ + 0.53), 0.08, 0.2, "Trail earth")   # front board edge
top = CY + 0.16
# goods, left to right
m.add_frustum((-1.3, top, CZ), 0.36, 0.44, 0.3, "Reed and thatch", segments=8, cap_top=False)     # apple basket
m.add_cylinder((-1.3, top, CZ), 0.38, 0.27, "Brick", segments=8, cap_bottom=False)
m.add_sphere((-1.3, top + 0.3, CZ), 0.36, "Brick", rings=2, segments=6, squash=0.6)              # apples heaped
for (x, z, r, c) in ((-0.6, 0.85, 0.32, "Meadow"), (-0.22, 1.15, 0.3, "Meadow"),   # review r1: goods 0.35 m up
                     (-0.3, 0.7, 0.28, "Cast-iron green")):
    m.add_sphere((x, top + r * 0.8, z), r, c, rings=2, segments=5)                              # cabbages
m.add_frustum((0.3, top, CZ - 0.05), 0.3, 0.22, 0.6, "Reed and thatch", segments=6)              # grain sack
m.add_frustum((0.3, top + 0.6, CZ - 0.05), 0.1, 0.14, 0.14, "Packed earth", segments=6)          # its tie
for k, y in enumerate((0.0, 0.17, 0.34)):                                                     # three cheeses
    m.add_cylinder((0.88, top + y, CZ + 0.1), 0.3 - 0.03 * k, 0.16, "Paper" if k != 1 else "Reed and thatch",
                   segments=6, cap_bottom=True)
m.add_box((1.42, top, CZ), (0.62, 0.22, 0.7), "Reed and thatch", bottom="Reed and thatch")   # bread basket
for dz in (-0.18, 0.12):
    beam(m, (1.22, top + 0.3, CZ + dz), (1.62, top + 0.3, CZ + dz + 0.06), 0.34, 0.26, "Trail earth")   # loaves
# in front of the counter: a crate of apples and a leaning sack
m.add_box((-0.9, 0, 1.75), (0.7, 0.42, 0.5), "Trail earth")
m.add_box((-0.9, 0.42, 1.75), (0.6, 0.08, 0.4), "Brick", bottom=False)
m.add_frustum((0.85, 0, 1.7), 0.28, 0.2, 0.55, "Limewash", segments=6, cap_bottom=False)
m.add_frustum((0.85, 0.55, 1.7), 0.09, 0.12, 0.12, "Packed earth", segments=6)
# behind: a crate, a stool, a barrel and a back rail with a hanging cloth
m.add_box((1.0, 0, -0.8), (0.8, 0.6, 0.7), "Trail earth")
m.add_box((1.0, 0.6, -0.8), (0.86, 0.08, 0.76), "Packed earth", bottom=False)
m.add_cylinder((-0.6, 0, -0.3), 0.25, 0.45, "Packed earth", segments=6)
m.add_cylinder((-1.3, 0, -0.9), 0.38, 0.9, "Mud", segments=6)                                    # barrel
beam(m, (-hx, 1.6, ZB), (hx, 1.6, ZB), 0.12, 0.12, "Mud")
m.add_box((-0.6, 0.9, ZB - 0.08), (1.3, 0.7, 0.06), "River", bottom="River")                     # a cloth hung to dry
out = asset_path("models/market-stall.glb")
print(glb.report(out, glb.write_glb(out, m, max_tris=650)))
ledger.add("models", out, kind="model", generator=__file__, used_in="Hamlet: market stall", verified=True)

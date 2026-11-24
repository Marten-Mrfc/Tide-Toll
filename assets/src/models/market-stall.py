# python3 assets/src/models/market-stall.py  ->  assets/models/market-stall.glb
# Hamlet market stall: a trestle counter under a sloping striped cloth awning on four posts, with baskets of
# grain, a sack and cabbages on the counter; a crate and a stool behind. 4 x 3 m footprint, 3 m high.
# Front (customer side, low edge of the awning) faces +Z.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam

m = glb.Mesh()
W, D = 3.8, 2.8                         # post rectangle (awning reaches the 4 x 3 footprint)
hx, hz = W / 2, D / 2
# posts: front ones lower, so the awning slopes down to the front
for x in (-hx, hx):
    m.add_box((x, 0, hz - 0.1), (0.2, 2.3, 0.2), "Mud")
    m.add_box((x, 0, -hz + 0.1), (0.2, 2.85, 0.2), "Mud")
# awning: striped cloth, alternating Brick and Limewash strips running front to back
strips = 5
for i in range(strips):
    x0 = -2.0 + 4.0 * i / strips
    x1 = -2.0 + 4.0 * (i + 1) / strips
    col = "Brick" if i % 2 == 0 else "Limewash"
    y0, y1, z0, z1 = 2.3, 2.85, 1.5, -1.5
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
# scalloped valance hanging off the front edge (reads as cloth from the tilted camera)
for i in range(strips):
    x = -2.0 + 4.0 * (i + 0.5) / strips
    m.add_box((x, 2.0, 1.48), (0.8, 0.3, 0.06), "Brick" if i % 2 == 0 else "Limewash", bottom="Brick" if i % 2 == 0 else "Limewash")
# counter: trestle legs and a thick plank top along the front
for x in (-1.5, 1.5):
    m.add_box((x, 0, 0.9), (0.18, 0.85, 0.7), "Mud")
m.add_box((0, 0.85, 0.9), (3.6, 0.15, 0.95), "Packed earth", bottom="Packed earth")
# goods on the counter
m.add_frustum((-1.1, 1.0, 0.95), 0.32, 0.38, 0.3, "Reed and thatch", segments=7, cap_top=False)   # basket
m.add_cylinder((-1.1, 1.0, 0.95), 0.3, 0.26, "Trail earth", segments=7, cap_bottom=False)         # grain in it
m.add_frustum((-0.35, 1.0, 0.9), 0.3, 0.2, 0.55, "Limewash", segments=6)                          # tied sack
m.add_sphere((0.45, 1.22, 0.95), 0.22, "Meadow", rings=2, segments=6)                             # cabbages
m.add_sphere((0.85, 1.22, 0.8), 0.22, "Meadow", rings=2, segments=6)
m.add_sphere((0.7, 1.22, 1.15), 0.2, "Cast-iron green", rings=2, segments=6)
m.add_frustum((1.35, 1.0, 0.95), 0.3, 0.34, 0.22, "Reed and thatch", segments=7)                  # flat basket
# behind the counter: a crate and a stool, so the back is not blank
m.add_box((1.0, 0, -0.8), (0.8, 0.6, 0.7), "Trail earth")
m.add_box((1.0, 0.6, -0.8), (0.86, 0.08, 0.76), "Packed earth", bottom=False)
m.add_cylinder((-0.8, 0, -0.5), 0.25, 0.45, "Packed earth", segments=6)
beam(m, (-hx, 1.6, -hz + 0.1), (hx, 1.6, -hz + 0.1), 0.12, 0.12, "Mud")                             # back rail
out = asset_path("models/market-stall.glb")
print(glb.report(out, glb.write_glb(out, m, max_tris=400)))
ledger.add("models", out, kind="model", generator=__file__, used_in="Hamlet: market stall", verified=True)

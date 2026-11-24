# python3 assets/src/models/reeds.py  ->  assets/models/reeds.glb
# Reed clump for river banks and reed beds ("Reed and thatch" #C9A55A, Art tab): a 2 x 2 m cell of tufts, each a fan
# of leaning blades up to 1.4 m round a low tussock, mostly straw gold with some green, and two cattails with dark heads (1.5 m).
# From above the tussocks and blades read as tufts with radiating strokes, like a reed symbol on a map. Origin at the centre of the cell on
# the ground (or the bed under shallow water). Front +Z by convention; it looks alike from every side.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import numpy as np
from common import glb, ledger, asset_path
from _kit import beam

SEED = 5
rng = np.random.default_rng(SEED)
m = glb.Mesh()


def blade(x, z, ang, lean, h, w, col):
    """Thin three-sided blade: a small triangular foot at (x, z), apex leaning out by `lean` towards angle `ang`."""
    foot = [np.array((x + w * math.cos(ang + k * 2.094), 0.0, z + w * math.sin(ang + k * 2.094))) for k in range(3)]
    apex = np.array((x + lean * math.cos(ang), h, z + lean * math.sin(ang)))
    centre = np.array((x, h * 0.3, z))
    for k in range(3):
        tri = [foot[k], foot[(k + 1) % 3], apex]
        m._face(tri, col, np.mean(tri, axis=0) - centre)


tufts = [(-0.55, -0.5), (0.15, -0.6), (0.62, 0.05), (-0.62, 0.3), (-0.05, 0.05), (0.4, 0.62), (-0.25, 0.68),
         (0.72, -0.68)]
for i, (x, z) in enumerate(tufts):
    x += float(rng.uniform(-0.05, 0.05)); z += float(rng.uniform(-0.05, 0.05))
    n = 3
    h0 = float(rng.uniform(0.5, 0.65))
    m.add_cone((x, 0, z), 0.3, h0, "Reed and thatch" if i % 3 else "Bent grass", segments=5,
               yaw=float(rng.uniform(0, 1.2)))                               # tussock: the tuft's mass from above
    a0 = float(rng.uniform(0, 2 * math.pi))
    for k in range(n):
        col = "Bent grass" if (i + k) % 4 == 0 else "Reed and thatch"
        ang = a0 + 2 * math.pi * k / n
        blade(x, z, ang, float(rng.uniform(0.4, 0.55)), float(rng.uniform(1.0, 1.4)), 0.13, col)
for x, z, h in ((-0.3, -0.15, 1.2), (0.3, 0.3, 1.15)):            # cattails
    beam(m, (x, 0, z), (x, h, z), 0.05, 0.05, "Packed earth", ends=False)
    m.add_cylinder((x, h, z), 0.09, 0.3, "Mud", segments=4, cap_bottom=False)
out = asset_path("models/reeds.glb")
print(glb.report(out, glb.write_glb(out, m, max_tris=150)))
ledger.add("models", out, kind="model", generator=__file__, used_in="Hamlet: reeds (bank and reed beds)", seed=SEED,
           verified=True)

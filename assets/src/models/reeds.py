# python3 assets/src/models/reeds.py  ->  assets/models/reeds.glb
# Reed clump for river banks and reed beds ("Reed and thatch" #C9A55A, Art tab): a 2 x 2 m cell of common reed,
# second round (2026-11-24): thin UPRIGHT stems (straw gold, some green) standing in three loose drifts, most of them
# topped by a dark seed head (Mud plume, 1.2 to 1.5 m up), and a few tall narrow leaf blades. No tussock or
# rosette at the foot. From above the clump reads as a scatter of dark heads over gold strokes, like a reed symbol
# on a map; from the side as a stand of upright stems. Origin at the centre of the cell on the ground (or the bed
# under shallow water). Front +Z by convention; it looks alike from every side. Written with ink="soft" (fine
# facets would otherwise collect ink; see common.glb.INK_SOFT).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import numpy as np
from common import glb, ledger, asset_path

SEED = 5
rng = np.random.default_rng(SEED)
m = glb.Mesh()


def stem(x, z, h, lean_ang, lean, w, col):
    """Thin upright three-sided stem tapering to a point, foot at (x, z), tip pushed `lean` towards lean_ang."""
    foot = [np.array((x + w * math.cos(k * 2.094 + lean_ang), 0.0, z + w * math.sin(k * 2.094 + lean_ang)))
            for k in range(3)]
    apex = np.array((x + lean * math.cos(lean_ang), h, z + lean * math.sin(lean_ang)))
    centre = (foot[0] + foot[1] + foot[2] + apex) / 4      # inside the blade, so every face points out
    for k in range(3):
        tri = [foot[k], foot[(k + 1) % 3], apex]
        m._face(tri, col, np.mean(tri, axis=0) - centre)
    return apex


drifts = [(-0.45, -0.35), (0.5, -0.2), (-0.05, 0.5)]
heads = 0
for d, (cx, cz) in enumerate(drifts):
    for k in range(5):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.0, 0.42)
        x, z = cx + r * math.cos(a), cz + r * math.sin(a)
        h = float(rng.uniform(1.15, 1.45))
        lean_ang = math.atan2(z, x) + float(rng.uniform(-0.6, 0.6))      # lean a little away from the centre
        lean = float(rng.uniform(0.04, 0.16))
        col = "Bent grass" if (d + k) % 3 == 0 else "Reed and thatch"
        if k < 3:                                                        # stem with a seed head
            tip = stem(x, z, h, lean_ang, lean, 0.06, col)
            hx, hz = x + lean * 0.85 * math.cos(lean_ang), z + lean * 0.85 * math.sin(lean_ang)
            m.add_frustum((hx, h - 0.42, hz), 0.15, 0.1, 0.4, "Mud", segments=3,
                          yaw=float(rng.uniform(0, 2.1)), cap_bottom=False)
            heads += 1
        else:                                                            # narrow leaf blade, no head
            stem(x, z, h * 0.95, lean_ang, 0.35 + lean * 1.5, 0.13, col)
out = asset_path("models/reeds.glb")
print(glb.report(out, glb.write_glb(out, m, max_tris=150, ink="soft")), f"({heads} heads)")
ledger.add("models", out, kind="model", generator=__file__, used_in="Hamlet: reeds (bank and reed beds)", seed=SEED,
           verified=True)

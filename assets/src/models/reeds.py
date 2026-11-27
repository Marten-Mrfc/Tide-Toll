# python3 assets/src/models/reeds.py  ->  assets/models/reeds.glb, reeds-small.glb, reeds-tall.glb
# Reed clumps for the river banks (round n3, track RV, 2026-11-26; rebuilt after the review the same night). Three
# shapes, not one shape scaled:
#   reeds-small  0.9 m: three short blades, no heads
#   reeds        1.6 m: five blades and two seed heads
#   reeds-tall   2.2 m: seven blades and two seed heads, the whole stand leaning one way
# Blades are flat, two-sided, 0.3 m wide at the foot (0.14 m read as pure ink at the normal view) and bending once, two in three Bent grass, the rest Cast-iron green; seed heads are
# Mud cylinders 0.12 m across and 0.35 m long on a Thatch stem. No hub at the foot, no Brick. RiverBanks plants them in
# rows of five to nine along both banks so the bank gets a band of reed. Origin at the clump's foot. ink="soft".
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import numpy as np
from common import glb, ledger, asset_path

SEED = 5


def blade(m, x, z, h, ang, bend, w, col, lean=(0.0, 0.0)):
    """A flat two-sided blade: foot w wide, a knee at 55 percent, a point at h. It faces across `ang` and bends
    `bend` metres outward along `ang`, plus the clump's `lean` (dx, dz per metre of height)."""
    ux, uz = math.cos(ang), math.sin(ang)                 # bending direction
    px, pz = -uz, ux                                      # across the blade
    def at(t, half):
        b = bend * t * t
        return np.array((x + ux * b + lean[0] * h * t + px * half, h * t, z + uz * b + lean[1] * h * t + pz * half))
    pts = [at(0, -w / 2), at(0, w / 2), at(0.55, w * 0.32), at(0.55, -w * 0.32), at(1, 0)]
    quads = ([pts[0], pts[1], pts[2], pts[3]], [pts[3], pts[2], pts[4]])
    for q in quads:
        c = np.mean(q, axis=0)
        n = np.cross(q[1] - q[0], q[2] - q[0])
        m._face(q, col, n)
        m._face(list(reversed(q)), col, -n)


def clump(height, n_blades, n_heads, spread, seed, lean=(0.0, 0.0)):
    rng = np.random.default_rng(seed)
    m = glb.Mesh()
    for k in range(n_blades):
        a = 2 * math.pi * k / n_blades + float(rng.uniform(-0.4, 0.4))
        r = spread * float(rng.uniform(0.2, 1.0))
        x, z = r * math.cos(a), r * math.sin(a)
        h = height * float(rng.uniform(0.7, 0.95))
        col = "Cast-iron green" if k % 3 == 1 else "Bent grass"
        blade(m, x, z, h, a, 0.15 + 0.2 * height * float(rng.uniform(0.6, 1.0)), 0.3, col, lean)
    for k in range(n_heads):
        a = math.pi * k + float(rng.uniform(0, 1.0))
        x, z = spread * 0.3 * math.cos(a), spread * 0.3 * math.sin(a)
        top = height * (1.0 - 0.08 * k)
        tx, tz = x + lean[0] * top, z + lean[1] * top
        # stem: a narrow Thatch blade to the head, then the head
        blade(m, x, z, top - 0.35, a, 0.02, 0.09, "Reed and thatch", lean)
        hx, hz = x + lean[0] * (top - 0.35), z + lean[1] * (top - 0.35)
        m.add_frustum((hx, top - 0.38, hz), 0.06, 0.06, 0.35, "Mud", segments=5, cap_bottom=True)
        m.add_frustum((hx, top - 0.03, hz), 0.03, 0.0, 0.08, "Reed and thatch", segments=4, cap_bottom=False)
    return m


if __name__ == "__main__":
    for name, h, nb, nh, spread, seed, lean in (("reeds-small", 0.9, 3, 0, 0.25, SEED + 1, (0.0, 0.0)),
                                                ("reeds", 1.6, 5, 2, 0.35, SEED, (0.0, 0.0)),
                                                ("reeds-tall", 2.2, 7, 2, 0.45, SEED + 2, (0.12, 0.05))):
        m = clump(h, nb, nh, spread, seed, lean)
        out = asset_path(f"models/{name}.glb")
        print(glb.report(out, glb.write_glb(out, m, max_tris=250, ink="soft")), f"({nb} blades, {nh} heads)")
        ledger.add("models", out, kind="model", generator=__file__,
                   used_in="River town: reeds in rows along both banks (RiverBanks)", seed=seed, verified=True)

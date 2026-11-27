# python3 assets/src/models/ford-stones.py  ->  assets/models/ford-stones.glb, assets/models/bank-stones.glb
# Round n3 (track RV, 2026-11-26): ONE stepping stone of the ford, which RiverLayer lays in a gently curved row,
# one every 2 m across the river, so the ford reads as a line to walk, not a scatter. A flat-topped stone about
# 1.3 x 1.1 m, 0.45 m above the water (y = 0 is the water's surface at high water), its top in Concrete with a
# lighter worn tread in Paper where feet land, its sides Wet mudflat, a dark wet foot ring at the water line and a
# pale Limewash ripple crescent on the downstream side (-Z). Crossing direction is along Z.
# bank-stones.glb: two or three low stones lying together on a river bank (RiverBanks scatters them).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import numpy as np
from common import glb, ledger, asset_path
from _kit import stone, hull2d, jitter_ngon, loft, cap

SEED = 11
rng = np.random.default_rng(SEED)
m = glb.Mesh()
# wet foot: a low wide ring of Wet mudflat just above the water
out = hull2d(jitter_ngon(rng, 0.72, 8, 0.1))
foot = [(x * 1.0, 0.0, z * 0.9) for x, z in out]
foot_top = [(x * 0.96, 0.12, z * 0.86) for x, z in out]
loft(m, foot, foot_top, "Wet mudflat")
# the stone: sides Wet mudflat low, Concrete above, flat top with a Paper tread
body = hull2d(jitter_ngon(rng, 0.62, 8, 0.12))
b0 = [(x, 0.12, z * 0.88) for x, z in body]
b1 = [(x * 0.97, 0.3, z * 0.86) for x, z in body]
b2 = [(x * 0.9, 0.45, z * 0.8) for x, z in body]
cap(m, foot_top, "Wet mudflat")
loft(m, b0, b1, "Concrete")
loft(m, b1, b2, "Concrete")
cap(m, b2, "Concrete")
tread = [(x * 0.42 + 0.06, 0.47, z * 0.34 - 0.04) for x, z in body]
loft(m, [(x, 0.45, z) for x, _, z in tread], tread, "Concrete")
cap(m, tread, "Paper")
# ripple crescent downstream: flat arc bands on the water
for r0, r1, a0, a1 in ((0.88, 0.97, -2.4, -0.75), (1.12, 1.19, -2.1, -1.05)):
    n = 6
    for i in range(n):
        t0 = a0 + (a1 - a0) * i / n
        t1 = a0 + (a1 - a0) * (i + 1) / n
        q = [(r0 * math.cos(t0), 0.02, r0 * math.sin(t0) * 0.9), (r1 * math.cos(t0), 0.02, r1 * math.sin(t0) * 0.9),
             (r1 * math.cos(t1), 0.02, r1 * math.sin(t1) * 0.9), (r0 * math.cos(t1), 0.02, r0 * math.sin(t1) * 0.9)]
        m.add_poly(q, "Limewash")
out_p = asset_path("models/ford-stones.glb")
print(glb.report(out_p, glb.write_glb(out_p, m, max_tris=150)))
ledger.add("models", out_p, kind="model", generator=__file__, used_in="River town: the ford (a row of stepping stones)",
           seed=SEED, verified=True)

b = glb.Mesh()
# After the review: two stones touching, 1.8 m across, the larger 0.5 m high, Concrete with a Paper top, so they read
# at the normal view as a boulder pair on the bank and not as a stray pebble.
for x, z, r, h in ((-0.3, 0.0, 0.62, 0.5), (0.55, 0.22, 0.42, 0.32)):
    stone(b, rng, (x, 0, z), r, h, top="Paper", side="Concrete", n=7, amount=0.16)
out_b = asset_path("models/bank-stones.glb")
print(glb.report(out_b, glb.write_glb(out_b, b, max_tris=100)))
ledger.add("models", out_b, kind="model", generator=__file__, used_in="River town: stones on the banks (RiverBanks)",
           seed=SEED, verified=True)

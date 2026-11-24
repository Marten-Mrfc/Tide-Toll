# python3 assets/src/models/rowboat.py  ->  assets/models/rowboat.glb
# Hamlet rowboat (Art tab: "small oval hull, no mast mark"; passes low bridges). Drawn 1.5x life size like people:
# 6.8 m long (along Z, bow to +Z, the front) x 2.4 m wide, 1.2 m from keel to gunwale. y = 0 is the waterline:
# the keel sits 0.3 m below it. Clinker-free chunky hull, open with a floor, two thwarts and a pair of oars stowed
# along the inside; a small transom stern. No mast.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam, loft, cap

KEEL, FLOOR, GUN = -0.3, 0.32, 0.9
# half outline at the gunwale, bow (+Z) to stern (-Z), x >= 0
HALF = [(0.0, 3.4), (0.62, 2.75), (1.02, 1.8), (1.2, 0.5), (1.17, -0.8), (1.02, -2.1), (0.78, -3.25)]
OUT = HALF + [(-x, z) for x, z in reversed(HALF[1:])]         # closed ring, bow first (stern transom between the
                                                              # two stern points)


def ring(sx, sz, y, dz=0.0):
    return [(x * sx, y, z * sz + dz) for x, z in OUT]


m = glb.Mesh()
gun_out = ring(1.0, 1.0, GUN)
gun_in = ring(0.86, 0.93, GUN, -0.05)
keel = ring(0.42, 0.8, KEEL, -0.15)
floor = ring(0.62, 0.84, FLOOR, -0.08)
loft(m, keel, gun_out, "Packed earth")                        # outer hull
cap(m, keel, "Mud", facing=(0, -1, 0))                        # flat bottom (seen only from below)
n = len(OUT)
for i in range(n):                                            # gunwale band, darker wood
    j = (i + 1) % n
    m.add_poly([gun_out[i], gun_out[j], gun_in[j], gun_in[i]], "Mud", facing=(0, 1, 0))
loft(m, floor, gun_in, "Trail earth", inward=True, centre=(0, 0.6, -0.1))   # inside of the hull
cap(m, floor, "Mud")                                          # floor boards
for z, w in ((0.9, 1.75), (-1.2, 1.8)):                       # thwarts (seats)
    m.add_box((0, 0.62, z), (w, 0.12, 0.42), "Trail earth", bottom="Trail earth")
for x in (-0.35, 0.35):                                       # oars stowed along the boat, blades aft
    beam(m, (x, 0.78, 1.9), (x, 0.78, -1.6), 0.1, 0.1, "Mud")
    m.add_box((x, 0.73, -2.0), (0.26, 0.08, 0.8), "Mud", bottom="Mud")
m.add_box((0, GUN - 0.05, 3.25), (0.22, 0.25, 0.3), "Mud", bottom=False)     # stem post at the bow
out = asset_path("models/rowboat.glb")
print(glb.report(out, glb.write_glb(out, m, max_tris=300)))
ledger.add("models", out, kind="model", generator=__file__, used_in="Hamlet: boats (rowboat)", verified=True)

# python3 assets/src/models/rowboat.py  ->  assets/models/rowboat.glb
# Hamlet rowboat (Art tab: "small oval hull, no mast mark"; passes low bridges). Drawn 1.5x life size like people:
# 6.8 m long (along Z, bow to +Z, the front) x 2.4 m wide, 1.2 m from keel to gunwale. y = 0 is the waterline:
# the keel sits 0.3 m below it. Clinker-free chunky hull, open with a floor, two thwarts and a pair of oars stowed
# along the inside; a small transom stern. No mast.
# Round n3 (track RV, 2026-11-26): two-tone: a Brick hull with a Thatch gunwale stripe and a Limewash inside, so it
# reads against River blue and is never mud-brown; a raised pointed bow post, two thwarts in Packed earth, a pair of
# oars out through the rowlocks at 40 degrees with broad blades, a coil of rope at the bow and a lantern on the
# stern post. The game puts its cargo in it (RiverBoats) and a boatman on the aft thwart.
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
loft(m, keel, gun_out, "Brick")                               # outer hull
cap(m, keel, "Mud", facing=(0, -1, 0))                        # flat bottom (seen only from below)
n = len(OUT)
for i in range(n):                                            # gunwale band, darker wood
    j = (i + 1) % n
    m.add_poly([gun_out[i], gun_out[j], gun_in[j], gun_in[i]], "Reed and thatch", facing=(0, 1, 0))
loft(m, floor, gun_in, "Limewash", inward=True, centre=(0, 0.6, -0.1))      # inside of the hull
cap(m, floor, "Trail earth")                                  # floor boards
for z, w in ((0.9, 1.75), (-1.2, 1.8)):                       # thwarts (seats)
    m.add_box((0, 0.62, z), (w + 0.1, 0.16, 0.46), "Packed earth", bottom="Packed earth")
import math
for sx in (-1, 1):                                            # oars out through rowlocks at 40 degrees, blades in the water
    lock = (sx * 1.15, GUN + 0.06, -0.3)
    a = math.radians(40)
    tip = (sx * (1.15 + 2.0 * math.cos(a)), 0.05, -0.3 - 2.0 * math.sin(a))
    beam(m, (sx * 0.35, GUN + 0.12, 0.1), tip, 0.13, 0.13, "Packed earth")
    beam(m, (tip[0] - sx * 0.3 * math.cos(a), 0.08, tip[2] + 0.3 * math.sin(a)), (tip[0] + sx * 0.35 * math.cos(a), 0.0, tip[2] - 0.35 * math.sin(a)),
         0.36, 0.08, "Packed earth")
    m.add_box(lock, (0.16, 0.18, 0.16), "Mud", bottom=False)
m.add_box((0, GUN - 0.05, 3.3), (0.24, 0.55, 0.28), "Brick", bottom=False)         # raised stem post at the bow
m.add_box((0, GUN + 0.5, 3.3), (0.28, 0.08, 0.32), "Reed and thatch", bottom=False)
m.add_frustum((0.0, FLOOR, 2.4), 0.36, 0.3, 0.2, "Reed and thatch", segments=6)    # rope coil at the bow
m.add_box((0, GUN - 0.05, -3.1), (0.18, 0.9, 0.18), "Mud", bottom=False)          # stern post with a lantern
m.add_box((0, GUN + 0.85, -3.1), (0.3, 0.32, 0.3), "Cast-iron green", bottom="Cast-iron green")
m.add_box((0, GUN + 0.9, -3.1), (0.32, 0.18, 0.2), "Reed and thatch", bottom=False)
out = asset_path("models/rowboat.glb")
print(glb.report(out, glb.write_glb(out, m, max_tris=300)))
ledger.add("models", out, kind="model", generator=__file__, used_in="River: boats (rowboat; cargo and boatman added by RiverBoats)", verified=True)

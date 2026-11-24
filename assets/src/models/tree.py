# python3 assets/src/models/tree.py  ->  assets/models/tree-oak.glb, tree-willow.glb, tree-poplar.glb
# "Round gouache canopies that read as map tree symbols from far away and as 3D crowns up close" (Art tab).
# Origin at the foot of the trunk. Round from every side; +Z is the front only by convention.
#   tree-oak     broad lumpy crown about 5.7 m across, 7.4 m high, Meadow with a darker Cast-iron green underside
#   tree-willow  weeping willow (the delta's riverside tree): short leaning-free trunk, a rounded pale cap about 4.4 m
#                across, and a curtain of 14 broad hanging fronds that fall from the cap's rim to 1.5 m (long) or 2.1 m (short) above the ground,
#                tips splayed a little outwards so the fringe shows round the cap from above. About 5.4 m high.
# All three are written with ink="soft": the screen-space ink pass inks their facet creases only above the soft
# threshold (common.glb.INK_SOFT), so a crown carries no more ink than a house; silhouettes are inked as usual.
#   tree-poplar  tall narrow column about 2.9 m across, 9 m high, for avenues and dike tops
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))                          # _kit
import math
from common import glb, ledger, asset_path
from _kit import loft, cap

TRUNK = "Mud"


def oak():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.5, 0.32, 3.4, TRUNK, segments=6)
    m.add_sphere((0, 3.75, 0), 2.7, "Cast-iron green", rings=2, segments=8, squash=0.42)   # shaded underside
    m.add_sphere((0, 4.75, 0), 2.55, "Meadow", rings=3, segments=8, squash=0.95)
    for x, y, z, r in ((1.7, 4.6, 0.9, 1.45), (-1.6, 4.5, 1.0, 1.45), (0.2, 4.4, -1.85, 1.45), (0.1, 6.0, 0.2, 1.4)):
        m.add_sphere((x, y, z), r, "Meadow", rings=3, segments=7)
    return m


def willow():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.5, 0.36, 3.0, TRUNK, segments=6)
    for a in (0.4, 2.5, 4.6):                                                        # three short limbs into the cap
        x, z = math.cos(a), math.sin(a)
        m.add_frustum((0.55 * x, 2.7, 0.55 * z), 0.22, 0.14, 1.0, TRUNK, segments=4, cap_bottom=False)
    m.add_sphere((0, 4.1, 0), 2.1, "Bent grass", rings=3, segments=8, squash=0.62)   # rounded cap
    m.add_sphere((0, 3.75, 0), 1.95, "Cast-iron green", rings=2, segments=8, squash=0.3)  # shaded underside
    n = 14
    for i in range(n):                                                               # hanging fronds: a curtain
        a = 2 * math.pi * (i + 0.25 * (i % 2)) / n
        long = i % 2 == 0
        r_top, y_top = 1.85, 4.1
        r_tip = r_top + (0.6 if long else 0.45)
        y_tip = 1.5 if long else 2.1
        u = (math.cos(a), math.sin(a))
        t = (-u[1], u[0])
        wt, wb = 0.95, 0.4                                                           # frond width top / tip
        top = [(u[0] * r_top + t[0] * wt / 2, y_top, u[1] * r_top + t[1] * wt / 2),
               (u[0] * r_top - t[0] * wt / 2, y_top, u[1] * r_top - t[1] * wt / 2),
               (u[0] * (r_top - 0.45), y_top, u[1] * (r_top - 0.45))]
        bot = [(u[0] * r_tip + t[0] * wb / 2, y_tip, u[1] * r_tip + t[1] * wb / 2),
               (u[0] * r_tip - t[0] * wb / 2, y_tip, u[1] * r_tip - t[1] * wb / 2),
               (u[0] * (r_tip - 0.2), y_tip, u[1] * (r_tip - 0.2))]
        col = "Bent grass" if long else "Meadow"
        loft(m, top, bot, col, centre=(u[0] * (r_top - 0.25), (y_top + y_tip) / 2, u[1] * (r_top - 0.25)))
        cap(m, bot, col, facing=(0, -1, 0))
    return m


def poplar():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.32, 0.22, 2.0, TRUNK, segments=6)
    m.add_sphere((0, 5.4, 0), 1.65, "Meadow", rings=4, segments=8, squash=2.15)
    m.add_sphere((0, 2.6, 0), 1.2, "Cast-iron green", rings=2, segments=7, squash=0.6)  # shaded skirt
    return m


for name, make, used in (("tree-oak", oak, "Hamlet: trees (oak)"), ("tree-willow", willow, "Hamlet: trees (weeping willow)"),
                         ("tree-poplar", poplar, "Hamlet: trees (poplar)")):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=300, ink="soft")))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, verified=True)

# python3 assets/src/models/tree.py  ->  assets/models/tree-oak.glb, tree-willow.glb, tree-poplar.glb
# "Round gouache canopies that read as map tree symbols from far away and as 3D crowns up close" (Art tab).
# Origin at the foot of the trunk. Round from every side; +Z is the front only by convention.
#   tree-oak     broad lumpy crown about 5.7 m across, 7.4 m high, Meadow with a darker Cast-iron green underside
#   tree-willow  weeping willow (the delta's riverside tree): short leaning-free trunk, a rounded pale cap about 4.4 m
#                across, and a curtain of 14 broad hanging fronds that fall from the cap's rim to 1.5 m (long) or 2.1 m (short) above the ground,
#                tips splayed a little outwards so the fringe shows round the cap from above. About 5.4 m high.
# Night 3 rework (style sheet section 4): every kind shows its trunk, has a crown of two or three lumps, each in
# three tones (Bent grass light cap on the sun side, Meadow body, Cast-iron green underside); two new kinds:
#   tree-birch     slender pale trunk with dark bands, airy high crown of three small lumps, 7.5 m
#   tree-hawthorn  a low dark bush 2.6 m high and 3.4 m long (a hedge piece) with red haws and one white blossom
# All five are written with ink="soft": the screen-space ink pass inks their facet creases only above the soft
# threshold (common.glb.INK_SOFT), so a crown carries no more ink than a house; silhouettes are inked as usual.
#   tree-poplar  tall narrow column about 2.9 m across, 9 m high, for avenues and dike tops
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))                          # _kit
import math
from common import glb, ledger, asset_path
from _kit import loft, cap

TRUNK = "Mud"


LIGHT, BASE, SHADE = "Bent grass", "Meadow", "Cast-iron green"


def lump(m, c, r, squash=0.85, light=LIGHT, base=BASE, shade=SHADE, seg=6):
    """One crown lump in three tones: a flattened shade disc under it, the base ball, and a light cap on its
    sun side (the sun comes from the south-east: +X, +Z), so every lump reads as light on top, dark under."""
    x, y, z = c
    m.add_sphere((x, y - r * squash * 0.45, z), r * 0.92, shade, rings=2, segments=seg, squash=0.38)
    m.add_sphere((x, y, z), r, base, rings=3, segments=seg, squash=squash)
    if light is None:
        return
    # review r1: the light cap was one squashed ball whose top read as a flat pentagon plateau; it is now two small
    # peaked lumps at different heights (same triangle count), so the top breaks like foliage
    m.add_sphere((x + r * 0.28, y + r * squash * 0.5, z + r * 0.12), r * 0.5, light, rings=1, segments=seg,
                 squash=0.95)
    m.add_sphere((x - r * 0.12, y + r * squash * 0.66, z + r * 0.3), r * 0.38, light, rings=1, segments=seg,
                 squash=1.1)


def limb(m, p, r0, r1, h, colour=TRUNK):
    m.add_frustum(p, r0, r1, h, colour, segments=5, cap_bottom=False)


def oak():
    """Broad oak, 7.6 m: a stout trunk 2.6 m clear of the crown, two limbs, a crown of three lumps."""
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.5, 0.34, 3.4, TRUNK, segments=6)
    m.add_frustum((0, 0, 0), 0.7, 0.5, 0.25, TRUNK, segments=6)                       # root flare
    lump(m, (0.0, 5.3, -0.3), 2.3)
    lump(m, (1.75, 4.5, 0.75), 1.6)
    lump(m, (-1.7, 4.4, 0.85), 1.5)
    return m


def willow():
    """Weeping willow, 6 m: a trunk clear to 2.4 m, one Meadow dome with a shade ring under it, and 14 long strands
    (review r1: no stiff cards, no umbrella) that start near the crown's top, lie over the dome and fall past its rim
    to 2.2 to 2.8 m above the ground, alternating Bent grass and Meadow, gaps between them so the trunk shows."""
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.48, 0.34, 3.4, TRUNK, segments=5, cap_top=False)
    lump(m, (0.0, 4.5, 0.0), 2.0, squash=0.7, seg=6, light=None)
    m.add_frustum((0.0, 3.4, 0), 1.6, 1.95, 0.4, SHADE, segments=7, cap_top=False, cap_bottom=True)   # shade ring
    n = 14
    for i in range(n):
        a = 2 * math.pi * (i + 0.3 * (i % 3)) / n
        u = (math.cos(a), math.sin(a))
        t = (-u[1], u[0])
        y_tip = 2.2 if i % 3 == 0 else 2.8
        path = [(0.9, 5.9, 0.5), (2.15, 4.45, 0.5), (2.45, y_tip, 0.28)]                # r, y, width
        def ring(r, y, w, d=0.16):
            return [(u[0] * r + t[0] * w / 2, y, u[1] * r + t[1] * w / 2),
                    (u[0] * r - t[0] * w / 2, y, u[1] * r - t[1] * w / 2),
                    (u[0] * (r - d), y - d * 0.5, u[1] * (r - d))]
        rings = [ring(*p) for p in path]
        col = LIGHT if i % 2 == 0 else BASE
        for ra, rb in zip(rings, rings[1:]):
            loft(m, ra, rb, col, centre=(u[0] * 0.5, 3.6, u[1] * 0.5))
        cap(m, rings[-1], col, facing=(0, -1, 0))
    m.add_sphere((0.1, 5.95, 0.1), 1.05, LIGHT, rings=1, segments=6, squash=0.45)       # crown top where strands part
    return m


def poplar():
    """Lombardy poplar, 9 m: a trunk clear to 1.8 m and a tall column in two lumps, the upper one narrower."""
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.32, 0.2, 2.6, TRUNK, segments=6)
    lump(m, (0, 3.9, 0), 1.45, squash=1.55)
    lump(m, (0.1, 6.9, 0.05), 1.05, squash=1.75)
    return m


def birch():
    """Silver birch, 7.8 m: a slender pale trunk (Limewash with dark bands) and an airy crown about 5 m across of
    three lumps set high, one hung low on a side (review r1), so the white trunk shows under it from the tilt."""
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.26, 0.16, 4.8, "Limewash", segments=5)
    for y, w in ((1.1, 0.21), (2.6, 0.18)):
        m.add_frustum((0, y, 0), w + 0.03, w + 0.02, 0.12, "Mud", segments=5, cap_bottom=True)   # bark bands
    lump(m, (0.2, 6.0, 0.1), 1.55, squash=1.0, seg=6)
    lump(m, (-1.3, 5.2, 0.4), 1.1, squash=0.95, seg=5)
    lump(m, (1.35, 4.5, -0.35), 1.0, squash=0.95, seg=5)
    return m


def hawthorn():
    """Hawthorn bush, a hedge piece: 2.7 m high, 3.4 m long along X: three Meadow lumps over a Cast-iron green
    underside on one short stem, two Bent grass lumps breaking the top, twelve red haws (Brick) round the crown and
    one white blossom (review r1: it was a near-black blob)."""
    m = glb.Mesh()
    m.add_frustum((0.1, 0, 0), 0.24, 0.16, 1.0, TRUNK, segments=5, cap_top=False)
    for (x, y, z), r in (((-0.95, 1.4, 0.0), 1.05), ((0.25, 1.75, 0.1), 1.15), ((1.25, 1.3, -0.1), 0.9)):
        lump(m, (x, y, z), r, squash=0.85, light=None, base=BASE, shade=SHADE, seg=5)
    for (x, y, z), r in (((-0.65, 2.25, 0.25), 0.62), ((-0.1, 2.5, -0.15), 0.45), ((0.7, 2.55, 0.25), 0.58),
                         ((1.35, 1.95, 0.1), 0.45)):
        m.add_sphere((x, y, z), r, LIGHT, rings=1, segments=5, squash=1.05)           # peaked lumps, no plateau
    for k in range(12):                                                              # haws round the crown
        a = 2 * math.pi * (k + 0.35 * (k % 2)) / 12
        rx, rz = 1.85, 1.05
        y = 1.35 + 0.55 * math.sin(a * 2 + 0.7) * 0.5 + (0.45 if math.sin(a) > 0 else 0.15)
        m.add_sphere((0.15 + rx * math.cos(a), y, rz * math.sin(a)), 0.18, "Brick", rings=1, segments=3)
    m.add_sphere((-0.1, 2.85, 0.45), 0.2, "Limewash", rings=1, segments=4)
    return m


for name, make, used in (("tree-oak", oak, "Hamlet: trees (oak)"), ("tree-willow", willow, "Hamlet: trees (weeping willow)"),
                         ("tree-poplar", poplar, "Hamlet: trees (poplar)"), ("tree-birch", birch, "Hamlet: trees (birch)"),
                         ("tree-hawthorn", hawthorn, "Hamlet: trees (hawthorn bush, hedge piece)")):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=300, ink="soft")))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, verified=True)

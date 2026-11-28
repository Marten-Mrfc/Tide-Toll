# python3 assets/src/models/villager-trades2.py  ->  assets/models/villager-head-{fleece,felthat,stocking,turban,fantail,headpad}.glb
# n7 step 3 (the second age's trades, docs/plans/n7/trades2-build-sheet.md, "The figures and icons"): two candidate
# headwears per trade, made exactly as villager-trades.py makes the first three (same face, same 1.4x drawn size, front
# +Z, node "head", in the body's frame so a piece may lie on the shoulders). Shape from above and one flat colour carry
# a trade; none uses a tunic colour (Brick, Packed earth, Cast-iron green, Mud, Soot brick) as its main colour, nor
# the first three's (River, Reed and thatch, Paper/white with a Brick ring) nor a Helper's dark hair.
#   shepherd A (chosen) "fleece": a Limewash fleece hood and a lumpy shoulder mantle (1.0 across): from above
#     the whole top of the figure is wool-white, wider than any hat but the straw hat, with no ring
#   shepherd B "felthat": a Slate roof felt hat, brim 0.56 (between the Hand's 0.48 and the reaper's 0.70), Limewash band
#   weaver A (chosen) "stocking": a Meadow stocking cap whose long tail lies back over the crown, a Limewash tassel:
#     from above a green teardrop pointing back
#   weaver B "turban": a Deep water cloth roll round the head, its crown the same
#   porter A (chosen) "fantail": a Slate roof leather fantail hood: crown, short peak, a broad flat fan behind (a
#     coal-heaver's hat, for sacks on the back): from above a grey fan behind the head, whichever way the porter faces
#   porter B "headpad": a Slate roof padded ring on the hair (for loads on the head), the dark hair showing inside it
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
import math
from _kit import beam

SKIN, HAIR = "Trail earth", "Mud"


def face(m):
    """The face of villager.py's heads (same place, same size)."""
    m.add_sphere((0, 2.16, 0.03), 0.36, SKIN, rings=3, segments=8)
    m.add_box((0, 2.08, 0.37), (0.1, 0.12, 0.08), SKIN, bottom=SKIN)


def head_fleece():
    m = glb.Mesh()
    face(m)
    # The hood: the crown, sides and back in fleece, open at the face (the face sphere stands 8 cm out of it).
    m.add_sphere((0, 2.26, -0.1), 0.41, "Limewash", rings=3, segments=8, squash=0.9)
    # The mantle: a fleece collar over the shoulders, 1.0 across, sloping down from the neck; it hides the tunic's
    # shoulders from above, so the shepherd's top is all pale.
    m.add_frustum((0, 1.55, -0.02), 0.5, 0.26, 0.22, "Limewash", segments=10)
    # Round 2: fleece lumps round the mantle's edge and on the hood, so it reads woolly and wider than the miller's
    # neat white cap (no ring, no band).
    for i in range(5):
        a = math.pi * (0.1 + 0.8 * i / 4)
        m.add_sphere((0.47 * math.cos(a), 1.6, -0.47 * math.sin(a) * 0.9 + 0.1 * (i % 2)), 0.14, "Limewash", rings=2, segments=4)
    m.add_sphere((0.1, 2.58, -0.16), 0.17, "Limewash", rings=2, segments=5)
    return m


def head_felthat():
    m = glb.Mesh()
    face(m)
    m.add_sphere((0, 2.24, -0.08), 0.37, HAIR, rings=2, segments=8, squash=0.7)
    m.add_frustum((0, 2.38, 0), 0.56, 0.45, 0.07, "Slate roof", segments=12)
    m.add_frustum((0, 2.365, 0), 0.55, 0.55, 0.015, "Packed earth", segments=12, cap_top=False)
    m.add_sphere((0, 2.47, 0), 0.31, "Slate roof", rings=2, segments=10, squash=0.75)                # round crown
    m.add_frustum((0, 2.44, 0), 0.33, 0.32, 0.08, "Limewash", segments=10, cap_top=False)            # fleece band
    return m


def head_stocking():
    m = glb.Mesh()
    face(m)
    m.add_sphere((0, 2.28, -0.05), 0.39, "Meadow", rings=3, segments=8, squash=0.78)
    m.add_frustum((0, 2.22, 0.0), 0.385, 0.39, 0.08, "Meadow", segments=10, cap_top=False)           # the turned-up edge
    # The tail: two tapering pieces lying back over the crown and down behind, a little to the right.
    beam(m, (0.0, 2.5, -0.1), (0.1, 2.5, -0.5), 0.3, 0.2, "Meadow")
    beam(m, (0.1, 2.5, -0.5), (0.18, 2.32, -0.74), 0.2, 0.15, "Meadow")
    m.add_sphere((0.2, 2.28, -0.8), 0.1, "Limewash", rings=2, segments=6)                            # tassel
    return m


def head_turban():
    m = glb.Mesh()
    face(m)
    m.add_sphere((0, 2.3, -0.04), 0.37, "Deep water", rings=3, segments=8, squash=0.75)
    m.add_frustum((0, 2.28, -0.02), 0.44, 0.44, 0.16, "Deep water", segments=10)                    # the roll
    m.add_frustum((0, 2.44, -0.02), 0.44, 0.34, 0.05, "Deep water", segments=10, cap_top=False)
    beam(m, (0.3, 2.38, -0.3), (0.38, 2.0, -0.42), 0.14, 0.05, "Deep water")                         # the end
    return m


def head_fantail():
    m = glb.Mesh()
    face(m)
    m.add_sphere((0, 2.3, -0.06), 0.4, "Slate roof", rings=3, segments=8, squash=0.74)              # crown
    m.add_box((0, 2.33, 0.4), (0.44, 0.04, 0.16), "Slate roof", bottom="Packed earth")              # short peak
    # The fantail (round 2: the first, steep flap hid behind the back at the play view's tilt): a broad flat fan from
    # the back of the crown, sloping a little, 0.5 wide at the crown and 1.0 at its hem, 0.6 behind the head: from above
    # a grey fan behind the head whichever way the porter faces.
    m.add_extrude((0, 2.3, 0), [(-0.25, -0.3), (0.25, -0.3), (0.5, -0.9), (-0.5, -0.9)], 0.05, "Slate roof", cap_bottom=True)
    beam(m, (-0.5, 2.3, -0.9), (0.5, 2.3, -0.9), 0.08, 0.1, "Packed earth")                          # the stitched hem
    return m


def head_headpad():
    m = glb.Mesh()
    face(m)
    m.add_sphere((0, 2.3, -0.06), 0.37, HAIR, rings=3, segments=8, squash=0.72)
    # The pad: a fat ring on the crown, 0.84 across, 0.3 open in the middle (the hair shows).
    m.add_frustum((0, 2.4, -0.04), 0.42, 0.42, 0.1, "Slate roof", segments=10, cap_top=False)
    m.add_frustum((0, 2.5, -0.04), 0.42, 0.3, 0.07, "Slate roof", segments=10, cap_top=False)
    m.add_frustum((0, 2.57, -0.04), 0.3, 0.16, 0.0001, "Slate roof", segments=10, cap_top=False)
    return m


def write(name, mesh, budget, used, node="head"):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, [(node, mesh)], max_tris=budget)))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, verified=True)


U = "Villagers, trades of the second age (n7 step 3, board docs/plans/n7/mockup/hats-trades2.png): "
write("villager-head-fleece", head_fleece(), 260, U + "the shepherd's Limewash fleece hood and shoulder mantle (chosen)")
write("villager-head-felthat", head_felthat(), 260, U + "shepherd candidate B, a Slate roof felt hat (dropped; kept for the board)")
write("villager-head-stocking", head_stocking(), 240, U + "the weaver's Meadow stocking cap with a Limewash tassel (chosen)")
write("villager-head-turban", head_turban(), 240, U + "weaver candidate B, a Deep water cloth roll (dropped; kept for the board)")
write("villager-head-fantail", head_fantail(), 240, U + "the porter's Slate roof fantail hood (chosen)")
write("villager-head-headpad", head_headpad(), 240, U + "porter candidate B, a Slate roof head pad ring (dropped; kept for the board)")

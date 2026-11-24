# python3 assets/src/models/tree.py  ->  assets/models/tree-oak.glb, tree-willow.glb, tree-poplar.glb
# "Round gouache canopies that read as map tree symbols from far away and as 3D crowns up close" (Art tab).
# Origin at the foot of the trunk. Round from every side; +Z is the front only by convention.
#   tree-oak     broad lumpy crown about 5.7 m across, 7.4 m high, Meadow with a darker Cast-iron green underside
#   tree-willow  pollard willow (the delta's ditch-side tree): fat short trunk, bushy pale crown, 4.6 m across, 5.5 m high
#   tree-poplar  tall narrow column about 2.9 m across, 9 m high, for avenues and dike tops
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
from common import glb, ledger, asset_path

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
    m.add_frustum((0, 0, 0), 0.62, 0.52, 2.3, TRUNK, segments=6)
    m.add_frustum((0, 2.3, 0), 0.52, 0.75, 0.35, TRUNK, segments=6)                    # the pollard's knuckle
    m.add_sphere((0, 3.2, 0), 2.0, "Cast-iron green", rings=2, segments=7, squash=0.42)
    m.add_sphere((0, 3.55, 0), 1.8, "Bent grass", rings=3, segments=7)
    for x, y, z in ((1.3, 3.3, 0.6), (-1.25, 3.35, 0.65), (0.2, 3.3, -1.35), (0.0, 4.35, 0.2)):
        m.add_sphere((x, y, z), 1.1, "Bent grass", rings=3, segments=6)
    return m


def poplar():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.32, 0.22, 2.0, TRUNK, segments=6)
    m.add_sphere((0, 5.4, 0), 1.65, "Meadow", rings=4, segments=8, squash=2.15)
    m.add_sphere((0, 2.6, 0), 1.2, "Cast-iron green", rings=2, segments=7, squash=0.6)  # shaded skirt
    return m


for name, make, used in (("tree-oak", oak, "Hamlet: trees (oak)"), ("tree-willow", willow, "Hamlet: trees (pollard willow)"),
                         ("tree-poplar", poplar, "Hamlet: trees (poplar)")):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=300)))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, verified=True)

# python3 assets/src/models/ferry.py  ->  assets/models/ferry-raft.glb, ferry-post.glb, ferry-rope.glb
# River set: the Hamlet rope ferry (Gameplay tab: "Ferry, Hamlet, about 30 each way per boat, never blocks boats";
# Art tab: "Ferry at this bank, people boarding; crossing drawn as a dotted line"; Sound tab: "rope pull, hull knock,
# a small hand bell to call the ferry"). Three pieces that the game puts together across any width of river:
#   ferry-raft  5.6 m long (Z, the crossing direction) x 3.6 m wide (X) plank raft on three log floats, deck top
#               0.35 m above the water (y = 0 is the waterline). Rails along both long sides (hand rail 1.0 m above
#               the deck), open ends with a hinged boarding flap at each end, a ferryman's pole on the deck.
#               The rope runs along the +X side through two guide posts: the rope line is x = +1.95, y = 1.3
#               (raft-local), at every z. Slide the raft along Z on the rope; it is symmetric front/back.
#   ferry-post  the landing post on each bank: a stout post (top 2.6 m) with the rope made fast at y = 1.3 and a
#               coil at its foot, and a small hand bell hung from a bracket (to call the ferry). Origin at the post's
#               foot on the bank; the river lies towards -Z; the rope leaves the post at (0, 1.3, -0.3) towards -Z.
#               Place one post on each bank with the far one turned 180 degrees, offset +1.95 m in X from the
#               raft's centre line, so the rope passes through the raft's guides.
#   ferry-rope  a 1 m rope piece from (0, 0, 0) to (0, 0, -1), 0.14 m thick: put it at the near post's rope point,
#               turn it to face the far post and scale Z by the distance between the two rope points.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam, tube
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

DECK, ROPE_X, ROPE_Y = 0.35, 1.95, 1.3
L, W = 5.6, 3.6
WOOD, PLANK, RAIL, ROPE = "Mud", "Trail earth", "Packed earth", "Reed and thatch"


def raft():
    m = glb.Mesh()
    for x in (-1.25, 0.0, 1.25):                                    # three log floats along Z, half sunk
        tube(m, (x, 0.05, L / 2 - 0.1), (x, 0.05, -L / 2 + 0.1), 0.3, WOOD, n=6)
    for i in range(7):                                              # deck planks across X, small gaps
        z = -L / 2 + 0.6 + 0.73 * i
        m.add_box((0, DECK - 0.12, z), (W, 0.12, 0.66), PLANK, bottom=PLANK)
    for sz in (-1, 1):                                              # boarding flaps at both ends, darker
        m.add_box((0, DECK - 0.14, sz * (L / 2 - 0.12)), (2.4, 0.1, 0.5), RAIL, bottom=RAIL)
    for sx in (-1, 1):                                              # rails on both long sides
        x = sx * (W / 2 - 0.12)
        for z in (-2.2, 0.0, 2.2):
            m.add_box((x, DECK, z), (0.16, 1.0, 0.16), WOOD, bottom=False)
        beam(m, (x, DECK + 0.95, -2.3), (x, DECK + 0.95, 2.3), 0.14, 0.14, RAIL)
        beam(m, (x, DECK + 0.5, -2.3), (x, DECK + 0.5, 2.3), 0.1, 0.1, RAIL)
    # rope guides on the +X side: a post outboard of the rail with a fork block that the rope runs through
    for z in (-1.6, 1.6):
        m.add_box((ROPE_X - 0.05, DECK - 0.12, z), (0.3, ROPE_Y - DECK + 0.12 - 0.1, 0.3), WOOD, bottom=WOOD)
        for dx in (-0.16, 0.16):
            m.add_box((ROPE_X + dx, ROPE_Y - 0.12, z), (0.08, 0.3, 0.3), WOOD, bottom=False)
    tube(m, (ROPE_X, ROPE_Y, L / 2 + 0.2), (ROPE_X, ROPE_Y, -L / 2 - 0.2), 0.07, ROPE, n=5)   # rope stub
    beam(m, (-0.6, DECK + 0.08, -2.4), (-0.9, DECK + 0.08, 2.4), 0.12, 0.12, RAIL)               # ferryman's pole
    return m


def post():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.34, 0.26, 2.6, WOOD, segments=6)                  # post
    m.add_cone((0, 2.6, 0), 0.26, 0.2, WOOD, segments=6)                         # weathered top
    m.add_frustum((0, 0, 0), 0.62, 0.5, 0.28, ROPE, segments=7)                   # rope coil at the foot
    m.add_frustum((0, 0.28, 0), 0.5, 0.42, 0.14, ROPE, segments=7, cap_bottom=False)
    m.add_cylinder((0, ROPE_Y - 0.12, 0), 0.36, 0.24, ROPE, segments=6)          # lashing where the rope is made fast
    tube(m, (0, ROPE_Y, -0.2), (0, ROPE_Y, -0.9), 0.07, ROPE, n=5)               # rope leaving towards the river
    # bell bracket towards +X, small hand bell hanging under it, rope pull hanging from the bell
    beam(m, (0.15, 2.2, 0), (0.95, 2.2, 0), 0.14, 0.16, WOOD)
    beam(m, (0.2, 1.75, 0), (0.7, 2.15, 0), 0.1, 0.1, WOOD)                      # strut
    m.add_frustum((0.8, 1.72, 0), 0.24, 0.14, 0.4, "Cast-iron green", segments=7, cap_bottom=True)   # bell
    m.add_box((0.8, 2.12, 0), (0.08, 0.1, 0.08), WOOD, bottom=False)
    beam(m, (0.8, 1.72, 0), (0.8, 1.15, 0), 0.06, 0.06, ROPE)                    # bell pull
    return m


def rope():
    m = glb.Mesh()
    tube(m, (0, 0, 0), (0, 0, -1.0), 0.07, ROPE, n=5)
    return m


for name, make, used, budget in (
        ("ferry-raft", raft, "River: rope ferry raft (rope line at x = +1.95, y = 1.3)", 500),
        ("ferry-post", post, "River: ferry landing post with hand bell, rope made fast at y = 1.3", 300),
        ("ferry-rope", rope, "River: ferry rope, 1 m piece scaled along Z between the two posts", 50)):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=budget)))
    ledger.add("models", out, kind="model", generator=__file__, note=BLOCKED, used_in=used, verified=True)

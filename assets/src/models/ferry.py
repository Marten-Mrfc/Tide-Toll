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
# Round n3 (track RV, 2026-11-26): posts 4.5 m with a Brick painted cap and a Thatch flag (seen at the normal view),
# the raft with Brick-capped rail posts and a pile of Thatch sacks; the rope is a dark Mud line 0.16 m thick that
# sags 0.7 m in the middle (ferry-rope: 1 m long in Z, the sag in absolute metres, so RiverLayer stretches only Z).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam, tube
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

DECK, ROPE_X, ROPE_Y = 0.35, 1.95, 1.3
L, W = 5.6, 3.6
WOOD, PLANK, RAIL, ROPE = "Mud", "Trail earth", "Packed earth", "Reed and thatch"
POST_H, SAG = 4.5, 0.7
import math


def raft():
    """After the review (n3 RV): one clear deck slab. Packed earth deck with a Paper edge band 0.15 m, plank lines in
    the shade tone, two Thatch rail posts a side with Thatch rails (not dark), a mooring post upright at the bow with a
    Brick cap, sacks and a Thatch crate; no loose pole. The rope guides stay on the +X side."""
    m = glb.Mesh()
    for x in (-1.25, 0.0, 1.25):                                    # three log floats along Z, half sunk
        tube(m, (x, 0.05, L / 2 - 0.1), (x, 0.05, -L / 2 + 0.1), 0.3, WOOD, n=6)
    m.add_box((0, DECK - 0.16, 0), (W, 0.16, L), "Paper", top="Paper", bottom=WOOD)          # the edge band shows round
    m.add_box((0, DECK - 0.16, 0), (W - 0.3, 0.18, L - 0.3), "Packed earth", top="Packed earth", bottom=False)  # deck
    for i in range(1, 6):                                           # plank lines across X, standing 1.5 cm proud
        z = -L / 2 + 0.15 + (L - 0.3) * i / 6
        m.add_box((0, DECK + 0.02, z), (W - 0.36, 0.015, 0.05), "Mud", bottom=False)
    for sx in (-1, 1):                                              # two Thatch rail posts a side and one rail
        x = sx * (W / 2 - 0.2)
        for z in (-1.9, 1.9):
            m.add_box((x, DECK + 0.02, z), (0.22, 0.95, 0.22), "Reed and thatch", bottom=False)
        beam(m, (x, DECK + 0.92, -2.05), (x, DECK + 0.92, 2.05), 0.18, 0.14, "Reed and thatch")
    # rope guides on the +X side: a post outboard of the rail with a fork block that the rope runs through
    for z in (-1.6, 1.6):
        m.add_box((ROPE_X - 0.05, DECK - 0.12, z), (0.3, ROPE_Y - DECK + 0.12 - 0.1, 0.3), WOOD, bottom=WOOD)
        for dx in (-0.16, 0.16):
            m.add_box((ROPE_X + dx, ROPE_Y - 0.12, z), (0.08, 0.3, 0.3), WOOD, bottom=False)
    tube(m, (ROPE_X, ROPE_Y, L / 2 - 0.3), (ROPE_X, ROPE_Y, -L / 2 + 0.3), 0.08, "Mud", n=5)   # rope stub, the rope's colour
    m.add_frustum((-0.9, DECK + 0.02, -L / 2 + 0.5), 0.2, 0.17, 1.2, WOOD, segments=6)          # mooring post at the bow
    m.add_frustum((-0.9, DECK + 1.22, -L / 2 + 0.5), 0.22, 0.22, 0.3, "Brick", segments=6)
    for x, z, h, yaw in ((0.55, -1.0, 0.0, 0.2), (0.95, -0.35, 0.0, -0.3), (0.75, -0.65, 0.42, 0.6)):   # sacks
        m.add_box((x, DECK + h, z), (0.6, 0.42, 0.8), "Reed and thatch", yaw=yaw, top="Reed and thatch", bottom=False)
        m.add_box((x, DECK + h + 0.42, z), (0.24, 0.1, 0.24), "Soot brick", yaw=yaw, bottom=False)
    m.add_box((0.6, DECK, 1.2), (0.9, 0.7, 0.8), "Reed and thatch", top="Reed and thatch", bottom=False)    # a crate
    m.add_box((0.6, DECK + 0.3, 1.2), (0.94, 0.08, 0.84), "Packed earth", bottom=False)                    # its band
    return m


def post():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.36, 0.28, POST_H, WOOD, segments=6)              # post
    m.add_frustum((0, POST_H, 0), 0.36, 0.36, 0.3, "Brick", segments=6)          # painted cap
    m.add_cone((0, POST_H + 0.3, 0), 0.38, 0.3, "Paper", segments=6)
    beam(m, (0, POST_H + 0.6, 0), (0, POST_H + 1.3, 0), 0.07, 0.07, WOOD)         # flag staff and flag
    m.add_poly([(0.04, POST_H + 1.28, 0), (0.85, POST_H + 1.1, 0.05), (0.04, POST_H + 0.9, 0)], ROPE, facing=(0, 0, 1))
    m.add_poly([(0.04, POST_H + 1.28, -0.02), (0.85, POST_H + 1.1, 0.03), (0.04, POST_H + 0.9, -0.02)], ROPE, facing=(0, 0, -1))
    m.add_frustum((0, 0, 0), 0.66, 0.54, 0.3, ROPE, segments=7)                   # rope coil at the foot
    m.add_frustum((0, 0.3, 0), 0.54, 0.46, 0.14, ROPE, segments=7, cap_bottom=False)
    m.add_cylinder((0, 2.1, 0), 0.4, 0.26, "Mud", segments=6)                    # lashing where the rope is made fast
    # bell bracket towards +X, small hand bell hanging under it, rope pull hanging from the bell
    beam(m, (0.15, 3.0, 0), (1.0, 3.0, 0), 0.16, 0.18, WOOD)
    beam(m, (0.2, 2.5, 0), (0.75, 2.95, 0), 0.12, 0.12, WOOD)                    # strut
    m.add_frustum((0.85, 2.48, 0), 0.28, 0.16, 0.44, "Cast-iron green", segments=7, cap_bottom=True)   # bell
    m.add_box((0.85, 2.92, 0), (0.08, 0.1, 0.08), WOOD, bottom=False)
    beam(m, (0.85, 2.48, 0), (0.85, 1.6, 0), 0.07, 0.07, ROPE)                   # bell pull
    m.add_box((-1.1, 0, -0.5), (1.0, 0.16, 0.6), "Packed earth", top="Trail earth")  # landing steps, beside the rope
    m.add_box((-1.1, 0, -1.0), (1.0, 0.08, 0.5), "Packed earth", top="Trail earth")
    return m


def rope():
    m = glb.Mesh()
    n = 10
    pts = [(0.0, -SAG * math.sin(math.pi * i / n), -i / n) for i in range(n + 1)]
    for a, b in zip(pts, pts[1:]):
        tube(m, a, b, 0.08, "Mud", n=5, ends=False)
    return m


for name, make, used, budget in (
        ("ferry-raft", raft, "River: rope ferry raft (rope line at x = +1.95, y = 1.3), sacks and a crate on deck", 600),
        ("ferry-post", post, "River: ferry landing post (4.5 m, painted cap, flag, hand bell, landing steps)", 300),
        ("ferry-rope", rope, "River: ferry rope, 1 m piece scaled along Z between the two posts, sagging 0.7 m", 150)):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=budget)))
    ledger.add("models", out, kind="model", generator=__file__, note=BLOCKED, used_in=used, verified=True)

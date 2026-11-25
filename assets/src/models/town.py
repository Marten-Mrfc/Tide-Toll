# python3 assets/src/models/town.py  ->  assets/models/mill.glb, bell-post.glb, plot-stakes.glb, charter-stone.glb,
#   villager-cargo-water.glb, villager-cargo-flour.glb
# First-age pieces for the incremental town (docs/plans/incremental-design.md), in the Hamlet style of cottage.py,
# well.py and market-stall.py: chunky boxes and prisms, one palette colour per face, no modelled outlines.
# Free sources were not used: kenney.nl and the other free asset sites are blocked from the build container
# (checked 2026-11-25), so these are built here as code. Deterministic (stones use a fixed seed).
#
#   mill            post mill, base 6 x 6 m (3 x 3 cells), roof ridge 7.6 m. A timber buck on a central post on a
#                   trestle with stone piers, gable roof with the ridge along Z, door, ladder and tail pole at the
#                   back (-Z). Nodes: "Body" (static) and "Sails" (four sails; node origin = the hub at
#                   (0, 5.6, 2.25); the sails turn about the node's local Z axis, so the game spins Sails.rotation.z).
#                   Sail tips reach 4.6 m from the hub (lowest 1.0 m above the ground).
#   bell-post       the town bell in a timber frame with a little thatch roof, 4 x 2 m (2 x 1 cells), 3.6 m high.
#                   Nodes: "Frame" and "Bell" (node origin = the hanger under the crossbeam at (0, 2.72, 0); the bell
#                   hangs below it and swings about the node's local X axis, so the game rocks Bell.rotation.x).
#   plot-stakes     four stakes and a cord around a cottage plot, 8 x 6 m (4 x 3 cells). Every piece is its own
#                   node so the game can fit any plot without stretching the stakes: "stake-nw", "stake-ne",
#                   "stake-sw", "stake-se" (node origin = the stake's foot, translated to the corner (+-4, 0, +-3));
#                   "cord-n" and "cord-s" run along X, "cord-w" and "cord-e" along Z (node origin = the middle of the
#                   side; cord-n/s are 8 m long along local X and cord-w/e 6 m along local Z, so for another plot move the
#                   stakes and scale each cord along its own axis only: x by width / 8, z by depth / 6). The south-west stake carries a cloth tag.
#   charter-stone   a standing stone with a carved face on the front (+Z): a sunken panel with a ring and three
#                   lines of carving, on two flat footing stones. 2 x 2 m (one cell), 1.9 m high.
#   villager-cargo-water  a yoke across the head with a bucket of water hanging at each side (same form as
#                   villager-cargo-*.glb from villager.py: one node, origin at the bottom centre of the load, attach
#                   at (0, 2.6, 0), the top of the head). The buckets hang BELOW the origin to y = -0.72 (beside the
#                   head, about shoulder height), and the yoke spans x = -0.86..0.86.
#   villager-cargo-flour  a tall pale flour sack with a tied neck (Grain is the round brown sack; Flour reads by its
#                   taller, squarer shape and its Paper colour).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import random

from common import glb, ledger, asset_path
from _kit import beam, tube, loft, cap, stone

BLOCKED = "free asset sites blocked from the build container (checked 2026-11-25)"
WOOD = "Mud"            # dark timber
PLANK = "Trail earth"   # weatherboard
DOOR = "Soot brick"

# ------------------------------------------------------------------ mill
HUB = (0.0, 5.6, 2.25)
SAIL_R = 4.6


def mill_body():
    m = glb.Mesh()
    # trestle: four stone piers, two crossed cross-trees, the main post and four quarter bars
    for x, z in ((-2.1, 0), (2.1, 0), (0, -2.1), (0, 2.0)):
        m.add_box((x, 0, z), (0.7, 0.45, 0.7), "Concrete")
    m.add_box((0, 0.45, 0), (4.8, 0.3, 0.34), WOOD)
    m.add_box((0, 0.45, -0.05), (0.34, 0.3, 4.6), WOOD)
    m.add_box((0, 0.75, 0), (0.5, 2.35, 0.5), WOOD)                                   # main post
    for x, z in ((-2.0, 0), (2.0, 0), (0, -2.0), (0, 1.9)):
        beam(m, (x, 0.65, z), (x * 0.12, 2.6, z * 0.12), 0.22, 0.22, WOOD)              # quarter bars
    # the buck: a weatherboarded body, floor sill and corner posts standing proud
    BW, BH, BD, BY = 3.0, 3.2, 3.6, 3.1
    m.add_box((0, BY, 0), (BW, BH, BD), PLANK, bottom=PLANK)
    m.add_box((0, BY - 0.25, 0), (3.2, 0.25, 3.8), WOOD, bottom=WOOD)                   # crown tree / sill
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.add_box((sx * BW / 2, BY, sz * BD / 2), (0.26, BH, 0.26), WOOD)
    for sx in (-1, 1):                                                                  # side windows
        m.add_box((sx * (BW / 2 + 0.03), BY + 1.7, 0.4), (0.1, 0.6, 0.6), DOOR, bottom=DOOR)
    m.add_box((0.7, BY + 1.9, BD / 2 + 0.03), (0.6, 0.5, 0.1), DOOR, bottom=DOOR)       # breast window (front)
    # gable roof, ridge along Z, dark boards
    top = BY + BH
    m.add_gable((0, top, 0), BD + 0.5, BW + 0.3, 1.25, "Soot brick", gable=PLANK, ridge_axis="z",
                overhang=0.15, bottom=WOOD)
    beam(m, (0, top + 1.23, -(BD + 0.5) / 2), (0, top + 1.23, (BD + 0.5) / 2), 0.3, 0.18, WOOD)   # ridge board
    # windshaft out of the front gable to the hub
    tube(m, (0, HUB[1], 1.6), (0, HUB[1], HUB[2] - 0.25), 0.22, WOOD, n=6)
    # back: door, a landing, the ladder down to the ground and the tail pole
    m.add_box((0, BY, -BD / 2 - 0.03), (1.0, 2.0, 0.1), DOOR, bottom=DOOR)
    m.add_box((0, BY - 0.12, -BD / 2 - 0.45), (1.4, 0.12, 0.8), WOOD, bottom=WOOD)       # landing
    for sx in (-1, 1):
        beam(m, (sx * 0.6, BY - 0.05, -BD / 2 - 0.8), (sx * 0.6, 0.0, -2.95), 0.18, 0.16, WOOD)   # stringers
    for t in (0.25, 0.5, 0.75):
        y = (BY - 0.05) * (1 - t)
        z = (-BD / 2 - 0.8) * (1 - t) + (-2.95) * t
        beam(m, (-0.6, y, z), (0.6, y, z), 0.16, 0.14, PLANK)                           # treads
    beam(m, (0.95, BY + 0.1, -BD / 2), (0.95, 0.35, -2.9), 0.24, 0.24, WOOD)            # tail pole
    m.add_box((0.95, 0, -2.85), (0.5, 0.35, 0.5), "Concrete")                            # its resting stone
    return m


def mill_sails():
    """Four sails in the local XY plane around the hub at the origin; front of the sails is +Z."""
    m = glb.Mesh()
    tube(m, (0, 0, -0.25), (0, 0, 0.35), 0.38, WOOD, n=6)                               # hub (poll end)
    up = (0, 0, 1)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        d = (math.cos(a), math.sin(a))
        n = (-d[1], d[0])                                                               # sail lies on this side
        z = 0.2
        beam(m, (d[0] * 0.3, d[1] * 0.3, z), (d[0] * SAIL_R, d[1] * SAIL_R, z), 0.24, 0.2, WOOD, up=up)   # whip
        # sail cloth on a lattice: outer frame bar, then the cloth panel, then two cross bars across it
        c0, c1, wdt = 1.1, SAIL_R - 0.15, 1.15
        off = 0.12 + wdt / 2
        p0 = (d[0] * c0 + n[0] * off, d[1] * c0 + n[1] * off, z)
        p1 = (d[0] * c1 + n[0] * off, d[1] * c1 + n[1] * off, z)
        beam(m, p0, p1, wdt, 0.06, "Limewash", up=up)
        e0 = (d[0] * c0 + n[0] * (off + wdt / 2), d[1] * c0 + n[1] * (off + wdt / 2), z + 0.04)
        e1 = (d[0] * c1 + n[0] * (off + wdt / 2), d[1] * c1 + n[1] * (off + wdt / 2), z + 0.04)
        beam(m, e0, e1, 0.12, 0.1, WOOD, up=up)                                         # hemlath
        for t in (0.42, 0.75):
            r = c0 + (c1 - c0) * t
            beam(m, (d[0] * r, d[1] * r, z + 0.05),
                 (d[0] * r + n[0] * (off + wdt / 2), d[1] * r + n[1] * (off + wdt / 2), z + 0.05),
                 0.12, 0.1, WOOD, up=up)                                                # sail bars
    return m


# ------------------------------------------------------------------ bell post
HANG_Y = 2.72


def bell_frame():
    m = glb.Mesh()
    for x in (-1.15, 1.15):
        m.add_box((x, 0, 0), (1.0, 0.22, 1.7), "Concrete")                              # stone footing
        m.add_box((x, 0.22, 0), (0.34, 0.2, 1.6), WOOD)                                 # sill
        m.add_box((x, 0.42, 0), (0.3, 2.6, 0.3), WOOD)                                  # post
        for sz in (-1, 1):
            beam(m, (x, 0.42, sz * 0.72), (x, 1.55, sz * 0.1), 0.18, 0.18, WOOD)        # braces
    m.add_box((0, 3.0, 0), (2.8, 0.3, 0.36), "Packed earth", bottom="Packed earth")     # crossbeam
    for sx in (-1, 1):
        beam(m, (sx * 1.15, 2.4, 0), (sx * 0.6, 3.0, 0), 0.18, 0.18, WOOD)              # knee braces
    m.add_box((0, HANG_Y + 0.08, 0), (0.5, 0.2, 0.4), WOOD, bottom=WOOD)                # hanger block
    # a narrow thatch cap over the crossbeam only, so the bell still shows from straight above
    m.add_gable((0, 3.3, 0), 3.2, 0.5, 0.4, "Reed and thatch", gable=WOOD, overhang=0.06, bottom=WOOD)
    beam(m, (-1.62, 3.68, 0), (1.62, 3.68, 0), 0.22, 0.12, "Packed earth")              # ridge binding
    # a rope cleat on the east post, with the bell rope's coil, so the side has a detail
    m.add_box((1.32, 1.1, 0), (0.12, 0.4, 0.2), "Packed earth", bottom="Packed earth")
    m.add_cylinder((1.42, 0.42, 0.0), 0.2, 0.18, "Reed and thatch", segments=6)
    return m


def bell():
    """The bell hangs below its node origin (the hanger)."""
    m = glb.Mesh()
    beam(m, (-0.42, -0.1, 0), (0.42, -0.1, 0), 0.24, 0.2, WOOD)                         # headstock
    m.add_frustum((0, -0.42, 0), 0.2, 0.12, 0.22, "Cast-iron green", segments=8)        # crown
    m.add_frustum((0, -0.95, 0), 0.32, 0.2, 0.53, "Cast-iron green", segments=8)        # waist
    m.add_frustum((0, -1.15, 0), 0.48, 0.32, 0.2, "Cast-iron green", segments=8,
                  cap_bottom=True, cap_colour="Mud")                                    # flared lip
    m.add_box((0, -1.32, 0), (0.12, 0.2, 0.12), WOOD, bottom=WOOD)                      # clapper ball below the lip
    beam(m, (0.0, -0.1, 0.12), (0.0, -0.55, 0.62), 0.1, 0.1, WOOD)                      # lever for the rope
    tube(m, (0.0, -0.55, 0.62), (0.0, -1.75, 0.62), 0.05, "Reed and thatch", n=4)       # bell rope
    return m


# ------------------------------------------------------------------ plot stakes
PX, PZ = 4.0, 3.0
CORD_Y = 0.55


def stake(tag=False):
    m = glb.Mesh()
    m.add_box((0, 0, 0), (0.3, 0.85, 0.3), "Packed earth", top="Trail earth")
    m.add_frustum((0, 0.85, 0), 0.2, 0.07, 0.22, "Trail earth", segments=4, cap_bottom=False)   # cut point
    m.add_cylinder((0, 0, 0), 0.36, 0.06, "Trail earth", segments=5)                    # trodden earth at the foot
    m.add_box((0, CORD_Y - 0.08, 0), (0.36, 0.16, 0.36), "Limewash", bottom="Limewash")  # cord lashing
    if tag:
        m.add_box((0.15, 0.62, 0.2), (0.06, 0.4, 0.4), "Brick", bottom="Brick")          # red cloth tag
    return m


def cord(axis):
    """Cord between two stakes along X (8 m) or Z (6 m), centred on the node origin."""
    m = glb.Mesh()
    a, b = ((-PX, CORD_Y, 0), (PX, CORD_Y, 0)) if axis == "x" else ((0, CORD_Y, -PZ), (0, CORD_Y, PZ))
    beam(m, a, b, 0.12, 0.12, "Limewash")
    return m


def plot_parts():
    parts = [("stake-nw", stake(), None, (-PX, 0, -PZ)), ("stake-ne", stake(), None, (PX, 0, -PZ)),
             ("stake-sw", stake(tag=True), None, (-PX, 0, PZ)), ("stake-se", stake(), None, (PX, 0, PZ))]
    # cords: full side length in the local frame, centred on the side
    for name, axis, t in (("cord-n", "x", (0, 0, -PZ)), ("cord-s", "x", (0, 0, PZ)),
                          ("cord-w", "z", (-PX, 0, 0)), ("cord-e", "z", (PX, 0, 0))):
        parts.append((name, cord(axis), None, t))
    return parts


# ------------------------------------------------------------------ charter stone
def charter_stone():
    rng = random.Random(7)
    m = glb.Mesh()
    stone(m, rng, (-0.25, 0, 0.1), 0.85, 0.16, n=7)                                     # footing stones
    stone(m, rng, (0.35, 0, -0.15), 0.7, 0.12, n=6)
    # the standing stone: a slab whose front (+Z) and back faces are flat, outline in X-Y (convex, rounded top)
    outline = [(-0.5, 0.1), (0.5, 0.1), (0.56, 1.0), (0.46, 1.55), (0.2, 1.85), (-0.18, 1.9), (-0.45, 1.65),
               (-0.58, 1.0)]
    zf, zb = 0.24, -0.24
    front = [(x, y, zf) for x, y in outline]
    back = [(x * 0.94, y, zb) for x, y in outline]
    loft(m, back, front, "Concrete", centre=(0, 1.0, 0))
    cap(m, front, "Concrete", facing=(0, 0, 1))
    cap(m, back, "Concrete", facing=(0, 0, -1))
    # carved face: a sunken panel (darker stone) with a raised ring and three lines of carving
    zc = zf + 0.01
    m.add_poly([(-0.34, 0.45, zc), (0.34, 0.45, zc), (0.36, 1.5, zc), (-0.36, 1.5, zc)], "Wet mudflat",
               facing=(0, 0, 1))
    ring_c, R = (0.0, 1.22), 0.2
    pts = [(ring_c[0] + R * math.cos(2 * math.pi * i / 8 + math.pi / 8),
            ring_c[1] + R * math.sin(2 * math.pi * i / 8 + math.pi / 8), zc + 0.03) for i in range(8)]
    m.add_poly(pts, "Concrete", facing=(0, 0, 1))                                       # boss of the ring
    m.add_box((0, 1.14, zc), (0.12, 0.16, 0.05), "Wet mudflat", bottom="Wet mudflat")   # the ring's hole
    for y, w in ((0.88, 0.52), (0.72, 0.52), (0.56, 0.36)):
        m.add_box((0, y, zc), (w, 0.07, 0.04), "Concrete", bottom="Concrete")           # carved lines
    # back: a lichen patch, so the back is not blank
    m.add_poly([(-0.3, 1.2, zb - 0.01), (0.05, 1.15, zb - 0.01), (0.12, 1.5, zb - 0.01), (-0.2, 1.6, zb - 0.01)],
               "Cast-iron green", facing=(0, 0, -1))
    return m


# ------------------------------------------------------------------ cargo
def cargo_water():
    m = glb.Mesh()
    beam(m, (-0.86, 0.08, 0), (0.86, 0.08, 0), 0.16, 0.14, "Packed earth")              # yoke across the head
    for sx in (-1, 1):
        x = sx * 0.72
        for dz in (-0.17, 0.17):
            beam(m, (x, 0.05, 0), (x, -0.32, dz), 0.05, 0.05, WOOD, ends=False)          # the bucket's two ropes
        m.add_frustum((x, -0.72, 0), 0.2, 0.26, 0.4, "Packed earth", segments=6, cap_top=False, cap_bottom=True)
        m.add_poly([(x + 0.245 * math.cos(math.pi * (i + 0.5) / 3), -0.38, 0.245 * math.sin(math.pi * (i + 0.5) / 3))
                    for i in range(6)], "River")                                        # water inside, near the rim
    return m


def cargo_flour():
    m = glb.Mesh()
    m.add_box((0, 0, 0), (0.56, 0.5, 0.44), "Paper", bottom="Paper")                     # body
    m.add_frustum((0, 0.5, 0), 0.3, 0.12, 0.16, "Paper", segments=6, cap_top=False)       # shoulders
    m.add_frustum((0, 0.62, 0), 0.12, 0.1, 0.08, "Packed earth", segments=6)               # tie
    m.add_frustum((0, 0.7, 0), 0.1, 0.17, 0.12, "Paper", segments=6)                       # tuft above the tie
    m.add_box((0, 0.18, 0.225), (0.3, 0.14, 0.02), "Reed and thatch", bottom="Reed and thatch")   # miller's mark
    return m


BUILDS = [
    ("mill", [("Body", mill_body()), ("Sails", mill_sails(), None, HUB)], 1500,
     "Hamlet: mill (post mill; nodes Body and Sails, Sails spins about its local Z at the hub)"),
    ("bell-post", [("Frame", bell_frame()), ("Bell", bell(), None, (0.0, HANG_Y, 0.0))], 500,
     "Town green: bell post (nodes Frame and Bell, Bell swings about its local X at the hanger)"),
    ("plot-stakes", plot_parts(), 300, "Town: plot marker for the next building (stake and cord nodes)"),
    ("charter-stone", charter_stone(), 300, "Town green: charter stone (after the charter is granted)"),
    ("villager-cargo-water", cargo_water(), 100, "Villagers: carried cargo (water, yoke and buckets)"),
    ("villager-cargo-flour", cargo_flour(), 100, "Villagers: carried cargo (flour sack)"),
]

if __name__ == "__main__":
    for name, parts, budget, used in BUILDS:
        if not isinstance(parts, glb.Mesh):
            for p in parts:
                print(f"  {name}/{p[0]}: {p[1].tri_count()} tris")
        out = asset_path(f"models/{name}.glb")
        print(glb.report(out, glb.write_glb(out, parts, max_tris=budget)))
        ledger.add("models", out, kind="model", generator=__file__, note=BLOCKED, used_in=used, verified=True)
    # preview only (not shipped): the water yoke lifted 1 m, so the contact sheet's ground does not hide the buckets
    from common import ROOT
    prev = ROOT / "build" / "previews" / "villager-cargo-water-lifted.glb"
    glb.write_glb(prev, glb.Mesh().append(cargo_water(), (0, 1.0, 0)))

# python3 assets/src/models/town-props.py [name ...]  ->  assets/models/prop-*.glb
# Ten small props the town dresses itself with as it prospers, in the Hamlet style of river-town.py and cottage.py:
# chunky boxes, prisms and low-segment cylinders, one palette colour per face, no modelled outlines, details standing
# proud by 0.05-0.15 m so the ink pass catches them. Each stays under 150 triangles and about 2 x 2 m (the haystack and
# the apple tree a little larger). Origin at the footprint centre on the ground, front facing +Z. Free sources were not
# used: kenney.nl and the other free asset sites are blocked from the build container (checked 2026-11-25), so these
# are built here as code. Deterministic (no randomness, no clock). Added 2026-11-26.
#
#   prop-woodpile      a stack of split logs (end grain Trail earth, bark Mud) between two stakes, a chopping block
#                      with an axe in it beside it. 1.9 x 1.1 m, 1.0 m high.
#   prop-washing-line  two posts 2 m apart (x = -1..1) and a line at 1.7 m with three cloths (Limewash, Brick, Paper).
#   prop-flower-bed    a raised bed in a plank edging, 1.8 x 1.0 m, Packed earth with clumps of leaves and flower heads
#                      in Brick, Paper and Reed and thatch.
#   prop-hen-coop      a small plank coop with a gabled Soot brick roof on short legs, a ramp to its door, and two hens
#                      (Limewash and Brick bodies, Brick combs; drawn 1.5x like the sheep) in front. 1.8 x 2.0 m.
#   prop-bench         a plank bench with a back rail, 1.8 m long, seat at 0.5 m, facing +Z.
#   prop-beehives      three straw skeps (domed, banded) on a plank stand, 2.0 x 0.8 m.
#   prop-haystack      a round haystack with a thatch cap, 2.6 m across, 2.4 m high, on a ring of spilled hay.
#   prop-apple-tree    small apple tree: short trunk, round crown 2.6 m across (darker underside), red apples standing
#                      out from the crown on all sides and a few fallen in the grass. 3.2 m high.
#   prop-handcart      a two-wheeled handcart parked with its shafts down on the ground to the south (+Z), a sack in it.
#   prop-signpost      one post, two arms pointing different ways (no text), a cap on top.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math

from common import glb, ledger, asset_path
from _kit import beam, tube, loft, cap

BLOCKED = "free asset sites blocked from the build container (checked 2026-11-25)"
WOOD = "Mud"
PLANK = "Trail earth"
BUDGET = 150


# ------------------------------------------------------------------ woodpile
def woodpile():
    m = glb.Mesh()
    # logs along Z (end grain shows to the front and back), three rows stacked in a pyramid, 4-sided rods
    rows = ((0.27, (-0.55, 0.0, 0.55)), (0.74, (-0.275, 0.275)), (1.21, (0.0,)))
    for y, xs in rows:
        for x in xs:
            tube(m, (x, y, -0.5), (x, y, 0.5), 0.27, WOOD, n=6, end_colour="Reed and thatch")   # pale end grain
    # chopping block and the axe stuck in it, to the south-east
    m.add_cylinder((0.75, 0, 0.75), 0.24, 0.42, "Packed earth", segments=4, cap_colour=PLANK)
    beam(m, (0.75, 0.48, 0.75), (0.45, 0.85, 0.95), 0.08, 0.08, PLANK, ends=False)   # handle
    m.add_box((0.75, 0.38, 0.75), (0.26, 0.14, 0.06), "Slate roof", bottom=False)   # head
    return m


# ------------------------------------------------------------------ washing line
def washing_line():
    m = glb.Mesh()
    for x in (-1.0, 1.0):
        m.add_box((x, 0, 0), (0.14, 1.85, 0.14), WOOD)
        beam(m, (x - 0.06 if x < 0 else x + 0.06, 1.75, 0), (x * 0.92, 1.75, 0), 0.1, 0.1, WOOD, ends=False)
    beam(m, (-1.0, 1.72, 0), (1.0, 1.72, 0), 0.05, 0.05, "Paper", ends=False)  # the line
    # three cloths folded over the line: a narrow tent shape (ridge on the line, 0.3 m apart at the hem), so each
    # shows as a colour band from straight above and as a hanging cloth from the side; slightly different drops
    for x, w, h, c in ((-0.55, 0.55, 0.75, "Limewash"), (0.05, 0.5, 0.6, "Brick"), (0.6, 0.45, 0.85, "Paper")):
        m.add_gable((x, 1.74 - h, 0), w, 0.3, h, c, bottom=c)
    return m


# ------------------------------------------------------------------ flower bed
def flower_bed():
    m = glb.Mesh()
    W, D, h = 1.8, 1.0, 0.22
    m.add_box((0, 0, 0), (W, h, D), {"default": PLANK, "+y": "Packed earth"}, bottom=False)
    # clumps: a dark leaf mound with a flower-head cap
    clumps = ((-0.6, -0.2, "Brick"), (-0.15, 0.2, "Paper"), (0.3, -0.18, "Reed and thatch"), (0.65, 0.2, "Brick"),
              (-0.55, 0.25, "Reed and thatch"), (0.2, 0.28, "Brick"))
    for i, (x, z, c) in enumerate(clumps):
        r = 0.22 if i < 4 else 0.17
        m.add_frustum((x, h, z), r, r * 0.8, 0.16, "Cast-iron green", segments=5, yaw=0.4 * i, cap_top=False,
                      cap_bottom=False)
        m.add_frustum((x, h + 0.16, z), r * 0.8, r * 0.45, 0.1, c, segments=5, yaw=0.4 * i, cap_bottom=False)
    return m


# ------------------------------------------------------------------ hen coop
HEN_SCALE = 1.5


def hen(m, pos, yaw, body):
    h = glb.Mesh()
    beam(h, (0, 0.32, -0.2), (0, 0.32, 0.12), 0.26, 0.3, body)               # body
    beam(h, (0, 0.4, -0.2), (0, 0.58, -0.3), 0.18, 0.1, body, ends=False)     # tail up
    h.add_box((0, 0.42, 0.16), (0.14, 0.2, 0.14), body, bottom=False)         # head and neck
    h.add_box((0, 0.62, 0.15), (0.05, 0.08, 0.12), "Brick", bottom=False)    # comb
    h.add_box((0, 0, -0.04), (0.08, 0.17, 0.06), "Reed and thatch")          # legs (one block)
    h.tris = [(a * HEN_SCALE, b * HEN_SCALE, c * HEN_SCALE, col) for a, b, c, col in h.tris]   # drawn 1.5x, like sheep
    m.append(h, pos, yaw)


def hen_coop():
    m = glb.Mesh()
    z0 = -0.35
    for sz in (-1, 1):                                                       # two skids under it
        m.add_box((0, 0, z0 + sz * 0.4), (1.4, 0.35, 0.14), WOOD)
    m.add_box((0, 0.35, z0), (1.3, 0.7, 1.0), "Packed earth", bottom="Packed earth")   # plank house
    m.add_gable((0, 1.05, z0), 1.4, 1.0, 0.5, "Soot brick", gable=PLANK, overhang=0.12)  # roof, ridge along X
    m.add_poly([(0.14, 0.45, z0 + 0.51), (0.46, 0.45, z0 + 0.51), (0.46, 0.88, z0 + 0.51), (0.14, 0.88, z0 + 0.51)],
               WOOD, facing=(0, 0, 1))                   # pop hole
    m.add_poly([(0.15, 0.46, z0 + 0.55), (0.45, 0.46, z0 + 0.55), (0.45, 0.02, z0 + 1.15),
                (0.15, 0.02, z0 + 1.15)], PLANK, facing=(0, 1, 0.8))         # ramp
    hen(m, (-0.5, 0, 0.55), 0.6, "Limewash")
    hen(m, (0.55, 0, 0.75), -0.9, "Brick")
    return m


# ------------------------------------------------------------------ bench
def bench():
    m = glb.Mesh()
    m.add_box((0, 0.42, 0.05), (1.8, 0.1, 0.42), PLANK, bottom=PLANK)          # seat
    for x in (-0.7, 0.7):
        m.add_box((x, 0, 0.05), (0.12, 0.42, 0.36), WOOD)                     # slab legs
        beam(m, (x, 0.0, -0.2), (x, 0.95, -0.24), 0.1, 0.1, WOOD, ends=True)   # back uprights
    m.add_box((0, 0.68, -0.23), (1.8, 0.22, 0.08), PLANK, bottom=PLANK)        # back rail
    return m


# ------------------------------------------------------------------ beehives
def skep(m, x, z, y0):
    """Domed straw skep: banded rings (alternating straw tones) to a rounded top, a dark entrance to +Z."""
    prof = ((0.34, 0.0), (0.35, 0.26), (0.22, 0.48), (0.0, 0.6))
    n = 6
    for i in range(len(prof) - 1):
        r0, y_0 = prof[i]
        r1, y_1 = prof[i + 1]
        c = "Reed and thatch" if i % 2 == 0 else PLANK
        m.add_frustum((x, y0 + y_0, z), r0, r1, y_1 - y_0, c, segments=n, cap_top=False, cap_bottom=False)
    m.add_poly([(x - 0.09, y0 + 0.01, z + 0.35), (x + 0.09, y0 + 0.01, z + 0.35), (x + 0.09, y0 + 0.13, z + 0.355),
                (x - 0.09, y0 + 0.13, z + 0.355)], WOOD, facing=(0, 0, 1))   # entrance


def beehives():
    m = glb.Mesh()
    m.add_box((0, 0.4, 0), (2.0, 0.08, 0.7), PLANK, bottom=PLANK)             # stand board
    for x in (-0.85, 0.85):
        m.add_box((x, 0, 0), (0.12, 0.4, 0.55), WOOD)
    for x in (-0.62, 0.0, 0.62):
        skep(m, x, 0.0, 0.48)
    return m


# ------------------------------------------------------------------ haystack
def haystack():
    m = glb.Mesh()
    n = 8
    prof = ((1.2, 0.0), (1.3, 0.8), (1.15, 1.5), (0.7, 2.05))
    for i in range(len(prof) - 1):
        r0, y0 = prof[i]
        r1, y1 = prof[i + 1]
        m.add_frustum((0, y0, 0), r0, r1, y1 - y0, "Reed and thatch", segments=n, cap_top=False, yaw=0.2,
                      cap_bottom=False)
    m.add_frustum((0, 1.9, 0), 0.95, 0.0, 0.55, PLANK, segments=n, yaw=0.2 + math.pi / n, cap_bottom=False)   # thatch cap
    m.add_box((0, 2.35, 0), (0.12, 0.3, 0.12), WOOD, bottom=False)                           # stake through the top
    # binding rope band and spilled hay ring
    ring = [(1.42 * math.cos(a), 0.04, 1.55 * math.sin(a)) for a in [2 * math.pi * (i + 0.5) / n + 0.2
                                                                     for i in range(n)]]
    cap(m, ring, "Bent grass")
    # a ladder leaning on the south face
    for x in (-0.25, 0.25):
        beam(m, (x, 0.04, 1.6), (x, 1.75, 1.12), 0.08, 0.08, WOOD)
    for t in (0.3, 0.6):
        beam(m, (-0.28, 0.04 + 1.71 * t, 1.6 - 0.48 * t), (0.28, 0.04 + 1.71 * t, 1.6 - 0.48 * t), 0.06, 0.06, WOOD,
             ends=False)
    return m


# ------------------------------------------------------------------ apple tree
def apple_tree():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.2, 0.15, 1.4, WOOD, segments=5)                 # trunk
    m.add_sphere((0, 1.95, 0), 1.0, "Cast-iron green", rings=2, segments=6, squash=0.55)   # dark underside
    m.add_sphere((0.05, 2.25, 0.05), 1.3, "Meadow", rings=3, segments=7, squash=0.7)       # crown
    # apples: chunky octahedra standing proud of the crown, spread round it and on top
    apples = ((0.95, 2.2, 0.7), (-1.0, 2.35, 0.5), (0.1, 2.05, 1.3), (1.25, 2.4, -0.3), (-0.4, 2.6, -1.1),
              (0.35, 3.1, 0.45), (-0.6, 3.0, -0.2))
    for c in apples:
        m.add_sphere(c, 0.17, "Brick", rings=1, segments=4)
    m.add_sphere((0.85, 0.1, 1.1), 0.15, "Brick", rings=1, segments=4)         # a windfall
    return m


# ------------------------------------------------------------------ handcart
def handcart():
    m = glb.Mesh()
    # body tipped forward: back edge on the axle line (z = -0.3) at 0.75 m, shafts resting on the ground to the south
    ax = -0.6
    yb = 0.62
    tilt = math.atan2(yb - 0.04, 1.75)                                          # body + shafts slope to the ground
    g = glb.Mesh()
    g.add_box((0, 0, 0), (1.0, 0.08, 1.2), PLANK, bottom=WOOD)                 # floor
    g.add_box((0, 0.08, -0.56), (1.0, 0.3, 0.08), "Packed earth", bottom=False)    # tailboard
    g.add_box((0, 0.08, 0.56), (1.0, 0.3, 0.08), "Packed earth", bottom=False)     # front board
    for x in (-0.48, 0.48):
        g.add_box((x, 0.08, 0), (0.08, 0.3, 1.2), "Packed earth", bottom=False)    # sides
    for x in (-0.38, 0.38):                                                    # shafts out the front
        beam(g, (x, -0.02, 0.4), (x, -0.02, 1.8), 0.09, 0.09, WOOD)
    g.add_box((0.1, 0.08, -0.1), (0.5, 0.36, 0.4), "Paper", bottom=False)     # sack
    # rotate g about X by -tilt (front down), then place
    c, s = math.cos(tilt), math.sin(tilt)
    for a, b, cc, col in g.tris:
        pts = []
        for p in (a, b, cc):
            y, z = p[1], p[2]
            pts.append((p[0], y * c - z * s + yb, y * s + z * c + ax))
        m.tris.append((*[glb._v(p) for p in pts], col))
    # wheels: 6-sided, on the axle, either side
    for x in (-0.62, 0.62):
        ring = [(x, yb - 0.08 + 0.48 * math.cos(a), ax + 0.48 * math.sin(a)) for a in
                [2 * math.pi * (i + 0.5) / 6 for i in range(6)]]
        ring2 = [(x + (0.08 if x > 0 else -0.08), p[1], p[2]) for p in ring]
        loft(m, ring, ring2, WOOD, centre=(x, yb - 0.08, ax))
        cap(m, ring2, "Packed earth", facing=(1 if x > 0 else -1, 0, 0))
        cap(m, ring, "Packed earth", facing=(-1 if x > 0 else 1, 0, 0))
    beam(m, (-0.62, yb - 0.08, ax), (0.62, yb - 0.08, ax), 0.08, 0.08, WOOD, ends=False)   # axle
    return m


# ------------------------------------------------------------------ signpost
def signpost():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.3, 0.24, 0.16, "Concrete", segments=5)          # stone at the foot
    m.add_box((0, 0, 0), (0.16, 2.3, 0.16), WOOD)                              # post
    m.add_frustum((0, 2.3, 0), 0.14, 0.0, 0.16, "Packed earth", segments=4)    # cap
    # two arms: flat boards with a pointed end, at different heights and directions
    for y, yaw, c in ((1.95, 0.35, PLANK), (1.6, math.pi - 0.6, "Limewash")):
        a = glb.Mesh()
        a.add_extrude((0, 0, 0), [(0.06, -0.05), (0.85, -0.05), (1.05, 0.0), (0.85, 0.05), (0.06, 0.05)], 0.26, c)
        # stand the board up: extrude goes along Y; it is a flat board in X-Y, thickness 0.1 along Z
        for p0, p1, p2, col in a.tris:
            m.tris.append((*[glb._v((p[0] * math.cos(yaw), y + p[1] - 0.13, -p[0] * math.sin(yaw)))
                             + glb._v((p[2] * math.sin(yaw), 0, p[2] * math.cos(yaw)))
                             for p in (p0, p1, p2)], col))
    return m


BUILDS = [
    ("prop-woodpile", woodpile, "Town dressing: woodpile with a chopping block"),
    ("prop-washing-line", washing_line, "Town dressing: washing line with three cloths"),
    ("prop-flower-bed", flower_bed, "Town dressing: flower bed"),
    ("prop-hen-coop", hen_coop, "Town dressing: hen coop with two hens"),
    ("prop-bench", bench, "Town dressing: bench"),
    ("prop-beehives", beehives, "Town dressing: three straw skeps on a stand"),
    ("prop-haystack", haystack, "Town dressing: haystack"),
    ("prop-apple-tree", apple_tree, "Town dressing: small apple tree with fruit"),
    ("prop-handcart", handcart, "Town dressing: handcart parked, shafts down"),
    ("prop-signpost", signpost, "Town dressing: signpost with two arms (no text)"),
]

if __name__ == "__main__":
    only = [a for a in sys.argv[1:] if not a.startswith("--")]
    for name, fn, used in BUILDS:
        if only and name not in only:
            continue
        mesh = fn()
        out = asset_path(f"models/{name}.glb")
        soft = name == "prop-apple-tree"
        print(glb.report(out, glb.write_glb(out, mesh, max_tris=BUDGET, ink="soft" if soft else None)))
        ledger.add("models", out, kind="model", generator=__file__, note=BLOCKED, used_in=used, verified=True)

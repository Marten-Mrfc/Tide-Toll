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
BUDGET = 250   # night 3: the style sheet allows a prop 250


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
    """Night 3 (round 2, BK; fixed after the review): a raised bed edged with 0.25 m Concrete stones, dark soil,
    three leaf cushions in Cast-iron green and Meadow, and twelve blossoms 0.35 m across in Brick, Reed and thatch and
    Paper lying on the cushions, each with a raised centre. 2.2 x 1.2 m."""
    import random
    rng = random.Random(11)
    m = glb.Mesh()
    W, D, h = 2.2, 1.2, 0.25
    m.add_box((0, 0, 0), (W - 0.2, h - 0.04, D - 0.2), {"default": "Mud", "+y": "Mud"}, bottom=False)   # soil
    # edge stones: a ring of blocks, each a little different, standing 4 cm proud of the soil
    xs = [-0.67, 0.0, 0.67]
    for x in xs:
        for sz in (-1, 1):
            m.add_box((x + rng.uniform(-0.03, 0.03), 0, sz * (D / 2 - 0.08)), (0.64, h + rng.uniform(-0.03, 0.02), 0.18),
                      "Concrete", bottom=False)
    for sx in (-1, 1):
        m.add_box((sx * (W / 2 - 0.08), 0, 0), (0.18, h, 0.66), "Concrete", bottom=False)
    cushions = ((-0.6, -0.1, 0.42, "Cast-iron green"), (0.05, 0.1, 0.44, "Meadow"),
                (0.64, -0.08, 0.4, "Cast-iron green"))
    for x, z, r, c in cushions:
        m.add_sphere((x, h, z), r, c, rings=2, segments=6, squash=0.45)
    # night 3 (BK fix): blossoms 0.35 m across in Brick and Reed and thatch (a Paper one now and then), lying on top of
    # the cushions, each with a raised centre (a low cone in the other colour) so it reads as a flower head
    cols = ("Brick", "Reed and thatch", "Brick", "Paper")
    k = 0
    for x, z, r, _ in cushions:
        for i in range(4):
            a = 2 * math.pi * i / 4 + rng.uniform(-0.4, 0.4)
            d = r * 0.5 if i else r * 0.1
            bx, bz = x + math.cos(a) * d, z + math.sin(a) * d
            top = h + r * 0.45 * math.sqrt(max(0.0, 1 - (d / r) ** 2)) + 0.03
            ring = [(bx + 0.175 * math.cos(2 * math.pi * j / 5 + a), top, bz + 0.175 * math.sin(2 * math.pi * j / 5 + a))
                    for j in range(5)]
            col = cols[k % 4]
            m.add_poly(ring, col)
            m.add_cone((bx, top, bz), 0.07, 0.07, "Reed and thatch" if col != "Reed and thatch" else "Soot brick",
                       segments=4, cap_bottom=False)
            k += 1
    return m


# ------------------------------------------------------------------ hen coop
HEN_SCALE = 1.5


def hen(m, pos, yaw, body, tail="Soot brick"):
    """Night 3 (BK fix): a hen you can name: a round body (0.5 m across once drawn 1.5x), a tail wedge cocked up
    behind in a darker tone, a round head on a short neck, a Reed and thatch beak and a Brick comb and wattle."""
    h = glb.Mesh()
    h.add_sphere((0, 0.2, -0.02), 0.17, body, rings=2, segments=6, squash=0.85)         # body
    beam(h, (0, 0.25, -0.12), (0, 0.38, -0.3), 0.14, 0.1, tail)                            # tail wedge, cocked up
    h.add_sphere((0, 0.42, 0.14), 0.09, body, rings=2, segments=5)                      # head
    h.add_cone((0, 0.41, 0.22), 0.04, 0.05, "Reed and thatch", segments=3, yaw=0.0)     # beak (points up; tiny)
    h.add_box((0, 0.49, 0.13), (0.04, 0.07, 0.12), "Brick", bottom=False)               # comb
    h.tris = [(a * HEN_SCALE, b * HEN_SCALE, c * HEN_SCALE, col) for a, b, c, col in h.tris]   # drawn 1.5x, like sheep
    m.append(h, pos, yaw)


def hen_coop():
    """Night 3 (round 2, BK): a coop of Packed earth planks 1.6 x 1.2 m on short legs under a Thatch roof with a
    ridge course, a ramp to its pop hole, a pen of four posts and a Trail earth rail in front, and two hens (Limewash and
    golden Reed and thatch, tails cocked up, Brick combs) in the pen; the door is framed and the ramp wide. 2.0 x 2.4 m."""
    m = glb.Mesh()
    z0 = -0.55
    for sz in (-1, 1):
        m.add_box((0, 0, z0 + sz * 0.45), (1.4, 0.32, 0.14), WOOD)                          # skids
    m.add_box((0, 0.32, z0), (1.5, 0.7, 1.1), "Packed earth", bottom="Mud")              # plank house
    m.add_gable((0, 1.02, z0), 1.7, 1.1, 0.62, "Reed and thatch", gable="Packed earth", overhang=0.16)
    m.add_box((0, 1.6, z0), (1.76, 0.1, 0.2), "Soot brick", bottom="Soot brick")         # ridge course
    m.add_box((0.28, 0.36, z0 + 0.55), (0.56, 0.62, 0.06), "Soot brick", bottom=False)   # door frame, proud
    m.add_poly([(0.06, 0.38, z0 + 0.59), (0.5, 0.38, z0 + 0.59), (0.5, 0.92, z0 + 0.59), (0.06, 0.92, z0 + 0.59)],
               "Ink", facing=(0, 0, 1))                                                  # open door (0.44 x 0.54)
    m.add_poly([(0.04, 0.38, z0 + 0.62), (0.52, 0.38, z0 + 0.62), (0.52, 0.02, z0 + 1.3),
                (0.04, 0.02, z0 + 1.3)], PLANK, facing=(0, 1, 0.8))                       # ramp, wide
    for x in (-0.9, 0.9):
        m.add_box((x, 0, 1.15), (0.16, 0.8, 0.16), WOOD)                                  # pen posts
    beam(m, (-0.9, 0.62, 1.15), (0.9, 0.62, 1.15), 0.1, 0.1, PLANK, ends=False)          # rail
    beam(m, (-0.9, 0.62, 0.1), (-0.9, 0.62, 1.15), 0.1, 0.1, PLANK, ends=False)
    beam(m, (0.9, 0.62, 0.1), (0.9, 0.62, 1.15), 0.1, 0.1, PLANK, ends=False)
    for (x, z, yaw, c, tl) in ((-0.5, 0.55, 0.6, "Limewash", "Limewash"), (0.05, 0.98, -1.2, "Reed and thatch", "Packed earth")):
        hen(m, (x, 0, z), yaw, c, tl)
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
def cart_wheel(m, x, yc, zc, r, t, n=12):
    """A wheel standing on edge in the YZ plane at x: 12-sided rim (Packed earth tread, Packed earth faces), a
    Soot brick felloe ring and an Ink hub on each face, so it reads round from the side and as a dark bar from above."""
    s = 1 if x > 0 else -1
    a = [(x - s * t / 2, yc + r * math.cos(2 * math.pi * (i + 0.5) / n), zc + r * math.sin(2 * math.pi * (i + 0.5) / n))
         for i in range(n)]
    b = [(x + s * t / 2, p[1], p[2]) for p in a]
    loft(m, a, b, "Mud", centre=(x, yc, zc))                                       # tread
    cap(m, b, "Packed earth", facing=(s, 0, 0))                                     # outer face
    for k in (1,):                                                                  # one spoke bar across the hub
        beam(m, (x + s * (t / 2 + 0.02), yc - r * 0.8 * k, zc - r * 0.35), (x + s * (t / 2 + 0.02), yc + r * 0.8 * k,
             zc + r * 0.35), 0.06, 0.06, "Mud", ends=False)
    hub = [(x + s * (t / 2 + 0.05), yc + 0.11 * math.cos(2 * math.pi * i / 6), zc + 0.11 * math.sin(2 * math.pi * i / 6))
           for i in range(6)]
    cap(m, hub, "Ink", facing=(s, 0, 0))


def handcart():
    """Night 3 (round 2, BK fix): a plank bed 0.8 x 1.2 m level on two round wheels 0.9 m across standing on edge
    either side (12 sides, Ink hubs, crossed spokes), two handles 1 m long and 0.2 m thick running out to the south
    (+Z) and resting on a prop leg, and a fat Limewash sack (0.6 m) tied twice with Reed and thatch cord. 1.3 x 2.4 m."""
    m = glb.Mesh()
    R, yb, T = 0.45, 0.5, 0.1
    m.add_box((0, yb, 0), (0.8, 0.08, 1.2), PLANK, bottom=WOOD)                       # bed floor
    for z in (-0.56, 0.56):
        m.add_box((0, yb + 0.08, z), (0.8, 0.28, 0.08), "Packed earth", bottom=False)  # tail and front boards
    for x in (-0.36, 0.36):
        m.add_box((x, yb + 0.08, 0), (0.08, 0.32, 1.2), "Packed earth", bottom=False)  # sides
    for x in (-0.3, 0.3):
        beam(m, (x, yb - 0.02, 0.3), (x, yb + 0.02, 1.55), 0.2, 0.14, WOOD)          # handles, 0.2 m thick
    m.add_box((0, 0, 0.5), (0.5, yb, 0.1), WOOD)                                       # prop leg under the front
    m.add_box((0, yb + 0.08, -0.1), (0.62, 0.5, 0.62), "Limewash", bottom=False)       # sack
    m.add_frustum((0, yb + 0.58, -0.1), 0.3, 0.1, 0.18, "Limewash", segments=6, cap_bottom=False)   # its neck
    for y in (yb + 0.25, yb + 0.6):
        m.add_box((0, y, -0.1), (0.66 if y < yb + 0.5 else 0.3, 0.07, 0.66 if y < yb + 0.5 else 0.3),
                  "Reed and thatch", bottom=False)                                     # two ties
    for x in (-0.47, 0.47):
        cart_wheel(m, x, R, 0.0, R, T)
    beam(m, (-0.45, R, 0), (0.45, R, 0), 0.08, 0.08, WOOD, ends=False)                 # axle
    return m


# ------------------------------------------------------------------ signpost
def signpost():
    """Night 3 (round 2, BK): a stout post (0.28 m) on a stone, a Packed earth cap, two arms 1.2 m long and 0.32 m deep
    pointing different ways with Brick painted tips, and a Thatch lantern hung from a bracket."""
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.42, 0.32, 0.2, "Concrete", segments=5)          # stone at the foot
    m.add_box((0, 0, 0), (0.28, 2.5, 0.28), WOOD)                              # post
    m.add_frustum((0, 2.5, 0), 0.24, 0.0, 0.24, "Packed earth", segments=4)    # cap
    for y, yaw, c in ((2.1, 0.35, PLANK), (1.65, math.pi - 0.6, "Limewash")):
        a = glb.Mesh()
        a.add_extrude((0, 0, 0), [(0.1, -0.1), (1.0, -0.1), (1.2, 0.0), (1.0, 0.1), (0.1, 0.1)], 0.32, c)
        a.add_extrude((0, 0, 0), [(1.0, -0.11), (1.21, 0.0), (1.0, 0.11)], 0.33, "Brick")   # painted tip
        for p0, p1, p2, col in a.tris:
            m.tris.append((*[glb._v((p[0] * math.cos(yaw), y + p[1] - 0.16, -p[0] * math.sin(yaw)))
                             + glb._v((p[2] * math.sin(yaw), 0, p[2] * math.cos(yaw)))
                             for p in (p0, p1, p2)], col))
    beam(m, (0, 1.3, 0.14), (0, 1.3, 0.6), 0.08, 0.08, WOOD)                   # lantern bracket
    m.add_box((0, 0.88, 0.56), (0.24, 0.3, 0.24), "Reed and thatch", bottom="Soot brick")   # lantern
    m.add_frustum((0, 1.18, 0.56), 0.18, 0.04, 0.12, "Soot brick", segments=4)
    return m


BUILDS = [
    ("prop-woodpile", woodpile, "Town dressing: woodpile with a chopping block"),
    ("prop-washing-line", washing_line, "Town dressing: washing line with three cloths"),
    ("prop-flower-bed", flower_bed, "Town dressing: flower bed"),
    ("prop-hen-coop", hen_coop, "Town dressing: thatched hen coop with a pen and three hens"),
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

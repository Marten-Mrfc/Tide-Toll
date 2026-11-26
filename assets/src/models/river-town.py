# python3 assets/src/models/river-town.py  ->  assets/models/pasture.glb, sheep.glb, weaver-cottage.glb, crane.glb,
#   seal-stone.glb, tide-post.glb
# Second-age pieces (docs/plans/age2-design.md, The River), in the Hamlet style of town.py, cottage.py, landing.py and
# field.py: chunky boxes and prisms, one palette colour per face, no modelled outlines, details standing 0.05-0.15 m
# proud so the ink pass catches them. Free sources were not used: kenney.nl and the other free asset sites are blocked
# from the build container (checked 2026-11-25), so these are built here as code. Deterministic (tufts: fixed seed).
#
#   pasture         sheep pasture on the far bank, 16 x 16 m (8 x 8 cells, like field-ripe), on a low Meadow slab.
#                   Low woven wattle fence (0.8 m) on all four sides with a hurdle gate in the south (+Z) side, a
#                   wooden trough with water by the west fence, a hay rack by the north fence, worn Bent-grass
#                   patches and tufts of grass. No sheep: the game scatters sheep.glb inside (keep them within
#                   x, z = -6.6..6.6). Gate gap: x = -1..1 on the south side (z = +7.6).
#   sheep           one sheep, drawn 1.5x like the villagers: 1.9 m long (nose to tail along Z), 1.0 m high; a white
#                   fleece body and a dark head and legs. Front (head) faces +Z. Nodes: "Body" and "Head" (node
#                   origin = the neck at (0, 0.78, 0.5); the game can dip Head.rotation.x for grazing).
#   weaver-cottage  8 x 6 m (4 x 3 cells, like cottage-02). A plank-walled cottage with hipped thatch on the west
#                   two-thirds and an open lean-to on the east (+X) holding a floor loom with red cloth on it, a
#                   bench, a basket of wool skeins and bolts of cloth hanging from the lean-to's front beam. The
#                   lean-to is open to the south and east and has a plank back wall, so the loom reads from the
#                   tilted camera; its plank roof is lower than the thatch and a different colour from above.
#   crane           small treadwheel crane for the landing: a 3 x 3 m cross sill, the treadwheel west of it (whole
#                   frame 4.2 x 3 m, x = -2.63..1.55), post 4.4 m; put it on the bank
#                   beside landing.glb. Nodes: "Base" (frame, treadwheel, post) and "Jib" (node origin =
#                   the pivot at the post top (0, 4.1, 0); the jib arm, the rope and a hanging wool bale; at rest the
#                   arm points +Z, reach 3.2 m; the game turns Jib.rotation.y).
#   seal-stone      the borough seal on a carved plinth for the green: 4 x 4 m (2 x 2 cells) two-step stone platform,
#                   a plinth with sunken carved panels on all four sides and a cornice, and the seal itself, a red
#                   disc with a raised rim and a ship in relief, tilted back so it reads from above and from the
#                   front. 3.6 m high. Grander than charter-stone (2 x 2 m, 1.9 m).
#   tide-post       a square oak post standing in the shallows, 1 x 1 m, origin on the water surface (y = 0 = the
#                   mean water level, like landing.glb); the post runs from y = -1.2 (the river bed, on a stone
#                   footing) to 2.6. Painted bands every 0.5 m (Limewash and Soot brick) and a proud collar at every
#                   whole metre (y = -1, 0, 1, 2) show the water level as the tide moves the water plane. No text.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import random

from common import glb, ledger, asset_path
from _kit import beam, tube, loft, cap, stone

BLOCKED = "free asset sites blocked from the build container (checked 2026-11-25)"
WOOD = "Mud"
PLANK = "Trail earth"
DOOR = "Soot brick"
SEED = 11


# ------------------------------------------------------------------ pasture
def box3(m, pos, size, colour):
    """Fence section: front, back and top only (ends meet the next section, bottom on the ground): 6 triangles."""
    x, y, z = pos
    hx, sy, hz = size[0] / 2, size[1], size[2] / 2
    m.add_poly([(x - hx, y, z + hz), (x + hx, y, z + hz), (x + hx, y + sy, z + hz), (x - hx, y + sy, z + hz)], colour,
               facing=(0, 0, 1))
    m.add_poly([(x - hx, y, z - hz), (x + hx, y, z - hz), (x + hx, y + sy, z - hz), (x - hx, y + sy, z - hz)], colour,
               facing=(0, 0, -1))
    m.add_poly([(x - hx, y + sy, z - hz), (x + hx, y + sy, z - hz), (x + hx, y + sy, z + hz),
                (x - hx, y + sy, z + hz)], colour, facing=(0, 1, 0))


def pasture():
    rng = random.Random(SEED)
    m = glb.Mesh()
    S, E = 16.0, 7.6                     # slab size; fence line half-size
    m.add_box((0, 0, 0), (S, 0.06, S), "Meadow")
    # worn patches: by the trough, the gate path and the hay rack
    for cx, cz, rx, rz in ((-5.6, 0.6, 1.6, 2.4), (0.0, 5.6, 1.3, 2.0), (2.5, -5.8, 2.2, 1.2)):
        m.add_poly([(cx + rx * math.cos(a), 0.07, cz + rz * math.sin(a)) for a in
                    [2 * math.pi * (i + 0.5) / 7 for i in range(7)]][::-1], "Bent grass")
    m.add_poly([(-0.8, 0.075, 7.95), (0.8, 0.075, 7.95), (0.7, 0.075, 4.2), (-0.7, 0.075, 4.2)], "Trail earth")
    # wattle fence: sections of 2.533 m (6 per side), alternate tones pushed to the front or the back, like
    # fence-wattle.glb but one band high; posts at the corners and every second joint
    n, H = 6, 0.72
    L = 2 * E / n
    for side in range(4):
        for i in range(n):
            t = -E + L * (i + 0.5)
            if side == 0 and i in (2, 3):            # south side: gate gap x = -1..1 (sections shortened)
                a, b = (t - L / 2, -1.0) if i == 2 else (1.0, t + L / 2)
                t, ln = (a + b) / 2, b - a
            else:
                ln = L
            k = (i + side) % 2
            col = PLANK if k else "Packed earth"
            off = 0.05 if k else -0.05
            sec = glb.Mesh()
            box3(sec, (t, 0.06, off), (ln + 0.02, H, 0.14), col)
            yaw = (0.0, math.pi / 2, math.pi, -math.pi / 2)[side]
            # side 0 = south (+Z), 1 = east, 2 = north, 3 = west
            pos = {0: (0, 0, E), 1: (E, 0, 0), 2: (0, 0, -E), 3: (-E, 0, 0)}[side]
            m.append(sec, pos, yaw if side in (0, 2) else -yaw)
    posts = [(sx * E, sz * E) for sx in (-1, 1) for sz in (-1, 1)]
    for v in (-E + 2 * L, E - 2 * L):
        posts += [(v, -E), (E, v), (-E, v)]
    posts += [(-1.0, E), (1.0, E)]                  # gate posts
    for x, z in posts:
        tall = abs(z - E) < 1e-6 and abs(abs(x) - 1.0) < 1e-6
        m.add_box((x, 0.06, z), (0.24, 1.15 if tall else 0.95, 0.24), WOOD)
    # hurdle gate, swung half open inwards from the west gate post
    g = glb.Mesh()
    for y in (0.3, 0.8):
        beam(g, (0.1, y, 0), (1.85, y, 0), 0.1, 0.12, PLANK, ends=False)
    beam(g, (0.1, 0.3, 0.06), (1.85, 0.8, 0.06), 0.1, 0.1, "Packed earth")
    beam(g, (1.85, 0.2, 0), (1.85, 0.9, 0), 0.12, 0.12, PLANK)
    m.append(g, (-1.0, 0.0, E), 0.55)
    # trough by the west fence: plank box with water, end boards standing proud
    m.add_box((-6.4, 0.06, 0), (0.8, 0.55, 2.4), PLANK, top="River", bottom=False)
    for z in (-1.2, 1.2):
        m.add_box((-6.4, 0.06, z), (0.95, 0.62, 0.14), WOOD, bottom=False)                # end boards, proud
    # hay rack by the north fence: a V of slats on legs, hay heaped in it
    for x in (1.2, 3.8):
        beam(m, (x, 0.06, -6.6), (x, 1.0, -6.6), 0.16, 0.16, WOOD)
    m.add_gable((2.5, 0.55, -6.6), 2.8, 0.9, -0.45, PLANK, gable=PLANK, ridge_axis="x")      # inverted V (trough of slats)
    m.add_gable((2.5, 0.55, -6.6), 2.6, 0.8, 0.45, "Reed and thatch", gable="Reed and thatch")  # hay heaped in it
    # grass tufts (low 5-sided cones, darker green), seeded positions away from the fence and the furniture
    tufts = 0
    while tufts < 9:
        x, z = rng.uniform(-6.6, 6.6), rng.uniform(-6.6, 6.6)
        if (x < -5.2 and abs(z) < 2.2) or (z < -5.4 and 0.6 < x < 4.4) or (abs(x) < 1.4 and z > 3.8):
            continue
        r = rng.uniform(0.3, 0.45)
        m.add_cone((x, 0.06, z), r, r * 1.3, "Cast-iron green" if tufts % 3 else "Meadow", segments=5,
                   yaw=rng.uniform(0, 1.2))
        tufts += 1
    return m


# ------------------------------------------------------------------ sheep
NECK = (0.0, 0.78, 0.5)


def sheep_body():
    m = glb.Mesh()
    for x in (-0.24, 0.24):                          # legs
        for z in (-0.4, 0.34):
            m.add_box((x, 0, z), (0.14, 0.42, 0.14), WOOD)
    # fleece: a squashed 8-sided body, wider than tall, with a lighter domed back
    ring0 = [(0.42 * math.cos(a), 0.36, 0.72 * math.sin(a)) for a in [2 * math.pi * (i + 0.5) / 8 for i in range(8)]]
    ring1 = [(0.55 * math.cos(a), 0.62, 0.84 * math.sin(a)) for a in [2 * math.pi * (i + 0.5) / 8 for i in range(8)]]
    ring2 = [(0.42 * math.cos(a), 0.98, 0.7 * math.sin(a)) for a in [2 * math.pi * (i + 0.5) / 8 for i in range(8)]]
    loft(m, ring0, ring1, "Limewash", centre=(0, 0.6, 0))
    loft(m, ring1, ring2, "Limewash", centre=(0, 0.6, 0))
    cap(m, ring2, "Paper")
    cap(m, ring0, "Limewash", facing=(0, -1, 0))
    m.add_box((0, 0.66, -0.86), (0.2, 0.2, 0.14), "Limewash", bottom="Limewash")   # tail
    return m


def sheep_head():
    """Head below and in front of the neck pivot (node origin)."""
    m = glb.Mesh()
    beam(m, (0, 0.02, -0.05), (0, -0.14, 0.5), 0.36, 0.38, WOOD)                 # head, long face forward and down
    m.add_box((0, 0.1, 0.02), (0.38, 0.12, 0.24), "Limewash", bottom="Limewash")  # fleece cap
    for sx in (-1, 1):                                                         # ears
        beam(m, (sx * 0.15, 0.04, 0.04), (sx * 0.36, -0.02, 0.0), 0.14, 0.06, WOOD)
    return m


# ------------------------------------------------------------------ weaver cottage
def weaver_cottage():
    m = glb.Mesh()
    # the house: plank walls 4.8 x 5.0, centred at x = -1.2
    cx, L, W, wh = -1.2, 4.8, 5.0, 2.3
    hx, hz = L / 2, W / 2
    m.add_box((cx, 0, 0), (L, wh, W), "Packed earth")
    m.add_hip((cx, wh, 0), L + 0.4, W + 0.4, 3.0, "Reed and thatch", ridge_frac=0.3, overhang=0.3)
    r = (L + 0.4) * 0.3 / 2
    beam(m, (cx - r - 0.25, wh + 2.98, 0), (cx + r + 0.25, wh + 2.98, 0), 0.45, 0.22, "Packed earth")
    for f in (0.3, 0.62):                                    # thatch courses on the long faces, lying on the slope
        hl = 2.9 + f * (r - 2.9) - 0.2
        for sz in (-1, 1):
            beam(m, (cx - hl, 2.02 + f * 3.33, sz * 3.0 * (1 - f)), (cx + hl, 2.02 + f * 3.33, sz * 3.0 * (1 - f)),
                 0.2, 0.09, "Trail earth", up=(0, 3.0, sz * 3.33), ends=False)
    m.add_box((cx + 0.5, 3.4, -0.9), (0.7, 2.9, 0.7), "Brick", bottom=False)             # a chimney: the house is lived in
    m.add_box((cx + 0.5, 6.2, -0.9), (0.94, 0.2, 0.94), "Soot brick", bottom="Soot brick")
    for sx in (-1, 1):                                       # corner posts and sill beams
        for sz in (-1, 1):
            m.add_box((cx + sx * hx, 0, sz * hz), (0.34, wh, 0.34), WOOD)
    for sz in (-1, 1):
        m.add_box((cx, 0, sz * (hz + 0.04)), (L, 0.28, 0.12), WOOD)
    # front door, lintel, a shutter; back window; west window
    m.add_box((cx - 0.9, 0, hz + 0.07), (1.1, 1.8, 0.16), DOOR)
    m.add_box((cx - 0.9, 1.8, hz + 0.08), (1.5, 0.2, 0.2), WOOD, bottom=WOOD)
    m.add_box((cx + 1.2, 0.95, hz + 0.06), (0.75, 0.6, 0.14), WOOD, bottom=WOOD)
    m.add_box((cx, 0.95, -hz - 0.06), (0.75, 0.6, 0.14), WOOD, bottom=WOOD)
    m.add_box((cx - hx - 0.06, 0.95, 0), (0.14, 0.55, 0.7), WOOD, bottom=WOOD)
    # lean-to on the east wall: x from 1.1 to 3.9, plank roof sloping from 2.2 at the wall to 1.85 at the posts
    x0, x1 = cx + hx, 3.75
    for z in (-2.3, 2.3):
        m.add_box((x1 - 0.12, 0, z), (0.22, 1.85, 0.22), WOOD)
    m.add_box((x1 - 0.12, 1.85, 0), (0.24, 0.2, 4.8), WOOD, bottom=WOOD)            # front (east) beam
    t = 0.12
    yi, yo = 2.25, 1.95
    top = [(x0, yi + t, -2.75), (x1 + 0.25, yo + t, -2.75), (x1 + 0.25, yo + t, 2.75), (x0, yi + t, 2.75)]
    bot = [(x0, yi, -2.75), (x1 + 0.25, yo, -2.75), (x1 + 0.25, yo, 2.75), (x0, yi, 2.75)]
    m.add_poly(top, "Brick", facing=(0.1, 1, 0))                                     # Brick-tiled lean-to roof
    m.add_poly(bot[::-1], "Packed earth", facing=(0, -1, 0))
    m.add_poly([bot[1], bot[2], top[2], top[1]], WOOD, facing=(1, 0, 0))
    for z0, s in ((-2.75, -1), (2.75, 1)):
        m.add_poly([(x0, yi, z0), (x1 + 0.25, yo, z0), (x1 + 0.25, yo + t, z0), (x0, yi + t, z0)], WOOD,
                   facing=(0, 0, s))
    for z in (-1.0, 1.0):                                                              # board battens on the roof
        beam(m, (x0 + 0.1, yi + t + 0.04, z), (x1 + 0.2, yo + t + 0.04, z), 0.14, 0.08, "Soot brick", ends=False)
    m.add_box(((x0 + x1) / 2, 0, -2.35), (x1 - x0, 1.9, 0.14), "Packed earth")          # plank back wall (north)
    m.add_box(((x0 + x1) / 2, 0, -2.44), (0.18, 1.9, 0.06), WOOD, bottom=False)         # its batten
    m.add_box(((x0 + x1) / 2, 0, 0), (x1 - x0 + 0.1, 0.06, 4.6), "Trail earth")       # beaten earth floor
    # the loom: four posts, side rails, breast and back beams, the warp and the red cloth woven so far
    lx, lz = 2.5, -0.1
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.add_box((lx + sx * 0.75, 0.06, lz + sz * 0.95), (0.16, 1.5, 0.16), WOOD)
        beam(m, (lx + sx * 0.75, 1.3, lz - 1.0), (lx + sx * 0.75, 1.3, lz + 1.0), 0.14, 0.14, WOOD, ends=False)
    beam(m, (lx - 0.85, 0.95, lz + 0.95), (lx + 0.85, 0.95, lz + 0.95), 0.18, 0.18, PLANK)          # breast beam
    beam(m, (lx - 0.85, 1.15, lz - 0.95), (lx + 0.85, 1.15, lz - 0.95), 0.2, 0.2, PLANK)            # warp beam
    m.add_poly([(lx - 0.6, 1.2, lz - 0.9), (lx + 0.6, 1.2, lz - 0.9), (lx + 0.6, 1.02, lz + 0.05),
                (lx - 0.6, 1.02, lz + 0.05)], "Limewash", facing=(0, 1, 0.2))                       # warp threads
    m.add_poly([(lx - 0.6, 1.02, lz + 0.05), (lx + 0.6, 1.02, lz + 0.05), (lx + 0.6, 1.04, lz + 0.92),
                (lx - 0.6, 1.04, lz + 0.92)], "Brick", facing=(0, 1, 0))                            # cloth woven
    beam(m, (lx - 0.85, 1.55, lz - 0.2), (lx + 0.85, 1.55, lz - 0.2), 0.12, 0.12, WOOD)             # heddle bar
    m.add_box((lx, 0.06, lz + 1.55), (1.4, 0.48, 0.36), WOOD)                                       # bench
    # wool basket and skeins, cloth bolts on the floor
    m.add_frustum((x0 + 0.55, 0.06, 1.7), 0.38, 0.45, 0.45, "Reed and thatch", segments=7)
    m.add_sphere((x0 + 0.55, 0.6, 1.7), 0.3, "Paper", rings=2, segments=5, squash=0.7)
    for i, c in enumerate(("Brick", "Limewash")):
        beam(m, (x0 + 0.3, 0.2 + 0.32 * i, -1.85), (x0 + 1.5, 0.2 + 0.32 * i, -1.85), 0.34, 0.3, c)
    # cloth hanging from the front (east) beam, two lengths, so the trade reads from outside
    for z, c in ((-1.3, "Brick"), (1.2, "Limewash")):
        m.add_box((x1 - 0.02, 0.85, z), (0.08, 1.0, 0.9), c, bottom=c)
    # front (south) yard: a skein drying pole on two forks, red cloth over it
    for x in (0.3, 2.9):
        m.add_box((x, 0, 2.75), (0.14, 1.3, 0.14), WOOD)
    beam(m, (0.25, 1.3, 2.75), (2.95, 1.3, 2.75), 0.12, 0.12, PLANK)
    m.add_box((1.6, 0.75, 2.75), (1.6, 0.55, 0.1), "Brick", bottom="Brick")
    return m


# ------------------------------------------------------------------ crane
PIVOT_Y = 4.1


def crane_base():
    m = glb.Mesh()
    # cross-shaped sill on four stones, the post and four braces
    for x, z in ((-1.3, 0), (1.3, 0), (0, -1.3), (0, 1.3)):
        m.add_box((x, 0, z), (0.5, 0.2, 0.5), "Concrete")
    m.add_box((0, 0.2, 0), (3.0, 0.26, 0.3), WOOD)
    m.add_box((0, 0.2, 0), (0.3, 0.26, 3.0), WOOD)
    m.add_box((0, 0.46, 0), (0.42, PIVOT_Y - 0.46, 0.42), WOOD)                         # post
    m.add_cylinder((0, PIVOT_Y, 0), 0.3, 0.14, "Packed earth", segments=6)              # bearing ring
    for x, z in ((1.3, 0), (-1.3, 0), (0, 1.3), (0, -1.3)):
        beam(m, (x, 0.42, z), (x * 0.12, 2.3, z * 0.12), 0.2, 0.2, WOOD, ends=False)
    # treadwheel west of the post: diameter 2.6 on an axle along X, rims of 8 beams, 8 treads, crossed spokes
    wx0, wx1, wy, R = -2.2, -1.0, 1.55, 1.3
    pts = [(math.cos(2 * math.pi * (i + 0.5) / 8), math.sin(2 * math.pi * (i + 0.5) / 8)) for i in range(8)]
    for x in (wx0, wx1):
        for i in range(8):
            a, b = pts[i], pts[(i + 1) % 8]
            beam(m, (x, wy + R * a[1], R * a[0]), (x, wy + R * b[1], R * b[0]), 0.16, 0.16, PLANK, ends=False,
                 up=(1, 0, 0))
        for i in range(2):
            a = pts[i * 2 + 1]
            beam(m, (x, wy + R * a[1], R * a[0]), (x, wy - R * a[1], -R * a[0]), 0.12, 0.12, WOOD, ends=False)
    for a in pts:
        beam(m, (wx0, wy + R * a[1] * 0.96, R * a[0] * 0.96), (wx1, wy + R * a[1] * 0.96, R * a[0] * 0.96), 0.16,
             0.12, "Packed earth", ends=False)
    beam(m, (-2.55, wy, 0), (-0.21, wy, 0), 0.16, 0.16, WOOD)                           # axle into the post
    for x in (-2.55,):                                                                  # outer A-frame
        beam(m, (x, 0.0, -0.8), (x, wy + 0.1, 0), 0.16, 0.16, WOOD, ends=False)
        beam(m, (x, 0.0, 0.8), (x, wy + 0.1, 0), 0.16, 0.16, WOOD, ends=False)
    tube(m, (-1.6, wy + 0.1, 0), (-0.25, PIVOT_Y - 0.25, 0), 0.05, "Reed and thatch", n=4)   # rope up the post
    return m


def crane_jib():
    """Jib about the pivot (node origin): arm along +Z, brace to the post, rope and a wool bale at the tip."""
    m = glb.Mesh()
    m.add_box((0, 0.0, -0.3), (0.34, 0.3, 0.9), WOOD, bottom=WOOD)                      # head block on the post
    beam(m, (0, 0.2, -0.6), (0, 0.55, 3.2), 0.24, 0.24, WOOD)                           # arm
    beam(m, (0, -1.3, 0.2), (0, 0.38, 1.9), 0.18, 0.18, WOOD)                           # brace
    m.add_box((0, 0.3, 3.15), (0.3, 0.24, 0.3), "Packed earth", bottom="Packed earth")   # sheave block
    tube(m, (0, 0.3, 3.15), (0, -2.4, 3.15), 0.05, "Reed and thatch", n=4, ends=False)   # rope down
    m.add_box((0, -3.0, 3.15), (0.7, 0.6, 0.6), "Paper", bottom="Paper")                 # wool bale
    m.add_box((0, -2.75, 3.15), (0.74, 0.1, 0.64), PLANK, bottom=PLANK)                  # its cord
    return m


# ------------------------------------------------------------------ seal stone
def seal_stone():
    rng = random.Random(SEED)
    m = glb.Mesh()
    # review r1: it read as a square block (a plinth or a crate); now round steps, a round drum 1.6 m high and the
    # seal disc standing on top, wider than the drum, so its silhouette is a coin on a post
    m.add_cylinder((0, 0, 0), 1.9, 0.25, "Concrete", segments=12, cap_bottom=False)    # lower step
    m.add_cylinder((0, 0.25, 0), 1.4, 0.25, "Concrete", segments=10, cap_bottom=False)  # upper step
    m.add_cylinder((0, 0.5, 0), 0.62, 0.22, "Wet mudflat", segments=10, cap_bottom=False)   # base moulding
    m.add_frustum((0, 0.72, 0), 0.48, 0.42, 1.38, "Concrete", segments=10, cap_bottom=False)  # the drum
    for y in (1.15, 1.75):                                                               # carved bands
        m.add_cylinder((0, y, 0), 0.475 - (y - 0.72) * 0.04, 0.1, "Wet mudflat", segments=10, cap_top=False,
                       cap_bottom=False)
    m.add_cylinder((0, 2.1, 0), 0.56, 0.16, "Wet mudflat", segments=10, cap_bottom=True)   # cornice
    for k in range(4):                                                                   # four corner stones
        a = math.pi / 4 + k * math.pi / 2
        stone(m, rng, (math.cos(a) * 1.55, 0.25, math.sin(a) * 1.55), 0.16, 0.3, n=5)
    # the seal: a River-blue disc tilted back 30 degrees on a cradle, raised rim and a ship in relief
    cy, tilt, R = 2.96, math.radians(18), 0.68
    k = R / 0.78                                      # the ship and rim were drawn for a 0.78 m disc
    up = (0, math.cos(tilt), -math.sin(tilt))         # disc's 'up' in its plane
    nrm = (0, math.sin(tilt), math.cos(tilt))         # disc's front normal

    def D(u, v, w=0.0):
        return (u, cy + v * up[1] + w * nrm[1], v * up[2] + w * nrm[2])
    m.add_box((0, 2.26, 0), (0.7, 0.12, 0.5), "Wet mudflat", bottom=False)            # cradle
    n = 12
    ang = [2 * math.pi * (i + 0.5) / n for i in range(n)]
    front = [D(R * math.cos(a), R * math.sin(a), 0.14) for a in ang]
    back = [D(R * math.cos(a), R * math.sin(a), -0.14) for a in ang]
    loft(m, back, front, "River", centre=D(0, 0))
    cap(m, back, "Deep water", facing=tuple(-c for c in nrm))
    cap(m, front, "River", facing=nrm)
    inner = [D(0.62 * k * math.cos(a), 0.62 * k * math.sin(a), 0.2) for a in ang]
    inner0 = [D(0.62 * k * math.cos(a), 0.62 * k * math.sin(a), 0.14) for a in ang]
    loft(m, inner0, inner, "Deep water", centre=D(0, 0, 0.17), inward=True)              # rim step down
    cap(m, [D(0.62 * k * math.cos(a), 0.62 * k * math.sin(a), 0.15) for a in ang], "Deep water", facing=nrm)
    # ship in relief on the sunken face: hull, mast and a square sail (Paper on Deep water; night 3: the borough seal is River blue)
    m.add_poly([D(-0.42 * k, -0.12 * k, 0.2), D(0.42 * k, -0.12 * k, 0.2), D(0.28 * k, -0.34 * k, 0.2), D(-0.28 * k, -0.34 * k, 0.2)], "Paper",
               facing=nrm)
    m.add_poly([D(-0.04 * k, -0.12 * k, 0.2), D(0.04 * k, -0.12 * k, 0.2), D(0.04 * k, 0.42 * k, 0.2), D(-0.04 * k, 0.42 * k, 0.2)], "Paper",
               facing=nrm)
    m.add_poly([D(-0.26 * k, 0.02 * k, 0.21), D(0.26 * k, 0.02 * k, 0.21), D(0.22 * k, 0.34 * k, 0.21), D(-0.22 * k, 0.34 * k, 0.21)], "Limewash",
               facing=nrm)
    return m


# ------------------------------------------------------------------ tide post
def tide_post():
    rng = random.Random(SEED + 1)
    m = glb.Mesh()
    stone(m, rng, (0.1, -1.25, 0.05), 0.5, 0.25, n=6)                                    # footing on the bed
    stone(m, rng, (-0.3, -1.25, -0.25), 0.35, 0.18, n=5)
    y0, y1 = -1.2, 2.6
    # painted bands every 0.5 m: Limewash and Soot brick, alternating
    y = y0
    i = 0
    while y < 2.2 - 1e-6:
        c = "Limewash" if i % 2 else "Soot brick"
        m.add_box((0, y, 0), (0.3, 0.5, 0.3), c, bottom=False if i else c)
        y += 0.5
        i += 1
    m.add_box((0, 2.2, 0), (0.3, y1 - 2.2 - 0.18, 0.3), "Packed earth", bottom=False)     # plain oak above the marks
    m.add_frustum((0, y1 - 0.18, 0), 0.24, 0.08, 0.18, "Packed earth", segments=4, cap_bottom=False)   # cut point
    for yy in (-1.0, 0.0, 1.0, 2.0):                                                      # collars at whole metres
        m.add_box((0, yy - 0.05, 0), (0.42, 0.1, 0.42), WOOD, bottom=WOOD)
    # a mooring ring on the south face, half a metre above the mean level
    tube(m, (-0.14, 0.62, 0.2), (0.14, 0.62, 0.2), 0.08, "Cast-iron green", n=4)
    return m


BUILDS = [
    ("pasture", pasture(), 500, "Far bank: sheep pasture (fenced, gate in the south side, trough, hay rack)"),
    ("sheep", [("Body", sheep_body()), ("Head", sheep_head(), None, NECK)], 150,
     "Far bank: sheep scattered in the pasture (nodes Body and Head, Head dips about its local X at the neck)"),
    ("weaver-cottage", weaver_cottage(), 560, "Near bank: weaver's cottage (Wool to Cloth; loom in the open lean-to)"),
    ("crane", [("Base", crane_base()), ("Jib", crane_jib(), None, (0.0, PIVOT_Y, 0.0))], 500,
     "Landing: treadwheel crane upgrade (nodes Base and Jib, Jib turns about its local Y at the post top)"),
    ("seal-stone", seal_stone(), 600, "Town green: borough seal stone, the seal in River blue (ends the second age)"),
    ("tide-post", tide_post(), 200, "River: tide post in the shallows (painted level marks, water plane shows the tide)"),
]

if __name__ == "__main__":
    only = [a for a in sys.argv[1:] if not a.startswith("--")]
    for name, parts, budget, used in BUILDS:
        if only and name not in only:
            continue
        if not isinstance(parts, glb.Mesh):
            for p in parts:
                print(f"  {name}/{p[0]}: {p[1].tri_count()} tris")
        out = asset_path(f"models/{name}.glb")
        print(glb.report(out, glb.write_glb(out, parts, max_tris=budget, ink="soft" if name == "pasture" else None)))
        ledger.add("models", out, kind="model", generator=__file__,
                   seed=SEED if name in ("pasture", "seal-stone", "tide-post") else None,
                   note=BLOCKED, used_in=used, verified=True)

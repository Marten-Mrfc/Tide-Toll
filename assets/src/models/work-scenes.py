# python3 assets/src/models/work-scenes.py [name ...]  ->  assets/models/work-*.glb
# Night 5 ("work you can watch", docs/plans/n5/work-you-can-watch.md): the things a job scene needs that the game has
# no model for yet. Made like the other models (common.glb, one palette colour per face, real steps and creases for the
# ink pass, nothing ruler-straight, seed 5), sized against the game's villager (the drawn villager is about 3.1 m from
# feet to hat at the game's walker scale, so "life size" here is that scale: a sheaf is a little under knee high).
# First used by the mock-up board (game/tests/AssetAudit.Work.cs); meant for the game if the idea is taken up.
#
# The field is built from row pieces so the game can show any row in any state: one piece is 2.0 m of one row
# (X along the row, 1.6 m pitch across, the field's own pitch in field.py), origin at the row's centre line on the
# earth. A row is drawn as pieces side by side; the reaper's progress is the boundary between ripe and stubble pieces,
# which moves a piece at a time (the swath piece lies just behind the reaper).
#   work-field-bed         the field without crop: field.py's footprint (16 m), a ragged grass rim, dark
#                          furrow earth (Mud); rows are laid on it
#   work-row-ripe          a ridge of earth carrying two staggered lines of upright tufts, toothed tops, ears on top,
#                          two tones (Reed and thatch, every third tuft Trail earth); 0.85 m high
#   work-row-green         regrowth, later: the same ridge with lower green tufts (Meadow, Bent grass tips); 0.45 m
#   work-row-shoots        regrowth, early: small Bent grass and Meadow shoots in two dotted lines; 0.2 m
#   work-row-stubble       stubble: short pale stumps in two dotted lines and a few fallen stalks; 0.15 m
#   work-row-swath         just cut: stalks lying across the row in a loose band, waiting to be bound
#   work-row-edge          the ragged edge where a reaper stands: half the piece standing, half fallen
#   work-sheaf             one sheaf: waisted, bound with a band at 40 percent, splayed ears on top, cut ends below
#   work-stook             three sheaves leaning together, tied at the top
#   work-sickle            a sickle drawn larger than life: crescent blade (Slate roof) 0.25 m at its broadest,
#                          handle (Packed earth); origin at the grip, blade reaching +Z and curling to -X
#   work-scarecrow         post, cross-arm, rag coat (Soot brick), straw head with a hat
#   work-jug               a clay water jug (Brick) with a cloth over it, for the row's end
#   work-wet-patch         wet ground round the well: uneven blobs, trodden earth (Packed earth) outside and wet
#                          ground (Wet mudflat) inside, 1 cm thick,
#                          two puddles (River) and footprints going off toward +Z
#   work-bucket            the well's bucket: staves (Packed earth), two iron bands (Slate roof), a bail; node "Rope"
#                          rises 1.25 m from the bail
#   work-pail              a carrying pail, unlike the bucket: wider and lower, Trail earth, rope handle, water top
#   work-sack-grain        a sack that slumps, with a tied neck (Reed and thatch, tie Packed earth)
#   work-sack-flour        the same in Limewash with a dark band (style sheet 6)
#   work-door-leaf         a plank door, planks of different length, two ledges and a brace; origin at the hinge
#   work-doorway           a door frame with a dark inside (Mud), a stone sill and a sack just inside
#   work-hoist             a hoist beam from the wall with a pulley, a rope and a grain sack on it; origin at the wall
#   work-scales            a balance for the stall's table: base, post, beam, two pans
#   work-apron             an apron in the villager body's frame (Limewash, Brick ties)
#   work-crate             a slatted crate of turnips (Limewash roots, Meadow tops)
#   work-pot               a clay pot with a lid (Brick, Soot brick)
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import random
import numpy as np
from common import glb, ledger, asset_path
from _kit import beam, loft, cap, ngon, hull2d, tube

SEED = 5
PIECE, PITCH = 2.0, 1.6


def tilt(mesh, ax=0.0, az=0.0, yaw=0.0, pos=(0, 0, 0)):
    """Rotate a mesh's points about X then Z (radians, about the origin), then yaw about Y, then move."""
    cx, sx, cz, sz, cy, sy = math.cos(ax), math.sin(ax), math.cos(az), math.sin(az), math.cos(yaw), math.sin(yaw)
    out = glb.Mesh()
    for *ps, col in mesh.tris:
        q = []
        for p in ps:
            x, y, z = p
            y, z = y * cx - z * sx, y * sx + z * cx
            x, y = x * cz - y * sz, x * sz + y * cz
            x, z = cy * x + sy * z, -sy * x + cy * z
            q.append(np.array((x + pos[0], y + pos[1], z + pos[2])))
        out.tris.append((*q, col))
    return out


def blob(rng, cx, cz, r, n=9, amount=0.25, sx=1.0, sz=1.0):
    return hull2d([(cx + x * sx, cz + z * sz) for x, z in
                   [(r * (1 + amount * (rng.random() * 2 - 1)) * math.cos(2 * math.pi * (i + 0.3 * rng.random()) / n),
                     r * (1 + amount * (rng.random() * 2 - 1)) * math.sin(2 * math.pi * (i + 0.3 * rng.random()) / n))
                    for i in range(n)]])


def ridge(m, rng, colour="Packed earth", h=0.12, w=0.95):
    """The row's ridge of earth: a low jittered band 2 m long, a little bowed."""
    bow = rng.uniform(-0.06, 0.06)
    pts = [(-PIECE / 2, -w / 2 + rng.uniform(-.04, .04)), (0, -w / 2 + bow), (PIECE / 2, -w / 2 + rng.uniform(-.04, .04)),
           (PIECE / 2, w / 2 + rng.uniform(-.04, .04)), (0, w / 2 + bow), (-PIECE / 2, w / 2 + rng.uniform(-.04, .04))]
    m.add_extrude((0, 0, 0), hull2d(pts), h + rng.uniform(-.02, .02), colour)


def tufts(m, rng, hmin, hmax, stalk, dark, ear, per_line=6, r=0.15, base=0.1, missing=1):
    """Upright tufts in two staggered lines along the ridge: 4-sided, splaying a little, toothed tops."""
    skip = {rng.randrange(per_line * 2) for _ in range(missing)}
    k = 0
    for line, z0 in enumerate((-0.22, 0.22)):
        for i in range(per_line):
            k += 1
            if k - 1 in skip:
                continue
            x = -PIECE / 2 + (i + 0.5 + 0.5 * line) * PIECE / (per_line + 0.5) + rng.uniform(-.06, .06)
            z = z0 + rng.uniform(-.07, .07)
            h = rng.uniform(hmin, hmax)
            col = dark if k % 3 == 0 else stalk
            rr = r * rng.uniform(0.85, 1.15)
            yaw = rng.uniform(0, math.pi / 2)
            m.add_frustum((x, base, z), rr * 0.8, rr * 1.15, h - base, col, segments=4, yaw=yaw, cap_top=ear is None)
            if ear is not None:
                m.add_cone((x, h, z), rr * 1.15, rng.uniform(0.1, 0.18), ear, segments=4, yaw=yaw)


def crop(m, rng, body_h, hmin, hmax, stalk, dark, ear, per_line=7, r=0.17, w=0.86):
    """Standing crop on a ridge: a dense body (a low jittered prism, so no earth shows through the row) with tufts
    rising out of it in three staggered lines, their tops toothed and uneven, every third one in the dark tone."""
    # the body slopes in (its top 0.12 m narrower on each side, its ends 0.15 m in), so the row is a ridge, not a slab
    bot, top = [], []
    for i in range(9):
        x = -PIECE / 2 + i * PIECE / 8
        jl, jr = rng.uniform(-.05, .03), rng.uniform(-.03, .05)
        for zz, j in ((-w / 2, jl), (w / 2, jr)):
            bot.append((x, zz + j))
    b2 = hull2d(bot)
    cx = sum(x for x, _ in b2) / len(b2)
    t2 = [(cx + (x - cx) * 0.93, z * 0.72) for x, z in b2]
    loft(m, [(x, 0.1, z) for x, z in b2], [(x, 0.1 + body_h, z) for x, z in t2], stalk, centre=(0, 0.1, 0))
    cap(m, [(x, 0.1 + body_h, z) for x, z in t2], stalk)
    k = 0
    for line, z0 in enumerate((-0.33, -0.11, 0.11, 0.33)):
        for i in range(per_line):
            k += 1
            if rng.random() < 0.08:
                continue
            x = -PIECE / 2 + (i + 0.2 + 0.25 * line) * PIECE / per_line + rng.uniform(-.05, .05)
            z = z0 + rng.uniform(-.05, .05)
            h = rng.uniform(hmin, hmax) + (0.06 if line in (1, 2) else -0.05)
            col = dark if k % 3 == 0 else stalk
            rr = r * rng.uniform(0.85, 1.2)
            yaw = rng.uniform(0, math.pi / 2)
            y0 = 0.1 + body_h * 0.6
            m.add_frustum((x, y0, z), rr * 0.75, rr * 1.2, h - y0, col, segments=4, yaw=yaw, cap_top=ear is None)
            if ear is not None:
                m.add_cone((x, h, z), rr * 1.2, rng.uniform(0.1, 0.16), ear, segments=4, yaw=yaw)


def row_ripe():
    rng = random.Random(SEED + 1)
    m = glb.Mesh()
    ridge(m, rng)
    crop(m, rng, 0.42, 0.72, 0.88, "Reed and thatch", "Trail earth", "Reed and thatch")
    return m


def row_green():
    rng = random.Random(SEED + 2)
    m = glb.Mesh()
    ridge(m, rng)
    crop(m, rng, 0.2, 0.36, 0.48, "Meadow", "Cast-iron green", "Bent grass", per_line=6, r=0.16)
    return m


def row_shoots():
    rng = random.Random(SEED + 3)
    m = glb.Mesh()
    ridge(m, rng, h=0.1)
    for line, z0 in enumerate((-0.22, 0.22)):
        for i in range(7):
            if rng.random() < 0.12:
                continue
            x = -PIECE / 2 + (i + 0.4 + 0.4 * line) * PIECE / 7.4 + rng.uniform(-.05, .05)
            m.add_cone((x, 0.08, z0 + rng.uniform(-.05, .05)), rng.uniform(0.09, 0.12), rng.uniform(0.14, 0.22),
                       "Bent grass" if (i + line) % 3 else "Meadow", segments=4, yaw=rng.uniform(0, 1.5))
    return m


def stalk(m, rng, x, z, y, length, ang, colour="Reed and thatch"):
    d = (math.cos(ang) * length / 2, math.sin(ang) * length / 2)
    beam(m, (x - d[0], y, z - d[1]), (x + d[0], y + rng.uniform(0, .03), z + d[1]), 0.09, 0.08, colour)


def row_stubble():
    rng = random.Random(SEED + 4)
    m = glb.Mesh()
    ridge(m, rng, colour="Trail earth", h=0.05)
    for line, z0 in enumerate((-0.22, 0.22)):
        for i in range(8):
            x = -PIECE / 2 + (i + 0.4 + 0.4 * line) * PIECE / 8.4 + rng.uniform(-.04, .04)
            m.add_frustum((x, 0.06, z0 + rng.uniform(-.05, .05)), 0.06, 0.07, rng.uniform(0.08, 0.14),
                          "Reed and thatch", segments=4, yaw=rng.uniform(0, 1.5))
    for _ in range(2):                                                               # a few fallen stalks
        stalk(m, rng, rng.uniform(-.7, .7), rng.uniform(-.45, .45), 0.09, rng.uniform(.45, .7), rng.uniform(-.8, .8))
    return m


def row_swath():
    rng = random.Random(SEED + 5)
    m = glb.Mesh()
    ridge(m, rng, colour="Trail earth", h=0.08)
    for i in range(9):                                                               # stalks lying across, ears one way
        x = -PIECE / 2 + 0.15 + i * 0.21 + rng.uniform(-.05, .05)
        a = math.pi / 2 + rng.uniform(-.5, .5)
        stalk(m, rng, x, rng.uniform(-.1, .1), 0.1 + 0.03 * (i % 2), rng.uniform(.75, .95), a)
        m.add_cone((x + math.cos(a) * .45, 0.1, rng.uniform(-.1, .1) + math.sin(a) * .45), 0.1, 0.12,
                   "Reed and thatch", segments=4)
    return m


def row_edge():
    """Where the reaper stands: the -X half still standing with a ragged face, the +X half cut, stalks fallen."""
    rng = random.Random(SEED + 6)
    m = glb.Mesh()
    ridge(m, rng)
    sub = glb.Mesh()
    crop(sub, rng, 0.42, 0.6, 0.88, "Reed and thatch", "Trail earth", "Reed and thatch")
    for tri in sub.tris:                                                             # keep the -X half only
        if max(p[0] for p in tri[:3]) < -0.05 + 0.25 * math.sin(7 * tri[0][2]):
            m.tris.append(tri)
    for i in range(5):
        x = 0.2 + i * 0.17 + rng.uniform(-.04, .04)
        stalk(m, rng, x, rng.uniform(-.25, .25), 0.12 + 0.03 * (i % 2), rng.uniform(.6, .8), rng.uniform(-.6, .6))
    return m


def field_bed():
    """field.py's footprint and grass headland, a ragged inner edge, and furrow earth (Mud) between rows."""
    rng = random.Random(SEED + 7)
    m = glb.Mesh()
    n, r = 28, 8.0 * 0.98
    pts = []
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        c, s = math.cos(a), math.sin(a)
        k = 1.0 / max(abs(c), abs(s)) ** 0.75
        pts.append((r * k * (1 + 0.04 * (rng.random() * 2 - 1)) * c, r * k * (1 + 0.04 * (rng.random() * 2 - 1)) * s))
    head = hull2d(pts)
    m.add_extrude((0, 0, 0), head, 0.04, "Bent grass")
    inner = hull2d([(x * 0.88 + rng.uniform(-.12, .12), z * 0.88 + rng.uniform(-.12, .12)) for x, z in head])
    m.add_extrude((0, 0.04, 0), inner, 0.05, "Bent grass", top="Mud", cap_bottom=False)
    return m


def sheaf_mesh(rng, h=0.62):
    m = glb.Mesh()
    w = h * 0.36
    m.add_frustum((0, 0, 0), w * 1.05, w * 0.7, h * 0.4, "Reed and thatch", segments=6, yaw=rng.uniform(0, 1))
    m.add_frustum((0, h * 0.36, 0), w * 0.78, w * 0.78, h * 0.09, "Packed earth", segments=6, cap_top=False)  # band
    m.add_frustum((0, h * 0.4, 0), w * 0.7, w * 1.2, h * 0.45, "Reed and thatch", segments=6, cap_top=False,
                  yaw=rng.uniform(0, 1))
    m.add_cone((0, h * 0.85, 0), w * 1.2, h * 0.15, "Reed and thatch", segments=6, yaw=rng.uniform(0, 1))  # ears
    return m


def sheaf():
    return sheaf_mesh(random.Random(SEED + 8))


def stook():
    rng = random.Random(SEED + 9)
    m = glb.Mesh()
    for k in range(3):
        a = 2 * math.pi * k / 3 + rng.uniform(-.2, .2)
        s = sheaf_mesh(rng, 0.62 * rng.uniform(0.92, 1.05))
        # top leans toward the centre: tilt so the top goes to local -Z, turn local +Z to the outward direction
        m.append(tilt(s, ax=-0.27, yaw=math.pi / 2 - a, pos=(math.cos(a) * 0.17, 0, math.sin(a) * 0.17)))
    m.add_frustum((0, 0.5, 0), 0.13, 0.11, 0.08, "Packed earth", segments=5)          # the tie at the top
    return m


def sickle():
    m = glb.Mesh()
    tube(m, (0, 0, -0.42), (0, 0, 0.06), 0.07, "Packed earth", n=6)                  # handle (grip at the origin)
    m.add_frustum((0, -0.07, 0.06), 0.09, 0.09, 0.14, "Slate roof", segments=6, cap_bottom=True)   # ferrule
    # crescent: from the ferrule forward and curling to -X; broad near the middle, a point at the tip
    R, cx, cz = 0.42, -0.42, 0.12
    segs = 7
    prev = None
    for i in range(segs + 1):
        t = i / segs
        a = math.radians(0 + 150 * t)
        broad = 0.25 * math.sin(math.pi * min(1.0, t * 1.3)) + 0.03
        o = (cx + R * math.cos(a), cz + R * math.sin(a))
        inn = (cx + (R - broad) * math.cos(a), cz + (R - broad) * math.sin(a))
        if prev:
            m.add_extrude((0, -0.03, 0), [prev[0], o, inn, prev[1]], 0.06, "Slate roof", cap_bottom=True)
        prev = (o, inn)
    return m


def scarecrow():
    rng = random.Random(SEED + 10)
    m = glb.Mesh()
    beam(m, (0, 0, 0), (0.03, 2.3, 0), 0.16, 0.16, "Packed earth")
    beam(m, (-0.9, 1.65, 0.02), (0.9, 1.7, -0.02), 0.14, 0.14, "Packed earth")
    m.add_frustum((0, 1.05, 0), 0.42, 0.34, 0.75, "Soot brick", segments=6)           # rag coat
    for sx in (-1, 1):
        beam(m, (sx * 0.2, 1.68, 0), (sx * 0.75, 1.62 + rng.uniform(-.05, .05), 0), 0.26, 0.24, "Soot brick")
        m.add_cone((sx * 0.9, 1.5, 0), 0.12, 0.22, "Reed and thatch", segments=4)     # straw at the cuffs
    m.add_sphere((0, 2.05, 0), 0.26, "Reed and thatch", rings=3, segments=6)          # straw head
    m.add_frustum((0, 2.22, 0), 0.46, 0.4, 0.06, "Packed earth", segments=8)          # hat brim
    m.add_frustum((0, 2.28, 0), 0.22, 0.18, 0.2, "Packed earth", segments=7)
    return m


def jug():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.16, 0.22, 0.2, "Brick", segments=7)
    m.add_frustum((0, 0.2, 0), 0.22, 0.1, 0.18, "Brick", segments=7, cap_top=False)
    m.add_frustum((0, 0.38, 0), 0.1, 0.12, 0.08, "Soot brick", segments=7)
    m.add_box((0.05, 0.3, 0.02), (0.36, 0.1, 0.3), "Limewash", yaw=0.4)               # cloth over it
    return m


def wet_patch():
    rng = random.Random(SEED + 11)
    m = glb.Mesh()
    # trodden earth at the outside, the wet ground inside it (two tones, so the edge is soft)
    for k, (cx, cz, r, sx, sz) in enumerate(((0, 0.3, 3.4, 1.0, 0.95), (0.9, 1.6, 2.4, 1.1, 0.8), (0, 0, 2.6, 1.0, 0.9),
                                             (-0.9, 0.7, 1.8, 0.9, 1.0), (0.5, 1.3, 1.9, 1.0, 0.8))):
        m.add_extrude((0, 0.004 + 0.004 * (k >= 2), 0), blob(rng, cx, cz, r, 11, 0.2, sx, sz), 0.01,
                      "Packed earth" if k < 2 else "Wet mudflat")
    m.add_extrude((0, 0.02, 0), blob(rng, 1.6, 1.7, 0.45, 8, 0.25, 1.3, 0.8), 0.01, "River")   # puddles
    m.add_extrude((0, 0.02, 0), blob(rng, -1.9, -0.4, 0.3, 7, 0.25, 1.0, 1.2), 0.01, "River")
    for k in range(6):                                                                # footprints toward +Z (the lane)
        z = 2.0 + k * 0.6
        x = 0.5 + (0.15 if k % 2 else -0.15) + 0.08 * k
        m.add_extrude((0, 0.02, 0), blob(rng, x, z, 0.09, 6, 0.1, 0.8, 1.5), 0.01, "Wet mudflat")
    return m


def bucket():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.3, 0.36, 0.5, "Packed earth", segments=8, cap_top=False, cap_bottom=True)
    for i in range(8):                                                                # staves: proud strips
        a = 2 * math.pi * (i + 0.5) / 8
        if i % 2:
            beam(m, (0.31 * math.cos(a), 0.02, 0.31 * math.sin(a)), (0.37 * math.cos(a), 0.5, 0.37 * math.sin(a)),
                 0.1, 0.03, "Trail earth")
    for y, r in ((0.08, 0.33), (0.38, 0.36)):                                         # two iron bands
        m.add_frustum((0, y, 0), r + 0.03, r + 0.035, 0.07, "Slate roof", segments=8, cap_top=False)
    m.add_frustum((0, 0.42, 0), 0.33, 0.33, 0.02, "River", segments=8)                # water just under the rim
    beam(m, (-0.36, 0.5, 0), (0, 0.85, 0), 0.07, 0.07, "Slate roof")                  # bail
    beam(m, (0, 0.85, 0), (0.36, 0.5, 0), 0.07, 0.07, "Slate roof")
    rope = glb.Mesh()
    tube(rope, (0, 0.85, 0), (0.02, 2.1, 0), 0.05, "Trail earth", n=5)
    return [("Bucket", m), ("Rope", rope)]


def pail():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.38, 0.44, 0.42, "Trail earth", segments=9, cap_top=False, cap_bottom=True)
    m.add_frustum((0, 0.18, 0), 0.43, 0.44, 0.07, "Packed earth", segments=9, cap_top=False)   # one hoop
    m.add_frustum((0, 0.34, 0), 0.41, 0.41, 0.02, "River", segments=9)                # full: water near the rim
    tube(m, (-0.42, 0.42, 0), (0, 0.66, 0), 0.04, "Packed earth", n=4)                # rope handle
    tube(m, (0, 0.66, 0), (0.42, 0.42, 0), 0.04, "Packed earth", n=4)
    return m


def sack_mesh(rng, colour, band=None, h=0.85, slump=0.1):
    m = glb.Mesh()
    prof = [(0.0, 0.44, 0.0), (0.14, 0.5, 0.25), (0.42, 0.4, 0.75), (0.6, 0.22, 1.0), (0.68, 0.1, 1.1)]
    rings = []
    for y, r, s in prof:
        pts = ngon(slump * s, 0, r * h / 0.85, 8, y=y * h / 0.85, phase=0.2, sx=1.18, sz=0.82)
        rings.append([(x + rng.uniform(-.015, .015), y2, z + rng.uniform(-.015, .015)) for x, y2, z in pts])
    for i, (a, b) in enumerate(zip(rings, rings[1:])):
        col = band if band and i == 1 else colour
        loft(m, a, b, col, centre=(slump * 0.5, (a[0][1] + b[0][1]) / 2, 0))
    cap(m, rings[-1], colour)
    top = rings[-1][0][1]
    m.add_frustum((slump, top - 0.02, 0), 0.15, 0.13, 0.07, "Packed earth", segments=6)   # the tie
    m.add_frustum((slump * 1.1, top + 0.05, 0), 0.1, 0.22, 0.16, colour, segments=5)       # the gathered neck above it
    return m


def sack_grain():
    return sack_mesh(random.Random(SEED + 12), "Reed and thatch", slump=0.16)


def sack_flour():
    return sack_mesh(random.Random(SEED + 13), "Limewash", band="Packed earth", slump=-0.08)


def door_leaf():
    """A plank door 1.25 x 2.1 m, hinge on its -X edge at the origin, face toward +Z."""
    rng = random.Random(SEED + 14)
    m = glb.Mesh()
    x = 0.0
    for i in range(4):
        w = 0.3 + rng.uniform(-.02, .02)
        h = 2.05 + rng.uniform(-.08, .06)
        m.add_box((x + w / 2, 0, 0), (w - 0.02, h, 0.1), "Packed earth", yaw=rng.uniform(-.01, .01))
        x += w
    for y in (0.35, 1.55):                                                            # ledges, proud on the face
        beam(m, (0.05, y, 0.08), (x - 0.05, y + rng.uniform(-.03, .03), 0.08), 0.16, 0.06, "Mud")
    beam(m, (0.1, 0.45, 0.1), (x - 0.12, 1.45, 0.1), 0.13, 0.05, "Mud")               # brace
    return m


def doorway():
    """A door frame 1.5 x 2.3 m standing 0.3 m proud of a wall at z = 0, dark inside, a stone sill, a sack inside."""
    rng = random.Random(SEED + 15)
    m = glb.Mesh()
    m.add_box((0, 0, -0.15), (1.3, 2.25, 0.3), "Mud")                                 # the dark inside (its face at +Z)
    for sx in (-1, 1):
        m.add_box((sx * 0.75, 0, 0), (0.22, 2.35, 0.34), "Packed earth", yaw=sx * 0.02)
    m.add_box((0, 2.3, 0), (1.75, 0.24, 0.36), "Packed earth")                        # lintel
    m.add_box((0, 0, 0.18), (1.6, 0.14, 0.5), "Concrete")                             # stone sill
    m.append(tilt(sack_mesh(rng, "Reed and thatch", h=0.7), yaw=0.6), pos=(-0.25, 0.0, -0.05))
    return m


def hoist():
    """A hoist beam out from a wall (origin at the wall, beam along +X), pulley, rope and a grain sack."""
    rng = random.Random(SEED + 16)
    m = glb.Mesh()
    beam(m, (0, 0, 0), (1.5, -0.05, 0), 0.24, 0.26, "Packed earth")
    beam(m, (0, -0.9, 0), (1.0, -0.12, 0), 0.16, 0.16, "Packed earth")               # strut
    m.add_frustum((1.4, -0.36, 0), 0.15, 0.15, 0.22, "Slate roof", segments=7)  # pulley block
    tube(m, (1.4, -0.3, 0), (1.42, -2.7, 0), 0.05, "Trail earth", n=5)
    m.append(sack_mesh(rng, "Reed and thatch", h=0.8, slump=0.0), pos=(1.42, -3.45, 0))
    return m


def scales():
    m = glb.Mesh()
    m.add_box((0, 0, 0), (0.42, 0.08, 0.3), "Packed earth")
    beam(m, (0, 0.08, 0), (0, 0.62, 0), 0.08, 0.08, "Packed earth")
    beam(m, (-0.42, 0.6, 0), (0.42, 0.64, 0), 0.08, 0.08, "Slate roof")
    for sx, dy in ((-1, 0.0), (1, 0.04)):
        beam(m, (sx * 0.4, 0.6 + dy, 0), (sx * 0.4, 0.34 + dy, 0), 0.08, 0.08, "Slate roof")
        m.add_frustum((sx * 0.4, 0.26 + dy, 0), 0.12, 0.2, 0.08, "Reed and thatch", segments=8)   # brass pans
    return m


def apron():
    """In the villager body's frame (villager.py): a bib and skirt over the tunic's front, ties at the belt."""
    m = glb.Mesh()
    rows = [(0.6, 0.29, 0.415), (1.0, 0.27, 0.385), (1.12, 0.2, 0.37), (1.5, 0.18, 0.33)]
    for (y0, w0, z0), (y1, w1, z1) in zip(rows, rows[1:]):
        q = [(-w0, y0, z0), (w0, y0, z0), (w1, y1, z1), (-w1, y1, z1)]
        qb = [(x, y, z - 0.04) for x, y, z in q]
        m.add_poly(q, "Limewash", facing=(0, 0, 1))
        for a, b in ((0, 1), (1, 2), (2, 3), (3, 0)):
            side = [q[a], q[b], qb[b], qb[a]]
            m._face([np.array(p) for p in side], "Limewash", np.mean([np.array(p) for p in side], axis=0) - np.array((0, (y0 + y1) / 2, z0 - 0.02)))
    beam(m, (-0.32, 1.06, 0.36), (0.32, 1.06, 0.36), 0.08, 0.08, "Brick")           # ties at the belt
    return m


def crate():
    rng = random.Random(SEED + 17)
    m = glb.Mesh()
    m.add_box((0, 0, 0), (0.85, 0.45, 0.6), "Trail earth")
    for y in (0.08, 0.3):
        for sz in (-1, 1):
            beam(m, (-0.44, y, sz * 0.31), (0.44, y, sz * 0.31), 0.1, 0.05, "Packed earth", up=(0, 1, 0))
    for i in range(6):                                                                # turnips: pale roots, green tops
        x, z = -0.27 + (i % 3) * 0.27 + rng.uniform(-.03, .03), -0.13 + (i // 3) * 0.26
        m.add_sphere((x, 0.5, z), 0.12, "Limewash", rings=2, segments=5)
        m.add_cone((x, 0.58, z), 0.1, 0.16, "Meadow", segments=4)
    return m


def pot():
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.18, 0.27, 0.3, "Brick", segments=8)
    m.add_frustum((0, 0.3, 0), 0.27, 0.17, 0.12, "Brick", segments=8, cap_top=False)
    m.add_frustum((0, 0.42, 0), 0.2, 0.18, 0.06, "Soot brick", segments=8)            # lid
    m.add_frustum((0, 0.48, 0), 0.06, 0.04, 0.06, "Soot brick", segments=5)
    return m


ITEMS = [
    ("work-field-bed", field_bed, 900, "soft", "Mock-up n5: the field's bed (grass rim, furrow earth) under the row pieces"),
    ("work-row-ripe", row_ripe, 520, "soft", "Mock-up n5: a 2 m piece of a ripe row"),
    ("work-row-green", row_green, 450, "soft", "Mock-up n5: a 2 m piece of a row growing back (green)"),
    ("work-row-shoots", row_shoots, 250, "soft", "Mock-up n5: a 2 m piece of a row growing back (shoots)"),
    ("work-row-stubble", row_stubble, 250, "soft", "Mock-up n5: a 2 m piece of a cut row (stubble)"),
    ("work-row-swath", row_swath, 300, "soft", "Mock-up n5: a 2 m piece of a row just cut (swath lying)"),
    ("work-row-edge", row_edge, 450, "soft", "Mock-up n5: the reaper's ragged edge in a row"),
    ("work-sheaf", sheaf, 120, None, "Mock-up n5: a sheaf"),
    ("work-stook", stook, 250, None, "Mock-up n5: a stook of three sheaves"),
    ("work-sickle", sickle, 150, None, "Mock-up n5: a reaper's sickle"),
    ("work-scarecrow", scarecrow, 250, None, "Mock-up n5: a scarecrow at the field"),
    ("work-jug", jug, 120, None, "Mock-up n5: a water jug at a row's end"),
    ("work-wet-patch", wet_patch, 400, None, "Mock-up n5: wet ground, puddles and footprints round the well"),
    ("work-bucket", bucket, 250, None, "Mock-up n5: the well's bucket and rope"),
    ("work-pail", pail, 150, None, "Mock-up n5: a full pail on the well's rim"),
    ("work-sack-grain", sack_grain, 120, None, "Mock-up n5: a grain sack set down"),
    ("work-sack-flour", sack_flour, 120, None, "Mock-up n5: a flour sack set down"),
    ("work-door-leaf", door_leaf, 150, None, "Mock-up n5: the mill's door standing open"),
    ("work-doorway", doorway, 250, None, "Mock-up n5: the mill's open doorway"),
    ("work-hoist", hoist, 250, None, "Mock-up n5: the mill's hoist with a sack"),
    ("work-scales", scales, 150, None, "Mock-up n5: a balance on the market stall"),
    ("work-apron", apron, 80, None, "Mock-up n5: a stallholder's apron (villager body frame)"),
    ("work-crate", crate, 250, None, "Mock-up n5: a crate of turnips on the stall"),
    ("work-pot", pot, 100, None, "Mock-up n5: a clay pot on the stall"),
]

want = set(sys.argv[1:])
for name, make, budget, ink, used in ITEMS:
    if want and name not in want:
        continue
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=budget, ink=ink)))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, seed=SEED, verified=False)

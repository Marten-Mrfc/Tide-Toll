# python3 assets/src/models/quay-scenes.py [name ...]  ->  assets/models/quay-*.glb
# Night 6 (docs/plans/n6/boats-design.md, part C and D): the things the designed landing needs that the game has no
# model for: the quay on piles, the treadwheel crane, the yard's stacks by kind, a sign per berth, a mooring post
# for waiting boats, a gangplank, a mooring line, a net of sacks and a bow wake. Made like the other models
# (common.glb, one palette colour per face, real steps and creases for the ink pass, nothing ruler-straight, seed 6).
# y = 0 is the bank and the water's surface at high water (as landing.py). First used by the mock-up board
# (game/tests/AssetAudit.Landing.cs); meant for the game if the design is taken up.
#   quay-deck        a timber quay 18 m long (Z, -9..9) and 2.6 m deep (X, -0.6 on the bank .. 2.0 over the water):
#                    planks across in two tones, a string piece along the water face, piles to below the water with
#                    pale caps, cross-braces, a bollard and an iron ladder at each berth (z = -4.5 and +4.5),
#                    a kerb on the land side
#   quay-crane       a treadwheel crane's standing part: a stepped stone base 2.4 m square, a post, a jib 5 m long
#                    rising along +X, its rope down from the tip to a net of sacks (posed mid-swing: the net 2.6 m up);
#                    origin at the post's foot; the game would split jib and net into nodes
#   quay-treadwheel  the treadwheel beside it: a wheel 3 m across on two A-frames, axle along X, rim and spokes
#   quay-stack-grain, quay-stack-flour   sacks stacked 3-2-1 on a low pallet
#   quay-stack-wool  fleece bales, lumpy, tied, on a pallet
#   quay-stack-cloth bolts of cloth in two colours, rolled, on a pallet
#   quay-sign-grain, quay-sign-flour   a berth's sign: a post and a board, the good's icon in relief (a sack with
#                    ears of grain, or a banded flour sack), a bar of how much is aboard (frame, fill, the fill's
#                    colour is the time left: Cast-iron green plenty, Reed and thatch some, Brick little); grain 60 %
#                    green, flour 30 % thatch; board faces +Z
#   quay-mooring-post three piles lashed together, standing out of the water 1.4 m
#   quay-gangplank   a plank 2.2 m along +X with cleats, origin at its low end
#   quay-line        a mooring line 1 m along +X (stretched along X in the scene), sagging
#   quay-wake        a V of broken water behind a boat moving toward -Z, 7 m long, origin at the bow
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import random
import numpy as np
from common import glb, ledger, asset_path
from _kit import beam, loft, cap, ngon, tube, stone

SEED = 6
DECK = 0.5
WOOD, PLANK, DARK = "Packed earth", "Trail earth", "Mud"


def sack_mesh(rng, colour, band=None, h=0.85, slump=0.1):
    """The slumped sack of work-scenes.py (same build), copied so this file runs alone."""
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
    m.add_frustum((slump, top - 0.02, 0), 0.15, 0.13, 0.07, "Packed earth", segments=6)
    m.add_frustum((slump * 1.1, top + 0.05, 0), 0.1, 0.22, 0.16, colour, segments=5)
    return m


def lying_sack(m, rng, x, y, z, colour, band=None, yaw=0.0):
    """A sack lying on its side for a stack: a squashed, jittered octagonal body with a tied end."""
    L, r = 0.95, 0.3
    c, s = math.cos(yaw), math.sin(yaw)
    def P(u, v, w):  # u along the sack, v up, w across
        return (x + u * c - w * s, y + v, z + u * s + w * c)
    rings = []
    for u, k in ((-L / 2, 0.8), (-L / 4, 1.0), (L / 4, 1.0), (L / 2, 0.7)):
        ring = []
        for i in range(8):
            a = 2 * math.pi * (i + 0.5) / 8
            ring.append(P(u + rng.uniform(-.02, .02), r * 0.72 * k * (1 + math.sin(a)) + rng.uniform(-.01, .01), r * 1.05 * k * math.cos(a)))
        rings.append(ring)
    for i, (a, b) in enumerate(zip(rings, rings[1:])):
        loft(m, a, b, band if (band and i == 1) else colour, centre=P(0, r * 0.72, 0))
    cap(m, rings[0], colour, facing=(-c, 0, -s))
    cap(m, rings[-1], colour, facing=(c, 0, s))
    e = P(L / 2 + 0.08, r * 0.7, 0)
    tube(m, P(L / 2, r * 0.7, 0), e, 0.1, "Packed earth", n=5)
    tube(m, e, P(L / 2 + 0.24, r * 0.75, 0), 0.13, colour, n=5, r1=0.06)


def pallet(m, w=2.0, d=1.3):
    for z in (-d / 2 + 0.1, 0, d / 2 - 0.1):
        m.add_box((0, 0, z), (w, 0.1, 0.16), DARK)
    for i in range(6):
        x = -w / 2 + 0.17 + i * (w - 0.34) / 5
        m.add_box((x, 0.1, 0), (0.3, 0.06, d), PLANK if i % 2 else WOOD)


def quay_deck():
    rng = random.Random(SEED)
    m = glb.Mesh()
    x0, x1 = -0.6, 2.0
    z = -9.0
    i = 0
    while z < 9.0 - 1e-6:                                                # planks across, two tones, small gaps
        w = 0.55 + rng.uniform(-0.05, 0.05)
        w = min(w, 9.0 - z)
        m.add_box(((x0 + x1) / 2 + rng.uniform(-.04, .04), DECK - 0.14, z + w / 2), (x1 - x0 + rng.uniform(-.05, .05), 0.14, w - 0.04),
                  PLANK if i % 3 else WOOD, yaw=rng.uniform(-0.012, 0.012), bottom=DARK)
        z += w; i += 1
    beam(m, (x1 + 0.08, DECK - 0.02, -9.1), (x1 + 0.08, DECK + 0.02, 9.1), 0.26, 0.26, DARK)       # string piece
    beam(m, (x0 - 0.05, DECK - 0.05, -9.1), (x0 - 0.05, DECK, 9.1), 0.22, 0.2, WOOD)               # kerb, land side
    for zz in [-8.6 + 2.15 * k for k in range(9)]:                       # piles on the water face and under the middle
        for xx, top in ((x1 + 0.12, DECK + 0.35), (0.9, DECK - 0.14)):
            m.add_box((xx, -1.6, zz + rng.uniform(-.05, .05)), (0.32, top + 1.6, 0.32), DARK, bottom=False)
            if top > DECK:
                m.add_box((xx, top, zz), (0.38, 0.08, 0.38), "Paper", bottom="Paper")
    for zz in [-8.6 + 2.15 * k for k in range(8)]:                       # cross-braces on the water face
        beam(m, (x1 + 0.3, -0.5, zz), (x1 + 0.3, DECK - 0.25, zz + 2.15), 0.12, 0.14, WOOD)
    for bz in (-4.5, 4.5):
        bx = x1 - 0.3
        m.add_cylinder((bx, DECK, bz - 1.6), 0.22, 0.42, "Soot brick", segments=7)                 # bollard
        m.add_cylinder((bx, DECK + 0.42, bz - 1.6), 0.3, 0.1, "Soot brick", segments=7)
        m.add_frustum((bx - 0.55, DECK, bz - 1.6), 0.32, 0.26, 0.12, "Reed and thatch", segments=7)  # a coil of line
        for dz in (-0.28, 0.28):                                          # iron ladder down the water face
            beam(m, (x1 + 0.27, DECK + 0.35, bz + 1.3 + dz), (x1 + 0.27, -1.4, bz + 1.3 + dz), 0.07, 0.07, "Slate roof")
        for k in range(6):
            yy = DECK - 0.05 - k * 0.36
            beam(m, (x1 + 0.27, yy, bz + 1.0), (x1 + 0.27, yy, bz + 1.6), 0.05, 0.05, "Slate roof")
    return m


def quay_crane():
    rng = random.Random(SEED + 1)
    m = glb.Mesh()
    # a stepped stone base: two courses of dressed blocks
    for k, (half, h) in enumerate(((1.2, 0.45), (0.95, 0.4))):
        y0 = k * 0.45
        n = 3
        for i in range(n):
            for j in range(n):
                cx = -half + (2 * half / n) * (i + 0.5); cz = -half + (2 * half / n) * (j + 0.5)
                s = 2 * half / n - 0.04
                m.add_box((cx, y0, cz), (s + rng.uniform(-.03, .03), h - 0.02, s + rng.uniform(-.03, .03)),
                          "Concrete" if (i + j + k) % 2 else "Wet mudflat", yaw=rng.uniform(-.03, .03))
    yb = 0.85
    m.add_box((0, yb, 0), (0.7, 0.2, 0.7), DARK)                                   # turntable
    beam(m, (0, yb + 0.2, 0), (0, yb + 3.4, 0), 0.42, 0.42, WOOD, up=(1, 0, 0))     # post
    for sx, sz in ((1, 0), (-1, 0), (0, 1), (0, -1)):                               # four raking struts
        beam(m, (sx * 0.85, yb + 0.2, sz * 0.85), (sx * 0.15, yb + 2.0, sz * 0.15), 0.18, 0.18, DARK)
    # the jib: two beams rising along +X, tied with blocks, a pulley at its tip
    tip = np.array((5.0, yb + 4.6, 0.0))
    root = np.array((-0.8, yb + 2.6, 0.0))
    for dz in (-0.18, 0.18):
        beam(m, root + (0, 0, dz), tip + (0, 0, dz), 0.16, 0.26, WOOD)
    for u in (0.25, 0.5, 0.75):
        p = root + (tip - root) * u
        m.add_box(tuple(p - (0, 0.16, 0)), (0.22, 0.32, 0.5), DARK)
    beam(m, (0, yb + 3.4, 0), tuple(root + (tip - root) * 0.45), 0.16, 0.16, DARK)   # stay from the post to the jib
    m.add_box((-0.9, yb + 2.2, 0), (0.6, 0.55, 0.62), "Concrete")                  # counterweight
    m.add_cylinder((tip[0], tip[1] - 0.22, -0.15), 0.24, 0.3, "Slate roof", segments=7)   # the sheave
    hook_y = 2.6
    tube(m, (tip[0], tip[1] - 0.1, 0), (tip[0], hook_y + 1.0, 0), 0.045, "Trail earth", n=5)
    beam(m, (tip[0], hook_y + 1.0, 0), (tip[0], hook_y + 0.75, 0), 0.12, 0.12, "Slate roof")  # hook block
    # the net of sacks: four sacks bundled, net cords over them, gathered at the hook
    srng = random.Random(SEED + 2)
    for k, (dx, dz) in enumerate(((-0.25, -0.22), (0.25, -0.2), (-0.2, 0.25), (0.25, 0.24))):
        lying_sack(m, srng, tip[0] + dx * 0.4, hook_y - 0.55 + (k // 2) * 0.05, dz, "Limewash", band="Packed earth", yaw=0.4 + k * 0.6)
    for a in range(5):
        ang = 2 * math.pi * a / 5
        p = (tip[0] + 0.62 * math.cos(ang), hook_y - 0.3, 0.62 * math.sin(ang))
        tube(m, p, (tip[0], hook_y + 0.75, 0), 0.03, "Mud", n=4)
    ring = [(tip[0] + 0.62 * math.cos(2 * math.pi * a / 10), hook_y - 0.3, 0.62 * math.sin(2 * math.pi * a / 10)) for a in range(10)]
    for a, b in zip(ring, ring[1:] + ring[:1]):
        tube(m, a, b, 0.03, "Mud", n=4)
    return m


def quay_treadwheel():
    m = glb.Mesh()
    R, W = 1.55, 1.1                                                     # wheel radius, width across (X)
    cy = R + 0.25
    n = 14
    for sx in (-W / 2, W / 2):                                           # two rims and their spokes
        pts = [(sx, cy + R * math.cos(2 * math.pi * k / n), R * math.sin(2 * math.pi * k / n)) for k in range(n)]
        for a, b in zip(pts, pts[1:] + pts[:1]):
            beam(m, a, b, 0.12, 0.16, WOOD, up=(1, 0, 0))
        for k in range(0, n, 2):
            beam(m, (sx, cy, 0), pts[k], 0.1, 0.1, DARK, up=(1, 0, 0))
    for k in range(n):                                                   # the treads across the rims
        a = 2 * math.pi * (k + 0.5) / n
        y, z = cy + (R - 0.05) * math.cos(a), (R - 0.05) * math.sin(a)
        beam(m, (-W / 2, y, z), (W / 2, y, z), 0.14, 0.06, PLANK)
    tube(m, (-W / 2 - 0.5, cy, 0), (W / 2 + 0.5, cy, 0), 0.1, "Slate roof", n=6)   # axle
    for sx in (-W / 2 - 0.4, W / 2 + 0.4):                               # A-frames
        beam(m, (sx, 0, -0.9), (sx, cy + 0.1, 0), 0.18, 0.18, DARK)
        beam(m, (sx, 0, 0.9), (sx, cy + 0.1, 0), 0.18, 0.18, DARK)
        beam(m, (sx, 0.6, -0.6), (sx, 0.6, 0.6), 0.12, 0.12, DARK)
    m.add_box((0, 0, 0), (W + 1.2, 0.12, 2.2), "Concrete")               # a stone footing
    return m


def stack_sacks(colour, band=None, seed=0):
    rng = random.Random(SEED + 10 + seed)
    m = glb.Mesh()
    pallet(m, 2.1, 1.3)
    y = 0.16
    for row, xs in enumerate(((-0.62, 0.0, 0.62), (-0.31, 0.31), (0.0,))):
        for x in xs:
            for z in ((-0.0,) if row == 2 else (-0.0,)):
                lying_sack(m, rng, x + rng.uniform(-.04, .04), y + row * 0.4, z + rng.uniform(-.05, .05), colour, band, yaw=math.pi / 2 + rng.uniform(-.12, .12))
    return m


def stack_wool():
    rng = random.Random(SEED + 20)
    m = glb.Mesh()
    pallet(m, 2.1, 1.3)
    for k, (x, y) in enumerate(((-0.52, 0.16), (0.52, 0.16), (0.0, 0.78))):
        out = [(0.5 * (1 + 0.14 * rng.uniform(-1, 1)) * math.cos(2 * math.pi * i / 9) * 1.1,
                0.5 * (1 + 0.14 * rng.uniform(-1, 1)) * math.sin(2 * math.pi * i / 9) * 1.15) for i in range(9)]
        lo = [(x + a, y, b) for a, b in out]
        mid = [(x + a * 1.08, y + 0.32, b * 1.08) for a, b in out]
        hi = [(x + a * 0.8, y + 0.6, b * 0.8) for a, b in out]
        loft(m, lo, mid, "Limewash"); loft(m, mid, hi, "Snow"); cap(m, hi, "Snow")
        for dz in (-0.25, 0.25):                                          # cord ties
            beam(m, (x - 0.58, y + 0.33, dz), (x + 0.58, y + 0.33, dz), 0.06, 0.66, "Packed earth")
    return m


def stack_cloth():
    rng = random.Random(SEED + 21)
    m = glb.Mesh()
    pallet(m, 2.1, 1.3)
    cols = ["Brick", "Slate roof", "Cast-iron green", "Brick", "Slate roof"]
    k = 0
    for row, xs in enumerate(((-0.45, 0.0, 0.45), (-0.22, 0.22))):
        for x in xs:
            y = 0.16 + 0.2 + row * 0.36
            tube(m, (x, y, -0.55 + rng.uniform(-.04, .04)), (x, y, 0.55 + rng.uniform(-.04, .04)), 0.2, cols[k % 5], n=7, end_colour="Limewash")
            k += 1
    return m


def sign(good):
    """A lectern-like sign: two posts and a board leaning back 50 degrees, so it reads from the game's camera above.
    Board faces +Z (and up)."""
    m = glb.Mesh()
    lean = math.radians(50)
    bw, bh = 2.3, 1.3
    yb, zb = 1.05, 0.0                                                           # the board's lower edge
    up = np.array((0.0, math.sin(lean), -math.cos(lean)))                       # along the board, bottom to top
    nrm = np.array((0.0, math.cos(lean), math.sin(lean)))                       # the board's face
    base = np.array((0.0, yb, zb))
    def B(u, v, w=0.0):                                                          # u across, v up the board, w out of it
        return tuple(base + np.array((u, 0, 0)) + up * v + nrm * w)
    for sx in (-bw / 2 + 0.2, bw / 2 - 0.2):                                    # two posts
        beam(m, (sx, 0, 0.05), (sx, yb + 0.15, 0.0), 0.16, 0.16, DARK, up=(1, 0, 0))
        beam(m, (sx, 0, -0.9), B(sx, bh * 0.8, -0.1), 0.14, 0.14, DARK)
        m.add_box((sx, 0, -0.4), (0.4, 0.14, 1.4), "Concrete")
    # frame and board
    def quad(u0, u1, v0, v1, w, colour):
        m.add_poly([B(u0, v0, w), B(u1, v0, w), B(u1, v1, w), B(u0, v1, w)], colour, facing=tuple(nrm))
    beam(m, B(-bw / 2 - 0.08, -0.05, -0.02), B(bw / 2 + 0.08, -0.05, -0.02), 0.14, 0.12, DARK)
    beam(m, B(-bw / 2 - 0.08, bh + 0.05, -0.02), B(bw / 2 + 0.08, bh + 0.05, -0.02), 0.14, 0.12, DARK)
    for su in (-1, 1):
        beam(m, B(su * (bw / 2 + 0.05), -0.1, -0.02), B(su * (bw / 2 + 0.05), bh + 0.1, -0.02), 0.12, 0.12, DARK)
    quad(-bw / 2, bw / 2, 0, bh, 0.0, "Paper")
    m.add_poly([B(-bw / 2, 0, -0.06), B(-bw / 2, bh, -0.06), B(bw / 2, bh, -0.06), B(bw / 2, 0, -0.06)], WOOD, facing=tuple(-nrm))
    w0 = 0.04
    ix, iv = -0.62, 0.68
    if good == "grain":
        prof = [(-0.24, -0.42), (0.24, -0.42), (0.3, -0.15), (0.2, 0.08), (-0.2, 0.08), (-0.3, -0.15)]
        m.add_poly([B(ix + a, iv + b, w0) for a, b in prof], "Reed and thatch", facing=tuple(nrm))
        quad(ix - 0.22, ix + 0.22, iv + 0.0, iv + 0.08, w0 + 0.02, "Packed earth")
        for k, ang in enumerate((-0.5, 0.0, 0.5)):
            u0 = ix + 0.05 * (k - 1)
            tube(m, B(u0, iv + 0.08, w0), B(u0 + 0.4 * math.sin(ang), iv + 0.08 + 0.4 * math.cos(ang), w0), 0.035, "Reed and thatch", n=4)
            m.add_sphere(B(u0 + 0.46 * math.sin(ang), iv + 0.1 + 0.46 * math.cos(ang), w0), 0.09, "Reed and thatch", rings=2, segments=5)
        fill, colour = 0.6, "Cast-iron green"
    else:
        prof = [(-0.28, -0.42), (0.28, -0.42), (0.34, -0.1), (0.22, 0.18), (-0.22, 0.18), (-0.34, -0.1)]
        m.add_poly([B(ix + a, iv + b, w0) for a, b in prof], "Limewash", facing=tuple(nrm))
        quad(ix - 0.33, ix + 0.33, iv - 0.2, iv - 0.08, w0 + 0.02, "Packed earth")
        quad(ix - 0.1, ix + 0.1, iv + 0.18, iv + 0.3, w0 + 0.02, "Packed earth")
        m.add_poly([B(ix - 0.16, iv + 0.3, w0), B(ix + 0.16, iv + 0.3, w0), B(ix + 0.22, iv + 0.48, w0), B(ix - 0.22, iv + 0.48, w0)], "Limewash", facing=tuple(nrm))
        fill, colour = 0.3, "Reed and thatch"
    # the bar: dark frame, the loaded part in the time's colour, the rest pale, quarter ticks
    u0, u1, v0, v1 = -0.15, 0.98, 0.42, 0.88
    quad(u0 - 0.06, u1 + 0.06, v0 - 0.06, v1 + 0.06, 0.02, "Ink")
    uf = u0 + (u1 - u0) * fill
    quad(u0, uf, v0, v1, 0.05, colour)
    quad(uf, u1, v0, v1, 0.05, "Limewash")
    for k in range(1, 4):
        uk = u0 + (u1 - u0) * k / 4
        quad(uk - 0.015, uk + 0.015, v0, v1, 0.07, "Ink")
    return m


def sign_grain():
    return sign("grain")


def sign_flour():
    return sign("flour")


def mooring_post():
    m = glb.Mesh()
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.3
        x, z = 0.2 * math.cos(a), 0.2 * math.sin(a)
        m.add_box((x, -1.4, z), (0.3, 2.8 - 0.1 * k, 0.3), DARK, bottom=False, yaw=a)
        m.add_box((x, 1.4 - 0.1 * k, z), (0.34, 0.08, 0.34), "Paper", bottom="Paper", yaw=a)
    m.add_frustum((0, 0.75, 0), 0.5, 0.5, 0.22, "Reed and thatch", segments=7)        # lashing
    m.add_frustum((0, 0.2, 0), 0.47, 0.47, 0.14, "Reed and thatch", segments=7)
    return m


def gangplank():
    m = glb.Mesh()
    beam(m, (0, 0, 0), (2.2, 0, 0), 0.62, 0.08, PLANK, up=(0, 1, 0))
    for k in range(6):
        x = 0.25 + k * 0.34
        beam(m, (x, 0.05, -0.27), (x, 0.05, 0.27), 0.06, 0.05, DARK)
    return m


def line():
    m = glb.Mesh()
    pts = [(0.0, 0.0, 0.0), (0.25, -0.09, 0.0), (0.5, -0.12, 0.0), (0.75, -0.09, 0.0), (1.0, 0.0, 0.0)]
    for a, b in zip(pts, pts[1:]):
        tube(m, a, b, 0.05, "Reed and thatch", n=5)
    return m


def wake():
    rng = random.Random(SEED + 40)
    m = glb.Mesh()
    for side in (-1, 1):                                                    # the V's two arms of broken water
        for k in range(7):
            u = 0.3 + k * 1.0
            x = side * (0.75 + 0.42 * u)
            z = u
            L = 0.75 - 0.06 * k
            w = 0.22 - 0.02 * k
            a = math.atan2(0.42 * side, 1.0)
            c, s = math.cos(a), math.sin(a)
            Y = 0.1
            pts = [(x - L / 2 * s, Y, z - L / 2 * c), (x + w * c * 1.5, Y, z), (x + L / 2 * s, Y, z + L / 2 * c), (x - w * c, Y, z)]
            m.add_poly(pts, "Limewash", facing=(0, 1, 0))
    for k in range(5):                                                      # the furrow behind the stern
        z = 2.5 + k * 1.1
        m.add_poly([(-0.2, 0.1, z), (0.2, 0.1, z), (0.14, 0.1, z + 0.6), (-0.14, 0.1, z + 0.6)], "Limewash", facing=(0, 1, 0))
    return m


ITEMS = [
    ("quay-deck", quay_deck, 2600, None, "Mock-up n6: the quay on piles, two berths, bollards, ladders"),
    ("quay-crane", quay_crane, 2400, None, "Mock-up n6: the treadwheel crane on its stone base, a net of sacks on its hook"),
    ("quay-treadwheel", quay_treadwheel, 1200, None, "Mock-up n6: the crane's treadwheel"),
    ("quay-stack-grain", lambda: stack_sacks("Reed and thatch", seed=0), 700, None, "Mock-up n6: the yard's grain stack"),
    ("quay-stack-flour", lambda: stack_sacks("Limewash", band="Packed earth", seed=1), 700, None, "Mock-up n6: the yard's flour stack"),
    ("quay-stack-wool", stack_wool, 600, None, "Mock-up n6: the yard's wool bales"),
    ("quay-stack-cloth", stack_cloth, 500, None, "Mock-up n6: the yard's bolts of cloth"),
    ("quay-sign-grain", sign_grain, 500, None, "Mock-up n6: a berth's sign: grain wanted, 60 % aboard, time plenty"),
    ("quay-sign-flour", sign_flour, 500, None, "Mock-up n6: a berth's sign: flour wanted, 30 % aboard, time some"),
    ("quay-mooring-post", mooring_post, 200, None, "Mock-up n6: a mooring post for a waiting boat"),
    ("quay-gangplank", gangplank, 150, None, "Mock-up n6: a boat's gangplank"),
    ("quay-line", line, 120, None, "Mock-up n6: a mooring line (stretched along X)"),
    ("quay-wake", wake, 200, None, "Mock-up n6: a moving boat's V wake"),
]

want = set(sys.argv[1:])
for name, make, budget, ink, used in ITEMS:
    if want and name not in want:
        continue
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=budget, ink=ink)))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, seed=SEED, verified=False)

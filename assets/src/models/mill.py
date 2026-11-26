# python3 assets/src/models/mill.py  ->  assets/models/mill.glb
# Night 3 (style sheet section 4): the mill as a smock (tower) mill, the tallest thing of the first age.
# Replaces the post mill of town.py, whose 9 m sails hung from a low buck and read from the game's tilt as a
# cross lying on the ground.
#
#   mill   an eight-sided timber tower on a stone plinth, 4.6 m across at the foot, tapering to 3.0 m at the curb
#          (7.0 m), weatherboard in dark timber (Mud) with three paler courses, a door and two windows on the front
#          (+Z), a window and a door at the back, a reefing stage (gallery) at 3.0 m. A boat-shaped cap in Soot
#          brick with a ridge board rises to 9.6 m; a tail pole runs from the cap down the back to a stone.
#          Nodes: "Body" (static) and "Sails" (node origin = the hub at (0, HUB_Y, HUB_Z); four upright sails in
#          the node's local XY plane, 7.0 m across tip to tip; the game turns Sails.rotation.z). The lowest tip is
#          HUB_Y - 3.5 = 4.1 m above the ground, so the sails clear the stage and villagers. Footprint 7.0 x 6.6 m,
#          so TownBuildings' fit to the 8 x 8 m plot keeps it at about life size (scale about 1.0).
# Deterministic (no randomness). Free asset sites are blocked from the build container, so it is made here in code.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math

from common import glb, ledger, asset_path
from _kit import beam, tube, loft, cap

WOOD = "Mud"
BAND = "Packed earth"
DOOR = "Soot brick"
CLOTH = "Limewash"

R0, R1 = 2.3, 1.5          # tower radius (to the corners) at the plinth and at the curb
Y0, Y1 = 0.6, 7.0          # plinth top, curb
HUB_Y, HUB_Z = 7.7, 2.6       # review r1: the sails stand about 0.6 m clear of the cap's nose
SAIL_R = 3.5


def ring(r, y, n=8, phase=math.pi / 8):
    return [(r * math.cos(phase + 2 * math.pi * i / n), y, r * math.sin(phase + 2 * math.pi * i / n)) for i in range(n)]


def r_at(y):
    return R0 + (R1 - R0) * (y - Y0) / (Y1 - Y0)


def on_face(m, y, h, w, colour, k=2, proud=0.06, depth=0.08):
    """A flat panel (door, window) on face k of the tower (face 2 looks along +Z), standing `proud` out."""
    a = math.pi / 8 + 2 * math.pi * k / 8 + math.pi / 8          # the face's mid angle
    nx, nz = math.cos(a), math.sin(a)
    rc = r_at(y + h / 2) * math.cos(math.pi / 8) + proud
    cx, cz = nx * rc, nz * rc
    tx, tz = -nz, nx
    lean = (R0 - R1) / (Y1 - Y0) * math.cos(math.pi / 8)        # the face leans in as it rises
    p = [(cx - tx * w / 2 + nx * lean * h / 2, y, cz - tz * w / 2 + nz * lean * h / 2),
         (cx + tx * w / 2 + nx * lean * h / 2, y, cz + tz * w / 2 + nz * lean * h / 2),
         (cx + tx * w / 2 - nx * lean * h / 2, y + h, cz + tz * w / 2 - nz * lean * h / 2),
         (cx - tx * w / 2 - nx * lean * h / 2, y + h, cz - tz * w / 2 - nz * lean * h / 2)]
    back = [(x - nx * depth, yy, z - nz * depth) for x, yy, z in p]
    loft(m, back, p, colour)
    cap(m, p, colour, facing=(nx, lean, nz))


def mill_body():
    m = glb.Mesh()
    # stone plinth, eight-sided, and a trodden apron of earth round it
    m.add_frustum((0, 0, 0), R0 + 0.45, R0 + 0.3, Y0, "Concrete", segments=8, cap_bottom=False)
    m.add_cylinder((0, 0, 0), R0 + 0.95, 0.04, "Trail earth", segments=10, cap_bottom=False)
    # the tower in three tiers, each with a paler course at its foot standing proud
    ys = [Y0, 2.6, 4.8, Y1]
    for i in range(3):
        a, b = ys[i], ys[i + 1]
        loft(m, ring(r_at(a), a), ring(r_at(b), b), WOOD, centre=(0, (a + b) / 2, 0))
        m.add_frustum((0, a, 0), r_at(a) + 0.08, r_at(a + 0.22) + 0.08, 0.22, BAND, segments=8, cap_bottom=True)
    # corner posts standing proud on the four front/back corners, so the taper reads in ink
    for k in range(8):
        ang = math.pi / 8 + 2 * math.pi * k / 8
        p0 = (math.cos(ang) * (R0 + 0.05), Y0, math.sin(ang) * (R0 + 0.05))
        p1 = (math.cos(ang) * (R1 + 0.05), Y1, math.sin(ang) * (R1 + 0.05))
        if k % 2 == 0:
            beam(m, p0, p1, 0.2, 0.2, BAND)
    # curb ring under the cap
    m.add_frustum((0, Y1, 0), R1 + 0.25, R1 + 0.25, 0.25, BAND, segments=8, cap_bottom=True)
    # front: door, two windows one above the other; back: a window and the back door
    on_face(m, Y0, 2.0, 1.2, "Cast-iron green", k=2, proud=0.1)                # review r1: a door that reads
    on_face(m, Y0 + 1.95, 0.2, 1.45, BAND, k=2, proud=0.12)                 # lintel
    on_face(m, 3.6, 0.7, 0.6, DOOR, k=2)
    on_face(m, 5.4, 0.6, 0.5, DOOR, k=2)
    on_face(m, 4.0, 0.6, 0.5, DOOR, k=6)
    on_face(m, Y0, 1.8, 0.9, DOOR, k=6)
    on_face(m, 2.9, 0.6, 0.5, DOOR, k=0)
    on_face(m, 5.0, 0.6, 0.5, DOOR, k=4)
    # reefing stage: a ring of boards on brackets at 3.0 m with a rail post at each corner
    sy = 3.0
    m.add_frustum((0, sy, 0), r_at(sy) + 0.85, r_at(sy) + 0.85, 0.16, "Trail earth", segments=8, cap_bottom=True)
    for k in range(8):
        ang = math.pi / 8 + 2 * math.pi * k / 8
        rr = r_at(sy) + 0.78
        x, z = math.cos(ang) * rr, math.sin(ang) * rr
        beam(m, (x, sy + 0.16, z), (x, sy + 1.05, z), 0.14, 0.14, WOOD)
        beam(m, (math.cos(ang) * (r_at(sy - 0.7)), sy - 0.7, math.sin(ang) * r_at(sy - 0.7)),
             (x * 0.92, sy, z * 0.92), 0.14, 0.14, WOOD)                              # bracket
    pts = ring(r_at(sy) + 0.78, sy + 1.0)
    for k in range(8):
        beam(m, pts[k], pts[(k + 1) % 8], 0.12, 0.1, WOOD)                               # hand rail
    # the cap (review r1: it read as a barn gable): a rounded, boat-shaped hood in Brick over a Soot brick skirt,
    # a half-round section that narrows to the nose and the tail, so from above it is an oval, with a Paper ridge band
    cy = Y1 + 0.25
    m.add_box((0, cy, -0.1), (2.9, 0.4, 3.2), "Soot brick", bottom="Soot brick")
    secs = [(-2.0, 0.95, 0.8), (-1.2, 1.6, 1.45), (0.9, 1.6, 1.45), (1.85, 1.0, 0.85)]   # z, half width, height
    n = 6
    def half(z, w, h):
        return [(w * math.cos(math.pi * i / n), cy + 0.4 + h * math.sin(math.pi * i / n), z) for i in range(n + 1)]
    rings = [half(*sec) for sec in secs]
    for a, b in zip(rings, rings[1:]):
        for i in range(n):
            q = [a[i], a[i + 1], b[i + 1], b[i]]
            mid = (0, cy + 0.4, (a[0][2] + b[0][2]) / 2)
            m._face([glb._v(p) for p in q], "Brick", glb._v(tuple(sum(c) / 4 for c in zip(*q))) - glb._v(mid))
    for rg, f in ((rings[0], -1), (rings[-1], 1)):
        cap(m, rg, "Soot brick", facing=(0, 0, f))                                   # tail and nose boards
    beam(m, (0, cy + 0.4 + 1.47, -1.3), (0, cy + 0.4 + 1.47, 1.0), 0.5, 0.08, "Paper")   # ridge band
    # windshaft out of the cap's front to the hub
    tube(m, (0, HUB_Y, 1.6), (0, HUB_Y, HUB_Z - 0.2), 0.24, WOOD, n=6)
    # tail pole from the cap's back down to its stone, with a cart wheel for turning the cap to the wind
    beam(m, (0, cy + 0.3, -1.6), (0, 0.5, -3.3), 0.26, 0.26, WOOD)
    m.add_box((0, 0, -3.3), (0.6, 0.4, 0.6), "Concrete")
    tube(m, (-0.2, 0.75, -3.05), (0.2, 0.75, -3.05), 0.4, BAND, n=8)
    # a sack of flour by the door, so the front has a story close up
    m.add_frustum((1.05, 0, 2.55), 0.32, 0.24, 0.6, "Paper", segments=6, cap_bottom=False)
    m.add_frustum((1.05, 0.6, 2.55), 0.1, 0.14, 0.12, BAND, segments=6)
    return m


def mill_sails():
    """Four sails in the local XY plane round the hub at the origin; front of the sails is +Z."""
    m = glb.Mesh()
    tube(m, (0, 0, -0.25), (0, 0, 0.35), 0.36, WOOD, n=6)                               # hub (poll end)
    up = (0, 0, 1)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        d = (math.cos(a), math.sin(a))
        n = (-d[1], d[0])                                                              # the sail's cloth side
        z = 0.2
        beam(m, (d[0] * 0.25, d[1] * 0.25, z), (d[0] * SAIL_R, d[1] * SAIL_R, z), 0.24, 0.2, WOOD, up=up)   # whip
        c0, c1, wdt = 0.75, SAIL_R - 0.1, 1.4      # review r1: 1.4 m wide
        off = 0.12 + wdt / 2
        # the cloth in two panels; the outer one on sail 2 is a patched Paper panel (close-up charm)
        mid = c0 + (c1 - c0) * 0.55
        for (r0, r1), col in (((c0, mid), CLOTH), ((mid + 0.05, c1), "Paper" if k == 2 else CLOTH)):
            p0 = (d[0] * r0 + n[0] * off, d[1] * r0 + n[1] * off, z)
            p1 = (d[0] * r1 + n[0] * off, d[1] * r1 + n[1] * off, z)
            beam(m, p0, p1, wdt, 0.07, col, up=up)
        e0 = (d[0] * c0 + n[0] * (off + wdt / 2), d[1] * c0 + n[1] * (off + wdt / 2), z + 0.05)
        e1 = (d[0] * c1 + n[0] * (off + wdt / 2), d[1] * c1 + n[1] * (off + wdt / 2), z + 0.05)
        beam(m, e0, e1, 0.13, 0.1, WOOD, up=up)                                        # hemlath
        for t in (0.0, 0.36, 0.7, 1.0):
            r = c0 + (c1 - c0) * t
            beam(m, (d[0] * r, d[1] * r, z + 0.06),
                 (d[0] * r + n[0] * (off + wdt / 2), d[1] * r + n[1] * (off + wdt / 2), z + 0.06),
                 0.11, 0.09, WOOD, up=up)                                              # sail bars
    return m


if __name__ == "__main__":
    body, sails = mill_body(), mill_sails()
    print(f"  mill/Body: {body.tri_count()} tris, mill/Sails: {sails.tri_count()} tris")
    out = asset_path("models/mill.glb")
    print(glb.report(out, glb.write_glb(out, [("Body", body), ("Sails", sails, None, (0.0, HUB_Y, HUB_Z))],
                                         max_tris=1500)))
    ledger.add("models", out, kind="model", generator=__file__,
               note="free asset sites blocked from the build container (checked 2026-11-25); night 3 rework as a smock mill",
               used_in="Hamlet: mill (smock mill; nodes Body and Sails, Sails spins about its local Z at the hub)",
               verified=True)

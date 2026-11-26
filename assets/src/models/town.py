# python3 assets/src/models/town.py  ->  assets/models/bell-post.glb, plot-stakes.glb, charter-stone.glb
#   (the mill: now mill.py; villager-cargo-water and -flour: now villager.py)
# First-age pieces for the incremental town (docs/plans/incremental-design.md), in the Hamlet style of cottage.py,
# well.py and market-stall.py: chunky boxes and prisms, one palette colour per face, no modelled outlines.
# Free sources were not used: kenney.nl and the other free asset sites are blocked from the build container
# (checked 2026-11-25), so these are built here as code. Deterministic (stones use a fixed seed).
#
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

# ------------------------------------------------------------------ bell post
HANG_Y = 2.72


def bell_frame():
    m = glb.Mesh()
    # night 3: the bell stands on a trodden patch with flags under the frame, so the civic place is found at once
    # (inside the frame's 3.3 m width, so TownBuildings' fit to the square plot keeps the frame's size)
    m.add_cylinder((0, 0, 0), 1.65, 0.03, "Trail earth", segments=10, cap_bottom=False)
    # review r1: one low flagged slab (the two wide footings read as crates), the footings only a sill's width
    m.add_box((0, 0.03, 0), (2.4, 0.05, 1.8), "Concrete")
    for x in (-1.15, 1.15):
        m.add_box((x, 0, 0), (0.5, 0.14, 1.7), "Concrete")                              # stone footing
        m.add_box((x, 0.14, 0), (0.34, 0.2, 1.6), WOOD)                                 # sill
        m.add_box((x, 0.34, 0), (0.3, 2.68, 0.3), WOOD)                                 # post
        for sz in (-1, 1):
            beam(m, (x, 0.42, sz * 0.72), (x, 1.55, sz * 0.1), 0.18, 0.18, WOOD)        # braces
    m.add_box((0, 3.0, 0), (2.8, 0.3, 0.36), "Packed earth", bottom="Packed earth")     # crossbeam
    for sx in (-1, 1):
        beam(m, (sx * 1.15, 2.4, 0), (sx * 0.6, 3.0, 0), 0.18, 0.18, WOOD)              # knee braces
    m.add_box((0, HANG_Y + 0.08, 0), (0.5, 0.2, 0.4), WOOD, bottom=WOOD)                # hanger block
    # a narrow slate cap over the crossbeam only, so the bell still shows from straight above (review r1: slate,
    # so the bell post and the well's thatch no longer read as one family)
    m.add_gable((0, 3.3, 0), 3.2, 0.5, 0.4, "Slate roof", gable=WOOD, overhang=0.06, bottom=WOOD)
    beam(m, (-1.62, 3.68, 0), (1.62, 3.66, 0), 0.2, 0.1, "Soot brick")                  # ridge tiles
    for sz in (-1, 1):
        beam(m, (-1.62, 3.47, sz * 0.2), (1.62, 3.45, sz * 0.2), 0.07, 0.05, "Wet mudflat", up=(0, 1, -sz))   # course
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
    """Night 3 (style sheet: civic = stone and one bright mark on a small paved patch): a standing stone 3.4 m tall
    (a villager is 1.9 m), leaning a touch, with the charter's seal (a raised Brick disc with a Paper ribbon) on its
    front (+Z), on a paved patch of flat stones inside a ring of trodden earth, 3.6 m across in all."""
    rng = random.Random(7)
    m = glb.Mesh()
    m.add_cylinder((0, 0, 0), 1.8, 0.03, "Trail earth", segments=12, cap_bottom=False)  # trodden ring
    m.add_cylinder((0, 0.03, 0), 1.45, 0.06, "Wet mudflat", segments=10, cap_bottom=False)   # joints between pavers
    for a in range(6):                                                                  # paving stones
        ang = 2 * math.pi * (a + 0.3) / 6
        stone(m, rng, (math.cos(ang) * 0.95, 0.06, math.sin(ang) * 0.95), 0.5, 0.06, n=5, top="Concrete",
              side="Concrete")
    stone(m, rng, (0, 0.06, 0), 0.75, 0.08, n=6, top="Concrete", side="Wet mudflat")      # footing
    # the standing stone: a slab with flat front and back, rough outline in X-Y, leaning back 3 degrees
    # review r1: 3.4 m tall (was 2.7), so it stands clearly above the bell post's frame (about 3 m)
    outline = [(x, y * 1.25) for x, y in ((-0.5, 0.08), (0.52, 0.08), (0.6, 1.2), (0.5, 2.1), (0.22, 2.65),
                                          (-0.2, 2.72), (-0.5, 2.35), (-0.62, 1.2))]
    lean = 0.05
    zf, zb = 0.3, -0.3
    front = [(x, y, zf - lean * y) for x, y in outline]
    back = [(x * 0.92, y, zb - lean * y) for x, y in outline]
    loft(m, back, front, "Concrete", centre=(0, 1.65, -0.07))
    cap(m, front, "Concrete", facing=(0, 0, 1))
    cap(m, back, "Concrete", facing=(0, 0, -1))
    # the seal: a raised Brick disc with a ring and a Paper ribbon hanging below it
    cy, R = 2.15, 0.38
    zc = zf - lean * cy + 0.02
    n = 10
    ring_f = [(R * math.cos(2 * math.pi * i / n), cy + R * math.sin(2 * math.pi * i / n), zc + 0.12) for i in range(n)]
    ring_b = [(x, y, zc - 0.02) for x, y, _ in ring_f]
    loft(m, ring_b, ring_f, "Soot brick", centre=(0, cy, zc))
    cap(m, ring_f, "Brick", facing=(0, 0, 1))
    m.add_box((0, cy - 0.06, zc + 0.12), (0.26, 0.12, 0.05), "Soot brick", bottom="Soot brick")   # the press mark
    for sx in (-1, 1):
        m.add_poly([(sx * 0.06, cy - R + 0.04, zc + 0.08), (sx * 0.22, cy - R + 0.04, zc + 0.08),
                    (sx * 0.3, cy - R - 0.5, zc + 0.12), (sx * 0.12, cy - R - 0.42, zc + 0.12)], "Paper",
                   facing=(0, 0, 1))                                                    # ribbon tails
    # carved lines under the seal and a lichen patch on the back, so no side is blank
    for y, w in ((1.35, 0.5), (1.15, 0.36)):
        m.add_box((0, y, zf - lean * y + 0.01), (w, 0.07, 0.05), "Wet mudflat", bottom="Wet mudflat")
    m.add_poly([(-0.3, 1.5, zb - lean * 1.5 - 0.01), (0.05, 1.45, zb - lean * 1.45 - 0.01),
                (0.12, 1.85, zb - lean * 1.85 - 0.01), (-0.2, 1.95, zb - lean * 1.95 - 0.01)],
               "Cast-iron green", facing=(0, 0, -1))
    return m


BUILDS = [
    # the mill moved to mill.py on night 3 (a smock mill)
    ("bell-post", [("Frame", bell_frame()), ("Bell", bell(), None, (0.0, HANG_Y, 0.0))], 500,
     "Town green: bell post (nodes Frame and Bell, Bell swings about its local X at the hanger)"),
    ("plot-stakes", plot_parts(), 300, "Town: plot marker for the next building (stake and cord nodes)"),
    ("charter-stone", charter_stone(), 300, "Town green: charter stone on its paved patch, the charter's seal in Brick (after the charter is granted)"),
    # villager-cargo-water and -flour moved to villager.py (night 3: loads carried in the arms, in the body's frame)
]

if __name__ == "__main__":
    for name, parts, budget, used in BUILDS:
        if not isinstance(parts, glb.Mesh):
            for p in parts:
                print(f"  {name}/{p[0]}: {p[1].tri_count()} tris")
        out = asset_path(f"models/{name}.glb")
        print(glb.report(out, glb.write_glb(out, parts, max_tris=budget)))
        ledger.add("models", out, kind="model", generator=__file__, note=BLOCKED, used_in=used, verified=True)

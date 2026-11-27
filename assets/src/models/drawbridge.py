# python3 assets/src/models/drawbridge.py  ->  assets/models/drawbridge-lift.glb, drawbridge-span.glb,
#                                              drawbridge-end.glb
# River set: the drawbridge (Art tab: "the white timber double-leaf drawbridge with an overhead balance beam is a
# Dutch signature"; "Deck down: walkers flow, signal arm horizontal / Deck raised with a long shadow, signal arm
# vertical, walkers bunch at the barriers"; Gameplay tab: closed, raising 2 s, open, lowering 2 s; class 1 closed,
# all boats open). One leaf (the prototype's bridge), white limewashed timber, built like the footbridge from pieces
# along Z that the game lines up across the river, but 4 m wide (two cells) and heavier, deck top 0.9 m above the
# water (y = 0 is the bank / water surface), so masts cannot pass while it is down.
#
#   drawbridge-lift  8 m along Z x 4 m (footprint; the gantry posts and signal stick out to 7.2 m across):
#                    a 2 m timber pier on the near bank with the gantry, z = +2..+4, and the 6 m lifting leaf that
#                    spans a 3-cell (6 m) river, z = -4..+2. Raised to 80 degrees the leaf (with its joists) leaves a 4.4 m channel.
#     nodes  frame    static: pier, its deck and rails, the gantry (two pairs of white posts with a lintel on top).
#            leaf     the lifting deck. Node origin = the hinge, at (0, 0.9, 2.0); mesh in local coordinates, deck
#                     top at local y = 0, running from local z = 0 to z = -6. Raise it by rotating the node about
#                     its local X axis by +angle (Godot: rotation.x = +a; the tip goes up). Fully open: 80 degrees.
#                     The underside is dark (Mud) with cross joists, so a raised leaf reads from above as a dark
#                     bar at the pier with open water behind it.
#            beam     the white balance beam frame: two beams with a counterweight at the back. Node origin = the
#                     pivot, at (0, 5.9, 2.0), straight above the hinge; front arms reach local z = -6 (above the
#                     leaf tip), back arms z = +4 with the counterweight. Rotate it by the SAME angle as the leaf.
#            chains   two hanging chains, 5 m, from the beam tips down to the leaf tip. Node origin = the top of the
#                     chains, at (0, 5.9, -4.0). Do not rotate it; move it with the beam tip:
#                     chains.position = (0, 5.9 + 6 sin a, 2.0 - 6 cos a). (Beam, leaf and chains are a
#                     parallelogram, so the chains stay vertical and the bottom lands on the leaf tip.)
#            signal   the signal arm, banded Brick and Limewash, on a post on the +X side of the pier. Node origin =
#                     the arm's pivot at (2.75, 4.0, 3.4); the arm points along local +X (horizontal = walkers' turn).
#                     Rotate about local Z by +90 degrees for vertical (boats' turn).
#   drawbridge-span  a fixed 2 m module (z = -1..+1), 4 m wide, deck 0.9, piles and white rails: repeat between the
#                    leaf tip and the far bank for rivers wider than 6 m (boats still pass only under the leaf).
#   drawbridge-end   2 x 4 m bank ramp, ground at +Z up to the deck at -Z, with a sill the leaf tip rests on, white
#                    rails and a barrier pole. Turn the far one 180 degrees.
#     nodes  ramp     static.
#            barrier  the pole that stops walkers while the bridge is open. Origin = its pivot at (-2.2, 1.35, 0.75), on a post outside the rail;
#                     the 4.6 m pole lies across the ramp along local +X, above the rails, its tip on a rest
#                     post at x = +2.25 (closed to walkers). Rotate about local Z by +80
#                     degrees to lift it (open to walkers, the default while the leaf is down).
# A row for a 6 m river (3 cells) whose banks are at z = -4 and z = +2: lift at 0, end at z = +5, far end (turned)
# at z = -5. The pier stands on the near bank.
# Round n3 (track RV, 2026-11-26): dark timber instead of white so it stands out from the grass: Soot brick rails
# and balance beam, Packed earth planks, Paper caps on the rail posts; the thin white gantry posts are now two stout
# Soot brick towers 1.1 x 1.1 m with Slate pyramid caps (a gatehouse) carrying the beam's pivot; a Brick
# counterweight; heavier chains. The barrier and the signal keep their Brick and Limewash bands. The game now
# uses the parts (RiverLayer.Drawbridge.cs): frame fixed, leaf and beam turning, chains following the beam tip.
# After the review: the towers 0.9 m square topping out at 5 m (the beam pivot 3.6 m above the deck), and the leaf one
# Packed earth slab on both faces with Packed earth rails, so the raised leaf reads apart from the Soot brick frame.
import sys, pathlib, math; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam, tube
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

DECK, PLANK_T = 0.9, 0.16
HZ = 2.0                 # hinge z (pier from HZ to HZ + 2, leaf from HZ - 6 to HZ)
LEAF = 6.0               # the leaf spans a whole 3-cell river, so the open channel is about 4.4 m wide
H = 3.6                  # beam pivot height above the deck (after the review: towers top out at 5 m)
BACK = 4.0               # back arm of the balance beam (counterweight end)
OPEN_DEG = 80
HALF_W = 2.0             # deck half width
RAIL_X, RAIL_H = 1.82, 1.1
POST_X = 2.35
CHAIN_X = 1.95
WHITE, PLANK, WOOD, DARK, IRON = "Soot brick", "Packed earth", "Packed earth", "Mud", "Ink"
BAND = "Limewash"


def rails(m, z0, z1, y0, y1, posts=None, colour=WHITE, x=RAIL_X):
    """White rails on both sides from z0 to z1 (deck heights y0, y1): posts, hand rail, knee rail."""
    posts = posts or [z0, z1]
    for sx in (-1, 1):
        xx = sx * x
        for z in posts:
            y = y0 + (y1 - y0) * (z - z0) / (z1 - z0)
            m.add_box((xx, y - 0.05, z), (0.24, RAIL_H + 0.05, 0.24), colour, bottom=False)
            m.add_box((xx, y + RAIL_H, z), (0.3, 0.1, 0.3), "Paper", bottom="Paper")
        beam(m, (xx, y0 + RAIL_H - 0.1, z0), (xx, y1 + RAIL_H - 0.1, z1), 0.2, 0.2, colour)
        beam(m, (xx, y0 + 0.55, z0), (xx, y1 + 0.55, z1), 0.14, 0.14, colour)


def deck(m, z0, z1, y=DECK, under=WOOD):
    """Planked deck between z0 and z1 (top at y), stringers under it."""
    n = max(1, round(abs(z1 - z0) / 0.5))
    for i in range(n):
        za = z0 + (z1 - z0) * (i + 0.04) / n
        zb = z0 + (z1 - z0) * (i + 0.96) / n
        m.add_box((0, y - PLANK_T, (za + zb) / 2), (2 * HALF_W, PLANK_T, abs(zb - za)), PLANK, bottom=under)
    for x in (-1.4, 0.0, 1.4):
        beam(m, (x, y - PLANK_T - 0.13, z0), (x, y - PLANK_T - 0.13, z1), 0.24, 0.26, DARK)


def frame():
    m = glb.Mesh()
    z0, z1 = HZ, HZ + 2.0
    deck(m, z0, z1)
    # pier: a heavy timber trestle down into the water, cross-braced, with a cap beam under the hinge
    for x in (-1.6, -0.55, 0.55, 1.6):
        for z in (z0 + 0.3, z1 - 0.3):
            m.add_box((x, -0.8, z), (0.3, DECK - PLANK_T - 0.26 + 0.8, 0.3), DARK, bottom=False)
    beam(m, (-HALF_W - 0.1, DECK - PLANK_T - 0.2, z0 + 0.1), (HALF_W + 0.1, DECK - PLANK_T - 0.2, z0 + 0.1), 0.36, 0.34, DARK)
    for z in (z0 + 0.3, z1 - 0.3):
        beam(m, (-1.6, -0.2, z), (1.6, DECK - PLANK_T - 0.35, z), 0.14, 0.14, DARK)
    rails(m, z0 + 0.15, z1, DECK, DECK, posts=[z0 + 0.15, z1])
    # gantry: a pair of posts each side (front and back of the pivot), lintel on top, braces to the deck
    top = DECK + H + 0.4
    for sx in (-1, 1):
        x = sx * (POST_X + 0.45)
        m.add_box((x, -0.6, HZ + 0.25), (0.9, top + 0.6, 0.9), WHITE, top=WHITE, bottom=False)         # tower
        for yb in (1.5, 3.1):                                                                             # courses
            m.add_box((x, yb, HZ + 0.25), (0.98, 0.12, 0.98), DARK, bottom=DARK)
        m.add_box((x, top, HZ + 0.25), (1.1, 0.14, 1.1), "Concrete", bottom="Concrete")
        m.add_cone((x, top + 0.14, HZ + 0.25), 0.78, 0.7, "Slate roof", segments=4, yaw=math.pi / 4)  # cap
        m.add_box((sx * POST_X, DECK + H - 0.18, HZ + 0.25), (0.34, 0.36, 0.72), DARK, bottom=DARK)   # pivot block
    # signal post outboard on the +X side, on a small bracket platform
    m.add_box((2.75, DECK - PLANK_T - 0.3, HZ + 1.4), (0.8, 0.3 + PLANK_T, 0.9), DARK, bottom=DARK)
    m.add_box((2.75, DECK, HZ + 1.4), (0.24, 3.25, 0.24), WHITE, bottom=False)
    m.add_box((2.75, DECK + 3.25, HZ + 1.4), (0.3, 0.12, 0.3), DARK, bottom=DARK)
    return m


def leaf():
    """Local frame: origin at the hinge on the deck top; the leaf runs to z = -LEAF."""
    m = glb.Mesh()
    deck(m, 0.0, -LEAF, y=0.0, under=PLANK)   # after the review: one Packed earth slab both faces, so a raised leaf reads
    for z in (-0.6, -2.2, -3.8, -5.4):                                       # cross joists on the underside
        beam(m, (-1.6, -PLANK_T - 0.4, z), (1.6, -PLANK_T - 0.4, z), 0.2, 0.2, WOOD)
    beam(m, (-CHAIN_X - 0.12, -0.1, -LEAF + 0.12), (CHAIN_X + 0.12, -0.1, -LEAF + 0.12), 0.24, 0.24, PLANK)   # tip
    for sx in (-1, 1):
        m.add_box((sx * CHAIN_X, 0.0, -LEAF + 0.12), (0.2, 0.2, 0.2), IRON, bottom=False)       # chain eyes
    rails(m, -0.2, -LEAF + 0.15, 0.0, 0.0, posts=[-0.2, -2.1, -4.0, -LEAF + 0.15], colour=PLANK)
    return m


def balance_beam():
    """Local frame: origin at the pivot; arms along Z (front to -LEAF, back to +BACK)."""
    m = glb.Mesh()
    for sx in (-1, 1):
        x = sx * CHAIN_X
        beam(m, (x, 0, -LEAF - 0.1), (x, 0, BACK), 0.26, 0.36, WHITE)
        tube(m, (x - sx * 0.16, 0, 0), (x + sx * 0.5, 0, 0), 0.1, DARK, n=6)                     # pivot axle
    for z in (-LEAF + 0.05, -3.0, -1.0):                                     # cross ties
        beam(m, (-CHAIN_X, 0, z), (CHAIN_X, 0, z), 0.2, 0.22, WHITE)
    beam(m, (-CHAIN_X, 0, -LEAF + 0.1), (CHAIN_X, 0, -3.0), 0.14, 0.16, WHITE)                   # St Andrew's cross
    beam(m, (CHAIN_X, 0, -LEAF + 0.1), (-CHAIN_X, 0, -3.0), 0.14, 0.16, WHITE)
    m.add_box((0, -0.5, BACK - 0.5), (2 * CHAIN_X + 0.26, 1.0, 1.0), "Brick", bottom="Brick")   # counterweight
    m.add_box((0, 0.5, BACK - 0.5), (2 * CHAIN_X + 0.32, 0.12, 1.06), DARK, bottom=DARK)
    return m


def chains():
    m = glb.Mesh()
    for sx in (-1, 1):
        x = sx * CHAIN_X
        tube(m, (x, -0.15, -0.0), (x, -H + 0.05, 0.0), 0.08, IRON, n=4)
        for k in range(4):                                                   # a few links read as a chain
            m.add_box((x, -0.6 - k * (H - 0.9) / 3, 0), (0.2, 0.28, 0.2), IRON, bottom=IRON)
    return m


def signal_arm():
    """Local frame: origin at the pivot, the arm along +X; banded Brick and Limewash."""
    m = glb.Mesh()
    for i in range(4):
        x0 = 0.15 + i * 0.4
        c = "Brick" if i % 2 == 0 else BAND
        m.add_box((x0 + 0.2, -0.13, 0.24), (0.4, 0.26, 0.16), c, bottom=c)
    m.add_box((-0.25, -0.15, 0.24), (0.5, 0.3, 0.2), DARK, bottom=DARK)       # counter-arm
    tube(m, (0, 0, 0.0), (0, 0, 0.4), 0.1, DARK, n=6)                          # axle through the post
    return m


def span():
    m = glb.Mesh()
    deck(m, -1.0, 1.0)
    for x in (-1.5, 1.5):
        m.add_box((x, -0.8, 0), (0.3, DECK - PLANK_T - 0.26 + 0.8, 0.3), DARK, bottom=False)
    beam(m, (-1.75, DECK - PLANK_T - 0.4, 0), (1.75, DECK - PLANK_T - 0.4, 0), 0.3, 0.3, DARK)
    rails(m, -1.0, 1.0, DECK, DECK)
    return m


def ramp():
    m = glb.Mesh()
    yb = DECK - PLANK_T
    hw = HALF_W - 0.02
    for sx in (-1, 1):                                                       # earth wedge
        m.add_poly([(sx * hw, 0, 1.0), (sx * hw, 0, -1.0), (sx * hw, yb, -1.0)], "Packed earth", facing=(sx, 0, 0))
    m.add_poly([(-hw, 0, -1.0), (hw, 0, -1.0), (hw, yb, -1.0), (-hw, yb, -1.0)], "Packed earth", facing=(0, 0, -1))
    for i in range(4):                                                       # sloping planks
        za, zb = 1.0 - 0.5 * i - 0.03, 1.0 - 0.5 * (i + 1) + 0.03
        ya = 0.02 + (DECK - 0.02) * (1.0 - za) / 2.0
        yb2 = 0.02 + (DECK - 0.02) * (1.0 - zb) / 2.0
        beam(m, (0, ya - PLANK_T / 2, za), (0, yb2 - PLANK_T / 2, zb), 2 * HALF_W, PLANK_T, PLANK)
    beam(m, (-HALF_W, DECK - 0.1, -0.95), (HALF_W, DECK - 0.1, -0.95), 0.2, 0.2, DARK)          # sill for the leaf tip
    m.add_box((-0.0, -0.5, -0.95), (2 * HALF_W, 0.5 + DECK - 0.2, 0.2), DARK, bottom=False)     # abutment face
    rails(m, 1.0, -1.0, 0.0, DECK)
    # barrier post on the -X side, and a rest post on the +X side for the pole's tip
    m.add_box((-2.2, 0, 0.75), (0.3, 1.35, 0.3), WHITE)
    m.add_box((2.25, 0, 0.75), (0.2, 1.17, 0.2), WHITE)
    m.add_box((2.25, 1.17, 0.75), (0.3, 0.08, 0.3), DARK, bottom=DARK)
    return m


def barrier():
    m = glb.Mesh()
    for i in range(6):                                                       # banded pole along +X, 4.6 m
        x0 = 0.1 + i * 0.75
        c = "Brick" if i % 2 == 0 else BAND
        m.add_box((x0 + 0.375, -0.09, 0), (0.75, 0.18, 0.18), c, bottom=c)
    m.add_box((-0.35, -0.13, 0), (0.6, 0.26, 0.22), DARK, bottom=DARK)        # counterweight stub
    return m


BUILDS = [
    ("drawbridge-lift", [("frame", frame()), ("leaf", leaf(), None, (0.0, DECK, HZ)),
                         ("beam", balance_beam(), None, (0.0, DECK + H, HZ)),
                         ("chains", chains(), None, (0.0, DECK + H, HZ - LEAF)),
                         ("signal", signal_arm(), None, (2.75, DECK + 3.1, HZ + 1.4))], 1500,
     "River: drawbridge lifting section (nodes frame, leaf, beam, chains, signal; leaf and beam rotate about X)"),
    ("drawbridge-span", span(), 400, "River: drawbridge fixed 2 m span module (for rivers wider than 6 m)"),
    ("drawbridge-end", [("ramp", ramp()), ("barrier", barrier(), None, (-2.2, 1.35, 0.75))], 500,
     "River: drawbridge bank ramp with barrier pole (nodes ramp, barrier)"),
]
if __name__ == "__main__":
    for name, parts, budget, used in BUILDS:
        if not isinstance(parts, glb.Mesh):
            for p in parts:
                print(f"  {name}/{p[0]}: {p[1].tri_count()} tris")
        out = asset_path(f"models/{name}.glb")
        print(glb.report(out, glb.write_glb(out, parts, max_tris=budget)))
        ledger.add("models", out, kind="model", generator=__file__, note=BLOCKED, used_in=used, verified=True)

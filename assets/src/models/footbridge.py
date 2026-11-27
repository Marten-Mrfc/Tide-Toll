# python3 assets/src/models/footbridge.py  ->  assets/models/footbridge-span.glb, footbridge-end.glb
# Hamlet "low footbridge" (Art tab): always walkable, low enough that masted boats cannot pass. Built from 2 m
# modules so one bridge stretches over 3 to 5 cells of water: place footbridge-end on each bank cell (rotate the far
# one by 180 degrees) and repeat footbridge-span every 2 m between them.
#   footbridge-span  2 m (along Z) x 2 m (X) module: four deck planks with gaps, two stringers, a pair of piles in
#                    the middle going down into the water, rail posts at both module ends (coincident with the next
#                    module's, identical, so nothing flickers) and a hand rail whose top is 1.2 m above the deck,
#                    flush with the post tops, plus a knee rail at 0.6 m.
#   footbridge-end   2 x 2 m bank landing: a plank ramp rising from the ground at +Z (the bank side, the front) to
#                    the deck height at -Z where the first span starts, on an earth wedge, with sloping rails.
# y = 0 is the bank / water surface; the deck top is at 0.6 m, the piles reach 0.8 m below the water surface.
# Heights match the pipeline table ("1.2 rails"), which measures rails from the deck: rails and posts end 1.2 m
# above the deck, so 1.8 m above the water overall (the table's note says so since 2026-11-24).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam

# Round n3 (track RV, 2026-11-26): the span is now ONE arched bridge 10 m long (RiverLayer stretches it to the
# crossing), 2.6 m wide, its deck humped 0.55 m, golden planks (Reed and thatch and Trail earth alternating, so the
# deck is a different tone from the paths) with gaps, chunky Soot brick rails 0.2 m thick on posts with Paper caps,
# a Cast-iron green lantern on one post, piles in pairs under it. The ends are stone abutments with the plank ramp.
DECK, PLANK_T, RAIL_X = 0.6, 0.16, 1.15
RAIL_H = 1.2          # top of hand rail and posts above the deck (pipeline table)
WOOD, PLANK, RAIL = "Soot brick", "Trail earth", "Soot brick"
PLANK2 = "Reed and thatch"
import math
LEN, HUMP = 10.0, 0.55


def rails(m, z0, z1, y0, y1):
    """Rail posts at z0 and z1 on both sides (Paper caps), hand rail and knee rail between (y0/y1 = deck height)."""
    for x in (-RAIL_X, RAIL_X):
        for z, y in ((z0, y0), (z1, y1)):
            m.add_box((x, y - 0.05, z), (0.24, RAIL_H + 0.05, 0.24), WOOD, bottom=False)
            m.add_box((x, y + RAIL_H, z), (0.3, 0.1, 0.3), "Paper", bottom="Paper")
        beam(m, (x, y0 + RAIL_H - 0.1, z0), (x, y1 + RAIL_H - 0.1, z1), 0.2, 0.2, RAIL)
        beam(m, (x, y0 + 0.55, z0), (x, y1 + 0.55, z1), 0.14, 0.14, RAIL)


def deck_y(z):
    return DECK + HUMP * math.cos(math.pi * z / LEN)


def span():
    m = glb.Mesh()
    n = 18                                                     # planks across the deck, gaps between, two tones
    for i in range(n):
        z = -LEN / 2 + (i + 0.5) * LEN / n
        y = deck_y(z)
        w = 2.3 + (0.12 if i % 3 == 0 else 0.0)
        beam(m, (-w / 2, y - PLANK_T / 2, z), (w / 2, y - PLANK_T / 2, z), LEN / n - 0.07, PLANK_T,
             PLANK2 if i % 2 else PLANK)
    segs = 5
    for x in (-0.8, 0.8):                                      # stringers under the planks, following the hump
        for k in range(segs):
            za, zb = -LEN / 2 + LEN * k / segs, -LEN / 2 + LEN * (k + 1) / segs
            beam(m, (x, deck_y(za) - PLANK_T - 0.12, za), (x, deck_y(zb) - PLANK_T - 0.12, zb), 0.2, 0.22, "Mud")
    for z in (-2.5, 2.5):                                      # pairs of piles into the water
        for x in (-0.95, 0.95):
            m.add_box((x, -0.8, z), (0.26, deck_y(z) - PLANK_T - 0.2 + 0.8, 0.26), "Mud", bottom=False)
    posts = [-LEN / 2, -LEN / 6, LEN / 6, LEN / 2]
    for a, b in zip(posts, posts[1:]):
        rails(m, a, b, deck_y(a), deck_y(b))
    # a lantern on the first post (right side)
    x, z = RAIL_X, LEN / 6
    y = deck_y(z) + RAIL_H + 0.1
    m.add_box((x, y, z), (0.1, 0.5, 0.1), "Ink", bottom=False)
    m.add_box((x, y + 0.5, z), (0.32, 0.36, 0.32), "Cast-iron green", top="Cast-iron green", bottom="Cast-iron green")
    m.add_box((x, y + 0.56, z), (0.34, 0.2, 0.2), "Reed and thatch", bottom=False)
    m.add_cone((x, y + 0.86, z), 0.24, 0.22, "Ink", segments=4)
    return m


def end():
    m = glb.Mesh()
    # earth wedge under the ramp: ground level at z = +1, deck height at z = -1
    yb = DECK - PLANK_T
    for sx in (-1, 1):
        m.add_poly([(sx * 1.18, 0, 1.0), (sx * 1.18, 0, -1.0), (sx * 1.18, yb, -1.0)], "Packed earth", facing=(sx, 0, 0))
    m.add_poly([(-1.18, 0, -1.0), (1.18, 0, -1.0), (1.18, yb, -1.0), (-1.18, yb, -1.0)], "Packed earth", facing=(0, 0, -1))
    m.add_poly([(-1.18, 0, 1.0), (1.18, 0, 1.0), (1.18, yb, -1.0), (-1.18, yb, -1.0)], "Packed earth", facing=(0, 1, 0.3))
    # sloping planks on the wedge
    for i in range(4):
        za, zb = 1.0 - 0.5 * i - 0.03, 1.0 - 0.5 * (i + 1) + 0.03
        ya, yb2 = 0.02 + (DECK - 0.02) * (1.0 - za) / 2.0, 0.02 + (DECK - 0.02) * (1.0 - zb) / 2.0   # plank tops
        beam(m, (0, ya - PLANK_T / 2, za), (0, yb2 - PLANK_T / 2, zb), 2.36, PLANK_T, PLANK2 if i % 2 else PLANK)
    rails(m, 1.0, -1.0, 0.0, DECK)
    # After the review: the abutment is one stone wall as wide as the span (2.6 m) at the river end, with a Paper cap,
    # and slim stone cheeks along the ramp, so it belongs to the bank and the bridge.
    m.add_box((0, -0.3, -1.12), (2.7, DECK + 0.3, 0.36), "Concrete", top="Concrete", bottom=False)
    m.add_box((0, DECK - 0.02, -1.12), (2.8, 0.1, 0.44), "Paper", bottom="Paper")
    for sx in (-1, 1):                                         # stone cheeks either side of the ramp
        m.add_box((sx * 1.3, 0, -0.45), (0.24, DECK * 0.75, 1.1), "Concrete", top="Paper")
    return m


for name, make, used in (("footbridge-span", span, "River town: footbridge, the arched span (stretched to the crossing)"),
                         ("footbridge-end", end, "Hamlet: footbridge, bank landing (one per bank)")):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=900)))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, verified=True)

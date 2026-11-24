# python3 assets/src/models/footbridge.py  ->  assets/models/footbridge-span.glb, footbridge-end.glb
# Hamlet "low footbridge" (Art tab): always walkable, low enough that masted boats cannot pass. Built from 2 m
# modules so one bridge stretches over 3 to 5 cells of water: place footbridge-end on each bank cell (rotate the far
# one by 180 degrees) and repeat footbridge-span every 2 m between them.
#   footbridge-span  2 m (along Z) x 2 m (X) module: four deck planks with gaps, two stringers, a pair of piles in
#                    the middle going down into the water, rail posts at both module ends (coincident with the next
#                    module's, identical, so nothing flickers) and a hand rail at 1.2 m plus a knee rail.
#   footbridge-end   2 x 2 m bank landing: a plank ramp rising from the ground at +Z (the bank side, the front) to
#                    the deck height at -Z where the first span starts, on an earth wedge, with sloping rails.
# y = 0 is the bank / water surface; the deck top is at 0.6 m, the piles reach 0.8 m below the water surface.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam

DECK, PLANK_T, RAIL_X = 0.6, 0.14, 0.92
WOOD, PLANK, RAIL = "Mud", "Trail earth", "Packed earth"


def rails(m, z0, z1, y0, y1):
    """Rail posts at z0 and z1 on both sides, hand rail and knee rail between (y0/y1 = deck height at z0/z1)."""
    for x in (-RAIL_X, RAIL_X):
        for z, y in ((z0, y0), (z1, y1)):
            m.add_box((x, y - 0.05, z), (0.16, 1.2 - 0.0 + 0.05, 0.16), WOOD, bottom=False)
        beam(m, (x, y0 + 1.1, z0), (x, y1 + 1.1, z1), 0.13, 0.13, RAIL)
        beam(m, (x, y0 + 0.6, z0), (x, y1 + 0.6, z1), 0.1, 0.1, RAIL)


def span():
    m = glb.Mesh()
    for i in range(4):                                         # planks across the deck, small gaps between
        z = -0.75 + 0.5 * i
        m.add_box((0, DECK - PLANK_T, z), (1.96, PLANK_T, 0.44), PLANK, bottom=PLANK)
    for x in (-0.7, 0.7):                                      # stringers under the planks
        beam(m, (x, DECK - PLANK_T - 0.1, -1.0), (x, DECK - PLANK_T - 0.1, 1.0), 0.18, 0.2, WOOD)
    for x in (-0.75, 0.75):                                    # piles into the water
        m.add_box((x, -0.8, 0), (0.22, DECK - PLANK_T - 0.2 + 0.8, 0.22), WOOD, bottom=False)
    rails(m, -1.0, 1.0, DECK, DECK)
    return m


def end():
    m = glb.Mesh()
    # earth wedge under the ramp: ground level at z = +1, deck height at z = -1
    yb = DECK - PLANK_T
    for sx in (-1, 1):
        m.add_poly([(sx * 0.98, 0, 1.0), (sx * 0.98, 0, -1.0), (sx * 0.98, yb, -1.0)], "Packed earth", facing=(sx, 0, 0))
    m.add_poly([(-0.98, 0, -1.0), (0.98, 0, -1.0), (0.98, yb, -1.0), (-0.98, yb, -1.0)], "Packed earth", facing=(0, 0, -1))
    m.add_poly([(-0.98, 0, 1.0), (0.98, 0, 1.0), (0.98, yb, -1.0), (-0.98, yb, -1.0)], "Packed earth", facing=(0, 1, 0.3))
    # sloping planks on the wedge
    for i in range(4):
        za, zb = 1.0 - 0.5 * i - 0.03, 1.0 - 0.5 * (i + 1) + 0.03
        ya, yb2 = 0.02 + (DECK - 0.02) * (1.0 - za) / 2.0, 0.02 + (DECK - 0.02) * (1.0 - zb) / 2.0   # plank tops
        beam(m, (0, ya - PLANK_T / 2, za), (0, yb2 - PLANK_T / 2, zb), 1.96, PLANK_T, PLANK)
    rails(m, 1.0, -1.0, 0.0, DECK)
    return m


for name, make, used in (("footbridge-span", span, "Hamlet: footbridge, 2 m span module (repeat across 3 to 5 cells)"),
                         ("footbridge-end", end, "Hamlet: footbridge, bank landing (one per bank)")):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=300)))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, verified=True)

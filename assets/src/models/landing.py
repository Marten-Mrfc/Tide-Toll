# python3 assets/src/models/landing.py  ->  assets/models/landing.glb
# River set: the Hamlet landing (Gameplay tab: "Landing: rowboats moor and buy goods carried straight on board; no
# storage; bank cell with 2 cells of channel depth"; Art tab, Hamlet landmarks: "timber jetty"). A short timber
# jetty running from the bank (+Z, the front, at ground level) out over the water to -Z: deck 2.4 m wide and 6 m
# long, top 0.5 m above the water (y = 0 is the bank / water surface), on piles. Four tall mooring posts with dark
# tops at the outer end and halfway along, a low bollard with a coiled line, and goods waiting for a boat on the
# shore half: two crates, three grain sacks and a wool bale (the cargo shapes villagers carry). A plank step meets the
# bank. Footprint 4 x 6 m (2 x 3 cells); boats moor along either side or at the end.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

DECK, PLANK_T, HW = 0.5, 0.14, 1.2
WOOD, PLANK, DARK = "Packed earth", "Trail earth", "Mud"

m = glb.Mesh()
for i in range(8):                                               # planks across the jetty, small gaps
    z = 2.625 - 0.75 * i
    m.add_box((0, DECK - PLANK_T, z), (2 * HW, PLANK_T, 0.69), PLANK, bottom=DARK)
for x in (-0.9, 0.9):                                            # stringers
    beam(m, (x, DECK - PLANK_T - 0.1, 3.0), (x, DECK - PLANK_T - 0.1, -3.0), 0.2, 0.2, DARK)
for z in (-2.7, -0.4):                                           # piles under the deck (the posts double as piles)
    for x in (-0.9, 0.9):
        m.add_box((x, -0.8, z), (0.22, DECK - PLANK_T - 0.2 + 0.8, 0.22), DARK, bottom=False)
m.add_box((0, 0, 3.25), (2.0, 0.22, 0.5), WOOD)                  # plank step onto the bank
# mooring posts: outside the deck edge, tall enough to read, dark weathered tops
for x, z in ((-1.42, -2.75), (1.42, -2.75), (-1.42, -0.2), (1.42, -0.2)):
    m.add_box((x, -0.8, z), (0.3, DECK + 0.8 + 1.0, 0.3), WOOD, bottom=False)
    m.add_box((x, DECK + 1.0, z), (0.36, 0.14, 0.36), DARK, bottom=DARK)
    if z < -1:
        m.add_box((x, DECK + 0.62, z), (0.36, 0.16, 0.36), "Reed and thatch", bottom="Reed and thatch")   # lashing
# low bollard at the end of the jetty with a coiled line
m.add_cylinder((0, DECK, -2.6), 0.2, 0.45, DARK, segments=6)
m.add_frustum((0.0, DECK, -2.0), 0.36, 0.3, 0.12, "Reed and thatch", segments=7)
# goods waiting on the shore half: crates, sacks, a wool bale
m.add_box((-0.6, DECK, 1.9), (0.9, 0.8, 0.9), WOOD)
m.add_box((-0.6, DECK + 0.8, 1.9), (0.94, 0.08, 0.3), DARK, bottom=DARK)
m.add_box((-0.55, DECK + 0.88, 1.9), (0.6, 0.5, 0.6), "Paper", bottom="Paper")                   # wool bale
m.add_box((-0.55, DECK + 1.08, 1.9), (0.64, 0.1, 0.64), PLANK, bottom=False)                     # its cord
m.add_box((-0.65, DECK, 0.85), (0.8, 0.6, 0.8), WOOD)
m.add_box((-0.65, DECK + 0.6, 0.85), (0.84, 0.08, 0.26), DARK, bottom=DARK)
for x, z in ((0.55, 2.0), (0.6, 1.15), (0.45, 0.35)):            # grain sacks
    m.add_sphere((x, DECK + 0.3, z), 0.36, PLANK, rings=2, segments=6, squash=0.85)

out = asset_path("models/landing.glb")
print(glb.report(out, glb.write_glb(out, m, max_tris=500)))
ledger.add("models", out, kind="model", generator=__file__, note=BLOCKED, verified=True,
           used_in="River: Hamlet landing (timber jetty with mooring posts and goods waiting)")

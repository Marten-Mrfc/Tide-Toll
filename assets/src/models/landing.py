# python3 assets/src/models/landing.py  ->  assets/models/landing.glb
# River set: the Hamlet landing (Gameplay tab: "Landing: rowboats moor and buy goods carried straight on board; no
# storage; bank cell with 2 cells of channel depth"; Art tab, Hamlet landmarks: "timber jetty"). A short timber
# jetty running from the bank (+Z, the front, at ground level) out over the water to -Z: deck 2.4 m wide and 6 m
# long, top 0.5 m above the water (y = 0 is the bank / water surface), on piles. Four tall mooring posts with dark
# tops at the outer end and halfway along, a low bollard with a coiled line, and goods waiting for a boat on the
# shore half: two crates, three grain sacks and a wool bale (the cargo shapes villagers carry). A plank step meets the
# bank. Footprint 3.2 x 6.5 m (fits 2 x 3 cells; the step reaches 0.5 m onto the bank); boats moor along either side
# or at the end.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

DECK, PLANK_T, HW = 0.5, 0.14, 1.2
WOOD, PLANK, DARK = "Packed earth", "Trail earth", "Mud"

m = glb.Mesh()
# Round n3 (track RV, 2026-11-26): piles that show (0.34 m, Mud, standing 1 m above the deck with Paper caps),
# a deck 3.2 x 6.5 m of planks in two tones, tidy goods capped and low (under 1.2 m): a crate stack under a Brick
# tarp, a pyramid of six sacks (Packed earth hessian with Soot brick ties), a barrel, a mooring post with a rope
# coil, and a lantern on a post at the jetty's end.
for i in range(8):                                               # planks across the jetty, small gaps, two tones
    z = 2.625 - 0.75 * i
    m.add_box((0, DECK - PLANK_T, z), (2 * HW + 0.3, PLANK_T, 0.69), PLANK if i % 2 else "Packed earth", bottom=DARK)
for x in (-0.9, 0.9):                                            # stringers
    beam(m, (x, DECK - PLANK_T - 0.1, 3.0), (x, DECK - PLANK_T - 0.1, -3.0), 0.2, 0.2, DARK)
m.add_box((0, 0, 3.25), (2.2, 0.22, 0.5), WOOD)                  # plank step onto the bank
# piles: outside the deck edge, through to the water, Paper caps
for x, z in ((-1.55, -2.9), (1.55, -2.9), (-1.55, -0.9), (1.55, -0.9), (-1.55, 1.1), (1.55, 1.1)):
    m.add_box((x, -0.8, z), (0.34, DECK + 0.8 + 1.0, 0.34), DARK, bottom=False)
    m.add_box((x, DECK + 1.0, z), (0.42, 0.12, 0.42), "Paper", bottom="Paper")
for x in (-1.55, 1.55):                                          # a rope lashing on the end piles
    m.add_box((x, DECK + 0.5, -2.9), (0.42, 0.16, 0.42), "Reed and thatch", bottom="Reed and thatch")
# mooring bollard and its coil at the end
m.add_cylinder((0.6, DECK, -2.6), 0.22, 0.5, DARK, segments=6)
m.add_frustum((-0.3, DECK, -2.5), 0.4, 0.34, 0.14, "Reed and thatch", segments=7)
# lantern on a post at the end (Brick hood)
m.add_box((-0.9, DECK, -3.0), (0.16, 1.9, 0.16), DARK, bottom=False)
m.add_box((-0.9, DECK + 1.9, -3.0), (0.36, 0.34, 0.36), "Reed and thatch", bottom="Soot brick")
m.add_cone((-0.9, DECK + 2.24, -3.0), 0.3, 0.26, "Brick", segments=4)
# crates in a tidy stack under a Brick tarp
m.add_box((-0.55, DECK, 1.9), (1.0, 0.55, 1.0), "Packed earth")
m.add_box((-0.55, DECK + 0.55, 1.9), (1.0, 0.45, 1.0), "Packed earth")
m.add_box((-0.55, DECK + 1.0, 1.9), (1.08, 0.08, 1.08), "Brick", bottom="Brick")
m.add_box((-1.1, DECK + 0.3, 1.9), (0.06, 0.7, 0.9), "Brick", bottom=False)         # the tarp's hanging side
m.add_box((-0.55, DECK + 0.25, 2.42), (0.9, 0.08, 0.06), DARK, bottom=False)      # batten
# a pyramid of six sacks: 3, 2, 1
def sack(x, y, z, yaw=0.0):
    m.add_box((x, y, z), (0.55, 0.3, 0.42), "Packed earth", yaw=yaw, top="Packed earth", bottom=False)
    m.add_box((x + 0.2, y + 0.08, z), (0.14, 0.14, 0.2), "Soot brick", yaw=yaw, bottom=False)
for k, x in enumerate((0.0, 0.55, 1.1)):
    sack(x - 0.1, DECK, 0.65)
for x in (0.17, 0.72):
    sack(x, DECK + 0.3, 0.65)
sack(0.45, DECK + 0.6, 0.65)
# a barrel
m.add_cylinder((0.7, DECK, 1.85), 0.34, 0.75, "Packed earth", segments=7)
m.add_cylinder((0.7, DECK + 0.3, 1.85), 0.36, 0.1, DARK, segments=7, cap_top=False, cap_bottom=False)
m.add_cylinder((0.7, DECK + 0.75, 1.85), 0.3, 0.03, "Trail earth", segments=7, cap_bottom=False)

out = asset_path("models/landing.glb")
print(glb.report(out, glb.write_glb(out, m, max_tris=700)))
ledger.add("models", out, kind="model", generator=__file__, note=BLOCKED, verified=True,
           used_in="River: the landing (timber jetty on six piles, tidy capped goods, lantern)")

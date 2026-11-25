# python3 assets/src/models/keeper-hut.py  ->  assets/models/keeper-hut.glb
# River set: the bridge keeper's hut (Gameplay tab: "Bridge keeper opens for each boat"; Sound tab: drawbridge
# "warning bell one bar early"). A tiny limewashed hut in a dark timber frame under a steep thatch roof, with a
# bell-cote on the ridge (the warning bell, a Cast-iron green bell under its own little slate roof, so it reads
# from above as a small cross-roof on the ridge) and a lantern on a bracket beside the door. Door and step face +Z
# (the front), a window on each other side (no blank backs), a bench on the east wall.
# Footprint 4 x 4 m (2 x 2 cells; the walls are 2.6 x 2.4); ridge 3.6 m, bell-cote top 4.7 m. Stand it on the bank
# next to the drawbridge-end ramp, door towards the bridge.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

WX, WZ, WH = 2.6, 2.4, 2.0
DARK, WOOD, WALL = "Mud", "Packed earth", "Limewash"

m = glb.Mesh()
m.add_box((0, 0, 0), (WX + 0.3, 0.2, WZ + 0.3), "Concrete")                    # stone sill
m.add_box((0, 0.2, 0), (WX, WH - 0.2, WZ), WALL)                                 # walls
for x in (-WX / 2, WX / 2):                                                      # corner posts (timber frame)
    for z in (-WZ / 2, WZ / 2):
        m.add_box((x, 0.2, z), (0.2, WH - 0.2, 0.2), DARK, bottom=False)
for z in (-WZ / 2 - 0.02, WZ / 2 + 0.02):                                        # wall plates front and back
    m.add_box((0, WH - 0.18, z), (WX + 0.1, 0.18, 0.16), DARK, bottom=DARK)
# door (stands out 0.12 so the ink pass outlines it) with a step
m.add_box((-0.3, 0.2, WZ / 2 + 0.06), (0.8, 1.5, 0.14), WOOD, bottom=False)
m.add_box((-0.3, 0, WZ / 2 + 0.4), (1.0, 0.2, 0.5), "Concrete")
# windows with shutters: one beside the door, one on each side, one at the back
for pos, size in (((0.75, 1.0, WZ / 2 + 0.05), (0.55, 0.5, 0.12)), ((-WX / 2 - 0.05, 1.0, 0), (0.12, 0.5, 0.6)),
                  ((WX / 2 + 0.05, 1.0, 0), (0.12, 0.5, 0.6)), ((0, 1.0, -WZ / 2 - 0.05), (0.6, 0.5, 0.12))):
    m.add_box(pos, size, DARK, bottom=DARK)
# steep thatch roof, ridge along X, limewashed gables
m.add_gable((0, WH, 0), WX + 0.5, WZ, 1.6, "thatch", gable=WALL, overhang=0.35)
m.add_box((0, WH + 1.55, 0), (WX + 0.55, 0.14, 0.3), DARK, bottom=False)          # ridge cap
# bell-cote on the ridge: two posts, a small slate roof, the bell hanging between
for x in (-0.38, 0.38):
    m.add_box((x, WH + 1.45, 0), (0.14, 0.95, 0.14), DARK, bottom=False)
m.add_gable((0, WH + 2.4, 0), 1.1, 0.75, 0.35, "Slate roof", gable=DARK, ridge_axis="z", overhang=0.05)
m.add_frustum((0, WH + 1.75, 0), 0.26, 0.15, 0.42, "Cast-iron green", segments=7, cap_bottom=True)
beam(m, (-0.38, WH + 2.25, 0), (0.38, WH + 2.25, 0), 0.1, 0.1, DARK)               # bell yoke
# lantern on a bracket by the door
beam(m, (0.95, 1.95, WZ / 2), (0.95, 1.95, WZ / 2 + 0.6), 0.1, 0.12, DARK)
m.add_box((0.95, 1.38, WZ / 2 + 0.55), (0.3, 0.42, 0.3), "Reed and thatch", bottom=DARK)   # lit glass
m.add_cone((0.95, 1.8, WZ / 2 + 0.55), 0.24, 0.16, DARK, segments=4)
# bench on the east wall
m.add_box((WX / 2 + 0.3, 0.42, 0.0), (0.4, 0.1, 1.4), WOOD, bottom=WOOD)
for z in (-0.55, 0.55):
    m.add_box((WX / 2 + 0.3, 0, z), (0.3, 0.42, 0.1), DARK)

out = asset_path("models/keeper-hut.glb")
print(glb.report(out, glb.write_glb(out, m, max_tris=500)))
ledger.add("models", out, kind="model", generator=__file__, note=BLOCKED, verified=True,
           used_in="River: bridge keeper's hut with warning bell and lantern (stands by the drawbridge)")

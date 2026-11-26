# python3 assets/src/models/well.py  ->  assets/models/well.glb
# Hamlet well (landmark list): stone ring with water, two posts, windlass with a crank and a bucket, a small steep
# thatch roof. 2.4 x 2.4 m footprint, 3 m high, front (crank and bucket side) faces +Z. The roof is narrow along Z
# so the water still shows from straight above.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam
import math

m = glb.Mesh()
# night 3 (style sheet: civic things stand on a small trodden or paved patch; a roof is never one blank plane):
# a ring of trodden earth round the well, thatch courses and a sag, a second bucket on the ground
# (kept inside the old 2.4 m footprint: TownBuildings fits the model's footprint to the 4 m plot, so a wider
# patch would shrink the well itself)
m.add_cylinder((0, 0, 0), 1.23, 0.04, "Trail earth", segments=10, cap_bottom=False)      # trodden patch
m.add_cylinder((0, 0, 0), 1.15, 0.85, "Concrete", segments=8)                      # stone ring
m.add_cylinder((0, 0.85, 0), 0.82, 0.03, "Deep water", segments=8, cap_bottom=False)  # water inside the ring
m.add_cylinder((0, 0.85, 0), 1.2, 0.12, "Wet mudflat", segments=8, cap_bottom=False, cap_top=False)  # coping band
for x in (-0.95, 0.95):
    m.add_box((x, 0.0, 0), (0.24, 2.35, 0.24), "Mud")                               # posts (stand on the ground)
beam(m, (-1.07, 1.75, 0), (1.07, 1.75, 0), 0.26, 0.26, "Packed earth")              # windlass drum
beam(m, (1.12, 1.75, 0), (1.12, 1.45, 0.35), 0.12, 0.12, "Mud")                     # crank arm
beam(m, (1.07, 1.45, 0.35), (1.18, 1.45, 0.35), 0.12, 0.12, "Mud")                   # crank handle
beam(m, (0.0, 1.62, 0), (0.0, 1.25, 0), 0.06, 0.06, "Trail earth")                  # rope
m.add_frustum((0, 0.95, 0), 0.2, 0.26, 0.32, "Packed earth", segments=6, cap_bottom=True)   # bucket
# small steep thatch roof, ridge along X, narrow so the water shows from above
# review r1: a fuller pitch (1.2 m wide) with a Brick ridge cap and two courses a side, so it reads as a roof, not a plank
m.add_gable((0, 2.23, 0), 2.3, 1.2, 0.8, "Reed and thatch", gable="Mud", overhang=0.12, bottom="Mud")
beam(m, (-1.2, 3.03, 0), (1.2, 3.02, 0), 0.3, 0.16, "Brick")                            # ridge cap
for sz in (-1, 1):
    for f in (0.33, 0.66):
        beam(m, (-1.2, 3.03 - 0.8 * f, sz * 0.6 * f), (1.2, 3.02 - 0.8 * f, sz * 0.6 * f), 0.08, 0.05, "Packed earth",
             up=(0, 1, -sz))                                                            # thatch courses
m.add_frustum((0.6, 0.97, 0.62), 0.18, 0.22, 0.3, "Packed earth", segments=6)              # a bucket on the rim
m.add_cylinder((0.6, 1.24, 0.62), 0.19, 0.04, "River", segments=6, cap_bottom=False)       # water in it
out = asset_path("models/well.glb")
print(glb.report(out, glb.write_glb(out, m, max_tris=400)))
ledger.add("models", out, kind="model", generator=__file__, used_in="Hamlet: well (landmark)", verified=True)

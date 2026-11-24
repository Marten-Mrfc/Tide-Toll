# python3 assets/src/models/well.py  ->  assets/models/well.glb
# Hamlet well (landmark list): stone ring with water, two posts, windlass with a crank and a bucket, a small steep
# thatch roof. 2.4 x 2.4 m footprint, 3 m high, front (crank and bucket side) faces +Z. The roof is narrow along Z
# so the water still shows from straight above.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam

m = glb.Mesh()
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
m.add_gable((0, 2.23, 0), 2.3, 0.9, 0.7, "Reed and thatch", gable="Mud", overhang=0.12, bottom="Mud")
beam(m, (-1.18, 2.93, 0), (1.18, 2.93, 0), 0.25, 0.14, "Packed earth")                 # ridge binding
out = asset_path("models/well.glb")
print(glb.report(out, glb.write_glb(out, m, max_tris=300)))
ledger.add("models", out, kind="model", generator=__file__, used_in="Hamlet: well (landmark)", verified=True)

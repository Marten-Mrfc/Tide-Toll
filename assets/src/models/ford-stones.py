# python3 assets/src/models/ford-stones.py  ->  assets/models/ford-stones.glb
# Hamlet ford: a line of flat stepping stones across one 2 x 2 m cell of shallow water, tops 0.3 m above the
# bed (y = 0 is the bed / low-water level; the game's water plane sits between, so the spring flood can drown them).
# Crossing direction is along Z (front +Z). Flat tops in Concrete (stone), sides in Wet mudflat (wet stone), plus two
# pebbles. Repeat cell by cell for a longer ford; the stones reach the cell edges so the line continues.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import glb, ledger, asset_path
from _kit import stone

SEED = 11
rng = np.random.default_rng(SEED)
m = glb.Mesh()
# three walking stones along Z, slightly zig-zagged, plus a flanking stone on each side
for x, z, r, h in ((-0.12, -0.68, 0.36, 0.30), (0.14, 0.0, 0.38, 0.28), (-0.08, 0.68, 0.35, 0.30),
                   (0.62, -0.42, 0.22, 0.20), (-0.6, 0.35, 0.22, 0.18)):
    stone(m, rng, (x, 0, z), r, h, n=7, amount=0.14)
for x, z in ((0.55, 0.62), (-0.62, -0.55)):          # pebbles
    stone(m, rng, (x, 0, z), 0.13, 0.12, n=5, amount=0.1)
out = asset_path("models/ford-stones.glb")
print(glb.report(out, glb.write_glb(out, m, max_tris=150)))
ledger.add("models", out, kind="model", generator=__file__, used_in="Hamlet: ford (stepping stones)", seed=SEED,
           verified=True)

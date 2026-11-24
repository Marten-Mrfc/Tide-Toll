# python3 assets/src/models/field.py  ->  assets/models/field-sprouting.glb, field-ripe.glb, field-stubble.glb
# Hamlet crop plot, 16 x 16 m (8 x 8 cells), crops under 0.6 m. Ten ploughed ridges run east-west (along X) on a
# low earth slab with a 0.4 m headland, so plots placed side by side tile into a strip-field patchwork with a
# visible edge between them. Seasons (Art tab): spring "sprouting rows", summer "golden fields", autumn
# "ploughed strips, stubble". Symmetric front to back; +Z is the front by convention.
# Second round (2026-11-24): low-pitched ridges and crop rows (gable profiles, no vertical box sides, slopes under
# about 0.65) and light Trail earth ends, so the furrows no longer go near-black in low side views; written with
# ink="soft", so only the plot's silhouette and row ends are inked, not every furrow crease (common.glb.INK_SOFT).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import glb, ledger, asset_path

SEED = 7
SIZE, ROWS, PITCH = 16.0, 10, 1.5
ROW_Z = [(-(ROWS - 1) / 2 + i) * PITCH for i in range(ROWS)]
ROW_L = SIZE - 0.8


def base():
    m = glb.Mesh()
    m.add_box((0, 0, 0), (SIZE, 0.08, SIZE), "Packed earth")                  # slab: furrows and headland
    for z in ROW_Z:                                                         # ploughed ridges, low pitch
        m.add_gable((0, 0.08, z), ROW_L, 1.3, 0.12, "Trail earth", gable="Trail earth")
    return m


def sprouting():
    m = base()
    for i, z in enumerate(ROW_Z):                                           # short green rows on each ridge
        for x0, x1 in ((-7.4, -2.6), (-2.2, 2.4), (2.8, 7.4)):
            m.add_gable(((x0 + x1) / 2, 0.17, z), x1 - x0, 0.7, 0.2, "Meadow")
    return m


def ripe():
    m = base()
    for i, z in enumerate(ROW_Z):                                           # tall golden rows, nearly closed
        m.add_gable((0, 0.12, z), ROW_L, 1.45, 0.46, "Reed and thatch")     # standing grain, combed to a ridge
    return m


def stubble():
    rng = np.random.default_rng(SEED)
    m = base()
    for z in ROW_Z:                                                         # cut stalks: thin low rows
        m.add_gable((0, 0.17, z), ROW_L - 0.4, 0.55, 0.14, "Reed and thatch")
    # sheaves stood in stooks, between the rows
    spots = [(-5.0, ROW_Z[1] + 0.75), (1.0, ROW_Z[2] + 0.75), (-2.0, ROW_Z[5] + 0.75), (4.5, ROW_Z[4] + 0.75),
             (-4.0, ROW_Z[7] + 0.75), (3.0, ROW_Z[7] + 0.75)]
    for x, z in spots:
        x += float(rng.uniform(-0.4, 0.4))
        m.add_cylinder((x, 0.08, z), 0.55, 0.18, "Reed and thatch", segments=5, cap_top=False, cap_bottom=False)
        m.add_cone((x, 0.26, z), 0.62, 0.32, "Reed and thatch", segments=5, yaw=float(rng.uniform(0, 1.2)))
    return m


for name, make, used in (("field-sprouting", sprouting, "Hamlet: fields (spring, sprouting rows)"),
                         ("field-ripe", ripe, "Hamlet: fields (summer, golden rows)"),
                         ("field-stubble", stubble, "Hamlet: fields (autumn, stubble and stooks)")):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=500, ink="soft")))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used,
               seed=SEED if name == "field-stubble" else None, verified=True)

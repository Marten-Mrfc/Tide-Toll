# python3 assets/src/models/villager.py  ->  assets/models/villager.glb, villager-cargo-{grain,wool,timber,cloth}.glb
# Villager (Art tab: "head dot and teardrop body with an ink outline"; no faces, no rig; shader bobbing; outfits are
# palette swaps on one body mesh). Drawn 1.5x life size: 1.0 x 1.0 m footprint, 2.6 m to the top of the head.
# villager.glb has two nodes so a shader can bob and sway them: "body" (teardrop tunic with a shoulder cape and a hood point
# down the back, feet, a belt and a satchel on the front +Z, so the walking direction reads) and "head" (the head dot).
# Cargo (Art tab: round sack = grain, square bale = wool, long bar = timber, folded square = cloth) is carried above
# the head; each cargo file is one node with its origin at its bottom centre: attach it at (0, 2.6, 0) (top of the head).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path, ROOT
from _kit import loft, ngon, beam

SEG = 7
TUNIC, CAPE, SKIN, BELT = "Brick", "Soot brick", "Limewash", "Packed earth"
CARRY_Y = 2.6


def body():
    m = glb.Mesh()
    # feet (two little blocks peeking out under the hem, toes to the front)
    for x in (-0.17, 0.17):
        m.add_box((x, 0, 0.08), (0.2, 0.16, 0.34), "Mud")
    # teardrop tunic: wide round hem, one smooth taper up to the neck, where the head dot sits
    prof = [(0.12, 0.30), (0.2, 0.45), (0.65, 0.48), (1.05, 0.41), (1.45, 0.29), (1.9, 0.12)]
    rings = [ngon(0, 0, r, SEG, y=y, phase=0.22) for y, r in prof]
    m.add_poly(rings[0], TUNIC, facing=(0, -1, 0))                     # hem underside (seen from low angles)
    m.add_poly(rings[-1], TUNIC, facing=(0, 1, 0))
    for a, b in zip(rings, rings[1:]):
        loft(m, a, b, TUNIC, centre=(0, (a[0][1] + b[0][1]) / 2, 0))
    # belt band, a hair proud of the tunic so it gets a line
    loft(m, ngon(0, 0, 0.49, SEG, y=0.82, phase=0.22), ngon(0, 0, 0.47, SEG, y=0.96, phase=0.22), BELT,
         centre=(0, 0.9, 0))
    # shoulder cape: a short collar band standing proud of the tunic
    cape_a = ngon(0, 0, 0.42, SEG, y=1.2, phase=0.22)
    cape_b = ngon(0, 0, 0.27, SEG, y=1.62, phase=0.22)
    loft(m, cape_a, cape_b, CAPE, centre=(0, 1.4, 0))
    m.add_poly(cape_a, CAPE, facing=(0, -1, 0))
    m.add_poly(cape_b, CAPE, facing=(0, 1, 0))
    # hood point hanging down the back (-Z): shows which way the villager faces
    beam(m, (0, 1.6, -0.22), (0, 1.1, -0.43), 0.2, 0.14, CAPE)
    # satchel on the front (+Z), proud of the tunic
    m.add_box((0.12, 0.6, 0.42), (0.3, 0.28, 0.14), BELT, bottom=BELT)
    return m


def head():
    m = glb.Mesh()
    m.add_sphere((0, 2.24, 0), 0.36, SKIN, rings=3, segments=7)
    return m


def cargo(kind):
    m = glb.Mesh()
    if kind == "grain":        # round sack
        m.add_sphere((0, 0.3, 0), 0.36, "Trail earth", rings=3, segments=7, squash=0.85)
        m.add_frustum((0, 0.56, 0), 0.08, 0.14, 0.14, "Mud", segments=5)              # tied neck
    elif kind == "wool":       # square bale
        m.add_box((0, 0, 0), (0.62, 0.5, 0.62), "Paper", bottom="Paper")
        m.add_box((0, 0.2, 0), (0.66, 0.1, 0.66), "Trail earth", bottom=False)        # cord
    elif kind == "timber":     # long bar, across the shoulders (along X)
        for dz, y in ((-0.12, 0.0), (0.12, 0.0), (0.0, 0.2)):
            beam(m, (-0.8, y + 0.1, dz), (0.8, y + 0.1, dz), 0.2, 0.2, "Packed earth")
    elif kind == "cloth":      # folded square
        m.add_box((0, 0, 0), (0.7, 0.14, 0.7), "Brick", bottom="Brick")
        m.add_box((0, 0.14, 0), (0.7, 0.12, 0.7), "Limewash", bottom=False)
        m.add_box((0, 0.26, 0), (0.7, 0.12, 0.7), "Brick", bottom=False)
    return m


out = asset_path("models/villager.glb")
print(glb.report(out, glb.write_glb(out, [("body", body()), ("head", head())], max_tris=300)))
ledger.add("models", out, kind="model", generator=__file__, used_in="Villagers: body and head (Hamlet outfit)",
           verified=True)
for kind in ("grain", "wool", "timber", "cloth"):
    out = asset_path(f"models/villager-cargo-{kind}.glb")
    print(glb.report(out, glb.write_glb(out, cargo(kind), max_tris=100)))
    ledger.add("models", out, kind="model", generator=__file__, used_in=f"Villagers: carried cargo ({kind})",
               verified=True)

# preview only (not shipped): villager carrying grain, for the contact sheet
prev = ROOT / "build" / "previews" / "villager-with-grain.glb"
prev.parent.mkdir(parents=True, exist_ok=True)
glb.write_glb(prev, [("body", body()), ("head", head()), ("item", glb.Mesh().append(cargo("grain"), (0, CARRY_Y, 0)))])

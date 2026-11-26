# python3 assets/src/models/villager.py  ->  assets/models/villager.glb, villager-head-hood.glb, villager-head-hat.glb,
#   villager-cargo-{water,grain,flour,timber,wool,cloth}.glb, villager-packframe.glb, villager-handcart.glb
# Night 3 (style sheet sections 4 and 6): a person, not a pawn. Drawn 1.4x life size (2.6 m to the top of the head) so
# the head, arms and load read at the normal view. Front is +Z.
#   villager.glb: node "body" (legs and shoes, tunic with belt and collar, two arms bent forward with hands that hold
#     the load at the chest) and node "head" (face and dark hair). The tunic faces are Brick: the game's villager
#     shader dyes only Brick faces to the villager's tunic colour (five palette colours), the rest keeps its own.
#   villager-head-hood.glb / villager-head-hat.glb: node "head": the other two heads (a pale hood; a straw hat).
#   Cargo files are in the BODY's frame (origin at the feet, +Z forward), so the game places them with the body's
#   transform: grain and flour sacks, the wool bale and the cloth bolt held in the arms, the log on the right
#   shoulder, the water yoke across the shoulders with a pail at each end.
#   villager-packframe.glb (Load step 3): a frame on the back with a shelf; villager-handcart.glb (Load step 4):
#   a two-wheeled cart pushed in front, its handles in the hands.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path, ROOT
import math
from _kit import loft, ngon, beam, tube, cap

SEG = 7
TUNIC = "Brick"            # dyed per villager by the game's shader
SKIN, HAIR, LEGS, SHOE, BELT = "Trail earth", "Mud", "Packed earth", "Mud", "Mud"
HOLD = (0.0, 1.22, 0.55)    # centre of a load held in the arms
SHOULDER_Y = 1.62


def body():
    m = glb.Mesh()
    for x in (-0.15, 0.15):
        m.add_box((x, 0, 0.06), (0.24, 0.14, 0.4), SHOE)                       # shoes, toes forward
        m.add_box((x, 0.14, 0), (0.2, 0.62, 0.22), LEGS, bottom=False)          # legs
    prof = [(0.66, 0.42), (0.8, 0.4), (1.25, 0.34), (1.6, 0.33), (1.74, 0.2)]
    rings = [ngon(0, 0, r, SEG, y=y, phase=0.22, sz=0.82) for y, r in prof]
    m.add_poly(rings[0], TUNIC, facing=(0, -1, 0))
    m.add_poly(rings[-1], TUNIC, facing=(0, 1, 0))
    for a, b in zip(rings, rings[1:]):
        loft(m, a, b, TUNIC, centre=(0, (a[0][1] + b[0][1]) / 2, 0))
    loft(m, ngon(0, 0, 0.375, SEG, y=1.0, phase=0.22, sz=0.84), ngon(0, 0, 0.365, SEG, y=1.12, phase=0.22, sz=0.84),
         BELT, centre=(0, 1.06, 0))                                             # belt, proud so the ink finds it
    m.add_frustum((0, 1.7, 0), 0.2, 0.15, 0.14, SKIN, segments=6)               # neck
    m.add_frustum((0, 1.68, 0), 0.27, 0.21, 0.07, "Limewash", segments=7, cap_top=False)   # pale collar: the tunic's edge
    for sx in (-1, 1):                                                          # arms: sleeve to the elbow, forearm
        sh = (sx * 0.36, SHOULDER_Y - 0.06, -0.02)
        el = (sx * 0.4, 1.2, 0.12)
        hd = (sx * 0.3, 1.24, 0.48)
        beam(m, sh, el, 0.2, 0.2, TUNIC)
        beam(m, el, hd, 0.17, 0.17, TUNIC)
        m.add_box((hd[0], hd[1] - 0.1, hd[2] + 0.02), (0.15, 0.2, 0.17), SKIN, bottom=SKIN)   # hands
    return m


def face(m):
    m.add_sphere((0, 2.16, 0.03), 0.36, SKIN, rings=3, segments=8)
    m.add_box((0, 2.08, 0.37), (0.1, 0.12, 0.08), SKIN, bottom=SKIN)            # nose: which way the face looks


def head_hair():
    m = glb.Mesh()
    face(m)
    m.add_sphere((0, 2.3, -0.06), 0.37, HAIR, rings=3, segments=8, squash=0.72)   # hair over the crown and back
    return m


def head_hood():
    m = glb.Mesh()
    face(m)
    m.add_sphere((0, 2.25, -0.17), 0.4, "Limewash", rings=3, segments=8, squash=1.0)
    beam(m, (0, 2.3, -0.48), (0, 1.8, -0.6), 0.22, 0.16, "Limewash")         # the hood's point down the back
    loft(m, ngon(0, 0, 0.34, 8, y=1.68), ngon(0, 0, 0.27, 8, y=1.86), "Limewash", centre=(0, 1.77, 0))   # cowl
    return m


def head_hat():
    m = glb.Mesh()
    face(m)
    m.add_sphere((0, 2.24, -0.08), 0.37, HAIR, rings=2, segments=8, squash=0.7)
    # Brim 0.34 m life size (0.48 drawn) so the shoulders and a load on the back show; a darker underside.
    m.add_frustum((0, 2.4, 0), 0.48, 0.45, 0.07, "Reed and thatch", segments=10)
    m.add_frustum((0, 2.385, 0), 0.47, 0.47, 0.015, "Packed earth", segments=10, cap_top=False)
    m.add_frustum((0, 2.47, 0), 0.31, 0.25, 0.2, "Reed and thatch", segments=8)    # crown
    m.add_frustum((0, 2.47, 0), 0.325, 0.31, 0.07, "Brick", segments=8, cap_top=False)   # band
    return m


def ring_x(x, cy, cz, r, n=6, sy=1.0, sz=1.0):
    """n points on an ellipse in the YZ plane at x (a sack lying across the arms)."""
    return [(x, cy + r * sy * math.cos(2 * math.pi * (i + 0.5) / n), cz + r * sz * math.sin(2 * math.pi * (i + 0.5) / n))
            for i in range(n)]


def sack_upright(m, c, colour, tie="Soot brick", ear=None):
    """A squat soft sack standing in the arms (0.45 x 0.35 x 0.3 m life size), its top gathered to a tied neck."""
    x, y, z = c
    prof = [(-0.36, 0.26), (-0.28, 0.34), (0.04, 0.33), (0.16, 0.22), (0.22, 0.1)]   # (height, radius)
    rings = [ngon(x, z, r, 6, y=y + h, sz=0.7) for h, r in prof]
    cap(m, rings[0], colour, facing=(0, -1, 0))
    for a, b in zip(rings, rings[1:]):
        loft(m, a, b, colour, centre=(x, y - 0.05, z))
    m.add_frustum((x, y + 0.2, z), 0.12, 0.12, 0.09, tie, segments=6)                   # the tie, 5 cm wide
    m.add_frustum((x, y + 0.29, z), 0.1, 0.17, 0.12, ear or colour, segments=6)          # the ear above it
    beam(m, (x, y - 0.3, z + 0.245), (x, y + 0.1, z + 0.235), 0.06, 0.03, "Mud")         # seam down the front


def sack_lying(m, c, colour, band, tie="Mud"):
    """A sack lying across both arms, its neck tied at the right, one dark band round its belly."""
    x, y, z = c
    prof = [(-0.5, 0.2), (-0.42, 0.29), (0.12, 0.3), (0.3, 0.2), (0.38, 0.1)]          # (along x, radius)
    rings = [ring_x(x + u, y, z, r, 6, sy=0.85) for u, r in prof]
    cap(m, rings[0], colour, facing=(-1, 0, 0))
    for a, b in zip(rings, rings[1:]):
        loft(m, a, b, colour, centre=(x - 0.05, y, z))
    tube(m, (x - 0.12, y, z), (x - 0.02, y, z), 0.32, band, n=6, r1=0.32)               # the band, standing proud
    tube(m, (x + 0.38, y, z), (x + 0.46, y, z), 0.1, tie, n=6)                          # tied neck
    tube(m, (x + 0.46, y, z), (x + 0.58, y, z), 0.1, colour, n=6, r1=0.17)              # the ear


def cargo(kind):
    m = glb.Mesh()
    if kind == "grain":        # a hessian sack (never Thatch: the straw hat and the roofs are Thatch), grain at its ear
        sack_upright(m, HOLD, "Packed earth", ear="Reed and thatch")
    elif kind == "flour":      # a white sack lying across the arms, one dark band, tied at the neck
        sack_lying(m, (HOLD[0], HOLD[1] + 0.06, HOLD[2]), "Limewash", band="Mud")
    elif kind == "wool":       # a ragged bale on the left shoulder: three lumps, two proud Thatch ties
        x, y, z = -0.42, 2.0, -0.05
        tube(m, (x, y, z - 0.42), (x, y, z + 0.42), 0.34, "Limewash", n=8)
        m.add_sphere((x - 0.02, y + 0.22, z - 0.2), 0.24, "Limewash", rings=2, segments=6)
        m.add_sphere((x + 0.03, y + 0.2, z + 0.22), 0.22, "Limewash", rings=2, segments=6)
        m.add_sphere((x - 0.2, y + 0.05, z + 0.02), 0.22, "Limewash", rings=2, segments=6)
        for tz in (-0.22, 0.24):
            tube(m, (x, y, z + tz - 0.05), (x, y, z + tz + 0.05), 0.42, "Reed and thatch", n=8)
    elif kind == "cloth":      # rolled bolt across the arms: Limewash cloth in broad Brick stripes, never one tunic colour
        x, y, z = HOLD
        tube(m, (-0.6, y + 0.05, z), (0.6, y + 0.05, z), 0.24, "Limewash", n=8, end_colour="Paper")
        for sx in (-0.38, 0.0, 0.38):
            tube(m, (sx - 0.09, y + 0.05, z), (sx + 0.09, y + 0.05, z), 0.255, "Brick", n=8)
    elif kind == "timber":     # a log on the right shoulder, along the body
        tube(m, (0.3, 1.95, -0.95), (0.3, 2.0, 1.0), 0.21, "Packed earth", n=7, end_colour="Trail earth")
        beam(m, (0.3, 2.0, 0.35), (0.3, 2.25, 0.55), 0.1, 0.1, "Packed earth")  # a stub of a branch
    elif kind == "water":      # yoke across the shoulders (0.12 m thick life size), a pail at each end
        beam(m, (-0.95, 1.86, -0.04), (0.95, 1.86, -0.04), 0.18, 0.17, "Packed earth")
        for sx in (-1, 1):
            beam(m, (sx * 0.86, 1.86, -0.04), (sx * 0.86, 1.2, -0.04), 0.08, 0.08, "Mud")
            m.add_frustum((sx * 0.86, 0.72, -0.04), 0.24, 0.3, 0.48, "River", segments=8, cap_top=False)
            m.add_cylinder((sx * 0.86, 1.1, -0.04), 0.27, 0.06, "Deep water", segments=8)
            m.add_frustum((sx * 0.86, 0.94, -0.04), 0.285, 0.29, 0.06, "Mud", segments=8, cap_top=False)
    return m


def packframe():
    m = glb.Mesh()
    for sx in (-1, 1):
        beam(m, (sx * 0.28, 0.75, -0.42), (sx * 0.3, 2.35, -0.46), 0.1, 0.1, "Packed earth")
        beam(m, (sx * 0.3, 1.6, -0.36), (sx * 0.22, 1.66, 0.12), 0.08, 0.06, "Mud")   # strap over the shoulder
    for y in (0.95, 2.2):
        beam(m, (-0.36, y, -0.45), (0.36, y, -0.45), 0.08, 0.08, "Packed earth")
    m.add_box((0, 1.2, -0.68), (0.72, 0.08, 0.5), "Packed earth", bottom="Packed earth")   # shelf the load sits on
    return m


def handcart():
    m = glb.Mesh()
    z0, z1, y0 = 0.95, 2.35, 0.62
    m.add_box((0, y0, (z0 + z1) / 2), (1.1, 0.1, z1 - z0), "Packed earth", bottom="Packed earth")
    for sx in (-1, 1):
        m.add_box((sx * 0.55, y0, (z0 + z1) / 2), (0.08, 0.34, z1 - z0), "Trail earth", bottom=False)
        tube(m, (sx * 0.66, 0.42, 1.65), (sx * 0.76, 0.42, 1.65), 0.42, "Mud", n=8, end_colour="Packed earth")
        beam(m, (sx * 0.5, y0 + 0.1, z0 + 0.1), (sx * 0.3, 1.2, 0.5), 0.08, 0.08, "Packed earth")   # handles
    m.add_box((0, y0, z1), (1.1, 0.34, 0.08), "Trail earth", bottom=False)
    m.add_box((0, y0, z0), (1.1, 0.3, 0.08), "Trail earth", bottom=False)
    beam(m, (0, 0.05, 2.2), (0, y0, 2.2), 0.1, 0.1, "Mud")                         # the leg it rests on
    return m


def write(name, parts, budget, used):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, parts, max_tris=budget)))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, verified=True)


write("villager", [("body", body()), ("head", head_hair())], 420,
      "Villagers: body (legs, tunic dyed per villager, arms holding the load) and head with hair")
write("villager-head-hood", [("head", head_hood())], 160, "Villagers: second head (a pale hood)")
write("villager-head-hat", [("head", head_hat())], 220, "Villagers: third head (a straw hat)")
for kind, what in (("water", "two pails on a yoke"), ("grain", "a tied hessian sack"), ("flour", "a white sack lying across the arms, one dark band"),
                   ("timber", "a log on the shoulder"), ("wool", "a ragged bale on the shoulder, tied twice"), ("cloth", "a rolled bolt, Limewash in Brick stripes")):
    write(f"villager-cargo-{kind}", cargo(kind), 250, f"Villagers: carried {kind} ({what}), in the body's frame")
write("villager-packframe", packframe(), 160, "Villagers: pack frame on the back (Load step 3)")
write("villager-handcart", handcart(), 250, "Villagers: handcart pushed in front (Load step 4)")

prev = ROOT / "build" / "previews" / "villager-with-grain.glb"
prev.parent.mkdir(parents=True, exist_ok=True)
glb.write_glb(prev, [("body", body()), ("head", head_hair()), ("item", cargo("grain"))])

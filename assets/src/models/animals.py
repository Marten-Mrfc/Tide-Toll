# python3 assets/src/models/animals.py [name ...]  ->  assets/models/animal-*.glb
# Night 3, round 3 (asset track LF, life in the town): the small animals and birds the town shows as decoration
# (game/scripts/town/TownLife.cs). Hamlet style like river-town.py's sheep: chunky lumps, one palette colour per face,
# a soft ink. Sizes are real size; the game draws them 1.5x like the villagers and the sheep. Front faces +Z.
#
#   animal-hen     a Limewash hen, 0.45 m long, Brick comb, Reed and thatch beak and legs. Nodes "Body" and "Head"
#                  (origin at the neck (0, 0.30, 0.10); the game dips Head.rotation.x to peck).
#   animal-duck    a duck on the water (origin = the water line): Packed earth back, Limewash breast, Cast-iron green
#                  head, Thatch bill, a thin Limewash ring ripple round it (after the review of 02:3x).
#   animal-heron   a heron standing in the shallows, 1.2 m tall: Limewash body and neck, Slate wings, Ink tips and crest,
#                  long Reed and thatch bill, thin Soot brick legs. Nodes "Body" and "Neck" (origin (0, 0.75, 0.1)).
#   animal-cat     a ginger (Reed and thatch) cat curled asleep, 0.5 m across, Brick ears, stripe and curled tail tip.
#   animal-dog     a Packed earth dog lying with its head up, 0.8 m long, Mud ears and Limewash muzzle. Nodes
#                  "Body" and "Head" (origin (0, 0.32, 0.25); the game turns Head.rotation.y to look round).
#   animal-bird    a pale (Limewash) bird with Ink wing tips: a shallow V of two wings, 0.7 m span, a short body.
#                  Nodes "Body" and "WingL", "WingR" (origins at the shoulders; the game flaps rotation.z).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

from common import glb, ledger, asset_path
from _kit import beam

NOTE = "made in code here; free asset sites blocked from the build container (checked 2026-11-25)"
HEN_NECK = (0.0, 0.30, 0.10)
HERON_NECK = (0.0, 0.75, 0.10)
DOG_NECK = (0.0, 0.32, 0.25)


def hen_body():
    m = glb.Mesh()
    for x in (-0.06, 0.06):
        beam(m, (x, 0, 0.0), (x, 0.14, 0.0), 0.04, 0.04, "Reed and thatch")
    m.add_sphere((0, 0.24, -0.02), 0.17, "Limewash", rings=2, segments=6, squash=0.75)
    beam(m, (0, 0.26, -0.14), (0, 0.38, -0.26), 0.12, 0.06, "Paper")              # tail
    return m


def hen_head():
    m = glb.Mesh()
    m.add_sphere((0, 0.06, 0.04), 0.08, "Limewash", rings=2, segments=5)
    beam(m, (0, 0.13, 0.0), (0, 0.15, 0.09), 0.03, 0.05, "Brick")                 # comb
    beam(m, (0, 0.05, 0.1), (0, 0.04, 0.17), 0.04, 0.03, "Reed and thatch")         # beak
    return m


def duck():
    """A mallard-ish duck: Packed earth back and tail, a Limewash breast that reads on the water, a Cast-iron green
    head, a Reed and thatch bill, and a thin pale ring ripple round it on the water line."""
    m = glb.Mesh()
    m.add_sphere((0, 0.06, -0.05), 0.2, "Packed earth", rings=2, segments=6, squash=0.5)
    m.add_sphere((0, 0.08, 0.08), 0.15, "Limewash", rings=2, segments=6, squash=0.6)        # breast
    beam(m, (0, 0.1, -0.18), (0, 0.16, -0.28), 0.1, 0.04, "Mud")                          # tail
    beam(m, (0, 0.1, 0.12), (0, 0.22, 0.15), 0.08, 0.08, "Cast-iron green", ends=False)  # neck
    m.add_sphere((0, 0.25, 0.16), 0.085, "Cast-iron green", rings=2, segments=5)
    beam(m, (0, 0.23, 0.22), (0, 0.22, 0.31), 0.07, 0.03, "Reed and thatch")             # bill
    import math
    r0, r1, k = 0.38, 0.42, 16
    for i in range(k):
        if i in (3, 4, 5, 11, 12, 13):        # a broken ring (two arcs either side), not a hard hoop
            continue
        a0, a1 = 2 * math.pi * i / k, 2 * math.pi * (i + 1) / k
        q = [(r0 * math.cos(a0), 0.0, r0 * math.sin(a0)), (r0 * math.cos(a1), 0.0, r0 * math.sin(a1)),
             (r1 * math.cos(a1), 0.0, r1 * math.sin(a1)), (r1 * math.cos(a0), 0.0, r1 * math.sin(a0))]
        m.add_poly(list(reversed(q)), "Limewash", facing=(0, 1, 0))                       # ring ripple
    return m


def heron_body():
    """Limewash body with Slate roof wings and Ink wing tips, long Soot brick legs standing in the shallows."""
    m = glb.Mesh()
    for x in (-0.06, 0.06):
        beam(m, (x, 0, 0.0), (x, 0.56, 0.0), 0.045, 0.045, "Soot brick")
    m.add_sphere((0, 0.68, -0.05), 0.18, "Limewash", rings=2, segments=6, squash=1.1)
    for sx in (-1, 1):
        beam(m, (sx * 0.12, 0.76, 0.02), (sx * 0.12, 0.64, -0.3), 0.07, 0.16, "Slate roof")   # folded wings
    beam(m, (0, 0.7, -0.26), (0, 0.6, -0.46), 0.2, 0.06, "Ink")                       # dark wing tips
    return m


def heron_neck():
    m = glb.Mesh()
    beam(m, (0, 0.0, 0.0), (0, 0.3, 0.08), 0.09, 0.09, "Limewash")
    m.add_sphere((0, 0.36, 0.1), 0.08, "Limewash", rings=2, segments=5)
    beam(m, (0, 0.4, 0.04), (0, 0.36, -0.16), 0.03, 0.03, "Ink")                    # crest
    beam(m, (0, 0.35, 0.16), (0, 0.32, 0.42), 0.045, 0.045, "Reed and thatch")       # bill
    return m


def cat():
    """A ginger (Reed and thatch) cat curled asleep with Brick ears and stripes, a Limewash muzzle and chest, and a
    tail that curls up and round with a Brick tip."""
    m = glb.Mesh()
    m.add_sphere((0, 0.1, -0.03), 0.21, "Reed and thatch", rings=2, segments=7, squash=0.5)
    beam(m, (-0.18, 0.17, -0.05), (0.18, 0.17, -0.05), 0.05, 0.03, "Brick", ends=False)   # stripe
    m.add_sphere((0.08, 0.15, 0.15), 0.115, "Reed and thatch", rings=2, segments=6, squash=0.85)
    m.add_sphere((0.09, 0.12, 0.24), 0.06, "Limewash", rings=2, segments=5)              # muzzle
    for dx in (0.0, 0.15):
        m.add_cone((dx, 0.22, 0.14), 0.05, 0.1, "Brick", segments=3)
    beam(m, (-0.21, 0.06, -0.02), (-0.22, 0.2, 0.14), 0.07, 0.07, "Reed and thatch")      # tail rises
    beam(m, (-0.22, 0.2, 0.14), (-0.08, 0.24, 0.24), 0.07, 0.07, "Reed and thatch")       # and curls over
    beam(m, (-0.08, 0.24, 0.24), (0.0, 0.18, 0.28), 0.07, 0.07, "Brick")                # its tip
    return m


def dog_body():
    """Lying down: chest and haunch as two lumps, front legs stretched forward under the head, Limewash paws."""
    m = glb.Mesh()
    m.add_sphere((0, 0.17, 0.05), 0.19, "Packed earth", rings=2, segments=6, squash=0.75)
    m.add_sphere((0, 0.2, 0.17), 0.12, "Limewash", rings=2, segments=5, squash=0.9)          # pale chest
    m.add_sphere((0, 0.15, -0.25), 0.2, "Packed earth", rings=2, segments=6, squash=0.65)
    for x in (-0.08, 0.08):
        beam(m, (x, 0.04, 0.12), (x, 0.04, 0.34), 0.07, 0.07, "Packed earth")         # front legs
        m.add_box((x, 0.0, 0.36), (0.08, 0.07, 0.07), "Limewash", bottom=False)          # paws
    beam(m, (0, 0.2, -0.42), (0.08, 0.32, -0.55), 0.05, 0.05, "Mud")                # tail, up
    return m


def dog_head():
    m = glb.Mesh()
    m.add_sphere((0, 0.05, 0.03), 0.11, "Packed earth", rings=2, segments=6)
    beam(m, (0, 0.02, 0.08), (0, 0.0, 0.2), 0.09, 0.08, "Limewash")                 # muzzle
    for sx in (-1, 1):
        beam(m, (sx * 0.08, 0.13, 0.0), (sx * 0.14, 0.0, 0.0), 0.08, 0.035, "Mud")    # ears
    return m


def bird_body():
    m = glb.Mesh()
    beam(m, (0, 0, -0.14), (0, 0, 0.14), 0.08, 0.07, "Limewash")
    return m


def bird_wing(side):
    """A Limewash wing with an Ink tip (the outer third), both faces."""
    m = glb.Mesh()
    mid = (side * 0.22, 0.03, -0.04)
    tip = (side * 0.34, 0.045, -0.08)
    inner = [(0, 0, 0.07), mid, (side * 0.2, 0.03, -0.1), (0, 0, -0.09)]
    outer = [(side * 0.22, 0.03, 0.0), tip, (side * 0.2, 0.03, -0.1)]
    for poly, col in ((inner, "Limewash"), (outer, "Ink")):
        pts = poly if side > 0 else list(reversed(poly))
        m.add_poly(pts, col, facing=(0, 1, 0))
        m.add_poly(list(reversed(pts)), col, facing=(0, -1, 0))
    return m


BUILDS = [
    ("animal-hen", [("Body", hen_body()), ("Head", hen_head(), None, HEN_NECK)], 200, "Town life: hens pecking by a cottage"),
    ("animal-duck", duck(), 200, "Town life: ducks drifting on the river"),
    ("animal-heron", [("Body", heron_body()), ("Neck", heron_neck(), None, HERON_NECK)], 240, "Town life: a heron in the shallows"),
    ("animal-cat", cat(), 200, "Town life: a cat asleep on a doorstep"),
    ("animal-dog", [("Body", dog_body()), ("Head", dog_head(), None, DOG_NECK)], 200, "Town life: a dog lying near the market"),
    ("animal-bird", [("Body", bird_body()), ("WingL", bird_wing(-1)), ("WingR", bird_wing(1))], 40,
     "Town life: birds gliding high over the map and a flock lifting from a field"),
]

if __name__ == "__main__":
    only = [a for a in sys.argv[1:] if not a.startswith("--")]
    for name, parts, budget, used in BUILDS:
        if only and name not in only:
            continue
        out = asset_path(f"models/{name}.glb")
        print(glb.report(out, glb.write_glb(out, parts, max_tris=budget, ink="soft")))
        ledger.add("models", out, kind="model", generator=__file__, note=NOTE, used_in=used, verified=True)

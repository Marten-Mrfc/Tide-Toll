# python3 assets/src/models/cottage.py  ->  assets/models/cottage-01.glb, cottage-02.glb, cottage-03.glb
# Hamlet (c. 1100) houses: timber longhouses, wattle and daub, hipped thatch (Art tab). Front door faces +Z.
#   cottage-01  8 x 6 m (4 x 3 cells), daub walls in a dark timber frame, hipped thatch, ridge 5.95 m
#   cottage-02  8 x 6 m, plank walls, owl gables at the ridge ends, a byre door on the east end, hay rick at the back
#   cottage-03  6 x 4 m (3 x 2 cells) small hut, daub walls, steep hip, ridge 4.9 m
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))                          # _kit
from common import glb, ledger, asset_path
from _kit import beam

WOOD = "Mud"          # timber frame (dark wood)
DOOR = "Soot brick"   # doors
SHUT = "Mud"          # shutters


def house(L, W, wall_h, roof_h, overhang=0.3, wall="Limewash", frame=True, mids=(), door_x=0.0, back_door_x=None,
          side_windows=True, byre=False, woodpile=True, hayrick=False, owl_gables=False):
    """Wall box L x W (x, z), hipped thatch whose eaves reach L + 0.6 + 2*overhang... sized so the roof footprint is
    (L + 0.4 + 2*overhang) x (W + 0.4 + 2*overhang)."""
    m = glb.Mesh()
    hx, hz = L / 2, W / 2
    # walls (daub or planks)
    m.add_box((0, 0, 0), (L, wall_h, W), wall)
    # hipped thatch: roof base 0.2 outside the walls, plus overhang
    m.add_hip((0, wall_h, 0), L + 0.4, W + 0.4, roof_h, "Reed and thatch", ridge_frac=0.42, overhang=overhang)
    # ridge cap (darker thatch binding) - a line along the ridge from above
    r = (L + 0.4) * 0.42 / 2
    top = wall_h + roof_h
    beam(m, (-r - 0.25, top - 0.02, 0), (r + 0.25, top - 0.02, 0), 0.45, 0.22, "Packed earth")
    # owl gables (half-hips with a small dark gable at each ridge end, the smoke outlet of a Low Countries hall house)
    if owl_gables:
        for sx in (-1, 1):
            m.add_gable((sx * (r + 0.2), top - 0.95, 0), 1.0, 1.3, 0.95, "Reed and thatch", gable=WOOD, bottom=WOOD)
    # timber frame: corner posts and a wall plate, standing proud of the daub so the ink pass catches them
    if frame:
        for sx in (-1, 1):
            for sz in (-1, 1):
                m.add_box((sx * hx, 0, sz * hz), (0.34, wall_h, 0.34), WOOD)
        for x in mids:
            for sz in (-1, 1):
                m.add_box((x, 0, sz * (hz + 0.04)), (0.24, wall_h, 0.12), WOOD)
        for sx in (-1, 1):
            m.add_box((sx * (hx + 0.04), 0, 0), (0.12, wall_h, 0.24), WOOD)
        # sill beam (front and back) at the foot of the wall
        for sz in (-1, 1):
            m.add_box((0, 0, sz * (hz + 0.04)), (L, 0.28, 0.12), WOOD)
    # front door (+Z) and its lintel
    m.add_box((door_x, 0, hz + 0.07), (1.1, 1.8, 0.16), DOOR)
    m.add_box((door_x, 1.8, hz + 0.08), (1.5, 0.2, 0.2), WOOD, bottom=WOOD)
    # front shutters either side of the door
    for x in (door_x - 1.9, door_x + 1.9):
        if abs(x) < hx - 0.5:
            m.add_box((x, 0.95, hz + 0.06), (0.75, 0.6, 0.14), SHUT, bottom=SHUT)
    # back (-Z): a small door or a shuttered window, so the back is never blank
    if back_door_x is not None:
        m.add_box((back_door_x, 0, -hz - 0.07), (0.9, 1.6, 0.16), DOOR)
    for x in (-hx * 0.45, hx * 0.45):
        if back_door_x is None or abs(x - back_door_x) > 1.0:
            m.add_box((x, 0.95, -hz - 0.06), (0.7, 0.55, 0.14), SHUT, bottom=SHUT)
    # ends (+X / -X)
    if byre:   # wide byre door on the east end (cattle under the same roof)
        m.add_box((hx + 0.08, 0, 0), (0.18, 1.85, 1.6), DOOR)
        m.add_box((hx + 0.09, 1.85, 0), (0.2, 0.2, 2.0), WOOD, bottom=WOOD)
    elif side_windows:
        m.add_box((hx + 0.06, 0.95, 0), (0.14, 0.55, 0.7), SHUT, bottom=SHUT)
    if side_windows and not byre:
        m.add_box((-hx - 0.06, 0.95, 0), (0.14, 0.55, 0.7), SHUT, bottom=SHUT)
    # yard details, kept inside the roof footprint
    if woodpile:   # log pile against the west end
        for i, y in enumerate((0, 0.35, 0.7)):
            m.add_box((-hx - 0.25, y, 0.2 * (i % 2)), (0.45, 0.35, 1.6 - 0.35 * i), "Packed earth",
                      bottom=None if y == 0 else False)
    if hayrick:    # small hay rick against the back wall
        m.add_cylinder((hx * 0.5, 0, -hz), 0.45, 0.9, "Reed and thatch", segments=6)
        m.add_cone((hx * 0.5, 0.9, -hz), 0.5, 0.55, "Reed and thatch", segments=6)
    return m


VARIANTS = {
    # name: (mesh, used_in)
    "cottage-01": (lambda: house(7.0, 5.0, 2.3, 3.45, mids=(-2.75, 2.75), door_x=0.0, back_door_x=None),
                   "Hamlet: houses (longhouse, daub)"),
    "cottage-02": (lambda: house(7.0, 5.0, 2.2, 3.3, wall="Packed earth", mids=(-2.6, 1.5), door_x=-1.4,
                                 back_door_x=-1.4, byre=True, woodpile=False, hayrick=True,
                                 owl_gables=True),
                   "Hamlet: houses (longhouse with byre, plank walls)"),
    "cottage-03": (lambda: house(5.0, 3.0, 2.1, 2.75, overhang=0.3, mids=(), door_x=0.6, side_windows=True,
                                 woodpile=True),
                   "Hamlet: houses (small hut)"),
}

for name, (make, used) in VARIANTS.items():
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=500)))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, verified=True)

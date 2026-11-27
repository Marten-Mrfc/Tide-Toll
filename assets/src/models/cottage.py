# python3 assets/src/models/cottage.py  ->  assets/models/cottage-{a,b,c}{0,1,2}.glb and cottage-01/-02/-03.glb
# Night 3 (style sheet sections 1 to 5): three cottages, each in three sizes (plain, extended once, extended twice), each
# size a real model with its own silhouette from above; front door +Z (the game turns it to the lane).
#   a: hipped thatch longhouse; once: a lower hipped wing on the east end; twice: and a back wing to the west (an L)
#      with a second chimney.
#   b: gabled thatch, limewashed gables, a stack outside the west gable; once: a lower bay in line to the east (a long
#      house stepping down); twice: and a gabled cross wing to the front-west with a second chimney.
#   c: gable end to the lane (ridge front to back), door in the gable; once: a lower second range beside it (a double
#      pile, two ridges side by side); twice: and a cross range behind (a T) with a second chimney.
# Nodes: "house" (all but the door and shutters), "accent" (door and shutters, modelled in Cast-iron green: the game
# tints this node per plot), and an empty node "Smoke" at the top of every chimney ("Smoke2" for the second).
# Walls 2.6 m, roofs about 45 degrees with 0.4 m eaves, three thatch courses a slope (painted bands, n8), a ridge cap, timber corner posts,
# a stone plinth. cottage-01/02/03 are copies of a0/b0/c0 for the river cottages and the stand-ins.
import math
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))                          # _kit
from common import glb, ledger, asset_path
from _kit import beam

THATCH, COURSE, RIDGE, PATCH = "Reed and thatch", "Trail earth", "Packed earth", "Packed earth"
# n8 (style sheet section 3): thatch courses are painted bands lying 1 cm proud of the slope, no step for the ink to
# find; the game shades them as a darker tone of the thatch. (They were beams 0.2 x 0.09 m: two black stripes each.)
PROUD, CW, LIFT = 0.01, 0.2, 0.012
WALL, FRAME, PLINTH, PANE = "Limewash", "Mud", "Slate roof", "Mud"
ACCENT = "Cast-iron green"
EAVE = 0.4


def roof_frame(axis, cx, cz):
    """Map (u along the ridge, y, v across) to (x, y, z) for a ridge along X or Z centred at (cx, cz)."""
    if axis == "x":
        return lambda u, y, v: (cx + u, y, cz + v)
    return lambda u, y, v: (cx + v, y, cz + u)


def block(m, b):
    """One range of the house: walls, plinth, corner posts, roof with courses and ridge cap."""
    cx, cz, lx, lz, h, kind, rh = b["cx"], b["cz"], b["lx"], b["lz"], b["h"], b["roof"], b["rh"]
    m.add_box((cx, 0, cz), (lx + 0.16, 0.3, lz + 0.16), PLINTH)
    m.add_box((cx, 0.3, cz), (lx, h - 0.3, lz), WALL, bottom=False)
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.add_box((cx + sx * lx / 2, 0.3, cz + sz * lz / 2), (0.24, h - 0.3, 0.24), FRAME, bottom=False)
    axis = kind[-1]
    ln, sp = (lx, lz) if axis == "x" else (lz, lx)        # along the ridge, across it
    ext = b.get("ext", 0.0)                               # the roof runs on back into the main roof (a cross wing)
    rcx, rcz = (cx - ext / 2, cz) if axis == "x" else (cx, cz - ext / 2)
    ln += ext
    T = roof_frame(axis, rcx, rcz)
    half = sp / 2 + 0.15
    if kind.startswith("gable"):
        m.add_gable((rcx, h, rcz), ln + 0.5, sp + 0.3, rh, THATCH, gable=WALL, ridge_axis=axis, overhang=EAVE)
        end_at = lambda f: (ln + 0.5) / 2
    else:
        rf = 0.4
        if axis == "x":
            m.add_hip((cx, h, cz), ln + 0.3, sp + 0.3, rh, THATCH, ridge_frac=rf, overhang=EAVE)
        else:
            m.add_hip((cx, h, cz), ln + 0.3, sp + 0.3, rh, THATCH, ridge_frac=rf, overhang=EAVE, yaw=math.pi / 2)
        Le, r = (ln + 0.3) / 2 + EAVE, (ln + 0.3) * rf / 2
        end_at = lambda f: Le + f * (r - Le) - 0.12
    we = half + EAVE
    drop = EAVE * rh / half
    run, rise = we, rh + drop                              # one roof face: across v from the eave to the ridge
    fs = ((0.17, 0.37, 0.57, 0.77) if b.get("courses", 4) == 4 else (0.22, 0.47, 0.72)) if b.get("main") else (0.3, 0.62)
    for f in fs:                                          # thatch courses (four on the main range), on the slope, proud
        y = h - drop + f * rise + LIFT
        for s in (-1, 1):
            v = s * we * (1 - f)
            e = end_at(f) - 0.08
            nrm = T(0, run, s * rise)                      # the face's normal, in the frame's axes
            up = (nrm[0] - T(0, 0, 0)[0], nrm[1], nrm[2] - T(0, 0, 0)[2])
            beam(m, T(-e, y, v), T(e, y, v), CW, PROUD, COURSE, up=up, ends=False)
        if kind.startswith("hip") and b.get("main") and f < 0.5:   # and on the main range's two hipped ends
            for s in (-1, 1):
                u = s * (end_at(f) + 0.1)
                vv = we * (1 - f) - 0.25
                if vv > 0.2:
                    beam(m, T(u, y, -vv), T(u, y, vv), CW, PROUD, COURSE, ends=False)
    if b.get("patch"):                                     # a patch of older, darker thatch on the near face
        y = h - drop + 0.4 * rise + 0.08
        v = we * 0.6
        nrm = T(0, run, rise)
        up = (nrm[0] - T(0, 0, 0)[0], nrm[1], nrm[2] - T(0, 0, 0)[2])
        pu = b["patch"]
        beam(m, T(pu - 0.55, y, v), T(pu + 0.55, y, v), 0.7, 0.07, PATCH, up=up)
    e = end_at(1.0) + (0.1 if kind.startswith("gable") else 0.25)
    if b.get("main"):                                      # the main ridge sags a little in the middle
        sag = 0.04 * e
        beam(m, T(-e, h + rh, 0), T(0, h + rh - sag, 0), 0.42, 0.24, RIDGE)
        beam(m, T(0, h + rh - sag, 0), T(e, h + rh, 0), 0.42, 0.24, RIDGE)
    else:
        beam(m, T(-e, h + rh, 0), T(e, h + rh, 0), 0.42, 0.24, RIDGE)
    if kind.startswith("gable"):                          # barge boards along the gable edges
        for s in (-1, 1):
            u = s * ((ln + 0.5) / 2 + 0.04)
            for t in (-1, 1):
                beam(m, T(u, h - drop - 0.05, t * we), T(u, h + rh + 0.05, 0), 0.16, 0.18, FRAME)
    return h + rh


def door(acc, m, x, z, face=1, axis="z"):
    """Door 1 x 2 m on a wall facing +Z (face=1) or +X (axis='x'), with a dark frame and a lintel."""
    if axis == "z":
        acc.add_box((x, 0.3, z + face * 0.07), (1.0, 1.9, 0.14), ACCENT)
        m.add_box((x, 2.2, z + face * 0.09), (1.4, 0.2, 0.2), FRAME, bottom=FRAME)
        m.add_box((x, 0, z + face * 0.45), (1.2, 0.32, 0.6), PLINTH)                 # door step
    else:
        acc.add_box((x + face * 0.07, 0.3, z), (0.14, 1.9, 1.0), ACCENT)
        m.add_box((x + face * 0.09, 2.2, z), (0.2, 0.2, 1.4), FRAME, bottom=FRAME)


def window(acc, m, x, z, face=1, axis="z", y=1.05, flowers=False):
    """A dark window with a shutter either side (accent)."""
    if axis == "z":
        m.add_box((x, y, z + face * 0.04), (0.7, 0.75, 0.1), PANE, bottom=PANE)
        for s in (-1, 1):
            acc.add_box((x + s * 0.55, y - 0.05, z + face * 0.06), (0.36, 0.85, 0.1), ACCENT, bottom=ACCENT)
        m.add_box((x, y - 0.12, z + face * 0.12), (0.95, 0.12, 0.2), FRAME, bottom=FRAME)      # sill
        if flowers:                                       # a flower box under the window, in the accent colour
            acc.add_box((x, y - 0.42, z + face * 0.2), (0.8, 0.26, 0.3), ACCENT, bottom=ACCENT)
            for k, c in enumerate(("Brick", "Limewash")):
                m.add_box((x + (k - 0.5) * 0.3, y - 0.16, z + face * 0.2), (0.2, 0.16, 0.2), c, bottom=False)
    else:
        m.add_box((x + face * 0.04, y, z), (0.1, 0.75, 0.7), PANE, bottom=PANE)
        for s in (-1, 1):
            acc.add_box((x + face * 0.06, y - 0.05, z + s * 0.55), (0.1, 0.85, 0.36), ACCENT, bottom=ACCENT)
        m.add_box((x + face * 0.12, y - 0.12, z), (0.2, 0.12, 0.95), FRAME, bottom=FRAME)


def chimney(m, x, z, top, base=1.0, pot=True):
    """0.7 m square brick stack rising 1 m above `top` (the ridge), with a soot cap. Returns the Smoke point."""
    y1 = top + 1.0
    m.add_box((x, base, z), (0.7, y1 - base, 0.7), "Brick", bottom=False)
    m.add_box((x, y1 - 0.1, z), (0.98, 0.22, 0.98), "Soot brick", bottom="Soot brick")    # a cap that reads
    if pot:
        m.add_box((x, y1 + 0.12, z), (0.34, 0.24, 0.34), "Brick", bottom=False)            # and a pot
    return (x, y1 + (0.4 if pot else 0.16), z)


def build(variant, size):
    m, acc, smoke = glb.Mesh(), glb.Mesh(), []
    if variant == "a":
        top = block(m, dict(cx=0, cz=0, lx=5.6, lz=4.4, h=2.6, roof="hip-x", rh=2.45, patch=-0.9, main=True))
        door(acc, m, -0.4, 2.2)
        window(acc, m, 1.7, 2.2, flowers=True)
        window(acc, m, -0.4, -2.2, face=-1)
        window(acc, m, -2.8, 0, face=-1, axis="x")
        smoke.append(chimney(m, 0.9, -0.35, top))
        if size >= 1:
            block(m, dict(cx=4.1, cz=-0.3, lx=2.6, lz=3.6, h=2.1, roof="hip-z", rh=1.75))
            window(acc, m, 4.1, 1.5, y=0.95)
            window(acc, m, 5.4, -0.3, axis="x", y=0.95)
        if size >= 2:
            t2 = block(m, dict(cx=-1.5, cz=-3.9, lx=3.0, lz=3.6, h=2.3, roof="hip-z", rh=2.0))
            window(acc, m, -3.0, -4.2, face=-1, axis="x", y=1.0)
            window(acc, m, -1.5, -5.7, face=-1, y=1.0)
            smoke.append(chimney(m, -1.5, -5.0, t2 - 0.4, pot=False))
    elif variant == "b":
        top = block(m, dict(cx=0, cz=0, lx=5.8, lz=4.2, h=2.6, roof="gable-x", rh=2.55, patch=1.2, main=True))
        door(acc, m, 0.9, 2.1)
        window(acc, m, -1.2, 2.1, flowers=True)
        window(acc, m, 0.9, -2.1, face=-1)
        smoke.append(chimney(m, -3.3, 0, top, base=0.0))                               # stack outside the west gable
        if size >= 1:
            block(m, dict(cx=4.4, cz=-0.1, lx=3.0, lz=3.8, h=2.15, roof="gable-x", rh=2.1))
            window(acc, m, 4.4, 1.8, y=0.95)
        if size >= 2:
            t2 = block(m, dict(cx=-1.7, cz=3.6, lx=3.2, lz=3.0, h=2.3, roof="gable-z", rh=1.95, ext=1.2))
            window(acc, m, -1.7, 5.1, y=1.0)
            window(acc, m, -0.1, 3.7, axis="x", y=1.0)
            smoke.append(chimney(m, -1.7, 3.0, t2, pot=False))
    else:
        top = block(m, dict(cx=0, cz=0, lx=4.6, lz=5.6, h=2.6, roof="gable-z", rh=2.4, main=True, courses=3))
        door(acc, m, 0, 2.8)
        window(acc, m, 2.3, 0.4, axis="x")
        window(acc, m, -2.3, 0.4, face=-1, axis="x")
        window(acc, m, 0, 2.8, y=3.0)                                                  # a small window in the gable
        smoke.append(chimney(m, 0, -1.7, top))
        if size >= 1:
            block(m, dict(cx=3.7, cz=0.5, lx=2.8, lz=4.0, h=2.15, roof="gable-z", rh=1.75))
            beam(m, (2.5, 2.62, -1.7), (2.5, 2.62, 2.7), 0.36, 0.14, RIDGE)                 # valley board between the ranges
            window(acc, m, 3.7, 2.5, y=0.95)
        if size >= 2:
            t2 = block(m, dict(cx=0.7, cz=-4.4, lx=7.2, lz=3.2, h=2.3, roof="gable-x", rh=1.65))
            window(acc, m, 3.0, -6.0, face=-1, y=1.0)
            window(acc, m, -1.4, -6.0, face=-1, y=1.0)
            smoke.append(chimney(m, 3.3, -4.4, t2, pot=False))
    parts = [("house", m), ("accent", acc)]
    for k, p in enumerate(smoke):
        parts.append(("Smoke" if k == 0 else f"Smoke{k + 1}", None, None, p))
    return parts


SHAPES = {"a": "hipped longhouse", "b": "gabled, west stack", "c": "gable to the lane"}
SIZES = {0: "plain", 1: "extended once (a wing)", 2: "extended twice (an L or T, second chimney)"}
if __name__ == "__main__":
    for v in "abc":
        for size in range(3):
            out = asset_path(f"models/cottage-{v}{size}.glb")
            print(glb.report(out, glb.write_glb(out, build(v, size), max_tris=900)))
            ledger.add("models", out, kind="model", generator=__file__, verified=True,
                       used_in=f"Town: cottage {v} ({SHAPES[v]}), {SIZES[size]}; accent node tinted per plot")
    for k, v in enumerate("abc"):
        out = asset_path(f"models/cottage-0{k + 1}.glb")
        print(glb.report(out, glb.write_glb(out, build(v, 0), max_tris=900)))
        ledger.add("models", out, kind="model", generator=__file__, verified=True,
                   used_in=f"River cottages and stand-ins: cottage {v} plain")

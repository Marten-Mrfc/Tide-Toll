# python3 assets/src/models/_river_scene.py  ->  build/previews/drawbridge-lift-raised.glb, barge-mast-lowered.glb,
#   barge-empty.glb, river-scene.glb, river-scene-open.glb (previews only, not shipped; "_" = not run by
#   build-assets.sh). Poses the River set's moving nodes the way the game will (leaf and beam raised, chains moved
#   with the beam tip, signal arm vertical, barrier down, mast lowered) and places the set at true scale by a 6 m
#   river with the Hamlet pieces, to check they belong together and read from above. Render with:
#   tools/render-contact-sheet.sh build/previews/drawbridge-lift-raised.glb --sides --view=12 --focus_y=2.5
#   tools/render-contact-sheet.sh build/previews/river-scene.glb --view=44 --focus_y=1 --panel=720 \
#       --out=assets/src/contact-sheets/river-scene.png
import sys, pathlib, json, struct, math
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import glb, palette as P, ASSETS, ROOT

UV = {n: np.array(P.uv(n)) for n in P.NAMES}
OPEN = math.radians(80)


def rot(axis, a):
    c, s = math.cos(a), math.sin(a)
    if axis == "x":
        return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    if axis == "z":
        return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])
    return np.eye(3)


def load(name, pose=None, hide=()):
    """Read one of our .glb files into a glb.Mesh, applying node translations and an optional pose:
    pose = {node: (axis, angle) or ("move", (x, y, z))}. Colours are recovered from the swatch UVs."""
    pose = pose or {}
    b = (ASSETS / "models" / f"{name}.glb").read_bytes()
    jl = struct.unpack("<I", b[12:16])[0]
    js = json.loads(b[20:20 + jl])
    binb = b[20 + jl + 8:]

    def acc(i, n):
        a = js["accessors"][i]
        v = js["bufferViews"][a["bufferView"]]
        dt = {5126: np.float32, 5123: np.uint16, 5125: np.uint32}[a["componentType"]]
        return np.frombuffer(binb, dt, a["count"] * n, v["byteOffset"]).reshape(a["count"], n)
    m = glb.Mesh()
    for node in js["nodes"]:
        if node["name"] in hide:
            continue
        t = np.array(node.get("translation", [0, 0, 0]), float)
        R = np.eye(3)
        p = pose.get(node["name"])
        if p and p[0] == "move":
            t = np.array(p[1], float)
        elif p:
            R = rot(*p)
        pr = js["meshes"][node["mesh"]]["primitives"][0]
        pos = acc(pr["attributes"]["POSITION"], 3).astype(float) @ R.T + t
        uv = acc(pr["attributes"]["TEXCOORD_0"], 2)
        idx = acc(pr["indices"], 1).ravel()
        for k in range(0, len(idx), 3):
            i0, i1, i2 = idx[k:k + 3]
            col = min(UV, key=lambda n: float(np.sum((UV[n] - uv[i0]) ** 2)))
            m.tris.append((pos[i0], pos[i1], pos[i2], col))
    return m


def raised(a=OPEN):
    """The drawbridge-lift pose for leaf angle a (as the game should do it)."""
    return {"leaf": ("x", a), "beam": ("x", a), "signal": ("z", math.pi / 2),
            "chains": ("move", (0.0, 5.9 + 6 * math.sin(a), 2.0 - 6 * math.cos(a)))}


PREV = ROOT / "build" / "previews"
PREV.mkdir(parents=True, exist_ok=True)
for name, mesh in (("drawbridge-lift-raised", load("drawbridge-lift", raised())),
                   ("barge-mast-lowered", load("barge", {"mast": ("x", -math.radians(80))})),
                   ("barge-empty", load("barge", hide=("cargo",)))):
    out = PREV / f"{name}.glb"
    print(glb.report(out, glb.write_glb(out, mesh)))

# ---------------------------------------------------------------- scene: a 6 m river along X with the set
SOFT_NAMES = ("tree-", "field-", "reeds")
WATER_Y = 0.12
G = 0.01


def scene(open_bridge):
    S, SOFT = glb.Mesh(), glb.Mesh()

    def put(name, x, z, yaw=0.0, y=0.0, pose=None, hide=()):
        (SOFT if name.startswith(SOFT_NAMES) else S).append(load(name, pose, hide), (x, y, z), yaw)

    def quad(x0, z0, x1, z1, y, col):
        S.add_poly([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], col)
    quad(-22, 3, 22, 16, G, "Meadow")
    quad(-22, -16, 22, -3, G, "Meadow")
    quad(-22, -3, 22, 3, WATER_Y, "River")
    quad(-22, -1.2, 22, 1.2, WATER_Y + 0.01, "Deep water")
    for (xa, za), (xb, zb), w in (((0, 5), (0, 14), 1.8), ((0, -5), (0, -14), 1.8), ((-16, 4), (-10, 12), 1.2),
                                  ((13, 6.5), (8, 12), 1.2)):
        d = np.array([xb - xa, zb - za], float); d /= np.linalg.norm(d); n = np.array([-d[1], d[0]]) * w / 2
        S.add_poly([(xa + n[0], 0.03, za + n[1]), (xb + n[0], 0.03, zb + n[1]), (xb - n[0], 0.03, zb - n[1]),
                    (xa - n[0], 0.03, za - n[1])], "Trail earth")
    # drawbridge across the river at x = 0 (crossing along Z): the leaf spans the river (z = -3..+3), the pier
    # stands on the south bank, ramps at z = +6 and -4
    pose = raised() if open_bridge else None
    put("drawbridge-end", 0, 6.0, pose=None if open_bridge else {"barrier": ("z", math.radians(80))})
    put("drawbridge-lift", 0, 1.0, pose=pose)
    put("drawbridge-end", 0, -4.0, math.pi, pose=None if open_bridge else {"barrier": ("z", math.radians(80))})
    put("keeper-hut", 5.0, 7.0, math.pi / 2)
    # ferry at x = -14: posts on both banks, the rope between them, the raft halfway
    put("ferry-post", -16.05, 3.4)
    put("ferry-post", -16.05, -3.4, math.pi)
    rope = load("ferry-rope")
    L = 2 * (3.4 - 0.3)
    at = np.array([-16.05, 1.3, 3.1])                    # near post's rope point; scale the piece by L along Z
    S.tris += [(a * [1, 1, L] + at, b * [1, 1, L] + at, c * [1, 1, L] + at, col) for a, b, c, col in rope.tris]
    put("ferry-raft", -18.0, -0.6, y=WATER_Y)
    # landing at x = 13 on the south bank, jetty reaching into the river, a rowboat moored to it
    put("landing", 13.0, 3.0 + 0.0, 0.0)
    put("rowboat", 15.6, 1.4, math.pi / 2, y=WATER_Y)
    # the barge: waiting west of the bridge when closed, passing under when open
    put("barge", -8.0 if not open_bridge else 0.0, -0.6, math.pi / 2, y=WATER_Y)
    for x, z in ((8.5, -3.6), (-8.0, 3.4), (19.0, -3.5)):
        put("reeds", x, z)
    put("tree-willow", -6.5, 6.5); put("tree-oak", 18, 11); put("cottage-02", -15.0, 11.5, math.pi)
    vg = load("villager"); grain = load("villager-cargo-grain")
    for x, z, yaw, carry in ((0.5, 8.5, math.pi, True), (-0.6, 10.5, math.pi, False), (12.6, 1.2, 0.0, True)):
        S.append(vg, (x, 0.0 if z > 3 else 0.5, z), yaw)
        if carry:
            S.append(grain, (x, (0.0 if z > 3 else 0.5) + 2.6, z), yaw)
    return [("river", S), ("river-soft-ink", SOFT, "soft")]


for name, o in (("river-scene", False), ("river-scene-open", True)):
    out = PREV / f"{name}.glb"
    print(glb.report(out, glb.write_glb(out, scene(o))))

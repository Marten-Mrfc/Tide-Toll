# python3 assets/src/models/_scene.py  ->  build/previews/hamlet-scene.glb (preview only, not shipped; "_" = not run
# by build-assets.sh). Places the finished Hamlet models together at true scale on a meadow by a river, to check
# that they belong together. Render it with:
#   tools/render-contact-sheet.sh build/previews/hamlet-scene.glb --view=50 --focus_y=1 --panel=720 --out=assets/src/contact-sheets/hamlet-scene.png
import sys, pathlib, json, struct, math
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import glb, palette as P, ASSETS, ROOT

UV = {n: np.array(P.uv(n)) for n in P.NAMES}


def load(name):
    """Read one of our .glb files back into a glb.Mesh (all nodes merged; colour recovered from the swatch UV)."""
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
    for mesh in js["meshes"]:
        pr = mesh["primitives"][0]
        pos = acc(pr["attributes"]["POSITION"], 3).astype(float)
        uv = acc(pr["attributes"]["TEXCOORD_0"], 2)
        idx = acc(pr["indices"], 1).ravel()
        for t in range(0, len(idx), 3):
            i0, i1, i2 = idx[t:t + 3]
            col = min(UV, key=lambda n: float(np.sum((UV[n] - uv[i0]) ** 2)))
            m.tris.append((pos[i0], pos[i1], pos[i2], col))
    return m


S = glb.Mesh()


def put(name, x, z, yaw=0.0, y=0.0):
    S.append(load(name), (x, y, z), yaw)


def quad(x0, z0, x1, z1, y, col):
    S.add_poly([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], col)


# ground: meadow south of the river (z > -6), north bank beyond it, river (3 cells wide) between
WATER_Y = 0.12
G = 0.01   # a hair above the contact sheet's own paper ground plane, which sits at y = 0
quad(-24, -6, 24, 27, G, "Meadow")
quad(-24, -21, 24, -12, G, "Meadow")
quad(-24, -12, 24, -6, WATER_Y, "River")
quad(-24, -10.2, 24, -7.8, WATER_Y + 0.01, "Deep water")
# worn trails (flat decals): bridge -> well -> stall, and to the ford
for (xa, za), (xb, zb), w in ((((-8, -5), (0, 0), 1.6)), (((0, 0), (9, -5), 1.4)), (((0, 0), (-6, 6), 1.2))):
    d = np.array([xb - xa, zb - za], float); d /= np.linalg.norm(d); n = np.array([-d[1], d[0]]) * w / 2
    S.add_poly([(xa + n[0], 0.03, za + n[1]), (xb + n[0], 0.03, zb + n[1]), (xb - n[0], 0.03, zb - n[1]),
                (xa - n[0], 0.03, za - n[1])], "Trail earth")

# footbridge over the river at x = -8: bank landings on both sides, three 2 m spans
put("footbridge-end", -8, -5)
for z in (-7, -9, -11):
    put("footbridge-span", -8, z)
put("footbridge-end", -8, -13, math.pi)
# ford at x = 10: stepping stones in three cells
for z in (-7, -9, -11):
    put("ford-stones", 10, z, y=0.0)
put("rowboat", 2, -9, math.pi / 2, y=WATER_Y)
for x, z in ((14.5, -5.6), (16.5, -5.8), (-12.5, -5.5), (-3.5, -12.6), (-1.5, -12.8)):
    put("reeds", x, z)

# houses, well, stall
put("cottage-01", -7, 5)
put("cottage-02", 6, 6, math.pi)
put("cottage-03", -16, 4, math.pi / 2)
put("well", 0, 0)
put("market-stall", 5, -1.5, 0.0)
# field with a wattle fence along its south edge, a gate and a corner post
put("field-ripe", 8, 18)
put("field-sprouting", -8, 18)
for x in (1, 3, 5, 9, 11, 13, 15):
    put("fence-wattle", x, 10)
put("fence-gate", 7, 10)
put("fence-post", 16, 10)
for z in (11, 13):
    put("fence-wattle", 0, z, math.pi / 2)
# trees
put("tree-oak", -18, -2); put("tree-oak", -2, 12.5); put("tree-willow", -12, -4.2); put("tree-willow", 13, -4.0)
put("tree-poplar", 18, 3); put("tree-poplar", 18, 7)
# villagers, one carrying grain
vg = load("villager"); grain = load("villager-cargo-grain")
for x, z, yaw, y, carry in ((-2.5, 1.5, 0.6, 0.0, True), (-8, -9, 0.0, 0.6, False), (10.1, -9, math.pi, 0.3, True),
                            (4.5, 1.6, math.pi, 0.0, False), (-5.5, 0.0, -2.2, 0.0, False)):
    S.append(vg, (x, y, z), yaw)
    if carry:
        S.append(grain, (x, y + 2.6, z), yaw)

out = ROOT / "build" / "previews" / "hamlet-scene.glb"
print(glb.report(out, glb.write_glb(out, S)))

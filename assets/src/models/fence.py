# python3 assets/src/models/fence.py  ->  assets/models/fence-wattle.glb, fence-post.glb, fence-gate.glb
# Hamlet fences are woven wattle (Art tab). Pieces snap to cell edges: the origin is the middle of the cell edge
# on the ground, the run is along X, and a 2 m segment has its posts centred on the two cell corners (x = -1, +1),
# so neighbouring segments share a post position (the coincident posts are identical, so nothing flickers).
#   fence-wattle  2 m straight segment: two posts and a woven panel (3 x 3 checker of withy sections), 1.0 m high
#   fence-post    a single corner or end post (stand it on a cell corner where a run turns or stops)
#   fence-gate    2 m hurdle gate (Hamlet gate): two taller posts and a braced timber hurdle, 1.2 m high
# Front faces +Z; the pieces look the same from both sides apart from the gate brace.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam

POST = "Mud"


def post(m, x, h=1.0, s=0.22):
    m.add_box((x, 0, 0), (s, h - 0.12, s), POST)
    m.add_frustum((x, h - 0.12, 0), s * 0.72, s * 0.3, 0.12, POST, segments=4, cap_bottom=False)  # trimmed top


def wattle():
    m = glb.Mesh()
    for x in (-1.0, 1.0):
        post(m, x)
    # woven panel: 3 bands x 3 sections in a checker, each section pushed to the front or the back of the stakes,
    # so the ink pass and the two wood tones draw the over-and-under weave
    for i, y in enumerate((0.1, 0.37, 0.64)):
        for j, x in enumerate((-0.6, 0.0, 0.6)):
            k = (i + j) % 2
            col = "Trail earth" if k else "Packed earth"
            sides = {"+z": col, "-z": col, "+y": col}
            if i == 0:
                sides["-y"] = col
            m.add_box((x, y, 0.04 if k else -0.04), (0.62, 0.27, 0.12), sides)
    return m


def corner_post():
    m = glb.Mesh()
    post(m, 0.0, h=1.1, s=0.26)
    return m


def gate():
    m = glb.Mesh()
    for x in (-1.0, 1.0):
        post(m, x, h=1.25, s=0.24)
    w, c = 0.11, "Trail earth"
    xa, xb = -0.84, 0.84
    beam(m, (xa, 0.25, 0), (xb, 0.25, 0), w, w, c)            # bottom rail
    beam(m, (xa, 1.02, 0), (xb, 1.02, 0), w, w, c)            # top rail
    beam(m, (xa, 0.62, 0), (xb, 0.62, 0), w * 0.8, w * 0.8, c)  # middle rail
    for x in (xa, 0.0, xb):
        beam(m, (x, 0.18, 0.0), (x, 1.09, 0.0), w, w, c)      # uprights
    beam(m, (xa, 0.25, 0.07), (xb, 1.02, 0.07), w, w, "Packed earth")   # diagonal brace, on the front face
    return m


for name, make, budget, used in (("fence-wattle", wattle, 100, "Hamlet: fence, straight 2 m segment (woven wattle)"),
                                 ("fence-post", corner_post, 100, "Hamlet: fence, corner or end post"),
                                 ("fence-gate", gate, 150, "Hamlet: gate (hurdle gate, 2 m)")):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=budget)))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, verified=True)

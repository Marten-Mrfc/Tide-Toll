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
STAKE = "Packed earth"


def post(m, x, h=1.0, s=0.22, colour=POST):
    m.add_box((x, 0, 0), (s, h - 0.12, s), colour)
    m.add_frustum((x, h - 0.12, 0), s * 0.72, s * 0.3, 0.12, colour, segments=4, cap_bottom=False)  # trimmed top


def wattle():
    """Night 3 (round 2, BK fix): five upright withies 0.18 m thick (Trail earth, 0.12 m of air between them) woven
    over and under three horizontal Packed earth rails between two Mud posts: the withies stand straight, one in
    front of the rails and the next behind them (after the pictures: bent withies read as dangling zigzags), so it
    reads as a woven hurdle with the light through it and not as a crumpled strip or a gate. 1.1 m."""
    m = glb.Mesh()
    for x in (-1.0, 1.0):
        post(m, x, h=1.12, s=0.22, colour=POST)
    for y in (0.3, 0.62, 0.94):
        beam(m, (-0.92, y, 0), (0.92, y, 0), 0.1, 0.1, STAKE, ends=False)              # rails
    for i, x in enumerate((-0.6, -0.3, 0.0, 0.3, 0.6)):
        s = 0.075 if i % 2 == 0 else -0.075                                            # in front, behind, in front...
        beam(m, (x, 0.0, s), (x, 1.04, s), 0.18, 0.1, "Trail earth", ends=True)
    return m


def corner_post():
    """Night 3 (round 2, BK fix): a 0.22 m Mud post leaning 4 degrees on a 0.3 m Packed earth foot, with a 0.3 m
    square Paper cap (a fresh-cut board), so the post shows as a pale dot from above and is not mistaken for the
    wattle's end."""
    m = glb.Mesh()
    lean = 0.07
    m.add_box((0, 0, 0), (0.32, 0.16, 0.32), STAKE)                                    # foot
    beam(m, (0, 0.1, 0), (lean, 1.0, 0), 0.22, 0.22, POST)
    m.add_box((lean, 1.0, 0), (0.32, 0.09, 0.32), "Paper", bottom=POST)                # cap
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


for name, make, budget, used in (("fence-wattle", wattle, 150, "Hamlet: fence, straight 2 m segment (woven wattle)"),
                                 ("fence-post", corner_post, 100, "Hamlet: fence, corner or end post"),
                                 ("fence-gate", gate, 150, "Hamlet: gate (hurdle gate, 2 m)")):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=budget)))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, verified=True)

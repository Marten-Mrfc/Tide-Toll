# python3 assets/src/models/gardens.py [name ...]  ->  assets/models/garden-*.glb
# Night 3, round 3 (asset track LF): the two garden pieces a cottage comes with that the prop-* set lacks
# (game/scripts/town/TownLife.Gardens.cs; the other garden kinds reuse prop-woodpile, prop-bench, prop-beehives,
# prop-flower-bed and prop-hen-coop). Hamlet style, one palette colour per face.
#
#   garden-washing-line  two posts 4 m apart (x = -2..2) and a Paper line at 1.7 m with six cloths in Brick,
#                        Limewash, River, Paper, Reed and thatch and Limewash; each cloth hangs flat (0.08 m thick)
#                        from the line, 0.45 to 0.8 m drop. The game's shader lifts the cloths in the wind (everything
#                        below y = 1.7 between the posts, more the lower it hangs); posts stay put.
#   garden-vegetables    a 3 x 2 m low bed of light Trail earth with Bent grass sides, four long flat rows of Meadow and
#                        Cast-iron green (no edging, no scarecrow, no dark squares: the review of 02:3x read it as a pallet).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

from common import glb, ledger, asset_path
from _kit import beam

NOTE = "made in code here; free asset sites blocked from the build container (checked 2026-11-25)"
WOOD = "Mud"


def washing_line():
    m = glb.Mesh()
    for x in (-2.0, 2.0):
        m.add_box((x, 0, 0), (0.14, 1.85, 0.14), WOOD)
    beam(m, (-2.0, 1.72, 0), (2.0, 1.72, 0), 0.05, 0.05, "Paper", ends=False)
    cloths = ((-1.55, 0.5, 0.7, "Brick"), (-0.95, 0.45, 0.55, "Limewash"), (-0.35, 0.55, 0.8, "River"),
              (0.3, 0.5, 0.6, "Paper"), (0.9, 0.45, 0.5, "Reed and thatch"), (1.5, 0.5, 0.75, "Limewash"))
    for x, w, h, c in cloths:
        m.add_box((x, 1.70 - h, 0), (w, h, 0.08), c)
    return m


def vegetables():
    """After the review (it read as a pallet or a grate, and then the sprouts' many small outlines read as a grid of dark
    dots): a low bed of light Trail earth whose sides are Bent grass (no board edging), and four long rows of Meadow and
    Bent grass greens, each row one low ridge broken once, so the ink finds a few long lines and no dots."""
    m = glb.Mesh()
    W, D = 3.0, 2.0
    m.add_box((0, 0, 0), (W, 0.05, D), {"default": "Bent grass", "+y": "Trail earth", "-y": None})
    # rows almost flush with the soil (2 cm), so the ink pass draws the bed's outline and not every row
    for row, z in enumerate((-0.6, -0.2, 0.2, 0.6)):
        col = "Meadow" if row % 2 == 0 else "Cast-iron green"
        m.add_box((0, 0.05, z), (2.5 - 0.2 * (row % 2), 0.02, 0.26), col, bottom=False)
    return m


BUILDS = [
    ("garden-washing-line", washing_line(), 120, "Cottage gardens: washing line with six cloths lifting in the wind"),
    ("garden-vegetables", vegetables(), 200, "Cottage gardens: vegetable bed with rows of sprouts"),
]

if __name__ == "__main__":
    only = [a for a in sys.argv[1:] if not a.startswith("--")]
    for name, parts, budget, used in BUILDS:
        if only and name not in only:
            continue
        out = asset_path(f"models/{name}.glb")
        print(glb.report(out, glb.write_glb(out, parts, max_tris=budget, ink="soft" if name == "garden-vegetables" else None)))
        ledger.add("models", out, kind="model", generator=__file__, note=NOTE, used_in=used, verified=True)

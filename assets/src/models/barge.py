# python3 assets/src/models/barge.py  ->  assets/models/barge.glb
# River set: the masted sailing barge (Art tab: "Masted boat: hull with a sail triangle and pennant; the sail mark
# means cannot pass a low bridge"; Gameplay tab: sailing barge, mast class 2, may lower its mast to pass one class
# lower). Drawn 1.5x life size like the rowboat, and clearly bigger: 12 m long (Z, bow to +Z) x 4.4 m across the
# leeboards (hull 4 m), gunwale 1.3 m above the water; y = 0 is the waterline, the flat bottom 0.5 m below it.
# A bluff Dutch-style hull in dark timber (Soot brick) with a Cast-iron green gunwale band, leeboards on both sides,
# an open hold amidships with a coaming, a little limewashed deckhouse aft and a big rudder with a tiller.
# Three nodes, so the game can animate and load it:
#   hull   hull, deck, hold, deckhouse, rudder, leeboards (static)
#   mast   pivot at the mast step on the deck, node translation (0, 1.15, 2.6). Local +Y is up the mast. To lower
#          the mast aft (passing one class lower) rotate the node about X by -80 degrees (top goes to -Z).
#          Mast 8.4 m above the deck (9.55 m above the water); a yard across the boat at 7.2 m carries the sail
#          furled into a fat Limewash roll 5.2 m wide, so from straight above the barge shows a white cross-bar
#          over the hull (the "sail mark"); a Brick pennant at the top points aft.
#   cargo  goods in the hold: grain sacks, crates, timber, a wool bale. Hide the node for an empty barge.
# Round n3 (track RV, 2026-11-26): the hull is no longer a dark smudge: Packed earth sides with a Brick top strake
# and a Limewash boot stripe at the waterline, Paper caps on the bollards. The cargo node is gone from the file:
# the game shows the barge's cargo itself, in the good's own load shape and colour (RiverBoats), so an empty barge
# is empty and a full one shows what it carries.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import beam, tube, plate, hull, cap
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

GUN, KEEL, DECK = 1.3, -0.5, 1.15
MAST_Z = 2.6
HALF = [(0.0, 6.0), (1.05, 5.65), (1.7, 4.8), (2.0, 3.4), (2.0, -3.2), (1.85, -4.9), (1.35, -5.8)]
HULL, BAND, DECKC, DARK = "Packed earth", "Brick", "Trail earth", "Mud"
import math
from _kit import loft


def hull_node():
    m = glb.Mesh()
    hull(m, HALF, GUN, KEEL, 0.78, 0.86, dict(side=HULL, bottom=DARK, band=BAND, deck=DECKC), inner=(0.92, 0.96),
         deck=DECK, dz_keel=-0.1)
    # boot stripe: a Limewash band round the hull just above the waterline, standing 3 cm proud
    out = HALF + [(-x, z) for x, z in reversed(HALF[1:])]
    out = [p for i, p in enumerate(out) if i == 0 or p != out[i - 1]]
    if out[-1] == out[0]:
        out = out[:-1]

    def ring(y, grow):
        t = (y - KEEL) / (GUN - KEEL)
        sx, sz, dz = 0.78 + 0.22 * t, 0.86 + 0.14 * t, -0.1 * (1 - t)
        return [(x * sx * grow + (0.03 if x > 0 else -0.03 if x < 0 else 0), y, z * sz * grow + dz) for x, z in out]
    loft(m, ring(0.12, 1.0), ring(0.42, 1.0), "Limewash")
    # hold amidships: dark floor patch with a coaming round it (cargo sits on the floor at DECK)
    m.add_box((0, DECK, -0.6), (2.7, 0.02, 4.6), DARK, bottom=False)
    for x in (-1.45, 1.45):
        m.add_box((x, DECK, -0.6), (0.2, 0.3, 4.8), "Packed earth", bottom=False)
    for z in (-3.0, 1.8):
        m.add_box((0, DECK, z), (3.1, 0.3, 0.2), "Packed earth", bottom=False)
    # mast step / tabernacle (two cheeks either side of the mast foot)
    for x in (-0.3, 0.3):
        m.add_box((x, DECK, MAST_Z), (0.16, 0.9, 0.5), DARK, bottom=False)
    # deckhouse aft: limewashed walls, a door facing forward, a curved-looking low roof (two shallow slopes)
    m.add_box((0, DECK, -4.3), (2.2, 1.0, 1.6), "Limewash", bottom=False)
    m.add_box((0, DECK, -3.47), (0.7, 0.8, 0.1), DARK, bottom=False)                     # door
    for x in (-1.13, 1.13):
        m.add_box((x, DECK + 0.45, -4.3), (0.1, 0.35, 0.45), DARK, bottom=False)         # side windows
    m.add_gable((0, DECK + 1.0, -4.3), 1.9, 2.4, 0.4, "Slate roof", gable="Limewash", ridge_axis="z")
    # rudder hung on the transom, with a long tiller over the deckhouse roof
    plate(m, [(-0.4, -5.75), (1.35, -5.75), (1.35, -6.25), (0.2, -6.7), (-0.4, -6.6)], 0, 0.22, DARK)
    beam(m, (0, 1.75, -6.2), (0, 2.05, -4.6), 0.16, 0.16, "Packed earth")
    # leeboards: fan-shaped boards hung outside the hull at midships (Dutch signature), drawn hauled up (moored)
    for sx in (-1, 1):
        x = sx * 2.1
        plate(m, [(1.4, 0.25), (1.4, -0.45), (0.05, -1.35), (0.05, 1.05)], x, 0.18, BAND, edge=DARK)
        m.add_box((sx * 2.05, GUN - 0.05, -0.1), (0.35, 0.2, 0.35), DARK, bottom=DARK)   # pivot block
    # bollards at the bow and stern for the mooring lines
    for x, z in ((-1.2, 4.9), (1.2, 4.9)):
        m.add_box((x, DECK, z), (0.3, 0.4, 0.3), DARK, bottom=False)
        m.add_box((x, DECK + 0.4, z), (0.36, 0.08, 0.36), "Paper", bottom="Paper")
    m.add_box((0, GUN - 0.1, 5.8), (0.3, 0.45, 0.4), DARK, bottom=False)                # stem head
    return m


def mast_node():
    """Local frame: origin at the mast foot on the deck, +Y up the mast."""
    m = glb.Mesh()
    m.add_frustum((0, 0, 0), 0.17, 0.11, 8.4, "Packed earth", segments=6, cap_bottom=False)
    m.add_cylinder((0, 8.4, 0), 0.16, 0.14, "Brick", segments=6)                         # truck (mast cap)
    # yard across the boat with the sail furled into a fat roll round it (the yard ends poke out)
    tube(m, (-2.75, 7.0, 0.12), (2.75, 7.0, 0.12), 0.1, DARK, n=4)
    tube(m, (-2.45, 7.0, 0.12), (2.45, 7.0, 0.12), 0.42, "Limewash", n=6)
    for x in (-1.2, 1.2):                                                                # gaskets (ties)
        tube(m, (x - 0.09, 7.0, 0.12), (x + 0.09, 7.0, 0.12), 0.45, DARK, n=6, ends=False)
    # pennant: a long triangle flying aft from the top, a little thick so it shows from above
    plate(m, [(8.25, -0.05), (7.85, -0.05), (8.08, -1.7)], 0, 0.2, "Brick")
    return m


def cargo_node():
    m = glb.Mesh()
    y = DECK + 0.02
    for x, z in ((-0.6, 0.95), (0.4, 1.0), (-0.1, 0.25), (0.75, 0.3)):                  # grain sacks
        m.add_sphere((x, y + 0.34, z), 0.42, "Trail earth", rings=2, segments=5, squash=0.8)
    for x, z, h in ((-0.65, -0.9, 0.9), (0.65, -0.9, 0.7)):                             # crates
        m.add_box((x, y, z), (1.0, h, 1.0), "Packed earth", bottom=False)
        m.add_box((x, y + h, z), (1.04, 0.08, 0.3), DARK, bottom=DARK)                   # lid batten
    for dz in (-0.22, 0.22):                                                             # timber baulks
        beam(m, (-1.1, y + 0.12, -2.25 + dz), (1.1, y + 0.12, -2.25 + dz), 0.24, 0.24, "Packed earth")
    beam(m, (-1.1, y + 0.36, -2.25), (1.1, y + 0.36, -2.25), 0.24, 0.24, "Packed earth")
    m.add_box((0.0, y + 0.9, -0.9), (0.6, 0.5, 0.6), "Paper", bottom="Paper")           # wool bale on the crate
    return m


parts = [("hull", hull_node()), ("mast", mast_node(), None, (0.0, DECK, MAST_Z))]
for n, mm, *_ in parts:
    print(f"  {n}: {mm.tri_count()} tris")
out = asset_path("models/barge.glb")
print(glb.report(out, glb.write_glb(out, parts, max_tris=600)))
ledger.add("models", out, kind="model", generator=__file__, note=BLOCKED, verified=True,
           used_in="River: masted sailing barge (nodes hull, mast; the cargo is drawn by the game in the good's shape)")

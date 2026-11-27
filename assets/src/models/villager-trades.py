# python3 assets/src/models/villager-trades.py  ->  assets/models/villager-head-kerchief.glb, villager-head-strawhat.glb,
#   villager-head-cap.glb, villager-apron.glb
# n7 step 2 (the trades, docs/plans/n7/trades-design.md): one headwear per trade, each one flat colour that reads from
# above at the normal view, made exactly as villager.py makes its heads (same face, same 1.4x drawn size, front +Z,
# node "head"); the miller's apron is in the BODY's frame (origin at the feet), as the cargo files are.
#   water carrier: a blue kerchief (River) tied tight over the crown, knot and two tails at the back
#   reaper: a wide straw hat (brim 0.70 drawn, against the Hand's 0.48), low cone crown, dark band
#   miller: a white flat cap (Paper, a stiff round top with a short peak) and a dusty apron (Limewash, flour patches)
# Mock-up step: the files exist for the board (docs/plans/n7/mockup/); the game does not load them yet.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from common import glb, ledger, asset_path
from _kit import ngon, loft, beam

SKIN, HAIR = "Trail earth", "Mud"


def face(m):
    """The face of villager.py's heads (same place, same size)."""
    m.add_sphere((0, 2.16, 0.03), 0.36, SKIN, rings=3, segments=8)
    m.add_box((0, 2.08, 0.37), (0.1, 0.12, 0.08), SKIN, bottom=SKIN)


def head_kerchief():
    m = glb.Mesh()
    face(m)
    # The cloth over the crown and down the back of the head, tight: a squashed dome a little larger than the hair.
    m.add_sphere((0, 2.27, -0.05), 0.395, "River", rings=3, segments=8, squash=0.8)
    # The hem across the brow, a band of the shade colour 1 cm proud (painted, not built).
    loft(m, ngon(0, -0.02, 0.385, 8, y=2.2), ngon(0, -0.03, 0.375, 8, y=2.27), "Deep water", centre=(0, 2.24, -0.03))
    # The knot at the back and two tails hanging to the shoulders.
    m.add_sphere((0, 2.24, -0.43), 0.11, "River", rings=2, segments=6)
    beam(m, (-0.04, 2.2, -0.45), (-0.13, 1.86, -0.5), 0.13, 0.05, "River")
    beam(m, (0.05, 2.2, -0.45), (0.12, 1.9, -0.47), 0.12, 0.05, "River")
    return m


def head_strawhat():
    m = glb.Mesh()
    face(m)
    m.add_sphere((0, 2.24, -0.08), 0.37, HAIR, rings=2, segments=8, squash=0.7)
    # The wide brim: 0.70 drawn (0.5 m life size), sloping down to its edge like a reaper's sun hat.
    m.add_frustum((0, 2.36, 0), 0.7, 0.45, 0.08, "Reed and thatch", segments=12)
    m.add_frustum((0, 2.345, 0), 0.69, 0.69, 0.015, "Packed earth", segments=12, cap_top=False)   # shade under the brim
    m.add_frustum((0, 2.44, 0), 0.36, 0.1, 0.3, "Reed and thatch", segments=10)                   # low cone crown
    m.add_frustum((0, 2.44, 0), 0.37, 0.31, 0.07, "Mud", segments=10, cap_top=False)               # band
    return m


def head_cap():
    m = glb.Mesh()
    face(m)
    m.add_sphere((0, 2.22, -0.1), 0.37, HAIR, rings=2, segments=8, squash=0.7)
    # A flat cap: a deep band round the head, then a stiff round top a little wider, tipped forward; a short peak.
    # From above: a flat white disc ringed by a dark band, the peak pointing forward (never a dome, as the pale hood is).
    m.add_frustum((0, 2.3, -0.02), 0.39, 0.4, 0.09, "Packed earth", segments=10, cap_top=False)
    m.add_frustum((0, 2.39, 0.02), 0.41, 0.5, 0.05, "Paper", segments=10, cap_top=False)
    m.add_frustum((0, 2.44, 0.02), 0.5, 0.48, 0.03, "Paper", segments=10)
    m.add_box((0, 2.32, 0.46), (0.48, 0.04, 0.24), "Limewash", bottom="Packed earth")   # the peak over the brow
    return m


def apron():
    """The miller's apron, in the body's frame: a bib from the collar to below the knees over the tunic's front,
    Limewash, with flour patches (Paper) 1 cm proud, a dark tie at the waist and a strap round the neck."""
    m = glb.Mesh()
    beam(m, (0, 0.42, 0.38), (0, 1.02, 0.355), 0.56, 0.03, "Limewash", up=(0, 0, 1))       # skirt, below the belt
    beam(m, (0, 1.02, 0.355), (0, 1.56, 0.285), 0.4, 0.03, "Limewash", up=(0, 0, 1))      # bib, up to the chest
    beam(m, (-0.36, 1.06, 0.33), (0.36, 1.06, 0.33), 0.06, 0.05, "Packed earth", up=(0, 0, 1))   # waist tie
    beam(m, (-0.14, 1.56, 0.29), (-0.17, 1.7, 0.12), 0.05, 0.04, "Limewash")   # neck straps
    beam(m, (0.14, 1.56, 0.29), (0.17, 1.7, 0.12), 0.05, 0.04, "Limewash")
    for (x, y0, y1, w) in ((-0.12, 0.55, 0.78, 0.18), (0.13, 0.7, 0.86, 0.14), (0.04, 1.2, 1.36, 0.16)):   # dust
        z0 = 0.38 - (y0 - 0.42) * 0.04
        z1 = 0.38 - (y1 - 0.42) * 0.04
        if y0 > 1.02:
            z0, z1 = 0.355 - (y0 - 1.02) * 0.13, 0.355 - (y1 - 1.02) * 0.13
        beam(m, (x, y0, z0 + 0.02), (x, y1, z1 + 0.02), w, 0.01, "Paper", up=(0, 0, 1))
    return m


def write(name, mesh, budget, used, node="head"):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, [(node, mesh)], max_tris=budget)))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, verified=True)


write("villager-head-kerchief", head_kerchief(), 220, "Villagers, trades (n7 step 2, mock-up): the water carrier's blue kerchief")
write("villager-head-strawhat", head_strawhat(), 260, "Villagers, trades (n7 step 2, mock-up): the reaper's wide straw hat")
write("villager-head-cap", head_cap(), 220, "Villagers, trades (n7 step 2, mock-up): the miller's white flat cap")
write("villager-apron", apron(), 120, "Villagers, trades (n7 step 2, mock-up): the miller's dusty apron, in the body's frame", node="apron")

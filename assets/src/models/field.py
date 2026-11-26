# python3 assets/src/models/field.py  ->  assets/models/field-sprouting.glb, field-ripe.glb, field-stubble.glb
# Hamlet crop plot, 16 x 16 m (8 x 8 cells), crops under 0.6 m. Seasons (Art tab): spring "sprouting rows",
# summer "golden fields", autumn "ploughed strips, stubble". +Z is the front by convention.
# Night 3 rework (style sheet: "a field looks like a pallet of planks"): no plank frame. Each field is a soft,
# uneven patch of ploughed earth (a jittered sixteen-sided outline, a low Bent grass headland round it), its furrows
# clipped to that outline so the rows end raggedly; the crop is never one bar per row:
#   field-sprouting  dark furrows (Packed earth) with broken rows of green shoots (Meadow, a few Bent grass)
#   field-ripe       a golden sea of grain (Reed and thatch, uneven heights) with a cut strip at the front holding
#                    three stooks, so it reads as grain being harvested
#   field-stubble    pale stubble rows with sheaves lying and five stooks standing
# Written with ink="soft" (only the silhouette and big creases are inked). Deterministic (seed 7).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import random
from common import glb, ledger, asset_path
from _kit import hull2d, beam

SEED = 7
SIZE, ROWS, PITCH = 16.0, 9, 1.6
ROW_Z = [(-(ROWS - 1) / 2 + i) * PITCH for i in range(ROWS)]


def outline(rng, r, n=16, amount=0.05):
    pts = []
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        # a rounded square: |cos|^p blend so the patch fills its plot but has no straight frame
        c, s = math.cos(a), math.sin(a)
        k = 1.0 / max(abs(c), abs(s)) ** 0.75
        rr = r * k * (1 + amount * (rng.random() * 2 - 1))
        pts.append((rr * c, rr * s))
    return hull2d(pts)


def span(poly, z):
    """x range of a convex outline at depth z."""
    xs = []
    for (x0, z0), (x1, z1) in zip(poly, poly[1:] + poly[:1]):
        if (z0 - z) * (z1 - z) <= 0 and z0 != z1:
            xs.append(x0 + (x1 - x0) * (z - z0) / (z1 - z0))
    return (min(xs), max(xs)) if len(xs) >= 2 else (0.0, 0.0)


def strip(m, poly, z, w, y, h, colour, top=None, x0=None, x1=None, inset=0.05):
    """A flat band across the patch at depth z, w deep and h high, its ends cut to the outline (so no tick of ink
    stands at a row end): the outline's span at both edges, or x0..x1 when given (clamped to the span)."""
    za, zb = z - w / 2, z + w / 2
    (a0, a1), (b0, b1) = span(poly, za), span(poly, zb)
    if x0 is not None:
        a0, b0 = max(a0, x0), max(b0, x0)
    if x1 is not None:
        a1, b1 = min(a1, x1), min(b1, x1)
    a0, b0, a1, b1 = a0 + inset, b0 + inset, a1 - inset, b1 - inset
    if a1 - a0 < 0.2 or b1 - b0 < 0.2:
        return
    m.add_extrude((0, y, 0), [(a0, za), (a1, za), (b1, zb), (b0, zb)], h, colour, top=top, cap_bottom=False)


def base(rng, earth="Packed earth", ridge="Trail earth"):
    m = glb.Mesh()
    head = outline(rng, SIZE / 2 * 0.98, amount=0.04)
    m.add_extrude((0, 0, 0), head, 0.04, "Bent grass")                                # headland
    poly = outline(rng, SIZE / 2 * 0.86, amount=0.06)
    m.add_extrude((0, 0.04, 0), poly, 0.06, earth, cap_bottom=False)                   # ploughed earth
    rows = []
    for z in ROW_Z:
        x0, x1 = span(poly, z)
        x0 += 0.3 + rng.random() * 0.4
        x1 -= 0.3 + rng.random() * 0.4
        if ridge:
            strip(m, poly, z, 1.0, 0.1, 0.04, ridge)
        rows.append((z, x0, x1))
    return m, rows, poly


def sprouting():
    rng = random.Random(SEED)
    m, rows, poly = base(rng)
    for z, x0, x1 in rows:          # review r1: one flat Meadow band per row, broken by two or three unequal gaps
        cuts = sorted(rng.uniform(x0 + 1.0, x1 - 1.0) for _ in range(2 + (rng.random() < 0.5)))
        a = x0
        for c in cuts + [x1]:
            g = 0.3 + rng.random() * 0.7 if c < x1 else 0.0
            strip(m, poly, z, 0.3, 0.14, 0.03, "Meadow", x0=a, x1=c - g / 2, inset=0.0)
            a = c + g / 2
    return m


def stook(m, rng, x, z, h=1.05):
    """Sheaves stood together: a wide straw skirt, a cone top and a dark band, about 1 m high and 1.1 m across."""
    m.add_frustum((x, 0.1, z), 0.58, 0.42, 0.45, "Reed and thatch", segments=6, cap_top=False, cap_bottom=False)
    m.add_cone((x, 0.55, z), 0.48, h - 0.45, "Reed and thatch", segments=6, yaw=rng.uniform(0, 1.2))
    m.add_cylinder((x, 0.45, z), 0.46, 0.1, "Packed earth", segments=6, cap_bottom=False, cap_top=False)   # band


def ripe():
    """A golden sea of standing grain: one uneven gold mass over the patch (no gaps that read as planks), heads
    combed into low broken ridges on top, a cut strip at the front with three stooks."""
    rng = random.Random(SEED + 1)
    m, rows, earth = base(rng, ridge=None)
    cut = ROW_Z[-2] - 0.8                                                             # front edge of the standing grain
    poly = [(x, z) for x, z in outline(random.Random(SEED + 5), SIZE / 2 * 0.8, amount=0.05) if z < cut]
    poly = hull2d(poly + [(span_x, cut) for span_x in (-6.2, 6.2)])
    m.add_extrude((0, 0.1, 0), poly, 0.32, "Reed and thatch", cap_bottom=False)
    # review r1: no ticks, no outlined heads; combed rows as 0.8 m bands standing 4 cm proud every 1.6 m (0.4 m bands were too busy), their
    # edges cut to the grain's outline, so the ink finds a step at each row and nothing else
    z = min(zz for _, zz in poly) + 0.6
    while z < cut - 0.7:
        strip(m, poly, z, 0.8, 0.42, 0.04, "Packed earth", top="Reed and thatch", inset=0.15)
        z += 1.6
    for z, x0, x1 in rows:                                                            # the cut strip: low stubble
        if z > cut:
            strip(m, earth, z, 0.7, 0.1, 0.03, "Trail earth", inset=0.3)
    for i, x in enumerate((-4.2, -0.3, 3.8)):                                         # stooks well inside the edge
        stook(m, rng, x + rng.uniform(-0.4, 0.4), cut + 0.85 + (i % 2) * 0.25)
    return m


def stubble():
    rng = random.Random(SEED + 2)
    m, rows, poly = base(rng, earth="Trail earth", ridge=None)
    for z, x0, x1 in rows:                                     # cut stalks: thin pale rows, ends cut to the patch
        strip(m, poly, z, 0.55, 0.1, 0.04, "Reed and thatch", inset=0.35)
    for x, r in ((-4.5, 1), (0.5, 2), (4.0, 4), (-2.0, 5), (3.0, 6)):
        stook(m, rng, x + rng.uniform(-0.4, 0.4), ROW_Z[r] + 0.8)
    for x, r, a in ((-1.5, 0, 0.4), (2.5, 3, -0.6), (-5.0, 6, 1.1), (5.0, 7, 0.2)):      # sheaves lying
        z = ROW_Z[r] + 0.8
        d = (math.cos(a) * 0.45, math.sin(a) * 0.45)
        beam(m, (x - d[0], 0.25, z - d[1]), (x + d[0], 0.25, z + d[1]), 0.34, 0.3, "Reed and thatch")
        beam(m, (x - d[0] * 0.1, 0.25, z - d[1] * 0.1), (x + d[0] * 0.1, 0.25, z + d[1] * 0.1), 0.38, 0.34,
             "Packed earth")                                                          # its band
    return m


for name, make, used in (("field-sprouting", sprouting, "Hamlet: fields (spring, sprouting rows)"),
                         ("field-ripe", ripe, "Hamlet: fields (summer, golden grain being harvested)"),
                         ("field-stubble", stubble, "Hamlet: fields (autumn, stubble, sheaves and stooks)")):
    out = asset_path(f"models/{name}.glb")
    print(glb.report(out, glb.write_glb(out, make(), max_tris=500, ink="soft")))
    ledger.add("models", out, kind="model", generator=__file__, used_in=used, seed=SEED, verified=True)

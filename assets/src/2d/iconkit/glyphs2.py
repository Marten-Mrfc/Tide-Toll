# Glyphs added after the round-3 review (docs/plans/n3/assets-r3-ic-review.md): shapes that read at 24 and 16 px,
# drawn on the full 48 box like glyphs.py. `f` is the fill: "c" the icon's colour, "p" Paper, a palette name, "i" Ink.
from .draw import path, circle, ellipse, rect, poly, line, dot, place, star_pts
from . import glyphs as g


def coin_stamped(cx=24, cy=24, r=18, f="c"):
    """A coin with a square hole (an old coin), so it is not a plain ring or a button."""
    k = r / 18.0
    return [circle(cx, cy, r, f), circle(cx, cy, r - 4.5 * k, None), rect(cx - 4.5 * k, cy - 4.5 * k, 9 * k, 9 * k, "p", 0.8)]


def flour_sack(cx=24, f="lime"):
    """A pale sack, tied neck, one dark band across the belly (style sheet section 6)."""
    return [path(f"M{cx - 5} 13 Q{cx - 16} 20 {cx - 16} 32 Q{cx - 16} 43 {cx} 43 Q{cx + 16} 43 {cx + 16} 32 Q{cx + 16} 20 {cx + 5} 13Z", f),
            rect(cx - 15, 28, 30, 6, "i", 0), path(f"M{cx - 5} 13 L{cx - 7} 6 H{cx + 7} L{cx + 5} 13"), line((cx - 6, 13), (cx + 6, 13))]


def cloth_bolt(f="c"):
    """A rolled bolt on its side: a spiral at the end and a loose flap of cloth, no stripes on the body."""
    return [path("M30 30 Q34 40 44 43 H18 Q10 40 8 30Z", f), rect(6, 10, 26, 22, f, 2), ellipse(32, 21, 7, 11, "p"),
            path("M32 21 Q35 21 35 18 Q34 14 31 15 Q27 17 28 22 Q29 27 33 27")]


def bundle_stack(f="c"):
    """A load: three bundles stacked (a pyramid), the Load line whatever the step."""
    return [rect(5, 28, 18, 14, f, 2), rect(25, 28, 18, 14, f, 2), rect(15, 13, 18, 14, f, 2),
            line((14, 28), (14, 42)), line((34, 28), (34, 42)), line((24, 13), (24, 27))]


def plot_house(f="c"):
    """A plain house on a plot, with a clear plus badge: buy a building."""
    return [poly([(2, 44), (8, 34), (40, 34), (46, 44)], "p"), g.house(21, 41, 26, 28, f),
            circle(37, 12, 9.5, "p"), line((32, 12), (42, 12)), line((37, 7), (37, 17))]


def house_wing(f="c"):
    """A cottage with a wing as wide as itself under a lower roof: extend a cottage."""
    return [poly([(24, 42), (24, 24), (44, 30), (44, 42)], "p"), g.house(14, 42, 22, 32, f), line((32, 36), (36, 36))]


def milestone(inner=None, f="c"):
    """A roadside milestone: a short squat stone with a slanted top on a small mound, a badge on its face."""
    out = [path("M2 44 Q24 34 46 44Z", "meadow"), poly([(11, 42), (11, 16), (37, 9), (37, 42)], f)]
    if inner is not None:
        out.append(place(inner if isinstance(inner, (list, tuple)) else [inner], at=(24, 27), size=20))
    return out


def sprout(f="c"):
    return [line((24, 43), (24, 26)), path("M24 30 Q12 30 9 19 Q20 18 24 30Z", f), path("M24 26 Q30 14 41 14 Q39 27 24 26Z", f),
            path("M10 44 Q24 38 38 44Z", "earth")]


def young_tree(f="c"):
    return [line((24, 44), (24, 30)), path("M24 10 Q35 12 35 22 Q35 32 24 32 Q13 32 13 22 Q13 12 24 10Z", f), path("M12 44 Q24 40 36 44Z", "earth")]


def full_tree(f="c"):
    return [rect(21, 30, 6, 14, f, 1), path("M24 3 Q34 3 37 10 Q45 13 44 21 Q46 30 37 32 Q31 37 24 34 Q17 37 11 32 Q2 30 4 21 Q3 13 11 10 Q14 3 24 3Z", f),
            path("M10 44 Q24 40 38 44Z", "earth")]


def slab_trowel(f="c"):
    """One slab being laid, a trowel above it: the first stretch paved."""
    return [poly([(4, 36), (14, 26), (40, 26), (30, 36)], f), poly([(4, 36), (30, 36), (30, 42), (4, 42)], f),
            poly([(22, 20), (40, 6), (44, 14)], "p"), line((42, 10), (46, 4))]


def footprints_rows(f="c"):
    """Many steps: three rows of footprints."""
    out = []
    for row, y in enumerate((6, 20, 34)):
        for i, x in enumerate((10, 24, 38)):
            out += g.footprint(x + (3 if row % 2 else -3), y, 0.62, f, flip=bool(i % 2))
    return out


def paved_square(f="c"):
    """Every road paved: a square of paving slabs set on the diagonal (no cross shape)."""
    out = [poly([(24, 3), (45, 24), (24, 45), (3, 24)], f)]
    for t in (1 / 3, 2 / 3):
        out.append(line((24 - 21 * t, 3 + 21 * t), (45 - 21 * t, 24 + 21 * t)))
        out.append(line((24 + 21 * t, 3 + 21 * t), (3 + 21 * t, 24 + 21 * t)))
    return out


def crowd(rows, f="c"):
    """Villagers: three in a row (a few) against two rows of smaller ones (many)."""
    if rows == 1:
        return [g.person(x, f, 0.62, 42) for x in (10, 24, 38)]
    return ([g.person(x, f, 0.5, 30) for x in (13, 24, 35)] + [g.person(x, f, 0.5, 44) for x in (7.5, 18.5, 29.5, 40.5)])


def crates_hold(f="c"):
    """A hold with crates stacked in it: ten loads."""
    return [rect(8, 14, 10, 10, f, 1), rect(19, 14, 10, 10, f, 1), rect(30, 14, 10, 10, f, 1),
            rect(13, 4, 10, 10, f, 1), rect(25, 4, 10, 10, f, 1), g.hull(24, 31, 44, 14, f)]


def heaped_hold(f="c"):
    """A heaped hold with a flag on top: fifty loads."""
    return [poly([(6, 25), (24, 10), (42, 25)], f), line((24, 11), (24, 2)), poly([(24, 2), (34, 5), (24, 8)], f),
            g.hull(24, 31, 44, 14, f)]


def coin_boat(f="c"):
    """River trade: a boat with a coin on its sail."""
    return [line((24, 32), (24, 3)), poly([(26, 4), (44, 28), (26, 28)], f), circle(33, 21, 4.5, None),
            g.hull(24, 37, 44, 12, f)]


def drawbridge(f="c"):
    """Raise the bridge: two towers, the two leaves tilted up on their chains, water below."""
    return [rect(3, 16, 9, 28, f, 1), rect(36, 16, 9, 28, f, 1), poly([(12, 37), (12, 31), (19, 18), (22, 21)], f),
            poly([(36, 37), (36, 31), (29, 18), (26, 21)], f), line((7, 16), (20, 19)), line((41, 16), (28, 19)), g.waves(43, 12, 36)]


def boot_in_water(f="c"):
    return [place(g.boot(f), at=(24, 22), size=34), path("M4 34 q5 -4 10 0 t10 0 t10 0 t10 0 V44 H4Z", "river"), g.waves(44, 4, 44)]


def boot_on_plank(f="c"):
    return [place(g.boot(f), at=(24, 14), size=26), rect(3, 27, 42, 5, f, 1), line((10, 32), (10, 37)), line((38, 32), (38, 37)), g.waves(43, 4, 44)]


def three_roofs(f="c"):
    """The borough: three roofs under one flag."""
    return [line((24, 18), (24, 2)), poly([(24, 3), (36, 6), (24, 10)], f), g.house(10, 44, 16, 22, f, door=False),
            g.house(38, 44, 16, 22, f, door=False), g.house(24, 44, 16, 28, f)]


def mooring_post(cx=14, f="c"):
    """A post with a rope loop and a cleat."""
    return [rect(cx - 5, 12, 10, 30, f, 2.5), line((cx - 9, 20), (cx + 9, 20)), path(f"M{cx + 5} 26 Q{cx + 12} 26 {cx + 12} 32 Q{cx + 12} 37 {cx + 5} 36")]


def seal_wax(f="c"):
    """A wax seal: a round blob with a pressed star and two ribbon tails (not a cog)."""
    return [poly([(17, 30), (11, 45), (17, 42), (21, 46), (23, 32)], f), poly([(31, 30), (37, 45), (31, 42), (27, 46), (25, 32)], f),
            path("M24 4 Q33 3 38 10 Q43 19 38 28 Q32 36 22 35 Q11 34 8 24 Q6 12 15 7 Q19 4 24 4Z", f),
            poly(star_pts(23.5, 19.5, 9, 4), "p")]


def drive_scroll(f="c"):
    """The charter drive: a scroll with a row of four boxes, the first two filled (a bar in steps)."""
    out = [rect(9, 9, 30, 30, f, 0), rect(5, 5, 38, 8, f, 4), rect(5, 35, 38, 8, f, 4)]
    for i in range(4):
        out.append(rect(11.5 + i * 7, 17, 4, 14, "i" if i < 2 else "p", 0.8))
    return out


def tide(high, f="c"):
    """The tide dial: the post with the water nearly to its top (high) or at its foot (low), and a big arrow."""
    y = 10 if high else 36
    arrow = ([(37, 4), (46, 17), (41, 17), (41, 43), (33, 43), (33, 17), (28, 17)] if high
             else [(37, 44), (46, 31), (41, 31), (41, 5), (33, 5), (33, 31), (28, 31)])
    return [path(f"M2 {y} q3.5 -4 7 0 t7 0 t7 0 V45 H2Z", f), line((12, 3), (12, 45)), line((8, 12), (16, 12)), line((8, 24), (16, 24)),
            line((8, 36), (16, 36)), poly(arrow, "p")]


def home(f="c"):
    """Home (centre the view): a roof over a small crosshair."""
    return [poly([(4, 22), (24, 5), (44, 22)], f), circle(24, 31, 10, "p"), line((24, 17), (24, 25)), line((24, 37), (24, 45)),
            line((10, 31), (17, 31)), line((31, 31), (38, 31)), dot(24, 31, 2.5)]


def lanes(f="c"):
    """Wider lanes: two road edges with arrows pushing them outwards."""
    return [rect(16, 4, 16, 40, f, 1), line((16, 4), (16, 44)), line((32, 4), (32, 44)), line((24, 9), (24, 15)), line((24, 33), (24, 39)),
            poly([(2, 24), (11, 16), (11, 32)], "i"), poly([(46, 24), (37, 16), (37, 32)], "i")]


def winged_boot(f="c"):
    """Light feet: a boot with a wing."""
    return [place(g.boot(f), at=(28, 26), size=38), path("M18 14 Q8 8 2 12 Q6 15 4 18 Q9 18 8 22 Q14 21 18 22Z", "p")]


def mill_sack(f="c"):
    """The miller's guild: the mill with a sack at its foot."""
    return [place(g.mill(f), at=(19, 22), size=40), place(flour_sack(f="lime"), at=(37, 35), size=20)]


def winch_drum(f="c"):
    """The drawbridge winch: a drum with two rope turns and a crank, on two legs, the rope end hanging."""
    return [line((10, 44), (16, 30)), line((34, 44), (28, 30)), rect(6, 12, 30, 18, f, 2), ellipse(36, 21, 4, 9, "p"),
            line((14, 12), (16, 30)), line((20, 12), (22, 30)), line((40, 21), (45, 21)), line((45, 21), (45, 10)), path("M8 30 Q6 37 8 42")]


def heavy_hammer(f="c"):
    """Tools: a heavy hammer upright over a crossed saw."""
    return [poly([(6, 34), (34, 6), (40, 12), (12, 40)], "p"), line((11, 30), (13, 32)), line((16, 25), (18, 27)), line((21, 20), (23, 22)),
            line((26, 15), (28, 17)), line((24, 20), (24, 45)), rect(11, 4, 26, 14, f, 2)]


def toll_bar(f="c"):
    """The toll gate: a heavy bar on a post, a coin hanging at its end."""
    return [rect(4, 16, 9, 28, f, 1.5), poly([(13, 18), (44, 12), (44, 20), (13, 26)], f), line((40, 20), (40, 27)), circle(40, 34, 6.5, "wheat")]


def berth(f="c"):
    """A berth: a mooring post with a moored boat's bow beside it."""
    return [mooring_post(12, f), path("M22 26 H46 V38 Q34 38 28 34 Q24 31 22 26Z", "earth"), line((17, 30), (24, 29)), g.waves(44, 4, 44)]


def lock(f="c"):
    return [path("M15 22 V15 Q15 6 24 6 Q33 6 33 15 V22"), rect(9, 21, 30, 22, f, 3), dot(24, 30, 2.6), line((24, 31), (24, 37))]


def check(f="c"):
    return [circle(24, 24, 19, f), line((14, 25), (21, 32), (34, 16))]

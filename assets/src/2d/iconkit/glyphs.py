# Shared glyphs of the night-3 icons, each drawn on the full 48 box (content inside about 5..43). `f` is the fill:
# "c" the icon's colour, "p" Paper. Only the vocabulary of draw.py is used, so every glyph has the same line.
from .draw import path, circle, ellipse, rect, poly, line, dot, place, star_pts


def coin(cx=24, cy=24, r=17, f="c"):
    out = [circle(cx, cy, r, f)]
    if r >= 9:
        out.append(circle(cx, cy, r * 0.55, None))
    return out


def coin_badge(f="c"):
    return coin(36, 36, 9.5, f)


def person(cx=24, f="c", s=1.0, base=43):
    h = 30 * s
    return [path(f"M{cx - 13 * s} {base} V{base - 6 * s} Q{cx - 13 * s} {base - 17 * s} {cx} {base - 17 * s} "
                 f"Q{cx + 13 * s} {base - 17 * s} {cx + 13 * s} {base - 6 * s} V{base}Z", f),
            circle(cx, base - h + 3 * s, 7 * s, f)]


def house(cx=24, base=42, w=30, h=30, f="c", door=True):
    x0, x1, roof = cx - w / 2, cx + w / 2, base - h * 0.55
    out = [poly([(x0, base), (x0, roof), (cx, base - h), (x1, roof), (x1, base)], f)]
    if door:
        out.append(path(f"M{cx - 4} {base} V{base - 9} H{cx + 4} V{base}", "p"))
    return out


def bell(cx=24, f="c"):
    return [path(f"M{cx - 15} 36 Q{cx - 11} 33 {cx - 11} 24 Q{cx - 11} 9 {cx} 9 Q{cx + 11} 9 {cx + 11} 24 "
                 f"Q{cx + 11} 33 {cx + 15} 36Z", f),
            circle(cx, 40, 3.5, f), line((cx, 9), (cx, 5))]


def drop(cx=24, cy=26, s=1.0, f="c"):
    return [path(f"M{cx} {cy - 19 * s} Q{cx + 14 * s} {cy - 2 * s} {cx + 13 * s} {cy + 6 * s} "
                 f"A{13 * s} {13 * s} 0 0 1 {cx - 13 * s} {cy + 6 * s} Q{cx - 14 * s} {cy - 2 * s} {cx} {cy - 19 * s}Z", f)]


def ear(cx=24, f="c"):
    out = [line((cx, 44), (cx, 12))]
    for i, y in enumerate((14, 22, 30)):
        out += [ellipse(cx - 5, y + 2, 4, 7, f, rot=-30), ellipse(cx + 5, y + 2, 4, 7, f, rot=30)]
    out.append(ellipse(cx, 9, 3.5, 6, f))
    return out


def sack(cx=24, f="c"):
    return [path(f"M{cx - 9} 12 L{cx - 15} 26 Q{cx - 17} 42 {cx} 42 Q{cx + 17} 42 {cx + 15} 26 L{cx + 9} 12Z", f),
            line((cx - 10, 12), (cx + 10, 12)), path(f"M{cx - 7} 12 L{cx - 10} 6 M{cx + 7} 12 L{cx + 10} 6")]


def boat(cx=24, base=36, w=36, f="c", mast=True):
    out = [path(f"M{cx - w / 2} {base - 8} H{cx + w / 2} L{cx + w / 2 - 7} {base} H{cx - w / 2 + 7}Z", f)]
    if mast:
        out += [line((cx, base - 8), (cx, 7)), poly([(cx + 2, 9), (cx + 13, base - 12), (cx + 2, base - 12)], "p")]
    return out


def hull(cx, cy, w=34, h=10, f="c"):
    """A boat's hull seen from the side, drawn at its real size (no scaling, so the line stays light)."""
    return [path(f"M{cx - w / 2} {cy - h / 2} H{cx + w / 2} L{cx + w / 2 - h * 0.7} {cy + h / 2} H{cx - w / 2 + h * 0.7}Z", f)]


def waves(y=42, x0=6, x1=42):
    d, x = f"M{x0} {y}", x0
    while x < x1:
        d += f" q3 -3 6 0"
        x += 6
    return [path(d)]


def book(f="c"):
    return [path("M24 13 Q15 8 5 10 V38 Q15 36 24 41Z", f), path("M24 13 Q33 8 43 10 V38 Q33 36 24 41Z", f),
            line((24, 13), (24, 41))]


def scroll(f="c"):
    return [rect(11, 9, 26, 30, f, 0), rect(6, 5, 36, 8, f, 4), rect(6, 35, 36, 8, f, 4),
            line((16, 20), (32, 20)), line((16, 27), (28, 27))]


def seal(cx=24, cy=24, r=16, f="c"):
    pts = star_pts(cx, cy, r + 3, r, n=12)
    return [poly(pts, f), circle(cx, cy, r * 0.55, None)]


def star(cx=24, cy=24, R=19, f="c"):
    return [poly(star_pts(cx, cy, R, R * 0.45), f)]


def road(f="c"):
    return [poly([(17, 7), (31, 7), (43, 42), (5, 42)], f), line((24, 12), (24, 16)), line((24, 22), (24, 27)), line((24, 33), (24, 39))]


def footprint(cx, cy, s=1.0, f="c", flip=False):
    k = -1 if flip else 1
    return [ellipse(cx, cy, 4.2 * s, 6.5 * s, f, rot=12 * k), ellipse(cx + 1.5 * s * k, cy + 10 * s, 3 * s, 3.3 * s, f)]


def tree(f="c"):
    return [line((24, 43), (24, 28)), path("M24 5 Q38 6 39 19 Q40 31 24 31 Q8 31 9 19 Q10 6 24 5Z", f)]


def mill(f="c"):
    return [poly([(17, 43), (19, 20), (29, 20), (31, 43)], f), line((24, 20), (10, 6)), line((24, 20), (38, 6)),
            line((24, 20), (10, 34)), line((24, 20), (38, 34)), circle(24, 20, 2.5, "p")]


def stall(f="c"):
    return [path("M7 17 L11 8 H37 L41 17 Q37 22 33 17 Q29 22 24 17 Q19 22 15 17 Q11 22 7 17Z", f),
            line((11, 20), (11, 42)), line((37, 20), (37, 42)), rect(11, 30, 26, 6, "p", 1)]


def field(f="c"):
    return [poly([(12, 10), (36, 10), (44, 40), (4, 40)], f), line((18, 12), (14, 38)), line((24, 12), (24, 38)), line((30, 12), (34, 38))]


def pail(f="c"):
    return [path("M11 16 H37 L33 42 H15Z", f), path("M11 16 Q24 0 37 16"), line((13, 25), (35, 25))]


def basket(f="c"):
    return [path("M9 22 Q24 2 39 22"), poly([(5, 22), (43, 22), (37, 42), (11, 42)], f), line((9, 31), (39, 31))]


def hammer(f="c"):
    return [line((18, 16), (40, 42)), poly([(4, 15), (20, 3), (31, 16), (15, 27)], f), line((9, 11), (20, 24))]


def hand(f="c"):
    return [path("M14 43 V22 Q14 19 16.5 19 Q19 19 19 22 V10 Q19 7 21.5 7 Q24 7 24 10 V20 V8 Q24 5 26.5 5 Q29 5 29 8 V20 "
                 "V11 Q29 8 31.5 8 Q34 8 34 11 V24 L37 19 Q39 16 41 18 Q42 20 40 23 L33 37 Q31 43 24 43Z", f)]


def boot(f="c"):
    return [path("M14 6 H26 V24 L38 29 Q42 31 42 36 V40 H14Z", f), line((14, 34), (42, 34))]


def cart(f="c"):
    return [poly([(6, 14), (36, 14), (33, 30), (9, 30)], f), line((36, 18), (44, 10)), circle(15, 35, 6, "p"), circle(29, 35, 6, "p")]


def chest(f="c"):
    return [path("M7 22 Q7 9 24 9 Q41 9 41 22Z", f), rect(7, 22, 34, 19, f, 1.5), rect(21, 19, 6, 8, "p", 1)]


def purse(f="c"):
    return [path("M17 15 Q6 24 7 34 Q8 43 24 43 Q40 43 41 34 Q42 24 31 15Z", f), path("M17 15 L13 7 Q24 11 35 7 L31 15"),
            line((17, 15), (31, 15))]


def loaf(f="c"):
    return [path("M6 34 Q4 16 24 15 Q44 16 42 34Z", f), line((15, 22), (19, 28)), line((23, 20), (27, 27)), line((31, 21), (34, 27))]


def scales(f="c"):
    return [line((24, 6), (24, 40)), line((14, 42), (34, 42)), line((8, 12), (40, 12)),
            path("M8 12 L3 26 H13Z", f), path("M40 12 L35 26 H45Z", f)]


def well(f="c"):
    return [rect(8, 26, 32, 16, f, 1.5), line((12, 26), (12, 10)), line((36, 26), (36, 10)), poly([(6, 12), (24, 4), (42, 12)], f),
            line((24, 12), (24, 20)), rect(20, 18, 8, 6, "p", 1)]


def fleece(f="c"):
    return [path("M10 30 Q5 30 6 24 Q4 17 11 16 Q12 9 19 11 Q24 5 30 11 Q37 9 38 16 Q45 18 42 25 Q44 31 37 31 Q34 37 24 35 Q14 37 10 30Z", f),
            line((14, 33), (14, 42)), line((34, 33), (34, 42)), circle(40, 22, 4, "p")]


def cloth(f="c"):
    return [path("M8 12 H34 V40 H8Z", f), ellipse(34, 26, 5, 14, "p"), line((14, 12), (14, 40)), line((21, 12), (21, 40)), line((28, 12), (28, 40))]


def post(cx=24, f="c"):
    return [rect(cx - 5, 10, 10, 32, f, 2.5), ellipse(cx, 10, 5, 2.5, "p"), line((cx - 5, 22), (cx + 5, 22))]


def crane(f="c"):
    return [rect(6, 32, 24, 11, f, 1.5), line((18, 32), (18, 8)), line((18, 8), (42, 12)), line((18, 16), (32, 10)),
            line((40, 12), (40, 26)), rect(35, 26, 10, 8, "p", 1)]


def bridge(f="c", raised=False):
    if raised:
        return [rect(4, 30, 10, 12, f, 1), rect(34, 30, 10, 12, f, 1), poly([(12, 30), (16, 30), (36, 8), (32, 6)], f), line((40, 30), (36, 8))]
    return [path("M4 42 V26 H44 V42 H36 Q24 30 12 42Z", f), line((4, 26), (4, 20)), line((44, 26), (44, 20))]


def gate(f="c"):
    return [rect(5, 20, 8, 22, f, 1.5), poly([(13, 22), (43, 14), (43, 20), (13, 28)], f), line((22, 26), (22, 42))]


def flag(f="c"):
    return [line((11, 44), (11, 5)), path("M11 7 Q20 3 27 8 Q34 12 41 8 V26 Q34 30 27 26 Q20 22 11 26Z", f)]


def door(f="c"):
    return [path("M11 43 V16 Q11 6 24 6 Q37 6 37 16 V43Z", f), line((24, 8), (24, 43)), dot(30, 27, 2)]


def feather(f="c"):
    return [path("M10 40 Q12 18 38 7 Q40 26 20 34Z", f), line((6, 44), (30, 17))]


def horseshoe(f="c"):
    return [path("M11 42 L9 25 Q8 7 24 7 Q40 7 39 25 L37 42 H30 L32 25 Q33 14 24 14 Q15 14 16 25 L18 42Z", f)]


def lantern(f="c"):
    return [path("M18 9 H30 M24 9 V4"), path("M15 14 H33 L36 36 H12Z", f), rect(10, 36, 28, 6, f, 1.5), path("M24 19 Q30 27 24 31 Q18 27 24 19Z", "p")]


def flower(f="c"):
    out = [line((24, 44), (24, 27)), path("M24 37 Q32 30 38 33 Q32 41 24 37Z", "meadow")]
    for dx, dy in ((0, -9), (8.5, -2.8), (5.3, 7.3), (-5.3, 7.3), (-8.5, -2.8)):
        out.append(circle(24 + dx, 17 + dy, 5.5, f))
    out.append(circle(24, 17, 4, "p"))
    return out


def arrow_up(cx=24, top=5, bot=43, w=12, f="c"):
    return [poly([(cx, top), (cx + w, top + w), (cx + w / 2, top + w), (cx + w / 2, bot), (cx - w / 2, bot), (cx - w / 2, top + w), (cx - w, top + w)], f)]


def rosette(f="c"):
    return [poly([(15, 28), (11, 44), (17, 41), (20, 46), (24, 32)], f), poly([(33, 28), (37, 44), (31, 41), (28, 46), (24, 32)], f),
            poly(star_pts(24, 20, 17, 13.5, n=12), f), circle(24, 20, 8, "p")]


def cobbles(f="c"):
    out = [rect(5, 8, 38, 32, f, 2)]
    out += [line((5, 19), (43, 19)), line((5, 30), (43, 30)), line((18, 8), (18, 19)), line((31, 8), (31, 19)),
            line((12, 19), (12, 30)), line((25, 19), (25, 30)), line((37, 19), (37, 30)), line((18, 30), (18, 40)), line((31, 30), (31, 40))]
    return out


def gravel():
    return [dot(x, y, r) for x, y, r in ((10, 30, 3), (19, 36, 3.5), (29, 31, 3), (38, 37, 3.5), (15, 23, 2.5), (33, 24, 2.5), (24, 26, 3))]


def rope(f="c"):
    return [circle(24, 26, 16, f), circle(24, 26, 10, "p"), circle(24, 26, 4, None), path("M38 34 Q44 40 40 45")]


def raft(f="c"):
    out = []
    for i in range(5):
        out.append(rect(6 + i * 7.4, 16, 6.6, 20, f, 3))
    out += [line((4, 21), (44, 21)), line((4, 31), (44, 31))]
    return out


def winch(f="c"):
    return [rect(10, 16, 22, 16, f, 2), line((6, 42), (14, 32)), line((36, 42), (28, 32)), line((32, 24), (40, 24)), line((40, 24), (40, 14)),
            line((14, 16), (14, 32)), line((20, 16), (20, 32)), line((26, 16), (26, 32))]


def hat(f="c"):
    return [path("M12 30 Q12 12 24 12 Q36 12 36 30Z", f), path("M4 32 Q24 26 44 32 Q44 37 24 36 Q4 37 4 32Z", f), line((12, 26), (36, 26))]


def loop_arrow(f="c"):
    return [path("M24 8 A16 16 0 1 1 9 30", None), poly([(24, 2), (31, 8), (24, 14)], f), dot(9, 30, 2)]


def crosshair():
    return [circle(24, 24, 15, "p"), line((24, 3), (24, 15)), line((24, 33), (24, 45)), line((3, 24), (15, 24)), line((33, 24), (45, 24)), dot(24, 24, 3)]


def scale_box(parts, size, at=(24, 24)):
    return place(parts, at=at, size=size)

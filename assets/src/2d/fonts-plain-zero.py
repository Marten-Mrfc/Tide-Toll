# python3 assets/src/2d/fonts-plain-zero.py  ->  assets/fonts/TideLegible-Regular.ttf, assets/fonts/TideLegible-Bold.ttf
"""Tide Legible: Atkinson Hyperlegible 1.006 with a plain (unslashed) zero, for the interface.

Atkinson draws `zero` with a slash: three contours, the outline and the counter cut in two by the slash
(`zero.tf`, the tabular zero, is a composite of `zero` and follows it). In prices "10,697" read as "1θ,697".
The counter of the plain zero is the counter Atkinson already draws outside the slash: of its four quarter
arcs, the top-right and bottom-left are whole in the font; the other two are those mirrored about the
counter's vertical axis. So the new counter is four quadratic arcs (curves, no polygon), the outline is kept
as it is, and the advance widths do not change (no layout moves). Every other glyph is untouched.

Licence: SIL Open Font License 1.1 (assets/licences/AtkinsonHyperlegible-OFL.txt), which allows modified
versions under the same licence. The derived family has its own name, keeps the copyright notice and the
licence fields, and says in its description what was changed. Deterministic (no timestamp written).
"""
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from common import ledger, asset_path

FAMILY, PS = "Tide Legible", "TideLegible"
SRC = {w: asset_path(f"fonts/AtkinsonHyperlegible-{w}.ttf") for w in ("Regular", "Bold")}
DESCRIPTION = ("Tide Legible is a modified version of Atkinson Hyperlegible 1.006 (Copyright 2020 Braille Institute "
               "of America, Inc.), made for the game Tide & Toll: the glyph zero (and so the tabular zero.tf, "
               "built from it) has a plain oval counter instead of the slash. The outline, the advance widths "
               "and every other glyph are unchanged. Licensed under the SIL Open Font License, Version 1.1.")


def contours(glyph, glyf):
    pen = RecordingPen()
    glyph.draw(pen, glyf)
    out, cur = [], []
    for op, args in pen.value:
        cur.append((op, args))
        if op in ("closePath", "endPath"):
            out.append(cur); cur = []
    return out


def segments(contour):
    """Yield (start, offcurves, end) for every qCurveTo of a contour."""
    last = None
    for op, args in contour:
        if op == "moveTo":
            last = args[0]
        elif op == "lineTo":
            last = args[0]
        elif op == "qCurveTo":
            yield last, list(args[:-1]), args[-1]
            last = args[-1]


def points(contour):
    return [p for op, args in contour for p in args]


def plain_counter(counter_parts):
    """The quarter arcs of the slashed counter that run from a side extreme to a top/bottom extreme, whole."""
    pts = [p for c in counter_parts for p in points(c)]
    xmin, xmax = min(p[0] for p in pts), max(p[0] for p in pts)
    ymin, ymax = min(p[1] for p in pts), max(p[1] for p in pts)
    cx = (xmin + xmax) / 2
    tr = bl = None
    for c in counter_parts:
        for s, offs, e in segments(c):
            if s[0] == xmax and e[1] == ymax: tr = (s, offs, e)        # right side -> top
            if s[0] == xmin and e[1] == ymin: bl = (s, offs, e)        # left side -> bottom
    assert tr and bl, "zero's counter is not built as expected; look at the glyph before changing this script"
    mx = lambda p: (round(2 * cx - p[0]), p[1])
    right, top, left, bottom = tr[0], tr[2], bl[0], bl[2]
    # bottom -> right -> top -> left -> bottom: counter-clockwise, against the clockwise outline (TrueType).
    br = [mx(p) for p in reversed(bl[1])]                               # bl mirrored, run from the bottom up
    tl = [mx(p) for p in reversed(tr[1])]                               # tr mirrored, run from the top to the left
    return [bottom, (br, right), (tr[1], top), (tl, left), (bl[1], bottom)], (xmin, xmax, ymin, ymax)


def make(weight):
    f = TTFont(SRC[weight], recalcTimestamp=False)
    glyf, hmtx = f["glyf"], f["hmtx"]
    for name in f.getGlyphOrder():                                       # nothing but zero.tf may borrow the zero
        g = glyf[name]
        if g.isComposite() and name != "zero.tf":
            assert all(c.glyphName != "zero" for c in g.components), f"{name} is built from zero"
    assert glyf["zero.tf"].isComposite() and [c.glyphName for c in glyf["zero.tf"].components] == ["zero"]
    cs = contours(glyf["zero"], glyf)
    assert len(cs) == 3, f"{weight}: zero has {len(cs)} contours, expected 3"
    outer = max(range(3), key=lambda i: (max(p[0] for p in points(cs[i])) - min(p[0] for p in points(cs[i]))))
    path, box = plain_counter([c for i, c in enumerate(cs) if i != outer])
    pen = TTGlyphPen(None)
    for op, args in cs[outer]:                                           # the outline as it is
        getattr(pen, op)(*args)
    pen.moveTo(path[0])
    for offs, end in path[1:-1]:
        pen.qCurveTo(*offs, end)
    pen.qCurveTo(*path[-1][0], path[0])
    pen.closePath()
    adv, lsb = hmtx["zero"]
    new = pen.glyph()                                                    # no instructions: the old ones named old points
    new.recalcBounds(glyf)
    assert new.xMin == lsb, "the outline moved"
    glyf["zero"] = new
    hmtx["zero"] = (adv, new.xMin)
    if "DSIG" in f:                                                      # a signature of the old file; now wrong
        del f["DSIG"]
    names = f["name"]
    full = FAMILY if weight == "Regular" else f"{FAMILY} {weight}"
    for rec in list(names.names):
        if rec.nameID in (16, 17, 18):
            names.removeNames(nameID=rec.nameID)
    for pid, eid, lid in {(r.platformID, r.platEncID, r.langID) for r in names.names}:
        names.setName(FAMILY, 1, pid, eid, lid)
        names.setName(f"1.006;NONE;{PS}-{weight}", 3, pid, eid, lid)
        names.setName(full, 4, pid, eid, lid)
        names.setName("Version 1.006; plain zero (Tide & Toll)", 5, pid, eid, lid)
        names.setName(f"{PS}-{weight}", 6, pid, eid, lid)
        names.setName(DESCRIPTION, 10, pid, eid, lid)
    for rec in names.names:
        assert "Atkinson Hyperlegible" not in rec.toUnicode() or rec.nameID in (0, 10), rec.nameID
    out = asset_path(f"fonts/{PS}-{weight}.ttf")
    f.save(out)
    return out, box


if __name__ == "__main__":
    for w, use in (("Regular", "UI: body text, numbers, buttons, tooltips (UiKit.UiFont)"),
                   ("Bold", "UI: bold body text and numbers (UiKit.UiBold)")):
        out, box = make(w)
        ledger.add("2d", out, kind="font", generator=__file__, used_in=use, licence="OFL-1.1",
                   url="https://github.com/google/fonts/tree/main/ofl/atkinsonhyperlegible",
                   note=(f"modified version of assets/fonts/AtkinsonHyperlegible-{w}.ttf (Atkinson Hyperlegible 1.006 "
                         "Copyright 2020 Braille Institute of America, Inc.; see its row); changed: zero and the tabular "
                         "zero.tf (a composite of it) get a plain counter, the font's own unslashed quarter arcs mirrored; "
                         "outline, advances and all other glyphs unchanged; renamed Tide Legible; copyright and OFL "
                         "fields kept; OFL text in assets/licences/AtkinsonHyperlegible-OFL.txt"),
                   verified=True)
        print(f"{out}: counter x {box[0]}..{box[1]}, y {box[2]}..{box[3]}")

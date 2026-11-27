#!/usr/bin/env python3
"""Prototype by the lead (not shipped): gives Atkinson Hyperlegible a plain zero.

The font draws `zero` as three contours (the outline and two halves of the counter, split by the slash) and `zero.tf`
(the tabular zero that the `tnum` feature switches to) as two (the outline and one counter that wraps round the slash).
Both are slashed. The counter of a plain zero is convex, so it is the convex hull of the points of what is there now.
This prototype flattens the curves and writes the hull as a polygon; the outline keeps its curves.

Usage: python3 docs/plans/n4/zero-prototype.py OUTDIR      (writes OUTDIR/mock-Regular.ttf and mock-Bold.ttf)
"""
import sys
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.basePen import decomposeQuadraticSegment


def flatten(glyphset, name, n=12):
    pen = DecomposingRecordingPen(glyphset)
    glyphset[name].draw(pen)
    contours, cur, last = [], [], None
    for op, args in pen.value:
        if op == 'moveTo':
            cur, last = [args[0]], args[0]
        elif op == 'lineTo':
            cur.append(args[0]); last = args[0]
        elif op == 'qCurveTo':
            for c, e in decomposeQuadraticSegment(list(args)):
                for i in range(1, n + 1):
                    t = i / n
                    cur.append(((1 - t) ** 2 * last[0] + 2 * (1 - t) * t * c[0] + t * t * e[0],
                                (1 - t) ** 2 * last[1] + 2 * (1 - t) * t * c[1] + t * t * e[1]))
                last = e
        elif op in ('closePath', 'endPath'):
            contours.append(cur); cur = []
    return contours


def area(c):
    return sum(c[i][0] * c[(i + 1) % len(c)][1] - c[(i + 1) % len(c)][0] * c[i][1] for i in range(len(c))) / 2


def hull(pts):
    pts = sorted(set((round(x, 2), round(y, 2)) for x, y in pts))
    cross = lambda o, a, b: (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, up = [], []
    for p in pts:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(up) >= 2 and cross(up[-2], up[-1], p) <= 0: up.pop()
        up.append(p)
    return lo[:-1] + up[:-1]


out = sys.argv[1]
for w in ('Regular', 'Bold'):
    f = TTFont(f'game/assets/fonts/AtkinsonHyperlegible-{w}.ttf')
    gs = f.getGlyphSet()
    for g in ('zero', 'zero.tf'):
        cs = flatten(gs, g)
        ar = [area(c) for c in cs]
        outer = max(range(len(cs)), key=lambda i: abs(ar[i]))
        h = hull([p for i, c in enumerate(cs) if i != outer for p in c])
        if (area(h) > 0) == (ar[outer] > 0): h = h[::-1]          # the counter winds against the outline
        pen = TTGlyphPen(None)
        rec = DecomposingRecordingPen(gs); gs[g].draw(rec)
        k, keep = -1, False
        for op, args in rec.value:                                 # the outline as it is, curves kept
            if op == 'moveTo': k += 1; keep = (k == outer)
            if keep: getattr(pen, op)(*args)
        pen.moveTo(h[0])
        for p in h[1:]: pen.lineTo(p)
        pen.closePath()
        adv, _ = f['hmtx'][g]
        f['glyf'][g] = pen.glyph()
        f['glyf'][g].recalcBounds(f['glyf'])
        f['hmtx'][g] = (adv, f['glyf'][g].xMin)
    for r in f['name'].names:
        if r.nameID in (1, 4, 6, 16):
            r.string = r.toUnicode().replace('Atkinson Hyperlegible', 'TT Mock Sans').replace('AtkinsonHyperlegible', 'TTMockSans')
    f.save(f'{out}/mock-{w}.ttf')

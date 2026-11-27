#!/usr/bin/env python3
"""Research n8: ink measurements from the ink lab's pictures (game/tests/AssetAudit.Ink.cs, --ink-lab).
   python3 tools/ink-lab/measure.py DIR [variant ...]
For each subject: model pixels = where the picture without ink differs from the ground alone (cast shadows on the
ground left out: a uniform darkening), ink pixels = where the variant differs from the picture without ink.
Prints: share of the model's pixels that are ink (alpha-weighted: a half-strength line counts half), line width
(median of the shorter run through each ink pixel), and the lines crossed by the middle column of the model."""
import sys, os, glob, json
import numpy as np
from PIL import Image

VARIANTS = ["now", "now720", "thin", "ramp", "multiply", "strength60", "inner-light", "inner-colour", "thin-inner-colour",
            "rec", "rec2", "soft-cottage", "fxaa", "msaa4", "ssaa2", "ssaa2-thin"]

def load(p):
    return np.asarray(Image.open(p).convert("RGB")).astype(np.float32)

def model_mask(noink, nothing):
    d = np.abs(noink - nothing).max(axis=2) > 18
    r = (noink + 1) / (nothing + 1)
    shadow = (r.max(axis=2) - r.min(axis=2) < 0.12) & (r.mean(axis=2) < 0.97) & (r.mean(axis=2) > 0.3)
    return d & ~shadow

def runs_min(ink):
    h = np.zeros(ink.shape, np.int32); v = np.zeros(ink.shape, np.int32)
    for y in range(ink.shape[0]):
        row = ink[y]; x = 0
        while x < len(row):
            if row[x]:
                e = x
                while e < len(row) and row[e]: e += 1
                h[y, x:e] = e - x; x = e
            else: x += 1
    for x in range(ink.shape[1]):
        col = ink[:, x]; y = 0
        while y < len(col):
            if col[y]:
                e = y
                while e < len(col) and col[e]: e += 1
                v[y:e, x] = e - y; y = e
            else: y += 1
    return np.minimum(h, v)[ink]

def measure(d, subj, var):
    noink = load(f"{d}/ink-{subj}-noink.png")
    pic = load(f"{d}/ink-{subj}-{var}.png")
    if os.path.exists(f"{d}/ink-{subj}-nothing.png"): mm = model_mask(noink, load(f"{d}/ink-{subj}-nothing.png"))
    else: mm = np.ones(noink.shape[:2], bool)   # a whole view (town, page): the share of the whole picture
    # the ink can fall just outside the model's own pixels (its outer line): grow the mask by 3 px
    g = mm.copy()
    for _ in range(3):
        g = g | np.roll(g, 1, 0) | np.roll(g, -1, 0) | np.roll(g, 1, 1) | np.roll(g, -1, 1)
    diff = np.abs(pic - noink).max(axis=2)
    ink = (diff > 30) & g
    # alpha-weighted: how far each pixel moved toward ink, as a share of how dark it would be in full flat ink
    inkc = np.array([43, 38, 32], np.float32)
    full = np.abs(noink - inkc).max(axis=2) + 1
    wgt = np.clip(diff / full, 0, 1) * g
    area = g.sum()
    w = runs_min(ink)
    ys, xs = np.nonzero(mm)
    cx = int(np.median(xs)) if len(xs) else 0
    col = ink[:, cx] & g[:, cx]
    lines = int(((col[1:] & ~col[:-1])).sum() + (1 if col[0] else 0))
    return dict(subject=subj, variant=var, model_px=int(area), ink_px=int(ink.sum()),
                ink_share=round(float(ink.sum()) / max(1, area), 3), weighted_share=round(float(wgt.sum()) / max(1, area), 3),
                line_px=float(np.median(w)) if len(w) else 0.0, lines_mid=lines)

if __name__ == "__main__":
    d = sys.argv[1]; want = sys.argv[2:]
    subs = sorted({os.path.basename(p)[4:-len("-noink.png")] for p in glob.glob(f"{d}/ink-*-noink.png")})
    print(f"{'subject':22} {'variant':20} {'model px':>8} {'ink %':>6} {'wtd %':>6} {'line px':>7} {'lines':>5}")
    out = []
    for s in subs:
        for v in VARIANTS:
            if not os.path.exists(f"{d}/ink-{s}-{v}.png") or (want and v not in want): continue
            r = measure(d, s, v); out.append(r)
            print(f"{s:22} {v:20} {r['model_px']:8} {100*r['ink_share']:6.1f} {100*r['weighted_share']:6.1f} {r['line_px']:7.1f} {r['lines_mid']:5}")
    json.dump(out, open(f"{d}/measure.json", "w"), indent=1)

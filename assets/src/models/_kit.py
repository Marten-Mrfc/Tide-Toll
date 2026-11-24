# Shared helpers for the models group (not a generator: names starting with "_" are not run by build-assets.sh).
#   from _kit import beam, loft, ngon, jitter_ngon
# Everything builds on common.glb.Mesh, so every face is one palette colour and the ink outline stays a
# screen-space pass (nothing outline-like is modelled here).
import math
import numpy as np

from common import glb  # noqa: F401  (re-exported for convenience)


def _v(p):
    return np.asarray(p, float)


def beam(m, p0, p1, w, h, colour, up=(0, 1, 0), ends=True):
    """Square-section beam (oriented box) from point p0 to point p1: braces, rails, oars, windlass, ridge caps.
    w = width across (horizontal side), h = height across (vertical side)."""
    p0, p1 = _v(p0), _v(p1)
    d = p1 - p0
    L = np.linalg.norm(d)
    d = d / L
    upv = _v(up)
    if abs(np.dot(d, upv)) > 0.95:
        upv = _v((1, 0, 0))
    s = np.cross(d, upv)
    s = s / np.linalg.norm(s) * (w / 2)
    u = np.cross(s, d)
    u = u / np.linalg.norm(u) * (h / 2)
    c = [p0 - s - u, p0 + s - u, p0 + s + u, p0 - s + u, p1 - s - u, p1 + s - u, p1 + s + u, p1 - s + u]
    centre = (p0 + p1) / 2
    faces = [[0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]]
    if ends:
        faces += [[0, 1, 2, 3], [4, 5, 6, 7]]
    for f in faces:
        pts = [c[i] for i in f]
        m._face(pts, colour, np.mean(pts, axis=0) - centre)


def loft(m, ring_a, ring_b, colour, inward=False, centre=None):
    """Side walls between two closed rings of 3D points (same count, same order): hulls, tapered plinths.
    The faces point away from `centre` (default: the rings' mean), or towards it with inward=True."""
    a = [_v(p) for p in ring_a]
    b = [_v(p) for p in ring_b]
    c = _v(centre) if centre is not None else np.mean(a + b, axis=0)
    n = len(a)
    for i in range(n):
        j = (i + 1) % n
        q = [a[i], a[j], b[j], b[i]]
        mid = np.mean(q, axis=0)
        out = mid - c
        out[1] = 0.0 if abs(out[0]) + abs(out[2]) > 1e-6 else out[1]
        if inward:
            out = -out
        # drop degenerate quads (two rings sharing a point)
        if np.linalg.norm(q[0] - q[3]) < 1e-9 and np.linalg.norm(q[1] - q[2]) < 1e-9:
            continue
        if np.linalg.norm(q[1] - q[2]) < 1e-9:
            q = [q[0], q[1], q[3]]
        elif np.linalg.norm(q[0] - q[3]) < 1e-9:
            q = [q[0], q[1], q[2]]
        m._face(q, colour, out)


def cap(m, ring, colour, facing=(0, 1, 0)):
    """Flat convex cap over a ring of 3D points."""
    m._face([_v(p) for p in ring], colour, _v(facing))


def ngon(cx, cz, r, n, y=0.0, phase=0.0, sx=1.0, sz=1.0):
    """n points on an ellipse in the XZ plane at height y."""
    return [(cx + r * sx * math.cos(phase + 2 * math.pi * i / n), y,
             cz + r * sz * math.sin(phase + 2 * math.pi * i / n)) for i in range(n)]


def jitter_ngon(rng, r, n, amount=0.15, phase=0.0):
    """Irregular convex-ish 2D outline [(x, z)] for stones: radius varies by +-amount (fraction)."""
    return [((r * (1 + amount * (rng.random() * 2 - 1))) * math.cos(phase + 2 * math.pi * (i + 0.2 * rng.random()) / n),
             (r * (1 + amount * (rng.random() * 2 - 1))) * math.sin(phase + 2 * math.pi * (i + 0.2 * rng.random()) / n))
            for i in range(n)]


def hull2d(pts):
    """Convex hull (monotone chain) of 2D points [(x, z)], counter-clockwise, so add_extrude always gets a
    convex outline even after jittering."""
    pts = sorted(set((round(x, 6), round(z, 6)) for x, z in pts))
    if len(pts) <= 2:
        return pts

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, hi = [], []
    for p in pts:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], p) <= 0:
            lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(hi) >= 2 and cross(hi[-2], hi[-1], p) <= 0:
            hi.pop()
        hi.append(p)
    return lo[:-1] + hi[:-1]


def stone(m, rng, pos, r, h, top="Concrete", side="Wet mudflat", n=6, amount=0.18, taper=0.8):
    """Low flat stone: jittered convex outline, sides tapering slightly inward to a flat top."""
    out = hull2d(jitter_ngon(rng, r, n, amount, phase=rng.random() * math.pi))
    x0, y0, z0 = pos
    bot = [(x0 + x, y0, z0 + z) for x, z in out]
    tp = [(x0 + x * taper, y0 + h, z0 + z * taper) for x, z in out]
    loft(m, bot, tp, side)
    cap(m, tp, top)

"""Helpers for the 2d group's ground textures (not a generator: the leading underscore keeps build-assets.sh off it).

Every function paints exact palette colours into a (S, S, 3) uint8 image and wraps at the edges, so a tile
stays seamless. The gouache look comes from three layers, all in palette colours only:
  blotch()  soft colour fields with ragged, brushy edges (low-frequency periodic noise, thresholded)
  strokes() short brush marks (rotated capsules), optionally laid along one direction
  grain()   paper tooth: small flecks where a neighbouring palette colour shows through
"""
import numpy as np

from common import palette as P


def noise(size, cx, cy, rng, octaves=1, persistence=0.5):
    """Periodic value noise in [0, 1] with cx lattice cells across x and cy down y (anisotropic when cx != cy).
    cx * 2**(octaves-1) and cy * 2**(octaves-1) should divide size."""
    out = np.zeros((size, size))
    amp = tot = 0.0
    amp = 1.0
    for o in range(octaves):
        kx, ky = cx * 2 ** o, cy * 2 ** o
        lat = rng.random((ky, kx))

        def axis(k):
            s = np.linspace(0, k, size, endpoint=False)
            i0 = np.floor(s).astype(int)
            f = s - i0
            return i0, (i0 + 1) % k, f * f * (3 - 2 * f)
        y0, y1, fy = axis(ky)
        x0, x1, fx = axis(kx)
        top = lat[np.ix_(y0, x0)] * (1 - fx) + lat[np.ix_(y0, x1)] * fx
        bot = lat[np.ix_(y1, x0)] * (1 - fx) + lat[np.ix_(y1, x1)] * fx
        out += amp * (top * (1 - fy)[:, None] + bot * fy[:, None])
        tot += amp
        amp *= persistence
    return out / tot


def blotch(img, colour, rng, cells, threshold, rag=0.12, rag_cells=32, octaves=3):
    """Gouache colour field: where low-frequency noise (plus a little high-frequency 'rag') exceeds threshold."""
    S = img.shape[0]
    n = noise(S, cells, cells, rng, octaves) + (noise(S, rag_cells, rag_cells, rng, 2) - 0.5) * rag
    mask = n > threshold
    img[mask] = P.rgb(colour)
    return mask


def strokes(img, colour, rng, count, length=(6, 12), width=(1.6, 2.4), angle=None, density=None):
    """Brush marks: `count` capsules of the given length/width (px). angle=None -> any direction, else
    (centre, spread) in radians. density: optional (S, S) field in [0, 1]; a stroke is kept where
    rng.random() < density at its centre (so marks gather where the field is high). Wraps at the edges."""
    S = img.shape[0]
    c = np.array(P.rgb(colour), np.uint8)
    for _ in range(count):
        x, y = rng.random() * S, rng.random() * S
        L = rng.uniform(*length)
        w = rng.uniform(*width)
        a = rng.uniform(0, np.pi) if angle is None else angle[0] + rng.uniform(-angle[1], angle[1])
        keep = rng.random()
        if density is not None and keep >= density[int(y) % S, int(x) % S]:
            continue
        r = int(np.ceil(L / 2 + w)) + 1
        xs = np.arange(int(x) - r, int(x) + r + 1)
        ys = np.arange(int(y) - r, int(y) + r + 1)
        X, Y = np.meshgrid(xs + 0.5 - x, ys + 0.5 - y)
        ux, uy = np.cos(a), np.sin(a)
        t = np.clip(X * ux + Y * uy, -L / 2, L / 2)
        # taper: full width in the middle, thinner towards the ends, like a brush lifting off
        half = w / 2 * (1 - 0.45 * (np.abs(t) / (L / 2 + 1e-9)) ** 2)
        d = np.hypot(X - t * ux, Y - t * uy)
        m = d <= half
        iy, ix = np.nonzero(m)
        img[ys[iy] % S, xs[ix] % S] = c


def grain(img, colour, rng, density, cells=128, where=None):
    """Paper tooth: flecks of `colour` (2-4 px clumps). density ~ fraction of pixels covered (0..0.2)."""
    S = img.shape[0]
    n = noise(S, cells, cells, rng, 1)
    th = np.quantile(n, 1 - density * 2)
    mask = (n > th) & (rng.random((S, S)) < 0.5 + 0.5 * (n - th) / (1 - th + 1e-9))
    if where is not None:
        mask &= where
    img[mask] = P.rgb(colour)
    return mask


def dots(img, colour, rng, count, radius=(0.8, 1.4)):
    """Tiny round specks (wildflowers, grit), wrapping."""
    S = img.shape[0]
    c = np.array(P.rgb(colour), np.uint8)
    for _ in range(count):
        x, y, r = rng.random() * S, rng.random() * S, rng.uniform(*radius)
        R = int(np.ceil(r)) + 1
        xs = np.arange(int(x) - R, int(x) + R + 1)
        ys = np.arange(int(y) - R, int(y) + R + 1)
        X, Y = np.meshgrid(xs + 0.5 - x, ys + 0.5 - y)
        iy, ix = np.nonzero(X * X + Y * Y <= r * r)
        img[ys[iy] % S, xs[ix] % S] = c


def base(size, colour):
    a = np.empty((size, size, 3), np.uint8)
    a[:] = P.rgb(colour)
    return a

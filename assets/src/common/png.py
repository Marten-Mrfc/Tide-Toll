"""PNG helpers for textures and previews: numpy only (no Pillow), plus ffmpeg for reading.

    from common import png, palette as P
    png.write_png(arr, path)               # (H, W, 3|4) uint8 or (H, W) grey -> 8-bit sRGB PNG, deterministic bytes
    arr = png.read_png(path)               # any PNG -> (H, W, 3) uint8 (decoded by ffmpeg)
    n = png.tile_noise(512, 6, rng, 3)     # seamless value noise in [0, 1]
    img = png.snap2("Meadow", "Bent grass", n, 0.55)   # every pixel exactly one of two palette colours
    img = png.snap(rgb_float_array)        # snap any RGB image to the nearest palette colour
    png.check_tiles(img) -> (wrap_diff, inner_diff)   # ratio near 1 = no seam
    png.palette_fraction(img) -> 0..1      # share of pixels exactly on the palette
    png.audit(path_or_arr, tol=3)          # most common colours vs the palette ('OK' / 'OFF')
    png.write_png(png.tile(img, 2), "build/x-tiled.png")   # 2 x 2 preview to eyeball seams
    png.upscale(img, 4)                    # nearest-neighbour zoom for previews

Audit from the shell:  python3 -m common.png audit IMAGE.png [tol]   (with PYTHONPATH=assets/src)
Pillow is deliberately not used: its licence (MIT-CMU) is not on the ledger allow list.
"""
import struct
import subprocess
import sys
import zlib
from pathlib import Path

import numpy as np

from . import palette as P


# ---------------------------------------------------------------- write / read
def encode_png(arr) -> bytes:
    a = np.asarray(arr)
    if a.dtype != np.uint8:
        a = np.clip(np.round(a), 0, 255).astype(np.uint8)
    if a.ndim == 2:
        ctype, ch = 0, 1
    elif a.shape[2] == 3:
        ctype, ch = 2, 3
    elif a.shape[2] == 4:
        ctype, ch = 6, 4
    else:
        raise ValueError(f"unsupported shape {a.shape}")
    h, w = a.shape[:2]
    raw = np.zeros((h, 1 + w * ch), np.uint8)          # filter byte 0 (None) on every row: simple and deterministic
    raw[:, 1:] = a.reshape(h, w * ch)

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    ihdr = struct.pack(">IIBBBBB", w, h, 8, ctype, 0, 0, 0)
    srgb = b"\x00"                                       # sRGB chunk, perceptual intent
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"sRGB", srgb)
            + chunk(b"IDAT", zlib.compress(raw.tobytes(), 9)) + chunk(b"IEND", b""))


def write_png(arr, path):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(encode_png(arr))
    return path


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"{path} is not a PNG")
    return struct.unpack(">II", head[16:24])


def read_png(path, alpha=False):
    """Decode any PNG with ffmpeg -> (H, W, 3) uint8 (or (H, W, 4) with alpha=True)."""
    w, h = png_size(path)
    fmt, ch = ("rgba", 4) if alpha else ("rgb24", 3)
    r = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-f", "rawvideo", "-pix_fmt", fmt, "-"],
                       capture_output=True, check=True)
    return np.frombuffer(r.stdout, np.uint8).reshape(h, w, ch).copy()


# ---------------------------------------------------------------- generation
def tile_noise(size, cells, rng, octaves=1, persistence=0.5):
    """Seamless (periodic) value noise in [0, 1], size x size; base lattice cells x cells, doubled per octave.
    `size` should be a multiple of cells * 2**(octaves-1) for exact wrapping (512 with 6 cells wraps too:
    the lattice itself is periodic, only the sample positions differ)."""
    out = np.zeros((size, size))
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        c = cells * 2 ** o
        lat = rng.random((c, c))
        xs = np.linspace(0, c, size, endpoint=False)
        i0 = np.floor(xs).astype(int)
        f = xs - i0
        f = f * f * (3 - 2 * f)
        i1 = (i0 + 1) % c
        top = lat[np.ix_(i0, i0)] * (1 - f)[None, :] + lat[np.ix_(i0, i1)] * f[None, :]
        bot = lat[np.ix_(i1, i0)] * (1 - f)[None, :] + lat[np.ix_(i1, i1)] * f[None, :]
        out += amp * (top * (1 - f)[:, None] + bot * f[:, None])
        tot += amp
        amp *= persistence
    return out / tot


def snap2(a, b, t, threshold=0.5):
    """Pick palette colour b where t > threshold, else a. Pixels stay exactly on the palette."""
    t = np.asarray(t)
    ca, cb = np.array(P.rgb(a), np.uint8), np.array(P.rgb(b), np.uint8)
    return np.where((t > threshold)[..., None], cb, ca).astype(np.uint8)


def layer(img, colour, mask):
    """Paint palette `colour` where boolean mask is true (in place) and return img."""
    img[np.asarray(mask, bool)] = P.rgb(colour)
    return img


def mix(a, b, t):
    """Float RGB blend between two palette colours (NOT on the palette; snap() it, or use for previews only)."""
    ca, cb = np.array(P.rgb(a), float), np.array(P.rgb(b), float)
    t = np.asarray(t, float)[..., None]
    return ca * (1 - t) + cb * t


def snap(img, names=None):
    """Snap every pixel of an RGB image to the nearest of `names` (default: whole palette)."""
    img = np.asarray(img, float)
    pal = P.array(names).astype(float)
    d = ((img[..., None, :] - pal) ** 2).sum(-1)
    return pal[d.argmin(-1)].astype(np.uint8)


def solid(size, colour):
    a = np.empty((size, size, 3), np.uint8)
    a[:] = P.rgb(colour)
    return a


# ---------------------------------------------------------------- checks and previews
def check_tiles(arr):
    """Seam check: mean abs difference across the wrap edges vs. between neighbouring pixels inside.
    Returns (wrap, inner); a seamless tile has wrap close to inner."""
    a = np.asarray(arr, float)
    wrap = (np.abs(a[:, 0] - a[:, -1]).mean() + np.abs(a[0] - a[-1]).mean()) / 2
    inner = (np.abs(a[:, 1:] - a[:, :-1]).mean() + np.abs(a[1:] - a[:-1]).mean()) / 2
    return float(wrap), float(inner)


def palette_fraction(arr, names=None):
    """Fraction of pixels exactly on a palette colour."""
    flat = np.asarray(arr)[..., :3].reshape(-1, 3)
    pal = P.array(names)
    return float((flat[:, None, :] == pal[None]).all(2).any(1).mean())


def audit(img, tol=3, top=12, min_frac=0.002):
    """Most common exact colours with the nearest palette entry. Rows: (hex, share, name, max channel diff, OK/OFF).
    Use on --unlit contact sheets or on textures; lit renders will never match."""
    a = read_png(img) if isinstance(img, (str, Path)) else np.asarray(img)
    a = a[..., :3].reshape(-1, 3)
    vals, cnt = np.unique(a, axis=0, return_counts=True)
    rows = []
    for i in np.argsort(-cnt)[:top]:
        if cnt[i] / len(a) < min_frac:
            continue
        v = tuple(int(x) for x in vals[i])
        n = P.nearest(v)
        d = max(abs(x - y) for x, y in zip(v, P.rgb(n)))
        rows.append((f"#{v[0]:02X}{v[1]:02X}{v[2]:02X}", f"{100 * cnt[i] / len(a):.1f}%", n, d, "OK" if d <= tol else "OFF"))
    return rows


def tile(arr, n=2):
    a = np.asarray(arr)
    return np.tile(a, (n, n) + (1,) * (a.ndim - 2))


def upscale(arr, k):
    return np.repeat(np.repeat(np.asarray(arr), k, 0), k, 1)


if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "audit":
        for r in audit(sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 3):
            print(*r)
    else:
        print(__doc__)

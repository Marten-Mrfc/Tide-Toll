# python3 assets/src/2d/book-desk.py  ->  assets/textures/table-wood.png (2048 x 1280 since the night-3 fix, seamless in both directions),
#   assets/textures/book-leather.png (1024 x 1146, one whole board: not tiled)
# Night 3, asset round 2 (track BK). The table the book lies on and the leather of its boards, made in code from the
# palette's earth tones (each colour is a palette colour or its shade / light tone, mixed 18 percent toward Ink or
# 12 percent toward Paper, as the style sheet allows; grain is a narrow band between two of them).
#   table-wood    five planks across the tile (they run along X), each its own tone between Packed earth and a
#                 Mud-shaded Packed earth; long grain lines that wander and bend round two or three knots a plank;
#                 an Ink gap between planks and one butt joint in some; soft lighter wear (Trail earth) in patches.
#                 The game repeats it twice across the table's depth (BookBuilder.TableMaterial): 800 x 500 m a tile.
#   book-leather  Soot brick leather with a pebbled grain (3 percent), edges rubbed lighter toward Brick, a
#                 blind-tooled double frame (pressed darker lines with a light lower lip) between the edge and the
#                 gilt rule with a gilt roll of dots between them (night 3 fix: pressed deeper, the edge band
#                 broader), and a single blind line with small diamond fleurons inside the gilt rule's corners. The
#                 middle, where the menu's lettering sits, is plain grain. Symmetric, so either face reads the same.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import math
import numpy as np
from common import png, ledger, palette as P, asset_path

NOTE = "procedural texture from palette tones (base, shade, light), no photo source"


def rgb(name):
    return np.array(P.rgb(name), float)


def mixc(a, b, t):
    return a * (1 - t) + b * t


INK = rgb("Ink")
PAPER = rgb("Paper")


def shade(c, t=0.18):
    return mixc(c, INK, t)


def light(c, t=0.12):
    return mixc(c, PAPER, t)


def periodic_noise(x, period, rng, terms=7, amp=1.0, fall=0.6):
    """Smooth 1-D noise that repeats every `period` (sum of sines with whole periods)."""
    out = np.zeros_like(x, dtype=float)
    a = amp
    for k in range(1, terms + 1):
        out += a * np.sin(2 * math.pi * k * x / period + rng.random() * 2 * math.pi)
        a *= fall
    return out


def table_wood(seed=31):
    rng = np.random.default_rng(seed)
    k = 2                      # night 3 fix: 2048 x 1280 (was 1024 x 640), so the close view is not smeared
    W, H = 1024 * k, 640 * k
    y, x = np.mgrid[0:H, 0:W].astype(float)
    pe, mud, trail = rgb("Packed earth"), rgb("Mud"), rgb("Trail earth")
    img = np.zeros((H, W, 3))
    widths = np.array([132, 118, 140, 124, 126], float)
    widths = widths / widths.sum() * H
    edges = np.concatenate([[0], np.cumsum(widths)])
    for i in range(5):
        y0, y1 = edges[i], edges[i + 1]
        inside = (y >= y0) & (y < y1)
        tone = mixc(pe, shade(pe, 0.5), [0.15, 0.45, 0.05, 0.35, 0.22][i])
        # grain coordinate: the row inside the plank, bent by slow wander and by knots
        v = y - y0
        v = v + periodic_noise(x[0], W, rng, amp=6.0 * k)[None, :]
        knots = []
        for _ in range(int(rng.integers(1, 3))):
            kx = rng.uniform(120 * k, W - 120 * k)
            ky = rng.uniform(y0 + 0.3 * widths[i], y1 - 0.3 * widths[i])
            knots.append((kx, ky, rng.uniform(9, 15) * k))
        for kx, ky, kr in knots:
            dx, dy = (x - kx) / (kr * 3.2), (y - ky) / (kr * 1.1)
            r2 = dx * dx + dy * dy
            v = v + np.sign(y - ky + 1e-3) * kr * 1.6 * np.exp(-r2)
        freq = rng.uniform(0.33, 0.42) / k
        g = np.sin(v * freq + periodic_noise(x[0], W, rng, amp=1.2)[None, :])
        g2 = np.sin(v * freq * 2.7 + 1.3)
        dark = np.clip((g - 0.72) / 0.28, 0, 1) * 0.9 + np.clip((g2 - 0.9) / 0.1, 0, 1) * 0.35
        lite = np.clip((-g - 0.85) / 0.15, 0, 1) * 0.5
        c = tone[None, None, :] * np.ones((H, W, 1))
        c = mixc(c, mud[None, None, :], (dark * 0.55)[..., None])
        c = mixc(c, light(tone, 0.25)[None, None, :], (lite * 0.6)[..., None])
        for kx, ky, kr in knots:
            dx, dy = (x - kx) / kr, (y - ky) / (kr * 0.7)
            r = np.sqrt(dx * dx + dy * dy)
            ring = (np.abs(np.sin(r * 4.0)) < 0.35) & (r < 1.6)
            c = np.where(ring[..., None], mixc(c, mud, 0.6), c)
            c = np.where((r < 0.55)[..., None], shade(mud, 0.25), c)
        img = np.where(inside[..., None], c, img)
        # a butt joint in two planks out of five
        if i in (1, 3):
            jx = rng.uniform(300 * k, W - 300 * k)
            joint = inside & (np.abs(x - jx) < 1.4)   # 2.8 px of 2048: half as wide as before
            img[joint] = INK
    # wear: soft lighter patches in Trail earth, broad and faint, tileable (whole-period sines)
    wear = (np.sin(2 * math.pi * x / W + 0.7) * np.sin(2 * math.pi * 2 * y / H + 1.9)
            + 0.5 * np.sin(2 * math.pi * 3 * x / W + 2.2) * np.sin(2 * math.pi * y / H))
    wear = np.clip((wear - 0.6) / 0.9, 0, 1) * 0.22
    img = mixc(img, trail[None, None, :], wear[..., None])
    # plank gaps (Ink, 3 px with a shaded lip below), including the tile edge so tiles join on a gap
    for e in edges[:-1]:
        gap = np.abs(y - e) < 1.6
        if e == 0:
            gap |= y > H - 1.6
        img[gap] = INK
        lip = (y - e >= 1.6) & (y - e < 4.0)
        img[lip] = mixc(img[lip], INK, 0.35)
    return np.clip(img, 0, 255).astype(np.uint8)


def leather(seed=37):
    rng = np.random.default_rng(seed)
    W, H = 1024, 1146          # the board: 309 x 346 m, so 3.31 px a metre
    ppm = W / 309.0
    y, x = np.mgrid[0:H, 0:W].astype(float)
    soot, brick = rgb("Soot brick"), rgb("Brick")
    base = soot
    img = np.ones((H, W, 3)) * base
    # pebbled grain: cell-ish noise from two scales of random values, upsampled by nearest blocks and smoothed
    def blocks(n):
        a = rng.random((H // n + 2, W // n + 2))
        a = np.kron(a, np.ones((n, n)))[:H, :W]
        k = np.ones(n) / n
        a = np.apply_along_axis(lambda r: np.convolve(r, k, mode="same"), 1, a)
        a = np.apply_along_axis(lambda r: np.convolve(r, k, mode="same"), 0, a)
        return a
    grain = blocks(3) * 0.6 + blocks(9) * 0.4
    img = img * (1 + (grain - 0.5)[..., None] * 0.07)
    # rubbed edges toward Brick (and lighter), with noise so the wear is uneven
    d = np.minimum(np.minimum(x, W - 1 - x), np.minimum(y, H - 1 - y)) / ppm   # metres from the edge
    # night 3 fix: a broader, clearer rubbed band (about 4 m strong, fading by 7 m) so the board's edge reads at the
    # normal view
    wear = np.clip(1 - d / 7.0, 0, 1) ** 1.2 * (0.6 + 0.4 * blocks(24))
    img = mixc(img, light(brick, 0.15), (wear * 0.8)[..., None])

    def frame(inset_m, width_m, depth):
        """A blind-tooled line: a pressed darker band with a lighter lip on its lower (south and east) side."""
        ins, w = inset_m * ppm, width_m * ppm
        dx = np.minimum(x, W - 1 - x)
        dy = np.minimum(y, H - 1 - y)
        dd = np.minimum(dx, dy)
        on = (np.abs(dd - ins) < w / 2)
        lip = (np.abs(dd - ins - w * 0.9) < w * 0.35)
        img[on] = mixc(img[on], INK, depth)
        img[lip & ~on] = mixc(img[lip & ~on], light(brick, 0.2), 0.35)

    # night 3 fix: the outer pair pressed deeper and wider (2 and 1.6 m, 60 percent toward Ink) so the blind tooling
    # shows at the normal view; the inner line (under the menu's lettering) stays quiet
    frame(5.5, 2.0, 0.6)
    frame(10.0, 1.6, 0.55)
    frame(22.0, 0.8, 0.35)
    # diamond fleurons inside the gilt rule's corners (the rule is at 16 m), and small dots along the outer pair
    for cx in (22.0, 309 - 22.0):
        for cy in (22.0, 346 - 22.0):
            px, py = cx * ppm, cy * ppm
            dia = (np.abs(x - px) + np.abs(y - py)) < 4.2 * ppm
            ring = dia & ((np.abs(x - px) + np.abs(y - py)) > 2.8 * ppm)
            img[dia] = mixc(img[dia], INK, 0.18)
            img[ring] = mixc(img[ring], INK, 0.3)
    step = 7.5 * ppm
    dotted = np.zeros((H, W), bool)
    mid_inset = 7.5 * ppm
    for along in np.arange(step, max(W, H), step):
        for (px, py) in ((along, mid_inset), (along, H - 1 - mid_inset), (mid_inset, along), (W - 1 - mid_inset, along)):
            if px < W and py < H:
                dotted |= (x - px) ** 2 + (y - py) ** 2 < (0.8 * ppm) ** 2
    img[dotted] = rgb("Reed and thatch")          # night 3 fix: a gilt roll of dots between the blind frames
    return np.clip(img, 0, 255).astype(np.uint8)


if __name__ == "__main__":
    for name, fn, used, seed in (
        ("table-wood", table_wood, "Book: the wooden table under the book (BookBuilder.TableMaterial)", 31),
        ("book-leather", leather, "Book: the leather of both boards (BookCover, BookBuilder)", 37),
    ):
        img = fn(seed)
        out = png.write_png(img, asset_path(f"textures/{name}.png"))
        print(out, img.shape)
        ledger.add("2d", out, kind="texture", generator=__file__, used_in=used, seed=seed, note=NOTE, verified=True)

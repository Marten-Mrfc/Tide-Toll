# python3 assets/src/2d/ground.py  ->  assets/textures/ground-{grass,trampled,trail,track,hollow-way}.png,
#   water-river.png, paper-page.png (512 x 512, seamless, every pixel on the palette), wear-ramp.png (256 x 8),
#   preview: assets/src/2d/previews/ground-preview.png (also build/2d/)
# Free sources (Poly Haven, ambientCG, OpenGameArt, Kenney) are blocked from the build container and are photo or
# pixel-art textures anyway; the Living Map wants flat gouache fields on paper, so these are made in code.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import png, ledger, palette as P, asset_path, ROOT
import _tex as T

S = 512
WRITER_NOTE = "procedural gouache texture, palette colours only"


def grass(seed):
    rng = np.random.default_rng(seed)
    img = T.base(S, "Meadow")
    # sunlit patches: second round uses 8 lattice cells (not 4) and a higher threshold, so the patches are smaller
    # and more numerous and no single big blob marks the tile repeat
    T.blotch(img, "Bent grass", rng, 8, 0.66, rag=0.16)
    T.blotch(img, "Cast-iron green", rng, 16, 0.80, rag=0.10, octaves=2)     # small shade under tufts
    T.grain(img, "Bent grass", rng, 0.035)                                    # paper tooth
    clump = T.noise(S, 8, 8, rng, 2)
    T.strokes(img, "Cast-iron green", rng, 900, length=(4, 8), width=(1.4, 2.0),
              angle=(-np.pi / 2, 0.6), density=np.clip(clump * 1.6 - 0.3, 0, 1))  # grass tufts, gathered
    T.dots(img, "Paper", rng, 28, radius=(0.9, 1.4))                          # wildflower specks
    T.dots(img, "Reed and thatch", rng, 22, radius=(0.9, 1.4))
    return img


def trampled(seed):
    rng = np.random.default_rng(seed)
    img = T.base(S, "Bent grass")
    T.blotch(img, "Meadow", rng, 8, 0.70, rag=0.18)                           # surviving green, small patches
    T.grain(img, "Reed and thatch", rng, 0.03)
    # grass laid flat in every direction (isotropic: the camera rotates, so no east-west streaks); shorter marks
    T.strokes(img, "Meadow", rng, 700, length=(7, 14), width=(1.4, 2.2), angle=None)
    T.strokes(img, "Reed and thatch", rng, 340, length=(6, 11), width=(1.2, 1.8), angle=None)
    T.dots(img, "Trail earth", rng, 40, radius=(0.9, 1.6))                    # first bare specks
    return img


def trail(seed):
    rng = np.random.default_rng(seed)
    img = T.base(S, "Trail earth")
    T.blotch(img, "Packed earth", rng, 6, 0.70, rag=0.14)
    T.grain(img, "Reed and thatch", rng, 0.03)                                # soft pencil-like grain
    T.grain(img, "Packed earth", rng, 0.025, cells=256)
    tufts = T.noise(S, 4, 4, rng, 2)
    T.strokes(img, "Bent grass", rng, 420, length=(5, 11), width=(1.3, 2.0), angle=(0, 0.35),
              density=np.clip(tufts * 2.2 - 0.9, 0, 1))                       # broken grass, in drifts
    return img


def track(seed):
    rng = np.random.default_rng(seed)
    img = T.base(S, "Packed earth")
    T.blotch(img, "Trail earth", rng, 5, 0.70, rag=0.2)                       # loose dry earth
    T.blotch(img, "Mud", rng, 8, 0.74, rag=0.12)                              # damp patches
    T.grain(img, "Trail earth", rng, 0.025)
    T.strokes(img, "Mud", rng, 160, length=(14, 30), width=(1.3, 2.0), angle=(0, 0.08))      # compaction lines
    T.dots(img, "Concrete", rng, 30, radius=(0.8, 1.2))                       # odd pebble
    return img


def hollow_way(seed):
    rng = np.random.default_rng(seed)
    """Second round: a dark, wet, sunken lane. Mud everywhere, small standing puddles in Deep water with a River
    glint on some, a few drier Packed earth crumbs; no ink marks and no pale blotches (the lane is the darkest
    ground state, its brightest pixels are the rare glints)."""
    img = T.base(S, "Mud")
    T.grain(img, "Packed earth", rng, 0.012, cells=256)                       # sparse drier crumbs
    puddle = T.noise(S, 16, 16, rng, 2) + (T.noise(S, 64, 64, rng, 1) - 0.5) * 0.12
    wet = puddle > 0.74
    img[wet] = P.rgb("Deep water")                                            # small puddles
    glint = wet & (T.noise(S, 64, 64, rng, 1) > 0.72) & (puddle > 0.79)
    img[glint] = P.rgb("River")                                               # sky caught in a few of them
    T.strokes(img, "Deep water", rng, 60, length=(5, 10), width=(1.2, 1.8), angle=None)   # wet hoof and foot prints
    return img


def water(seed):
    """Flat slate blue with engraved-style waterlines: parallel, gently waving, broken into long dashes."""
    rng = np.random.default_rng(seed)
    img = T.base(S, "River")
    yy, xx = np.mgrid[0:S, 0:S].astype(float)
    spacing = 32                                                              # 16 lines per tile
    for k in range(S // spacing):
        y0 = k * spacing + rng.uniform(-3, 3)
        ph1, ph2 = rng.uniform(0, 2 * np.pi, 2)
        yc = y0 + 2.6 * np.sin(2 * np.pi * 2 * xx / S + ph1) + 1.2 * np.sin(2 * np.pi * 5 * xx / S + ph2)
        dy = (yy - yc + S / 2) % S - S / 2
        dash = T.noise(S, 6, 1, rng, 2)[0]                                    # periodic along x
        on = dash > 0.38
        w = 0.95 + 0.35 * np.sin(2 * np.pi * 3 * xx / S + ph2)              # engraver's swell
        img[(np.abs(dy) <= w) & on[None, :].repeat(S, 0)] = P.rgb("Deep water")
    return img


def paper(seed):
    rng = np.random.default_rng(seed)
    img = T.base(S, "Paper")
    T.blotch(img, "Limewash", rng, 8, 0.66, rag=0.22, rag_cells=64)           # cloudy sizing
    T.strokes(img, "Limewash", rng, 700, length=(5, 16), width=(0.9, 1.3))     # fibres
    T.grain(img, "Limewash", rng, 0.04, cells=256)
    T.strokes(img, "Paper", rng, 300, length=(4, 10), width=(0.9, 1.2))       # fibres in the cloudy parts
    return img


# wear-ramp.png is DELIBERATELY not snapped to the palette (decided 2026-11-24, recorded in docs/asset-pipeline.md
# "Textures"): it is a lookup gradient for the wear shader, not a picture. Its plateaus are exact palette colours;
# only the +-0.035 transition zones between stages are interpolated (in linear light), so wear changes smoothly
# instead of popping a whole stage when one footstep crosses a threshold. The shader never shows a ramp pixel as a
# surface colour by itself: it uses the ramp to pick and mix the five ground textures, which are fully on palette.
# Wear stages from the Art tab: meadow 0-0.10, bent grass 0.10-0.30, trail 0.30-0.55, track 0.55-0.80,
# worn way 0.80-1.00. The ramp holds each stage colour and blends over +-0.035 around each boundary.
STAGES = [("Meadow", 0.00), ("Bent grass", 0.10), ("Trail earth", 0.30), ("Packed earth", 0.55), ("Mud", 0.80)]
SOFT = 0.035


def wear_ramp(width=256, height=8):
    w = (np.arange(width) + 0.5) / width
    lin = np.array([P.linear01(n) for n, _ in STAGES])
    col = np.repeat(lin[:1], width, 0)
    for i in range(1, len(STAGES)):
        t = np.clip((w - (STAGES[i][1] - SOFT)) / (2 * SOFT), 0, 1)
        t = t * t * (3 - 2 * t)
        col = col * (1 - t[:, None]) + lin[i] * t[:, None]
    srgb = np.where(col <= 0.0031308, col * 12.92, 1.055 * col ** (1 / 2.4) - 0.055)
    row = np.round(srgb * 255).astype(np.uint8)
    return np.repeat(row[None], height, 0)


def crossing_preview(tiles, rng, W=1536, H=448):
    """A trail crossing the meadow: wear falls off from a winding centre line; each pixel takes the ground state
    for its wear (thresholds as in STAGES), with a little noise so the stage edges fray like soft pencil."""
    yy, xx = np.mgrid[0:H, 0:W].astype(float)
    def dist_to(cy, amp, per, ph):
        yc = cy + amp * np.sin(2 * np.pi * xx / per + ph)
        return np.abs(yy - yc)
    main = np.exp(-(dist_to(H * 0.52, 70, 900, 0.4) / 46) ** 2) * (0.55 + 0.45 * np.clip(xx / W * 1.6, 0, 1))
    side = np.exp(-(np.hypot(xx - (W * 0.62 + (yy - H) * 0.35), 0) / 20) ** 2) * 0.5 * (yy > H * 0.5)
    fray = (T.noise(512, 32, 32, rng, 2) - 0.5) * 0.16
    fray = np.tile(fray, (H // 512 + 1, W // 512 + 1))[:H, :W]
    wear = np.clip(np.maximum(main, side) + fray, 0, 1)
    out = np.empty((H, W, 3), np.uint8)
    names = ["grass", "trampled", "trail", "track", "hollow-way"]
    for i, (_, lo) in enumerate(STAGES):
        hi = STAGES[i + 1][1] if i + 1 < len(STAGES) else 2
        t = np.tile(tiles[names[i]], (H // S + 1, W // S + 1, 1))[:H, :W]
        m = (wear >= lo) & (wear < hi)
        out[m] = t[m]
    return out


def main():
    makers = [("grass", grass, 11, "Ground: meadow, wear 0-0.10 (grass)"),
              ("trampled", trampled, 12, "Ground: bent grass, wear 0.10-0.30 (trampled)"),
              ("trail", trail, 13, "Ground: trail, wear 0.30-0.55"),
              ("track", track, 14, "Ground: track, wear 0.55-0.80"),
              ("hollow-way", hollow_way, 15, "Ground: hollow way (worn way), wear 0.80-1.00"),
              ]
    tiles = {}
    for name, fn, seed, used in makers:
        img = fn(seed)
        tiles[name] = img
        out = png.write_png(img, asset_path(f"textures/ground-{name}.png"))
        print(out.name, "seam (wrap, inner):", tuple(round(v, 1) for v in png.check_tiles(img)),
              "on palette:", png.palette_fraction(img))
        ledger.add("2d", out, kind="texture", generator=__file__, used_in=used, seed=seed, note=WRITER_NOTE,
                   verified=True)
    for fname, fn, seed, used in [("water-river", water, 16, "Ground: river and water surfaces (shader scrolls the waterlines)"),
                                  ("paper-page", paper, 17, "Book: page paper under the map, UI paper cards")]:
        img = fn(seed)
        tiles[fname] = img
        out = png.write_png(img, asset_path(f"textures/{fname}.png"))
        print(out.name, "seam (wrap, inner):", tuple(round(v, 1) for v in png.check_tiles(img)),
              "on palette:", png.palette_fraction(img))
        ledger.add("2d", out, kind="texture", generator=__file__, used_in=used, seed=seed, note=WRITER_NOTE,
                   verified=True)
    ramp = wear_ramp()
    out = png.write_png(ramp, asset_path("textures/wear-ramp.png"))
    print(out.name, ramp.shape)
    ledger.add("2d", out, kind="texture", generator=__file__, used_in="Wear shader: wear 0-1 to ground colour (u = wear)",
               note="gradient through the five ground-state palette colours, blended in linear light (in-between "
                    "pixels are off the palette by design)", verified=True)

    # ---- preview: five states side by side, a trail crossing grass, then water, paper and the ramp
    rng = np.random.default_rng(99)
    gap, cw = 16, 288
    W = 5 * cw + 6 * gap
    cross = crossing_preview(tiles, rng, W - 2 * gap, 448)
    H = gap + cw + gap + cross.shape[0] + gap + cw + gap
    sheet = np.empty((H, W, 3), np.uint8)
    sheet[:] = P.rgb("Ink")
    for i, n in enumerate(["grass", "trampled", "trail", "track", "hollow-way"]):
        x = gap + i * (cw + gap)
        sheet[gap:gap + cw, x:x + cw] = tiles[n][:cw, :cw]
    y = 2 * gap + cw
    sheet[y:y + cross.shape[0], gap:gap + cross.shape[1]] = cross
    y += cross.shape[0] + gap
    sheet[y:y + cw, gap:gap + cw] = tiles["water-river"][:cw, :cw]
    sheet[y:y + cw, 2 * gap + cw:2 * gap + 2 * cw] = tiles["paper-page"][:cw, :cw]
    two = png.tile(tiles["grass"], 2)[::2, ::2][:cw, :cw]                    # 2 x 2 tiling at half size: seams?
    sheet[y:y + cw, 3 * gap + 2 * cw:3 * gap + 3 * cw] = two
    rx = 4 * gap + 3 * cw
    rw = W - gap - rx
    big = np.repeat(ramp[:1], cw, 0)[:, (np.arange(rw) * ramp.shape[1] // rw)]
    sheet[y:y + cw, rx:rx + rw] = big
    for d in (ROOT / "build" / "2d", pathlib.Path(__file__).resolve().parent / "previews"):
        print(png.write_png(sheet, d / "ground-preview.png"))
    # ---- repeat check: every ground tile laid 4 x 4 at quarter size (the review looked for repeats at 4 x 4)
    names = ["grass", "trampled", "trail", "track", "hollow-way"]
    q = 4
    rows = [png.tile(tiles[n], q)[::q, ::q] for n in names]                  # 512 px each
    rep4 = np.empty((S, len(names) * S + (len(names) - 1) * gap, 3), np.uint8)
    rep4[:] = P.rgb("Paper")
    for i, r in enumerate(rows):
        rep4[:, i * (S + gap):i * (S + gap) + S] = r
    for d in (ROOT / "build" / "2d", pathlib.Path(__file__).resolve().parent / "previews"):
        print(png.write_png(rep4, d / "ground-tiled-4x4.png"))
    off = {n: round(1 - png.palette_fraction(tiles[n]), 4) for n in names}
    ramp_cols = {tuple(c) for c in ramp[0]}
    on = sum(1 for c in ramp_cols if any(tuple(P.rgb(nm)) == c for nm, _ in STAGES))
    print("off-palette fraction per tile:", off, f"| wear-ramp: {len(ramp_cols)} colours, {on} exact stage colours, "
          f"{len(ramp_cols) - on} interpolated (intentional)")


if __name__ == "__main__":
    main()

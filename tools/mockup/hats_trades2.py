#!/usr/bin/env python3
"""Builds docs/plans/n7/mockup/hats-trades2.png (the second age's headwear, n7 step 3) from the real frames of
game/tests/AssetAudit.Trades2.cs (asset-audit.tscn -- --trades2 --no-town --out-dir=DIR) and the icons in assets/icons.
Each frame is cropped to where it differs from t2-ground.png (the same view with nothing on it), plus a margin.

    python3 tools/mockup/hats_trades2.py SHOTS_DIR OUT_DIR "INK NOTE"
"""
import io, sys, pathlib
from PIL import Image, ImageChops
import cairosvg
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from trades_board import Kit, text, INK, MUTED, CAPTION, TITLE, PAPER

ROOT = pathlib.Path(__file__).resolve().parents[2]
BG = (226, 216, 196, 255)
ROW = ["Helper", "carrier", "reaper", "miller", "SHEPHERD", "WEAVER", "PORTER"]
TUNICS = ["Brick", "Packed earth", "Cast-iron green", "Mud", "Soot brick"]
GRID_ROWS = ["shepherd: felt hat (chosen)", "felt hat with fleece cape", "felt hat with a Mud band", "weaver: stocking cap",
             "porter: Deep water fantail", "carrier's kerchief (to compare)"]


def icon(name, px):
    png = cairosvg.svg2png(url=str(ROOT / "assets" / "icons" / f"{name}.svg"), output_width=px * 4, output_height=px * 4)
    return Image.open(io.BytesIO(png)).convert("RGBA").resize((px, px), Image.LANCZOS)


def main():
    src, out, ink = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), sys.argv[3]
    k = Kit(1.0)
    g = 24
    ground = Image.open(src / "t2-ground.png").convert("RGB")
    shot = lambda n: Image.open(src / f"t2-{n}.png").convert("RGBA")

    def box(*names, m=10):
        bb = None
        for n in names:
            d = ImageChops.difference(shot(n).convert("RGB"), ground).convert("L").point(lambda v: 255 if v > 18 else 0)
            b = d.getbbox()
            bb = b if bb is None else (min(bb[0], b[0]), min(bb[1], b[1]), max(bb[2], b[2]), max(bb[3], b[3]))
        return (bb[0] - m, bb[1] - m, bb[2] + m, bb[3] + m)

    W, H = 2400, 4800
    B = Image.new("RGBA", (W, H), BG)
    text(B, k, (g, 14), "Trades of the second age: shepherd, weaver, porter (headwear and icons)", "serif", 30, INK)
    text(B, k, (g, 56), "Real frames from the game (game/tests/AssetAudit.Trades2.cs): its ground, sun, camera tilt and ink, at the "
         "1920 x 1080 play view. 3 x pictures are enlarged without smoothing; the others are true size. " + ink, "ui", CAPTION, MUTED)
    y = 96
    # (a) the row: chosen set, then the dropped candidates.
    for key, title, labels in (("row", "a. Helper, carrier, reaper, miller, shepherd, weaver, porter (chosen set)", ROW),
                               ("alt", "a'. Dropped: Helper (scale), fleece hood, felt hat + cape, felt hat + band, "
                                "cloth roll, large slate fantail, head pad", ["Helper", "fleece", "+cape", "+band", "roll", "fan slate", "pad"]),
                               ("pair", "a''. Carrier and porter in turn, turned four ways (kerchief, then fantail)",
                                ["carrier", "porter"] * 4)):
        text(B, k, (g, y), title + ": empty-handed, then each with its own good", "serif", TITLE, INK)
        y += 34
        bb = box(f"{key}-bare", f"{key}-load")
        bw, bh = bb[2] - bb[0], bb[3] - bb[1]
        x = g
        for n in ("bare", "load"):
            c = shot(f"{key}-{n}").crop(bb)
            B.alpha_composite(c.resize((bw * 3, bh * 3), Image.NEAREST), (x, y))
            step = bw * 3 / len(labels)
            for i, lab in enumerate(labels):
                tw = k.font("ui", CAPTION).getlength(lab)
                text(B, k, (x + step * (i + 0.5) - tw / 2, y + bh * 3 + 4), lab, "ui", CAPTION, INK)
            B.alpha_composite(c, (x, y + bh * 3 + 30))
            x += bw * 3 + g
        y += bh * 3 + 30 + bh + 30
    # (b) grid.
    text(B, k, (g, y), "b. The chosen headwear, the shepherd's variants and the carrier's kerchief on every tunic colour of the game (TownVillagers.Clothes); "
         "3 x, then true size", "serif", TITLE, INK)
    y += 40
    bb = box("grid")
    gw, gh = bb[2] - bb[0], bb[3] - bb[1]
    grid = shot("grid").crop(bb)
    gx = g + 300
    for c, nm in enumerate(TUNICS):
        cx = gx + (c + 0.5) * gw * 3 / 5
        tw = k.font("ui", CAPTION).getlength(nm)
        text(B, k, (cx - tw / 2, y), nm, "ui", CAPTION, INK)
    B.alpha_composite(grid.resize((gw * 3, gh * 3), Image.NEAREST), (gx, y + 24))
    for r, nm in enumerate(GRID_ROWS):
        text(B, k, (g, y + 24 + (r + 0.45) * gh * 3 / 6), nm, "ui", CAPTION, INK)
    B.alpha_composite(grid, (gx + gw * 3 + g, y + 24))
    text(B, k, (gx + gw * 3 + g, y + 24 + gh + 8), "true size", "ui", CAPTION, MUTED)
    y += 24 + gh * 3 + 30
    # (c) crowds.
    text(B, k, (g, y), "c. A second-age street of twenty at true size: six trades and Helpers (left), the same street with "
         "the first three trades only (right); below, 2 x", "serif", TITLE, INK)
    y += 34
    bb = box("crowd-new", "crowd-old")
    cw, ch = bb[2] - bb[0], bb[3] - bb[1]
    for i, n in enumerate(("crowd-new", "crowd-old")):
        B.alpha_composite(shot(n).crop(bb), (g + i * (cw + g), y))
    y += ch + 16
    for i, n in enumerate(("crowd-new", "crowd-old")):
        c = shot(n).crop(bb)
        B.alpha_composite(c.resize((cw * 2, ch * 2), Image.NEAREST), (g, y))
        y += ch * 2 + 12
    y += 18
    # (d) icons.
    text(B, k, (g, y), "d. The seven trade icons at 24 px (1920 x 1080) and 19 px (1280 x 720) on paper; below, 3 x", "serif", TITLE, INK)
    y += 34
    panel = Image.new("RGBA", (W - 2 * g, 250), PAPER)
    names = [("trade-helper", "Helper"), ("trade-water", "carrier"), ("trade-grain", "reaper"), ("trade-flour", "miller"),
             ("trade-wool", "shepherd"), ("trade-cloth", "weaver"), ("trade-boat", "porter")]
    px0 = 16
    for px in (24, 19):
        text(panel, k, (px0, 6), f"{px} px", "ui", CAPTION, INK)
        for nm, lab in names:
            ic = icon(nm, px)
            tile = Image.new("RGBA", (px + 8, px + 8), PAPER)
            tile.alpha_composite(ic, (4, 4))
            panel.alpha_composite(tile, (px0, 30))
            panel.alpha_composite(tile.resize((tile.width * 3, tile.height * 3), Image.NEAREST), (px0, 74))
            text(panel, k, (px0, 74 + tile.height * 3 + 6), lab, "ui", CAPTION, MUTED)
            px0 += max(tile.width * 3, 80) + 12
        px0 += 40
    B.alpha_composite(panel, (g, y))
    y += 260
    B = B.crop((0, 0, W, y + 10))
    B.convert("RGB").save(out / "hats-trades2.png", optimize=True)
    print("hats-trades2", B.size)


if __name__ == "__main__":
    main()

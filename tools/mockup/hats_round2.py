#!/usr/bin/env python3
"""Builds docs/plans/n7/mockup/hats-round2.png (the trades' headwear, fix round 2) from the real frames of
game/tests/AssetAudit.Trades.cs (asset-audit.tscn -- --trades --no-town --out-dir=DIR) and the icons in assets/icons.

    python3 tools/mockup/hats_round2.py SHOTS_DIR OUT_DIR "INK NOTE"
"""
import io, sys, pathlib
from PIL import Image, ImageDraw
import cairosvg
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from trades_board import Kit, text, INK, MUTED, CAPTION, BODY, TITLE, PAPER, WHEAT

ROOT = pathlib.Path(__file__).resolve().parents[2]
BG = (226, 216, 196, 255)
NORMAL = (840, 470, 1080, 590)
HELPERS = (880, 470, 1040, 590)
GRID = (745, 400, 1175, 672)
LOADS = (780, 480, 1155, 590)
CLOSE = (60, 200, 1860, 860)
TUNICS = ["Brick", "River", "Cast-iron green", "Thatch", "Soot brick", "Packed earth", "Slate roof", "Mud"]
ROWS = ["Helper", "Carrier", "Reaper", "Miller"]


def icon(name, px):
    png = cairosvg.svg2png(url=str(ROOT / "assets" / "icons" / f"{name}.svg"), output_width=px * 4, output_height=px * 4)
    return Image.open(io.BytesIO(png)).convert("RGBA").resize((px, px), Image.LANCZOS)


def main():
    src, out, ink = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), sys.argv[3]
    k = Kit(1.0)
    g = 24
    shot = lambda n, v="normal": Image.open(src / f"trades-{n}-{v}.png").convert("RGBA")
    W, H = 2400, 2900
    B = Image.new("RGBA", (W, H), BG)
    text(B, k, (g, 14), "Trades: the headwear, round 2 (now beside planned)", "serif", 30, INK)
    text(B, k, (g, 56), "Real frames from the game (game/tests/AssetAudit.Trades.cs): its ground, sun, camera tilt and ink. "
         "3 x pictures are enlarged without smoothing; the small ones are true size at 1920 x 1080. " + ink, "ui", CAPTION, MUTED)
    y = 96
    # 1. Now beside planned: Helper/Hand, carrier, reaper, miller.
    text(B, k, (g, y), "1. Helper (Hand today), water carrier, reaper, miller, at the play view", "serif", TITLE, INK)
    y += 34
    nw, nh = NORMAL[2] - NORMAL[0], NORMAL[3] - NORMAL[1]
    x = g
    for n, lab in (("now", "Now, each carrying its good"), ("planned", "Planned, carrying"), ("bare", "Planned, empty-handed: the hat alone")):
        c = shot(n).crop(NORMAL)
        text(B, k, (x, y), lab, "ui", CAPTION, MUTED)
        B.alpha_composite(c.resize((nw * 3, nh * 3), Image.NEAREST), (x, y + 22))
        B.alpha_composite(c, (x, y + 22 + nh * 3 + 8))
        x += nw * 3 + g
    hw = HELPERS[2] - HELPERS[0]
    c = shot("helpers").crop(HELPERS)
    hx, hy = g + 2 * (nw * 3 + g) + nw + 2 * g, y + 22 + nh * 3 + 8   # right of the empty-handed true-size copy
    text(B, k, (hx, hy), "Planned Helpers: hair in Mud, Packed earth, Soot brick", "ui", CAPTION, MUTED)
    B.alpha_composite(c.resize((hw * 2, nh * 2), Image.NEAREST), (hx, hy + 22))
    B.alpha_composite(c, (hx + hw * 2 + 12, hy + 22))
    text(B, k, (hx, hy + 22 + nh * 2 + 4), "2 x, then true size", "ui", CAPTION, MUTED)
    y += 22 + nh * 3 + 8 + 22 + nh * 2 + 30
    # 2. Close.
    text(B, k, (g, y), "2. Close up (the game's tilt for that zoom), at 60 percent", "serif", TITLE, INK)
    y += 34
    cs = 0.6
    cw, ch = round((CLOSE[2] - CLOSE[0]) * cs), round((CLOSE[3] - CLOSE[1]) * cs)
    for i, (n, lab) in enumerate((("now", "Now"), ("planned", "Planned"))):
        xx = g + i * (cw + g)
        text(B, k, (xx, y), lab, "ui", CAPTION, MUTED)
        B.alpha_composite(shot(n, "close").crop(CLOSE).resize((cw, ch), Image.LANCZOS), (xx, y + 22))
    y += 22 + ch + 30
    # 3. Grid.
    text(B, k, (g, y), "3. Every headwear on every tunic colour: today's five (Brick, River, Cast-iron green, Thatch, Soot brick), "
         "the review's two (Packed earth, Slate roof) and Mud; 3 x, then true size", "serif", TITLE, INK)
    y += 40
    gw, gh = GRID[2] - GRID[0], GRID[3] - GRID[1]
    grid = shot("grid").crop(GRID)
    gx = g + 120
    big = grid.resize((gw * 3, gh * 3), Image.NEAREST)
    first, step = 781 - GRID[0], 51.0          # column centres in the crop (measured on the frame)
    for c, nm in enumerate(TUNICS):
        cx = gx + (first + step * c) * 3
        tw = k.font("ui", CAPTION).getlength(nm)
        text(B, k, (cx - tw / 2, y), nm, "ui", CAPTION, INK if c not in (1, 3) else (164, 85, 58, 255))
    B.alpha_composite(big, (gx, y + 24))
    for r, nm in enumerate(ROWS):
        text(B, k, (g, y + 24 + round((40 + r * 66) * 3 * gh / 272 / 1.0 * 0.97)), nm, "ui", BODY, INK)
    B.alpha_composite(grid, (gx + gw * 3 + g, y + 24))
    text(B, k, (gx + gw * 3 + g, y + 24 + gh + 8), "true size", "ui", CAPTION, MUTED)
    y += 24 + gh * 3 + 30
    # 4. Loads.
    text(B, k, (g, y), "4. Under a load: carrier, reaper, miller with their own good, then with another's "
         "(carrier with flour, reaper with pails, miller with a grain basket); 3 x, then true size", "serif", TITLE, INK)
    y += 34
    lw, lh = LOADS[2] - LOADS[0], LOADS[3] - LOADS[1]
    lc = shot("loads").crop(LOADS)
    B.alpha_composite(lc.resize((lw * 3, lh * 3), Image.NEAREST), (g, y))
    B.alpha_composite(lc, (g + lw * 3 + g, y))
    y += lh * 3 + 30
    # 5. Icons.
    text(B, k, (g, y), "5. The trade icons at 24 (1920 x 1080) and 19 px (1280 x 720), on paper and on the current cap's wheat; "
         "below, 3 x", "serif", TITLE, INK)
    y += 34
    panel = Image.new("RGBA", (W - 2 * g, 250), PAPER)
    names = [("trade-helper", "Helper"), ("trade-water", "Water carrier"), ("trade-grain", "Reaper"), ("trade-flour", "Miller"),
             ("shop-train", "Train")]
    px0 = 16
    for px in (24, 19):
        for bg in (PAPER, WHEAT):
            for nm, lab in names:
                ic = icon(nm, px)
                tile = Image.new("RGBA", (px + 8, px + 8), bg)
                tile.alpha_composite(ic, (4, 4))
                panel.alpha_composite(tile, (px0, 30))
                panel.alpha_composite(tile.resize((tile.width * 3, tile.height * 3), Image.NEAREST), (px0, 74))
                if px == 24 and bg == PAPER:
                    text(panel, k, (px0, 74 + tile.height * 3 + 6), lab, "ui", CAPTION, MUTED)
                px0 += tile.width * 3 + 12
            px0 += 24
        text(panel, k, (px0 - 5 * ((px + 8) * 3 + 12) * 2 - 48, 6), f"{px} px", "ui", CAPTION, INK)
    B.alpha_composite(panel, (g, y))
    y += 260
    B = B.crop((0, 0, W, y + 10))
    B.convert("RGB").save(out / "hats-round2.png", optimize=True)
    print("hats-round2", B.size)


if __name__ == "__main__":
    main()

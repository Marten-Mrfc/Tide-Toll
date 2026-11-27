#!/usr/bin/env python3
"""Builds docs/plans/n7/mockup/hats-now-and-planned.png from the real frames of game/tests/AssetAudit.Trades.cs
(asset-audit.tscn -- --trades --no-town --out-dir=DIR): four villagers (Hand, water carrier, reaper, miller) as the game
draws them now and in the trades' headwear, at the 1080p play view (true pixels, and 3 x for reading) and close up.

    python3 tools/mockup/hats_board.py SHOTS_DIR OUT_DIR
"""
import sys, pathlib
from PIL import Image, ImageDraw
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from trades_board import Kit, text, INK, MUTED, CAPTION, TITLE

BG = (226, 216, 196, 255)
NORMAL = (840, 470, 1080, 590)      # the row at the play view (1920 x 1080 frame), true pixels
CLOSE = (60, 200, 1860, 860)        # the row close up


def main():
    src, out = map(pathlib.Path, sys.argv[1:3])
    k = Kit(1.0)
    g = 24
    shots = {n: {v: Image.open(src / f"trades-{n}-{v}.png").convert("RGBA") for v in ("normal", "close")}
             for n in ("now", "planned", "bare")}
    nw, nh = NORMAL[2] - NORMAL[0], NORMAL[3] - NORMAL[1]
    cs = 0.6
    cw, ch = round((CLOSE[2] - CLOSE[0]) * cs), round((CLOSE[3] - CLOSE[1]) * cs)
    W = max(2 * cw + 3 * g, 9 * nw + 4 * g)
    H = 100 + 34 + 22 + nh * 4 + 8 + 40 + 34 + 22 + ch + 24
    B = Image.new("RGBA", (W, H), BG)
    text(B, k, (g, 16), "Trades: now beside planned (the figures)", "serif", 30, INK)
    text(B, k, (g, 58), "Left to right in each: Hand, water carrier, reaper, miller. Real frames from the game "
         "(game/tests/AssetAudit.Trades.cs): the game's ground, sun, camera tilt and ink; the planned heads are the "
         "models in assets/models/villager-head-{kerchief,strawhat,cap}.glb and villager-apron.glb.", "ui", CAPTION, MUTED)
    y = 100
    text(B, k, (g, y), "At the play view (1920 x 1080, the follow view's zoom): enlarged 3 x without smoothing; "
         "true size below", "serif", TITLE, INK)
    y += 34
    x = g
    for n, lab in (("now", "Now (each carrying its good)"), ("planned", "Planned, carrying"), ("bare", "Planned, empty-handed: the hat alone")):
        crop = shots[n]["normal"].crop(NORMAL)
        text(B, k, (x, y), lab, "ui", CAPTION, MUTED)
        B.alpha_composite(crop.resize((nw * 3, nh * 3), Image.NEAREST), (x, y + 22))
        B.alpha_composite(crop, (x, y + 22 + nh * 3 + 8))
        x += nw * 3 + g
    y += 22 + nh * 4 + 8 + 40
    text(B, k, (g, y), "Close up (the game's tilt for that zoom), scaled to 60 percent", "serif", TITLE, INK)
    y += 34
    for i, (n, lab) in enumerate((("now", "Now"), ("planned", "Planned"))):
        x = g + i * (cw + g)
        text(B, k, (x, y), lab, "ui", CAPTION, MUTED)
        B.alpha_composite(shots[n]["close"].crop(CLOSE).resize((cw, ch), Image.LANCZOS), (x, y + 22))
    ImageDraw.Draw(B).line([(g + cw + g / 2, y), (g + cw + g / 2, y + ch + 22)], fill=INK, width=2)
    B.convert("RGB").save(out / "hats-now-and-planned.png", optimize=True)
    print("hats board", B.size)


if __name__ == "__main__":
    main()

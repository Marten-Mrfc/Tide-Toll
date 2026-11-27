#!/usr/bin/env python3
"""Night 5 mock-up board: "work you can watch", each real frame ("now") beside a staged one ("planned").
   python3 tools/mockup/work_board.py SHOTS_DIR NOW_DIR OUT_DIR
SHOTS_DIR holds work-<place>-close.png / -normal.png from the asset audit (AssetAudit.Work.cs);
NOW_DIR holds the real frames now-<place>.jpg. Writes the single pictures and the boards into OUT_DIR."""
import sys, os
from PIL import Image, ImageDraw, ImageFont

shots, now_dir, out = sys.argv[1:4]
os.makedirs(out, exist_ok=True)
PLACES = [("well", "The well"), ("field", "A field"), ("mill", "The mill"), ("market", "The market")]
PW, PH, GAP, STRIP = 800, 600, 16, 50
BG, WHITE, WHEAT = (0x28, 0x24, 0x1F), (255, 255, 255), (0xC9, 0xA5, 0x5A)
FONT = ImageFont.truetype("/usr/share/fonts/opentype/inter/Inter-Bold.otf", 28)

def fit(im):
    return im.convert("RGB").resize((PW, PH), Image.LANCZOS)

def board(panels, path, pw=PW, ph=PH):
    cols = 2
    PW, PH = pw, ph
    rows = (len(panels) + cols - 1) // cols
    W = GAP + cols * (PW + GAP)
    H = GAP + rows * (STRIP + PH + GAP)
    b = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(b)
    for i, (label, colour, im) in enumerate(panels):
        x = GAP + (i % cols) * (PW + GAP)
        y = GAP + (i // cols) * (STRIP + PH + GAP)
        d.text((x + 4, y + STRIP // 2), label, font=FONT, fill=colour, anchor="lm")
        b.paste(im, (x, y + STRIP))
    b.save(path, optimize=True)
    print(path, b.size)

pairs, normals = [], []
for key, name in PLACES:
    real = os.path.join(shots, f"work-now-{key}-close.png")   # a fresh real frame (AssetAudit.NowWork.cs), if taken
    now = fit(Image.open(real if os.path.exists(real) else os.path.join(now_dir, f"now-{key}.jpg")))
    planned = fit(Image.open(os.path.join(shots, f"work-{key}-close.png")))
    now.save(os.path.join(out, f"{key}-now.png"))
    planned.save(os.path.join(out, f"{key}-planned.png"))
    pairs += [(f"{name}: now", WHITE, now), (f"{name}: planned", WHEAT, planned)]
    nrm = Image.open(os.path.join(shots, f"work-{key}-normal.png")).convert("RGB")
    nrm.save(os.path.join(out, f"{key}-planned-normal.png"))
    real_n = os.path.join(shots, f"work-now-{key}-normal.png")
    if not os.path.exists(real_n):
        real_n = os.path.join(now_dir, f"now-{key}-normal.jpg")
    if os.path.exists(real_n):
        rn = Image.open(real_n).convert("RGB").resize((640, 480), Image.LANCZOS)
        rn.save(os.path.join(out, f"{key}-now-normal.png"))
        normals.append((f"{name}: now", WHITE, rn))
    normals.append((f"{name}: planned", WHEAT, nrm))   # at its own size: the play zoom's pixels
board(pairs, os.path.join(out, "work-now-and-planned.png"))
board(normals, os.path.join(out, "work-planned-normal.png"), 640, 480)

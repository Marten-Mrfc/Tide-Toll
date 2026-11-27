#!/usr/bin/env python3
"""Night 6 mock-up board 2: the landing, a real frame now beside the staged design, close up and at the normal view.
   python3 tools/mockup/landing_board.py SHOTS_DIR OUT_DIR
SHOTS_DIR holds work-landing-{now,planned}-{close,normal}.png from the asset audit's --landing pass
(game/tests/AssetAudit.Landing.cs). Writes the single pictures and landing-now-and-planned.png into OUT_DIR."""
import os, sys
from PIL import Image, ImageDraw, ImageFont

shots, out = sys.argv[1:3]
os.makedirs(out, exist_ok=True)
PW, PH, GAP, STRIP = 800, 600, 16, 50
BG, WHITE, WHEAT = (0x28, 0x24, 0x1F), (255, 255, 255), (0xC9, 0xA5, 0x5A)
FONT = ImageFont.truetype("/usr/share/fonts/opentype/inter/Inter-Bold.otf", 28)

panels = []
for kind, label in (("close", "The landing"), ("normal", "From the normal view")):
    for side, col in (("now", WHITE), ("planned", WHEAT)):
        im = Image.open(os.path.join(shots, f"work-landing-{side}-{kind}.png")).convert("RGB").resize((PW, PH), Image.LANCZOS)
        im.save(os.path.join(out, f"landing-{side}-{kind}.png"))
        panels.append((f"{label}: {side}", col, im))
W = GAP + 2 * (PW + GAP)
H = GAP + 2 * (STRIP + PH + GAP)
b = Image.new("RGB", (W, H), BG)
d = ImageDraw.Draw(b)
for i, (lab, col, im) in enumerate(panels):
    x = GAP + (i % 2) * (PW + GAP)
    y = GAP + (i // 2) * (STRIP + PH + GAP)
    d.text((x + 4, y + STRIP // 2), lab, font=FONT, fill=col, anchor="lm")
    b.paste(im, (x, y + STRIP))
p = os.path.join(out, "landing-now-and-planned.png")
b.save(p, optimize=True)
print(p, b.size)

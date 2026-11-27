#!/usr/bin/env python3
"""Joins tools/feel-strips.sh's frames into one sheet per reward: OUT/feel-NAME.png, 4 columns by 3 rows of
960 x 540 frames (real pixels at the play zoom), the frame number in a corner (usage: feel-sheet.py OUT_DIR)."""
import glob
import os
import sys
from PIL import Image, ImageDraw, ImageFont

COLS, ROWS, W, H, GAP = 4, 3, 960, 540, 6


def main(out):
    frames = os.path.join(out, "frames")
    try:
        font = ImageFont.truetype("DejaVuSans-Bold.ttf", 26)
    except OSError:
        font = ImageFont.load_default()
    names = sorted({os.path.basename(p)[:-7] for p in glob.glob(os.path.join(frames, "feel-*-00.png"))})
    for name in names:
        paths = [os.path.join(frames, f"{name}-{k:02d}.png") for k in range(COLS * ROWS)]
        if not all(os.path.exists(p) for p in paths):
            print(f"sheet {name}: frames missing, skipped")
            continue
        sheet = Image.new("RGB", (COLS * W + (COLS + 1) * GAP, ROWS * H + (ROWS + 1) * GAP), (30, 26, 22))
        draw = ImageDraw.Draw(sheet)
        for k, p in enumerate(paths):
            im = Image.open(p).convert("RGB")
            x, y = GAP + (k % COLS) * (W + GAP), GAP + (k // COLS) * (H + GAP)
            sheet.paste(im, (x, y))
            draw.rectangle([x + 6, y + 6, x + 46, y + 40], fill=(0, 0, 0))
            draw.text((x + 12, y + 8), f"{k + 1}", fill=(255, 255, 255), font=font)
        sheet.save(os.path.join(out, f"{name}.png"))
        print(f"sheet {name}: {os.path.join(out, name + '.png')}")


if __name__ == "__main__":
    main(sys.argv[1])

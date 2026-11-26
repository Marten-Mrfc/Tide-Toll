#!/usr/bin/env python3
"""Joins the frame strips of tools/motion-strips.sh into contact sheets: build/motion/NAME.png, 4 columns by 3 rows of
640 x 360 frames, the frame number in a corner of each (usage: motion-sheet.py OUT_DIR)."""
import os
import sys
from PIL import Image, ImageDraw, ImageFont

NAMES = ["pause", "resume", "settings-open", "tab-forward", "tab-back", "settings-close", "question", "age-page", "achievements", "age-panel"]
COLS, ROWS, W, H, GAP = 4, 3, 640, 360, 6


def main(out):
    frames = os.path.join(out, "frames")
    try:
        font = ImageFont.truetype("DejaVuSans-Bold.ttf", 26)
    except OSError:
        font = ImageFont.load_default()
    made = 0
    for name in NAMES:
        paths = [os.path.join(frames, f"{name}-{k:02d}.png") for k in range(COLS * ROWS)]
        if not all(os.path.exists(p) for p in paths):
            print(f"sheet {name}: frames missing, skipped")
            continue
        sheet = Image.new("RGB", (COLS * W + (COLS + 1) * GAP, ROWS * H + (ROWS + 1) * GAP), (30, 26, 22))
        draw = ImageDraw.Draw(sheet)
        for k, p in enumerate(paths):
            im = Image.open(p).convert("RGB")
            if im.size != (W, H):
                im = im.resize((W, H), Image.LANCZOS)
            x, y = GAP + (k % COLS) * (W + GAP), GAP + (k // COLS) * (H + GAP)
            sheet.paste(im, (x, y))
            label = f"{k + 1}"
            draw.rectangle([x + 6, y + 6, x + 46, y + 40], fill=(0, 0, 0))
            draw.text((x + 12, y + 8), label, fill=(255, 255, 255), font=font)
        sheet.save(os.path.join(out, f"{name}.png"))
        made += 1
        print(f"sheet {name}: {os.path.join(out, name + '.png')}")
    return 0 if made == len(NAMES) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "build/motion"))

#!/usr/bin/env python3
"""Joins tools/work-strips.sh's frames into one contact sheet per job: OUT/work-JOB.png. Four columns; the first two
rows are the eight frames at the normal view, the last two the same moments close up (960 x 540 each, real pixels),
each with its number and the caption the test wrote (time from the start of work or the delivery, the villager's
phase). With a second argument, a JPEG copy of each sheet goes there (usage: work-sheet.py OUT_DIR [JPEG_DIR])."""
import glob
import os
import sys
from PIL import Image, ImageDraw, ImageFont

COLS, W, H, GAP, N = 4, 960, 540, 6, 8


def main(out, jpeg=None):
    frames = os.path.join(out, "frames")
    try:
        font = ImageFont.truetype("DejaVuSans-Bold.ttf", 24)
    except OSError:
        font = ImageFont.load_default()
    jobs = sorted({os.path.basename(p)[5:-14] for p in glob.glob(os.path.join(frames, "work-*-normal-00.png"))})
    for job in jobs:
        rows = 2 * ((N + COLS - 1) // COLS)
        sheet = Image.new("RGB", (COLS * W + (COLS + 1) * GAP, rows * H + (rows + 1) * GAP), (30, 26, 22))
        draw = ImageDraw.Draw(sheet)
        for view, r0 in (("normal", 0), ("close", rows // 2)):
            for k in range(N):
                p = os.path.join(frames, f"work-{job}-{view}-{k:02d}.png")
                if not os.path.exists(p):
                    continue
                cap_path = os.path.join(frames, f"work-{job}-{k:02d}.txt")
                cap = open(cap_path).read().strip() if os.path.exists(cap_path) else f"{job} {k + 1}"
                im = Image.open(p).convert("RGB")
                x, y = GAP + (k % COLS) * (W + GAP), GAP + (r0 + k // COLS) * (H + GAP)
                sheet.paste(im, (x, y))
                label = f"{cap}  ({view})"
                tw = draw.textlength(label, font=font)
                draw.rectangle([x + 6, y + 6, x + 18 + tw, y + 38], fill=(0, 0, 0))
                draw.text((x + 12, y + 9), label, fill=(255, 255, 255), font=font)
        path = os.path.join(out, f"work-{job}.png")
        sheet.save(path)
        print(f"sheet {job}: {path}")
        if jpeg:
            os.makedirs(jpeg, exist_ok=True)
            jp = os.path.join(jpeg, f"work-{job}.jpg")
            sheet.save(jp, quality=82, optimize=True)
            print(f"jpeg {job}: {jp}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)

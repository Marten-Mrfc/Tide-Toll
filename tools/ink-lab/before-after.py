#!/usr/bin/env python3
"""n8: the before/after boards from two --ink-board runs (game/tests/AssetAudit.InkBoard.cs), all 1:1 pixels of a
1920 x 1080 window.
   python3 tools/ink-lab/before-after.py BEFORE_DIR AFTER_DIR OUT_DIR FONT
Writes ink-before-and-after.png (every kind in pairs, the street, the busy town) and ink-window-sizes.png."""
import sys
from PIL import Image, ImageDraw, ImageFont

B, A, OUT, FONTP = sys.argv[1:5]
FONT = ImageFont.truetype(FONTP, 22)
STRIP, GAP = 38, 10
WHITE, WHEAT, DARK, BG = (255, 255, 255), (0xC9, 0xA5, 0x5A), (0x2B, 0x26, 0x20), (0x1C, 0x19, 0x15)
KINDS = ["cottage-a0", "cottage-a1", "cottage-a2", "cottage-b0", "cottage-b1", "cottage-b2", "cottage-c0", "cottage-c1",
         "cottage-c2", "weaver-cottage", "mill", "market-stall", "well", "villagers-group"]

def panel(img, label, colour):
    p = Image.new("RGB", (img.width, img.height + STRIP), DARK)
    ImageDraw.Draw(p).text((10, 7), label, font=FONT, fill=colour)
    p.paste(img, (0, STRIP))
    return p

def pair(name, box, label):
    b = Image.open(f"{B}/board-{name}.png").convert("RGB").crop(box)
    a = Image.open(f"{A}/board-{name}.png").convert("RGB").crop(box)
    return [panel(b, f"{label}: now", WHITE), panel(a, f"{label}: new", WHEAT)]

def stack(rows, path):
    width = max(sum(p.width for p in r) + GAP * (len(r) + 1) for r in rows)
    height = sum(max(p.height for p in r) for r in rows) + GAP * (len(rows) + 1)
    out = Image.new("RGB", (width, height), BG)
    y = GAP
    for r in rows:
        x = GAP
        for p in r:
            out.paste(p, (x, y)); x += p.width + GAP
        y += max(p.height for p in r) + GAP
    out.save(path)
    print(path, out.size)

rows, cur = [], []
for k in KINDS:                      # 640 x 480 pictures: the middle 400 x 300
    cur += pair(k, (120, 90, 520, 390), k)
    if len(cur) == 6: rows.append(cur); cur = []
if cur: rows.append(cur)
rows.append(pair("street", (0, 90, 960, 390), "street"))
rows.append(pair("town", (480, 270, 1440, 810), "busy town (middle of the 1920 x 1080 view)"))
stack(rows, f"{OUT}/ink-before-and-after.png")

wrow_b, wrow_a = [], []
for h in (720, 900, 1080, 1440):
    wrow_b.append(panel(Image.open(f"{B}/board-window-{h}.png").convert("RGB"), f"{h}: now", WHITE))
    wrow_a.append(panel(Image.open(f"{A}/board-window-{h}.png").convert("RGB"), f"{h}: new", WHEAT))
stack([wrow_b, wrow_a], f"{OUT}/ink-window-sizes.png")

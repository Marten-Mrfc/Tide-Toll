#!/usr/bin/env python3
"""Research n8: the two boards for the owner from the ink lab's pictures (build/n8/*).
   python3 tools/ink-lab/board.py LABROOT OUTDIR
Board 1 (ink-now-and-options.png): the owner's picture and the same cottage (cottage-a2) at the same view and size.
Board 2 (ink-options-street-town.png): the same options on a street, the busy town and the whole page."""
import sys
from PIL import Image, ImageDraw, ImageFont

R, OUT = sys.argv[1], sys.argv[2]
FONT = ImageFont.truetype(sys.argv[3] if len(sys.argv) > 3 else "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 22)
STRIP, GAP = 38, 10
WHITE, WHEAT, DARK, BG = (255, 255, 255), (0xC9, 0xA5, 0x5A), (0x2B, 0x26, 0x20), (0x1C, 0x19, 0x15)

def panel(img, label, colour):
    p = Image.new("RGB", (img.width, img.height + STRIP), DARK)
    ImageDraw.Draw(p).text((10, 7), label, font=FONT, fill=colour)
    p.paste(img, (0, STRIP))
    return p

def grid(panels, cols, path):
    w = max(p.width for p in panels); h = max(p.height for p in panels)
    rows = (len(panels) + cols - 1) // cols
    b = Image.new("RGB", (cols * w + (cols + 1) * GAP, rows * h + (rows + 1) * GAP), BG)
    for i, p in enumerate(panels):
        b.paste(p, (GAP + (i % cols) * (w + GAP), GAP + (i // cols) * (h + GAP)))
    b.save(path)
    print(path, b.size)

# Board 1. The lab pictures are 640 x 480 at the 1080p play view's metres per pixel; the cottage is in the middle.
# The owner's picture (365 x 267) shows the cottage about 1.22 times larger than a 1080p screen does: it is brought
# to the game's scale (300 x 219, Lanczos) so every panel covers the same ground. All panels then 2x, nearest.
BOX = (170, 131, 470, 350)
def cot(sub, var, subject="cottage0-ext2"):
    return Image.open(f"{R}/{sub}/ink-{subject}-{var}.png").convert("RGB").crop(BOX)
owner = Image.open(f"{R}/owner-picture.png").convert("RGB").resize((300, 219), Image.LANCZOS)
two = lambda im: im.resize((im.width * 2, im.height * 2), Image.NEAREST)
b1 = [
    panel(two(owner), "Owner's picture", WHITE),
    panel(two(cot("lab2", "now")), "Now (1920 x 1080)", WHITE),
    panel(two(cot("lab2", "thin")), "Thinner line", WHEAT),
    panel(two(cot("lab2", "inner-colour")), "Inner lines in the roof's own colour", WHEAT),
    panel(two(cot("few", "now")), "Fewer, wider courses", WHEAT),
    panel(two(cot("flat", "now")), "Courses painted flat", WHEAT),
    panel(two(cot("lab2", "ssaa2-thin")), "Drawn at 2x, scaled down", WHEAT),
    panel(two(cot("flat", "rec2")), "Recommended", WHEAT),
]
grid(b1, 4, f"{OUT}/ink-now-and-options.png")

# Board 2: street (lab, 640 x 480 at play m/px, cropped to the five cottages), the busy town (1280 x 720 at play
# m/px, the left 760 x 430) and the whole page (1280 x 720 at 1:1, the 760 x 428 around the town).
def street(sub, var):
    return Image.open(f"{R}/{sub}/ink-street-{var}.png").convert("RGB").crop((20, 110, 620, 400)).resize((760, 367), Image.NEAREST)
def town(sub, var):
    return Image.open(f"{R}/{sub}/ink-town-{var}.png").convert("RGB").crop((0, 110, 760, 540))
def page(sub, var):
    # With the whole page in view the game samples with a step of 1 px at 1080p too (LinePx gives 1.0 there):
    # "now720" is that picture; the lab's "now" (step 2) would overstate it.
    var = "now720" if (sub, var) == ("town", "now") else var
    return Image.open(f"{R}/{sub}/ink-page-{var}.png").convert("RGB").crop((284, 0, 1044, 428))
cols = [("Now", "lab2", "now", "town", "now"), ("Thinner line", "lab2", "thin", "town", "thin"),
        ("Inner lines in the roof's own colour", "lab2", "inner-colour", "town", "inner-colour"),
        ("Courses painted flat", "flat", "now", "flat", "now"), ("Recommended", "flat", "rec2", "flat", "rec2")]
b2 = []
for maker in (street, town, page):
    for label, s1, v1, s2, v2 in cols:
        img = maker(s1, v1) if maker is street else maker(s2, v2)
        b2.append(panel(img, label, WHITE if label == "Now" else WHEAT))
grid(b2, 5, f"{OUT}/ink-options-street-town.png")

#!/usr/bin/env python3
"""Contact sheets of the icons grouped by kind, rendered by Godot's own SVG renderer on the game's Paper:
  DIR/icons-<group>-24-48.png   every icon of the group at 24 and 48 px with its name
  DIR/icons-<group>-192.png     the same at 192 px
  DIR/icons-all-24.png          every icon at 24 px beside its neighbours (the test: can you tell them apart?)
Groups by file name prefix: shop, boon, perk/milestone, ach (first age, river), msg, nav, and the older sets.
    python3 tools/asset-audit/icon_sheets.py DIR
"""
import os, subprocess, sys, tempfile
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PAPER, INK = (0xF2, 0xEA, 0xD8), (0x2B, 0x26, 0x20)
RIVER_ACH = {"first-mooring", "full-hold", "ten-holds", "fifty-holds", "wet-feet", "ferryman", "dry-shoes", "raise-the-bridge",
             "toll-keeper", "first-fleece", "weavers-pride", "skippers-friend", "two-berths", "the-crane", "river-trade", "borough"}


def group_of(n):
    if n.startswith("ach-"):
        return "ach-river" if n[4:] in RIVER_ACH else "ach-first"
    for p in ("shop", "boon", "msg", "nav"):
        if n.startswith(p + "-"):
            return p
    if n.startswith(("perk-", "milestone-")):
        return "perk-milestone"
    return "older"


def render(paths, sizes, tmp):
    jobs = [f"{p}\t{tmp}/{os.path.basename(p)[:-4]}-{s}.png\t{s}" for p in paths for s in sizes]
    lst = os.path.join(tmp, "jobs.txt")
    open(lst, "w").write("\n".join(jobs) + "\n")
    subprocess.run([os.environ.get("GODOT", "godot"), "--headless", "--path", os.path.join(ROOT, "tools/contact-sheet"),
                    "-s", "res://svg_to_png.gd", "--", f"--list={lst}"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=900)


def get(tmp, n, s):
    f = f"{tmp}/{n}-{s}.png"
    return Image.open(f).convert("RGBA") if os.path.exists(f) else None


def sheet_small(names, tmp, out, title):
    cw, ch, cols = 210, 84, 6
    rows = (len(names) + cols - 1) // cols
    im = Image.new("RGB", (cw * cols + 20, ch * rows + 40), PAPER)
    d = ImageDraw.Draw(im)
    d.text((10, 10), title, fill=INK)
    for i, n in enumerate(names):
        x, y = 10 + (i % cols) * cw, 34 + (i // cols) * ch
        a, b = get(tmp, n, 24), get(tmp, n, 48)
        if a: im.paste(a, (x + 8, y + 12), a)
        if b: im.paste(b, (x + 44, y), b)
        d.text((x + 4, y + 56), n, fill=INK)
    im.save(out)


def sheet_big(names, tmp, out, title):
    cw, ch, cols = 220, 230, 6
    rows = (len(names) + cols - 1) // cols
    im = Image.new("RGB", (cw * cols + 20, ch * rows + 40), PAPER)
    d = ImageDraw.Draw(im)
    d.text((10, 10), title, fill=INK)
    for i, n in enumerate(names):
        x, y = 10 + (i % cols) * cw, 30 + (i // cols) * ch
        c = get(tmp, n, 192)
        if c: im.paste(c, (x + 10, y), c)
        d.text((x + 10, y + 198), n, fill=INK)
    im.save(out)


def sheet_all24(groups, tmp, out):
    cols, cw, ch = 16, 96, 44
    lines = []
    for g, names in groups:
        lines.append(("title", g))
        for k in range(0, len(names), cols):
            lines.append(("row", names[k:k + cols]))
    im = Image.new("RGB", (cols * cw + 20, sum(16 if t == "title" else ch for t, _ in lines) + 20), PAPER)
    d = ImageDraw.Draw(im)
    y = 10
    for t, v in lines:
        if t == "title":
            d.text((10, y), v, fill=INK); y += 16; continue
        for i, n in enumerate(v):
            a = get(tmp, n, 24)
            x = 10 + i * cw
            if a: im.paste(a, (x + 36, y), a)
            d.text((x + 2, y + 28), n.split("-", 1)[1][:15], fill=INK)
        y += ch
    im.save(out)


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    idir = os.path.join(ROOT, "assets/icons")
    paths = [os.path.join(idir, f) for f in sorted(os.listdir(idir)) if f.endswith(".svg")]
    names = [os.path.basename(p)[:-4] for p in paths]
    order = ["shop", "boon", "perk-milestone", "ach-first", "ach-river", "msg", "nav", "older"]
    groups = [(g, [n for n in names if group_of(n) == g]) for g in order]
    with tempfile.TemporaryDirectory() as tmp:
        render(paths, (24, 48, 192), tmp)
        for g, ns in groups:
            if not ns: continue
            sheet_small(ns, tmp, os.path.join(out, f"icons-{g}-24-48.png"), f"{g}: 24 and 48 px on Paper")
            sheet_big(ns, tmp, os.path.join(out, f"icons-{g}-192.png"), f"{g}: 192 px on Paper")
        sheet_all24([gn for gn in groups if gn[0] != "older"], tmp, os.path.join(out, "icons-all-24.png"))
    print(f"icon sheets: {len(names)} icons in {sum(1 for g in groups if g[1])} groups -> {out}")


if __name__ == "__main__":
    main()

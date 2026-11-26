#!/usr/bin/env python3
"""Asset audit, flat assets: every texture (assets/textures/*.png) and icon (assets/icons/*.svg) on the game's Paper,
at real size and enlarged, as DIR/<kind>-<name>.png (640 x 480). Icons are rasterised by Godot's own SVG renderer
(tools/contact-sheet/svg_to_png.gd, headless), as the game draws them, at 24, 48 and 192 px.
Writes DIR/flat.jsonl (name, kind, file, size).
    python3 tools/asset-audit/flat.py DIR
"""
import json, os, subprocess, sys, tempfile
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PAPER = (0xF2, 0xEA, 0xD8)
INK = (0x2B, 0x26, 0x20)


def panel():
    return Image.new("RGB", (640, 480), PAPER)


def label(im, text, xy):
    ImageDraw.Draw(im).text(xy, text, fill=INK)


def texture(path, out):
    src = Image.open(path).convert("RGBA")
    im = panel()
    w, h = src.size
    # Real size on the left (cut to 300 x 440 if larger), enlarged (nearest) on the right.
    real = src.crop((0, 0, min(w, 300), min(h, 440)))
    im.paste(real, (10, 30), real)
    label(im, f"real size {w}x{h}" + (" (cut)" if w > 300 or h > 440 else ""), (10, 10))
    crop = src.crop((0, 0, min(w, 64), min(h, 64)))
    k = max(1, min(300 // crop.size[0], 400 // crop.size[1]))
    big = crop.resize((crop.size[0] * k, crop.size[1] * k), Image.NEAREST)
    im.paste(big, (330, 30), big)
    label(im, f"top-left {crop.size[0]}x{crop.size[1]} px x{k}", (330, 10))
    im.save(out)
    return [w, h]


def icons(paths, outdir, tmp):
    jobs, sizes = [], (24, 48, 192)
    for p in paths:
        n = os.path.splitext(os.path.basename(p))[0]
        for s in sizes:
            jobs.append(f"{p}\t{tmp}/{n}-{s}.png\t{s}")
    lst = os.path.join(tmp, "jobs.txt")
    open(lst, "w").write("\n".join(jobs) + "\n")
    godot = os.environ.get("GODOT", "godot")
    subprocess.run([godot, "--headless", "--path", os.path.join(ROOT, "tools/contact-sheet"), "-s", "res://svg_to_png.gd",
                    "--", f"--list={lst}"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=600)
    done = {}
    for p in paths:
        n = os.path.splitext(os.path.basename(p))[0]
        im = panel()
        x = 20
        ok = True
        for s in sizes:
            f = f"{tmp}/{n}-{s}.png"
            if not os.path.exists(f):
                ok = False
                continue
            ic = Image.open(f).convert("RGBA")
            y = 240 - ic.size[1] // 2
            im.paste(ic, (x, y), ic)
            label(im, f"{s} px", (x, 240 + s // 2 + 8))
            x += s + 60
        # On Ink as well (the icons must read on dark grounds).
        f = f"{tmp}/{n}-48.png"
        if os.path.exists(f):
            ic = Image.open(f).convert("RGBA")
            ImageDraw.Draw(im).rectangle((540, 200, 620, 280), fill=INK)
            im.paste(ic, (556, 216), ic)
            label(im, "on Ink", (556, 290))
        label(im, n + ("" if ok else "  (not all sizes rendered)"), (20, 20))
        im.save(os.path.join(outdir, f"icon-{n}.png"))
        done[n] = ok
    return done


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    rows = []
    tdir = os.path.join(ROOT, "assets/textures")
    for f in sorted(os.listdir(tdir)):
        if f.endswith(".png"):
            n = f[:-4]
            size = texture(os.path.join(tdir, f), os.path.join(out, f"texture-{n}.png"))
            rows.append({"name": f"texture-{n}", "kind": "texture", "file": f"assets/textures/{f}", "pixels": size})
    idir = os.path.join(ROOT, "assets/icons")
    paths = [os.path.join(idir, f) for f in sorted(os.listdir(idir)) if f.endswith(".svg")]
    with tempfile.TemporaryDirectory() as tmp:
        done = icons(paths, out, tmp)
    for p in paths:
        n = os.path.splitext(os.path.basename(p))[0]
        rows.append({"name": f"icon-{n}", "kind": "icon", "file": f"assets/icons/{os.path.basename(p)}", "rendered": done.get(n, False)})
    with open(os.path.join(out, "flat.jsonl"), "w") as fh:
        for r in rows:
            fh.write(json.dumps(r) + "\n")
    print(f"flat: {sum(r['kind'] == 'texture' for r in rows)} textures, {sum(r['kind'] == 'icon' for r in rows)} icons")


if __name__ == "__main__":
    main()

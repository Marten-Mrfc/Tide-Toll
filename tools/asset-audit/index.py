#!/usr/bin/env python3
"""Asset audit, index and sheets: reads DIR/models.jsonl, flat.jsonl, sounds.jsonl (written by the picture steps) and
writes DIR/assets.json (name, kind, file, source script, triangles and size for models, where the game uses it,
ledger row) and DIR/sheet-<kind>-<n>.png (at most 12 assets a sheet; models show normal and close side by side,
each tile named underneath). With --jpeg=DIR2 also copies every -normal and -close picture there as JPEG (q 85).
    python3 tools/asset-audit/index.py DIR [--jpeg=DIR2]
"""
import csv, glob, json, os, re, sys
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PAPER, INK = (0xF2, 0xEA, 0xD8), (0x2B, 0x26, 0x20)

KINDS = [  # first match wins (model and code-built names)
    (r"^villager", "person"),
    (r"^town-field", "nature"),
    (r"^town-", "building"),
    (r"^(crossing|landing-|river-)", "river"),
    (r"^(tree-|trees-|reeds|field-|pasture|sheep|prop-apple-tree)", "nature"),
    (r"^(barge|rowboat|drawbridge|footbridge|ferry|ford-stones|landing|tide-post|crane|river)", "river"),
    (r"^(cottage|well|market-stall|mill|keeper-hut|weaver-cottage)", "building"),
    (r"^(prop-|fence-|plot-stakes|charter-stone|seal-stone|bell-post|hand-)", "prop"),
    (r"^book", "book"),
    (r"^(road|ground)", "ground"),
]

# Which generator writes each model (from the ledger where it names one; these fill the gaps).
SCRIPT_BY_PREFIX = [
    ("villager-cargo", "assets/src/models/villager.py"), ("villager", "assets/src/models/villager.py"),
    ("prop-", "assets/src/models/town-props.py"), ("tree-", "assets/src/models/tree.py"),
    ("field-", "assets/src/models/field.py"), ("fence-", "assets/src/models/fence.py"),
    ("ferry-", "assets/src/models/ferry.py"), ("footbridge-", "assets/src/models/footbridge.py"),
    ("drawbridge-", "assets/src/models/drawbridge.py"), ("cottage-", "assets/src/models/cottage.py"),
]


def kind_of(name):
    for pat, k in KINDS:
        if re.search(pat, name):
            return k
    return "prop"


def ledger():
    rows = {}
    with open(os.path.join(ROOT, "assets/LEDGER.csv"), newline="") as fh:
        for r in csv.DictReader(fh):
            rows[r["item"]] = r
    return rows


def script_of(name, file, led):
    r = led.get(file)
    if r:
        m = re.search(r"(assets/src/\S+?\.(py|gd|gdshader|sh))", r["source"])
        if m:
            return m.group(1)
    if file.startswith("code:"):
        return file[6:].split(" ")[0]
    for pre, s in SCRIPT_BY_PREFIX:
        if name.startswith(pre):
            return s
    return ""


_SOURCES = None


def sources():
    global _SOURCES
    if _SOURCES is None:
        _SOURCES = []
        for p in sorted(glob.glob(os.path.join(ROOT, "game/scripts/**/*.cs"), recursive=True)):
            _SOURCES.append((os.path.relpath(p, ROOT), open(p, encoding="utf-8").read().split("\n")))
    return _SOURCES


def used_in(stem):
    """Lines of game/scripts that name the asset: "stem" as a string, its file name, or a "prefix-{" pattern."""
    pats = [f'"{stem}"', f"{stem}.", f"/{stem}"]
    if "-" in stem:
        pats.append(stem.rsplit("-", 1)[0] + "-{")
        pats.append(stem[:-1] + "{")  # cottage-0{1 + extensions}
        rest = stem.split("-", 1)[1]
        if len(rest) >= 5:
            pats.append(f'"{rest}"')  # ground-{g} with g in { "grass", "hollow-way" }
    out = []
    for rel, lines in sources():
        for i, line in enumerate(lines, 1):
            if any(p in line for p in pats):
                out.append(f"{rel}:{i}")
                break
    return out


def font(size):
    for f in ("assets/fonts/AtkinsonHyperlegible-Regular.ttf",):
        p = os.path.join(ROOT, f)
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def sheets(out, items):
    f = font(18)
    for kind in sorted({i["kind"] for i in items}):
        group = [i for i in items if i["kind"] == kind and i.get("pictures")]
        for n in range(0, len(group), 12):
            part = group[n:n + 12]
            pair = kind in ("building", "person", "nature", "river", "prop", "book", "ground")
            tw, th = (640, 240) if pair or kind == "sound" else (320, 240)
            cols = 3 if pair else 2 if kind == "sound" else 4
            rows = (len(part) + cols - 1) // cols
            sheet = Image.new("RGB", (cols * (tw + 8) + 8, rows * (th + 34) + 8), PAPER)
            d = ImageDraw.Draw(sheet)
            for k, it in enumerate(part):
                x, y = 8 + (k % cols) * (tw + 8), 8 + (k // cols) * (th + 34)
                pics = it["pictures"]
                w = tw // len(pics)
                for j, p in enumerate(pics):
                    im = Image.open(os.path.join(out, p)).convert("RGB")
                    im.thumbnail((w, th))
                    sheet.paste(im, (x + j * w, y))
                d.text((x, y + th + 6), it["name"] + (f"  ({it['triangles']} tri)" if it.get("triangles") else "  (in the town)" if it.get("made_in_code") and it["name"].startswith(("town-", "road-", "river-", "landing-", "crossing-")) else ""), fill=INK, font=f)
            sheet.save(os.path.join(out, f"sheet-{kind}-{n // 12 + 1}.png"))


def main():
    out = sys.argv[1]
    jpeg = next((a[7:] for a in sys.argv[2:] if a.startswith("--jpeg=")), None)
    led = ledger()
    items = []

    def rows(fn):
        p = os.path.join(out, fn)
        return [json.loads(l) for l in open(p) if l.strip()] if os.path.exists(p) else []

    for r in rows("models.jsonl"):
        name, file = r["name"], r["file"]
        pics = [p for p in (f"{name}-normal.png", f"{name}-close.png") if os.path.exists(os.path.join(out, p))]
        stem = name if not file.startswith("code:") else None
        items.append({
            "name": name, "kind": kind_of(name), "file": file if not file.startswith("code:") else "",
            "made_in_code": file.startswith("code:"), "source": script_of(name, file, led),
            "triangles": r["triangles"], "size_m": r["size_m"],
            "used_in": used_in(stem) if stem else [file[6:].split(" ")[0]],
            "ledger_row": file in led, "pictures": pics,
        })
    for r in rows("flat.jsonl"):
        stem = os.path.splitext(os.path.basename(r["file"]))[0]
        items.append({
            "name": r["name"], "kind": r["kind"], "file": r["file"], "made_in_code": False,
            "source": script_of(r["name"], r["file"], led), "pixels": r.get("pixels"),
            "used_in": used_in(stem), "ledger_row": r["file"] in led,
            "pictures": [r["name"] + ".png"] if os.path.exists(os.path.join(out, r["name"] + ".png")) else [],
        })
    for r in rows("sounds.jsonl"):
        items.append({
            "name": r["name"], "kind": "sound", "file": r["file"], "made_in_code": False,
            "source": script_of(r["name"], r["file"], led), "length_s": r.get("length_s"), "peak_dbfs": r.get("peak_dbfs"),
            "lufs": r.get("lufs"), "used_in": r.get("used_in", []), "ledger_row": r["file"] in led,
            "pictures": [r["name"] + ".png"] if os.path.exists(os.path.join(out, r["name"] + ".png")) else [],
        })
    json.dump({"assets": items}, open(os.path.join(out, "assets.json"), "w"), indent=1)
    for old in glob.glob(os.path.join(out, "sheet-*.png")):
        os.remove(old)
    sheets(out, items)
    counts = {}
    for i in items:
        counts[i["kind"]] = counts.get(i["kind"], 0) + 1
    print("index:", " ".join(f"{k}={v}" for k, v in sorted(counts.items())), f"total={len(items)}")
    if jpeg:
        os.makedirs(jpeg, exist_ok=True)
        n = 0
        for p in sorted(glob.glob(os.path.join(out, "*-normal.png")) + glob.glob(os.path.join(out, "*-close.png"))):
            Image.open(p).convert("RGB").save(os.path.join(jpeg, os.path.basename(p)[:-4] + ".jpg"), quality=85)
            n += 1
        print(f"jpeg: {n} pictures in {jpeg}")


if __name__ == "__main__":
    main()

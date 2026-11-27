#!/usr/bin/env python3
"""Builds the owner's review page for night 3 from docs/review/n3/manifest.json.

Writes <out>/index.html (content-only HTML as the Artifact tool expects), JPEG copies (quality 80) of every picture in
<out>/img/, MP3 copies of the new sounds in <out>/audio/, <out>/files.json (published path -> source path, relative to
the repository; split into files-1.json, files-2.json, ... of at most 250 entries when there are more) and <out>/list.md
(the same numbered list as plain text, for the chat).

Each picture in the manifest has a "path" (the final picture) and may have "or" (stand-ins tried in order while the path
does not exist; the page marks them). Items with "pending": true are left out and listed. "optional" pictures are shown
when they exist and are never counted as missing. An item whose required picture is missing is left out and listed.

Usage: python3 docs/review/n3/build_review.py [--manifest FILE] [--out DIR] [--strict] [--no-standins]
  --strict       exit 1 if a picture of an item not marked pending is missing (stand-ins still count as present)
  --no-standins  ignore "or" stand-ins: only the final pictures count (use with --strict for the final build)
"""
import argparse
import html
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(HERE))
from make_closing import closing  # noqa: E402
from page_style import CSS, JS  # noqa: E402

MAX_PER_FILES_JSON = 250


def esc(s):
    return html.escape(str(s), quote=True)


def resolve(pic, standins):
    cands = [pic["path"]] + (pic.get("or", []) if standins else [])
    for i, c in enumerate(cands):
        if (ROOT / c).is_file():
            return c, i > 0
    return None, False


def jpeg(src: Path, dst: Path, quality=80):
    """A JPEG copy; skipped when the copy is newer than its source. Returns the picture's size."""
    if dst.exists() and dst.stat().st_mtime >= src.stat().st_mtime:
        with Image.open(dst) as im:
            return im.size
    dst.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(src) as im:
        im = im.convert("RGB")
        im.save(dst, "JPEG", quality=quality, optimize=True, progressive=True)
        return im.size


def mp3(src: Path, dst: Path):
    if dst.exists() and dst.stat().st_mtime >= src.stat().st_mtime:
        return
    dst.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(src), "-codec:a", "libmp3lame", "-q:a", "4", str(dst)], check=True)


def figure(name, size, label, alt, standin):
    w, h = size
    stand = ' <span class="stand">(stand-in: not yet the final picture)</span>' if standin else ""
    return (f'<figure><button type="button" class="shot" data-full="{name}" aria-label="{esc(label)}: open at full size">'
            f'<img src="{name}" width="{w}" height="{h}" loading="lazy" alt="{esc(alt)}"></button>'
            + (f"<figcaption>{esc(label)}{stand}</figcaption>" if label not in ("Before", "After") or stand else "") + "</figure>")


def plate(n, item, figs):
    sides = [f for f in figs if f[0] in ("before", "after")]
    if sides:
        cols = []
        for side, title in (("before", "Before"), ("after", "After")):
            these = [f[1] for f in figs if f[0] == side]
            if these:
                cols.append(f'<div class="side {side}"><span class="tag">{title}</span><div class="figs">{"".join(these)}</div></div>')
        body = f'<div class="pair">{"".join(cols)}</div>'
        rest = [f[1] for f in figs if f[0] not in ("before", "after")]
        if rest:
            body += f'<div class="row">{"".join(rest)}</div>'
    else:
        wide = " wide" if len(figs) == 1 else ""
        body = f'<div class="row{wide}">{"".join(f[1] for f in figs)}</div>'
    return (f'<article class="plate" id="item-{n}" data-n="{n}" data-id="{esc(item["id"])}">{body}'
            f'<div class="cap"><span class="num">{n}</span><p class="shows">{esc(item["shows"])}</p>'
            f'<div class="ask"><span class="q">{esc(item["question"])}</span>'
            f'<span class="yn" role="group" aria-label="Answer to {n}"><button type="button" class="yes" aria-pressed="false">Yes</button>'
            f'<button type="button" class="no" aria-pressed="false">No</button></span></div></div></article>')


def closing_html(sec, out: Path, files, missing):
    parts = [f'<section class="notes" id="{esc(sec["id"])}"><h2>{esc(sec["title"])}</h2>']
    kind = sec.get("kind", "text")
    for para in sec.get("paras", []):
        if para:
            parts.append(f"<p>{para}</p>")
    if kind == "text" and sec.get("items"):
        if sec.get("items_title"):
            parts.append(f"<h3>{esc(sec['items_title'])}</h3>")
        parts.append("<ul>" + "".join(f"<li>{i}</li>" for i in sec["items"]) + "</ul>")
    elif kind == "two":
        parts.append('<div class="two">')
        for col in sec["columns"]:
            parts.append(f"<div><h3>{esc(col['title'])}</h3><ul>" + "".join(f"<li>{i}</li>" for i in col["items"]) + "</ul></div>")
        parts.append("</div>")
    elif kind == "table":
        parts.append('<div class="tablewrap"><table><thead><tr>' + "".join(f"<th>{esc(h)}</th>" for h in sec["head"]) + "</tr></thead><tbody>")
        for row in sec["rows"]:
            parts.append("<tr>" + "".join(f'<td class="n">{c}</td>' if i == 0 and sec.get("numbered") else f"<td>{c}</td>"
                                          for i, c in enumerate(row)) + "</tr>")
        parts.append("</tbody></table></div>")
    elif kind == "music":
        parts.append('<ul class="tunes">')
        for t in sec["tunes"]:
            src = ROOT / t["file"]
            if not src.is_file():
                missing.append(("sounds", t["file"]))
                continue
            name = "audio/" + src.stem + ".mp3"
            mp3(src, out / name)
            files[name] = os.path.relpath(out / name, ROOT)
            parts.append(f'<li><div><strong>{esc(t["title"])}</strong><br><small>{esc(t["note"])}</small></div>'
                         f'<audio controls preload="none" src="{name}"></audio></li>')
        parts.append("</ul>")
    parts.append("</section>")
    return "".join(parts)


def build(m, out: Path, standins=True):
    files, missing, pending, report, listing, body, copied = {}, [], [], [], [], [], {}
    n = 0
    for sec in m["sections"]:
        sec_items, sec_pics, sec_stand = 0, 0, 0
        html_items = []
        listing.append(f"\n**{sec['title']}**\n")
        for item in sec["items"]:
            if item.get("pending"):
                pending.append((sec["id"], item["id"], item.get("needs", ", ".join(p["path"] for p in item["pictures"]))))
                continue
            got, lack = [], []
            for p in item["pictures"]:
                src, standin = resolve(p, standins)
                if src:
                    got.append((p, src, standin))
                elif not p.get("optional"):
                    lack.append(p["path"])
            if lack:
                missing.extend((item["id"], x) for x in lack)
                continue
            n += 1
            figs = []
            for k, (p, src, standin) in enumerate(got, 1):
                if src in copied:  # the same picture in two items is published once
                    name, size = copied[src]
                else:
                    name = f"img/{item['id']}-{k}.jpg"
                    size = jpeg(ROOT / src, out / name)
                    copied[src] = (name, size)
                if sec["id"] in ("interface", "messages") and p.get("size") != "strip" and size != (1920, 1080):
                    print(f"warning: {src} is {size[0]} x {size[1]}, not 1920 x 1080", file=sys.stderr)
                files[name] = os.path.relpath(out / name, ROOT)
                figs.append((p.get("side"), figure(name, size, p["label"], item["shows"], standin)))
                sec_pics += 1
                sec_stand += standin
            sec_items += 1
            html_items.append(plate(n, item, figs))
            listing.append(f"{n}. {item['shows']} **{item['question']}**")
        report.append((sec["title"], sec_items, sec_pics, sec_stand))
        if html_items:
            note = f"<p>{sec['note']}</p>" if sec.get("note") else ""
            body.append(f'<section id="{esc(sec["id"])}"><h2>{esc(sec["title"])}</h2>{note}{"".join(html_items)}</section>')

    c = closing()
    closing_parts = [closing_html(s, out, files, missing) for s in c["closing"]]
    shown = [s for s in m["sections"] if any(not i.get("pending") for i in s["items"])]
    total_pics = sum(r[2] for r in report)

    head = [f"<title>{esc(m['title'])}</title>", '<meta name="viewport" content="width=device-width, initial-scale=1">',
            f"<style>{CSS}</style>", '<div class="wrap">', '<header class="lede">',
            f'<div class="eyebrow">{esc(m.get("eyebrow", ""))}</div>', f"<h1>{esc(m.get('heading', m['title']))}</h1>"]
    head += [f"<p>{p}</p>" for p in m.get("intro", [])]
    facts = [f"{n} questions", f"{total_pics} pictures", f"newest build {c['newest']['commit']}"] + m.get("facts", [])
    head.append('<ul class="facts">' + "".join(f"<li>{esc(f)}</li>" for f in facts) + "</ul></header>")
    head.append('<div class="bar"><span class="count" id="count">0 answered</span>'
                '<span class="grow hint">Yes or No under each picture. Your answers stay in this browser.</span>'
                '<button type="button" id="copy">Copy my answers</button><button type="button" id="clear">Clear</button>'
                '<span class="hint" id="copystatus" role="status"></span></div>'
                '<textarea class="copyout" id="copyout" hidden readonly aria-label="Your answers as text"></textarea>')
    toc = [f'<li><a href="#{esc(s["id"])}">{esc(s["title"])}</a></li>' for s in shown]
    toc += [f'<li><a href="#{esc(s["id"])}">{esc(s["title"])}</a></li>' for s in c["closing"]]
    head.append('<nav aria-label="Sections"><ul class="toc">' + "".join(toc) + "</ul></nav>")
    tail = ['</div><div class="zoom" id="zoom" role="dialog" aria-modal="true" aria-label="Picture at full size">'
            '<button type="button" class="close" id="zoomclose">Close</button><img id="zoomimg" alt=""></div>',
            f"<script>{JS}</script>"]

    out.mkdir(parents=True, exist_ok=True)
    (out / "index.html").write_text("\n".join(head + body + closing_parts + tail), encoding="utf-8")
    (out / "list.md").write_text("\n".join(listing).strip() + "\n", encoding="utf-8")
    for old in list(out.glob("files*.json")):
        old.unlink()
    keys = sorted(files)
    if len(keys) <= MAX_PER_FILES_JSON:
        (out / "files.json").write_text(json.dumps(files, indent=1), encoding="utf-8")
        parts = ["files.json"]
    else:
        parts = []
        for i in range(0, len(keys), MAX_PER_FILES_JSON):
            name = f"files-{i // MAX_PER_FILES_JSON + 1}.json"
            (out / name).write_text(json.dumps({k: files[k] for k in keys[i:i + MAX_PER_FILES_JSON]}, indent=1), encoding="utf-8")
            parts.append(name)

    print(f"{'section':<46} {'items':>5} {'pictures':>8} {'stand-ins':>9}")
    for title, i, p, s in report:
        print(f"{title:<46} {i:>5} {p:>8} {s:>9}")
    print(f"{'total':<46} {n:>5} {total_pics:>8} {sum(r[3] for r in report):>9}")
    print(f"closing: newest build {c['newest']['round']} {c['newest']['commit']}; " + ", ".join(f"{k} {v}" for k, v in c["counts"].items()))
    for sec, iid, needs in pending:
        print(f"pending: {iid} ({sec}) needs {needs}")
    for iid, path in missing:
        print(f"missing: {iid}: {path}")
    sizes = [(ROOT / v).stat().st_size for v in files.values()]
    page = (out / "index.html").stat().st_size
    big = [k for k, v in files.items() if (ROOT / v).stat().st_size > 16e6]
    print(f"files beside the page: {len(files)} ({' + '.join(parts)}), {sum(sizes) / 1e6:.1f} MB; page {page / 1e3:.0f} kB; "
          f"largest file {max(sizes, default=0) / 1e6:.2f} MB" + (f"; OVER 16 MB: {big}" if big else ""))
    if sum(sizes) > 64e6 or len(parts) > 1:
        print("publish in several calls to the same url: one per files-N.json (at most 255 files and 64 MB per call)")
    print(f"page: {out / 'index.html'}")
    return n, missing


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--manifest", default=str(HERE / "manifest.json"))
    ap.add_argument("--out", default=str(ROOT / "build/review-n3"))
    ap.add_argument("--strict", action="store_true")
    ap.add_argument("--no-standins", action="store_true")
    a = ap.parse_args()
    out = Path(a.out).resolve()
    if not shutil.which("ffmpeg"):
        print("warning: ffmpeg not found; sounds cannot be converted", file=sys.stderr)
    _, miss = build(json.loads(Path(a.manifest).read_text(encoding="utf-8")), out, standins=not a.no_standins)
    if a.strict and miss:
        print(f"strict: {len(miss)} missing", file=sys.stderr)
        sys.exit(1)

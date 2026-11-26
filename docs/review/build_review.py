#!/usr/bin/env python3
"""Builds the screenshot review page for the owner.

Reads docs/review/review.json (sections of numbered screenshots, each with one line on what it shows and one
yes-or-no question, plus the closing sections) and writes build/review/index.html, JPEG copies of the screenshots
in build/review/img/ and MP3 copies of the music in build/review/audio/. It also writes build/review/list.md:
the same numbered list as plain text for the chat.

Usage: python3 docs/review/build_review.py [--json docs/review/review.json] [--out build/review]
The page is content-only HTML as the Artifact tool expects (no doctype, html, head or body tags).
"""
import argparse
import html
import json
import subprocess
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]


def esc(s):
    return html.escape(str(s), quote=True)


def jpeg(src: Path, dst: Path, quality=84):
    im = Image.open(src).convert("RGB")
    if im.size != (1920, 1080):
        print(f"warning: {src} is {im.size[0]} x {im.size[1]}, not 1920 x 1080", file=sys.stderr)
    dst.parent.mkdir(parents=True, exist_ok=True)
    im.save(dst, "JPEG", quality=quality, optimize=True, progressive=True)
    return im.size


def mp3(src: Path, dst: Path):
    dst.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(src), "-codec:a", "libmp3lame", "-q:a", "4", str(dst)],
                   check=True)


CSS = r"""
/* Layout: a ledger of numbered plates. One column of screenshots at reading width; a slim bar that keeps count of
   the answers; closing notes set like the back pages of the game's book. Colours are the game's own palette. */
:root {
  --paper: #F2EAD8; --page: #FBF6EA; --ink: #2B2620; --faded: #6E655A; --rule: #D6CBB3;
  --leather: #4A241A; --gilt: #9A742A; --river: #3F5F75; --meadow: #4F7A3A; --brick: #A4553A;
  --yes-bg: #DDE9D2; --no-bg: #F1D9CF; --chip: #EAE0C9;
  --display: "Alegreya", Georgia, "Times New Roman", serif;
  --body: "Atkinson Hyperlegible", system-ui, -apple-system, "Segoe UI", sans-serif;
}
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {
  --paper: #1E1612; --page: #2A1F19; --ink: #EFE6D3; --faded: #B5A892; --rule: #4A3A2E;
  --leather: #E7C98A; --gilt: #D9B45A; --river: #9CC0D6; --meadow: #A9C98F; --brick: #E09A80;
  --yes-bg: #2F4226; --no-bg: #4B2A20; --chip: #3A2C23; color-scheme: dark } }
:root[data-theme="dark"] {
  --paper: #1E1612; --page: #2A1F19; --ink: #EFE6D3; --faded: #B5A892; --rule: #4A3A2E;
  --leather: #E7C98A; --gilt: #D9B45A; --river: #9CC0D6; --meadow: #A9C98F; --brick: #E09A80;
  --yes-bg: #2F4226; --no-bg: #4B2A20; --chip: #3A2C23; color-scheme: dark }

body { background: var(--paper); color: var(--ink); font-family: var(--body); font-size: 17px; line-height: 1.5; }
.wrap { max-width: 1120px; margin: 0 auto; padding-block: 28px 64px;
  padding-left: max(16px, env(safe-area-inset-left, 0px)); padding-right: max(16px, env(safe-area-inset-right, 0px));
  display: flex; flex-direction: column; gap: 40px; }
h1, h2, h3 { font-family: var(--display); font-weight: 700; line-height: 1.15; text-wrap: balance; margin: 0; }
h1 { font-size: clamp(2rem, 5vw, 3rem); color: var(--leather); }
h2 { font-size: 1.7rem; padding-bottom: 6px; border-bottom: 2px solid var(--rule); }
h3 { font-size: 1.2rem; }
p { margin: 0; max-width: 68ch; }
a { color: var(--river); }
:focus-visible { outline: 3px solid var(--river); outline-offset: 2px; }
.lede { display: flex; flex-direction: column; gap: 12px; }
.eyebrow { font-size: .8rem; letter-spacing: .12em; text-transform: uppercase; color: var(--faded); }
.facts { display: flex; flex-wrap: wrap; gap: 8px 10px; padding: 0; margin: 0; list-style: none; }
.facts li { background: var(--chip); border-radius: 4px; padding: 4px 10px; font-size: .92rem; }
.toc { display: flex; flex-wrap: wrap; gap: 6px 14px; padding: 0; margin: 0; list-style: none; font-size: .95rem; }

.bar { position: sticky; top: env(safe-area-inset-top, 0px); z-index: 5; background: var(--page); border: 1px solid var(--rule);
  border-radius: 6px; padding: 8px 12px; display: flex; flex-wrap: wrap; align-items: center; gap: 8px 14px; }
body::before { content: ""; position: fixed; inset: 0 0 auto; height: env(safe-area-inset-top, 0px); z-index: 10; background: var(--paper); }
.bar .count { font-variant-numeric: tabular-nums; }
.bar .grow { flex: 1 1 auto; min-width: 0; }
button { font: inherit; color: var(--ink); background: var(--chip); border: 1px solid var(--rule); border-radius: 4px;
  padding: 6px 14px; cursor: pointer; }
button:hover { border-color: var(--faded); }
.copyout { width: 100%; min-height: 7em; font: .9rem/1.4 ui-monospace, Menlo, Consolas, monospace; background: var(--page);
  color: var(--ink); border: 1px solid var(--rule); border-radius: 4px; padding: 8px; }

section { display: flex; flex-direction: column; gap: 22px; }
.plate { display: flex; flex-direction: column; gap: 10px; background: var(--page); border: 1px solid var(--rule);
  border-radius: 6px; padding: 12px; }
.frame { overflow-x: auto; border: 1px solid var(--ink); background: var(--ink); line-height: 0; cursor: zoom-in; }
.frame img { display: block; width: 100%; height: auto; }
.plate.zoomed .frame { cursor: zoom-out; }
.plate.zoomed .frame img { width: 1920px; max-width: none; }
.cap { display: grid; grid-template-columns: auto 1fr; gap: 4px 14px; align-items: baseline; }
.num { font-family: var(--display); font-weight: 700; font-size: 1.6rem; color: var(--gilt); font-variant-numeric: tabular-nums;
  min-width: 2ch; text-align: right; }
.shows { min-width: 0; }
.ask { grid-column: 2; min-width: 0; display: flex; flex-wrap: wrap; align-items: center; gap: 8px 14px; }
.q { font-family: var(--display); font-size: 1.15rem; font-weight: 500; flex: 1 1 22ch; min-width: 0; }
.yn { display: inline-flex; gap: 6px; }
.yn button[aria-pressed="true"].yes { background: var(--yes-bg); border-color: var(--meadow); font-weight: 700; }
.yn button[aria-pressed="true"].no { background: var(--no-bg); border-color: var(--brick); font-weight: 700; }
.hint { color: var(--faded); font-size: .9rem; }

.notes { display: flex; flex-direction: column; gap: 14px; }
.notes ul, .notes ol { margin: 0; padding-left: 1.3em; display: flex; flex-direction: column; gap: 6px; max-width: 75ch; }
.two { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 22px; }
.two > div { min-width: 0; display: flex; flex-direction: column; gap: 10px; }
.tablewrap { overflow-x: auto; }
table { border-collapse: collapse; width: 100%; font-size: .95rem; }
th, td { text-align: left; vertical-align: top; padding: 7px 10px; border-bottom: 1px solid var(--rule); }
th { font-family: var(--display); font-size: 1rem; }
td.n { font-variant-numeric: tabular-nums; color: var(--faded); white-space: nowrap; width: 3ch; }
.tunes { display: flex; flex-direction: column; gap: 12px; padding: 0; margin: 0; list-style: none; }
.tunes li { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 320px); gap: 6px 16px; align-items: center;
  border-bottom: 1px solid var(--rule); padding-bottom: 10px; }
.tunes audio { width: 100%; }
.tunes small { color: var(--faded); }
@media (max-width: 640px) { .tunes li { grid-template-columns: 1fr; } .cap { grid-template-columns: 1fr; } .ask { grid-column: 1; }
  .num { text-align: left; } }
@media (prefers-reduced-motion: no-preference) { button { transition: border-color .12s; } }
"""

JS = r"""
(function () {
  var KEY = 'tide-toll-review-answers-v1';
  var answers = {};
  try { answers = JSON.parse(localStorage.getItem(KEY) || '{}') || {}; } catch (e) { answers = {}; }
  var plates = Array.prototype.slice.call(document.querySelectorAll('.plate'));
  var count = document.getElementById('count');
  var out = document.getElementById('copyout');
  var status = document.getElementById('copystatus');
  function save() { try { localStorage.setItem(KEY, JSON.stringify(answers)); } catch (e) {} }
  function paint() {
    var n = 0;
    plates.forEach(function (p) {
      var id = p.getAttribute('data-n'), a = answers[id];
      if (a) n++;
      p.querySelector('button.yes').setAttribute('aria-pressed', a === 'yes' ? 'true' : 'false');
      p.querySelector('button.no').setAttribute('aria-pressed', a === 'no' ? 'true' : 'false');
    });
    count.textContent = n + ' of ' + plates.length + ' answered';
  }
  function text() {
    var lines = [];
    plates.forEach(function (p) { var id = p.getAttribute('data-n'); if (answers[id]) lines.push(id + ' ' + answers[id]); });
    return lines.length ? lines.join('\n') + '\ncontinue' : '';
  }
  plates.forEach(function (p) {
    var id = p.getAttribute('data-n');
    p.querySelector('button.yes').addEventListener('click', function () { answers[id] = answers[id] === 'yes' ? '' : 'yes'; if (!answers[id]) delete answers[id]; save(); paint(); });
    p.querySelector('button.no').addEventListener('click', function () { answers[id] = answers[id] === 'no' ? '' : 'no'; if (!answers[id]) delete answers[id]; save(); paint(); });
    var f = p.querySelector('.frame');
    f.addEventListener('click', function () { p.classList.toggle('zoomed'); });
    f.addEventListener('keydown', function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); p.classList.toggle('zoomed'); } });
  });
  document.getElementById('copy').addEventListener('click', function () {
    var t = text();
    if (!t) { status.textContent = 'Nothing answered yet.'; return; }
    out.hidden = false; out.value = t;
    function fallback() { out.focus(); out.select(); status.textContent = 'Select the text below and copy it.'; }
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(t).then(function () { status.textContent = 'Copied. Paste it into the chat.'; }, fallback);
    } else { fallback(); }
  });
  document.getElementById('clear').addEventListener('click', function () { answers = {}; save(); paint(); out.hidden = true; status.textContent = 'Cleared.'; });
  paint();
})();
"""


def build(data, out: Path):
    parts = []
    title = data.get("title", "Tide & Toll Night Review")
    parts.append(f"<title>{esc(title)}</title>")
    parts.append('<meta name="claude:viewport-fit-sides" content="cover">')
    parts.append('<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Alegreya:wght@500;700&family=Atkinson+Hyperlegible:wght@400;700&display=swap">')
    parts.append(f"<style>{CSS}</style>")
    parts.append('<div class="wrap">')

    # Lede
    parts.append('<header class="lede">')
    parts.append(f'<div class="eyebrow">{esc(data.get("eyebrow", ""))}</div>')
    parts.append(f"<h1>{esc(data.get('heading', title))}</h1>")
    for para in data.get("intro", []):
        parts.append(f"<p>{para}</p>")
    if data.get("facts"):
        parts.append('<ul class="facts">' + "".join(f"<li>{esc(f)}</li>" for f in data["facts"]) + "</ul>")
    parts.append("</header>")

    # Answer bar
    parts.append('<div class="bar"><span class="count" id="count">0 answered</span><span class="grow hint">Press Yes or No under each picture. Click a picture to see it at full size.</span>'
                 '<button type="button" id="copy">Copy my answers</button><button type="button" id="clear">Clear</button>'
                 '<span class="hint" id="copystatus" role="status"></span></div>')
    parts.append('<textarea class="copyout" id="copyout" hidden readonly aria-label="Your answers as text"></textarea>')

    # Contents
    parts.append('<nav aria-label="Sections"><ul class="toc">')
    for sec in data["sections"]:
        parts.append(f'<li><a href="#{esc(sec["id"])}">{esc(sec["title"])}</a></li>')
    for extra in data.get("closing", []):
        parts.append(f'<li><a href="#{esc(extra["id"])}">{esc(extra["title"])}</a></li>')
    parts.append("</ul></nav>")

    n = 0
    files = {}
    listing = []
    for sec in data["sections"]:
        parts.append(f'<section id="{esc(sec["id"])}"><h2>{esc(sec["title"])}</h2>')
        if sec.get("note"):
            parts.append(f"<p>{sec['note']}</p>")
        listing.append(f"\n**{sec['title']}**\n")
        for shot in sec["shots"]:
            src = ROOT / shot["file"]
            if not src.exists():
                print(f"warning: missing {src}; skipped", file=sys.stderr)
                continue
            n += 1
            name = f"img/{n:02d}.jpg"
            jpeg(src, out / name)
            files[name] = str((out / name).relative_to(ROOT))
            parts.append(
                f'<article class="plate" data-n="{n}">'
                f'<div class="frame" tabindex="0" role="button" aria-label="Screenshot {n} at full size"><img src="{name}" width="1920" height="1080" loading="lazy" alt="{esc(shot["shows"])}"></div>'
                f'<div class="cap"><span class="num">{n}</span><p class="shows">{esc(shot["shows"])}</p>'
                f'<div class="ask"><span class="q">{esc(shot["question"])}</span>'
                f'<span class="yn"><button type="button" class="yes" id="yes-{n}" aria-pressed="false">Yes</button>'
                f'<button type="button" class="no" id="no-{n}" aria-pressed="false">No</button></span></div></div>'
                f"</article>")
            listing.append(f"{n}. {shot['shows']} **{shot['question']}**")
        parts.append("</section>")

    # Closing sections
    for extra in data.get("closing", []):
        parts.append(f'<section class="notes" id="{esc(extra["id"])}"><h2>{esc(extra["title"])}</h2>')
        kind = extra.get("kind", "text")
        if kind == "text":
            for para in extra.get("paras", []):
                parts.append(f"<p>{para}</p>")
            if extra.get("items"):
                parts.append("<ul>" + "".join(f"<li>{i}</li>" for i in extra["items"]) + "</ul>")
        elif kind == "two":
            parts.append('<div class="two">')
            for col in extra["columns"]:
                parts.append(f"<div><h3>{esc(col['title'])}</h3><ul>" + "".join(f"<li>{i}</li>" for i in col["items"]) + "</ul></div>")
            parts.append("</div>")
        elif kind == "table":
            parts.append('<div class="tablewrap"><table><thead><tr>' + "".join(f"<th>{esc(h)}</th>" for h in extra["head"]) + "</tr></thead><tbody>")
            for row in extra["rows"]:
                parts.append("<tr>" + "".join(f'<td class="n">{c}</td>' if i == 0 and extra.get("numbered") else f"<td>{c}</td>" for i, c in enumerate(row)) + "</tr>")
            parts.append("</tbody></table></div>")
        elif kind == "music":
            for para in extra.get("paras", []):
                parts.append(f"<p>{para}</p>")
            parts.append('<ul class="tunes">')
            for t in extra["tunes"]:
                src = ROOT / t["file"]
                if not src.exists():
                    print(f"warning: missing {src}; skipped", file=sys.stderr)
                    continue
                name = "audio/" + Path(t["file"]).stem + ".mp3"
                mp3(src, out / name)
                files[name] = str((out / name).relative_to(ROOT))
                parts.append(f'<li><div><strong>{esc(t["title"])}</strong><br><small>{esc(t["note"])}</small></div>'
                             f'<audio controls preload="none" src="{name}"></audio></li>')
            parts.append("</ul>")
        parts.append("</section>")

    parts.append("</div>")
    parts.append(f"<script>{JS}</script>")
    out.mkdir(parents=True, exist_ok=True)
    (out / "index.html").write_text("\n".join(parts), encoding="utf-8")
    (out / "files.json").write_text(json.dumps(files, indent=1), encoding="utf-8")
    (out / "list.md").write_text("\n".join(listing).strip() + "\n", encoding="utf-8")
    total = sum((ROOT / v).stat().st_size for v in files.values())
    print(f"review page: {n} screenshots, {len(files)} files, {total / 1e6:.1f} MB beside the page -> {out / 'index.html'}")
    return n


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", default=str(ROOT / "docs/review/review.json"))
    ap.add_argument("--out", default=str(ROOT / "build/review"))
    a = ap.parse_args()
    build(json.loads(Path(a.json).read_text(encoding="utf-8")), Path(a.out).resolve())

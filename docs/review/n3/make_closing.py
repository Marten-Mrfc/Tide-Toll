#!/usr/bin/env python3
"""The closing sections of the night-3 review page, read from the night's log and from honest.md.

From docs/incremental-log.md: the newest Windows build (the last row of the "Windows builds" table whose result starts
with "success"), "Decisions made for you" (table rows "| n | decision | why |"), "Needs you" (numbered lines "1. ...")
and "New sounds" (one sound per line that names an audio file in backticks, for example
"- **Coin chime** (`assets/audio/sfx/coin.ogg`): a short bright chime when coin arrives"; the bold text is the title,
the rest after the path is the note). From docs/review/n3/honest.md: the "- " lines under "## Better" and "## Not better".

build_review.py imports closing(); run this file alone to write docs/review/n3/closing.json and see what was read.
Usage: python3 docs/review/n3/make_closing.py
"""
import html
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
LOG = ROOT / "docs/incremental-log.md"
HONEST = HERE / "honest.md"
ACTIONS = "https://github.com/Marten-Mrfc/Tide-Toll/actions"


def section(text, title):
    m = re.search(r"^## " + re.escape(title) + r"\s*$(.*?)(?=^## |\Z)", text, re.S | re.M)
    return m.group(1) if m else ""


def md(s):
    """A line of the log's Markdown as safe HTML: bold, code and bare links."""
    s = html.escape(s.strip(), quote=False)
    s = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", s)
    s = re.sub(r"`(.+?)`", r"<code>\1</code>", s)
    s = re.sub(r"(?<![\"=>])(https://[^\s<)]+)", r'<a href="\1">\1</a>', s)
    return s


def cells(line):
    parts = [c.strip() for c in line.strip().strip("|").split("|")]
    return parts


def builds(log):
    rows = []
    for line in section(log, "Windows builds").splitlines():
        if not line.startswith("|") or "---" in line:
            continue
        c = cells(line)
        if len(c) < 4 or not c[2].startswith("https://"):
            continue
        rows.append({"round": c[0], "commit": c[1].strip("`"), "url": c[2], "result": " | ".join(c[3:])})
    return rows


def decisions(log):
    out = []
    for line in section(log, "Decisions made for you").splitlines():
        c = cells(line) if line.startswith("|") else []
        if len(c) >= 3 and c[0].isdigit():
            out.append((int(c[0]), md(c[1]), md(" | ".join(c[2:]))))
    return sorted(out)


def needs(log):
    return [md(re.sub(r"^\d+\.\s*", "", l)) for l in section(log, "Needs you").splitlines() if re.match(r"^\d+\.", l.strip())]


def sounds(log):
    out = []
    for line in section(log, "New sounds").splitlines():
        m = re.search(r"`([^`]+\.(?:ogg|wav|mp3|flac))`", line)
        if not m:
            continue
        path = m.group(1)
        t = re.search(r"\*\*(.+?)\*\*", line)
        title = t.group(1) if t else Path(path).stem.replace("-", " ").replace("_", " ")
        note = line[m.end():].strip(" ):-–|").strip()
        if not note:
            note = re.sub(r"\*\*.+?\*\*|`[^`]+`|^\s*[-*\d.]+\s*|[():]", " ", line).strip()
        out.append({"file": path, "title": title, "note": note})
    return out


def honest():
    better, worse, cur = [], [], None
    if HONEST.exists():
        for line in HONEST.read_text(encoding="utf-8").splitlines():
            h = line.strip().lower()
            if h.startswith("## "):
                cur = worse if "not" in h else better if "better" in h else None
            elif cur is not None and line.startswith("- "):
                cur.append(md(line[2:]))
    return better, worse


def closing():
    log = LOG.read_text(encoding="utf-8")
    b = builds(log)
    ok = [r for r in b if r["result"].lower().startswith("success")]
    newest = ok[-1] if ok else {"round": "?", "commit": "?", "url": ACTIONS, "result": ""}
    better, worse = honest()
    out = []
    out.append({
        "id": "build", "title": "The newest Windows build", "kind": "text",
        "paras": [
            f'<a href="{html.escape(newest["url"])}"><strong>Build {html.escape(newest["round"])}, commit {html.escape(newest["commit"])}</strong></a>. '
            'Open the run page, scroll to the bottom, download the artifact <code>TideToll-windows-x64</code>, unzip all of it '
            'and start <code>TideToll.exe</code>. You need to be signed in to GitHub. The exe is not signed, so Windows SmartScreen '
            'will warn once: choose "More info", then "Run anyway".',
            ("In this build: " + md(re.sub(r"^success,?\s*", "", newest["result"]))) if newest["result"] else "",
            "Nobody has run this exe on Windows tonight. Everything ran on Linux in a cloud machine without a graphics card.",
        ],
        "items": [f'{md(r["round"])}, commit <code>{html.escape(r["commit"])}</code>: <a href="{html.escape(r["url"])}">run page</a>' for r in b],
        "items_title": "Every build of the night, oldest first",
    })
    out.append({"id": "honest", "title": "What got better and what did not, in my honest view", "kind": "two",
                "columns": [{"title": "Better", "items": better}, {"title": "Not better", "items": worse}]})
    d = decisions(log)
    out.append({"id": "decisions", "title": "Decisions made for you", "kind": "table", "numbered": True,
                "head": ["#", "Decision", "Why"], "rows": [[str(n), a, w] for n, a, w in d]})
    nd = needs(log)
    out.append({"id": "needs", "title": "Needs you", "kind": "text",
                "items": nd, "paras": [] if nd else ["Nothing at the moment."]})
    s = sounds(log)
    if s:
        out.append({"id": "sounds", "title": "New sounds to listen to", "kind": "music",
                    "paras": ["Made in code tonight and checked only by measurement: nobody has heard them."], "tunes": s})
    return {"newest": newest, "closing": out,
            "counts": {"builds": len(b), "decisions": len(d), "needs": len(nd), "sounds": len(s),
                       "better": len(better), "not_better": len(worse)}}


if __name__ == "__main__":
    c = closing()
    (HERE / "closing.json").write_text(json.dumps(c, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    n = c["newest"]
    print(f"closing.json: newest build {n['round']} {n['commit']}; " + ", ".join(f"{k} {v}" for k, v in c["counts"].items()))

#!/usr/bin/env python3
"""Merge the per-group ledger fragments (assets/src/ledger-rows/<group>.csv, ledger columns, no header) into
assets/LEDGER.csv. A fragment row replaces the LEDGER.csv row with the same `item`; new items are appended in
fragment order. Afterwards the fragments are emptied (they stay as files so builders keep writing to them).

    python3 tools/merge-ledger-rows.py --dry-run   show what would change
    python3 tools/merge-ledger-rows.py             merge, empty the fragments, then run tools/check-ledger.py
    python3 tools/merge-ledger-rows.py --keep      merge but leave the fragments as they are

This is the lead's step after the builders finish; builders never edit assets/LEDGER.csv while working in parallel.
"""
import csv
import io
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LEDGER = ROOT / "assets" / "LEDGER.csv"
FRAGS = ROOT / "assets" / "src" / "ledger-rows"
HEADER = ["item", "kind", "source", "licence", "url", "used_in", "date_checked", "verified"]


def read(path):
    with path.open(newline="", encoding="utf-8") as f:
        return [r for r in csv.reader(f) if any(c.strip() for c in r)]


def main() -> int:
    dry = "--dry-run" in sys.argv
    keep = "--keep" in sys.argv
    rows = read(LEDGER)
    if rows[0] != HEADER:
        print(f"unexpected LEDGER.csv header {rows[0]}")
        return 1
    header, body = rows[0], rows[1:]
    pos = {r[0]: i for i, r in enumerate(body)}
    added = replaced = 0
    frags = sorted(FRAGS.glob("*.csv"))
    for frag in frags:
        for r in read(frag):
            if r == HEADER:
                continue
            if len(r) != len(HEADER):
                print(f"{frag.name}: bad row (needs {len(HEADER)} fields): {r}")
                return 1
            if r[0] in pos:
                if body[pos[r[0]]] != r:
                    replaced += 1
                    print(f"replace {r[0]}  (from {frag.name})")
                body[pos[r[0]]] = r
            else:
                pos[r[0]] = len(body)
                body.append(r)
                added += 1
                print(f"add     {r[0]}  (from {frag.name})")
    print(f"{added} added, {replaced} replaced, from {len(frags)} fragment files")
    if dry:
        return 0
    buf = io.StringIO()
    csv.writer(buf, lineterminator="\n").writerows([header] + body)
    LEDGER.write_text(buf.getvalue(), encoding="utf-8")
    if not keep:
        for frag in frags:
            frag.write_text("", encoding="utf-8")
    return subprocess.call([sys.executable, str(ROOT / "tools" / "check-ledger.py")])


if __name__ == "__main__":
    sys.exit(main())

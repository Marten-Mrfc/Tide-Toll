#!/usr/bin/env python3
"""Ledger check (rules in docs/asset-pipeline.md): every shipped file under assets/ needs a row in assets/LEDGER.csv with an
allowed licence. Fails on a missing row, an empty licence, or a non-commercial / banned licence.

A file row's `item` is its path from the repository root (assets/models/cottage.glb).
Rows for tools and packages (kind npm-dev-dependency, nuget, tool, ...) are allowed too.
Exempt: assets/LEDGER.csv itself, generator sources under assets/src/, saved licence texts
under assets/licences/, and README.md files.
"""
import csv
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "assets"
LEDGER = ASSETS / "LEDGER.csv"
HEADER = ["item", "kind", "source", "licence", "url", "used_in", "date_checked", "verified"]
# Anything non-commercial, no-derivatives, or of unknown origin is out (Art tab, licence rules).
BANNED = re.compile(r"\bNC\b|non-?commercial|\bND\b|no-?deriv|sampling\+|remarc|unknown|unclear|personal use|editorial", re.I)
ALLOWED = re.compile(r"^(CC0(-1\.0)?|CC-BY-(3\.0|4\.0)|public domain|MIT|Apache-2\.0|BSD-[23]-Clause|OFL-1\.1|Zlib|self-made|Sonniss GDC)$", re.I)


def exempt(rel: str) -> bool:
    return (rel == "assets/LEDGER.csv" or rel.startswith("assets/src/") or rel.startswith("assets/licences/")
            or rel.endswith("/README.md") or rel.endswith(".import"))


def main() -> int:
    errors = []
    if not LEDGER.exists():
        print("LEDGER FAIL: assets/LEDGER.csv is missing")
        return 1
    with LEDGER.open(newline="", encoding="utf-8") as f:
        reader = csv.reader(f)
        header = next(reader, [])
        if header != HEADER:
            errors.append(f"header is {header}, expected {HEADER}")
        rows = [r for r in reader if any(c.strip() for c in r)]
    items = {}
    for n, r in enumerate(rows, start=2):
        if len(r) != len(HEADER):
            errors.append(f"line {n}: {len(r)} fields, expected {len(HEADER)}")
            continue
        row = dict(zip(HEADER, r))
        lic = row["licence"].strip()
        if not lic:
            errors.append(f"line {n} ({row['item']}): empty licence")
        elif BANNED.search(lic):
            errors.append(f"line {n} ({row['item']}): licence '{lic}' is not allowed (non-commercial or unclear)")
        elif not ALLOWED.match(lic):
            errors.append(f"line {n} ({row['item']}): licence '{lic}' is not on the allow list in docs/asset-pipeline.md")
        if not row["source"].strip():
            errors.append(f"line {n} ({row['item']}): empty source")
        # Made-in-code assets must name a generator script that is actually in the repository.
        if lic.lower().startswith("self-made"):
            scripts = re.findall(r"assets/src/[\w./-]+", row["source"])
            if not scripts:
                errors.append(f"line {n} ({row['item']}): self-made but source names no generator under assets/src/")
            for sp in scripts:
                if not (ROOT / sp.rstrip(".")).exists():
                    errors.append(f"line {n} ({row['item']}): generator {sp} does not exist")
        items[row["item"].strip()] = n
    files = sorted(p.relative_to(ROOT).as_posix() for p in ASSETS.rglob("*") if p.is_file())
    checked = 0
    for rel in files:
        if exempt(rel):
            continue
        checked += 1
        if rel not in items:
            errors.append(f"{rel}: no ledger row")
    for e in errors:
        print("LEDGER FAIL:", e)
    print(f"ledger: {len(rows)} rows, {checked} asset files checked, {len(errors)} problems")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())

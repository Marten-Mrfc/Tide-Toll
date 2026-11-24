#!/usr/bin/env python3
"""Ledger check (rules in docs/asset-pipeline.md): every shipped file under assets/ needs a row in assets/LEDGER.csv with an
allowed licence. Fails on a missing row, an empty licence, or a non-commercial / banned licence.

A file row's `item` is its path from the repository root (assets/models/cottage.glb), and that file must exist.
Rows for tools and packages (kind npm-dev-dependency, nuget, tool) name a package instead and are not looked up
on disk. Any other kind is an error. A self-made row's source must name its generator under assets/src/, and
every generator it names must be an existing, non-empty file.
Exempt: assets/LEDGER.csv itself, generator sources under assets/src/, saved licence texts
under assets/licences/, and README.md files.

  python3 tools/check-ledger.py               checks assets/LEDGER.csv (what ./check runs)
  python3 tools/check-ledger.py --fragments   also counts the not-yet-merged rows in assets/src/ledger-rows/*.csv
                                              (same columns, no header), as tools/build-assets.sh does
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
# Kinds whose `item` is a file under assets/ (it must exist), and kinds for software (item is a package name).
FILE_KINDS = {"model", "texture", "icon", "font", "sound", "music", "ambience"}
PACKAGE_KINDS = {"npm-dev-dependency", "npm-dependency", "nuget", "tool"}
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
    where = [f"line {n}" for n in range(2, len(rows) + 2)]
    if "--fragments" in sys.argv[1:]:
        for frag in sorted((ASSETS / "src" / "ledger-rows").glob("*.csv")):
            with frag.open(newline="", encoding="utf-8") as f:
                frows = [r for r in csv.reader(f) if any(c.strip() for c in r)]
            if frows and frows[0] == HEADER:
                errors.append(f"{frag.relative_to(ROOT)}: fragments have no header row")
                frows = frows[1:]
            rows += frows
            where += [f"{frag.relative_to(ROOT).as_posix()}:{n}" for n in range(1, len(frows) + 1)]
    items = {}
    for n, r in zip(where, rows):
        if len(r) != len(HEADER):
            errors.append(f"{n}: {len(r)} fields, expected {len(HEADER)}")
            continue
        row = dict(zip(HEADER, r))
        lic = row["licence"].strip()
        if not lic:
            errors.append(f"{n} ({row['item']}): empty licence")
        elif BANNED.search(lic):
            errors.append(f"{n} ({row['item']}): licence '{lic}' is not allowed (non-commercial or unclear)")
        elif not ALLOWED.match(lic):
            errors.append(f"{n} ({row['item']}): licence '{lic}' is not on the allow list in docs/asset-pipeline.md")
        if not row["source"].strip():
            errors.append(f"{n} ({row['item']}): empty source")
        # Made-in-code assets must name a generator script that is actually in the repository.
        if lic.lower().startswith("self-made"):
            scripts = re.findall(r"assets/src/[\w./-]+", row["source"])
            if not scripts:
                errors.append(f"{n} ({row['item']}): self-made but source names no generator under assets/src/")
            for sp in scripts:
                gp = ROOT / sp.rstrip(".")
                if not gp.exists():
                    errors.append(f"{n} ({row['item']}): generator {sp} does not exist")
                elif not gp.is_file():
                    errors.append(f"{n} ({row['item']}): generator {sp} is not a file")
                elif gp.stat().st_size == 0 or not gp.read_bytes().strip():
                    errors.append(f"{n} ({row['item']}): generator {sp} is empty")
        kind = row["kind"].strip()
        item = row["item"].strip()
        if kind in FILE_KINDS:
            if not item.startswith("assets/"):
                errors.append(f"{n} ({item}): kind {kind} needs the file's path from the repository root (assets/...)")
            elif not (ROOT / item).is_file():
                errors.append(f"{n} ({item}): file does not exist")
        elif kind not in PACKAGE_KINDS:
            errors.append(f"{n} ({item}): unknown kind '{kind}' (files: {', '.join(sorted(FILE_KINDS))}; "
                          f"software: {', '.join(sorted(PACKAGE_KINDS))})")
        prev = items.get(row["item"].strip())
        # A fragment row may restate a LEDGER.csv row (it replaces it on merge); other repeats are errors.
        if prev is not None and prev.startswith("line ") == n.startswith("line "):
            errors.append(f"{n} ({row['item']}): duplicate row (first at {items[row['item'].strip()]})")
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

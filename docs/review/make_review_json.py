#!/usr/bin/env python3
"""Joins the tour's result (build/tour/shots.json) with the lead's closing notes (docs/review/closing.json) into
docs/review/review.json, the input of build_review.py. Shots the tour could not make are left out. A shot whose
'wrong' note is not empty gets that note appended to what it shows, so the owner sees the known fault.

Usage: python3 docs/review/make_review_json.py [--tour build/tour] [--closing docs/review/closing.json]
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

ap = argparse.ArgumentParser()
ap.add_argument("--tour", default="build/tour")
ap.add_argument("--closing", default="docs/review/closing.json")
ap.add_argument("--out", default="docs/review/review.json")
a = ap.parse_args()

tour = json.loads((ROOT / a.tour / "shots.json").read_text(encoding="utf-8"))
closing = json.loads((ROOT / a.closing).read_text(encoding="utf-8"))
plan = json.loads((ROOT / "docs/review/shots-plan.json").read_text(encoding="utf-8"))
questions = {s["id"]: s["question"] for sec in plan["sections"] for s in sec["shots"]}

sections, made, skipped = [], 0, []
for sec in tour["sections"]:
    shots = []
    for s in sec["shots"]:
        f = ROOT / a.tour / (s.get("file") or (s["id"] + ".png"))
        if not s.get("made", True) or not f.exists():
            skipped.append(s["id"] + ": " + (s.get("why") or "not made"))
            continue
        shows = s["shows"].strip()
        wrong = (s.get("wrong") or "").strip()
        if wrong:
            shows += " Known fault: " + wrong
        shots.append({"file": str(f.relative_to(ROOT)), "shows": shows, "question": questions.get(s["id"], s.get("question", ""))})
        made += 1
    if shots:
        sections.append({"id": sec["id"], "title": sec["title"], "shots": shots})

for extra in closing.get("extra_sections", []):
    shots = [s for s in extra["shots"] if (ROOT / s["file"]).exists()]
    made += len(shots)
    if shots:
        sections.append({"id": extra["id"], "title": extra["title"], "note": extra.get("note", ""), "shots": shots})

review = {k: closing[k] for k in ("title", "eyebrow", "heading", "intro", "facts") if k in closing}
review["sections"] = sections
review["closing"] = closing.get("closing", [])
(ROOT / a.out).write_text(json.dumps(review, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
print(f"review.json: {made} screenshots in {len(sections)} sections; left out: {len(skipped)}")
for s in skipped:
    print("  left out", s)

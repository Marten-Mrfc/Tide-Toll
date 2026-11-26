#!/usr/bin/env python3
"""Merges the reviewers' notes (build/tour/notes-*.json) into build/tour/shots.json and writes
build/tour/still-wrong.json. Each notes file is {"shots": {"<id>": {"shows": "...", "wrong": "..."}},
"still_wrong": ["most serious first", ...]}. A shot without a note keeps the tour's own text but loses its
old 'wrong' note, which described an earlier build."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
tour = ROOT / "build/tour"
shots = json.loads((tour / "shots.json").read_text(encoding="utf-8"))
notes, wrong = {}, []
for f in sorted(tour.glob("notes-*.json")):
    d = json.loads(f.read_text(encoding="utf-8"))
    notes.update(d.get("shots", {}))
    wrong += d.get("still_wrong", [])
n = 0
for sec in shots["sections"]:
    for s in sec["shots"]:
        if s["id"] in notes:
            s["shows"] = notes[s["id"]].get("shows", s["shows"])
            s["wrong"] = notes[s["id"]].get("wrong", "")
            n += 1
        else:
            s["wrong"] = ""
(tour / "shots.json").write_text(json.dumps(shots, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
(tour / "still-wrong.json").write_text(json.dumps(wrong, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
print(f"notes merged for {n} shots; {len(wrong)} still-wrong lines")

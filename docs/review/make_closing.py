#!/usr/bin/env python3
"""Writes docs/review/closing.json: the opening lines and the closing sections of the owner's review page.

The decisions, the things that need the owner and the newest Windows build are read from docs/incremental-log.md,
so the page and the log cannot disagree. The honest view and the music list are written here by the lead.
The list of what still looks wrong is read from build/tour/still-wrong.json if it exists (a list of strings
written by the agent who looked at the final pictures).

Usage: python3 docs/review/make_closing.py --shots N
"""
import argparse
import html
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ap = argparse.ArgumentParser()
ap.add_argument("--shots", type=int, default=0)
a = ap.parse_args()

log = (ROOT / "docs/incremental-log.md").read_text(encoding="utf-8")


def section(title):
    m = re.search(r"^## " + re.escape(title) + r"\s*$(.*?)(?=^## |\Z)", log, re.S | re.M)
    return m.group(1) if m else ""


def md(s):
    s = html.escape(s.strip(), quote=False)
    s = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", s)
    s = re.sub(r"`(.+?)`", r"<code>\1</code>", s)
    return s


decisions = []
for line in section("Decisions made for you").splitlines():
    m = re.match(r"^\|\s*(\d+)\s*\|(.*)\|(.*)\|\s*$", line)
    if m:
        decisions.append((int(m.group(1)), md(m.group(2)), md(m.group(3))))
decisions.sort()

needs = [md(re.sub(r"^\d+\.\s*", "", l)) for l in section("Needs you").splitlines() if re.match(r"^\d+\.", l)]

builds = []
for line in section("Windows builds").splitlines():
    m = re.match(r"^\|\s*(\S+)\s*\|\s*`(\w+)`\s*\|\s*(https://\S+)\s*\|(.*)\|\s*$", line)
    if m:
        builds.append((m.group(1), m.group(2), m.group(3), m.group(4).strip()))
newest = builds[-1] if builds else ("?", "?", "https://github.com/Marten-Mrfc/Tide-Toll/actions", "")

wrong_file = ROOT / "build/tour/still-wrong.json"
wrong = [md(w) for w in json.loads(wrong_file.read_text(encoding="utf-8"))] if wrong_file.exists() else []

closing = {
    "title": "Tide & Toll Night Review",
    "eyebrow": "Tide & Toll, the overnight rebuild, 26 November 2026",
    "heading": "A town that grows one path at a time",
    "intro": [
        "This is the incremental rebuild you asked for, in play order: one town (the default one, Dunstead) from its first minute to the borough seal at the end of the second age. Every picture was taken from the final build by one command, on a cloud machine with a software renderer, at 1920 x 1080.",
        "Under each picture is one line saying what it shows and one question. Press Yes or No, then <strong>Copy my answers</strong>, paste them into the chat and say continue. Where a picture shows a fault I already know of, its line says so.",
    ],
    "facts": [
        f"{a.shots} pictures" if a.shots else "pictures in play order",
        f"final commit {newest[1]}",
        "first age about 33 minutes (played by real input)",
        "second age 22 to 34 minutes (bots only)",
        "everything made in code, 0 euro",
    ],
    "closing": [
        {
            "id": "build", "title": "The newest Windows build", "kind": "text",
            "paras": [
                f'<a href="{newest[2]}">The build of round {html.escape(newest[0])}, commit {newest[1]}</a>: open the run page, scroll to the bottom, download the artifact <code>TideToll-windows-x64</code>, unzip all of it and start <code>TideToll.exe</code>. You need to be signed in to GitHub. The exe is not signed, so Windows SmartScreen will warn once: choose "More info", then "Run anyway".',
                "Nobody has run this exe on Windows. Everything tonight ran on Linux in a cloud machine without a graphics card.",
            ],
            "items": [f'Round {html.escape(b[0])}, commit <code>{b[1]}</code>: <a href="{b[2]}">run page</a>' for b in builds],
        },
        {
            "id": "honest", "title": "What is fun and what is not, in my honest view", "kind": "two",
            "columns": [
                {"title": "Fun", "items": [
                    "The first twenty minutes of the first age. Something new arrives every minute or two, several kinds of thing are within reach at once, and a whole age played by real input counted 3 to 4 decisions a minute.",
                    "Decisions made on the map itself: paving a road and extending a cottage by clicking it.",
                    "The choice of two boons, four times an age. It is the one place where two players end with different towns.",
                    "Requests and the charter drive: a goal a minute or two away, and a last push for the charter instead of a wait.",
                    "Zooming out until the town is a page in a book, and closing the book to pause.",
                    "Watching the river: boats mooring and loading, the queue at the ferry, the drawbridge lifting.",
                ]},
                {"title": "Not fun yet", "items": [
                    "Minutes 20 to 30 of the first age: under 2 decisions a minute.",
                    "Apart from the four boons, every player ends up buying the same things. Only the order differs.",
                    "Most upgrade steps are 3 to 4 percent and are felt only in the coin figure. The cheap ladder called Regulars may read as filler.",
                    "The second age has not been played through with real input by anyone. Its numbers come from bots, and its margins are thin: on ten further towns, 7 of 40 runs missed a pacing target a little.",
                    "In the second age the landing earns three quarters or more of all coin, so the first age's market matters little.",
                    "The ford matters on only about half of the towns.",
                    "Nothing has been heard. The music and the sounds were checked by measurement only.",
                ]},
            ],
        },
    ],
}
if wrong:
    closing["closing"].append({"id": "wrong", "title": "What still looks wrong", "kind": "text",
                               "paras": ["Found by looking at the final pictures. Most serious first."], "items": wrong})
closing["closing"] += [
    {"id": "decisions", "title": "Decisions made for you", "kind": "table", "numbered": True,
     "head": ["#", "Decision", "Why"], "rows": [[str(n), d, w] for n, d, w in decisions]},
    {"id": "needs", "title": "Needs you", "kind": "text", "items": needs},
    {
        "id": "music", "title": "Music to listen to", "kind": "music",
        "paras": ["You called the first theme perfect. The other five were made in code with the same instruments, mix and loudness, and nobody has heard them. In the game the first four rotate, Still Pages plays while the book is closed, and Rising plays once when the next age is rung in."],
        "tunes": [
            {"file": "assets/audio/music/theme-hamlet-draft.ogg", "title": "Tide & Toll theme", "note": "the one you know: D Dorian, 64 beats a minute, 45 seconds, loops"},
            {"file": "assets/audio/music/music-ferry-morning.ogg", "title": "Ferry Morning", "note": "new: D Mixolydian, 68 beats a minute, 74 seconds, loops"},
            {"file": "assets/audio/music/music-long-field.ogg", "title": "The Long Field", "note": "new: A Aeolian, 64 beats a minute, 79 seconds, loops"},
            {"file": "assets/audio/music/music-low-meadow.ogg", "title": "Low Meadow", "note": "new: D Aeolian, 60 beats a minute, 72 seconds, loops"},
            {"file": "assets/audio/music/music-theme-rising.ogg", "title": "Tide and Toll, Rising", "note": "new, for the age change: the theme made grander, 49 seconds, with an ending"},
            {"file": "assets/audio/music/music-still-pages.ogg", "title": "Still Pages", "note": "new, for the paused book: quieter and sparser, 60 seconds, loops"},
        ],
    },
]
earlier = ROOT / "build/tour-c7973d5"
closing["extra_sections"] = [{
    "id": "earlier", "title": "Two pictures from the build one step earlier",
    "note": "The tour could not retake these two on the final build: its second click on the cottage missed, and the ford was not in use at the moment of the picture. They come from the tree just before the last small pass, which changed only the crowded-road tint, the charter stone and the tour's timing.",
    "shots": [
        {"file": "build/tour-c7973d5/b05-extended.png",
         "shows": "A cottage after its extensions, beside a field. Known fault: the extended cottage looks thin, and one and two extensions are hard to tell apart.",
         "question": "Can you tell an extended cottage from a plain one?"},
        {"file": "build/tour-c7973d5/f04-ford.png",
         "shows": "The ford at low water: stones show in the river, villagers queue to cross, and the tide dial reads low water. Known fault: a boat's tag is cut off by a tree, and crowded roads still have the strong red tint that the final build softened.",
         "question": "Does the tide read as something you can see and use?"},
    ],
}]
(ROOT / "docs/review/closing.json").write_text(json.dumps(closing, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
print(f"closing.json: {len(decisions)} decisions, {len(needs)} needs, {len(builds)} builds, {len(wrong)} still-wrong notes; newest build round {newest[0]} {newest[1]}")

# python3 assets/src/audio/playlist-music.py  ->  assets/audio/music/playlist.json
# The game's music list: id, file, score, title, role ("age1" first-age rotation, "age-change", "pause"), length in
# seconds (read from each score JSON, so it runs after the music-*.py generators; build-assets.sh runs files in name
# order), whether it loops, key and tempo, and the suggested first-age rotation with the existing theme first.
import json
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
from common import ledger, asset_path

M = "assets/audio/music/"
PIECES = [  # (id, title, role, loops, key, bpm, score file)
    ("theme-hamlet-draft", "Tide and Toll", "age1", True, "D Dorian", 64, "theme-hamlet-draft-score.json"),
    ("music-ferry-morning", "Ferry Morning", "age1", True, "D Mixolydian", 68, None),
    ("music-long-field", "The Long Field", "age1", True, "A Aeolian", 64, None),
    ("music-low-meadow", "Low Meadow", "age1", True, "D Aeolian", 60, None),
    ("music-theme-rising", "Tide and Toll, Rising", "age-change", False, "D Dorian", 64, None),
    ("music-still-pages", "Still Pages", "pause", True, "D Dorian", 60, None),
]
out = []
for pid, title, role, loops, key, bpm, score in PIECES:
    score = score or f"{pid}-score.json"
    sc = json.loads(asset_path(f"audio/music/{score}").read_text(encoding="utf-8"))
    secs = sc.get("seconds", sc.get("loop_seconds"))
    out.append({"id": pid, "file": M + pid + ".ogg", "score": M + score, "title": title, "role": role,
                "seconds": round(secs, 3), "loops": loops, "key": key, "bpm": bpm})
playlist = {
    "about": "Background music. role age1 = first-age rotation, age-change = played once when the age changes, "
             "pause = while the book is open. All 48 kHz stereo Ogg Vorbis; loops are sample-exact (loop the whole "
             "file, no crossfade). Integrated loudness -20 LUFS, except the pause piece at -22 on purpose.",
    "rotation": [p[0] for p in PIECES if p[2] == "age1"],
    "rotation_note": "Play in this order and wrap round, the theme first. Suggested: each piece plays through once "
                     "(the theme twice, it is 45 s), 0 to 2 s of silence between pieces. Tempo never follows game speed.",
    "pieces": out,
}
js = asset_path("audio/music/playlist.json")
js.write_text(json.dumps(playlist, indent=1) + "\n", encoding="utf-8")
print("  playlist:", ", ".join(f"{p['id']} {p['seconds']} s" for p in out))
ledger.add("audio", js, kind="music", generator=__file__, verified=True,
           used_in="Music: playlist for the game (first-age rotation order, age-change and pause pieces)")

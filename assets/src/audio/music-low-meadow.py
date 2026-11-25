# python3 assets/src/audio/music-low-meadow.py  ->  assets/audio/music/music-low-meadow.ogg (48 kHz stereo Vorbis loop)
#                                                  assets/audio/music/music-low-meadow-score.json
# First-age rotation piece. Same pipe, lyre, frame drum, drone and room as the Hamlet theme (engine: _music.py).
# D Aeolian (the theme's D with B flat: darker, evening), 60 BPM in 3/4, 24 bars = 72 s loop, the theme's drone.
# Form: A (1-8, opens on F falling to A) B (9-16, a leap of a fifth, up to B flat) A' (17-24) home on D held into the downbeat.
# The drum only answers in B (bars 9-16); A' is pipe, lyre and drone again, as the theme's first half.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from _music import render

PIECE = {
    "id": "music-low-meadow", "title": "Low Meadow", "role": "age1", "seed": 813, "generator": __file__,
    "final": "D", "mode": "aeolian", "bpm": 60, "loop": True,
    "form": "A (1-8) B (9-16) A' (17-24); 72 s seamless loop",
    "melody": [
        "F5:2 E5:1", "D5:1 C5:1 D5:1", "A4:3", "A#4:1 C5:1 D5:1",                      # A
        "E5:1 F5:2", "G5:1 F5:1 E5:1", "D5:2 C5:1", "D5:3",
        "D5:1 A5:2", "G5:1 F5:1 G5:1", "A#5:1 A5:1 G5:1", "A5:3",                       # B
        "F5:1 E5:1 D5:1", "C5:2 D5:1", "E5:1 C5:1 A4:1", "D5:3",
        "F5:2 E5:1", "D5:1 C5:1 D5:1", "A#4:2 A4:1", "G4:1 A4:1 C5:1",                  # A'
        "D5:2 E5:1", "F5:1 E5:1 C5:1", "E5:2 C5:1", "D5:3",
    ],
    "drum": [(b, 1, "dum") for b in range(9, 17)] + [(b, 3, "tek") for b in range(9, 16)] + [(16, 2.5, "tek")],
    "lyre": [(1, 1, ["D3", "A3"]), (4, 1, ["A#2", "F3"]), (5, 1, ["D3", "A3"]), (9, 1, ["F3", "C4"]),
             (12, 1, ["G2", "D3"]), (13, 1, ["D3", "A3"]), (17, 1, ["D3", "A3"]), (19, 1, ["A#2", "F3"]),
             (21, 1, ["C3", "G3"]), (23, 1, ["A3"])],
    "drone": [("D2", 0.45), ("D3", 1.0), ("A3", 0.6)],
    "instruments": {"pipe": "lead, wooden pipe", "drone": "river drone D2 + D3 + A3",
                    "lyre": "open fifths, moving D Bb D F G D D Bb C", "frame_drum": "answers bars 9-16 only"},
    "used_in": "Music: Low Meadow, first-age rotation (D Aeolian, 3/4 at 60 BPM, 24 bars, 72 s seamless loop)",
}

if __name__ == "__main__":
    render(PIECE)

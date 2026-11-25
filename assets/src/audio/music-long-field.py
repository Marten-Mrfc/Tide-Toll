# python3 assets/src/audio/music-long-field.py  ->  assets/audio/music/music-long-field.ogg (48 kHz stereo Vorbis loop)
#                                                  assets/audio/music/music-long-field-score.json
# First-age rotation piece. Same pipe, lyre, frame drum, drone and room as the Hamlet theme (engine: _music.py).
# A Aeolian (the theme's own notes, A as final: a fifth above the theme's D), 64 BPM in 3/4 (the theme's tempo),
# 28 bars = 78.75 s loop. The pipe sits lower (G4-A5) and moves in long-short waltz steps.
# Form: A (1-8) A' (9-16) B (17-24, up to A5) coda (25-28) home on A held into the downbeat. Drum from bar 9.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from _music import render

PIECE = {
    "id": "music-long-field", "title": "The Long Field", "role": "age1", "seed": 802, "generator": __file__,
    "final": "A", "mode": "aeolian", "bpm": 64, "loop": True,
    "form": "A (1-8) A' (9-16) B (17-24) coda (25-28); 78.75 s seamless loop",
    "melody": [
        "E5:2 D5:1", "C5:1 B4:1 A4:1", "B4:2 C5:1", "D5:3",                             # A
        "E5:1 G5:1 E5:1", "D5:2 C5:1", "B4:1 G4:1 B4:1", "A4:3",
        "E5:2 D5:1", "C5:1 E5:1 G5:1", "F5:2 E5:1", "D5:3",                             # A'
        "C5:1 B4:1 C5:1", "D5:1 E5:1 G4:1", "B4:2 G4:1", "A4:3",
        "A5:2 G5:1", "E5:1 F5:1 G5:1", "A5:3", "G5:1 E5:1 D5:1",                        # B
        "E5:2 C5:1", "D5:1 C5:1 B4:1", "C5:2 B4:1", "E5:3",
        "E5:2 D5:1", "C5:1 B4:1 A4:1", "B4:1 G4:1 B4:1", "A4:3",                        # coda
    ],
    "drum": [(b, 1, "dum") for b in range(9, 29)] + [(b, 3, "tek") for b in range(9, 28)],
    "lyre": [(1, 1, ["A2", "E3"]), (5, 1, ["C3", "G3"]), (9, 1, ["A2", "E3"]), (13, 1, ["G2", "D3"]),
             (17, 1, ["F2", "C3"]), (21, 1, ["C3", "G3"]), (24, 1, ["E3", "B3"]), (25, 1, ["A2", "E3"]), (27, 1, ["E3"])],
    "drone": [("A1", 0.35), ("A2", 1.0), ("E3", 0.6)],
    "instruments": {"pipe": "lead, wooden pipe, lower register", "drone": "river drone A1 + A2 + E3",
                    "lyre": "open fifths, moving A C A G F C E A", "frame_drum": "from bar 9"},
    "used_in": "Music: The Long Field, first-age rotation (A Aeolian, 3/4 at 64 BPM, 28 bars, 78.75 s seamless loop)",
}

if __name__ == "__main__":
    render(PIECE)

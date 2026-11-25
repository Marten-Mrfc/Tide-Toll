# python3 assets/src/audio/music-ferry-morning.py  ->  assets/audio/music/music-ferry-morning.ogg (48 kHz stereo Vorbis loop)
#                                                     assets/audio/music/music-ferry-morning-score.json
# First-age rotation piece. Same pipe, lyre, frame drum, drone and room as the Hamlet theme (engine: _music.py).
# D Mixolydian (the theme's D with F# and C natural: brighter, morning), 68 BPM in 3/4, 28 bars = 74.1 s loop.
# Form: A (bars 1-8, rising A-D-F# opening) B (9-16, climbs to C6) A' (17-24, varied) coda (25-28, home on D,
# held into the downbeat). The drum enters with B. Lyre fifths move D, D, G, C, D, D, D (harmony by the lyre only).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from _music import render

PIECE = {
    "id": "music-ferry-morning", "title": "Ferry Morning", "role": "age1", "seed": 801, "generator": __file__,
    "final": "D", "mode": "mixolydian", "bpm": 68, "loop": True,
    "form": "A (1-8) B (9-16) A' (17-24) coda (25-28); 74.1 s seamless loop",
    "melody": [
        "A4:1 D5:1 F#5:1", "E5:2 D5:1", "C5:1 D5:1 E5:1", "A4:3",                      # A
        "A4:1 D5:1 F#5:1", "A5:2 G5:1", "F#5:1 E5:1 C5:1", "D5:3",
        "F#5:1 G5:1 A5:1", "B5:2 G5:1", "A5:1 F#5:2", "E5:3",                             # B
        "A5:1 C6:1 B5:1", "A5:2 G5:1", "F#5:1 G5:1 E5:1", "E5:2 -:1",
        "A4:1 D5:1 F#5:1", "E5:1 F#5:1 D5:1", "C5:2 E5:1", "A4:3",                      # A'
        "A4:1 D5:1 F#5:1", "A5:1 B5:1 G5:1", "F#5:1 E5:1 C5:1", "D5:3",
        "-:1 F#5:1 E5:1", "D5:1 C5:1 A4:1", "C5:1 E5:1 E5:1", "D5:3",                   # coda
    ],
    "drum": [(b, 1, "dum") for b in range(9, 29)] + [(b, 3, "tek") for b in range(9, 28)],
    "lyre": [(1, 1, ["D3", "A3"]), (5, 1, ["D3", "A3"]), (9, 1, ["G2", "D3"]), (13, 1, ["C3", "G3"]),
             (17, 1, ["D3", "A3"]), (21, 1, ["A3"]), (25, 1, ["D3", "A3"]), (27, 1, ["A3"])],
    "drone": [("D2", 0.45), ("D3", 1.0), ("A3", 0.6)],
    "instruments": {"pipe": "lead, wooden pipe", "drone": "river drone D2 + D3 + A3", "lyre": "open fifths at phrase starts",
                    "frame_drum": "from bar 9 (dum on beat 1, soft tek on 3)"},
    "used_in": "Music: Ferry Morning, first-age rotation (D Mixolydian, 3/4 at 68 BPM, 28 bars, 74.1 s seamless loop)",
}

if __name__ == "__main__":
    render(PIECE)

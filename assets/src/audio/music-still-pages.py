# python3 assets/src/audio/music-still-pages.py  ->  assets/audio/music/music-still-pages.ogg (48 kHz stereo Vorbis loop)
#                                                   assets/audio/music/music-still-pages-score.json
# For the paused book: quieter and sparser than the rotation, same pipe, lyre, drone and room as the Hamlet theme
# (engine: _music.py). D Dorian, 60 BPM in 3/4, 20 bars = 60 s loop. Long pipe notes in the low register (G4-E5)
# with whole bars of rest between phrases, single lyre strings, no drum, and 2 dB under the rotation (-22 LUFS
# integrated instead of -20) so the pause feels like a held breath. Ends on D held into the downbeat.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from _music import render

PIECE = {
    "id": "music-still-pages", "title": "Still Pages", "role": "pause", "seed": 805, "generator": __file__,
    "final": "D", "mode": "dorian", "bpm": 60, "loop": True, "target_i": -22.0,
    "form": "four short phrases with rests between; 60 s seamless loop",
    "melody": [
        "A4:3", "-:1 D5:2", "C5:3", "-:3",
        "E5:2 D5:1", "A4:3", "-:3", "-:1 G4:2",
        "A4:3", "-:3", "C5:2 D5:1", "E5:3",
        "-:3", "D5:2 C5:1", "A4:3", "-:3",
        "G4:2 A4:1", "C5:3", "-:1 E5:2", "D5:3",
    ],
    "lyre": [(1, 1, ["D3"]), (4, 2, ["A3"]), (5, 1, ["D3", "A3"]), (8, 1, ["G3"]), (10, 1, ["A3"]),
             (13, 1, ["C3", "G3"]), (16, 1, ["A3"]), (17, 1, ["D3", "A3"]), (19, 1, ["E3"])],
    "gains": {"pipe": 0.8},
    "drone": [("D2", 0.45), ("D3", 1.0), ("A3", 0.6)],
    "instruments": {"pipe": "lead, wooden pipe, sparse, low register", "drone": "river drone D2 + D3 + A3",
                    "lyre": "single strings and two fifths", "frame_drum": "none"},
    "used_in": "Music: Still Pages, for the paused book (D Dorian, 3/4 at 60 BPM, 20 bars, 60 s seamless loop, 2 dB quieter)",
}

if __name__ == "__main__":
    render(PIECE)

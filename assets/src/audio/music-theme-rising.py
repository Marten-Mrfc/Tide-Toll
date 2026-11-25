# python3 assets/src/audio/music-theme-rising.py  ->  assets/audio/music/music-theme-rising.ogg (48 kHz stereo, not a loop)
#                                                    assets/audio/music/music-theme-rising-score.json
# The age change: the Hamlet theme (theme-hamlet.py, D Dorian, 3/4 at 64 BPM) made grander, played once with an
# ending. Bars 1-12 are the theme's tune note for note (bars 1-8 and 9-12 of its second pass); bars 13-16 lift it:
# G A G | E G A | B (held) C | D6, held 4.5 beats, an octave above home. Fuller than the theme: a second pipe plays
# a counter-melody under it (mostly sixths and contrary motion, D4-E5), the lyre rolls a chord every bar instead of
# every two (fifths plus octaves, three strings at the end), and the frame drum plays from bar 1, with a stroke on
# every beat in the last phrase. The drone swells in under the first bar and dies away with the last note.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from _music import render

PIECE = {
    "id": "music-theme-rising", "title": "Tide and Toll, Rising", "role": "age-change", "seed": 804, "generator": __file__,
    "final": "D", "mode": "dorian", "bpm": 64, "loop": False, "end_hold_beats": 4.5, "tail_s": 2.6,
    "form": "the theme once (bars 1-12 as written), bars 13-16 lift to D6; a held last note and a real ending",
    "melody": [
        "D5:1 E5:1 G5:1", "A5:3", "B5:1 A5:1 G5:1", "A5:2 E5:1",                        # theme bars 1-4
        "G5:1 A5:1 G5:1", "E5:2 F5:1", "E5:1 D5:1 E5:1", "D5:3",                        # theme bars 5-8
        "D5:1 E5:1 G5:1", "A5:3", "B5:2 A5:1", "A5:2 E5:1",                             # theme bars 9-12
        "G5:1 A5:1 G5:1", "E5:1 G5:1 A5:1", "B5:2 C6:1", "D6:3",                        # the lift
    ],
    "counter": [
        "A4:3", "C5:2 B4:1", "G4:2 E4:1", "E4:1 F4:1 G4:1",
        "C5:2 D5:1", "C5:2 A4:1", "G4:1 A4:1 G4:1", "D4:1 F4:1 A4:1",
        "A4:2 B4:1", "C5:3", "D5:2 C5:1", "C5:2 B4:1",
        "B4:1 C5:1 B4:1", "G4:1 B4:1 C5:1", "D5:2 E5:1", "A4:3",
    ],
    "drum": [(b, 1, "dum") for b in range(1, 17)] + [(b, 3, "tek") for b in range(1, 13)]
            + [(b, bt, "tek") for b in (13, 14, 15) for bt in (2, 3)] + [(15, 3.5, "tek")],
    "lyre": [(1, 1, ["D3", "A3", "D4"]), (2, 1, ["A3"]), (3, 1, ["G3", "D4"]), (4, 1, ["A3", "E4"]),
             (5, 1, ["C3", "G3", "C4"]), (6, 1, ["A3"]), (7, 1, ["G3", "D4"]), (8, 1, ["D3", "A3", "D4"]),
             (9, 1, ["D3", "A3", "D4"]), (10, 1, ["F3", "C4"]), (11, 1, ["G3", "D4"]), (12, 1, ["A3", "E4"]),
             (13, 1, ["C3", "G3", "C4"]), (14, 1, ["E3", "B3"]), (15, 1, ["G3", "D4", "G4"]),
             (16, 1, ["D3", "A3", "D4"])],
    "gains": {"lyre": 1.15},
    "drone": [("D2", 0.45), ("D3", 1.0), ("A3", 0.6)],
    "instruments": {"pipe": "lead, wooden pipe (the theme's tune)", "counter_pipe": "second wooden pipe, counter-melody",
                    "drone": "river drone D2 + D3 + A3, swells in, dies with the last note",
                    "lyre": "a chord every bar", "frame_drum": "whole piece, every beat in bars 13-15"},
    "used_in": "Music: Tide and Toll, Rising, the theme made grander for the age change (D Dorian, 3/4 at 64 BPM, 16 bars, played once with an ending)",
}

if __name__ == "__main__":
    render(PIECE)

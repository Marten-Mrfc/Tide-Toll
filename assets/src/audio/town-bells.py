# python3 assets/src/audio/town-bells.py  ->  assets/audio/sfx/ (48 kHz 16-bit mono WAV)
# Pitched town moments, all in the music's D Dorian (D E F G A B C) so they sit on the score without clashing.
#   level-up-01..02        a level reached: a rising phrase of four small hand bells, about 2 s
#                          01: D E G A (the bell motif's notes); 02: A C D E, ending a fifth above D
#   gift-01..03            a gift arrives: two to four wooden mallet taps, then one bright brass chime bar
#   achievement-01..02     a wax seal pressed into warm wax, then a chime (A5 / D6)
#   request-asked-01..02   a request: a small hand bell rung twice (hum A4 / G4)
#   request-met-01..02     a request met: a quick bright flourish on chime bars ending on D or A
#   request-missed-01      a request missed: two soft falling wooden notes, A4 to D4 (a resolving fifth, not sad)
#   charter-01             the charter granted: three chapel bell strokes, A4, E4, D4, the last left to ring (4 s)
# Voices in _town.py; seeds 950-999. Level: bells -21 LUFS (bell.py), small chimes -22, soft ones -25.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import audio as A
from _synth import place_at
from _town import small_bell, chime, wood_knock, seal, church_bell, finish

SR = A.SR
G = __file__


def check_key(names):
    dorian = {"D", "E", "F", "G", "A", "B", "C"}
    assert all(n[:-1] in dorian for n in names), names


# ---- level-up: four small bells rising, spaced a little wider each time, last one rings
for i, notes in enumerate((["D4", "E4", "G4", "A4"], ["A4", "C5", "D5", "E5"]), start=1):
    check_key(notes)
    seed = 950 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(2.2 * SR))
    tt = 0.0
    for k, nm in enumerate(notes):
        last = k == len(notes) - 1
        b = small_bell(rng, nm, force=0.8 + 0.1 * k, dur=1.5 if last else 0.9)
        place_at(x, b * (0.75 + 0.08 * k), tt)
        tt += 0.16 + 0.03 * k
    finish("level-up", i, x, -21.0, seed, f"Town: level reached, rising small bells {'-'.join(notes)}", G,
           fout=0.4, fmax=6000)

# ---- gift: mallet taps then a chime
GIFTS = [(["D4", "A4"], "D6"), (["G4", "A4", "D5"], "A5"), (["A4", "G4", "E4", "A4"], "E6")]
for i, (taps, ch) in enumerate(GIFTS, start=1):
    check_key(taps + [ch])
    seed = 960 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(1.9 * SR))
    tt = 0.0
    for nm in taps:
        place_at(x, wood_knock(rng, A.NOTE[nm], dur=0.3, crack=0.4) * rng.uniform(0.55, 0.7), tt)
        tt += rng.uniform(0.09, 0.12)
    place_at(x, chime(rng, ch, dur=1.9 - tt - 0.05, force=1.0, tau=0.6), tt + 0.05)
    finish("gift", i, x, -22.0, seed, f"Town: gift arrives on the map (mallet taps {'-'.join(taps)}, chime {ch})", G,
           fout=0.3)

# ---- achievement: seal pressed, then chime
for i, ch in enumerate(("A5", "D6"), start=1):
    seed = 970 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(2.0 * SR))
    place_at(x, seal(rng), 0.0)
    place_at(x, chime(rng, ch, dur=1.55, tau=0.75) * 0.6, 0.38)
    place_at(x, chime(rng, "D5" if ch == "A5" else "A5", dur=1.5, tau=0.7, force=0.6) * 0.3, 0.40)
    finish("achievement", i, x, -21.0, seed, f"Town: achievement, a wax seal pressed then a chime ({ch})", G, fout=0.3)

# ---- request-asked: a hand bell rung twice, second a little softer
for i, nm in enumerate(("A4", "G4"), start=1):
    seed = 980 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(1.3 * SR))
    place_at(x, small_bell(rng, nm, force=1.0, dur=1.3), 0.0)
    place_at(x, small_bell(rng, nm, force=0.85, dur=1.0) * 0.8, rng.uniform(0.2, 0.24))
    finish("request-asked", i, x, -22.0, seed, f"Town: request asked, a hand bell rung twice (hum {nm})", G, fout=0.3,
           fmax=6000)

# ---- request-met: a quick rising flourish on chime bars
for i, notes in enumerate((["D5", "E5", "F5", "A5", "D6"], ["E5", "G5", "A5"]), start=1):
    check_key(notes)
    seed = 985 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(1.4 * SR))
    tt = 0.0
    for k, nm in enumerate(notes):
        last = k == len(notes) - 1
        place_at(x, chime(rng, nm, dur=1.4 - tt, tau=0.55 if last else 0.25, force=0.8) * (1.0 if last else 0.6), tt)
        tt += 0.065 if len(notes) > 3 else 0.09
    finish("request-met", i, x, -22.0, seed, f"Town: request met, a small bright flourish {'-'.join(notes)}", G,
           fout=0.3)

# ---- request-missed: two soft wooden notes falling A4 -> D4
seed = 991
rng = np.random.default_rng(seed)
x = np.zeros(int(0.8 * SR))
place_at(x, wood_knock(rng, A.NOTE["A4"], dur=0.5, crack=0.0, soft=0.003), 0.0)
place_at(x, wood_knock(rng, A.NOTE["D4"], dur=0.5, crack=0.0, soft=0.003) * 0.9, 0.2)
finish("request-missed", 1, A.lowpass(x, 2500, 4), -25.0, seed,
       "Town: request missed, two soft falling wooden notes A4 to D4", G, fout=0.2, fmax=4000)

# ---- charter: three chapel bell strokes, the first two damped, the last left to ring
seed = 995
rng = np.random.default_rng(seed)
x = np.zeros(int(4.2 * SR))
for k, (nm, damp, force) in enumerate((("A4", 0.25, 0.95), ("E4", 0.25, 0.95), ("D4", 0.8, 1.05))):
    b = church_bell(rng, nm, dur=4.2 - 0.6 * k, force=force, damp=damp)
    place_at(x, b, 0.6 * k)
finish("charter", 1, x, -20.0, seed, "Town: charter granted, three chapel bell strokes A4 E4 D4, the last rings", G,
       fout=0.8, fmax=4000)

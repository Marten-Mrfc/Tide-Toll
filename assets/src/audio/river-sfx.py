# python3 assets/src/audio/river-sfx.py  ->  assets/audio/sfx/ (48 kHz 16-bit mono WAV, dry like coin and place)
# The second age (The River), docs/plans/age2-design.md. Voices live in _river2.py (on top of _town and _river);
# seeds 1000-1099. Levels follow coin (-24 LUFS momentary max): target = -24 + the volume in river-zz-sounds-json.py.
#   moor-01..03       a boat moors: a soft hull bump on the quay, then a mooring line thrown onto the planks (~1.4 s)
#   cast-off-01..02   a filled boat casts off: a short cheerful rising two-note horn call, then a push of water (~1.6 s)
#   quay-drop-01..04  a cargo unit set down on the quay: a sack or bale dropped on planks (~0.6 s)
#   tide-turn-01..02  the tide turns: a low wooden clapper, three slow knocks (A3 A3 D3 / D3 D3 A2) (~2.3 s)
#   loom-01..03       the loom: shuttle and beater, two beats (~1.3 s)
#   sheep-01..03      a sheep: one short soft bleat (~0.6 s)
#   crane-01..02      the crane: a creak of the treadwheel and a rope under load (~1.7 s)
#   toll-01..03       a toll paid: three to five coins dropped into a wooden box (~0.9 s)
#   seal-01           the borough seal granted: four chapel bell strokes D5 A4 E4 D4 (hum), the last left to ring (5 s)
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import audio as A
from _synth import place_at
from _river import horn
from _town import church_bell, finish
from _river2 import (hull_bump, rope_throw, water_push, sack_drop, clapper, shuttle, beater, bleat, rope_strain,
                     coin_in_box, creak)

SR = A.SR
G = __file__
REF = -24.0   # coin's loudness (town-sounds.json reference -24.2); targets are REF + the event's volume_db

# ---- moor: hull bump, then the line thrown (volume +1)
for i in range(1, 4):
    seed = 1000 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(1.5 * SR))
    place_at(x, hull_bump(rng, rng.uniform(80, 96)), 0.0)
    place_at(x, rope_throw(rng) * 1.3, rng.uniform(0.42, 0.52))
    finish("moor", i, x, REF + 1.0, seed, "River: a boat moors (soft hull bump on the quay, a mooring line thrown)",
           G, fout=0.12, fmax=6000)

# ---- cast-off: two-note horn call rising (tuned, D Dorian), then the push of water (volume +2)
for i, (n1, n2) in enumerate((("D4", "A4"), ("A3", "D4")), start=1):
    seed = 1010 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(1.7 * SR))
    place_at(x, horn(rng, n1, 0.17) * 0.85, 0.0)
    place_at(x, horn(rng, n2, 0.34), 0.21)
    place_at(x, water_push(rng, rng.uniform(0.75, 0.9)) * 1.4, 0.62)
    finish("cast-off", i, x, REF + 2.0, seed, f"River: a filled boat casts off (horn call {n1}-{n2}, a push of water)",
           G, fout=0.15, fmax=6000)

# ---- quay-drop: a sack or bale dropped on the planks (volume 0)
for i in range(1, 5):
    seed = 1020 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(0.62 * SR))
    place_at(x, sack_drop(rng, rng.uniform(105, 135), weight=rng.uniform(0.85, 1.0)), 0.0)
    finish("quay-drop", i, x, REF + 0.0, seed, "River: a cargo unit set down on the quay (sack dropped on planks)",
           G, fout=0.1, fmax=6000)

# ---- tide-turn: a low wooden clapper, three slow knocks (tuned, volume +1)
for i, notes in enumerate((("A3", "A3", "D3"), ("D3", "D3", "A2")), start=1):
    seed = 1030 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(2.4 * SR))
    for k, nm in enumerate(notes):
        place_at(x, clapper(rng, A.NOTE[nm], force=(1.0, 0.9, 0.95)[k]), 0.8 * k)
    finish("tide-turn", i, x, REF + 1.0, seed, f"River: the tide turns (low wooden clapper, three slow knocks "
           f"{' '.join(notes)})", G, fout=0.3, fmax=4000)

# ---- loom: shuttle and beater, twice (volume -1)
for i in range(1, 4):
    seed = 1040 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(1.35 * SR))
    tt = 0.0
    for k in range(2):
        s = shuttle(rng)
        place_at(x, s * (1.0 if k == 0 else 0.9), tt)
        tb = tt + rng.uniform(0.26, 0.32)
        place_at(x, beater(rng, rng.uniform(140, 165)) * (1.0 if k == 0 else 0.95), tb)
        tt = tb + rng.uniform(0.2, 0.26)
    finish("loom", i, x, REF - 1.0, seed, "River: the weaver's loom (shuttle and beater, two beats)", G, fout=0.15,
           fmax=6000)

# ---- sheep: one soft bleat (volume -2)
for i, (f0, d) in enumerate(((255.0, 0.58), (285.0, 0.5), (235.0, 0.64)), start=1):
    seed = 1050 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int((d + 0.08) * SR))
    place_at(x, bleat(rng, f0 * rng.uniform(0.98, 1.02), d), 0.0)
    finish("sheep", i, x, REF - 2.0, seed, "River: a sheep on the far-bank pasture (one short soft bleat)", G,
           fout=0.08, fmax=6000)

# ---- crane: treadwheel creak, then the rope taking the load (volume 0)
for i in range(1, 3):
    seed = 1060 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(1.75 * SR))
    wd = rng.uniform(0.9, 1.1)
    wheel = creak(rng, wd, rate=(35.0, 70.0), body=(210.0, 470.0, 900.0), tau=0.018, jitter=0.3, swell=0.3)
    place_at(x, A.lowpass(wheel, 2500, 4), 0.0)
    place_at(x, rope_strain(rng, rng.uniform(0.75, 0.9)) * 0.45, wd * 0.55)
    finish("crane", i, x, REF + 0.0, seed, "River: the crane (treadwheel creak, a rope under load)", G, fout=0.2,
           fmax=6000)

# ---- toll: three to five coins dropped into a wooden box (volume +1)
for i, count in enumerate((3, 4, 5), start=1):
    seed = 1070 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(1.0 * SR))
    tt = 0.0
    for k in range(count):
        place_at(x, coin_in_box(rng, rng.uniform(2200, 3200)) * rng.uniform(0.6, 1.0), tt)
        tt += rng.uniform(0.07, 0.13)
    finish("toll", i, x, REF + 1.0, seed, f"River: a toll paid at the drawbridge ({count} coins into a wooden box)",
           G, fout=0.15)

# ---- seal: four chapel bell strokes, the first three damped, the last left to ring (about 5 s, volume +4.5)
seed = 1090
rng = np.random.default_rng(seed)
x = np.zeros(int(5.0 * SR))
for k, (nm, damp, force) in enumerate((("D5", 0.25, 0.9), ("A4", 0.25, 0.95), ("E4", 0.3, 0.95),
                                       ("D4", 0.9, 1.1))):
    place_at(x, church_bell(rng, nm, dur=5.0 - 0.6 * k, force=force, damp=damp), 0.6 * k)
finish("seal", 1, x, REF + 4.5, seed, "River: borough seal granted, four chapel bell strokes D5 A4 E4 D4, the last "
       "rings", G, fout=1.0, fmax=4000)

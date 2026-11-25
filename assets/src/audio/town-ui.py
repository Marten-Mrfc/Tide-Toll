# python3 assets/src/audio/town-ui.py  ->  assets/audio/sfx/ (48 kHz 16-bit mono WAV, dry, short)
#   ui-press-01..04    a paper tick for a press (a card tab snapping, 2-4 kHz, about 90 ms)
#   ui-hover-01..02    the same, softer and darker (for hover; much quieter)
#   ui-tab-01..03      a ribbon bookmark flicked: a short fluttering swish and a small tick as it lands
#   book-close-01..02  the book closing: leaves swishing shut, then the soft thump of the board cover
#   book-open-01..02   the book opening: a creak of the leather spine, then one leaf of paper falling open
#   page-turn-01..03   one page turned: lift, swish across, a soft flap as it lands
# Voices in _town.py; seeds 1000-1049. UI sits under coin (-24 LUFS): press -28.5, hover -33, tab -28, book -25.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import audio as A
from _synth import place_at, crackles, burst
from _town import paper_tick, swish, cover_thump, creak, finish

SR = A.SR
G = __file__

for i in range(1, 5):
    seed = 1000 + i
    rng = np.random.default_rng(seed)
    finish("ui-press", i, paper_tick(rng), -28.5 + rng.uniform(-0.4, 0.4), seed,
           "Interface: button press, a paper tick", G, fout=0.02, fmax=8000)
for i in range(1, 3):
    seed = 1010 + i
    rng = np.random.default_rng(seed)
    finish("ui-hover", i, paper_tick(rng, soft=True), -33.0, seed, "Interface: hover, a soft paper tick", G,
           fout=0.02, fmax=8000)
for i in range(1, 4):
    seed = 1020 + i
    rng = np.random.default_rng(seed)
    d = rng.uniform(0.14, 0.19)
    x = np.zeros(int(0.3 * SR))
    place_at(x, swish(rng, d, 1500, 6000, peak=0.6, flutter=0.6, flutter_hz=rng.uniform(28, 40)) * 0.6, 0.0)
    place_at(x, paper_tick(rng, soft=True) * 0.6, d - 0.01)
    finish("ui-tab", i, A.lowpass(x, 7000, 2), -28.0, seed, "Interface: tab change, a ribbon bookmark flicked", G,
           fout=0.03, fmax=8000)

for i in range(1, 3):
    seed = 1030 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(0.75 * SR))
    place_at(x, swish(rng, 0.22, 400, 3500, peak=0.75) * 0.25, 0.0)                    # leaves fanning shut
    place_at(x, cover_thump(rng, rng.uniform(100, 125)), 0.2)
    place_at(x, cover_thump(rng, rng.uniform(170, 200)) * 0.15, 0.218)                  # the back board answers
    finish("book-close", i, x, -25.0, seed, "Interface: book closed, the soft thump of its cover", G, fout=0.08,
           fmax=6000)

for i in range(1, 3):
    seed = 1040 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(0.9 * SR))
    cr = creak(rng, 0.3, rate=(35.0, 70.0), body=(260.0, 520.0, 900.0), tau=0.01, jitter=0.3, swell=0.4)
    place_at(x, A.lowpass(cr, 2500, 2) * 0.6, 0.0)                                      # leather spine
    leaf = swish(rng, 0.32, 700, 5000, peak=0.4) * 0.35
    leaf += crackles(rng, 0.32, 10, lo=2000, hi=6000, length=0.002, gain=(0.05, 0.15))
    place_at(x, leaf, 0.26)
    place_at(x, burst(rng, 0.05, 200, 1200, 0.012, attack=0.003) * 0.25, 0.56)          # the leaf lands
    finish("book-open", i, x, -25.0, seed, "Interface: book opened, a spine creak and a leaf of paper", G,
           fout=0.08, fmax=8000)

for i in range(1, 4):
    seed = 1045 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(0.55 * SR))
    d = rng.uniform(0.3, 0.38)
    place_at(x, crackles(rng, 0.06, 5, lo=2500, hi=6000, length=0.002, gain=(0.05, 0.15)), 0.0)   # corner lifted
    place_at(x, swish(rng, d, 600, 5000, peak=rng.uniform(0.45, 0.6)) * 0.4, 0.03)
    place_at(x, burst(rng, 0.06, 250, 1500, 0.01, attack=0.002) * 0.25, 0.03 + d - 0.03)          # soft flap
    finish("page-turn", i, x, -27.0, seed, "Interface: one page turned", G, fout=0.06, fmax=8000)

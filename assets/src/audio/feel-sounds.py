# python3 assets/src/audio/feel-sounds.py  ->  assets/audio/sfx/{coin-hop,dust-puff,grit,ripple,bell-creak,gift-ring}-NN.wav
# Night 3 (feel): the small rewards on the map, each with its sound. Same family as the town set (_town.py):
# short, soft, dry, wooden/metal/water, levelled against the coin (about -24 LUFS momentary max) so they sit
# as loud as their neighbours. Pitched things in D Dorian. Written to town-sounds.json by town-zz-sounds-json.py.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import audio as A
from _synth import place_at, burst, bubble, modal
from _town import grains, finish, chime, creak, coin, swish

COIN = -24.2
SR = A.SR


def buf(d):
    return np.zeros(int(d * SR))


# coin-hop: one small coin flipped up and landing on the others: a light flick, a short ring, a soft tick.
for i in range(1, 5):
    seed = 1300 + i
    rng = np.random.default_rng(seed)
    x = buf(0.4)
    place_at(x, burst(rng, 0.02, 3000, 7000, 0.003, attack=0.0003) * 0.25, 0.0)          # the flick
    place_at(x, coin(rng, rng.uniform(2400, 3400)) * 0.8, rng.uniform(0.11, 0.16))         # lands
    finish("coin-hop", i, x, COIN - 3.0, seed, "Feel: a coin hops out at the delivery (pitch varied)", __file__)

# dust-puff: a breath of dry earth: soft low noise puff, a few grains.
for i in range(1, 4):
    seed = 1310 + i
    rng = np.random.default_rng(seed)
    x = buf(0.35)
    place_at(x, swish(rng, 0.25, lo=200, hi=1400, peak=0.2), 0.0)
    x += grains(rng, 0.35, 60, lo=1200, hi=4000, gain=(0.02, 0.06), shape=lambda u: np.exp(-u * 5))
    finish("dust-puff", i, A.lowpass(x, 3000, 4), COIN - 4.0, seed, "Feel: dust puff (a cottage extended, a new trail)", __file__)

# grit: a handful of gravel grains scattered along a road.
for i in range(1, 4):
    seed = 1320 + i
    rng = np.random.default_rng(seed)
    x = grains(rng, 0.55, 380, lo=1500, hi=6000, gain=(0.1, 0.6), shape=lambda u: np.sin(np.pi * min(u * 1.2, 1)) ** 0.6)
    finish("grit", i, x, COIN - 2.0, seed, "Feel: grit scattering along a road being paved", __file__)

# ripple: a small lap of water against a post: soft slosh and one or two bubbles.
for i in range(1, 4):
    seed = 1330 + i
    rng = np.random.default_rng(seed)
    x = buf(0.5)
    m = int(0.35 * SR)
    tt = np.arange(m) / SR
    place_at(x, A.bandpass(rng.standard_normal(m), 250, 1300, 2) * np.minimum(tt / 0.03, 1) * np.exp(-tt / 0.08) * 0.6, 0.0)
    for _ in range(int(rng.integers(1, 3))):
        place_at(x, bubble(rng, rng.uniform(600, 1100), 0.06, 0.014, 0.5) * 0.15, rng.uniform(0.03, 0.2))
    finish("ripple", i, A.lowpass(x, 3500, 4), COIN - 3.0, seed, "Feel: a ripple at the landing or a ford stone", __file__)

# bell-creak: the bell's yoke turning in its wooden frame: a short stick-slip creak.
for i in range(1, 3):
    seed = 1340 + i
    rng = np.random.default_rng(seed)
    x = creak(rng, 0.45, rate=(70.0, 120.0), body=(260.0, 520.0, 980.0), swell=0.3)
    finish("bell-creak", i, A.lowpass(x, 3000, 4), COIN - 3.0, seed, "Feel: the bell swings in its frame", __file__)

# gift-ring: a soft struck chime (D Dorian), short: a level's gift arriving on the ground.
for i, note in enumerate(("A5", "D6"), start=1):
    seed = 1350 + i
    rng = np.random.default_rng(seed)
    x = chime(rng, note, dur=0.9, force=0.6, tau=0.35)
    finish("gift-ring", i, x, COIN - 1.0, seed, "Feel: soft ring where a level's gift stands", __file__, fout=0.2)

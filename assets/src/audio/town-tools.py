# python3 assets/src/audio/town-tools.py  ->  assets/audio/sfx/ (48 kHz 16-bit mono WAV, dry like coin and place)
#   buy-01..04          an upgrade bought: a quill scratch in the ledger, then one small coin dropped (about 0.6 s)
#   cannot-01..02       not enough coin: a dull, kind double knock on a wooden counter (low, soft felt, no buzz)
#   extend-01..03       a cottage extended: a short saw (three strokes), then two hammer blows (about 1.4 s)
#   pave-gravel-01..03  a road paved with gravel: a shovelful poured, then two rake strokes (about 1.2 s)
#   pave-cobbles-01..03 a road paved with setts: three stones set down, two mallet taps to bed them (about 1.1 s)
# Voices live in _town.py; seeds 900-949. Levels follow coin (-24 LUFS momentary max) and place (-22).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import audio as A
from _synth import place_at
from _town import quill, coin, wood_knock, saw, hammer, grains, swish, stone, finish

SR = A.SR
G = __file__

# ---- buy: quill scratch + one coin
for i in range(1, 5):
    seed = 900 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(0.65 * SR))
    q = quill(rng, rng.uniform(0.26, 0.34), strokes=int(rng.integers(2, 4)))
    place_at(x, q * 0.55, 0.0)
    place_at(x, A.lowpass(coin(rng, rng.uniform(2300, 3100)), 9000, 4), len(q) / SR + rng.uniform(0.0, 0.03))
    finish("buy", i, x, -23.0 + rng.uniform(-0.5, 0.5), seed, "Town: upgrade bought (quill scratch in the ledger, "
           "then a small coin)", G)

# ---- cannot: two dull felt knocks on a counter (G3 then D3 body, no crack, low-passed: kind, not an error buzz)
for i, (n1, n2) in enumerate((("G3", "D3"), ("A3", "E3")), start=1):
    seed = 910 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(0.5 * SR))
    place_at(x, wood_knock(rng, A.NOTE[n1], crack=0.0, soft=0.004), 0.0)
    place_at(x, wood_knock(rng, A.NOTE[n2], crack=0.0, soft=0.004) * 0.75, rng.uniform(0.13, 0.16))
    x = A.lowpass(x, 1600, 4)
    finish("cannot", i, x, -25.0, seed, f"Town: not enough coin, a dull kind knock on a wooden counter ({n1}, {n2})",
           G, fmax=4000)

# ---- extend: saw then two hammer blows
for i in range(1, 4):
    seed = 920 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(1.5 * SR))
    sd = rng.uniform(0.5, 0.65)
    place_at(x, saw(rng, sd, strokes=int(rng.integers(3, 5))) * 0.5, 0.0)
    t1 = sd + rng.uniform(0.12, 0.18)
    place_at(x, hammer(rng, rng.uniform(160, 200), 1.0), t1)
    place_at(x, hammer(rng, rng.uniform(160, 200), 0.85) * 0.9, t1 + rng.uniform(0.28, 0.34))
    finish("extend", i, x, -22.0, seed, "Town: cottage extended (a short saw, then two hammer blows)", G)

# ---- pave-gravel: pour (grain density rising then falling, low-mid) and two rake strokes
for i in range(1, 4):
    seed = 930 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(1.3 * SR))
    pd = rng.uniform(0.5, 0.6)
    pour = grains(rng, pd, 900, lo=350, hi=3500, length=(0.002, 0.007), gain=(0.1, 0.8),
                  shape=lambda u: np.sin(np.pi * min(u * 1.3, 1)) ** 0.7)
    pour += A.lowpass(swish(rng, pd, 150, 900, peak=0.35), 900, 2) * 0.35          # the heap settling
    place_at(x, pour, 0.0)
    tt = pd + 0.05
    for k in range(2):
        rd = rng.uniform(0.22, 0.3)
        rake = grains(rng, rd, 500, lo=600, hi=4500, length=(0.0015, 0.004), gain=(0.1, 0.6),
                      shape=lambda u: np.sin(np.pi * u))
        rake += swish(rng, rd, 1200, 4000, peak=0.5) * 0.15                         # tines scraping
        place_at(x, rake * (1.0 if k == 0 else 0.8), tt)
        tt += rd + rng.uniform(0.04, 0.08)
    finish("pave-gravel", i, x, -23.0, seed, "Town: road paved with gravel (poured, then raked)", G)

# ---- pave-cobbles: three setts set down, then two mallet taps (wooden mallet on stone: wood knock + clack)
for i in range(1, 4):
    seed = 940 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(1.2 * SR))
    tt = 0.0
    for k in range(3):
        place_at(x, stone(rng, rng.uniform(900, 1500)) * rng.uniform(0.7, 1.0), tt)
        tt += rng.uniform(0.16, 0.22)
    tt += 0.08
    for k in range(2):
        tap = wood_knock(rng, A.NOTE["D3"] * rng.uniform(0.97, 1.03), crack=0.3) * 0.7
        place_at(tap, stone(rng, rng.uniform(1600, 2000)) * 0.35, 0.0)
        place_at(x, tap * (1.0 if k == 0 else 0.8), tt)
        tt += rng.uniform(0.17, 0.21)
    finish("pave-cobbles", i, x, -23.0, seed, "Town: road paved with cobbles (stones set, then tapped in)", G)

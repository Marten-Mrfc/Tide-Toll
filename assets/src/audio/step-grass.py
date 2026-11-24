# python3 assets/src/audio/step-grass.py  ->  assets/audio/sfx/step-grass-01..08.wav (48 kHz 16-bit mono)
# Hamlet footwear set (soft leather): a heel swish, then a quieter ball-of-foot swish 50-90 ms later, a few
# grass-blade crackles and a faint muted thud. Unpitched (unpaved ground). Kept inside the 1-6 kHz footfall lane.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import audio as A, ledger, asset_path
from _synth import burst, crackles, place_at, level, report, band_energy

TARGET = -25.0                          # loudest 400 ms block, LUFS (quiet: many walkers, under the music)


def step(rng):
    dur = 0.32
    x = np.zeros(int(dur * A.SR))
    lo, hi = 1000 * rng.uniform(0.9, 1.2), 4600 * rng.uniform(0.85, 1.1)
    heel = burst(rng, 0.2, lo, hi, 0.045 * rng.uniform(0.85, 1.2), attack=0.006)
    toe_t = rng.uniform(0.05, 0.09)
    toe = burst(rng, 0.2, lo * 1.15, hi, 0.035 * rng.uniform(0.8, 1.2), attack=0.008) * rng.uniform(0.45, 0.7)
    thud = A.lowpass(rng.standard_normal(int(0.08 * A.SR)), 180, 2) * A.env_exp(int(0.08 * A.SR), 0.018) * 0.35
    crk = crackles(rng, dur, int(rng.integers(4, 9)), start=0.004, spread=toe_t + 0.08, lo=2200, hi=5200)
    place_at(x, heel, 0.0); place_at(x, toe, toe_t); place_at(x, thud, 0.002); x += crk * 0.9
    return A.fade(A.lowpass(x, 5500, 4), 0.001, 0.03)


for i in range(1, 9):
    seed = 100 + i
    rng = np.random.default_rng(seed)
    x = level(step(rng), TARGET + rng.uniform(-1, 1))
    out = A.write_wav(asset_path(f"audio/sfx/step-grass-{i:02d}.wav"), x)
    report(out, f"above 6 kHz {band_energy(x, 6000, 24000):.3%}, 1-6 kHz {band_energy(x, 1000, 6000):.0%}")
    ledger.add("audio", out, kind="sound", generator=__file__, verified=True, used_in="Villagers: footsteps on grass (Hamlet, soft leather)", seed=seed)

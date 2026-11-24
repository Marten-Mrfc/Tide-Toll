# python3 assets/src/audio/step-dirt.py  ->  assets/audio/sfx/step-dirt-01..08.wav (48 kHz 16-bit mono)
# Hamlet footwear set (soft leather) on a dirt trail: drier and shorter than grass, more low-mid body (heel
# pad on packed earth), a little grit scuff on the toe roll. Unpitched (unpaved ground).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import audio as A, ledger, asset_path
from _synth import burst, crackles, place_at, level, report, band_energy

TARGET = -25.0


def step(rng):
    dur = 0.24
    x = np.zeros(int(dur * A.SR))
    body = burst(rng, 0.12, 220, 1600 * rng.uniform(0.9, 1.1), 0.022 * rng.uniform(0.85, 1.2), attack=0.002)
    toe_t = rng.uniform(0.045, 0.075)
    toe = burst(rng, 0.1, 400, 2200, 0.016 * rng.uniform(0.8, 1.2), attack=0.002) * rng.uniform(0.4, 0.6)
    scuff = burst(rng, 0.09, 2200, 5200, 0.025, attack=0.01) * rng.uniform(0.18, 0.3)
    grit = crackles(rng, dur, int(rng.integers(3, 7)), start=toe_t, spread=0.07, lo=2500, hi=5500, length=0.0015,
                    gain=(0.1, 0.3))
    place_at(x, body, 0.0); place_at(x, toe, toe_t); place_at(x, scuff, toe_t + 0.004); x += grit
    return A.fade(A.lowpass(x, 5500, 4), 0.0008, 0.03)


for i in range(1, 9):
    seed = 200 + i
    rng = np.random.default_rng(seed)
    x = level(step(rng), TARGET + rng.uniform(-1, 1))
    out = A.write_wav(asset_path(f"audio/sfx/step-dirt-{i:02d}.wav"), x)
    report(out, f"above 6 kHz {band_energy(x, 6000, 24000):.3%}, below 1 kHz {band_energy(x, 0, 1000):.0%}")
    ledger.add("audio", out, kind="sound", generator=__file__, verified=True, used_in="Villagers: footsteps on dirt trail (Hamlet, soft leather)", seed=seed)

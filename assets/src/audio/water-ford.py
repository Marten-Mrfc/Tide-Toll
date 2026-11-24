# python3 assets/src/audio/water-ford.py  ->  assets/audio/sfx/water-ford-01..06.wav (48 kHz 16-bit mono)
# Wading splash at the ford: a soft slap as a foot or hoof meets the water, a sloshing body that falls away,
# a few rising Minnaert bubble plinks and a quiet drip tail. Low-passed so it stays calm (no hiss on top).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import audio as A, ledger, asset_path
from _synth import burst, bubble, place_at, level, report, band_energy

TARGET = -23.0


def splash(rng):
    dur = rng.uniform(0.75, 0.95)
    x = np.zeros(int(dur * A.SR))
    slap = burst(rng, 0.12, 350, 2600, 0.03 * rng.uniform(0.8, 1.2), attack=0.004)
    n = int((dur - 0.02) * A.SR)
    t = np.arange(n) / A.SR
    # slosh: low-mid noise whose band drifts down while it decays, with a slow wobble
    slosh = A.bandpass(rng.standard_normal(n), 180, 1500, 2) * np.minimum(t / 0.02, 1) * np.exp(-t / 0.14)
    slosh = A.fade(slosh, 0, 0.15)
    slosh *= 1 + 0.5 * np.sin(2 * np.pi * rng.uniform(5, 9) * t + rng.uniform(0, 6))
    place_at(x, slap, 0.0)
    place_at(x, slosh * rng.uniform(0.6, 0.9), 0.012)
    for _ in range(int(rng.integers(5, 10))):
        f0 = rng.uniform(450, 1600)
        b = bubble(rng, f0, 0.06, rng.uniform(0.008, 0.02), rise=rng.uniform(0.3, 0.9)) * rng.uniform(0.08, 0.22)
        place_at(x, b, rng.uniform(0.02, 0.4))
    for _ in range(int(rng.integers(2, 4))):                       # late drips
        b = bubble(rng, rng.uniform(900, 1900), 0.05, 0.01, rise=0.8) * rng.uniform(0.04, 0.09)
        place_at(x, b, rng.uniform(0.4, dur - 0.1))
    return A.fade(A.lowpass(x, 4500, 4), 0.002, 0.08)


for i in range(1, 7):
    seed = 300 + i
    rng = np.random.default_rng(seed)
    x = level(splash(rng), TARGET + rng.uniform(-1, 1))
    out = A.write_wav(asset_path(f"audio/sfx/water-ford-{i:02d}.wav"), x)
    report(out, f"above 6 kHz {band_energy(x, 6000, 24000):.3%}")
    ledger.add("audio", out, kind="sound", generator=__file__, verified=True, used_in="Crossings: ford wading splash / lap", seed=seed)

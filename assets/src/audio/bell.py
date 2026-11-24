# python3 assets/src/audio/bell.py  ->  assets/audio/sfx/bell-01..06.wav (48 kHz 16-bit mono, 6 s each)
# The Hamlet's one small church bell, tuned to D: strike tone (prime) D5, hum D4 an octave below, minor-third
# tierce (fits D Dorian), quint, nominal D6 and a few upper partials. Each partial is a slightly split doublet, so
# it warbles slowly the way a real, slightly uneven bell does. One clapper strike: a short bright tick and a dull
# thunk, then long decaying partials (the hum lasts longest). Six strikes of the same bell: the clapper hits a
# little harder or softer, so the upper partials and the warble change while the pitch does not.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import audio as A, ledger, asset_path
from _synth import modal, burst, place_at, level, report, band_energy

PRIME = A.NOTE["D5"]                    # 587.3 Hz strike tone; the bell motif and drone are on D
DUR = 6.0
TARGET = -21.0                          # an event sound: clearly above the beds, still under the music
#            hum   prime  tierce quint  nominal  ...upper partials
RATIOS = [0.5, 1.0, 1.19, 1.50, 2.0, 2.52, 2.66, 3.01, 4.17]
AMPS = [0.65, 1.0, 0.55, 0.30, 0.50, 0.22, 0.16, 0.10, 0.06]
TAUS = [2.4, 1.5, 1.05, 0.8, 0.7, 0.35, 0.3, 0.22, 0.15]       # seconds (1/e)


def strike(rng, force):
    freqs, amps, taus, phases = [], [], [], []
    for k, (r, a, tau) in enumerate(zip(RATIOS, AMPS, TAUS)):
        split = rng.uniform(0.4, 1.6) * (1 if k else 0.5)           # Hz between the two halves of the doublet
        bright = force ** (k / 3)                                     # harder strikes wake the upper partials more
        for s, share in ((-0.5, 0.72), (0.5, 0.28)):               # unequal halves: a gentle warble, not a tremolo
            freqs.append(PRIME * r + s * split)
            amps.append(a * bright * share * rng.uniform(0.9, 1.1))
            taus.append(tau * rng.uniform(0.9, 1.1))
            phases.append(rng.uniform(0, 2 * np.pi))
    x = modal(freqs, amps, taus, DUR, phases)
    # the partials do not all start at full strength: a 3 ms swell keeps the attack sharp but click-free
    x *= np.minimum(np.arange(len(x)) / (0.003 * A.SR), 1)
    tick = burst(rng, 0.03, 1800, 3000, 0.004, attack=0.0003) * 0.35 * force
    thunk = burst(rng, 0.06, 250, 900, 0.012, attack=0.0005) * 0.3
    place_at(x, tick, 0.0); place_at(x, thunk, 0.0)
    return A.fade(A.lowpass(x, 3400, 4), 0.0005, 0.4)


for i, force in enumerate((1.0, 0.8, 1.15, 0.9, 1.05, 0.7), start=1):
    seed = 500 + i
    rng = np.random.default_rng(seed)
    x = strike(rng, force)
    x = level(x, TARGET + 2.5 * np.log2(force))
    out = A.write_wav(asset_path(f"audio/sfx/bell-{i:02d}.wav"), x)
    f = A.estimate_f0(x[: A.SR], 500, 700)
    report(out, f"prime {f:.1f} Hz ({1200 * np.log2(f / PRIME):+.0f} c), 300 Hz-3 kHz {band_energy(x, 300, 3000):.0%}, hum+ 280 Hz-3 kHz {band_energy(x, 280, 3000):.0%}", fmax=4000)
    ledger.add("audio", out, kind="sound", generator=__file__, verified=True, seed=seed,
               used_in=f"Buildings: Hamlet chapel bell, one strike (D5), strike force {force}")

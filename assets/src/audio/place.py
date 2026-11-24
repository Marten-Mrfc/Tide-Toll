# python3 assets/src/audio/place.py  ->  assets/audio/sfx/place-01..06.wav (48 kHz 16-bit mono)
# Placing a fence or footbridge segment (Hamlet: woven wattle on stakes): one soft, pitched wooden knock as a stake
# is tapped home, with a little rustle of the woven withies settling. The knock is a struck wooden stake (modal
# synthesis: a few inharmonic partials that die within a tenth of a second), excited by a felt-soft mallet.
# Pitched into the Hamlet's D Dorian over the D drone; the game can pick the pitch per segment:
#   01 D4, 02 E4, 03 G4, 04 A4, 05 B4, 06 D5.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import audio as A, ledger, asset_path
from _synth import modal, burst, crackles, place_at, level, report

TARGET = -22.0
PITCHES = ["D4", "E4", "G4", "A4", "B4", "D5"]
RATIOS = [1.0, 2.71, 5.15, 8.5]           # a stake/bar's free modes, slightly irregular (wood is not uniform)
AMPS = [1.0, 0.45, 0.18, 0.06]
TAUS = [0.075, 0.035, 0.018, 0.009]


def knock(rng, f0):
    dur = 0.45
    j = rng.uniform(0.98, 1.02, len(RATIOS)); j[0] = 1.0                     # keep the fundamental in tune
    x = modal(f0 * np.array(RATIOS) * j, AMPS, np.array(TAUS) * rng.uniform(0.85, 1.15),
              dur, rng.uniform(0, 2 * np.pi, len(RATIOS)))
    x *= np.minimum(np.arange(len(x)) / (0.0015 * A.SR), 1)              # felt-soft mallet contact
    place_at(x, burst(rng, 0.03, 600, 2500, 0.004, attack=0.0008) * 0.25, 0.0)   # contact noise
    rustle = crackles(rng, dur, int(rng.integers(5, 9)), start=0.015, spread=0.16, lo=1800, hi=4200,
                      length=0.004, gain=(0.05, 0.14))
    x += rustle
    return A.fade(A.lowpass(x, 4000, 4), 0.0005, 0.12)


for i, name in enumerate(PITCHES, start=1):
    seed = 600 + i
    rng = np.random.default_rng(seed)
    f0 = A.NOTE[name]
    x = level(knock(rng, f0), TARGET - 1.5 * (f0 > 400))                 # higher knocks sound louder: trim them
    out = A.write_wav(asset_path(f"audio/sfx/place-{i:02d}.wav"), x)
    f = A.estimate_f0(x, f0 * 0.9, f0 * 1.1)
    report(out, f"{name}: measured {f:.1f} Hz ({1200 * np.log2(f / f0):+.0f} c)", fmax=6000)
    ledger.add("audio", out, kind="sound", generator=__file__, verified=True, seed=seed,
               used_in=f"Tools: place fence or footbridge segment, soft wattle-stake knock pitched {name}")

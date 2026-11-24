# python3 assets/src/audio/erase.py  ->  assets/audio/sfx/erase-01..06.wav (48 kHz 16-bit mono)
# Erasing a fence or footbridge segment (the design doc's "fence or hedge removed: pull, snap, rustle"): a short
# soft brushing pull whose band rises as the stake lifts out of the soil, one small dry twig snap from the wattle,
# then a light rustle of loose withies. Soft and short, never a buzzer; unpitched. Second round (2026-11-24): about
# 0.3 s (was 0.5 s with a slow swell, long for a UI sound): a quicker pull with a fast attack, the snap at once.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from scipy import signal
from common import audio as A, ledger, asset_path
from _synth import modal, burst, crackles, place_at, level, report

TARGET = -24.0


def pull(rng, dur):
    """Noise through a band-pass whose centre glides up (lifting), with a swell-and-fall envelope."""
    n = int(dur * A.SR)
    t = np.arange(n) / A.SR
    noise = rng.standard_normal(n)
    f_lo, f_hi = rng.uniform(500, 800), rng.uniform(2200, 3200)
    out = np.zeros(n)
    hop = 480                                                    # 10 ms blocks, filter state carried over
    zi = None
    for k in range(0, n, hop):
        fc = f_lo * (f_hi / f_lo) ** (k / n)
        sos = signal.butter(2, [fc / 1.6, fc * 1.6], btype="bandpass", fs=A.SR, output="sos")
        if zi is None:
            zi = np.zeros((sos.shape[0], 2))
        out[k:k + hop], zi = signal.sosfilt(sos, noise[k:k + hop], zi=zi)
    u = np.clip(t / dur, 0, 1)
    env = np.minimum(u / 0.15, 1) * (1 - u) ** 1.5                  # fast rise (15% of the pull), then falls away
    return out * env


def erase(rng):
    dur = rng.uniform(0.28, 0.32)
    x = np.zeros(int(dur * A.SR))
    pd = rng.uniform(0.12, 0.16)
    place_at(x, pull(rng, pd) * 0.8, 0.0)
    snap_t = pd * rng.uniform(0.6, 0.8)
    snap = burst(rng, 0.02, 1500, 5000, 0.0018, attack=0.0002) * 0.9
    snap = A.mix(snap, modal([rng.uniform(1500, 2100), rng.uniform(3300, 3900)], [0.35, 0.15], [0.008, 0.004], 0.03))
    place_at(x, snap * rng.uniform(0.9, 1.1), snap_t)
    place_at(x, crackles(rng, dur - snap_t, int(rng.integers(7, 11)), start=0.005, spread=0.1, lo=1800, hi=5000,
                         length=0.004, gain=(0.08, 0.25)), snap_t)
    return A.fade(A.lowpass(x, 5000, 4), 0.002, 0.05)


for i in range(1, 7):
    seed = 700 + i
    rng = np.random.default_rng(seed)
    x = level(erase(rng), TARGET + rng.uniform(-0.8, 0.8))
    out = A.write_wav(asset_path(f"audio/sfx/erase-{i:02d}.wav"), x)
    report(out, f"{len(x) / A.SR:.2f} s", fmax=8000)
    ledger.add("audio", out, kind="sound", generator=__file__, verified=True, seed=seed,
               used_in="Tools: erase fence or footbridge segment (pull, snap, rustle)")

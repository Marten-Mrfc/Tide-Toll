"""Shared helpers for the audio generators in this folder (a helper, not a generator: build-assets.sh skips `_` files).

Everything here is plain numpy/scipy synthesis on top of common.audio; no samples from anywhere.

    from _synth import *            # after the usual bootstrap; this folder is on sys.path when a generator runs
    x = level(x, -24)               # loudest 400 ms block to -24 LUFS, true peak capped at -3 dBTP
    report(out, "build/audio-previews")   # A.check + spectrogram PNG, prints a one-line summary

Periodic ("seamless by construction") tools for loops: every partial is snapped to a whole number of cycles in
the loop (`snap_freq`), noise is shaped in the FFT domain over exactly the loop length (`periodic_noise`), events
are added with their tails wrapped round the end (`add_wrapped`), and reverb is a circular convolution
(`circular_reverb`). Nothing is cross-faded, so the last sample flows straight into the first.
"""
from pathlib import Path

import numpy as np
from scipy import signal

from common import ROOT, audio as A

SR = A.SR
PREVIEWS = ROOT / "build" / "audio-previews"


# ---------------------------------------------------------------- levels and reporting
def level(x, m_lufs, tp_ceiling=-3.0):
    """Scale so the loudest 400 ms block sits at m_lufs (ungated momentary max); then pull down if the
    true peak would exceed tp_ceiling dBTP. Returns the scaled array."""
    x = np.asarray(x, float)
    g = 10 ** ((m_lufs - A.momentary_max_lufs(x)) / 20)
    x = x * g
    tp = A.true_peak_db(x)
    if tp > tp_ceiling:
        x = x * 10 ** ((tp_ceiling - tp) / 20)
    return x


def report(path, extra="", fmax=12000, loop=False):
    """Check a written file against the guide and draw its spectrogram into build/audio-previews/.
    loop=True (a loop's WAV master): a loop never starts or ends in silence, so check()'s one-shot rule
    "starts/ends away from zero" does not apply; the seam is tested by seam_report(..., check=True) instead,
    which must run first."""
    path = Path(path)
    r = A.check(path)
    if loop:
        r["problems"] = [p for p in r["problems"] if not p.startswith("starts/ends away from zero")]
    png = A.spectrogram_png(path, PREVIEWS / (path.stem + ".png"), title=path.stem, fmax=fmax)
    print(f"{path.name:32s} {r['duration_s']:6.2f} s  TP {r['true_peak_dbtp']:6.2f} dBTP  "
          f"M-max {r['momentary_max_lufs']:6.1f} LUFS  I {r['lufs_i']} LUFS  {extra}  problems={r['problems']}")
    if r["problems"]:
        raise SystemExit(f"{path.name}: {r['problems']}")
    return r, png


def band_energy(x, lo, hi):
    """Fraction of the signal's energy between lo and hi Hz."""
    x = np.asarray(x, float)
    x = x.mean(1) if x.ndim == 2 else x
    X = np.abs(np.fft.rfft(x)) ** 2
    f = np.fft.rfftfreq(len(x), 1 / SR)
    return float(X[(f >= lo) & (f < hi)].sum() / max(X.sum(), 1e-20))


# ---------------------------------------------------------------- small physical voices
def modal(freqs, amps, taus, dur, phases=None):
    """Sum of exponentially decaying sines (modal synthesis): wood, stone, membrane, bell partials."""
    t = A.t_axis(dur)
    out = np.zeros_like(t)
    for i, (f, a, tau) in enumerate(zip(freqs, amps, taus)):
        if f >= SR / 2 - 500:
            continue
        ph = 0.0 if phases is None else phases[i]
        out += a * np.sin(2 * np.pi * f * t + ph) * np.exp(-t / tau)
    return out


def burst(rng, dur, lo, hi, tau, attack=0.0005):
    """Band-limited noise burst with a short linear attack and an exponential decay."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    e = np.minimum(t / max(attack, 1e-6), 1) * np.exp(-t / tau)
    return A.bandpass(rng.standard_normal(n), lo, hi, 2) * e


def crackles(rng, dur, count, start=0.0, spread=None, lo=2000, hi=5500, length=0.003, gain=(0.2, 0.6)):
    """Sparse tiny noise grains (grass blades, twigs, wattle withies)."""
    n = int(dur * SR)
    c = np.zeros(n)
    L = int(length * SR)
    spread = dur - start - length if spread is None else spread
    for _ in range(count):
        i = int((start + rng.uniform(0, spread)) * SR)
        if i + L <= n:
            c[i:i + L] += rng.standard_normal(L) * np.hanning(L) * rng.uniform(*gain)
    return A.bandpass(c, lo, hi, 2)


def bubble(rng, f0, dur, tau, rise=0.6):
    """Minnaert bubble: a sine that rises in pitch as it decays (the 'plink' in water)."""
    t = A.t_axis(dur)
    f = f0 * (1 + rise * t / max(dur, 1e-6))
    ph = 2 * np.pi * np.cumsum(f) / SR
    e = np.minimum(t / 0.003, 1) * np.exp(-t / tau)
    return np.sin(ph) * e


def place_at(buf, x, t0):
    """Add x into buf starting at time t0 (s); anything past the end is dropped."""
    i = int(round(t0 * SR))
    if i >= len(buf):
        return buf
    m = min(len(x), len(buf) - i)
    buf[i:i + m] += x[:m]
    return buf


# ---------------------------------------------------------------- periodic (loop) tools
def snap_freq(f, n):
    """Nearest frequency with a whole number of cycles in n samples (so a sine loops without a seam)."""
    step = SR / n
    return max(1, round(f / step)) * step


def periodic_noise(n, rng, lo=None, hi=None, tilt=0.0):
    """Noise that is exactly periodic over n samples: random-phase spectrum, band-limited to [lo, hi] Hz with
    smooth 2nd-order Butterworth-like skirts, power tilted by f^-tilt. Unit RMS."""
    f = np.fft.rfftfreq(n, 1 / SR)
    mag = np.ones_like(f)
    fs = np.maximum(f, 1.0)
    if lo:
        mag *= 1 / np.sqrt(1 + (lo / fs) ** 4)
    if hi:
        mag *= 1 / np.sqrt(1 + (fs / hi) ** 4)
    mag *= fs ** (-tilt / 2)
    mag[0] = 0
    X = mag * np.exp(1j * rng.uniform(0, 2 * np.pi, len(f)))
    x = np.fft.irfft(X, n)
    return x / max(float(np.std(x)), 1e-12)


def periodic_lfo(n, rng, cycles=(1, 2, 3), depth=0.3):
    """Slow smooth gain curve around 1.0 made of sines with whole numbers of cycles over n samples."""
    t = np.arange(n) / n
    w = np.zeros(n)
    for k in cycles:
        w += rng.uniform(0.5, 1.0) / k * np.sin(2 * np.pi * k * t + rng.uniform(0, 2 * np.pi))
    w /= max(float(np.max(np.abs(w))), 1e-9)
    return 1 + depth * w


def add_wrapped(buf, x, t0):
    """Add x into a loop buffer starting at t0 seconds; the part past the end wraps round to the start."""
    n = len(buf)
    i = int(round(t0 * SR)) % n
    x = np.asarray(x, float)
    while len(x):
        m = min(len(x), n - i)
        buf[i:i + m] += x[:m]
        x = x[m:]
        i = 0
    return buf


def reverb_ir(rng, rt60=1.8, dur=2.5, predelay=0.012, damp_hz=(5000, 1200), early=6):
    """Stereo room impulse response: a few early reflections, then decaying noise that darkens over time
    (cut-off glides from damp_hz[0] to damp_hz[1]). Returns (n, 2), first sample is silent (wet only)."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    ir = np.zeros((n, 2))
    for ch in range(2):
        noise = rng.standard_normal(n)
        bright = A.lowpass(noise, damp_hz[0], 2)
        dark = A.lowpass(noise, damp_hz[1], 2)
        mixw = np.clip(t / (dur * 0.6), 0, 1)
        tail = (bright * (1 - mixw) + dark * mixw) * np.exp(-6.91 * t / rt60)
        tail *= np.clip((t - predelay) / 0.03, 0, 1)
        for _ in range(early):
            k = int((predelay + rng.uniform(0.002, 0.045)) * SR)
            ir[k, ch] += rng.uniform(0.3, 0.7) * rng.choice([-1, 1])
        ir[:, ch] += tail * 0.25
    return ir / np.sqrt(np.sum(ir ** 2, axis=0, keepdims=True))


def circular_reverb(x, ir):
    """Convolve a loop (n, 2) with ir (m, 2) circularly: the tail of the end lands on the start, so the
    reverberated loop is still seamless. ir must be shorter than the loop."""
    n = len(x)
    out = np.zeros_like(x)
    for ch in range(2):
        h = np.zeros(n)
        h[:len(ir)] = ir[:, ch]
        out[:, ch] = np.fft.irfft(np.fft.rfft(x[:, ch]) * np.fft.rfft(h), n)
    return out


def seam_report(x, name, keep=0.75, check=False):
    """Join the end of a loop to its start and measure the step at the join against the signal's own
    sample-to-sample steps; also draw the joined clip (end | start) as a spectrogram for a visual check.
    Returns (step at join, 99.9th percentile of ordinary steps)."""
    x = np.asarray(x, float)
    mono = x.mean(1) if x.ndim == 2 else x
    d = np.abs(np.diff(mono))
    join = abs(mono[0] - mono[-1])
    k = int(keep * SR)
    joined = np.concatenate([x[-k:], x[:k]])
    A.spectrogram_png(joined, PREVIEWS / f"{name}-seam.png", title=f"{name} seam")
    # second-difference (curvature) test: a click shows as a spike in the 2nd difference at the join
    d2 = np.abs(np.diff(np.concatenate([mono[-3:], mono[:3]]), 2))
    d2_all = np.percentile(np.abs(np.diff(mono, 2)), 99.9)
    p = float(np.percentile(d, 99.9))
    print(f"  seam {name}: step at join {join:.5f} vs 99.9% of steps {p:.5f}; "
          f"max 2nd diff at join {d2.max():.5f} vs 99.9% {d2_all:.5f}")
    if check and (join > p or d2.max() > d2_all):
        raise SystemExit(f"{name}: the join is rougher than the loop's own signal (click at the seam)")
    return join, p, float(d2.max()), float(d2_all)

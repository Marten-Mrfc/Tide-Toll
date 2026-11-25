"""Voices for the River set's sounds (a helper, not a generator: build-assets.sh skips `_` files).

All plain numpy/scipy synthesis on top of common.audio and _synth; no samples from anywhere.

    creak(rng, dur, rate=(60, 140), body=(320, 640, 1150), q=0.035)   stick-slip timber or rope creak
    ratchet(rng, dur, rate)        pawl clicking over the winch's gear teeth, `rate` clicks per second
    chain(rng, dur, density)       loose chain links clinking (high, short metallic pings)
    thud(rng, f0=95)               heavy timber landing on its sill
    coin(rng, f0)                  one small coin hitting others
    hand_bell(rng, note, force)    a small hand bell (hum `note`), short ring
    horn(rng, note, dur)           a cow-horn call, pitch settling onto `note`
"""
import numpy as np

from common import audio as A
from _synth import modal, burst, bubble, place_at

SR = A.SR


def creak(rng, dur, rate=(60.0, 140.0), body=(320.0, 640.0, 1150.0), tau=0.012, jitter=0.25, swell=0.35):
    """Stick-slip creak: an impulse train whose rate glides between rate[0] and rate[1] (random walk) with
    jittered spacing, each impulse ringing a few wooden body modes. Envelope swells in and out."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    # slowly wandering rate between the two bounds
    walk = np.cumsum(rng.standard_normal(n // 480 + 2)) * 0.25
    walk = (walk - walk.min()) / max(np.ptp(walk), 1e-9)
    r = rate[0] + (rate[1] - rate[0]) * np.interp(np.arange(n) / 480, np.arange(len(walk)), walk)
    imp = np.zeros(n)
    pos = 0.0
    while True:
        i = int(pos)
        if i >= n:
            break
        imp[i] += rng.uniform(0.5, 1.0)
        pos += SR / r[i] * (1 + jitter * rng.uniform(-1, 1))
    ir_len = int(0.06 * SR)
    freqs = np.array(body) * rng.uniform(0.94, 1.06, len(body))
    amps = [1.0, 0.6, 0.35, 0.2][:len(body)]
    ir = modal(freqs, amps, [tau * rng.uniform(0.8, 1.2) for _ in body], ir_len / SR, rng.uniform(0, 6.28, len(body)))
    x = np.convolve(imp, ir)[:n]
    env = np.clip(np.minimum(t / (swell * dur), (dur - t) / (swell * dur)), 0, 1) ** 0.7
    return x * env


def ratchet(rng, dur, rate=9.0, start=0.0, accel=0.0):
    """Pawl clicks: a dry tick plus two metallic partials, `rate` per second (accel per second added)."""
    n = int(dur * SR)
    x = np.zeros(n)
    tt = start
    while tt < dur - 0.05:
        c = burst(rng, 0.02, 1500, 4200, 0.003, attack=0.0002) * rng.uniform(0.6, 0.9)
        ping = modal(np.array([2450.0, 3720.0]) * rng.uniform(0.97, 1.03, 2), [0.3, 0.18], [0.025, 0.015], 0.06)
        place_at(x, c, tt)
        place_at(x, ping * np.minimum(np.arange(len(ping)) / (0.0005 * SR), 1), tt)
        place_at(x, burst(rng, 0.04, 200, 700, 0.01) * 0.35, tt)                # the drum's body knock
        tt += 1.0 / (rate + accel * tt) * rng.uniform(0.96, 1.04)
    return x


def chain(rng, dur, density=30.0, lo=2200.0, hi=5200.0):
    """Loose chain links: random short metallic pings (two partials each) at `density` per second."""
    n = int(dur * SR)
    x = np.zeros(n)
    for _ in range(int(density * dur)):
        f = rng.uniform(lo, hi)
        p = modal([f, f * rng.uniform(1.35, 1.6)], [1.0, 0.5], [0.012, 0.008], 0.04, rng.uniform(0, 6.28, 2))
        p *= np.minimum(np.arange(len(p)) / (0.0004 * SR), 1) * rng.uniform(0.1, 0.35)
        place_at(x, p, rng.uniform(0, dur - 0.04))
    return x


def thud(rng, f0=95.0):
    """Heavy timber landing: low body modes, a short knock of noise and a woody upper mode."""
    dur = 0.6
    x = modal(np.array([f0, f0 * 2.3, f0 * 4.1, f0 * 7.2]) * rng.uniform(0.97, 1.03, 4), [1.0, 0.55, 0.25, 0.12],
              [0.16, 0.08, 0.04, 0.02], dur, rng.uniform(0, 6.28, 4))
    x *= np.minimum(np.arange(len(x)) / (0.002 * SR), 1)
    place_at(x, burst(rng, 0.12, 120, 1400, 0.025, attack=0.001) * 0.6, 0.0)
    return x


def coin(rng, f0):
    """One small coin: inharmonic plate modes (thin disc ratios), very short contact click."""
    ratios = np.array([1.0, 1.52, 2.17, 2.68, 3.4])
    x = modal(f0 * ratios * rng.uniform(0.98, 1.02, 5), [1.0, 0.7, 0.45, 0.3, 0.15],
              np.array([0.09, 0.07, 0.05, 0.035, 0.02]) * rng.uniform(0.7, 1.3), 0.35, rng.uniform(0, 6.28, 5))
    x *= np.minimum(np.arange(len(x)) / (0.0003 * SR), 1)
    place_at(x, burst(rng, 0.01, 2500, 6000, 0.0015, attack=0.0001) * 0.4, 0.0)
    return x


# small hand bell: same partial ratios as the chapel bell, much shorter, no hum doublets
HB_RATIOS = [0.5, 1.0, 1.19, 1.5, 2.0, 2.52, 3.01]
HB_AMPS = [0.35, 1.0, 0.5, 0.25, 0.4, 0.18, 0.08]
HB_TAUS = [0.9, 0.55, 0.4, 0.3, 0.25, 0.14, 0.09]


def hand_bell(rng, note, force=1.0, dur=1.4):
    prime = 2 * A.NOTE[note]
    fr = [prime * r * rng.uniform(0.997, 1.003) for r in HB_RATIOS]
    am = [a * force ** (k / 3) for k, a in enumerate(HB_AMPS)]
    x = modal(fr, am, [tau * rng.uniform(0.9, 1.1) for tau in HB_TAUS], dur, rng.uniform(0, 6.28, len(fr)))
    x *= np.minimum(np.arange(len(x)) / (0.002 * SR), 1)
    place_at(x, burst(rng, 0.02, 1800, 3500, 0.003, attack=0.0003) * 0.3 * force, 0.0)
    return A.lowpass(x, 6000, 4)


def horn(rng, note, dur=0.9):
    """Cow-horn call: a few harmonics through a horn-ish formant, pitch rising a little into the note, breathy
    attack, soft vibrato, a slight fall at the end."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    f0 = A.NOTE[note]
    bend = 1 - 0.06 * np.exp(-t / 0.05) - 0.03 * np.clip((t - (dur - 0.15)) / 0.15, 0, 1)
    vib = 1 + 0.006 * np.sin(2 * np.pi * 5.2 * t) * np.clip((t - 0.2) / 0.2, 0, 1)
    ph = 2 * np.pi * np.cumsum(f0 * bend * vib) / SR
    x = sum((0.8 ** k) * np.sin((k + 1) * ph) for k in range(8))
    x = A.bandpass(x, f0 * 0.8, 1800, 2) + 0.3 * A.lowpass(x, 1200, 2)
    breath = A.bandpass(rng.standard_normal(n), 800, 3000, 2) * 0.05 * np.exp(-t / 0.1)
    env = np.clip(t / 0.06, 0, 1) * np.clip((dur - t) / 0.12, 0, 1)
    return (x + breath) * env


def splash(rng, size=1.0):
    """A small dip: slap, slosh and a few bubbles (an oar blade entering the water)."""
    dur = 0.55
    x = np.zeros(int(dur * SR))
    place_at(x, burst(rng, 0.09, 400, 3000, 0.022 * size, attack=0.004) * size, 0.0)
    m = int(0.4 * SR)
    tt = np.arange(m) / SR
    slosh = A.bandpass(rng.standard_normal(m), 200, 1600, 2) * np.minimum(tt / 0.015, 1) * np.exp(-tt / (0.09 * size))
    place_at(x, slosh * 0.6, 0.01)
    for _ in range(int(rng.integers(3, 7))):
        place_at(x, bubble(rng, rng.uniform(500, 1500), 0.05, rng.uniform(0.008, 0.016), rng.uniform(0.3, 0.9))
                 * rng.uniform(0.06, 0.18), rng.uniform(0.02, 0.3))
    return A.lowpass(x, 4500, 4)

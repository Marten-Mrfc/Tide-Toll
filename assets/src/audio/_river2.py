"""Voices for the second age's (The River) sounds (a helper, not a generator: build-assets.sh skips `_` files).

Same world as _town and _river: small, dry, wood, rope, water and metal; plain numpy/scipy synthesis, no samples.
Pitched things sit in D Dorian. Every voice takes an rng so each variant is seeded and the render is deterministic.

    hull_bump(rng, f0)          a boat's hull nudging the quay timbers: low hollow knock with a slosh of water
    rope_throw(rng)             a mooring line thrown: whirr through the air, slap and coil landing on planks
    water_push(rng, dur)        a pole or oar pushing off: a slow low surge of water with a few bubbles
    sack_drop(rng, f0, weight)  a sack or bale dropped on planks: soft heavy body, cloth puff, plank knock, grain
    clapper(rng, f0)            a low wooden clapper: a knock and the loose leaf's quick second contact
    shuttle(rng)                a loom shuttle thrown through the shed: short swish ending in a wooden box clack
    beater(rng, f0)             the loom's beater bar against the cloth: a dull wooden thump with a reed rattle
    bleat(rng, f0, dur)         one soft sheep bleat: voiced source through 'a-e' formants, light natural tremor
    rope_strain(rng, dur)       a hemp rope under load: fine fibre creak, higher and tighter than a timber creak
    coin_in_box(rng, f0)        one coin dropped into a small wooden box: coin ring over a hollow box knock
"""
import numpy as np

from common import audio as A
from _synth import modal, burst, bubble, crackles, place_at
from _river import creak, thud, coin, splash
from _town import wood_knock, swish, grains

SR = A.SR


def hull_bump(rng, f0=70.0):
    """Hull against the quay: a low hollow body (thud, low-passed), a dull timber knock, water slapping."""
    dur = 0.8
    x = np.zeros(int(dur * SR))
    place_at(x, A.lowpass(thud(rng, f0), 700, 4) * 0.55, 0.0)
    place_at(x, wood_knock(rng, f0 * rng.uniform(2.6, 3.0), dur=0.4, crack=0.1, soft=0.004) * 0.65, 0.004)
    m = int(0.6 * SR)
    t = np.arange(m) / SR
    slosh = A.bandpass(rng.standard_normal(m), 180, 1300, 2) * np.minimum(t / 0.04, 1) * np.exp(-t / 0.16)
    place_at(x, slosh * 0.55, 0.02)
    for _ in range(int(rng.integers(2, 5))):
        place_at(x, bubble(rng, rng.uniform(450, 1100), 0.05, rng.uniform(0.008, 0.015), rng.uniform(0.3, 0.8))
                 * rng.uniform(0.04, 0.1), rng.uniform(0.06, 0.4))
    return A.lowpass(x, 3500, 4)


def rope_throw(rng):
    """A line thrown: a soft whirr (rope through air, low band with a flutter as the coil opens), then the slap
    of the rope landing and the loose coils settling on the planks."""
    fly = rng.uniform(0.28, 0.38)
    x = np.zeros(int((fly + 0.5) * SR))
    place_at(x, swish(rng, fly, 250, 1800, peak=0.6, flutter=0.35, flutter_hz=rng.uniform(14, 20)) * 0.45, 0.0)
    place_at(x, burst(rng, 0.08, 300, 2200, 0.018, attack=0.0015) * 0.8, fly)          # the slap
    place_at(x, wood_knock(rng, rng.uniform(260, 320), dur=0.3, crack=0.15, soft=0.003) * 0.3, fly + 0.003)
    for k in range(int(rng.integers(2, 4))):                                            # coils dropping
        place_at(x, burst(rng, 0.05, 400, 2500, 0.01, attack=0.001) * rng.uniform(0.2, 0.4),
                 fly + 0.06 + k * rng.uniform(0.04, 0.07))
    return A.lowpass(x, 4000, 4)


def water_push(rng, dur=0.7):
    """A push off the bank: a slow surge of water (low band noise, rounded rise and fall) with a few bubbles."""
    n = int(dur * SR)
    x = A.lowpass(swish(rng, dur, 120, 1100, peak=0.3), 1400, 2) * 0.8
    t = np.arange(n) / SR
    x += A.bandpass(rng.standard_normal(n), 400, 2000, 2) * np.sin(np.pi * t / dur) ** 3 * 0.12
    for _ in range(int(rng.integers(3, 6))):
        place_at(x, bubble(rng, rng.uniform(400, 1000), 0.06, rng.uniform(0.01, 0.02), rng.uniform(0.3, 0.8))
                 * rng.uniform(0.04, 0.1), rng.uniform(0.1, dur - 0.1))
    return A.lowpass(x, 3000, 4)


def sack_drop(rng, f0=95.0, weight=1.0):
    """A sack or bale on planks: soft heavy body (no hard edge), a cloth puff, the plank's short knock, grain shift."""
    dur = 0.6
    x = np.zeros(int(dur * SR))
    body = A.lowpass(thud(rng, f0), 500, 4)
    body *= np.minimum(np.arange(len(body)) / (0.006 * SR), 1)            # soft: the sack gives
    place_at(x, body * 0.55 * weight, 0.0)
    place_at(x, burst(rng, 0.15, 250, 1600, 0.035, attack=0.006) * 0.6, 0.0)   # cloth puff
    place_at(x, wood_knock(rng, rng.uniform(200, 260), dur=0.3, crack=0.12, soft=0.004) * 0.6, 0.002)
    x += grains(rng, dur, 260, lo=700, hi=3500, length=(0.002, 0.006), gain=(0.02, 0.08),
                shape=lambda u: np.exp(-u * 6))                            # grain inside settling
    return A.lowpass(x, 3000, 4)


def clapper(rng, f0=A.NOTE["D3"], force=1.0):
    """A low wooden clapper: a felt-less wooden knock and, 18-28 ms later, the loose leaf's second smaller contact."""
    x = wood_knock(rng, f0, dur=0.7, crack=0.45 * force, soft=0.002)
    place_at(x, wood_knock(rng, f0 * 1.01, dur=0.4, crack=0.15, soft=0.002) * 0.35, rng.uniform(0.018, 0.028))
    place_at(x, A.lowpass(thud(rng, f0 * 0.5), 400, 4)[:len(x)] * 0.12, 0.0)  # the post it hangs on
    return A.lowpass(x, 3000, 4) * force


def shuttle(rng):
    """The shuttle through the shed: a short rising swish of thread and wood, ending in a dry clack in the box."""
    d = rng.uniform(0.13, 0.17)
    x = np.zeros(int((d + 0.25) * SR))
    place_at(x, swish(rng, d, 1200, 5000, peak=0.8) * 0.25, 0.0)
    place_at(x, wood_knock(rng, rng.uniform(520, 600), dur=0.25, crack=0.6, soft=0.0008) * 0.7, d)
    return A.lowpass(x, 6000, 4)


def beater(rng, f0=150.0):
    """The beater bar against the fell: dull low wooden thump, the reed's short rattle of many thin slats."""
    x = wood_knock(rng, f0, dur=0.45, crack=0.1, soft=0.003)
    place_at(x, A.lowpass(thud(rng, f0 * 0.7), 600, 4)[:len(x)] * 0.5, 0.0)
    x += crackles(rng, 0.45, 14, start=0.0, spread=0.03, lo=1500, hi=4500, length=0.002, gain=(0.05, 0.15))
    return A.lowpass(x, 4000, 4)


def bleat(rng, f0=260.0, dur=0.6):
    """A soft ewe's bleat: a breathy voiced source (harmonics with a gentle falling pitch and a light tremor near
    7-9 Hz, not the cartoon 'baa' warble), through formants moving from 'a' to 'e', soft attack and fall."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    glide = 1 + 0.05 * np.exp(-t / 0.08) - 0.06 * (t / dur) ** 1.5
    trem_hz = rng.uniform(7, 9)
    jitter = np.interp(t, np.linspace(0, dur, 24), rng.uniform(-0.006, 0.006, 24))
    f = f0 * glide * (1 + 0.012 * np.sin(2 * np.pi * trem_hz * t) + jitter)
    ph = 2 * np.pi * np.cumsum(f) / SR
    src = sum((0.75 ** k) * np.sin((k + 1) * ph) for k in range(14))
    src += A.bandpass(rng.standard_normal(n), 600, 4000, 2) * 0.25          # breath
    u = t / dur
    out = np.zeros(n)
    for (fa, fb, bw, g) in ((750, 550, 160, 1.0), (1250, 1750, 220, 0.6), (2600, 2500, 320, 0.25)):
        # formant glide approximated by two fixed bands crossfaded
        a = A.bandpass(src, fa - bw / 2, fa + bw / 2, 2)
        b = A.bandpass(src, fb - bw / 2, fb + bw / 2, 2)
        out += g * (a * (1 - u) + b * u)
    out += A.lowpass(src, 500, 2) * 0.15
    env = np.minimum(t / 0.05, 1) ** 1.5 * np.clip((dur - t) / (0.35 * dur), 0, 1) ** 1.3
    env *= 1 + 0.15 * np.sin(2 * np.pi * trem_hz * t + 1.0)
    return A.lowpass(out * env, 3500, 4)


def rope_strain(rng, dur=0.8):
    """Hemp under load: a tight, fine creak (fast stick-slip, small bright body) that swells with the load."""
    x = creak(rng, dur, rate=(160.0, 260.0), body=(900.0, 1500.0, 2300.0), tau=0.004, jitter=0.35, swell=0.4)
    x += crackles(rng, dur, int(dur * 25), lo=2500, hi=6000, length=0.0015, gain=(0.05, 0.15))
    return A.lowpass(x, 5000, 4)


def coin_in_box(rng, f0):
    """One coin into a small wooden box: the coin's ring, a little damped, over a hollow box knock."""
    c = A.lowpass(coin(rng, f0), 7000, 4)
    c *= np.exp(-np.arange(len(c)) / (0.12 * SR))                         # resting on wood damps it
    place_at(c, wood_knock(rng, rng.uniform(330, 400), dur=0.3, crack=0.2, soft=0.001) * 0.45, 0.0)
    return c


__all__ = ["hull_bump", "rope_throw", "water_push", "sack_drop", "clapper", "shuttle", "beater", "bleat",
           "rope_strain", "coin_in_box", "creak", "splash"]

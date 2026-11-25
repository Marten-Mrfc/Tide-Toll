"""Voices for the incremental town's sounds (a helper, not a generator: build-assets.sh skips `_` files).

Same world as the Hamlet and River sets: small, dry, wooden and metal, plain numpy/scipy synthesis on top of
common.audio, _synth and _river. No samples from anywhere. Pitched things sit in the music's D Dorian
(D E F G A B C). Every voice takes an rng so each variant is seeded and the render is deterministic.

    quill(rng, dur)            quill nib scratching a ledger line (short strokes of fine, high grain)
    paper_tick(rng, soft)      a card or paper tab pressed: a tiny dry snap in the UI lane (2-4 kHz)
    wood_knock(rng, f0, ...)   a felt-mallet knock on a wooden bar or stake, pitched (place.py's voice, re-made)
    chime(rng, note, ...)      a small struck chime bar (free-free bar modes), hum `note`
    small_bell(rng, note, ...) a small hand bell (River's hand_bell, with an extra force/length control)
    church_bell(rng, note, ...) the chapel bell strike (bell.py's partials, re-made here so bell.py is untouched)
    swish(rng, dur, lo, hi)    paper or ribbon moving through air (noise band with a rise-and-fall envelope)
    grains(rng, dur, rate, ...) many tiny gravel grains (short band noise ticks)
    stone(rng, f0)             one stone set on others: short inharmonic clack with a low knock
    hammer(rng, f0)            a hammer blow on a nail into timber: metal ping over a wooden body
    saw(rng, dur, strokes)     a short hand saw: tooth buzz through a bright band, push/pull strokes
    seal(rng)                  hot wax pressed under a brass seal: soft squash and a small dull thump
    cover_thump(rng, f0)       a book's board cover landing: low soft thump with a puff of air
"""
import numpy as np

from common import audio as A
from _synth import modal, burst, crackles, place_at
from _river import hand_bell, creak, thud, coin

SR = A.SR


def env_ar(n, attack, tau):
    t = np.arange(n) / SR
    return np.minimum(t / max(attack, 1e-6), 1) * np.exp(-t / tau)


def quill(rng, dur=0.32, strokes=3):
    """A few short pen strokes: high, fine-grained scratch (2.5-7 kHz) with a rough grain inside each stroke."""
    n = int(dur * SR)
    x = np.zeros(n)
    tt = 0.0
    for k in range(strokes):
        L = rng.uniform(0.05, 0.11)
        m = int(L * SR)
        t = np.arange(m) / SR
        env = np.sin(np.pi * t / L) ** 1.5
        grain = 1 + 0.8 * np.abs(A.lowpass(rng.standard_normal(m), 180, 2)) * 3   # the nib catching the fibres
        s = A.bandpass(rng.standard_normal(m), 2500 * rng.uniform(0.9, 1.1), 7000, 2) * env * grain
        place_at(x, s * rng.uniform(0.5, 0.9), tt)
        place_at(x, crackles(rng, L, int(rng.integers(6, 12)), lo=3000, hi=7500, length=0.0015,
                             gain=(0.2, 0.5)), tt)
        tt += L + rng.uniform(0.01, 0.04)
        if tt > dur - 0.06:
            break
    return A.lowpass(x, 8000, 4)


def paper_tick(rng, soft=False):
    """A dry paper snap: a 2-5 kHz burst of a few ms and a faint card mode near 3 kHz."""
    dur = 0.09
    x = burst(rng, dur, 1500 if soft else 1800, 4500 if soft else 5500, 0.005 if soft else 0.004,
              attack=0.0012 if soft else 0.0006)
    f = rng.uniform(2600, 3300)
    card = modal([f, f * 1.37], [0.35, 0.15], [0.010, 0.006], dur, rng.uniform(0, 6.28, 2))
    card *= np.minimum(np.arange(len(card)) / (0.0008 * SR), 1)
    x = x + card * (0.5 if soft else 1.0)
    place_at(x, burst(rng, 0.03, 300, 1200, 0.006, attack=0.001) * (0.15 if soft else 0.3), 0.0)  # the finger
    return A.lowpass(x, 3500 if soft else 6000, 4)


def wood_knock(rng, f0, dur=0.45, crack=0.6, soft=0.0015):
    """Felt mallet on a wooden bar (place.py's voice): inharmonic bar modes that die within a tenth of a second."""
    ratios = np.array([1.0, 2.71, 5.15, 8.5]) * np.r_[1.0, rng.uniform(0.98, 1.02, 3)]
    x = modal(f0 * ratios, [1.0, 0.45, 0.18, 0.06], np.array([0.075, 0.035, 0.018, 0.009]) * rng.uniform(0.85, 1.15),
              dur, rng.uniform(0, 2 * np.pi, 4))
    x *= np.minimum(np.arange(len(x)) / (soft * SR), 1)
    place_at(x, burst(rng, 0.03, 600, 2500, 0.004, attack=0.0008) * 0.25, 0.0)
    if crack:
        c = modal(np.array([2300.0, 2900.0, 3500.0]) * rng.uniform(0.97, 1.03, 3), [0.55, 0.45, 0.3],
                  [0.03, 0.022, 0.015], dur, rng.uniform(0, 2 * np.pi, 3))
        x += c * np.minimum(np.arange(len(c)) / (0.0008 * SR), 1) * crack
    return A.lowpass(x, 4000, 4)


CHIME_RATIOS = [1.0, 2.756, 5.404]           # free-free bar: a small brass chime bar
def chime(rng, note, dur=1.6, force=1.0, tau=0.9):
    f0 = A.NOTE[note]
    fr = [f0 * r * rng.uniform(0.998, 1.002) for r in CHIME_RATIOS] + [f0 * 2.0 * 1.001]   # touch of octave
    am = [1.0, 0.35 * force, 0.12 * force ** 1.5, 0.12]
    ta = [tau, tau * 0.35, tau * 0.12, tau * 0.5]
    x = modal(fr, am, [t * rng.uniform(0.9, 1.1) for t in ta], dur, rng.uniform(0, 6.28, len(fr)))
    x *= np.minimum(np.arange(len(x)) / (0.0015 * SR), 1)
    place_at(x, burst(rng, 0.015, 2000, 5000, 0.002, attack=0.0002) * 0.25 * force, 0.0)   # brass mallet tick
    return A.lowpass(x, 7000, 4)


def small_bell(rng, note, force=1.0, dur=1.4):
    return hand_bell(rng, note, force=force, dur=dur)


BELL_RATIOS = [0.5, 1.0, 1.19, 1.50, 2.0, 2.52, 2.66, 3.01, 4.17]
BELL_AMPS = [0.65, 1.0, 0.55, 0.30, 0.50, 0.22, 0.16, 0.10, 0.06]
BELL_TAUS = [2.4, 1.5, 1.05, 0.8, 0.7, 0.35, 0.3, 0.22, 0.15]


def church_bell(rng, hum_note, dur=4.0, force=1.0, damp=1.0):
    """The chapel bell (bell.py's partial set, doublets and clapper ring), re-made so bell.py stays as it is."""
    prime = 2 * A.NOTE[hum_note]
    fr, am, ta, ph = [], [], [], []
    for k, (r, a, tau) in enumerate(zip(BELL_RATIOS, BELL_AMPS, BELL_TAUS)):
        split = rng.uniform(0.4, 1.6) * (1 if k else 0.5)
        for s, share in ((-0.5, 0.72), (0.5, 0.28)):
            fr.append(prime * r + s * split); am.append(a * force ** (k / 3) * share * rng.uniform(0.9, 1.1))
            ta.append(tau * damp * rng.uniform(0.9, 1.1)); ph.append(rng.uniform(0, 2 * np.pi))
    for f, tau in zip([2150.0, 2480.0, 2790.0], [0.18, 0.12, 0.09]):
        fr.append(f * rng.uniform(0.985, 1.015)); am.append(0.5 * force); ta.append(tau); ph.append(rng.uniform(0, 6.28))
    x = modal(fr, am, ta, dur, ph)
    x *= np.minimum(np.arange(len(x)) / (0.003 * SR), 1)
    place_at(x, burst(rng, 0.03, 1800, 3000, 0.004, attack=0.0003) * 0.35 * force, 0.0)
    place_at(x, burst(rng, 0.06, 250, 900, 0.012, attack=0.0005) * 0.3, 0.0)
    return A.lowpass(x, 5200, 4)


def swish(rng, dur, lo=900, hi=6000, peak=0.5, flutter=0.0, flutter_hz=35.0):
    """Paper or ribbon through the air: band noise with a smooth rise and fall (peak = where the top sits, 0-1).
    flutter adds an amplitude wobble (a ribbon's edge beating)."""
    n = int(dur * SR)
    t = np.arange(n) / n
    env = np.where(t < peak, (t / peak) ** 2, ((1 - t) / (1 - peak)) ** 1.6)
    if flutter:
        env = env * (1 + flutter * np.sin(2 * np.pi * flutter_hz * dur * t + rng.uniform(0, 6.28)))
    return A.bandpass(rng.standard_normal(n), lo, hi, 2) * env


def grains(rng, dur, rate, lo=500, hi=4000, length=(0.002, 0.006), gain=(0.2, 1.0), shape=None):
    """Many tiny stone grains: short noise ticks at `rate` per second, density following `shape(t/dur)`."""
    n = int(dur * SR)
    x = np.zeros(n)
    count = int(rate * dur)
    for _ in range(count):
        u = rng.uniform(0, 1)
        if shape is not None and rng.uniform(0, 1) > shape(u):
            continue
        L = int(rng.uniform(*length) * SR)
        i = int(u * (n - L))
        x[i:i + L] += rng.standard_normal(L) * np.hanning(L) * rng.uniform(*gain)
    return A.bandpass(x, lo, hi, 2)


def stone(rng, f0=1100.0):
    """A stone sett set down on others: short inharmonic clack, a low knock of the bed, a little grit."""
    dur = 0.3
    r = np.array([1.0, 1.63, 2.41, 3.37]) * rng.uniform(0.95, 1.05, 4)
    x = modal(f0 * r, [1.0, 0.6, 0.35, 0.2], np.array([0.022, 0.015, 0.01, 0.007]) * rng.uniform(0.8, 1.2), dur,
              rng.uniform(0, 6.28, 4))
    x *= np.minimum(np.arange(len(x)) / (0.0005 * SR), 1)
    place_at(x, burst(rng, 0.08, 120, 600, 0.02, attack=0.001) * 0.7, 0.0)           # the sand bed takes it
    x += grains(rng, dur, 120, lo=1500, hi=5000, gain=(0.03, 0.1), shape=lambda u: np.exp(-u * 8))
    return A.lowpass(x, 6000, 4)


def hammer(rng, f0=180.0, force=1.0):
    """Hammer on a nail into timber: wooden body (pitched low), a short metal ping of the nail head, a crack."""
    dur = 0.5
    x = modal(np.array([f0, f0 * 2.6, f0 * 5.1]) * rng.uniform(0.97, 1.03, 3), [1.0, 0.5, 0.2],
              [0.09, 0.04, 0.02], dur, rng.uniform(0, 6.28, 3))
    x *= np.minimum(np.arange(len(x)) / (0.0008 * SR), 1)
    fp = rng.uniform(3000, 3800)
    ping = modal([fp, fp * 1.47, fp * 2.08], [0.4, 0.25, 0.1], [0.035, 0.02, 0.012], dur, rng.uniform(0, 6.28, 3))
    x += ping * np.minimum(np.arange(len(ping)) / (0.0004 * SR), 1) * force
    place_at(x, burst(rng, 0.04, 800, 4000, 0.004, attack=0.0002) * 0.5 * force, 0.0)
    return A.lowpass(x, 7000, 4)


def saw(rng, dur=0.6, strokes=3):
    """A short hand saw: each stroke is a tooth buzz (impulses at 150-260 Hz, rate following the stroke speed)
    through a bright band and a woody body, with a push/pull envelope."""
    n = int(dur * SR)
    x = np.zeros(n)
    L = dur / strokes
    for k in range(strokes):
        m = int(L * SR * rng.uniform(0.85, 0.98))
        t = np.arange(m) / SR
        speed = np.sin(np.pi * t / (m / SR))                      # the blade speeds up and slows each stroke
        rate = 140 + 140 * speed * (1.0 if k % 2 == 0 else 0.8)
        imp = np.zeros(m)
        pos = 0.0
        while int(pos) < m:
            imp[int(pos)] = rng.uniform(0.5, 1.0)
            pos += SR / rate[int(pos)] * rng.uniform(0.9, 1.1)
        ir = burst(rng, 0.004, 2000, 6500, 0.0008, attack=0.0001)
        teeth = np.convolve(imp, ir)[:m]
        dust = A.bandpass(rng.standard_normal(m), 1500, 5000, 2) * 0.25
        body = A.bandpass(np.convolve(imp, burst(rng, 0.01, 400, 1200, 0.002))[:m], 400, 1500, 2) * 0.6
        s = (teeth + dust + body) * speed ** 1.2 * (1.0 if k % 2 == 0 else 0.75)
        place_at(x, s, k * L)
    return A.lowpass(x, 7000, 4)


def seal(rng):
    """Brass seal into warm wax: a soft squash (low band noise, slow attack) and a small dull thump of the handle."""
    dur = 0.45
    n = int(dur * SR)
    t = np.arange(n) / SR
    squash = A.bandpass(rng.standard_normal(n), 180, 1400, 2) * np.minimum(t / 0.03, 1) * np.exp(-t / 0.07) * 0.5
    x = squash + thud(rng, rng.uniform(150, 175))[:n] * 0.6
    x += crackles(rng, dur, 6, start=0.02, spread=0.12, lo=1200, hi=3500, length=0.003, gain=(0.03, 0.08))
    return A.lowpass(x, 3000, 4)


def cover_thump(rng, f0=110.0):
    """A book's board cover landing on the leaves: low soft thump, a puff of air pushed out, the boards' tap."""
    dur = 0.5
    x = thud(rng, f0)[:int(dur * SR)] * 0.8
    place_at(x, burst(rng, 0.12, 250, 1800, 0.03, attack=0.004) * 0.45, 0.0)
    place_at(x, burst(rng, 0.03, 900, 2800, 0.004, attack=0.0005) * 0.12, 0.002)
    return A.lowpass(x, 2500, 4)


__all__ = ["quill", "paper_tick", "wood_knock", "chime", "small_bell", "church_bell", "swish", "grains", "stone",
           "hammer", "saw", "seal", "cover_thump", "creak", "coin", "thud", "env_ar"]


def finish(name, i, x, target, seed, used_in, generator, fout=0.06, fmax=12000):
    """Fade, level (loudest 400 ms to `target` LUFS, true peak under -3 dBTP), write
    assets/audio/sfx/<name>-<i>.wav, check it against the guide and add its ledger row. Returns the path."""
    from common import ledger, asset_path
    from _synth import level, report
    x = level(A.fade(x, 0.0005, fout), target)
    out = A.write_wav(asset_path(f"audio/sfx/{name}-{i:02d}.wav"), x)
    report(out, "", fmax=fmax)
    ledger.add("audio", out, kind="sound", generator=generator, verified=True, seed=seed, used_in=used_in)
    return out

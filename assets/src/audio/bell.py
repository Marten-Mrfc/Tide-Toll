# python3 assets/src/audio/bell.py  ->  assets/audio/sfx/bell-01..06.wav     gameplay strikes, about 3 s
#                                     assets/audio/sfx/bell-long-d4, -e4, -g4, -a4.wav   long strikes, 6 s
# (48 kHz 16-bit mono). The Hamlet's small chapel bell. Second round (2026-11-24): the bell is tuned per strike to
# the four notes of the bell motif, D4, E4, G4 and A4 (the hum, the note the ear holds after the strike; the
# strike tone/prime is an octave above), so the six gameplay strikes are not near-duplicates:
#   bell-01 D4, 02 E4, 03 G4, 04 A4, 05 D4 (soft), 06 A4 (hard)
# and each strike has bright, short strike partials between 2 and 3 kHz (the clapper's ring), so the sound is not
# dull after the first 50 ms. Each partial is a slightly split doublet, so it warbles slowly the way a real, slightly
# uneven bell does. The gameplay strikes are damped to about 3 s; the long 6 s strikes are kept as named variants
# for moments that want the full ring (a wedding, the end of a season).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import audio as A, ledger, asset_path
from _synth import modal, burst, place_at, level, report, band_energy

DUR = 6.0
TARGET = -21.0                          # an event sound: clearly above the beds, still under the music
#            hum   prime  tierce quint  nominal  ...upper partials
RATIOS = [0.5, 1.0, 1.19, 1.50, 2.0, 2.52, 2.66, 3.01, 4.17]
AMPS = [0.65, 1.0, 0.55, 0.30, 0.50, 0.22, 0.16, 0.10, 0.06]
TAUS = [2.4, 1.5, 1.05, 0.8, 0.7, 0.35, 0.3, 0.22, 0.15]       # seconds (1/e)


# bright strike partials (Hz, fixed in the 2-3 kHz band whatever the note, as the clapper rings the lip)
STRIKE_HZ = [2150.0, 2480.0, 2790.0]
STRIKE_TAU = [0.18, 0.12, 0.09]


def strike(rng, force, hum_note, dur=DUR, damp=1.0):
    PRIME = 2 * A.NOTE[hum_note]
    freqs, amps, taus, phases = [], [], [], []
    for k, (r, a, tau) in enumerate(zip(RATIOS, AMPS, TAUS)):
        split = rng.uniform(0.4, 1.6) * (1 if k else 0.5)           # Hz between the two halves of the doublet
        bright = force ** (k / 3)                                     # harder strikes wake the upper partials more
        for s, share in ((-0.5, 0.72), (0.5, 0.28)):               # unequal halves: a gentle warble, not a tremolo
            freqs.append(PRIME * r + s * split)
            amps.append(a * bright * share * rng.uniform(0.9, 1.1))
            taus.append(tau * damp * rng.uniform(0.9, 1.1))
            phases.append(rng.uniform(0, 2 * np.pi))
    for f, tau in zip(STRIKE_HZ, STRIKE_TAU):                     # clapper ring, 2-3 kHz
        freqs.append(f * rng.uniform(0.985, 1.015))
        amps.append(0.5 * force)
        taus.append(tau)
        phases.append(rng.uniform(0, 2 * np.pi))
    x = modal(freqs, amps, taus, dur, phases)
    # the partials do not all start at full strength: a 3 ms swell keeps the attack sharp but click-free
    x *= np.minimum(np.arange(len(x)) / (0.003 * A.SR), 1)
    tick = burst(rng, 0.03, 1800, 3000, 0.004, attack=0.0003) * 0.35 * force
    thunk = burst(rng, 0.06, 250, 900, 0.012, attack=0.0005) * 0.3
    place_at(x, tick, 0.0); place_at(x, thunk, 0.0)
    return A.fade(A.lowpass(x, 5200, 4), 0.0005, 0.4 if dur > 4 else 0.6)


def late_bright(x):
    """Share of the energy above 1.5 kHz after the first 50 ms (the review found almost none)."""
    return band_energy(x[int(0.05 * A.SR):], 1500, 12000)


takes = {}
GAME = [("D4", 1.0), ("E4", 0.9), ("G4", 1.05), ("A4", 0.95), ("D4", 0.75), ("A4", 1.15)]
for i, (note, force) in enumerate(GAME, start=1):
    seed = 500 + i
    rng = np.random.default_rng(seed)
    x = strike(rng, force, note, dur=3.0, damp=0.55)
    x = level(x, TARGET + 2.5 * np.log2(force))
    out = A.write_wav(asset_path(f"audio/sfx/bell-{i:02d}.wav"), x)
    takes[out.name] = x
    f = A.estimate_f0(x[: A.SR], A.NOTE[note] * 0.9, A.NOTE[note] * 1.1)
    report(out, f"hum {note} measured {f:.1f} Hz ({1200 * np.log2(f / A.NOTE[note]):+.0f} c), "
                f"2-3 kHz {band_energy(x, 2000, 3000):.1%}, >1.5 kHz after 50 ms {late_bright(x):.1%}", fmax=4000)
    ledger.add("audio", out, kind="sound", generator=__file__, verified=True, seed=seed,
               used_in=f"Buildings: Hamlet chapel bell, one gameplay strike (about 3 s), hum {note}, force {force}")
for k, note in enumerate(("D4", "E4", "G4", "A4"), start=1):
    seed = 520 + k
    x = level(strike(np.random.default_rng(seed), 1.0, note), TARGET)
    out = A.write_wav(asset_path(f"audio/sfx/bell-long-{note.lower()}.wav"), x)
    report(out, f"long strike, hum {note}, >1.5 kHz after 50 ms {late_bright(x):.1%}", fmax=4000)
    ledger.add("audio", out, kind="sound", generator=__file__, verified=True, seed=seed,
               used_in=f"Buildings: Hamlet chapel bell, long strike (6 s, full ring), hum {note}")
names = sorted(takes)
cmax = max(abs(np.corrcoef(takes[a][:A.SR], takes[b][:A.SR])[0, 1]) for i, a in enumerate(names) for b in names[i + 1:])
print(f"  highest correlation between two gameplay strikes (first second): {cmax:.2f}")

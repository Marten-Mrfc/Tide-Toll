# python3 assets/src/audio/drawbridge-winch.py  ->  assets/audio/sfx/drawbridge-raise-01..03.wav,
#   drawbridge-lower-01..03.wav (48 kHz 16-bit mono, about 2.2 s each)
# The drawbridge moving (Sound tab: "winch ratchet in time, creak, a thud on the downbeat when it lands"; Gameplay
# tab: raising 2 s, lowering 2 s). Raise: the winch pawl clicks steadily over the gear teeth (8 to 9 a second, so
# it can sit on the beat at the game's 120 bpm eighths), the timber leaf creaks under load, the chains rattle as
# they take up slack, and the pawl locks with a clunk when the leaf is up. Lower: the pawl is held off, so the drum
# freewheels faster under the brake, chains pay out, the leaf creaks, and lands on its sill with a heavy thud at
# about 2.0 s. The warning bell before a raise is the boat-call / chapel bell, not part of these files.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import audio as A, ledger, asset_path
from _synth import place_at, level, report, burst
from _river import creak, ratchet, chain, thud
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

TARGET = -22.0
DUR = 2.3


def raise_bridge(rng):
    x = np.zeros(int(DUR * A.SR))
    rate = rng.uniform(8.0, 9.0)
    place_at(x, ratchet(rng, 1.95, rate) * 0.8, 0.02)
    place_at(x, creak(rng, 1.7, rate=(45, 110), body=(260, 520, 980)) * 0.5, 0.15)
    place_at(x, creak(rng, 0.6, rate=(90, 170), body=(410, 820, 1500)) * 0.35, rng.uniform(0.6, 1.2))
    ch = chain(rng, 1.9, density=rng.uniform(12, 16)) * 0.5
    place_at(x, ch * np.linspace(1.0, 0.4, len(ch)), 0.05)
    clunk = thud(rng, f0=rng.uniform(150, 180)) * 0.6
    place_at(x, clunk, 2.0)                                                  # pawl locks at the top
    place_at(x, burst(rng, 0.03, 1500, 4000, 0.004) * 0.5, 2.0)
    return A.fade(A.lowpass(x, 7000, 4), 0.003, 0.1)


def lower_bridge(rng):
    x = np.zeros(int(DUR * A.SR))
    rate = rng.uniform(12.0, 14.0)
    rt = ratchet(rng, 1.9, rate, accel=2.0) * 0.5
    place_at(x, rt, 0.03)
    place_at(x, creak(rng, 1.7, rate=(55, 130), body=(240, 480, 900)) * 0.5, 0.1)
    ch = chain(rng, 1.95, density=rng.uniform(16, 22)) * 0.5
    place_at(x, ch * np.linspace(0.5, 1.0, len(ch)), 0.02)
    place_at(x, thud(rng, f0=rng.uniform(85, 105)) * 1.0, 1.98)              # the leaf lands on its sill
    place_at(x, burst(rng, 0.25, 150, 2000, 0.05, attack=0.002) * 0.35, 1.98)
    return A.fade(A.lowpass(x, 7000, 4), 0.003, 0.08)


for kind, fn, base in (("raise", raise_bridge, 700), ("lower", lower_bridge, 710)):
    for i in range(1, 4):
        seed = base + i
        rng = np.random.default_rng(seed)
        x = level(fn(rng), TARGET + rng.uniform(-0.7, 0.7))
        out = A.write_wav(asset_path(f"audio/sfx/drawbridge-{kind}-{i:02d}.wav"), x)
        report(out, kind)
        ledger.add("audio", out, kind="sound", generator=__file__, note=BLOCKED, verified=True, seed=seed,
                   used_in=f"River: drawbridge {kind} (winch ratchet, timber creak, chain{', landing thud' if kind == 'lower' else ', pawl clunk'}; about 2 s)")

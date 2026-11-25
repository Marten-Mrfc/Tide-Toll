# python3 assets/src/audio/boat-turn-away.py  ->  assets/audio/sfx/boat-turn-away-01..03.wav (48 kHz 16-bit mono)
# A boat giving up and turning back (Gameplay tab: each boat that turns back costs 5 river reputation). Two low,
# muffled bell strikes falling by a third, damped short as if a hand stopped the bell: a disappointed "oh-oh", not an
# alarm. Notes stay in the theme's D Dorian: G3 to E3, D4 to B3, C4 to A3. A soft hull knock sits under the first.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import audio as A, ledger, asset_path
from _synth import place_at, level, report, modal
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

TARGET = -23.0
RATIOS = [0.5, 1.0, 1.19, 1.5, 2.0, 2.52]
AMPS = [0.9, 1.0, 0.5, 0.25, 0.3, 0.08]
TAUS = [0.7, 0.45, 0.3, 0.22, 0.15, 0.08]


def muffled(rng, note, force):
    prime = 2 * A.NOTE[note]
    x = modal([prime * r for r in RATIOS], [a * force ** (k / 3) for k, a in enumerate(AMPS)],
              [t * rng.uniform(0.9, 1.1) for t in TAUS], 1.3, rng.uniform(0, 6.28, len(RATIOS)))
    x *= np.minimum(np.arange(len(x)) / (0.004 * A.SR), 1)          # soft (padded) striker
    return A.lowpass(x, 1800, 2)


for i, (a, b) in enumerate((("G3", "E3"), ("D4", "B3"), ("C4", "A3")), start=1):
    seed = 810 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(1.9 * A.SR))
    place_at(x, muffled(rng, a, 1.0), 0.0)
    place_at(x, muffled(rng, b, 0.85) * 0.9, rng.uniform(0.36, 0.42))
    knock = modal(np.array([140.0, 330.0]) * rng.uniform(0.95, 1.05, 2), [1.0, 0.4], [0.06, 0.03], 0.25)
    place_at(x, knock * np.minimum(np.arange(len(knock)) / (0.002 * A.SR), 1) * 0.3, 0.0)
    x = level(A.fade(x, 0.002, 0.3), TARGET + rng.uniform(-0.5, 0.5))
    out = A.write_wav(asset_path(f"audio/sfx/boat-turn-away-{i:02d}.wav"), x)
    report(out, f"{a} -> {b}", fmax=4000)
    ledger.add("audio", out, kind="sound", generator=__file__, note=BLOCKED, verified=True, seed=seed,
               used_in=f"River: boat turns back (two muffled low bell strikes, {a} falling to {b})")

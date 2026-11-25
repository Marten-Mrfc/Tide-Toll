# python3 assets/src/audio/ferry-rope.py  ->  assets/audio/sfx/ferry-rope-01..04.wav (48 kHz 16-bit mono)
# The ferry rope taking the strain (Sound tab: "Ferry: rope pull, hull knock"). A hand-over-hand pull on the
# hemp rope: a low stick-slip creak where the rope rubs the guide post (slower and lower than the drawbridge's
# timber), a soft fibrous rasp of the rope through the hands, and a small hollow knock of the raft's planks.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import audio as A, ledger, asset_path
from _synth import place_at, level, report, burst, modal
from _river import creak
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

TARGET = -24.0
for i in range(1, 5):
    seed = 770 + i
    rng = np.random.default_rng(seed)
    dur = rng.uniform(0.9, 1.2)
    x = np.zeros(int(dur * A.SR))
    place_at(x, creak(rng, dur - 0.15, rate=(28, 70), body=(210, 430, 760), tau=0.016, swell=0.4), 0.05)
    n = int((dur - 0.2) * A.SR)
    t = np.arange(n) / A.SR
    rasp = A.bandpass(rng.standard_normal(n), 900, 3500, 2) * 0.08 * np.sin(np.pi * t / t[-1]) ** 2
    place_at(x, rasp, 0.08)
    knock = modal(np.array([180.0, 410.0, 760.0]) * rng.uniform(0.95, 1.05, 3), [1.0, 0.5, 0.2], [0.05, 0.03, 0.015], 0.2)
    knock *= np.minimum(np.arange(len(knock)) / (0.001 * A.SR), 1)
    place_at(x, knock * 0.35, rng.uniform(0.3, dur - 0.3))
    x = level(A.fade(A.lowpass(x, 6000, 4), 0.003, 0.08), TARGET + rng.uniform(-0.6, 0.6))
    out = A.write_wav(asset_path(f"audio/sfx/ferry-rope-{i:02d}.wav"), x)
    report(out, "rope creak")
    ledger.add("audio", out, kind="sound", generator=__file__, note=BLOCKED, verified=True, seed=seed,
               used_in="River: ferry rope pull (rope creak on the guide, rasp, plank knock)")

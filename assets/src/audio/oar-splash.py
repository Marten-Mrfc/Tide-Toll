# python3 assets/src/audio/oar-splash.py  ->  assets/audio/sfx/oar-splash-01..06.wav (48 kHz 16-bit mono)
# Rowing (Sound tab: "Rowboat: paired oar creak and splash, on beats 1 and 3"). Each file is one stroke: the oar
# creaks in its thole pin as it is pulled (a short, high stick-slip creak), then the blade dips with a small slap,
# slosh and bubbles. 05 and 06 are bigger, wake-like washes (a barge's sweep or the wake slapping the bank).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import audio as A, ledger, asset_path
from _synth import place_at, level, report
from _river import creak, splash
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

TARGET = -24.0
for i in range(1, 7):
    seed = 790 + i
    rng = np.random.default_rng(seed)
    big = i >= 5
    x = np.zeros(int((0.95 if big else 0.75) * A.SR))
    if not big:
        place_at(x, creak(rng, 0.22, rate=(110, 220), body=(620, 1250, 2100), tau=0.008, swell=0.3) * 0.35, 0.0)
        place_at(x, splash(rng, 1.0), rng.uniform(0.16, 0.2))
    else:
        place_at(x, splash(rng, 1.6), 0.0)
        place_at(x, splash(rng, 1.2) * 0.6, rng.uniform(0.18, 0.28))
    x = level(A.fade(x, 0.002, 0.08), TARGET + rng.uniform(-0.6, 0.6) - (0 if big else 1))
    out = A.write_wav(asset_path(f"audio/sfx/oar-splash-{i:02d}.wav"), x)
    report(out, "wash" if big else "stroke")
    ledger.add("audio", out, kind="sound", generator=__file__, note=BLOCKED, verified=True, seed=seed,
               used_in="River: " + ("boat wake wash against the bank" if big else "rowboat oar stroke (thole-pin creak and splash)"))

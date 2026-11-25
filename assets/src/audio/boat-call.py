# python3 assets/src/audio/boat-call.py  ->  assets/audio/sfx/boat-call-01..06.wav (48 kHz 16-bit mono)
# A boat calling for the bridge or the ferry (Sound tab: "Boat waiting: idle bob, rope slap, impatient call or horn";
# "Ferry: a small hand bell to call the ferry"). 01 to 03: a small hand bell shaken two or three times, hum on D5,
# A4 and E5 (the theme's D Dorian); 04 to 06: a short cow-horn call on A3, D4 and G3, the Hamlet's wordless boatman's
# horn. All sit in the bells lane (300 Hz to 3 kHz) and stay polite: a call, never an alarm.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import audio as A, ledger, asset_path
from _synth import place_at, level, report
from _river import hand_bell, horn
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

TARGET = -22.0
VARIANTS = [("bell", "D5", 2), ("bell", "A4", 3), ("bell", "E5", 2), ("horn", "A3", 1), ("horn", "D4", 2),
            ("horn", "G3", 1)]
for i, (kind, note, count) in enumerate(VARIANTS, start=1):
    seed = 730 + i
    rng = np.random.default_rng(seed)
    if kind == "bell":
        x = np.zeros(int(1.9 * A.SR))
        tt = 0.0
        for k in range(count):
            place_at(x, hand_bell(rng, note, force=rng.uniform(0.8, 1.1)) * (1.0 if k == 0 else rng.uniform(0.7, 0.9)), tt)
            tt += rng.uniform(0.2, 0.26)
        x = A.fade(x, 0.001, 0.3)
    else:
        x = np.zeros(int(1.6 * A.SR))
        tt = 0.0
        for k in range(count):
            d = rng.uniform(0.75, 0.9) if count == 1 else (0.32 if k == 0 else 0.7)
            place_at(x, horn(rng, note, d) * (0.85 if k == 0 and count > 1 else 1.0), tt)
            tt += d + 0.08
        x = A.fade(x, 0.002, 0.15)
    x = level(x, TARGET + rng.uniform(-0.5, 0.5))
    out = A.write_wav(asset_path(f"audio/sfx/boat-call-{i:02d}.wav"), x)
    f = A.estimate_f0(x, A.NOTE[note] * (1.8 if kind == "bell" else 0.9), A.NOTE[note] * (2.2 if kind == "bell" else 1.1))
    report(out, f"{kind} {note}, measured {f:.0f} Hz", fmax=6000)
    ledger.add("audio", out, kind="sound", generator=__file__, note=BLOCKED, verified=True, seed=seed,
               used_in=f"River: boat calling for the bridge or ferry ({'hand bell, hum' if kind == 'bell' else 'cow horn'} {note})")

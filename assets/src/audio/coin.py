# python3 assets/src/audio/coin.py  ->  assets/audio/sfx/coin-01..06.wav (48 kHz 16-bit mono)
# Trade income (Sound tab: "Boat sale: coin clink, quantised"; "at high trade volume the clinks merge into a
# tambourine-like texture"). One to three small coins dropping onto others: thin-disc plate modes between 2 and
# 7 kHz with a tiny contact click, short (under half a second) so the game can quantise and stack them. The
# lowest partial of each coin sits in the UI lane (2 to 4 kHz).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import audio as A, ledger, asset_path
from _synth import place_at, level, report, band_energy
from _river import coin
BLOCKED = "free asset sites (kenney.nl, quaternius.com, polyhaven.com, ambientcg.com, opengameart.org, freesound.org) blocked from the build container, checked 2026-11-25, so made in code"

TARGET = -24.0
for i, count in enumerate((1, 2, 1, 3, 2, 1), start=1):
    seed = 750 + i
    rng = np.random.default_rng(seed)
    x = np.zeros(int(0.5 * A.SR))
    tt = 0.0
    for k in range(count):
        place_at(x, coin(rng, rng.uniform(2200, 3300)) * (1.0 if k == 0 else rng.uniform(0.5, 0.8)), tt)
        tt += rng.uniform(0.045, 0.085)
    x = level(A.fade(A.lowpass(x, 9000, 4), 0.0005, 0.06), TARGET + rng.uniform(-0.6, 0.6))
    out = A.write_wav(asset_path(f"audio/sfx/coin-{i:02d}.wav"), x)
    report(out, f"{count} coin(s), 2-4 kHz {band_energy(x, 2000, 4000):.0%}")
    ledger.add("audio", out, kind="sound", generator=__file__, note=BLOCKED, verified=True, seed=seed,
               used_in=f"River: trade income coin clink ({count} coin{'s' if count > 1 else ''})")

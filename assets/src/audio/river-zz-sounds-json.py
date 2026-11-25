# python3 assets/src/audio/river-zz-sounds-json.py  ->  assets/audio/sfx/river-sounds.json (for the game builder)
# The second age's (The River) sound events, in the same form as town-sounds.json (written by town-zz-sounds-json.py,
# which stays as it is): files (play round-robin, the guide's +-2 dB and +-50 cents on top where pitch_vary is
# true), loop flag, volume_db (intended loudness against the coin sound, about -24 LUFS momentary max; the files
# are rendered at it, so play them at the coin's bus gain) and measured_vs_coin_db (loudest 400 ms block, mean
# over the variants). Only the second age's new events: the river sounds made earlier (oar-splash, boat-call,
# boat-turn-away, ferry-rope, drawbridge-raise/-lower, water-ford) are not in any table yet and are left out here.
# Named river-zz-* so build-assets.sh (name order) runs it after river-sfx.py and the other generators.
import sys, pathlib, json; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import audio as A, ledger, asset_path

EVENTS = [  # event, stem, variants, volume_db vs coin, loop, pitch_vary
    ("boat-moored", "moor", 3, 1.0, False, True),
    ("boat-cast-off", "cast-off", 2, 2.0, False, False),
    ("cargo-on-quay", "quay-drop", 4, 0.0, False, True),
    ("tide-turned", "tide-turn", 2, 1.0, False, False),
    ("loom", "loom", 3, -1.0, False, True),
    ("sheep", "sheep", 3, -2.0, False, True),
    ("crane", "crane", 2, 0.0, False, True),
    ("toll-paid", "toll", 3, 1.0, False, True),
    ("seal-granted", "seal", 1, 4.5, False, False),
]

coin = np.mean([A.momentary_max_lufs(A.read_wav(asset_path(f"audio/sfx/coin-{i:02d}.wav"))[0]) for i in range(1, 7)])
out = {"about": "River (second age) sound events. Files are 48 kHz mono WAV one-shots. volume_db: intended loudness "
                "against the coin sound; the files are already rendered at it, so play them at the coin's bus gain. "
                "pitch_vary false: tuned in D Dorian, do not detune.",
       "reference": "assets/audio/sfx/coin-01.wav", "reference_loudness_lufs": round(float(coin), 1), "events": {}}
for ev, stem, n, vol, loop, vary in EVENTS:
    files = [f"assets/audio/sfx/{stem}-{i:02d}.wav" for i in range(1, n + 1)]
    meas = []
    for f in files:
        p = asset_path(f.removeprefix("assets/"))
        if not p.exists():
            raise SystemExit(f"{f} missing: run river-sfx.py first")
        meas.append(A.momentary_max_lufs(A.read_wav(p)[0]))
    m = float(np.mean(meas)) - coin
    out["events"][ev] = {"files": files, "volume_db": vol, "measured_vs_coin_db": round(m, 1), "loop": loop,
                         "pitch_vary": vary}
    if abs(m - vol) > 1.0:
        raise SystemExit(f"{ev}: files measure {m:+.1f} dB against coin, table says {vol:+.1f}")
path = asset_path("audio/sfx/river-sounds.json")
path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
print(f"river-sounds.json: {len(EVENTS)} events, coin reference {coin:.1f} LUFS")
ledger.add("audio", path, kind="sound", generator=__file__,
           used_in="River: sound event table for the game builder (files, volume against coin, loop flag)",
           verified=True)

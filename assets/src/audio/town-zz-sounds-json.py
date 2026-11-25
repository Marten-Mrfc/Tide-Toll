# python3 assets/src/audio/town-zz-sounds-json.py  ->  assets/audio/sfx/town-sounds.json (for the game builder)
# One entry per town event: its files (play round-robin, the guide's +-2 dB and +-50 cents on top for the one-shots
# that repeat often), whether it loops, and volume_db: how loud the event should sit against the existing coin
# sound (coin-01..06, about -24 LUFS momentary max). The files are already rendered at that loudness, so the
# builder plays them at the same bus gain as the coin; volume_db is the intended offset, measured_vs_coin_db
# what the files actually measure (loudest 400 ms block, mean over the variants; for the loop, integrated).
# Written from the table below, then checked against the files on disk. Named town-zz-* so build-assets.sh
# (name order) runs it after mill-loop.py and the other town-*.py generators.
import sys, pathlib, json; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import audio as A, ledger, asset_path

EVENTS = [  # event, stem, variants, volume_db vs coin, loop, pitch_vary (False for tuned phrases)
    ("upgrade-bought", "buy", 4, 1.0, False, True),
    ("cannot-afford", "cannot", 2, -1.0, False, True),
    ("level-reached", "level-up", 2, 3.0, False, False),
    ("gift-arrives", "gift", 3, 2.0, False, False),
    ("road-paved-gravel", "pave-gravel", 3, 1.0, False, True),
    ("road-paved-cobbles", "pave-cobbles", 3, 1.0, False, True),
    ("cottage-extended", "extend", 3, 2.0, False, True),
    ("achievement", "achievement", 2, 3.0, False, False),
    ("request-asked", "request-asked", 2, 2.0, False, False),
    ("request-met", "request-met", 2, 2.0, False, False),
    ("request-missed", "request-missed", 1, -1.0, False, False),
    ("ui-press", "ui-press", 4, -4.5, False, True),
    ("ui-hover", "ui-hover", 2, -9.0, False, True),
    ("ui-tab", "ui-tab", 3, -4.0, False, True),
    ("book-close", "book-close", 2, -1.0, False, True),
    ("book-open", "book-open", 2, -1.0, False, True),
    ("page-turn", "page-turn", 3, -3.0, False, True),
    ("mill", "mill-loop", 1, -6.0, True, False),
    ("charter-granted", "charter", 1, 4.0, False, False),
]

coin = np.mean([A.momentary_max_lufs(A.read_wav(asset_path(f"audio/sfx/coin-{i:02d}.wav"))[0]) for i in range(1, 7)])
out = {"about": "Town sound events. Files are 48 kHz; one-shots mono WAV, the mill loop stereo Ogg (loop the whole "
                "file, sample-exact). volume_db: intended loudness against the coin sound; the files are already "
                "rendered at it, so play them at the coin's bus gain. pitch_vary false: tuned in D Dorian, do not "
                "detune.",
       "reference": "assets/audio/sfx/coin-01.wav", "reference_loudness_lufs": round(float(coin), 1), "events": {}}
for ev, stem, n, vol, loop, vary in EVENTS:
    files = [f"assets/audio/sfx/{stem}.ogg"] if loop else [f"assets/audio/sfx/{stem}-{i:02d}.wav" for i in range(1, n + 1)]
    meas = []
    for f in files:
        p = asset_path(f.removeprefix("assets/"))
        if not p.exists():
            raise SystemExit(f"{f} missing: run the town generators first")
        meas.append(A.ebur128(p)["lufs_i"] if loop else A.momentary_max_lufs(A.read_wav(p)[0]))
    m = float(np.mean(meas)) - (coin if not loop else coin)
    out["events"][ev] = {"files": files, "volume_db": vol, "measured_vs_coin_db": round(m, 1), "loop": loop,
                         "pitch_vary": vary}
    if not loop and abs(m - vol) > 1.0:
        raise SystemExit(f"{ev}: files measure {m:+.1f} dB against coin, table says {vol:+.1f}")
path = asset_path("audio/sfx/town-sounds.json")
path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
print(f"town-sounds.json: {len(EVENTS)} events, coin reference {coin:.1f} LUFS")
ledger.add("audio", path, kind="sound", generator=__file__,
           used_in="Town: sound event table for the game builder (files, volume against coin, loop flag)",
           verified=True)

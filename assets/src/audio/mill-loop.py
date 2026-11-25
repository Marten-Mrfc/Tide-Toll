# python3 assets/src/audio/mill-loop.py  ->  assets/audio/sfx/mill-loop.ogg (48 kHz stereo Vorbis q7, exactly
# 6.000 s = 288000 samples, seamless loop; the 16-bit WAV master stays in build/audio-loops/)
# The mill turning slowly: one turn of the sails per loop, so four soft sail sweeps of air (low, wide), the timber
# shaft creaking twice in its bearing (stick-slip creak), and the wallower's wooden teeth knocking softly, eight per
# turn. Quiet: an ambience spot under the river bed (-30 LUFS integrated; the river is -27).
# Seamless by construction, as water-river-loop.py: periodic noise and gain curves over exactly the loop length,
# events wrapped round the end, filters run on a doubled copy keeping the steady second half. Nothing cross-faded.
# Lives in sfx/ beside the other town sounds (the brief); format follows the guide's rule for loops (Ogg, stereo).
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import numpy as np
from common import ROOT, audio as A, ledger, asset_path
from _synth import periodic_noise, add_wrapped, level, report, seam_report, band_energy
from _town import creak, wood_knock

SEED = 1100
LOOP_S = 6.0
TARGET_I = -30.0
rng = np.random.default_rng(SEED)
N = int(LOOP_S * A.SR)
t = np.arange(N) / N

x = np.zeros((N, 2))
# 1. four sail sweeps per turn: low air, each sail passing a little left to right
for ch in range(2):
    air = periodic_noise(N, rng, lo=120, hi=900, tilt=0.6)
    phase = 0.04 * (1 if ch else -1)
    gate = (0.5 + 0.5 * np.cos(2 * np.pi * 4 * (t + phase))) ** 3
    x[:, ch] += air * (0.12 + 0.6 * gate)

# 2. shaft creaks, twice per turn (wrapped), wood body modes low
for k, t0 in enumerate((0.7, 3.6)):
    c = creak(rng, rng.uniform(1.1, 1.4), rate=(28.0, 55.0), body=(210.0, 470.0, 820.0), tau=0.014, jitter=0.3,
              swell=0.45)
    add_wrapped(x, A.pan(A.lowpass(c, 2200, 2) * 2.2, -0.2 + 0.4 * k), t0)

# 3. wallower teeth: eight soft knocks per turn on a low wooden note (D3, the drone's key), evenly spaced
for k in range(8):
    kn = wood_knock(rng, A.NOTE["D3"] * rng.uniform(0.985, 1.015), dur=0.3, crack=0.0, soft=0.004)
    add_wrapped(x, A.pan(kn * rng.uniform(0.18, 0.26), rng.uniform(-0.15, 0.15)), (k + 0.5) * LOOP_S / 8)

x = A.lowpass(np.concatenate([x, x]), 4500, 2)[N:]
x = A.highpass(np.concatenate([x, x]), 60, 2)[N:]
x = level(x, -28.0, tp_ceiling=-3.0)
wav = A.write_wav(ROOT / "build" / "audio-loops" / "mill-loop.wav", x)
x = x * 10 ** ((TARGET_I - A.ebur128(wav)["lufs_i"]) / 20)
wav = A.write_wav(wav, x)
out = A.encode_ogg(wav, asset_path("audio/sfx/mill-loop.ogg"), quality=7, stereo=True)
dec = A.decode(out, channels=2)
assert len(dec) == N, f"Ogg length {len(dec)} differs from the loop length {N}"
report(out, f"below 300 Hz {band_energy(x, 0, 300):.0%}", loop=True)
seam_report(x, "mill-loop-wav", check=True)
seam_report(dec, "mill-loop", check=True)
ledger.add("audio", out, kind="ambience", generator=__file__, verified=True, seed=SEED,
           used_in="Town: the mill turning, slow sail sweeps and shaft creak, 6 s seamless loop (quiet)")

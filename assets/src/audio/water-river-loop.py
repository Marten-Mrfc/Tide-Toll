# python3 assets/src/audio/water-river-loop.py  ->  assets/audio/ambience/water-river-loop.ogg (48 kHz stereo Vorbis,
# exactly 10.000 s = 480000 samples, seamless loop; the 16-bit WAV it is encoded from stays in build/audio-loops/)
# Calm river bed for the Hamlet: a low flowing body, quieter laps against the bank, scattered bubble plinks, and the
# river drone the score is built on (a slowly breathing D with its fifth A, harmonics up to about 800 Hz so it still
# reads on handheld speakers). Seamless by construction: every sine has a whole number of cycles in the loop, the
# noise is shaped over exactly the loop length, and plinks that run past the end wrap round to the start.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import ROOT, audio as A, ledger, asset_path
from _synth import (bubble, periodic_noise, periodic_lfo, snap_freq, add_wrapped, level, report, seam_report,
                    band_energy)

SEED = 400
LOOP_S = 10.0
TARGET_I = -27.0                       # an ambience bed sits well under the -20 LUFS gameplay mix
rng = np.random.default_rng(SEED)
N = int(LOOP_S * A.SR)
t = np.arange(N) / A.SR

# 1. flowing body: shared low noise plus a decorrelated part per side, breathing over 2-5 cycles per loop
common_body = periodic_noise(N, rng, lo=110, hi=1400, tilt=0.8)
x = np.zeros((N, 2))
for ch in range(2):
    side = periodic_noise(N, rng, lo=110, hi=1400, tilt=0.8)
    body = (0.7 * common_body + 0.7 * side) * periodic_lfo(N, rng, cycles=(2, 3, 5), depth=0.25)
    # 2. laps: brighter noise gated by a slow irregular pulse (6-17 cycles per loop = 0.6-1.7 Hz)
    lap_gate = np.clip(periodic_lfo(N, rng, cycles=(6, 9, 13, 17), depth=1.0) - 0.9, 0, None) ** 1.5
    laps = periodic_noise(N, rng, lo=500, hi=3200, tilt=0.5) * lap_gate
    x[:, ch] = body * 1.0 + laps * 0.55

# 3. bubble plinks, wrapped round the loop end
for _ in range(26):
    b = bubble(rng, rng.uniform(500, 1700), 0.07, rng.uniform(0.008, 0.02), rise=rng.uniform(0.3, 0.9))
    p = rng.uniform(-0.8, 0.8)
    add_wrapped(x, A.pan(b * rng.uniform(0.25, 0.7), p), rng.uniform(0, LOOP_S))

# 4. the river drone: D2, A2, D3, A3, D4 (+ a little A4, D5), each partial an exact loop harmonic, breathing slowly
drone = np.zeros((N, 2))
breath = periodic_lfo(N, rng, cycles=(1, 2), depth=0.35)
for note, amp in (("D2", 1.0), ("A2", 0.55), ("D3", 0.7), ("A3", 0.4), ("D4", 0.3), ("A4", 0.12), ("D5", 0.08)):
    for ch, det in ((0, -1), (1, 1)):                       # 0.1 Hz apart per side: a slow, wide shimmer
        f = snap_freq(A.NOTE[note], N) + det * A.SR / N
        drone[:, ch] += amp * np.sin(2 * np.pi * f * t + rng.uniform(0, 2 * np.pi))
drone *= breath[:, None]
x = x / np.std(x) + drone / np.std(drone) * 0.22           # drone about 13 dB under the water

x = A.lowpass(np.concatenate([x, x]), 6000, 2)[N:]           # filter a doubled copy and keep the steady 2nd half
x = A.highpass(np.concatenate([x, x]), 45, 2)[N:]            # (periodic: no filter start-up at the seam)
I0 = A.momentary_max_lufs(x)
x = level(x, -23.0, tp_ceiling=-3.0)                          # coarse; refine to the integrated target below
wav = A.write_wav(ROOT / "build" / "audio-loops" / "water-river-loop.wav", x)
i_now = A.ebur128(wav)["lufs_i"]
x = x * 10 ** ((TARGET_I - i_now) / 20)
wav = A.write_wav(wav, x)
out = A.encode_ogg(wav, asset_path("audio/ambience/water-river-loop.ogg"), stereo=True)
dec = A.decode(out, channels=2)
print(f"  decoded {len(dec)} samples (want {N}); D2 drone at {A.estimate_f0(x, 60, 90):.2f} Hz (D2 = {A.NOTE['D2']:.2f})")
assert len(dec) == N, "Ogg length differs from the loop length"
report(out, f"below 300 Hz {band_energy(x, 0, 300):.0%}")
seam_report(x, "water-river-loop-wav", check=True)
seam_report(dec, "water-river-loop", check=True)
ledger.add("audio", out, kind="ambience", generator=__file__, verified=True, seed=SEED,
           used_in="Ambience: calm river bed with the D drone (Hamlet), 10 s seamless loop")

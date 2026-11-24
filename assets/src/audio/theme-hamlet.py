# python3 assets/src/audio/theme-hamlet.py  ->  assets/audio/music/theme-hamlet-draft.ogg (48 kHz stereo Vorbis loop)
#                                               assets/audio/music/theme-hamlet-draft.wav (48 kHz 16-bit stereo, same loop)
#                                               assets/audio/music/theme-hamlet-draft-score.json (the notes below)
# First draft of the Tide & Toll theme, Hamlet arrangement (Sound & Music tab): a wooden pipe alone over the river
# drone, in a slow 3 with a free, breathing feel; a frame drum answers bars 5-8; a small lyre marks the two phrase
# starts with the open fifth D-A. D Dorian, no chord changes, 1-2 voices. 8 bars at 64 BPM in 3/4 = 22.5 s, played
# twice (second round, 2026-11-24): the second pass (bars 9-16) varies the tune a little (bars 11, 14 and 15) and the
# drum answers through the whole pass, so the loop is 16 bars = 45 s = exactly 2,160,000 samples (sample-exact).
# Seam level (second round): the last note (bar 16, D5) is held into the downbeat and the opening lyre roll waits
# 120 ms (the first pipe note 80 ms), so the level 50 ms either side of the join matches within 3 dB (measured and asserted below).
#
# Seamless by construction (no cross-fade): drone partials have whole numbers of cycles in the loop, every note is
# added with its tail wrapped round to the start (the last bar's ring-out sounds under bar 1), and the reverb is a
# circular convolution over the loop.
#
# The melody rules (design doc): mostly stepwise, spans a sixth (D5 to B5), avoids the third degree (F) on strong
# beats, works in Dorian, Mixolydian and major (only F/F# changes, and F sits on a weak beat), and its first four
# notes (D E G A) are the bell motif. Edit MELODY below; the generator re-renders and re-exports the score.
import json
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
import numpy as np
from common import ROOT, audio as A, ledger, asset_path
from _synth import (modal, burst, periodic_noise, periodic_lfo, snap_freq, add_wrapped, reverb_ir, circular_reverb,
                    report, seam_report, band_energy)

SEED = 800
BPM = 64
BEATS_PER_BAR = 3
BEAT = 60 / BPM                                    # 0.9375 s
BAR = BEATS_PER_BAR * BEAT                         # 2.8125 s
BARS = 16
N = int(round(BARS * BAR * A.SR))                  # 2,160,000 samples
assert abs(N - BARS * BAR * A.SR) < 1e-6
TARGET_I = -20.0                                   # LUFS integrated (gameplay mix target in the guide)
HUMANISE = 0.012                                   # +-12 ms onset offsets: a free, unmetronomic feel

# ---------------------------------------------------------------- THE SCORE (edit here)
# One line per bar: (note, beats). Beats in each bar add up to 3. "-" would be a rest.
MELODY = [
    [("D5", 1), ("E5", 1), ("G5", 1)],             # bar 1  bell motif D E G ...
    [("A5", 3)],                                   # bar 2  ... A (motif complete), held
    [("B5", 1), ("A5", 1), ("G5", 1)],             # bar 3  top of the sixth (B: the Dorian colour)
    [("A5", 2), ("E5", 1)],                        # bar 4  half cadence on A
    [("G5", 1), ("A5", 1), ("G5", 1)],             # bar 5  answer (frame drum enters)
    [("E5", 2), ("F5", 1)],                        # bar 6  F only on the weak third beat
    [("E5", 1), ("D5", 1), ("E5", 1)],             # bar 7
    [("D5", 3)],                                   # bar 8  home on D, rings into bar 9
]
# Second pass: the same tune with a small variation (bars 11, 14, 15), ending home on D held into bar 1.
MELODY = MELODY + [
    MELODY[0], MELODY[1],
    [("B5", 2), ("A5", 1)],                        # bar 11 the top note held longer
    MELODY[3], MELODY[4],
    [("E5", 1), ("G5", 1), ("F5", 1)],             # bar 14 a turn up before the weak-beat F
    [("E5", 2), ("D5", 1)],                        # bar 15 settles a beat early
    [("D5", 3)],                                   # bar 16 home on D, held into the downbeat of bar 1
]
HOLD_LAST = 1.04                                   # the last note's length as a fraction of its beats (ties over)
# Frame drum, bars 5-8 only: (bar, beat, stroke). "dum" = open low stroke, "tek" = soft rim stroke.
DRUM = [(b, 1, "dum") for b in (5, 6, 7, 8)] + [(b, 3, "tek") for b in (5, 6, 7)] + [(8, 2.5, "tek")]
DRUM += [(b, 1, "dum") for b in range(9, 17)] + [(b, 3, "tek") for b in range(9, 16)]          # second pass
# Lyre: the open fifth D3-A3 rolled at each phrase start, plus a soft single A3 in the middle of each phrase.
LYRE = [(1, 1, ["D3", "A3"]), (3, 1, ["A3"]), (5, 1, ["D3", "A3"]), (7, 1, ["A3"]),
        (9, 1, ["D3", "A3"]), (11, 1, ["A3"]), (13, 1, ["D3", "A3"]), (15, 1, ["A3"])]
FIRST_DELAY = 0.08                                 # the loop's first pipe note enters 80 ms late, under the tie
LYRE_DELAY = 0.12                                  # the rolls start 120 ms after the beat (shapes the loop start)
# Drone: (note, relative level). The river's D with its fifth, harmonics up to about 800 Hz.
DRONE = [("D2", 0.45), ("D3", 1.0), ("A3", 0.6)]

for b, bar in enumerate(MELODY, 1):
    assert abs(sum(d for _, d in bar) - BEATS_PER_BAR) < 1e-9, f"bar {b} does not add up to {BEATS_PER_BAR} beats"
    assert bar[0][0] != "F5", f"bar {b}: the third degree (F) on a strong beat"

rng = np.random.default_rng(SEED)
mix = {k: np.zeros((N, 2)) for k in ("pipe", "lyre", "drum", "drone")}


def onset(bar, beat):
    return (bar - 1) * BAR + (beat - 1) * BEAT


# ---------------------------------------------------------------- voices
def pipe(f0, dur, rng):
    """Wooden pipe / recorder: nearly sine, a little 2nd and 3rd harmonic, breath noise round the pitch, a soft
    chiff at the start, a slight scoop up into pitch, delayed gentle vibrato."""
    tail = 0.18
    n = int((dur + tail) * A.SR)
    t = np.arange(n) / A.SR
    scoop = 1 - 0.006 * np.exp(-t / 0.035)
    vib = 1 + 0.0028 * np.sin(2 * np.pi * rng.uniform(4.6, 5.2) * t + rng.uniform(0, 6)) * np.clip((t - 0.35) / 0.5, 0, 1)
    ph = 2 * np.pi * np.cumsum(f0 * scoop * vib) / A.SR
    tone = np.sin(ph) + 0.16 * np.sin(2 * ph + 0.3) + 0.09 * np.sin(3 * ph + 1.1) + 0.03 * np.sin(4 * ph)
    breath = A.bandpass(rng.standard_normal(n), f0 * 0.85, f0 * 1.18, 2) * 0.35 + \
        A.bandpass(rng.standard_normal(n), 1500, 5000, 2) * 0.05
    env = A.env_adsr(n, 0.05, 0.12, 0.82, tail + 0.06)
    env *= 1 - 0.12 * np.clip(t / max(dur, 0.3), 0, 1)              # a long note relaxes a little
    chiff = burst(rng, 0.04, 1800, 5500, 0.008, attack=0.002)
    x = (tone + breath) * env
    x[:len(chiff)] += chiff * 0.25
    return A.fade(A.lowpass(x, 6000, 2), 0.001, 0.02)


def lyre(f0, rng):
    """Small lyre: a gut string plucked softly (Karplus-Strong, dark burst, long-ish decay)."""
    x = A.karplus_strong(f0, 4.0, rng, decay=0.9985, bright=0.25)
    x *= np.minimum(np.arange(len(x)) / (0.002 * A.SR), 1)
    return A.fade(A.lowpass(x, 3000, 2), 0.0005, 0.5)


def frame_drum(stroke, rng):
    """Frame drum: membrane modes (1, 1.59, 2.14, 2.30 ...) with a small pitch drop, plus the hand's slap."""
    if stroke == "dum":
        f0, amps, taus, slap_band, slap_gain = 82.0, [1.0, 0.35, 0.18, 0.12], [0.32, 0.12, 0.08, 0.06], (150, 900), 0.25
    else:
        f0, amps, taus, slap_band, slap_gain = 82.0, [0.15, 0.25, 0.3, 0.25], [0.08, 0.06, 0.05, 0.04], (700, 3000), 0.35
    dur = 1.2
    t = A.t_axis(dur)
    x = np.zeros_like(t)
    for r, a, tau in zip([1.0, 1.59, 2.14, 2.30], amps, taus):
        f = f0 * r * (1 + 0.06 * np.exp(-t / 0.05))
        x += a * np.sin(2 * np.pi * np.cumsum(f) / A.SR) * np.exp(-t / tau)
    x *= np.minimum(t / 0.002, 1)
    sl = burst(rng, 0.06, *slap_band, 0.01, attack=0.0008)
    x[:len(sl)] += sl * slap_gain
    return A.fade(x, 0.0005, 0.2) * (1.0 if stroke == "dum" else 0.45)


# ---------------------------------------------------------------- render
events = []                                         # for the exported score
t_acc = 0.0
for b, bar in enumerate(MELODY, 1):
    beat = 1.0
    for note, beats in bar:
        if note != "-":
            t0 = onset(b, beat) + rng.uniform(-HUMANISE, HUMANISE)
            if b == 1 and beat == 1:
                t0 += FIRST_DELAY                                       # let the held last note ring into bar 1
            last = b == len(MELODY) and beat + beats > BEATS_PER_BAR
            dur = beats * BEAT * (HOLD_LAST if last else 0.93)          # breathe between notes; tie the last over
            vel = 10 ** ((-1.5 if beat != 1 else 0.0) / 20) * rng.uniform(0.94, 1.0)  # lighter off the downbeat
            add_wrapped(mix["pipe"], A.pan(pipe(A.NOTE[note], dur, rng) * vel, -0.12), t0)
            events.append({"bar": b, "beat": beat, "note": note, "beats": beats, "midi": int(round(69 + 12 * np.log2(A.NOTE[note] / 440)))})
        beat += beats

for b, beat, stroke in DRUM:
    t0 = onset(b, beat) + rng.uniform(-HUMANISE / 2, HUMANISE / 2)
    add_wrapped(mix["drum"], A.pan(frame_drum(stroke, rng), 0.2), t0)

for b, beat, notes in LYRE:
    for k, note in enumerate(notes):                                    # roll the fifth upwards, 45 ms apart
        t0 = onset(b, beat) + LYRE_DELAY + 0.045 * k + rng.uniform(0, HUMANISE)
        add_wrapped(mix["lyre"], A.pan(lyre(A.NOTE[note], rng) * (0.8 if len(notes) == 1 else 1.0), 0.35), t0)

# drone: hurdy-gurdy-like stack on each drone note; every partial an exact loop harmonic; slow breathing per partial
tt = np.arange(N) / A.SR
for note, lvl in DRONE:
    f0 = A.NOTE[note]
    for h in range(1, 12):
        if f0 * h > 900:
            break
        amp = lvl * h ** -1.25 * (0.6 if h % 2 == 0 else 1.0)
        breath = periodic_lfo(N, rng, cycles=(1, 2, 3), depth=0.25)
        for ch, det in ((0, -1), (1, 1)):
            f = snap_freq(f0 * h, N) + det * A.SR / N * (h == 1)          # fundamentals 0.044 Hz apart L/R
            mix["drone"][:, ch] += amp * breath * np.sin(2 * np.pi * f * tt + rng.uniform(0, 2 * np.pi))
bow_noise = periodic_noise(N, rng, lo=250, hi=1000, tilt=1.0) * periodic_lfo(N, rng, cycles=(2, 3), depth=0.4)
mix["drone"] += 0.02 * np.stack([bow_noise, np.roll(bow_noise, 4800)], 1)

GAINS = {"pipe": 1.0, "lyre": 0.38, "drum": 0.55, "drone": 0.16}         # balance (pipe leads, drone under)
dry = sum(mix[k] * GAINS[k] for k in mix)
ir = reverb_ir(np.random.default_rng(SEED + 1), rt60=2.0, dur=3.0, predelay=0.018)
wet = circular_reverb(dry, ir)
x = dry + 0.28 * wet * np.std(dry) / max(np.std(wet), 1e-12)
x = A.highpass(np.concatenate([x, x]), 40, 2)[N:]                         # periodic high-pass (keep the 2nd copy)

# level: integrated loudness (ebur128 on a temp WAV), keeping the true peak at or under -2 dBTP
tmp = ROOT / "build" / "audio-loops" / "theme-hamlet-level.wav"
x *= 0.25 / np.max(np.abs(x))
i0 = A.ebur128(A.write_wav(tmp, x))["lufs_i"]
x *= 10 ** ((TARGET_I - i0) / 20)
tp = A.true_peak_db(x)
if tp > -2.0:
    x *= 10 ** ((-2.0 - tp) / 20)

wav = A.write_wav(asset_path("audio/music/theme-hamlet-draft.wav"), x)
ogg = A.encode_ogg(wav, asset_path("audio/music/theme-hamlet-draft.ogg"), stereo=True)
dec = A.decode(ogg, channels=2)
assert len(dec) == N, f"Ogg decodes to {len(dec)} samples, want {N}"
print(f"  loop {N} samples = {N / A.SR:.4f} s; drone D3 at {A.estimate_f0(mix['drone'], 140, 155):.2f} Hz "
      f"(D3 = {A.NOTE['D3']:.2f}); pipe B5 top {A.NOTE['B5']:.1f} Hz")
seam_report(x, "theme-hamlet-draft-wav", check=True)


def rms_db(seg):
    return 10 * np.log10(np.mean(np.asarray(seg, float) ** 2) + 1e-20)
w = int(0.05 * A.SR)
seam_db = (rms_db(x[-w:]), rms_db(x[:w]))
print(f"  seam level: last 50 ms {seam_db[0]:.1f} dB, first 50 ms {seam_db[1]:.1f} dB, "
      f"difference {seam_db[1] - seam_db[0]:+.1f} dB (want within 3 dB)")
assert abs(seam_db[1] - seam_db[0]) <= 3.0, "level step at the loop seam"
seam_report(dec, "theme-hamlet-draft", check=True)
for p in (wav, ogg):
    report(p, f"70-300 Hz {band_energy(x, 70, 300):.0%}", fmax=6000, loop=True)

# ---------------------------------------------------------------- score export (data next to the audio)
score = {
    "title": "Tide & Toll theme, Hamlet arrangement (first draft)",
    "generator": "assets/src/audio/theme-hamlet.py",
    "key": "D", "mode": "Dorian (D E F G A B C)", "metre": "3/4", "bpm": BPM, "bars": BARS,
    "loop_samples_48k": N, "loop_seconds": N / A.SR,
    "feel": f"free and breathing: onsets humanised by +-{int(HUMANISE * 1000)} ms, notes held 93% of their length",
    "bell_motif": [e["note"] for e in events[:4]],
    "range": "D5-B5 (a major sixth)",
    "form": "8 bars, then the same 8 bars again with a small variation in bars 11, 14 and 15 (45 s loop)",
    "rules": ["mostly stepwise", "spans a sixth", "no third degree (F) on a strong beat",
              "works in Dorian, Mixolydian and major (only F/F# changes)", "first four notes are the bell motif"],
    "instruments": {"pipe": "lead, wooden pipe / recorder, bars 1-8",
                    "drone": "river drone " + " + ".join(n for n, _ in DRONE) + ", whole loop",
                    "lyre": "small lyre, open fifth D3-A3 at phrase starts",
                    "frame_drum": "answers bars 5-8 (dum on beat 1, soft tek after)"},
    "melody_by_bar": [" ".join(f"{n}:{d}" for n, d in bar) for bar in MELODY],
    "pipe": events,
    "frame_drum": [{"bar": b, "beat": bt, "stroke": s} for b, bt, s in sorted(DRUM)],
    "lyre": [{"bar": b, "beat": bt, "notes": ns} for b, bt, ns in LYRE],
    "drone": [{"note": n, "level": l} for n, l in DRONE],
}
js = asset_path("audio/music/theme-hamlet-draft-score.json")
js.write_text(json.dumps(score, indent=1) + "\n", encoding="utf-8")
print("  score:", " | ".join(score["melody_by_bar"]))

used = "Music: Tide & Toll theme, Hamlet arrangement, draft (8 bars played twice with a variation, D Dorian, 3/4 at 64 BPM, 45 s seamless loop)"
ledger.add("audio", ogg, kind="music", generator=__file__, verified=True, seed=SEED, used_in=used)
ledger.add("audio", wav, kind="music", generator=__file__, verified=True, seed=SEED, used_in=used + "; 16-bit WAV master of the same loop")
ledger.add("audio", js, kind="music", generator=__file__, verified=True, seed=SEED,
           used_in="Music: the theme's notes (score data for the Hamlet draft; source for later arrangements)")

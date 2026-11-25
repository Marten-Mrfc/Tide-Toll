"""Shared engine for the background music (a helper, not a generator: build-assets.sh skips `_` files).

Same composer, same instruments, same room as the Hamlet theme (theme-hamlet.py): the wooden pipe, the small lyre,
the frame drum and the river drone are copied here unchanged from that generator, and so are the mix gains, the
reverb (rt60 2.0 s, predelay 18 ms, 28% wet), the 40 Hz high-pass, the humanising (+-12 ms, notes held 93%) and the
level (-20 LUFS integrated, true peak at or under -2 dBTP). theme-hamlet.py itself is not changed and does not use
this file, so the theme renders exactly as before. A piece is a dict (see the music-*.py generators):

    render(PIECE)       ->  assets/audio/music/<id>.ogg + <id>-score.json, WAV master in build/audio-music/,
                            ledger rows, and a one-line measurement report

Loops ("loop": True) are seamless by construction exactly like the theme (wrapped tails, periodic drone partials,
circular reverb, last note tied into the downbeat, first note 80 ms late, lyre rolls 120 ms late). A piece with
"loop": False (the age change) is rendered linearly with a real ending: the drone swells in and dies away with
the last note, and the reverb tail rings out.
"""
import json

import numpy as np

from common import ROOT, audio as A, ledger, asset_path, rel
from _synth import (burst, periodic_noise, periodic_lfo, snap_freq, add_wrapped, place_at, reverb_ir,
                    circular_reverb, report, seam_report, band_energy)

HUMANISE = 0.012
FIRST_DELAY = 0.08
LYRE_DELAY = 0.12
HOLD_LAST = 1.04
GAINS = {"pipe": 1.0, "lyre": 0.38, "drum": 0.55, "drone": 0.16}      # the theme's balance
COUNTER_GAIN = 0.55                                                    # second pipe (counter-melody), under the lead
TARGET_I = -20.0
OGG_QUALITY = 7      # theme uses 5; at 7 the Vorbis error at the loop join (the encoder does not know the file loops) passes the seam check (see music-progress.md)
WAV_DIR = ROOT / "build" / "audio-music"
PC = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8,
      "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}
MODES = {"ionian": [0, 2, 4, 5, 7, 9, 11], "dorian": [0, 2, 3, 5, 7, 9, 10], "phrygian": [0, 1, 3, 5, 7, 8, 10],
         "lydian": [0, 2, 4, 6, 7, 9, 11], "mixolydian": [0, 2, 4, 5, 7, 9, 10], "aeolian": [0, 2, 3, 5, 7, 8, 10]}
SHARP = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]


def midi(note):
    name, octave = note[:-1], int(note[-1])
    return 12 * (octave + 1) + PC[name]


def freq(note):
    return 440.0 * 2 ** ((midi(note) - 69) / 12)


def mode_pcs(final, mode):
    return sorted((PC[final] + s) % 12 for s in MODES[mode])


def parse_bars(lines, beats_per_bar=3):
    """'D5:1 E5:1 G5:1' per bar -> [(note, beats), ...]; '-' is a rest."""
    bars = []
    for b, line in enumerate(lines, 1):
        bar = [(tok.split(":")[0], float(tok.split(":")[1])) for tok in line.split()]
        assert abs(sum(d for _, d in bar) - beats_per_bar) < 1e-9, f"bar {b} '{line}' does not add up to 3 beats"
        bars.append(bar)
    return bars


# ---------------------------------------------------------------- voices (copied unchanged from theme-hamlet.py)
def pipe(f0, dur, rng):
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
    env *= 1 - 0.12 * np.clip(t / max(dur, 0.3), 0, 1)
    chiff = burst(rng, 0.04, 1800, 5500, 0.008, attack=0.002)
    x = (tone + breath) * env
    x[:len(chiff)] += chiff * 0.25
    return A.fade(A.lowpass(x, 6000, 2), 0.001, 0.02)


def lyre(f0, rng):
    x = A.karplus_strong(f0, 4.0, rng, decay=0.9985, bright=0.25)
    x *= np.minimum(np.arange(len(x)) / (0.002 * A.SR), 1)
    return A.fade(A.lowpass(x, 3000, 2), 0.0005, 0.5)


def frame_drum(stroke, rng):
    if stroke == "dum":
        f0, amps, taus, slap_band, slap_gain = 82.0, [1.0, 0.35, 0.18, 0.12], [0.32, 0.12, 0.08, 0.06], (150, 900), 0.25
    else:
        f0, amps, taus, slap_band, slap_gain = 82.0, [0.15, 0.25, 0.3, 0.25], [0.08, 0.06, 0.05, 0.04], (700, 3000), 0.35
    t = A.t_axis(1.2)
    x = np.zeros_like(t)
    for r, a, tau in zip([1.0, 1.59, 2.14, 2.30], amps, taus):
        f = f0 * r * (1 + 0.06 * np.exp(-t / 0.05))
        x += a * np.sin(2 * np.pi * np.cumsum(f) / A.SR) * np.exp(-t / tau)
    x *= np.minimum(t / 0.002, 1)
    sl = burst(rng, 0.06, *slap_band, 0.01, attack=0.0008)
    x[:len(sl)] += sl * slap_gain
    return A.fade(x, 0.0005, 0.2) * (1.0 if stroke == "dum" else 0.45)


# ---------------------------------------------------------------- render
def render(P):
    """Render one piece (dict). Returns the dict of measurements."""
    bpm, loop = P["bpm"], P["loop"]
    beat_s = 60 / bpm
    bar_s = 3 * beat_s
    melody = parse_bars(P["melody"])
    counter = parse_bars(P["counter"]) if P.get("counter") else None
    bars = len(melody)
    assert counter is None or len(counter) == bars
    scale = mode_pcs(P["final"], P["mode"])
    for line in [melody] + ([counter] if counter else []):
        for b, bar in enumerate(line, 1):
            for note, _ in bar:
                assert note == "-" or midi(note) % 12 in scale, f"{P['id']} bar {b}: {note} not in {P['final']} {P['mode']}"
    for _, _, notes in P.get("lyre", []):
        for n in notes:
            assert midi(n) % 12 in scale, f"{P['id']} lyre {n} not in mode"
    for n, _ in P["drone"]:
        assert midi(n) % 12 in scale

    end_hold = P.get("end_hold_beats", 3 * HOLD_LAST)                   # length of the last note in beats
    if loop:
        N = int(round(bars * bar_s * A.SR))
        music_end = N / A.SR
    else:
        music_end = (bars - 1) * bar_s + end_hold * beat_s
        N = int(round((music_end + P.get("tail_s", 2.6)) * A.SR))
    add = (lambda buf, x, t0: add_wrapped(buf, x, t0)) if loop else (lambda buf, x, t0: place_at(buf, x, max(t0, 0.0)))
    rng = np.random.default_rng(P["seed"])
    mix = {k: np.zeros((N, 2)) for k in ("pipe", "lyre", "drum", "drone")}

    def onset(bar, beat):
        return (bar - 1) * bar_s + (beat - 1) * beat_s

    def play(line, gain, pan, delay_first):
        ev = []
        for b, bar in enumerate(line, 1):
            beat = 1.0
            for note, beats in bar:
                if note != "-":
                    t0 = onset(b, beat) + rng.uniform(-HUMANISE, HUMANISE)
                    if b == 1 and beat == 1 and delay_first:
                        t0 += FIRST_DELAY
                    last = b == len(line) and beat + beats > 3
                    dur = (end_hold * beat_s if last else beats * beat_s * 0.93)
                    vel = 10 ** ((-1.5 if beat != 1 else 0.0) / 20) * rng.uniform(0.94, 1.0)
                    add(mix["pipe"], A.pan(pipe(freq(note), dur, rng) * vel * gain, pan), t0)
                    ev.append({"bar": b, "beat": beat, "note": note, "beats": beats, "midi": midi(note)})
                beat += beats
        return ev

    events = play(melody, 1.0, -0.12, loop)
    cevents = play(counter, COUNTER_GAIN, 0.3, loop) if counter else []

    for b, bt, stroke in P.get("drum", []):
        t0 = onset(b, bt) + rng.uniform(-HUMANISE / 2, HUMANISE / 2)
        add(mix["drum"], A.pan(frame_drum(stroke, rng), 0.2), t0)

    for b, bt, notes in P.get("lyre", []):
        for k, note in enumerate(notes):
            t0 = onset(b, bt) + LYRE_DELAY + 0.045 * k + rng.uniform(0, HUMANISE)
            add(mix["lyre"], A.pan(lyre(freq(note), rng) * (0.8 if len(notes) == 1 else 1.0), 0.35), t0)

    # drone: the theme's stack; in a loop every partial is periodic, in an ending piece it swells in and dies away
    tt = np.arange(N) / A.SR
    if loop:
        shape = np.ones(N)
    else:
        shape = np.clip(tt / 1.5, 0, 1) * np.clip((music_end + 0.6 - tt) / 3.0, 0, 1)
    for note, lvl in P["drone"]:
        f0 = freq(note)
        for h in range(1, 12):
            if f0 * h > 900:
                break
            amp = lvl * h ** -1.25 * (0.6 if h % 2 == 0 else 1.0)
            breath = periodic_lfo(N, rng, cycles=(1, 2, 3), depth=0.25)
            for ch, det in ((0, -1), (1, 1)):
                f = snap_freq(f0 * h, N) + det * A.SR / N * (h == 1)
                mix["drone"][:, ch] += amp * breath * shape * np.sin(2 * np.pi * f * tt + rng.uniform(0, 2 * np.pi))
    bow = periodic_noise(N, rng, lo=250, hi=1000, tilt=1.0) * periodic_lfo(N, rng, cycles=(2, 3), depth=0.4) * shape
    mix["drone"] += 0.02 * np.stack([bow, np.roll(bow, 4800)], 1)

    dry = sum(mix[k] * GAINS[k] * P.get("gains", {}).get(k, 1.0) for k in mix)
    ir = reverb_ir(np.random.default_rng(P["seed"] + 1), rt60=2.0, dur=3.0, predelay=0.018)
    wet = circular_reverb(dry, ir)                    # an ending piece ends in tail_s of near-silence: little wrap
    x = dry + 0.28 * wet * np.std(dry) / max(np.std(wet), 1e-12)
    if loop:
        x = A.highpass(np.concatenate([x, x]), 40, 2)[N:]
    else:
        x = A.highpass(x, 40, 2)
        x = A.fade(x, 0.002, 0.8)

    tmp = WAV_DIR / f"{P['id']}-level.wav"
    x *= 0.25 / np.max(np.abs(x))
    i0 = A.ebur128(A.write_wav(tmp, x))["lufs_i"]
    x *= 10 ** ((P.get("target_i", TARGET_I) - i0) / 20)
    tp = A.true_peak_db(x)
    if tp > -2.0:
        x *= 10 ** ((-2.0 - tp) / 20)

    wav = A.write_wav(WAV_DIR / f"{P['id']}.wav", x)
    ogg = A.encode_ogg(wav, asset_path(f"audio/music/{P['id']}.ogg"), quality=OGG_QUALITY, stereo=True)
    dec = A.decode(ogg, channels=2)
    assert len(dec) == N, f"Ogg decodes to {len(dec)} samples, want {N}"
    if loop:
        seam_report(x, f"{P['id']}-wav", check=True)
        w = int(0.05 * A.SR)
        db = lambda s: 10 * np.log10(np.mean(np.asarray(s, float) ** 2) + 1e-20)
        step = db(x[:w]) - db(x[-w:])
        print(f"  seam level: difference {step:+.1f} dB (want within 3 dB)")
        assert abs(step) <= 3.0, "level step at the loop seam"
        seam_report(dec, P["id"], check=True)
    report(ogg, f"70-300 Hz {band_energy(x, 70, 300):.0%}", fmax=6000, loop=loop)

    names = [SHARP[p] for p in scale]
    score = {
        "id": P["id"], "title": P["title"], "role": P["role"], "generator": rel(P["generator"]),
        "key": P["final"], "mode": f"{P['mode'].capitalize()} ({' '.join(names)})", "metre": "3/4", "bpm": bpm,
        "bars": bars, "loops": loop, "samples_48k": N, "seconds": N / A.SR,
        "feel": "as the Hamlet theme: onsets humanised by +-12 ms, notes held 93% of their length",
        "form": P["form"], "range": "",
        "instruments": P["instruments"],
        "melody_by_bar": P["melody"],
        "counter_by_bar": P.get("counter"),
        "pipe": events, "counter_pipe": cevents,
        "frame_drum": [{"bar": b, "beat": bt, "stroke": s} for b, bt, s in sorted(P.get("drum", []))],
        "lyre": [{"bar": b, "beat": bt, "notes": ns} for b, bt, ns in P.get("lyre", [])],
        "drone": [{"note": n, "level": l} for n, l in P["drone"]],
    }
    lo = min(events, key=lambda e: e["midi"])["note"]
    hi = max(events, key=lambda e: e["midi"])["note"]
    score["range"] = f"{lo}-{hi}"
    js = asset_path(f"audio/music/{P['id']}-score.json")
    js.write_text(json.dumps(score, indent=1) + "\n", encoding="utf-8")

    ledger.add("audio", ogg, kind="music", generator=P["generator"], verified=True, seed=P["seed"], used_in=P["used_in"])
    ledger.add("audio", js, kind="music", generator=P["generator"], verified=True, seed=P["seed"],
               used_in=f"Music: the notes of {P['title']} (score data)")
    return score

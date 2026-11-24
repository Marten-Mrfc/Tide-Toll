"""Audio synthesis helpers: numpy + scipy, float arrays in [-1, 1] at 48 kHz (mono (n,), stereo (n, 2)).

Everything takes an explicit rng = np.random.default_rng(seed); nothing reads the clock, so output is
reproducible. Record each variation's seed in the ledger row.

    from common import audio as A
    rng = np.random.default_rng(101)
    x = A.step_grass(rng)                         # voices: step_grass, step_dirt, bell, karplus_strong, bow, click
    x = A.normalize_peak(x, -4)                   # leave headroom: peaks well under -1 dBFS
    A.write_wav("assets/audio/sfx/step-grass-01.wav", x)          # 16-bit PCM, 48 kHz (stereo if x is (n, 2))
    A.encode_ogg(wav, "assets/audio/ambience/x.ogg", stereo=True)  # ffmpeg libvorbis -q:a 5, 48 kHz
    A.check(path) -> {'true_peak_dbtp', 'sample_peak_dbfs', 'lufs_i', 'momentary_max_lufs', 'duration_s', ...,
                      'problems': [...]}                          # problems is empty when it meets the guide
    A.spectrogram_png(path_or_array, "build/audio/step-grass-01.png", title="step-grass-01")

Building blocks: t_axis, env_exp, env_adsr, fade, white, pink, brown, wobble, lowpass, highpass, bandpass,
mix, pad, normalize_peak, to_stereo, pan, loop_crossfade, true_peak_db, momentary_max_lufs, estimate_f0, NOTE.
Notes on loudness: ffmpeg ebur128 gives an integrated LUFS of -70 for one-shots shorter than its 400 ms gate;
use momentary_max_lufs for those (the loudest 400 ms block, ungated).
"""
import json
import re
import subprocess
import wave
from pathlib import Path

import numpy as np
from scipy import signal

SR = 48000
# Equal-tempered pitches (A4 = 440 Hz) for the D-centred theme.
NOTE = {f"{n}{o}": 440.0 * 2 ** ((i - 9) / 12 + (o - 4))
        for o in range(1, 8) for i, n in enumerate(["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"])}
# Frequency lanes from the Sound tab (Hz).
LANES = {"drone": (70, 300), "bells": (300, 3000), "footfall": (1000, 6000), "ui": (2000, 4000)}


# ---------------------------------------------------------------- basics
def t_axis(dur, sr=SR): return np.arange(int(round(dur * sr))) / sr
def db(x): return 10 ** (x / 20)
def peak_db(x): return 20 * np.log10(max(float(np.max(np.abs(x))), 1e-12))


def normalize_peak(x, target_db=-4.0):
    return x * (db(target_db) / max(float(np.max(np.abs(x))), 1e-12))


def pad(x, dur, sr=SR):
    n = int(round(dur * sr))
    x = np.asarray(x)
    return np.pad(x, [(0, max(0, n - len(x)))] + [(0, 0)] * (x.ndim - 1))[:n]


def mix(*xs):
    """Sum arrays of different lengths (all mono or all stereo), aligned at the start."""
    n = max(len(x) for x in xs)
    return sum(np.pad(x, [(0, n - len(x))] + [(0, 0)] * (x.ndim - 1)) for x in xs)


def delay(x, seconds, sr=SR):
    return np.concatenate([np.zeros((int(round(seconds * sr)),) + x.shape[1:]), x])


# ---------------------------------------------------------------- envelopes
def env_exp(n, tau, sr=SR): return np.exp(-np.arange(n) / (tau * sr))


def env_adsr(n, a, d, s, r, sr=SR):
    """a, d, r in seconds; s = sustain level 0..1; n = total samples (release ends at n)."""
    A, D, R = int(a * sr), int(d * sr), int(r * sr)
    S = max(n - A - D - R, 0)
    e = np.concatenate([np.linspace(0, 1, A, endpoint=False), np.linspace(1, s, D, endpoint=False),
                        np.full(S, s), np.linspace(s, 0, R)])
    return e[:n] if len(e) >= n else np.pad(e, (0, n - len(e)))


def fade(x, fin=0.002, fout=0.005, sr=SR):
    """Short linear fades so a one-shot never starts or ends with a click."""
    x = np.array(x, float)
    a, b = int(fin * sr), int(fout * sr)
    shape = (-1,) + (1,) * (x.ndim - 1)
    if a:
        x[:a] *= np.linspace(0, 1, a).reshape(shape)
    if b:
        x[-b:] *= np.linspace(1, 0, b).reshape(shape)
    return x


# ---------------------------------------------------------------- noise
def white(n, rng): return rng.standard_normal(n)


def _shaped(n, rng, power):
    X = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n)
    f[0] = f[1]
    x = np.fft.irfft(X / f ** power, n)
    return x / max(float(np.max(np.abs(x))), 1e-12)


def pink(n, rng): return _shaped(n, rng, 0.5)       # 1/f power: wind, distant water
def brown(n, rng): return _shaped(n, rng, 1.0)      # 1/f^2 power: rumble, river body


def wobble(n, rng, rate=0.7, depth=0.5, sr=SR):
    """Slow random gain wobble around 1.0 (low-passed noise) for water beds and wind."""
    w = lowpass(rng.standard_normal(n), rate, sr=sr)
    w /= max(float(np.std(w)), 1e-9)
    return 1 + depth * w / 3


# ---------------------------------------------------------------- filters (causal Butterworth, second-order sections)
def _sos(kind, f, order, sr): return signal.butter(order, f, btype=kind, fs=sr, output="sos")
def lowpass(x, f, order=2, sr=SR): return signal.sosfilt(_sos("lowpass", f, order, sr), x, axis=0)
def highpass(x, f, order=2, sr=SR): return signal.sosfilt(_sos("highpass", f, order, sr), x, axis=0)
def bandpass(x, lo, hi, order=2, sr=SR): return signal.sosfilt(_sos("bandpass", [lo, hi], order, sr), x, axis=0)


# ---------------------------------------------------------------- stereo and loops
def to_stereo(x): return np.stack([x, x], axis=1)


def pan(x, p):
    """Equal-power pan of a mono array; p = -1 (left) .. +1 (right)."""
    a = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], axis=1)


def loop_crossfade(x, fade_s=1.0, sr=SR):
    """Make a seamless loop: the last fade_s seconds are cross-faded (equal power) into the start and cut off.
    The result's last sample flows into its first, so the loop point is sample-exact with no click."""
    n = int(round(fade_s * sr))
    if len(x) <= 2 * n:
        raise ValueError("loop source must be longer than twice the cross-fade")
    shape = (-1,) + (1,) * (x.ndim - 1)
    t = np.linspace(0, 1, n, endpoint=False).reshape(shape)
    head, tail = x[:n], x[-n:]
    out = np.array(x[:-n], float)
    out[:n] = head * np.sin(t * np.pi / 2) + tail * np.cos(t * np.pi / 2)
    return out


# ---------------------------------------------------------------- voices
def karplus_strong(freq, dur, rng, decay=0.996, bright=0.5, sr=SR):
    """Plucked string tuned with a first-order allpass (error well under 1 cent). decay = loop gain 0.99..0.999."""
    Pd = sr / freq
    N = int(np.floor(Pd - 1.0))
    d = Pd - 0.5 - N
    c = (1 - d) / (1 + d)
    n = int(dur * sr)
    burst = rng.uniform(-1, 1, N + 2)
    burst = lowpass(burst, 1500 + 9000 * bright, 1, sr)
    x = np.zeros(n)
    x[:len(burst)] = burst - burst.mean()
    A = np.zeros(N + 3)
    A[0] = 1.0
    A[1] = c
    A[N:N + 3] -= np.convolve([1.0, 1.0], [c, 1.0]) * (decay / 2)
    return signal.lfilter([1.0, c], A, x)


def bell(freq, dur, rng=None, strike=0.6, sr=SR):
    """Additive church-bell: inharmonic partials (hum, prime, tierce, quint, nominal...) with their own decays.
    freq = the prime (strike tone)."""
    t = t_axis(dur, sr)
    ratios = [0.5, 1.0, 1.183, 1.506, 2.0, 2.514, 2.662, 3.011, 4.166]
    amps = [0.55, 1.0, 0.6, 0.5, 0.45, 0.25, 0.2, 0.12, 0.08]
    taus = [1.9, 1.6, 1.0, 0.9, 0.7, 0.45, 0.4, 0.3, 0.2]
    out = np.zeros_like(t)
    for r, a, tau in zip(ratios, amps, taus):
        if freq * r >= sr / 2:
            continue
        ph = rng.uniform(0, 2 * np.pi) if rng is not None else 0.0
        out += a * np.sin(2 * np.pi * freq * r * t + ph) * np.exp(-t / tau)
    if rng is not None and strike:
        tick = bandpass(rng.standard_normal(len(t)), 2000, 6000, 2, sr) * np.exp(-t / 0.004)
        out += strike * 0.15 * tick
    return fade(out, 0.001, 0.05, sr)


def bow(freq, dur, rng, vibrato=5.0, vib_depth=0.004, attack=0.35, release=0.6, sr=SR):
    """Sustained bowed-string-like tone: band-limited saw stack, delayed vibrato, rosin noise, slow attack."""
    t = t_axis(dur, sr)
    f = freq * (1 + vib_depth * np.sin(2 * np.pi * vibrato * t) * np.clip(t / 0.8, 0, 1))
    ph = 2 * np.pi * np.cumsum(f) / sr
    saw = sum(np.sin(k * ph) / k for k in range(1, 15) if k * freq < 9000)
    tone = lowpass(saw, 2500, 2, sr) + 0.04 * bandpass(rng.standard_normal(len(t)), 1500, 4000, 2, sr)
    return tone * env_adsr(len(t), attack, 0.1, 0.8, release, sr)


def _crackles(n, rng, count, sr):
    c = np.zeros(n)
    L = int(0.003 * sr)
    for _ in range(count):
        i = int(rng.uniform(0.005, 0.12) * sr)
        c[i:i + L] += rng.standard_normal(L) * np.hanning(L) * rng.uniform(0.2, 0.6)
    return c


def step_grass(rng, weight=1.0, sr=SR):
    """Footstep on grass: soft swish in the 1-6 kHz footfall lane, muted heel thud, a few blade crackles.
    Ends with a 6 kHz low-pass so it stays inside the lane (the proof version was broadband to 12 kHz)."""
    n = int(0.22 * sr)
    t = np.arange(n) / sr
    sw = bandpass(rng.standard_normal(n), 1100 * rng.uniform(0.9, 1.15), 4800 * rng.uniform(0.9, 1.1), 2, sr)
    e = (1 - np.exp(-t / 0.006)) * np.exp(-t / (0.055 * rng.uniform(0.85, 1.2)))
    thud = lowpass(rng.standard_normal(n), 220, 2, sr) * np.exp(-t / 0.025) * 0.5
    crack = highpass(_crackles(n, rng, int(rng.integers(3, 7)), sr), 2500, 2, sr)
    x = weight * (sw * e + thud + crack * 0.8)
    return fade(lowpass(x, 6000, 4, sr), 0.001, 0.02, sr)


def step_dirt(rng, weight=1.0, sr=SR):
    """Footstep on packed earth: drier and shorter than grass, more low-mid body, a little grit."""
    n = int(0.16 * sr)
    t = np.arange(n) / sr
    body = bandpass(rng.standard_normal(n), 250, 1800 * rng.uniform(0.9, 1.1), 2, sr)
    e = (1 - np.exp(-t / 0.002)) * np.exp(-t / (0.03 * rng.uniform(0.85, 1.2)))
    grit = bandpass(rng.standard_normal(n), 2500, 5500, 2, sr) * np.exp(-t / 0.015) * 0.3
    x = weight * (body * e + grit)
    return fade(lowpass(x, 6000, 4, sr), 0.0005, 0.02, sr)


def click(rng, freq=3000.0, dur=0.05, reverse=False, sr=SR):
    """Short filtered click in the UI lane (place); reverse=True gives the erase variant."""
    n = int(dur * sr)
    t = np.arange(n) / sr
    x = bandpass(rng.standard_normal(n), freq * 0.7, freq * 1.3, 2, sr) * np.exp(-t / (dur / 6))
    x += 0.5 * np.sin(2 * np.pi * freq * t) * np.exp(-t / (dur / 8))
    x = fade(x, 0.0005, 0.005, sr)
    return x[::-1].copy() if reverse else x


# ---------------------------------------------------------------- IO
def write_wav(path, x, sr=SR):
    """16-bit PCM WAV; mono for (n,), stereo for (n, 2). Clips at +-1."""
    x = np.asarray(x, float)
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.clip(np.round(x * 32767), -32768, 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(2 if x.ndim == 2 else 1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(pcm.tobytes())
    return path


def read_wav(path, mono=False):
    """-> (array float in [-1, 1], sample rate). Stereo comes back (n, 2) unless mono=True."""
    with wave.open(str(path)) as w:
        ch, sr = w.getnchannels(), w.getframerate()
        if w.getsampwidth() != 2:
            raise ValueError(f"{path}: only 16-bit WAV is supported")
        x = np.frombuffer(w.readframes(w.getnframes()), "<i2").astype(float) / 32768
    x = x.reshape(-1, ch)
    return (x.mean(1) if mono or ch == 1 else x), sr


def decode(path, sr=SR, channels=1):
    """Decode any audio file with ffmpeg -> float array ((n,) mono or (n, channels))."""
    r = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", str(channels), "-ar", str(sr),
                        "-"], capture_output=True, check=True)
    x = np.frombuffer(r.stdout, "<f4").astype(float)
    return x if channels == 1 else x.reshape(-1, channels)


def encode_ogg(wav_path, ogg_path, quality=5, stereo=False):
    """Ogg Vorbis via ffmpeg libvorbis at 48 kHz. Loops: keep the WAV out of assets/ (build/ is fine)."""
    Path(ogg_path).parent.mkdir(parents=True, exist_ok=True)
    cmd = ["ffmpeg", "-y", "-v", "error", "-i", str(wav_path), "-c:a", "libvorbis", "-q:a", str(quality),
           "-ar", "48000", "-map_metadata", "-1", "-fflags", "+bitexact", "-flags:a", "+bitexact"]
    if stereo:
        cmd += ["-ac", "2"]
    subprocess.run(cmd + [str(ogg_path)], check=True)
    return Path(ogg_path)


# ---------------------------------------------------------------- measurement
def true_peak_db(x, sr=SR):
    """BS.1770-style true peak (4x oversampled), dBTP, over all channels."""
    y = signal.resample_poly(np.asarray(x, float), 4, 1, axis=0)
    return 20 * np.log10(max(float(np.max(np.abs(y))), 1e-12))


def k_weight(x):
    """BS.1770 K-weighting at 48 kHz."""
    x = signal.lfilter([1.53512485958697, -2.69169618940638, 1.19839281085285],
                       [1.0, -1.69065929318241, 0.73248077421585], x, axis=0)
    return signal.lfilter([1.0, -2.0, 1.0], [1.0, -1.99004745483398, 0.99007225036621], x, axis=0)


def momentary_max_lufs(x, sr=SR):
    """Loudest 400 ms block (100 ms hop), ungated. Sounds shorter than 400 ms are zero-padded to 400 ms."""
    x = np.asarray(x, float)
    y = k_weight(np.pad(x, [(0, max(0, int(0.4 * sr) - len(x)))] + [(0, 0)] * (x.ndim - 1)))
    if y.ndim == 1:
        y = y[:, None]
    B, H = int(0.4 * sr), int(0.1 * sr)
    ms = [np.mean(y[i:i + B] ** 2, axis=0).sum() for i in range(0, len(y) - B + 1, H)]
    return -0.691 + 10 * np.log10(max(ms) + 1e-12)


def ebur128(path):
    """ffmpeg ebur128: integrated LUFS, loudness range and true peak of a file."""
    r = subprocess.run(["ffmpeg", "-nostats", "-i", str(path), "-af", "ebur128=peak=true:framelog=quiet",
                        "-f", "null", "-"], capture_output=True, text=True)
    s = r.stderr[r.stderr.rfind("Summary:"):]

    def g(p):
        m = re.search(p, s)
        return float(m.group(1)) if m else None
    return {"lufs_i": g(r"I:\s+(-?[\d.]+) LUFS"), "lra": g(r"LRA:\s+([\d.]+) LU"),
            "true_peak_dbtp": g(r"Peak:\s+(-?[\d.]+) dBFS")}


def check(path, max_true_peak=-1.0):
    """Measure a WAV or Ogg and list problems against docs/asset-pipeline.md (format, peaks under -1 dBFS)."""
    path = Path(path)
    probs = []
    info = {"file": str(path)}
    if path.suffix == ".wav":
        x, sr = read_wav(path)
        with wave.open(str(path)) as w:
            info.update(channels=w.getnchannels(), rate=sr, bits=8 * w.getsampwidth())
        if sr != SR:
            probs.append(f"sample rate {sr}, want 48000")
    else:
        r = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "stream=codec_name,sample_rate,channels",
                            "-of", "json", str(path)], capture_output=True, text=True, check=True)
        st = json.loads(r.stdout)["streams"][0]
        info.update(codec=st["codec_name"], channels=st["channels"], rate=int(st["sample_rate"]))
        if info["rate"] != SR:
            probs.append(f"sample rate {info['rate']}, want 48000")
        x = decode(path, channels=st["channels"])
        sr = SR
    info["duration_s"] = round(len(x) / sr, 3)
    info["sample_peak_dbfs"] = round(peak_db(x), 2)
    info["true_peak_dbtp"] = round(true_peak_db(x, sr), 2)
    info["momentary_max_lufs"] = round(momentary_max_lufs(x, sr), 1)
    e = ebur128(path)
    info["lufs_i"] = e["lufs_i"]
    info["lufs_i_note"] = "-70 means shorter than the gate; use momentary_max_lufs" if (e["lufs_i"] or 0) <= -69 else ""
    info["ffmpeg_true_peak_dbtp"] = e["true_peak_dbtp"]
    tp = max(v for v in (info["true_peak_dbtp"], e["true_peak_dbtp"]) if v is not None)
    if tp > max_true_peak:
        probs.append(f"true peak {tp:.2f} dBTP is above {max_true_peak} dBTP")
    first = np.abs(np.atleast_2d(x.T).T[0]).max()
    last = np.abs(np.atleast_2d(x.T).T[-1]).max()
    if path.suffix == ".wav" and (first > 0.02 or last > 0.02):
        probs.append(f"starts/ends away from zero ({first:.3f} / {last:.3f}): click risk (fade, or check the loop)")
    info["problems"] = probs
    return info


def estimate_f0(x, lo, hi, sr=SR):
    """Strongest frequency in [lo, hi] Hz (zero-padded FFT + parabolic interpolation). Check tuning with it."""
    x = np.asarray(x, float)
    if x.ndim == 2:
        x = x.mean(1)
    n = 1 << 20
    X = np.abs(np.fft.rfft(x * np.hanning(len(x)), n))
    f = np.fft.rfftfreq(n, 1 / sr)
    k = int(np.argmax(np.where((f >= lo) & (f <= hi), X, 0)))
    a, b, c = np.log(X[k - 1:k + 2] + 1e-12)
    return float(f[k] + 0.5 * (a - c) / (a - 2 * b + c) * (f[1] - f[0]))


# ---------------------------------------------------------------- spectrogram (numpy + common.png, no matplotlib)
def _ramp(v):
    """0..1 -> RGB along Ink, Night tint, Brick, Reed and thatch, Paper (a palette 'magma')."""
    from . import palette as P
    stops = np.array([P.rgb(n) for n in ("Ink", "Night tint", "Brick", "Reed and thatch", "Paper")], float)
    pos = np.clip(v, 0, 1) * (len(stops) - 1)
    i = np.minimum(pos.astype(int), len(stops) - 2)
    f = (pos - i)[..., None]
    return stops[i] * (1 - f) + stops[i + 1] * f


def spectrogram_png(src, png_path, sr=SR, title="", fmax=12000, nfft=1024, db_range=90, width=800, height=360,
                    lanes=True):
    """Waveform strip (top) + log-magnitude spectrogram (linear Hz, 0..fmax, bottom) as a PNG.
    Grid: thin lines every 1 kHz, Ink ticks at the left for 70/300 Hz and 1/3/6 kHz lane edges (lanes=True).
    `title` is not drawn (no font); it only names the file in messages."""
    from .png import write_png
    if isinstance(src, (str, Path)):
        x, sr = read_wav(src, mono=True) if str(src).endswith(".wav") else (decode(src), SR)
    else:
        x = np.asarray(src, float)
        x = x.mean(1) if x.ndim == 2 else x
    wave_h = height // 4
    spec_h = height - wave_h - 2
    # waveform: min/max per column
    img = np.empty((height, width, 3), float)
    img[:] = (242, 234, 216)
    cols = np.array_split(x, width) if len(x) >= width else [x[i:i + 1] for i in range(len(x))]
    mid = wave_h // 2
    for c, seg in enumerate(cols[:width]):
        if len(seg) == 0:
            continue
        lo, hi = float(seg.min()), float(seg.max())
        y0, y1 = int(mid - hi * (mid - 1)), int(mid - lo * (mid - 1))
        img[max(y0, 0):min(y1 + 1, wave_h), c] = (43, 38, 32)
    img[wave_h:wave_h + 2] = (43, 38, 32)
    # spectrogram
    f, tt, S = signal.spectrogram(x, sr, nperseg=nfft, noverlap=nfft * 3 // 4, window="hann")
    Sdb = 10 * np.log10(S + 1e-12)
    v = (Sdb - (Sdb.max() - db_range)) / db_range
    keep = f <= fmax
    v, f = v[keep], f[keep]
    yi = np.clip(((1 - np.arange(spec_h) / (spec_h - 1)) * fmax / (f[1] - f[0])).astype(int), 0, len(f) - 1)
    xi = np.clip((np.arange(width) / width * v.shape[1]).astype(int), 0, v.shape[1] - 1)
    img[wave_h + 2:] = _ramp(v[np.ix_(yi, xi)])
    y_of = lambda hz: wave_h + 2 + int((1 - hz / fmax) * (spec_h - 1))     # noqa: E731
    for k in range(1000, int(fmax), 1000):
        img[y_of(k), ::4] = (169, 166, 156)
    if lanes:
        for hz in (70, 300, 1000, 3000, 6000):
            if hz < fmax:
                img[y_of(hz), :12] = (242, 234, 216)
    write_png(img, png_path)
    return Path(png_path)

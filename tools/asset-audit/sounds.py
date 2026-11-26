#!/usr/bin/env python3
"""Asset audit, sounds: for every file under assets/audio (wav, ogg) a 640 x 240 picture of its waveform and
spectrogram (DIR/sound-<name>.png), and DIR/sounds.md: file, length, peak, loudness (integrated LUFS after
ITU-R BS.1770: K-weighting, 400 ms blocks, -70 LUFS absolute and -10 LU relative gates), and where the game uses it
(the cue tables assets/audio/sfx/town-sounds.json and river-sounds.json, assets/audio/music/playlist.json, and the
lines of game/scripts that name it). Ogg files are decoded with ffmpeg. Writes DIR/sounds.jsonl too.
    python3 tools/asset-audit/sounds.py DIR
"""
import glob, io, json, os, re, subprocess, sys, wave
import numpy as np
from PIL import Image, ImageDraw
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PAPER, INK, RIVER = (0xF2, 0xEA, 0xD8), (0x2B, 0x26, 0x20), (0x5B, 0x7F, 0x95)


def load(path):
    if path.endswith(".wav"):
        data = open(path, "rb").read()
    else:
        data = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "wav", "-acodec", "pcm_s16le", "-"],
                              capture_output=True, check=True).stdout
    w = wave.open(io.BytesIO(data))
    n, ch, sw, sr = w.getnframes(), w.getnchannels(), w.getsampwidth(), w.getframerate()
    raw = w.readframes(n)
    if sw == 2:
        x = np.frombuffer(raw, "<i2").astype(np.float64) / 32768.0
    elif sw == 3:
        b = np.frombuffer(raw, np.uint8).reshape(-1, 3)
        x = ((b[:, 0].astype(np.int32) | (b[:, 1].astype(np.int32) << 8) | (b[:, 2].astype(np.int32) << 16)) << 8 >> 8) / 8388608.0
    else:
        x = np.frombuffer(raw, "<i4").astype(np.float64) / 2147483648.0
    if len(x) < ch:  # ffmpeg pipe header may report 0 frames
        x = np.frombuffer(data[44:], "<i2").astype(np.float64) / 32768.0
    x = x[: len(x) // ch * ch].reshape(-1, ch)
    return x, sr


def lufs(x, sr):
    # K-weighting (BS.1770-4) designed for any rate: high shelf then high pass.
    f0, g, q = 1681.974450955533, 3.999843853973347, 0.7071752369554196
    k = np.tan(np.pi * f0 / sr); vh = 10 ** (g / 20); vb = vh ** 0.4996667741545416
    a0 = 1 + k / q + k * k
    b1 = [(vh + vb * k / q + k * k) / a0, 2 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0]
    a1 = [1, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0]
    f0, q = 38.13547087602444, 0.5003270373238773
    k = np.tan(np.pi * f0 / sr)
    a2 = [1, 2 * (k * k - 1) / (1 + k / q + k * k), (1 - k / q + k * k) / (1 + k / q + k * k)]
    b2 = [1, -2, 1]
    y = signal.lfilter(b2, a2, signal.lfilter(b1, a1, x, axis=0), axis=0)
    blk, hop = int(0.4 * sr), int(0.1 * sr)
    if len(y) < blk:
        ms = np.mean(y ** 2, axis=0).sum()
        return -0.691 + 10 * np.log10(ms) if ms > 0 else float("-inf")
    ms = np.array([np.mean(y[i:i + blk] ** 2, axis=0).sum() for i in range(0, len(y) - blk + 1, hop)])
    l = -0.691 + 10 * np.log10(np.maximum(ms, 1e-12))
    ms = ms[l > -70]
    if len(ms) == 0:
        return float("-inf")
    rel = -0.691 + 10 * np.log10(ms.mean()) - 10
    ms = ms[-0.691 + 10 * np.log10(ms) > rel]
    return -0.691 + 10 * np.log10(ms.mean())


def picture(x, sr, name, info, out):
    W, H = 640, 240
    im = Image.new("RGB", (W, H), PAPER)
    d = ImageDraw.Draw(im)
    mono = x.mean(axis=1)
    # Waveform (top 90 px): min/max per column.
    top, h = 22, 84
    cols = np.array_split(mono, W) if len(mono) >= W else [mono]
    for i, c in enumerate(cols):
        if len(c):
            lo, hi = c.min(), c.max()
            d.line((i, top + h / 2 - hi * h / 2, i, top + h / 2 - lo * h / 2), fill=RIVER)
    d.line((0, top + h / 2, W, top + h / 2), fill=(200, 190, 170))
    # Spectrogram (bottom), log frequency 50 Hz .. 20 kHz, dB scale.
    f, t, s = signal.spectrogram(mono, sr, nperseg=1024, noverlap=768)
    s = 10 * np.log10(s + 1e-12)
    s = np.clip((s - (s.max() - 80)) / 80, 0, 1)
    sh = H - top - h - 6
    fy = np.geomspace(50, min(20000, sr / 2), sh)[::-1]
    rows = np.searchsorted(f, fy).clip(0, len(f) - 1)
    img = s[rows][:, np.linspace(0, s.shape[1] - 1, W).astype(int)] if s.shape[1] else np.zeros((sh, W))
    rgb = (np.array(PAPER)[None, None, :] * (1 - img[..., None]) + np.array(INK)[None, None, :] * img[..., None]).astype(np.uint8)
    im.paste(Image.fromarray(rgb), (0, top + h + 6))
    d.text((4, 4), f"{name}   {info}", fill=INK)
    d.text((W - 60, top + h + 8), "20 kHz", fill=RIVER)
    d.text((W - 60, H - 14), "50 Hz", fill=RIVER)
    im.save(out)


def uses(rel):
    """Cue-table entries and code lines that name the file (by file name or by its stem without -NN)."""
    base = os.path.basename(rel)
    stem = re.sub(r"(-\d\d)?\.(wav|ogg)$", "", base)
    found = []
    for tab in ("assets/audio/sfx/town-sounds.json", "assets/audio/sfx/river-sounds.json", "assets/audio/music/playlist.json"):
        p = os.path.join(ROOT, tab)
        if not os.path.exists(p):
            continue
        for i, line in enumerate(open(p), 1):
            if base in line:
                ev = ""
                found.append(f"{tab}:{i}")
                break
    for p in sorted(glob.glob(os.path.join(ROOT, "game/scripts/**/*.cs"), recursive=True)):
        for i, line in enumerate(open(p, encoding="utf-8"), 1):
            if base in line or f'"{stem}"' in line or f'"{stem}-' in line or f"/{stem}" in line or (stem.count("-") and f'{stem.rsplit("-", 1)[0]}-{{' in line):
                found.append(f"{os.path.relpath(p, ROOT)}:{i}")
                break
    return found


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    files = sorted(glob.glob(os.path.join(ROOT, "assets/audio/**/*.wav"), recursive=True) +
                   glob.glob(os.path.join(ROOT, "assets/audio/**/*.ogg"), recursive=True))
    rows = []
    stems = [os.path.splitext(os.path.basename(p))[0] for p in files]
    for p in files:
        rel = os.path.relpath(p, ROOT)
        name = os.path.splitext(os.path.basename(p))[0]
        if stems.count(name) > 1:  # theme-hamlet-draft is both .ogg and .wav
            name += "-" + p.rsplit(".", 1)[1]
        try:
            x, sr = load(p)
        except Exception as e:  # noqa: BLE001 - report and go on
            rows.append({"name": f"sound-{name}", "kind": "sound", "file": rel, "error": str(e)})
            continue
        peak = 20 * np.log10(max(np.abs(x).max(), 1e-9))
        loud = lufs(x, sr)
        length = len(x) / sr
        u = uses(rel)
        info = f"{length:.2f} s  {sr} Hz  {x.shape[1]} ch  peak {peak:.1f} dBFS  {loud:.1f} LUFS"
        picture(x, sr, name, info, os.path.join(out, f"sound-{name}.png"))
        rows.append({"name": f"sound-{name}", "kind": "sound", "file": rel, "length_s": round(length, 3), "rate": sr,
                     "channels": int(x.shape[1]), "peak_dbfs": round(float(peak), 1), "lufs": round(float(loud), 1), "used_in": u})
    with open(os.path.join(out, "sounds.jsonl"), "w") as fh:
        for r in rows:
            fh.write(json.dumps(r) + "\n")
    with open(os.path.join(out, "sounds.md"), "w") as fh:
        fh.write("# Sounds\n\nEvery file under `assets/audio`, measured by `tools/asset-audit/sounds.py`. Loudness is integrated "
                 "LUFS (BS.1770 gating); short one-shots under 0.4 s are measured over their whole length.\n\n")
        fh.write("| File | Length (s) | Peak (dBFS) | Loudness (LUFS) | Used in |\n| --- | --- | --- | --- | --- |\n")
        for r in rows:
            if "error" in r:
                fh.write(f"| `{r['file']}` | | | | could not be read: {r['error']} |\n")
                continue
            fh.write(f"| `{r['file']}` | {r['length_s']:.2f} | {r['peak_dbfs']:.1f} | {r['lufs']:.1f} | "
                     f"{', '.join('`' + x + '`' for x in r['used_in']) or '**not found in the cue tables or code**'} |\n")
    print(f"sounds: {len(rows)} files")


if __name__ == "__main__":
    main()

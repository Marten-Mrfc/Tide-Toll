"""Measure the music against the Hamlet theme (a check, not a generator: build-assets.sh skips `_` files).

    PYTHONPATH=assets/src python3 assets/src/audio/_check_music.py

Reads every piece in assets/audio/music/playlist.json (decoded Ogg plus its score JSON) and prints, beside the theme:
length, RMS and sample peak (dBFS), true peak, integrated loudness, spectral centroid and band shares, the loop
seam (step and 2nd difference at the join against the loop's own 99.9th percentile, and against the quietest 50 ms
block's median step, the noise floor), whether every scored note is in the stated mode, and the longest run of
melody notes (same intervals and same lengths) any two pieces share. Exits 1 on a failed rule.
"""
import json
import sys

import numpy as np

from common import ROOT, audio as A
from _music import PC, MODES

MUSIC = ROOT / "assets" / "audio" / "music"
MAX_SHARED = 4             # notes; a "short motif" (the theme's own bell motif is 4 notes)


def db(v):
    return 20 * np.log10(max(float(v), 1e-12))


def measure(ogg):
    x = A.decode(ogg, channels=2)
    m = x.mean(1)
    X = np.abs(np.fft.rfft(m)) ** 2
    f = np.fft.rfftfreq(len(m), 1 / A.SR)
    band = lambda lo, hi: float(X[(f >= lo) & (f < hi)].sum() / X.sum())
    d = np.abs(np.diff(m))
    w = int(0.05 * A.SR)
    near = np.abs(np.diff(np.concatenate([m[-w:], m[:w]])))
    floor = float(np.max(np.delete(near, w - 1)))          # largest ordinary step within 50 ms either side of the join
    d2 = np.abs(np.diff(np.concatenate([m[-3:], m[:3]]), 2))
    return {
        "seconds": len(m) / A.SR, "rms_db": db(np.sqrt(np.mean(x ** 2))), "peak_db": db(np.max(np.abs(x))),
        "tp_db": A.true_peak_db(x), "lufs_i": A.ebur128(ogg)["lufs_i"],
        "centroid_hz": float((f * X).sum() / X.sum()),
        "bands": [band(0, 70), band(70, 300), band(300, 3000), band(3000, 24000)],
        "join": abs(m[0] - m[-1]), "p999": float(np.percentile(d, 99.9)), "floor": floor,
        "d2_join": float(d2.max()), "d2_p999": float(np.percentile(np.abs(np.diff(m, 2)), 99.9)),
        "clipped": int(np.sum(np.abs(x) >= 0.999)),
    }


def mode_ok(score):
    final = score["key"]
    name = score["mode"].split()[0].lower()
    pcs = {(PC[final] + s) % 12 for s in MODES[name]}
    notes = [e["midi"] for e in score["pipe"] + score.get("counter_pipe", [])]
    bad = [n for n in notes if n % 12 not in pcs]
    return len(notes), bad


def shape(score):
    ev = score["pipe"]
    return [(ev[i + 1]["midi"] - ev[i]["midi"], ev[i]["beats"]) for i in range(len(ev) - 1)]


def longest_shared(a, b):
    """Longest common run of (interval, length) steps; a run of k steps is k+1 notes."""
    best = 0
    prev = [0] * (len(b) + 1)
    for i in range(1, len(a) + 1):
        cur = [0] * (len(b) + 1)
        for j in range(1, len(b) + 1):
            if a[i - 1] == b[j - 1]:
                cur[j] = prev[j - 1] + 1
                best = max(best, cur[j])
        prev = cur
    return best + 1 if best else 1


def main():
    pl = json.loads((MUSIC / "playlist.json").read_text())
    fails = []
    rows, scores = [], {}
    for p in pl["pieces"]:
        ogg = ROOT / p["file"]
        sc = json.loads((ROOT / p["score"]).read_text())
        scores[p["id"]] = (p, sc)
        r = measure(ogg)
        n, bad = mode_ok(sc)
        rows.append((p, r, n, bad))
    theme = rows[0][1]
    print(f"{'piece':28s} {'len s':>6s} {'RMS':>6s} {'dRMS':>5s} {'peak':>6s} {'TP':>6s} {'LUFS':>6s} {'cent':>5s} "
          f"{'<70':>4s} {'70-300':>6s} {'.3-3k':>5s} {'>3k':>4s}  seam join/near/p99.9  d2 join/p99.9  notes")
    for p, r, n, bad in rows:
        b = r["bands"]
        print(f"{p['id']:28s} {r['seconds']:6.2f} {r['rms_db']:6.1f} {r['rms_db'] - theme['rms_db']:+5.1f} "
              f"{r['peak_db']:6.1f} {r['tp_db']:6.1f} {r['lufs_i']:6.1f} {r['centroid_hz']:5.0f} {b[0]:4.0%} {b[1]:6.0%} "
              f"{b[2]:5.0%} {b[3]:4.0%}  {r['join']:.4f}/{r['floor']:.4f}/{r['p999']:.4f}  "
              f"{r['d2_join']:.4f}/{r['d2_p999']:.4f}  {n} in mode" + (f", OUT {bad}" if bad else ""))
        if bad:
            fails.append(f"{p['id']}: notes out of mode {bad}")
        if r["clipped"]:
            fails.append(f"{p['id']}: {r['clipped']} clipped samples")
        if p["role"] == "age1" and abs(r["lufs_i"] - theme["lufs_i"]) > 1.0:
            fails.append(f"{p['id']}: loudness {r['lufs_i']} vs theme {theme['lufs_i']}")
        if p["loops"] and (r["join"] > r["p999"] or r["join"] > r["floor"] or r["d2_join"] > r["d2_p999"]):
            fails.append(f"{p['id']}: seam rougher than the loop's own signal")
        lo, hi = (60, 100) if p["role"] == "age1" and p["id"] != pl["rotation"][0] else (30, 50) if p["role"] == "age-change" else (0, 1e9)
        if not lo <= r["seconds"] <= hi:
            fails.append(f"{p['id']}: length {r['seconds']:.1f} s outside {lo}-{hi}")
    print("\nlongest shared melody run (notes with the same intervals and lengths):")
    ids = list(scores)
    for i in range(len(ids)):
        for j in range(i + 1, len(ids)):
            k = longest_shared(shape(scores[ids[i]][1]), shape(scores[ids[j]][1]))
            derived = {ids[i], ids[j]} == {pl["rotation"][0], "music-theme-rising"}
            flag = " (the variation of the theme: shared on purpose)" if derived else ""
            print(f"  {ids[i]:28s} {ids[j]:28s} {k:3d}{flag}")
            if k > MAX_SHARED and not derived:
                fails.append(f"{ids[i]} and {ids[j]} share {k} notes")
    print("\nFAILED:\n  " + "\n  ".join(fails) if fails else "\nall music checks pass")
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main()

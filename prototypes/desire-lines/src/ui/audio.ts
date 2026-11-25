// Web Audio for the desire-lines toy. No files, no network, no dependencies.
// Copied from the audio round's proven module (desire-audio.ts, measured in
// OfflineAudioContext against the game's own recipes; see README "Sound") and
// adapted: it counts what it schedules for the page's tests (Stats), and says
// whether the theme is playing (for the note glyph, its visual twin).
// Ports the recipes in assets/src/audio (step-grass, step-dirt, water-ford, bell, place, erase, water-river-loop,
// theme-hamlet) to JS. Every cue is rendered ONCE into an AudioBuffer when create() runs (inside the Sound click);
// playing a cue is then BufferSource -> Gain -> StereoPanner -> bus. The pipe of the theme is live (one oscillator).
export type Surface = 'grass' | 'dirt' | 'gravel' | 'mud' | 'bridge';
/** One screen area (e.g. a 4x2 grid cell). The page keeps these objects and mutates them: no allocation per call. */
export interface Area { pan: number; walkers: number; surface: Surface; ford: number }
export interface Stats {
  alive: number; maxAlive: number; dropped: number; beats: number;
  /** Footstep voices scheduled, and the most in any one beat (the budget is 8). */
  steps: number; maxBeatSteps: number;
  /** Other cues played: tools, bells, fords, theme notes. */
  cues: number; bells: number; themeNotes: number;
  /** Whether a round of the theme is sounding now (not in one of its silences). */
  themePlaying: boolean;
}
export interface DesireAudio {
  readonly ctx: BaseAudioContext;
  setMuted(m: boolean): void; setVolume(v: number): void;
  ensureRunning(): void; hidden(h: boolean): void;
  footstep(s: Surface, pan: number, gain: number): void; ford(pan: number): void;
  bell(): void; place(): void; erase(): void; refuse(): void;
  river(level: number): void; rain(level: number): void; theme(on: boolean): void;
  beat(areas: Area[], n: number): void; stats(): Stats;
}
type F = Float32Array<ArrayBuffer>;
const SURF: Surface[] = ['grass', 'dirt', 'gravel', 'mud', 'bridge'];
// Loudness of each buffer (loudest 400 ms block, dB RMS, mono). Calibrated against the game's files (see report).
const LV = { grass: -24.7, dirt: -20.5, gravel: -27, mud: -24.7, bridge: -19, ford: -22.2, bell: -22.1, place: -17.9,
  erase: -24.3, refuse: -25.8, river: -33.2, rain: -35.7 };
const BPM = 64, BEAT = 60 / BPM, LOOK = 0.35;
// Theme score (theme-hamlet-draft-score.json, bars 1-8): bars split by |, "NOTE[:beats]" (default 1 beat).
const MELODY = 'D5 E5 G5|A5:3|B5 A5 G5|A5:2 E5|G5 A5 G5|E5:2 F5|E5 D5 E5|D5:3';
const DRUM: [number, number, number][] = [[5, 1, 0], [5, 3, 1], [6, 1, 0], [6, 3, 1], [7, 1, 0], [7, 3, 1], [8, 1, 0], [8, 2.5, 1]];
const LYRE: [number, number, number[]][] = [[1, 1, [50, 57]], [3, 1, [57]], [5, 1, [50, 57]], [7, 1, [57]]]; // midi D3 A3

// ---------------------------------------------------------------- offline DSP (runs once, in create())
function rng(seed: number) {
  let s = seed >>> 0;
  return () => { s = (s + 0x6D2B79F5) >>> 0; let t = s; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
}
function biq(x: F, type: 0 | 1, f: number, q: number, sr: number) { // 0 lowpass, 1 highpass (RBJ, in place)
  const w = 2 * Math.PI * Math.min(f, sr * 0.45) / sr, c = Math.cos(w), al = Math.sin(w) / (2 * q), a0 = 1 + al;
  const b0 = (type ? 1 + c : 1 - c) / 2 / a0, b1 = (type ? -(1 + c) : 1 - c) / a0, a1 = -2 * c / a0, a2 = (1 - al) / a0;
  let x1 = 0, x2 = 0, y1 = 0, y2 = 0;
  for (let i = 0; i < x.length; i++) {
    const v = x[i], y = b0 * v + b1 * x1 + b0 * x2 - a1 * y1 - a2 * y2;
    x2 = x1; x1 = v; y2 = y1; y1 = y; x[i] = y;
  }
  return x;
}
const lp = (x: F, f: number, sr: number, o = 2) => { biq(x, 0, f, 0.7071, sr); if (o > 2) { biq(x, 0, f, 0.7071, sr); } return x; };
const band = (x: F, lo: number, hi: number, sr: number) => { biq(x, 1, lo, 0.7071, sr); return biq(x, 0, hi, 0.7071, sr); };
function noise(n: number, r: () => number): F { const x = new Float32Array(n); for (let i = 0; i < n; i++) x[i] = r() * 2 - 1; return x; }
function add(dst: F, src: F, t: number, sr: number, g = 1) { const o = Math.round(t * sr); for (let i = 0; i < src.length && o + i < dst.length; i++) if (o + i >= 0) dst[o + i] += src[i] * g; }
function fade(x: F, a: number, b: number, sr: number) {
  const na = Math.floor(a * sr), nb = Math.floor(b * sr);
  for (let i = 0; i < na && i < x.length; i++) x[i] *= i / na;
  for (let i = 0; i < nb && i < x.length; i++) x[x.length - 1 - i] *= i / nb;
  return x;
}
function burst(r: () => number, sr: number, dur: number, lo: number, hi: number, tau: number, att = 0.0005): F {
  const x = band(noise(Math.floor(dur * sr), r), lo, hi, sr);
  for (let i = 0; i < x.length; i++) { const t = i / sr; x[i] *= Math.min(t / att, 1) * Math.exp(-t / tau); }
  return x;
}
function crackles(r: () => number, sr: number, dur: number, count: number, start: number, spread: number, lo: number, hi: number, len: number, g0: number, g1: number): F {
  const c = new Float32Array(Math.floor(dur * sr)), L = Math.floor(len * sr);
  for (let k = 0; k < count; k++) {
    const i = Math.floor((start + r() * spread) * sr), g = g0 + r() * (g1 - g0);
    if (i + L <= c.length) for (let j = 0; j < L; j++) c[i + j] += (r() * 2 - 1) * 0.5 * (1 - Math.cos(2 * Math.PI * (j + 0.5) / L)) * g;
  }
  return band(c, lo, hi, sr);
}
/** Sum of decaying sines by complex rotation (no Math.sin per sample): wood, bell, membrane partials. */
function modal(sr: number, dur: number, fr: number[], am: number[], tau: number[], r: () => number): F {
  const x = new Float32Array(Math.floor(dur * sr));
  for (let p = 0; p < fr.length; p++) {
    if (fr[p] >= sr / 2 - 500) continue;
    const w = 2 * Math.PI * fr[p] / sr, d = Math.exp(-1 / (tau[p] * sr)), cr = d * Math.cos(w), ci = d * Math.sin(w), ph = r() * 6.2832;
    let re = Math.cos(ph), im = Math.sin(ph);
    const n = Math.min(x.length, Math.ceil(tau[p] * sr * 12));
    for (let i = 0; i < n; i++) { x[i] += am[p] * im; const t = re * cr - im * ci; im = re * ci + im * cr; re = t; }
  }
  return x;
}
function bubble(sr: number, f0: number, dur: number, tau: number, rise: number): F {
  const x = new Float32Array(Math.floor(dur * sr)); let ph = 0;
  for (let i = 0; i < x.length; i++) { const t = i / sr; ph += 2 * Math.PI * f0 * (1 + rise * t / dur) / sr; x[i] = Math.sin(ph) * Math.min(t / 0.003, 1) * Math.exp(-t / tau); }
  return x;
}
/** Scale so the loudest 400 ms block (100 ms hop) has the given RMS in dBFS. */
function level(x: F, db: number, sr: number) {
  const B = Math.floor(0.4 * sr), H = Math.floor(0.1 * sr); let best = 0;
  for (let s = 0; s === 0 || s + B <= x.length; s += H) {
    let a = 0; const e = Math.min(x.length, s + B);
    for (let i = s; i < e; i++) a += x[i] * x[i];
    best = Math.max(best, a / B);
  }
  const g = Math.pow(10, db / 20) / Math.sqrt(best + 1e-20);
  for (let i = 0; i < x.length; i++) x[i] *= g;
  return x;
}
const midiHz = (m: number) => 440 * Math.pow(2, (m - 69) / 12);
const NOTE: Record<string, number> = { C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 };
const noteMidi = (s: string) => 12 * (+s[1] + 1) + NOTE[s[0]];

function stepBuf(kind: Surface, r: () => number, sr: number): F {
  let x: F;
  if (kind === 'grass') { // step-grass.py: heel swish, quieter toe swish, blade crackles, muted thud
    const dur = 0.32; x = new Float32Array(Math.floor(dur * sr));
    const lo = 1000 * (0.9 + 0.3 * r()), hi = 4600 * (0.85 + 0.25 * r()), toeT = 0.05 + 0.04 * r();
    add(x, burst(r, sr, 0.2, lo, hi, 0.045 * (0.85 + 0.35 * r()), 0.006), 0, sr);
    add(x, burst(r, sr, 0.2, lo * 1.15, hi, 0.035 * (0.8 + 0.4 * r()), 0.008), toeT, sr, 0.45 + 0.25 * r());
    const th = lp(noise(Math.floor(0.08 * sr), r), 180, sr); for (let i = 0; i < th.length; i++) th[i] *= Math.exp(-i / sr / 0.018);
    add(x, th, 0.002, sr, 0.35);
    add(x, crackles(r, sr, dur, 4 + Math.floor(r() * 5), 0.004, toeT + 0.08, 2200, 5200, 0.003, 0.2, 0.6), 0, sr, 0.9);
    lp(x, 5500, sr, 4); fade(x, 0.001, 0.03, sr);
  } else if (kind === 'dirt' || kind === 'mud') { // step-dirt.py: dry body, toe roll, scuff, grit
    const dur = 0.24; x = new Float32Array(Math.floor(dur * sr));
    const toeT = 0.045 + 0.03 * r(), mud = kind === 'mud';
    add(x, burst(r, sr, 0.12, 220, 1600 * (0.9 + 0.2 * r()), 0.022 * (mud ? 1.6 : 1) * (0.85 + 0.35 * r()), 0.002), 0, sr);
    add(x, burst(r, sr, 0.1, 400, 2200, 0.016 * (0.8 + 0.4 * r()), 0.002), toeT, sr, 0.4 + 0.2 * r());
    if (!mud) {
      add(x, burst(r, sr, 0.09, 2200, 5200, 0.025, 0.01), toeT + 0.004, sr, 0.18 + 0.12 * r());
      add(x, crackles(r, sr, dur, 3 + Math.floor(r() * 4), toeT, 0.07, 2500, 5500, 0.0015, 0.1, 0.3), 0, sr);
    } else { // invented: a squelch (rising low bubble) and a darker, longer body
      add(x, bubble(sr, 170 + 60 * r(), 0.09, 0.03, 0.9), toeT, sr, 0.5);
      lp(x, 1100, sr, 4);
    }
    lp(x, 5500, sr, 4); fade(x, 0.0008, 0.03, sr);
  } else if (kind === 'gravel') { // invented from the dirt recipe: a short shaker of many grains
    const dur = 0.26; x = new Float32Array(Math.floor(dur * sr));
    add(x, burst(r, sr, 0.1, 300, 1800, 0.02, 0.002), 0, sr, 0.5);
    add(x, crackles(r, sr, dur, 26 + Math.floor(r() * 12), 0.0, 0.14, 1800, 6000, 0.002, 0.2, 0.7), 0, sr);
    lp(x, 6000, sr, 4); fade(x, 0.0008, 0.04, sr);
  } else { // bridge: invented from place.py: a hollow plank knock (low modes) with a footfall noise tick
    const dur = 0.3, f0 = 150 * (0.95 + 0.1 * r());
    x = modal(sr, dur, [f0, f0 * 2.3, f0 * 4.1, f0 * 6.5], [1, 0.5, 0.25, 0.1], [0.07, 0.04, 0.02, 0.012], r);
    for (let i = 0; i < x.length; i++) x[i] *= Math.min(i / (0.0015 * sr), 1);
    add(x, burst(r, sr, 0.04, 500, 3000, 0.006, 0.001), 0, sr, 0.5); lp(x, 4500, sr, 4); fade(x, 0.0005, 0.05, sr);
  }
  return x;
}
function fordBuf(r: () => number, sr: number): F { // water-ford.py
  const dur = 0.75 + 0.2 * r(), x = new Float32Array(Math.floor(dur * sr));
  add(x, burst(r, sr, 0.12, 350, 2600, 0.03 * (0.8 + 0.4 * r()), 0.004), 0, sr);
  const n = Math.floor((dur - 0.02) * sr), s = band(noise(n, r), 180, 1500, sr), wob = 5 + 4 * r(), wph = r() * 6;
  for (let i = 0; i < n; i++) { const t = i / sr; s[i] *= Math.min(t / 0.02, 1) * Math.exp(-t / 0.14) * (1 + 0.5 * Math.sin(2 * Math.PI * wob * t + wph)); }
  fade(s, 0, 0.15, sr); add(x, s, 0.012, sr, 0.6 + 0.3 * r());
  for (let k = 5 + Math.floor(r() * 5); k > 0; k--) add(x, bubble(sr, 450 + 1150 * r(), 0.06, 0.008 + 0.012 * r(), 0.3 + 0.6 * r()), 0.02 + 0.38 * r(), sr, 0.08 + 0.14 * r());
  for (let k = 2 + Math.floor(r() * 2); k > 0; k--) add(x, bubble(sr, 900 + 1000 * r(), 0.05, 0.01, 0.8), 0.4 + (dur - 0.5) * r(), sr, 0.04 + 0.05 * r());
  lp(x, 4500, sr, 4); return fade(x, 0.002, 0.08, sr);
}
function bellBuf(r: () => number, sr: number, humMidi: number, force: number): F { // bell.py, 3 s gameplay strike (damp 0.55)
  const RA = [0.5, 1, 1.19, 1.5, 2, 2.52, 2.66, 3.01, 4.17], AM = [0.65, 1, 0.55, 0.3, 0.5, 0.22, 0.16, 0.1, 0.06], TA = [2.4, 1.5, 1.05, 0.8, 0.7, 0.35, 0.3, 0.22, 0.15];
  const prime = 2 * midiHz(humMidi), fr: number[] = [], am: number[] = [], ta: number[] = [];
  RA.forEach((ra, k) => { const split = (0.4 + 1.2 * r()) * (k ? 1 : 0.5);
    for (const [s, sh] of [[-0.5, 0.72], [0.5, 0.28]]) { fr.push(prime * ra + s * split); am.push(AM[k] * Math.pow(force, k / 3) * sh * (0.9 + 0.2 * r())); ta.push(TA[k] * 0.55 * (0.9 + 0.2 * r())); } });
  [[2150, 0.18], [2480, 0.12], [2790, 0.09]].forEach(([f, t]) => { fr.push(f * (0.985 + 0.03 * r())); am.push(0.5 * force); ta.push(t); });
  const x = modal(sr, 3, fr, am, ta, r);
  for (let i = 0; i < x.length; i++) x[i] *= Math.min(i / (0.003 * sr), 1);
  add(x, burst(r, sr, 0.03, 1800, 3000, 0.004, 0.0003), 0, sr, 0.35 * force); add(x, burst(r, sr, 0.06, 250, 900, 0.012, 0.0005), 0, sr, 0.3);
  lp(x, 5200, sr, 4); return fade(x, 0.0005, 0.6, sr);
}
function knockBuf(r: () => number, sr: number, f0: number, soft: number): F { // place.py (soft = lower, duller: refuse)
  const dur = 0.45, RA = [1, 2.71, 5.15, 8.5], x = modal(sr, dur, RA.map((q, i) => f0 * q * (i ? 0.98 + 0.04 * r() : 1)), [1, 0.45, 0.18, 0.06], [0.075, 0.035, 0.018, 0.009].map(t => t * (0.85 + 0.3 * r())), r);
  for (let i = 0; i < x.length; i++) x[i] *= Math.min(i / (0.0015 * sr), 1);
  add(x, burst(r, sr, 0.03, 600, 2500, 0.004, 0.0008), 0, sr, 0.25);
  add(x, crackles(r, sr, dur, 5 + Math.floor(r() * 4), 0.015, 0.16, 1800, 4200, 0.004, 0.05, 0.14), 0, sr);
  if (!soft) { const c = modal(sr, dur, [2300, 2900, 3500].map(f => f * (0.97 + 0.06 * r())), [0.55, 0.45, 0.3], [0.03, 0.022, 0.015], r); for (let i = 0; i < c.length; i++) x[i] += c[i] * Math.min(i / (0.0008 * sr), 1); }
  lp(x, soft ? 1800 : 4000, sr, 4); return fade(x, 0.0005, 0.12, sr);
}
function eraseBuf(r: () => number, sr: number): F { // erase.py: rising band-pass pull, one twig snap, rustle
  const dur = 0.28 + 0.04 * r(), x = new Float32Array(Math.floor(dur * sr)), pd = 0.12 + 0.04 * r(), n = Math.floor(pd * sr);
  const nz = noise(n, r), pull = new Float32Array(n), f0 = 500 + 300 * r(), f1 = 2200 + 1000 * r(), hop = Math.floor(0.01 * sr);
  let x1 = 0, x2 = 0, y1 = 0, y2 = 0;
  for (let k = 0; k < n; k += hop) {
    const fc = f0 * Math.pow(f1 / f0, k / n), w = 2 * Math.PI * fc / sr, q = 1.0, al = Math.sin(w) / (2 * q), a0 = 1 + al, b0 = al / a0, a1 = -2 * Math.cos(w) / a0, a2 = (1 - al) / a0;
    for (let i = k; i < Math.min(n, k + hop); i++) { const v = nz[i], y = b0 * v - b0 * x2 - a1 * y1 - a2 * y2; x2 = x1; x1 = v; y2 = y1; y1 = y; pull[i] = y; }
  }
  for (let i = 0; i < n; i++) { const u = i / n; pull[i] *= Math.min(u / 0.15, 1) * Math.pow(1 - u, 1.5); }
  add(x, pull, 0, sr, 1.6);
  const st = pd * (0.6 + 0.2 * r()), sn = burst(r, sr, 0.02, 1500, 5000, 0.0018, 0.0002), sm = modal(sr, 0.03, [1500 + 600 * r(), 3300 + 600 * r()], [0.35, 0.15], [0.008, 0.004], r);
  for (let i = 0; i < sn.length; i++) sn[i] = sn[i] * 0.9 + (i < sm.length ? sm[i] : 0);
  add(x, sn, st, sr); add(x, crackles(r, sr, dur - st, 7 + Math.floor(r() * 4), 0.005, 0.1, 1800, 5000, 0.004, 0.08, 0.25), st, sr);
  lp(x, 5000, sr, 4); return fade(x, 0.002, 0.05, sr);
}
/** Two-channel loop made seamless by an equal-power cross-fade of its own tail over its head (noise) plus exact-cycle sines. */
function loopFrom(gen: (n: number, ch: number) => F, n: number, ov: number): [F, F] {
  const out: [F, F] = [new Float32Array(n), new Float32Array(n)];
  for (let ch = 0; ch < 2; ch++) {
    const y = gen(n + ov, ch), o = out[ch];
    for (let i = 0; i < n; i++) o[i] = y[i];
    for (let i = 0; i < ov; i++) { const a = (i / ov) * Math.PI / 2; o[i] = y[i + n] * Math.cos(a) + y[i] * Math.sin(a); }
  }
  return out;
}
function riverLoop(sr: number): [F, F] { // water-river-loop.py: body, laps, plinks, drone (D2..D5), 8 s
  const N = Math.floor(8 * sr), r = rng(400), T = N / sr;
  const L = loopFrom((n, ch) => {
    const b = lp(band(noise(n, r), 110, 1400, sr), 1400, sr), lap = band(noise(n, r), 500, 3200, sr), x = new Float32Array(n);
    const ph = [r() * 6, r() * 6, r() * 6];
    for (let i = 0; i < n; i++) { const t = i / sr / T * 2 * Math.PI, br = 1 + 0.25 * (Math.sin(2 * t + ph[0]) + Math.sin(3 * t + ph[1]) + Math.sin(5 * t + ph[2])) / 3;
      const g = Math.max(0, 1 + (Math.sin(6 * t + ph[0]) + Math.sin(9 * t + ph[1]) + Math.sin(13 * t + ph[2])) / 3 - 0.9); x[i] = b[i] * 3 * br + lap[i] * 3 * Math.pow(g, 1.5) * 0.55; }
    return x;
  }, N, Math.floor(1.0 * sr), );
  [[38, 1], [45, 0.55], [50, 0.7], [57, 0.4], [62, 0.3], [69, 0.12], [74, 0.08]].forEach(([m, a]) => { // exact-cycle partials, 1/T apart per side
    for (let ch = 0; ch < 2; ch++) { const f = Math.round(midiHz(m) * T) / T + (ch ? 1 : -1) / T, w = 2 * Math.PI * f / sr, ph = r() * 6.28, o = L[ch];
      for (let i = 0; i < N; i++) o[i] += 0.22 * 1.2 * a * Math.sin(w * i + ph) * (1 + 0.3 * Math.sin(2 * Math.PI * i / N + 1)); }
  });
  for (let k = 0; k < 26; k++) { const b = bubble(sr, 500 + 1200 * r(), 0.07, 0.008 + 0.012 * r(), 0.3 + 0.6 * r()), t0 = Math.floor(r() * N), g = 0.25 + 0.45 * r(), p = (r() - 0.5) * 1.6;
    for (let i = 0; i < b.length; i++) { const j = (t0 + i) % N; L[0][j] += b[i] * g * 1.5 * (1 - p) / 2; L[1][j] += b[i] * g * 1.5 * (1 + p) / 2; } }
  return L;
}
function rainLoop(sr: number): [F, F] { // invented: hiss + dense tiny drops, 4 s
  const N = Math.floor(4 * sr), r = rng(900);
  const L = loopFrom((n) => { const x = band(noise(n, r), 700, 9000, sr); for (let k = 0; k < 400 * n / sr; k++) { const i = Math.floor(r() * (n - 300)), g = 0.3 + 1.2 * r() * r(); for (let j = 0; j < 40; j++) x[i + j] += (r() - 0.5) * g * Math.exp(-j / 8) * 0.8; } return lp(x, 9000, sr); }, N, Math.floor(0.5 * sr));
  return L;
}
function reverbIR(sr: number): F[] { // theme-hamlet.py reverb_ir: rt60 2 s, 18 ms predelay, darkening tail
  const n = Math.floor(2.4 * sr), r = rng(801), out: F[] = [];
  for (let ch = 0; ch < 2; ch++) {
    const x = new Float32Array(n); let z = 0;
    for (let i = Math.floor(0.018 * sr); i < n; i++) { const t = i / sr, k = 0.8 - 0.65 * Math.min(t / 1.5, 1); z += k * ((r() * 2 - 1) - z); x[i] = z * Math.exp(-6.9 * t / 2); }
    out.push(x);
  }
  return out;
}

// ---------------------------------------------------------------- the engine
export function create(ctx: BaseAudioContext, opts?: { bpm?: number; volume?: number }): DesireAudio {
  const RT = 'baseLatency' in ctx, sr = ctx.sampleRate, beatLen = opts?.bpm ? 60 / opts.bpm : BEAT, rnd = Math.random;
  const mk = (x: F, db: number) => { level(x, db, sr); const b = ctx.createBuffer(1, x.length, sr); b.getChannelData(0).set(x); return b; };
  const mk2 = (c: [F, F], db: number) => { const m = new Float32Array(c[0].length); for (let i = 0; i < m.length; i++) m[i] = (c[0][i] + c[1][i]) * 0.5; // loudness from the mid, same gain both sides
    const B = Math.floor(0.4 * sr); let best = 0; for (let s = 0; s + B <= m.length; s += Math.floor(0.1 * sr)) { let a = 0; for (let i = s; i < s + B; i++) a += m[i] * m[i]; best = Math.max(best, a / B); }
    const g = Math.pow(10, db / 20) / Math.sqrt(best), b = ctx.createBuffer(2, m.length, sr); for (let k = 0; k < 2; k++) { const d = b.getChannelData(k); for (let i = 0; i < d.length; i++) d[i] = c[k][i] * g; } return b; };
  // buffers (made once)
  const steps: Record<Surface, AudioBuffer[]> = { grass: [], dirt: [], gravel: [], mud: [], bridge: [] };
  SURF.forEach((s, si) => { const r = rng(100 + si * 10); for (let v = 0; v < 6; v++) steps[s].push(mk(stepBuf(s, r, sr), LV[s])); });
  // dense areas play one pre-mixed one-beat shuffle (5 steps) instead of four separate steps: one voice, not four
  const shuf: Record<Surface, AudioBuffer[]> = { grass: [], dirt: [], gravel: [], mud: [], bridge: [] }, sh = rng(77);
  SURF.forEach(s => { for (let v = 0; v < 3; v++) { const x = new Float32Array(Math.floor(beatLen * sr) + Math.floor(0.4 * sr)); for (let k = 0; k < 5; k++) { const b = steps[s][(sh() * 6) | 0]; add(x, b.getChannelData(0) as F, (k + sh() * 0.8) / 5 * beatLen, sr, 0.35 + 0.3 * sh()); }
    const b = ctx.createBuffer(1, x.length, sr); b.getChannelData(0).set(x); shuf[s].push(b); } });
  const fords = [0, 1, 2].map(i => mk(fordBuf(rng(300 + i), sr), LV.ford));
  const bells = [62, 64, 67, 69].map((m, i) => mk(bellBuf(rng(500 + i), sr, m, [1, 0.9, 1.05, 0.95][i]), LV.bell));
  const places = [62, 64, 67, 69, 71, 74].map((m, i) => mk(knockBuf(rng(600 + i), sr, midiHz(m), 0), LV.place - (m > 70 ? 1.5 : 0)));
  const erases = [0, 1, 2].map(i => mk(eraseBuf(rng(700 + i), sr), LV.erase));
  const refuses = [0, 1].map(i => mk(knockBuf(rng(650 + i), sr, midiHz(i ? 50 : 52), 1), LV.refuse)); // played twice, a step apart
  const mkPk = (x: F, pk: number) => { let m = 1e-9; for (let i = 0; i < x.length; i++) m = Math.max(m, Math.abs(x[i])); const g = pk / m, b = ctx.createBuffer(1, x.length, sr), d = b.getChannelData(0); for (let i = 0; i < x.length; i++) d[i] = x[i] * g; return b; };
  const lyres = [50, 57].map((m, i) => { const f = midiHz(m), N = Math.round(sr / f), x = new Float32Array(Math.floor(4 * sr)), r = rng(30 + i); // Karplus-Strong, dark pluck
    for (let k = 0; k < N + 1; k++) x[k] = r() * 2 - 1; lp(x.subarray(0, N + 1), 1500 + 9000 * 0.25, sr);
    for (let k = N + 1; k < x.length; k++) x[k] = 0.9985 * 0.5 * (x[k - N] + x[k - N - 1]);
    for (let k = 0; k < Math.floor(0.002 * sr); k++) x[k] *= k / (0.002 * sr); lp(x, 3000, sr); fade(x, 0.0005, 0.5, sr); return mkPk(x, 0.4); });
  const drums = [0, 1].map(i => { const r = rng(40 + i), dum = i === 0, x = modal(sr, 1.2, [1, 1.59, 2.14, 2.3].map(q => 82 * q), dum ? [1, 0.35, 0.18, 0.12] : [0.15, 0.25, 0.3, 0.25], dum ? [0.32, 0.12, 0.08, 0.06] : [0.08, 0.06, 0.05, 0.04], r);
    for (let k = 0; k < x.length; k++) x[k] *= Math.min(k / (0.002 * sr), 1);
    add(x, burst(r, sr, 0.06, dum ? 150 : 700, dum ? 900 : 3000, 0.01, 0.0008), 0, sr, dum ? 0.25 : 0.35); fade(x, 0.0005, 0.2, sr);
    if (!dum) for (let k = 0; k < x.length; k++) x[k] *= 0.45; return mkPk(x, dum ? 0.55 : 0.25); });
  let riverBuf: AudioBuffer | null = null, rainBuf: AudioBuffer | null = null;
  const noiseBuf = (() => { const b = ctx.createBuffer(1, Math.floor(2 * sr), sr); b.getChannelData(0).set(noise(b.length, rng(7))); return b; })();
  const pw = ctx.createPeriodicWave(new Float32Array([0, 0, 0, 0, 0]), new Float32Array([0, 1, 0.16, 0.09, 0.03]));

  // master chain: buses -> master gain -> limiter -> soft ceiling -> destination
  const sfx = ctx.createGain(), amb = ctx.createGain(), mus = ctx.createGain(), master = ctx.createGain();
  const stepTone = ctx.createBiquadFilter(); stepTone.type = 'lowpass'; stepTone.frequency.value = 12000; stepTone.Q.value = 0.5;
  const lim = ctx.createDynamicsCompressor(); lim.threshold.value = -4; lim.knee.value = 0; lim.ratio.value = 20; lim.attack.value = 0.002; lim.release.value = 0.18;
  const ceil = ctx.createWaveShaper(), curve = new Float32Array(2049); for (let i = 0; i < curve.length; i++) { const v = (i / 1024 - 1), a = Math.abs(v); curve[i] = Math.sign(v) * (a < 0.78 ? a : 0.78 + 0.11 * Math.tanh((a - 0.78) / 0.11)); } ceil.curve = curve;
  const conv = ctx.createConvolver(), wet = ctx.createGain(), rb = ctx.createBuffer(2, Math.floor(2.4 * sr), sr), ir = reverbIR(sr); rb.copyToChannel(ir[0], 0); rb.copyToChannel(ir[1], 1); conv.buffer = rb; wet.gain.value = 0.3;
  sfx.gain.value = 1; amb.gain.value = 1; mus.gain.value = 0.15; mus.connect(master); mus.connect(conv); conv.connect(wet); wet.connect(master);
  sfx.connect(master); amb.connect(master); stepTone.connect(sfx); master.connect(lim); lim.connect(ceil); ceil.connect(ctx.destination);
  let vol = opts?.volume ?? 0.8, muted = false, on = true;
  const setMaster = () => master.gain.setTargetAtTime(muted ? 0 : vol * vol, ctx.currentTime, 0.03);
  master.gain.value = vol * vol;

  // voices: ring of (start, end, class) for the budget and the "alive" statistics
  const vs = new Float64Array(64), ve = new Float64Array(64), vc = new Uint8Array(64); let vh = 0;
  const CAP = [16, 4, 4, 8]; // steps, ui cues, ford+bell, theme notes
  const st: Stats = { alive: 0, maxAlive: 0, dropped: 0, beats: 0, steps: 0, maxBeatSteps: 0, cues: 0, bells: 0, themeNotes: 0, themePlaying: false };
  let beatSteps = 0;
  function count(t: number, cls: number) { let n = 0; for (let i = 0; i < 64; i++) if (vs[i] <= t && ve[i] > t && (cls < 0 || vc[i] === cls)) n++; return n; }
  function claim(t: number, end: number, cls: number) {
    if (count(t, cls) >= CAP[cls]) { st.dropped++; return false; }
    vs[vh] = t; ve[vh] = end; vc[vh] = cls; vh = (vh + 1) & 63;
    st.alive = count(t, -1) + (riverNode ? 1 : 0) + (rainNode ? 1 : 0); if (st.alive > st.maxAlive) st.maxAlive = st.alive; return true;
  }
  function ended(this: AudioScheduledSourceNode) { this.disconnect(); }
  function play(buf: AudioBuffer, bus: AudioNode, t: number, gain: number, pan: number, rate: number, cls: number) {
    if (!on || muted) return;
    t = Math.max(t, ctx.currentTime); if (!claim(t, t + buf.duration / rate, cls)) return;
    st.cues++; if (cls === 3) st.themeNotes++;
    const s = ctx.createBufferSource(), g = ctx.createGain(), p = ctx.createStereoPanner();
    s.buffer = buf; s.playbackRate.value = rate; g.gain.value = gain; p.pan.value = pan < -1 ? -1 : pan > 1 ? 1 : pan;
    s.connect(g); g.connect(p); p.connect(bus); s.onended = ended; s.start(t);
  }
  // footstep lanes: 7 pans x 5 gains of persistent Gain->StereoPanner chains, so a step voice is ONE BufferSource
  const GL = [0.25, 0.35, 0.5, 0.7, 1], lanes: GainNode[] = [];
  for (let p = 0; p < 7; p++) for (let q = 0; q < 5; q++) { const g = ctx.createGain(), pn = ctx.createStereoPanner(); g.gain.value = GL[q]; pn.pan.value = p / 3 - 1; g.connect(pn); pn.connect(stepTone); lanes.push(g); }
  function stepAt(buf: AudioBuffer, t: number, gain: number, pan: number, rate: number) {
    if (!on || muted) return;
    t = Math.max(t, ctx.currentTime); if (!claim(t, t + buf.duration / rate, 0)) return;
    st.steps++; beatSteps++;
    let q = 0; while (q < 4 && gain > Math.sqrt(GL[q] * GL[q + 1])) q++;
    const s = ctx.createBufferSource(); s.buffer = buf; s.playbackRate.value = rate; s.connect(lanes[Math.round((pan < -1 ? -1 : pan > 1 ? 1 : pan) * 3 + 3) * 5 + q]); s.onended = ended; s.start(t);
  }
  // beds
  let riverNode: AudioBufferSourceNode | null = null, riverG: GainNode | null = null, rainNode: AudioBufferSourceNode | null = null, rainG: GainNode | null = null, rainF: BiquadFilterNode | null = null;
  let rainLevel = 0, rainOffAt = 0, riverOffAt = 0;
  function bed(buf: AudioBuffer, bus: AudioNode, g: GainNode, f?: BiquadFilterNode) {
    const s = ctx.createBufferSource(); s.buffer = buf; s.loop = true; s.connect(f || g); if (f) f.connect(g); g.connect(bus); s.start(ctx.currentTime, rnd() * buf.duration); return s;
  }
  // theme: events merged and sorted once. kind 0 pipe(midi,beats) 1 lyre(idx) 2 drum(idx)
  const ev: { t: number; k: number; a: number; d: number }[] = [];
  MELODY.split('|').forEach((bar, b) => { let beat = 0; bar.split(' ').forEach(tok => { const [nm, bt] = tok.split(':'), d = bt ? +bt : 1; ev.push({ t: (b * 3 + beat) * BEAT + (b + beat === 0 ? 0.08 : 0), k: 0, a: noteMidi(nm), d: d * BEAT * 0.93, }); beat += d; }); });
  LYRE.forEach(([bar, beat, ns]) => ns.forEach((m, k) => ev.push({ t: ((bar - 1) * 3 + beat - 1) * BEAT + 0.12 + 0.045 * k, k: 1, a: m === 50 ? 0 : 1, d: 0 })));
  DRUM.forEach(([bar, beat, s]) => ev.push({ t: ((bar - 1) * 3 + beat - 1) * BEAT, k: 2, a: s, d: 0 }));
  ev.sort((a, b) => a.t - b.t);
  let themeOn = false, themeT0 = 0, themeI = 0;
  function pipe(f: number, t: number, dur: number, vel: number) {
    if (!claim(t, t + dur + 0.5, 3)) return;
    st.cues++; st.themeNotes++;
    const o = ctx.createOscillator(), v = ctx.createOscillator(), vg = ctx.createGain(), n = ctx.createBufferSource(), bp = ctx.createBiquadFilter(), ng = ctx.createGain(), e = ctx.createGain();
    o.setPeriodicWave(pw); o.frequency.setValueAtTime(f * 0.994, t); o.frequency.setTargetAtTime(f, t, 0.035);
    v.frequency.value = 4.6 + 0.6 * rnd(); vg.gain.setValueAtTime(0, t); vg.gain.setValueAtTime(0, t + 0.35); vg.gain.linearRampToValueAtTime(f * 0.0028, t + 0.85); v.connect(vg); vg.connect(o.frequency);
    n.buffer = noiseBuf; n.loop = true; bp.type = 'bandpass'; bp.frequency.value = f; bp.Q.value = 3; ng.gain.value = 0.22; n.connect(bp); bp.connect(ng); ng.connect(e); o.connect(e);
    e.gain.setValueAtTime(0, t); e.gain.linearRampToValueAtTime(vel, t + 0.05); e.gain.setTargetAtTime(vel * 0.8, t + 0.05, 0.12); e.gain.setTargetAtTime(0.0001, t + dur, 0.05);
    const pn = ctx.createStereoPanner(); pn.pan.value = -0.12; e.connect(pn); pn.connect(mus);
    const end = t + dur + 0.5; o.start(t); v.start(t); n.start(t, rnd() * 1.5); o.stop(end); v.stop(end); n.stop(end);
    o.onended = () => { e.disconnect(); pn.disconnect(); };
  }
  const roundEnd = 8 * 3 * BEAT + 3.5; // the tune and its ring-out
  function pumpTheme(now: number) {
    st.themePlaying = now >= themeT0 && now < themeT0 + roundEnd;
    while (themeT0 + ev[themeI].t < now + 1.2) {
      const E = ev[themeI], t = themeT0 + E.t + (rnd() - 0.5) * 0.024;
      if (t > now) { if (E.k === 0) pipe(midiHz(E.a), t, E.d, E.t % BEAT < 0.2 && Math.round(E.t / BEAT) % 3 === 0 ? 0.9 : 0.75); else if (E.k === 1) play(lyres[E.a], mus, t, E.a ? 0.8 : 1, 0.35, 1, 3); else play(drums[E.a], mus, t, 1, 0.2, 1, 3); }
      if (++themeI === ev.length) { themeI = 0; themeT0 += 8 * 3 * BEAT + 3.5 + 12 + 14 * rnd(); } // 22.5 s tune, 3.5 s ring-out, 12-26 s of silence
    }
  }
  // scheduler: one pass per beat; at most 12 step voices a beat, however many walkers
  let nextBeat = 0;
  const clamp1 = (v: number) => v < -1 ? -1 : v > 1 ? 1 : v;
  function ensureRunning() { if (RT && ctx.state !== "running") (ctx as AudioContext).resume(); }
  const api: DesireAudio = {
    ctx,
    setMuted(m) { muted = m; setMaster(); if (m) { if (RT) setTimeout(() => { if (muted) (ctx as AudioContext).suspend(); }, 200); } else ensureRunning(); },
    setVolume(v) { vol = v < 0 ? 0 : v > 1 ? 1 : v; setMaster(); },
    ensureRunning,
    hidden(h) { if (!RT || ctx.state === 'closed') return; if (h) (ctx as AudioContext).suspend(); else if (!muted) ensureRunning(); },
    footstep(s, pan, gain) { stepAt(steps[s][(rnd() * 6) | 0], ctx.currentTime, gain, pan, 0.94 + 0.12 * rnd()); },
    ford(pan) { play(fords[(rnd() * 3) | 0], sfx, ctx.currentTime, 0.9, pan, 0.96 + 0.08 * rnd(), 2); },
    bell() { st.bells++; play(bells[(rnd() * 4) | 0], sfx, ctx.currentTime, 1, 0, 1, 2); },
    place() { play(places[(rnd() * 6) | 0], sfx, ctx.currentTime, 1, 0, 1, 1); },
    erase() { play(erases[(rnd() * 3) | 0], sfx, ctx.currentTime, 1, 0, 1, 1); },
    refuse() { const t = ctx.currentTime; play(refuses[0], sfx, t, 1, 0, 1, 1); play(refuses[1], sfx, t + 0.11, 0.9, 0, 1, 1); },
    river(level) {
      const l = level < 0 ? 0 : level > 1 ? 1 : level, now = ctx.currentTime;
      if (l > 0 && !riverNode) { riverBuf = riverBuf || mk2(riverLoop(sr), LV.river); riverG = ctx.createGain(); riverG.gain.value = 0; riverNode = bed(riverBuf, amb, riverG); }
      if (riverG) { riverG.gain.setTargetAtTime(l * l, now, 0.5); riverOffAt = l > 0 ? 0 : now + 4; }
    },
    rain(level) {
      const l = level < 0 ? 0 : level > 1 ? 1 : level, now = ctx.currentTime; rainLevel = l;
      if (l > 0 && !rainNode) { rainBuf = rainBuf || mk2(rainLoop(sr), LV.rain); rainG = ctx.createGain(); rainG.gain.value = 0; rainF = ctx.createBiquadFilter(); rainF.type = 'lowpass'; rainNode = bed(rainBuf, amb, rainG, rainF); }
      if (rainG && rainF) { rainG.gain.setTargetAtTime(l * l, now, 0.4); rainF.frequency.setTargetAtTime(2500 + 6500 * l, now, 0.4); rainOffAt = l > 0 ? 0 : now + 3; }
      stepTone.frequency.setTargetAtTime(12000 - 4500 * l, now, 0.4);
    },
    theme(o) { themeOn = o; st.themePlaying = false; if (o) { themeT0 = ctx.currentTime + 0.5; themeI = 0; } },
    beat(areas, n) {
      if (!on || muted) return;
      const now = ctx.currentTime;
      if (themeOn) pumpTheme(now);
      if (now < nextBeat - LOOK) return;
      if (rainOffAt && now > rainOffAt && rainNode) { rainNode.stop(); rainNode.disconnect(); rainNode = null; rainOffAt = 0; }
      if (riverOffAt && now > riverOffAt && riverNode) { riverNode.stop(); riverNode.disconnect(); riverNode = null; riverOffAt = 0; }
      const t0 = nextBeat < now ? now + 0.03 : nextBeat; nextBeat = t0 + beatLen; st.beats++;
      beatSteps = 0;
      let want = 0;
      for (let i = 0; i < n; i++) { const w = areas[i].walkers; if (w > 0) want += w < 12 ? Math.min(w < 3 ? 1 : 2, 1.2 * Math.sqrt(w)) : 1; }
      const scale = want > 8 ? 8 / want : 1;
      for (let i = 0; i < n; i++) {
        const a = areas[i], w = a.walkers;
        if (w > 0) {
          const dense = w >= 12, sub = w < 3 ? 1 : 2, p = dense ? Math.min(1, scale) : Math.min(1, Math.min(sub, 1.2 * Math.sqrt(w)) * scale / sub);
          const surf: Surface = a.surface === 'dirt' && rainLevel > 0.35 ? 'mud' : a.surface; // rain turns dirt to mud
          const swing = surf === 'mud' ? 0.12 : 0, g = (0.27 + 0.06 * Math.log2(1 + w)) * (surf === 'grass' ? 1 - 0.3 * rainLevel : 1);
          if (dense) { if (rnd() < p) stepAt(shuf[surf][(rnd() * 3) | 0], t0, g * 0.8, a.pan, 1); }
          else for (let j = 0; j < sub; j++) if (rnd() < p) stepAt(steps[surf][(rnd() * 6) | 0], t0 + (j + (j & 1 ? swing : 0)) / sub * beatLen + (rnd() - 0.5) * 0.024, g * (0.85 + 0.3 * rnd()), a.pan + (rnd() - 0.5) * 0.3, 0.94 + 0.12 * rnd());
        }
        if (a.ford > 0 && rnd() < Math.min(1, 0.4 * Math.sqrt(a.ford))) play(fords[(rnd() * 3) | 0], sfx, t0 + rnd() * beatLen, 0.8, clamp1(a.pan), 0.96 + 0.08 * rnd(), 2);
      }
      if (beatSteps > st.maxBeatSteps) st.maxBeatSteps = beatSteps;
    },
    stats() { return st; },
  };
  return api;
}

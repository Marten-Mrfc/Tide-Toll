// Sound for the page, made in code (audio.ts). It is OFF until the player
// presses the Sound button (or M): no AudioContext exists before that press,
// and nothing about sound is remembered across reloads. Once on:
// - Footsteps: every BEAT_MS the walkers out on the map are counted in a
//   4 x 2 grid of map areas (pan from the area's centre), with the ground most
//   of them stand on (grass, worn earth, gravel, mud, the bridge deck) and how
//   many are in the ford. audio.ts turns those counts into at most 8 step
//   voices a beat, never one sound per villager.
// - Beds: the river (always on the map) and rain (with the weather's rain level).
// - A bell at dawn each in-game day (at most one every BELL_GAP_MS).
// - Place, erase and refused cues for the tools; the theme, quiet, with gaps.
// It pauses with the game and when the tab is hidden. Each sound has a visual
// twin on screen: walkers (steps), the river and the ford, the rain streaks,
// the day in the village panel (bell), the marks where a tool acts, and the
// note glyph on the Sound button while the theme plays.
// Per frame with sound off: one null check. With sound on: no allocation; the
// areas are 8 objects made once and refilled.
import { World } from "../sim/world";
import { PARAMS } from "../sim/params";
import { N } from "../sim/grid";
import { create, DesireAudio, Area, Surface, Stats } from "./audio";

const BEAT_MS = 120; // how often the counts are refreshed (audio.ts acts once per musical beat)
const BELL_GAP_MS = 6000; // dawn bells closer than this (fast speeds) are skipped
const COLS = 4, ROWS = 2;
const SURFS: Surface[] = ["grass", "dirt", "gravel", "mud", "bridge"];

export interface SoundState {
  on: boolean; created: boolean; ctxState: string; volume: number; muted: boolean; work: number; stats: Stats | null; areas: Area[];
}

export class Sound {
  private au: DesireAudio | null = null;
  private on = false;
  private muted = true; // what the engine was last told
  private volume = 0.7;
  private paused = false;
  private areas: Area[] = [];
  private counts = new Uint16Array(COLS * ROWS * SURFS.length);
  private lastBeat = -1e9;
  private lastDay = -1;
  private lastBell = -1e9;
  private lastRain = -1;
  /** Frames on which the sound did any work (0 while it is off: the tests check that). */
  work = 0;

  constructor(private button: HTMLButtonElement, private slider: HTMLInputElement, private note: HTMLElement) {
    for (let i = 0; i < COLS * ROWS; i++) this.areas.push({ pan: ((i % COLS) + 0.5) / COLS * 1.6 - 0.8, walkers: 0, surface: "grass", ford: 0 });
    button.addEventListener("click", () => this.toggle());
    slider.addEventListener("input", () => { this.volume = Number(slider.value) / 100; this.au?.setVolume(this.volume); });
    slider.value = String(Math.round(this.volume * 100));
    document.addEventListener("visibilitychange", () => {
      if (!this.au) return;
      this.au.hidden(document.hidden);
      this.sync();
    });
    // A context can be suspended or interrupted (iOS); a press is the moment it may resume.
    window.addEventListener("pointerdown", () => { if (this.au && this.on && !this.paused) this.au.ensureRunning(); }, true);
  }

  /** Turns sound on or off. The first time on, the AudioContext is made here, inside the press. */
  toggle() {
    this.on = !this.on;
    if (this.on && !this.au) {
      type AC = typeof AudioContext;
      const Ctor = (window.AudioContext || (window as unknown as { webkitAudioContext?: AC }).webkitAudioContext) as AC | undefined;
      if (!Ctor) { this.on = false; this.button.title = "Sound is not available in this browser"; this.render(); return; }
      this.au = create(new Ctor(), { volume: this.volume });
      this.muted = false;
      this.au.ensureRunning();
      this.au.river(0.5);
      this.au.theme(true);
      this.lastRain = -1;
    }
    this.sync();
    this.render();
  }

  get isOn() { return this.on; }

  /** The game's pause: sound pauses with it. */
  setPaused(p: boolean) { this.paused = p; this.sync(); }

  /** A tool cue. */
  cue(kind: "place" | "erase" | "refuse") {
    const au = this.au;
    if (!au || this.muted) return;
    au.ensureRunning();
    if (kind === "place") au.place(); else if (kind === "erase") au.erase(); else au.refuse();
  }

  /** A new world: the day count starts again. */
  reset() { this.lastDay = -1; }

  /** Call once a frame. With sound off this returns at once. */
  update(now: number, w: World) {
    const au = this.au;
    if (!au || this.muted) return;
    this.work++;
    // Dawn bell: a new in-game day.
    const day = Math.floor(w.time / PARAMS.daySimSec);
    if (day !== this.lastDay) {
      if (this.lastDay >= 0 && day > this.lastDay && now - this.lastBell >= BELL_GAP_MS) { au.bell(); this.lastBell = now; }
      this.lastDay = day;
    }
    const rain = w.rainOn ? w.weather.rain : 0;
    if (Math.abs(rain - this.lastRain) > 0.02 || (rain === 0 && this.lastRain > 0)) { au.rain(rain); this.lastRain = rain; }
    if (now - this.lastBeat >= BEAT_MS) {
      this.lastBeat = now;
      this.count(w);
    }
    au.beat(this.areas, this.areas.length);
    this.note.hidden = !au.stats().themePlaying;
  }

  /** Counts moving walkers per area, the ground under them, and those in the ford. */
  private count(w: World) {
    const counts = this.counts, areas = this.areas, S = SURFS.length;
    counts.fill(0);
    for (let i = 0; i < areas.length; i++) { areas[i].walkers = 0; areas[i].ford = 0; }
    const m = w.map, thr = PARAMS.stateThresholds[1];
    for (const k of w.walkers) {
      if (!w.isVisible(k)) continue;
      const c = k.cell, x = c % N, y = (c / N) | 0;
      const a = Math.min(ROWS - 1, (y * ROWS / N) | 0) * COLS + Math.min(COLS - 1, (x * COLS / N) | 0);
      if (m.ford[c]) { areas[a].ford++; continue; }
      const s = w.bridgeAt[c] >= 0 ? 4 : w.gravel[c] ? 2 : w.mud[c] ? 3 : w.wear[c] >= thr ? 1 : 0;
      counts[a * S + s]++;
      areas[a].walkers++;
    }
    for (let a = 0; a < areas.length; a++) {
      let best = 0;
      for (let s = 1; s < S; s++) if (counts[a * S + s] > counts[a * S + best]) best = s;
      areas[a].surface = SURFS[best];
    }
  }

  /** Tells the engine whether to sound: on, not paused and the tab visible. */
  private sync() {
    const au = this.au;
    if (!au) return;
    const mute = !this.on || this.paused || document.hidden;
    if (mute !== this.muted) {
      this.muted = mute;
      // Days that passed while it was silent ring no bell: the next frame only notes the day.
      if (!mute) this.lastDay = -1;
      au.setMuted(mute);
      if (mute) this.note.hidden = true;
    }
  }

  private render() {
    this.button.setAttribute("aria-pressed", String(this.on));
    this.slider.parentElement!.hidden = !this.on;
    if (!this.on) this.note.hidden = true;
  }

  state(): SoundState {
    const au = this.au;
    return { on: this.on, created: !!au, ctxState: au ? (au.ctx as AudioContext).state : "none", volume: this.volume, muted: this.muted, work: this.work,
      stats: au ? { ...au.stats() } : null, areas: this.areas.map(a => ({ ...a })) };
  }
}

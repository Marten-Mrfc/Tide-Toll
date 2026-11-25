// Rain and mud. The weather has its own random stream, seeded from the map seed,
// so rolling for rain never moves the walkers' RNG: a rainless seed, or a run
// with rain switched off, consumes nothing from it. Rain is rolled once at the
// start of every in-game day (two numbers a day: the chance and the strength).
//
// State the page reads (all plain numbers, updated every tick):
// - rain: 0..1, how hard it is raining now (fades in and out; for drawing and sound)
// - wetness: 0..1, how wet the ground is; mud costs and slows in proportion to it
// - mud (on World): 1 on cells that are mud now (worn earth, not gravel, not a bridge or the ford)
import { PARAMS } from "./params";
import { mulberry32, Rng } from "./rng";

export class Weather {
  /** How hard it is raining now, 0..1 (fades over rainRampSimSec). */
  rain = 0;
  /** How wet the ground is, 0..1: 1 while raining and through the mud days, falling to 0 as it dries. */
  wetness = 0;
  /** Whether it is raining now (the rain day, or a forced shower). */
  raining = false;
  /** Strength of the current shower, 0.5..1 (drawing and sound only). */
  strength = 0;
  /** Sim time the current or last shower started and stops. */
  rainStart = -1e9;
  rainEnd = -1e9;
  /** Sim time the mud is gone (dry ground). */
  mudUntil = -1e9;
  /** Day index (0-based) last rolled. */
  private day = -1;
  private rng: Rng;
  /** Rain days so far (rolled or forced), for tests and the page. */
  rainDays = 0;

  constructor(seed: number) {
    this.rng = mulberry32(((seed * 2654435761) ^ 0x5eed) >>> 0);
  }

  /** Advances the weather to sim time t. Returns true when a shower started this tick. */
  update(t: number): boolean {
    const P = PARAMS;
    const day = Math.floor(t / P.daySimSec);
    let started = false;
    while (this.day < day) {
      this.day++;
      const season = Math.floor((this.day * P.daySimSec) / P.seasonSimSec) % 4;
      const roll = this.rng(), strength = 0.5 + 0.5 * this.rng();
      if (this.day === day && roll < P.rainChance[season]) {
        const start = this.day * P.daySimSec;
        this.begin(start, start + P.daySimSec, start + P.mudDays[season] * P.daySimSec, strength);
        started = true;
      }
    }
    this.raining = t >= this.rainStart && t < this.rainEnd;
    const ramp = P.rainRampSimSec;
    this.rain = this.raining ? this.strength * Math.min(1, (t - this.rainStart) / ramp, (this.rainEnd - t) / ramp) : 0;
    if (t >= this.mudUntil) this.wetness = 0;
    else {
      const up = Math.min(1, Math.max(0, (t - this.rainStart) / P.wetUpSimSec));
      const dry = Math.min(1, Math.max(0, (this.mudUntil - t) / P.drySimSec));
      // A new shower on wet ground starts from the wetness the ground still has.
      this.wetness = Math.max(this.carried * (1 - up), Math.min(up, dry), this.raining ? up : 0);
    }
    return started;
  }
  /** Wetness left over from earlier rain when a new shower began. */
  private carried = 0;

  private begin(start: number, end: number, mudUntil: number, strength: number) {
    this.carried = this.wetness;
    this.rainStart = start;
    this.rainEnd = end;
    this.mudUntil = Math.max(this.mudUntil, mudUntil);
    this.strength = strength;
    this.rainDays++;
  }

  /**
   * Starts rain now (debug and tests), for `simSec` seconds, with mud lasting
   * as a rain day of this season would after it stops. Does not touch the
   * weather's random stream, so the daily rolls that follow are unchanged.
   */
  startRain(t: number, simSec = PARAMS.daySimSec, strength = 1) {
    const P = PARAMS, season = Math.floor(t / P.seasonSimSec) % 4;
    this.begin(t, t + simSec, t + simSec + (P.mudDays[season] - 1) * P.daySimSec, strength);
    this.update(t);
  }

  /** Stops the rain now; with `dry` the mud goes too (otherwise it lasts its days out). */
  stopRain(t: number, dry = false) {
    if (this.rainEnd > t) this.rainEnd = t;
    if (dry) this.mudUntil = t;
    this.update(t);
  }
}

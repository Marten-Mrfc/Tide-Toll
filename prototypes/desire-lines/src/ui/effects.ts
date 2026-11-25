// Things drawn every frame over the map, all cheap and allocation-free per frame:
// - Rain: slanted streaks from one pre-drawn tile (a pattern) holding a near
//   and a fainter far layer, so a frame fills the map once; faded in with the
//   weather's rain level, still while the game is paused. Nothing is drawn or
//   computed for rain while it is not raining.
// - New houses: a short "raising" moment (the cottage rises out of its garden
//   plot with a puff of dust), then rings that draw the eye and a small badge
//   that stays a while, so the player notices where the town grew.
// - Marks of the player's actions, the visual twins of the tool sounds: a
//   ring that pops out where something was placed, a dust puff where
//   something was removed, a red shake where an action was refused.
import { World } from "../sim/world";
import { Palette } from "./palette";
import type { Renderer } from "./render";

const RAISE_MS = 1100; // a new house rises over this long (real time)
const RING_MS = 3200; // then rings pulse from it
const BADGE_MS = 15000; // and a badge marks it this long
const PULSE_MS = 650; // action marks
const PULSES = 16;
const TILE = 112; // rain tile, CSS px

export type PulseKind = 0 | 1 | 2; // place, erase, refuse

export class Effects {
  private pal: Palette | null = null;
  /** Real time each grown house was first seen (index as world.grown). */
  private seen: number[] = [];
  private now = 0;
  private hiddenIds = new Set<number>();
  // Rain.
  private tile: HTMLCanvasElement | null = null;
  private pattern: CanvasPattern | null = null;
  private tileD = 0;
  private tileColour = "";
  private rainClock = 0;
  private lastNow = 0;
  // Action marks: a ring buffer of x, y (cells), kind, start time.
  private px = new Float32Array(PULSES);
  private py = new Float32Array(PULSES);
  private pk = new Uint8Array(PULSES);
  private pt = new Float64Array(PULSES).fill(-1e9);
  private ph = 0;

  setPalette(p: Palette) { this.pal = p; this.tileColour = ""; }
  reset() { this.seen = []; this.hiddenIds.clear(); this.pt.fill(-1e9); }

  /** Notes new houses and returns how many grown houses stand (are no longer rising). */
  standing(w: World, now: number): number {
    this.now = now;
    const g = w.grown;
    while (this.seen.length < g.length) this.seen.push(now);
    let n = 0;
    while (n < g.length && now - this.seen[n] >= RAISE_MS) n++;
    return n;
  }

  /** Building ids of houses still being raised (the static layer leaves them out). */
  raising(w: World): Set<number> {
    const h = this.hiddenIds;
    h.clear();
    for (let i = 0; i < w.grown.length; i++) if (this.now - (this.seen[i] ?? this.now) < RAISE_MS) h.add(w.grown[i].building);
    return h;
  }

  isGrown(w: World, id: number): boolean {
    for (const h of w.grown) if (h.building === id) return true;
    return false;
  }

  /** How many houses were raised in the last BADGE_MS (for the panel). */
  recent(now: number): number {
    let n = 0;
    for (const t of this.seen) if (now - t < BADGE_MS) n++;
    return n;
  }

  /** A mark for an action at (x, y) in cells. */
  pulse(kind: PulseKind, x: number, y: number, now: number) {
    const i = this.ph;
    this.px[i] = x; this.py[i] = y; this.pk[i] = kind; this.pt[i] = now;
    this.ph = (i + 1) % PULSES;
  }

  draw(ctx: CanvasRenderingContext2D, r: Renderer, now: number) {
    const w = r.world, p = this.pal!.css, s = r.scale, d = r.pen.dpr;
    if (!r.paused) this.rainClock += Math.min(100, Math.max(0, now - this.lastNow));
    this.lastNow = now;

    // New houses.
    const g = w.grown, B = w.buildings;
    for (let i = g.length - 1; i >= 0; i--) {
      const age = now - (this.seen[i] ?? now);
      if (age >= RAISE_MS + BADGE_MS) break; // older ones are done (they are in build order)
      const h = g[i], b = B[h.building], cx = (h.x + h.w / 2) * s, cy = (h.y + h.h / 2) * s;
      if (age < RAISE_MS) {
        const t = age / RAISE_MS;
        r.drawBuilding(ctx, b, true, easeOut(t));
        // Dust at the foot of the walls.
        ctx.fillStyle = p.trampled;
        ctx.globalAlpha = 0.7 * (1 - t);
        for (let k = 0; k < 5; k++) {
          const a = k * 1.2566 + t * 0.8, rr = (0.9 + 1.3 * t) * s;
          ctx.beginPath();
          ctx.arc(cx + Math.cos(a) * rr, cy + 0.6 * s + Math.sin(a) * rr * 0.4, (0.35 + 0.3 * t) * s, 0, 6.2832);
          ctx.fill();
        }
        ctx.globalAlpha = 1;
      } else {
        const a2 = age - RAISE_MS;
        ctx.strokeStyle = p.new;
        if (a2 < RING_MS) {
          // Two rings a beat apart, spreading and fading.
          for (let k = 0; k < 2; k++) {
            const t = (a2 - k * 500) / (RING_MS - 500);
            if (t <= 0 || t >= 1) continue;
            ctx.globalAlpha = 0.9 * (1 - t);
            ctx.lineWidth = Math.max(1.5 * d, s * 0.25) * (1 - 0.5 * t);
            ctx.beginPath();
            ctx.arc(cx, cy, (2 + 7 * easeOut(t)) * s, 0, 6.2832);
            ctx.stroke();
          }
        }
        // The badge: a small filled circle with a halo at the plot's corner, fading at the end.
        const fade = Math.min(1, (RAISE_MS + BADGE_MS - age) / 1500);
        const bx = (h.x + h.w - 0.1) * s, by = (h.y + 0.1) * s, br = Math.max(2.5 * d, s * 0.45);
        ctx.globalAlpha = fade;
        ctx.fillStyle = p.halo;
        ctx.beginPath(); ctx.arc(bx, by, br + 1.5 * d, 0, 6.2832); ctx.fill();
        ctx.fillStyle = p.new;
        ctx.beginPath(); ctx.arc(bx, by, br, 0, 6.2832); ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    // Action marks.
    for (let i = 0; i < PULSES; i++) {
      const age = now - this.pt[i];
      if (age < 0 || age >= PULSE_MS) continue;
      const t = age / PULSE_MS, x = this.px[i] * s, y = this.py[i] * s, k = this.pk[i];
      if (k === 0) {
        ctx.strokeStyle = p.ink;
        ctx.globalAlpha = 0.8 * (1 - t);
        ctx.lineWidth = Math.max(1.5 * d, s * 0.2);
        ctx.beginPath(); ctx.arc(x, y, (0.6 + 2.2 * easeOut(t)) * s, 0, 6.2832); ctx.stroke();
      } else if (k === 1) {
        ctx.fillStyle = p.trampled;
        ctx.globalAlpha = 0.75 * (1 - t);
        for (let j = 0; j < 4; j++) {
          const a = j * 1.5708 + 0.6, rr = (0.4 + 1.6 * easeOut(t)) * s;
          ctx.beginPath(); ctx.arc(x + Math.cos(a) * rr, y + Math.sin(a) * rr, (0.5 + 0.4 * t) * s, 0, 6.2832); ctx.fill();
        }
      } else {
        // A short shake of a red cross.
        ctx.globalAlpha = 1 - t;
        r.cross(this.px[i] + Math.sin(t * 40) * 0.35 * (1 - t), this.py[i], 0.8);
      }
      ctx.globalAlpha = 1;
    }

    // Rain on top: light streaks, darker ground is drawn with the ground layer.
    const rain = w.rainOn ? w.weather.rain : 0;
    if (rain > 0.01) {
      const pat = this.rainPattern(ctx, d, p.rain);
      if (pat) {
        const W = r.canvas.width, T = TILE * d, c = this.rainClock;
        ctx.fillStyle = pat;
        const ox = (c * 0.09 * d) % T, oy = (c * 0.62 * d) % T;
        ctx.globalAlpha = 0.6 * rain;
        ctx.translate(ox, oy);
        ctx.fillRect(-ox, -oy, W, W);
        ctx.translate(-ox, -oy);
        ctx.globalAlpha = 1;
      }
    }
  }

  /** The rain tile: short slanted streaks, drawn once per scale and theme. */
  private rainPattern(ctx: CanvasRenderingContext2D, d: number, colour: string): CanvasPattern | null {
    if (d === this.tileD && colour === this.tileColour) return this.pattern;
    this.tileD = d; this.tileColour = colour;
    const T = Math.round(TILE * d), t = this.tile ?? document.createElement("canvas");
    this.tile = t;
    t.width = t.height = T;
    const g = t.getContext("2d")!;
    g.clearRect(0, 0, T, T);
    g.strokeStyle = colour;
    g.lineCap = "round";
    let seed = 7;
    const rnd = () => { seed = (seed * 16807) % 2147483647; return seed / 2147483647; };
    // 26 near streaks, then 26 fainter, thinner, shorter far ones between them.
    for (let k = 0; k < 52; k++) {
      const far = k >= 26, x = rnd() * T, y = rnd() * T, len = (7 + 9 * rnd()) * d * (far ? 0.75 : 1);
      g.globalAlpha = (0.45 + 0.55 * rnd()) * (far ? 0.6 : 0.92);
      g.lineWidth = (0.8 + 0.6 * rnd()) * d * (far ? 0.8 : 1);
      for (const [ox, oy] of [[0, 0], [-T, 0], [0, -T], [-T, -T]]) {
        g.beginPath();
        g.moveTo(x + ox, y + oy);
        g.lineTo(x + ox + len * 0.18, y + oy + len);
        g.stroke();
      }
    }
    this.pattern = ctx.createPattern(t, "repeat");
    return this.pattern;
  }
}

function easeOut(t: number): number { return 1 - (1 - t) * (1 - t); }

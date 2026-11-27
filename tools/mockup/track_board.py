#!/usr/bin/env python3
"""Night 6 mock-up board 1: a boat's journey on the real river, now (the simulation's own track, tick by tick) beside
planned (the same visit through tools/mockup/boat_course.py), seen from straight above, in the game's palette.
    python3 tools/mockup/track_board.py TRACK_DIR MAIN_ID WAIT_ID OUT_DIR
TRACK_DIR holds boats.csv and river.json from tools/mockup/boat-track (seed 1)."""
import math, os, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(__file__))
import boat_course as bc

track, main_id, wait_id, out = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4]
os.makedirs(out, exist_ok=True)
SS = 3                                                 # drawn 3x, scaled down: smooth edges
H = lambda s: tuple(int(s[i:i + 2], 16) for i in (1, 3, 5))
PAPER, INK, MEADOW, EARTH, RIVER, THATCH, BRICK, SLATE, IRON, LIME = map(H, (
    "#F2EAD8", "#2B2620", "#7FA35B", "#B89568", "#5B7F95", "#C9A55A", "#A4553A", "#5E6B78", "#44604F", "#E9E2CF"))
BANK = (0x6E, 0x8F, 0x4E)
SHALLOW = (0x6F, 0x93, 0xA6)
TIMBER = (0x8A, 0x63, 0x3E)
UPC, DOWNC, WAITC = LIME, THATCH, BRICK                # coming up, going down, the boat that waited

rv, v_main = bc.load(track, main_id)
_, v_wait = bc.load(track, wait_id)
R = bc.River(rv)


def mix(a, b, u):
    return tuple(int(a[i] + (b[i] - a[i]) * u) for i in range(3))


class Panel:
    def __init__(self, x0, x1, y0, y1, w, h):
        self.x0, self.y0 = x0, y0
        self.k = min(w / (x1 - x0), h / (y1 - y0)) * SS
        self.W, self.H = w * SS, h * SS
        # centre the window
        self.ox = (self.W - (x1 - x0) * self.k) / 2
        self.oy = (self.H - (y1 - y0) * self.k) / 2
        self.im = Image.new("RGB", (self.W, self.H), MEADOW)
        self.d = ImageDraw.Draw(self.im)

    def P(self, x, y):
        return (self.ox + (x - self.x0) * self.k, self.oy + (y - self.y0) * self.k)

    def river(self):
        d, n = self.d, rv["size"]
        # banks: a darker strip of grass, then the water
        west = [self.P(rv["west"][y] - 0.35, y + 0.5) for y in range(n)]
        east = [self.P(rv["east"][y] + 0.35, y + 0.5) for y in range(n)]
        d.polygon(west + east[::-1], fill=BANK)
        west = [self.P(rv["west"][y], y + 0.5) for y in range(n)]
        east = [self.P(rv["east"][y], y + 0.5) for y in range(n)]
        d.polygon(west + east[::-1], fill=RIVER)
        lw = max(1, int(self.k * 0.06))
        d.line(west, fill=INK, width=lw); d.line(east, fill=INK, width=lw)

    def crossings(self):
        d = self.d; k = self.k
        for l in rv["links"]:
            kind = l["kind"]; y = l["py"][0]
            xa, xb = l["px"][0], l["px"][-1]
            if kind == "Ford":
                for x in [xa + 0.5 + i * 0.8 for i in range(int((xb - xa) / 0.8))]:
                    for dy in (-0.35, 0.35):
                        cx, cy = self.P(x + (0.2 if dy > 0 else 0), y + dy)
                        r = k * 0.22
                        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=SLATE, outline=INK, width=max(1, int(k * 0.04)))
            elif kind == "Ferry":
                d.line([self.P(xa - 0.5, y), self.P(xb + 0.5, y)], fill=INK, width=max(1, int(k * 0.05)))
                a, b = self.P(xa + 0.1, y - 0.7), self.P(xa + 1.6, y + 0.7)
                d.rectangle((a, b), fill=TIMBER, outline=INK, width=max(1, int(k * 0.05)))
            elif kind in ("Footbridge", "Drawbridge"):
                half = 0.55 if kind == "Footbridge" else 0.75
                a, b = self.P(xa - 0.3, y - half), self.P(xb + 0.3, y + half)
                d.rectangle((a, b), fill=TIMBER, outline=INK, width=max(1, int(k * 0.07)))
                for i in range(int(xb - xa) + 1):
                    p, q = self.P(xa + i, y - half), self.P(xa + i, y + half)
                    d.line([p, q], fill=INK, width=max(1, int(k * 0.03)))
                if kind == "Drawbridge":   # the two lifting frames
                    for xx in (xa - 0.1, xb + 0.1):
                        c = self.P(xx, y); r = k * 0.35
                        d.rectangle((c[0] - r, c[1] - r, c[0] + r, c[1] + r), fill=SLATE, outline=INK, width=max(1, int(k * 0.05)))

    def landing_now(self):
        d, k = self.d, self.k
        for p in rv["plots"]:
            if p["kind"] != "Landing": continue
            # the plot and today's short jetty out into the water at the berths' row
            a, b = self.P(p["x"], p["y"]), self.P(p["x"] + p["w"], p["y"] + p["h"])
            d.rectangle((a, b), fill=EARTH, outline=INK, width=max(1, int(k * 0.04)))
            w = rv["west"][p["y"]]
            a, b = self.P(w - 0.6, p["y"] + 0.4), self.P(w + 0.45, p["y"] + 1.6)
            d.rectangle((a, b), fill=TIMBER, outline=INK, width=max(1, int(k * 0.05)))

    def quay_planned(self):
        d, k = self.d, self.k
        by0, by1 = rv["berthY"][0] - 1.4, rv["berthY"][1] + 1.4
        w = R.west(rv["berthY"][0])
        land = next(p for p in rv["plots"] if p["kind"] == "Landing")
        # the yard behind the quay
        a, b = self.P(land["x"] - 0.3, by0), self.P(w - 0.4, by1)
        d.rectangle((a, b), fill=EARTH, outline=INK, width=max(1, int(k * 0.04)))
        # stacks by kind
        cols = [THATCH, LIME, PAPER, BRICK]
        for i, c in enumerate(cols):
            cx, cy = self.P(land["x"] + 0.6, by0 + 0.9 + i * 1.3); r = k * 0.42
            d.rounded_rectangle((cx - r, cy - r * 0.8, cx + r, cy + r * 0.8), radius=r * 0.3, fill=c, outline=INK, width=max(1, int(k * 0.04)))
        # the quay: timber on piles along the bank, two berths long
        a, b = self.P(w - 0.45, by0), self.P(w + 0.3, by1)
        d.rectangle((a, b), fill=TIMBER, outline=INK, width=max(1, int(k * 0.06)))
        y = by0
        while y < by1:
            p, q = self.P(w - 0.45, y), self.P(w + 0.3, y)
            d.line([p, q], fill=INK, width=max(1, int(k * 0.02))); y += 0.35
        for by in rv["berthY"]:
            c = self.P(w + 0.12, by - 0.7); r = k * 0.14
            d.ellipse((c[0] - r, c[1] - r, c[0] + r, c[1] + r), fill=INK)          # bollard
        # the crane between the berths, on its stone base
        cy = (rv["berthY"][0] + rv["berthY"][1]) / 2
        c = self.P(w - 0.2, cy); r = k * 0.45
        d.rectangle((c[0] - r, c[1] - r, c[0] + r, c[1] + r), fill=SLATE, outline=INK, width=max(1, int(k * 0.05)))
        e = self.P(w + 0.9, cy - 0.6)
        d.line([c, e], fill=TIMBER, width=max(2, int(k * 0.16)))
        d.line([c, e], fill=INK, width=max(1, int(k * 0.04)))
        # mooring posts in the lee of the near bank
        for slot in range(3):
            c = self.P(R.west(rv["waitY"][slot]) + 0.05, rv["waitY"][slot] - 0.9); r = k * 0.13
            d.ellipse((c[0] - r, c[1] - r, c[0] + r, c[1] + r), fill=TIMBER, outline=INK, width=max(1, int(k * 0.04)))

    def lanes(self):
        d, k = self.d, self.k
        for f, col in ((R.up, UPC), (R.down, DOWNC)):
            y = 30.0
            while y < rv["size"]:
                a, b = self.P(f(y), y), self.P(f(y + 0.6), y + 0.6)
                d.line([a, b], fill=mix(RIVER, col, 0.45), width=max(1, int(k * 0.05)))
                y += 1.4

    def boat(self, x, y, h, col, L, B, alpha=1.0):
        d = self.d
        c, s = math.cos(h), math.sin(h)
        pts = [(0.5, 0), (0.15, 0.5), (-0.38, 0.46), (-0.5, 0.28), (-0.5, -0.28), (-0.38, -0.46), (0.15, -0.5)]   # bow at +X
        poly = [self.P(x + (px * L) * c - (py * B) * s, y + (px * L) * s + (py * B) * c) for px, py in pts]
        fill = mix(RIVER, col, alpha)
        d.polygon(poly, fill=fill, outline=INK, width=max(1, int(self.k * 0.035)))
        if L > 1.2:   # a full-size boat: a thwart line so the bow end reads
            a = self.P(x + 0.1 * L * c - 0.4 * B * s, y + 0.1 * L * s + 0.4 * B * c)
            b = self.P(x + 0.1 * L * c + 0.4 * B * s, y + 0.1 * L * s - 0.4 * B * c)
            d.line([a, b], fill=INK, width=max(1, int(self.k * 0.03)))

    def ring(self, x, y, rad):
        c = self.P(x, y); r = rad * self.k
        w = max(2, int(self.k * 0.09))
        self.d.ellipse((c[0] - r, c[1] - r, c[0] + r, c[1] + r), outline=PAPER, width=w + 2 * SS)
        self.d.ellipse((c[0] - r, c[1] - r, c[0] + r, c[1] + r), outline=BRICK, width=w)

    def done(self):
        return self.im.resize((self.W // SS, self.H // SS), Image.LANCZOS)


def samples(visit, every, keep, lo=-1e9, hi=1e9):
    """Indices to draw: every `every` seconds, plus each tick where `keep(i)` (a jump or a snap)."""
    out, last = [], -1e9
    for i, tk in enumerate(visit):
        if not (lo <= tk["y"] <= hi): continue
        if tk["t"] - last >= every - 1e-6 or keep(i):
            out.append(i); last = tk["t"]
    return out


def keep_now(visit):
    def k(i):
        if i == 0 or i + 1 >= len(visit): return False
        a, b = visit[i - 1], visit[i]
        dh = abs((b["heading"] - a["heading"] + math.pi) % (2 * math.pi) - math.pi)
        return abs(b["x"] - a["x"]) > 0.3 or dh > 0.3
    return k


ROWBOAT = (2.1, 0.8)   # cells (4.2 m by 1.6 m in the game)


def draw_visit(pn, visit, poses, col, every, keep, lo, hi, size, still_every=1e9):
    """poses[i] = (x, y, heading). Boats lying still are drawn once every still_every seconds (they are one shape)."""
    idx = samples(visit, every, keep, lo, hi)
    # a boat lying still is drawn once, full size, at the middle of its stay
    runs, start = [], 0
    for i in range(1, len(visit) + 1):
        if i == len(visit) or visit[i]["phase"] != visit[start]["phase"]:
            if visit[start]["phase"] in ("Moored", "Waiting"): runs.append((start + i) // 2)
            start = i
    idx = sorted(set(j for j in idx if visit[j]["phase"] not in ("Moored", "Waiting")) | set(runs))
    drawn_still = {}
    for i in idx:
        ph = visit[i]["phase"]
        if ph in ("Moored", "Waiting"):
            key = (ph, round(poses[i][0], 1), round(poses[i][1], 1))
            if key in drawn_still and visit[i]["t"] - drawn_still[key] < still_every: continue
            drawn_still[key] = visit[i]["t"]
        x, y, h = poses[i]
        sz = ROWBOAT if ph in ("Moored", "Waiting") else size
        c = UPC if ph in ("Arriving",) else DOWNC if ph == "Leaving" else col
        if col == WAITC and ph != "Leaving": c = WAITC if ph == "Waiting" else mix(WAITC, UPC, 0.5)
        pn.boat(x, y, h, c, sz[0], sz[1], 0.95)


def now_poses(v):
    return [(t["x"], t["y"], t["heading"]) for t in v]


def planned_poses(v):
    res, _ = bc.follow(rv, v)
    return [(r[0], r[1], r[2]) for r in res]


def make(window, size, lo, hi, every, rings, planned):
    pn = Panel(*window)
    pn.river(); pn.crossings()
    if planned:
        pn.lanes(); pn.quay_planned()
    else:
        pn.landing_now()
    for v, col in ((v_wait, WAITC), (v_main, UPC)):
        poses = planned_poses(v) if planned else now_poses(v)
        keep = (lambda i: False) if planned else keep_now(v)
        draw_visit(pn, v, poses, col, every, keep, lo, hi, size)
    for (x, y, r) in rings:
        pn.ring(x, y, r)
    return pn.done()


# the four places, from the data: a bend's sideways jumps, the slide to the quay, the jump out, the about-turn
def find_places():
    v = v_main
    bend = next(t for t in v if t["phase"] == "Arriving" and 95 < t["y"] < 100)
    moor = next(i for i, t in enumerate(v) if t["phase"] == "Moored")
    slide = v[moor - 6]
    leave = next(i for i, t in enumerate(v) if t["phase"] == "Leaving")
    jump = v[leave]
    w = v_wait                                    # the about-turn: the boat that waited, leaving berth 0
    lw = next(i for i, t in enumerate(w) if t["phase"] == "Leaving")
    turn = w[lw]
    return (bend["x"], bend["y"]), (slide["x"], slide["y"]), (jump["x"], jump["y"]), (turn["x"], turn["y"])


bend, slide, jump, turn = find_places()
print("places", bend, slide, jump, turn)
full = (48, 78, 54, 130, 800, 1420)
rings_full = [(bend[0], bend[1], 2.6)]
near = (62, 78, 58.5, 80.5, 800, 1100)
rings_near = [(slide[0] - 0.1, slide[1] + 0.3, 1.3), (jump[0] - 0.5, jump[1] + 0.2, 1.0), (turn[0] - 0.4, turn[1] - 0.3, 1.25)]
SMALL = (0.75, 0.38)
MID = (0.85, 0.42)
panels = [
    ("The journey: now", (255, 255, 255), make(full, SMALL, -1e9, 1e9, 0.25, rings_full, False)),
    ("The journey: planned", (0xC9, 0xA5, 0x5A), make(full, SMALL, -1e9, 1e9, 0.25, [], True)),
    ("At the landing: now", (255, 255, 255), make(near, MID, 56, 82, 0.25, rings_near, False)),
    ("At the landing: planned", (0xC9, 0xA5, 0x5A), make(near, MID, 56, 82, 0.25, [], True)),
]
names = ["journey-now", "journey-planned", "landing-reach-now", "landing-reach-planned"]
for (lab, c, im), nm in zip(panels, names):
    im.save(os.path.join(out, f"{nm}.png"))

GAP, STRIP = 16, 50
BG = (0x28, 0x24, 0x1F)
FONT = ImageFont.truetype("/usr/share/fonts/opentype/inter/Inter-Bold.otf", 28)
W = GAP + 2 * (800 + GAP)
rowsH = [max(p[2].size[1] for p in panels[0:2]), max(p[2].size[1] for p in panels[2:4])]
Hh = GAP + sum(STRIP + h + GAP for h in rowsH)
b = Image.new("RGB", (W, Hh), BG); d = ImageDraw.Draw(b)
y = GAP
for r in range(2):
    for c in range(2):
        lab, col, im = panels[r * 2 + c]
        x = GAP + c * (800 + GAP)
        d.text((x + 4, y + STRIP // 2), lab, font=FONT, fill=col, anchor="lm")
        b.paste(im, (x, y + STRIP))
    y += STRIP + rowsH[r] + GAP
p = os.path.join(out, "boats-journey-now-and-planned.png")
b.save(p, optimize=True); print(p, b.size)

#!/usr/bin/env python3
"""Night 6 mock-up: a prototype of the designed boat course (docs/plans/n6/boats-design.md, part A and B).

It takes the simulation's own track of a boat's visit (tools/mockup/boat-track writes boats.csv and river.json from a
real town) and gives where the designed boat would be drawn at each of the same ticks. The simulation stays the
truth for WHEN and HOW FAR UP-RIVER a boat is; the prototype only decides where across the river it lies, its bow,
and an eased speed along its own path.

    python3 tools/mockup/boat_course.py TRACK_DIR BOAT_ID       # prints t, phase, sim x y, designed x y heading, lag

Units are map cells (1 cell = 2 m), y grows south (down-river: the stream runs south, boats come up from row 127).
The town's (near) bank is the west bank, x smaller. Headings are atan2(dy, dx) like the simulation's.

How it works, for the builder of the real feature:
1. The middle of the river is a smooth curve: CourseX per row, smoothed with a Gaussian over SMOOTH rows.
2. Two lanes: boats coming up keep UP_OFF to the near (landing) side of the middle, boats going down DOWN_OFF to the
   far side. By the mooring posts (the simulation's WaitY rows) the up lane moves toward the middle (PINCH) to pass the
   boats lying there in the lee of the near bank.
3. A visit is one path made of pieces: up the lane, the S in to the berth (or to a post, then post to berth), push-off,
   a turning arc to starboard (bow north, east, south: the far side is open water), down the far lane. Each point of
   the path carries the simulation's y it stands for, so the simulation's y at a tick gives a target distance along
   the path.
4. The drawn boat runs along the path toward that target with limited speed (VMAX, less in bends and the arc) and
   limited acceleration (ACCEL, BRAKE): it gathers way and loses it over about a boat's length and stops smoothly
   where the simulation stops dead (berth, post, below the drawbridge). It is never ahead of the simulation.
5. The bow follows the path's tangent, except in the push-off (it moves sideways, bow still up-river).
"""
import csv, json, math, sys

SMOOTH = 3.0        # rows, Gaussian sigma of the river's middle line
UP_OFF = 0.85       # cells from the middle toward the near bank: the up lane
DOWN_OFF = 0.95     # cells from the middle toward the far bank: the down lane
PINCH = 0.30        # the up lane's offset beside the mooring posts
POST_IN = 0.45      # a waiting boat's centre from the near bank's water edge
QUAY_IN = 0.75      # a moored boat's centre from the near bank's water edge (quay piles 0.35 into the water)
S_LEN = 4.5         # rows over which the S comes in from the lane to the quay
PUSH = 0.45         # cells pushed off sideways before the turn
VMAX = 3.6          # cells per second (the simulation sails at 3.0): room to catch up after a stop
VARC = 1.6          # cells per second in the turning arc
ACCEL, BRAKE = 2.5, 4.0  # cells per second per second
TURN = math.radians(100)  # the bow turns at most this fast (radians a second)


def smooth_course(cx):
    n = len(cx); out = []
    r = int(3 * SMOOTH)
    for y in range(n):
        s = w = 0.0
        for k in range(-r, r + 1):
            yy = min(n - 1, max(0, y + k))
            g = math.exp(-0.5 * (k / SMOOTH) ** 2)
            s += g * cx[yy]; w += g
        out.append(s / w)
    return out


def smoothstep(u):
    u = min(1.0, max(0.0, u))
    return u * u * (3 - 2 * u)


class River:
    def __init__(self, rv):
        self.rv = rv
        self.mid = smooth_course(rv["courseX"])
        self.n = rv["size"]
        self.wait = rv["waitY"]
        self.berth_y = rv["berthY"]

    def at(self, arr, y):  # value per row, row centres at row + 0.5
        f = min(self.n - 1.0, max(0.0, y - 0.5))
        i = int(f); j = min(self.n - 1, i + 1); u = f - i
        return arr[i] * (1 - u) + arr[j] * u

    def middle(self, y):
        return self.at(self.mid, y)

    def west(self, y):
        return self.at(self.rv["west"], y)

    def pinch(self, y):  # 1 beside the mooring posts, 0 away from them, smooth
        a, b = self.wait[0] - 1.5, self.wait[-1] + 1.5
        return smoothstep((y - (a - 2.5)) / 2.5) * (1 - smoothstep((y - b) / 2.5))

    def up(self, y):
        off = UP_OFF + (PINCH - UP_OFF) * self.pinch(y)
        return self.middle(y) - off

    def down(self, y):
        return self.middle(y) + DOWN_OFF

    def quay_x(self, k):
        return self.west(self.berth_y[k]) + QUAY_IN

    def post_x(self, slot):   # clear of the bank over the boat's whole length (about 2 cells)
        y = self.wait[slot]
        return max(self.west(y + d * 0.25) for d in range(-5, 6)) + POST_IN


class Path:
    """Points (x, y, simy, heading or None, vmax)."""
    def __init__(self):
        self.p = []

    def add(self, x, y, simy, head=None, vmax=VMAX):
        if self.p and abs(self.p[-1][0] - x) < 1e-9 and abs(self.p[-1][1] - y) < 1e-9:
            return
        self.p.append((x, y, simy, head, vmax))

    def finish(self):
        self.s = [0.0]
        for a, b in zip(self.p, self.p[1:]):
            self.s.append(self.s[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
        # curvature slow-down: lower vmax where the tangent turns
        out = []
        for i, q in enumerate(self.p):
            j0, j1 = max(0, i - 6), min(len(self.p) - 1, i + 6)
            h0 = math.atan2(self.p[min(j0 + 1, j1)][1] - self.p[j0][1], self.p[min(j0 + 1, j1)][0] - self.p[j0][0])
            h1 = math.atan2(self.p[j1][1] - self.p[max(j1 - 1, j0)][1], self.p[j1][0] - self.p[max(j1 - 1, j0)][0])
            dh = abs((h1 - h0 + math.pi) % (2 * math.pi) - math.pi)
            ds = max(1e-6, self.s[j1] - self.s[j0])
            k = dh / ds                     # radians per cell
            out.append((q[0], q[1], q[2], q[3], min(q[4], max(1.2, VMAX / (1 + 1.2 * k)))))
        self.p = out

    def target(self, i0, i1, simy):
        """Distance along the path where piece [i0, i1) stands for the simulation's y."""
        best = self.s[i0]
        for i in range(i0, i1 - 1):
            a, b = self.p[i][2], self.p[i + 1][2]
            lo, hi = min(a, b), max(a, b)
            if lo - 1e-9 <= simy <= hi + 1e-9 and hi > lo:
                u = (simy - a) / (b - a)
                return self.s[i] + u * (self.s[i + 1] - self.s[i])
        # outside: the nearer end
        if abs(simy - self.p[i1 - 1][2]) < abs(simy - self.p[i0][2]):
            return self.s[i1 - 1]
        return self.s[i0]

    def point(self, s):
        lo, hi = 0, len(self.s) - 1
        while hi - lo > 1:
            m = (lo + hi) // 2
            if self.s[m] <= s: lo = m
            else: hi = m
        a, b = self.p[lo], self.p[hi]
        L = self.s[hi] - self.s[lo]
        u = 0 if L <= 0 else (s - self.s[lo]) / L
        x = a[0] + (b[0] - a[0]) * u; y = a[1] + (b[1] - a[1]) * u
        if a[3] is not None and b[3] is not None:
            dh = (b[3] - a[3] + math.pi) % (2 * math.pi) - math.pi
            h = a[3] + dh * u
        else:
            i0, i1 = max(0, lo - 2), min(len(self.p) - 1, hi + 2)
            h = math.atan2(self.p[i1][1] - self.p[i0][1], self.p[i1][0] - self.p[i0][0])
        return x, y, h, a[4] + (b[4] - a[4]) * u


def build_path(rv, visit):
    """visit: list of ticks (dicts with t, phase, berth, x, y). Returns the path and the piece each tick belongs to."""
    R = River(rv)
    P = Path()
    pieces = []              # (first tick index, path start index, path end index)
    step = 0.05
    n = R.n
    # Where does it go first: berth or post? The first Arriving ticks tell (berth >= 0), or Waiting follows.
    phases = [v["phase"] for v in visit]
    first_wait = next((i for i, v in enumerate(visit) if v["phase"] == "Waiting"), -1)
    moored = next((i for i, v in enumerate(visit) if v["phase"] == "Moored"), -1)
    leave = next((i for i, v in enumerate(visit) if v["phase"] == "Leaving"), -1)
    y0 = visit[0]["y"]

    def lane_in(y_end, x_end, y_from):
        i0 = len(P.p)
        y = y_from
        while y > y_end + 1e-9:
            f = smoothstep((y_end + S_LEN - y) / S_LEN)
            P.add(R.up(y) + (x_end - R.up(y)) * f, y, y)
            y -= step
        P.add(x_end, y_end, y_end)
        return i0, len(P.p)

    if first_wait >= 0:
        wy = visit[first_wait]["y"]
        slot = min(range(3), key=lambda k: abs(R.wait[k] - wy))
        a, b = lane_in(wy, R.post_x(slot), y0)
        pieces.append((0, a, b))
        pieces.append((first_wait, b - 1, b))
        if moored >= 0:
            k = visit[moored]["berth"]
            by = R.berth_y[k]; bx = R.quay_x(k)
            i0 = len(P.p); y = wy; px = R.post_x(slot)
            while y > by + 1e-9:
                f = smoothstep((wy - y) / (wy - by))
                bulge = 0.35 * math.sin(math.pi * f)       # slips out a little from the post, then in to the quay
                P.add(px + (bx - px) * f + bulge, y, y)
                y -= step
            P.add(bx, by, by)
            arr2 = next(i for i in range(first_wait, len(visit)) if visit[i]["phase"] == "Arriving")
            pieces.append((arr2, i0 - 1 if i0 > 0 else i0, len(P.p)))
    elif moored >= 0:
        k = visit[moored]["berth"]
        a, b = lane_in(R.berth_y[k], R.quay_x(k), y0)
        pieces.append((0, a, b))
    if moored >= 0:
        pieces.append((moored, len(P.p) - 1, len(P.p)))
    if leave >= 0:
        sx, sy = P.p[-1][0], P.p[-1][1]
        i0 = len(P.p) - 1
        # push-off: sideways out, bow still up-river; stands for the first 0.4 cells of the simulation's way down
        for k in range(1, 11):
            u = smoothstep(k / 10)
            P.add(sx + PUSH * u, sy, sy + 0.4 * k / 10, -math.pi / 2, VARC)
        # the turning arc to starboard, drifting down with the stream; stands for the next 2.0 cells
        x0 = sx + PUSH
        xe = R.down(sy + 1.2)
        rad = (xe - x0) / 2
        cx = x0 + rad
        drift = 1.8
        N = 40
        for k in range(1, N + 1):
            ph = math.pi * k / N
            x = cx - rad * math.cos(ph)
            y = sy - 0.45 * rad * math.sin(ph) + drift * smoothstep(k / N)
            head = -math.pi / 2 + ph
            P.add(x, y, sy + 0.4 + 2.0 * k / N, head, VARC)
        # down the far lane, from where the arc ends; the simulation's y maps to the path's own y from here
        ye = P.p[-1][1]
        sim_e = sy + 2.4
        y = ye + step
        while y < n + 1.5:
            blend = smoothstep((y - ye) / 3.0)
            P.add(xe + (R.down(y) - xe) * blend, y, sim_e + (y - ye))
            y += step
        pieces.append((leave, i0, len(P.p)))
    P.finish()
    return P, pieces


def follow(rv, visit):
    """The designed boat at each tick of the visit: list of (x, y, heading, lag_cells, speed)."""
    P, pieces = build_path(rv, visit)
    out = []
    s = 0.0; v = 0.0; dt = 0.1
    pi = 0
    for i, tk in enumerate(visit):
        while pi + 1 < len(pieces) and i >= pieces[pi + 1][0]:
            pi += 1
        _, a, b = pieces[pi]
        tgt = P.target(a, b, tk["y"])
        if tk["phase"] == "Waiting" or tk["phase"] == "Moored":
            tgt = P.s[b - 1]
        tgt = max(tgt, s)                  # never goes back along its own path
        e = tgt - s
        _, _, _, vcap = P.point(s)
        # look ahead for a slower stretch (the arc, a bend) and brake for it in time
        look = s
        for _ in range(8):
            look = min(P.s[-1], look + 0.25)
            vcap = min(vcap, math.sqrt(P.point(look)[3] ** 2 + 2 * BRAKE * (look - s)))
        vstop = math.sqrt(max(0.0, 2 * BRAKE * e))   # stop where the target stops
        vdes = min(vcap, vstop, VMAX)
        if vdes > v: v = min(vdes, v + ACCEL * dt)
        else: v = max(vdes, v - BRAKE * dt)
        s = min(tgt, s + v * dt)
        x, y, h, _ = P.point(s)
        if tk["phase"] in ("Moored", "Waiting") and e < 0.05:
            h = -math.pi / 2                  # lying alongside or at the post: bow up-river, into the stream
        if out:                               # the bow turns at most TURN radians a second
            dh = (h - out[-1][2] + math.pi) % (2 * math.pi) - math.pi
            h = out[-1][2] + max(-TURN * dt, min(TURN * dt, dh))
        out.append((x, y, h, tgt - s, v))
    return out, P


def load(track_dir, boat_id):
    rv = json.load(open(f"{track_dir}/river.json"))
    visit = []
    for r in csv.DictReader(open(f"{track_dir}/boats.csv")):
        if r["id"] == str(boat_id):
            visit.append({"t": float(r["t"]), "phase": r["phase"], "berth": int(r["berth"]), "x": float(r["x"]),
                          "y": float(r["y"]), "heading": float(r["heading"]), "kind": r["kind"]})
    return rv, visit


if __name__ == "__main__":
    rv, visit = load(sys.argv[1], int(sys.argv[2]))
    res, P = follow(rv, visit)
    worst = max(r[3] for r in res)
    print("t,phase,sim_x,sim_y,sim_heading,x,y,heading,behind_cells,speed")
    for tk, r in zip(visit, res):
        print(f"{tk['t']:.1f},{tk['phase']},{tk['x']:.3f},{tk['y']:.3f},{tk['heading']:.3f},{r[0]:.3f},{r[1]:.3f},{r[2]:.3f},{r[3]:.2f},{r[4]:.2f}")
    print(f"# most behind the simulation along the path: {worst:.2f} cells", file=sys.stderr)

#!/usr/bin/env python3
"""Report on one interface audit run (tools/ui-audit.sh): python3 tools/ui-audit-report.py DIR [--jpeg=TO] -> DIR/report.md
(--jpeg=TO also writes TO/<id>.jpg, quality 80, of every picture.)

(a) overlaps between visible things of different cards or groups, per state; (b) cut-offs: partly outside the
window, text wider or taller than its box; (c) moves: named things whose rectangle differs between states of the
same age; (d) per state: cards or groups visible, share of the window they cover, whether the middle third is free;
(e) the smallest text height per state. "Visible" means opacity >= 0.05 and a rectangle of positive size.
"""
import glob
import json
import os
import sys

MIN_A = 0.05


def load(d):
    states = []
    for p in sorted(glob.glob(os.path.join(d, "u*.json"))):
        with open(p) as f:
            s = json.load(f)
        s["items"] = [i for i in s["items"] if i.get("opacity", 1) >= MIN_A and i["rect"][2] > 0.5 and i["rect"][3] > 0.5]
        states.append(s)
    return states


def inter(a, b):
    x0, y0 = max(a[0], b[0]), max(a[1], b[1])
    x1, y1 = min(a[0] + a[2], b[0] + b[2]), min(a[1] + a[3], b[1] + b[3])
    return (x1 - x0) * (y1 - y0) if x1 > x0 and y1 > y0 else 0.0


def is3d(i):
    return i["group"].startswith("3d:")


def card_of(i):
    """The card for overlap purposes: the group; 3D labels each count as their own card."""
    return i["group"] if not is3d(i) else i["name"]


def union_area(rects, W, H):
    # coverage on a coarse 8 px grid (exact enough for a share)
    cell = 8
    cols, rows = (W + cell - 1) // cell, (H + cell - 1) // cell
    grid = bytearray(cols * rows)
    for r in rects:
        x0, y0 = max(0, int(r[0] // cell)), max(0, int(r[1] // cell))
        x1, y1 = min(cols, int((r[0] + r[2]) // cell) + 1), min(rows, int((r[1] + r[3]) // cell) + 1)
        for y in range(y0, y1):
            grid[y * cols + x0:y * cols + x1] = b"\x01" * max(0, x1 - x0)
    return sum(grid) / (cols * rows), grid, cols, rows, cell


def fmt(r):
    return "(%d,%d %dx%d)" % tuple(int(round(v)) for v in r)


def short(n):
    return n if len(n) <= 70 else "..." + n[-67:]


def jpegs(d, to):
    """JPEG (quality 80) copies of every picture of the run, for the review page."""
    from PIL import Image
    os.makedirs(to, exist_ok=True)
    n = 0
    for p in sorted(glob.glob(os.path.join(d, "u*.png"))):
        Image.open(p).convert("RGB").save(os.path.join(to, os.path.basename(p)[:-4] + ".jpg"), "JPEG", quality=80)
        n += 1
    print("wrote %d jpegs to %s" % (n, to))


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--jpeg=")]
    for a in sys.argv[1:]:
        if a.startswith("--jpeg="):
            jpegs(args[0], a[7:])
    d = args[0]
    states = load(d)
    shots = {}
    sp = os.path.join(d, "shots.json")
    if os.path.exists(sp):
        for s in json.load(open(sp))["shots"]:
            shots[s["id"]] = s
    out = []
    if not states:
        print("no states in", d)
        return 1
    W, H = states[0]["window"]
    out.append("# Interface audit report: %s\n" % os.path.basename(os.path.normpath(d)))
    out.append("Window %dx%d, interface %s %%, %d states. Visible = opacity >= %.2f. Groups are the nearest panel (card) or "
               "the top node under the interface layer; 3D labels count one each.\n" % (W, H, states[0]["scale"], len(states), MIN_A))

    # (d) and (e) summary table first
    out.append("## Per state: cards, cover, middle third, smallest text\n")
    out.append("| state | age | lvl | stage | items | cards/groups | 3D labels | covered | middle third free | smallest text px | title |")
    out.append("|---|---|---|---|---|---|---|---|---|---|---|")
    mid = (W / 3, H / 3, W / 3, H / 3)
    summary = []
    for s in states:
        it = s["items"]
        groups = sorted({i["group"] for i in it if not is3d(i)})
        n3 = sum(1 for i in it if is3d(i))
        share, grid, cols, rows, cell = union_area([i["rect"] for i in it], W, H)
        in_mid = [i for i in it if inter(i["rect"], mid) > 0]
        mid_free = "yes" if not in_mid else "no (%d: %s)" % (len(in_mid), ", ".join(sorted({short(card_of(i)).split("/")[-1] for i in in_mid})[:4]))
        fonts = [i["font_px"] for i in it if "font_px" in i and not is3d(i)]
        small = min(fonts) if fonts else "-"
        title = shots.get(s["id"], {}).get("title", "")
        summary.append((s["id"], len(groups)))
        out.append("| %s | %d | %d | %s | %d | %d | %d | %.0f %% | %s | %s | %s |" % (
            s["id"], s["age"], s["level"], s["stage"], len(it), len(groups), n3, share * 100, mid_free, small, title))
    out.append("")

    # (a) overlaps
    out.append("## (a) Overlaps between different cards or groups\n")
    out.append("Pairs of visible things from different groups whose rectangles intersect (largest first, at most 25 per state). "
               "A thing inside a full-window backdrop is listed once per backdrop at most.\n")
    worst = []
    for s in states:
        it = s["items"]
        pairs = []
        for a in range(len(it)):
            for b in range(a + 1, len(it)):
                A, B = it[a], it[b]
                if card_of(A) == card_of(B):
                    continue
                # a group nested inside the other (e.g. a card inside a page group) is not an overlap of different cards
                if A["name"].startswith(B["group"] + "/") or B["name"].startswith(A["group"] + "/"):
                    continue
                ar = inter(A["rect"], B["rect"])
                if ar > 4:
                    pairs.append((ar, A, B))
        pairs.sort(key=lambda p: -p[0])
        out.append("### %s (%d pairs)\n" % (s["id"], len(pairs)))
        for ar, A, B in pairs[:25]:
            out.append("- %d px2: `%s` %s [%s] with `%s` %s [%s]" % (
                ar, short(A["name"]), fmt(A["rect"]), (A.get("text") or "")[:25], short(B["name"]), fmt(B["rect"]), (B.get("text") or "")[:25]))
            worst.append((ar, s["id"], A["name"], B["name"]))
        out.append("")

    # (b) cut-offs
    out.append("## (b) Cut-offs\n")
    for s in states:
        lines = []
        for i in s["items"]:
            x, y, w, h = i["rect"]
            if x < -0.5 or y < -0.5 or x + w > W + 0.5 or y + h > H + 0.5:
                if not (x <= 0 and y <= 0 and x + w >= W and y + h >= H):  # full-window backdrops are not cut off
                    lines.append("- outside the window: `%s` %s %s" % (short(i["name"]), fmt(i["rect"]), (i.get("text") or "")[:30]))
            ts = i.get("text_size")
            if ts and not is3d(i):
                if ts[0] > w + 1.5 or ts[1] > h + 1.5:
                    lines.append("- text bigger than its box%s: `%s` box %dx%d text %dx%d \"%s\"" % (
                        " (trimmed)" if i.get("trims") else "", short(i["name"]), w, h, ts[0], ts[1], (i.get("text") or "")[:30]))
        if lines:
            out.append("### %s\n" % s["id"])
            out.extend(lines[:40])
            if len(lines) > 40:
                out.append("- ... %d more" % (len(lines) - 40))
            out.append("")

    # (c) moves
    out.append("## (c) Things that move between states of the same age\n")
    out.append("Named 2D things seen in two or more states of an age whose rectangle is not the same in all of them "
               "(3D labels and floating coin numbers left out: they follow the map). Grouped by group.\n")
    for age in (1, 2):
        seen = {}
        for s in states:
            if s["age"] != age:
                continue
            for i in s["items"]:
                if is3d(i) or "Floater" in i["name"]:
                    continue
                seen.setdefault(i["name"], []).append((s["id"], tuple(round(v) for v in i["rect"]), i["group"]))
        moved = {}
        for n, occ in seen.items():
            rs = {o[1] for o in occ}
            if len(occ) >= 2 and len(rs) > 1:
                pos = len({r[:2] for r in rs}) > 1
                moved.setdefault(occ[0][2], []).append((n, occ, pos))
        npos = sum(1 for v in moved.values() for m in v if m[2])
        out.append("### Age %d: %d things change rectangle (%d of them change place, the rest only size), in %d groups\n" % (
            age, sum(len(v) for v in moved.values()), npos, len(moved)))
        for g in sorted(moved, key=lambda g: (-sum(1 for m in moved[g] if m[2]), -len(moved[g]))):
            out.append("- **%s** (%d things, %d change place)" % (short(g), len(moved[g]), sum(1 for m in moved[g] if m[2])))
            moved[g].sort(key=lambda m: (not m[2], m[0]))
            for n, occ, pos in moved[g][:6]:
                byr = {}
                for sid, r, _ in occ:
                    byr.setdefault(r, []).append(sid)
                out.append("  - %s`%s`: %s" % ("" if pos else "(size only) ", short(n), "; ".join("%s in %s" % (fmt(r), ",".join(ids)) for r, ids in list(byr.items())[:6])))
            if len(moved[g]) > 6:
                out.append("  - ... %d more" % (len(moved[g]) - 6))
        out.append("")

    worst.sort(key=lambda w: -w[0])
    out.insert(2, "## Headline\n\n- States with the most cards/groups: %s\n- Largest overlaps: %s\n" % (
        ", ".join("%s (%d)" % x for x in sorted(summary, key=lambda x: -x[1])[:6]),
        "; ".join("%s %s x %s (%d px2)" % (w[1], w[2].split("/")[-1], w[3].split("/")[-1], w[0]) for w in worst[:6])))
    with open(os.path.join(d, "report.md"), "w") as f:
        f.write("\n".join(out) + "\n")
    print("wrote", os.path.join(d, "report.md"))
    return 0


if __name__ == "__main__":
    sys.exit(main())

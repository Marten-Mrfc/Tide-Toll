# python3 assets/src/2d/icons-age2.py  ->  assets/icons/good-wool.svg, good-cloth.svg, ui-tide-high.svg,
#   ui-tide-low.svg, ui-boat-berth.svg, ui-crane.svg, ui-ferry.svg, ui-bridge.svg, ui-toll.svg, ui-request.svg,
#   ui-hint.svg, ui-extend.svg, ui-tools.svg, ui-value.svg (24 x 24 viewBox),
#   preview: assets/src/2d/previews/icons-age2-preview.png (also build/2d/): every icon at 20, 40 and 80 px on Paper
#   (top block) and on Ink (bottom block), rendered from the real files.
# Second-age set (docs/plans/age2-design.md, The River): the two new goods, the tide, the landing's berth and crane,
# the crossings, the toll, a boat's request, hints and three upgrade marks. Same house language as the Hamlet icons
# (assets/src/2d/icons.py, whose Icon class and helpers this script reuses): every element a closed Paper shape in a
# 2-unit Ink outline, open lines drawn as closed bands at least 3 units wide, Ink only for small accents inside a
# Paper shape. The free asset sites are blocked from the build container (checked 2026-11-25), so the icons are drawn
# here as code.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import numpy as np
from common import svg, ledger, asset_path, ROOT
from icons import Icon, INK, preview, cross, arc_pts, rot  # noqa: E402


def wave(ic, y, x0=1.8, x1=22.2, amp=0.9, width=3.4, n=3):
    xs = np.linspace(x0, x1, 25)
    ic.band([(x, y + amp * math.sin((x - x0) / (x1 - x0) * n * 2 * math.pi)) for x in xs], width)


def good_wool():
    ic = Icon("good-wool")                   # a fleece: one scalloped outline of curls, ink curl marks inside
    pts = []
    n, R, r = 7, 6.6, 3.6
    for i in range(n):
        a0 = 2 * math.pi * (i + 0.25) / n
        cx, cy = 12.0 + R * 1.15 * math.cos(a0), 12.4 + R * 0.8 * math.sin(a0)
        for k in range(5):                   # outer half of each curl
            t = a0 - math.pi / 2 + math.pi * k / 4
            pts.append((cx + r * math.cos(t), cy + r * math.sin(t)))
    ic.shape(pts)
    for cx, cy in ((8.2, 12.0), (15.4, 11.2), (12.0, 15.6)):
        ic.circle(cx, cy, 1.3, fill=INK)
    return ic


def good_cloth():
    ic = Icon("good-cloth")                  # a bolt of cloth: the flat length, the roll at its end, ink stripes
    ic.shape([(7.0, 6.4), (21.8, 9.4), (21.8, 20.6), (7.0, 17.6)])
    ic.rect(10.2, 9.6, 21.8, 11.4, fill=INK)
    ic.rect(10.2, 15.2, 21.8, 17.0, fill=INK)
    ic.circle(7.0, 12.0, 5.6)                # the roll, end on
    ic.circle(7.0, 12.0, 1.8, fill=INK)
    return ic


def tide_post(ic, x0=2.6, x1=7.4):
    ic.rect(x0, 1.6, x1, 22.4, r=0.8)
    for y in (5.4, 10.2, 15.0):
        ic.rect(x0 + 1.2, y, x1 - 1.2, y + 1.4, fill=INK)          # level marks


def ui_tide_high():
    ic = Icon("ui-tide-high")                # the tide post deep in water (two waves), and an up arrow
    tide_post(ic)
    wave(ic, 15.6)
    wave(ic, 9.4)
    ic.band([(17.6, 6.2), (17.6, 4.4)], 3.6, head=(3.6, 4.2))
    return ic


def ui_tide_low():
    ic = Icon("ui-tide-low")                 # the post high and dry above low water, and a down arrow
    tide_post(ic)
    wave(ic, 19.4)
    ic.band([(16.4, 2.0), (16.4, 9.4)], 4.0, head=(4.6, 4.6))
    return ic


def ui_boat_berth():
    ic = Icon("ui-boat-berth")               # a boat moored at a jetty post, the line made fast
    wave(ic, 20.6)
    ic.rect(1.6, 3.0, 5.6, 22.4, r=0.8)      # mooring post
    ic.rect(1.2, 4.4, 6.0, 6.8, fill=INK)    # its dark top band
    ic.band([(5.2, 9.0), (11.4, 13.4)], 3.0) # mooring line
    ic.rect(14.6, 3.0, 17.4, 14.0)           # mast
    ic.shape([(7.8, 13.0), (22.6, 13.0), (20.2, 18.8), (9.6, 18.8)])   # hull
    return ic


def ui_crane():
    ic = Icon("ui-crane")                    # the treadwheel crane: wheel, post, jib, rope, a bale on the hook
    wheel = arc_pts((7.2, 15.0), 6.2, 0, 2 * math.pi, 16)[:-1]
    hole = arc_pts((7.2, 15.0), 3.0, 2 * math.pi, 0, 12)[:-1]
    ic.ring(wheel, hole)
    ic.rect(9.4, 3.0, 12.6, 22.4)            # post
    ic.rect(1.4, 19.8, 15.4, 22.6, r=0.8)    # sill
    ic.band([(9.4, 5.4), (22.2, 2.8)], 3.4)  # jib
    ic.rect(18.6, 4.8, 21.2, 12.4)           # rope
    ic.rect(16.4, 12.0, 23.0, 18.0, r=1.0)   # bale
    return ic


def ui_ferry():
    ic = Icon("ui-ferry")                    # a passenger on the ferry raft, the rope overhead
    wave(ic, 20.8)
    ic.band([(1.4, 3.6), (22.6, 3.6)], 3.2)  # rope
    ic.rect(15.0, 2.4, 18.0, 13.6)           # rope guide post on the raft
    ic.shape([(5.0, 15.0), (11.4, 15.0), (10.4, 10.6), (8.2, 9.4), (6.0, 10.6)])   # passenger's body
    ic.circle(8.2, 7.0, 2.6)                 # head
    ic.shape([(1.8, 13.8), (22.2, 13.8), (20.2, 18.6), (3.8, 18.6)])               # raft
    return ic


def ui_bridge():
    ic = Icon("ui-bridge")                   # an arched footbridge over the water, rail posts on the arch
    wave(ic, 20.6, x0=5.0, x1=19.0, n=2)
    outer = arc_pts((12.0, 26.0), 13.0, math.radians(200), math.radians(340), 14)
    inner = arc_pts((12.0, 26.0), 9.4, math.radians(337), math.radians(203), 14)
    for x in (5.6, 11.0, 16.4):              # rail posts (behind the deck band)
        top = 26.0 - math.sqrt(13.0 ** 2 - (x + 1.4 - 12.0) ** 2)
        ic.rect(x, top - 5.4, x + 2.8, top + 1.0)
    ic.band([(4.0, 9.8), (12.0, 7.2), (20.0, 9.8)], 3.0)   # hand rail
    ic.shape(outer + inner)                  # the arch / deck as one closed band
    return ic


def ui_toll():
    ic = Icon("ui-toll")                     # the toll bar across the way, and a coin
    ic.rect(2.2, 6.0, 6.6, 22.4, r=0.8)      # bar post
    ic.rect(1.2, 19.8, 8.0, 22.6, r=0.8)     # its foot
    ic.rect(3.6, 7.2, 22.8, 11.4, r=1.2)     # the bar
    for x in (9.6, 15.2, 20.0):
        ic.rect(x, 7.2, x + 2.0, 11.4, fill=INK)               # stripes
    ic.circle(4.4, 9.3, 1.0)                 # pivot
    ic.circle(15.4, 17.4, 5.0)               # coin
    ic.circle(15.4, 17.4, 2.0, fill=INK)
    return ic


def ui_request():
    ic = Icon("ui-request")                  # a speech bubble from the boat with the wanted good in it (a sack)
    ic.shape([(2.0, 2.4), (22.0, 2.4), (22.0, 16.0), (11.0, 16.0), (5.6, 21.8), (6.6, 16.0), (2.0, 16.0)])
    ic.shape([(8.2, 13.6), (15.8, 13.6), (15.8, 9.0), (13.8, 7.6), (14.8, 5.2), (9.2, 5.2), (10.2, 7.6),
              (8.2, 9.0)], fill=INK)         # sack accent
    return ic


def ui_hint():
    ic = Icon("ui-hint")                     # a lit candle on its dish: a hint lights the way
    ic.rect(1.8, 19.2, 22.2, 22.4, r=1.2)    # dish
    ic.rect(9.0, 10.4, 15.0, 19.4, r=0.6)    # candle
    flame = [(12.0, 1.2)] + [(12.0 + 3.0 * math.cos(t), 6.6 + 2.6 * math.sin(t))
                             for t in [math.pi * (-0.15 + 1.3 * k / 9) for k in range(10)]][::-1]
    ic.shape(flame)
    ic.rect(11.2, 8.8, 12.8, 10.6, fill=INK)  # wick
    ic.band([(2.4, 5.4), (6.2, 7.0)], 3.0)    # light rays
    ic.band([(21.6, 5.4), (17.8, 7.0)], 3.0)
    return ic


def ui_extend():
    ic = Icon("ui-extend")                   # a cottage with a lower wing built on, and a plus
    ic.shape([(1.6, 12.0), (8.2, 6.0), (14.8, 12.0), (14.8, 21.8), (1.6, 21.8)])
    ic.shape([(0.6, 12.8), (8.2, 5.4), (15.8, 12.8), (8.2, 8.4)])    # roof band
    ic.shape([(14.8, 13.4), (22.4, 15.6), (22.4, 21.8), (14.8, 21.8)])   # the new wing, lean-to roof line
    ic.rect(6.4, 15.6, 10.0, 21.8, fill=INK)  # door
    ic.shape(cross(19.0, 6.2, 4.2, 1.5))
    return ic


def ui_tools():
    ic = Icon("ui-tools")                    # a wooden mallet: handle band, head across it
    ic.band([(9.4, 9.4), (20.4, 20.4)], 3.4)
    head = [(-6.0, -3.4), (6.0, -3.4), (6.0, 3.4), (-6.0, 3.4)]
    ic.shape(rot([(8.0 + x, 8.0 + y) for x, y in head], math.radians(-45), (8.0, 8.0)))
    ic.shape(rot([(8.0 + x, 8.0 + y) for x, y in [(-1.0, -3.4), (1.0, -3.4), (1.0, 3.4), (-1.0, 3.4)]],
                 math.radians(-45), (8.0, 8.0)), fill=INK)   # iron band round the head
    return ic


def ui_value():
    ic = Icon("ui-value")                    # a coin and an up arrow: worth more
    ic.circle(9.4, 13.6, 8.2)
    ic.circle(9.4, 13.6, 5.0)
    ic.circle(9.4, 13.6, 1.8, fill=INK)
    ic.band([(19.6, 21.6), (19.6, 7.6)], 3.8, head=(5.2, 4.2))
    return ic


ICONS = [
    (good_wool, "Goods: Wool"),
    (good_cloth, "Goods: Cloth"),
    (ui_tide_high, "Chapter interface: tide high (boats can come in, the ford is closed)"),
    (ui_tide_low, "Chapter interface: tide low (the ford can be walked)"),
    (ui_boat_berth, "Landing: berth (and the second-berth upgrade)"),
    (ui_crane, "Landing: crane upgrade (bigger cargoes)"),
    (ui_ferry, "Crossings: ferry offer and raft upgrades"),
    (ui_bridge, "Crossings: footbridge offer"),
    (ui_toll, "Drawbridge: toll upgrade and toll income"),
    (ui_request, "Landing: a boat's cargo request"),
    (ui_hint, "Book interface: hint"),
    (ui_extend, "Upgrade bookmark: extend cottages"),
    (ui_tools, "Upgrade bookmark: tools"),
    (ui_value, "Upgrade bookmark: value of a good (Wool, Cloth, better prices)"),
]


def main():
    paths = []
    for fn, used in ICONS:
        ic = fn()
        out = asset_path(f"icons/{ic.name}.svg")
        out.write_text(ic.svg().replace("generated by assets/src/2d/icons.py", "generated by assets/src/2d/icons-age2.py"),
                       encoding="utf-8")
        probs = svg.lint(out, icon_style=True)
        print(out.name, "OK" if not probs else probs)
        if probs:
            raise SystemExit(f"{out}: {probs}")
        paths.append(out)
        ledger.add("2d", out, kind="icon", generator=__file__, used_in=used,
                   note="SVG written as code: closed Paper shapes in a 2-unit Ink outline; free asset sites blocked from the build container (checked 2026-11-25)", verified=True)
    if "--no-preview" not in sys.argv:
        preview(paths, [ROOT / "build" / "2d" / "icons-age2-preview.png",
                        pathlib.Path(__file__).resolve().parent / "previews" / "icons-age2-preview.png"])


if __name__ == "__main__":
    main()

# python3 assets/src/2d/icons-town.py  ->  assets/icons/up-*.svg, ui-bell, ui-achievement, ui-settings, ui-display,
#   ui-keys, ui-save, ui-quit, ui-lock, ui-check, good-water, good-grain, good-flour (24 x 24 viewBox),
#   preview: assets/src/2d/previews/icons-town-preview.png (also build/2d/): every icon at 20, 40 and 80 px on Paper
#   (top block) and on Ink (bottom block), rendered from the real files.
# Incremental-town set (docs/plans/incremental-design.md): upgrade bookmarks, the bell, the book's menu pages and the
# three goods. Same house language as the Hamlet icons (assets/src/2d/icons.py, whose Icon class and helpers this
# script reuses): every element a closed Paper shape in a 2-unit Ink outline, open lines drawn as closed bands at
# least 3 units wide, Ink only for small accents inside a Paper shape. The free asset sites are blocked from the
# build container (checked 2026-11-25), so the icons are drawn here as code.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
from common import svg, ledger, asset_path, ROOT
from icons import Icon, INK, preview, cross, arc_pts  # noqa: E402


def plus_badge(ic, cx=18.4, cy=5.6):
    """The 'one more' mark of the upgrade icons: a plus in one closed outline."""
    ic.shape(cross(cx, cy, 4.2, 1.5))


def up_villager():
    ic = Icon("up-villager")                 # a villager (head dot, teardrop body) and a plus
    ic.shape([(3.0, 22.0), (15.0, 22.0), (13.4, 15.0), (11.0, 12.4), (7.0, 12.4), (4.6, 15.0)])
    ic.circle(9.0, 8.0, 3.8)
    plus_badge(ic)
    return ic


def up_pace():
    ic = Icon("up-pace")                     # a boot with two speed bands behind it
    ic.rect(1.6, 6.4, 8.6, 9.8, r=1.2)
    ic.rect(3.4, 12.0, 8.6, 15.4, r=1.2)
    ic.shape([(10.8, 3.0), (16.4, 3.0), (16.4, 13.2), (21.6, 15.6), (22.4, 20.8), (10.8, 20.8)])
    ic.rect(10.8, 18.0, 22.4, 20.8)          # sole
    return ic


def up_load():
    ic = Icon("up-load")                     # a full basket with a handle
    outer = arc_pts((12.0, 11.6), 7.6, math.pi, 2 * math.pi, 14)
    inner = arc_pts((12.0, 11.6), 4.6, 2 * math.pi, math.pi, 14)
    ic.shape(outer + inner)                  # handle as one closed band
    ic.circle(9.0, 11.6, 2.6)                # the load heaped in it
    ic.circle(14.6, 11.4, 2.8)
    ic.shape([(2.4, 11.6), (21.6, 11.6), (19.0, 21.6), (5.0, 21.6)])
    ic.rect(4.4, 15.0, 19.6, 17.0, fill=INK)  # weave band (accent)
    return ic


def up_pave():
    ic = Icon("up-pave")                     # cobbles laid in courses
    for y0, xs in ((2.4, (2.0, 9.0, 16.0)), (9.2, (5.4, 12.4)), (16.0, (2.0, 9.0, 16.0))):
        for x in xs:
            ic.rect(x, y0, x + 6.0, y0 + 5.4, r=1.4)
    ic.rect(1.0, 9.2, 3.6, 14.6, r=1.0)
    ic.rect(19.4, 9.2, 23.0, 14.6, r=1.0)
    return ic


def up_building():
    ic = Icon("up-building")                 # a cottage and a plus
    ic.shape([(2.4, 11.6), (9.8, 5.0), (17.2, 11.6), (17.2, 21.8), (2.4, 21.8)])
    ic.shape([(1.0, 12.6), (9.8, 4.4), (18.6, 12.6), (9.8, 7.4)])     # roof band
    ic.rect(7.8, 15.0, 11.8, 21.8, fill=INK)  # door
    plus_badge(ic, 18.6, 5.2)
    return ic


def bell_shape(ic, cx=12.0, top=4.6, bottom=18.0, w=8.6):
    ic.rect(cx - 1.6, top - 3.0, cx + 1.6, top + 0.6)                 # hanger
    ic.shape([(cx - w, bottom), (cx + w, bottom), (cx + w - 1.6, bottom - 2.4), (cx + 4.8, top + 6.0),
              (cx + 3.4, top), (cx - 3.4, top), (cx - 4.8, top + 6.0), (cx - w + 1.6, bottom - 2.4)])
    ic.circle(cx, bottom + 2.4, 2.0)                                  # clapper


def ui_bell():
    ic = Icon("ui-bell")
    bell_shape(ic)
    return ic


def ui_achievement():
    ic = Icon("ui-achievement")              # a rosette with two ribbon tails and an ink star
    ic.shape([(6.4, 12.0), (10.6, 12.0), (10.6, 22.4), (8.5, 20.4), (6.4, 22.4)])
    ic.shape([(13.4, 12.0), (17.6, 12.0), (17.6, 22.4), (15.5, 20.4), (13.4, 22.4)])
    ic.circle(12.0, 9.2, 7.4)
    pts = [(12.0 + (4.0 if i % 2 == 0 else 1.7) * math.sin(math.pi * i / 5),
            9.4 - (4.0 if i % 2 == 0 else 1.7) * math.cos(math.pi * i / 5)) for i in range(10)]
    ic.shape(pts, fill=INK)
    return ic


def ui_settings():
    ic = Icon("ui-settings")                 # a cog with a hole
    n, ro, ri = 8, 10.2, 7.6
    outer = []
    for i in range(n):
        a = 2 * math.pi * i / n
        for da, r in ((-0.22, ri), (-0.15, ro), (0.15, ro), (0.22, ri)):
            outer.append((12 + r * math.cos(a + da), 12 + r * math.sin(a + da)))
    hole = [(12 + 3.2 * math.cos(2 * math.pi * i / 16), 12 + 3.2 * math.sin(2 * math.pi * i / 16)) for i in range(16)]
    ic.ring(outer, hole[::-1])
    return ic


def ui_display():
    ic = Icon("ui-display")                  # a screen on a stand
    ic.shape([(9.0, 16.6), (15.0, 16.6), (16.4, 21.6), (7.6, 21.6)])
    ic.ring([(1.6, 3.0), (22.4, 3.0), (22.4, 17.0), (1.6, 17.0)], [(5.0, 6.4), (5.0, 13.6), (19.0, 13.6), (19.0, 6.4)])
    return ic


def ui_keys():
    ic = Icon("ui-keys")                     # a row of key caps on a board
    ic.rect(1.4, 5.0, 22.6, 19.0, r=2.0)
    for x in (5.6, 10.8, 16.0):
        ic.rect(x, 8.8, x + 2.4, 10.8, fill=INK)
    ic.rect(7.4, 13.6, 16.6, 15.4, fill=INK)  # space bar
    return ic


def ui_save():
    ic = Icon("ui-save")                     # a closed book with a ribbon marker
    ic.rect(3.0, 2.0, 20.0, 22.0, r=1.6)
    ic.rect(3.0, 2.0, 7.0, 22.0, r=1.2)      # spine
    ic.shape([(12.0, 0.8), (17.4, 0.8), (17.4, 13.0), (14.7, 10.4), (12.0, 13.0)])   # ribbon
    return ic


def ui_quit():
    ic = Icon("ui-quit")                     # an open door with an arrow going out
    ic.ring([(2.0, 2.0), (13.0, 2.0), (13.0, 22.0), (2.0, 22.0)], [(5.4, 5.4), (5.4, 18.6), (9.6, 18.6), (9.6, 5.4)])
    ic.band([(9.0, 12.0), (17.2, 12.0)], 4.0, head=(5.2, 4.8))
    return ic


def ui_lock():
    ic = Icon("ui-lock")                     # a padlock: shackle band, body, ink keyhole
    outer = arc_pts((12.0, 10.4), 6.6, math.pi, 2 * math.pi, 14)
    inner = arc_pts((12.0, 10.4), 3.4, 2 * math.pi, math.pi, 14)
    ic.shape(outer + [(18.6, 11.4), (15.4, 11.4)] + inner + [(8.6, 11.4), (5.4, 11.4)])
    ic.rect(3.0, 10.4, 21.0, 22.0, r=1.6)
    ic.circle(12.0, 15.0, 1.4, fill=INK)
    ic.rect(11.2, 15.0, 12.8, 19.0, fill=INK)
    return ic


def ui_check():
    ic = Icon("ui-check")                    # a tick as one closed band
    ic.band([(3.4, 12.6), (9.4, 18.6), (20.6, 5.4)], 4.6)
    return ic


def good_water():
    ic = Icon("good-water")                  # a drop with an ink highlight
    pts = [(12.0, 1.8)] + [(12.0 + 7.4 * math.cos(t), 14.8 + 7.2 * math.sin(t))
                           for t in [math.pi * (-0.2 + 1.4 * k / 15) for k in range(16)]][::-1]
    ic.shape(pts)
    ic.shape([(7.6, 14.6), (9.4, 14.6), (10.2, 18.0), (8.8, 18.6)], fill=INK)
    return ic


def good_grain():
    ic = Icon("good-grain")                  # an ear of grain: stalk and kernels in pairs
    ic.rect(10.6, 9.0, 13.4, 22.6)           # stalk
    for y in (15.2, 8.8):
        for sx in (-1, 1):
            cx, cy = 12.0 + sx * 3.6, y
            ic.shape([(cx + 2.6 * math.cos(t) * math.cos(sx * 0.5) - 3.4 * math.sin(t) * math.sin(sx * 0.5),
                       cy + 2.6 * math.cos(t) * math.sin(sx * 0.5) + 3.4 * math.sin(t) * math.cos(sx * 0.5))
                      for t in [2 * math.pi * k / 10 for k in range(10)]])
    ic.shape([(12.0, 0.8), (14.6, 4.2), (12.0, 7.6), (9.4, 4.2)])   # tip kernel
    return ic


def good_flour():
    ic = Icon("good-flour")                  # a flour sack: square body, tied neck, tuft, ink dusting
    ic.shape([(4.0, 22.0), (20.0, 22.0), (20.0, 12.0), (15.6, 8.0), (8.4, 8.0), (4.0, 12.0)])
    ic.shape([(8.6, 2.0), (15.4, 2.0), (13.6, 6.0), (10.4, 6.0)])   # tuft
    ic.rect(9.4, 5.6, 14.6, 8.6, r=0.8)      # tie
    for x, y in ((9.0, 15.0), (15.0, 15.0), (12.0, 18.4)):
        ic.circle(x, y, 1.2, fill=INK)
    return ic


ICONS = [
    (up_villager, "Upgrade bookmark: one more villager"),
    (up_pace, "Upgrade bookmark: pace (everyone walks faster)"),
    (up_load, "Upgrade bookmark: load (carry more per trip)"),
    (up_pave, "Upgrade bookmark: pave a road"),
    (up_building, "Upgrade bookmark: buildings offered by levels"),
    (ui_bell, "Book interface: ring the bell"),
    (ui_achievement, "Book interface: achievements"),
    (ui_settings, "Pause menu: settings"),
    (ui_display, "Settings page: display"),
    (ui_keys, "Settings page: keys"),
    (ui_save, "Settings page: saving"),
    (ui_quit, "Pause menu: quit"),
    (ui_lock, "Book interface: locked upgrade or gift"),
    (ui_check, "Book interface: done, achievement earned"),
    (good_water, "Goods: Water"),
    (good_grain, "Goods: Grain"),
    (good_flour, "Goods: Flour"),
]


def main():
    paths = []
    for fn, used in ICONS:
        ic = fn()
        out = asset_path(f"icons/{ic.name}.svg")
        out.write_text(ic.svg().replace("generated by assets/src/2d/icons.py", "generated by assets/src/2d/icons-town.py"),
                       encoding="utf-8")
        probs = svg.lint(out, icon_style=True)
        print(out.name, "OK" if not probs else probs)
        if probs:
            raise SystemExit(f"{out}: {probs}")
        paths.append(out)
        ledger.add("2d", out, kind="icon", generator=__file__, used_in=used,
                   note="SVG written as code: closed Paper shapes in a 2-unit Ink outline; free asset sites blocked from the build container (checked 2026-11-25)", verified=True)
    preview(paths, [ROOT / "build" / "2d" / "icons-town-preview.png",
                    pathlib.Path(__file__).resolve().parent / "previews" / "icons-town-preview.png"])


if __name__ == "__main__":
    main()

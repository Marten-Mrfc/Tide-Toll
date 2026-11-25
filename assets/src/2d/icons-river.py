# python3 assets/src/2d/icons-river.py  ->  assets/icons/tool-ferry.svg, tool-drawbridge.svg, ui-raise-bridge.svg,
#   ui-lower-bridge.svg, ui-hire-keeper.svg, ui-coin.svg, ui-boat.svg, ui-reputation.svg (24 x 24 viewBox),
#   preview: assets/src/2d/previews/icons-river-preview.png (also build/2d/): every icon at 20, 40 and 80 px on Paper
#   (top block) and on Ink (bottom block), rendered from the real files.
# River set icons, drawn in the same house language as the 14 Hamlet icons (assets/src/2d/icons.py, whose Icon class
# and helpers this script reuses): every element a closed Paper shape in a 2-unit Ink outline, open lines drawn as
# closed bands at least 3 units wide, Ink only for small accents inside a Paper shape. Free sources were not used:
# kenney.nl and the other free asset sites are blocked from the build container (checked 2026-11-25), so the icons
# are drawn here as code.
import sys, pathlib; sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))  # assets/src
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import math
import numpy as np
from common import svg, ledger, asset_path, ROOT
from icons import Icon, INK, preview  # noqa: E402  (the Hamlet icon generator; its main() only runs as a script)


def water(ic, y=20.4):
    xs = np.linspace(2.4, 21.6, 25)
    ic.band([(x, y + 0.9 * math.sin((x - 2.4) / 19.2 * 3 * 2 * math.pi)) for x in xs], 3.4)


def star(cx, cy, r, ri, n=5):
    return [(cx + (r if i % 2 == 0 else ri) * math.sin(math.pi * i / n),
             cy - (r if i % 2 == 0 else ri) * math.cos(math.pi * i / n)) for i in range(2 * n)]


def tool_ferry():
    ic = Icon("tool-ferry")                  # side view: a raft running on a rope stretched over the water
    water(ic)
    ic.band([(1.4, 4.6), (22.6, 4.6)], 3.2)  # the rope, bank to bank
    ic.rect(10.4, 3.4, 13.6, 13.0)           # the raft's rope guide
    ic.shape([(2.2, 12.2), (21.8, 12.2), (19.8, 17.4), (4.2, 17.4)])   # raft
    return ic


def tool_drawbridge():
    ic = Icon("tool-drawbridge")             # side view, lowered: gantry post, balance beam with counterweight,
    xs = np.linspace(10.6, 22.4, 13)         # chain down to the leaf tip, pier, water under the leaf
    ic.band([(x, 20.8 + 0.9 * math.sin((x - 10.6) / 11.8 * 2 * 2 * math.pi)) for x in xs], 3.2)   # water
    ic.rect(1.2, 16.6, 9.0, 22.2, r=0.8)     # pier
    ic.rect(1.2, 12.8, 22.8, 16.8, r=1.0)    # deck and leaf
    ic.rect(6.2, 3.6, 9.6, 13.0)             # gantry post
    ic.rect(18.8, 5.4, 21.8, 13.0)           # chain
    ic.rect(4.0, 2.4, 22.4, 6.0, r=1.0)      # balance beam
    ic.rect(1.0, 1.6, 5.6, 8.2, r=0.8)       # counterweight at the back end
    ic.circle(7.9, 4.2, 1.3, fill=INK)       # pivot (ink accent inside the beam)
    return ic


def leaf_pier(ic):
    ic.rect(1.4, 15.8, 7.6, 21.8, r=0.8)     # pier / bank


def ui_raise_bridge():
    ic = Icon("ui-raise-bridge")             # the leaf swung up from its pier, with an up arrow
    ic.band([(19.0, 20.4), (19.0, 8.6)], 4.2, head=(4.0, 4.6))
    ic.band([(5.2, 16.2), (13.2, 3.6)], 4.4)
    leaf_pier(ic)
    return ic


def ui_lower_bridge():
    ic = Icon("ui-lower-bridge")             # the leaf lying flat on its pier, with a down arrow
    ic.band([(17.0, 1.4), (17.0, 6.6)], 4.2, head=(4.4, 5.0))
    ic.rect(1.4, 17.6, 7.6, 22.2, r=0.8)     # pier
    ic.rect(17.8, 17.6, 21.6, 22.2)          # far bank post the leaf rests on
    ic.rect(1.6, 13.4, 22.8, 17.8, r=1.2)    # leaf
    return ic


def ui_hire_keeper():
    ic = Icon("ui-hire-keeper")              # the keeper (head and shoulders) beside the warning bell
    ic.rect(2.0, 13.0, 14.8, 22.0, r=3.0)
    ic.circle(8.4, 7.8, 4.0)
    ic.rect(18.0, 2.6, 21.0, 6.4)            # bell handle
    ic.shape([(15.0, 18.2), (23.0, 18.2), (21.8, 15.8), (21.6, 9.6), (19.5, 6.0), (17.4, 9.6), (17.2, 15.8)])
    ic.circle(19.5, 20.0, 1.6)               # clapper
    return ic


def ui_coin():
    ic = Icon("ui-coin")                     # a toll coin: rim, face, and a T stamped in ink
    ic.circle(12.0, 12.0, 9.6)
    ic.circle(12.0, 12.0, 6.4)
    ic.shape([(8.6, 7.8), (15.4, 7.8), (15.4, 9.6), (12.9, 9.6), (12.9, 16.2), (11.1, 16.2), (11.1, 9.6), (8.6, 9.6)],
             fill=INK)
    return ic


def ui_boat():
    ic = Icon("ui-boat")                     # a masted boat: hull, mast, sail triangle and pennant (the sail mark)
    ic.rect(10.0, 2.4, 13.2, 16.0)           # mast
    ic.shape([(10.0, 2.6), (10.0, 6.6), (3.4, 4.6)])                      # pennant
    ic.shape([(14.4, 4.2), (14.4, 14.2), (21.6, 14.2)])                   # sail
    ic.shape([(1.6, 15.2), (22.4, 15.2), (19.0, 21.2), (5.0, 21.2)])      # hull
    return ic


def ui_reputation():
    ic = Icon("ui-reputation")               # river reputation: a swallow-tailed pennant on its staff, with a star
    ic.shape([(6.0, 3.0), (21.8, 3.0), (17.4, 8.6), (21.8, 14.2), (6.0, 14.2)])
    ic.shape(star(11.4, 8.8, 3.6, 1.5), fill=INK)
    ic.rect(2.6, 1.8, 6.4, 22.2, r=1.2)      # staff
    return ic


ICONS = [
    (tool_ferry, "Tool bar: ferry (raft on a rope between bank posts)"),
    (tool_drawbridge, "Tool bar: drawbridge (gantry, balance beam, deck)"),
    (ui_raise_bridge, "Drawbridge control: raise the bridge (manual raise)"),
    (ui_lower_bridge, "Drawbridge control: lower the bridge"),
    (ui_hire_keeper, "Drawbridge control: hire the bridge keeper"),
    (ui_coin, "Chapter page and pop-ups: money, trade income (toll coin)"),
    (ui_boat, "Chapter page and alerts: boat (masted boat with sail mark)"),
    (ui_reputation, "Chapter page: river reputation (pennant with a star)"),
]


def main():
    paths = []
    for fn, used in ICONS:
        ic = fn()
        out = asset_path(f"icons/{ic.name}.svg")
        out.write_text(ic.svg().replace("generated by assets/src/2d/icons.py", "generated by assets/src/2d/icons-river.py"),
                       encoding="utf-8")
        probs = svg.lint(out, icon_style=True)
        print(out.name, "OK" if not probs else probs)
        if probs:
            raise SystemExit(f"{out}: {probs}")
        paths.append(out)
        ledger.add("2d", out, kind="icon", generator=__file__, used_in=used,
                   note="SVG written as code: closed Paper shapes in a 2-unit Ink outline; free asset sites blocked from the build container (checked 2026-11-25)", verified=True)
    preview(paths, [ROOT / "build" / "2d" / "icons-river-preview.png",
                    pathlib.Path(__file__).resolve().parent / "previews" / "icons-river-preview.png"])


if __name__ == "__main__":
    main()

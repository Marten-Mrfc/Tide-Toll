# Message kinds (message-system.md section 1; names "msg-<kind>") and the navigation buttons (TownHud.Timers.cs asks
# for nav-achievements, nav-page, nav-home, nav-pause, nav-bell), plus the tide dial's two states (nav-tide-*).
from .draw import Icon, place, line, path, poly, circle, rect, dot
from . import glyphs as g
from . import glyphs2 as h
from . import shop as shopset


def messages():
    I = []
    I.append(Icon("msg-level", "brick", "a flag: a level reached").add(g.flag()))
    I.append(Icon("msg-charter", "wheat", "the charter: a scroll with a seal").add(g.scroll(), place(g.seal(f="brick"), at=(34, 34), size=20)))
    I.append(Icon("msg-drive", "wheat", "the charter drive: a scroll with four boxes, two filled").add(h.drive_scroll()))
    I.append(Icon("msg-boon", "wheat", "a star: a boon chosen").add(g.star()))
    I.append(Icon("msg-age", "brick", "a book with a turning page and a ribbon: a new age").add(g.book(), path("M24 13 Q30 4 40 4 Q34 16 24 25", "p"), poly([(30, 37), (30, 47), (33, 44), (36, 47), (36, 36)], "wheat")))
    I.append(Icon("msg-seal", "brick", "a wax seal with a pressed star and ribbons").add(h.seal_wax()))
    I.append(Icon("msg-achievement", "wheat", "a rosette").add(g.rosette()))
    I.append(Icon("msg-building", "packed", "a door: a building opened").add(g.door()))
    I.append(Icon("msg-milestone", "stone", "a roadside milestone").add(h.milestone(path("M10 24 H34 M26 14 L36 24 L26 34"))))
    I.append(Icon("msg-perk", "slate", "a horseshoe: a perk").add(g.horseshoe()))
    I.append(Icon("msg-touch", "brick", "a flower: a small touch").add(g.flower()))
    I.append(Icon("msg-trouble", "brick", "a warning triangle").add(path("M24 5 L44 41 H4Z", "c"), line((24, 17), (24, 28)), dot(24, 34, 2.2)))
    I.append(Icon("msg-hint", "wheat", "a lantern: a hint").add(g.lantern()))
    return I


def nav():
    I = []
    I.append(Icon("nav-achievements", "wheat", "a rosette: the achievements page").add(g.rosette()))
    I.append(Icon("nav-page", "brick", "an open book: this age's page").add(g.book(), line((9, 18), (19, 19)), line((9, 25), (19, 26)), line((29, 19), (39, 18)), line((29, 26), (39, 25))))
    I.append(Icon("nav-home", "meadow", "a roof over a crosshair: back home").add(h.home()))
    I.append(Icon("nav-pause", "slate", "two bars: pause").add(rect(11, 8, 9, 32, "c", 2), rect(28, 8, 9, 32, "c", 2)))
    I.append(Icon("nav-bell", "wheat", "the bell").add(g.bell()))
    for key, note in (("high", "high tide: the water near the post's top, an arrow up"), ("low", "low tide: the water at the post's foot, an arrow down")):
        I.append(Icon(f"nav-tide-{key}", "river", note).add(h.tide(key == "high")))
    return I


# The names the interface asks for today (docs/plans/n3/assets-r3-ic-progress.md, "For the lead"): the same new drawings
# under the old names, so the game shows them before its names are put right. Each is (today's name, new name).
ALIASES = [("ui-tide-high", "nav-tide-high"), ("ui-tide-low", "nav-tide-low"),
           ("up-pave", "boon-wider-lanes"), ("up-pace", "boon-stronger-backs"), ("good-water", "boon-deeper-well"),
           ("good-grain", "boon-longer-rows"), ("ui-extend", "boon-third-room"), ("ui-tools", "boon-faster-hands"),
           ("ui-value", "boon-market-right"), ("good-flour", "boon-millers-guild")]


def today():
    """New-style icons under the names TownHud asks for today, plus the coin, lock and check in the new style."""
    from .draw import Icon as _I
    known = {ic.name: ic for ic in nav() + shopset.boons()}
    out = []
    for old, new in ALIASES:
        src = known[new]
        ic = _I(old, "paper", f"same drawing as {new} (the name the interface asks for today)")
        ic.c, ic.parts = src.c, list(src.parts)
        out.append(ic)
    out.append(Icon("ui-coin", "wheat", "a coin with a stamp (every price)").add(h.coin_stamped()))
    out.append(Icon("ui-lock", "slate", "a padlock: a locked step").add(h.lock()))
    out.append(Icon("ui-check", "meadow", "a tick in a disc: a finished track").add(h.check()))
    return out

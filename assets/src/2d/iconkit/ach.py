# The 40 achievements: one shared medal frame (a scalloped disc: wheat in the first age, river blue in the second)
# with each achievement's own glyph in Paper inside it. Names: "ach-" + the simulation's id with "_" as "-" (first
# age, TownAchievements.All), and the name in lower case with words joined by "-" (second age, RiverAchievements).
from .draw import Icon, place, line, path, poly, circle, rect, dot, ellipse, star_pts
from . import glyphs as g
from . import glyphs2 as h

GLYPH = 38  # the glyph's box inside the medal


def medal(name, colour, glyph, note):
    frame = [poly(star_pts(24, 24, 23.5, 22.4, n=18), "c")]
    return Icon(name, colour, note).add(frame, place(glyph, at=(24, 24), size=GLYPH))


def houses(n):
    if n == 1:
        return g.house(24, 40, 30, 30, "p")
    return [g.house(13, 40, 18, 22, "p", door=False), g.house(35, 40, 18, 22, "p", door=False), g.house(24, 40, 20, 30, "p")]


def boats(n):
    if n == 2:
        return [g.hull(20, 14, 32, 12, "p"), g.hull(28, 34, 32, 12, "p")]
    return [g.hull(24, 8, 26, 10, "p"), g.hull(12, 24, 22, 10, "p"), g.hull(36, 24, 22, 10, "p"), g.hull(24, 40, 26, 10, "p")]


FIRST = [
    ("first-coin", "one coin with a stamp", h.coin_stamped(f="p")),
    ("purse-1k", "a full purse", g.purse("p")),
    ("treasury-20k", "a treasure chest", g.chest("p")),
    ("trips-100", "two footprints", [g.footprint(16, 10, 1.3, "p"), g.footprint(31, 22, 1.3, "p", True)]),
    ("trips-1000", "rows of footprints", h.footprints_rows("p")),
    ("water-200", "a full pail", g.pail("p")),
    ("grain-200", "a sheaf", [line((24, 26), (14, 44)), line((24, 26), (34, 44)), line((24, 26), (24, 44)),
                              ellipse(16, 12, 4, 9, "p", -20), ellipse(32, 12, 4, 9, "p", 20), ellipse(24, 9, 4, 9, "p"), rect(16, 24, 16, 5, "p", 2)]),
    ("flour-50", "a loaf", g.loaf("p")),
    ("villagers-5", "five heads in a row", h.crowd(1, "p")),
    ("villagers-10", "ten heads in two rows", h.crowd(2, "p")),
    ("first-pave", "one slab laid, a trowel", h.slab_trowel("p")),
    ("cobbles", "cobbles", g.cobbles("p")),
    ("all-paved", "every road paved: a square of slabs", h.paved_square("p")),
    ("worn-5", "a worn path", [path("M8 44 Q4 30 18 24 Q32 18 26 4 H36 Q42 22 28 30 Q16 36 20 44Z", "p")]),
    ("bell-10", "the bell", g.bell(f="p")),
    ("pace-10", "a boot", g.boot("p")),
    ("handcart", "a handcart", g.cart("p")),
    ("level-3", "a sprout", h.sprout("p")),
    ("level-6", "a young tree", h.young_tree("p")),
    ("level-9", "a full tree", h.full_tree("p")),
    ("charter", "the charter scroll", g.scroll("p")),
    ("market", "the market stall", g.stall("p")),
    ("mill", "the mill", g.mill("p")),
    ("rate-300", "a rising line", [line((5, 40), (17, 26), (26, 32), (40, 12)), poly([(32, 9), (44, 6), (42, 18)], "p")]),
]

RIVER = [
    ("first-mooring", "a boat tied to a post", [h.mooring_post(9, "p"), g.hull(31, 34, 28, 12, "p")]),
    ("full-hold", "a boat full of sacks", [circle(14, 22, 7, "p"), circle(34, 22, 7, "p"), circle(24, 14, 7, "p"), circle(24, 25, 7, "p"), g.hull(24, 35, 44, 14, "p")]),
    ("ten-holds", "a hold of stacked crates", h.crates_hold("p")),
    ("fifty-holds", "a heaped hold with a flag", h.heaped_hold("p")),
    ("wet-feet", "a boot standing in the water", h.boot_in_water("p")),
    ("ferryman", "the ferry", [line((3, 12), (45, 12)), line((24, 12), (24, 24)), rect(6, 24, 36, 9, "p", 2), g.waves(40)]),
    ("dry-shoes", "a boot on a plank over the water", h.boot_on_plank("p")),
    ("raise-the-bridge", "a drawbridge raised between two towers", h.drawbridge("p")),
    ("toll-keeper", "the toll gate", g.gate("p")),
    ("first-fleece", "a fleece", g.fleece("p")),
    ("weavers-pride", "a rolled bolt of cloth", h.cloth_bolt("p")),
    ("skippers-friend", "a skipper's cap", g.hat("p")),
    ("two-berths", "two mooring posts", [g.post(14, "p"), g.post(34, "p"), g.waves(44, 4, 44)]),
    ("the-crane", "the crane", g.crane("p")),
    ("river-trade", "a boat with a coin on its sail", h.coin_boat("p")),
    ("borough", "three roofs under one flag", h.three_roofs("p")),
]


def achievements():
    return ([medal("ach-" + k, "wheat", gl, n) for k, n, gl in FIRST] +
            [medal("ach-" + k, "river", gl, n) for k, n, gl in RIVER])

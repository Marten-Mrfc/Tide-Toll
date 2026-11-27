# Shop lines (TownHud.ShopIconName: "shop-" + kind, "shop-value-<good>", Bell and BellTrack both "shop-bell"),
# their steps and building offers (wished names, see the progress file), the boons, perks and milestones.
from .draw import Icon, place, line, path, poly, circle, rect, dot, ellipse
from . import glyphs as g
from . import glyphs2 as h


def badge(parts, f="c"):
    """A thing with a coin badge: the price lines. The thing in its own colour, the coin in the icon's gold."""
    return [place(parts, at=(20, 20), size=38), g.coin_badge(f)]


def plus(cx=38, cy=10):
    return [circle(cx, cy, 8, "p"), line((cx - 4, cy), (cx + 4, cy)), line((cx, cy - 4), (cx, cy + 4))]


def shop():
    I = []
    I.append(Icon("shop-villager", "brick", "a villager").add(g.person(24)))
    I.append(Icon("shop-hand", "earth", "an open hand: extra hands").add(g.hand()))
    I.append(Icon("shop-pace", "brick", "a boot with speed lines").add(place(g.boot(), at=(28, 24), size=40), line((4, 16), (10, 16)), line((2, 24), (9, 24)), line((4, 32), (10, 32))))
    I.append(Icon("shop-load", "wheat", "a load: three bundles stacked (every Load step asks this name today)").add(h.bundle_stack()))
    I.append(Icon("shop-load-basket", "wheat", "Load step 1: a basket").add(g.basket()))
    I.append(Icon("shop-load-yoke", "earth", "Load step 2: a yoke with two pails").add(
        path("M4 14 Q24 4 44 14", None), line((9, 13), (9, 24)), line((39, 13), (39, 24)),
        path("M3 24 H15 L13 40 H5Z", "c"), path("M33 24 H45 L43 40 H35Z", "c"), circle(24, 12, 4, "p")))
    I.append(Icon("shop-load-handcart", "earth", "Load step 3: a handcart").add(g.cart()))
    I.append(Icon("shop-tools", "slate", "a heavy hammer over a saw").add(h.heavy_hammer()))
    I.append(Icon("shop-value-water", "wheat", "a water drop (the price pill has the coin)").add(g.drop(f="river")))
    I.append(Icon("shop-value-grain", "wheat", "an ear of grain").add(g.ear(f="bent")))
    I.append(Icon("shop-value-flour", "wheat", "a pale flour sack with a dark band").add(h.flour_sack()))
    I.append(Icon("shop-value-wool", "wheat", "a fleece").add(g.fleece(f="lime")))
    I.append(Icon("shop-value-cloth", "wheat", "a rolled bolt of cloth").add(h.cloth_bolt(f="brick")))
    I.append(Icon("shop-regulars", "brick", "a customer with a purse").add(place(g.person(24), at=(18, 22), size=38), place(g.purse(f="wheat"), at=(35, 34), size=24)))
    I.append(Icon("shop-pave", "stone", "a cobbled road").add(
        poly([(16, 6), (32, 6), (44, 42), (4, 42)], "c"), line((12, 18), (36, 18)), line((8, 30), (40, 30)),
        line((24, 6), (24, 18)), line((18, 18), (16, 30)), line((30, 18), (32, 30)), line((24, 30), (24, 42))))
    I.append(Icon("shop-extend", "brick", "a cottage with a wing as wide as itself").add(h.house_wing()))
    I.append(Icon("shop-bell", "wheat", "the bell").add(g.bell()))
    I.append(Icon("shop-belltrack", "wheat", "a louder bell: the bell with sound").add(
        place(g.bell(), at=(22, 25), size=40), path("M40 12 Q45 20 40 28"), path("M4 12 Q-1 20 4 28")))
    I.append(Icon("shop-building", "brick", "a house on a plot with a plus: a building to buy").add(h.plot_house()))
    I.append(Icon("shop-building-bucket", "river", "the second bucket").add(g.pail()))
    I.append(Icon("shop-building-stall", "brick", "the market stall").add(g.stall()))
    I.append(Icon("shop-building-field", "meadow", "the second field").add(g.field()))
    I.append(Icon("shop-building-mill", "wheat", "the mill").add(g.mill()))
    I.append(Icon("shop-nextage", "brick", "a book with a turning page").add(
        g.book("c"), path("M24 13 Q30 4 40 4 Q34 16 24 25", "p")))
    I.append(Icon("shop-ferry", "earth", "the ferry on its rope").add(
        line((3, 14), (45, 14)), line((16, 14), (16, 24)), line((32, 14), (32, 24)), rect(6, 24, 36, 9, "c", 2), g.waves(40)))
    I.append(Icon("shop-raft", "earth", "a raft of logs").add(g.raft(), g.waves(42)))
    I.append(Icon("shop-rope", "earth", "a coil of rope").add(g.rope()))
    I.append(Icon("shop-berth", "packed", "a mooring post with a boat's bow").add(h.berth()))
    I.append(Icon("shop-crane", "packed", "the landing crane").add(g.crane()))
    I.append(Icon("shop-boatprices", "wheat", "a boat with a coin on its sail").add(h.coin_boat(f="earth")))
    I.append(Icon("shop-toll", "brick", "the toll bar with a coin hanging").add(h.toll_bar()))
    I.append(Icon("shop-winch", "slate", "a drum with rope turns and a crank").add(h.winch_drum()))
    return I


def boons():
    I = []
    I.append(Icon("boon-wider-lanes", "earth", "a road with its edges pushed outwards").add(h.lanes()))
    I.append(Icon("boon-stronger-backs", "earth", "a heavy sack lifted").add(place(g.sack(), at=(18, 26), size=40), g.arrow_up(38, 4, 44, 9, "p")))
    I.append(Icon("boon-deeper-well", "river", "a well and a deep drop").add(g.well(), line((24, 30), (24, 44))))
    I.append(Icon("boon-longer-rows", "meadow", "long furrows").add(
        poly([(4, 40), (44, 40), (44, 10), (4, 22)], "c"), line((4, 28), (44, 19)), line((4, 34), (44, 28)), line((44, 34), (44, 34))))
    I.append(Icon("boon-third-room", "brick", "a cottage with three windows").add(
        g.house(24, 42, 38, 30, "c", door=False), rect(9, 28, 7, 7, "p", 1), rect(20.5, 28, 7, 7, "p", 1), rect(32, 28, 7, 7, "p", 1)))
    I.append(Icon("boon-faster-hands", "earth", "a hand at speed").add(place(g.hand(), at=(28, 24), size=42), line((2, 16), (11, 16)), line((1, 25), (11, 25)), line((2, 34), (11, 34))))
    I.append(Icon("boon-market-right", "wheat", "market scales").add(g.scales()))
    I.append(Icon("boon-millers-guild", "wheat", "the mill with a flour sack at its foot").add(h.mill_sack()))
    return I


def perks():
    I = []
    I.append(Icon("perk-good-neighbours", "brick", "two cottages side by side").add(g.house(15, 42, 20, 26), g.house(33, 42, 20, 26)))
    I.append(Icon("perk-light-feet", "brick", "a boot with a wing").add(h.winged_boot()))
    I.append(Icon("perk-masons-eye", "stone", "a mason's square on a stone").add(
        rect(5, 26, 30, 16, "c", 1.5), poly([(18, 6), (24, 6), (24, 30), (44, 30), (44, 36), (18, 36)], "p")))
    for key, inner, note in (("pace", g.boot("p"), "boot"), ("tools", g.hammer("p"), "hammer"), ("value", h.coin_stamped(f="wheat"), "coin")):
        I.append(Icon(f"milestone-{key}", "stone", f"a roadside milestone with a {note}").add(h.milestone(inner)))
    return I

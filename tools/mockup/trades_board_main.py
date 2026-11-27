#!/usr/bin/env python3
"""Builds docs/plans/n7/mockup/trades-now-and-planned.png from real frames (tools/people-shots.sh at 1920 x 1080 and
1280 x 720) and the planned parts drawn by trades_board.py.

    python3 tools/mockup/trades_board_main.py FRAMES_1080 FRAMES_720 OUT_DIR
"""
import sys, json, pathlib
from PIL import Image, ImageDraw
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from trades_board import (Kit, trades_card, villager_card, shop_train, tooltip, small_tip, stepper_button, trade_cap,
                          text, INK, PAPER, MUTED, CAPTION, BODY, TITLE, STEP_W, STEP_H, CAPS)

BG = (226, 216, 196, 255)


def card_bottom(fr, k):
    """The shop card's lower border: scanned in the column 150 units in from its left edge."""
    W, H = fr.size
    x = W - k.U(16) - k.U(320) + k.U(150)
    rgb = fr.convert("RGB")
    for y in range(H // 3, H):
        if sum(rgb.getpixel((x, y))) < 330 and sum(rgb.getpixel((x, y + 1))) < 330:
            return y + 2
    raise SystemExit("no shop bottom found")


def planned_frames(fr_none, fr_card, k, measures, tag):
    rail = k.U(320) if fr_none.size[0] / k.s >= 1600 else k.U(272)
    a = fr_none.copy()
    _, fits = trades_card(a, k, rail)
    measures[f"{tag} trades card lines (text px, room px)"] = fits
    b = fr_card.copy()
    # Under the planned card: the same place from the frame with the card closed (30 frames later), so the old card's
    # edge does not show through the planned card's 96 percent paper.
    x0, y0 = k.U(16), k.U(16 + 336 + 8)
    b.paste(fr_none.crop((x0, y0, x0 + rail, y0 + k.U(152))), (x0, y0))
    rect, fit2, caps = villager_card(b, k, rail, *VILLAGER)
    measures[f"{tag} villager card line 2 (text, px, room px)"] = fit2
    measures[f"{tag} trade caps (x, y, w, h)"] = caps
    s, info = shop_train(fr_none.convert("RGB"), k, rail, k.U(244), card_bottom(fr_none, k))
    measures[f"{tag} shop Train row"] = info
    # The whole planned screen: Trades card and Train row together.
    whole = s.copy()
    trades_card(whole, k, rail)
    return a, b, s, whole, rail


VILLAGER = None


def label(img, xy, s, k, size=TITLE, colour=INK, kind="serif"):
    text(img, k, xy, s, kind, size, colour)


def states_panel(measures):
    """Stepper rest, hover, pressed, disabled at both scales (true size and 3x), the trade line's tooltip and a cap's."""
    k1, k7 = Kit(1.0), Kit(0.78)
    P = Image.new("RGBA", (2240, 600), PAPER)
    kb = Kit(1.0)
    label(P, (24, 14), "The stepper's states, a trade cap's, and the tooltips (true size; below each, the same at 3 x)", kb)
    x = 24
    for k, tag in ((k1, "1920 x 1080 (s 1.00)"), (k7, "1280 x 720 (s 0.78)")):
        text(P, kb, (x, 56), tag, "ui", CAPTION, MUTED)
        xx = x
        for look in ("rest", "hover", "pressed", "disabled"):
            tile = Image.new("RGBA", (k.U(STEP_W) + 12, k.U(STEP_H) + 12), PAPER)
            stepper_button(tile, k, 6, 6, "+", look)
            P.alpha_composite(tile, (xx, 84))
            big = tile.resize((tile.width * 3, tile.height * 3), Image.NEAREST)
            P.alpha_composite(big, (xx, 130))
            text(P, kb, (xx, 130 + big.height + 4), look, "ui", CAPTION, MUTED)
            xx += big.width + 16
        measures[f"stepper button at {tag} (px)"] = (k.U(STEP_W), k.U(STEP_H))
        xx += 10
        for i, (cur, likes, look, nm) in enumerate(((True, False, "rest", "current"), (False, True, "rest", "liked"),
                                                    (False, False, "hover", "hover"))):
            tile = Image.new("RGBA", (k.U(40) + 12, k.U(32) + 12), PAPER)
            trade_cap(tile, k, 6, 6, CAPS[2 if cur else (2 if likes else 1)][0], cur, likes, look)
            P.alpha_composite(tile, (xx, 84))
            big = tile.resize((tile.width * 3, tile.height * 3), Image.NEAREST)
            P.alpha_composite(big, (xx, 130))
            text(P, kb, (xx, 130 + big.height + 4), nm, "ui", CAPTION, MUTED)
            xx += big.width + 16
        measures[f"trade cap at {tag} (px)"] = (k.U(40), k.U(32))
        x = xx + 30
    # Tooltips at 1920 x 1080 (true size).
    tooltip(P, k1, 24, 420 - 60, "Reapers", "4 villagers for 4 places at the fields. Work 20% faster and sell grain for "
            "10% more. Trained 3 of 10.", [])
    text(P, kb, (24, 340), "Trade line's tooltip, as written", "ui", CAPTION, MUTED)
    tooltip(P, k1, 360, 420 - 60, "Reapers", "Work at the fields; 1 helping at the well now.",
            [("Members", "4 for 4 places"), ("Bonus", "20% faster, grain 10% more"), ("Trained", "3 of 10")])
    text(P, kb, (360, 340), "Best fix: the shop tip's facts", "ui", CAPTION, MUTED)
    small_tip(P, k1, 700, 360, "Reaper: likes this work, +5%")
    text(P, kb, (700, 340), "A trade cap's tooltip", "ui", CAPTION, MUTED)
    tooltip(P, k7, 1010, 360, "Reapers", "4 villagers for 4 places at the fields. Work 20% faster and sell grain for "
            "10% more. Trained 3 of 10.", [])
    text(P, kb, (1010, 340), "Trade line's tooltip at 1280 x 720", "ui", CAPTION, MUTED)
    # The 272 rail (interface size 150 percent at 1280 x 720: s 1.17), true size: what does not fit is dropped whole.
    k5 = Kit(1.17)
    tile = Image.new("RGBA", (k5.U(16) * 2 + k5.U(272), k5.U(16 + 336 + 8) + k5.U(156) + k5.U(16)), (127, 163, 91, 255))
    _, fits = trades_card(tile, k5, k5.U(272), rows=[("trade-water", "Water carriers", "2 waiting", 1),
                                                     ("trade-grain", "Reapers", "1 helping", 4), ("trade-flour", "Millers", "", 2)])
    measures["272 rail at 1280 x 720, 150 percent: trades card lines (text px, room px)"] = fits
    crop = tile.crop((0, k5.U(16 + 336), tile.width, tile.height))
    P.alpha_composite(crop, (1370, 360))
    text(P, kb, (1370, 340), "1280 x 720 at 150 percent (272 rail): the captions do not fit and are dropped", "ui", CAPTION, MUTED)
    tile2 = Image.new("RGBA", tile.size, (127, 163, 91, 255))
    _, fit2, _ = villager_card(tile2, k5, k5.U(272), "Quenild the younger", "Lives near the market", "likes the millstone",
                               "Drawing water at the well", 107, "1,491", current=1, liked=3)
    measures["272 rail at 1280 x 720, 150 percent: villager line 2 (text, px, room)"] = fit2
    return P, (tile2.crop((0, k5.U(16 + 336), tile.width, tile.height)))


def main():
    global VILLAGER
    d1080, d720, out = map(pathlib.Path, sys.argv[1:4])
    out.mkdir(parents=True, exist_ok=True)
    measures = {}
    # The villager the frames selected (its name, home, doing, trips and coin as the real card shows them).
    names = json.loads(pathlib.Path(sys.argv[4]).read_text()) if len(sys.argv) > 4 else {}
    panels = {}
    for d, s, tag in ((d1080, 1.0, "1920x1080"), (d720, 0.78, "1280x720")):
        k = Kit(s)
        none = Image.open(d / "people-none.png").convert("RGBA")
        cardf = Image.open(d / "people-card.png").convert("RGBA")
        v = names.get(tag, {})
        VILLAGER = (v.get("name", "Joss"), v.get("home", "Lives by the well lane"), "likes the sickle",
                    v.get("now", "On the way to the well"), v.get("trips", 160), v.get("earned", "1,921"))
        a, b, sh, whole, rail = planned_frames(none, cardf, k, measures, tag)
        cw = k.U(16) * 2 + rail
        ch = k.U(560)
        W = none.size[0]
        sc = (W - cw, 0, W, ch)
        panels[tag] = [("Nothing selected", none.crop((0, 0, cw, ch)), a.crop((0, 0, cw, ch))),
                       ("A villager selected", cardf.crop((0, 0, cw, ch)), b.crop((0, 0, cw, ch))),
                       ("The shop's People group", none.crop(sc), sh.crop(sc))]
        if tag == "1920x1080":
            panels["whole"] = whole
            whole.convert("RGB").save(out / "trades-planned-whole-1920x1080.png")
    kb = Kit(1.0)
    half = 1100
    gap = 24
    Wb = 2 * half + 3 * gap
    rows_h = [70 + panels["1920x1080"][0][1].height + 50, panels["1280x720"][0][1].height + 50]
    states, card272 = states_panel(measures)
    states.alpha_composite(card272, (1370 + card272.width + 24, 360))
    Hb = 90 + sum(rows_h) + 60 + 1080 + 60 + states.height + 30
    B = Image.new("RGBA", (Wb, Hb), BG)
    d = ImageDraw.Draw(B)
    label(B, (gap, 16), "Trades: now beside planned (the interface)", kb, 30, INK)
    text(B, kb, (gap, 56), "Real frames from tools/people-shots.sh; planned parts drawn at true size with the game's fonts, "
         "colours and sizes (notes.md says which pixels are real).", "ui", CAPTION, MUTED)
    y = 90
    for tag in ("1920x1080", "1280x720"):
        ps = panels[tag]
        for side, x0 in (("Now", gap), ("Planned", half + 2 * gap)):
            if tag == "1920x1080":
                label(B, (x0, y), side, kb, 30)
            xx = x0
            yy = y + (40 if tag == "1920x1080" else 0)
            for title, now, plan in ps:
                text(B, kb, (xx, yy), f"{title}, {tag.replace('x', ' x ')}", "ui", CAPTION, MUTED)
                B.alpha_composite(now if side == "Now" else plan, (xx, yy + 22))
                xx += now.width + 14
        d.line([(half + gap * 1.5, y), (half + gap * 1.5, y + rows_h[0 if tag == "1920x1080" else 1])], fill=INK, width=2)
        y += rows_h[0 if tag == "1920x1080" else 1]
    y += 20
    label(B, (gap, y), "Planned, the whole screen at 1920 x 1080 (Trades card and Train row; nothing selected)", kb, 24)
    B.alpha_composite(panels["whole"], ((Wb - 1920) // 2, y + 40))
    y += 40 + 1080 + 20
    B.alpha_composite(states, ((Wb - states.width) // 2, y))
    B.convert("RGB").save(out / "trades-now-and-planned.png", optimize=True)
    (out / "measures.json").write_text(json.dumps(measures, indent=1))
    print("board", B.size)


if __name__ == "__main__":
    main()

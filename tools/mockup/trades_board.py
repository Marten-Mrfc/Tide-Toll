#!/usr/bin/env python3
"""n7 step 2 mock-up: the trades' interface, now beside planned (docs/plans/n7/mockup/trades-now-and-planned.png).

    python3 tools/mockup/trades_board.py FRAMES_1080 FRAMES_720 [OUT]

FRAMES_* are folders of real frames from tools/people-shots.sh (people-none.png, people-card.png) at 1920 x 1080 and
1280 x 720. The planned parts are drawn at true size over those frames with the game's fonts (assets/fonts), the
colours, radii and line weights of game/scripts/UiKit*.cs and the zones of game/scripts/HudZones.cs; every number
is in sheet units and scaled as the game scales it (UiKit.U: round(units * s); UiKit.Type: round(size * s)).
"""
import io, sys, pathlib
from PIL import Image, ImageDraw, ImageFont
import cairosvg

ROOT = pathlib.Path(__file__).resolve().parents[2]
FONTS = ROOT / "assets" / "fonts"
ICONS = ROOT / "assets" / "icons"
SS = 4  # supersampling for shapes (the game's StyleBoxFlat draws antialiased)


def hexc(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def lerp(c1, c2, t):
    return tuple(round(c1[i] + (c2[i] - c1[i]) * t) for i in range(3)) + (255,)


# UiKit.cs / UiKit.Styles.cs / Palette.cs
PAPER, INK = hexc("F2EAD8"), hexc("2B2620")
WHEAT = hexc("C9A55A")
MUTED = lerp(INK, PAPER, 0.4)          # UiKit.Muted
MUTEDNUM = lerp(INK, PAPER, 0.3)       # UiKit.MutedNumber
DISABLED = lerp(INK, PAPER, 0.55)      # UiKit.DisabledInk
PAPERLIGHT = lerp(PAPER, (255, 255, 255), 0.45)
PAPERDARKER = lerp(PAPER, INK, 0.12)
WASH = lerp(WHEAT, PAPER, 0.7)          # ShopRow: Wheat.Lerp(Paper, 0.7)
CAPTION, BODY, TITLE, LARGE = 15, 17, 21, 30


class Kit:
    """The scale of one canvas: s = UiKit.ScaleFor(window height) (x interface size)."""

    def __init__(self, s):
        self.s = s
        self._f = {}

    def U(self, u):
        return round(u * self.s)

    def T(self, size):
        return max(1, round(size * self.s))

    def font(self, kind, size):
        key = (kind, size)
        if key not in self._f:
            path = {"ui": "TideLegible-Regular.ttf", "bold": "TideLegible-Bold.ttf", "serif": "Alegreya-Variable.ttf"}[kind]
            self._f[key] = ImageFont.truetype(str(FONTS / path), self.T(size), layout_engine=ImageFont.Layout.RAQM)
        return self._f[key]


def icon(name, px):
    png = cairosvg.svg2png(url=str(ICONS / f"{name}.svg"), output_width=px * 4, output_height=px * 4)
    return Image.open(io.BytesIO(png)).convert("RGBA").resize((px, px), Image.LANCZOS)


def text_w(k, kind, size, s, tnum=False):
    f = k.font(kind, size)
    return f.getlength(s, features=["tnum"] if tnum else None)


def text(img, k, xy, s, kind="ui", size=BODY, colour=INK, box_h=None, align="left", box_w=None, tnum=False):
    """A Label: the line (ascent + descent) centred in its box (VerticalAlignment.Center), as Godot does."""
    f = k.font(kind, size)
    asc, desc = f.getmetrics()
    x, y = xy
    if box_h is not None:
        y = y + (box_h - (asc + desc)) / 2
    w = f.getlength(s, features=["tnum"] if tnum else None)
    if align == "right":
        x = x + box_w - w
    elif align == "center":
        x = x + (box_w - w) / 2
    d = ImageDraw.Draw(img)
    d.text((round(x), round(y)), s, font=f, fill=colour, anchor="la", features=["tnum"] if tnum else None)
    return w


def shape(img, rect, radius, fill=None, border=None, bw=0, wash=None):
    """A StyleBoxFlat: rounded rect, fill (with alpha), border inside the rect. wash=(fraction, fade_px, colour)."""
    x, y, w, h = rect
    lay = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    r = radius * SS
    if border is not None and bw > 0:
        d.rounded_rectangle((0, 0, w * SS - 1, h * SS - 1), r, fill=border)
        inner = (bw * SS, bw * SS, w * SS - 1 - bw * SS, h * SS - 1 - bw * SS)
        mask = Image.new("L", lay.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle(inner, max(0, r - bw * SS), fill=255)
        fl = Image.new("RGBA", lay.size, fill if fill else (0, 0, 0, 0))
        if wash:
            frac, fade, col = wash
            wl = Image.new("RGBA", lay.size, (0, 0, 0, 0))
            wd = ImageDraw.Draw(wl)
            edge = int(w * SS * frac)
            for xx in range(0, min(w * SS, edge + fade * SS)):
                a = 255 if xx < edge else int(255 * (1 - (xx - edge) / (fade * SS)))
                wd.line([(xx, 0), (xx, h * SS)], fill=col[:3] + (a,))
            fl = Image.alpha_composite(fl, wl)
        lay.paste(fl, (0, 0), mask)
    else:
        d.rounded_rectangle((0, 0, w * SS - 1, h * SS - 1), r, fill=fill)
    lay = lay.resize((w, h), Image.LANCZOS)
    img.alpha_composite(lay, (x, y))


def card(img, k, rect):
    """UiKit.CardBox(Card): paper at 96 percent, ink border U(2), corner U(4)."""
    shape(img, rect, k.U(4), fill=PAPER[:3] + (245,), border=INK, bw=max(1, k.U(2)))


def hairline(img, x0, x1, y, alpha=0.16):
    d = ImageDraw.Draw(img)
    d.line([(x0, y), (x1, y)], fill=INK[:3] + (round(255 * alpha),))


# ---------------------------------------------------------------------------------------------------------------- zones
def person_rect(k, rail):
    """HudZones.Person: x 16, y 16 + 336 + 8, the ledger's width."""
    return (k.U(16), k.U(16 + 336 + 8), rail)


STEP_W, STEP_H, COUNT_W = 32, 24, 28   # the stepper (units): two pills 32 x 24 around a 28-unit count cell


def stepper_button(img, k, x, y, glyph, look):
    w, h = k.U(STEP_W), k.U(STEP_H)
    dy = 0
    if look == "rest":
        shape(img, (x, y, w, h), h // 2, fill=PAPER, border=MUTED, bw=1)
        col = INK
    elif look == "hover":
        shape(img, (x, y, w, h), h // 2, fill=PAPERLIGHT, border=INK, bw=1)
        col = INK
    elif look == "pressed":
        shape(img, (x, y, w, h), h // 2, fill=PAPERDARKER, border=INK, bw=1)
        col, dy = INK, max(1, k.U(1))
    else:  # disabled: the blocked pill (ink at 30 percent, 1), disabled ink
        shape(img, (x, y, w, h), h // 2, fill=PAPER, border=INK[:3] + (77,), bw=1)
        col = DISABLED
    # The glyph: a drawn bar (and cross bar), 10 units long, 2 thick: a font's minus sits off centre.
    d = ImageDraw.Draw(img)
    cx, cy = x + w / 2, y + h / 2 + dy
    L, t = k.U(10) / 2, max(1, k.U(2))
    d.rectangle((round(cx - L), round(cy - t / 2), round(cx + L) - 1, round(cy - t / 2) + t - 1), fill=col)
    if glyph == "+":
        d.rectangle((round(cx - t / 2), round(cy - L), round(cx - t / 2) + t - 1, round(cy + L) - 1), fill=col)


def stepper(img, k, right, y, count, minus="rest", plus="rest"):
    """Right-aligned at `right`, vertically in a 28-unit line starting at y."""
    bw, cw, g = k.U(STEP_W), k.U(COUNT_W), k.U(2)
    by = y + (k.U(28) - k.U(STEP_H)) // 2
    xp = right - bw
    xc = xp - g - cw
    xm = xc - g - bw
    stepper_button(img, k, xm, by, "-", minus)
    text(img, k, (xc, y), str(count), "bold", BODY, INK, box_h=k.U(28), align="center", box_w=cw, tnum=True)
    stepper_button(img, k, xp, by, "+", plus)
    return xm


TRADES = [  # (icon, name, status) as the design draws the card
    ("trade-water", "Water carriers", "", 1),
    ("trade-grain", "Reapers", "1 helping", 4),
    ("trade-flour", "Millers", "", 2),
]


def trades_card(img, k, rail, states=None, rows=TRADES, hands=5):
    """The Trades card (design: 156 high): heading 28, three lines of 28, the foot; returns its rect."""
    x, y, w = person_rect(k, rail)
    h = k.U(156)
    card(img, k, (x, y, w, h))
    pad = k.U(12)
    inner_r = x + w - pad
    # Heading: "Trades" (title, serif) and, at the right, "Keep filled" and the small On pill (the shop's On state).
    text(img, k, (x + pad, y + k.U(8)), "Trades", "serif", TITLE, INK, box_h=k.U(28))
    pw, ph = k.U(40), k.U(22)
    py = y + k.U(8) + (k.U(28) - ph) // 2
    shape(img, (inner_r - pw, py, pw, ph), ph // 2, fill=INK)
    text(img, k, (inner_r - pw, py), "On", "bold", CAPTION, PAPER, box_h=ph, align="center", box_w=pw)
    cw = text_w(k, "ui", CAPTION, "Keep filled")
    text(img, k, (inner_r - pw - k.U(8) - cw, y + k.U(8)), "Keep filled", "ui", CAPTION, MUTED, box_h=k.U(28))
    fits = []
    for i, (ic, name, status, n) in enumerate(rows):
        ly = y + k.U(40 + 28 * i)
        img.alpha_composite(icon(ic, k.U(20)), (x + pad, ly + k.U(4)))
        nx = x + pad + k.U(28)
        nw = text(img, k, (nx, ly), name, "ui", BODY, INK, box_h=k.U(28))
        st = (states or {}).get(i, {})
        left = stepper(img, k, inner_r, ly, n, st.get("minus", "rest" if n > 0 else "disabled"),
                       st.get("plus", "rest" if hands > 0 else "disabled"))
        if status:
            sx = nx + nw + k.U(8)
            sw = text_w(k, "ui", CAPTION, status)
            room = left - k.U(8) - sx
            fits.append((name, status, round(sw), round(room)))
            if sw <= room:
                text(img, k, (sx, ly), status, "ui", CAPTION, MUTEDNUM, box_h=k.U(28))
        fits.append((name, "", round(nw), round(left - k.U(8) - nx)))
    hairline(img, x + pad, inner_r, y + k.U(126))
    text(img, k, (x + pad, y + k.U(128)), f"Hands {hands} · take whatever pays best", "ui", CAPTION, MUTEDNUM, box_h=k.U(20))
    return (x, y, w, h), fits


def coin_text(img, k, x, y, box_h, before, amount, colour):
    """TownHud.SetCoinText: words, the coin icon (14), the amount (tabular)."""
    w = text(img, k, (x, y), before, "ui", CAPTION, colour, box_h=box_h)
    cs = k.U(14)
    img.alpha_composite(icon("ui-coin", cs), (round(x + w + k.U(1)), y + (box_h - cs) // 2))
    text(img, k, (x + w + cs + k.U(5), y), amount, "ui", CAPTION, colour, box_h=box_h, tnum=True)


CAPS = [("trade-helper", "Helper"), ("trade-water", "Water carrier"), ("trade-grain", "Reaper"), ("trade-flour", "Miller")]
CAP_W, CAP_H = 40, 32


def trade_cap(img, k, x, y, ic, current, likes, look="rest"):
    w, h = k.U(CAP_W), k.U(CAP_H)
    if current:
        shape(img, (x, y, w, h), k.U(4), fill=WHEAT)
    elif look == "hover":
        shape(img, (x, y, w, h), k.U(4), fill=PAPERLIGHT, border=INK, bw=1)
    else:
        shape(img, (x, y, w, h), k.U(4), fill=PAPER, border=MUTED, bw=1)
    s = k.U(20)
    img.alpha_composite(icon(ic, s), (x + (w - s) // 2, y + (h - s) // 2))
    if likes:
        r = max(2, k.U(3))
        cx, cy = x + w - k.U(6), y + k.U(6)
        ImageDraw.Draw(img).ellipse((cx - r, cy - r, cx + r, cy + r), fill=INK)


def villager_card(img, k, rail, name, home, likes, now, trips, earned, current=2, liked=2, hover_cap=None):
    """The villager's card, planned (design: 152 high): the second line says what it likes; the Trade line."""
    x, y, w = person_rect(k, rail)
    h = k.U(152)
    card(img, k, (x, y, w, h))
    pad = k.U(12)
    inner = w - 2 * pad
    text(img, k, (x + pad, y + k.U(8)), name, "bold", BODY, INK, box_h=k.U(24))
    # The close cross: as built (Title size, Muted, top right).
    text(img, k, (x + w - k.U(6) - k.U(22), y + k.U(4)), "×", "ui", TITLE, MUTED, box_h=k.U(28), align="center", box_w=k.U(22))
    line2 = f"{home} · {likes}"
    l2w = text_w(k, "ui", CAPTION, line2)
    fit2 = (line2, round(l2w), inner)
    if l2w > inner:   # does not fit: the home line gives way (the leaning is the new information)
        line2 = f"{home.replace('Lives by the ', 'By the ').replace('Lives near the ', 'Near the ')} · {likes}"
    text(img, k, (x + pad, y + k.U(33)), line2, "ui", CAPTION, MUTED, box_h=k.U(20))
    text(img, k, (x + pad, y + k.U(52)), now, "ui", CAPTION, INK, box_h=k.U(20))
    coin_text(img, k, x + pad, y + k.U(71), k.U(20), f"{trips} trips · earned ", earned, MUTED)
    ty = y + k.U(104)
    text(img, k, (x + pad, ty), "Trade", "ui", CAPTION, MUTED, box_h=k.U(CAP_H))
    cx = x + pad + k.U(48)
    caps = []
    for i, (ic, nm) in enumerate(CAPS):
        trade_cap(img, k, cx, ty, ic, i == current, i == liked, "hover" if hover_cap == i else "rest")
        caps.append((cx, ty, k.U(CAP_W), k.U(CAP_H)))
        cx += k.U(CAP_W) + k.U(4)
    return (x, y, w, h), fit2, caps


# ---------------------------------------------------------------------------------------------------------------- shop
def shop_train(frame, k, rail, insert_y, card_bottom, price="640", frac=0.55):
    """Inserts the Train row after Load: the rows below move down one pitch (48 + 4), the card grows with them."""
    W, H = frame.size
    out = frame.copy()
    cl = W - k.U(16) - rail
    pitch = k.U(48) + k.U(4)
    below = frame.crop((cl, insert_y, W - k.U(16), card_bottom))
    out.paste(below, (cl, insert_y + pitch))
    # The new band: a blank slice of the card (1 px row inside the 4-unit gap above the heading), repeated.
    gap_y = insert_y - max(1, k.U(2))
    strip = frame.crop((cl, gap_y, W - k.U(16), gap_y + 1))
    for yy in range(insert_y, insert_y + pitch):
        out.paste(strip, (cl, yy))
    # The row: as ShopRow lays out a tall row (icon 24 at (8, 4) of the row; name at 38; pill 104 x 32 at the right).
    rl, rr = cl + k.U(8), W - k.U(16) - k.U(8)
    img = out.convert("RGBA")
    img.alpha_composite(icon("trade-grain", k.U(24)), (rl + k.U(8), insert_y + k.U(4)))
    nx = rl + k.U(38)
    pw, ph = k.U(104), k.U(32)
    px = rr - k.U(6) - pw
    nw = text(img, k, (nx, insert_y + k.U(16) - k.U(12)), "Train the reapers", "ui", BODY, INK, box_h=k.U(24))
    room = px - k.U(6) - nx
    stepw = text_w(k, "ui", CAPTION, "3")
    if nw + k.U(6) + stepw <= room:
        text(img, k, (nx + nw + k.U(6), insert_y + k.U(16) - k.U(12)), "3", "ui", CAPTION, MUTEDNUM, box_h=k.U(24))
    l2 = "Reapers work and sell 4% better"
    l2w = text_w(k, "ui", CAPTION, l2)
    text(img, k, (nx, insert_y + k.U(38) - k.U(10)), l2, "ui", CAPTION, MUTEDNUM, box_h=k.U(20))
    # The pill, "not yet": paper, the wheat wash from the left, muted border 1, coin at 9, price right-aligned.
    shape(img, (px, insert_y, pw, ph), ph // 2, fill=PAPER, border=MUTED, bw=1, wash=(frac, k.U(8), WASH))
    cs = k.U(16)
    img.alpha_composite(icon("ui-coin", cs), (px + k.U(9), insert_y + (ph - cs) // 2))
    text(img, k, (px, insert_y), price, "ui", BODY, MUTEDNUM, box_h=ph, align="right", box_w=pw - k.U(12), tnum=True)
    return img, {"name_w": round(nw), "name_room": round(room), "line2_w": round(l2w), "line2_room": round(rr - nx),
                 "card_bottom_after": card_bottom + pitch, "shop_max": H - k.U(104)}


def tooltip(img, k, x, y, title, sentence, facts):
    """The shop tip's family (TownHud.ShopTip.cs): 296 wide, title (body bold), sentence (caption), hairline, facts."""
    w = k.U(296)
    pad = k.U(12)
    f = k.font("ui", CAPTION)
    words, lines, cur = sentence.split(), [], ""
    for wd in words:
        t = (cur + " " + wd).strip()
        if f.getlength(t) <= w - 2 * pad:
            cur = t
        else:
            lines.append(cur)
            cur = wd
    lines.append(cur)
    lh = k.U(19)
    h = pad + k.U(24) + k.U(2) + lh * len(lines) + (k.U(9) + lh * len(facts) if facts else 0) + pad
    card(img, k, (x, y, w, h))
    yy = y + pad
    text(img, k, (x + pad, yy), title, "bold", BODY, INK, box_h=k.U(24))
    yy += k.U(26)
    for ln in lines:
        text(img, k, (x + pad, yy), ln, "ui", CAPTION, INK, box_h=lh)
        yy += lh
    if facts:
        yy += k.U(4)
        hairline(img, x + pad, x + w - pad, yy, 0.3)
        yy += k.U(5)
        for lab, val in facts:
            text(img, k, (x + pad, yy), lab, "ui", CAPTION, MUTED, box_h=lh)
            text(img, k, (x + pad + k.U(76), yy), val, "ui", CAPTION, INK, box_h=lh)
            yy += lh
    return (x, y, w, h)


def small_tip(img, k, x, y, s):
    """A one-line tooltip (the plain tip card: paper, ink border, 8 padding, caption)."""
    pad = k.U(8)
    w = round(text_w(k, "ui", CAPTION, s)) + 2 * pad
    h = k.U(20) + 2 * pad
    card(img, k, (x, y, w, h))
    text(img, k, (x + pad, y + pad), s, "ui", CAPTION, INK, box_h=k.U(20))
    return (x, y, w, h)

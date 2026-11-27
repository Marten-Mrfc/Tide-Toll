using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 3, layout sheet version 2, section 2: every zone's rectangle for a canvas, as pure functions (no nodes). The
/// canvas is the visible rectangle in canvas pixels and <c>s</c> the interface scale (UiKit.ScaleFor of the window's
/// height); the sheet's numbers are units of <c>s</c> pixels, anchored to the window's edges with whole-pixel offsets.
/// The interface (TownHud.Zones.cs) and the layout test (game-layout) both read it.
/// </summary>
public readonly struct HudZones
{
    public const float Margin = 16, Gap = 8;
    public const float PurseH = 112, GoalH = 104, StripH = 40, NoteH = 56, LedgerH = 336;
    public const float BellW = 128, TimerH = 48, NavAchW = 112, NavBtn = 48;
    public const float BannerH = 96, ChoiceMaxH = 200, LineH = 40, TipW = 296, MessageMaxW = 560;
    public const float MiddleTop = 120, MiddleBottomGap = 64;
    /// <summary>n7: the villager's card, docked in the left column under the ledger; n7 trades: the Trades card shares
    /// the zone (both 156 high, crossfading).</summary>
    public const float PersonH = 156;

    public readonly Vector2 Canvas;
    public readonly float S;
    public HudZones(Vector2 canvas, float s) { Canvas = canvas; S = s; }

    /// <summary>The sheet canvas: the visible rectangle divided by s.</summary>
    public float W => Canvas.X / S;
    public float H => Canvas.Y / S;
    public int P(float units) => Mathf.RoundToInt(units * S);
    /// <summary>Rail width R in sheet units: 320 when W is 1600 or more, else 272.</summary>
    public float RailUnits => RailFor(W);
    public static float RailFor(float sheetW) => sheetW >= 1600 ? 320 : 272;
    public int Rail => P(RailUnits);

    private Rect2 FromLeft(float x, float y, float w, float h) => new(P(x), P(y), P(w), P(h));
    private Rect2 FromBottomLeft(float x, float fromBottom, float w, float h) => new(P(x), Canvas.Y - P(fromBottom), P(w), P(h));

    // Ledger and its four sections.
    public Rect2 Ledger => FromLeft(Margin, Margin, RailUnits, LedgerH);
    public Rect2 Purse => FromLeft(Margin, Margin, RailUnits, PurseH);
    public Rect2 Goal => FromLeft(Margin, Margin + PurseH + Gap, RailUnits, GoalH);
    public Rect2 Strip => FromLeft(Margin, Margin + PurseH + GoalH + 2 * Gap, RailUnits, StripH);
    public Rect2 Note => FromLeft(Margin, Margin + PurseH + GoalH + StripH + 3 * Gap, RailUnits, NoteH);

    /// <summary>n7 (villagers as people): the card of the villager clicked on, under the ledger, the ledger's width.</summary>
    public Rect2 Person => FromLeft(Margin, Margin + LedgerH + Gap, RailUnits, PersonH);

    // Timers.
    public Rect2 Bell => FromBottomLeft(Margin, 64, BellW, TimerH);
    public Rect2 Tide => FromBottomLeft(Margin + BellW + Gap, 64, RailUnits - BellW - Gap, TimerH);

    // Shop: R wide at the right, as tall as its rows, at most H - 104.
    public float ShopMaxHeight => Canvas.Y - P(104);
    public Rect2 Shop(float contentPx)
    {
        float h = Math.Min(contentPx, ShopMaxHeight);
        return new Rect2(Canvas.X - P(Margin) - Rail, P(Margin), Rail, h);
    }
    public Rect2 ShopMax => Shop(float.MaxValue);

    // Navigation, right-aligned at W - 16: Achievements, this age's page, centre the view, pause.
    public Rect2 NavButton(int i)
    {
        float right = Canvas.X - P(Margin), y = Canvas.Y - P(64), h = P(NavBtn);
        // From the right: pause (3), home (2), page (1), achievements (0).
        float x = right;
        for (int k = 3; k >= i; k--)
        {
            float w = P(k == 0 ? NavAchW : NavBtn);
            x -= w;
            if (k == i) return new Rect2(x, y, w, h);
            x -= P(Gap);
        }
        return new Rect2();
    }
    public Rect2 Nav => NavButton(0).Merge(NavButton(3));

    // Message and line: centred.
    public float MessageWUnits => Math.Min(MessageMaxW, W - 2 * RailUnits - 80);
    public Rect2 Message(float hUnits)
    {
        float w = P(MessageWUnits);
        return new Rect2(MathF.Round((Canvas.X - w) / 2f), P(Margin), w, P(hUnits));
    }
    public Rect2 Banner => Message(BannerH);
    public Rect2 Line
    {
        get
        {
            float w = P(MessageWUnits);
            return new Rect2(MathF.Round((Canvas.X - w) / 2f), Canvas.Y - P(56), w, P(LineH));
        }
    }

    /// <summary>The middle: x 16+R+8 to W-16-R-8, y 120 to H-64.</summary>
    public Rect2 Middle
    {
        get
        {
            float x0 = P(Margin + Gap) + Rail, x1 = Canvas.X - P(Margin + Gap) - Rail;
            return new Rect2(x0, P(MiddleTop), x1 - x0, Canvas.Y - P(MiddleBottomGap) - P(MiddleTop));
        }
    }

    /// <summary>A page: min(960, W-64) x min(600, H-96), between the rails when that leaves 640 or more.</summary>
    public Rect2 Page
    {
        get
        {
            float w = Math.Min(960, W - 64), h = Math.Min(600, H - 96);
            float between = W - 2 * (Margin + RailUnits + Gap);
            if (between >= 640) w = Math.Min(w, between);
            float pw = P(w), ph = P(h);
            return new Rect2(MathF.Round((Canvas.X - pw) / 2f), MathF.Round((Canvas.Y - ph) / 2f), pw, ph);
        }
    }

    /// <summary>The modal card (480 wide, centred).</summary>
    public Rect2 Modal(float hUnits) => new(MathF.Round((Canvas.X - P(480)) / 2f), MathF.Round((Canvas.Y - P(hUnits)) / 2f), P(480), P(hUnits));

    public enum TipSide { Left, Right, Above }

    /// <summary>
    /// Where a tooltip of this size goes for its owner: beside it on the given side (left of a shop row, right of the
    /// ledger and timers, above a navigation button), kept inside the window, off its owner and off a showing message
    /// (<paramref name="avoid"/>; empty for none). Falls back to the other sides when the first does not fit.
    /// </summary>
    public Rect2 TipRect(Rect2 owner, Vector2 size, TipSide side, Rect2 avoid = default)
    {
        float g = P(Gap), m = P(4);
        var win = new Rect2(Vector2.Zero, Canvas).Grow(-m);
        Span<TipSide> order = stackalloc TipSide[3];
        order[0] = side;
        order[1] = side == TipSide.Above ? TipSide.Left : TipSide.Above;
        order[2] = side == TipSide.Right ? TipSide.Left : TipSide.Right;
        Rect2 best = default;
        for (int k = 0; k < 3; k++)
        {
            Vector2 at = order[k] switch
            {
                TipSide.Left => new Vector2(owner.Position.X - g - size.X, owner.Position.Y),
                TipSide.Right => new Vector2(owner.End.X + g, owner.Position.Y),
                _ => new Vector2(owner.End.X - size.X, owner.Position.Y - g - size.Y),
            };
            // Keep inside the window, sliding along the side it lies on (never back over the owner).
            // (Never Math.Clamp: a tip larger than the window must not throw; it keeps to the top left then.)
            if (order[k] == TipSide.Above) at.X = Math.Max(win.Position.X, Math.Min(at.X, win.End.X - size.X));
            else at.Y = Math.Max(win.Position.Y, Math.Min(at.Y, win.End.Y - size.Y));
            var r = new Rect2(at.Round(), size);
            if (k == 0) best = r;
            bool inside = win.Encloses(r), off = !r.Intersects(owner), clear = avoid.Size == Vector2.Zero || !r.Intersects(avoid);
            if (inside && off && clear) return r;
            if (inside && off && !win.Encloses(best)) best = r;
        }
        return best;
    }
}

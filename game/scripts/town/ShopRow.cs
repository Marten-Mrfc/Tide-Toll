using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Sheet v3 (docs/plans/n4/shop-sheet-v3.md): one shop row. The whole row is one Button with no text and no style of
/// its own (its children ignore the mouse); the row draws its own band (hover ink 7 percent, pressed 14 percent with
/// the content 1 down, none when blocked). Tall (56): icon with its key cap under it, name and step, line 2, the pill
/// on line 1. Compact (40): icon (20), the same name, the pill (28 tall). The pill has four states: affordable (wheat),
/// not yet (paper, a wheat wash from the left clipped to the pill's outline), blocked (paper, faint border, dimmed
/// coin), On (ink). Nothing of a pill is drawn outside its outline. Positions and opacity are animated by the shop
/// (TownHud.ShopRows.cs); text changes only with its signature.
/// </summary>
public sealed partial class ShopRow
{
    public enum PillState { Affordable, NotYet, Blocked, On }

    public readonly Button Button;
    public readonly Panel Band;
    public readonly Label Key, Name, Step, Line2, Price;
    public readonly TextureRect Icon, Coin;
    /// <summary>The icon name the row shows (last look: a row's item can change its step, and its icon with it).</summary>
    public string IconKey = "";
    public readonly Panel Pill, Frame;
    public readonly PillWash Wash;
    public int Id;
    public ShopItem Item;
    public int Sig = int.MinValue;
    public PillState State = PillState.NotYet;
    public bool Tall = true;
    public float Y = -1, Want, From, To, SlideT = -1, SinceMove = 99f, Alpha, Pulse = -1f;
    public bool Leaving, Waiting, WasAffordable;
    public float FillK;
    public string KeyShown = "\u0001", NameShown = "", Line2Shown = "\u0001", PriceShown = "";
    public int StepShown = -1;
    public bool StateSet;
    /// <summary>The look the band shows now: 0 none, 1 hover, 2 pressed.</summary>
    public int BandShown;
    private readonly float _s;
    private readonly StyleBoxFlat _pillAfford, _pillPaper, _pillOn, _frameNotYet, _frameBlocked, _bandHover, _bandPressed;
    private bool _centred;
    private float _nameX;

    public ShopRow(float s)
    {
        _s = s;
        Button = new Button { Text = "", ClipContents = true, MouseFilter = Control.MouseFilterEnum.Stop };
        UiKit.StyleRow(Button, s);
        Band = new Panel { Name = "Band", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _bandHover = BandBox(0.07f);
        _bandPressed = BandBox(0.14f);
        Key = UiKit.KeyCap(s);
        Icon = UiKit.IconRect(s, null);
        Name = UiKit.Fixed(s, UiKit.Body);
        Name.Name = "Name";
        Name.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        Step = UiKit.Fixed(s, UiKit.Caption, UiKit.UiFont, UiKit.MutedNumber);
        Step.Name = "Step";
        Line2 = UiKit.Fixed(s, UiKit.Caption, UiKit.UiFont, UiKit.MutedNumber);
        Line2.Name = "Line2";
        Pill = new Panel { Name = "Pill", MouseFilter = Control.MouseFilterEnum.Ignore, ClipContents = true };
        _pillAfford = PillBox(UiKit.Wheat, 0, UiKit.Wheat, "Pill");
        _pillPaper = PillBox(UiKit.Paper, 0, UiKit.Paper, "Pill");
        _pillOn = PillBox(UiKit.Ink, 0, UiKit.Ink, "Pill");
        _frameNotYet = PillBox(UiKit.Paper, 1, UiKit.Muted, "PillFrame");
        _frameBlocked = PillBox(UiKit.Paper, 1, new Color(UiKit.Ink, 0.3f), "PillFrame");
        _frameNotYet.DrawCenter = false;
        _frameBlocked.DrawCenter = false;
        Wash = new PillWash { Name = "Wash", Colour = UiKit.Wheat.Lerp(UiKit.Paper, 0.5f) };
        Frame = new Panel { Name = "Frame", MouseFilter = Control.MouseFilterEnum.Ignore };
        Coin = UiKit.IconRect(s, TownHud.IconOr("ui-coin", "ui-coin"), 16);
        Coin.Name = "Coin";
        Price = UiKit.Fixed(s, UiKit.Body, UiKit.TabBold, UiKit.Ink, HorizontalAlignment.Right);
        Price.Name = "Price";
        Price.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        Pill.AddChild(Wash);
        Pill.AddChild(Frame);
        Pill.AddChild(Coin);
        Pill.AddChild(Price);
        foreach (var c in new Control[] { Band, Key, Icon, Name, Step, Line2, Pill }) Button.AddChild(c);
    }

    private StyleBoxFlat PillBox(Color bg, int border, Color edge, string meta)
    {
        var b = new StyleBoxFlat { BgColor = bg, BorderColor = edge, AntiAliasing = true };
        b.SetBorderWidthAll(border);
        b.SetCornerRadiusAll(U(16));
        b.SetMeta("ui_card", meta);
        return b;
    }

    private StyleBoxFlat BandBox(float alpha)
    {
        var b = new StyleBoxFlat { BgColor = new Color(UiKit.Ink, alpha), AntiAliasing = true };
        b.SetCornerRadiusAll(U(4));
        b.SetMeta("ui_card", "RowBand");
        return b;
    }

    private int U(float n) => UiKit.U(_s, n);

    /// <summary>Places the children for the row's size: tall (two lines) or compact (one line). The name's font size is
    /// the shop's (one size for every name, decided from the whole catalogue).</summary>
    public void Layout(float width, bool tall, float pillUnits, int nameSize)
    {
        Tall = tall;
        float h = U(tall ? 56 : 40), pw = U(pillUnits), ph = U(tall ? 32 : 28);
        Button.Size = new Vector2(width, h);
        Band.Position = Vector2.Zero;
        Band.Size = Button.Size;
        float icon = U(tall ? 24 : 20);
        Icon.Size = Icon.CustomMinimumSize = new Vector2(icon, icon);
        Icon.Position = tall ? new Vector2(U(6), U(8)) : new Vector2(U(8), (h - icon) / 2f);
        Key.Visible = tall && Key.Text.Length > 0;
        _nameX = tall ? U(NameX) : U(36);
        float lineY = tall ? U(7) : 0, lineH = tall ? U(26) : h;
        Name.Position = new Vector2(_nameX, lineY);
        Name.Size = new Vector2(NameBox(width, pillUnits, _s, tall), lineH);
        Name.AddThemeFontSizeOverride(TownHud.FontSizeName, nameSize);
        Step.Size = new Vector2(U(40), lineH);
        Line2.Visible = tall;
        Line2.Size = new Vector2(width - _nameX - U(8), U(20));
        PlaceKey();
        Pill.Position = new Vector2(width - pw - U(6), tall ? U(4) : (h - ph) / 2f);
        Pill.Size = new Vector2(pw, ph);
        Pill.PivotOffset = Pill.Size / 2f;
        foreach (var box in new[] { _pillAfford, _pillPaper, _pillOn, _frameNotYet, _frameBlocked }) box.SetCornerRadiusAll((int)(ph / 2f));
        Wash.Position = Vector2.Zero;
        Wash.Size = Pill.Size;
        Frame.Position = Vector2.Zero;
        Frame.Size = Pill.Size;
        Coin.Position = new Vector2(U(9), (ph - U(16)) / 2f);
        PlacePrice();
        PlaceStep();
        Sig = int.MinValue; NameShown = ""; PriceShown = ""; Line2Shown = "\u0001"; StateSet = false; BandShown = -1;
    }

    /// <summary>Where a tall row's name starts (sheet units: the icon is 6..30) and its least gap to the pill.</summary>
    public const float NameX = 38, NameGap = 6;

    /// <summary>The name's box (pixels): from the name's start to the pill, less a gap.</summary>
    public static float NameBox(float rowWidth, float pillUnits, float s, bool tall) =>
        rowWidth - UiKit.U(s, tall ? NameX : 36) - UiKit.U(s, pillUnits) - UiKit.U(s, 6) - UiKit.U(s, NameGap);

    /// <summary>The key cap under the icon, centred on it; line 2 starts after it when a long key name needs the room.</summary>
    public void PlaceKey()
    {
        float capW = Math.Max(U(20), TownHud.TextWidth(Key) + U(8)), cx = Icon.Position.X + Icon.Size.X / 2f;
        float x = Math.Max(U(2), cx - capW / 2f);
        Key.Position = new Vector2(x, U(32));
        Key.Size = new Vector2(capW, U(20));
        float l2 = Key.Visible ? Math.Max(_nameX, x + capW + U(6)) : _nameX;
        Line2.Position = new Vector2(l2, U(32));
        Line2.Size = new Vector2(Button.Size.X - l2 - U(8), U(20));
    }

    /// <summary>The step number just after the name, dropped whole if it does not fit. The name is never resized.</summary>
    public void PlaceStep()
    {
        float nameW = TownHud.TextWidth(Name);
        Step.Position = new Vector2(Name.Position.X + Math.Min(nameW, Name.Size.X) + U(6), Name.Position.Y);
        Step.Visible = Step.Text.Length > 0 && nameW + U(6) + TownHud.TextWidth(Step) <= Name.Size.X;
    }

    public void SetState(PillState st)
    {
        State = st;
        Pill.AddThemeStyleboxOverride("panel", st switch { PillState.Affordable => _pillAfford, PillState.On => _pillOn, _ => _pillPaper });
        Frame.AddThemeStyleboxOverride("panel", st == PillState.Blocked ? _frameBlocked : _frameNotYet);
        Frame.Visible = st is PillState.NotYet or PillState.Blocked;
        Wash.Visible = st == PillState.NotYet;
        var ink = st switch { PillState.Affordable => UiKit.Ink, PillState.On => UiKit.Paper, PillState.Blocked => UiKit.DisabledInk, _ => UiKit.MutedNumber };
        Price.AddThemeColorOverride("font_color", ink);
        Price.AddThemeFontOverride("font", st is PillState.Affordable or PillState.On ? UiKit.TabBold : UiKit.TabRegular);
        Coin.Modulate = new Color(1, 1, 1, st == PillState.Blocked ? 0.4f : 1f);
        // Sheet v3: a blocked row shows its icon and name in the disabled look as well.
        bool off = st == PillState.Blocked;
        Icon.Modulate = new Color(1, 1, 1, off ? 0.4f : 1f);
        Name.AddThemeColorOverride("font_color", off ? UiKit.DisabledInk : UiKit.Ink);
        Step.AddThemeColorOverride("font_color", off ? UiKit.DisabledInk : UiKit.MutedNumber);
    }

    /// <summary>The price's text: with the coin sign and right-aligned, or centred without it ("On", "from 12", "–").</summary>
    public void SetPrice(string text, bool centred)
    {
        Price.Text = text;
        _centred = centred;
        PlacePrice();
    }

    private void PlacePrice()
    {
        float pw = Pill.Size.X, ph = Pill.Size.Y;
        Coin.Visible = !_centred;
        // The coin sign ends at 25; the digits keep at least 4 from it and 11 from the pill's right end.
        Price.Position = _centred ? new Vector2(U(6), 0) : new Vector2(U(29), 0);
        Price.Size = new Vector2(pw - Price.Position.X - (_centred ? U(6) : U(11)), ph);
        Price.HorizontalAlignment = _centred ? HorizontalAlignment.Center : HorizontalAlignment.Right;
    }

    /// <summary>The "not yet" wash: how far the coin has come toward the price (0..1), drawn inside the outline.</summary>
    public void SetFill(float k)
    {
        k = Math.Clamp(k, 0, 1);
        if (Math.Abs(k - FillK) < 0.002f && Wash.K == k) return;
        FillK = k;
        Wash.SetK(k);
    }

    /// <summary>The band: hover (ink 7), pressed (ink 14, content 1 down), none when blocked or not under the pointer.</summary>
    public void SetBand(bool over, bool pressed)
    {
        int want = State == PillState.Blocked || !over ? 0 : pressed ? 2 : 1;
        if (want == BandShown) return;
        int was = BandShown;
        BandShown = want;
        Band.Visible = want > 0;
        if (want > 0) Band.AddThemeStyleboxOverride("panel", want == 2 ? _bandPressed : _bandHover);
        float dy = (want == 2 ? U(1) : 0) - (was == 2 ? U(1) : 0);
        if (dy == 0) return;
        foreach (var c in new Control[] { Key, Icon, Name, Step, Line2, Pill }) c.Position += new Vector2(0, dy);
    }
}

/// <summary>Sheet v3: the "not yet" pill's wash, the part of the pill's capsule left of k of its width, as one polygon
/// (so nothing is drawn outside the outline). Its points are made only when k changes, into a buffer kept per size.</summary>
public partial class PillWash : Control
{
    public Color Colour = Colors.Wheat;
    public float K { get; private set; } = -1;
    private const int Arc = 10;
    private readonly Vector2[] _buf = new Vector2[4 * Arc + 8];
    private Vector2[] _poly = Array.Empty<Vector2>();
    private readonly System.Collections.Generic.Dictionary<int, Vector2[]> _bySize = new();

    public PillWash() { MouseFilter = MouseFilterEnum.Ignore; Resized += () => { K = -1; QueueRedraw(); }; }

    public void SetK(float k) { if (k == K) return; K = k; QueueRedraw(); }

    /// <summary>The wash's points (tests: every one lies inside the capsule).</summary>
    public ReadOnlySpan<Vector2> Points => _poly;

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y, r = h / 2f, c = Math.Clamp(K, 0, 1) * w;
        if (c < 0.5f || w <= 0 || h <= 0) { _poly = Array.Empty<Vector2>(); return; }
        int n = 0;
        // Left end: the arc of x <= c (all of it once c passes r).
        float a0 = c >= r ? MathF.PI / 2f : MathF.Acos(Math.Clamp((c - r) / r, -1, 1));
        for (int i = 0; i <= Arc; i++)
        {
            float a = a0 + (2 * MathF.PI - 2 * a0) * i / Arc;
            _buf[n++] = new Vector2(r + r * MathF.Cos(a), r - r * MathF.Sin(a));
        }
        if (c > r)
        {
            float right = w - r;
            if (c <= right) { _buf[n++] = new Vector2(c, h); _buf[n++] = new Vector2(c, 0); }
            else
            {
                // Right end: from the bottom up to x = c, the straight cut, and back over the top.
                float b = MathF.Acos(Math.Clamp((c - right) / r, -1, 1));
                for (int i = 0; i <= Arc; i++)
                {
                    float a = -MathF.PI / 2f + (MathF.PI / 2f - b) * i / Arc;
                    _buf[n++] = new Vector2(right + r * MathF.Cos(a), r - r * MathF.Sin(a));
                }
                for (int i = b < 0.001f ? 1 : 0; i <= Arc; i++)
                {
                    float a = b + (MathF.PI / 2f - b) * i / Arc;
                    _buf[n++] = new Vector2(right + r * MathF.Cos(a), r - r * MathF.Sin(a));
                }
            }
        }
        if (!_bySize.TryGetValue(n, out var poly)) _bySize[n] = poly = new Vector2[n];
        Array.Copy(_buf, poly, n);
        _poly = poly;
        DrawColoredPolygon(poly, Colour);
    }
}

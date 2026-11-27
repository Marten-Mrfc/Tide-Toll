using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 3 (layout sheet v2, section 3): one shop row. The whole row is one Button with no text (its children ignore
/// the mouse), so hover, pressed and disabled come from one set of styles. Two lines when tall (56 units: key cap,
/// icon, name, step, the price pill; line 2 the worth and the state), one when compact (40: key, name, pill). The pill
/// carries the state: affordable (wheat), not yet (paper with a wheat line growing from the left), blocked (dashed),
/// On (ink). Positions and opacity are animated by the shop (TownHud.ShopRows.cs); text changes only with its
/// signature.
/// </summary>
public sealed partial class ShopRow
{
    public enum PillState { Affordable, NotYet, Blocked, On }

    public readonly Button Button;
    public readonly Label Key, Name, Step, Line2, Price;
    public readonly TextureRect Icon, Coin;
    /// <summary>The icon name the row shows (last look: a row's item can change its step, and its icon with it).</summary>
    public string IconKey = "";
    public readonly Panel Pill;
    public readonly ColorRect PillFill;
    public readonly DashFrame Dash;
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
    private readonly float _s;
    private readonly StyleBoxFlat _pillAfford, _pillPaper, _pillOn;

    public ShopRow(float s)
    {
        _s = s;
        Button = new Button { Text = "", ClipContents = true, MouseFilter = Control.MouseFilterEnum.Stop };
        UiKit.StyleRow(Button, s);
        Key = UiKit.KeyCap(s);
        Icon = UiKit.IconRect(s, null);
        Name = UiKit.Fixed(s, UiKit.Body);
        Name.Name = "Name";
        Step = UiKit.Fixed(s, UiKit.Caption, UiKit.UiFont, UiKit.MutedNumber);
        Step.Name = "Step";
        Line2 = UiKit.Fixed(s, UiKit.Caption, UiKit.UiFont, UiKit.MutedNumber);
        Line2.Name = "Line2";
        Pill = new Panel { Name = "Pill", MouseFilter = Control.MouseFilterEnum.Ignore, ClipContents = true };
        _pillAfford = PillBox(UiKit.Wheat, 0, UiKit.Wheat);
        _pillPaper = PillBox(UiKit.Paper, 1, UiKit.Muted);
        _pillOn = PillBox(UiKit.Ink, 2, UiKit.Ink);
        PillFill = new ColorRect { Name = "Fill", Color = UiKit.Wheat, MouseFilter = Control.MouseFilterEnum.Ignore };
        Dash = new DashFrame { Name = "Dash", Colour = UiKit.Muted, Visible = false };
        Coin = UiKit.IconRect(s, TownHud.IconOr("ui-coin", "ui-coin"), 16);
        Coin.Name = "Coin";
        Price = UiKit.Fixed(s, UiKit.Body, UiKit.UiBold, UiKit.Ink, HorizontalAlignment.Left);
        Price.Name = "Price";
        Pill.AddChild(PillFill);
        Pill.AddChild(Coin);
        Pill.AddChild(Price);
        Pill.AddChild(Dash);
        foreach (var c in new Control[] { Key, Icon, Name, Step, Line2, Pill }) Button.AddChild(c);
    }

    private StyleBoxFlat PillBox(Color bg, int border, Color edge)
    {
        var b = new StyleBoxFlat { BgColor = bg, BorderColor = edge, AntiAliasing = true };
        b.SetBorderWidthAll(border);
        if (border == 0) b.BorderColor = bg;
        b.SetCornerRadiusAll(UiKit.U(_s, 16));
        b.SetMeta("ui_card", "Pill");
        return b;
    }

    private int U(float n) => UiKit.U(_s, n);

    /// <summary>Places the children for the row's size: tall (two lines) or compact (one line).</summary>
    public void Layout(float width, bool tall, float pillUnits)
    {
        Tall = tall;
        float h = U(tall ? 56 : 40), pw = U(pillUnits), ph = U(32);
        Button.Size = new Vector2(width, h);
        Key.Position = new Vector2(U(6), tall ? U(6) : (h - U(20)) / 2f);
        Key.Size = new Vector2(U(20), U(20));
        Icon.Visible = tall;
        Icon.Position = new Vector2(U(30), U(4));
        float nameX = tall ? U(60) : U(32);
        Name.Position = new Vector2(nameX, tall ? U(2) : 0);
        Name.Size = new Vector2(width - nameX - pw - U(16), tall ? U(26) : h);
        Name.AddThemeFontSizeOverride("font_size", UiKit.Type(_s, tall ? UiKit.Body : UiKit.Caption));
        Name.AddThemeFontOverride("font", tall ? UiKit.UiFont : UiKit.UiBold);
        Step.Size = new Vector2(U(40), tall ? U(26) : h);
        Line2.Visible = tall;
        // Line 2 runs the full width under the name; the pill sits on line 1 (tall) or in the middle (compact).
        Line2.Position = new Vector2(nameX, U(33));
        Line2.Size = new Vector2(width - nameX - U(8), U(20));
        Pill.Position = new Vector2(width - pw - U(6), tall ? U(3) : (h - ph) / 2f);
        Pill.Size = new Vector2(pw, ph);
        Pill.PivotOffset = Pill.Size / 2f;
        Dash.Position = Vector2.Zero;
        Dash.Size = Pill.Size;
        Coin.Position = new Vector2(U(7), (ph - U(16)) / 2f);
        Price.Position = new Vector2(U(25), 0);
        Price.Size = new Vector2(pw - U(29), ph);
        PlaceStep();
        Sig = int.MinValue; NameShown = ""; PriceShown = ""; Line2Shown = "\u0001"; StateSet = false;
    }

    /// <summary>The step number just after the name, dropped whole if it does not fit.</summary>
    public void PlaceStep()
    {
        // L1 left-over (sheet 4): a name that does not fit is first set in caption size, and only then cut.
        Name.AddThemeFontSizeOverride(TownHud.FontSizeName, UiKit.Type(_s, UiKit.Body));
        float nameW = TownHud.TextWidth(Name);
        if (nameW > Name.Size.X)
        {
            Name.AddThemeFontSizeOverride(TownHud.FontSizeName, UiKit.Type(_s, UiKit.Caption));
            nameW = TownHud.TextWidth(Name);
        }
        Step.Position = new Vector2(Name.Position.X + Math.Min(nameW, Name.Size.X) + U(6), Name.Position.Y);
        Step.Visible = Step.Text.Length > 0 && nameW + U(6) + TownHud.TextWidth(Step) <= Name.Size.X;
    }

    public void SetState(PillState st)
    {
        State = st;
        Pill.AddThemeStyleboxOverride("panel", st switch { PillState.Affordable => _pillAfford, PillState.On => _pillOn, _ => _pillPaper });
        Dash.Visible = st == PillState.Blocked;
        PillFill.Visible = st == PillState.NotYet;
        // L1 fix (sheet 4): muted text that carries a number is ink at 70 percent (prices, line 2), not 60.
        var ink = st switch { PillState.Affordable => UiKit.Ink, PillState.On => UiKit.Paper, _ => UiKit.MutedNumber };
        Price.AddThemeColorOverride("font_color", ink);
        Price.AddThemeFontOverride("font", st is PillState.Affordable or PillState.On ? UiKit.UiBold : UiKit.UiFont);
        Coin.Visible = st != PillState.On;
        Price.Position = new Vector2(st == PillState.On ? U(8) : U(25), 0);
        Price.Size = new Vector2(Pill.Size.X - Price.Position.X - U(4), Pill.Size.Y);
        Price.HorizontalAlignment = st == PillState.On ? HorizontalAlignment.Center : HorizontalAlignment.Left;
    }

    /// <summary>Pave's pill reads "from 1,234": the word takes the coin's place (it is a price all the same).</summary>
    public void FromPrice(bool from)
    {
        if (!from || State == PillState.On) return;
        Coin.Visible = false;
        Price.Position = new Vector2(U(8), 0);
        Price.Size = new Vector2(Pill.Size.X - Price.Position.X - U(4), Pill.Size.Y);
    }

    /// <summary>Body size when it fits the pill, else caption (both on the sheet), so a price is never cut.</summary>
    public void FitPrice()
    {
        Price.AddThemeFontSizeOverride(TownHud.FontSizeName, UiKit.Type(_s, UiKit.Body));
        if (TownHud.TextWidth(Price) > Price.Size.X) Price.AddThemeFontSizeOverride(TownHud.FontSizeName, UiKit.Type(_s, UiKit.Caption));
    }

    /// <summary>The "not yet" line under the pill: 3 units, growing from the left as coin comes in (no strings).</summary>
    public void SetFill(float k)
    {
        FillK = k;
        if (!PillFill.Visible) return;
        float h = U(3);
        PillFill.Position = new Vector2(0, Pill.Size.Y - h);
        PillFill.Size = new Vector2(Pill.Size.X * Math.Clamp(k, 0, 1), h);
    }
}

/// <summary>A dashed frame (the blocked pill's border): drawn once per size, no style box can dash.</summary>
public partial class DashFrame : Control
{
    public Color Colour = Colors.Black;
    public DashFrame() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.One, Size - Vector2.One * 2);
        float dash = Math.Max(3f, Size.Y / 6f);
        DrawDashedLine(r.Position, new Vector2(r.End.X, r.Position.Y), Colour, 1.5f, dash);
        DrawDashedLine(new Vector2(r.Position.X, r.End.Y), r.End, Colour, 1.5f, dash);
        DrawDashedLine(r.Position, new Vector2(r.Position.X, r.End.Y), Colour, 1.5f, dash);
        DrawDashedLine(new Vector2(r.End.X, r.Position.Y), r.End, Colour, 1.5f, dash);
    }
}

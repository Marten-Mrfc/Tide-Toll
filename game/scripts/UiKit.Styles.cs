using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 3, layout sheet version 2, section 4: the one set of styles of the play interface. One scale for boxes and
/// type (<see cref="U"/> and <see cref="Type"/> use the same <c>s</c>, no floor), four type sizes, named colours, three
/// card variants, five button states, the bar, the key cap. Every size the town's interface uses is a sheet unit given
/// to <see cref="U"/>; every colour is one of these names.
/// </summary>
public static partial class UiKit
{
    // Type: four sizes in play (sheet units), display outside play.
    public const float Caption = 15f, Body = 17f, Title = 21f, Large = 30f, DisplaySize = 40f, CoinNumber = 20f;
    /// <summary>The four play sizes, for the layout test.</summary>
    public static readonly float[] PlaySizes = { Caption, Body, Title, Large };

    // Colours.
    public static readonly Color Wheat = Palette.Thatch;
    public static readonly Color RiverBlue = Palette.River;
    public static readonly Color Moss = Palette.CastIronGreen;
    public static readonly Color BrickRed = Palette.Brick;
    /// <summary>Muted text: ink at 60 percent; muted text that carries a number: ink at 70 percent (over paper).</summary>
    public static readonly Color Muted = Ink.Lerp(Paper, 0.4f);
    public static readonly Color MutedNumber = Ink.Lerp(Paper, 0.3f);
    public static readonly Color PaperLight = Paper.Lerp(Colors.White, 0.45f);
    public static readonly Color PaperDarker = Paper.Lerp(Ink, 0.12f);
    public static readonly Color DisabledInk = Ink.Lerp(Paper, 0.55f);
    public static readonly Color CoinGold = Color.FromHtml("#F2C14E");
    public static readonly Color CoinEdge = Color.FromHtml("#3A2A10");
    /// <summary>The ledger's faint ruled lines: ink at about 8 percent.</summary>
    public static readonly Color Ruling = new(Ink, 0.08f);

    /// <summary>A length in sheet units as canvas pixels (whole pixels).</summary>
    public static int U(float s, float units) => Mathf.RoundToInt(units * s);
    /// <summary>A type size in sheet units as a font size in pixels: the same scale as boxes, no floor.</summary>
    public static int Type(float s, float size) => Mathf.Max(1, Mathf.RoundToInt(size * s));

    public enum CardKind { Card, Quiet, Accent, Trouble }

    /// <summary>Card: paper 96 percent, ink border 2, corner 4, padding 12. Quiet: no border, 88 percent. Accent: wheat
    /// fill (trouble: brick border).</summary>
    public static StyleBoxFlat CardBox(float s, CardKind kind = CardKind.Card, float pad = 12)
    {
        var b = new StyleBoxFlat { AntiAliasing = true, BorderColor = kind == CardKind.Trouble ? BrickRed : Ink };
        b.BgColor = kind switch
        {
            CardKind.Quiet => new Color(Paper, 0.88f),
            CardKind.Accent or CardKind.Trouble => Wheat.Lerp(Paper, 0.35f),
            _ => new Color(Paper, 0.96f),
        };
        b.SetBorderWidthAll(kind == CardKind.Quiet ? 0 : Mathf.Max(1, U(s, 2)));
        b.SetCornerRadiusAll(U(s, 4));
        b.SetContentMarginAll(U(s, pad));
        b.SetMeta("ui_card", kind.ToString());
        return b;
    }

    public enum ButtonLook { Normal, Hover, Pressed, Disabled, On }

    public static StyleBoxFlat ButtonBox(float s, ButtonLook look, float pad = 8)
    {
        var b = new StyleBoxFlat { AntiAliasing = true, BorderColor = Ink };
        b.SetCornerRadiusAll(U(s, 4));
        b.SetContentMarginAll(U(s, pad));
        int w = Mathf.Max(1, U(s, 2));
        switch (look)
        {
            case ButtonLook.Normal: b.BgColor = Paper; break;
            case ButtonLook.Hover: b.BgColor = PaperLight; w = Mathf.Max(2, U(s, 3)); break;
            case ButtonLook.Pressed: b.BgColor = PaperDarker; b.ContentMarginTop += Mathf.Max(1, U(s, 1)); b.ContentMarginBottom -= Mathf.Max(1, U(s, 1)); break;
            case ButtonLook.Disabled: b.BgColor = Paper; b.BorderColor = DisabledInk; w = 1; break;
            case ButtonLook.On: b.BgColor = Ink; break;
        }
        b.SetBorderWidthAll(w);
        b.SetMeta("ui_card", "Button" + look);
        return b;
    }

    /// <summary>A shop row: air at rest (no frame, the shop is the card); hover lighter with border 3; pressed darker.</summary>
    public static void StyleRow(Button b, float s)
    {
        StyleButton(b, s, 0);
        var rest = new StyleBoxFlat { BgColor = new Color(Paper, 0), DrawCenter = false };
        rest.SetMeta("ui_card", "Row");
        b.AddThemeStyleboxOverride("normal", rest);
        var off = (StyleBoxFlat)rest.Duplicate();
        b.AddThemeStyleboxOverride("disabled", off);
    }

    /// <summary>The five button states on a button (no keyboard focus in play).</summary>
    public static void StyleButton(Button b, float s, float pad = 8, bool on = false)
    {
        b.FocusMode = Control.FocusModeEnum.None;
        b.AddThemeStyleboxOverride("normal", ButtonBox(s, on ? ButtonLook.On : ButtonLook.Normal, pad));
        b.AddThemeStyleboxOverride("hover", ButtonBox(s, on ? ButtonLook.On : ButtonLook.Hover, pad));
        b.AddThemeStyleboxOverride("pressed", ButtonBox(s, on ? ButtonLook.On : ButtonLook.Pressed, pad));
        b.AddThemeStyleboxOverride("hover_pressed", ButtonBox(s, on ? ButtonLook.On : ButtonLook.Pressed, pad));
        b.AddThemeStyleboxOverride("disabled", ButtonBox(s, ButtonLook.Disabled, pad));
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        b.AddThemeColorOverride("font_disabled_color", DisabledInk);
        foreach (var c in FontStates) b.AddThemeColorOverride(c, on ? Paper : Ink);
        b.AddThemeFontSizeOverride("font_size", Type(s, Body));
    }

    private static readonly string[] FontStates = { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" };

    /// <summary>A label with a fixed box: it never grows its parent, never wraps, clips with an ellipsis.</summary>
    public static Label Fixed(float s, float size, Font? font = null, Color? colour = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var l = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore, ClipText = true, AutowrapMode = TextServer.AutowrapMode.Off,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, HorizontalAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
        };
        l.AddThemeFontSizeOverride("font_size", Type(s, size));
        l.AddThemeFontOverride("font", font ?? UiFont);
        l.AddThemeColorOverride("font_color", colour ?? Ink);
        return l;
    }

    /// <summary>A bar: 8 high, corner 4, ink border 1; the fill is a child ColorRect set by <see cref="SetBar"/>.</summary>
    public static Panel Bar(float s, Color fill, out ColorRect fillRect)
    {
        var p = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, ClipContents = true };
        var b = new StyleBoxFlat { BgColor = PaperDarker, BorderColor = Ink, AntiAliasing = true };
        b.SetBorderWidthAll(1);
        b.SetCornerRadiusAll(U(s, 4));
        b.SetMeta("ui_card", "Bar");
        p.AddThemeStyleboxOverride("panel", b);
        fillRect = new ColorRect { Color = fill, MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddChild(fillRect);
        return p;
    }

    public static void SetBar(Control back, ColorRect fill, float k)
    {
        var sz = back.Size;
        fill.Position = new Vector2(1, 1);
        fill.Size = new Vector2(Mathf.Max(0, (sz.X - 2) * Mathf.Clamp(k, 0, 1)), Mathf.Max(0, sz.Y - 2));
    }

    private static FontVariation? _capFont;
    /// <summary>The key caps' face: the sans at caption size with its line's air trimmed, so a cap fits under an icon in a
    /// 48-unit button at every scale (the same family and size, only the leading is less).</summary>
    public static FontVariation CapFont => _capFont ??= new FontVariation { BaseFont = UiFont, SpacingTop = -2, SpacingBottom = -3 };
    private static FontVariation? _tightBold;
    /// <summary>The bold sans with the same trimmed leading (a count above a key cap in a 48-unit button).</summary>
    public static FontVariation TightBold => _tightBold ??= new FontVariation { BaseFont = UiBold, SpacingTop = -3, SpacingBottom = -4 };

    /// <summary>A key cap: caption in a small box, muted.</summary>
    public static Label KeyCap(float s)
    {
        var l = Fixed(s, Caption, CapFont, Muted, HorizontalAlignment.Center);
        var b = new StyleBoxFlat { BgColor = new Color(Paper, 0), BorderColor = Muted, AntiAliasing = true };
        b.SetBorderWidthAll(1);
        b.SetCornerRadiusAll(U(s, 3));
        b.ContentMarginLeft = b.ContentMarginRight = 1;
        b.ContentMarginTop = b.ContentMarginBottom = 0;
        l.AddThemeStyleboxOverride("normal", b);
        l.AddThemeConstantOverride("line_spacing", 0);
        l.Name = "Key";
        return l;
    }

    /// <summary>A 24-unit icon.</summary>
    public static TextureRect IconRect(float s, Texture2D? tex, float units = 24) => new()
    {
        Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        Size = Vector2.One * U(s, units), CustomMinimumSize = Vector2.One * U(s, units),
        TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, MouseFilter = Control.MouseFilterEnum.Ignore, Name = "Icon",
    };

    /// <summary>A 1 px muted rule.</summary>
    public static ColorRect MutedRule() => new() { Color = new Color(Ink, 0.3f), MouseFilter = Control.MouseFilterEnum.Ignore, Name = "Rule" };

    /// <summary>Coin amounts: "1,234" up to a million, then "1.24M" (pills).</summary>
    public static string Short(double c)
    {
        c = System.Math.Floor(c);
        if (c < 1_000_000) return c.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        return (c / 1_000_000).ToString(c < 10_000_000 ? "0.00" : c < 100_000_000 ? "0.0" : "0", System.Globalization.CultureInfo.InvariantCulture) + "M";
    }
}

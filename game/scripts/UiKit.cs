using Godot;

namespace TideToll.Game;

/// <summary>
/// The interface's look, in the book's own language (UX tab, "Panels"): paper cards #F2EAD8 with ink #2B2620 text,
/// thin ink rules, no drop shadows; Atkinson Hyperlegible for interface text and numbers, Alegreya for headings,
/// Uncial Antiqua for the chapter title only (24 px and up). Everything is sized from one scale factor taken from
/// the window height (1.0 at 1080p, never below 0.78 so body text stays at least 12 px at 720p), and the interface is
/// rebuilt when the window size changes, so text is rasterised at its real size and stays sharp.
/// Icons are the SVG files rasterised at the size they are shown.
/// </summary>
public static partial class UiKit
{
    /// <summary>Pixels of a bookmark tab tucked under the page edge (before scaling).</summary>
    public const float Tuck = 9f;
    public static readonly Color Paper = Palette.Paper;
    public static readonly Color Ink = Palette.Ink;
    public static readonly Color PaperDark = Color.FromHtml("#E4D6B8");
    public static readonly Color PaperHover = Color.FromHtml("#F8F2E4");
    public static readonly Color Pressed = Color.FromHtml("#E9D49A");   // thatch-pale: a bookmark pulled out
    public static readonly Color Focus = Color.FromHtml("#56B4E9");     // player-preview sky blue
    public static readonly Color FadedInk = Ink.Lerp(Paper, 0.38f);
    public static readonly Color Good = Color.FromHtml("#009E73");
    public static readonly Color Warn = Color.FromHtml("#E69F00");

    private static FontFile? _ui, _uiBold, _serif, _display;
    public static FontFile UiFont => _ui ??= GD.Load<FontFile>("res://assets/fonts/TideLegible-Regular.ttf");
    public static FontFile UiBold => _uiBold ??= GD.Load<FontFile>("res://assets/fonts/TideLegible-Bold.ttf");
    public static FontFile Serif => _serif ??= GD.Load<FontFile>("res://assets/fonts/Alegreya-Variable.ttf");
    public static FontFile Display => _display ??= GD.Load<FontFile>("res://assets/fonts/UncialAntiqua-Regular.ttf");

    private static readonly System.Collections.Generic.Dictionary<FontFile, FontFile> Msdf = new();

    /// <summary>
    /// A copy of a font rendered as a multichannel signed distance field: text printed on the book (map title, folios,
    /// paper tags) stays crisp at every zoom from one small glyph atlas. Cached per font.
    /// </summary>
    public static FontFile PrintFont(FontFile f)
    {
        if (Msdf.TryGetValue(f, out var m)) return m;
        m = (FontFile)f.Duplicate();
        m.MultichannelSignedDistanceField = true;
        m.MsdfPixelRange = 16;
        m.MsdfSize = 128;
        Msdf[f] = m;
        return m;
    }

    /// <summary>Interface scale for a viewport height.</summary>
    public static float ScaleFor(float height) => Mathf.Clamp(height / 1080f, 0.78f, 2.5f);

    public static int Px(float s, float px) => Mathf.RoundToInt(px * s);
    /// <summary>A font size: scaled, but never under 12 px (UX tab: body text 12 px minimum at 1280 x 800).</summary>
    public static int FontPx(float s, float px) => Mathf.Max(12, Mathf.RoundToInt(px * s));

    public static Theme MakeTheme(float s)
    {
        var t = new Theme { DefaultFont = UiFont, DefaultFontSize = FontPx(s, 16) };
        // Buttons are bookmarks: paper with an ink edge, tucked under the page on the left (no left border).
        t.SetStylebox("normal", "Button", Tab(s, Paper, 1.5f, 0));
        t.SetStylebox("hover", "Button", Tab(s, PaperHover, 1.5f, 0));
        t.SetStylebox("pressed", "Button", Tab(s, Pressed, 2.5f, 7));
        t.SetStylebox("hover_pressed", "Button", Tab(s, Pressed, 2.5f, 7));
        t.SetStylebox("disabled", "Button", Tab(s, PaperDark, 1f, 0));
        t.SetStylebox("focus", "Button", FocusBox(s));
        foreach (var c in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            t.SetColor(c, "Button", Ink);
        t.SetColor("font_disabled_color", "Button", FadedInk);
        t.SetColor("font_color", "Label", Ink);
        // Tooltips: a slip of paper with an ink edge.
        var tip = Card(s, Paper, 1.5f);
        tip.SetContentMarginAll(Px(s, 8));
        t.SetStylebox("panel", "TooltipPanel", tip);
        t.SetColor("font_color", "TooltipLabel", Ink);
        t.SetFont("font", "TooltipLabel", UiFont);
        t.SetFontSize("font_size", "TooltipLabel", FontPx(s, 15));
        // Sliders: an ink rule with a paper bead.
        var line = new StyleBoxFlat { BgColor = FadedInk, ContentMarginTop = Px(s, 2), ContentMarginBottom = Px(s, 2) };
        line.SetCornerRadiusAll(Px(s, 2));
        var filled = new StyleBoxFlat { BgColor = Ink, ContentMarginTop = Px(s, 2), ContentMarginBottom = Px(s, 2) };
        filled.SetCornerRadiusAll(Px(s, 2));
        t.SetStylebox("slider", "HSlider", line);
        t.SetStylebox("grabber_area", "HSlider", filled);
        t.SetStylebox("grabber_area_highlight", "HSlider", filled);
        t.SetStylebox("focus", "HSlider", FocusBox(s));
        t.SetIcon("grabber", "HSlider", Bead(Px(s, 18), false));
        t.SetIcon("grabber_highlight", "HSlider", Bead(Px(s, 18), true));
        t.SetIcon("grabber_disabled", "HSlider", Bead(Px(s, 18), false));
        t.SetConstant("center_grabber", "HSlider", 1);
        t.SetStylebox("panel", "PanelContainer", new StyleBoxEmpty());
        return t;
    }

    /// <summary>
    /// A bookmark tab: square on the left, where it is tucked under the page edge (Tuck pixels of it lie
    /// under the page, which the PageLip draws over it), rounded on the right.
    /// </summary>
    public static StyleBoxFlat Tab(float s, Color bg, float border, float pullOut)
    {
        var b = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = Ink,
            ContentMarginLeft = Px(s, 10 + Tuck), ContentMarginRight = Px(s, 10),
            ContentMarginTop = Px(s, 5), ContentMarginBottom = Px(s, 5),
            // Pressed tabs are pulled further out of the book, to the right: shape, not only colour, says "on".
            ExpandMarginRight = Px(s, pullOut),
            AntiAliasing = true,
        };
        b.BorderWidthTop = b.BorderWidthBottom = Mathf.Max(1, Px(s, border));
        b.BorderWidthRight = Mathf.Max(1, Px(s, pullOut > 0 ? border + 1.5f : border));
        b.BorderWidthLeft = 0;
        b.CornerRadiusTopRight = b.CornerRadiusBottomRight = Px(s, 7);
        return b;
    }

    public static StyleBoxFlat Card(float s, Color bg, float border)
    {
        var b = new StyleBoxFlat { BgColor = bg, BorderColor = Ink, AntiAliasing = true };
        b.SetBorderWidthAll(Mathf.Max(1, Px(s, border)));
        b.SetCornerRadiusAll(Px(s, 3));
        b.SetContentMarginAll(Px(s, 12));
        return b;
    }

    /// <summary>Keyboard focus: a thick sky-blue frame drawn outside the control (always visible, never colour alone: it is a frame).</summary>
    public static StyleBoxFlat FocusBox(float s)
    {
        var b = new StyleBoxFlat { DrawCenter = false, BorderColor = Focus, AntiAliasing = true };
        b.SetBorderWidthAll(Mathf.Max(2, Px(s, 3)));
        b.SetCornerRadiusAll(Px(s, 5));
        b.SetExpandMarginAll(Px(s, 3));
        return b;
    }

    private static ImageTexture Bead(int size, bool hot)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float r = size / 2f - 0.5f, ring = Mathf.Max(1.5f, size / 8f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).Length();
                float a = Mathf.Clamp(r - d + 0.5f, 0, 1);
                var c = d > r - ring ? Ink : hot ? Pressed : Paper;
                img.SetPixel(x, y, new Color(c, a));
            }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>
    /// An SVG icon from assets/icons: imported at 4x (project.godot, importer_defaults), mipmapped here (cached), so a
    /// TextureRect with linear-mipmap filtering draws it crisply at the interface's size.
    /// </summary>
    public static Texture2D Icon(string name, int px) => ModelLib.MipTexture($"res://assets/icons/{name}.svg");

    public static Label Text(string text, float s, float px, FontFile? font = null, Color? colour = null)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", FontPx(s, px));
        if (font != null) l.AddThemeFontOverride("font", font);
        l.AddThemeColorOverride("font_color", colour ?? Ink);
        return l;
    }

    /// <summary>A thin ink rule across a column.</summary>
    public static ColorRect Rule(float s, float alpha = 1f) => new()
    {
        Color = new Color(Ink, alpha),
        CustomMinimumSize = new Vector2(0, Mathf.Max(1, Px(s, 1.5f))),
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };
}

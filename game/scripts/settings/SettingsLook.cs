using Godot;

namespace TideToll.Game.Settings;

/// <summary>
/// The settings pages' theme, in the interface kit's language (UiKit colours, fonts, cards and focus frame): buttons
/// are paper slips with an ink edge, sliders an ink rule with a paper bead, ribbons are bookmarks tucked under the
/// right page and pulled out when chosen. Uses only UiKit's colours, fonts, Card, FocusBox and Px helpers.
/// </summary>
public static class SettingsLook
{
    /// <summary>How much of a ribbon lies under the page edge, in unscaled pixels.</summary>
    public const float Tuck = 12f;

    public static Theme Theme(float s)
    {
        var t = new Theme { DefaultFont = UiKit.UiFont, DefaultFontSize = UiKit.FontPx(s, 18) };
        t.SetStylebox("normal", "Button", Slip(s, UiKit.Paper, 1.5f));
        t.SetStylebox("hover", "Button", Slip(s, UiKit.PaperHover, 2f));
        t.SetStylebox("pressed", "Button", Slip(s, UiKit.Pressed, 2.5f));
        t.SetStylebox("hover_pressed", "Button", Slip(s, UiKit.Pressed, 2.5f));
        t.SetStylebox("disabled", "Button", Slip(s, UiKit.PaperDark, 1f));
        t.SetStylebox("focus", "Button", UiKit.FocusBox(s));
        foreach (var c in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            t.SetColor(c, "Button", UiKit.Ink);
        t.SetColor("font_disabled_color", "Button", UiKit.FadedInk);
        t.SetFont("font", "Button", UiKit.UiBold);
        t.SetColor("font_color", "Label", UiKit.Ink);

        var tip = UiKit.Card(s, UiKit.Paper, 1.5f);
        tip.SetContentMarginAll(UiKit.Px(s, 8));
        t.SetStylebox("panel", "TooltipPanel", tip);
        t.SetColor("font_color", "TooltipLabel", UiKit.Ink);
        t.SetFont("font", "TooltipLabel", UiKit.UiFont);
        t.SetFontSize("font_size", "TooltipLabel", UiKit.FontPx(s, 15));

        // Sliders: an ink rule with a paper bead; the part up to the bead is solid ink.
        var line = new StyleBoxFlat { BgColor = UiKit.FadedInk, ContentMarginTop = UiKit.Px(s, 2), ContentMarginBottom = UiKit.Px(s, 2) };
        line.SetCornerRadiusAll(UiKit.Px(s, 2));
        var filled = new StyleBoxFlat { BgColor = UiKit.Ink, ContentMarginTop = UiKit.Px(s, 2), ContentMarginBottom = UiKit.Px(s, 2) };
        filled.SetCornerRadiusAll(UiKit.Px(s, 2));
        t.SetStylebox("slider", "HSlider", line);
        t.SetStylebox("grabber_area", "HSlider", filled);
        t.SetStylebox("grabber_area_highlight", "HSlider", filled);
        t.SetStylebox("focus", "HSlider", UiKit.FocusBox(s));
        int bead = UiKit.Px(s, 22);
        t.SetIcon("grabber", "HSlider", Bead(bead, false));
        t.SetIcon("grabber_highlight", "HSlider", Bead(bead, true));
        t.SetIcon("grabber_disabled", "HSlider", Bead(bead, false));
        t.SetConstant("center_grabber", "HSlider", 1);

        // Scroll bars: a thin faded rule with an ink grip.
        var track = new StyleBoxFlat { BgColor = new Color(UiKit.Ink, 0.12f), ContentMarginLeft = UiKit.Px(s, 4), ContentMarginRight = UiKit.Px(s, 4) };
        track.SetCornerRadiusAll(UiKit.Px(s, 3));
        var grip = new StyleBoxFlat { BgColor = UiKit.FadedInk };
        grip.SetCornerRadiusAll(UiKit.Px(s, 3));
        var gripHot = new StyleBoxFlat { BgColor = UiKit.Ink };
        gripHot.SetCornerRadiusAll(UiKit.Px(s, 3));
        t.SetStylebox("scroll", "VScrollBar", track);
        t.SetStylebox("grabber", "VScrollBar", grip);
        t.SetStylebox("grabber_highlight", "VScrollBar", gripHot);
        t.SetStylebox("grabber_pressed", "VScrollBar", gripHot);
        t.SetStylebox("panel", "ScrollContainer", new StyleBoxEmpty());
        t.SetStylebox("focus", "ScrollContainer", new StyleBoxEmpty());
        t.SetStylebox("panel", "PanelContainer", UiKit.Card(s, UiKit.Paper, 2f));
        return t;
    }

    /// <summary>A paper slip: the page buttons.</summary>
    public static StyleBoxFlat Slip(float s, Color bg, float border)
    {
        var b = UiKit.Card(s, bg, border);
        b.ContentMarginLeft = b.ContentMarginRight = UiKit.Px(s, 14);
        b.ContentMarginTop = b.ContentMarginBottom = UiKit.Px(s, 6);
        return b;
    }

    /// <summary>Styles a section ribbon: tucked under the page on the left, rounded on the right, pulled out when chosen.</summary>
    public static void Ribbon(Button b, float s, Color tint)
    {
        b.AddThemeStyleboxOverride("normal", RibbonBox(s, tint, 1.5f, 0));
        b.AddThemeStyleboxOverride("hover", RibbonBox(s, tint.Lightened(0.35f), 2f, 0));
        b.AddThemeStyleboxOverride("pressed", RibbonBox(s, UiKit.Paper, 2.5f, 0));
        b.AddThemeStyleboxOverride("hover_pressed", RibbonBox(s, UiKit.Paper, 2.5f, 0));
        var f = UiKit.FocusBox(s);
        f.ExpandMarginLeft = -UiKit.Px(s, Tuck - 2);
        b.AddThemeStyleboxOverride("focus", f);
        b.AddThemeFontOverride("font", UiKit.Serif);
        b.AddThemeFontSizeOverride("font_size", UiKit.FontPx(s, 25));
    }

    private static StyleBoxFlat RibbonBox(float s, Color bg, float border, float pullOut)
    {
        var b = new StyleBoxFlat
        {
            BgColor = bg, BorderColor = UiKit.Ink, AntiAliasing = true,
            ContentMarginLeft = UiKit.Px(s, Tuck + 14), ContentMarginRight = UiKit.Px(s, 12),
            ContentMarginTop = UiKit.Px(s, 4), ContentMarginBottom = UiKit.Px(s, 4),
            ExpandMarginRight = UiKit.Px(s, pullOut),
        };
        b.BorderWidthTop = b.BorderWidthBottom = b.BorderWidthRight = Mathf.Max(1, UiKit.Px(s, border));
        b.BorderWidthLeft = 0;
        b.CornerRadiusTopRight = b.CornerRadiusBottomRight = UiKit.Px(s, 9);
        return b;
    }

    private static ImageTexture Bead(int size, bool hot)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float r = size / 2f - 0.5f, ring = Mathf.Max(1.5f, size / 7f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).Length();
                float a = Mathf.Clamp(r - d + 0.5f, 0, 1);
                var c = d > r - ring ? UiKit.Ink : hot ? UiKit.Pressed : UiKit.Paper;
                img.SetPixel(x, y, new Color(c, a));
            }
        return ImageTexture.CreateFromImage(img);
    }
}

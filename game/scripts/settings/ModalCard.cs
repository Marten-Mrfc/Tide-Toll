using System;
using Godot;

namespace TideToll.Game.Settings;

/// <summary>
/// The one modal card (layout sheet section 2, round L3): the pause cover's "Start a new town?" and the settings'
/// "That key is taken", "Keep this?" and "Delete the save?". 480 wide, centred on the window, card style (paper, ink
/// border 2, corner 4), a title, a text with room for three lines, the buttons in a row below the text. Built once per
/// question (never per frame).
/// </summary>
public static class ModalCard
{
    public const float Width = 480f;

    /// <summary>The card with its title, its text (three lines' room) and a row for its buttons (returned to fill).</summary>
    public static PanelContainer Build(float s, string title, string text, out Label body, out HBoxContainer row, string name = "Modal")
    {
        var card = new PanelContainer { Name = name, MouseFilter = Control.MouseFilterEnum.Stop };
        var box = UiKit.CardBox(s, UiKit.CardKind.Card, 24);
        box.BgColor = PageLeaf.BookPaper;
        card.AddThemeStyleboxOverride("panel", box);
        var col = new VBoxContainer { Name = "Column" };
        col.AddThemeConstantOverride("separation", UiKit.Px(s, 12));
        card.AddChild(col);
        var head = UiKit.Text(title, s, 26, UiKit.Serif);
        head.Name = "Title";
        col.AddChild(head);
        body = UiKit.Text(text, s, 18, UiKit.UiFont);
        body.Name = "Text";
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        float inner = Width * s - 2 * UiKit.Px(s, 24);
        // Room for three lines, whatever the text: the buttons stand at the same place for every question.
        float line = UiKit.FontPx(s, 18) * 1.45f;
        body.CustomMinimumSize = new Vector2(inner, MathF.Ceiling(line * 3f));
        col.AddChild(body);
        row = new HBoxContainer { Name = "Buttons", Alignment = BoxContainer.AlignmentMode.End };
        row.AddThemeConstantOverride("separation", UiKit.Px(s, 12));
        col.AddChild(row);
        card.CustomMinimumSize = new Vector2(Width * s, 0);
        return card;
    }

    /// <summary>A button of the card: the interface's button style, 32 high, with the keyboard focus look of the pages.</summary>
    public static Button Button(Button b, float s, bool primary)
    {
        UiKit.StyleButton(b, s, 10, on: false);
        b.FocusMode = Control.FocusModeEnum.All;
        var focus = UiKit.ButtonBox(s, UiKit.ButtonLook.Hover, 10);
        b.AddThemeStyleboxOverride("focus", focus);
        // One style for both buttons (round L3 review: they were in two fonts); the safe choice keeps the focus.
        b.AddThemeFontOverride("font", UiKit.UiBold);
        b.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0));
        b.CustomMinimumSize = new Vector2(Math.Max(b.CustomMinimumSize.X, 120 * s), 32 * s);
        return b;
    }

    /// <summary>Centres the card on a canvas of this size.</summary>
    public static void Centre(Control card, Vector2 canvas)
    {
        card.ResetSize();
        var size = card.GetCombinedMinimumSize();
        card.Size = size;
        card.Position = ((canvas - size) / 2f).Round();
    }
}

using System;
using Godot;

namespace TideToll.Game.Settings;

/// <summary>
/// A choice among a few options, in one focusable control: the value in the middle with an ink arrow on each side.
/// Left and right (keys or a click on either half) step through; Enter or Space steps forward and wraps.
/// </summary>
public partial class ChoiceButton : Button
{
    public string[] Options = Array.Empty<string>();
    public int Index { get; private set; }
    public event Action<int>? Chosen;
    private float _arrow = 8f;

    public ChoiceButton() { }

    public ChoiceButton(string[] options, int index, float s)
    {
        Options = options;
        Index = Math.Clamp(index, 0, options.Length - 1);
        Text = options[Index];
        _arrow = Mathf.Max(5f, 8f * s);
        FocusMode = FocusModeEnum.All;
        Pressed += () => Step(+1, true);
    }

    public void SetIndex(int i, bool notify)
    {
        i = Math.Clamp(i, 0, Options.Length - 1);
        if (i == Index) return;
        Index = i;
        Text = Options[i];
        QueueRedraw();
        if (notify) Chosen?.Invoke(i);
    }

    private void Step(int d, bool wrap)
    {
        if (Disabled) return;
        int i = Index + d;
        if (wrap) i = (i + Options.Length) % Options.Length;
        SetIndex(i, true);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (Disabled) return;
        if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            // A click on the left half goes back, on the right half forward.
            // Accept first: a step can rebuild the page and take this control out of the tree.
            AcceptEvent();
            GrabFocus();
            if (!mb.Pressed) Step(mb.Position.X < Size.X / 2 ? -1 : +1, false);
            return;
        }
        if (e.IsActionPressed("ui_left", true)) { AcceptEvent(); Step(-1, false); }
        else if (e.IsActionPressed("ui_right", true)) { AcceptEvent(); Step(+1, false); }
    }

    public override void _Draw()
    {
        var c = Disabled ? UiKit.FadedInk : UiKit.Ink;
        float a = _arrow, mid = Size.Y / 2, pad = a * 1.6f;
        bool canBack = Index > 0, canOn = Index < Options.Length - 1;
        DrawColoredPolygon(new[] { new Vector2(pad, mid), new Vector2(pad + a, mid - a), new Vector2(pad + a, mid + a) },
            canBack ? c : new Color(c, 0.25f));
        float r = Size.X - pad;
        DrawColoredPolygon(new[] { new Vector2(r, mid), new Vector2(r - a, mid - a), new Vector2(r - a, mid + a) },
            canOn ? c : new Color(c, 0.25f));
    }
}

/// <summary>
/// A volume slider: an HSlider whose last few pixels at either end are the end itself, so a click at the far right
/// is 100 and at the far left is 0 (a plain slider gives 99 or 1 there, which reads as a fault).
/// </summary>
public partial class VolumeSlider : HSlider
{
    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            float edge = Mathf.Max(4f, Size.X * 0.03f);
            if (mb.Position.X >= Size.X - edge) { Value = MaxValue; GrabFocus(); AcceptEvent(); }
            else if (mb.Position.X <= edge) { Value = MinValue; GrabFocus(); AcceptEvent(); }
        }
    }
}

/// <summary>An on/off switch: a button that stays pressed when on, labelled On or Off. Left turns it off, right on.</summary>
public partial class SwitchButton : Button
{
    public event Action<bool>? Switched;

    public SwitchButton() { }

    public SwitchButton(bool on)
    {
        ToggleMode = true;
        FocusMode = FocusModeEnum.All;
        SetPressedNoSignal(on);
        Text = on ? "On" : "Off";
        Toggled += v => { Text = v ? "On" : "Off"; Switched?.Invoke(v); };
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_left", true)) { AcceptEvent(); if (ButtonPressed) ButtonPressed = false; }
        else if (e.IsActionPressed("ui_right", true)) { AcceptEvent(); if (!ButtonPressed) ButtonPressed = true; }
    }
}

/// <summary>Draws the table, the cover and the two pages of the open book behind the settings.</summary>
public partial class SpreadArt : Control
{
    public Rect2 Cover, Left, Right;
    public float S = 1f;
    public bool PagesOnly;

    public override void _Draw()
    {
        if (!PagesOnly)
        {
            // The table: dark wood with plank seams.
            DrawRect(new Rect2(Vector2.Zero, Size), Palette.Mud);
            var seam = Palette.Mud.Darkened(0.25f);
            for (float y = 40 * S; y < Size.Y; y += 150 * S) DrawRect(new Rect2(0, y, Size.X, Mathf.Max(1, 2 * S)), seam);
            // The cover, a little larger than the pages, with a shadow under it.
            DrawRect(new Rect2(Cover.Position + new Vector2(6, 8) * S, Cover.Size), new Color(0, 0, 0, 0.25f));
            var cover = new StyleBoxFlat { BgColor = Palette.SootBrick, BorderColor = Palette.SootBrick.Darkened(0.35f) };
            cover.SetBorderWidthAll(Mathf.Max(1, UiKit.Px(S, 2)));
            cover.SetCornerRadiusAll(UiKit.Px(S, 10));
            DrawStyleBox(cover, Cover);
            return;
        }
        foreach (var (page, side) in new[] { (Left, -1), (Right, 1) })
        {
            // Page stack: stepped edges along the outer side.
            for (int i = 3; i >= 1; i--)
            {
                var off = new Vector2(side * i * 2.5f * S, i * 1.8f * S);
                DrawRect(new Rect2(page.Position + off, page.Size), UiKit.PaperDark.Darkened(0.06f * i));
            }
            DrawRect(page, PageLeaf.BookPaper);
            // Shade into the gutter so the spread reads as a bound book.
            float w = 70 * S;
            int steps = 14;
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps;
                float x = side < 0 ? page.End.X - (i + 1) * w / steps : page.Position.X + w * t;
                DrawRect(new Rect2(x, page.Position.Y, w / steps + 1, page.Size.Y),
                    new Color(UiKit.Ink, 0.16f * (1 - t) * (1 - t)));
            }
            DrawRect(page, new Color(UiKit.Ink, 0.5f), false, Mathf.Max(1, S));
        }
        // The spine line.
        DrawLine(new Vector2(Right.Position.X, Right.Position.Y), new Vector2(Right.Position.X, Right.End.Y),
            new Color(UiKit.Ink, 0.45f), Mathf.Max(1, 1.5f * S));
    }
}

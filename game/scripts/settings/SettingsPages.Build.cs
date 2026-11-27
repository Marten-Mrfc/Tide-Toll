using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game.Settings;

/// <summary>
/// Building the spread: the table, the ribbons, the pages, and all four sections at once, each under its own mask
/// (SectionMask); a section's own page built again in place when one of its controls asks (Refresh).
/// </summary>
public partial class SettingsPages
{
    // ---------------------------------------------------------------- building

    private void Build()
    {
        if (_frame != null) { RemoveChild(_frame); _frame.QueueFree(); }
        _shared.Clear();
        foreach (var d in _sec) d.Clear();
        _slipBlocker = _slipCard = null;
        _countdown = null;
        var canvas = GetViewportRect().Size;
        _canvas = canvas;
        _s = UiKit.ScaleFor(GetWindow().Size.Y);
        float s = _s;
        _frame = new Control { Name = "Frame", MouseFilter = MouseFilterEnum.Ignore, Theme = SettingsLook.Theme(s) };
        _frame.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_frame);

        // Round L3: a margin of at least 40 to the window, the navigation row's room at the foot (zoomed all the way out
        // the 3D book stands exactly here, with navigation under it), and the ribbons inside the window on the right.
        float m = OwnTable ? 16 * s : Mathf.Max(40f, 40 * s), foot = OwnTable ? m : Mathf.Max(m, 80 * s), ribbonOut = OwnTable ? 150 * s : 170 * s, lip = 10 * s;
        float bookW = Mathf.Min(canvas.X - 2 * m - ribbonOut, 1600 * s);
        float bookH = Mathf.Min(canvas.Y - m - foot, 900 * s);
        float x0 = Mathf.Round((canvas.X - bookW - ribbonOut) / 2), y0 = Mathf.Round(m + (canvas.Y - m - foot - bookH) / 2);
        var cover = new Rect2(x0, y0, bookW, bookH);
        CoverRect = cover;
        // The pages have the 3D pages' proportions (the camera's spread view puts them exactly under these): two pages of
        // PageWidth with the 2 m gap at the spine, PageDepth deep. The cover shrinks to fit them, centred where it was.
        // (Alone, with no 3D pages to lie on, it keeps its own proportions: the whole cover.)
        float ph = bookH - 2 * lip, span = 2 * BookBuilder.PageWidth + 2f;
        float pw = OwnTable ? Mathf.Floor((bookW - 2 * lip) / 2) : ph * span / BookBuilder.PageDepth / 2f;
        if (2 * pw > bookW - 2 * lip) { pw = (bookW - 2 * lip) / 2f; ph = 2 * pw * BookBuilder.PageDepth / span; }
        x0 += Mathf.Round((bookW - 2 * pw - 2 * lip) / 2); y0 += Mathf.Round((bookH - ph - 2 * lip) / 2);
        bookW = 2 * pw + 2 * lip; bookH = ph + 2 * lip;
        cover = new Rect2(x0, y0, bookW, bookH);
        CoverRect = cover;
        var left = new Rect2(x0 + lip, y0 + lip, pw, ph);
        var right = new Rect2(left.End.X, y0 + lip, pw, ph);
        _rightPage = right;
        _leftPage = left;

        // In the game no table or cover of its own: the 3D book and table the camera shows are the ones around the pages
        // (OwnTable false). Alone (the settings tests and their shots) it draws its own.
        if (OwnTable)
        {
            var back = new SpreadArt { Name = "Table", Cover = cover, Left = left, Right = right, S = s, MouseFilter = MouseFilterEnum.Ignore };
            back.SetAnchorsPreset(LayoutPreset.FullRect);
            _frame.AddChild(back);
        }
        BuildRibbons(right, s);
        _pagesMask = new SectionMask { Name = "PagesMask", Size = canvas };
        _frame.AddChild(_pagesMask);
        var pages = new SpreadArt { Name = "Pages", Cover = cover, Left = left, Right = right, S = s, PagesOnly = true, MouseFilter = MouseFilterEnum.Ignore, Size = canvas };
        _pagesMask.AddChild(pages);

        for (int i = 0; i < Sections.Length; i++)
        {
            // Each section lives under a mask the size of the canvas; a turning page narrows one mask and widens the other.
            var clip = new SectionMask { Name = "Section" + Sections[i], Size = canvas };
            _frame.AddChild(clip);
            _clip[i] = clip;
            BuildSection(i);
        }
        // The copy of the screen the leaf's front reads (its place in the order is set while a leaf turns: OrderForLeaf).
        _underLeaf = new BackBufferCopy { Name = "UnderLeaf", CopyMode = BackBufferCopy.CopyModeEnum.Disabled, Rect = new Rect2(Vector2.Zero, canvas) };
        _frame.AddChild(_underLeaf);
        _leaf = new PageLeaf { Name = "Leaf", S = s, Size = canvas };
        _frame.AddChild(_leaf);
        // The leaf's face lives beside the sections (its order is set by OrderForLeaf); the leaf and the frame share a place.
        _leaf.Face.Reparent(_frame, false);
        if (_slip.Length > 0) BuildSlip(s);
        SyncView();
        SetRibbonsPressed();
        if (_from >= 0) ApplyState(); else SetRest();
        if (_warm) WarmUp(true);
    }

    private void BuildSection(int i)
    {
        var clip = _clip[i]!;
        _building = i;
        // Round L3: each section carries its own paper, so the page coming hides the page going under it (the page going
        // is drawn whole while a leaf turns: the leaf's front carries it).
        if (clip.GetNodeOrNull("Paper") == null)
            clip.AddChild(new SpreadArt { Name = "Paper", Cover = CoverRect, Left = _leftPage, Right = _rightPage, S = _s, PagesOnly = true, MouseFilter = MouseFilterEnum.Ignore, Size = _canvas });
        var inner = new Control { Name = "Content", MouseFilter = MouseFilterEnum.Ignore, Size = _canvas };
        clip.AddChild(inner);
        _inner[i] = inner;
        float s = _s;
        var (leftCol, rightCol) = BuildPageColumns(inner, i, _leftPage, _rightPage, s);
        switch (i)
        {
            case SoundAt: BuildSound(leftCol, rightCol, s); break;
            case DisplayAt: BuildDisplay(leftCol, rightCol, s); break;
            case KeysAt: BuildKeys(leftCol, rightCol, s); break;
            case SavingAt: BuildSaving(leftCol, rightCol, s); break;
        }
        _building = -1;
    }

    /// <summary>Builds one section's pages again (and the slip), in place: its own controls asked for it.</summary>
    private void Refresh(int i)
    {
        if (_frame == null || _clip[i] == null) return;
        if (_inner[i] is { } old) { _clip[i]!.RemoveChild(old); old.QueueFree(); }
        _sec[i].Clear();
        BuildSection(i);
        if (_slipBlocker != null) { _frame.RemoveChild(_slipBlocker); _slipBlocker.QueueFree(); _slipBlocker = null; }
        if (_slipCard != null) { _frame.RemoveChild(_slipCard); _slipCard.QueueFree(); _slipCard = null; }
        _shared.Remove("slip.0");
        _shared.Remove("slip.1");
        _countdown = null;
        if (_slip.Length > 0) BuildSlip(_s);
        SyncView();
        if (_from >= 0) ApplyState(); else SetRest();
    }

    private void Refresh() => Refresh(_target);

    /// <summary>The controls by name: the shared ones and the chosen section's.</summary>
    private void SyncView()
    {
        _controls.Clear();
        foreach (var kv in _shared) _controls[kv.Key] = kv.Value;
        foreach (var kv in _sec[_target]) _controls[kv.Key] = kv.Value;
    }

    private void BuildRibbons(Rect2 right, float s)
    {
        var group = new ButtonGroup { AllowUnpress = false };
        float tuck = SettingsLook.Tuck * s, h = 54 * s, gap = 14 * s, y = right.Position.Y + 70 * s;
        for (int i = 0; i < Sections.Length; i++)
        {
            string name = Sections[i];
            var b = new Button
            {
                Name = "Ribbon" + name, Text = name, ToggleMode = true, ButtonGroup = group, FocusMode = FocusModeEnum.All,
                Alignment = HorizontalAlignment.Left, TooltipText = Blurbs[i],
                Position = new Vector2(right.End.X - tuck - 2 * s, y + i * (h + gap)), Size = new Vector2(RibbonWidth(i), h),
            };
            b.Pressed += () => { if (Section != name) ShowSection(name); };
            _frame!.AddChild(b);
            _shared["ribbon." + name] = b;
            _ribbons[i] = b;
            StyleRibbon(b, i, RibbonTints[i]);
        }
    }

    private float RibbonWidth(int i) => 150 * _s + SettingsLook.Tuck * _s - 12 * _s + 24 * _s * _ribbonOut[i];

    private void SetRibbonsPressed()
    {
        for (int i = 0; i < 4; i++) if (_ribbons[i] is { } b && IsInstanceValid(b)) b.SetPressedNoSignal(i == _target);
    }

    private (VBoxContainer left, VBoxContainer right) BuildPageColumns(Control into, int sec, Rect2 left, Rect2 right, float s)
    {
        float padX = 46 * s, padTop = 34 * s, padBottom = 22 * s;
        var lbox = new VBoxContainer { Name = "LeftPage", Position = left.Position + new Vector2(padX, padTop),
            Size = left.Size - new Vector2(2 * padX, padTop + padBottom) };
        lbox.AddThemeConstantOverride("separation", UiKit.Px(s, 8));
        into.AddChild(lbox);
        lbox.AddChild(UiKit.Text("Settings", s, 22, UiKit.Display, UiKit.FadedInk));
        var heading = UiKit.Text(Sections[sec], s, 44, UiKit.Serif);
        heading.Name = "Heading";
        lbox.AddChild(heading);
        var blurb = UiKit.Text(Blurbs[sec], s, 17, null, UiKit.FadedInk);
        blurb.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        lbox.AddChild(blurb);
        lbox.AddChild(UiKit.Rule(s, 0.6f));
        // The width a row may use: the page column less the focus-frame margins and the scroll bar.
        _colW = lbox.Size.X - 2 * UiKit.Px(s, 7) - UiKit.Px(s, 6) - 12 * s;
        var leftCol = Column(lbox, "LeftScroll", s);

        var foot = new HBoxContainer { Name = "Foot" };
        lbox.AddChild(foot);
        var backBtn = new Button { Name = "Back", Text = "Back", FocusMode = FocusModeEnum.All, TooltipText = "Close the settings (Esc)",
            CustomMinimumSize = new Vector2(130 * s, 0) };
        backBtn.Pressed += Close;
        foot.AddChild(backBtn);
        Target["back"] = backBtn;
        foot.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        foot.AddChild(Folio(sec * 2 + 1, s));

        var rbox = new VBoxContainer { Name = "RightPage", Position = right.Position + new Vector2(padX, padTop),
            Size = right.Size - new Vector2(2 * padX, padTop + padBottom) };
        rbox.AddThemeConstantOverride("separation", UiKit.Px(s, 8));
        into.AddChild(rbox);
        var rightCol = Column(rbox, "RightScroll", s);
        var rfoot = new HBoxContainer();
        rfoot.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        rfoot.AddChild(Folio(sec * 2 + 2, s));
        rbox.AddChild(rfoot);
        return (leftCol, rightCol);
    }

    private static Label Folio(int n, float s)
    {
        var l = UiKit.Text(n.ToString(), s, 18, UiKit.Serif, UiKit.FadedInk);
        l.VerticalAlignment = VerticalAlignment.Bottom;
        l.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        return l;
    }

    private static VBoxContainer Column(Control parent, string name, float s)
    {
        var scroll = new ScrollContainer { Name = name, FollowFocus = true, SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        parent.AddChild(scroll);
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", UiKit.Px(s, 14));
        // Room for the focus frame, which is drawn outside the controls.
        var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        int p = UiKit.Px(s, 7);
        pad.AddThemeConstantOverride("margin_left", p);
        pad.AddThemeConstantOverride("margin_right", p + UiKit.Px(s, 6));
        pad.AddThemeConstantOverride("margin_top", p);
        pad.AddThemeConstantOverride("margin_bottom", p);
        scroll.AddChild(pad);
        pad.AddChild(col);
        return col;
    }

    // ---------------------------------------------------------------- shared row pieces

    /// <summary>A sub-heading on a page.</summary>
    private static void Heading(VBoxContainer col, string text, float s)
    {
        var l = UiKit.Text(text, s, 26, UiKit.Serif);
        col.AddChild(l);
    }

    /// <summary>
    /// A labelled row: the name on the left, the control on the right, an optional note under it. When the column is
    /// too narrow for both side by side (large interface size in a small window), the control goes under its name.
    /// </summary>
    private Control Row(VBoxContainer col, string label, Control control, float s, string? note = null, float width = 250)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", UiKit.Px(s, 4));
        bool stack = _colW < (width + 150) * s;
        Container row = stack ? new VBoxContainer() : new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiKit.Px(s, stack ? 4 : 14));
        var l = UiKit.Text(label, s, 19);
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(stack ? 0 : 120 * s, 0);
        l.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(l);
        control.CustomMinimumSize = new Vector2(Mathf.Min(width * s, _colW), Mathf.Max(control.CustomMinimumSize.Y, 40 * s));
        control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        // Stacked: a slider row takes the whole width, a button keeps its own.
        control.SizeFlagsHorizontal = stack && control is not HBoxContainer ? SizeFlags.ShrinkBegin : SizeFlags.Fill;
        row.AddChild(control);
        box.AddChild(row);
        if (note != null)
        {
            var n = UiKit.Text(note, s, 15, null, UiKit.FadedInk);
            n.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(n);
        }
        col.AddChild(box);
        return box;
    }

    private Button PageButton(string key, string text, float s, Action onPress, string tooltip = "")
    {
        var b = new Button { Text = text, FocusMode = FocusModeEnum.All, TooltipText = tooltip };
        b.Pressed += onPress;
        Target[key] = b;
        return b;
    }
}

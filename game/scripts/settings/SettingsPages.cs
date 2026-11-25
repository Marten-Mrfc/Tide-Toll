using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game.Settings;

/// <summary>
/// The settings as an open spread of the book: four ribbon bookmarks on the right edge switch between Sound, Display,
/// Keys and Saving; the left page carries the section's title and the Back control. Fills the screen and stops the
/// mouse reaching the game. Raises Closed on Back or Esc (the owner hides or frees it). Everything works with the
/// keyboard alone: arrows or Tab move, Enter or Space use, left and right change sliders and choices, Esc goes back.
/// Built in canvas units from a scale taken from the real window height, so the interface size (the window's content
/// scale) makes it larger or smaller; pages scroll when their contents do not fit. Rebuilt when the window changes.
/// </summary>
public partial class SettingsPages : Control
{
    public static readonly string[] Sections = { "Sound", "Display", "Keys", "Saving" };
    private static readonly string[] Blurbs =
    {
        "How loud each part of the game is.",
        "The window, its size and how large the writing is.",
        "Which key does what. Choose a key, then press the new one.",
        "When the town is written down, and where.",
    };
    private static readonly Color[] RibbonTints =
    {
        Color.FromHtml("#D9C9E6"), Color.FromHtml("#C9DCE6"), Color.FromHtml("#CFE0C3"), Color.FromHtml("#EAD7B0"),
    };

    public event Action? Closed;

    public GameSettings Store { get; }
    public SettingsApplier Applier { get; }
    public ISaveActions? Saves { get; }
    public string Section { get; private set; } = "Sound";
    /// <summary>Seconds a new window mode or size waits for "Keep" before it goes back.</summary>
    public float KeepSeconds { get; set; } = 10f;
    /// <summary>Every control by a stable name ("ribbon.Sound", "sound.Music", "display.mode", "keys.tt_bell.0", ...).</summary>
    public IReadOnlyDictionary<string, Control> Controls => _controls;
    /// <summary>The slip (small dialog) on show: "", "keep", "delete" or "clash".</summary>
    public string Slip => _slip;
    public float UiScale => _s;

    private readonly Dictionary<string, Control> _controls = new();
    private Control? _frame;
    private float _s = 1f;
    private string _slip = "";
    private bool _rebuildQueued;
    private AudioStreamPlayer _tick = null!;
    private Label? _countdown;
    private Rect2 _rightPage;
    private float _colW = 600f;

    public SettingsPages() : this(GameSettings.Current ?? new GameSettings(), null!, null) { }

    public SettingsPages(SettingsRuntime runtime, ISaveActions? saves) : this(runtime.Store, runtime.Applier, saves) { }

    public SettingsPages(GameSettings store, SettingsApplier applier, ISaveActions? saves)
    {
        Store = store;
        Applier = applier;
        Saves = saves;
        Name = "SettingsPages";
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _Ready()
    {
        _tick = new AudioStreamPlayer { Name = "TestSound", Stream = GD.Load<AudioStream>("res://assets/audio/sfx/coin-01.wav"), VolumeDb = -6 };
        AddChild(_tick);
        GetViewport().SizeChanged += QueueRebuild;
        Store.Changed += OnStoreChanged;
        Build();
        FocusName("ribbon." + Section);
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= QueueRebuild;
        Store.Changed -= OnStoreChanged;
    }

    private void OnStoreChanged(string key) { if (key == GameSettings.KInterface) QueueRebuild(); }

    /// <summary>Switches to a section (by name) and puts the focus on its ribbon.</summary>
    public void ShowSection(string name)
    {
        if (Array.IndexOf(Sections, name) < 0) return;
        CancelCapture();
        Section = name;
        Build();
        FocusName("ribbon." + name);
    }

    public void Close()
    {
        CancelCapture();
        if (_slip == "keep") RevertDisplay();
        _slip = "";
        Closed?.Invoke();
    }

    public void QueueRebuild()
    {
        if (_rebuildQueued) return;
        _rebuildQueued = true;
        CallDeferred(MethodName.RebuildKeepingFocus);
    }

    private void RebuildKeepingFocus()
    {
        _rebuildQueued = false;
        if (!IsInsideTree()) return;
        string? focused = FocusedName();
        Build();
        if (focused != null && _controls.ContainsKey(focused)) FocusName(focused);
        else FocusDefault();
    }

    public string? FocusedName()
    {
        var f = GetViewport().GuiGetFocusOwner();
        foreach (var kv in _controls) if (kv.Value == f) return kv.Key;
        return null;
    }

    public void FocusName(string name)
    {
        if (_controls.TryGetValue(name, out var c) && c.IsVisibleInTree()) c.GrabFocus();
    }

    private void FocusDefault()
    {
        if (_slip.Length > 0) FocusName("slip.0"); else FocusName("ribbon." + Section);
    }

    // ---------------------------------------------------------------- building

    private void Build()
    {
        if (_frame != null) { RemoveChild(_frame); _frame.QueueFree(); }
        _controls.Clear();
        _countdown = null;
        var canvas = GetViewportRect().Size;
        _s = UiKit.ScaleFor(GetWindow().Size.Y);
        float s = _s;
        _frame = new Control { Name = "Frame", MouseFilter = MouseFilterEnum.Ignore, Theme = SettingsLook.Theme(s) };
        _frame.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_frame);

        float m = 16 * s, ribbonOut = 150 * s, lip = 10 * s;
        float bookW = Mathf.Min(canvas.X - 2 * m - ribbonOut, 1600 * s);
        float bookH = Mathf.Min(canvas.Y - 2 * m, 900 * s);
        float x0 = Mathf.Round((canvas.X - bookW - ribbonOut) / 2), y0 = Mathf.Round((canvas.Y - bookH) / 2);
        var cover = new Rect2(x0, y0, bookW, bookH);
        float pw = Mathf.Floor((bookW - 2 * lip) / 2);
        var left = new Rect2(x0 + lip, y0 + lip, pw, bookH - 2 * lip);
        var right = new Rect2(left.End.X, y0 + lip, pw, bookH - 2 * lip);
        _rightPage = right;

        var back = new SpreadArt { Name = "Table", Cover = cover, Left = left, Right = right, S = s, MouseFilter = MouseFilterEnum.Ignore };
        back.SetAnchorsPreset(LayoutPreset.FullRect);
        _frame.AddChild(back);
        BuildRibbons(right, s);
        var pages = new SpreadArt { Name = "Pages", Cover = cover, Left = left, Right = right, S = s, PagesOnly = true, MouseFilter = MouseFilterEnum.Ignore };
        pages.SetAnchorsPreset(LayoutPreset.FullRect);
        _frame.AddChild(pages);

        var (leftCol, rightCol) = BuildPageColumns(left, right, s);
        switch (Section)
        {
            case "Sound": BuildSound(leftCol, rightCol, s); break;
            case "Display": BuildDisplay(leftCol, rightCol, s); break;
            case "Keys": BuildKeys(leftCol, rightCol, s); break;
            case "Saving": BuildSaving(leftCol, rightCol, s); break;
        }
        if (_slip.Length > 0) BuildSlip(s);
    }

    private void BuildRibbons(Rect2 right, float s)
    {
        var group = new ButtonGroup { AllowUnpress = false };
        float tuck = SettingsLook.Tuck * s, h = 54 * s, gap = 14 * s, y = right.Position.Y + 70 * s;
        for (int i = 0; i < Sections.Length; i++)
        {
            string name = Sections[i];
            bool on = name == Section;
            var b = new Button
            {
                Name = "Ribbon" + name, Text = name, ToggleMode = true, ButtonGroup = group, FocusMode = FocusModeEnum.All,
                Alignment = HorizontalAlignment.Left, TooltipText = Blurbs[i],
                Position = new Vector2(right.End.X - tuck - 2 * s, y + i * (h + gap)), Size = new Vector2(150 * s + tuck - 12 * s + (on ? 24 * s : 0), h),
            };
            SettingsLook.Ribbon(b, s, RibbonTints[i]);
            b.SetPressedNoSignal(on);
            b.Pressed += () => { if (Section != name) ShowSection(name); };
            _frame!.AddChild(b);
            _controls["ribbon." + name] = b;
        }
    }

    private (VBoxContainer left, VBoxContainer right) BuildPageColumns(Rect2 left, Rect2 right, float s)
    {
        float padX = 46 * s, padTop = 34 * s, padBottom = 22 * s;
        var lbox = new VBoxContainer { Name = "LeftPage", Position = left.Position + new Vector2(padX, padTop),
            Size = left.Size - new Vector2(2 * padX, padTop + padBottom) };
        lbox.AddThemeConstantOverride("separation", UiKit.Px(s, 8));
        _frame!.AddChild(lbox);
        lbox.AddChild(UiKit.Text("Settings", s, 22, UiKit.Display, UiKit.FadedInk));
        var heading = UiKit.Text(Section, s, 44, UiKit.Serif);
        heading.Name = "Heading";
        lbox.AddChild(heading);
        var blurb = UiKit.Text(Blurbs[Array.IndexOf(Sections, Section)], s, 17, null, UiKit.FadedInk);
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
        _controls["back"] = backBtn;
        foot.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        foot.AddChild(Folio(Array.IndexOf(Sections, Section) * 2 + 1, s));

        var rbox = new VBoxContainer { Name = "RightPage", Position = right.Position + new Vector2(padX, padTop),
            Size = right.Size - new Vector2(2 * padX, padTop + padBottom) };
        rbox.AddThemeConstantOverride("separation", UiKit.Px(s, 8));
        _frame.AddChild(rbox);
        var rightCol = Column(rbox, "RightScroll", s);
        var rfoot = new HBoxContainer();
        rfoot.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        rfoot.AddChild(Folio(Array.IndexOf(Sections, Section) * 2 + 2, s));
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

    /// <summary>A labelled row: the name on the left, the control on the right, an optional note under it.</summary>
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
        _controls[key] = b;
        return b;
    }

    private float _lastTick = -1f;
    private void PlayTick(string bus)
    {
        float now = Time.GetTicksMsec() / 1000f;
        if (now - _lastTick < 0.12f) return;
        _lastTick = now;
        _tick.Bus = bus;
        _tick.Play();
    }

    // ---------------------------------------------------------------- input

    public override void _Input(InputEvent e)
    {
        if (!IsVisibleInTree()) return;
        if (_capture != null)
        {
            if (e is InputEventKey k && k.Pressed && !k.Echo) { Captured(k); GetViewport().SetInputAsHandled(); }
            else if (e is InputEventMouseButton mb && mb.Pressed) CancelCapture();
            else if (e is InputEventKey) GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.IsAction("ui_cancel", true))
        {
            GetViewport().SetInputAsHandled();
            if (_slip.Length > 0) SlipCancel(); else Close();
        }
    }

    public override void _Process(double delta)
    {
        if (_slip != "keep") return;
        _keepLeft -= (float)delta;
        if (_keepLeft <= 0) { RevertDisplay(); return; }
        if (_countdown != null) _countdown.Text = KeepText();
    }
}

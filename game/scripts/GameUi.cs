using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game;

/// <summary>
/// The interface inside the book (node "Hud", a CanvasLayer): the chapter page on the left page (ChapterPage), the
/// bookmark tabs out of the map page's outer edge (ToolTabs), the flow-preview readout pinned near the pointer, and a
/// small fps and ms-per-tick readout in the bottom-right corner. It follows the book on screen every frame (pans,
/// zooms, book and map views), is rebuilt at the right scale when the window size changes, and owns the keyboard
/// shortcuts (handled before the controls, so Space always pauses and buttons still answer Enter).
/// Text is updated about twice a second and only where a value changed.
/// </summary>
public partial class GameUi : CanvasLayer
{
    private Main _main = null!;
    private Control _root = null!;
    public ChapterPage Chapter { get; private set; } = null!;
    public ToolTabs Tabs { get; private set; } = null!;
    private PanelContainer _tip = null!;
    private Label _tipText = null!, _fps = null!;
    private float _s;
    private Vector2 _builtFor;
    private double _since = 1, _now;
    private Vector2 _tabsAt = new(-1, -1);
    private string _shownPreview = "";

    public bool Overlay { get; private set; }
    /// <summary>Every control by name (tests): the tabs, speed buttons and sliders.</summary>
    public IReadOnlyDictionary<string, Control> Controls => Tabs.Controls;
    public float UiScale => _s;
    public int Rebuilds { get; private set; }

    public override void _Ready()
    {
        _main = GetParent<Main>();
        Name = "Hud";
        Layer = 10;
        Build();
        GetViewport().SizeChanged += OnResize;
        _main.Sim.TimeChanged += Sync;
        _main.Sim.WorldReplaced += OnWorldReplaced;
        _main.Tools.ToolChanged += Sync;
        _main.Tools.Message += OnMessage;
        _main.Audio.Bell += OnBell;
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= OnResize;
        _main.Sim.TimeChanged -= Sync;
        _main.Sim.WorldReplaced -= OnWorldReplaced;
        _main.Tools.ToolChanged -= Sync;
        _main.Tools.Message -= OnMessage;
        _main.Audio.Bell -= OnBell;
    }

    private void OnResize() => CallDeferred(MethodName.RebuildIfResized);

    private void RebuildIfResized()
    {
        var vp = GetViewport().GetVisibleRect().Size;
        if (vp == _builtFor) return;
        _root.QueueFree();
        Build();
    }

    private void Build()
    {
        var vp = GetViewport().GetVisibleRect().Size;
        _builtFor = vp;
        _s = UiKit.ScaleFor(vp.Y);
        Rebuilds++;
        _root = new Control { Name = "Root", Theme = UiKit.MakeTheme(_s), MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        Chapter = new ChapterPage();
        Chapter.Build(_s);
        _root.AddChild(Chapter);

        Tabs = new ToolTabs();
        Tabs.Build(_s);
        _root.AddChild(Tabs);
        Wire();

        _tip = new PanelContainer { Name = "PreviewTip", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        var tipStyle = UiKit.Card(_s, UiKit.Paper, 1.5f);
        tipStyle.SetContentMarginAll(UiKit.Px(_s, 7));
        _tip.AddThemeStyleboxOverride("panel", tipStyle);
        _tipText = UiKit.Text("", _s, 15, UiKit.UiBold);
        _tip.AddChild(_tipText);
        _root.AddChild(_tip);

        _fps = UiKit.Text("", _s, 13, UiKit.UiFont, new Color(UiKit.Paper, 0.85f));
        _fps.Name = "Fps";
        // Pinned to the bottom-right corner, growing leftward as the text changes length.
        _fps.AnchorLeft = _fps.AnchorRight = _fps.AnchorTop = _fps.AnchorBottom = 1;
        _fps.GrowHorizontal = Control.GrowDirection.Begin;
        _fps.GrowVertical = Control.GrowDirection.Begin;
        _fps.OffsetLeft = _fps.OffsetRight = -UiKit.Px(_s, 10);
        _fps.OffsetTop = _fps.OffsetBottom = -UiKit.Px(_s, 6);
        _root.AddChild(_fps);

        _main.Rig.PlayRoomPx = ToolTabs.WidthFor(_s) + 24 * _s;
        _tabsAt = new Vector2(-1, -1);
        _shownPreview = "";
        Chapter.SetStatus(_main.Tools.LastMessage.Length > 0 ? _main.Tools.LastMessage : "Choose a tool from the bookmarks on the right, or press F, B or E.");
        Chapter.SetOverlay(Overlay);
        Sync();
        _since = 1;
    }

    private void Wire()
    {
        var sim = _main.Sim;
        Tabs.Pause.Toggled += on => sim.SetPaused(on);
        int[] sp = { 1, 2, 4, 8 };
        for (int i = 0; i < 4; i++) { int v = sp[i]; Tabs.Speed[i].Pressed += () => { sim.SetSpeed(v); Sync(); }; }
        Tabs.Fence.Pressed += () => _main.Tools.ToggleTool(Tool.Fence);
        Tabs.Bridge.Pressed += () => _main.Tools.ToggleTool(Tool.Bridge);
        Tabs.Erase.Pressed += () => _main.Tools.ToggleTool(Tool.Erase);
        Tabs.Preview.Toggled += SetPreview;
        Tabs.Overlay.Toggled += SetOverlay;
        Tabs.Sound.Toggled += SetSound;
        Tabs.Volume.SetValueNoSignal(_main.Audio.Volume * 100);
        Tabs.Volume.ValueChanged += v => _main.Audio.SetVolume((float)v / 100f);
        Tabs.Reset.Pressed += Reset;
        Tabs.NewMap.Pressed += NewMap;
        Tabs.Walkers.SetValueNoSignal(sim.WalkerCount);
        Tabs.Walkers.ValueChanged += v => { sim.SetWalkers((int)v); Tabs.WalkersLabel.Text = $"Walkers {sim.WalkerCount}"; };
    }

    /// <summary>Puts every control's state in line with the game (after a click, a key or an event).</summary>
    public void Sync()
    {
        var sim = _main.Sim;
        Tabs.ShowPaused(sim.Paused);
        int[] sp = { 1, 2, 4, 8 };
        for (int i = 0; i < 4; i++) Tabs.Speed[i].SetPressedNoSignal(sim.Speed == sp[i]);
        var t = _main.Tools.Tool;
        Tabs.Fence.SetPressedNoSignal(t == Tool.Fence);
        Tabs.Bridge.SetPressedNoSignal(t == Tool.Bridge);
        Tabs.Erase.SetPressedNoSignal(t == Tool.Erase);
        Tabs.Preview.SetPressedNoSignal(_main.Tools.PreviewEnabled);
        Tabs.Overlay.SetPressedNoSignal(Overlay);
        Tabs.ShowSound(_main.Audio.Enabled);
        Tabs.Walkers.SetValueNoSignal(sim.WalkerCount);
        Tabs.WalkersLabel.Text = $"Walkers {sim.WalkerCount}";
    }

    // ------------------------------------------------------------------ actions (buttons and keys)

    public void SetOverlay(bool on)
    {
        Overlay = on;
        _main.Ground.WearOverlay = on;
        Chapter.SetOverlay(on);
        Sync();
    }

    public void SetPreview(bool on)
    {
        _main.Tools.PreviewEnabled = on;
        _main.Tools.Say(on ? "Flow preview on: tools show the routes that would change." : "Flow preview off.");
        Sync();
    }

    public void SetSound(bool on)
    {
        _main.Audio.SetEnabled(on);
        _main.Tools.Say(on ? "Sound on." : "Sound off.");
        Sync();
    }

    public void Reset() { _main.Sim.Reset(); _main.Tools.Say("Map reset: day one again, same seed."); }
    public void NewMap() { _main.Sim.NewMap(); _main.Tools.Say($"New map, seed {_main.Sim.Seed}."); }

    private void OnWorldReplaced() { Sync(); _since = 1; }
    private void OnMessage(string m) => Chapter.SetStatus(m);
    private void OnBell() => Chapter.Bell(_now);

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k || k.CtrlPressed || k.AltPressed || k.MetaPressed) return;
        var sim = _main.Sim;
        var tools = _main.Tools;
        bool handled = true;
        switch (k.Keycode)
        {
            case Key.Space: sim.TogglePause(); break;
            case Key.Key1: sim.SetSpeed(1); Sync(); break;
            case Key.Key2: sim.SetSpeed(2); Sync(); break;
            case Key.Key3: sim.SetSpeed(4); Sync(); break;
            case Key.Key4: sim.SetSpeed(8); Sync(); break;
            case Key.F: tools.ToggleTool(Tool.Fence); break;
            case Key.B: tools.ToggleTool(Tool.Bridge); break;
            case Key.E: tools.ToggleTool(Tool.Erase); break;
            case Key.Escape:
                tools.SetTool(Tool.None);
                _root.GetViewport().GuiReleaseFocus();
                break;
            case Key.P: SetPreview(!tools.PreviewEnabled); break;
            case Key.W: SetOverlay(!Overlay); break;
            case Key.M: SetSound(!_main.Audio.Enabled); break;
            case Key.R: Reset(); break;
            case Key.N: NewMap(); break;
            case Key.Tab when _root.GetViewport().GuiGetFocusOwner() == null:
                // Nothing has focus yet: Tab starts at the first bookmark (Godot only moves focus that exists).
                (k.ShiftPressed ? (Control)Tabs.Walkers : Tabs.Pause).GrabFocus();
                break;
            default: handled = false; break;
        }
        if (handled) GetViewport().SetInputAsHandled();
    }

    // ------------------------------------------------------------------ per frame

    public override void _Process(double delta)
    {
        _now += delta;
        var vp = GetViewport().GetVisibleRect().Size;
        Place(vp);
        Tip(vp);
        _since += delta;
        if (_since < 0.5) return;
        _since = 0;
        var sim = _main.Sim;
        var w = sim.World;
        Chapter.Refresh(w.Stats(), w.WalkersOut(), sim.Seed, _now);
        _fps.Text = $"{Engine.GetFramesPerSecond():0} fps · sim {sim.MsPerTick:0.00} ms/tick · {sim.TicksPerSecond:0} ticks/s";
    }

    /// <summary>The chapter column over the left page, the tabs out of the right page's outer edge.</summary>
    private void Place(Vector2 vp)
    {
        var cam = _main.Rig.Camera;
        var left = _main.LeftPageRect;
        var a = cam.UnprojectPosition(new Vector3(left.Position.X, 0, left.Position.Y));
        var b = cam.UnprojectPosition(new Vector3(left.End.X, 0, left.End.Y));
        Chapter.Place(new Rect2(a, b - a).Abs(), vp);
        var right = _main.RightPageRect;
        var edge = cam.UnprojectPosition(new Vector3(right.End.X, 0, right.Position.Y));
        float x = Mathf.Min(edge.X - 3 * _s, vp.X - Tabs.TabWidth - 10 * _s);
        float y = Mathf.Max(12 * _s, edge.Y + 20 * _s);
        float h = Tabs.GetCombinedMinimumSize().Y;
        if (y + h > vp.Y - 30 * _s) y = Mathf.Max(8 * _s, vp.Y - 30 * _s - h);
        var at = new Vector2(Mathf.Round(x), Mathf.Round(y));
        if (at != _tabsAt) { _tabsAt = at; Tabs.Position = at; Tabs.Size = new Vector2(Tabs.TabWidth, h); }
    }

    /// <summary>The preview readout near the pointer, repeated on the chapter page as a status line.</summary>
    private void Tip(Vector2 vp)
    {
        var tools = _main.Tools;
        string text = tools.PreviewEnabled ? tools.Previewer.Text : "";
        if (!ReferenceEquals(text, _shownPreview))
        {
            _shownPreview = text;
            _tipText.Text = text;
            Chapter.SetPreviewLine(text);
            _tip.ResetSize();
        }
        bool show = text.Length > 0 && tools.PointerKnown && !tools.PointerOverUi && tools.Tool != Tool.None;
        if (_tip.Visible != show) _tip.Visible = show;
        if (!show) return;
        var p = tools.Pointer;
        var size = _tip.Size;
        float x = Mathf.Min(p.X + 18 * _s, vp.X - size.X - 6);
        float y = p.Y + 22 * _s + size.Y > vp.Y ? p.Y - size.Y - 14 * _s : p.Y + 22 * _s;
        var at = new Vector2(Mathf.Round(Mathf.Max(6, x)), Mathf.Round(Mathf.Max(6, y)));
        if (_tip.Position != at) _tip.Position = at;
    }
}

using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim;

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
    /// <summary>The paper tags naming the well, market, field and ford at overview zoom.</summary>
    public MapTags Tags { get; private set; } = null!;
    public ToolTabs Tabs { get; private set; } = null!;
    /// <summary>A badge by every drawbridge (state, queues, lever, keeper), and the notes that float up from the river.</summary>
    public BridgeBadges Badges { get; private set; } = null!;
    public RiverFloats Floats { get; private set; } = null!;
    private PanelContainer _tip = null!, _fpsPlate = null!;
    private Label _tipText = null!, _fps = null!;
    private ToolTabs.PageLip _lip = null!;
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
        _main.Feed.Chronicle += OnChronicle;
        _main.Feed.Event += OnRiverEvent;
        _main.Feed.CycleNote += OnCycleNote;
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= OnResize;
        _main.Sim.TimeChanged -= Sync;
        _main.Sim.WorldReplaced -= OnWorldReplaced;
        _main.Tools.ToolChanged -= Sync;
        _main.Tools.Message -= OnMessage;
        _main.Audio.Bell -= OnBell;
        _main.Feed.Chronicle -= OnChronicle;
        _main.Feed.Event -= OnRiverEvent;
        _main.Feed.CycleNote -= OnCycleNote;
    }

    private void OnResize() => CallDeferred(MethodName.RebuildIfResized);

    private void RebuildIfResized()
    {
        var vp = GetViewport().GetVisibleRect().Size;
        if (vp == _builtFor) return;
        // Out of the tree first, so the new "Root" never sits beside the old one (it would be renamed "@Root@2").
        RemoveChild(_root);
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

        // First, so the chapter page and the tabs lie over them.
        Tags = new MapTags();
        Tags.Build(_main, _s);
        _root.AddChild(Tags);

        Floats = new RiverFloats();
        Floats.Build(_main, _s);
        _root.AddChild(Floats);
        Badges = new BridgeBadges();
        Badges.Build(_main, _s);
        _root.AddChild(Badges);

        Chapter = new ChapterPage();
        Chapter.Build(_s, _main.Params.River != null);
        _root.AddChild(Chapter);

        Tabs = new ToolTabs();
        Tabs.Build(_s, _main.Params.River);
        _root.AddChild(Tabs);
        // Drawn over the tabs: the page's edge lying on top of them, so they read as bookmarks tucked under the page.
        _lip = new ToolTabs.PageLip { Name = "PageLip", Tabs = Tabs, Scale1 = _s };
        _root.AddChild(_lip);
        Wire();

        _tip = new PanelContainer { Name = "PreviewTip", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        var tipStyle = UiKit.Card(_s, UiKit.Paper, 1.5f);
        tipStyle.SetContentMarginAll(UiKit.Px(_s, 7));
        _tip.AddThemeStyleboxOverride("panel", tipStyle);
        _tipText = UiKit.Text("", _s, 15, UiKit.UiBold);
        _tip.AddChild(_tipText);
        _root.AddChild(_tip);

        // The fps and ms-per-tick readout: paper text on a small ink plate in the bottom-right corner, so it reads over
        // the table, the page stack or the map alike; placed in _Process whenever its text changes.
        _fpsPlate = new PanelContainer { Name = "Fps", MouseFilter = Control.MouseFilterEnum.Ignore };
        var plate = new StyleBoxFlat { BgColor = new Color(UiKit.Ink, 0.9f), BorderColor = new Color(UiKit.Paper, 0.35f), AntiAliasing = true };
        plate.SetBorderWidthAll(1);
        plate.SetCornerRadiusAll(UiKit.Px(_s, 4));
        plate.ContentMarginLeft = plate.ContentMarginRight = UiKit.Px(_s, 8);
        plate.ContentMarginTop = plate.ContentMarginBottom = UiKit.Px(_s, 3);
        _fpsPlate.AddThemeStyleboxOverride("panel", plate);
        _fps = UiKit.Text("", _s, 13, UiKit.UiFont, UiKit.Paper);
        _fps.Name = "Text";
        _fpsPlate.AddChild(_fps);
        _fpsPlate.Visible = false; // until it has text
        _root.AddChild(_fpsPlate);

        _main.Rig.PlayRoomPx = ToolTabs.WidthFor(_s) + 24 * _s;
        _tabsAt = new Vector2(-1, -1);
        _shownPreview = "";
        Chapter.SetStatus(_main.Tools.LastMessage.Length > 0 ? _main.Tools.LastMessage : "Choose a tool from the bookmarks on the right, or press F, B or E.");
        Chapter.SetOverlay(Overlay);
        Chapter.SetNotes(_main.Feed);
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
        if (Tabs.Ferry != null) Tabs.Ferry.Pressed += () => _main.Tools.ToggleTool(Tool.Ferry);
        if (Tabs.Drawbridge != null) Tabs.Drawbridge.Pressed += () => _main.Tools.ToggleTool(Tool.Drawbridge);
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
        Tabs.Ferry?.SetPressedNoSignal(t == Tool.Ferry);
        Tabs.Drawbridge?.SetPressedNoSignal(t == Tool.Drawbridge);
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

    private void OnWorldReplaced() { Sync(); Tags.SetWorld(_main.Sim.World); Badges.Rebuild(); Floats.Clear(); _since = 1; }
    private void OnChronicle() => Chapter.SetNotes(_main.Feed);
    private void OnRiverEvent(RiverEvent e) => Floats.OnEvent(e);
    private void OnCycleNote(int crossing, string text) => Floats.AtCrossing(crossing, text, false);
    private void OnMessage(string m) => Chapter.SetStatus(m);
    private void OnBell() => Chapter.Bell(_now);

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k || k.AltPressed || k.MetaPressed) return;
        var sim = _main.Sim;
        var tools = _main.Tools;
        // The river's keys: L and K for the selected drawbridge, Ctrl+1..9 for drawbridge n.
        if (tools.RiverKey(k.Keycode, k.CtrlPressed)) { GetViewport().SetInputAsHandled(); return; }
        if (k.CtrlPressed) return;
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
            case Key.Y when tools.RiverOn: tools.ToggleTool(Tool.Ferry); break;
            case Key.D when tools.RiverOn: tools.ToggleTool(Tool.Drawbridge); break;
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
        if (_main.Sim.World.River is { } rv) Chapter.TrackDay(rv, _main.Sim.World.Time, _main.Sim.World.Params.DaySimSec);
        _since += delta;
        if (_since < 0.5) return;
        _since = 0;
        var sim = _main.Sim;
        var w = sim.World;
        Chapter.Refresh(w.Stats(), w.WalkersOut(), sim.Seed, _now);
        if (w.River is { } river)
        {
            Chapter.RefreshRiver(river, w.Time, w.Params.DaySimSec, _main.Feed);
            Tabs.ShowPrices(river.Money);
        }
        _fps.Text = $"{Engine.GetFramesPerSecond():0} fps · sim {sim.MsPerTick:0.00} ms/tick · {sim.TicksPerSecond:0} ticks/s";
        PlaceFps(vp);
    }

    /// <summary>The readout's plate, a margin in from the bottom-right corner of the window.</summary>
    private void PlaceFps(Vector2 vp)
    {
        _fpsPlate.Visible = true;
        _fpsPlate.ResetSize();
        float m = UiKit.Px(_s, 12);
        var at = new Vector2(Mathf.Round(vp.X - _fpsPlate.Size.X - m), Mathf.Round(vp.Y - _fpsPlate.Size.Y - m));
        if (_fpsPlate.Position != at) _fpsPlate.Position = at;
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
        // Tucked a few pixels under the page edge (the lip draws the edge over them); pushed further onto the page
        // only when the window is too narrow, and then they lie on it as loose slips (no lip).
        float tuck = ToolTabs.Tuck * _s;
        float x = Mathf.Min(edge.X - tuck, vp.X - Tabs.TabWidth - 10 * _s);
        _lip.Place(edge.X, x + tuck >= edge.X - 0.5f);
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

using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game.Settings;

/// <summary>
/// The settings as an open spread of the book: four ribbon bookmarks on the right edge switch between Sound, Display,
/// Keys and Saving; the left page carries the section's title and the Back control. Fills the screen and stops the
/// mouse reaching the game. Raises Closed on Back or Esc (the owner hides it). Everything works with the keyboard
/// alone: arrows or Tab move, Enter or Space use, left and right change sliders and choices, Esc goes back.
/// Built in canvas units from a scale taken from the real window height, so the interface size (the window's content
/// scale) makes it larger or smaller; pages scroll when their contents do not fit.
/// All four sections are built at once and kept (the whole spread is built again only when the window or the
/// interface size changes): choosing a ribbon turns a page (<see cref="TurnSeconds"/>), a leaf sweeping across the
/// spread forwards for a later section and backwards for an earlier one, the old section going out ahead of its edge
/// and the new one coming in behind it, the chosen ribbon sliding out as the other slides back. A section rebuilds only
/// its own page when one of its own controls asks (a key being chosen, a slip, a save written).
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
    private const int SoundAt = 0, DisplayAt = 1, KeysAt = 2, SavingAt = 3;

    public event Action? Closed;
    /// <summary>A page turned to another section (the owner plays the page-turn sound): once per ribbon chosen.</summary>
    public event Action? PageTurned;

    public GameSettings Store { get; }
    public SettingsApplier Applier { get; }
    public ISaveActions? Saves { get; }
    /// <summary>The section chosen (the one on show, or the one a page is turning to).</summary>
    public string Section => Sections[_target];
    /// <summary>Seconds a new window mode or size waits for "Keep" before it goes back.</summary>
    public float KeepSeconds { get; set; } = 10f;
    /// <summary>
    /// Every control of the chosen section and the shared ones, by a stable name ("ribbon.Sound", "sound.Music",
    /// "display.mode", "keys.tt_bell.0", "back", "slip.0", ...).
    /// </summary>
    public IReadOnlyDictionary<string, Control> Controls => _controls;
    /// <summary>The slip (small dialog) on show: "", "keep", "delete" or "clash".</summary>
    public string Slip => _slip;
    public float UiScale => _s;
    /// <summary>The book's cover on screen (canvas units): the owner lines the 3D book up with it.</summary>
    public Rect2 CoverRect { get; private set; }
    /// <summary>
    /// False while the owner has the pages put away or fading out: Esc is not theirs then, and a change to the settings
    /// made elsewhere rebuilds them at once (they are on show again only after such a rebuild).
    /// </summary>
    public bool Live { get; set; } = true;

    /// <summary>Seconds a page takes to turn.</summary>
    public const float TurnSeconds = 0.36f;
    /// <summary>A page turning, 0..1 of the way (tests and frame strips); 1 at rest.</summary>
    public float TurnProgress => _from >= 0 ? _turnT : 1f;
    public bool Turning => _from >= 0;
    /// <summary>The moving edge of the turning leaf (canvas x), the spine, and the share of the pages each section shows (tests).</summary>
    public float LeafEdge { get; private set; }
    public float Spine => _rightPage.Position.X;
    public float SectionShown(int i)
    {
        if (_clip[i] is not { } c || !IsInstanceValid(c) || !c.Visible) return 0f;
        var area = SpreadArea;
        float x0 = Math.Max(area.Position.X, c.Region.Position.X), x1 = Math.Min(area.End.X, c.Region.End.X);
        return Math.Max(0f, x1 - x0) / Math.Max(1f, area.Size.X);
    }
    public bool SectionVisible(int i) => _clip[i] is { } c && IsInstanceValid(c) && c.Visible;
    /// <summary>How far each ribbon is pulled out, 0..1 (tests).</summary>
    public float RibbonOut(int i) => _ribbonOut[i];
    /// <summary>Tests and frame strips: hold a turn at this share of the way (negative lets it run).</summary>
    public float HoldTurnAt = -1f;

    private readonly Dictionary<string, Control> _controls = new(), _shared = new();
    private readonly Dictionary<string, Control>[] _sec = { new(), new(), new(), new() };
    private readonly SectionMask?[] _clip = new SectionMask?[4];
    private readonly Control?[] _inner = new Control?[4];
    private readonly Button?[] _ribbons = new Button?[4];
    private readonly float[] _ribbonOut = new float[4], _ribbonFrom = new float[4];
    private Control? _frame, _slipBlocker, _slipCard;
    private PageLeaf? _leaf;
    private float _s = 1f;
    private string _slip = "";
    private bool _rebuildQueued;
    private AudioStreamPlayer _tick = null!;
    private Label? _countdown;
    private Rect2 _rightPage, _leftPage;
    private Vector2 _canvas;
    private float _colW = 600f;
    private int _building = -1;          // the section whose controls are being made (-1: the shared ones)
    private int _target, _showing;       // the section chosen; the one a turn is bringing in (or on show at rest)
    private int _from = -1, _queued = -1;
    private float _turnT = 1f;
    private bool _fwd, _warm;

    private Dictionary<string, Control> Target => _building >= 0 ? _sec[_building] : _shared;

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

    private void OnStoreChanged(string key) { if (key == GameSettings.KInterface || !Live) QueueRebuild(); }

    /// <summary>Switches to a section (by name): a page turns to it; the focus goes to its ribbon at once.</summary>
    public void ShowSection(string name)
    {
        int i = Array.IndexOf(Sections, name);
        if (i < 0) return;
        CancelCapture();
        if (i == _target) { FocusName("ribbon." + name); return; }
        _target = i;
        bool turn = _frame != null && IsVisibleInTree() && Live && !_warm;
        if (!turn) { _from = -1; _queued = -1; _showing = i; SetRest(); }
        else if (_from < 0) StartTurn(_showing, i);
        else if (i == _from)
        {
            // Back to the section the page is leaving: the same leaf turns back from where it is.
            (_from, _showing) = (_showing, _from);
            _fwd = !_fwd;
            _turnT = 1f - _turnT;
            _queued = -1;
            for (int k = 0; k < 4; k++) _ribbonFrom[k] = k == _from ? 1f : 0f;
        }
        else if (i == _showing) _queued = -1;
        else _queued = i;  // a third section: this turn finishes quickly, then the next one starts
        if (turn) PageTurned?.Invoke();
        SyncView();
        SetRibbonsPressed();
        ApplyState();
        FocusName("ribbon." + name);
    }

    /// <summary>Back on show (the owner's settings page opening again): Sound, as when they were new.</summary>
    public void OpenFresh()
    {
        CancelCapture();
        _slip = "";
        _savingNote = "";
        _target = _showing = SoundAt;
        _from = -1; _queued = -1;
        RefreshSaving();
        SyncView();
        SetRibbonsPressed();
        SetRest();
        FocusName("ribbon." + Section);
    }

    /// <summary>Start-up warm-up: every section drawn once (the owner holds the pages at no opacity), so its text is shaped
    /// and its glyphs are made before a page first turns to it.</summary>
    public void WarmUp(bool on)
    {
        _warm = on;
        if (on) for (int i = 0; i < 4; i++) { if (_clip[i] is { } c) { c.Visible = true; Clip(i, 0, _canvas.X); } }
        else SetRest();
        _leaf?.Place(new Rect2(_leftPage.Position, new Vector2(_rightPage.End.X - _leftPage.Position.X, _rightPage.Size.Y)), Spine, on ? Spine + 40 : Spine, on ? 0.5f : 0f);
    }

    public void Close()
    {
        CancelCapture();
        if (_slip == "keep") RevertDisplay();
        _slip = "";
        if (_slipCard != null) Refresh(_target);
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
        if (!IsVisibleInTree() || !Live) return;
        // While a page turns, the section coming in already takes clicks over the whole spread (SectionMask hides only its
        // drawing), so a click finds its control and the page goes on turning; a click on a ribbon turns it again.
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
        if (_from >= 0) StepTurn((float)delta);
        if (_slip != "keep") return;
        _keepLeft -= (float)delta;
        if (_keepLeft <= 0) { RevertDisplay(); return; }
        if (_countdown != null) _countdown.Text = KeepText();
    }
}

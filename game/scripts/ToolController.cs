using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

public enum Tool { None, Fence, Bridge, Erase, Ferry, Drawbridge }

/// <summary>
/// The three Prototype 1 tools, as the toy's input.ts plays them, on the map page:
/// - Fence: press on the map and drag; a ghost line follows (4-connected cells, as the simulation fences) and the
///   flow preview runs during the drag; release places it.
/// - Footbridge: hovering shows the bridge that would be built (or a cross and the reason in words); press and
///   release over the same crossing builds it, sliding away cancels.
/// - Erase: hovering shows the brush and what it would remove; click or drag removes fences and whole bridges.
/// - Ferry and Drawbridge (river rules; the footbridge goes the same way with them on): hovering the river shows the
///   crossing's cells, its flow preview (with the boats it would hold) and its price, or a cross and the reason in words
///   (no room, too close, not enough coin); press and release over the same crossing builds it.
/// - With no tool in hand, a click on a drawbridge selects it (its badge gets a focus frame); a second click, the
///   badge's lever or L raises or lowers it by hand; Ctrl+1 to 9 works drawbridge 1 to 9 without selecting it; K hires
///   the selected bridge's keeper (see ToolController.River.cs).
/// Escape or a right click (without a drag, which pans) puts the tool down. Tools work while paused.
/// Pointer presses arrive through _UnhandledInput, after the interface controls, so a press on a tab never reaches
/// the map; while the pointer is over a control the ghost and preview are hidden; a drag released over a control is
/// cancelled. Every pick goes through CameraRig.PickLocal, so it holds at any pan, zoom or (later) tilt.
/// </summary>
public partial class ToolController : Node
{
    private Main _main = null!;
    private SimDriver Sim => _main.Sim;
    private World W => _main.Sim.World;

    public Tool Tool { get; private set; } = Tool.None;
    public FlowPreviewer Previewer { get; private set; } = null!;
    public ToolGhost Ghost { get; private set; } = null!;
    public RouteLayer Routes { get; private set; } = null!;

    /// <summary>A status line for the interface (tool hints, results, reasons). Raised on change only.</summary>
    public event Action<string>? Message;
    public event Action? ToolChanged;
    /// <summary>Raised after a successful placement (true) or erase (false), and on a refused placement: for sound.</summary>
    public event Action<bool>? Placed;
    public event Action? Erased;
    public event Action? Refused;

    /// <summary>The pointer on screen, and whether it is over the map rather than an interface control.</summary>
    public Vector2 Pointer { get; private set; }
    public bool PointerOverUi { get; private set; }
    public bool PointerKnown { get; private set; }
    public string LastMessage { get; private set; } = "";
    /// <summary>Frames in which preview work was skipped because the simulation was busy (perf test, stats).</summary>
    public int PreviewSkips { get; private set; }

    private (int x, int y)? _dragStart;           // fence drag
    private (int x, int y) _dragEnd;
    private int _bridgeDownCell = -1;              // footbridge press: first cell of the crossing
    private bool _eraseDown;
    private (int x, int y)? _lastErase;
    private (int x, int y) _hoverCell = (-999, -999);
    private Vector2 _rightDown;

    private readonly List<int> _cells = new(), _ok = new(), _bad = new(), _fenceT = new(), _bridgeT = new(), _line = new();
    private readonly List<int> _bridgeCells = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    public static readonly string[] Hints =
    {
        "",
        "Fence: drag across the map to draw a fence line. Esc or right-click puts it down.",
        "Footbridge: point at the river to see the bridge, click to build it.",
        "Erase: click or drag over fences, bridges and ferries to remove them.",
        "Ferry: point at the river and click to start a ferry. Walkers queue for the raft; boats always pass.",
        "Drawbridge: point at the river and click to build one. Raise it for barges with a click on it or L.",
    };

    public void Init(Main main)
    {
        _main = main;
        Name = "Tools";
        ProcessPriority = 50; // after the driver and the camera, before nothing in particular
        Previewer = new FlowPreviewer(W);
        Ghost = new ToolGhost();
        Ghost.Init((float)main.Params.CellMeters);
        main.MapOrigin.AddChild(Ghost);
        Routes = new RouteLayer();
        Routes.Init((float)main.Params.CellMeters);
        main.MapOrigin.AddChild(Routes);
        Sim.WorldReplaced += OnWorldReplaced;
        Sim.Edited += OnEdited;
    }

    public override void _ExitTree()
    {
        if (_main?.Sim == null) return;
        Sim.WorldReplaced -= OnWorldReplaced;
        Sim.Edited -= OnEdited;
    }

    private void OnWorldReplaced()
    {
        Previewer.SetWorld(W);
        CheckSelection();
        CancelGesture();
        Refresh();
    }

    // After any edit the ghost's verdicts (can fence, bridge fits) may have changed.
    private void OnEdited(int v)
    {
        CheckSelection();
        if (_dragStart == null && !_eraseDown) Refresh();
    }

    public void SetTool(Tool t)
    {
        CancelGesture();
        Tool = t;
        // Deferred: changing the cursor makes Input inject a motion event, which would cut short the event being
        // handled now (a right-click release would then never reach the camera, leaving it panning).
        CallDeferred(MethodName.UpdateCursor);
        Say(Hints[(int)t]);
        ToolChanged?.Invoke();
        Refresh();
    }

    private void UpdateCursor() => Input.SetDefaultCursorShape(Tool == Tool.None ? Input.CursorShape.Arrow : Input.CursorShape.Cross);

    /// <summary>Selecting the tool in hand again puts it down (tabs and keys toggle).</summary>
    public void ToggleTool(Tool t) => SetTool(Tool == t ? Tool.None : t);

    public void Say(string msg)
    {
        if (msg == LastMessage) return;
        LastMessage = msg;
        Message?.Invoke(msg);
    }

    public bool PreviewEnabled
    {
        get => Previewer.Enabled;
        set { Previewer.Enabled = value; Refresh(); }
    }

    // ------------------------------------------------------------------ input

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseMotion mm:
                Move(mm.Position);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb:
                Move(mb.Position);
                if (Tool != Tool.None && Down(mb.Position)) GetViewport().SetInputAsHandled();
                else if (Tool == Tool.None && ClickBridge(mb.Position)) GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } rb:
                if (rb.Pressed) _rightDown = rb.Position;
                else if (Tool != Tool.None && rb.Position.DistanceTo(_rightDown) < 6f) SetTool(Tool.None);
                break;
        }
    }

    // Releases come here first (before the interface): a gesture that started on the map must end even if the
    // pointer is now over a tab, and then it is cancelled rather than placed.
    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mb && Gesturing)
        {
            Pointer = mb.Position;
            PointerOverUi = OverUi();
            Up(mb.Position);
            GetViewport().SetInputAsHandled();
        }
    }

    public bool Gesturing => _dragStart != null || _bridgeDownCell >= 0 || _eraseDown;

    private bool OverUi() => GetViewport().GuiGetHoveredControl() != null;

    private void Move(Vector2 pos)
    {
        Pointer = pos;
        PointerKnown = true;
        PointerOverUi = OverUi();
        if (Tool == Tool.Fence && _dragStart != null)
        {
            var c = ClampedCell(pos);
            if (c != _dragEnd) { _dragEnd = c; FenceGhost(); }
        }
        else if (Tool == Tool.Erase && _eraseDown) EraseAt(pos);
        else Hover(pos);
    }

    private bool Down(Vector2 pos)
    {
        if (!_main.Rig.PickCell(pos, out int cx, out int cy)) return false;
        switch (Tool)
        {
            case Tool.Fence:
                _dragStart = (cx, cy);
                _dragEnd = (cx, cy);
                FenceGhost();
                break;
            case Tool.Bridge when !RiverOn:
                Hover(pos, force: true);
                _bridgeDownCell = _bridgeCells.Count > 0 ? _bridgeCells[0] : -1;
                if (_bridgeDownCell < 0) { Say(BridgeReason(cx, cy)); Refused?.Invoke(); }
                break;
            case Tool.Bridge or Tool.Ferry or Tool.Drawbridge:
                Hover(pos, force: true);
                _bridgeDownCell = _bridgeCells.Count > 0 ? _bridgeCells[0] : -1;
                if (_bridgeDownCell < 0) { Say(_crossingWhy); Refused?.Invoke(); }
                break;
            case Tool.Erase:
                _eraseDown = true;
                _lastErase = null;
                EraseAt(pos);
                break;
        }
        return true;
    }

    private void Up(Vector2 pos)
    {
        if (Tool == Tool.Fence && _dragStart != null)
        {
            int n = 0;
            if (PointerOverUi) Say("Fence cancelled.");
            else
            {
                FenceCells(_dragEnd);
                n = Sim.PlaceFence(CollectionsMarshal.AsSpan(_cells));
                Say(n > 0 ? $"Fence placed ({n} cells). Watch the walkers find a way round." : "Nothing to fence there.");
                if (n > 0) Placed?.Invoke(true); else Refused?.Invoke();
            }
            _dragStart = null;
        }
        else if (RiverOn && CrossingTool(Tool) is { } ct && _bridgeDownCell >= 0) CrossingUp(ct, pos);
        else if (Tool == Tool.Bridge && _bridgeDownCell >= 0)
        {
            int down = _bridgeDownCell;
            _bridgeDownCell = -1;
            Vector3 l = default;
            bool same = !PointerOverUi && _main.Rig.PickLocal(pos, out l)
                        && W.BridgeCellsAt(l.X / CellM, l.Z / CellM, _bridgeCells) && _bridgeCells[0] == down;
            if (same)
            {
                var b = Sim.PlaceBridge((int)(l.X / CellM), (int)(l.Z / CellM));
                Say(b != null ? $"Footbridge built ({b.Cells.Count} cells)." : "No room for a footbridge there.");
                if (b != null) Placed?.Invoke(true); else Refused?.Invoke();
            }
            else Say("Footbridge cancelled.");
        }
        _eraseDown = false;
        _lastErase = null;
        Previewer.Clear();
        _hoverCell = (-999, -999);
        Hover(pos);
    }

    private void CancelGesture()
    {
        _dragStart = null;
        _bridgeDownCell = -1;
        _eraseDown = false;
        _lastErase = null;
        _hoverCell = (-999, -999);
        Previewer.Clear();
        Ghost.Clear();
    }

    /// <summary>Re-evaluates the ghost at the last pointer position (after a tool change or an edit).</summary>
    private void Refresh()
    {
        _hoverCell = (-999, -999);
        if (PointerKnown) Hover(Pointer); else { Ghost.Clear(); Previewer.Clear(); }
    }

    private float CellM => (float)_main.Params.CellMeters;

    private (int, int) ClampedCell(Vector2 pos)
    {
        int n = W.Grid.Width;
        if (!_main.Rig.PickLocal(pos, out var l)) return _dragEnd;
        return (Math.Clamp((int)MathF.Floor(l.X / CellM), 0, n - 1), Math.Clamp((int)MathF.Floor(l.Z / CellM), 0, n - 1));
    }

    /// <summary>Why no footbridge fits at a cell, in words (see <see cref="ToolTargets.BridgeReason"/>).</summary>
    public string BridgeReason(int cx, int cy) => ToolTargets.BridgeReason(W, cx, cy);

    // ------------------------------------------------------------------ per frame

    public override void _Process(double delta)
    {
        // The pointer can come to rest over a control without a new motion event reaching the map.
        if (PointerKnown && !Gesturing)
        {
            bool over = OverUi();
            if (over != PointerOverUi) { PointerOverUi = over; Refresh(); }
        }
        // A frame in which the simulation already took more than SkipAboveSimMs (several ticks at 8x) does no preview
        // work at all, so the two never stack into one long frame; the preview simply finishes a frame or two later.
        // Otherwise the preview gets what the simulation left of the toy's 6 ms (at least 2 ms, so it always moves on).
        double sim = Sim.SimMsThisFrame;
        if (sim > FlowPreviewer.SkipAboveSimMs) PreviewSkips++;
        else Previewer.Update(Clock.Elapsed.TotalMilliseconds, Math.Max(2.0, FlowPreviewer.BudgetMs - sim));
        var vp = GetViewport().GetVisibleRect().Size;
        // Route widths are in pixels at 800 px of window height and grow with taller windows (as the interface does).
        float px = Math.Max(1f, vp.Y / 800f);
        Routes.Show(Previewer.Enabled ? Previewer.Current : null, _main.Rig.Camera.Size / Math.Max(1f, vp.Y) * px, Clock.Elapsed.TotalSeconds, delta);
    }
}

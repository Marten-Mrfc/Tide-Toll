using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

public enum Tool { None, Fence, Bridge, Erase }

/// <summary>
/// The three Prototype 1 tools, as the toy's input.ts plays them, on the map page:
/// - Fence: press on the map and drag; a ghost line follows (4-connected cells, as the simulation fences) and the
///   flow preview runs during the drag; release places it.
/// - Footbridge: hovering shows the bridge that would be built (or a cross and the reason in words); press and
///   release over the same crossing builds it, sliding away cancels.
/// - Erase: hovering shows the brush and what it would remove; click or drag removes fences and whole bridges.
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
        "Erase: click or drag over fences and bridges to remove them.",
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
        CancelGesture();
        Refresh();
    }

    // After any edit the ghost's verdicts (can fence, bridge fits) may have changed.
    private void OnEdited(int v) { if (_dragStart == null && !_eraseDown) Refresh(); }

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
            case Tool.Bridge:
                Hover(pos, force: true);
                _bridgeDownCell = _bridgeCells.Count > 0 ? _bridgeCells[0] : -1;
                if (_bridgeDownCell < 0) { Say(BridgeReason(cx, cy)); Refused?.Invoke(); }
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

    // ------------------------------------------------------------------ ghosts and previews

    private void FenceCells((int x, int y) end)
    {
        _cells.Clear();
        W.Grid.Line4(_dragStart!.Value.x, _dragStart.Value.y, end.x, end.y, _cells);
    }

    private void FenceGhost()
    {
        FenceCells(_dragEnd);
        int n = W.Grid.Width;
        var fence = W.Fence;
        _ok.Clear(); _bad.Clear();
        Ghost.Begin();
        foreach (int c in _cells)
        {
            bool ok = W.CanFence(c) && fence[c] == 0;
            if (ok) _ok.Add(c); else _bad.Add(c);
            Ghost.Cell(c % n, c / n, ok ? ToolGhost.Ok : ToolGhost.Bad, ok ? ToolGhost.OkColour : ToolGhost.BadColour);
        }
        Ghost.End();
        if (_ok.Count > 0) Previewer.Request(new WhatIf { Block = _ok.ToArray() }, FlowPreviewer.Key('f', CollectionsMarshal.AsSpan(_ok)));
        else Previewer.Clear();
        Say(_bad.Count > 0 && _ok.Count > 0 ? "Fence: hatched cells are water, doors or buildings and stay open."
            : _ok.Count == 0 ? "Fences can't stand on water, doors or buildings." : Hints[1]);
    }

    private void Hover(Vector2 pos, bool force = false)
    {
        bool over = PointerOverUi;
        if (Tool == Tool.None || over || !_main.Rig.PickLocal(pos, out var l))
        {
            Ghost.Clear();
            Previewer.Clear();
            _hoverCell = (-999, -999);
            return;
        }
        float x = l.X / CellM, y = l.Z / CellM;
        int cx = (int)MathF.Floor(x), cy = (int)MathF.Floor(y), n = W.Grid.Width;
        bool onMap = cx >= 0 && cy >= 0 && cx < n && cy < n;
        if (!force && (cx, cy) == _hoverCell) return;
        _hoverCell = (cx, cy);
        Ghost.Begin();
        if (!onMap) { Ghost.End(); Previewer.Clear(); return; }
        switch (Tool)
        {
            case Tool.Fence:
                bool ok = W.CanFence(cy * n + cx) && W.Fence[cy * n + cx] == 0;
                Ghost.Cell(cx, cy, ok ? ToolGhost.Ok : ToolGhost.Bad, ok ? ToolGhost.OkColour : ToolGhost.BadColour);
                Previewer.Clear();
                break;
            case Tool.Bridge:
                if (W.BridgeCellsAt(x, y, _bridgeCells))
                {
                    foreach (int c in _bridgeCells) Ghost.Cell(c % n, c / n, ToolGhost.Ok, ToolGhost.OkColour);
                    Previewer.Request(new WhatIf { Bridge = _bridgeCells.ToArray() }, FlowPreviewer.Key('b', CollectionsMarshal.AsSpan(_bridgeCells)));
                    Say(Hints[2]);
                }
                else
                {
                    Ghost.Cell(cx, cy, ToolGhost.Bad, ToolGhost.BadColour, 2f);
                    Previewer.Clear();
                    Say(BridgeReason(cx, cy));
                }
                break;
            case Tool.Erase:
                EraseTargets(cx, cy);
                Ghost.Ring(x, y, 3.2f);
                foreach (int c in _fenceT) Ghost.Cell(c % n, c / n, ToolGhost.Remove, ToolGhost.BadColour);
                foreach (int c in _bridgeT) Ghost.Cell(c % n, c / n, ToolGhost.Remove, ToolGhost.BadColour);
                if (_fenceT.Count + _bridgeT.Count > 0)
                    Previewer.Request(new WhatIf { Open = _fenceT.ToArray(), CloseBridge = _bridgeT.ToArray() },
                        FlowPreviewer.Key('e', CollectionsMarshal.AsSpan(_fenceT), CollectionsMarshal.AsSpan(_bridgeT)));
                else Previewer.Clear();
                break;
        }
        Ghost.End();
    }

    /// <summary>Why no footbridge fits at a cell, in words (mirrors World.BridgeCellsAt's checks; read-only).</summary>
    public string BridgeReason(int cx, int cy)
    {
        var w = W;
        int n = w.Grid.Width;
        if (cy < 0 || cy >= n) return "Point at the river to lay a footbridge.";
        int rx = (int)MathF.Floor(w.RiverX[cy]);
        if (Math.Abs(cx - rx) > w.Map.RiverHalfW + 4) return "A footbridge goes across the river: point at the water.";
        if (Math.Abs(cy - w.Map.FordY) <= 2) return "Too close to the ford: villagers already cross here.";
        for (int x = Math.Max(0, rx - 8); x <= Math.Min(n - 1, rx + 8); x++)
            if (w.BridgeAt[cy * n + x] >= 0) return "There is already a footbridge here.";
        return "No footbridge fits here: both ends need open ground on the bank.";
    }

    /// <summary>Fence cells in the 3 x 3 round a cell, and every cell of any bridge touched (the toy's brush).</summary>
    private void EraseTargets(int x, int y)
    {
        var w = W;
        int n = w.Grid.Width;
        _fenceT.Clear(); _bridgeT.Clear();
        var fence = w.Fence;
        var bridgeAt = w.BridgeAt;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue;
                int c = yy * n + xx;
                if (fence[c] != 0) _fenceT.Add(c);
                int b = bridgeAt[c];
                if (b < 0 || _bridgeT.Contains(c)) continue;
                foreach (var br in w.Bridges)
                    if (br.Id == b)
                        foreach (int bc in br.Cells) if (!_bridgeT.Contains(bc)) _bridgeT.Add(bc);
            }
    }

    private void EraseAt(Vector2 pos)
    {
        if (PointerOverUi || !_main.Rig.PickCell(pos, out int cx, out int cy)) { Hover(pos); return; }
        int n = W.Grid.Width;
        // Along the drag, so a fast swipe leaves no gaps.
        _line.Clear();
        if (_lastErase is { } le) W.Grid.Line4(le.x, le.y, cx, cy, _line); else _line.Add(cy * n + cx);
        _lastErase = (cx, cy);
        _cells.Clear();
        foreach (int p in _line)
        {
            EraseTargets(p % n, p / n);
            _cells.AddRange(_fenceT);
            _cells.AddRange(_bridgeT);
        }
        if (_cells.Count > 0 && Sim.Erase(CollectionsMarshal.AsSpan(_cells)) > 0) { Say("Removed."); Erased?.Invoke(); }
        Hover(pos, force: true);
    }

    // ------------------------------------------------------------------ per frame

    public override void _Process(double delta)
    {
        // The pointer can come to rest over a control without a new motion event reaching the map.
        if (PointerKnown && !Gesturing)
        {
            bool over = OverUi();
            if (over != PointerOverUi) { PointerOverUi = over; Refresh(); }
        }
        double budget = Sim.SimMsThisFrame > Sim.TickBudgetMs * 0.8 ? FlowPreviewer.BusyBudgetMs : FlowPreviewer.BudgetMs;
        Previewer.Update(Clock.Elapsed.TotalMilliseconds, budget);
        var vp = GetViewport().GetVisibleRect().Size;
        Routes.Show(Previewer.Enabled ? Previewer.Current : null, _main.Rig.Camera.Size / Math.Max(1f, vp.Y), Clock.Elapsed.TotalSeconds, delta);
    }
}

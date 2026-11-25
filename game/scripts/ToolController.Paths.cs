using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The finished toy's two newer path tools (input.ts), in both chapters and free in both:
/// - Gravel (G): press and drag to brush a strip; every cell under the pointer snaps to a worn trail's centreline within
///   one cell (World.GravelSnap) and gaps are filled with a 4-connected line; the ghost and the flow preview follow the
///   stroke; release lays it ("Gravel laid (N cells).").
/// - Gate (T): hovering finds the nearest fence cell within 2 cells that takes a gate at any angle (a stair corner takes
///   a two-cell gate) or an existing gate (taken out), with the toy's reasons in words when none fits; press and release
///   over the same gate places or removes it, sliding away cancels.
/// </summary>
public partial class ToolController
{
    private bool _brushing;
    private int _lastBrush = -1;
    private readonly List<int> _brush = new(), _brushLine = new(), _gravelT = new();
    private int _gateDown = -1, _gateAt = -1;
    private int[] _gateCells = Array.Empty<int>();
    private bool _gateRemove;
    private string _gateReason = "Gates go on fences.";
    private string? _afterUp; // said after the release's re-hover, so the result is the last word

    /// <summary>The gravel stroke so far (tests).</summary>
    public int BrushCells => _brush.Count;

    private static string Plural(int n, string one) => n == 1 ? $"1 {one}" : $"{n} {one}s";

    private bool PathsDown(Vector2 pos, int cx, int cy)
    {
        switch (Tool)
        {
            case Tool.Gravel:
                _brushing = true;
                _brush.Clear();
                _lastBrush = -1;
                BrushTo(cx, cy);
                return true;
            case Tool.Gate:
                Hover(pos, force: true);
                _gateDown = _gateAt;
                if (_gateDown < 0) { Say(_gateReason); Refused?.Invoke(); }
                return true;
        }
        return false;
    }

    private bool PathsUp(Vector2 pos)
    {
        if (Tool == Tool.Gravel && _brushing)
        {
            _brushing = false;
            if (PointerOverUi) _afterUp = "Gravel cancelled.";
            else
            {
                int n = Sim.PlaceGravel(CollectionsMarshal.AsSpan(_brush));
                _afterUp = n > 0 ? $"Gravel laid ({Plural(n, "cell")})." : "Nothing to gravel there.";
                if (n > 0) Placed?.Invoke(true); else Refused?.Invoke();
            }
            _brush.Clear();
            _lastBrush = -1;
            return true;
        }
        if (Tool == Tool.Gate && _gateDown >= 0)
        {
            int before = _gateDown;
            _gateDown = -1;
            _hoverCell = (-999, -999);
            if (!PointerOverUi) Hover(pos, force: true);
            if (!PointerOverUi && _gateAt == before)
            {
                if (_gateRemove)
                {
                    bool ok = Sim.RemoveGate(before);
                    if (ok) { _afterUp = "Gate removed."; Erased?.Invoke(); }
                }
                else
                {
                    bool ok = Sim.PlaceGate(before);
                    _afterUp = ok ? "Gate placed." : _gateReason;
                    if (ok) Placed?.Invoke(true); else Refused?.Invoke();
                }
            }
            else _afterUp = "Gate cancelled.";
            return true;
        }
        return false;
    }

    /// <summary>The release's result, said after the hover that follows it.</summary>
    private void SayAfterUp()
    {
        if (_afterUp == null) return;
        Say(_afterUp);
        _afterUp = null;
    }

    private void CancelPaths()
    {
        _brushing = false;
        _brush.Clear();
        _lastBrush = -1;
        _gateDown = -1;
    }

    /// <summary>Extends the stroke to a cell (snapped to a trail's centreline), filling gaps with a 4-connected line.</summary>
    private void BrushTo(int cx, int cy)
    {
        int n = W.Grid.Width;
        int s = W.GravelSnap(cy * n + cx);
        if (s == _lastBrush) return;
        _brushLine.Clear();
        if (_lastBrush < 0) _brushLine.Add(s);
        else W.Grid.Line4(_lastBrush % n, _lastBrush / n, s % n, s / n, _brushLine);
        _lastBrush = s;
        var gravel = W.Gravel;
        foreach (int v in _brushLine)
            if (W.CanGravel(v) && gravel[v] == 0 && !_brush.Contains(v)) _brush.Add(v);
        Ghost.Begin();
        foreach (int c in _brush) Ghost.Cell(c % n, c / n, ToolGhost.Ok, ToolGhost.OkColour);
        Ghost.End();
        if (_brush.Count > 0) Previewer.Request(new WhatIf { Gravel = _brush.ToArray() }, FlowPreviewer.Key('g', CollectionsMarshal.AsSpan(_brush)));
        else Previewer.Clear();
    }

    /// <summary>The hover ghost of the gravel and gate tools (inside Ghost.Begin/End).</summary>
    private void PathsHover(float x, float y, int cx, int cy)
    {
        int n = W.Grid.Width;
        if (Tool == Tool.Gravel)
        {
            int c = W.GravelSnap(cy * n + cx);
            bool ok = W.CanGravel(c) && W.Gravel[c] == 0;
            Ghost.Cell(c % n, c / n, ok ? ToolGhost.Ok : ToolGhost.Bad, ok ? ToolGhost.OkColour : ToolGhost.BadColour);
            Previewer.Clear();
            return;
        }
        // Gate: the nearest fence cell within two that takes one, an existing gate to take out, or why not.
        var t = W.GateTarget(x, y);
        _gateCells = t.Cells;
        _gateRemove = t.Remove;
        _gateAt = t.Cells.Length > 0 ? t.Cells[0] : -1;
        _gateReason = t.Reason is { Length: > 0 } r ? r : "Gates go on fences.";
        if (_gateAt < 0)
        {
            Ghost.Cell(cx, cy, ToolGhost.Bad, ToolGhost.BadColour);
            Previewer.Clear();
            if (_gateDown < 0) Say(_gateReason);
            return;
        }
        foreach (int c in t.Cells) Ghost.Cell(c % n, c / n, t.Remove ? ToolGhost.Remove : ToolGhost.Ok, t.Remove ? ToolGhost.BadColour : ToolGhost.OkColour, 1.3f);
        if (t.Remove)
        {
            Previewer.Request(new WhatIf { Ungate = t.Cells }, FlowPreviewer.Key('u', t.Cells));
            Say("Gate: click to take this gate out.");
        }
        else
        {
            Previewer.Request(new WhatIf { Gate = t.Cells }, FlowPreviewer.Key('t', t.Cells));
            Say(t.Cells.Length == 2 ? "Gate: a corner of the fence's staircase takes a gate of two cells. Click to put it in." : Hints[(int)Tool.Gate]);
        }
    }

    /// <summary>The cells a gate press would change now (tests).</summary>
    public ReadOnlySpan<int> GateCells => _gateCells;
}

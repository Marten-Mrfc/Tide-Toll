using System;
using System.Runtime.InteropServices;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The tools' ghosts and flow-preview requests (the second half of <see cref="ToolController"/>): the fence line
/// under a drag, the hover ghost of each tool, and the eraser's swipe. What a tool would touch is asked of
/// <see cref="ToolTargets"/>, which only reads the world.
/// </summary>
public partial class ToolController
{
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

    /// <summary>The hover ghost and hint; hints said here wait while a build's success holds the status line.</summary>
    private void Hover(Vector2 pos, bool force = false)
    {
        _hinting = true;
        try { HoverGhost(pos, force); }
        finally { _hinting = false; }
    }

    private void HoverGhost(Vector2 pos, bool force)
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
            case Tool.Bridge or Tool.Ferry or Tool.Drawbridge when RiverOn:
                CrossingHover(CrossingTool(Tool)!.Value, x, y, cx, cy);
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
                    Say(ToolTargets.BridgeReason(W, cx, cy));
                }
                break;
            case Tool.Gravel or Tool.Gate:
                PathsHover(x, y, cx, cy);
                break;
            case Tool.Erase:
                ToolTargets.EraseTargets(W, cx, cy, _fenceT, _bridgeT, _gravelT);
                Ghost.Ring(x, y, 3.2f);
                foreach (int c in _fenceT) Ghost.Cell(c % n, c / n, ToolGhost.Remove, ToolGhost.BadColour);
                foreach (int c in _bridgeT) Ghost.Cell(c % n, c / n, ToolGhost.Remove, ToolGhost.BadColour);
                foreach (int c in _gravelT) Ghost.Cell(c % n, c / n, ToolGhost.Remove, ToolGhost.BadColour);
                if (_bridgeT.Count > 0 && RiverOn) Say(ToolTargets.EraseCost(W, _bridgeT));
                else if (_fenceT.Count + _gravelT.Count > 0) Say(Hints[3]);
                if (_fenceT.Count + _bridgeT.Count + _gravelT.Count > 0)
                    Previewer.Request(new WhatIf { Open = _fenceT.ToArray(), CloseBridge = _bridgeT.ToArray(), Ungravel = _gravelT.ToArray() },
                        FlowPreviewer.Key('e', CollectionsMarshal.AsSpan(_fenceT), CollectionsMarshal.AsSpan(_bridgeT)) ^ FlowPreviewer.Key('v', CollectionsMarshal.AsSpan(_gravelT)));
                else Previewer.Clear();
                break;
        }
        Ghost.End();
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
            ToolTargets.EraseTargets(W, p % n, p / n, _fenceT, _bridgeT, _gravelT);
            _cells.AddRange(_fenceT);
            _cells.AddRange(_bridgeT);
            _cells.AddRange(_gravelT);
        }
        if (_cells.Count > 0 && Sim.Erase(CollectionsMarshal.AsSpan(_cells)) > 0) { Say("Removed."); Erased?.Invoke(); }
        Hover(pos, force: true);
    }
}

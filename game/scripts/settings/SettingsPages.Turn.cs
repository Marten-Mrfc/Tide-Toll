using System;
using Godot;

namespace TideToll.Game.Settings;

/// <summary>
/// A page turning between two sections: the leaf's edge sweeps across the spread (right to left for a later section,
/// left to right for an earlier one) on one eased curve; the section being left keeps the part of the spread the edge
/// has not reached, the new one shows behind it, so no frame is blank and neither is cut in. A turn chosen back
/// mid-way turns back from where it is; a third section waits for this turn (which then runs a little faster).
/// The chosen section takes clicks over the whole spread and the focus from the moment it is chosen (a mask only hides
/// its drawing); the others take neither.
/// </summary>
public partial class SettingsPages
{
    /// <summary>How much faster a turn runs while another section waits for it.</summary>
    public const float QueuedPace = 1.3f;
    /// <summary>Turns started so far (tests: each turn's leaf is a leaf of its own).</summary>
    public int Turns { get; private set; }

    private void StartTurn(int from, int to)
    {
        _from = from;
        _showing = to;
        _fwd = to > from;
        _turnT = 0f;
        Turns++;
        for (int k = 0; k < 4; k++) _ribbonFrom[k] = _ribbonOut[k];
        ApplyState();
    }

    private void StepTurn(float dt)
    {
        if (HoldTurnAt >= 0f) _turnT = Math.Clamp(HoldTurnAt, 0f, 1f);
        else _turnT += Math.Min(dt, BookMotion.MaxStep) * (_queued >= 0 ? QueuedPace : 1f) / TurnSeconds;
        if (_turnT < 1f || HoldTurnAt >= 0f) { ApplyState(); return; }
        int landed = _showing;
        _from = -1;
        if (_queued >= 0 && _queued != landed)
        {
            int next = _queued;
            _queued = -1;
            StartTurn(landed, next);
            return;
        }
        _queued = -1;
        SetRest();
    }

    /// <summary>The page lands at once on the section chosen (tests).</summary>
    public void FinishTurn()
    {
        if (_from < 0) return;
        _from = -1;
        _queued = -1;
        _showing = _target;
        SetRest();
    }

    /// <summary>Shows one section over [x0, x1] of the canvas only (its pages stay where they are).</summary>
    private void Clip(int i, float x0, float x1)
    {
        var c = _clip[i];
        if (c == null) return;
        if (!c.Visible) c.Visible = true;
        c.SetRegion(new Rect2(x0, 0, Math.Max(0f, x1 - x0), _canvas.Y));
    }

    private void Live_(int i, bool on)
    {
        var c = _clip[i];
        if (c == null) return;
        var f = on ? FocusBehaviorRecursiveEnum.Inherited : FocusBehaviorRecursiveEnum.Disabled;
        var m = on ? MouseBehaviorRecursiveEnum.Inherited : MouseBehaviorRecursiveEnum.Disabled;
        if (c.FocusBehaviorRecursive != f) c.FocusBehaviorRecursive = f;
        if (c.MouseBehaviorRecursive != m) c.MouseBehaviorRecursive = m;
    }

    private float LeftEdge => _leftPage.Position.X;
    private float RightEdge => _rightPage.End.X;
    /// <summary>The two pages on screen together (canvas units): the owner lines the 3D pages up with them.</summary>
    public Rect2 PagesRect => SpreadArea;
    private Rect2 SpreadArea => new(new Vector2(_leftPage.Position.X, _leftPage.Position.Y),
        new Vector2(_rightPage.End.X - _leftPage.Position.X, _rightPage.Size.Y));

    /// <summary>Every section, ribbon and the leaf for the turn's place now (or at rest).</summary>
    private void ApplyState()
    {
        if (_frame == null) return;
        if (_from < 0) { SetRest(); return; }
        float e = BookMotion.Smoother(_turnT);
        float l = LeftEdge, r = RightEdge, spine = Spine, w = _canvas.X;
        float x = _fwd ? r - (r - l) * e : l + (r - l) * e;
        LeafEdge = x;
        for (int i = 0; i < 4; i++)
        {
            if (i == _from) { if (_fwd) Clip(i, 0, x); else Clip(i, x, w); }
            else if (i == _showing) { if (_fwd) Clip(i, x, w); else Clip(i, 0, x); }
            // A section chosen while another turn finishes (it waits its turn): drawn nowhere yet, but it already takes
            // the clicks and the focus, as the chosen section does.
            else if (i == _target) Clip(i, 0, 0);
            else if (_clip[i] is { } c && c.Visible) c.Visible = false;
            Live_(i, i == _target);
        }
        float half = x >= spine ? r - spine : spine - l;
        float lift = 1f - Math.Abs(x - spine) / Math.Max(1f, half);
        _leaf?.Place(SpreadArea, spine, x, Math.Clamp(lift, 0f, 1f));
        for (int k = 0; k < 4; k++) SetRibbonOut(k, BookMotion.Mix(_ribbonFrom[k], k == _showing ? 1f : 0f, e));
    }

    private void SetRest()
    {
        if (_frame == null) return;
        for (int i = 0; i < 4; i++)
        {
            if (_clip[i] is not { } c) continue;
            if (i == _showing) Clip(i, 0, _canvas.X);
            else if (c.Visible) c.Visible = false;
            Live_(i, i == _showing);
        }
        // The section on show is the first of the four in the tree, so a search by name finds its pages.
        if (_clip[_showing] is { } shown)
        {
            int first = int.MaxValue;
            foreach (var c in _clip) if (c != null) first = Math.Min(first, c.GetIndex());
            if (shown.GetIndex() != first) _frame.MoveChild(shown, first);
        }
        LeafEdge = _fwd ? LeftEdge : RightEdge;
        _leaf?.Place(SpreadArea, Spine, Spine, 0f);
        for (int k = 0; k < 4; k++) SetRibbonOut(k, k == _showing ? 1f : 0f);
    }

    private void SetRibbonOut(int i, float v)
    {
        _ribbonOut[i] = v;
        if (_ribbons[i] is not { } b || !IsInstanceValid(b)) return;
        float wdt = RibbonWidth(i);
        if (Math.Abs(b.Size.X - wdt) > 0.01f) b.Size = new Vector2(wdt, b.Size.Y);
    }
}

/// <summary>
/// A section's mask: only the part of its pages inside <see cref="Region"/> is drawn (its children are clipped to what
/// it draws, ClipChildren). Unlike ClipContents it leaves input alone, so a control under the not yet revealed part
/// can still be clicked as the page lands.
/// </summary>
public partial class SectionMask : Control
{
    public Rect2 Region { get; private set; }

    public SectionMask()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipChildren = ClipChildrenMode.Only;
    }

    public void SetRegion(Rect2 r)
    {
        if (r == Region) return;
        Region = r;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Region.Size.X > 0f && Region.Size.Y > 0f) DrawRect(Region, Colors.White);
    }
}

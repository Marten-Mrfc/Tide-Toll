using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Round 6: the map's words (stake labels "What" on plots of both ages, boat labels "Wants") keep clear of the
/// interface. Once a frame each label's rectangle on screen is found; a label that touches a card (ledger, request,
/// drive, notes, shop, bell, banner, achievements, pause, choice, tide, slim strip) or a label already kept is hidden,
/// and every label is hidden while a page (achievements, age page) is open, so none shows through it. Labels are only
/// hidden (their own Visible, which no owner sets: the owners show and hide the parent), never moved, so the code that owns them is untouched.
/// </summary>
public partial class TownHud
{
    private readonly List<(Label3D label, bool boat)> _mapLabels = new();
    private Control?[] _labelCards = Array.Empty<Control?>();
    private Node?[] _labelOwners = Array.Empty<Node?>();
    private readonly List<Rect2> _keptRects = new();
    private readonly List<Rect2> _cardRects = new();
    private int _labelScan;
    /// <summary>Map labels faded out this frame because a card or another label is there (tests).</summary>
    public int LabelsHidden { get; private set; }
    /// <summary>Map labels shown this frame, and their rectangles on screen (tests: none overlap a card or each other).</summary>
    public IReadOnlyList<Rect2> LabelsShownRects => _keptRects;

    private void LabelsFrame(float dt)
    {
        if (--_labelScan <= 0)
        {
            // Labels come and go with plots and boats: look again twice a second (no search per frame).
            _labelScan = 30;
            _mapLabels.Clear();
            // Taken again until all three exist (the river's layer comes later; a cached null hid none of its labels).
            if (_labelOwners.Length == 0 || Array.IndexOf(_labelOwners, null) >= 0) _labelOwners = new Node?[] { _main.Boats, _main.Buildings, _main.River };
            foreach (Node? n in _labelOwners)
                if (n != null && IsInstanceValid(n))
                    foreach (var c in n.FindChildren("W*", "Label3D", true, false))
                        if (c is Label3D l) { string nm = l.Name; if (nm == "What" || nm == "Wants") _mapLabels.Add((l, nm == "Wants")); }
        }
        _cardRects.Clear();
        if (_labelCards.Length == 0) _labelCards = new Control?[] { _ledger, _req, _drive, _shop, _bell, _nextAge, _banner, _achMark, PauseMark, _choice, _tide, _slim, _paveBanner, _home, _hoverTip, _suggest };
        foreach (var c in _labelCards)
            if (c != null && IsInstanceValid(c) && c.IsVisibleInTree() && c.Modulate.A > 0.05f) _cardRects.Add(c.GetGlobalRect().Grow(UiKit.Px(_s, 6)));
        for (int i = 0; i < _noteList.Count; i++)
            if (IsInstanceValid(_noteList[i].label) && _noteList[i].label.GetParent() is Control slip && slip.IsVisibleInTree()) _cardRects.Add(slip.GetGlobalRect().Grow(UiKit.Px(_s, 6)));
        bool page = AchievementsOpen || AgePageOpen;
        _keptRects.Clear();
        LabelsHidden = 0;
        var cam = _main.Rig.Camera;
        // Boats' labels first (they say what a boat wants now), then the stakes.
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < _mapLabels.Count; i++)
            {
                var (l, boat) = _mapLabels[i];
                if (!IsInstanceValid(l) || !l.IsInsideTree()) continue;
                if (boat != (pass == 0)) continue;
                bool hide = page;
                Rect2 r = default;
                if (!hide && l.GetParent() is Node3D owner && owner.IsVisibleInTree())
                {
                    r = ScreenRect(l, cam);
                    if (r.Size.X <= 0) continue;
                    foreach (var c in _cardRects) if (c.Intersects(r)) { hide = true; break; }
                    if (!hide) foreach (var k in _keptRects) if (k.Intersects(r)) { hide = true; break; }
                }
                else if (!hide) continue;
                // Shown or hidden outright (GeometryInstance3D.Transparency does not reach a Label3D's own material).
                if (l.Visible == hide) l.Visible = !hide;
                if (hide) LabelsHidden++;
                else _keptRects.Add(r);
            }
    }

    /// <summary>A Label3D's rectangle on screen (canvas units), from its text box at its place, facing the camera.</summary>
    private static Rect2 ScreenRect(Label3D l, Camera3D cam)
    {
        if (cam.IsPositionBehind(l.GlobalPosition)) return default;
        var box = l.GetAabb();
        var sc = l.GlobalTransform.Basis.Scale;
        var centre = l.GlobalTransform * box.GetCenter();
        var right = cam.GlobalBasis.X * box.Size.X * sc.X * 0.5f;
        var up = cam.GlobalBasis.Y * box.Size.Y * sc.Y * 0.5f;
        var r = new Rect2(cam.UnprojectPosition(centre), Vector2.Zero);
        r = r.Expand(cam.UnprojectPosition(centre - right - up));
        r = r.Expand(cam.UnprojectPosition(centre + right + up));
        r = r.Expand(cam.UnprojectPosition(centre + right - up));
        r = r.Expand(cam.UnprojectPosition(centre - right + up));
        return r;
    }
}

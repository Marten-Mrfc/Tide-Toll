using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 3, L2 (message-system.md section 1, Goal): a second request at the same time goes to the strip in one line
/// with its count and time, and stays there while it runs (the goal section keeps the request it has). The strip's
/// order: "Ring in the next age", the charter drive, then the second request. A third waits (it is not shown).
/// </summary>
public partial class TownHud
{
    private Label _second = null!;
    private readonly MessageFader _secondF = new(LargeSeconds);
    private int _secondSig;

    /// <summary>The strip's second request as shown ("" none; tests).</summary>
    public string SecondRequestText => _second.Visible && _secondF.On ? _second.Text : "";

    private void BuildSecondRequest()
    {
        int pad = UiKit.U(_s, 12);
        _second = UiKit.Fixed(_s, UiKit.Caption, UiKit.UiFont, UiKit.Ink);
        _second.Name = "Second";
        _second.ClipText = true;
        _second.VerticalAlignment = VerticalAlignment.Center;
        _second.Position = new Vector2(pad, 0);
        _second.Size = new Vector2(_stripBox.Size.X - 2 * pad, _stripBox.Size.Y);
        _second.Visible = false;
        _stripBox.AddChild(_second);
        _secondF.Set(false);
        _secondSig = 0;
    }

    /// <summary>A skipper's request while the town's own holds the goal section: one line in the strip.</summary>
    private void SecondRequestFrame()
    {
        var t = _drv.Town;
        int best = -1;
        if (t.Request.Active && t.InRiverAge)
            for (int i = 0; i < t.BoatCount; i++)
            {
                var b = t.Boat(i);
                if (b.Phase is BoatPhase.Gone or BoatPhase.Leaving || !b.Request) continue;
                if (best < 0 || b.Phase == BoatPhase.Moored) best = i;
            }
        bool want = best >= 0 && !_nextAgeF.On && !_driveF.On && _fullA > 0.01f;
        _secondF.Want(want);
        _secondF.Step(_msgDt);
        if (best >= 0)
        {
            var v = t.Boat(best);
            int secs = (int)Math.Ceiling(v.Patience);
            int sig = HashCode.Combine(v.Id, v.Loaded, v.Want, secs, (int)v.Phase);
            if (sig != _secondSig)
            {
                _secondSig = sig;
                string text = v.Phase == BoatPhase.Moored
                    ? $"Skipper {v.Skipper}: {v.Loaded} of {v.Want} {GoodName(v.Good)}, {secs} s"
                    : $"Skipper {v.Skipper}: {v.Want} {GoodName(v.Good)}, waiting for a berth";
                // Nothing in the strip ends in an ellipsis (lead 22:40): a line that does not fit drops the name.
                if (TextWidth(_second, text) > _second.Size.X)
                    text = v.Phase == BoatPhase.Moored ? $"Skipper: {v.Loaded} of {v.Want} {GoodName(v.Good)}, {secs} s" : $"Skipper: {v.Want} {GoodName(v.Good)}, no berth yet";
                _second.Text = text;
            }
        }
        float a = _secondF.Value;
        bool vis = a > 0f;
        if (_second.Visible != vis) _second.Visible = vis;
        if (vis) { _second.Modulate = new Color(1, 1, 1, a); _second.Position = new Vector2(_second.Position.X, UiKit.U(_s, 8) * (1f - a)); }
    }
}

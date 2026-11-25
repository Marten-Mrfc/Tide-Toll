using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Paper tags naming the places walkers go to, as the toy labels them: Well, Market, Field and Ford. Small paper slips
/// in Alegreya with an ink edge, each tied by a short ink thread to its place on the map, tilted a degree or two as if
/// pinned by hand. They show at overview zoom and fade out as the view zooms in past about 150 m (up close the
/// buildings speak for themselves). Screen-space, behind the chapter page and the tabs, hidden when not wholly on
/// screen. Per frame only positions and the fade change, and only when they moved; no allocation.
/// </summary>
public partial class MapTags : Control
{
    private static readonly string[] Names = { "Well", "Market", "Field", "Ford" };
    /// <summary>The fade: fully shown above FullSize metres of view height, gone below GoneSize.</summary>
    public const float FullSize = 170f, GoneSize = 120f;

    private Main _main = null!;
    private float _s;
    private readonly PanelContainer[] _tags = new PanelContainer[4];
    private readonly Vector3[] _at = new Vector3[4];
    private readonly bool[] _has = new bool[4];
    private readonly Vector2[] _pin = new Vector2[4];
    private float _alpha = -1;

    /// <summary>Current opacity of the tags (tests): 1 at overview, 0 zoomed in.</summary>
    public float Alpha => Math.Max(0, _alpha);
    public PanelContainer Tag(int i) => _tags[i];

    public void Build(Main main, float s)
    {
        _main = main;
        _s = s;
        Name = "MapTags";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        for (int i = 0; i < 4; i++)
        {
            var p = new PanelContainer { Name = Names[i], MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            var box = new StyleBoxFlat { BgColor = UiKit.Paper, BorderColor = UiKit.Ink, AntiAliasing = true };
            box.SetBorderWidthAll(1);
            box.SetCornerRadiusAll(UiKit.Px(s, 2));
            box.ContentMarginLeft = box.ContentMarginRight = UiKit.Px(s, 6);
            box.ContentMarginTop = UiKit.Px(s, 1);
            box.ContentMarginBottom = UiKit.Px(s, 2);
            p.AddThemeStyleboxOverride("panel", box);
            var l = UiKit.Text(Names[i], s, 15, UiKit.Serif);
            l.Name = "Text";
            p.AddChild(l);
            p.Rotation = Mathf.DegToRad(i % 2 == 0 ? -1.5f : 1.2f);
            AddChild(p);
            _tags[i] = p;
        }
        SetWorld(main.Sim.World);
    }

    /// <summary>The places for a (new) world: the first well, market and field, and the ford's middle.</summary>
    public void SetWorld(World w)
    {
        float cell = (float)w.Params.CellMeters;
        var kinds = new[] { BuildingKind.Well, BuildingKind.Market, BuildingKind.Field };
        for (int i = 0; i < 3; i++)
        {
            var list = w.OfKind(kinds[i]);
            _has[i] = list.Length > 0;
            if (!_has[i]) continue;
            var b = w.Buildings[list[0]];
            _at[i] = new Vector3((b.X + b.W / 2f) * cell, 0, b.Y * cell); // the north edge: the tag hangs above it
        }
        int fy = w.Map.FordY;
        _has[3] = fy >= 0 && fy < w.Grid.Height;
        if (_has[3]) _at[3] = new Vector3(w.RiverX[fy] * cell, 0, (fy - 1.5f) * cell);
        _alpha = -1;
    }

    public override void _Process(double delta)
    {
        var rig = _main.Rig;
        float a = Mathf.Clamp((rig.Camera.Size - GoneSize) / (FullSize - GoneSize), 0f, 1f);
        if (a != _alpha) { _alpha = a; Modulate = new Color(1, 1, 1, a); }
        var vp = GetViewportRect().Size;
        bool moved = false;
        for (int i = 0; i < 4; i++)
        {
            var t = _tags[i];
            if (!_has[i] || a <= 0) { if (t.Visible) { t.Visible = false; moved = true; } continue; }
            var pin = rig.LocalToScreen(_at[i]);
            var size = t.GetCombinedMinimumSize();
            var pos = new Vector2(Mathf.Round(pin.X - size.X / 2f), Mathf.Round(pin.Y - size.Y - UiKit.Px(_s, 9)));
            bool on = pos.X >= 4 && pos.Y >= 4 && pos.X + size.X <= vp.X - 4 && pos.Y + size.Y <= vp.Y - 4;
            if (t.Visible != on) { t.Visible = on; moved = true; }
            if (on && (t.Position != pos || _pin[i] != pin)) { t.Position = pos; t.Size = size; _pin[i] = pin; moved = true; }
        }
        if (moved) QueueRedraw();
    }

    /// <summary>The ink threads from each tag down to its place, with a small dot where it is pinned.</summary>
    public override void _Draw()
    {
        for (int i = 0; i < 4; i++)
        {
            var t = _tags[i];
            if (!t.Visible) continue;
            var from = new Vector2(t.Position.X + t.Size.X / 2f, t.Position.Y + t.Size.Y);
            DrawLine(from, _pin[i], new Color(UiKit.Ink, 0.85f), Mathf.Max(1f, _s), true);
            DrawCircle(_pin[i], Mathf.Max(1.5f, 2.2f * _s), UiKit.Ink);
        }
    }
}

using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Paper tags naming the places walkers go to, as the toy labels them: Well, Market, Field and Ford. Small paper slips
/// in Alegreya with an ink edge, each tied by a short ink thread to its place on the map, tilted a degree or two as if
/// pinned by hand. They show at overview zoom and fade out as the view zooms in past about 150 m (up close the
/// buildings speak for themselves). Screen-space, behind the chapter page and the tabs, hidden when not wholly on
/// screen. While the spring flood closes the ford a fifth tag, "Ford closed" (bold, vermillion edge), takes the Ford
/// tag's place at every zoom. Per frame only positions and the fade change, and only when they moved; no allocation.
/// </summary>
public partial class MapTags : Control
{
    private static readonly string[] Names = { "Well", "Market", "Field", "Ford", "Ford closed" };
    /// <summary>The "Ford closed" tag (index 4): shown at every zoom while the spring flood closes the ford, in place of "Ford".</summary>
    public const int FordClosed = 4;
    private static readonly Color Stop = Color.FromHtml("#D55E00");
    /// <summary>The fade: fully shown above FullSize metres of view height, gone below GoneSize.</summary>
    public const float FullSize = 170f, GoneSize = 120f;

    private Main _main = null!;
    private float _s;
    private readonly PanelContainer[] _tags = new PanelContainer[5];
    private readonly Vector3[] _at = new Vector3[5];
    private readonly bool[] _has = new bool[5];
    private readonly Vector2[] _pin = new Vector2[5];
    private float _alpha = -1;
    private readonly Rect2[] _boats = new Rect2[64];

    /// <summary>The Ford tag's second line in Chapter II ("" in Chapter I), and whether a boat hid the tag last frame (tests).</summary>
    public string FordHint { get; private set; } = "";
    public bool FordHiddenByBoat { get; private set; }

    /// <summary>"free · wet grain sells at half price", from RiverRules.WetGrainValueShare.</summary>
    public static string FordHintText(RiverRules r)
    {
        double share = r.WetGrainValueShare;
        string at = System.Math.Abs(share - 0.5) < 1e-9 ? "half price" : System.Math.Abs(share - 1) < 1e-9 ? "full price"
            : $"{share * 100:0}% of its price";
        return $"free · wet grain sells at {at}";
    }

    /// <summary>Current opacity of the tags (tests): 1 at overview, 0 zoomed in.</summary>
    public float Alpha => Math.Max(0, _alpha);
    public PanelContainer Tag(int i) => _tags[i];
    public int Count => _tags.Length;

    /// <summary>A tag's rectangle on screen when it is showing (badges keep clear of it).</summary>
    public bool VisibleRect(int i, out Rect2 r)
    {
        var t = _tags[i];
        r = new Rect2(t.Position, t.Size);
        return t.Visible && t.Modulate.A > 0.05f;
    }

    public void Build(Main main, float s)
    {
        _main = main;
        _s = s;
        Name = "MapTags";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        for (int i = 0; i < 5; i++)
        {
            var p = new PanelContainer { Name = Names[i].Replace(" ", ""), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            var box = new StyleBoxFlat { BgColor = UiKit.Paper, BorderColor = i == FordClosed ? Stop : UiKit.Ink, AntiAliasing = true };
            box.SetBorderWidthAll(i == FordClosed ? Mathf.Max(2, UiKit.Px(s, 2)) : 1);
            box.SetCornerRadiusAll(UiKit.Px(s, 2));
            box.ContentMarginLeft = box.ContentMarginRight = UiKit.Px(s, 6);
            box.ContentMarginTop = UiKit.Px(s, 1);
            box.ContentMarginBottom = UiKit.Px(s, 2);
            p.AddThemeStyleboxOverride("panel", box);
            var l = i == FordClosed ? UiKit.Text(Names[i], s, 15, UiKit.UiBold) : UiKit.Text(Names[i], s, 15, UiKit.Serif);
            l.Name = "Text";
            if (i == 3 && main.Sim.World.River is { } river)
            {
                // Chapter II: the Ford tag says what wading costs (decision B), from the rules: grain gets wet and sells
                // for less, and a walker with a sack wades slower.
                var col = new VBoxContainer { Name = "Lines", MouseFilter = MouseFilterEnum.Ignore };
                col.AddThemeConstantOverride("separation", 0);
                l.HorizontalAlignment = HorizontalAlignment.Center;
                col.AddChild(l);
                FordHint = FordHintText(river.Rules);
                var h = UiKit.Text(FordHint, s, 12, UiKit.UiFont, UiKit.FadedInk.Darkened(0.35f));
                h.Name = "Hint";
                h.HorizontalAlignment = HorizontalAlignment.Center;
                col.AddChild(h);
                p.AddChild(col);
            }
            else p.AddChild(l);
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
        _has[4] = _has[3];
        _at[4] = _at[3];
        _alpha = -1;
    }

    public override void _Process(double delta)
    {
        var rig = _main.Rig;
        float a = Mathf.Clamp((rig.Camera.Size - GoneSize) / (FullSize - GoneSize), 0f, 1f);
        _alpha = a;
        var r = _main.Sim.World.River;
        bool closed = r != null && r.Flooded;
        var vp = GetViewportRect().Size;
        bool moved = false;
        int nb = r != null ? BoatBoxes(rig) : 0;
        FordHiddenByBoat = false;
        for (int i = 0; i < 5; i++)
        {
            var t = _tags[i];
            // Place names fade with the zoom; "Ford closed" shows at every zoom while it is true, and "Ford" gives way to it.
            float ta = i == FordClosed ? (closed ? 1f : 0f) : i == 3 && closed ? 0f : a;
            if (!_has[i] || ta <= 0) { if (t.Visible) { t.Visible = false; moved = true; } continue; }
            var m = new Color(1, 1, 1, ta);
            if (t.Modulate != m) { t.Modulate = m; moved = true; }
            var pin = rig.LocalToScreen(_at[i]);
            var size = t.GetCombinedMinimumSize();
            var pos = new Vector2(Mathf.Round(pin.X - size.X / 2f), Mathf.Round(pin.Y - size.Y - UiKit.Px(_s, 9)));
            bool on = pos.X >= 4 && pos.Y >= 4 && pos.X + size.X <= vp.X - 4 && pos.Y + size.Y <= vp.Y - 4;
            if (on && i == 3 && nb > 0 && UnderBoat(new Rect2(pos, size).Expand(pin), nb)) { on = false; FordHiddenByBoat = true; }
            if (t.Visible != on) { t.Visible = on; moved = true; }
            if (on && (t.Position != pos || _pin[i] != pin)) { t.Position = pos; t.Size = size; _pin[i] = pin; moved = true; }
        }
        if (moved) QueueRedraw();
    }

    /// <summary>Every boat drawn, as a box on screen (as RiverFloats does); returns how many.</summary>
    private int BoatBoxes(CameraRig rig)
    {
        var boats = _main.Boats;
        if (boats == null) return 0;
        int nb = 0;
        float mpp = Mathf.Max(1e-3f, rig.Camera.Size / Mathf.Max(1f, GetViewportRect().Size.Y));
        for (int i = 0; i < boats.Slots && nb < _boats.Length; i++)
        {
            if (!boats.BoatBox(i, out var at, out float half)) continue;
            var c = rig.LocalToScreen(at);
            float rr = half / mpp + 2f;
            _boats[nb++] = new Rect2(c.X - rr * 0.45f, c.Y - rr, rr * 0.9f, 2f * rr);
        }
        return nb;
    }

    private bool UnderBoat(Rect2 r, int nb)
    {
        for (int i = 0; i < nb; i++) if (r.Intersects(_boats[i])) return true;
        return false;
    }

    /// <summary>The ink threads from each tag down to its place, with a small dot where it is pinned.</summary>
    public override void _Draw()
    {
        for (int i = 0; i < 5; i++)
        {
            var t = _tags[i];
            if (!t.Visible) continue;
            var from = new Vector2(t.Position.X + t.Size.X / 2f, t.Position.Y + t.Size.Y);
            var ink = new Color(UiKit.Ink, 0.85f * t.Modulate.A);
            DrawLine(from, _pin[i], ink, Mathf.Max(1f, _s), true);
            DrawCircle(_pin[i], Mathf.Max(1.5f, 2.2f * _s), ink);
        }
    }
}

using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Pave mode: every road that can be paved lights up as a pale blue band on the map, the busiest (the town's
/// suggestion) carries a "Suggested" tag, hovering a road shows its price or why it cannot be paved, and a click paves
/// it. P, 4 or the Pave bookmark switch the mode; Esc leaves it. A drag still moves the view.
/// </summary>
public partial class TownHud
{
    public bool PaveMode { get; private set; }
    public int HoverRoad { get; private set; } = -1;
    /// <summary>Roads lit in the current overlay (tests).</summary>
    public int LitRoads { get; private set; }
    private MeshInstance3D _paveMesh = null!;
    private ImmediateMesh _paveIm = null!;
    private StandardMaterial3D _paveMat = null!;
    private Label _hoverTip = null!, _suggest = null!, _paveBanner = null!;
    private int _litHover = -2, _litSig = -1;

    private void BuildPave()
    {
        if (_paveMesh == null || !IsInstanceValid(_paveMesh))
        {
            _paveIm = new ImmediateMesh();
            _paveMat = Palette.Unlit(new Color(UiKit.Focus, 0.55f), transparent: true);
            _paveMat.VertexColorUseAsAlbedo = true;
            _paveMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            // Each pixel lit once (stencil): where bands overlap at a join they no longer stack into darker blotches.
            // Depth tested, so under the tilt a building in front hides the band behind it.
            _paveMat.StencilMode = BaseMaterial3D.StencilModeEnum.Custom;
            _paveMat.StencilFlags = 3; // read and write
            _paveMat.StencilCompare = BaseMaterial3D.StencilCompareEnum.NotEqual;
            _paveMat.StencilReference = 2;
            _paveMesh = new MeshInstance3D { Name = "PaveOverlay", Mesh = _paveIm, MaterialOverride = _paveMat, Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _main.MapOrigin.AddChild(_paveMesh);
        }
        _hoverTip = Tag("HoverTip", 16);
        _suggest = Tag("Suggest", 15);
        _paveBanner = UiKit.Text("Pave mode: click a lit road to pave it. Esc or P to stop.", _s, 18, UiKit.UiBold);
        var bannerBox = UiKit.Card(_s, new Color(UiKit.Paper, 0.95f), 2f);
        bannerBox.SetContentMarginAll(UiKit.Px(_s, 8));
        _paveBanner.AddThemeStyleboxOverride("normal", bannerBox);
        _paveBanner.Visible = false;
        _root.AddChild(_paveBanner);
        _litHover = -2;
    }

    private Label Tag(string name, float px)
    {
        var l = UiKit.Text("", _s, px, UiKit.UiBold);
        l.Name = name;
        var box = UiKit.Card(_s, UiKit.Paper, 1.5f);
        box.SetContentMarginAll(UiKit.Px(_s, 5));
        l.AddThemeStyleboxOverride("normal", box);
        l.Visible = false;
        _root.AddChild(l);
        return l;
    }

    public void SetPaveMode(bool on)
    {
        if (on && _drv.Town.SuggestedPave() < 0 && !AnyPavable())
        {
            Note("No road is worn in enough to pave yet.");
            on = false;
        }
        PaveMode = on;
        HoverRoad = -1;
        _litHover = -2;
    }

    private bool AnyPavable()
    {
        var t = _drv.Town;
        for (int r = 0; r < t.RoadCount; r++) if (t.RoadOpen(r) && t.CanPave(r, out _)) return true;
        return false;
    }

    /// <summary>The open road nearest a map-local point within 1.5 cells, or -1.</summary>
    public int RoadAt(Vector3 local)
    {
        var t = _drv.Town;
        float px = local.X / TownGround.Cell - 0.5f, py = local.Z / TownGround.Cell - 0.5f;
        int best = -1;
        float bestD = 1.5f * 1.5f;
        var roads = t.Plan.Roads;
        for (int r = 0; r < roads.Length; r++)
        {
            if (!t.RoadOpen(r)) continue;
            var xs = roads[r].Xs; var ys = roads[r].Ys;
            for (int k = 0; k + 1 < xs.Length; k++)
            {
                float d = SegDist2(px, py, xs[k], ys[k], xs[k + 1], ys[k + 1]);
                if (d < bestD) { bestD = d; best = r; }
            }
        }
        return best;
    }

    private static float SegDist2(float px, float py, float ax, float ay, float bx, float by)
    {
        float ex = bx - ax, ey = by - ay, l2 = ex * ex + ey * ey;
        float t = l2 > 0 ? Math.Clamp(((px - ax) * ex + (py - ay) * ey) / l2, 0f, 1f) : 0f;
        float qx = ax + ex * t - px, qy = ay + ey * t - py;
        return qx * qx + qy * qy;
    }

    /// <summary>Screen point of the middle of a road (tests click it).</summary>
    public Vector2 RoadScreen(int r)
    {
        var road = _drv.Town.Plan.Roads[r];
        road.At(road.Length / 2, out double x, out double y, out _);
        return _main.Rig.LocalToScreen(new Vector3((float)x * TownGround.Cell, 0, (float)y * TownGround.Cell));
    }

    private void PaveInput(InputEvent e)
    {
        if (!PaveMode) return;
        if (e is InputEventMouseMotion mm)
        {
            HoverRoad = _main.Rig.PickLocal(mm.Position, out var l) ? RoadAt(l) : -1;
            if (HoverRoad >= 0) _hoverTip.Position = mm.Position + new Vector2(UiKit.Px(_s, 16), UiKit.Px(_s, 12));
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mb && !_main.Rig.LeftDragged)
        {
            int r = _main.Rig.PickLocal(mb.Position, out var l) ? RoadAt(l) : -1;
            if (r < 0) return;
            var t = _drv.Town;
            if (t.CanPave(r, out string why))
            {
                if (t.Pave(r) && !AnyPavable()) SetPaveMode(false);
            }
            else Note(why.Length > 0 ? why + "." : "That road cannot be paved yet.");
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            SetPaveMode(false);
        }
    }

    private void PaveFrame()
    {
        var t = _drv.Town;
        _paveBanner.Visible = PaveMode;
        if (PaveMode)
        {
            _paveBanner.ResetSize();
            _paveBanner.Position = new Vector2((_built.X - _paveBanner.Size.X) / 2f, UiKit.Px(_s, 14));
        }
        _paveMesh.Visible = PaveMode;
        _hoverTip.Visible = PaveMode && HoverRoad >= 0;
        if (!PaveMode) { _suggest.Visible = false; return; }
        if (HoverRoad >= 0)
        {
            _hoverTip.Text = t.CanPave(HoverRoad, out string why)
                ? $"{(t.NextSurface(HoverRoad) == Surface.Cobbles ? "Cobbles" : "Gravel")}: {Money(t.PavePrice(HoverRoad))} coin. Click to pave."
                : (why.Length > 0 ? why : "Cannot be paved yet");
            _hoverTip.ResetSize();
        }
        int sug = t.SuggestedPave();
        _suggest.Visible = sug >= 0;
        if (sug >= 0)
        {
            _suggest.Text = $"Busiest road: {Money(t.PavePrice(sug))} coin";
            _suggest.ResetSize();
            _suggest.Position = RoadScreen(sug) - new Vector2(_suggest.Size.X / 2f, _suggest.Size.Y + UiKit.Px(_s, 10));
        }
        // Rebuild the lit bands when the hovered road or the paving changes.
        int sig = t.RoadsPaved * 1000 + sug + 7 * t.Level + (int)Math.Min(999, t.Coin / 50) * 0;
        for (int r = 0; r < t.RoadCount; r++) if (t.RoadOpen(r) && t.RoadInfo(r).WornIn) sig += r * 31;
        if (sig == _litSig && HoverRoad == _litHover) return;
        _litSig = sig; _litHover = HoverRoad;
        _paveIm.ClearSurfaces();
        int lit = 0;
        _paveIm.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        bool any = false;
        // The hovered road first, then the suggestion, then the rest: with the stencil the first band drawn wins a pixel.
        for (int pass = 0; pass < 3; pass++)
        for (int r = 0; r < t.RoadCount; r++)
        {
            int want = r == HoverRoad ? 0 : r == sug ? 1 : 2;
            if (want != pass) continue;
            if (!t.RoadOpen(r) || !t.CanPave(r, out _)) continue;
            var c = r == HoverRoad ? new Color(UiKit.Focus, 0.85f) : r == sug ? new Color(UiKit.Focus.Lerp(Colors.White, 0.2f), 0.7f) : new Color(UiKit.Focus, 0.42f);
            Band(t.Plan.Roads[r], r == HoverRoad ? 0.85f : 0.6f, c);
            lit++;
            any = true;
        }
        if (!any) { _paveIm.SurfaceSetColor(Colors.Transparent); _paveIm.SurfaceAddVertex(Vector3.Zero); _paveIm.SurfaceAddVertex(Vector3.Zero); _paveIm.SurfaceAddVertex(Vector3.Zero); }
        _paveIm.SurfaceEnd();
        LitRoads = lit;
    }

    private void Band(Road road, float halfCells, Color c)
    {
        float h = halfCells * TownGround.Cell, y = 0.3f;
        for (int k = 0; k + 1 < road.Xs.Length; k++)
        {
            var a = new Vector3((road.Xs[k] + 0.5f) * TownGround.Cell, y, (road.Ys[k] + 0.5f) * TownGround.Cell);
            var b = new Vector3((road.Xs[k + 1] + 0.5f) * TownGround.Cell, y, (road.Ys[k + 1] + 0.5f) * TownGround.Cell);
            var d = (b - a);
            if (d.LengthSquared() < 1e-6f) continue;
            var n = new Vector3(-d.Z, 0, d.X).Normalized() * h;
            var e = d.Normalized() * h * 0.5f;
            Vector3 a0 = a - e + n, a1 = a - e - n, b0 = b + e + n, b1 = b + e - n;
            foreach (var v in new[] { a0, a1, b0, b0, a1, b1 }) { _paveIm.SurfaceSetColor(c); _paveIm.SurfaceAddVertex(v); }
        }
    }
}

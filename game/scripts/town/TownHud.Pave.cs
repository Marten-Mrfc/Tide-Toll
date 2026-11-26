using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Pave mode: every road that can be paved lights up as a pale blue band along its drawn curve (Road.Px, Py); the
/// town's suggestion carries a "Suggested" tag; the road under the pointer shows its price; a click paves exactly that
/// road. Hover and click use one picking (<see cref="PaveTarget"/>), which only ever finds a road that can be paved
/// now, recomputed every frame from the pointer (the view may move under a still pointer). Choosing Pave brings the
/// suggested road into view when it is off screen, and the view goes back to following the town afterwards. A pave
/// leaves the mode; holding Shift keeps it on. P, 4 or the bookmark switch the mode; Esc or a right click leave it.
/// </summary>
public partial class TownHud
{
    public bool PaveMode { get; private set; }
    /// <summary>The road the tip names now (-1: none). Only a road that can be paved now.</summary>
    public int HoverRoad { get; private set; } = -1;
    /// <summary>Roads lit in the current overlay (tests).</summary>
    public int LitRoads { get; private set; }
    /// <summary>The road the last click in Pave mode acted on (-1 none), and the text the tip showed then (tests).</summary>
    public int LastPaveClickRoad { get; private set; } = -1;
    public string HoverTipText => _hoverTip.Visible ? _hoverTip.Text : "";
    private MeshInstance3D _paveMesh = null!;
    private ImmediateMesh _paveIm = null!;
    private StandardMaterial3D _paveMat = null!;
    private Label _hoverTip = null!, _suggest = null!, _paveBanner = null!;
    private int _litHover = -2, _litSig = -1, _litMode;
    private Vector2 _pointer = new(-1, -1);
    private bool _modeTookView;

    private void BuildPave()
    {
        if (_paveMesh == null || !IsInstanceValid(_paveMesh))
        {
            _paveIm = new ImmediateMesh();
            _paveMat = Palette.Unlit(new Color(UiKit.Focus, 0.55f), transparent: true);
            _paveMat.VertexColorUseAsAlbedo = true;
            _paveMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            // Each pixel lit once (stencil): where bands overlap at a join they no longer stack into darker blotches.
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
        _paveBanner = UiKit.Text("", _s, 18, UiKit.UiBold);
        var bannerBox = UiKit.Card(_s, new Color(UiKit.Paper, 0.95f), 2f);
        bannerBox.SetContentMarginAll(UiKit.Px(_s, 8));
        _paveBanner.AddThemeStyleboxOverride("normal", bannerBox);
        _paveBanner.Visible = false;
        _root.AddChild(_paveBanner);
        _litHover = -2;
        BuildExtend();
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
        if (on && !AnyPavable())
        {
            var t = _drv.Town;
            int sug = t.SuggestedPave();
            Note(sug >= 0 && t.Coin < t.PavePrice(sug) ? $"Paving costs {Money(t.PavePrice(sug))} coin." : "No road is worn in enough to pave yet.");
            on = false;
        }
        if (on && ExtendMode) SetExtendMode(false);
        bool was = PaveMode;
        PaveMode = on;
        HoverRoad = -1;
        _litHover = -2;
        if (on && !was) BringIntoView(SuggestedRoadPoint());
        if (!on && was) GiveViewBack();
    }

    private bool AnyPavable()
    {
        var t = _drv.Town;
        for (int r = 0; r < t.RoadCount; r++) if (Pavable(r)) return true;
        return false;
    }

    /// <summary>A road that can be paved this moment: open, worn in, a surface left to lay, and the coin for it.</summary>
    public bool Pavable(int r)
    {
        var t = _drv.Town;
        return t.RoadOpen(r) && t.CanPave(r, out _) && t.Coin >= t.PavePrice(r);
    }

    private Vector3? SuggestedRoadPoint()
    {
        int r = _drv.Town.SuggestedPave();
        if (r < 0) return null;
        var road = _drv.Town.Plan.Roads[r];
        road.At(road.Length / 2, out double x, out double y, out _);
        return new Vector3((float)x * TownGround.Cell, 0, (float)y * TownGround.Cell);
    }

    /// <summary>Moves the view so a map point is in sight, clear of the interface, if it is not already.</summary>
    private void BringIntoView(Vector3? local)
    {
        if (local == null) return;
        var rig = _main.Rig;
        var at = rig.LocalToScreen(local.Value);
        // Only when it is off screen (or at the very edge) or under a card of the interface (round 4: the target must end
        // up clear of the interface; the view's centre is).
        float m = UiKit.Px(_s, 40);
        var safe = new Rect2(m, m, _built.X - 2 * m, _built.Y - 2 * m);
        if (safe.HasPoint(at) && !OverInterface(at)) return;
        _modeTookView = !rig.UserMoved;
        rig.UserMoved = true;
        rig.LookAt(local.Value, rig.TargetSize, instant: false);
    }

    private void GiveViewBack()
    {
        if (_modeTookView) { _main.Rig.UserMoved = false; _modeTookView = false; }
    }

    /// <summary>
    /// The one picking for hover and click: the road that can be paved now nearest the screen point, within 1.2 cells
    /// of its drawn curve, or -1. Roads that cannot be paved are never found, so they never take a click.
    /// </summary>
    public int PaveTarget(Vector2 screen)
    {
        if (OverInterface(screen) || !_main.Rig.PickLocal(screen, out var local)) return -1;
        var t = _drv.Town;
        double px = local.X / TownGround.Cell - 0.5, py = local.Z / TownGround.Cell - 0.5;
        int best = -1;
        double bestD = 1.2 * 1.2;
        var roads = t.Plan.Roads;
        for (int r = 0; r < roads.Length; r++)
        {
            if (!Pavable(r)) continue;
            double d = CurveDist2(roads[r], px, py);
            if (d < bestD) { bestD = d; best = r; }
        }
        return best;
    }

    /// <summary>Squared distance (cells) from a point to a road's drawn curve.</summary>
    public static double CurveDist2(Road road, double px, double py)
    {
        var xs = road.Px; var ys = road.Py;
        double best = double.MaxValue;
        for (int k = 0; k + 1 < xs.Length; k++)
        {
            double ax = xs[k], ay = ys[k], ex = xs[k + 1] - ax, ey = ys[k + 1] - ay, l2 = ex * ex + ey * ey;
            double u = l2 > 0 ? Math.Clamp(((px - ax) * ex + (py - ay) * ey) / l2, 0, 1) : 0;
            double qx = ax + ex * u - px, qy = ay + ey * u - py, d = qx * qx + qy * qy;
            if (d < best) best = d;
        }
        return best;
    }

    /// <summary>Screen point of the middle of a road (tests click it).</summary>
    public Vector2 RoadScreen(int r)
    {
        var road = _drv.Town.Plan.Roads[r];
        road.At(road.Length / 2, out double x, out double y, out _);
        return _main.Rig.LocalToScreen(new Vector3((float)x * TownGround.Cell, 0, (float)y * TownGround.Cell));
    }

    /// <summary>Screen point at a fraction along a road's curve (tests).</summary>
    public Vector2 RoadScreenAt(int r, double frac)
    {
        var road = _drv.Town.Plan.Roads[r];
        road.At(road.Length * Math.Clamp(frac, 0, 1), out double x, out double y, out _);
        return _main.Rig.LocalToScreen(new Vector3((float)x * TownGround.Cell, 0, (float)y * TownGround.Cell));
    }

    private void PaveInput(InputEvent e)
    {
        if (e is InputEventMouseMotion mm) _pointer = mm.Position;
        if (!PaveMode) return;
        if (e is InputEventMouseMotion m2)
        {
            HoverRoad = PaveTarget(m2.Position);
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mb && !_main.Rig.LeftDragged)
        {
            _pointer = mb.Position;
            int r = PaveTarget(mb.Position);
            HoverRoad = r;
            LastPaveClickRoad = r;
            if (r < 0) return;
            var t = _drv.Town;
            if (t.Pave(r))
            {
                if (!mb.ShiftPressed || !AnyPavable()) SetPaveMode(false);
            }
            // Not marked handled: the camera must see the release too, or it keeps the button down and the next
            // mouse move drags the map (round 3: that was a real bug after every Pave click).
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            SetPaveMode(false);
        }
    }

    private void PaveFrame()
    {
        var t = _drv.Town;
        bool banner = PaveMode || ExtendMode;
        _paveBanner.Visible = banner;
        if (banner)
        {
            string want = ModeHelp(PaveMode);
            if (_paveBanner.Text != want) _paveBanner.Text = want;
            _paveBanner.ResetSize();
            _paveBanner.Position = new Vector2((_built.X - _paveBanner.Size.X) / 2f, UiKit.Px(_s, 14));
        }
        if (PaveMode && !AnyPavable()) SetPaveMode(false);
        if (PaveMode && _pointer.X >= 0) HoverRoad = PaveTarget(_pointer); // the view may have moved under the pointer
        _paveMesh.Visible = PaveMode || ExtendMode;
        _hoverTip.Visible = (PaveMode && HoverRoad >= 0) || (ExtendMode && HoverCottage >= 0);
        if (!PaveMode && !ExtendMode) { _suggest.Visible = false; return; }
        if (!PaveMode) { ExtendFrame(); return; }
        if (HoverRoad >= 0)
        {
            string tip = $"{(t.NextSurface(HoverRoad) == Surface.Cobbles ? "Cobbles" : "Gravel")} for this road: {Money(t.PavePrice(HoverRoad))} coin. Click to pave.";
            if (_hoverTip.Text != tip) { _hoverTip.Text = tip; _hoverTip.ResetSize(); }
            _hoverTip.Position = _pointer + new Vector2(UiKit.Px(_s, 16), UiKit.Px(_s, 12));
        }
        int sug = t.SuggestedPave();
        _suggest.Visible = sug >= 0 && Pavable(sug);
        if (_suggest.Visible)
        {
            string s = $"Suggested: {Money(t.PavePrice(sug))} coin";
            if (_suggest.Text != s) { _suggest.Text = s; _suggest.ResetSize(); }
            _suggest.Position = RoadScreen(sug) - new Vector2(_suggest.Size.X / 2f, _suggest.Size.Y + UiKit.Px(_s, 10));
        }
        // Rebuild the lit bands when the hovered road or what can be paved changes.
        int sig = t.RoadsPaved * 1000 + sug + 7 * t.Level;
        for (int r = 0; r < t.RoadCount; r++) if (Pavable(r)) sig += (r + 1) * 31;
        if (sig == _litSig && HoverRoad == _litHover && _litMode == 1) return;
        _litSig = sig; _litHover = HoverRoad; _litMode = 1;
        _paveIm.ClearSurfaces();
        int lit = 0;
        _paveIm.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        // The hovered road first, then the suggestion, then the rest: with the stencil the first band drawn wins a pixel.
        for (int pass = 0; pass < 3; pass++)
            for (int r = 0; r < t.RoadCount; r++)
            {
                int want = r == HoverRoad ? 0 : r == sug ? 1 : 2;
                if (want != pass) continue;
                if (!Pavable(r)) continue;
                var c = r == HoverRoad ? new Color(UiKit.Focus, 0.85f) : r == sug ? new Color(UiKit.Focus.Lerp(Colors.White, 0.2f), 0.7f) : new Color(UiKit.Focus, 0.42f);
                Band(t.Plan.Roads[r], r == HoverRoad ? 0.85f : 0.6f, c);
                lit++;
            }
        EmptyGuard(lit);
        _paveIm.SurfaceEnd();
        LitRoads = lit;
    }

    private void EmptyGuard(int lit)
    {
        if (lit > 0) return;
        _paveIm.SurfaceSetColor(Colors.Transparent);
        for (int i = 0; i < 3; i++) _paveIm.SurfaceAddVertex(Vector3.Zero);
    }

    /// <summary>
    /// A band along a road's drawn curve: one continuous strip with a mitred normal at every curve point, so the many
    /// short segments of a bend neither overlap nor leave slivers; each end runs on half a band width.
    /// </summary>
    private void Band(Road road, float halfCells, Color c)
    {
        float C = TownGround.Cell, h = halfCells * C, y = 0.3f;
        var xs = road.Px; var ys = road.Py;
        int n = xs.Length;
        if (n < 2) return;
        Vector3 P(int k) => new((float)(xs[k] + 0.5) * C, y, (float)(ys[k] + 0.5) * C);
        Vector3 prevL = default, prevR = default;
        for (int k = 0; k < n; k++)
        {
            var a = P(Math.Max(0, k - 1)); var b = P(Math.Min(n - 1, k + 1));
            var t = b - a;
            if (t.LengthSquared() < 1e-8f) t = Vector3.Right;
            t = t.Normalized();
            var nrm = new Vector3(-t.Z, 0, t.X) * h;
            var p = P(k);
            if (k == 0) p -= t * h * 0.5f;
            if (k == n - 1) p += t * h * 0.5f;
            Vector3 l = p + nrm, r = p - nrm;
            if (k > 0) Quad(prevL, prevR, l, r, c);
            prevL = l; prevR = r;
        }
    }

    private void Quad(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Color c)
    {
        _paveIm.SurfaceSetColor(c); _paveIm.SurfaceAddVertex(a0);
        _paveIm.SurfaceSetColor(c); _paveIm.SurfaceAddVertex(a1);
        _paveIm.SurfaceSetColor(c); _paveIm.SurfaceAddVertex(b0);
        _paveIm.SurfaceSetColor(c); _paveIm.SurfaceAddVertex(b0);
        _paveIm.SurfaceSetColor(c); _paveIm.SurfaceAddVertex(a1);
        _paveIm.SurfaceSetColor(c); _paveIm.SurfaceAddVertex(b1);
    }
}

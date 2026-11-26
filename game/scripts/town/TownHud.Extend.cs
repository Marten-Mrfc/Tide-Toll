using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Extend mode (rules v2), the twin of Pave mode: every cottage that can be extended now gets a pale blue frame round
/// its footprint, the town's suggestion a "Suggested" tag, the cottage under the pointer its price and rooms, and a
/// click extends exactly that cottage. Picking matches the drawn house: the ground under the pointer inside the
/// footprint (a little margin), or the pointer on the house's body as seen from the tilted camera. An extension leaves
/// the mode; Shift keeps it on. E, 5 or the bookmark switch it; Esc or a right click leave it.
/// </summary>
public partial class TownHud
{
    public bool ExtendMode { get; private set; }
    /// <summary>The cottage plot the tip names now (-1 none); only a cottage that can be extended now.</summary>
    public int HoverCottage { get; private set; } = -1;
    public int LitCottages { get; private set; }
    public int LastExtendClickPlot { get; private set; } = -1;

    private void BuildExtend() { }

    public void SetExtendMode(bool on)
    {
        if (on && !AnyExtendable())
        {
            var t = _drv.Town;
            string why = t.ExtendLockReason;
            Note(why.Length > 0 ? why + "." : t.Coin < t.ExtendPrice ? $"Extending costs {Money(t.ExtendPrice)} coin." : "No cottage can be extended now.");
            on = false;
        }
        if (on && PaveMode) SetPaveMode(false);
        bool was = ExtendMode;
        ExtendMode = on;
        HoverCottage = -1;
        _litHover = -2;
        if (on && !was)
        {
            int s = _drv.Town.SuggestedExtend();
            if (s >= 0) BringIntoView(PlotCentre(s));
        }
        if (!on && was) GiveViewBack();
    }

    public bool Extendable(int plot)
    {
        var t = _drv.Town;
        return t.CanExtendNow(plot);
    }

    private bool AnyExtendable()
    {
        var plots = _drv.Town.Plan.Plots;
        for (int p = 0; p < plots.Length; p++) if (plots[p].Kind == PlotKind.Cottage && Extendable(p)) return true;
        return false;
    }

    private static Vector3 PlotCentre(Plot p, float height = 0f)
        => new((p.X + p.W / 2f) * TownGround.Cell, height, (p.Y + p.H / 2f) * TownGround.Cell);
    private Vector3 PlotCentre(int plot) => PlotCentre(_drv.Town.Plan.Plots[plot]);

    /// <summary>Screen point of a plot's middle at roof height (tests click it).</summary>
    public Vector2 PlotScreen(int plot) => _main.Rig.LocalToScreen(PlotCentre(_drv.Town.Plan.Plots[plot], 1.5f));

    /// <summary>The one picking for hover and click: the cottage that can be extended now under the screen point, or -1.</summary>
    public int ExtendTarget(Vector2 screen)
    {
        if (OverInterface(screen)) return -1;
        var t = _drv.Town;
        var plots = t.Plan.Plots;
        bool ground = _main.Rig.PickLocal(screen, out var local);
        double gx = local.X / TownGround.Cell, gy = local.Z / TownGround.Cell;
        int best = -1;
        float bestD = float.MaxValue;
        for (int p = 0; p < plots.Length; p++)
        {
            var pl = plots[p];
            if (pl.Kind != PlotKind.Cottage || !Extendable(p)) continue;
            // On the ground inside the footprint, with half a cell of margin.
            if (ground && gx >= pl.X - 0.5 && gx <= pl.X + pl.W + 0.5 && gy >= pl.Y - 0.5 && gy <= pl.Y + pl.H + 0.5) return p;
            // Or on the house's body as the camera sees it: near the line from its foot to its roof on screen.
            var foot = _main.Rig.LocalToScreen(PlotCentre(pl));
            var roof = _main.Rig.LocalToScreen(PlotCentre(pl, 3f));
            var w = _main.Rig.LocalToScreen(PlotCentre(pl) + new Vector3(pl.W * TownGround.Cell / 2f, 0, 0));
            float r = Math.Max(8f, foot.DistanceTo(w));
            float d = DistToSeg(screen, foot, roof);
            if (d < r && d < bestD) { bestD = d; best = p; }
        }
        return best;
    }

    private static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        var e = b - a;
        float l2 = e.LengthSquared();
        float u = l2 > 0 ? Math.Clamp((p - a).Dot(e) / l2, 0f, 1f) : 0f;
        return (a + e * u).DistanceTo(p);
    }

    private void ExtendInput(InputEvent e)
    {
        if (!ExtendMode) return;
        if (e is InputEventMouseMotion mm) HoverCottage = ExtendTarget(mm.Position);
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mb && !_main.Rig.LeftDragged)
        {
            _pointer = mb.Position;
            int p = ExtendTarget(mb.Position);
            HoverCottage = p;
            LastExtendClickPlot = p;
            if (p < 0) return;
            if (_drv.Town.Extend(p) && (!mb.ShiftPressed || !AnyExtendable())) SetExtendMode(false);
            // Not marked handled: the camera must see the release too, or it keeps the button down and the next
            // mouse move drags the map (round 3: that was a real bug after every Pave click).
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }) SetExtendMode(false);
    }

    private void ExtendFrame()
    {
        var t = _drv.Town;
        if (!AnyExtendable()) { SetExtendMode(false); return; }
        if (_pointer.X >= 0) HoverCottage = ExtendTarget(_pointer);
        _hoverTip.Visible = HoverCottage >= 0;
        if (HoverCottage >= 0)
        {
            int room = t.CottageRoom(HoverCottage);
            string tip = $"Extend this cottage: {Money(t.ExtendPrice)} coin. Rooms {room} to {room + 1}.";
            if (_hoverTip.Text != tip) { _hoverTip.Text = tip; _hoverTip.ResetSize(); }
            _hoverTip.Position = TipBeside(PlotScreenRect(t.Plan.Plots[HoverCottage]));
        }
        int sug = t.SuggestedExtend();
        _suggest.Visible = sug >= 0 && Extendable(sug);
        if (_suggest.Visible)
        {
            string s = $"Suggested: {Money(t.ExtendPrice)} coin";
            if (_suggest.Text != s) { _suggest.Text = s; _suggest.ResetSize(); }
            _suggest.Position = PlotScreen(sug) - new Vector2(_suggest.Size.X / 2f, _suggest.Size.Y + UiKit.Px(_s, 24));
        }
        int sig = t.ExtensionsBought * 1000 + sug + 7 * t.Level;
        var plots = t.Plan.Plots;
        for (int p = 0; p < plots.Length; p++) if (plots[p].Kind == PlotKind.Cottage && Extendable(p)) sig += (p + 1) * 31;
        if (sig == _litSig && HoverCottage == _litHover && _litMode == 2) return;
        _litSig = sig; _litHover = HoverCottage; _litMode = 2;
        _paveIm.ClearSurfaces();
        _paveIm.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        int lit = 0;
        for (int p = 0; p < plots.Length; p++)
        {
            if (plots[p].Kind != PlotKind.Cottage || !Extendable(p)) continue;
            var c = p == HoverCottage ? new Color(UiKit.Focus, 0.9f) : p == sug ? new Color(UiKit.Focus.Lerp(Colors.White, 0.2f), 0.75f) : new Color(UiKit.Focus, 0.5f);
            Frame(plots[p], p == HoverCottage ? 0.7f : 0.5f, c);
            lit++;
        }
        EmptyGuard(lit);
        _paveIm.SurfaceEnd();
        LitCottages = lit;
    }

    /// <summary>Round 6: a plot's screen rect, ground to roof (the price tip and coin numbers keep clear of it).</summary>
    private Rect2 PlotScreenRect(Plot p)
    {
        float C = TownGround.Cell;
        var r = new Rect2(_main.Rig.LocalToScreen(new Vector3(p.X * C, 0, p.Y * C)), Vector2.Zero);
        foreach (float h in new[] { 0f, 7f })
            foreach (var (x, z) in new[] { (p.X, p.Y), (p.X + p.W, p.Y), (p.X, p.Y + p.H), (p.X + p.W, p.Y + p.H) })
                r = r.Expand(_main.Rig.LocalToScreen(new Vector3(x * C, h, z * C)));
        return r;
    }

    /// <summary>
    /// Round 6: the price tip sits beside the pointer, past the target's side (right, or left near the screen's right
    /// edge), level with the pointer: never over the cottage roof or the people at its door.
    /// </summary>
    private Vector2 TipBeside(Rect2 target)
    {
        var sz = _hoverTip.Size;
        float gap = UiKit.Px(_s, 14);
        float x = Math.Max(_pointer.X + UiKit.Px(_s, 24), target.End.X + gap);
        if (x + sz.X > _built.X - UiKit.Px(_s, 8)) x = Math.Min(_pointer.X - UiKit.Px(_s, 24), target.Position.X - gap) - sz.X;
        float y = Math.Clamp(_pointer.Y - sz.Y / 2f, UiKit.Px(_s, 8), _built.Y - sz.Y - UiKit.Px(_s, 8));
        // Last pass: the tip still sat on villagers (b04, d03). Level with the pointer when clear of them, else just
        // above or below the target, whichever covers nobody (allocation-free: a few rects tested against the walkers).
        float lo = UiKit.Px(_s, 8), hi = _built.Y - sz.Y - UiKit.Px(_s, 8);
        float up = Math.Clamp(target.Position.Y - gap - sz.Y, lo, hi), down = Math.Clamp(target.End.Y + gap, lo, hi);
        if (CoversVillager(new Rect2(x, y, sz)))
        {
            if (!CoversVillager(new Rect2(x, up, sz))) y = up;
            else if (!CoversVillager(new Rect2(x, down, sz))) y = down;
        }
        return new Vector2(x, y);
    }

    /// <summary>Whether a screen rect lies over any villager (with a small margin for the figure's height).</summary>
    private bool CoversVillager(Rect2 r)
    {
        var t = _drv.Town;
        float C = TownGround.Cell, m = UiKit.Px(_s, 10);
        r = r.Grow(m);
        for (int i = 0; i < t.VillagerCount; i++)
        {
            var v = t.Villager(i);
            if (r.HasPoint(_main.Rig.LocalToScreen(new Vector3((float)v.X * C, 1f, (float)v.Y * C)))) return true;
        }
        return false;
    }

    /// <summary>The price tip's screen rect (tests: clear of the hovered cottage).</summary>
    public Rect2 HoverTipRect => _hoverTip.GetGlobalRect();
    public Rect2 HoverCottageRect => HoverCottage >= 0 ? PlotScreenRect(_drv.Town.Plan.Plots[HoverCottage]) : new Rect2();

    /// <summary>A band round a plot's footprint, just outside it.</summary>
    private void Frame(Plot p, float width, Color c)
    {
        float C = TownGround.Cell, y = 0.3f, w = width * C;
        float x0 = p.X * C - 0.2f * C, x1 = (p.X + p.W) * C + 0.2f * C, z0 = p.Y * C - 0.2f * C, z1 = (p.Y + p.H) * C + 0.2f * C;
        Quad(new Vector3(x0 - w, y, z0 - w), new Vector3(x0 - w, y, z0), new Vector3(x1 + w, y, z0 - w), new Vector3(x1 + w, y, z0), c);
        Quad(new Vector3(x0 - w, y, z1), new Vector3(x0 - w, y, z1 + w), new Vector3(x1 + w, y, z1), new Vector3(x1 + w, y, z1 + w), c);
        Quad(new Vector3(x0 - w, y, z0), new Vector3(x0, y, z0), new Vector3(x0 - w, y, z1), new Vector3(x0, y, z1), c);
        Quad(new Vector3(x1, y, z0), new Vector3(x1 + w, y, z0), new Vector3(x1, y, z1), new Vector3(x1 + w, y, z1), c);
    }
}

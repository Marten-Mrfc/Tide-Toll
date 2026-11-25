using Godot;

namespace TideToll.Game;

/// <summary>
/// The marks of a growing town (the toy's effects.ts), drawn over the map page in screen space so they keep their size
/// at every zoom: while a new cottage rises, a puff of trampled-earth dust at its foot; then two vermillion rings a
/// beat apart spreading from it (3.2 s), and a small vermillion badge with a paper halo at its plot's corner that stays
/// 15 s and fades, so the player notices where the town grew. Times come from TownLayer (real time, so a fast-forward
/// still shows the newest houses). Redrawn only while a mark is showing; allocation-free.
/// </summary>
public partial class NewHouses : Control
{
    public const double RingSec = 3.2, BadgeSec = 15;
    public static readonly Color Mark = Color.FromHtml("#B3261E"); // the toy's --map-new
    private Main _main = null!;
    private float _s = 1;
    private bool _drawn;

    /// <summary>Marks drawn in the last frame (tests and screenshots).</summary>
    public int Showing { get; private set; }

    public void Build(Main main, float s)
    {
        _main = main;
        _s = s;
        Name = "NewHouses";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta)
    {
        var town = _main.Town;
        var w = _main.Sim.World;
        int g = w.Grown.Length, active = 0;
        for (int i = g - 1; i >= 0; i--)
        {
            double at = town.SeenAt(i);
            if (at < -1e8) continue; // stood up during a fast-forward, unmarked
            if (at < 0) continue;
            if (town.Now - at >= TownLayer.RaiseSec + BadgeSec) break; // older ones are done (build order)
            active++;
        }
        if (active > 0 || _drawn) QueueRedraw();
        Showing = active;
    }

    public override void _Draw()
    {
        _drawn = false;
        var town = _main.Town;
        var w = _main.Sim.World;
        var rig = _main.Rig;
        var vp = GetViewportRect().Size;
        if (vp.Y < 1) return;
        float pxPerM = vp.Y / rig.Camera.Size, cell = (float)w.Params.CellMeters;
        var grown = w.Grown;
        var buildings = w.Buildings;
        for (int i = grown.Length - 1; i >= 0; i--)
        {
            double at = town.SeenAt(i);
            if (at < 0) continue;
            double age = town.Now - at;
            if (age >= TownLayer.RaiseSec + BadgeSec) break;
            var b = buildings[grown[i].Building];
            var centre = rig.LocalToScreen(new Vector3((b.X + b.W / 2f) * cell, 0, (b.Y + b.H / 2f) * cell));
            float sc = cell * pxPerM; // pixels per cell
            _drawn = true;
            if (age < TownLayer.RaiseSec)
            {
                float t = (float)(age / TownLayer.RaiseSec);
                var dust = new Color(Palette.TrailEarth.Lerp(Palette.Paper, 0.3f), 0.7f * (1 - t));
                for (int k = 0; k < 5; k++)
                {
                    float a = k * 1.2566f + t * 0.8f, rr = (0.9f + 1.3f * t) * sc;
                    var p = centre + new Vector2(Mathf.Cos(a) * rr, 0.6f * sc + Mathf.Sin(a) * rr * 0.4f);
                    DrawCircle(p, Mathf.Max(2f * _s, (0.35f + 0.3f * t) * sc), dust);
                }
                continue;
            }
            double a2 = age - TownLayer.RaiseSec;
            if (a2 < RingSec)
            {
                for (int k = 0; k < 2; k++)
                {
                    float t = (float)((a2 - k * 0.5) / (RingSec - 0.5));
                    if (t <= 0 || t >= 1) continue;
                    float e = 1 - (1 - t) * (1 - t);
                    float r = Mathf.Max(10f * _s, (2 + 7 * e) * sc);
                    DrawArc(centre, r, 0, Mathf.Tau, 48, new Color(Mark, 0.9f * (1 - t)), Mathf.Max(1.5f, 2.5f * _s * (1 - 0.5f * t)), true);
                }
            }
            // The badge at the plot's north-east corner, fading over its last second and a half.
            float fade = Mathf.Clamp((float)((TownLayer.RaiseSec + BadgeSec - age) / 1.5), 0, 1);
            var corner = rig.LocalToScreen(new Vector3((b.X + b.W - 0.1f) * cell, 0, (b.Y + 0.1f) * cell));
            float br = Mathf.Max(4.5f * _s, 0.45f * sc);
            DrawCircle(corner, br + 2f * _s, new Color(UiKit.Paper, fade));
            DrawCircle(corner, br, new Color(Mark, fade));
            DrawArc(corner, br + 2f * _s, 0, Mathf.Tau, 24, new Color(UiKit.Ink, 0.6f * fade), Mathf.Max(1f, _s), true);
        }
    }
}

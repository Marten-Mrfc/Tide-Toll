using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Draws the flow preview on the map (rendering proof 7, shaders/route.gdshader): the routes walkers take now as
/// thin dashed ink, faint; the routes they would take after the change as solid sky-blue lines over an ink halo, with
/// arrow heads for direction, at full strength only where they leave the old route; a cut-off route as vermillion
/// dots ending in a ring and cross at the destination it can no longer reach. Colour is never the only cue: dashes,
/// solid lines, arrows and the cross carry the meaning. One mesh, one draw call, rebuilt only when a new preview
/// result arrives; per frame only two uniforms change.
/// </summary>
public partial class RouteLayer : MeshInstance3D
{
    public static readonly Color Preview = Color.FromHtml("#56B4E9");  // player preview, sky blue
    public static readonly Color Blocked = Color.FromHtml("#D55E00");  // blocked, vermillion

    private ShaderMaterial _mat = null!;
    private FlowPreview? _shown;
    private float _cell = 2f;
    private float _opacity, _mpp = -1;
    private readonly List<Vector3> _v = new(), _n = new();
    private readonly List<Vector2> _uv = new(), _uv2 = new();
    private readonly List<Color> _c = new();
    private readonly List<int> _i = new();
    private readonly List<float> _ring = new();
    private static readonly StringName Mpp = new("metres_per_px"), March = new("march"), Opacity = new("opacity");

    /// <summary>Routes in the mesh now (tests).</summary>
    public int Routes { get; private set; }
    public int Vertices => _v.Count;

    public void Init(float cellMetres)
    {
        Name = "Routes";
        _cell = cellMetres;
        _mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/route.gdshader") };
        MaterialOverride = _mat;
        CastShadow = ShadowCastingSetting.Off;
        ExtraCullMargin = 400;
        Visible = false;
    }

    /// <summary>Per frame: follow the zoom, march the dashes, fade in or out, and rebuild when the result changed.</summary>
    public void Show(FlowPreview? p, float metresPerPx, double time, double delta)
    {
        if (!ReferenceEquals(p, _shown) && p != null) { Build(p); _shown = p; }
        float target = p != null ? 1f : 0f;
        if (_opacity != target)
        {
            _opacity = Mathf.MoveToward(_opacity, target, (float)delta * 6f);
            _mat.SetShaderParameter(Opacity, _opacity);
            Visible = _opacity > 0;
            if (_opacity == 0) _shown = null;
        }
        if (!Visible) return;
        if (metresPerPx != _mpp) { _mpp = metresPerPx; _mat.SetShaderParameter(Mpp, metresPerPx); }
        _mat.SetShaderParameter(March, (float)(time % 1000.0));
    }

    private void Build(FlowPreview p)
    {
        _v.Clear(); _n.Clear(); _uv.Clear(); _uv2.Clear(); _c.Clear(); _i.Clear();
        Routes = 0;
        var ink = Palette.Ink;
        // "Now" first (underneath), then "after" with its ink halo, so new routes read on top.
        foreach (var c in p.Changes)
        {
            AddRoute(c.OldPts, null, 0.5f, ink, 1.6f, 0.16f, 0.7f, false);
            Routes++;
        }
        foreach (var c in p.Changes)
        {
            if (c.Reachable)
            {
                AddRoute(c.NewPts, c.OldPts, 1f, ink, 3.4f, 0.20f, 0.8f, false);
                AddRoute(c.NewPts, c.OldPts, 1f, Preview, 2.0f, 0.22f, 1f, true);
            }
            else
            {
                AddRoute(c.NewPts, null, 1f, ink, 3.2f, 0.20f, 0.6f, false);
                AddRoute(c.NewPts, null, 0.25f, Blocked, 2.2f, 0.22f, 1f, false);
            }
            Routes++;
        }
        var b = p.Blocked;
        foreach (var (dest, _) in b) BlockedMark(dest, p);
        Commit();
    }

    /// <summary>A ring round the destination's door and a cross on it.</summary>
    private void BlockedMark(int dest, FlowPreview p)
    {
        // The door is the end of any old route to this destination.
        foreach (var c in p.Changes)
        {
            if (c.Dest != dest || c.OldPts.Length < 2) continue;
            float x = c.OldPts[^2], y = c.OldPts[^1], r = 2.4f;
            _ring.Clear();
            for (int k = 0; k <= 24; k++)
            {
                float a = k * Mathf.Tau / 24f;
                _ring.Add(x + r * Mathf.Cos(a)); _ring.Add(y + r * Mathf.Sin(a));
            }
            var ring = _ring.ToArray();
            AddRoute(ring, null, 1f, Palette.Ink, 3.4f, 0.24f, 0.8f, false);
            AddRoute(ring, null, 1f, Blocked, 2.0f, 0.26f, 1f, false);
            float d = 1.3f;
            AddRoute(new[] { x - d, y - d, x + d, y + d }, null, 1f, Blocked, 2.0f, 0.26f, 1f, false);
            AddRoute(new[] { x + d, y - d, x - d, y + d }, null, 1f, Blocked, 2.0f, 0.26f, 1f, false);
            return;
        }
    }

    /// <summary>
    /// A ribbon along pts (x,y pairs in cells). Where <paramref name="old"/> is given, points within a cell of it get
    /// 40% strength, so only the parts of the new route that differ draw at full strength.
    /// </summary>
    private void AddRoute(float[] pts, float[]? old, float style, Color colour, float halfPx, float y, float strength, bool arrows)
    {
        int n = pts.Length / 2;
        if (n < 2) return;
        float dist = 0;
        for (int k = 0; k < n; k++)
        {
            var p = P(pts, k, y);
            var q = k + 1 < n ? P(pts, k + 1, y) : p;
            var r = k > 0 ? P(pts, k - 1, y) : p;
            var dir = (q - p).Normalized() + (p - r).Normalized();
            dir = dir.LengthSquared() < 1e-6f ? (q - r).Normalized() : dir.Normalized();
            var perp = new Vector3(-dir.Z, 0, dir.X);
            if (k > 0) dist += (p - r).Length();
            float s = strength * (old != null && Near(old, pts[2 * k], pts[2 * k + 1]) ? 0.4f : 1f);
            int b = _v.Count;
            var col = new Color(colour, style);
            _v.Add(p); _n.Add(perp); _uv2.Add(new Vector2(halfPx, s)); _uv.Add(new Vector2(dist, 1)); _c.Add(col);
            _v.Add(p); _n.Add(-perp); _uv2.Add(new Vector2(halfPx, s)); _uv.Add(new Vector2(dist, -1)); _c.Add(col);
            if (k > 0) { _i.Add(b - 2); _i.Add(b - 1); _i.Add(b); _i.Add(b - 1); _i.Add(b + 1); _i.Add(b); }
        }
        if (!arrows) return;
        // An arrow head 8 m in, then every 20 m, and one at the end: direction never relies on colour.
        float acc = 0, next = 8;
        for (int k = 1; k < n; k++)
        {
            var a = P(pts, k - 1, y);
            var bb = P(pts, k, y);
            float seg = (bb - a).Length();
            if (seg < 1e-4f) continue;
            var d = (bb - a) / seg;
            var pp = new Vector3(-d.Z, 0, d.X);
            while (acc + seg >= next)
            {
                var at = a + d * (next - acc);
                float s = strength * (old != null && Near(old, at.X / _cell, at.Z / _cell) ? 0.4f : 1f);
                Arrow(at, d, pp, colour, halfPx, y + 0.01f, s);
                next += 20;
            }
            acc += seg;
        }
    }

    private Vector3 P(float[] pts, int k, float y) => new(pts[2 * k] * _cell, y, pts[2 * k + 1] * _cell);

    private static bool Near(float[] old, float x, float y)
    {
        for (int j = 0; j + 1 < old.Length; j += 2)
        {
            float dx = old[j] - x, dy = old[j + 1] - y;
            if (dx * dx + dy * dy < 1.2f) return true;
        }
        return false;
    }

    private void Arrow(Vector3 at, Vector3 d, Vector3 pp, Color colour, float halfPx, float y, float strength)
    {
        float l = halfPx * 2.4f + 2f, w = halfPx * 1.6f + 1.5f;
        at.Y = y;
        int b = _v.Count;
        Span<Vector3> offs = stackalloc Vector3[] { d * l, -d * l * 0.35f + pp * w, -d * l * 0.35f - pp * w };
        foreach (var o in offs)
        {
            _v.Add(at); _n.Add(o.Normalized()); _uv2.Add(new Vector2(o.Length(), strength)); _uv.Add(Vector2.Zero); _c.Add(new Color(Palette.Ink, 0f));
        }
        _i.Add(b); _i.Add(b + 1); _i.Add(b + 2);
        // The head in ink with the route's colour inside, so it reads on any ground.
        b = _v.Count;
        foreach (var o in offs)
        {
            var q = o * 0.6f;
            _v.Add(at + new Vector3(0, 0.005f, 0)); _n.Add(q.Normalized()); _uv2.Add(new Vector2(q.Length(), strength)); _uv.Add(Vector2.Zero); _c.Add(new Color(colour, 0f));
        }
        _i.Add(b); _i.Add(b + 1); _i.Add(b + 2);
    }

    private void Commit()
    {
        if (_v.Count == 0) { Mesh = null; return; }
        var arr = new Godot.Collections.Array();
        arr.Resize((int)Mesh.ArrayType.Max);
        arr[(int)Mesh.ArrayType.Vertex] = _v.ToArray();
        arr[(int)Mesh.ArrayType.Normal] = _n.ToArray();
        arr[(int)Mesh.ArrayType.TexUV] = _uv.ToArray();
        arr[(int)Mesh.ArrayType.TexUV2] = _uv2.ToArray();
        arr[(int)Mesh.ArrayType.Color] = _c.ToArray();
        arr[(int)Mesh.ArrayType.Index] = _i.ToArray();
        var m = new ArrayMesh();
        m.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arr);
        m.CustomAabb = new Aabb(new Vector3(-20, -1, -20), new Vector3(300, 4, 300));
        Mesh = m;
    }
}

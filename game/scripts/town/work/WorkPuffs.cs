using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 5: the places' small pooled effects, in map metres: flour puffs at the mill's door and chimney puffs (soft,
/// translucent spheres, so the ink pass draws no line round them, as on the board), and splash rings at the well
/// (thin translucent rings that widen and fade). Two MultiMeshes, a fixed pool; full means a new one is skipped.
/// </summary>
public partial class WorkPuffs : Node3D
{
    public const int Cap = 48;
    private struct P { public bool Ring; public Vector3 Pos, Vel; public float Age, Life, Size, Grow, Delay; public Color C; }
    private readonly P[] _p = new P[Cap];
    private int _n;
    private MultiMesh _puffs = null!, _rings = null!;
    public int Live => _n;
    public int Started { get; private set; }

    public override void _Ready()
    {
        Name = "Puffs";
        // the meshes, materials and MultiMeshes are made once for the process (static; see WellWorks); one pool is
        // drawn at a time (a new scene takes them over from the one it replaces)
        _puffs = Made[0] ??= Make(new SphereMesh { Radius = 0.5f, Height = 0.8f, RadialSegments = 10, Rings = 5 });
        _rings = Made[1] ??= Make(new TorusMesh { InnerRadius = 0.82f, OuterRadius = 1f, Rings = 20, RingSegments = 3 });
        AddChild(new MultiMeshInstance3D { Name = "Flour", Multimesh = _puffs, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddChild(new MultiMeshInstance3D { Name = "Splash", Multimesh = _rings, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Clear();
    }

    private static readonly MultiMesh?[] Made = new MultiMesh?[2];

    private static MultiMesh Make(Mesh mesh)
    {
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, AlbedoColor = Colors.White,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        mesh.SurfaceSetMaterial(0, mat);
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = Cap, VisibleInstanceCount = 0 };
        return mm;
    }

    public void Clear() { _n = 0; if (_puffs != null) { _puffs.VisibleInstanceCount = 0; _rings.VisibleInstanceCount = 0; } }

    /// <summary>A soft puff that rises from <paramref name="at"/> and swells and fades over <paramref name="life"/> seconds.</summary>
    public void Puff(Vector3 at, Vector3 vel, float size, float life, Color c, float delay = 0f)
    {
        if (_n >= Cap) return;
        Started++;
        _p[_n++] = new P { Pos = at, Vel = vel, Size = size, Life = life, C = c, Delay = delay };
    }

    /// <summary>A ring on the ground or water that widens from <paramref name="size"/> by <paramref name="grow"/> and fades.</summary>
    public void Ring(Vector3 at, float size, float grow, float life, Color c, float delay = 0f)
    {
        if (_n >= Cap) return;
        Started++;
        _p[_n++] = new P { Ring = true, Pos = at, Size = size, Grow = grow, Life = life, C = c, Delay = delay };
    }

    public override void _Process(double delta)
    {
        float dt = (float)Math.Min(delta, 0.1);
        int w = 0, np = 0, nr = 0;
        for (int i = 0; i < _n; i++)
        {
            var p = _p[i];
            if (p.Delay > 0) { p.Delay -= dt; _p[w++] = p; continue; }
            p.Age += dt;
            if (p.Age >= p.Life) continue;
            float u = p.Age / p.Life;
            p.Pos += p.Vel * dt;
            p.Vel *= 1f - Math.Min(1f, dt * 1.2f);
            if (p.Ring)
            {
                float r = p.Size + p.Grow * (1f - (1f - u) * (1f - u));
                _rings.SetInstanceTransform(nr, new Transform3D(Basis.Identity.Scaled(new Vector3(r, 0.6f, r)), p.Pos));
                _rings.SetInstanceColor(nr++, new Color(p.C, (1f - u) * 0.8f * Math.Min(1f, u * 8f)));
            }
            else
            {
                // swells quickly, then grows slowly as it thins away
                float s = p.Size * (0.35f + 0.65f * Math.Min(1f, u * 4f) + 0.5f * u);
                _puffs.SetInstanceTransform(np, new Transform3D(Basis.Identity.Scaled(new Vector3(s, s * 0.8f, s)), p.Pos));
                _puffs.SetInstanceColor(np++, new Color(p.C, p.C.A * (1f - u) * Math.Min(1f, u * 6f)));
            }
            _p[w++] = p;
        }
        _n = w;
        _puffs.VisibleInstanceCount = np;
        _rings.VisibleInstanceCount = nr;
    }
}

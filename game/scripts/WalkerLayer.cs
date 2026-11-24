using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Every villager in ONE MultiMesh (rendering proof 3). Each frame the walkers that are out on the map are
/// written into a pre-sized float buffer (12 transform floats and 4 custom floats per instance) and sent in one
/// call; walkers inside a building are simply not written. Positions are interpolated between the previous and
/// the current tick with the driver's alpha, facing follows the step direction, and the bob and sway are done in
/// the vertex shader from per-instance phase data and an animation clock that stops while paused.
/// The villager draws in the transparent pass, so the screen-space ink pass does not bury it; a thin inverted
/// hull (walker_hull.gdshader) inks it instead, about 1.2 px wide at any zoom.
/// </summary>
public partial class WalkerLayer : MultiMeshInstance3D
{
    private const int Stride = 16;
    public int Capacity { get; private set; }
    /// <summary>Instance scale (the mesh is already drawn 1.5x life size). The camera sets it from the zoom.</summary>
    public float WalkerScale = 1f;
    /// <summary>Walkers drawn in the last sync.</summary>
    public int Drawn { get; private set; }

    private SimDriver _sim = null!;
    private float[] _buf = Array.Empty<float>();
    private float[] _yaw = Array.Empty<float>();
    private ShaderMaterial _mat = null!, _hull = null!;
    private float _metresPerPx = 0.2f;
    private Rid _rid;

    private static readonly StringName AnimTime = new("anim_time"), HullW = new("hull_w");

    public void Init(SimDriver sim)
    {
        _sim = sim;
        Name = "Walkers";
        Capacity = sim.World.Params.MaxWalkers;
        _buf = new float[Capacity * Stride];
        _yaw = new float[Capacity];
        var src = ModelLib.Mesh("villager");
        // A private copy of the merged villager mesh with the walker shaders instead of the standard material.
        var mesh = (ArrayMesh)src.Duplicate();
        _mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/walker.gdshader") };
        _mat.SetShaderParameter("atlas", ((StandardMaterial3D)src.SurfaceGetMaterial(0)).AlbedoTexture);
        _hull = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/walker_hull.gdshader") };
        _hull.SetShaderParameter("ink", Palette.Ink);
        _mat.NextPass = _hull;
        mesh.SurfaceSetMaterial(0, _mat);
        Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,       // always 4 floats per instance in Godot 4.7
            InstanceCount = Capacity,   // set once; only VisibleInstanceCount changes
            Mesh = mesh,
        };
        Multimesh.VisibleInstanceCount = 0;
        _rid = Multimesh.GetRid();
        CastShadow = ShadowCastingSetting.Off; // 500 shadow casters cost more than they show
        // Instances span the whole map but the buffer upload does not grow the node's bounds: never cull it.
        CustomAabb = new Aabb(new Vector3(-8, -4, -8), new Vector3(sim.World.Grid.Width * 2f + 16, 16, sim.World.Grid.Height * 2f + 16));
    }

    /// <summary>Ink width follows the zoom so it stays about 1.2 screen pixels.</summary>
    public void SetMetresPerPixel(float metresPerPx) => _metresPerPx = metresPerPx;

    public override void _Process(double delta) => Sync();

    /// <summary>Writes every visible walker into the buffer and uploads it. Allocation-free.</summary>
    public void Sync()
    {
        var world = _sim.World;
        var ws = world.Walkers;
        float cell = (float)world.Params.CellMeters;
        float alpha = _sim.Alpha;
        float s = WalkerScale;
        int n = 0;
        for (int i = 0; i < ws.Length && n < Capacity; i++)
        {
            ref readonly Walker k = ref ws[i];
            if (!k.Visible) continue;
            float dx = (float)(k.X - k.Px), dz = (float)(k.Y - k.Py);
            float x = (float)(k.Px + dx * alpha) * cell, z = (float)(k.Py + dz * alpha) * cell;
            bool moving = dx * dx + dz * dz > 1e-8f;
            if (moving) _yaw[i] = MathF.Atan2(dx, dz); // the model faces +Z at yaw 0
            float c = MathF.Cos(_yaw[i]) * s, sn = MathF.Sin(_yaw[i]) * s;
            int o = n * Stride;
            _buf[o + 0] = c;   _buf[o + 1] = 0; _buf[o + 2] = sn;  _buf[o + 3] = x;
            _buf[o + 4] = 0;   _buf[o + 5] = s; _buf[o + 6] = 0;   _buf[o + 7] = 0;
            _buf[o + 8] = -sn; _buf[o + 9] = 0; _buf[o + 10] = c;  _buf[o + 11] = z;
            _buf[o + 12] = (k.Id * 0.6180339f) % 1f;  // stride phase, golden-ratio spread so neighbours differ
            _buf[o + 13] = 1.6f + (k.Id % 5) * 0.08f; // strides per second
            _buf[o + 14] = moving ? 0.12f : 0f;       // bob height in metres before scale
            _buf[o + 15] = 0;
            n++;
        }
        Drawn = n;
        Multimesh.VisibleInstanceCount = n;
        RenderingServer.MultimeshSetBuffer(_rid, _buf);
        float t = (float)_sim.AnimTime;
        _mat.SetShaderParameter(AnimTime, t);
        _hull.SetShaderParameter(AnimTime, t);
        _hull.SetShaderParameter(HullW, 1.2f * _metresPerPx / Math.Max(0.01f, s));
    }

    /// <summary>World-independent position of drawn instance i (map-local metres), for tests.</summary>
    public Vector3 InstancePosition(int i) => new(_buf[i * Stride + 3], 0, _buf[i * Stride + 11]);
}

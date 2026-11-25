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
    /// <summary>Deck height of the footbridge models above the water (assets/src/models/footbridge.py).</summary>
    private const float BridgeDeck = 0.6f;
    public int Capacity { get; private set; }
    /// <summary>Instance scale (the mesh is already drawn 1.5x life size). The camera sets it from the zoom.</summary>
    public float WalkerScale = 1f;
    /// <summary>Walkers drawn in the last sync.</summary>
    public int Drawn { get; private set; }

    private SimDriver _sim = null!;
    private CrossingLayer _crossings = null!;
    private float[] _buf = Array.Empty<float>();
    private float[] _yaw = Array.Empty<float>();
    private ShaderMaterial _mat = null!, _hull = null!;
    private MultiMeshInstance3D _shadows = null!;
    private Rid _shadowRid;
    private float _metresPerPx = 0.2f;
    private Rid _rid;

    private static readonly StringName AnimTime = new("anim_time"), HullW = new("hull_w");

    public void Init(SimDriver sim, CrossingLayer crossings)
    {
        _sim = sim;
        _crossings = crossings;
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
        // Outfits: six palette tunic colours and three hats or hoods, picked per villager (custom data w).
        // Mid-tone palette colours that stand out on green grass and pale earth alike; hats darker than the tunics or straw-pale.
        // No greens: a green tunic vanishes into the meadow at overview zoom.
        Color[] tunics = { Palette.Brick, Palette.River, Palette.Thatch, Palette.Limewash, Palette.Limewash.Lerp(Palette.River, 0.45f), Palette.Brick.Lerp(Palette.Thatch, 0.5f) };
        for (int i = 0; i < tunics.Length; i++) _mat.SetShaderParameter("tunic" + i, tunics[i]);
        // A dark hood, a straw hat or a soot-brown cap: the crown reads apart from the tunic from above, never as a hole.
        Color[] hats = { Palette.Mud, Palette.Thatch.Lerp(Palette.Paper, 0.25f), Palette.SootBrick };
        for (int i = 0; i < hats.Length; i++) _mat.SetShaderParameter("hat" + i, hats[i]);
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
        // A soft shadow disc under each villager: a second MultiMesh fed the same buffer, drawn just before them.
        var shadowMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/walker_shadow.gdshader"), RenderPriority = -1 };
        shadowMat.SetShaderParameter("ink", Palette.Ink);
        var disc = new PlaneMesh { Size = new Vector2(1.15f, 1.15f), Material = shadowMat, CenterOffset = new Vector3(0, 0.04f, 0) };
        var smm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, InstanceCount = Capacity, Mesh = disc,
        };
        smm.VisibleInstanceCount = 0;
        _shadows = new MultiMeshInstance3D { Name = "Shadows", Multimesh = smm, CastShadow = ShadowCastingSetting.Off };
        _shadowRid = smm.GetRid();
        CastShadow = ShadowCastingSetting.Off; // 500 shadow casters cost more than they show
        // Instances span the whole map but the buffer upload does not grow the node's bounds: never cull it.
        CustomAabb = new Aabb(new Vector3(-8, -4, -8), new Vector3(sim.World.Grid.Width * 2f + 16, 16, sim.World.Grid.Height * 2f + 16));
        _shadows.CustomAabb = CustomAabb;
        AddChild(_shadows);
    }

    /// <summary>Metres per screen pixel the ink hull is sized for (tests).</summary>
    public float MetresPerPixel => _metresPerPx;

    /// <summary>Ink width follows the zoom so it stays about 1.2 screen pixels.</summary>
    public void SetMetresPerPixel(float metresPerPx)
    {
        if (float.IsFinite(metresPerPx) && metresPerPx > 0) _metresPerPx = metresPerPx; // ignore a zero-height window
    }

    public override void _Process(double delta) => Sync();

    /// <summary>Writes every visible walker into the buffer and uploads it. Allocation-free.</summary>
    public void Sync()
    {
        var world = _sim.World;
        var ws = world.Walkers;
        var bridgeAt = world.BridgeAt;
        float cell = (float)world.Params.CellMeters;
        float alpha = _sim.Alpha;
        float s = WalkerScale;
        bool river = world.River != null;
        int n = 0;
        for (int i = 0; i < ws.Length && n < Capacity; i++)
        {
            ref readonly Walker k = ref ws[i];
            if (!k.Visible) continue;
            float dx = (float)(k.X - k.Px), dz = (float)(k.Y - k.Py);
            float x = (float)(k.Px + dx * alpha) * cell, z = (float)(k.Py + dz * alpha) * cell;
            bool moving = dx * dx + dz * dz > 1e-8f;
            if (moving) _yaw[i] = MathF.Atan2(dx, dz); // the model faces +Z at yaw 0
            // On a footbridge the walker stands on the deck, not in the water under it; a drawbridge deck is higher.
            float y = bridgeAt[k.Cell] >= 0 ? (river && EditLayer.IsDrawbridge(world, bridgeAt[k.Cell]) ? CrossingLayer.DrawDeck : BridgeDeck) : 0f;
            if (k.Hold == WalkerHold.Riding && _crossings.RaftAt(k.HoldAt, out var raft))
            {
                // Riders stand on the raft as drawn, spread over its deck (it is shorter than the stage-to-stage line).
                x = raft.X + ((k.Id * 0.618034f) % 1f - 0.5f) * 3.2f;
                z = raft.Z + ((k.Id * 0.754878f) % 1f - 0.5f) * 1.8f + 0.3f;
                y = CrossingLayer.RaftDeck;
                moving = false;
            }
            else if (k.Hold == WalkerHold.Waiting)
            {
                // A queue at a raised bridge or a ferry stage bunches up: each waiting walker steps a little off its
                // stopping point, in a loose crowd back from the water, more of them the longer they wait.
                float jx = (k.Id * 0.618034f) % 1f, jz = (k.Id * 0.754878f) % 1f - 0.5f;
                float spread = 1.4f + MathF.Min(1.2f, k.WaitT * 0.1f);
                // Always back from the water, never into it.
                float away = x / cell >= world.RiverX[Math.Clamp(k.Cell / world.Grid.Width, 0, world.Grid.Width - 1)] ? 1f : -1f;
                x += away * jx * spread * 1.6f;
                z += jz * spread * 2.2f;
                moving = false;
            }
            float c = MathF.Cos(_yaw[i]) * s, sn = MathF.Sin(_yaw[i]) * s;
            int o = n * Stride;
            _buf[o + 0] = c;   _buf[o + 1] = 0; _buf[o + 2] = sn;  _buf[o + 3] = x;
            _buf[o + 4] = 0;   _buf[o + 5] = s; _buf[o + 6] = 0;   _buf[o + 7] = y;
            _buf[o + 8] = -sn; _buf[o + 9] = 0; _buf[o + 10] = c;  _buf[o + 11] = z;
            _buf[o + 12] = (k.Id * 0.6180339f) % 1f;  // stride phase, golden-ratio spread so neighbours differ
            _buf[o + 13] = 1.6f + (k.Id % 5) * 0.08f; // strides per second
            _buf[o + 14] = moving ? 0.12f : 0f;       // bob height in metres before scale
            _buf[o + 15] = (k.Id * 0.7548777f + 0.31f) % 1f; // outfit: fixed per villager, neighbours differ
            n++;
        }
        Drawn = n;
        Multimesh.VisibleInstanceCount = n;
        RenderingServer.MultimeshSetBuffer(_rid, _buf);
        _shadows.Multimesh.VisibleInstanceCount = n;
        RenderingServer.MultimeshSetBuffer(_shadowRid, _buf);
        float t = (float)_sim.AnimTime;
        _mat.SetShaderParameter(AnimTime, t);
        _hull.SetShaderParameter(AnimTime, t);
        // About 1.2 px of ink up close, thinning to 0.8 px at overview zoom, where a villager is only 7 px across.
        float px = Mathf.Lerp(1.2f, 0.8f, Mathf.Clamp((_metresPerPx - 0.15f) / 0.25f, 0f, 1f));
        _hull.SetShaderParameter(HullW, px * _metresPerPx / Math.Max(0.01f, s));
    }

    /// <summary>World-independent position of drawn instance i (map-local metres), for tests.</summary>
    public Vector3 InstancePosition(int i) => new(_buf[i * Stride + 3], _buf[i * Stride + 7], _buf[i * Stride + 11]);
}

using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// The villagers at the simulation's positions: interpolated between the last two ticks, then eased so that nothing
/// pops (a queue moving up, a villager leaving a workplace) and snapped only after a jump of more than
/// <see cref="SnapCells"/> (a fast-forward). A walking villager faces the way it moves (never sideways); a waiting or
/// working one faces the simulation's heading; turns are smooth. Loads ride on the villager: the water yoke, the
/// grain sack or the flour sack. One MultiMesh for bodies and one per good.
/// Round 2: the body is drawn in one of a few light clothes colours (villager.gdshader tints the model's palette
/// shading), the head in the model's own colours, each villager on a soft round shadow; queueing and working villagers
/// stand where the simulation puts them: on the road, at a queue spot, or on their own work spot (rules v2).
/// </summary>
public partial class TownVillagers : Node3D
{
    public const int Capacity = 96;
    public const float Cell = TownGround.Cell;
    public const float SnapCells = 6f;
    public float WalkerScale = 1f;

    private TownDriver _drv = null!;
    private MultiMeshInstance3D _bodies = null!, _heads = null!, _shadows = null!;
    /// <summary>Light clothes colours, chosen by villager id.</summary>
    public static readonly Color[] Clothes =
    {
        // Round 3: stronger, so six villagers read as six people (still soft enough for the paper look).
        Color.FromHtml("#B8433A"), Color.FromHtml("#3F78B5"), Color.FromHtml("#D9A21E"),
        Color.FromHtml("#4E8F4A"), Color.FromHtml("#8A5AA8"), Color.FromHtml("#E8E0CC"),
    };
    /// <summary>The model's height in metres (before <see cref="WalkerScale"/>), for tests.</summary>
    public float ModelHeight { get; private set; } = 2.6f;
    private readonly MultiMeshInstance3D[] _loads = new MultiMeshInstance3D[4];
    private readonly int[] _loadCount = new int[4];
    private float[] _x = new float[Capacity], _y = new float[Capacity], _yaw = new float[Capacity], _phase = new float[Capacity];
    private bool[] _seen = new bool[Capacity];

    /// <summary>Villagers drawn, loads drawn, the largest jump of a drawn villager in the last frame in cells (tests).</summary>
    public int Drawn { get; private set; }
    public int LoadsDrawn { get; private set; }
    public float MaxStepCells { get; private set; }
    /// <summary>Largest angle in radians between a walking villager's facing and its motion this frame (tests: no sideways sliding).</summary>
    public float MaxSideways { get; private set; }
    public Vector2 Shown(int i) => new(_x[i], _y[i]);

    public static string LoadModel(Good g) => g switch
    {
        Good.Water => ModelLib.Pick("villager-cargo-water", "villager-cargo-cloth"),
        Good.Flour => ModelLib.Pick("villager-cargo-flour", "villager-cargo-grain"),
        _ => "villager-cargo-grain",
    };

    public void Init(TownDriver drv)
    {
        _drv = drv;
        Name = "Villagers";
        bool parts = ModelLib.HasPart("villager", "body") && ModelLib.HasPart("villager", "head");
        _bodies = ModelLib.Multi("villager", "Bodies", Capacity);
        if (parts)
        {
            var body = ModelLib.Part("villager", "body");
            var src = ModelLib.Mesh("villager").SurfaceGetMaterial(0) as StandardMaterial3D;
            var dyed = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/villager.gdshader") };
            dyed.SetShaderParameter("palette", src?.AlbedoTexture);
            body.SurfaceSetMaterial(0, dyed);
            _bodies.Multimesh.InstanceCount = 0;
            _bodies.Multimesh.UseColors = true;
            _bodies.Multimesh.Mesh = body;
            _bodies.Multimesh.InstanceCount = Capacity;
            for (int i = 0; i < Capacity; i++) _bodies.Multimesh.SetInstanceColor(i, Clothes[i % Clothes.Length]);
            _heads = new MultiMeshInstance3D
            {
                Name = "Heads",
                Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, InstanceCount = Capacity, Mesh = SoftInk(ModelLib.Part("villager", "head")) },
            };
            AddChild(_heads);
        }
        ModelHeight = Math.Max(0.5f, ModelLib.Mesh("villager").GetAabb().End.Y);
        AddChild(_bodies);
        _shadows = new MultiMeshInstance3D
        {
            Name = "Shadows",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, InstanceCount = Capacity, Mesh = ShadowDisc() },
        };
        AddChild(_shadows);
        for (int g = 1; g <= 3; g++)
        {
            _loads[g] = ModelLib.Multi(LoadModel((Good)g), "Load" + (Good)g, Capacity);
            AddChild(_loads[g]);
        }
        drv.Replaced += Reset;
    }

    public override void _ExitTree() { if (_drv != null) _drv.Replaced -= Reset; }

    public void Reset() => Array.Clear(_seen);

    public override void _Process(double delta)
    {
        var t = _drv.Town;
        float a = _drv.Alpha, dt = (float)Math.Max(delta, 1e-4);
        int n = Math.Min(t.VillagerCount, Capacity);
        float maxStep = 0, maxSide = 0;
        // Fast enough to follow any walker at the current speed, slow enough that a queue shuffle is a short walk.
        float limit = (float)(TownRules.BaseSpeed * 4.0 * Math.Max(1, _drv.Speed)) * dt;
        float turn = 10f * dt * (float)Math.Max(1, Math.Min(_drv.Speed, 8));
        Array.Clear(_loadCount);
        var bodies = _bodies.Multimesh;
        for (int i = 0; i < n; i++)
        {
            var v = t.Villager(i);
            float tx = (float)(v.PrevX + (v.X - v.PrevX) * a), ty = (float)(v.PrevY + (v.Y - v.PrevY) * a);
            // Rules v2: the simulation stands workers on their own work spots and queuers on queue spots; draw them there.
            if (!_seen[i]) { _x[i] = tx; _y[i] = ty; _yaw[i] = HeadingYaw(v.Heading); _seen[i] = true; }
            float dx = tx - _x[i], dy = ty - _y[i], d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > SnapCells) { _x[i] = tx; _y[i] = ty; d = 0; }
            else if (d > limit) { _x[i] += dx / d * limit; _y[i] += dy / d * limit; d = limit; }
            else { _x[i] = tx; _y[i] = ty; }
            maxStep = Math.Max(maxStep, d);
            bool moving = d > 0.002f;
            float want = moving ? HeadingYaw(Math.Atan2(dy, dx)) : HeadingYaw(v.Heading);
            _yaw[i] = TurnToward(_yaw[i], want, moving ? Math.Max(turn, 0.5f) : turn);
            if (moving && v.Phase == VillagerPhase.Walking) maxSide = Math.Max(maxSide, MathF.Abs(Wrap(want - _yaw[i])));
            if (moving) _phase[i] += dt * 9f;
            float bob = moving ? MathF.Abs(MathF.Sin(_phase[i])) * 0.12f * WalkerScale : 0f;
            var basis = new Basis(Vector3.Up, _yaw[i]).Scaled(Vector3.One * WalkerScale);
            var pos = new Vector3(_x[i] * Cell, bob, _y[i] * Cell);
            var xf = new Transform3D(basis, pos);
            bodies.SetInstanceTransform(i, xf);
            _heads?.Multimesh.SetInstanceTransform(i, xf);
            _shadows.Multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * WalkerScale * (1f - bob * 0.6f)), new Vector3(pos.X, 0.04f, pos.Z)));
            if (v.Carry != Good.None && (int)v.Carry < 4)
            {
                int g = (int)v.Carry;
                // Round 3: loads bigger, so Water, Grain and Flour read at the normal view.
                float lift = v.Carry == Good.Water ? 2.05f : 2.7f;
                var lb = new Basis(Vector3.Up, _yaw[i]).Scaled(Vector3.One * WalkerScale * (v.Carry == Good.Water ? 1.45f : 1.7f));
                _loads[g].Multimesh.SetInstanceTransform(_loadCount[g]++, new Transform3D(lb, pos + new Vector3(0, lift * WalkerScale, 0)));
            }
        }
        bodies.VisibleInstanceCount = n;
        if (_heads != null) _heads.Multimesh.VisibleInstanceCount = n;
        _shadows.Multimesh.VisibleInstanceCount = n;
        int loads = 0;
        for (int g = 1; g <= 3; g++) { _loads[g].Multimesh.VisibleInstanceCount = _loadCount[g]; loads += _loadCount[g]; }
        Drawn = n;
        LoadsDrawn = loads;
        MaxStepCells = maxStep;
        MaxSideways = maxSide;
    }

    /// <summary>A copy of the mesh whose material marks it for soft ink (as trees are), so small figures stay light.</summary>
    private static ArrayMesh SoftInk(ArrayMesh src)
    {
        var mesh = (ArrayMesh)src.Duplicate();
        if (mesh.SurfaceGetMaterial(0) is StandardMaterial3D m)
        {
            var c = (StandardMaterial3D)m.Duplicate();
            c.StencilMode = BaseMaterial3D.StencilModeEnum.Custom;
            c.StencilFlags = 2;
            c.StencilCompare = BaseMaterial3D.StencilCompareEnum.Always;
            c.StencilReference = 1;
            mesh.SurfaceSetMaterial(0, c);
        }
        return mesh;
    }

    /// <summary>A soft round shadow: a flat disc, dark in the middle and clear at the rim (vertex alpha).</summary>
    private static ArrayMesh ShadowDisc()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int seg = 20;
        float r0 = 0.45f, r1 = 0.95f;
        var dark = new Color(0.10f, 0.09f, 0.06f, 0.34f);
        var clear = new Color(0.10f, 0.09f, 0.06f, 0f);
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * MathF.Tau / seg, a1 = (i + 1) * MathF.Tau / seg;
            var p0 = new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0));
            var p1 = new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1));
            st.SetColor(dark); st.AddVertex(Vector3.Zero); st.AddVertex(p1 * r0); st.AddVertex(p0 * r0);
            st.SetColor(dark); st.AddVertex(p0 * r0); st.AddVertex(p1 * r0);
            st.SetColor(clear); st.AddVertex(p1 * r1);
            st.SetColor(dark); st.AddVertex(p0 * r0);
            st.SetColor(clear); st.AddVertex(p1 * r1); st.AddVertex(p0 * r1);
        }
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = false,
        });
        return mesh;
    }

    /// <summary>Yaw turning the model's front (+Z) along a map heading (atan2(dy, dx), y south).</summary>
    public static float HeadingYaw(double h) => MathF.PI / 2f - (float)h;

    private static float Wrap(float a)
    {
        while (a > MathF.PI) a -= MathF.PI * 2f;
        while (a < -MathF.PI) a += MathF.PI * 2f;
        return a;
    }

    private static float TurnToward(float from, float to, float step)
    {
        float d = Wrap(to - from);
        if (MathF.Abs(d) <= step) return to;
        return from + MathF.Sign(d) * step;
    }
}

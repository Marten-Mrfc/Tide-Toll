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
/// n5 (work you can watch): those who wait are drawn in a line along their lane (TownVillagers.Line.cs), workers at the
/// drawing's work spots in their job's pose and rhythm (TownVillagers.Work.cs), loads taken up and set down, carts
/// parked, tools in the hand (TownVillagers.Props.cs); places answer through WorkSites.cs.
/// </summary>
public partial class TownVillagers : Node3D
{
    /// <summary>n7: up to 200 villagers drawn (was 96; the second age can pass it).</summary>
    public const int Capacity = 200;
    public const float Cell = TownGround.Cell;
    public const float SnapCells = 6f;
    public float WalkerScale = 1f;

    private TownDriver _drv = null!;
    private MultiMeshInstance3D _bodies = null!, _heads = null!, _shadows = null!, _frames = null!, _carts = null!;
    private readonly MultiMeshInstance3D?[] _headKinds = new MultiMeshInstance3D?[3];
    private readonly int[] _headCount = new int[3];
    /// <summary>Night 3: the five tunic colours of the style sheet (section 2), chosen per villager by <see cref="TunicOf"/>.</summary>
    public static readonly Color[] Clothes = { Palette.Brick, Palette.River, Palette.CastIronGreen, Palette.Thatch, Palette.SootBrick };
    /// <summary>The three heads: hair, a pale hood, a straw hat (villager.glb's head, then the two head files).</summary>
    public static readonly string[] HeadModels = { "villager", "villager-head-hood", "villager-head-hat" };
    /// <summary>A villager's head (0 hair, 1 hood, 2 hat) and tunic colour, from its index: the same town always looks the same.</summary>
    public static int HeadOf(int i) => i % 3;
    public static Color TunicOf(int i) => Clothes[(i + i / 3) % Clothes.Length];
    private readonly float[] _lean = new float[Capacity];
    private long _lastTick = -1;
    /// <summary>The model's height in metres (before <see cref="WalkerScale"/>), for tests.</summary>
    public float ModelHeight { get; private set; } = 2.6f;
    private readonly MultiMeshInstance3D[] _loads = new MultiMeshInstance3D[6];
    private readonly int[] _loadCount = new int[6];
    private float[] _x = new float[Capacity], _y = new float[Capacity], _yaw = new float[Capacity], _phase = new float[Capacity];
    private bool[] _seen = new bool[Capacity];
    // Round 6 (W3): drawn-only offsets so people never stand inside each other; eased, in cells.
    private readonly float[] _ox = new float[Capacity], _oy = new float[Capacity], _px = new float[Capacity]; // _px: this frame's bob
    /// <summary>Spacing kept between drawn villagers, in metres per unit of <see cref="WalkerScale"/>.</summary>
    public const float Personal = 1.0f;

    /// <summary>Villagers drawn, loads drawn, the largest jump of a drawn villager in the last frame in cells (tests).</summary>
    public int Drawn { get; private set; }
    public int LoadsDrawn { get; private set; }
    public float MaxStepCells { get; private set; }
    /// <summary>Largest angle in radians between a walking villager's facing and its motion this frame (tests: no sideways sliding).</summary>
    public float MaxSideways { get; private set; }
    /// <summary>Smallest distance in metres between two drawn villagers who stand still (queued or working) this frame (tests: W3).</summary>
    public float MinStandingGap { get; private set; } = float.MaxValue;
    public Vector2 Shown(int i) => new(_x[i], _y[i]);

    public static string LoadModel(Good g) => g switch
    {
        Good.Water => ModelLib.Pick("villager-cargo-water", "villager-cargo-cloth"),
        Good.Flour => ModelLib.Pick("villager-cargo-flour", "villager-cargo-grain"),
        Good.Wool => ModelLib.Pick("villager-cargo-wool", "villager-cargo-grain"),
        Good.Cloth => ModelLib.Pick("villager-cargo-cloth", "villager-cargo-flour"),
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
            var body = DyedBody();
            _bodies.Multimesh.InstanceCount = 0;
            _bodies.Multimesh.UseColors = true;
            _bodies.Multimesh.Mesh = body;
            _bodies.Multimesh.InstanceCount = Capacity;
            for (int i = 0; i < Capacity; i++) _bodies.Multimesh.SetInstanceColor(i, TunicOf(i));
            for (int h = 0; h < 3; h++)
            {
                string hm = ModelLib.Has(HeadModels[h]) && ModelLib.HasPart(HeadModels[h], "head") ? HeadModels[h] : "villager";
                _headKinds[h] = new MultiMeshInstance3D
                {
                    Name = h == 0 ? "Heads" : "Heads" + h,
                    Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, InstanceCount = Capacity, Mesh = SoftInk(ModelLib.Part(hm, "head")) },
                };
                AddChild(_headKinds[h]);
            }
            _heads = _headKinds[0]!;
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
        for (int g = 1; g <= 5; g++)
        {
            _loads[g] = ModelLib.Multi(LoadModel((Good)g), "Load" + (Good)g, Capacity * 2);
            AddChild(_loads[g]);
        }
        _frames = ModelLib.Multi(ModelLib.Pick("villager-packframe", "villager-cargo-timber"), "PackFrames", Capacity);
        _carts = ModelLib.Multi(ModelLib.Pick("villager-handcart", "villager-cargo-timber"), "Handcarts", Capacity);
        AddChild(_frames);
        AddChild(_carts);
        InitProps();
        InitRings();
        for (int w = 0; w < _lane.Length; w++) _lane[w] = -1;
        drv.Replaced += Reset;
    }

    public override void _ExitTree() { if (_drv != null) _drv.Replaced -= Reset; }

    private static ArrayMesh? _dyedBody;
    /// <summary>The body with the tunic shader (the tunic faces take the instance colour).</summary>
    public static ArrayMesh DyedBody()
    {
        if (_dyedBody != null) return _dyedBody;
        var body = (ArrayMesh)ModelLib.Part("villager", "body").Duplicate();
        var src = ModelLib.Mesh("villager").SurfaceGetMaterial(0) as StandardMaterial3D;
        var dyed = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/villager.gdshader") };
        if (src?.AlbedoTexture is { } palette) dyed.SetShaderParameter("palette", palette);
        body.SurfaceSetMaterial(0, dyed);
        return _dyedBody = body;
    }

    /// <summary>
    /// One villager as the game draws villager <paramref name="index"/> (tunic, head), carrying good g at a Load level,
    /// leaning as a walker (lean 1) or standing (0): for the asset audit's pictures.
    /// </summary>
    public static Node3D Figure(int index, float scale, Good g = Good.None, int level = 0, float lean = 0f, string? load = null)
    {
        var n = new Node3D { Name = "Figure" };
        var xf = new Transform3D((new Basis(Vector3.Right, 0.13f * lean)).Scaled(Vector3.One * scale), Vector3.Zero);
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = DyedBody(), InstanceCount = 1 };
        mm.SetInstanceTransform(0, xf);
        mm.SetInstanceColor(0, TunicOf(index));
        n.AddChild(new MultiMeshInstance3D { Name = "Body", Multimesh = mm });
        string hm = HeadModels[HeadOf(index)];
        if (!ModelLib.Has(hm)) hm = "villager";
        n.AddChild(new MeshInstance3D { Name = "Head", Mesh = ModelLib.Part(hm, "head"), Transform = xf });
        if (g == Good.None && load == null) return n;
        string lm = load ?? LoadModel(g);
        for (int k = 0; k < BundlesAt(level); k++)
            if (!(level >= 3 && g == Good.Water && k > 0))
                n.AddChild(new MeshInstance3D { Name = "Load" + k, Mesh = ModelLib.Mesh(lm), Transform = xf * LoadPlace(g, level, k) });
        if (FrameAt(level)) n.AddChild(new MeshInstance3D { Name = "Frame", Mesh = ModelLib.Mesh(ModelLib.Pick("villager-packframe", "villager-cargo-grain")), Transform = xf });
        if (CartAt(level)) n.AddChild(new MeshInstance3D { Name = "Cart", Mesh = ModelLib.Mesh(ModelLib.Pick("villager-handcart", "villager-cargo-grain")), Transform = xf });
        return n;
    }

    public void Reset()
    {
        Array.Clear(_seen); Array.Clear(_ox); Array.Clear(_oy); Array.Clear(_seenPose); Array.Clear(_inLine);
        Array.Clear(_fly); Array.Clear(_take); Array.Clear(_tip); Array.Fill(_lane, -1); _lastTick = -1;
        HoverId = -1; SelectedId = -1; _hoverA = 0f; _selA = 0f;
    }

    /// <summary>Catch-up rate of a drawn villager, cells per second of frame time (delta, not frame count); beyond SnapCells it snaps.</summary>
    public static float ChaseCellsPerSecond(double speed) => (float)(TownRules.BaseSpeed * 4.0 * Math.Max(1, speed));
    /// <summary>The longest catch-up after a jump, in seconds of frame time: what a test's wait must give the drawing.</summary>
    public static double LongestCatchUpSeconds(double speed) => SnapCells / ChaseCellsPerSecond(speed);

    public override void _Process(double delta)
    {
        var t = _drv.Town;
        float a = _drv.Alpha, dt = (float)Math.Max(delta, 1e-4);
        int n = Math.Min(t.VillagerCount, Capacity);
        float maxStep = 0, maxSide = 0;
        // Fast enough to follow any walker at the current speed, slow enough that a queue shuffle is a short walk.
        float limit = ChaseCellsPerSecond(_drv.Speed) * dt;
        float turn = 10f * dt * (float)Math.Max(1, Math.Min(_drv.Speed, 8));
        Array.Clear(_loadCount);
        Array.Clear(_headCount);
        int frames = 0, carts = 0, sickles = 0, scythes = 0;
        var bodies = _bodies.Multimesh;
        MaxPoseRate = 0;
        // n5: a stretch played without frames (a bot, a test) is a fast-forward: everyone is drawn afresh where they
        // are, with no taking up or setting down of loads the drawing never saw.
        long tick = t.Tick;
        if (_lastTick >= 0 && (tick < _lastTick || tick - _lastTick > 50 + (long)(TownRules.TickHz * Math.Max(1, _drv.Speed) * Math.Max(dt, 0.25f) * 3))) Array.Clear(_seen);
        _lastTick = tick;
        // n5: the lanes of the lines, the bell's heads up and the tempo.
        Lanes(t);
        WorkFrame(t, dt);
        for (int i = 0; i < n; i++)
        {
            var v = t.Villager(i);
            float tx = (float)(v.PrevX + (v.X - v.PrevX) * a), ty = (float)(v.PrevY + (v.Y - v.PrevY) * a);
            bool fresh = !_seen[i];
            if (fresh) { _x[i] = tx; _y[i] = ty; _yaw[i] = HeadingYaw(v.Heading); _seen[i] = true; }
            Transitions(t, i, v, fresh);
            // n5: those who wait stand in a line along the lane, workers at their work spots (WorkSites); drawing only.
            float face = HeadingYaw(v.Heading);
            _stood[i] = StandTarget(t, i, v, dt, ref tx, ref ty, ref face);
            if (fresh) { _x[i] = tx; _y[i] = ty; _yaw[i] = face; }
            float dx = tx - _x[i], dy = ty - _y[i], d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > SnapCells) { _x[i] = tx; _y[i] = ty; d = 0; }
            else if (d > limit) { _x[i] += dx / d * limit; _y[i] += dy / d * limit; d = limit; }
            else { _x[i] = tx; _y[i] = ty; }
            maxStep = Math.Max(maxStep, d);
            bool moving = d > 0.002f;
            float want = moving ? HeadingYaw(Math.Atan2(dy, dx)) : face;
            _yaw[i] = TurnToward(_yaw[i], want, moving ? Math.Max(turn, 0.5f) : turn);
            if (moving && v.Phase == VillagerPhase.Walking) maxSide = Math.Max(maxSide, MathF.Abs(Wrap(want - _yaw[i])));
            if (moving) _phase[i] += dt * 9f;
            float bob = moving ? MathF.Abs(MathF.Sin(_phase[i])) * 0.12f * WalkerScale : 0f;
            _px[i] = bob;
            // Night 3: a walker leans into its step; standing, it straightens up (eased, so nothing snaps).
            _lean[i] += ((moving ? 1f : 0f) - _lean[i]) * Math.Min(1f, dt * 6f);
        }
        if (OldSpread) SpreadRound6(t, n, dt); else Spread(t, n, dt);
        for (int i = 0; i < n; i++)
        {
            var v = t.Villager(i);
            float bob = _px[i];
            Pose(t, i, v, dt, out float pitch, out float crouch, out float swing, out float fwd);
            // Lean forward and a small sway from side to side with the step, about the feet; the job's pose on top.
            float sway = MathF.Sin(_phase[i]) * 0.07f * _lean[i];
            var basis = new Basis(Vector3.Up, _yaw[i] + swing) * new Basis(Vector3.Right, 0.13f * _lean[i] + pitch) * new Basis(Vector3.Back, sway);
            if (crouch < 0.999f) basis *= Basis.FromScale(new Vector3(1f, crouch, 1f));
            basis = basis.Scaled(Vector3.One * WalkerScale);
            var pos = new Vector3((_x[i] + _ox[i]) * Cell, bob, (_y[i] + _oy[i]) * Cell);
            if (fwd != 0f) pos += new Vector3(MathF.Sin(_yaw[i]), 0, MathF.Cos(_yaw[i])) * fwd;
            var xf = new Transform3D(basis, pos);
            bodies.SetInstanceTransform(i, xf);
            int hk = HeadOf(i);
            if (_headKinds[hk] != null) _headKinds[hk]!.Multimesh.SetInstanceTransform(_headCount[hk]++, xf);
            _shadows.Multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * WalkerScale * (1f - bob * 0.6f)), new Vector3(pos.X, 0.04f, pos.Z)));
            // Night 3: loads are modelled in the body's frame (held in the arms, on the shoulder, a yoke on the
            // shoulders); each Load step shows: a second load on the back, a pack frame, a handcart. n5: taken up,
            // set down, parked at work, the tool in the hand (TownVillagers.Props.cs).
            DrawProps(t, i, v, xf, new Vector3(pos.X, 0, pos.Z), dt, ref frames, ref carts, ref sickles, ref scythes);
        }
        _frameOut = Math.Max(0f, _frameOut - dt * 3.5f);
        bodies.VisibleInstanceCount = n;
        for (int h = 0; h < 3; h++) if (_headKinds[h] != null) _headKinds[h]!.Multimesh.VisibleInstanceCount = _headCount[h];
        _frames.Multimesh.VisibleInstanceCount = frames;
        _carts.Multimesh.VisibleInstanceCount = carts;
        _sickles.Multimesh.VisibleInstanceCount = sickles;
        _scythes.Multimesh.VisibleInstanceCount = scythes;
        _shadows.Multimesh.VisibleInstanceCount = n;
        int loads = 0;
        for (int g = 1; g <= 5; g++) { _loads[g].Multimesh.VisibleInstanceCount = _loadCount[g]; loads += _loadCount[g]; }
        Drawn = n;
        RingFrame(dt, n);
        LoadsDrawn = loads;
        MaxStepCells = maxStep;
        MaxSideways = maxSide;
    }

    /// <summary>Round 6 (W6): how much a carried load holds at a Load level (0 none bought, 1 two loads, 2 pack frame, 3 handcart).</summary>
    public static float LoadScale(int level) => level <= 0 ? 1f : level == 1 ? 1.25f : level == 2 ? 1.4f : 1.75f;
    /// <summary>Loads drawn per carrying villager at a Load level: one in the arms, then two (on the back, on the frame, in the cart).</summary>
    public static int BundlesAt(int level) => level >= 1 ? 2 : 1;
    /// <summary>Whether a Load level shows the pack frame (step 3) or the handcart (step 4).</summary>
    public static bool FrameAt(int level) => level == 2;
    public static bool CartAt(int level) => level >= 3;

    private static readonly Basis Turned = new(Vector3.Up, MathF.PI);
    /// <summary>
    /// Night 3: where load k of a carrying villager sits, in the body's frame (the load models are built in it: a load
    /// in the arms, the log on the right shoulder, the yoke on the shoulders). Level 0: one load. Level 1: a second one
    /// turned onto the back. Level 2: the second one up on the pack frame's shelf. Level 3: both in the handcart.
    /// </summary>
    public static Transform3D LoadPlace(Good g, int level, int k)
    {
        if (level >= 3)
        {
            if (g == Good.Water)   // the pails stood in the cart along its length, the yoke laid over them
                return new Transform3D(new Basis(Vector3.Up, MathF.PI / 2f).Scaled(Vector3.One * 0.62f), new Vector3(0, 0.3f, 1.62f));
            return new Transform3D(Basis.Identity, new Vector3(k == 0 ? -0.25f : 0.25f, -0.14f, k == 0 ? 0.95f : 1.35f));
        }
        if (k == 0) return Transform3D.Identity;
        if (level == 2) return new Transform3D(Turned, new Vector3(0, 0.42f, -0.18f));
        if (g == Good.Water) return new Transform3D(new Basis(Vector3.Up, MathF.PI / 2f).Scaled(Vector3.One * 0.7f), new Vector3(0, 0.55f, -0.2f));
        // The second load on the back, low and a little to the side so the hat brim does not hide it.
        return new Transform3D(Turned, new Vector3(0.12f, -0.03f, -0.05f));
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

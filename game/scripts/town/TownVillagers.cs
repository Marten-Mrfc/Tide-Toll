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
/// </summary>
public partial class TownVillagers : Node3D
{
    public const int Capacity = 96;
    public const float Cell = TownGround.Cell;
    public const float SnapCells = 6f;
    public float WalkerScale = 1f;

    private TownDriver _drv = null!;
    private MultiMeshInstance3D _bodies = null!;
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
        _bodies = ModelLib.Multi("villager", "Bodies", Capacity);
        AddChild(_bodies);
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
            bodies.SetInstanceTransform(i, new Transform3D(basis, pos));
            if (v.Carry != Good.None && (int)v.Carry < 4)
            {
                int g = (int)v.Carry;
                float lift = v.Carry == Good.Water ? 1.95f : 2.45f;
                var lb = new Basis(Vector3.Up, _yaw[i]).Scaled(Vector3.One * WalkerScale * (v.Carry == Good.Water ? 1f : 1.2f));
                _loads[g].Multimesh.SetInstanceTransform(_loadCount[g]++, new Transform3D(lb, pos + new Vector3(0, lift * WalkerScale, 0)));
            }
        }
        bodies.VisibleInstanceCount = n;
        int loads = 0;
        for (int g = 1; g <= 3; g++) { _loads[g].Multimesh.VisibleInstanceCount = _loadCount[g]; loads += _loadCount[g]; }
        Drawn = n;
        LoadsDrawn = loads;
        MaxStepCells = maxStep;
        MaxSideways = maxSide;
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

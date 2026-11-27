using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 5 (build sheet C, "The mill"; D, the hoist): a doorway with a plank door that stands open while anyone is at
/// work inside or waits at the door and closes when the mill is idle; sails at an idle rate, faster the more places are
/// busy; a flour puff from the door at each unit finished; grain sacks on the left of the step, one for each who waits
/// (up to three); flour sacks on the right, one for each unit just ground, taken up by those who leave; the hoist on the
/// tower from the Tools step <see cref="HoistTools"/>, its sack riding up and down while the mill works. All pieces hang
/// under the mill's Body (TownBuildings), so they pop and sink with it; positions are the board's (AssetAudit.Work.cs).
/// </summary>
public partial class MillWorks : Node3D
{
    /// <summary>The Tools level ("Fine tools", the second milestone) from which the hoist stands on the mill.</summary>
    public const int HoistTools = 10;
    /// <summary>Sail speed (radians a second at game speed 1): idle, and added for each worker.</summary>
    public const float IdleRate = 0.25f, RatePerWorker = 0.55f;
    public const float DoorSeconds = 0.6f, SackSeconds = 0.35f;
    /// <summary>Seconds a flour sack stays on the step after its unit when nobody tells this place it was taken up.</summary>
    public const float FlourHold = 1.1f;
    private const float OpenYaw = 1.89f;

    private WorkPlaces _w = null!;
    private int _plot = -1;
    private Node3D? _body, _door, _hoist, _hoistSack;
    private readonly MeshInstance3D?[] _grain = new MeshInstance3D?[3], _flour = new MeshInstance3D?[3];
    private readonly float[] _grainS = new float[3], _flourS = new float[3], _flourHold = new float[3];
    private readonly bool[] _flourOn = new bool[3];
    private readonly int[] _flourAge = new int[3];
    private int _serial;
    private float _front, _sailAngle, _hoistS, _hoistBob;
    private static readonly Vector3[] GrainAt = { new(-2.1f, 0, 0.2f), new(-2.9f, 0, -0.3f), new(-2.45f, 0, 1.05f) };
    private static readonly float[] GrainYaw = { 0.3f, 1.9f, 3.0f };
    private static readonly Vector3[] FlourPos = { new(2.0f, 0, 0.3f), new(2.7f, 0, -0.3f), new(2.35f, 0, 1.0f) };
    private static readonly float[] FlourYaw = { 2.6f, 1.2f, 0.4f };

    /// <summary>The door's opening, 0 shut to 1 open (tests), and whether it should be open now.</summary>
    public float DoorOpen { get; private set; }
    public bool DoorWanted { get; private set; }
    /// <summary>The sails' rate now (radians a second at game speed 1; eased toward the busy places' rate).</summary>
    public float SailRate { get; private set; } = IdleRate;
    public float SailTarget { get; private set; } = IdleRate;
    public int GrainShown => (_grainS[0] > 0.5f ? 1 : 0) + (_grainS[1] > 0.5f ? 1 : 0) + (_grainS[2] > 0.5f ? 1 : 0);
    public int FlourShown => (_flourS[0] > 0.5f ? 1 : 0) + (_flourS[1] > 0.5f ? 1 : 0) + (_flourS[2] > 0.5f ? 1 : 0);
    public bool HoistShown => _hoist != null && _hoistS > 0.5f;
    public int PlotId => _plot;
    /// <summary>Largest per-frame change of the door's opening (tests).</summary>
    public float LastDoorChange { get; private set; }

    public MillWorks() { }
    public MillWorks(WorkPlaces w) { _w = w; Name = "MillWorks"; }

    public void Reset()
    {
        _plot = -1;
        var plots = _w.Town.Plan.Plots;
        for (int p = 0; p < plots.Length; p++) if (plots[p].Kind == PlotKind.Mill) { _plot = p; break; }
        _body = null; _door = null; _hoist = null; _hoistSack = null;
        for (int i = 0; i < 3; i++) { _grain[i] = _flour[i] = null; _grainS[i] = _flourS[i] = 0; _flourOn[i] = false; }
        DoorOpen = 0; SailRate = IdleRate; _hoistS = 0;
    }

    private static MeshInstance3D Put(Node3D parent, string model, Vector3 pos, float yaw, float scale = 1f)
    {
        var mi = new MeshInstance3D { Mesh = ModelLib.Mesh(model), Position = pos, Rotation = new Vector3(0, yaw, 0), Scale = Vector3.One * scale };
        parent.AddChild(mi);
        return mi;
    }

    private void Attach(Node3D body)
    {
        _body = body;
        var b = ModelLib.Mesh("mill").GetAabb();
        _front = b.End.Z;
        var holder = new Node3D { Name = "Work" };
        body.AddChild(holder);
        Put(holder, ModelLib.Pick("work-doorway", "work-door-leaf"), new Vector3(0, 0.22f, _front - 0.75f), 0f);
        _door = Put(holder, "work-door-leaf", new Vector3(0.6f, 0.22f, _front - 0.36f), MathF.PI + OpenYaw * DoorOpen);
        for (int i = 0; i < 3; i++)
        {
            _grain[i] = Put(holder, "work-sack-grain", GrainAt[i] + new Vector3(0, 0, _front), GrainYaw[i]);
            _flour[i] = Put(holder, "work-sack-flour", FlourPos[i] + new Vector3(0, 0, _front), FlourYaw[i]);
        }
        _hoist = Put(holder, "work-hoist", new Vector3(b.End.X - 1.2f, 6.2f, 1.2f), -0.55f);
        // (the hoist model carries its own rope and sack; a running rope is not drawn yet)
        Apply();
    }

    public void UnitFinished(int plot, int slot)
    {
        if (plot != _plot || _body == null) return;
        // a flour sack set on the step (the oldest gives way when three stand)
        int s = -1;
        for (int i = 0; i < 3; i++) if (!_flourOn[i] && _flourS[i] <= 0f) { s = i; break; }
        if (s < 0) { s = 0; for (int i = 1; i < 3; i++) if (_flourAge[i] < _flourAge[s]) s = i; }
        _flourOn[s] = true; _flourAge[s] = ++_serial; _flourHold[s] = -1f;
        // flour in the air at the door
        var c = new Color(Palette.Limewash, 0.85f);
        for (int i = 0; i < 4; i++)
        {
            var at = _w.OnPlot(_plot, new Vector3(0.1f + i * 0.35f, 2.6f + i * 0.25f, _front + 0.35f + i * 0.2f));
            _w.Puffs.Puff(at, new Vector3(0.25f, 0.55f, 0.2f), 0.9f - i * 0.12f, 1.1f, c, i * 0.08f);
        }
    }

    /// <summary>A flour sack was lifted by someone leaving (the oldest on the step).</summary>
    public void Taken(int plot)
    {
        if (plot != _plot) return;
        int s = -1;
        for (int i = 0; i < 3; i++) if (_flourOn[i] && _flourHold[i] < 0f && (s < 0 || _flourAge[i] < _flourAge[s])) s = i;
        if (s >= 0) _flourHold[s] = WorkPlaces.DetectFromSim ? FlourHold : 0f;
    }

    public Vector3 StepAt(int plot) => _w.OnPlot(plot, new Vector3(0, 0.22f, _front + 0.9f));
    public Vector3 DoorAt(int plot) => _w.OnPlot(plot, new Vector3(0, 0.22f, _front - 0.3f));
    public Vector3 FlourAt(int plot, int i) => _w.OnPlot(plot, FlourPos[Math.Clamp(i, 0, 2)] + new Vector3(0, 0, _front));

    public void Step(float dt)
    {
        if (_plot < 0) return;
        var body = _w.Buildings.BuiltBody(_plot);
        if (body != _body) { _body = null; if (body != null) Attach(body); }
        int wp = _w.WorkplaceOf(_plot);
        var t = _w.Town;
        int busy = 0, queue = 0;
        if (wp >= 0) { var v = t.Workplace(wp); if (v.Open) { busy = v.Busy; queue = v.QueueLength; } }
        DoorWanted = busy > 0 || queue > 0;
        float before = DoorOpen;
        DoorOpen = Mathf.MoveToward(DoorOpen, DoorWanted ? 1f : 0f, dt / DoorSeconds);
        LastDoorChange = MathF.Abs(DoorOpen - before);
        SailTarget = IdleRate + RatePerWorker * busy;
        SailRate = Mathf.MoveToward(SailRate, SailTarget, dt * RatePerWorker / WorkPlaces.MinEase);   // one worker more or less: over 0.15 s
        _sailAngle = (_sailAngle + dt * SailRate * _w.Pace) % (MathF.PI * 2f);
        if (_w.Buildings.MillSailsNode is { } sails) sails.Rotation = new Vector3(sails.Rotation.X, sails.Rotation.Y, _sailAngle);
        for (int i = 0; i < 3; i++)
        {
            _grainS[i] = Mathf.MoveToward(_grainS[i], i < Math.Min(3, queue) ? 1f : 0f, dt / SackSeconds);
            if (_flourOn[i] && _flourHold[i] >= 0f) { _flourHold[i] -= dt; if (_flourHold[i] <= 0f) _flourOn[i] = false; }
            _flourS[i] = Mathf.MoveToward(_flourS[i], _flourOn[i] ? 1f : 0f, dt / SackSeconds);
        }
        _hoistS = Mathf.MoveToward(_hoistS, t.ToolsLevel >= HoistTools ? 1f : 0f, dt / 0.6f);
        if (busy > 0) _hoistBob += dt * _w.Pace * 0.5f;
        if (_body != null) Apply();
    }

    private void Apply()
    {
        if (_door != null) _door.Rotation = new Vector3(0, MathF.PI + OpenYaw * WorkPlaces.Smooth(DoorOpen), 0);
        for (int i = 0; i < 3; i++)
        {
            Grow(_grain[i], _grainS[i]);
            Grow(_flour[i], _flourS[i]);
        }
        if (_hoist != null)
        {
            float h = WorkPlaces.Smooth(_hoistS);
            _hoist.Visible = h > 0.001f;
            _hoist.Scale = new Vector3(1f, Math.Max(0.001f, h), 1f);
            if (_hoistSack != null)
            {
                // the sack rides up to the pulley and down again while the mill works (the rope runs)
                float y = 2.4f + 2.2f * (0.5f - 0.5f * MathF.Cos(_hoistBob * MathF.PI));
                _hoistSack.Visible = h > 0.001f;
                _hoistSack.Position = new Vector3(_hoistSack.Position.X, y, _hoistSack.Position.Z);
                _hoistSack.Scale = Vector3.One * 0.75f * Math.Max(0.001f, h);
            }
        }
    }

    private static void Grow(Node3D? n, float s)
    {
        if (n == null) return;
        float e = WorkPlaces.Smooth(s);
        n.Visible = e > 0.001f;
        n.Scale = new Vector3(0.6f + 0.4f * e, Math.Max(0.001f, e), 0.6f + 0.4f * e);
    }
}

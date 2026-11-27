using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 5 (build sheet C, "The well"; D, the second rope): while anyone draws the crank turns and the rope winds the
/// bucket up the shaft; at each unit finished the bucket swings to the near rim and tips (a splash ring), the two pails
/// on the rim stand full until they are taken up, and the wet patch round the well darkens; idle, it dries slowly.
/// From the Tools step <see cref="SecondRopeTools"/> ("Sharp tools") a second rope and bucket hang from the windlass and
/// work half a turn behind the first. Pieces hang under the well's Body (TownBuildings); the board drew the well at 1.6
/// times its model, so its positions are divided by <see cref="BoardScale"/>.
/// </summary>
public partial class WellWorks : Node3D
{
    public const int SecondRopeTools = 5;
    public const float BoardScale = 1.6f, TipSeconds = 0.5f, PailHold = 0.9f;
    private const float Drum = 1.75f, Low = 0.35f, High = 1.32f;

    private WorkPlaces _w = null!;
    private int _plot = -1;
    private Node3D? _body;
    private readonly MeshInstance3D?[] _bucket = new MeshInstance3D?[2], _rope = new MeshInstance3D?[2], _pail = new MeshInstance3D?[2];
    private MeshInstance3D? _wet;
    private Node3D? _crank;
    private readonly float[] _h = new float[2], _rise = new float[2], _tip = { 1f, 1f };
    private float _crankA, _pailS, _pailHold = -1f, _second;
    private bool _pailOn;
    // The pails stand on the rim on the side toward the view (the map's south), whichever way the well is turned, so
    // they read at the normal view; the bucket tips over that rim too (set in Attach from the Body's turn).
    private readonly Vector3[] PailPos = new Vector3[2];
    private Vector3 _toView = Vector3.Back;
    private static readonly float[] PailYaw = { 0.4f, 1.2f };
    private StandardMaterial3D? _wood;   // not static: a Resource held past the scene is finalized after the engine is gone

    /// <summary>The wet ground's state, 0 dry to 1 soaked (tests), the bucket's drawn height share, pails shown, tips started.</summary>
    public float Wet { get; private set; }
    public float BucketHeight => _h[0];
    public bool PailsShown => _pailS > 0.5f;
    public int Tips { get; private set; }
    public bool SecondRope => _second > 0.5f;
    public float LastLargestChange { get; private set; }

    public WellWorks() { }
    public WellWorks(WorkPlaces w) { _w = w; Name = "WellWorks"; }

    public void Reset()
    {
        _plot = -1;
        var plots = _w.Town.Plan.Plots;
        for (int p = 0; p < plots.Length; p++) if (plots[p].Kind == PlotKind.Well) { _plot = p; break; }
        _body = null;
        Wet = 0.35f;   // a well that has stood a while: trodden, a little wet
        _pailS = 0; _pailOn = false;
        for (int i = 0; i < 2; i++) { _h[i] = 0; _rise[i] = 0; _tip[i] = 1f; _bucket[i] = _rope[i] = _pail[i] = null; }
    }

    private static MeshInstance3D Put(Node3D parent, Mesh mesh, Vector3 pos, float yaw, float scale)
    {
        var mi = new MeshInstance3D { Mesh = mesh, Position = pos, Rotation = new Vector3(0, yaw, 0), Scale = Vector3.One * scale };
        parent.AddChild(mi);
        return mi;
    }

    private void Attach(Node3D body)
    {
        _body = body;
        float yaw = body.Rotation.Y;
        _toView = new Vector3(-MathF.Sin(yaw), 0, MathF.Cos(yaw));
        for (int i = 0; i < 2; i++)
        {
            var d = _toView.Rotated(Vector3.Up, i == 0 ? -0.55f : 0.45f);
            PailPos[i] = d * 1.0f + new Vector3(0, 1.72f / BoardScale, 0);   // on the stone of the rim, not over the water
        }
        var holder = new Node3D { Name = "Work" };
        body.AddChild(holder);
        float k = 1f / BoardScale;
        _wet = Put(holder, ModelLib.Mesh("work-wet-patch"), new Vector3(0, -0.012f, 0), 0.25f, k);
        _wet.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        bool parts = ModelLib.HasPart("work-bucket", "Bucket") && ModelLib.HasPart("work-bucket", "Rope");
        for (int i = 0; i < 2; i++)
        {
            _bucket[i] = Put(holder, parts ? ModelLib.Part("work-bucket", "Bucket") : ModelLib.Mesh("work-bucket"), Vector3.Zero, 0.3f, 1.1f * k);
            _rope[i] = parts ? Put(holder, ModelLib.Part("work-bucket", "Rope"), Vector3.Zero, 0f, 1f) : null;
            _pail[i] = Put(holder, ModelLib.Mesh("work-pail"), PailPos[i], PailYaw[i], k);
        }
        // The crank: a short arm with a handle turning at the end of the windlass (+X), where the worker stands.
        _wood ??= new StandardMaterial3D { AlbedoColor = Palette.Mud, Roughness = 1f, SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled };
        _crank = new Node3D { Name = "Crank", Position = new Vector3(1.2f, Drum, 0) };
        holder.AddChild(_crank);
        _crank.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.1f, 0.1f, 0.42f), Material = _wood }, Position = new Vector3(0, 0, 0.2f) });
        _crank.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.09f, 0.09f), Material = _wood }, Position = new Vector3(0.12f, 0, 0.4f) });
        Apply();
    }

    public void UnitFinished(int plot, int slot)
    {
        if (plot != _plot || _body == null) return;
        int b = SecondRope && slot % 2 == 1 ? 1 : 0;
        if (_tip[b] >= 1f) { _tip[b] = 0f; Tips++; }
        _rise[b] = 0f;
        _pailOn = true; _pailHold = -1f;
        Wet = Math.Min(1f, Wet + 0.12f);
        var at = _w.OnPlot(_plot, new Vector3(b == 0 ? 0.15f : -0.35f, 1.05f, 0) + _toView * 0.55f);
        _w.Puffs.Ring(at, 0.25f, 0.9f, 0.7f, new Color(Palette.Limewash.Lerp(Palette.River, 0.3f), 1f));
        _w.Puffs.Ring(_w.OnPlot(_plot, new Vector3(0, 0.03f, 0) + _toView * 1.1f), 0.4f, 1.2f, 0.9f, new Color(Palette.River, 0.8f), 0.15f);
    }

    /// <summary>Someone began drawing: the two pails stand on the rim and fill as the bucket comes up.</summary>
    public void Started(int plot)
    {
        if (plot != _plot) return;
        _pailOn = true; _pailHold = -1f;
    }

    /// <summary>Where the yoke's two pails are lifted from: between the two pails on the rim.</summary>
    public Vector3 PailsMid(int plot) => (PailAt(plot, 0) + PailAt(plot, 1)) * 0.5f + new Vector3(0, 0.3f, 0);

    /// <summary>The full pails were taken up by the one leaving.</summary>
    public void Taken(int plot)
    {
        if (plot != _plot) return;
        _pailHold = WorkPlaces.DetectFromSim ? PailHold : 0f;
    }

    public Vector3 PailAt(int plot, int i) => _w.OnPlot(plot, PailPos[Math.Clamp(i, 0, 1)]);

    public Vector3 CrankSpot(int plot, int slot, out float yaw)
    {
        var at = _w.OnPlot(plot, new Vector3(slot % 2 == 0 ? 2.75f : -2.75f, 0, 0.9f) / BoardScale);
        var c = _w.OnPlot(plot, Vector3.Zero);
        yaw = MathF.Atan2(c.X - at.X, c.Z - at.Z);
        return new Vector3(at.X, 0, at.Z);
    }

    public void Step(float dt)
    {
        if (_plot < 0) return;
        var body = _w.Buildings.BuiltBody(_plot);
        if (body != _body) { _body = null; if (body != null) Attach(body); }
        int busy = _w.Busy(_plot);
        float game = dt * (float)(_w.Driver.Paused ? 0 : _w.Driver.Speed);
        float unit = (float)Math.Max(0.05, _w.UnitSeconds(PlotKind.Well));
        _second = Mathf.MoveToward(_second, _w.Town.ToolsLevel >= SecondRopeTools ? 1f : 0f, dt / 0.6f);
        float largest = 0f;
        for (int b = 0; b < 2; b++)
        {
            bool works = b == 0 ? busy > 0 : busy > 1 || (busy > 0 && SecondRope);
            if (works) _rise[b] = Math.Min(1f, _rise[b] + game / unit);
            if (_tip[b] < 1f) _tip[b] = Math.Min(1f, _tip[b] + dt / TipSeconds);
            float target = _tip[b] < 1f ? 1f : _rise[b];
            float before = _h[b];
            _h[b] = WorkPlaces.Ease(_h[b], target, dt, 0.35f);
            largest = Math.Max(largest, MathF.Abs(_h[b] - before));
        }
        if (busy > 0) _crankA = (_crankA + dt * _w.Pace * 4.5f) % (MathF.PI * 2f);
        if (_pailOn && _pailHold >= 0f) { _pailHold -= dt; if (_pailHold <= 0f) _pailOn = false; }
        _pailS = Mathf.MoveToward(_pailS, _pailOn ? 1f : 0f, dt / WorkSites.TakeSeconds);
        if (busy == 0) Wet = Math.Max(0.15f, Wet - dt * _w.Pace * 0.012f);
        LastLargestChange = largest;
        if (_body != null) Apply();
    }

    private void Apply()
    {
        float k = 1f / BoardScale, bs = 1.1f * k;
        for (int b = 0; b < 2; b++)
        {
            var bk = _bucket[b];
            if (bk == null) continue;
            float show = b == 0 ? 1f : WorkPlaces.Smooth(_second);
            bk.Visible = show > 0.001f;
            float swing = MathF.Sin(MathF.PI * _tip[b]);
            float y = Low + (High - Low) * WorkPlaces.Smooth(_h[b]);
            float x = b == 0 ? (SecondRope ? 0.35f : 0.05f) : -0.4f;
            var pos = new Vector3(x, y + swing * 0.12f, 0) + _toView * (swing * 0.55f);
            bk.Position = pos;
            // tips over the near rim: turned about the axis across the swing
            var axis = new Vector3(_toView.Z, 0, -_toView.X);
            bk.Basis = new Basis(axis, swing * 1.0f) * new Basis(Vector3.Up, 0.3f + b * 1.4f);
            bk.Scale = Vector3.One * bs * Math.Max(0.001f, show);
            if (_rope[b] is { } rope)
            {
                float top = y + 0.85f * bs;
                float sy = Math.Max(0.02f, (Drum - top) / 1.25f);
                rope.Visible = bk.Visible;
                rope.Scale = new Vector3(bs * show, sy, bs * show);
                rope.Position = new Vector3(pos.X * 0.6f + x * 0.4f, top - 0.85f * sy, pos.Z * 0.6f);
            }
        }
        if (_crank != null) _crank.Rotation = new Vector3(_crankA, 0, 0);
        float e = WorkPlaces.Smooth(_pailS);
        for (int i = 0; i < 2; i++)
            if (_pail[i] is { } p)
            {
                p.Visible = e > 0.001f;
                p.Scale = new Vector3(0.6f + 0.4f * e, Math.Max(0.001f, e), 0.6f + 0.4f * e) / BoardScale;
            }
        if (_wet != null)
        {
            // darker and wider with use; trodden earth only when dry (translucent: the ink draws no hard line round it)
            _wet.Transparency = Math.Clamp(0.6f - 0.6f * Wet, 0f, 0.95f);
            float s = (0.8f + 0.25f * Wet) * k;
            _wet.Scale = new Vector3(s, k, s);
        }
    }
}

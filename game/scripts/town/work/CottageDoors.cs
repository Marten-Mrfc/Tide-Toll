using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 5 (build sheet C, "A cottage"): the front door opens (eased) as one of the cottage's own people comes or goes
/// within a few steps of it and closes behind them; a load delivered home sends a puff from the chimney. The door is a
/// leaf in the cottage's own door colour hinged on its frame (cottage.py's door: 1 x 1.9 m), with the dark inside
/// showing behind it as it opens. Pieces hang in the cottage's own frame (TownBuildings' Cottage/Shift nodes), so they
/// follow its size and a cottage that grows gets its door again. The water butt is not drawn yet.
/// </summary>
public partial class CottageDoors : Node3D
{
    public const float DoorSeconds = 0.45f, Near = 2.6f, Hold = 0.5f, OpenYaw = 1.75f;
    private WorkPlaces _w = null!;
    private int[] _plots = Array.Empty<int>();
    private Node3D?[] _body = Array.Empty<Node3D?>(), _leaf = Array.Empty<Node3D?>(), _shift = Array.Empty<Node3D?>();
    private MeshInstance3D?[] _dark = Array.Empty<MeshInstance3D?>();
    private Vector3[] _door = Array.Empty<Vector3>(), _smoke = Array.Empty<Vector3>();
    private float[] _open = Array.Empty<float>(), _hold = Array.Empty<float>();
    // made once for the process (static; see WellWorks), one leaf mesh per door colour
    private static StandardMaterial3D? _inside;
    private static BoxMesh? _insideBox;
    private static readonly System.Collections.Generic.Dictionary<Material, BoxMesh> Leaves = new();

    public int Puffs { get; private set; }
    public float LastLargestChange { get; private set; }

    public CottageDoors() { }
    public CottageDoors(WorkPlaces w) { _w = w; Name = "CottageDoors"; }

    public void Reset()
    {
        var plots = _w.Town.Plan.Plots;
        int n = 0;
        for (int p = 0; p < plots.Length; p++) if (plots[p].Kind == PlotKind.Cottage) n++;
        _plots = new int[n];
        n = 0;
        for (int p = 0; p < plots.Length; p++) if (plots[p].Kind == PlotKind.Cottage) _plots[n++] = p;
        _body = new Node3D?[n]; _leaf = new Node3D?[n]; _shift = new Node3D?[n]; _dark = new MeshInstance3D?[n];
        _door = new Vector3[n]; _smoke = new Vector3[n]; _open = new float[n]; _hold = new float[n];
    }

    private int IndexOf(int plot)
    {
        for (int i = 0; i < _plots.Length; i++) if (_plots[i] == plot) return i;
        return -1;
    }

    /// <summary>The door's place on the wall in the cottage model's frame, by variant (cottage.py: door(acc, m, x, z)).</summary>
    private static Vector3 DoorLocal(string model)
    {
        char v = model.Length > 8 && model.StartsWith("cottage-") ? model[8] : 'a';
        return v switch { 'b' => new Vector3(0.9f, 0.3f, 2.1f), 'c' => new Vector3(0f, 0.3f, 2.8f), _ => new Vector3(-0.4f, 0.3f, 2.2f) };
    }

    private void Attach(int i, Node3D body)
    {
        _body[i] = body;
        _leaf[i] = null; _shift[i] = null;
        if (body.GetChildCount() == 0 || body.GetChild(0).GetChildCount() == 0 || body.GetChild(0).GetChild(0) is not Node3D shift) return;
        _shift[i] = shift;
        string model = _w.Buildings.DrawnModel(_plots[i]);
        var d = DoorLocal(model);
        Material? accent = (shift.GetNodeOrNull("Accent") as GeometryInstance3D)?.MaterialOverride;
        _inside ??= new StandardMaterial3D { AlbedoColor = Palette.Ink.Lerp(Palette.Mud, 0.35f), Roughness = 1f, SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled };
        _dark[i] = new MeshInstance3D { Name = "Inside", Mesh = _insideBox ??= new BoxMesh { Size = new Vector3(0.92f, 1.82f, 0.04f), Material = _inside }, Position = d + new Vector3(0, 0.93f, 0.16f), Visible = false };
        shift.AddChild(_dark[i]);
        var hinge = new Node3D { Name = "DoorLeaf", Position = d + new Vector3(-0.5f, 0f, 0.2f) };
        var colour = accent ?? _inside;
        if (!Leaves.TryGetValue(colour, out var leaf)) Leaves[colour] = leaf = new BoxMesh { Size = new Vector3(0.98f, 1.88f, 0.1f), Material = colour };
        hinge.AddChild(new MeshInstance3D { Mesh = leaf, Position = new Vector3(0.49f, 0.95f, 0) });
        shift.AddChild(hinge);
        _leaf[i] = hinge;
        if (shift.GetNodeOrNull<Node3D>("Smoke") is { } sm) _smoke[i] = sm.Position; else _smoke[i] = new Vector3(0, 6f, 0);
        Apply(i);
    }

    /// <summary>The door's middle on the threshold (map metres).</summary>
    public Vector3 DoorAt(int plot)
    {
        int i = IndexOf(plot);
        if (i < 0 || _shift[i] == null || !_shift[i]!.IsInsideTree()) { var p = _w.Town.Plan.Plots[plot]; return new Vector3((p.DoorX + 0.5f) * WorkPlaces.Cell, 0, (p.DoorY + 0.5f) * WorkPlaces.Cell); }
        var at = ToLocal(_shift[i]!.ToGlobal(DoorLocal(_w.Buildings.DrawnModel(plot)) + new Vector3(0, -0.3f, 0.5f)));
        return at;
    }

    /// <summary>A load came home: a puff from the chimney and the door opens to let it in.</summary>
    public void CameHome(int plot, Good good)
    {
        int i = IndexOf(plot);
        if (i < 0 || _shift[i] == null || !_shift[i]!.IsInsideTree()) return;
        _hold[i] = Math.Max(_hold[i], 1.2f);
        var at = ToLocal(_shift[i]!.ToGlobal(_smoke[i]));
        var c = new Color(Palette.Limewash.Lerp(Palette.FadedInk, 0.25f), 0.8f);
        _w.Puffs.Puff(at, new Vector3(0.3f, 1.1f, 0.1f), 1.1f, 1.6f, c);
        _w.Puffs.Puff(at + new Vector3(0.2f, 0.4f, 0), new Vector3(0.4f, 1.3f, 0.1f), 0.8f, 1.4f, c, 0.25f);
        Puffs++;
    }

    /// <summary>The opening of a cottage's door, 0 shut to 1 open (tests).</summary>
    public float OpenOf(int plot) { int i = IndexOf(plot); return i < 0 ? 0f : _open[i]; }
    /// <summary>Whether one of a cottage's people is near its door (tests: the door opens for them).</summary>
    public bool Wanted(int plot) { int i = IndexOf(plot); return i >= 0 && NearDoor(i); }

    private bool NearDoor(int i)
    {
        var t = _w.Town;
        var p = t.Plan.Plots[_plots[i]];
        double dx0 = p.DoorX + 0.5, dy0 = p.DoorY + 0.5;
        for (int v = 0; v < t.VillagerCount; v++)
        {
            var vv = t.Villager(v);
            if (vv.Home != _plots[i] || vv.Phase != VillagerPhase.Walking) continue;
            double dx = vv.X - dx0, dy = vv.Y - dy0;
            if (dx * dx + dy * dy < Near * Near) return true;
        }
        return false;
    }

    public void Step(float dt)
    {
        float largest = 0f;
        for (int i = 0; i < _plots.Length; i++)
        {
            var body = _w.Buildings.BuiltBody(_plots[i]);
            if (body != _body[i]) { _body[i] = null; if (body != null) Attach(i, body); }
            if (_leaf[i] == null) continue;
            if (NearDoor(i)) _hold[i] = Math.Max(_hold[i], Hold);
            _hold[i] = Math.Max(0f, _hold[i] - dt);
            float before = _open[i];
            _open[i] = Mathf.MoveToward(_open[i], _hold[i] > 0f ? 1f : 0f, dt / DoorSeconds);
            largest = Math.Max(largest, MathF.Abs(_open[i] - before));
            if (_open[i] != before) Apply(i);
        }
        LastLargestChange = largest;
    }

    private void Apply(int i)
    {
        float e = WorkPlaces.Smooth(_open[i]);
        if (_leaf[i] != null) _leaf[i]!.Rotation = new Vector3(0, -OpenYaw * e, 0);
        if (_dark[i] != null) _dark[i]!.Visible = e > 0.01f;
    }
}

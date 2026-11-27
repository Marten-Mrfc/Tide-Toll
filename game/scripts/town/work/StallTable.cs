using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 5 (build sheet C, "The market stall"): each load delivered to the market arrives on the table in a short arc
/// from the villager and stands there among the last deliveries, told by kind (water in a pot, grain and flour in their
/// sacks, wool and cloth in a crate; eight places), until it is sold some seconds later and sinks away (the coin hops as
/// now, TownFeel). The stall model's own goods stay, so a quiet table holds its few standing goods, as today. A
/// stallholder in an apron stands at the table's end and turns to whoever comes near.
/// </summary>
public partial class StallTable : Node3D
{
    public const int Places = 8, Pool = 40;
    public const float ArcSeconds = 0.45f, SellSeconds = 4.5f, LeaveSeconds = 0.4f, BoardScale = 1.5f;
    private const float TableY = 1.35f / BoardScale;

    private WorkPlaces _w = null!;
    private int _plot = -1;
    private Node3D? _body;
    private Node3D? _keeper;
    private float _keeperYaw;
    private struct Item { public bool On; public int Place; public Good Good; public Vector3 From; public float Age; public int Serial; }
    private readonly Item[] _it = new Item[Pool];
    private readonly MeshInstance3D?[] _nodes = new MeshInstance3D?[Pool];
    private int _serial;

    /// <summary>Loads delivered to the market, loads put on the table, loads that found no place (tests: none).</summary>
    public int Received { get; private set; }
    public int Shown { get; private set; }
    public int Missed { get; private set; }
    public int OnTable { get { int n = 0; for (int i = 0; i < Pool; i++) if (_it[i].On && _it[i].Age < ArcSeconds + SellSeconds) n++; return n; } }
    public float LastLargestMove { get; private set; }

    public StallTable() { }
    public StallTable(WorkPlaces w) { _w = w; Name = "StallTable"; }

    public void Reset()
    {
        _plot = -1;
        var plots = _w.Town.Plan.Plots;
        for (int p = 0; p < plots.Length; p++) if (plots[p].Kind == PlotKind.Market) { _plot = p; break; }
        _body = null;
        for (int i = 0; i < Pool; i++) { _it[i].On = false; if (_nodes[i] != null) _nodes[i]!.Visible = false; }
        if (_keeper != null) _keeper.Visible = false;
    }

    public override void _Ready()
    {
        for (int i = 0; i < Pool; i++)
        {
            _nodes[i] = new MeshInstance3D { Name = "Good" + i, Visible = false, Mesh = ModelLib.Mesh("work-pot") };
            AddChild(_nodes[i]);
        }
    }

    private static string ModelOf(Good g) => g switch
    {
        Good.Water => "work-pot", Good.Grain => "work-sack-grain", Good.Flour => "work-sack-flour", _ => "work-crate",
    };
    private static float ScaleOf(Good g) => g is Good.Grain or Good.Flour ? 0.72f : g == Good.Water ? 1f : 0.85f;

    /// <summary>A place on the table in the stall model's frame: two rows of four along the front.</summary>
    private Vector3 Local(int i)
    {
        var b = ModelLib.Mesh("market-stall").GetAabb();
        float front = b.End.Z;
        int row = i / 4, col = i % 4;
        float x = -1.35f + col * 0.9f + (row == 1 ? 0.45f : 0f) - 0.2f;
        return new Vector3(x, TableY, front - 0.62f - row * 0.42f);
    }

    public Vector3 PlaceAt(int plot, int i) => _w.OnPlot(plot, Local(Math.Clamp(i, 0, Places - 1)));
    public Vector3 EdgeAt(int plot)
    {
        var b = ModelLib.Mesh("market-stall").GetAabb();
        return _w.OnPlot(plot, new Vector3(0, TableY, b.End.Z - 0.25f));
    }

    /// <summary>The place the next load will take (the villagers aim their arc there).</summary>
    public int NextPlace()
    {
        for (int p = 0; p < Places; p++)
        {
            bool taken = false;
            for (int i = 0; i < Pool; i++) if (_it[i].On && _it[i].Place == p && _it[i].Age < ArcSeconds + SellSeconds) taken = true;
            if (!taken) return p;
        }
        int old = -1;
        for (int i = 0; i < Pool; i++) if (_it[i].On && _it[i].Age < ArcSeconds + SellSeconds && (old < 0 || _it[i].Serial < _it[old].Serial)) old = i;
        return old >= 0 ? _it[old].Place : 0;
    }

    /// <summary>Joined: a load has landed at the end of the villager's arc (aimed at <see cref="NextPlace"/>): it drops
    /// the last bit onto its place from just above, settling with a small bounce, so it reads at the normal view.</summary>
    public void Landed(int plot, Good good)
    {
        if (plot != _plot) return;
        var at = PlaceAt(plot, NextPlace());
        Delivered(plot, good, at + new Vector3(0, 0.9f, 0));
    }

    public void Delivered(int plot, Good good, Vector3 from)
    {
        if (plot != _plot) return;
        Received++;
        // a free place: the first not taken; when all eight are taken, the oldest is sold now and its place reused
        int place = -1;
        for (int p = 0; p < Places && place < 0; p++)
        {
            bool taken = false;
            for (int i = 0; i < Pool; i++) if (_it[i].On && _it[i].Place == p && _it[i].Age < ArcSeconds + SellSeconds) taken = true;
            if (!taken) place = p;
        }
        if (place < 0)
        {
            int old = -1;
            for (int i = 0; i < Pool; i++) if (_it[i].On && _it[i].Age < ArcSeconds + SellSeconds && (old < 0 || _it[i].Serial < _it[old].Serial)) old = i;
            if (old >= 0) { place = _it[old].Place; _it[old].Age = ArcSeconds + SellSeconds; }
        }
        int slot = -1;
        for (int i = 0; i < Pool; i++) if (!_it[i].On) { slot = i; break; }
        if (slot < 0)
        {
            // every node busy (deliveries faster than the table can sell, at the highest speeds): the one furthest gone
            // from the table is given up for the new load
            for (int i = 0; i < Pool; i++) if (_it[i].Age >= ArcSeconds + SellSeconds && (slot < 0 || _it[i].Age > _it[slot].Age)) slot = i;
        }
        if (slot < 0 || place < 0) { Missed++; return; }
        _it[slot] = new Item { On = true, Place = place, Good = good, From = from, Age = 0f, Serial = ++_serial };
        var n = _nodes[slot]!;
        n.Mesh = ModelLib.Mesh(ModelOf(good));
        Shown++;
    }

    public void Step(float dt)
    {
        if (_plot < 0) return;
        var body = _w.Buildings.BuiltBody(_plot);
        if (body != _body)
        {
            _body = body;
            if (body != null && _keeper == null)
            {
                _keeper = TownVillagers.Figure(7, 1f);
                _keeper.Name = "Stallholder";
                var apron = new MeshInstance3D { Mesh = ModelLib.Mesh(ModelLib.Pick("work-apron", "villager")) };
                if (ModelLib.Has("work-apron")) _keeper.AddChild(apron);
                AddChild(_keeper);
            }
        }
        bool shown = body != null && _w.Buildings.PlotDrawn(_plot);
        float bodyScale = body?.Scale.X ?? 1f, yaw = body?.Rotation.Y ?? 0f;
        float largest = 0f;
        for (int i = 0; i < Pool; i++)
        {
            ref var it = ref _it[i];
            var n = _nodes[i]!;
            if (!it.On || !shown) { n.Visible = false; if (!shown) it.On = false; continue; }
            it.Age += dt;
            if (it.Age >= ArcSeconds + SellSeconds + LeaveSeconds) { it.On = false; n.Visible = false; continue; }
            var to = _w.OnPlot(_plot, Local(it.Place));
            float s = bodyScale / BoardScale * ScaleOf(it.Good);
            Vector3 pos;
            float grow = 1f;
            if (it.Age < ArcSeconds)
            {
                // a short arc from the villager's arms onto the table
                float u = WorkPlaces.Smooth(it.Age / ArcSeconds);
                pos = it.From.Lerp(to, u) + new Vector3(0, MathF.Sin(MathF.PI * u) * 1.2f, 0);
                // grows to a little over its size as it lands and settles back (the eye catches the arrival)
                grow = 0.75f + 0.25f * u + 0.35f * MathF.Sin(MathF.PI * u);
            }
            else
            {
                pos = to;
                float sold = it.Age - ArcSeconds - SellSeconds;
                if (sold > 0f) { float u = WorkPlaces.Smooth(sold / LeaveSeconds); grow = 1f - u; pos.Y -= 0.2f * u; }
            }
            if (n.Visible) largest = Math.Max(largest, (n.Position - pos).Length());
            n.Visible = grow > 0.001f;
            n.Position = pos;
            n.Rotation = new Vector3(0, yaw + it.Place * 0.7f, 0);
            n.Scale = Vector3.One * s * Math.Max(0.001f, grow);
        }
        LastLargestMove = largest;
        if (_keeper != null)
        {
            _keeper.Visible = shown;
            if (shown)
            {
                var at = _w.OnPlot(_plot, new Vector3(-3.3f, 0, ModelLib.Mesh("market-stall").GetAabb().End.Z * BoardScale - 1.7f) / BoardScale);
                at.Y = 0;
                // turns to whoever comes near (the nearest villager within a few steps), else faces the table's front
                float want = yaw;
                var t = _w.Town;
                float best = 8f * WorkPlaces.Cell;
                for (int v = 0; v < t.VillagerCount; v++)
                {
                    var vv = t.Villager(v);
                    float dx = (float)vv.X * WorkPlaces.Cell - at.X, dz = (float)vv.Y * WorkPlaces.Cell - at.Z;
                    float d = MathF.Sqrt(dx * dx + dz * dz);
                    if (d < best && d > 0.5f) { best = d; want = MathF.Atan2(dx, dz); }
                }
                _keeperYaw += Mathf.Wrap(want - _keeperYaw, -MathF.PI, MathF.PI) * Math.Min(1f, dt * 4f);
                _keeper.Position = at;
                _keeper.Rotation = new Vector3(0, _keeperYaw, 0);
                _keeper.Scale = Vector3.One * _w.Main.Villagers.WalkerScale;
            }
        }
    }
}

using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// The town's plots drawn with the Hamlet models: cottages, the well, fields, the market stall, the mill (its sails
/// turn while it works) and the bell on its green (it swings when rung), and the charter stone at the end of the age.
/// A plot given by a level pops in (it rises from the ground with a small overshoot). A plot not given yet but laid
/// out by the next level is staked out (plot-stakes.glb); a building offer given but not bought is staked too.
/// New models load by name with a named stand-in when the file is not there (see <see cref="Model"/>).
/// </summary>
public partial class TownBuildings : Node3D
{
    public const float Cell = TownGround.Cell;
    public const float PopSeconds = 0.7f;

    private TownDriver _drv = null!;
    private Node3D?[] _built = Array.Empty<Node3D?>();
    private Node3D?[] _stakes = Array.Empty<Node3D?>();
    private float[] _pop = Array.Empty<float>();
    private Node3D?[] _extras = Array.Empty<Node3D?>();   // offers that add to a plot already drawn (the second bucket)
    private Node3D? _sails, _bell, _charter;
    private float _sailAngle, _bellSwing;

    /// <summary>Plots drawn as built, plots staked, pops running (tests).</summary>
    public int BuiltCount { get; private set; }
    public int StakedCount { get; private set; }
    public int Popping { get; private set; }
    private readonly float[] _extraPop = new float[8];
    public bool OfferDrawn(int o) => o < _extras.Length && _extras[o] != null;
    public bool PlotDrawn(int p) => _built[p] != null && _built[p]!.Visible;
    public bool PlotStaked(int p) => _stakes[p] != null && _stakes[p]!.Visible;
    public float SailAngle => _sailAngle;
    public float BellSwing => _bellSwing;
    public bool CharterShown => _charter != null && _charter.Visible;

    /// <summary>The model for a plot kind, with the stand-in used when the new file is missing.</summary>
    public static string Model(PlotKind kind, int id) => kind switch
    {
        PlotKind.Cottage => $"cottage-0{1 + id % 3}",
        PlotKind.Well => "well",
        PlotKind.Field => "field-ripe",
        PlotKind.Market => "market-stall",
        PlotKind.Mill => ModelLib.Pick("mill", "market-stall"),
        PlotKind.BellGreen => ModelLib.Pick("bell-post", "well"),
        _ => "well",
    };

    public void Init(TownDriver drv)
    {
        _drv = drv;
        Name = "Buildings";
        drv.Replaced += Rebuild;
        Rebuild();
    }

    public override void _ExitTree() { if (_drv != null) _drv.Replaced -= Rebuild; }

    public void Rebuild()
    {
        foreach (var c in GetChildren()) { RemoveChild(c); c.QueueFree(); }
        var plots = _drv.Town.Plan.Plots;
        _built = new Node3D?[plots.Length];
        _stakes = new Node3D?[plots.Length];
        _pop = new float[plots.Length];
        _extras = new Node3D?[_drv.Town.Plan.Offers.Length];
        _sails = _bell = _charter = null;
        Sync(instant: true);
    }

    private static Vector3 Centre(Plot p) => new((p.X + p.W / 2f) * Cell, 0, (p.Y + p.H / 2f) * Cell);

    /// <summary>Yaw that turns a model's front (+Z) toward the plot's door.</summary>
    private static float DoorYaw(Plot p)
    {
        float dx = p.DoorX + 0.5f - (p.X + p.W / 2f), dy = p.DoorY + 0.5f - (p.Y + p.H / 2f);
        if (MathF.Abs(dx) > MathF.Abs(dy)) return dx > 0 ? MathF.PI / 2f : -MathF.PI / 2f;
        return dy > 0 ? 0f : MathF.PI;
    }

    private Node3D MakeBuilt(Plot p)
    {
        string model = Model(p.Kind, p.Id);
        var root = new Node3D { Name = $"Plot{p.Id}", Position = Centre(p) };
        float fw = p.W * Cell, fh = p.H * Cell;
        float yaw = DoorYaw(p);
        bool side = MathF.Abs(MathF.Sin(yaw)) > 0.5f;
        var aabb = ModelLib.Mesh(model).GetAabb();
        float ax = Math.Max(0.1f, side ? aabb.Size.Z : aabb.Size.X), az = Math.Max(0.1f, side ? aabb.Size.X : aabb.Size.Z);
        var body = new Node3D { Name = "Body", Rotation = new Vector3(0, yaw, 0) };
        root.AddChild(body);
        if (p.Kind == PlotKind.Field)
        {
            // Fields fill their footprint (flat, so a non-uniform scale reads fine).
            body.AddChild(ModelLib.Instance(model, "Model"));
            if (side) body.Scale = new Vector3(fh / aabb.Size.X, 1f, fw / aabb.Size.Z) * 0.96f;
            else body.Scale = new Vector3(fw / aabb.Size.X, 1f, fh / aabb.Size.Z) * 0.96f;
            return root;
        }
        float s = Math.Min(fw / ax, fh / az) * 0.88f;
        s = Math.Min(s, 1.6f);
        body.Scale = Vector3.One * s;
        if (p.Kind == PlotKind.Mill && model == "mill" && ModelLib.HasPart("mill", "Sails") && ModelLib.HasPart("mill", "Body"))
        {
            body.AddChild(ModelLib.PartInstance("mill", "Body", "MillBody"));
            _sails = ModelLib.PartInstance("mill", "Sails", "Sails");
            body.AddChild(_sails);
        }
        else if (p.Kind == PlotKind.BellGreen && model == "bell-post" && ModelLib.HasPart("bell-post", "Bell") && ModelLib.HasPart("bell-post", "Frame"))
        {
            body.AddChild(ModelLib.PartInstance("bell-post", "Frame", "Frame"));
            _bell = ModelLib.PartInstance("bell-post", "Bell", "Bell");
            body.AddChild(_bell);
            string stone = ModelLib.Pick("charter-stone", "well");
            _charter = ModelLib.Instance(stone, "Charter");
            _charter.Position = new Vector3(fw * 0.3f / s, 0, fh * 0.25f / s);
            _charter.Visible = false;
            body.AddChild(_charter);
        }
        else body.AddChild(ModelLib.Instance(model, "Model"));
        return root;
    }

    private Node3D MakeStakes(Plot p)
    {
        string model = ModelLib.Pick("plot-stakes", "fence-post");
        var root = new Node3D { Name = $"Stakes{p.Id}", Position = Centre(p) };
        var m = ModelLib.Instance(model, "Model");
        var aabb = ModelLib.Mesh(model).GetAabb();
        m.Scale = new Vector3(p.W * Cell / Math.Max(0.1f, aabb.Size.X) * 0.9f, 1f, p.H * Cell / Math.Max(0.1f, aabb.Size.Z) * 0.9f);
        root.AddChild(m);
        return root;
    }

    /// <summary>Plot p should be drawn as a working or given building.</summary>
    private bool ShowBuilt(Plot p)
    {
        var t = _drv.Town;
        if (!t.PlotGiven(p.Id)) return false;
        return p.Offer < 0 || t.OfferBought(p.Offer);
    }

    private bool ShowStakes(Plot p)
    {
        var t = _drv.Town;
        if (ShowBuilt(p)) return false;
        if (t.PlotGiven(p.Id)) return true;   // an offer given but not bought: its empty plot
        return t.Level < t.MaxLevel && p.Level == t.Level + 1;
    }

    /// <summary>Brings the drawn plots in line with the town; new buildings pop in unless instant.</summary>
    public void Sync(bool instant)
    {
        var plots = _drv.Town.Plan.Plots;
        int built = 0, staked = 0;
        for (int i = 0; i < plots.Length; i++)
        {
            var p = plots[i];
            bool b = ShowBuilt(p);
            if (b && _built[i] == null)
            {
                _built[i] = MakeBuilt(p);
                AddChild(_built[i]);
                if (!instant) { _pop[i] = 0.0001f; _built[i]!.Scale = new Vector3(1, 0.02f, 1); }
            }
            if (_built[i] != null) _built[i]!.Visible = b;
            if (b) built++;
            bool st = ShowStakes(p);
            if (st && _stakes[i] == null) { _stakes[i] = MakeStakes(p); AddChild(_stakes[i]); }
            if (_stakes[i] != null) _stakes[i]!.Visible = st;
            if (st) staked++;
        }
        // An offer bought for a plot that is already standing (the well's second bucket): a piece beside it.
        var offers = _drv.Town.Plan.Offers;
        for (int o = 0; o < offers.Length; o++)
        {
            var of = offers[o];
            if (of.Plot < 0 || plots[of.Plot].Offer == o || !_drv.Town.OfferBought(o)) continue;
            built++;
            if (_extras[o] != null) continue;
            var p = plots[of.Plot];
            string model = ModelLib.Pick("villager-cargo-water", "well");
            var root = new Node3D { Name = $"Extra{o}", Position = Centre(p) + new Vector3(p.W * Cell * 0.42f, 0, p.H * Cell * 0.3f) };
            var m = ModelLib.Instance(model, "Model");
            float lift = -ModelLib.Mesh(model).GetAabb().Position.Y;
            m.Position = new Vector3(0, lift * 1.3f, 0);
            m.Scale = Vector3.One * 1.3f;
            root.AddChild(m);
            AddChild(root);
            _extras[o] = root;
            if (!instant) { root.Scale = new Vector3(1, 0.02f, 1); _extraPop[o % _extraPop.Length] = 0.0001f; }
        }
        BuiltCount = built;
        StakedCount = staked;
    }

    public override void _Process(double delta)
    {
        var t = _drv.Town;
        float dt = (float)delta;
        bool changed = false;
        foreach (var e in _drv.Events)
            if (e.Kind is TownEventKind.LevelReached or TownEventKind.BuildingOpened or TownEventKind.OfferAvailable) changed = true;
        if (changed) Sync(instant: false);
        int popping = 0;
        for (int i = 0; i < _pop.Length; i++)
        {
            if (_pop[i] <= 0 || _built[i] == null) continue;
            _pop[i] += dt / PopSeconds;
            float k = Math.Min(1f, _pop[i]);
            // Rise with a small overshoot, then settle.
            float e = 1f - MathF.Pow(1f - k, 3f);
            float over = MathF.Sin(k * MathF.PI) * 0.12f;
            _built[i]!.Scale = new Vector3(1f + over * 0.5f, Math.Max(0.02f, e + over), 1f + over * 0.5f);
            if (k >= 1f) { _pop[i] = 0; _built[i]!.Scale = Vector3.One; }
            else popping++;
        }
        for (int o = 0; o < _extras.Length && o < _extraPop.Length; o++)
        {
            if (_extraPop[o] <= 0 || _extras[o] == null) continue;
            _extraPop[o] += dt / PopSeconds;
            float k = Math.Min(1f, _extraPop[o]);
            _extras[o]!.Scale = new Vector3(1, Math.Max(0.02f, 1f - MathF.Pow(1f - k, 3f)), 1);
            if (k >= 1f) _extraPop[o] = 0; else popping++;
        }
        Popping = popping;
        // The mill's sails turn while it works; the bell swings while the town hurries.
        bool millWorks = false;
        var plots = t.Plan.Plots;
        for (int i = 0; i < plots.Length; i++) if (plots[i].Kind == PlotKind.Mill && t.PlotWorking(i)) millWorks = true;
        if (_sails != null && millWorks)
        {
            _sailAngle = (_sailAngle + dt * 1.2f * (float)Math.Min(_drv.Speed, 4)) % (MathF.PI * 2f);
            _sails.Rotation = new Vector3(0, 0, _sailAngle);
        }
        float swingTarget = t.BellRinging ? 1f : 0f;
        _bellSwing = Mathf.MoveToward(_bellSwing, swingTarget, dt * 3f);
        if (_bell != null) _bell.Rotation = new Vector3(MathF.Sin((float)_drv.AnimTime * 9f) * 0.45f * _bellSwing, 0, 0);
        if (_charter != null) _charter.Visible = t.HasCharter;
    }
}

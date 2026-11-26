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
    private int[] _ext = Array.Empty<int>();
    /// <summary>The extensions the drawn cottage on a plot shows (tests).</summary>
    public int DrawnExtensions(int p) => p < _ext.Length && _built[p] != null ? _ext[p] : -1;
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
    public static string Model(PlotKind kind, int id, int extensions = 0) => kind switch
    {
        // Rules v2: the three cottage models stand for the three sizes (built, extended once, extended twice).
        PlotKind.Cottage => $"cottage-0{1 + Math.Clamp(extensions, 0, 2)}",
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
        _loadedWithCharter = true;
        foreach (var c in GetChildren()) { RemoveChild(c); c.QueueFree(); }
        var plots = _drv.Town.Plan.Plots;
        _built = new Node3D?[plots.Length];
        _stakes = new Node3D?[plots.Length];
        _pop = new float[plots.Length];
        _ext = new int[plots.Length];
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
        int ext = p.Kind == PlotKind.Cottage ? _drv.Town.Extensions(p.Id) : 0;
        _ext[p.Id] = ext;
        string model = Model(p.Kind, p.Id, ext);
        var root = new Node3D { Name = $"Plot{p.Id}", Position = Centre(p) };
        float fw = p.W * Cell, fh = p.H * Cell;
        float yaw = DoorYaw(p);
        bool side = MathF.Abs(MathF.Sin(yaw)) > 0.5f;
        var aabb = ModelLib.Mesh(model).GetAabb();
        // Round 4: an extended cottage is wider (a lean-to with its own gable) and, from the second extension, has a
        // fenced yard behind; the footprint is fitted to the whole group.
        float wide = p.Kind == PlotKind.Cottage && ext > 1 ? 1.8f : p.Kind == PlotKind.Cottage && ext > 0 ? 1.5f : 1f, deep = 1f;
        float ax = Math.Max(0.1f, side ? aabb.Size.Z * deep : aabb.Size.X * wide), az = Math.Max(0.1f, side ? aabb.Size.X * wide : aabb.Size.Z * deep);
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
        float s = Math.Min(fw / ax, fh / az) * (p.Kind == PlotKind.Cottage ? (ext > 0 ? 0.95f : 0.74f) : 0.88f);
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
            // Round 6 (W1): the stone was the model's own size under the green's scale, small and inside the bell's frame,
            // so the charter read only as a banner. It now stands in the green's front corner, clear of the frame, about
            // 0.45 of the green's width across (at least 3 m), large enough to see at the normal view.
            var sa = ModelLib.Mesh(stone).GetAabb();
            float want = Math.Max(3f, 0.45f * Math.Min(fw, fh));
            float cs = want / Math.Max(0.1f, Math.Max(sa.Size.X, sa.Size.Z)) / s;
            _charter.Scale = Vector3.One * cs;
            _charter.Position = new Vector3(fw * 0.34f / s, 0, fh * 0.34f / s);
            // Round 6: pale dressed stone, so it stands out from the grass (it read green-grey before).
            if (_charter is GeometryInstance3D cg) cg.MaterialOverride = PaleStone;
            _charter.Visible = false;
            _charterBase = _charter.Position;
            _charterScale = _charter.Scale;
            body.AddChild(_charter);
        }
        else body.AddChild(ModelLib.Instance(model, "Model"));
        if (p.Kind == PlotKind.Cottage && ext > 0) AddExtensions(body, aabb, ext);
        return root;
    }

    /// <summary>
    /// Round 4: what each extension adds, readable at the normal view: the first a lean-to beside the house with its
    /// own gable (a small cottage turned side on, lower than the main roof); the second (round 6) a second lean-to on the other side;
    /// the third and later a woodpile by the lean-to. (No chimney model exists: see the round 4 progress notes.)
    /// </summary>
    private static void AddExtensions(Node3D body, Aabb main, int ext)
    {
        var c = main.GetCenter();
        // Round 6 (W7): two extensions add a second lean-to on the other side (a long row of three gables), so one and
        // two extensions differ plainly from above; the row is centred, the house in the middle.
        float shift = ext >= 2 ? 0f : main.Size.X * 0.25f; // one extension: the house moves left, the lean-to sits right
        body.GetNode<Node3D>("Model").Position = new Vector3(-shift, 0, 0);
        string leanModel = ModelLib.Pick("cottage-01", "cottage-01");
        var la = ModelLib.Mesh(leanModel).GetAabb();
        float k = Math.Min(main.Size.Y * 0.68f / Math.Max(0.1f, la.Size.Y), main.Size.Z * 0.8f / Math.Max(0.1f, la.Size.X));
        var lean = ModelLib.Instance(leanModel, "LeanTo");
        lean.RotationDegrees = new Vector3(0, 90, 0);
        lean.Scale = Vector3.One * k;
        // Turned side on, its depth (model Z) runs along the house's X.
        float lw = la.Size.Z * k;
        lean.Position = new Vector3(main.End.X - shift + lw * 0.5f - main.Size.X * 0.06f, 0, c.Z) + new Vector3(-la.GetCenter().Z * k, 0, la.GetCenter().X * k);
        body.AddChild(lean);
        if (ext >= 2)
        {
            var lean2 = ModelLib.Instance(leanModel, "LeanTo2");
            lean2.RotationDegrees = new Vector3(0, -90, 0);
            lean2.Scale = Vector3.One * k;
            lean2.Position = new Vector3(main.Position.X - lw * 0.5f + main.Size.X * 0.06f, 0, c.Z) + new Vector3(la.GetCenter().Z * k, 0, -la.GetCenter().X * k);
            body.AddChild(lean2);
        }
        // Round 6 (W7): the wattle yard behind is gone: thin fences under the ink outline read, from above, as a dark box.
        if (ext >= 3 && ModelLib.Has("prop-woodpile"))
        {
            var wa = ModelLib.Mesh("prop-woodpile").GetAabb();
            var pile = ModelLib.Instance("prop-woodpile", "Woodpile");
            pile.Scale = Vector3.One * (main.Size.Y * 0.3f / Math.Max(0.1f, wa.Size.Y));
            pile.Position = new Vector3(main.End.X - shift + lw * 0.6f, 0, main.End.Z - main.Size.Z * 0.1f);
            body.AddChild(pile);
        }
    }

    private static void Fence(Node3D body, string name, Vector3 at, float along, float h, float yawDeg)
    {
        var f = ModelLib.Instance("fence-wattle", name);
        f.Scale = new Vector3(along, h, h);
        f.RotationDegrees = new Vector3(0, yawDeg, 0);
        f.Position = at;
        // Round 6 (W7): the yard fence in pale new-cut wood: dark wattle read, from above, as a dark box round the house.
        f.MaterialOverride = PaleStakes;
        body.AddChild(f);
    }

    /// <summary>The colour of staked plots' stakes and cord (pale new-cut wood).</summary>
    public static readonly Color StakeColour = Color.FromHtml("#E6D5AE");
    private static StandardMaterial3D? _paleStakes, _paleStone;
    /// <summary>The charter stone's colour (pale dressed stone, light against the grass).</summary>
    public static readonly Color CharterStoneColour = Color.FromHtml("#F0EADB");
    private static StandardMaterial3D PaleStone => _paleStone ??= new StandardMaterial3D { AlbedoColor = CharterStoneColour, Roughness = 0.9f };
    private static StandardMaterial3D PaleStakes => _paleStakes ??= new StandardMaterial3D { AlbedoColor = StakeColour, Roughness = 0.95f };

    private Node3D MakeStakes(Plot p)
    {
        string model = ModelLib.Pick("plot-stakes", "fence-post");
        var root = new Node3D { Name = $"Stakes{p.Id}", Position = Centre(p) };
        var m = ModelLib.Instance(model, "Model");
        var aabb = ModelLib.Mesh(model).GetAabb();
        m.Scale = new Vector3(p.W * Cell / Math.Max(0.1f, aabb.Size.X) * 0.9f, 1f, p.H * Cell / Math.Max(0.1f, aabb.Size.Z) * 0.9f);
        // Round 4: pale new-cut stakes, not the model's dark wood, so a staked plot reads as marked out, not built.
        m.MaterialOverride = PaleStakes;
        root.AddChild(m);
        // Round 3: a small label says what will stand here, so a staked plot never reads as an editor's box.
        var label = new Label3D
        {
            Name = "What", Text = StakeText(p), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 38,
            PixelSize = 0.03f, Modulate = Palette.Ink, OutlineModulate = new Color(Palette.Paper, 0.9f), OutlineSize = 14,
            Font = UiKit.UiBold, Position = new Vector3(0, 1.6f, p.H * Cell / 2f + 1.6f), NoDepthTest = false, DoubleSided = true,
            VerticalAlignment = VerticalAlignment.Top,
        };
        root.AddChild(label);
        return root;
    }

    /// <summary>The words on a staked plot: what will stand there and when.</summary>
    public string StakeText(Plot p)
    {
        var t = _drv.Town;
        string what = p.Kind switch
        {
            PlotKind.Cottage => "A cottage", PlotKind.Well => "A well", PlotKind.Field => "A field", PlotKind.Market => "The market",
            PlotKind.Mill => "The mill", PlotKind.BellGreen => "The bell", _ => "Something new",
        };
        if (t.PlotGiven(p.Id)) return what + ", to buy";
        return $"{what}, at level {p.Level}";
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
            if (st && _stakes[i]!.GetNodeOrNull<Label3D>("What") is { } what) what.Text = StakeText(p);
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
        {
            if (e.Kind is TownEventKind.LevelReached or TownEventKind.BuildingOpened or TownEventKind.OfferAvailable) changed = true;
            if (e.Kind == TownEventKind.CottageExtended && e.Plot >= 0 && e.Plot < _built.Length && _built[e.Plot] != null)
            {
                // The bigger cottage pops in in place of the smaller one.
                var old = _built[e.Plot]!;
                RemoveChild(old); old.QueueFree();
                _built[e.Plot] = null;
                changed = true;
            }
        }
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
        _pealLeft = Math.Max(0f, _pealLeft - dt);
        float swingTarget = t.BellRinging || _pealLeft > 0 ? 1f : 0f;
        _bellSwing = Mathf.MoveToward(_bellSwing, swingTarget, dt * 3f);
        if (_bell != null) _bell.Rotation = new Vector3(MathF.Sin((float)_drv.AnimTime * 9f) * 0.45f * _bellSwing, 0, 0);
        if (_charter != null)
        {
            // The charter stone arrives: it comes down from above and settles, over 1.4 s (instant for a loaded town).
            if (t.HasCharter && !_charter.Visible) { _charterT = _loadedWithCharter ? 1.4f : 0f; }
            _charter.Visible = t.HasCharter;
            if (t.HasCharter && _charterT < 1.4f)
            {
                _charterT += dt;
                float k = Math.Clamp(_charterT / 1.4f, 0f, 1f), e = 1f - (1f - k) * (1f - k) * (1f - k);
                _charter.Position = new Vector3(_charterBase.X, (1f - e) * 9f, _charterBase.Z);
                _charter.Scale = _charterScale * (0.5f + 0.5f * e);
            }
        }
        _loadedWithCharter = false;
    }

    private float _charterT = 1.4f, _pealLeft;
    private bool _loadedWithCharter = true;
    private Vector3 _charterBase, _charterScale = Vector3.One;
    /// <summary>The charter stone's size across on the map in metres (tests: large enough to see).</summary>
    /// <summary>Whether the charter stone is drawn in pale stone (tests).</summary>
    public bool CharterPale => _charter is GeometryInstance3D g && g.MaterialOverride == PaleStone;
    public float CharterSize => _charter == null ? 0f : _charterScale.X * ((Node3D)_charter.GetParent()).Scale.X * Math.Max(ModelLib.Mesh(ModelLib.Pick("charter-stone", "well")).GetAabb().Size.X, ModelLib.Mesh(ModelLib.Pick("charter-stone", "well")).GetAabb().Size.Z);
    /// <summary>The charter stone is still coming down (tests and screenshots).</summary>
    public bool CharterArriving => _charter != null && _charter.Visible && _charterT < 1.4f;
    /// <summary>The bell swings for a while without the town's hurry (the next age rung in).</summary>
    public void Peal(float seconds) => _pealLeft = seconds;
}

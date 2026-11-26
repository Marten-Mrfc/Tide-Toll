using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Round 4: the second age's plan on the map, as the simulation gives it (town.RiverPlan, levels 17 to 28): the
/// landing and its road, the cottages on both banks, the pasture with grazing sheep, the weaver's cottage, the lanes,
/// and the crossings with their own models and behaviour (ford stones, the ferry's posts, rope and raft, the
/// footbridge, the drawbridge whose leaf follows DrawbridgeAngle, the keeper's hut). A river plot laid out by the next
/// level is staked with a label; a given one pops in. The tide moves the water's edge on the banks (ground shader
/// uniform tide_drop: at low water the river narrows and wet mud shows) and the tide post stands at the landing.
/// Boats are drawn by <see cref="RiverBoats"/>. River positions are already at cell centres: drawn at x * Cell.
/// Nothing here runs before the town rings in the second age.
/// </summary>
public partial class RiverLayer : Node3D
{
    public const float Cell = TownGround.Cell;
    /// <summary>Metres the water's edge draws back from the bank at the lowest water.</summary>
    public const float TideDropMetres = 3.2f;

    private Main _main = null!;
    private TownDriver _drv = null!;
    private RiverPlan? _plan;
    private Node3D?[] _plots = Array.Empty<Node3D?>(), _stakes = Array.Empty<Node3D?>(), _links = Array.Empty<Node3D?>(), _linkStakes = Array.Empty<Node3D?>();
    private float[] _pop = Array.Empty<float>(), _linkPop = Array.Empty<float>();
    private readonly List<Node3D> _sheep = new();
    private readonly List<Node3D> _sheepHeads = new();
    private Node3D? _raft, _lift, _tidePost, _seal;
    private float _liftLen;
    private float _sealT = 2f;
    private bool _wasSeal;
    private static readonly StringName TideDropS = new("tide_drop");

    /// <summary>Drawn state for tests.</summary>
    public bool Built => _plan != null;
    public bool PlotDrawn(int p) => p < _plots.Length && _plots[p] != null && _plots[p]!.Visible;
    public bool PlotStaked(int p) => p < _stakes.Length && _stakes[p] != null && _stakes[p]!.Visible;
    public bool LinkDrawn(int l) => l < _links.Length && _links[l] != null && _links[l]!.Visible;
    public Vector2 RaftShown => _raft == null ? new Vector2(-1, -1) : new Vector2(_raft.Position.X / Cell, _raft.Position.Z / Cell);
    public bool RaftVisible => _raft != null && _raft.Visible;
    public float LiftShown { get; private set; }
    public float TideDropShown { get; private set; }
    public int SheepDrawn => _sheep.Count;
    public float SheepHeadTilt => _sheepHeads.Count > 0 ? _sheepHeads[0].Rotation.X : 0f;
    public bool SealShown => _seal != null && _seal.Visible;
    public bool SealArriving => SealShown && _sealT < 1.6f;

    public void Init(Main main)
    {
        _main = main;
        _drv = main.Driver;
        Name = "River";
        _drv.Replaced += Clear;
    }

    public override void _ExitTree() { if (_drv != null) _drv.Replaced -= Clear; }

    private void Clear()
    {
        foreach (var c in GetChildren()) { RemoveChild(c); c.QueueFree(); }
        _plan = null;
        _sheep.Clear(); _sheepHeads.Clear();
        _raft = _lift = _tidePost = _seal = null;
        _wasSeal = _drv.Town.HasSeal;
        _sealT = 2f;
        _main.Ground?.Material.SetShaderParameter("tide_drop", 0f);
        TideDropShown = 0;
    }

    private void Build(RiverPlan rp)
    {
        _plan = rp;
        _plots = new Node3D?[rp.Plots.Length];
        _stakes = new Node3D?[rp.Plots.Length];
        _pop = new float[rp.Plots.Length];
        _links = new Node3D?[rp.Links.Length];
        _linkStakes = new Node3D?[rp.Links.Length];
        _linkPop = new float[rp.Links.Length];
        // The tide post: at the landing's waterside, the water's edge moves on it.
        var landing = rp.PlotOf(RiverPlotKind.Landing);
        int row = Math.Clamp(rp.LandingRow, 0, rp.CourseX.Length - 1);
        double cx = rp.CourseX[row];
        float half = (float)_drv.Town.Plan.Map.RiverHalfW;
        float side = landing.X + landing.W / 2f < cx ? -1f : 1f;
        _tidePost = Fit(ModelLib.Pick("tide-post", "fence-post"), 1.0f, 1.0f, 0f, 0.6f);
        _tidePost.Name = "TidePost";
        _tidePost.Position = new Vector3((float)(cx + side * (half - 0.6f)) * Cell, 0, (landing.Y - 0.6f) * Cell);
        _tidePost.Visible = false;
        AddChild(_tidePost);
    }

    /// <summary>A model scaled uniformly to fit w x d metres (its own x and z), turned by yaw, under a fresh node.</summary>
    private static Node3D Fit(string model, float w, float d, float yaw, float shrink = 0.9f)
    {
        var root = new Node3D();
        var body = new Node3D { Name = "Body", Rotation = new Vector3(0, yaw, 0) };
        root.AddChild(body);
        var aabb = ModelLib.Mesh(model).GetAabb();
        bool sideways = MathF.Abs(MathF.Sin(yaw)) > 0.5f;
        float ax = Math.Max(0.1f, sideways ? aabb.Size.Z : aabb.Size.X), az = Math.Max(0.1f, sideways ? aabb.Size.X : aabb.Size.Z);
        float s = Math.Min(w / ax, d / az) * shrink;
        body.Scale = Vector3.One * s;
        body.AddChild(ModelLib.Instance(model, "Model"));
        return root;
    }

    /// <summary>A model laid along a line from a to b (map metres): its long axis stretched to the length, the rest
    /// scaled to the width. Returns the node, its origin at a.</summary>
    private static Node3D Stretch(string model, Vector3 a, Vector3 b, float width, float lift = 0f, bool centred = true)
    {
        var aabb = ModelLib.Mesh(model).GetAabb();
        bool alongX = aabb.Size.X >= aabb.Size.Z;
        float len = Math.Max(0.1f, (b - a).Length());
        float longDim = Math.Max(0.05f, alongX ? aabb.Size.X : aabb.Size.Z), shortDim = Math.Max(0.05f, alongX ? aabb.Size.Z : aabb.Size.X);
        float ws = width / shortDim;
        var root = new Node3D { Position = a };
        float yaw = MathF.Atan2(-(b.Z - a.Z), b.X - a.X); // turns +X toward b
        var body = new Node3D { Name = "Body", Rotation = new Vector3(0, alongX ? yaw : yaw + MathF.PI / 2f, 0) };
        root.AddChild(body);
        var m = ModelLib.Instance(model, "Model");
        float ls = len / longDim;
        m.Scale = alongX ? new Vector3(ls, ws, ws) : new Vector3(ws, ws, ls);
        // Put the model's long-axis start at the origin (or its centre at the midpoint).
        float start = alongX ? aabb.Position.X : aabb.Position.Z;
        float mid = start + longDim / 2f;
        float shift = centred ? -mid * ls + len / 2f : -start * ls;
        m.Position = (alongX ? new Vector3(shift, lift, 0) : new Vector3(0, lift, shift));
        body.AddChild(m);
        return root;
    }

    private static Vector3 P(double x, double y) => new((float)x * Cell, 0, (float)y * Cell);

    private Node3D MakePlot(RiverPlot p)
    {
        var rp = _plan!;
        float fw = p.W * Cell, fh = p.H * Cell;
        // Face the door point.
        float dx = (float)(rp.Px[p.Point] - (p.X + p.W / 2f)), dy = (float)(rp.Py[p.Point] - (p.Y + p.H / 2f));
        float yaw = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? MathF.PI / 2f : -MathF.PI / 2f) : (dy > 0 ? 0f : MathF.PI);
        string model = p.Kind switch
        {
            RiverPlotKind.Landing => ModelLib.Pick("landing", "market-stall"),
            RiverPlotKind.Weaver => ModelLib.Pick("weaver-cottage", "cottage-02"),
            RiverPlotKind.Pasture => ModelLib.Pick("pasture", "field-stubble"),
            _ => "cottage-01",
        };
        Node3D root;
        if (p.Kind == RiverPlotKind.Pasture)
        {
            root = new Node3D();
            var aabb = ModelLib.Mesh(model).GetAabb();
            var m = ModelLib.Instance(model, "Model");
            m.Scale = new Vector3(fw / Math.Max(0.1f, aabb.Size.X), 1f, fh / Math.Max(0.1f, aabb.Size.Z)) * 0.96f;
            root.AddChild(m);
            AddSheep(root, fw, fh, p.Id);
        }
        else if (p.Kind == RiverPlotKind.Landing)
        {
            // The landing reaches out over the water: its footprint is on the bank, so let it overhang a little.
            root = Fit(model, fw * 1.15f, fh * 1.15f, yaw, 0.95f);
        }
        else root = Fit(model, fw, fh, yaw, p.Kind == RiverPlotKind.Cottage ? 0.78f : 0.88f);
        root.Name = $"RiverPlot{p.Id}";
        root.Position = new Vector3((p.X + p.W / 2f) * Cell, 0, (p.Y + p.H / 2f) * Cell);
        return root;
    }

    private void AddSheep(Node3D pasture, float fw, float fh, int seed)
    {
        string model = ModelLib.Pick("sheep", "villager");
        bool parts = ModelLib.HasPart(model, "Head") && ModelLib.HasPart(model, "Body");
        var aabb = ModelLib.Mesh(model).GetAabb();
        float s = 1.5f / Math.Max(0.1f, Math.Max(aabb.Size.X, aabb.Size.Z));
        var rng = new Random(seed * 31 + 7);
        for (int i = 0; i < 5; i++)
        {
            var sh = new Node3D { Name = "Sheep" + i };
            sh.Position = new Vector3(((float)rng.NextDouble() - 0.5f) * fw * 0.7f, 0, ((float)rng.NextDouble() - 0.5f) * fh * 0.7f);
            sh.Rotation = new Vector3(0, (float)rng.NextDouble() * MathF.Tau, 0);
            sh.Scale = Vector3.One * s;
            if (parts)
            {
                sh.AddChild(ModelLib.PartInstance(model, "Body", "Body"));
                var head = ModelLib.PartInstance(model, "Head", "Head");
                sh.AddChild(head);
                _sheepHeads.Add(head);
            }
            else sh.AddChild(ModelLib.Instance(model, "Model"));
            pasture.AddChild(sh);
            _sheep.Add(sh);
        }
    }

    private Node3D MakeStakes(RiverPlot p)
    {
        string model = ModelLib.Pick("plot-stakes", "fence-post");
        var root = new Node3D { Name = $"RiverStakes{p.Id}", Position = new Vector3((p.X + p.W / 2f) * Cell, 0, (p.Y + p.H / 2f) * Cell) };
        var m = ModelLib.Instance(model, "Model");
        var aabb = ModelLib.Mesh(model).GetAabb();
        m.Scale = new Vector3(p.W * Cell / Math.Max(0.1f, aabb.Size.X) * 0.9f, 1f, p.H * Cell / Math.Max(0.1f, aabb.Size.Z) * 0.9f);
        root.AddChild(m);
        root.AddChild(StakeLabel(StakeText(p.Kind, p.Level), new Vector3(0, 1.6f, p.H * Cell / 2f + 1.6f)));
        return root;
    }

    private static Label3D StakeLabel(string text, Vector3 at) => new()
    {
        Name = "What", Text = text, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 38,
        PixelSize = 0.03f, Modulate = Palette.Ink, OutlineModulate = new Color(Palette.Paper, 0.9f), OutlineSize = 14,
        Font = UiKit.UiBold, Position = at, DoubleSided = true, VerticalAlignment = VerticalAlignment.Top,
    };

    public static string StakeText(RiverPlotKind k, int level) => k switch
    {
        RiverPlotKind.Landing => "The landing", RiverPlotKind.Weaver => "The weaver", RiverPlotKind.Pasture => "The pasture",
        _ => "A cottage",
    } + $", at level {level}";

    public static string LinkName(RiverLinkKind k) => k switch
    {
        RiverLinkKind.Ford => "The ford", RiverLinkKind.Ferry => "The ferry", RiverLinkKind.Footbridge => "The footbridge",
        RiverLinkKind.Drawbridge => "The drawbridge", _ => "A lane",
    };

    private Node3D MakeLink(RiverLink l)
    {
        var root = new Node3D { Name = $"RiverLink{l.Id}" };
        int n = l.Px.Length;
        var a = P(l.Px[0], l.Py[0]);
        var b = P(l.Px[n - 1], l.Py[n - 1]);
        switch (l.Kind)
        {
            case RiverLinkKind.Lane:
                root.AddChild(Ribbon(l, 1.2f, Palette.TrailEarth.Lerp(Palette.BentGrass, 0.25f)));
                break;
            case RiverLinkKind.Ford:
            {
                string m = ModelLib.Pick("ford-stones", "fence-post");
                float len = (b - a).Length();
                int k = Math.Max(2, (int)(len / 2.6f));
                for (int i = 0; i <= k; i++)
                {
                    var st = Fit(m, 2.4f, 2.4f, i * 1.3f, 1f);
                    st.Position = a.Lerp(b, i / (float)k);
                    root.AddChild(st);
                }
                break;
            }
            case RiverLinkKind.Ferry:
            {
                string post = ModelLib.Pick("ferry-post", "fence-post");
                var pa = Fit(post, 1.4f, 1.4f, 0f, 1f); pa.Position = a + (a - b).Normalized() * 0.8f; root.AddChild(pa);
                var pb = Fit(post, 1.4f, 1.4f, MathF.PI, 1f); pb.Position = b + (b - a).Normalized() * 0.8f; root.AddChild(pb);
                root.AddChild(Stretch(ModelLib.Pick("ferry-rope", "fence-wattle"), pa.Position, pb.Position, 0.25f, 1.6f));
                _raft = Fit(ModelLib.Pick("ferry-raft", "rowboat"), 3.6f, 3.6f, 0f, 1f);
                _raft.Name = "Raft";
                AddChild(_raft);
                break;
            }
            case RiverLinkKind.Footbridge:
            {
                string end = ModelLib.Pick("footbridge-end", "fence-post");
                var ea = Fit(end, 2.4f, 2.4f, 0f, 1f); ea.Position = a; root.AddChild(ea);
                var eb = Fit(end, 2.4f, 2.4f, MathF.PI, 1f); eb.Position = b; root.AddChild(eb);
                root.AddChild(Stretch(ModelLib.Pick("footbridge-span", "fence-wattle"), a, b, 2.2f));
                break;
            }
            case RiverLinkKind.Drawbridge:
            {
                string end = ModelLib.Pick("drawbridge-end", "fence-post");
                var ea = Fit(end, 3f, 3f, 0f, 1f); ea.Position = a; root.AddChild(ea);
                var eb = Fit(end, 3f, 3f, MathF.PI, 1f); eb.Position = b; root.AddChild(eb);
                var mid = a.Lerp(b, 0.5f);
                // The far half is fixed; the near half is the leaf, hinged at the near bank, lifted by the winch.
                root.AddChild(Stretch(ModelLib.Pick("drawbridge-span", "footbridge-span"), mid, b, 3f));
                _liftLen = (mid - a).Length();
                var hinge = new Node3D { Name = "Hinge", Position = a };
                var dir = (mid - a).Normalized();
                hinge.Rotation = new Vector3(0, MathF.Atan2(-dir.Z, dir.X), 0); // local +X toward the far bank
                _lift = new Node3D { Name = "Leaf" };
                hinge.AddChild(_lift);
                var leaf = Stretch(ModelLib.Pick("drawbridge-lift", "footbridge-span"), Vector3.Zero, new Vector3(_liftLen, 0, 0), 3f);
                _lift.AddChild(leaf);
                root.AddChild(hinge);
                var hut = Fit(ModelLib.Pick("keeper-hut", "cottage-01"), 3.2f, 3.2f, 0f, 1f);
                hut.Position = a + new Vector3(0, 0, -4.5f) + (a - b).Normalized() * 3f;
                root.AddChild(hut);
                break;
            }
        }
        return root;
    }

    /// <summary>A flat earth strip along a lane's polyline (one quad per segment, rounded at the joints by overlap).</summary>
    private static MeshInstance3D Ribbon(RiverLink l, float width, Color col)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        float h = width / 2f, y = 0.05f;
        for (int i = 0; i + 1 < l.Px.Length; i++)
        {
            var a = P(l.Px[i], l.Py[i]); var b = P(l.Px[i + 1], l.Py[i + 1]);
            var d = b - a;
            if (d.LengthSquared() < 1e-6f) continue;
            d = d.Normalized();
            var nrm = new Vector3(-d.Z, 0, d.X) * h;
            a -= d * h * 0.9f; b += d * h * 0.9f;
            var p0 = a + nrm; var p1 = a - nrm; var p2 = b - nrm; var p3 = b + nrm;
            p0.Y = p1.Y = p2.Y = p3.Y = y;
            st.AddVertex(p0); st.AddVertex(p2); st.AddVertex(p1);
            st.AddVertex(p0); st.AddVertex(p3); st.AddVertex(p2);
        }
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = col, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        });
        return new MeshInstance3D { Name = "Lane", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    public override void _Process(double delta)
    {
        var t = _drv.Town;
        if (!t.InRiverAge) return;
        if (_plan == null) Build(t.RiverPlan!);
        var rp = _plan!;
        float dt = (float)delta;
        bool instant = _drv.StepsRun == 0;
        int next = t.Level + 1;
        for (int i = 0; i < rp.Plots.Length; i++)
        {
            var p = rp.Plots[i];
            bool given = t.RiverPlotGiven(i);
            if (given && _plots[i] == null)
            {
                _plots[i] = MakePlot(p);
                AddChild(_plots[i]);
                if (!instant) { _pop[i] = 0.0001f; _plots[i]!.Scale = new Vector3(1, 0.02f, 1); }
            }
            bool stake = !given && p.Level == next && t.Level < t.MaxLevel;
            if (stake && _stakes[i] == null) { _stakes[i] = MakeStakes(p); AddChild(_stakes[i]); }
            if (_stakes[i] != null) _stakes[i]!.Visible = stake;
        }
        for (int i = 0; i < rp.Links.Length; i++)
        {
            var l = rp.Links[i];
            bool open = t.RiverLinkOpen(i);
            // The ferry is drawn once given (its posts and rope) but works only once bought: its raft shows then.
            bool given = open || (l.Kind == RiverLinkKind.Ferry && t.Level >= l.Level);
            if (given && _links[i] == null)
            {
                _links[i] = MakeLink(l);
                AddChild(_links[i]);
                if (!instant) { _linkPop[i] = 0.0001f; _links[i]!.Scale = new Vector3(1, 0.02f, 1); }
            }
            bool stake = !given && l.Kind != RiverLinkKind.Lane && l.Level == next && t.Level < t.MaxLevel;
            if (stake && _linkStakes[i] == null)
            {
                int n = l.Px.Length;
                var at = P(l.Px[0], l.Py[0]).Lerp(P(l.Px[n - 1], l.Py[n - 1]), 0.5f);
                var s = new Node3D { Name = $"LinkStake{i}", Position = at };
                s.AddChild(StakeLabel($"{LinkName(l.Kind)}, at level {l.Level}", new Vector3(0, 1.2f, 0)));
                _linkStakes[i] = s;
                AddChild(s);
            }
            if (_linkStakes[i] != null) _linkStakes[i]!.Visible = stake;
        }
        Pop(_plots, _pop, dt);
        Pop(_links, _linkPop, dt);
        // The ferry's raft where the simulation has it.
        var f = t.Ferry;
        if (_raft != null)
        {
            _raft.Visible = f.Built;
            if (f.Built)
            {
                _raft.Position = P(f.X, f.Y);
                _raft.Rotation = new Vector3(0, TownVillagers.HeadingYaw(f.Heading), 0);
            }
        }
        // The drawbridge's leaf follows its angle (0 down .. 1.2 up), turning about the near bank.
        if (_lift != null)
        {
            LiftShown = (float)t.DrawbridgeAngle;
            _lift.Rotation = new Vector3(0, 0, LiftShown);
        }
        // The tide: the water's edge on the banks, eased a little so a load or a fast-forward does not jump.
        float drop = (float)(1.0 - t.TideLevel) * TideDropMetres;
        TideDropShown = instant ? drop : Mathf.MoveToward(TideDropShown, drop, dt * 6f);
        _main.Ground.Material.SetShaderParameter(TideDropS, TideDropShown);
        if (_tidePost != null) _tidePost.Visible = t.RiverPlotGiven(0);
        // Sheep graze: heads dip and rise, each at its own pace.
        double at2 = _drv.AnimTime;
        for (int i = 0; i < _sheepHeads.Count; i++)
            _sheepHeads[i].Rotation = new Vector3(0.35f + 0.35f * MathF.Sin((float)at2 * (0.7f + 0.13f * i) + i * 1.7f), 0, 0);
        SealFrame(t, dt);
    }

    /// <summary>The seal stone comes down beside the landing when the borough seal is granted.</summary>
    private void SealFrame(Town t, float dt)
    {
        if (!t.HasSeal) return;
        if (_seal == null)
        {
            var rp = _plan!;
            var landing = rp.PlotOf(RiverPlotKind.Landing);
            var at = new Vector3((float)rp.Px[RiverPoint.Landing] * Cell, 0, (float)rp.Py[RiverPoint.Landing] * Cell);
            var away = new Vector3((landing.X + landing.W / 2f) * Cell, 0, (landing.Y + landing.H / 2f) * Cell) - at;
            _seal = Fit(ModelLib.Pick("seal-stone", "charter-stone"), 3.2f, 3.2f, 0f, 1f);
            _seal.Name = "SealStone";
            _seal.Position = at - away.Normalized() * 3.5f + new Vector3(0, 0, 3.5f);
            AddChild(_seal);
            _sealT = _wasSeal ? 2f : 0f;
        }
        if (_sealT < 1.6f)
        {
            _sealT += dt;
            float k = Math.Clamp(_sealT / 1.6f, 0f, 1f), e = 1f - (1f - k) * (1f - k) * (1f - k);
            var p = _seal.Position;
            _seal.Position = new Vector3(p.X, (1f - e) * 12f, p.Z);
            _seal.Scale = Vector3.One * (0.5f + 0.5f * e);
        }
    }

    private static void Pop(Node3D?[] nodes, float[] pop, float dt)
    {
        for (int i = 0; i < pop.Length; i++)
        {
            if (pop[i] <= 0 || nodes[i] == null) continue;
            pop[i] += dt / TownBuildings.PopSeconds;
            float k = Math.Min(1f, pop[i]);
            float e = 1f - MathF.Pow(1f - k, 3f), over = MathF.Sin(k * MathF.PI) * 0.12f;
            nodes[i]!.Scale = new Vector3(1f + over * 0.5f, Math.Max(0.02f, e + over), 1f + over * 0.5f);
            if (k >= 1f) { pop[i] = 0; nodes[i]!.Scale = Vector3.One; }
        }
    }

    /// <summary>Widens a map-cell box (as Main.TownExtent builds it) to take in what is given of the riverside.</summary>
    public static void Widen(Town t, ref float x0, ref float y0, ref float x1, ref float y1)
    {
        if (!t.InRiverAge) return;
        var rp = t.RiverPlan!;
        for (int i = 0; i < rp.Plots.Length; i++)
        {
            var p = rp.Plots[i];
            if (!t.RiverPlotGiven(i) && p.Level != t.Level + 1) continue;
            x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X + p.W); y1 = Math.Max(y1, p.Y + p.H);
        }
        for (int i = 0; i < rp.Links.Length; i++)
        {
            var l = rp.Links[i];
            if (l.Level > t.Level + 1) continue;
            for (int k = 0; k < l.Px.Length; k++)
            {
                x0 = Math.Min(x0, (float)l.Px[k] - 1); y0 = Math.Min(y0, (float)l.Py[k] - 1);
                x1 = Math.Max(x1, (float)l.Px[k] + 1); y1 = Math.Max(y1, (float)l.Py[k] + 1);
            }
        }
        // The river itself, between the banks at the landing's row.
        int row = Math.Clamp(rp.LandingRow, 0, rp.CourseX.Length - 1);
        float half = (float)t.Plan.Map.RiverHalfW + 1;
        x0 = Math.Min(x0, (float)rp.CourseX[row] - half); x1 = Math.Max(x1, (float)rp.CourseX[row] + half);
    }
}

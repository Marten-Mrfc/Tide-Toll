using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>Night 3: the cottages (three variants, each a real model at every size), their accents and smoke points.</summary>
public partial class TownBuildings
{
    /// <summary>
    /// Night 3: three cottages (a hipped longhouse, a gabled house with a stack at the gable, a house with its gable to
    /// the lane), each a real model at every size (plain, extended once, twice). The variant and the accent colour of
    /// door and shutters come from the cottage's place among the plan's cottages, so a street varies and the same
    /// town always looks the same (nothing saved).
    /// </summary>
    public static string CottageModel(int ordinal, int extensions)
    {
        int e = Math.Clamp(extensions, 0, 2), v = ((ordinal % 3) + 3) % 3;
        return ModelLib.Pick($"cottage-{"abc"[v]}{e}", $"cottage-0{1 + v}");
    }
    /// <summary>Door and shutter colours (style sheet section 2), chosen per cottage by <see cref="CottageAccent"/>.</summary>
    public static readonly Color[] Accents = { Palette.CastIronGreen, Palette.Brick, Palette.River, Palette.Thatch, Palette.SlateRoof };
    public static int CottageAccent(int ordinal) => (ordinal * 2 + 1) % Accents.Length;
    private static readonly StandardMaterial3D?[] AccentMats = new StandardMaterial3D?[5];
    private static StandardMaterial3D AccentMaterial(int a) => AccentMats[a] ??= new StandardMaterial3D
    {
        AlbedoColor = Accents[a], Roughness = 1f, DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Lambert,
        SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
    };
    /// <summary>The cottage's place among the plan's cottages (0 for the first cottage plot).</summary>
    public static int CottageOrdinal(TownPlan plan, int plotId)
    {
        int k = 0;
        foreach (var q in plan.Plots) { if (q.Id == plotId) return k; if (q.Kind == PlotKind.Cottage) k++; }
        return k;
    }
    /// <summary>
    /// Last look (night 3): each size its own scale, so a plain cottage stands as large beside the villagers as it did
    /// before tonight: the widest model of a size (over the three variants) fills <see cref="CottageFill"/> of the plot's
    /// shorter side: plain 0.86, extended once 0.91, twice 0.95 (was one scale for all, the twice-extended one at 0.95,
    /// which left the plain cottage at about 0.74). The sizes stay told apart by their shapes (wings, porch, chimneys).
    /// </summary>
    public static readonly float[] CottageFill = { 0.86f, 0.91f, 0.95f };
    private static readonly float[] _cottageSpan = new float[3];
    /// <summary>The widest cottage model of an extension count (metres, X or Z).</summary>
    public static float CottageSpan(int ext)
    {
        ext = Math.Clamp(ext, 0, 2);
        if (_cottageSpan[ext] > 0) return _cottageSpan[ext];
        float span = 1f;
        for (int v = 0; v < 3; v++)
        {
            var b = ModelLib.Mesh(CottageModel(v, ext)).GetAabb();
            span = Math.Max(span, Math.Max(b.Size.X, b.Size.Z));
        }
        return _cottageSpan[ext] = span;
    }
    private static readonly System.Collections.Generic.Dictionary<string, bool> PartsKnown = new();
    private static bool HasPartCached(string model, string node)
    {
        string key = model + "/" + node;
        if (!PartsKnown.TryGetValue(key, out bool has)) PartsKnown[key] = has = ModelLib.HasPart(model, node);
        return has;
    }

    /// <summary>The cottage on a plot: its model centred, the accent node tinted, a "Smoke" marker on every chimney.</summary>
    private Node3D MakeCottage(Plot p, Node3D root, Node3D body, int ext, float fw, float fh)
    {
        body.AddChild(CottageNode(CottageOrdinal(_drv.Town.Plan, p.Id), ext, fw, fh));
        return root;
    }

    /// <summary>The cottage the game draws for the k-th cottage plot at an extension count on a plot fw x fh metres (front +Z).</summary>
    public static Node3D CottageNode(int k, int ext, float fw, float fh)
    {
        string model = CottageModel(k, ext);
        var aabb = ModelLib.Mesh(model).GetAabb();
        var root = new Node3D { Name = "Cottage", Scale = Vector3.One * (Math.Min(fw, fh) * CottageFill[Math.Clamp(ext, 0, 2)] / CottageSpan(ext)) };
        var shift = new Node3D { Name = "Shift", Position = new Vector3(-(aabb.Position.X + aabb.Size.X / 2f), 0, -(aabb.Position.Z + aabb.Size.Z / 2f)) };
        root.AddChild(shift);
        if (HasPartCached(model, "house") && HasPartCached(model, "accent"))
        {
            shift.AddChild(ModelLib.PartInstance(model, "house", "Model"));
            var acc = ModelLib.PartInstance(model, "accent", "Accent");
            acc.MaterialOverride = AccentMaterial(CottageAccent(k));
            shift.AddChild(acc);
        }
        else shift.AddChild(ModelLib.Instance(model, "Model"));
        foreach (var sm in SmokeNames)
            if (HasPartCached(model, sm))
            {
                var mk = new Marker3D { Name = sm, Position = ModelLib.PartOrigin(model, sm) };
                if (sm == "Smoke") mk.AddChild(SmokePuffs(k));   // a thin thread of smoke from the first chimney
                shift.AddChild(mk);
            }
        return root;
    }
    /// <summary>The empty nodes at the tops of a cottage's chimneys; the first one smokes.</summary>
    public static readonly string[] SmokeNames = { "Smoke", "Smoke2" };

    /// <summary>
    /// Night 3 (style sheet section 7, life): a few pale puffs rising from a chimney, growing and fading as they drift
    /// off with the wind. Five puffs a chimney at ten updates a second: about a hundred puffs in a full town.
    /// </summary>
    private static CpuParticles3D SmokePuffs(int k)
    {
        return new CpuParticles3D
        {
            Name = "SmokePuffs", Amount = 5, Lifetime = 4.5, FixedFps = 10, Preprocess = 4.5 + (k % 5) * 0.7,
            LocalCoords = false, Mesh = PuffMesh, Direction = new Vector3(0.35f, 1f, 0.1f), Spread = 8f,
            Gravity = Vector3.Zero, InitialVelocityMin = 0.45f, InitialVelocityMax = 0.6f,
            ScaleAmountMin = 0.8f, ScaleAmountMax = 1.1f, ScaleAmountCurve = PuffScale, ColorRamp = PuffFade,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }
    private static SphereMesh? _puffMesh;
    private static SphereMesh PuffMesh => _puffMesh ??= new SphereMesh
    {
        Radius = 0.42f, Height = 0.7f, RadialSegments = 7, Rings = 3,
        Material = new StandardMaterial3D
        {
            AlbedoColor = Colors.White, VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        },
    };
    private static Curve? _puffScale;
    private static Curve PuffScale
    {
        get
        {
            if (_puffScale != null) return _puffScale;
            _puffScale = new Curve();
            _puffScale.AddPoint(new Vector2(0f, 0.45f));
            _puffScale.AddPoint(new Vector2(1f, 1.6f));
            return _puffScale;
        }
    }
    private static Gradient? _puffFade;
    private static Gradient PuffFade => _puffFade ??= new Gradient
    {
        Offsets = new[] { 0f, 0.15f, 1f },
        Colors = new[] { new Color(Palette.Limewash, 0f), new Color(Palette.Limewash, 0.8f), new Color(Palette.Paper, 0f) },
    };
}

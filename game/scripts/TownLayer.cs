using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The simulation's buildings drawn with the Hamlet models, rebuilt whenever the world is replaced. One node per
/// building ("House3", "Well0", "Market2", "Field1") under "Town", so tools and the inspector can find them;
/// each piece is one merged mesh, one draw call.
/// - Houses: one of the three cottages, chosen by a hash of the seed and building id (same seed, same town),
///   turned so the front door faces the simulation's door cell, scaled down to fit the 3 x 3 cell footprint and
///   pushed to the back of it so the door cell in front stays visible.
/// - Well: centred on its footprint. Market: four stalls round a small square that opens toward the door.
/// - Field: drawn by the ground shader (crop rows that trails trample), which reads better than the 16 m field
///   model at the simulation's 18 x 12 m footprint; a fence post marks each corner.
/// </summary>
public partial class TownLayer : Node3D
{
    private static readonly string[] Cottages = { "cottage-01", "cottage-02", "cottage-03" };
    private SimDriver _sim = null!;

    public void Init(SimDriver sim)
    {
        _sim = sim;
        Name = "Town";
        sim.WorldReplaced += Rebuild;
        Rebuild();
    }

    public override void _ExitTree()
    {
        if (_sim != null) _sim.WorldReplaced -= Rebuild;
    }

    public void Rebuild()
    {
        foreach (var c in GetChildren()) { RemoveChild(c); c.QueueFree(); }
        var w = _sim.World;
        float cell = (float)w.Params.CellMeters;
        foreach (var b in w.Buildings)
        {
            var node = new Node3D { Name = $"{b.Kind}{b.Id}" };
            AddChild(node);
            // Footprint centre in map metres, and the yaw that turns the model's front (+Z) toward the door.
            float cx = (b.X + b.W / 2f) * cell, cz = (b.Y + b.H / 2f) * cell;
            float yaw = DoorYaw(b, w.Grid);
            node.Position = new Vector3(cx, 0, cz);
            node.Rotation = new Vector3(0, yaw, 0);
            // Footprint depth along the facing direction (the frame is turned, so W and H may swap).
            bool sideways = MathF.Abs(MathF.Sin(yaw)) > 0.5f;
            float depth = (sideways ? b.W : b.H) * cell, width = (sideways ? b.H : b.W) * cell;
            switch (b.Kind)
            {
                case BuildingKind.House: AddHouse(node, b, w.Seed, width, depth); break;
                case BuildingKind.Well: AddPiece(node, "well", "Well", 0, -0.4f, 0, 1f); break;
                case BuildingKind.Market: AddMarket(node, width, depth); break;
                case BuildingKind.Field: AddFieldPosts(node, b.W * cell, b.H * cell, yaw); break;
            }
        }
    }

    /// <summary>Yaw that turns +Z toward the door cell (houses, well and market have the door on their south row).</summary>
    public static float DoorYaw(in Building b, Grid g)
    {
        float dx = g.X(b.Door) + 0.5f - (b.X + b.W / 2f), dz = g.Y(b.Door) + 0.5f - (b.Y + b.H / 2f);
        if (MathF.Abs(dz) >= MathF.Abs(dx)) return dz >= 0 ? 0f : MathF.PI;
        return dx >= 0 ? MathF.PI / 2f : -MathF.PI / 2f;
    }

    // Thatch tones: fresh straw, the palette's Reed and thatch, weathered toward Packed earth, old and greyed.
    private static readonly Color[] ThatchTones =
    {
        Palette.Thatch.Lerp(Palette.Paper, 0.16f), Palette.Thatch, Palette.Thatch.Lerp(Palette.PackedEarth, 0.32f),
        Palette.Thatch.Lerp(Color.FromHtml("#6F6450"), 0.38f),
    };
    private static ShaderMaterial? _building;

    /// <summary>The cottages' material (shaders/building.gdshader): the model look plus a per-house thatch tone.</summary>
    private static ShaderMaterial BuildingMaterial(string model)
    {
        if (_building != null) return _building;
        _building = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/building.gdshader") };
        _building.SetShaderParameter("atlas", ((StandardMaterial3D)ModelLib.Mesh(model).SurfaceGetMaterial(0)).AlbedoTexture);
        _building.SetShaderParameter("thatch", Palette.Thatch);
        return _building;
    }

    private static void AddHouse(Node3D node, in Building b, int seed, float width, float depth)
    {
        int pick = (int)(Hash.Hash2(seed * 101 + 7, b.Id) * Cottages.Length);
        string model = Cottages[pick];
        var aabb = ModelLib.Mesh(model).GetAabb();
        // Life-size cottages are wider than the 6 m footprint: scale to fit with a little room, never up.
        float s = MathF.Min(1f, MathF.Min((width - 0.2f) / aabb.Size.X, (depth - 0.6f) / aabb.Size.Z));
        float back = -depth / 2f + aabb.Size.Z * s / 2f + 0.1f; // back of the house at the back of the footprint
        var mi = AddPiece(node, model, "Cottage", 0, back, 0, s);
        mi.MaterialOverride = BuildingMaterial(model);
        int tone = (int)(Hash.Hash2(seed * 31 + 3, b.Id * 7 + 1) * ThatchTones.Length);
        mi.SetInstanceShaderParameter("thatch_tone", ThatchTones[Math.Clamp(tone, 0, ThatchTones.Length - 1)]);
    }

    private static void AddMarket(Node3D node, float width, float depth)
    {
        // Two stalls along the back facing the door, one each side facing in: a little square open to the door.
        float back = -depth / 2f + 1.5f;
        AddPiece(node, "market-stall", "StallBackW", -width / 4f, back, 0, 1f);
        AddPiece(node, "market-stall", "StallBackE", width / 4f, back, 0, 1f);
        AddPiece(node, "market-stall", "StallW", -width / 2f + 1.5f, back + 3.6f, MathF.PI / 2f, 1f);
        AddPiece(node, "market-stall", "StallE", width / 2f - 1.5f, back + 3.6f, -MathF.PI / 2f, 1f);
    }

    private static void AddFieldPosts(Node3D node, float w, float h, float yaw)
    {
        // The node is turned by yaw; undo it so the posts land on the real corners.
        node.Rotation = Vector3.Zero;
        int i = 0;
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                AddPiece(node, "fence-post", $"Post{i++}", sx * (w / 2f - 0.2f), sz * (h / 2f - 0.2f), 0, 1.2f);
    }

    private static MeshInstance3D AddPiece(Node3D parent, string model, string name, float x, float z, float yaw, float scale)
    {
        var mi = ModelLib.Instance(model, name);
        mi.Position = new Vector3(x, 0, z);
        mi.Rotation = new Vector3(0, yaw, 0);
        mi.Scale = new Vector3(scale, scale, scale);
        parent.AddChild(mi);
        return mi;
    }
}

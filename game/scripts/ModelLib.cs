using System.Collections.Generic;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Loads the Hamlet .glb models once and hands out each one as a single merged mesh with a Living Map material:
/// Lambert, no specular, the palette atlas sampled nearest (so flat colours stay flat). Models whose glTF nodes
/// carry extras {"ink": "soft"} (trees, fields, reeds) get a material that writes 1 into the stencil buffer, and the
/// ink pass inks their creases only above a higher threshold (InkOutline). The same setup as tools/contact-sheet.
/// Also loads colour textures with mipmaps built at load time, because the imported copies under game/assets are
/// generated and gitignored, so their import settings cannot be relied on.
/// </summary>
public static class ModelLib
{
    private static readonly Dictionary<string, ArrayMesh> Meshes = new();
    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static readonly Dictionary<string, Vector3> Origins = new();

    public static string Path(string model) => $"res://assets/models/{model}.glb";

    /// <summary>The model's meshes merged into one surface (all Hamlet models share one palette material).</summary>
    public static ArrayMesh Mesh(string model)
    {
        if (Meshes.TryGetValue(model, out var cached)) return cached;
        var root = GD.Load<PackedScene>(Path(model)).Instantiate<Node3D>();
        var st = new SurfaceTool();
        StandardMaterial3D? src = null;
        bool soft = false;
        Collect(root, Transform3D.Identity, st, ref src, ref soft);
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, Material(src, soft));
        root.Free();
        Meshes[model] = mesh;
        return mesh;
    }

    /// <summary>
    /// One named node of a model (and its children) as a merged mesh in that node's own frame: its translation is
    /// left out, so the node's origin is the pivot (the river models put hinges and mast steps there). Cached.
    /// </summary>
    public static ArrayMesh Part(string model, string node)
    {
        string key = model + "/" + node;
        if (Meshes.TryGetValue(key, out var cached)) return cached;
        var root = GD.Load<PackedScene>(Path(model)).Instantiate<Node3D>();
        var n = Find(root, node) ?? throw new System.InvalidOperationException($"{model}.glb has no node '{node}'");
        var st = new SurfaceTool();
        StandardMaterial3D? src = null;
        bool soft = false;
        // The node itself at the identity: its children keep their offsets from it.
        Collect(n, Transform3D.Identity * (n is Node3D n3 ? n3.Transform.AffineInverse() : Transform3D.Identity), st, ref src, ref soft);
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, Material(src, soft));
        root.Free();
        Meshes[key] = mesh;
        return mesh;
    }

    /// <summary>Translation of a named node in its model (the pivot the game rotates a part about).</summary>
    public static Vector3 PartOrigin(string model, string node)
    {
        string key = model + "/" + node;
        if (Origins.TryGetValue(key, out var o)) return o;
        var root = GD.Load<PackedScene>(Path(model)).Instantiate<Node3D>();
        var n = Find(root, node) as Node3D;
        var at = n?.Position ?? Vector3.Zero;
        root.Free();
        Origins[key] = at;
        return at;
    }

    private static Node? Find(Node n, string name)
    {
        if (n.Name == name) return n;
        foreach (var c in n.GetChildren()) { var f = Find(c, name); if (f != null) return f; }
        return null;
    }

    /// <summary>A part of a model as a node with its own mesh, placed at its pivot (see <see cref="Part"/>).</summary>
    public static MeshInstance3D PartInstance(string model, string node, string name)
        => new() { Name = name, Mesh = Part(model, node), Position = PartOrigin(model, node) };

    /// <summary>Whether the model is drawn with soft ink (its nodes say so in their glTF extras).</summary>
    public static bool IsSoft(string model)
    {
        var m = Mesh(model).SurfaceGetMaterial(0) as StandardMaterial3D;
        return m != null && m.StencilMode == BaseMaterial3D.StencilModeEnum.Custom;
    }

    private static void Collect(Node n, Transform3D parent, SurfaceTool st, ref StandardMaterial3D? src, ref bool soft)
    {
        var xf = n is Node3D n3 ? parent * n3.Transform : parent;
        soft |= HasSoftInk(n);
        if (n is MeshInstance3D mi && mi.Mesh != null)
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                st.AppendFrom(mi.Mesh, s, xf);
                src ??= mi.Mesh.SurfaceGetMaterial(s) as StandardMaterial3D ?? mi.GetActiveMaterial(s) as StandardMaterial3D;
            }
        foreach (var c in n.GetChildren()) Collect(c, xf, st, ref src, ref soft);
    }

    private static bool HasSoftInk(Node n)
    {
        if (!n.HasMeta("extras")) return false;
        var e = n.GetMeta("extras");
        return e.VariantType == Variant.Type.Dictionary && e.AsGodotDictionary().TryGetValue("ink", out var v) && v.AsString() == "soft";
    }

    private static StandardMaterial3D Material(StandardMaterial3D? src, bool soft)
    {
        var m = new StandardMaterial3D
        {
            AlbedoTexture = src?.AlbedoTexture ?? GD.Load<Texture2D>("res://assets/textures/palette.png"),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Lambert,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            MetallicSpecular = 0f,
            Roughness = 1f,
        };
        if (soft)
        {
            m.StencilMode = BaseMaterial3D.StencilModeEnum.Custom;
            m.StencilFlags = 2; // write
            m.StencilCompare = BaseMaterial3D.StencilCompareEnum.Always;
            m.StencilReference = 1;
        }
        return m;
    }

    /// <summary>A model as a MeshInstance3D node (one draw call), for things that should be nodes: buildings, bridges.</summary>
    public static MeshInstance3D Instance(string model, string name)
    {
        return new MeshInstance3D { Name = name, Mesh = Mesh(model) };
    }

    /// <summary>A colour texture with mipmaps (ground tiles, paper, water), so it does not shimmer when zoomed out.</summary>
    public static Texture2D MipTexture(string path)
    {
        if (Textures.TryGetValue(path, out var t)) return t;
        var src = GD.Load<Texture2D>(path);
        var img = src.GetImage();
        if (img.IsCompressed()) img.Decompress();
        if (!img.HasMipmaps()) img.GenerateMipmaps();
        t = ImageTexture.CreateFromImage(img);
        Textures[path] = t;
        return t;
    }

    /// <summary>A MultiMesh of one model with room for <paramref name="capacity"/> instances (none visible yet).</summary>
    public static MultiMeshInstance3D Multi(string model, string name, int capacity)
    {
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = capacity,
            Mesh = Mesh(model),
        };
        mm.VisibleInstanceCount = 0;
        return new MultiMeshInstance3D { Name = name, Multimesh = mm };
    }
}

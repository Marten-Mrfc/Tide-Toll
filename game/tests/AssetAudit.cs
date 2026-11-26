using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// The asset audit (tools/asset-audit.sh): every model the game shows, pictured in the running game (its ground,
/// sun, ambient light and ink pass), interface hidden. Two pictures of 640 x 480 per subject:
/// &lt;name&gt;-normal.png at the play zoom and tilt (the same metres per pixel as the start view at 1920 x 1080), one
/// villager beside it for scale; &lt;name&gt;-close.png filling the picture, three-quarter view (yaw 35 degrees) at the
/// game's tilt for that zoom. Subjects: every file in assets/models, plus villagers with each cargo and at each Load
/// step (built as TownVillagers builds them). Writes &lt;out&gt;/models.jsonl (name, file, triangles, size in metres).
/// Arguments after "--": --out-dir=DIR, --only=name,name, --spot=x,y (map cell the subjects stand on).
/// </summary>
public partial class AssetAudit : TownTestBase
{
    protected override string Marker => "ASSET AUDIT PASS";
    protected override Vector2I WindowSize => new(1920, 1080);

    private string _dir = "";
    private HashSet<string>? _only;
    private Vector2 _spot = new(104, 24);
    private readonly List<string> _lines = new();

    private sealed record Subject(string Name, string File, Func<Node3D> Build, bool Scale);

    protected override async Task Run()
    {
        _dir = ProjectSettings.GlobalizePath("res://../build/n3/assets-audit/before");
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--out-dir=")) _dir = a[10..];
            else if (a.StartsWith("--only=")) _only = new HashSet<string>(a[7..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            else if (a.StartsWith("--spot="))
            {
                var p = a[7..].Split(',');
                _spot = new Vector2(float.Parse(p[0]), float.Parse(p[1]));
            }
        }
        System.IO.Directory.CreateDirectory(_dir);
        Main.Hud.Visible = false;
        // The map's own trees would stand in front of the subjects: hidden (trees are pictured as a group below).
        if (Main.MapOrigin.GetNodeOrNull<Node3D>("Trees") is { } trees) trees.Visible = false;
        await Frames(4);
        // The play zoom: the follow view of the starting town at 1920 x 1080, kept at the same metres per pixel.
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Frames(2);
        float play1080 = Main.Rig.Camera.Size;
        float walker = Main.Villagers.WalkerScale;
        Main.Rig.UserMoved = true;
        GetWindow().Size = new Vector2I(640, 480);
        await Frames(6);
        float playSize = play1080 * 480f / 1080f;
        Notes.Add($"play1080={play1080:0.0} play640={playSize:0.0} walker={walker:0.00}");

        var subjects = Subjects(walker);
        int made = 0;
        foreach (var s in subjects)
        {
            if (_only != null && !_only.Contains(s.Name)) continue;
            await Shoot(s, playSize, walker);
            made++;
        }
        await TownShots(playSize);
        System.IO.File.WriteAllLines(System.IO.Path.Combine(_dir, _only == null ? "models.jsonl" : "models-only.jsonl"), _lines);
        Notes.Add($"made={made} dir={_dir}");
    }

    private List<Subject> Subjects(float walker)
    {
        var list = new List<Subject>();
        var dir = DirAccess.Open("res://assets/models");
        Check(dir != null, "no res://assets/models (run tools/sync-assets.sh)");
        var files = dir!.GetFiles().Where(f => f.EndsWith(".glb")).OrderBy(f => f).ToList();
        foreach (var f in files)
        {
            string name = f[..^4];
            bool person = name.StartsWith("villager");
            list.Add(new Subject(name, "assets/models/" + f, () => Model(name, person ? walker : 1f), !person));
        }
        // Villagers as the game draws them: each cargo at the first Load step, grain at every Load step.
        foreach (Good g in new[] { Good.Water, Good.Grain, Good.Flour, Good.Wool, Good.Cloth })
            list.Add(new Subject($"villager-carrying-{g.ToString().ToLowerInvariant()}", "code: game/scripts/town/TownVillagers.cs",
                () => Carrier(g, 0, walker), false));
        for (int lv = 1; lv <= 3; lv++)
        {
            int l = lv;
            list.Add(new Subject($"villager-load-step-{l}", "code: game/scripts/town/TownVillagers.cs", () => Carrier(Good.Grain, l, walker), false));
        }
        for (int e = 1; e <= 2; e++)
        {
            int ext = e;
            float wide = ext > 1 ? 1.8f : 1.5f;
            list.Add(new Subject($"cottage-extension-{ext}", "code: game/scripts/town/TownBuildings.cs (cottage-0" + (1 + ext) + ".glb widened)", () =>
            {
                var n = Model($"cottage-0{1 + ext}", 1f);
                ((Node3D)n.GetChild(0)).Scale = new Vector3(wide, 1, 1);
                return n;
            }, true));
        }
        list.Add(new Subject("trees-group", "code: game/scripts/town/TownTrees.cs (tree-oak, tree-poplar, tree-willow)", TreeGroup, true));
        return list;
    }

    private static Node3D TreeGroup()
    {
        var n = new Node3D { Name = "Subject" };
        (string m, float x, float z, float yaw)[] at =
        {
            ("tree-oak", -3f, 1f, 0f), ("tree-poplar", 2.5f, -2f, 1f), ("tree-willow", 3f, 3.5f, 2f), ("tree-oak", -1f, -4.5f, 2.5f), ("tree-poplar", -5.5f, -2.5f, 0.5f),
        };
        foreach (var (m, x, z, yaw) in at)
            n.AddChild(new MeshInstance3D { Mesh = ModelLib.Mesh(m), Position = new Vector3(x, 0, z), Rotation = new Vector3(0, yaw, 0) });
        return n;
    }

    private static Node3D Model(string name, float scale)
    {
        var n = new Node3D { Name = "Subject" };
        var mi = ModelLib.Instance(name, "Mesh");
        mi.Scale = Vector3.One * scale;
        n.AddChild(mi);
        return n;
    }

    /// <summary>A walker with a load, placed as TownVillagers places it (yaw 0: facing +Z).</summary>
    private static Node3D Carrier(Good g, int lv, float w)
    {
        var n = Model("villager", w);
        float lift = g == Good.Water ? 2.05f : 2.7f;
        float ls = TownVillagers.LoadScale(lv);
        var lb = Basis.Identity.Scaled(Vector3.One * w * ls * (g == Good.Water ? 1.45f : 1.7f));
        string model = TownVillagers.LoadModel(g);
        n.AddChild(new MeshInstance3D { Mesh = ModelLib.Mesh(model), Transform = new Transform3D(lb, new Vector3(0, (lift + (ls - 1f) * 0.6f) * w, 0)) });
        if (lv >= 2)
            n.AddChild(new MeshInstance3D { Mesh = ModelLib.Mesh(model), Transform = new Transform3D(lb.Scaled(Vector3.One * 0.85f), new Vector3(0, (lift - 0.7f) * w, -0.75f * w)) });
        return n;
    }

    private static Aabb Bounds(Node3D root)
    {
        Aabb? box = null;
        foreach (var mi in All(root))
        {
            var b = mi.Transform * mi.Mesh.GetAabb();
            box = box == null ? b : box.Value.Merge(b);
        }
        return box ?? new Aabb(Vector3.Zero, Vector3.One);
    }

    private static IEnumerable<MeshInstance3D> All(Node n)
    {
        if (n is MeshInstance3D mi && mi.Mesh != null) yield return mi;
        foreach (var c in n.GetChildren()) foreach (var m in All(c)) yield return m;
    }

    private static int Triangles(Node3D root)
    {
        int t = 0;
        foreach (var mi in All(root))
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                var arr = mi.Mesh.SurfaceGetArrays(s);
                var idx = arr[(int)Mesh.ArrayType.Index];
                t += idx.VariantType != Variant.Type.Nil && idx.AsInt32Array().Length > 0
                    ? idx.AsInt32Array().Length / 3
                    : arr[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length / 3;
            }
        return t;
    }

    private async Task Shoot(Subject s, float playSize, float walker)
    {
        var at = new Vector3((_spot.X + 0.5f) * TownGround.Cell, 0, (_spot.Y + 0.5f) * TownGround.Cell);
        var subject = s.Build();
        subject.Position = at;
        Main.MapOrigin.AddChild(subject);
        var box = Bounds(subject);
        Node3D? buddy = null;
        if (s.Scale)
        {
            buddy = Model("villager", walker);
            buddy.Position = at + new Vector3(box.End.X + 1.2f * walker, 0, Math.Min(box.End.Z, 1.5f));
            Main.MapOrigin.AddChild(buddy);
        }
        var size = box.Size;
        _lines.Add(new JsonObject
        {
            ["name"] = s.Name, ["file"] = s.File, ["triangles"] = Triangles(subject),
            ["size_m"] = new JsonArray(Math.Round(size.X, 2), Math.Round(size.Y, 2), Math.Round(size.Z, 2)),
        }.ToJsonString());

        // Normal: yaw 0, the play zoom.
        Main.Rig.Yaw.Rotation = Vector3.Zero;
        var c = box.GetCenter();
        Main.Rig.Place(Centre(at + new Vector3(c.X, 0, c.Z), c.Y, playSize, 0f), playSize);
        await Snap(s.Name + "-normal");

        // Close: three-quarter view filling the picture.
        if (buddy != null) buddy.Visible = false;
        float yaw = Mathf.DegToRad(35f);
        Main.Rig.Yaw.Rotation = new Vector3(0, yaw, 0);
        float p = Mathf.DegToRad(Main.Rig.PitchFor(8f));
        // Screen extent of the box turned by the yaw: across (width) and, tilted, its depth plus its height.
        float cy = MathF.Abs(MathF.Cos(yaw)), sy = MathF.Abs(MathF.Sin(yaw));
        float across = size.X * cy + size.Z * sy;
        float depth = size.X * sy + size.Z * cy;
        float tall = depth * MathF.Sin(p) + size.Y * MathF.Cos(p);
        float close = Math.Max(1.2f, Math.Max(tall, across * 480f / 640f) * 1.15f);
        Main.Rig.Place(Centre(at + new Vector3(c.X, 0, c.Z), c.Y, close, yaw), close);
        await Snap(s.Name + "-close");

        Main.Rig.Yaw.Rotation = Vector3.Zero;
        subject.QueueFree();
        buddy?.QueueFree();
        await Frames(1);
    }

    /// <summary>The ground point under the screen centre that puts a point at height h in the middle of the picture.</summary>
    private Vector3 Centre(Vector3 ground, float h, float size, float yaw)
    {
        float p = Mathf.DegToRad(Main.Rig.PitchFor(size));
        var fwd = new Vector3(-MathF.Sin(yaw), 0, -MathF.Cos(yaw));
        return ground + fwd * (h / MathF.Max(0.2f, MathF.Tan(p)));
    }

    private async Task Snap(string name)
    {
        // Held by hand: otherwise Main's follow view (on whenever something sets UserMoved back) takes the camera.
        Main.Rig.UserMoved = true;
        var size = Main.Rig.Camera.Size;
        await Frames(4);
        if (MathF.Abs(Main.Rig.Camera.Size - size) > 0.01f) Notes.Add($"{name}: view moved ({size:0.0} -> {Main.Rig.Camera.Size:0.0})");
        var img = GetViewport().GetTexture().GetImage();
        if (img.GetSize() != new Vector2I(640, 480)) img.Resize(640, 480, Image.Interpolation.Lanczos);
        img.SavePng(System.IO.Path.Combine(_dir, name + ".png"));
    }
}

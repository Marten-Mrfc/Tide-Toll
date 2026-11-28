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
    private bool _noTown, _nowWork, _inkLab, _inkWin, _inkTown, _inkCheck, _inkBoard;
    private HashSet<string>? _inkNames;
    private Vector2 _spot = new(104, 24);
    private readonly List<string> _lines = new();

    private sealed record Subject(string Name, string File, Func<Node3D> Build, bool Scale);

    protected override async Task Run()
    {
        _dir = ProjectSettings.GlobalizePath("res://../build/n3/assets-audit/before");
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--out-dir=")) _dir = a[10..];
            else if (a == "--no-town") _noTown = true;
            else if (a == "--now-work") _nowWork = true;
            else if (a == "--ink-lab") _inkLab = true;       // research n8 (AssetAudit.Ink.cs)
            else if (a == "--ink-windows") _inkWin = true;
            else if (a == "--ink-town") _inkTown = true;
            else if (a == "--ink-check") _inkCheck = true;   // game-ink (AssetAudit.InkCheck.cs)
            else if (a == "--ink-board") _inkBoard = true;   // n8 before/after pictures (AssetAudit.InkBoard.cs)
            else if (a.StartsWith("--ink-variants=")) _inkNames = new HashSet<string>(a[15..].Split(','));
            else if (a == "--trades") _trades = true;
            else if (a == "--trades2") _trades2 = true;   // n7 step 3: the second age's headwear (AssetAudit.Trades2.cs)   // n7 step 2: the trades' headwear (AssetAudit.Trades.cs)
            else if (a == "--landing") _landing = true;   // night 6: the landing now and planned (AssetAudit.Landing.cs)   // night 5: real frames at the workplaces (AssetAudit.NowWork.cs)
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
        if (Main.MapOrigin.GetNodeOrNull<Node3D>("Life") is { } life) life.Visible = false;   // night 3 (LF): animals and birds
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

        if (_trades) { await TradesShots(play1080, walker); return; }
        if (_trades2) { await Trades2Shots(play1080, walker); return; }
        if (_inkCheck || _inkBoard)
        {
            if (_inkCheck) await InkCheck(playSize, play1080, walker);
            if (_inkBoard) await InkBoard(play1080, walker);
            return;
        }
        if (_inkLab || _inkWin || _inkTown)
        {
            if (_inkLab) await InkLab(playSize, walker);
            if (_inkTown) await InkTown(play1080, _inkNames);
            if (_inkWin) await InkWindows();
            Notes.Add($"ink lab dir={_dir}");
            return;
        }
        var subjects = Subjects(walker);
        int made = 0;
        foreach (var s in subjects)
        {
            if (_only != null && !_only.Contains(s.Name)) continue;
            await Shoot(s, playSize, walker);
            made++;
        }
        // Night 3: when only book subjects are asked for, the book is pictured at once on the new town (it needs no
        // played town), instead of after the bot's whole run to the seal.
        if (_landing) await LandingShots(playSize);
        else if (_nowWork) await NowWorkShots(playSize);
        else if (!_noTown && _only != null && _only.Count > 0 && System.Linq.Enumerable.All(_only, n => n.StartsWith("book-")))
        {
            _play = playSize;
            await Frames(30);
            await BookShots();
        }
        else if (!_noTown) await TownShots(playSize);
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
            list.Add(new Subject($"cottage-extension-{ext}", "code: game/scripts/town/TownBuildings.Cottages.cs (cottage-a" + ext + ".glb as the game places it)", () =>
            {
                var n = new Node3D { Name = "Subject" };
                n.AddChild(TownBuildings.CottageNode(0, ext, 6f, 6f));
                return n;
            }, true));
        }
        // Night 3: the nine cottages as the game places them (rows: plain, once, twice; columns: variants a, b, c).
        list.Add(new Subject("cottages-all", "code: game/scripts/town/TownBuildings.Cottages.cs (cottage-{a,b,c}{0,1,2}.glb)", () =>
        {
            var n = new Node3D { Name = "Subject" };
            for (int e = 0; e < 3; e++)
                for (int v = 0; v < 3; v++)
                {
                    var c = TownBuildings.CottageNode(v, e, 6f, 6f);
                    c.Position = new Vector3((v - 1) * 7f, 0, (e - 1) * 7f);
                    n.AddChild(c);
                }
            return n;
        }, true));
        // Night 3: a street: five cottages of mixed variants and sizes in a row, a villager carrying each good walking past.
        list.Add(new Subject("street", "code: TownBuildings.Cottages.cs + TownVillagers.cs", () => Street(walker), false));
        list.Add(new Subject("villagers-group", "code: game/scripts/town/TownVillagers.cs (heads and tunics by index)", () =>
        {
            var n = new Node3D { Name = "Subject" };
            for (int i = 0; i < 6; i++)
            {
                var f = TownVillagers.Figure(i, walker, i % 2 == 0 ? Good.None : (Good)(1 + i % 5), 0, i % 2, trade: GroupTrades[i]);
                f.Position = new Vector3((i % 3 - 1) * 1.6f * walker, 0, (i / 3) * 1.8f * walker);
                f.Rotation = new Vector3(0, 0.4f * (i - 2), 0);
                n.AddChild(f);
            }
            return n;
        }, false));
        list.Add(new Subject("trees-group", "code: game/scripts/town/TownTrees.cs (tree-oak, -poplar, -willow, -birch, -hawthorn)", TreeGroup, true));
        AddWorkSubjects(list, walker);   // night 5: the "work you can watch" mock-up (AssetAudit.Work.cs)
        list.Add(new Subject("wood-and-field", "code: the five tree kinds beside the three field states, with a villager", WoodAndField, true));
        return list;
    }

    private static Node3D TreeGroup()
    {
        var n = new Node3D { Name = "Subject" };
        (string m, float x, float z, float yaw)[] at =
        {
            ("tree-oak", -3f, 1f, 0f), ("tree-poplar", 2.5f, -2f, 1f), ("tree-willow", 3f, 3.5f, 2f), ("tree-birch", -1f, -4.5f, 2.5f), ("tree-hawthorn", -5.5f, -2.5f, 0.5f),
        };
        foreach (var (m, x, z, yaw) in at)
            n.AddChild(new MeshInstance3D { Mesh = ModelLib.Mesh(m), Position = new Vector3(x, 0, z), Rotation = new Vector3(0, yaw, 0) });
        return n;
    }

    /// <summary>Night 3: a wood of the five kinds at the edge of three fields (sprouting, ripe, stubble), a villager between.</summary>
    private Node3D WoodAndField()
    {
        var n = new Node3D { Name = "Subject" };
        (string m, float x, float z, float yaw)[] at =
        {
            ("tree-oak", -14f, -9f, 0f), ("tree-birch", -10f, -10.5f, 1f), ("tree-poplar", -6f, -11f, 2f), ("tree-hawthorn", -2f, -10f, 0.3f),
            ("tree-willow", 2.5f, -10.5f, 2.5f), ("tree-hawthorn", 6f, -10f, 3.1f), ("tree-oak", 10f, -11f, 1.4f), ("tree-birch", 14f, -10f, 0.7f),
            ("field-sprouting", -11f, 0f, 0f), ("field-ripe", 0f, 0f, 0f), ("field-stubble", 11f, 0f, 0f),
        };
        foreach (var (m, x, z, yaw) in at)
        {
            var mi = new MeshInstance3D { Mesh = ModelLib.Mesh(m), Position = new Vector3(x, 0, z), Rotation = new Vector3(0, yaw, 0) };
            if (m.StartsWith("field")) mi.Scale = new Vector3(0.66f, 1f, 0.66f);
            n.AddChild(mi);
        }
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

    /// <summary>A walker with a load, built as TownVillagers draws it (yaw 0: facing +Z).</summary>
    private static Node3D Carrier(Good g, int lv, float w)
    {
        var n = new Node3D { Name = "Subject" };
        n.AddChild(TownVillagers.Figure((int)g, w, g, lv, 1f));
        return n;
    }

    /// <summary>Five cottages of mixed variants and sizes side by side, doors to the lane (+Z), carriers walking past.</summary>
    private static Node3D Street(float w)
    {
        var n = new Node3D { Name = "Subject" };
        int[] ext = { 0, 2, 1, 0, 2 };
        for (int k = 0; k < 5; k++)
        {
            var c = TownBuildings.CottageNode(k, ext[k], 6f, 6f);
            c.Position = new Vector3((k - 2) * 6.2f, 0, 0);
            n.AddChild(c);
        }
        Good[] goods = { Good.Water, Good.Grain, Good.Flour, Good.Wool, Good.Cloth, Good.Grain };
        for (int i = 0; i < goods.Length; i++)
        {
            var f = TownVillagers.Figure(i, w, goods[i], 0, 1f, i == 5 ? ModelLib.Pick("villager-cargo-timber", "villager-cargo-grain") : null);
            f.Position = new Vector3((i - 2.5f) * 4.6f, 0, 4.6f);
            f.Rotation = new Vector3(0, MathF.PI / 2f, 0);
            n.AddChild(f);
        }
        return n;
    }

    private static Aabb Bounds(Node3D root)
    {
        Aabb? box = null;
        foreach (var mi in All(root))
        {
            // Night 3: the mesh's transform relative to the subject (nested nodes, such as a cottage's scale and shift).
            var xf = mi.Transform;
            for (var q = mi.GetParent() as Node3D; q != null && q != root; q = q.GetParent() as Node3D) xf = q.Transform * xf;
            var b = xf * mi.Mesh.GetAabb();
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
        // The street is wider than the play view: drawn 20 percent further out so its five cottages show whole.
        float normal = s.Name == "street" ? playSize * 1.2f : playSize;
        var wf = WorkFrame(s.Name);   // night 5: the mock-up's tableaux have a framing of their own
        if (wf != null) c = wf.Value.c;
        Main.Rig.Place(Centre(at + new Vector3(c.X, 0, c.Z), c.Y, normal, 0f), normal);
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
        if (wf != null) { close = wf.Value.size; GetWindow().Size = new Vector2I(1280, 960); await Frames(6); }
        Main.Rig.Place(Centre(at + new Vector3(c.X, 0, c.Z), c.Y, close, yaw), close);
        await Snap(s.Name + "-close");
        if (wf != null) { GetWindow().Size = new Vector2I(640, 480); await Frames(6); }

        Main.Rig.Yaw.Rotation = Vector3.Zero;
        subject.QueueFree();
        buddy?.QueueFree();
        WorkCleanup();
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
        if (img.GetSize() != new Vector2I(640, 480) && !name.StartsWith("work-")) img.Resize(640, 480, Image.Interpolation.Lanczos);
        img.SavePng(System.IO.Path.Combine(_dir, name + ".png"));
    }
}

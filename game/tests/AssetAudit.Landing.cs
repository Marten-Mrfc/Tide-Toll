using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Night 6 mock-up (docs/plans/n6/boats-design.md, docs/plans/n6/mockup/notes.md): the landing now and planned, in
/// one run (--landing). The town bot plays the first age to the charter, the second age is rung in, and the town runs
/// on (bot playing) until boats lie at the landing with villagers bringing goods: that real frame is taken
/// (work-landing-now-close / -normal). Then, on the same river and with the same camera, the landing, crane, boats and
/// villagers of the game are hidden and the designed landing is staged in their place with the quay-*.glb models
/// (assets/src/models/quay-scenes.py), the game's rowboat and barge and posed villagers (work-landing-planned-*).
/// Nothing of the game is changed; the hidden nodes are shown again afterwards.
/// </summary>
public partial class AssetAudit
{
    private bool _landing;

    private async Task LandingShots(float playSize)
    {
        _play = playSize;
        if (Main.MapOrigin.GetNodeOrNull<Node3D>("Trees") is { } trees) trees.Visible = true;
        if (Main.MapOrigin.GetNodeOrNull<Node3D>("Life") is { } life) life.Visible = true;
        var bot = new TownBot(BotKind.Payback, 1);
        Main.Driver.RunBot(bot, t => t.CanRingNextAge, 300 * 60 * TownRules.TickHz);
        Notes.Add($"landing: charter={T.CanRingNextAge} level={T.Level}");
        T.RingNextAge();
        for (int i = 0; i < 400 && !Main.River.Built; i++) await Frames(1);
        await Unblock();
        // On until the crane stands and boats lie at both berths (or one, after a long while), at high water.
        static int Moored(Town t) { int n = 0; for (int i = 0; i < t.BoatCount; i++) if (t.Boat(i).Phase == BoatPhase.Moored) n++; return n; }
        int ran = Main.Driver.RunBot(bot, t => t.CraneFactor > 1.0001 && Moored(t) >= 2 && t.HighWater, 50 * 60 * TownRules.TickHz);
        if (Moored(T) < 1) ran += Main.Driver.RunBot(bot, t => Moored(t) >= 1, 10 * 60 * TownRules.TickHz);
        await Frames(16);
        await Unblock();
        Notes.Add($"landing: ran={ran} moored={Moored(T)} crane={T.CraneFactor:0.00} high={T.HighWater} level={T.Level}");

        var rp = T.RiverPlan!;
        int n = T.Plan.Map.Size;
        int EdgeW(int y) { for (int x = 0; x < n; x++) if (T.Plan.Map.Water[y * n + x] != 0) return x; return n / 2; }
        int yL = rp.LandingRow;
        var origin = Cells(EdgeW(yL), yL);                 // the water's edge at the landing's row (+X the water, +Z down-river)
        _spot = new Vector2(EdgeW(yL) - 0.5f, yL - 0.5f);  // so the trail painter's spot is the origin too
        var look = origin + new Vector3(-1.5f, 0, 2.5f);

        GetWindow().Size = new Vector2I(1280, 960);
        await Frames(6);
        await LandingShoot("work-landing-now", look, 24f);

        // Hide the game's landing, crane, tide post, boats and villagers; stage the design in their place.
        var hidden = new System.Collections.Generic.List<Node3D>();
        void Hide(Node? nd) { if (nd is Node3D n3 && n3.Visible) { n3.Visible = false; hidden.Add(n3); } }
        Hide(Main.River.FindChild("RiverPlot0", true, false));
        // The crane and the tide post are set visible by the river layer every frame: moved out of sight instead.
        var moved = new System.Collections.Generic.List<(Node3D, Vector3)>();
        foreach (var nm in new[] { "Crane", "TidePost" })
            if (Main.River.FindChild(nm, true, false) is Node3D mv) { moved.Add((mv, mv.Position)); mv.Position += new Vector3(0, -500f, 0); }
        Hide(Main.MapOrigin.FindChild("Boats", true, false));
        Hide(Main.Villagers);
        var stage = new Node3D { Name = "LandingPlanned", Position = origin };
        Main.MapOrigin.AddChild(stage);
        StageLanding(stage, rp, EdgeW, origin);
        await Frames(10);
        await LandingShoot("work-landing-planned", look, 24f);
        stage.QueueFree();
        foreach (var h in hidden) h.Visible = true;
        foreach (var (mv, at) in moved) mv.Position = at;
        WorkCleanup();
        GetWindow().Size = new Vector2I(640, 480);
        await Frames(6);
    }

    private async Task LandingShoot(string name, Vector3 local, float close)
    {
        Main.Rig.Yaw.Rotation = Vector3.Zero;
        Main.Rig.Place(Centre(local, 1f, _play, 0f), _play);
        await Snap(name + "-normal");
        float yaw = Mathf.DegToRad(35f);
        Main.Rig.Yaw.Rotation = new Vector3(0, yaw, 0);
        Main.Rig.Place(Centre(local, 1f, close, yaw), close);
        await Snap(name + "-close");
        Main.Rig.Yaw.Rotation = Vector3.Zero;
    }

    /// <summary>A hull at its game length (RiverBoats.Hull), bow along heading yaw (0 = +Z, down-river).</summary>
    private static Node3D Boat(Node3D parent, string model, float length, Vector3 pos, float yaw)
    {
        var aabb = ModelLib.Mesh(model).GetAabb();
        float l = Math.Max(0.1f, Math.Max(aabb.Size.X, aabb.Size.Z));
        var root = new Node3D { Position = pos, Rotation = new Vector3(0, yaw, 0) };
        var hull = new Node3D { Scale = Vector3.One * (length / l) };
        var m = ModelLib.Instance(model, "Model");
        if (aabb.Size.X > aabb.Size.Z) m.Rotation = new Vector3(0, MathF.PI / 2f, 0);
        hull.AddChild(m);
        root.AddChild(hull);
        parent.AddChild(root);
        return root;
    }

    /// <summary>A line or plank model laid from a to b (its length along +X in the model).</summary>
    private static void Span(Node3D parent, string model, Vector3 a, Vector3 b, float thick = 1f)
    {
        var d = b - a;
        float len = d.Length();
        var mi = new MeshInstance3D { Mesh = ModelLib.Mesh(model) };
        var x = d / len;
        var z = x.Cross(Vector3.Up).Normalized();
        var y = z.Cross(x);
        mi.Transform = new Transform3D(new Basis(x * len, y * thick, z * thick), a);
        parent.AddChild(mi);
    }

    private void StageLanding(Node3D s, RiverPlan rp, Func<int, int> edgeW, Vector3 origin)
    {
        float w = Main.Villagers.WalkerScale;
        int yL = rp.LandingRow;
        float Water(float z) => (edgeW(Math.Clamp(yL + (int)MathF.Floor(z / TownGround.Cell), 0, rp.CourseX.Length - 1)) - edgeW(yL)) * TownGround.Cell;
        float Mid(float z) => ((float)rp.CourseX[Math.Clamp(yL + (int)MathF.Floor(z / TownGround.Cell), 0, rp.CourseX.Length - 1)] - edgeW(yL)) * TownGround.Cell;
        const float North = MathF.PI, South = 0f;

        // The lane to the yard, the game's worn trail, from the bank road to the yard's south side.
        Trail(new Vector3(-14f, 0, 13f), new Vector3(-9.5f, 0, 9f), new Vector3(-7.6f, 0, 4f), new Vector3(-7.4f, 0, -1f), new Vector3(-7.6f, 0, -7f));
        // The quay on its piles, two berths.
        Put(s, "quay-deck", Vector3.Zero);
        // The yard behind it: a stack per kind.
        Put(s, "quay-stack-grain", new Vector3(-6.4f, 0, -5.2f), 0.05f);
        Put(s, "quay-stack-flour", new Vector3(-6.6f, 0, -2.6f), -0.04f);
        Put(s, "quay-stack-wool", new Vector3(-6.4f, 0, 0.0f), 0.06f);
        Put(s, "quay-stack-cloth", new Vector3(-6.2f, 0, 2.6f), -0.03f);
        // The crane between the berths on the quay's edge, mid-swing toward the rowboat's hold; its treadwheel beside.
        Put(s, "quay-crane", new Vector3(-0.9f, 0, 0.1f), Mathf.DegToRad(55f));
        Put(s, "quay-treadwheel", new Vector3(-3.4f, 0, 2.2f), Mathf.DegToRad(-55f));
        // Signs at the berths' ends, faced to the camera.
        Put(s, "quay-sign-grain", new Vector3(-1.6f, 0, -6.6f), Mathf.DegToRad(35f));
        Put(s, "quay-sign-flour", new Vector3(-1.6f, 0, 6.8f), Mathf.DegToRad(35f));

        // The rowboat at berth A, bow up-river, line to its bollard, gangplank down, hold part full of grain.
        var row = Boat(s, "rowboat", 4.2f, new Vector3(2.95f, 0, -4.6f), North);
        foreach (var (x, z, yw) in new[] { (0.15f, 0.55f, 0.3f), (-0.2f, 0.2f, -0.4f), (0.2f, -0.05f, 1.2f), (-0.1f, 0.85f, 0.9f) })
            Put(row, "work-sack-grain", new Vector3(x, 0.22f, z), yw, 0.55f);
        Span(s, "quay-line", new Vector3(3.0f, 0.55f, -6.6f), new Vector3(1.7f, 0.95f, -6.1f), 1.0f);
        Span(s, "quay-line", new Vector3(2.9f, 0.5f, -2.6f), new Vector3(1.7f, 0.95f, -2.4f), 1.0f);
        Span(s, "quay-gangplank", new Vector3(0.9f, 0.5f, -4.1f), new Vector3(2.45f, 0.42f, -4.1f));
        // The barge at berth B.
        Boat(s, "barge", 8.5f, new Vector3(4.3f, 0, 4.4f), North);
        Span(s, "quay-line", new Vector3(4.0f, 0.7f, 0.4f), new Vector3(1.7f, 0.95f, 2.9f), 1.0f);

        // Waiting at a mooring post below the quay, out of the lanes; another boat passing down the far lane.
        float zw = 14.5f;
        Put(s, "quay-mooring-post", new Vector3(Water(zw - 2.5f) + 0.6f, 0, zw - 2.4f));
        Boat(s, "rowboat", 4.2f, new Vector3(Water(zw) + 1.0f, 0, zw), North + 0.05f);
        Span(s, "quay-line", new Vector3(Water(zw) + 1.0f, 0.5f, zw - 2.0f), new Vector3(Water(zw - 2.5f) + 0.6f, 0.9f, zw - 2.4f), 1.0f);
        float zp = -9.5f, xp = Mid(zp) + 2.3f;
        Boat(s, "rowboat", 4.2f, new Vector3(xp, 0, zp), South);
        Put(s, "quay-wake", new Vector3(xp, 0, zp + 2.1f), MathF.PI);

        // Villagers: one setting a sack down on the grain stack, three waiting in a line on the lane, facing the yard.
        Person(s, 0, w, new Vector3(-6.4f, 0, -3.95f), North + 0.15f, 30f, Good.Grain, 0, 0.9f);
        var line = new[] { new Vector3(-7.3f, 0, 5.0f), new Vector3(-8.1f, 0, 7.0f), new Vector3(-9.1f, 0, 8.9f) };
        var goods = new[] { Good.Flour, Good.Wool, Good.Cloth };
        var yard = new Vector3(-6.4f, 0, -1.0f);
        for (int i = 0; i < line.Length; i++) Person(s, i + 1, w, line[i], Face(line[i], yard), 0f, goods[i]);
        // One on the quay by the crane, working the swing (hand on the net's guide line).
        Person(s, 5, w, new Vector3(0.9f, 0.5f, -2.0f), Face(new Vector3(0.9f, 0, -2f), new Vector3(2f, 0, -5f)), 12f);
    }
}

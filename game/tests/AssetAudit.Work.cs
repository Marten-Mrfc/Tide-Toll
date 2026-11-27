using System;
using System.Reflection;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Night 5 mock-up (docs/plans/n5/work-you-can-watch.md, docs/plans/n5/mockup/notes.md): four staged tableaux of "work
/// you can watch" at the well, a field, the mill and the market stall. Round 2: everything is a model made like the
/// game's own (assets/src/models/work-scenes.py: field rows by state, sheaves and stooks, sickle, bucket and pails, wet
/// ground, sacks, door, hoist, scales, apron, crates, pots), lit by the game's sun with the game's material. A
/// villager's pose is only a transform of the rigid figure (lean about the feet, a little less height, yaw). The lanes
/// are the game's own worn trail: the ground's wear texture is painted along them for the picture and given back after.
/// Only the flour puffs and the dust on the mill's step are plain meshes here (translucent, so the ink pass leaves them).
/// </summary>
public partial class AssetAudit
{
    private void AddWorkSubjects(System.Collections.Generic.List<Subject> list, float w)
    {
        list.Add(new Subject("work-well", "mock-up: well.glb + work-*.glb (assets/src/models/work-scenes.py)", () => WorkWell(w), false));
        list.Add(new Subject("work-field", "mock-up: work-field-bed.glb, work-row-*.glb, work-stook.glb, work-sickle.glb", () => WorkField(w), false));
        list.Add(new Subject("work-mill", "mock-up: mill.glb + work-doorway, -door-leaf, -hoist, -sack-*.glb", () => WorkMill(w), false));
        list.Add(new Subject("work-market", "mock-up: market-stall.glb + work-scales, -crate, -pot, -sack-*, -apron.glb", () => WorkMarket(w), false));
    }

    /// <summary>Framing of each tableau's close picture: the point in the middle (subject-local, y = height) and
    /// the camera's size in metres.</summary>
    private static (Vector3 c, float size)? WorkFrame(string name) => name switch
    {
        "work-well" => (new Vector3(-1.2f, 1.2f, 3.2f), 14f),
        "work-field" => (new Vector3(-0.4f, 0.6f, 1.4f), 17f),
        "work-mill" => (new Vector3(0.6f, 3.2f, 3.0f), 15f),
        "work-market" => (new Vector3(-0.6f, 1.4f, 1.2f), 12f),
        _ => null,
    };

    // ---- helpers ----

    private Vector3 SpotAt => new((_spot.X + 0.5f) * TownGround.Cell, 0, (_spot.Y + 0.5f) * TownGround.Cell);

    /// <summary>A villager as the game draws it, then posed: standing at pos, facing yaw (0 = +Z), leaned forward
    /// leanDeg about the feet, its height times crouch.</summary>
    private static Node3D Person(Node3D parent, int idx, float w, Vector3 pos, float yaw, float leanDeg = 0f,
        Good g = Good.None, int lv = 0, float crouch = 1f, float walk = 0f, string? load = null)
    {
        var f = TownVillagers.Figure(idx, w, g, lv, walk, load);
        f.Position = pos;
        f.Rotation = new Vector3(Mathf.DegToRad(leanDeg), yaw, 0);
        f.Scale = new Vector3(1, crouch, 1);
        parent.AddChild(f);
        return f;
    }

    private static float Face(Vector3 from, Vector3 to) => MathF.Atan2(to.X - from.X, to.Z - from.Z);

    /// <summary>A model at its own origin (the generators put it on the ground), turned by yaw and scaled.</summary>
    private static Node3D Put(Node3D parent, string model, Vector3 pos, float yaw = 0f, float scale = 1f, Vector3? rot = null)
    {
        var mi = new MeshInstance3D { Mesh = ModelLib.Mesh(model), Position = pos, Rotation = rot ?? new Vector3(0, yaw, 0), Scale = Vector3.One * scale };
        parent.AddChild(mi);
        return mi;
    }

    /// <summary>Something held, in the figure's body frame (villager.py metres, scaled by the walker scale).</summary>
    private static Node3D Hold(Node3D fig, string model, float w, Vector3 bodyPos, Vector3 rot, float scale = 1f)
        => Put(fig, model, bodyPos * w, 0f, scale, rot);

    /// <summary>Flour in the air: translucent, so the ink pass (on depth) draws no line round it.</summary>
    private static void Puff(Node3D n, float r, Vector3 pos, float alpha)
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(Palette.Limewash, alpha), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Lambert, SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled, Roughness = 1f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        n.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = r, Height = 1.6f * r, RadialSegments = 12, Rings = 6, Material = mat }, Position = pos, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    // ---- the game's trail painted along the scene's lanes ----

    private TownGround? _painted;

    /// <summary>Paints the ground's wear (as a worn-in trail) on the cells along a polyline of subject-local points.</summary>
    private void Trail(params Vector3[] pts)
    {
        var g = Main.Ground;
        var t = typeof(TownGround);
        const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
        if (t.GetField("_rg", F)?.GetValue(g) is not byte[] rg || t.GetField("_wearImg", F)?.GetValue(g) is not Image img
            || t.GetField("_wearTex", F)?.GetValue(g) is not ImageTexture tex) { Notes.Add("trail: ground fields not found"); return; }
        g.SetProcess(false);
        _painted = g;
        var at = SpotAt;
        for (int k = 0; k + 1 < pts.Length; k++)
        {
            var a = at + pts[k];
            var b = at + pts[k + 1];
            int steps = (int)MathF.Ceiling((b - a).Length() / 0.5f);
            for (int s = 0; s <= steps; s++)
            {
                var p = a.Lerp(b, s / (float)Math.Max(1, steps));
                int cx = (int)MathF.Floor(p.X / TownGround.Cell), cy = (int)MathF.Floor(p.Z / TownGround.Cell);
                if (cx < 0 || cy < 0 || cx >= TownGround.N || cy >= TownGround.N) continue;
                int i = cy * TownGround.N + cx;
                rg[2 * i] = (byte)(TownGround.WornWear * 255f);
            }
        }
        img.SetData(TownGround.N, TownGround.N, false, Image.Format.Rg8, rg);
        tex.Update(img);
    }

    /// <summary>Gives the ground back to the town after a tableau's pictures.</summary>
    private void WorkCleanup()
    {
        if (_painted == null) return;
        _painted.SetProcess(true);
        _painted.Upload();
        _painted = null;
    }

    // ---- the well ----

    private Node3D WorkWell(float w)
    {
        var n = new Node3D { Name = "Subject" };
        float s = 1.6f;
        // The crank side (+X of the model) turned toward the camera (which looks from +X+Z at yaw 35 degrees).
        float wy = 0f;
        var crankDir = new Vector3(MathF.Cos(wy), 0, -MathF.Sin(wy));
        Trail(new Vector3(0.2f, 0, 2.4f), new Vector3(-1.4f, 0, 5.5f), new Vector3(-2.2f, 0, 8.5f), new Vector3(-2.4f, 0, 12f));
        Trail(new Vector3(-1.4f, 0, 5.5f), new Vector3(-5f, 0, 4.4f), new Vector3(-9f, 0, 4.6f));
        Put(n, "work-wet-patch", new Vector3(0, -0.012f, 0), 0.25f);
        Put(n, "well", Vector3.Zero, wy, s);
        // The bucket just come up over the shaft's near rim, its rope running up under the roof to the roller.
        Put(n, "work-bucket", new Vector3(0.35f, 1.85f, 0.55f), 0.3f, 1.05f);
        // Two full pails on the rim, on the lane side.
        Put(n, "work-pail", new Vector3(-1.25f, 1.72f, 0.95f), 0.4f);
        Put(n, "work-pail", new Vector3(-0.55f, 1.72f, 1.55f), 1.2f);
        // The worker at the crank, on the camera's side, leaning into it.
        var wpos = new Vector3(2.75f, 0, 0.9f);
        Person(n, 1, w, wpos, Face(wpos, Vector3.Zero), 28f);
        // Four waiting along the lane, a step apart, facing the well; the first holds an empty pail.
        Vector3[] line = { new(-1.0f, 0, 4.6f), new(-1.6f, 0, 6.6f), new(-2.05f, 0, 8.6f), new(-2.3f, 0, 10.6f) };
        for (int i = 0; i < 4; i++) Person(n, 2 + i, w, line[i], Face(line[i], Vector3.Zero));
        // One leaving along the side lane with the yoke of pails.
        Person(n, 0, w, new Vector3(-6.4f, 0, 4.4f), -MathF.PI / 2f - 0.1f, 0f, Good.Water, 0, 1f, 1f);
        Put(n, "animal-cat", new Vector3(2.6f, 0, 3.6f), 2.2f);
        return n;
    }

    // ---- a field ----

    private Node3D WorkField(float w)
    {
        var n = new Node3D { Name = "Subject" };
        Trail(new Vector3(-12f, 0, 6.6f), new Vector3(-4f, 0, 7.0f), new Vector3(3f, 0, 6.8f), new Vector3(9f, 0, 8.0f), new Vector3(13f, 0, 10.4f));
        // The game fills the plot with its field (TownBuildings: scaled to the footprint, times 0.96); a first-age field
        // plot is wider than deep, as in the real frame: the bed is drawn 0.62 deep, and six rows fit on it.
        var f = new Node3D { Name = "Field", Scale = new Vector3(0.96f, 1, 1) };
        n.AddChild(f);
        var bed = Put(f, "work-field-bed", Vector3.Zero);
        bed.Scale = new Vector3(1, 1, 0.62f);
        string[][] rows =
        {
            new[] { "green", "green", "green", "green", "green", "green" },               // cut earlier, grown back
            new[] { "shoots", "shoots", "shoots", "shoots", "shoots", "shoots", "shoots" }, // cut later, coming up
            new[] { "stubble", "stubble", "stubble", "stubble", "stubble", "stubble", "swath" },
            new[] { "stubble", "stubble", "swath", "edge", "ripe", "ripe", "ripe" },        // reaper 1
            new[] { "stubble", "swath", "edge", "ripe", "ripe", "ripe", "ripe" },           // reaper 2
            new[] { "ripe", "ripe", "ripe", "ripe", "ripe", "ripe" },
        };
        var rng = new Random(5);
        float Z(int r) => (-2.5f + r) * 1.6f;
        for (int r = 0; r < rows.Length; r++)
        {
            int count = rows[r].Length;
            for (int k = 0; k < count; k++)
            {
                float x = (k - (count - 1) / 2f) * 2f;
                if (k == 0) x += 0.2f + (float)rng.NextDouble() * 0.4f;            // ragged row ends
                if (k == count - 1) x -= 0.2f + (float)rng.NextDouble() * 0.4f;
                string st = rows[r][k];
                // the edge piece's standing half is its -X half: turned round, the standing crop is ahead (+X)
                Put(f, "work-row-" + st, new Vector3(x, 0.09f, Z(r)), st == "edge" ? MathF.PI : (k + r) % 2 == 0 ? 0f : MathF.PI);
            }
        }
        // Stooks on the cut rows, not in a line; a sheaf lying at the end of row 2, being lifted.
        Put(f, "work-stook", new Vector3(-4.4f, 0.09f, Z(2) + 0.1f), 0.4f, 1.5f);
        Put(f, "work-stook", new Vector3(-1.0f, 0.09f, Z(2) - 0.2f), 1.3f, 1.45f);
        Put(f, "work-stook", new Vector3(-5.2f, 0.09f, Z(3) + 0.3f), 2.1f, 1.55f);
        // Two reapers, bent, each with a sickle held low in front, the blade toward the standing crop (+X).
        var sickleRot = new Vector3(0.55f, 0, 0);
        var r1 = Person(f, 1, w, new Vector3(-0.6f, 0.12f, Z(3)), MathF.PI / 2f, 32f, crouch: 0.92f);
        Hold(r1, "work-sickle", w, new Vector3(-0.3f, 1.18f, 0.5f), sickleRot, 1.0f);
        var r2 = Person(f, 4, w, new Vector3(-2.6f, 0.12f, Z(4)), MathF.PI / 2f, 30f, crouch: 0.92f);
        Hold(r2, "work-sickle", w, new Vector3(-0.3f, 1.18f, 0.5f), sickleRot, 1.0f);
        // One lifting a sheaf at the end of the finished row.
        var lifter = Person(f, 2, w, new Vector3(5.7f, 0.09f, Z(2) + 0.1f), -MathF.PI / 2f, 30f, crouch: 0.9f);
        Hold(lifter, "work-sheaf", w, new Vector3(0, 0.95f, 0.55f), new Vector3(0.3f, 0, 0), 1.1f);
        // At the row's end: the water jug. Two crows on the shoots; the scarecrow at the far corner.
        Put(f, "work-jug", new Vector3(-7.0f, 0.06f, Z(3) + 0.6f), 0.5f);
        Put(f, "animal-bird", new Vector3(-3.4f, 0.12f, Z(1) + 0.4f), 0.8f);
        Put(f, "animal-bird", new Vector3(-2.2f, 0.12f, Z(1) - 0.3f), 2.6f);
        Put(n, "work-scarecrow", new Vector3(-6.0f, 0.05f, -4.6f), 0.3f);
        // Two waiting at the field's edge on the lane, facing the field; a handcart standing by.
        for (int i = 0; i < 2; i++)
        {
            var p = new Vector3(-4.6f - i * 2.1f, 0, 7.0f);
            Person(n, 3 + 2 * i, w, p, MathF.PI + 0.15f);
        }
        Put(n, ModelLib.Pick("prop-handcart", "villager-handcart"), new Vector3(4.2f, 0, 7.8f), -0.3f, 1.2f);
        return n;
    }

    // ---- the mill ----

    private Node3D WorkMill(float w)
    {
        var n = new Node3D { Name = "Subject" };
        Trail(new Vector3(0.3f, 0, 4.2f), new Vector3(-0.4f, 0, 8f), new Vector3(-2.0f, 0, 12f), new Vector3(-3.5f, 0, 16f));
        Put(n, "mill", Vector3.Zero);
        var b = ModelLib.Mesh("mill").GetAabb();
        float front = b.End.Z;
        // The doorway with the door standing open toward the camera.
        Put(n, "work-doorway", new Vector3(0, 0.22f, front - 0.75f));
        Put(n, "work-door-leaf", new Vector3(0.72f, 0.22f, front - 0.55f), -1.25f);
        // Flour in the air at the door, and dust on the step (translucent: no ink).
        Puff(n, 0.6f, new Vector3(0.35f, 2.0f, front + 0.45f), 0.9f);
        Puff(n, 0.5f, new Vector3(0.95f, 2.6f, front + 0.85f), 0.8f);
        Puff(n, 0.4f, new Vector3(1.45f, 3.1f, front + 1.2f), 0.65f);
        Puff(n, 0.3f, new Vector3(1.85f, 3.5f, front + 1.5f), 0.5f);
        Puff(n, 0.22f, new Vector3(2.1f, 3.85f, front + 1.75f), 0.38f);
        n.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.9f, BottomRadius = 0.9f, Height = 0.01f, RadialSegments = 14, Material = new StandardMaterial3D { AlbedoColor = new Color(Palette.Limewash, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } },
            Position = new Vector3(0.2f, 0.26f, front + 0.1f), Scale = new Vector3(1.3f, 1, 0.6f),
        });
        // The hoist on the camera's side of the tower, a sack on its rope.
        Put(n, "work-hoist", new Vector3(b.End.X - 1.2f, 6.2f, 1.2f), -0.55f);
        // Sacks set down: grain (ochre) stacked on the left, flour (pale, banded) on the right.
        Put(n, "work-sack-grain", new Vector3(-2.1f, 0.0f, front + 0.2f), 0.3f);
        Put(n, "work-sack-grain", new Vector3(-2.9f, 0.0f, front - 0.3f), 1.9f);
        Put(n, "work-sack-grain", new Vector3(-2.45f, 0.0f, front + 1.05f), 3.0f);
        Put(n, "work-sack-grain", new Vector3(-2.4f, 0.0f, front + 0.4f), 0.9f, 0.9f, new Vector3(0, 0.9f, 1.35f)).Position = new Vector3(-2.9f, 0.5f, front + 0.45f);
        Put(n, "work-sack-flour", new Vector3(2.0f, 0.0f, front + 0.3f), 2.6f);
        Put(n, "work-sack-flour", new Vector3(2.7f, 0.0f, front - 0.3f), 1.2f);
        // A villager carrying a grain sack in through the door.
        Person(n, 1, w, new Vector3(-0.95f, 0.22f, front + 0.55f), MathF.PI + 0.5f, 8f, Good.Grain, 0, 1f, 1f);
        // Two waiting in a line by the door, facing it, each with a sack.
        Person(n, 3, w, new Vector3(0.0f, 0, front + 3.4f), MathF.PI - 0.1f, 0f, Good.Grain);
        Person(n, 4, w, new Vector3(-0.5f, 0, front + 5.6f), MathF.PI - 0.1f, 0f, Good.Grain);
        return n;
    }

    // ---- the market stall ----

    private Node3D WorkMarket(float w)
    {
        var n = new Node3D { Name = "Subject" };
        float s = 1.5f;
        Trail(new Vector3(-11f, 0, 4.6f), new Vector3(-3f, 0, 4.2f), new Vector3(4f, 0, 4.8f), new Vector3(11f, 0, 6.5f));
        Put(n, "market-stall", Vector3.Zero, 0f, s);
        var b = ModelLib.Mesh("market-stall").GetAabb();
        float front = b.End.Z * s, table = 1.35f;
        // Goods told by kind: turnips in crates, pots, grain and flour in sacks; the scales in the middle.
        Put(n, "work-crate", new Vector3(-1.9f, table, front - 1.0f), 0.1f);
        Put(n, "work-crate", new Vector3(-1.75f, table + 0.45f, front - 1.15f), -0.15f, 0.9f);
        Put(n, "work-pot", new Vector3(-0.85f, table, front - 0.8f), 0.3f);
        Put(n, "work-pot", new Vector3(-0.4f, table, front - 1.2f), 1.1f, 0.85f);
        Put(n, "work-scales", new Vector3(0.35f, table, front - 0.85f), 0.05f);
        Put(n, "work-sack-flour", new Vector3(1.45f, table, front - 1.1f), 0.3f, 0.7f);
        Put(n, "work-sack-grain", new Vector3(2.15f, table, front - 1.0f), 1.6f, 0.7f);
        // The stallholder in an apron at the table's end (under the awning they would not be seen).
        var keeper = Person(n, 2, w, new Vector3(-3.3f, 0, front - 1.7f), 0.9f);
        Hold(keeper, "work-apron", w, Vector3.Zero, Vector3.Zero, w);
        // A villager setting a sack down on the table.
        var giver = Person(n, 4, w, new Vector3(1.0f, 0, front + 0.9f), MathF.PI, 22f);
        Hold(giver, "work-sack-grain", w, new Vector3(0, 0.95f, 0.62f), new Vector3(0.2f, 0, 0), 0.75f);
        // Another walking away along the lane with empty hands.
        Person(n, 0, w, new Vector3(-4.6f, 0, front + 2.9f), -MathF.PI / 2f + 0.15f, 0f, Good.None, 0, 1f, 1f);
        return n;
    }
}

using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// n7 step 2 mock-up (--trades): villagers as the game draws them now and in the trades' planned headwear
/// (villager-head-kerchief, -strawhat, -cap, villager-apron; round 2: the Helper's hair-only heads
/// villager-head-helper-{mud,earth,soot}), on the game's ground with its ink, at the 1080p play view (yaw 0, the play
/// zoom's tilt) and close up. Test-only: the game does not load the new heads yet. Pictures:
/// trades-{now,planned,bare,helpers}-{normal,close}.png, trades-grid-normal.png (every headwear on every tunic colour,
/// today's five, the two the review proposes, and Mud), trades-loads-normal.png (each trade with its own good and another's). With
/// --ink-variants=villagers-trades-group the ink check measures six villagers in mixed headwear (InkSubject).
/// </summary>
public partial class AssetAudit
{
    private bool _trades;
    // Villager indices: their tunics and today's heads (index % 3: hair, hood, straw hat).
    private static readonly int[] TradeIdx = { 0, 4, 2, 7 };
    private static readonly Good[] TradeGood = { Good.None, Good.Water, Good.Grain, Good.Flour };
    private static readonly string[] TradeHead = { "villager-head-helper-mud", "villager-head-kerchief", "villager-head-strawhat", "villager-head-cap" };
    /// <summary>Today's tunic list (TownVillagers.Clothes) and the two the review proposes in place of River and Thatch.</summary>
    private static readonly Color[] GridTunics =
        { Palette.Brick, Palette.River, Palette.CastIronGreen, Palette.Thatch, Palette.SootBrick, Palette.PackedEarth, Palette.SlateRoof, Palette.Mud };
    /// <summary>The tunics proposed for the build (today's without River and Thatch, with Packed earth and Slate roof).</summary>
    private static readonly Color[] NewTunics = { Palette.Brick, Palette.PackedEarth, Palette.CastIronGreen, Palette.SlateRoof, Palette.SootBrick };

    /// <summary>One villager: today's figure for <paramref name="idx"/>, then (head != null) the planned head, the miller's
    /// apron, and a tunic colour of its own.</summary>
    private static Node3D TradeFigure(int idx, float walker, Good load, int level, string? head, Color? tunic, float lean = 0f)
    {
        var f = TownVillagers.Figure(idx, walker, load, level, lean);
        if (head != null)
        {
            var h = f.GetNode<MeshInstance3D>("Head");
            Check(ModelLib.Has(head), "missing model " + head);
            h.Mesh = ModelLib.Part(head, "head");
            if (head == "villager-head-cap") f.AddChild(new MeshInstance3D { Name = "Apron", Mesh = ModelLib.Part("villager-apron", "apron"), Transform = h.Transform });
        }
        if (tunic is Color c) f.GetNode<MultiMeshInstance3D>("Body").Multimesh.SetInstanceColor(0, c);
        return f;
    }

    private Node3D TradeRow(float walker, bool planned, bool loads)
    {
        var n = new Node3D { Name = "Subject" };
        for (int k = 0; k < 4; k++)
        {
            var f = TradeFigure(TradeIdx[k], walker, loads ? TradeGood[k] : Good.None, 0, planned ? TradeHead[k] : null,
                planned ? NewTunics[TradeIdx[k] % NewTunics.Length] : null, loads && k > 0 ? 1f : 0f);
            f.Position = new Vector3((k - 1.5f) * 1.9f * walker, 0, 0);
            n.AddChild(f);
        }
        return n;
    }

    private static Node3D HelperRow(float walker)
    {
        var n = new Node3D { Name = "Subject" };
        string[] tones = { "mud", "earth", "soot" };
        for (int k = 0; k < 3; k++)
        {
            var f = TradeFigure(k, walker, k == 1 ? Good.Grain : Good.None, 0, "villager-head-helper-" + tones[k], NewTunics[k * 2 % 5], k == 1 ? 1f : 0f);
            f.Position = new Vector3((k - 1f) * 1.9f * walker, 0, 0);
            n.AddChild(f);
        }
        return n;
    }

    /// <summary>Rows: the Helper, carrier, reaper, miller heads; columns: the seven tunic colours of GridTunics.</summary>
    private static Node3D TunicGrid(float walker)
    {
        var n = new Node3D { Name = "Subject" };
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < GridTunics.Length; c++)
            {
                var f = TradeFigure(0, walker, Good.None, 0, TradeHead[r], GridTunics[c]);
                f.Position = new Vector3((c - 3.5f) * 2.0f * walker, 0, (r - 1.5f) * 3.0f * walker);
                n.AddChild(f);
            }
        return n;
    }

    /// <summary>Carrier, reaper, miller with their own good, then with another trade's: carrier with flour, reaper with
    /// pails, miller with grain in a basket (Load step 1).</summary>
    private static Node3D LoadStrip(float walker)
    {
        var n = new Node3D { Name = "Subject" };
        (int head, Good g, int lv)[] who = { (1, Good.Water, 0), (2, Good.Grain, 0), (3, Good.Flour, 0), (1, Good.Flour, 0), (2, Good.Water, 0), (3, Good.Grain, 1) };
        for (int k = 0; k < who.Length; k++)
        {
            var f = TradeFigure(0, walker, who[k].g, who[k].lv, TradeHead[who[k].head], NewTunics[(k + 1) % 5], 1f);
            f.Position = new Vector3((k - 2.5f) * 2.0f * walker + (k >= 3 ? 1.2f * walker : 0f), 0, 0);
            n.AddChild(f);
        }
        return n;
    }

    /// <summary>The ink check's group of six (as "villagers-group": same places, turns and loads) in mixed planned headwear.</summary>
    private static Node3D TradesInkGroup(float walker)
    {
        var n = new Node3D { Name = "Subject" };
        string[] heads = { "villager-head-helper-mud", "villager-head-kerchief", "villager-head-strawhat", "villager-head-cap", "villager-head-helper-soot", "villager-head-strawhat" };
        for (int i = 0; i < 6; i++)
        {
            var f = TradeFigure(i, walker, i % 2 == 0 ? Good.None : (Good)(1 + i % 5), 0, heads[i], NewTunics[(i + i / 3) % 5], i % 2);
            f.Position = new Vector3((i % 3 - 1) * 1.6f * walker, 0, (i / 3) * 1.8f * walker);
            f.Rotation = new Vector3(0, 0.4f * (i - 2), 0);
            n.AddChild(f);
        }
        return n;
    }

    private async Task TradesShots(float play1080, float walker)
    {
        GetWindow().Size = new Vector2I(1920, 1080);
        await Frames(6);
        var at = new Vector3((_spot.X + 0.5f) * TownGround.Cell, 0, (_spot.Y + 0.5f) * TownGround.Cell);
        foreach (var (name, build, close) in new (string, Func<Node3D>, bool)[]
                 {
                     ("now", () => TradeRow(walker, false, true), true), ("planned", () => TradeRow(walker, true, true), true),
                     ("bare", () => TradeRow(walker, true, false), true), ("helpers", () => HelperRow(walker), false),
                     ("grid", () => TunicGrid(walker), false), ("loads", () => LoadStrip(walker), false),
                 })
        {
            var row = build();
            row.Position = at;
            Main.MapOrigin.AddChild(row);
            Main.Rig.Yaw.Rotation = Vector3.Zero;
            float h = 1.2f * walker;
            Main.Rig.Place(Centre(at, h, play1080, 0f), play1080);
            await SnapFull($"trades-{name}-normal");
            if (close)
            {
                float cs = 4.2f * walker;
                Main.Rig.Place(Centre(at, h, cs, 0f), cs);
                await SnapFull($"trades-{name}-close");
            }
            row.QueueFree();
            await Frames(2);
        }
        Notes.Add($"trades shots in {_dir} (play1080={play1080:0.0}, walker={walker:0.00})");
    }

    private async Task SnapFull(string name)
    {
        Main.Rig.UserMoved = true;
        await Frames(6);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_dir, name + ".png"));
    }
}

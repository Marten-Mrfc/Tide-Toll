using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// n7 step 3 board (--trades2): the second age's headwear (assets/src/models/villager-trades2.py) beside the first
/// three and a Helper, rendered in the game at the 1080p play view (yaw 0, the play zoom's tilt). Test-only: the game
/// does not load the new heads; the head mesh of <see cref="TownVillagers.Figure"/> is swapped as in AssetAudit.Trades.cs.
/// Pictures (1920 x 1080, cropped by tools/mockup/hats_trades2.py against t2-ground): t2-row-{bare,load} (Helper,
/// carrier, reaper, miller, shepherd, weaver, porter), t2-alt-{bare,load} (the dropped candidates), t2-pair-{bare,load}
/// (carriers and porters turned four ways), t2-grid (every new
/// headwear, both candidates, on every tunic colour of TownVillagers.Clothes), t2-crowd-{new,old} (twenty villagers as a
/// second-age street, and the same street with the first three trades only). With --ink-variants=villagers-trades2-group
/// the ink check measures six villagers, one in each of the six headwears.
/// </summary>
public partial class AssetAudit
{
    private bool _trades2;
    // Round 3 (the lead): the shepherd wears the felt hat; the porter the smaller Deep water fantail. "+cape" adds the
    // fleece cape (villager-cape) as a body piece.
    private const string Felt = "villager-head-felthat", Stocking = "villager-head-stocking", Fantail = "villager-head-fantail";
    private const string Cape = "+cape";
    private static readonly string[] T2Heads =
        { "villager-head-helper-mud", "villager-head-kerchief", "villager-head-strawhat", "villager-head-cap", Felt, Stocking, Fantail };
    private static readonly string[] T2Alt =
        { "villager-head-helper-earth", "villager-head-fleece", "villager-head-felthat" + Cape, "villager-head-felthat-band",
          "villager-head-turban", "villager-head-fantail-slate", "villager-head-headpad" };
    private static readonly Good[] T2Goods = { Good.Grain, Good.Water, Good.Grain, Good.Flour, Good.Wool, Good.Cloth, Good.Flour };
    private static readonly Good[] T2AltGoods = { Good.None, Good.Wool, Good.Wool, Good.Wool, Good.Cloth, Good.Flour, Good.Flour };

    /// <summary>TradeFigure with the "+cape" suffix understood: the head named before it, the fleece cape added.</summary>
    private static Node3D T2Figure(int idx, float walker, Good load, string head, Color? tunic, float lean)
    {
        bool cape = head.EndsWith(Cape);
        var f = TradeFigure(idx, walker, load, 0, cape ? head[..^Cape.Length] : head, tunic, lean);
        if (cape)
        {
            Check(ModelLib.Has("villager-cape"), "missing model villager-cape");
            f.AddChild(new MeshInstance3D { Name = "Cape", Mesh = ModelLib.Part("villager-cape", "cape"), Transform = f.GetNode<MeshInstance3D>("Head").Transform });
        }
        return f;
    }

    /// <summary>Round 3: carriers and porters side by side, turned four ways (the round blue head against the dark blue
    /// hood with a flap).</summary>
    private static Node3D T2Pair(float walker, bool load)
    {
        var n = new Node3D { Name = "Subject" };
        for (int k = 0; k < 8; k++)
        {
            bool porter = k % 2 == 1;
            var f = T2Figure(k, walker, load ? (porter ? Good.Flour : Good.Water) : Good.None, porter ? Fantail : "villager-head-kerchief", null, load ? 1f : 0f);
            f.Position = new Vector3((k - 3.5f) * 1.9f * walker, 0, 0);
            f.Rotation = new Vector3(0, (k / 2) * 1.5708f, 0);
            n.AddChild(f);
        }
        return n;
    }

    private static Node3D T2Row(float walker, string[] heads, Good[] goods, bool load)
    {
        var n = new Node3D { Name = "Subject" };
        for (int k = 0; k < heads.Length; k++)
        {
            var f = T2Figure(k, walker, load ? goods[k] : Good.None, heads[k], null, load ? 1f : 0f);
            f.Position = new Vector3((k - (heads.Length - 1) * 0.5f) * 1.9f * walker, 0, 0);
            n.AddChild(f);
        }
        return n;
    }

    /// <summary>Rows (round 3): shepherd's felt hat, with the cape, with a Mud band; weaver; porter; the carrier's kerchief
    /// (to compare with the porter); columns: the game's five tunic colours.</summary>
    private static Node3D T2Grid(float walker)
    {
        string[] rows = { Felt, Felt + Cape, "villager-head-felthat-band", Stocking, Fantail, "villager-head-kerchief" };
        var n = new Node3D { Name = "Subject" };
        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < TownVillagers.Clothes.Length; c++)
            {
                var f = T2Figure(0, walker, Good.None, rows[r], TownVillagers.Clothes[c], 0f);
                f.Position = new Vector3((c - 2f) * 2.0f * walker, 0, (r - 2.5f) * 2.6f * walker);
                n.AddChild(f);
            }
        return n;
    }

    /// <summary>Twenty villagers along a street, as a second-age town might mix them (Helpers 3, carriers 2, reapers 4,
    /// millers 2, shepherds 3, weavers 3, porters 3), a third carrying their own good; <paramref name="old"/> gives the
    /// same street the first three trades only (the new trades' people as reapers, carriers and Helpers).</summary>
    private static Node3D T2Crowd(float walker, bool old)
    {
        int[] who = { 2, 4, 0, 5, 2, 6, 1, 4, 3, 5, 2, 6, 0, 4, 1, 6, 5, 2, 3, 0 };
        int[] was = { 2, 2, 0, 2, 2, 1, 1, 0, 3, 2, 2, 2, 0, 1, 1, 0, 2, 2, 3, 0 };
        var n = new Node3D { Name = "Subject" };
        uint s = 12345;
        float Rnd() { s = s * 1664525u + 1013904223u; return (s >> 8) / 16777216f; }
        for (int i = 0; i < who.Length; i++)
        {
            int t = old ? was[i] : who[i];
            float x = (-9f + 18f * (i + Rnd() * 0.8f) / who.Length) * walker, z = (Rnd() * 5f - 2.5f) * walker;
            bool carry = Rnd() < 0.35f;
            string head = t == 0 ? "villager-head-helper-" + new[] { "mud", "earth", "soot" }[i % 3] : T2Heads[t];
            var f = TradeFigure(i, walker, carry ? T2Goods[t] : Good.None, 0, head, null, carry ? 1f : 0f);
            f.Position = new Vector3(x, 0, z);
            f.Rotation = new Vector3(0, Rnd() * 6.283f, 0);
            n.AddChild(f);
        }
        return n;
    }

    /// <summary>The ink check's group (as "villagers-group": same places, turns and loads), one in each of the six
    /// headwears (carrier, reaper, miller with apron, shepherd, weaver, porter).</summary>
    private static Node3D Trades2InkGroup(float walker)
    {
        var n = new Node3D { Name = "Subject" };
        string[] heads = { "villager-head-kerchief", "villager-head-strawhat", "villager-head-cap", Felt, Stocking, Fantail };
        for (int i = 0; i < 6; i++)
        {
            var f = TradeFigure(i, walker, i % 2 == 0 ? Good.None : (Good)(1 + i % 5), 0, heads[i], null, i % 2);
            f.Position = new Vector3((i % 3 - 1) * 1.6f * walker, 0, (i / 3) * 1.8f * walker);
            f.Rotation = new Vector3(0, 0.4f * (i - 2), 0);
            n.AddChild(f);
        }
        return n;
    }

    private async Task Trades2Shots(float play1080, float walker)
    {
        GetWindow().Size = new Vector2I(1920, 1080);
        await Frames(6);
        var at = new Vector3((_spot.X + 0.5f) * TownGround.Cell, 0, (_spot.Y + 0.5f) * TownGround.Cell);
        foreach (var (name, build) in new (string, Func<Node3D>)[]
                 {
                     ("ground", () => new Node3D { Name = "Subject" }),
                     ("row-bare", () => T2Row(walker, T2Heads, T2Goods, false)), ("row-load", () => T2Row(walker, T2Heads, T2Goods, true)),
                     ("alt-bare", () => T2Row(walker, T2Alt, T2AltGoods, false)), ("alt-load", () => T2Row(walker, T2Alt, T2AltGoods, true)),
                     ("grid", () => T2Grid(walker)),
                     ("pair-bare", () => T2Pair(walker, false)), ("pair-load", () => T2Pair(walker, true)), ("crowd-new", () => T2Crowd(walker, false)), ("crowd-old", () => T2Crowd(walker, true)),
                 })
        {
            var row = build();
            row.Position = at;
            Main.MapOrigin.AddChild(row);
            Main.Rig.Yaw.Rotation = Vector3.Zero;
            Main.Rig.Place(Centre(at, 1.2f * walker, play1080, 0f), play1080);
            await SnapFull($"t2-{name}");
            row.QueueFree();
            await Frames(2);
        }
        Notes.Add($"trades2 shots in {_dir} (play1080={play1080:0.0}, walker={walker:0.00})");
    }
}

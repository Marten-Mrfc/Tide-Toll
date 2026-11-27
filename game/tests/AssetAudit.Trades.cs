using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// n7 step 2 mock-up (--trades): four villagers in a row (Hand, water carrier, reaper, miller) as the game draws them now
/// and in the trades' planned headwear (villager-head-kerchief, -strawhat, -cap and villager-apron), on the game's ground
/// with its ink, at the 1080p play view (yaw 0, the play zoom's tilt) and close up at the game's tilt for that zoom.
/// Test-only: the game does not load the new heads yet. Pictures: trades-{now,planned,bare}-{normal,close}.png.
/// </summary>
public partial class AssetAudit
{
    private bool _trades;
    // Villager indices: their tunics and today's heads (index % 3: hair, hood, straw hat).
    private static readonly int[] TradeIdx = { 0, 4, 2, 7 };
    private static readonly Good[] TradeGood = { Good.None, Good.Water, Good.Grain, Good.Flour };
    private static readonly string[] TradeHead = { "villager", "villager-head-kerchief", "villager-head-strawhat", "villager-head-cap" };

    private Node3D TradeRow(float walker, bool planned, bool loads)
    {
        var n = new Node3D { Name = "Subject" };
        for (int k = 0; k < 4; k++)
        {
            var f = TownVillagers.Figure(TradeIdx[k], walker, loads ? TradeGood[k] : Good.None, 0, loads && k > 0 ? 1f : 0f);
            if (planned)
            {
                var head = f.GetNode<MeshInstance3D>("Head");
                Check(ModelLib.Has(TradeHead[k]), "missing model " + TradeHead[k]);
                head.Mesh = ModelLib.Part(TradeHead[k], "head");
                if (k == 3) f.AddChild(new MeshInstance3D { Name = "Apron", Mesh = ModelLib.Part("villager-apron", "apron"), Transform = head.Transform });
            }
            f.Position = new Vector3((k - 1.5f) * 1.9f * walker, 0, 0);
            n.AddChild(f);
        }
        return n;
    }

    private async Task TradesShots(float play1080, float walker)
    {
        GetWindow().Size = new Vector2I(1920, 1080);
        await Frames(6);
        var at = new Vector3((_spot.X + 0.5f) * TownGround.Cell, 0, (_spot.Y + 0.5f) * TownGround.Cell);
        foreach (var (name, planned, loads) in new[] { ("now", false, true), ("planned", true, true), ("bare", true, false) })
        {
            var row = TradeRow(walker, planned, loads);
            row.Position = at;
            Main.MapOrigin.AddChild(row);
            Main.Rig.Yaw.Rotation = Vector3.Zero;
            float h = 1.2f * walker;
            Main.Rig.Place(Centre(at, h, play1080, 0f), play1080);
            await SnapFull($"trades-{name}-normal");
            float close = 4.2f * walker;
            Main.Rig.Place(Centre(at, h, close, 0f), close);
            await SnapFull($"trades-{name}-close");
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

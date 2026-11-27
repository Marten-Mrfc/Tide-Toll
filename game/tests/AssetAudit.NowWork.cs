using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Night 5 mock-up, the "now" side: real frames of the game as it is, at the workplaces the staged tableaux show
/// (AssetAudit.Work.cs), framed alike (yaw 35 degrees, the same camera size) at a busy moment. Run with --now-work:
/// the town bot plays the first age to level 8, then for each of the well, a field, the mill and the market the town
/// runs on (bot playing) until someone is working there and at least one waits (or a minute of play has passed), the
/// drawing catches up and work-now-&lt;place&gt;-close.png / -normal.png are taken at 1280 x 960. Nothing is staged.
/// </summary>
public partial class AssetAudit
{
    private async Task NowWorkShots(float playSize)
    {
        _play = playSize;
        if (Main.MapOrigin.GetNodeOrNull<Node3D>("Trees") is { } trees) trees.Visible = true;
        if (Main.MapOrigin.GetNodeOrNull<Node3D>("Life") is { } life) life.Visible = true;
        var bot = new TownBot(BotKind.Payback, 1);
        Main.Driver.RunBot(bot, t => t.Level >= 8 || t.CanRingNextAge, 300 * 60 * TownRules.TickHz);
        await Frames(30);
        await Unblock();
        Notes.Add($"now-work level={T.Level}");
        GetWindow().Size = new Vector2I(1280, 960);
        await Frames(6);
        foreach (var (kind, name) in new[] { (PlotKind.Well, "well"), (PlotKind.Field, "field"), (PlotKind.Mill, "mill"), (PlotKind.Market, "market") })
        {
            int wp = OpenOf(kind);
            if (wp < 0)
            {
                // not built yet at this stop: the town plays on (the bot buying as it does) until it is
                Main.Driver.RunBot(bot, t => OpenOf(kind) >= 0, 40 * 60 * TownRules.TickHz);
                await Frames(20);
                await Unblock();
                wp = OpenOf(kind);
            }
            if (wp < 0) { Notes.Add($"now-work {name}: none open"); continue; }
            int w = wp;
            int ran = Main.Driver.RunBot(bot, t => t.Workplace(w).Busy > 0 && t.Workplace(w).QueueLength >= 1, 90 * TownRules.TickHz);
            await Frames(12);   // the drawn villagers catch up with the simulation
            var v = T.Workplace(w);
            Notes.Add($"now-work {name}: busy={v.Busy} queue={v.QueueLength} ticks={ran}");
            var p = T.Plan.Plots[v.Plot];
            var frame = WorkFrame("work-" + name) ?? (Vector3.Zero, 14f);
            await NowShoot("work-now-" + name, Cells(p.X + p.W / 2.0, p.Y + p.H / 2.0), frame.size, 1.2f);
        }
        GetWindow().Size = new Vector2I(640, 480);
        await Frames(6);
    }

    private int OpenOf(PlotKind kind)
    {
        for (int i = 0; i < T.WorkplaceCount; i++)
            if (T.Workplace(i).Kind == kind && T.Workplace(i).Open) return i;
        return -1;
    }

    private async Task NowShoot(string name, Vector3 local, float close, float h)
    {
        Main.Rig.Yaw.Rotation = Vector3.Zero;
        Main.Rig.Place(Centre(local, h, _play, 0f), _play);   // the same view as the staged normal pictures (twice the pixels)
        await Snap(name + "-normal");
        float yaw = Mathf.DegToRad(35f);
        Main.Rig.Yaw.Rotation = new Vector3(0, yaw, 0);
        Main.Rig.Place(Centre(local, h, close, yaw), close);
        await Snap(name + "-close");
        Main.Rig.Yaw.Rotation = Vector3.Zero;
    }
}

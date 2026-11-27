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
        // Night 5 (the places): the market and a cottage are photographed too (below), and a busy field three times, a
        // few seconds of play apart (work-now-field-1..3), so its fronts, stooks and regrowth can be seen to move.
        foreach (var (kind, name) in new[] { (PlotKind.Well, "well"), (PlotKind.Field, "field"), (PlotKind.Mill, "mill") })
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
            if (kind == PlotKind.Field)
                for (int k = 1; k <= 3; k++)
                {
                    // the town plays on a few seconds (the bot acting), drawn every frame, then the same frames again
                    for (int f = 0; f < 4; f++) { bot.Act(T); await Frames(1); }
                    Notes.Add($"now-work field-{k}: busy={T.Workplace(w).Busy} steps={Main.Work.Fields.StepsAt(v.Plot)} gold={Main.Work.Fields.GoldShare(v.Plot):0.00}");
                    await NowShoot("work-now-field-" + k, Cells(p.X + p.W / 2.0, p.Y + p.H / 2.0), frame.size, 1.2f);
                }
        }
        await MarketAndCottage(bot);
        GetWindow().Size = new Vector2I(640, 480);
        await Frames(6);
    }

    /// <summary>The market once loads stand on its table, and a cottage with its door open for one of its people.</summary>
    private async Task MarketAndCottage(TownBot bot)
    {
        int market = -1;
        for (int p = 0; p < T.Plan.Plots.Length; p++) if (T.Plan.Plots[p].Kind == PlotKind.Market) market = p;
        if (market >= 0 && !Main.Buildings.PlotDrawn(market))
        {
            Main.Driver.RunBot(bot, t => Main.Buildings.PlotDrawn(market) || t.OfferBought(T.Plan.Plots[market].Offer), 40 * 60 * TownRules.TickHz);
            await Frames(10);
        }
        if (market >= 0 && Main.Buildings.PlotDrawn(market))
        {
            for (int f = 0; f < 40 && Main.Work.Stall.OnTable < 3; f++) { bot.Act(T); await Frames(1); }
            Notes.Add($"now-work market: on-table={Main.Work.Stall.OnTable} shown={Main.Work.Stall.Shown}");
            var mp = T.Plan.Plots[market];
            var mf = WorkFrame("work-market") ?? (Vector3.Zero, 12f);
            await NowShoot("work-now-market", Cells(mp.X + mp.W / 2.0, mp.Y + mp.H / 2.0), mf.size, 1.2f);
        }
        else Notes.Add("now-work market: not built");
        int cot = -1;
        for (int f = 0; f < 60 && cot < 0; f++)
        {
            bot.Act(T);
            await Frames(1);
            for (int p = 0; p < T.Plan.Plots.Length; p++)
                if (T.Plan.Plots[p].Kind == PlotKind.Cottage && Main.Work.Doors.OpenOf(p) > 0.8f) { cot = p; break; }
        }
        if (cot < 0) { Notes.Add("now-work cottage: no door seen open"); return; }
        Main.Driver.SetPaused(true);
        await Frames(2);
        var cp = T.Plan.Plots[cot];
        Notes.Add($"now-work cottage: plot {cot} door {Main.Work.Doors.OpenOf(cot):0.00}");
        await NowShoot("work-now-cottage", Cells(cp.X + cp.W / 2.0, cp.Y + cp.H / 2.0), 12f, 1.2f);
        Main.Driver.SetPaused(false);
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

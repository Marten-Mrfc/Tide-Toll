using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Round 4: game-perf's second-age town. The bot plays on from the charter (PlayOn) to the borough seal and a minute
/// past it; the game adopts it and the same budgets as the first age's town apply (CPU ms per frame at 1x and 8x,
/// managed bytes per frame).
/// </summary>
public partial class Perf
{
    private async Task<bool> RiverPerf(string charterTown, Probe end)
    {
        var town = TownSave.Load(charterTown);
        var bot = new TownBot(BotKind.Payback, 1) { PlayOn = true };
        var events = new System.Collections.Generic.List<TownEvent>();
        int ticks = 0;
        while (!town.HasSeal && ticks < 20 * 60 * 60 * 10) { town.Step(); bot.Act(town); ticks++; if (ticks % 600 == 0) town.DrainEvents(events); }
        Check(town.HasSeal, "the bot reached the borough seal");
        for (int i = 0; i < 600; i++) { town.Step(); bot.Act(town); }
        town.DrainEvents(events);
        Main.Driver.Adopt(town);
        bool ok = true;
        foreach (var (speed, budget) in new[] { (1.0, Budget1x), (8.0, Budget8x) })
        {
            Main.Driver.SetSpeed(speed);
            await Frames(120);
            double sum = 0, worst = 0;
            const int n = 600;
            long alloc0 = GC.GetTotalAllocatedBytes(false);
            for (int i = 0; i < n; i++) { await Frames(1); sum += end.LastMs; worst = Math.Max(worst, end.LastMs); }
            long perFrame = (GC.GetTotalAllocatedBytes(false) - alloc0) / n, gate = speed > 1 ? AllocGate8x : AllocGate1x;
            double mean = sum / n;
            bool pass = mean < budget && perFrame < gate;
            ok &= pass;
            GD.Print($"PERF river {town.VillagerCount} villagers {speed:0}x: cpu ms/frame mean {mean:0.000} worst {worst:0.00} (gate mean < {budget}) {(pass ? "ok" : "OVER")}; bytes/frame {perFrame} (gate < {gate}); level {town.Level}");
        }
        return ok;
    }
}

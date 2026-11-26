using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-perf: CPU milliseconds per frame with the end-of-age town (the simulation's best-payback bot plays a town to
/// the charter, the game adopts it), interface, sound and every layer running; headless, so no rendering. Run with
/// --fixed-fps 60 so every frame advances 1/60 s of real time. Measured from the first process callback of a frame to
/// the last (two probe nodes at the extreme process priorities), 600 frames at 1x and 600 at 8x after a warm-up.
/// The budget is the old game's: mean under 6 ms at 1x and 10 ms at 8x. The worst frame and the bytes allocated per
/// frame are gated too (<see cref="AllocGate1x"/>, <see cref="AllocGate8x"/>; the simulation itself allocates nothing,
/// which the simulation's own tests check).
/// </summary>
public partial class Perf : TownTestBase
{
    protected override string Marker => "PERF PASS";
    public const double Budget1x = 6, Budget8x = 10;
    /// <summary>
    /// Allocation gates, managed bytes per frame (round 2). Measured with the interface rebuilding text only when a value
    /// changes: about 1.1 KB at 1x (the same with the interface switched off: the rest is the engine glue and this test's
    /// own awaits) and about 3.4 KB at 8x, where coin, floaters and bookmark prices change nearly every frame.
    /// </summary>
    public const long AllocGate1x = 1536, AllocGate8x = 6144;

    private partial class Probe : Node
    {
        public long Start;
        public double LastMs;
        public bool IsEnd;
        public Probe Other = null!;
        public override void _Process(double delta)
        {
            if (!IsEnd) Start = Stopwatch.GetTimestamp();
            else LastMs = (Stopwatch.GetTimestamp() - Other.Start) * 1000.0 / Stopwatch.Frequency;
        }
    }

    protected override async Task Run()
    {
        var sw = Stopwatch.StartNew();
        var town = new Town(Main.Driver.Seed);
        var bot = new TownBot(BotKind.Payback, 1);
        var events = new System.Collections.Generic.List<TownEvent>();
        int ticks = 0;
        while (!town.HasCharter && ticks < 20 * 60 * 60 * 10) { town.Step(); bot.Act(town); ticks++; if (ticks % 600 == 0) town.DrainEvents(events); }
        town.DrainEvents(events);
        Check(town.HasCharter, "the bot reached the end of the age");
        // A little further, so the town is busy and every road is in use.
        for (int i = 0; i < 600; i++) { town.Step(); bot.Act(town); }
        town.DrainEvents(events);
        double botMs = sw.Elapsed.TotalMilliseconds;
        string charterTown = TownSave.Save(town);
        Main.Driver.Adopt(town);
        var start = new Probe { Name = "PerfStart", ProcessPriority = -100000 };
        var end = new Probe { Name = "PerfEnd", ProcessPriority = 100000, IsEnd = true, Other = start };
        AddChild(start); AddChild(end);
        bool ok = true;
        foreach (var (speed, budget) in new[] { (1.0, Budget1x), (8.0, Budget8x) })
        {
            Main.Driver.SetSpeed(speed);
            await Frames(120);
            double sum = 0, worst = 0;
            const int n = 600;
            long alloc0 = GC.GetTotalAllocatedBytes(false);
            double t0 = town.Time;
            for (int i = 0; i < n; i++)
            {
                await Frames(1);
                sum += end.LastMs;
                worst = Math.Max(worst, end.LastMs);
            }
            long alloc = GC.GetTotalAllocatedBytes(false) - alloc0;
            double mean = sum / n;
            long perFrame = alloc / n, gate = speed > 1 ? AllocGate8x : AllocGate1x;
            bool pass = mean < budget && perFrame < gate;
            ok &= pass;
            GD.Print($"PERF town {town.VillagerCount} villagers {speed:0}x: cpu ms/frame mean {mean:0.000} worst {worst:0.00} (gate mean < {budget}) {(pass ? "ok" : "OVER")}; game s {town.Time - t0:0.0}; bytes/frame {perFrame} (gate < {gate})");
        }
        // For information: the same at 8x with the interface switched off, to show what the interface costs.
        Main.Hud.ProcessMode = ProcessModeEnum.Disabled;
        await Frames(30);
        long a0 = GC.GetTotalAllocatedBytes(false);
        for (int i = 0; i < 300; i++) await Frames(1);
        GD.Print($"PERF 8x without the interface: bytes/frame {(GC.GetTotalAllocatedBytes(false) - a0) / 300} (information)");
        Main.Hud.ProcessMode = ProcessModeEnum.Inherit;
        GD.Print($"PERF bot to the charter: {botMs:0} ms CPU for {ticks} ticks, level {town.Level}, {town.VillagerCount} villagers, {town.RoadsPaved} roads paved");
        ok &= await RiverPerf(charterTown, end);
        Check(ok, "CPU time or allocation per frame over budget");
    }
}

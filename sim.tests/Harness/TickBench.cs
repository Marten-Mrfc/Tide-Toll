using System;
using System.Diagnostics;

namespace TideToll.Sim.Testing;

/// <summary>Tick timing: warm up, then time each tick and count bytes allocated on this thread.</summary>
public static class TickBench
{
    public readonly record struct Result(int Walkers, int Ticks, double MeanMs, double P99Ms, double MaxMs, long AllocatedBytes, bool River = false, bool Weather = false);

    /// <summary>
    /// With <paramref name="river"/> the river rules are on with goods hauled, a ferry and a drawbridge with its keeper
    /// built before the warm-up, boats on their schedule and the drawbridge raised by hand every 20 s. With
    /// <paramref name="weather"/> as well, the flood band is under water and the river frozen for the timed ticks (both at
    /// once: the heaviest case, wading costs and ice wear together).
    /// </summary>
    public static Result Measure(int seed, int walkers, int warmup = 600, int ticks = 600, bool river = false, bool weather = false)
    {
        river |= weather;
        var w = river ? new World(seed, walkers, SimParams.Default with { River = RiverRules.Default with { StartMoney = 1000 } }) : new World(seed, walkers);
        int draw = -1;
        if (river)
        {
            RiverBot.Build(w, CrossingType.Ferry);
            draw = RiverBot.Build(w, CrossingType.Drawbridge).Id;
            w.HireKeeper(draw);
        }
        for (int i = 0; i < warmup; i++) w.Tick();
        if (weather)
        {
            w.StartFlood(ticks / (double)w.Params.TicksPerDay + 1);
            w.Freeze();
            for (int i = 0; i < 20; i++) w.Tick(); // the fields catch up with the new ground first
        }
        var ms = new double[ticks];
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < ticks; i++)
        {
            if (draw >= 0 && i % 200 == 0) w.SetBridge(draw, true);
            long t0 = Stopwatch.GetTimestamp();
            w.Tick();
            ms[i] = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        }
        long alloc = GC.GetAllocatedBytesForCurrentThread() - a0;
        double sum = 0;
        foreach (double v in ms) sum += v;
        Array.Sort(ms);
        return new Result(walkers, ticks, sum / ticks, ms[(int)Math.Ceiling(0.99 * ticks) - 1], ms[^1], alloc, river, weather);
    }

    public static string Line(Result r) =>
        $"BENCH {r.Walkers} walkers{(r.Weather ? " + river rules, flood and ice" : r.River ? " + river rules" : "")}: {r.MeanMs:F3} ms per tick mean, p99 {r.P99Ms:F2} ms, max {r.MaxMs:F2} ms over {r.Ticks} ticks; {r.AllocatedBytes} bytes allocated";

#if DEBUG
    public static readonly bool IsRelease = false;
#else
    public static readonly bool IsRelease = true;
#endif
}

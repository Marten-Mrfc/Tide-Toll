using System;
using System.Diagnostics;

namespace TideToll.Sim.Testing;

/// <summary>Tick timing: warm up, then time each tick and count bytes allocated on this thread.</summary>
public static class TickBench
{
    public readonly record struct Result(int Walkers, int Ticks, double MeanMs, double P99Ms, double MaxMs, long AllocatedBytes);

    public static Result Measure(int seed, int walkers, int warmup = 600, int ticks = 600)
    {
        var w = new World(seed, walkers);
        for (int i = 0; i < warmup; i++) w.Tick();
        var ms = new double[ticks];
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < ticks; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            w.Tick();
            ms[i] = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        }
        long alloc = GC.GetAllocatedBytesForCurrentThread() - a0;
        double sum = 0;
        foreach (double v in ms) sum += v;
        Array.Sort(ms);
        return new Result(walkers, ticks, sum / ticks, ms[(int)Math.Ceiling(0.99 * ticks) - 1], ms[^1], alloc);
    }

    public static string Line(Result r) =>
        $"BENCH {r.Walkers} walkers: {r.MeanMs:F3} ms per tick mean, p99 {r.P99Ms:F2} ms, max {r.MaxMs:F2} ms over {r.Ticks} ticks; {r.AllocatedBytes} bytes allocated";

#if DEBUG
    public static readonly bool IsRelease = false;
#else
    public static readonly bool IsRelease = true;
#endif
}

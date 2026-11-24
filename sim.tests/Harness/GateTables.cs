using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static TideToll.Sim.Testing.ToyHarness;

namespace TideToll.Sim.Testing;

/// <summary>One seed of the gate table (the toy's Row).</summary>
public sealed record SeedRow(int Seed, int Walkers, double Ft, double Det, double Cov, double Mean, double Min, double Max, double MeanD, int Below, uint WearFnv);

/// <summary>
/// The toy's gate tables: each seed runs 45 in-game days and samples the trunk share daily from day 20.
/// Gate G3 is judged on held-out seeds 101 to 120: EVERY daily sample must lie within 30% to 70%.
/// </summary>
public static class GateTables
{
    public const int SampleFrom = 20, Days = 45;

    public static SeedRow Row(int seed, int walkers)
    {
        var w = new World(seed, walkers);
        var trunk = new List<double>(); var trunkD = new List<double>(); var det = new List<double>(); var cov = new List<double>();
        for (int d = 1; d <= Days; d++)
        {
            Run(w, 1);
            if (d >= SampleFrom)
            {
                var s = w.Stats();
                trunk.Add(s.TrunkShare); trunkD.Add(s.TrunkShareD); det.Add(s.DetourRatio); cov.Add(s.TrailCoverage);
            }
        }
        return new SeedRow(seed, walkers, w.Metrics.FirstTrailTime, Mean(det), Mean(cov), Mean(trunk), trunk.Min(), trunk.Max(),
            Mean(trunkD), trunk.Count(v => v < 0.3 || v > 0.7), Fnv(w.Wear));
    }

    /// <summary>The toy's mean(): sum in order / max(1, n).</summary>
    public static double Mean(IReadOnlyList<double> a)
    {
        double s = 0;
        foreach (double v in a) s += v;
        return s / Math.Max(1, a.Count);
    }

    /// <summary>Runs the seeds on up to <paramref name="threads"/> threads; each world is independent, so results do not depend on scheduling.</summary>
    public static List<SeedRow> RunSeeds(IEnumerable<(int Seed, int Walkers)> jobs, int threads)
    {
        var list = jobs.ToList();
        var rows = new SeedRow[list.Count];
        Parallel.For(0, list.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads) }, i => rows[i] = Row(list[i].Seed, list[i].Walkers));
        return rows.ToList();
    }

    public static IEnumerable<(int, int)> Range(int first, int count, int walkers = 200) => Enumerable.Range(first, count).Select(s => (s, walkers));

    public static void PrintTable(TextWriter o, string title, IEnumerable<SeedRow> rowsIn, double daySimSec = 15)
    {
        var rows = rowsIn.OrderBy(r => r.Seed).ToList();
        o.WriteLine($"\n{title}");
        o.WriteLine($"({rows[0].Walkers} walkers, {Days} in-game days, 1 day = {daySimSec} s at 1x; trunk share sampled daily, days {SampleFrom} to {Days})");
        o.WriteLine("seed | first trail | detour | coverage | trunk mean   min    max  | out of band | trunk D");
        foreach (var r in rows)
            o.WriteLine($"{r.Seed,4} | {(r.Ft < 0 ? "   none" : (F(r.Ft, 0) + " s").PadLeft(7))}     | {F(r.Det, 3)}  |  {Pct(r.Cov)}  |     {Pct(r.Mean)} {Pct(r.Min)} {Pct(r.Max)} | {r.Below,4} days   | {Pct(r.MeanD)}");
        List<double> Col(Func<SeedRow, double> f) => rows.Select(f).ToList();
        o.WriteLine($"all  | {(F(Mean(Col(r => r.Ft)), 1) + " s").PadLeft(7)} avg | {F(Mean(Col(r => r.Det)), 3)}  |  {Pct(Mean(Col(r => r.Cov)))}  |     {Pct(Mean(Col(r => r.Mean)))} {Pct(Col(r => r.Min).Min())} {Pct(Col(r => r.Max).Max())} | {rows.Count(r => r.Below > 0),4} seeds  | {Pct(Mean(Col(r => r.MeanD)))}");
        o.WriteLine($"     lowest seed mean {Pct(Col(r => r.Mean).Min())}; detour within 1.05-1.30 on {rows.Count(r => r.Det >= 1.05 && r.Det <= 1.3)} of {rows.Count}; coverage within 3%-8% on {rows.Count(r => r.Cov >= 0.03 && r.Cov <= 0.08)} of {rows.Count}");
    }

    /// <summary>The toy's two gate checks on the held-out rows.</summary>
    public static void GateChecks(IReadOnlyList<SeedRow> gate, CheckLog log, double daySimSec = 15)
    {
        log.Check(gate.All(r => r.Min >= 0.3 && r.Max <= 0.7), $"G3: every daily trunk-share sample (days {SampleFrom} to {Days}) within 30%-70% on all {gate.Count} held-out seeds");
        log.Check(gate.All(r => r.Ft >= 0 && r.Ft <= 3 * daySimSec), "a first trail forms within 3 in-game days (45 s at 1x) on every held-out seed");
    }
}

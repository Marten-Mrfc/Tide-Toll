using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TideToll.Sim.Testing;

/// <summary>
/// Every tick of a long run timed (tools: <c>ticks</c>), with what ran in it, to find the worst single ticks: the route
/// searches of the field refresh (World.FieldSearches), the decay step, a new house, an edit. Presets: default
/// (SimParams.Default: rain and growth on), toy (SimParams.Toy) and river (Default with the river rules, a ferry and a
/// drawbridge with its keeper, as the bench builds them).
/// </summary>
public static class TickProfile
{
    public readonly record struct Tick(long Index, double Ms, int Searches, bool Decay, bool NewHouse, bool Edited, int Walkers);

    public sealed record Result(string Preset, int Seed, int Walkers, int Days, Tick[] Ticks);

    public static World Make(int seed, int walkers, string preset)
    {
        switch (preset)
        {
            case "toy": return new World(seed, walkers, SimParams.Toy);
            case "legacy": return new World(seed, walkers, SimParams.Legacy);
            case "river":
                var w = new World(seed, walkers, SimParams.Default with { River = RiverRules.Default with { StartMoney = 1000, KeeperUnlockRaises = 0 } });
                RiverBot.Build(w, CrossingType.Ferry);
                int d = RiverBot.Build(w, CrossingType.Drawbridge).Id;
                if (d >= 0) w.HireKeeper(d);
                return w;
            default: return new World(seed, walkers, SimParams.Default);
        }
    }

    public static Result Run(int seed, int walkers, int days, string preset)
    {
        var w = Make(seed, walkers, preset);
        long n = (long)days * w.Params.TicksPerDay;
        var ticks = new Tick[n];
        double nextDecay = w.Params.DecayStepSimSec;
        for (long i = 0; i < n; i++)
        {
            long s0 = w.FieldSearches;
            int houses = w.Houses.Length;
            long v0 = w.Version;
            long t0 = Stopwatch.GetTimestamp();
            w.Tick();
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            bool decay = w.Time >= nextDecay - 1e-9;
            if (decay) nextDecay += w.Params.DecayStepSimSec;
            ticks[i] = new Tick(i + 1, ms, (int)(w.FieldSearches - s0), decay, w.Houses.Length != houses, w.Version != v0, w.WalkerCount);
        }
        return new Result(preset, seed, walkers, days, ticks);
    }

    private static double Pct(double[] sorted, double p) => sorted[Math.Max(0, (int)Math.Ceiling(p * sorted.Length) - 1)];

    public static string Summary(Result r)
    {
        var ms = r.Ticks.Select(t => t.Ms).OrderBy(x => x).ToArray();
        var first = r.Ticks.Where(t => t.Index > 600).Select(t => t.Ms).OrderBy(x => x).ToArray();
        double S(int k) => r.Ticks.Where(t => t.Searches == k).Select(t => t.Ms).DefaultIfEmpty(0).Average();
        return $"TICKS {r.Preset} seed {r.Seed}, {r.Walkers} walkers, {r.Days} days ({ms.Length} ticks): mean {ms.Average():F3} ms, p99 {Pct(ms, 0.99):F2}, p99.9 {Pct(ms, 0.999):F2}, max {ms[^1]:F2}"
            + $" | after tick 600: p99 {Pct(first, 0.99):F2}, max {first[^1]:F2} | mean by route searches: 0 {S(0):F2}, 1 {S(1):F2}, 2 {S(2):F2} ms";
    }

    public static IEnumerable<string> Worst(Result r, int top)
    {
        double tpd = r.Ticks.Length / (double)r.Days;
        foreach (var t in r.Ticks.OrderByDescending(t => t.Ms).Take(top))
            yield return $"  tick {t.Index,6} (day {t.Index / tpd + 1:F1}): {t.Ms,6:F2} ms, {t.Searches} route searches{(t.Decay ? ", decay" : "")}{(t.NewHouse ? ", NEW HOUSE" : "")}{(t.Edited ? ", edited" : "")}, {t.Walkers} walkers";
    }
}

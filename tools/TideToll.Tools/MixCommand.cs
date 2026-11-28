using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TideToll.Sim.Town;

namespace TideToll.Tools;

/// <summary>
/// people --mix [--seeds 1-10] [--play alone|helpers]: the share of villagers in each trade under the town's own filling,
/// sampled every 10 s (first age from the moment trades show; second age from the ring to the seal): the mean share per
/// trade, the shares at minutes 10, 15, 20, 25, 30 and at the charter, the largest share any trade holds, and the share of
/// samples one trade holds more than half / more than 60 % of the town. Also the first age's trips by kind of work
/// (water, grain home, grain to market, flour). The four bots. Step 3: seven trades (the second age shows all six and the Helpers).
/// </summary>
public static class MixCommand
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly int[] AtMinutes = { 10, 15, 20, 25, 30 };

    private sealed class Age
    {
        public double[] Sum = new double[Town.TradeCount]; public int Samples, Over50, Over60; public double Max;
        public void Add(int[] n, int all)
        {
            Samples++;
            double top = 0;
            for (int i = 0; i < n.Length; i++) { double s = (double)n[i] / all; Sum[i] += s; top = Math.Max(top, s); }
            Max = Math.Max(Max, top);
            if (top > 0.5) Over50++;
            if (top > 0.6) Over60++;
        }
    }

    private sealed class Row
    {
        public int Seed; public BotKind Bot;
        public Age First = new(), Second = new();
        public double[][] At = new double[AtMinutes.Length + 1][]; // + the charter
        public double[] AtSeal = new double[Town.TradeCount];
        public long[] Trips = new long[4]; // water, grain home, grain market, flour (first age)
        public int[,] Places = new int[3, 4]; public int[] Vill = new int[3]; // at levels 5, 10, 15
    }

    public static int Run(Dictionary<string, string> o)
    {
        BandsCommand.Apply(o); // --set Name=v
        string range = o.TryGetValue("seeds", out var r) ? r : "1-10";
        var p = range.Split('-');
        int a = int.Parse(p[0], Inv), z = p.Length > 1 ? int.Parse(p[1], Inv) : a;
        bool helpers = o.TryGetValue("play", out var pl) && pl == "helpers";
        var bots = new[] { BotKind.Payback, BotKind.Cheapest, BotKind.Random, BotKind.Saver };
        var rows = new Row[(z - a + 1) * bots.Length];
        System.Threading.Tasks.Parallel.For(0, rows.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 },
            i => rows[i] = Play(a + i / bots.Length, bots[i % bots.Length], helpers));
        Console.WriteLine($"MIX {(helpers ? "filling off, every villager a Helper" : "the town fills (left alone)")}");
        string[] names = { "Helpers", "Carriers", "Reapers", "Millers", "Shepherds", "Weavers", "Porters" };
        const int N1 = Town.FirstAgeTrades, N2 = Town.TradeCount;
        foreach (var k in bots)
        {
            var s = rows.Where(x => x.Bot == k).ToArray();
            Console.WriteLine($"MIX {k}: first age mean share " + string.Join(", ", Enumerable.Range(0, N1).Select(i => $"{names[i]} {100 * s.Average(x => x.First.Sum[i] / Math.Max(1, x.First.Samples)):F0} %"))
                + $" | largest {100 * s.Average(x => x.First.Max):F0} % (max {100 * s.Max(x => x.First.Max):F0} %) | one trade > 50 % {100.0 * s.Sum(x => x.First.Over50) / s.Sum(x => x.First.Samples):F0} % of the time, > 60 % {100.0 * s.Sum(x => x.First.Over60) / s.Sum(x => x.First.Samples):F0} %");
            for (int m = 0; m <= AtMinutes.Length; m++)
            {
                var have = s.Where(x => x.At[m] != null).ToArray();
                if (have.Length == 0) continue;
                Console.WriteLine($"MIX {k}: at {(m < AtMinutes.Length ? "minute " + AtMinutes[m] : "the charter")} ({have.Length} runs) " + string.Join(", ", Enumerable.Range(0, N1).Select(i => $"{names[i]} {100 * have.Average(x => x.At[m][i]):F0} %")));
            }
            Console.WriteLine($"MIX {k}: second age mean share " + string.Join(", ", Enumerable.Range(0, N2).Select(i => $"{names[i]} {100 * s.Average(x => x.Second.Sum[i] / Math.Max(1, x.Second.Samples)):F0} %"))
                + $" | largest {100 * s.Average(x => x.Second.Max):F0} % (max {100 * s.Max(x => x.Second.Max):F0} %) | one trade > 50 % {100.0 * s.Sum(x => x.Second.Over50) / Math.Max(1, s.Sum(x => x.Second.Samples)):F0} %, > 60 % {100.0 * s.Sum(x => x.Second.Over60) / Math.Max(1, s.Sum(x => x.Second.Samples)):F0} %");
            Console.WriteLine($"MIX {k}: at the seal " + string.Join(", ", Enumerable.Range(0, N2).Select(i => $"{names[i]} {100 * s.Average(x => x.AtSeal[i]):F0} %")));
            for (int li = 0; li < 3; li++)
                Console.WriteLine($"MIX {k}: at level {5 * (li + 1)} places well {s.Average(x => x.Places[li, 1]):F1}, fields {s.Average(x => x.Places[li, 2]):F1}, mill {s.Average(x => x.Places[li, 3]):F1}; villagers {s.Average(x => x.Vill[li]):F1}");
            double all = s.Sum(x => x.Trips.Sum());
            Console.WriteLine($"MIX {k}: first age trips: water {100 * s.Sum(x => x.Trips[0]) / all:F0} %, grain home {100 * s.Sum(x => x.Trips[1]) / all:F0} %, grain to market {100 * s.Sum(x => x.Trips[2]) / all:F0} %, flour {100 * s.Sum(x => x.Trips[3]) / all:F0} %");
            foreach (var x in s)
                Console.WriteLine($"MIXRUN {x.Seed,3} {k,-8} | first " + string.Join(" ", Enumerable.Range(0, N1).Select(i => $"{100 * x.First.Sum[i] / Math.Max(1, x.First.Samples),3:F0}")) + $" | max {100 * x.First.Max:F0} | >50 {100.0 * x.First.Over50 / Math.Max(1, x.First.Samples):F0} | second " + string.Join(" ", Enumerable.Range(0, N2).Select(i => $"{100 * x.Second.Sum[i] / Math.Max(1, x.Second.Samples),3:F0}")) + $" | max {100 * x.Second.Max:F0}");
        }
        return 0;
    }

    private static Row Play(int seed, BotKind kind, bool helpers)
    {
        var t = new Town(seed);
        if (helpers) t.SetTownFillsTrades(false);
        var bot = new TownBot(kind, seed) { PlayOn = true };
        var row = new Row { Seed = seed, Bot = kind };
        var ev = new List<TownEvent>();
        var n = new int[Town.TradeCount];
        int market = -1;
        foreach (var pl in t.Plan.Plots) if (pl.Kind == PlotKind.Market) market = pl.Id;
        bool rung = false, sealed_ = false; int nextAt = 0;
        for (long k = 0; k < 120L * 60 * TownRules.TickHz && !sealed_; k++)
        {
            t.Step(); bot.Act(t);
            t.DrainEvents(ev);
            foreach (var e in ev)
            {
                if (e.Kind == TownEventKind.NextAge) rung = true;
                if (e.Kind == TownEventKind.LevelReached && !rung && e.Level is 5 or 10 or 15)
                {
                    int li = e.Level / 5 - 1; row.Vill[li] = t.VillagerCount;
                    for (int q = 1; q < 4; q++) row.Places[li, q] = t.TradeInfo((Trade)q).Places;
                }
                if (e.Kind == TownEventKind.Seal) { sealed_ = true; Count(t, n); for (int i = 0; i < n.Length; i++) row.AtSeal[i] = (double)n[i] / t.VillagerCount; }
                if (e.Kind == TownEventKind.Charter) { Count(t, n); row.At[AtMinutes.Length] = Share(n, t.VillagerCount); }
                if (!rung && e.Kind == TownEventKind.Delivery)
                {
                    if (e.Good == Good.Water) row.Trips[0]++;
                    else if (e.Good == Good.Grain) row.Trips[e.Plot == market ? 2 : 1]++;
                    else if (e.Good == Good.Flour) row.Trips[3]++;
                }
            }
            if (!rung && nextAt < AtMinutes.Length && t.Time >= AtMinutes[nextAt] * 60 && !t.HasCharter)
            {
                Count(t, n); row.At[nextAt] = Share(n, t.VillagerCount); nextAt++;
            }
            if (t.Tick % (10 * TownRules.TickHz) != 0) continue;
            if (!rung && !t.HasCharter && t.TradesShown) { Count(t, n); row.First.Add(n, t.VillagerCount); }
            else if (rung && !t.HasSeal) { Count(t, n); row.Second.Add(n, t.VillagerCount); }
        }
        return row;
    }

    /// <summary>Villagers by trade; with every villager a Helper (--play helpers), by the trade of the job each is on now
    /// (no job, wool or cloth: index 0), i.e. the old best-pay rule's split of the town by work.</summary>
    private static void Count(Town t, int[] n)
    {
        Array.Clear(n);
        for (int i = 0; i < t.VillagerCount; i++)
        {
            var v = t.Villager(i);
            n[(int)(v.Trade != Trade.None || t.TownFillsTrades ? v.Trade : Town.TradeOfJob(v.Job))]++;
        }
    }

    private static double[] Share(int[] n, int all) => n.Select(x => (double)x / all).ToArray();
}

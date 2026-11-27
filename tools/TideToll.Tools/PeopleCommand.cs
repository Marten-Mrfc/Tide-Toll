using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TideToll.Sim.Town;

namespace TideToll.Tools;

/// <summary>
/// people [--seeds 1-6] [--bot payback|cheapest|all] [--river] [--max 120] [--lean 0.10]
/// Villagers as people (n7 step 1): plays each seed with a bot to the charter (and with --river on to the seal) and prints,
/// per age, the minute, the purchases, coin a minute and the herding: the share of time (sampled every 10 s, 4 or more
/// villagers) with more than three quarters of the villagers on one job.
/// </summary>
public static class PeopleCommand
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private sealed class Row
    {
        public int Seed; public BotKind Bot;
        public double Charter = -1, Seal = -1; public int Buys1, Buys2;
        public int Samples1, Herd1, Samples2, Herd2;
        public double Earned2, Minutes2;
    }

    public static int Run(Dictionary<string, string> o)
    {
        string range = o.TryGetValue("seeds", out var r) ? r : "1-6";
        var p = range.Split('-');
        int a = int.Parse(p[0], Inv), z = p.Length > 1 ? int.Parse(p[1], Inv) : a;
        string which = o.TryGetValue("bot", out var b) ? b : "all";
        bool river = o.ContainsKey("river");
        int max = o.TryGetValue("max", out var m) ? int.Parse(m, Inv) : 120;
        if (o.TryGetValue("replay", out var rf))
        {
            // n7: play a saved town on with the payback bot for --minutes and print the final save's SHA-256 (the rules compared).
            if (o.TryGetValue("millfix", out var mf0)) { TownRules.MillWaitCountsOnTheWay = mf0 != "0"; TownRules.MillWaitCountsOnTheWayInSecondAge = mf0 == "2"; }
            var town = TownSave.Load(System.IO.File.ReadAllText(rf));
            var rb = new TownBot(BotKind.Payback, town.Plan.Seed) { PlayOn = true };
            int mins = o.TryGetValue("minutes", out var mm0) ? int.Parse(mm0, Inv) : 5;
            for (long k = 0; k < (long)mins * 60 * TownRules.TickHz; k++) { town.Step(); rb.Act(town); }
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(TownSave.Save(town)));
            Console.WriteLine($"REPLAY {rf} {mins} min: {Convert.ToHexString(hash)}");
            return 0;
        }
        if (o.TryGetValue("lean", out var ln)) TownRules.LeanBonus = double.Parse(ln, Inv);
        if (o.TryGetValue("sitefix", out var sf)) TownRules.SiteWaitCountsOnTheWay = sf != "0";
        if (o.TryGetValue("millfix", out var mf)) { TownRules.MillWaitCountsOnTheWay = mf != "0"; TownRules.MillWaitCountsOnTheWayInSecondAge = mf == "2"; }
        var bots = new List<BotKind>();
        if (which is "all" or "payback") bots.Add(BotKind.Payback);
        if (which is "all" or "cheapest") bots.Add(BotKind.Cheapest);
        var rows = new Row[(z - a + 1) * bots.Count];
        System.Threading.Tasks.Parallel.For(0, rows.Length, i => rows[i] = Play(a + i / bots.Count, bots[i % bots.Count], river, max));
        Console.WriteLine("seed | bot | charter min | buys | herd 1st | seal min | buys 2nd | coin/min 2nd | herd 2nd");
        foreach (var x in rows)
            Console.WriteLine($"{x.Seed,4} | {x.Bot,-8} | {Min(x.Charter)} | {x.Buys1,4} | {Pct(x.Herd1, x.Samples1)} | {Min(x.Seal)} | {x.Buys2,4} | {(x.Minutes2 > 0 ? x.Earned2 / x.Minutes2 : 0),8:F0} | {Pct(x.Herd2, x.Samples2)}");
        foreach (var k in bots)
        {
            var s = rows.Where(x => x.Bot == k).ToArray();
            var ch = s.Where(x => x.Charter >= 0).Select(x => x.Charter / 60).ToArray();
            string line = $"MEAN {k}: charter {(ch.Length > 0 ? ch.Average() : -1):F2} min ({ch.Length} of {s.Length}), buys {s.Average(x => x.Buys1):F1}, herd 1st {Pct(s.Sum(x => x.Herd1), s.Sum(x => x.Samples1))}";
            if (river)
            {
                var se = s.Where(x => x.Seal >= 0).Select(x => x.Seal / 60).ToArray();
                line += $" | seal {(se.Length > 0 ? se.Average() : -1):F2} min ({se.Length}), buys 2nd {s.Average(x => x.Buys2):F1}, coin/min 2nd {s.Average(x => x.Minutes2 > 0 ? x.Earned2 / x.Minutes2 : 0):F0}, herd 2nd {Pct(s.Sum(x => x.Herd2), s.Sum(x => x.Samples2))}";
            }
            Console.WriteLine(line);
        }
        return 0;
    }

    private static Row Play(int seed, BotKind kind, bool river, int maxMinutes)
    {
        var t = new Town(seed);
        var bot = new TownBot(kind, seed) { PlayOn = river };
        var row = new Row { Seed = seed, Bot = kind };
        var events = new List<TownEvent>();
        var byJob = new int[16];
        double rungAt = -1, earnedAtRing = 0;
        long ticks = (long)maxMinutes * 60 * TownRules.TickHz;
        for (long k = 0; k < ticks; k++)
        {
            t.Step();
            t.DrainEvents(events);
            foreach (var e in events)
            {
                if (e.Kind == TownEventKind.Charter && row.Charter < 0) row.Charter = e.Time;
                if (e.Kind == TownEventKind.NextAge) { rungAt = e.Time; earnedAtRing = t.CoinEarned; }
                if (e.Kind == TownEventKind.Seal && row.Seal < 0) { row.Seal = e.Time - rungAt; row.Earned2 = t.CoinEarned - earnedAtRing; row.Minutes2 = (e.Time - rungAt) / 60; }
                if (e.Kind == TownEventKind.Purchase) { if (rungAt < 0) row.Buys1++; else if (row.Seal < 0) row.Buys2++; }
            }
            if (t.Tick % (10 * TownRules.TickHz) == 0 && t.VillagerCount >= 4 && row.Seal < 0 && (rungAt >= 0 || row.Charter < 0))
            {
                Array.Clear(byJob);
                for (int i = 0; i < t.VillagerCount; i++) byJob[(int)t.Villager(i).Job]++;
                bool herd = byJob.Max() * 4 > t.VillagerCount * 3;
                if (rungAt < 0) { row.Samples1++; if (herd) row.Herd1++; }
                else { row.Samples2++; if (herd) row.Herd2++; }
            }
            bot.Act(t);
            if (!river && row.Charter >= 0) break;
            if (river && row.Seal >= 0) break;
        }
        if (river && row.Seal < 0 && rungAt >= 0) { row.Earned2 = t.CoinEarned - earnedAtRing; row.Minutes2 = (t.Time - rungAt) / 60; }
        return row;
    }

    private static string Min(double s) => s < 0 ? "  -  " : (s / 60).ToString("F1", Inv).PadLeft(5);
    private static string Pct(int n, int d) => d == 0 ? "  -" : $"{100.0 * n / d,3:F0} %";
}

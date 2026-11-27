using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TideToll.Sim.Town;

namespace TideToll.Tools;

/// <summary>
/// bands [--seeds 1-10] [--millfix 0|1|2] [--sitefix 0|1] [--lean x] [--set Name=v,Name=v] [--bell] [--first-only]
/// n7 retune: plays the four bots on the seeds exactly as the pacing tests do (TownPacing.Run for the first age,
/// TownRiverPacing.Run for both ages) and prints every run's figures and, for every band test of TownPacingTests and
/// TownRiverPacingTests, the run nearest its edge and its margin (share of the band's value; negative = outside).
/// --set writes public static double/int/bool fields of TownRules or TownRulesRiver (for measuring, never shipped).
/// </summary>
public static class BandsCommand
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly BotKind[] Bots = { BotKind.Cheapest, BotKind.Payback, BotKind.Random, BotKind.Saver };

    private sealed class Edge { public string Name = ""; public double Margin = double.MaxValue; public string Where = ""; }

    public static int Run(Dictionary<string, string> o)
    {
        string range = o.TryGetValue("seeds", out var r) ? r : "1-10";
        var p = range.Split('-');
        int a = int.Parse(p[0], Inv), z = p.Length > 1 ? int.Parse(p[1], Inv) : a;
        Apply(o);
        if (o.ContainsKey("rates")) return Rates(a, z);
        if (o.ContainsKey("demo")) return Demo(a, z);
        if (o.ContainsKey("choice0")) return Choice0(a, z);
        int ns = z - a + 1;
        var first = new PacingResult[ns * Bots.Length];
        var river = new RiverPacingResult[ns * Bots.Length];
        bool firstOnly = o.ContainsKey("first-only");
        System.Threading.Tasks.Parallel.For(0, first.Length * (firstOnly ? 1 : 2), new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            int j = i % first.Length; int seed = a + j / Bots.Length; var bot = Bots[j % Bots.Length];
            if (i < first.Length) first[j] = TownPacing.Run(seed, bot, TownTargets.RunMinutes);
            else river[j] = TownRiverPacing.Run(seed, bot);
        });
        var edges = new Dictionary<string, Edge>();
        void E(string name, double margin, string where)
        {
            if (!edges.TryGetValue(name, out var e)) edges[name] = e = new Edge { Name = name };
            if (margin < e.Margin) { e.Margin = margin; e.Where = where; }
        }
        Console.WriteLine("FIRST seed | bot | charter min | buys | first coin | first buy | min early | min late | nothing aff s | map gap s | top share | saver gap s | requests");
        for (int s = 0; s < ns; s++)
        {
            double fast = double.MaxValue, slow = 0;
            for (int b = 0; b < Bots.Length; b++)
            {
                var x = first[s * Bots.Length + b];
                string w = $"seed {x.Seed} {TownBot.Name(x.Bot)}";
                long total = x.ByKind.Sum(); double top = total > 0 ? (double)x.ByKind.Max() / total : 0;
                Console.WriteLine($"FIRST {x.Seed,3} | {x.Bot,-8} | {x.Charter / 60,6:F2} | {x.Purchases,4} | {x.FirstCoin,5:F1} | {x.FirstPurchase,5:F1} | {x.MinEarlyPerMinute,3} | {x.MinLatePerMinute,3} | {x.LongestNothingAffordable,4:F0} | {x.LongestMapGap,4:F0} | {top,5:F3} | {x.LongestPurchaseGap,4:F0} | {x.RequestsMet}/{x.RequestsAsked}");
                Console.WriteLine($"EARLY {x.Seed,3} | {x.Bot,-8} | {string.Join(" ", x.Minutes.Take(12).Select(m => m.Purchases))} | boons {string.Join("", x.Boons.Select(b => b < 0 ? "-" : (b % 2 == 0 ? "A" : "B")))} | choice0 {x.ChoiceWorth[0]:F1}/{x.ChoiceWorth[1]:F1} | lastgap {x.LastGap:F2}/{x.LongestEarlierGap:F2} | training {x.ByKind[(int)ShopItemKind.Training]} | cpm {string.Join(" ", x.Minutes.Take(12).Select(m => m.CoinPerMinute.ToString("F0", Inv)))}");
                E("1 first coin <= 25 s", (TownTargets.FirstCoinBy - x.FirstCoin) / TownTargets.FirstCoinBy, w + $" {x.FirstCoin:F1} s");
                E("1 first purchase <= 45 s", (TownTargets.FirstPurchaseBy - x.FirstPurchase) / TownTargets.FirstPurchaseBy, w + $" {x.FirstPurchase:F1} s");
                if (x.Bot != BotKind.Saver)
                {
                    if (x.WorstEarlyMinute >= 0) E("1 purchases a minute, early >= 4", (x.MinEarlyPerMinute - 4.0) / 4, w + $" {x.MinEarlyPerMinute} in minute {x.WorstEarlyMinute}");
                    if (x.WorstLateMinute >= 0) E("1 purchases a minute, late >= 2", (x.MinLatePerMinute - 2.0) / 2, w + $" {x.MinLatePerMinute} in minute {x.WorstLateMinute}");
                }
                else E("1 saver gap <= 90 s", (TownTargets.SaverMaxPurchaseGap - x.LongestPurchaseGap) / TownTargets.SaverMaxPurchaseGap, w + $" {x.LongestPurchaseGap:F0} s");
                E("1 nothing affordable <= 60 s", (TownTargets.MaxNothingAffordable - x.LongestNothingAffordable) / TownTargets.MaxNothingAffordable, w + $" {x.LongestNothingAffordable:F0} s");
                double c = x.Charter < 0 ? 999 : x.Charter / 60;
                E("1 charter 25-40 min (floor)", (c - TownTargets.AgeMinMinutes) / TownTargets.AgeMinMinutes, w + $" {c:F2} min");
                E("1 charter 25-40 min (ceiling)", (TownTargets.AgeMaxMinutes - c) / TownTargets.AgeMaxMinutes, w + $" {c:F2} min");
                E("1 track share <= 0.4", (TownTargets.MaxTrackShare - top) / TownTargets.MaxTrackShare, w + $" {top:F3}");
                E("1 map gap <= 210 s", (TownTargets.MaxMapGap - x.LongestMapGap) / TownTargets.MaxMapGap, w + $" {x.LongestMapGap:F0} s");
                if (x.Bot is BotKind.Payback or BotKind.Saver)
                {
                    int miss = TownTargets.EveryKind.Count(k => x.ByKind[(int)k] == 0) + (x.LoadSteps < TownRules.LoadPrices.Length ? 1 : 0) + (x.Buildings < 4 ? 1 : 0);
                    E("1 every kind, load step, building (misses)", -miss, w + $" {miss}");
                }
                if (x.Bot is BotKind.Cheapest or BotKind.Payback)
                {
                    double sh = x.RequestsAsked == 0 ? 0 : (double)x.RequestsMet / x.RequestsAsked;
                    E("1 requests single run 0.3-0.95", Math.Min((sh - 0.3) / 0.3, (0.95 - sh) / 0.95), w + $" {sh:F2}");
                }
                if (x.Charter > 0) { fast = Math.Min(fast, x.Charter); slow = Math.Max(slow, x.Charter); }
            }
            if (slow > 0) E("1 bots' spread <= 2", (TownTargets.MaxBotSpread - slow / fast) / TownTargets.MaxBotSpread, $"seed {a + s} {slow / fast:F2}");
        }
        foreach (var b in new[] { BotKind.Cheapest, BotKind.Payback })
        {
            var sh = first.Where(x => x.Bot == b).Select(x => x.RequestsAsked == 0 ? 0 : (double)x.RequestsMet / x.RequestsAsked).Average();
            E("1 requests average 0.5-0.8", Math.Min((sh - 0.5) / 0.5, (0.8 - sh) / 0.8), $"{TownBot.Name(b)} {sh:F3}");
        }
        foreach (var b in Bots)
        {
            var s = first.Where(x => x.Bot == b).ToArray();
            Console.WriteLine($"FIRST MEAN {b}: charter {s.Average(x => x.Charter / 60):F2} min, buys {s.Average(x => (double)x.Purchases):F1}");
        }
        if (o.ContainsKey("bell"))
        {
            var w = new double[ns][];
            System.Threading.Tasks.Parallel.For(0, ns, i => w[i] = TownPacing.BellWorth(a + i));
            var all = w.SelectMany(v => v).ToArray();
            for (int i = 0; i < ns; i++) for (int j = 0; j < w[i].Length; j++)
                    E("1 bell single 0.05-0.35", Math.Min((w[i][j] - 0.05) / 0.05, (0.35 - w[i][j]) / 0.35), $"seed {a + i} minute {TownTargets.BellAtMinutes[j]} {w[i][j]:F3}");
            E("1 bell average 0.10-0.25", Math.Min((all.Average() - 0.10) / 0.10, (0.25 - all.Average()) / 0.25), $"{all.Average():F3}");
            foreach (var b in Bots)
            {
                var np = TownPacing.Run(1, b, TownTargets.RunMinutes, noPave: true);
                var aa = first[(1 - a) * Bots.Length + Array.IndexOf(Bots, b)];
                double cc = np.Charter < 0 ? 1e9 : np.Charter;
                E("1 paving matters (seed 1) >= 15 % slower", (cc / aa.Charter - 1 - TownTargets.NoPaveSlower) / TownTargets.NoPaveSlower, $"{TownBot.Name(b)} {cc / 60:F1} vs {aa.Charter / 60:F1}");
            }
        }
        if (!firstOnly) River(river, E);
        Console.WriteLine("BAND | nearest run | margin (share of the band's value)");
        foreach (var e in edges.Values.OrderBy(e => e.Name, StringComparer.Ordinal))
            Console.WriteLine($"BAND {e.Name} | {e.Where} | {(e.Margin == double.MaxValue ? "-" : (e.Margin * 100).ToString("F1", Inv) + " %")}{(e.Margin < 0 ? "  FAIL" : e.Margin < 0.03 ? "  thin" : "")}");
        return 0;
    }

    private static void River(RiverPacingResult[] river, Action<string, double, string> E)
    {
        Console.WriteLine("RIVER seed | bot | rung min | seal min | buys 2nd | coin/min 2nd | landing share mid | drawbridge-seal min | tolls | nothing aff s | map gap s | ford-only min | crossing wait s | level minutes");
        long fordX = 0; double fordW = 0; var used = new HashSet<int>(); double spent = 0, earned = 0;
        foreach (var x in river)
        {
            string w = $"seed {x.Seed} {TownBot.Name(x.Bot)}";
            double all = x.CoinLanding + x.CoinToll + x.CoinMarket + x.CoinTown;
            var lv = string.Join(" ", x.LevelTimes.Select(t => (t / 60).ToString("F1", Inv)));
            Console.WriteLine($"RIVER {x.Seed,3} | {x.Bot,-8} | {x.RungAt / 60,6:F2} | {x.MinutesOfAge,6:F2} | {x.Purchases,4} | {(x.Seal > 0 ? all / (x.Seal / 60) : 0),8:F0} | {x.LandingShareAtMid,5:F3} | {x.DrawbridgeMinutes,5:F2} | {x.TollsBeforeSeal,2} | {x.LongestNothingAffordable,4:F0} | {x.LongestMapGap,4:F0} | {x.FordOnlySeconds / 60,5:F2} | {x.LongestCrossingWait,4:F0} | {lv}");
            E("2 seal (every run)", x.Seal > 0 ? 1 : -1, w);
            if (x.Bot != BotKind.Saver)
            {
                E("2 age 20-35 min, buyers (floor)", (x.MinutesOfAge - TownRiverTargets.AgeMinutesMin) / TownRiverTargets.AgeMinutesMin, w + $" {x.MinutesOfAge:F2} min");
                E("2 age 20-35 min, buyers (ceiling)", (TownRiverTargets.AgeMinutesMax - x.MinutesOfAge) / TownRiverTargets.AgeMinutesMax, w + $" {x.MinutesOfAge:F2} min");
            }
            double ppm = x.Seal > 0 ? x.Purchases / (x.Seal / 60) : 0;
            E("2 purchases a minute >= 2", (ppm - 2) / 2, w + $" {ppm:F2}");
            E("2 nothing affordable <= 60 s", (TownRiverTargets.NothingAffordableMax - x.LongestNothingAffordable) / TownRiverTargets.NothingAffordableMax, w + $" {x.LongestNothingAffordable:F0} s");
            E("2 map gap <= 210 s", (TownRiverTargets.MapGapMax - x.LongestMapGap) / TownRiverTargets.MapGapMax, w + $" {x.LongestMapGap:F0} s");
            E("2 landing share >= 1/3", (x.LandingShareAtMid - TownRiverTargets.LandingShareMin) / TownRiverTargets.LandingShareMin, w + $" {x.LandingShareAtMid:F3}");
            E("2 crossing wait <= 240 s", (TownRiverTargets.CrossingWaitMax - x.LongestCrossingWait) / TownRiverTargets.CrossingWaitMax, w + $" {x.LongestCrossingWait:F0} s");
            E("2 ford only >= 2 min", (x.FordOnlySeconds / 60 - TownRiverTargets.FordOnlyMinutesMin) / TownRiverTargets.FordOnlyMinutesMin, w + $" {x.FordOnlySeconds / 60:F2} min");
            E("2 tolls before seal >= 2", (x.TollsBeforeSeal - 2.0) / 2, w + $" {x.TollsBeforeSeal}");
            E("2 drawbridge to seal >= 3.5 min", (x.DrawbridgeMinutes - TownRiverTargets.DrawbridgeMinutesMin) / TownRiverTargets.DrawbridgeMinutesMin, w + $" {x.DrawbridgeMinutes:F2} min");
            if (x.TollStepSpent > 0) E("2 toll steps back, single run >= 0.35", (x.TollStepEarned / x.TollStepSpent - TownRiverTargets.TollStepsBackMin) / TownRiverTargets.TollStepsBackMin, w + $" spent {x.TollStepSpent:F0} earned {x.TollStepEarned:F0}");
            bool cap = x.CapAtSeal[1] > 0; for (int c = 2; c < 5; c++) cap &= x.CapAtOpen[c] > x.PrevCapAtOpen[c] && x.CapAtSeal[c] > x.CapAtSeal[c - 1];
            E("2 each crossing carries more", cap ? 1 : -1, w);
            if (x.Bot == BotKind.Saver)
            {
                int miss = 0; for (int k = (int)ShopItemKind.Ferry; k <= (int)ShopItemKind.Winch; k++) if (x.ByKind[k] == 0) miss++;
                E("2 saver buys every new kind (misses)", -miss, w + $" {miss}");
            }
            fordX += x.FordCrossingsBeforeFerry; fordW += x.FordHighWaterWaitBeforeFerry; if (x.FordCrossingsBeforeFerry > 0) used.Add(x.Seed);
            spent += x.TollStepSpent; earned += x.TollStepEarned;
        }
        E("2 ford crossings, all runs >= 300", (fordX - 300.0) / 300, $"{fordX}");
        E("2 ford high-water wait, all runs >= 1000", (fordW - 1000) / 1000, $"{fordW:F0}");
        E("2 ford used on seeds >= 4", (used.Count - 4.0) / 4, $"{used.Count}");
        E("2 toll steps back, all runs >= 1.0", spent > 0 ? earned / spent - 1 : -1, $"spent {spent:F0} earned {earned:F0}");
        foreach (var b in Bots)
        {
            var s = river.Where(x => x.Bot == b && x.Seal > 0).ToArray();
            Console.WriteLine($"RIVER MEAN {b}: rung {s.Average(x => x.RungAt / 60):F2} min, seal {s.Average(x => x.MinutesOfAge):F2} min after the ring ({s.Length}), buys 2nd {s.Average(x => (double)x.Purchases):F1}, coin/min 2nd {s.Average(x => (x.CoinLanding + x.CoinToll + x.CoinMarket + x.CoinTown) / (x.Seal / 60)):F0}");
        }
    }

    /// <summary>--rates: per bot, the best coin a minute when each second-age level is reached, over the rate at the ring
    /// (what the old level rule multiplied each next level's minutes by), averaged over the seeds; and the seal minute.</summary>
    private static int Rates(int a, int z)
    {
        int ns = z - a + 1;
        var g = new double[ns * Bots.Length][];
        System.Threading.Tasks.Parallel.For(0, g.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            int seed = a + i / Bots.Length; var kind = Bots[i % Bots.Length];
            var t = new Town(seed); var bot = new TownBot(kind, seed) { PlayOn = true };
            var ev = new List<TownEvent>(); var row = new double[17]; double scale = 0, rung = -1;
            for (long k = 0; k < 120L * 60 * TownRules.TickHz && !t.HasSeal; k++)
            {
                t.Step(); t.DrainEvents(ev);
                foreach (var e in ev)
                {
                    if (e.Kind == TownEventKind.NextAge) { scale = Math.Max(TownRulesRiver.LevelRateMin, t.MaxCoinPerMinute); rung = e.Time; row[13] = scale; row[14] = t.CoinPerMinute; row[15] = t.VillagerCount; row[16] = t.CoinEarned; }
                    if (e.Kind == TownEventKind.LevelReached && rung >= 0 && e.Level > t.Plan.MaxLevel) row[e.Level - t.Plan.MaxLevel - 1] = Math.Max(1, t.MaxCoinPerMinute / scale);
                    if (e.Kind == TownEventKind.Seal) row[12] = (e.Time - rung) / 60;
                }
                bot.Act(t);
            }
            row[12] = (t.Time - rung) / 60; row[16] = (t.CoinEarned - row[16]) / row[12];
            Console.WriteLine($"RUN {seed,2} {kind,-8} ring max {row[13],7:F0} ring now {row[14],7:F0} villagers {row[15],3} mean 2nd {row[16],7:F0} ratio {row[16] / row[13],5:F2} seal {row[12],5:F1} | " + string.Join(" ", row.Take(11).Select(x => x.ToString("F2", Inv))));
            g[i] = row;
        });
        foreach (var b in Bots)
        {
            var rows = Enumerable.Range(0, g.Length).Where(i => Bots[i % Bots.Length] == b).Select(i => g[i]).ToArray();
            Console.WriteLine($"RATES {b,-8}: " + string.Join(" ", Enumerable.Range(0, 12).Select(l => rows.Average(r => r[l]).ToString("F2", Inv))) + $" | seal {rows.Average(r => r[12]):F2}");
        }
        Console.WriteLine("RATES all     : " + string.Join(" ", Enumerable.Range(0, 12).Select(l => g.Average(r => r[l]).ToString("F3", Inv))));
        return 0;
    }

    /// <summary>--demo: richer and poorer runs of one town. Each seed is played by the payback bot to the ring once; from that
    /// moment the same town is played on with what boats pay (BoatPremium) at 0.8, 1 and 1.25 times its value, so the runs
    /// differ only in what they earn after the ring. Prints coin a minute and the seal minute of each (sequential: it writes
    /// a static).</summary>
    private static int Demo(int a, int z)
    {
        double[] f = { 0.8, 1.0, 1.25 };
        double prem = TownRulesRiver.BoatPremium;
        int ordered = 0, n = 0;
        Console.WriteLine("DEMO seed | coin/min 2nd x0.8 / x1 / x1.25 | seal min x0.8 / x1 / x1.25 | richer never later");
        for (int seed = a; seed <= z; seed++)
        {
            var t = new Town(seed); var bot = new TownBot(BotKind.Payback, seed) { PlayOn = true };
            while (!t.InRiverAge && t.Time < 90 * 60) { t.Step(); bot.Act(t); }
            string ring = TownSave.Save(t);
            var cpm = new double[3]; var seal = new double[3];
            for (int k = 0; k < 3; k++)
            {
                TownRulesRiver.BoatPremium = prem * f[k];
                var u = TownSave.Load(ring); var b = new TownBot(BotKind.Payback, seed) { PlayOn = true };
                double t0 = u.Time, e0 = u.CoinEarned;
                while (!u.HasSeal && u.Time - t0 < 90 * 60) { u.Step(); b.Act(u); }
                seal[k] = (u.Time - t0) / 60; cpm[k] = (u.CoinEarned - e0) / seal[k];
            }
            TownRulesRiver.BoatPremium = prem;
            bool ok = cpm[2] >= cpm[1] && cpm[1] >= cpm[0] ? seal[2] <= seal[1] && seal[1] <= seal[0] : true;
            n++; if (ok) ordered++;
            Console.WriteLine($"DEMO {seed,3} | {cpm[0],7:F0} / {cpm[1],7:F0} / {cpm[2],7:F0} | {seal[0],5:F2} / {seal[1],5:F2} / {seal[2],5:F2} | {(ok ? "yes" : "NO")}");
        }
        Console.WriteLine($"DEMO richer never later on {ordered} of {n} seeds");
        return 0;
    }

    /// <summary>n7 trades: the payback bot with each side of choice 0 forced; the charter minute per seed.</summary>
    private static int Choice0(int a, int z)
    {
        int n = z - a + 1;
        var ch = new double[n, 2];
        System.Threading.Tasks.Parallel.For(0, 2 * n, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            int seed = a + i / 2, side = i % 2;
            var t = new Town(seed);
            var bot = new TownBot(BotKind.Payback, seed) { ForceChoice0 = side };
            while (!t.HasCharter && t.Time < 60 * 60) { t.Step(); bot.Act(t); }
            ch[i / 2, side] = t.Time / 60;
        });
        int sa = 0, sb = 0; double ma = 0, mb = 0;
        for (int s = 0; s < n; s++)
        {
            Console.WriteLine($"CHOICE0 {a + s,3} | lanes {ch[s, 0],6:F2} | backs {ch[s, 1],6:F2}");
            ma += ch[s, 0] / n; mb += ch[s, 1] / n;
            if (ch[s, 0] < ch[s, 1]) sa++; else if (ch[s, 1] < ch[s, 0]) sb++;
        }
        Console.WriteLine($"CHOICE0 mean lanes {ma:F2} backs {mb:F2} differ {Math.Abs(ma - mb) / Math.Min(ma, mb) * 100:F2} % | sooner: lanes {sa}, backs {sb}");
        return 0;
    }

    /// <summary>The switches and --set (measuring only).</summary>
    public static void Apply(Dictionary<string, string> o)
    {
        if (o.TryGetValue("lean", out var ln)) TownRules.LeanBonus = double.Parse(ln, Inv);
        if (o.TryGetValue("sitefix", out var sf)) TownRules.SiteWaitCountsOnTheWay = sf != "0";
        if (o.TryGetValue("millfix", out var mf)) { TownRules.MillWaitCountsOnTheWay = mf != "0"; TownRules.MillWaitCountsOnTheWayInSecondAge = mf == "2"; }
        if (!o.TryGetValue("set", out var set)) return;
        foreach (var kv in set.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var q = kv.Split('=');
            int br = q[0].IndexOf('[');
            if (br > 0)
            {
                var af = (q[0].StartsWith("First.") ? typeof(TownRules).GetField(q[0][6..br]) : typeof(TownRulesRiver).GetField(q[0][..br])) ?? throw new ArgumentException("no array " + q[0]);
                ((double[])af.GetValue(null)!)[int.Parse(q[0][(br + 1)..^1], Inv)] = double.Parse(q[1], Inv);
                Console.WriteLine($"SET {q[0]} = {q[1]}");
                continue;
            }
            var f = typeof(TownRules).GetField(q[0]) ?? typeof(TownRulesRiver).GetField(q[0]) ?? throw new ArgumentException("no field " + q[0]);
            if (f.FieldType == typeof(double)) f.SetValue(null, double.Parse(q[1], Inv));
            else if (f.FieldType == typeof(int)) f.SetValue(null, int.Parse(q[1], Inv));
            else if (f.FieldType == typeof(bool)) f.SetValue(null, q[1] is "1" or "true");
            else throw new ArgumentException("not a number field " + q[0]);
            Console.WriteLine($"SET {q[0]} = {q[1]}");
        }
    }
}

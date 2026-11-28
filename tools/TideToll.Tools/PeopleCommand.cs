using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TideToll.Sim.Town;

namespace TideToll.Tools;

/// <summary>
/// people [--seeds 1-6] [--bot payback|cheapest|random|saver|all|four] [--set Name=v] [--river] [--max 120] [--lean 0.10]
/// Villagers as people (n7 step 1): plays each seed with a bot to the charter (and with --river on to the seal) and prints,
/// per age, the minute, the purchases, coin a minute and the herding: the share of time (sampled every 10 s, 4 or more
/// villagers) with more than three quarters of the villagers on one job.
/// n7 trades: --play alone|careful|carriers|helpers (TownTradePlayer); per age also the share of the trips made by villagers
/// with a trade that were at their own trade ("own"), and the town's trade changes a minute ("hats/min").
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
        public int Own1, Trips1, Own2, Trips2, Moves1, Moves2;
        public int[,] ByTrade2 = new int[Town.TradeCount, 11]; // second age: trips by the maker's trade and the job
    }

    private static TradePlay _play = TradePlay.LeftAlone;
    private static bool Trace;

    public static int Run(Dictionary<string, string> o)
    {
        if (o.ContainsKey("mix")) return MixCommand.Run(o); // n7 trades: the trades' mix (MixCommand.cs)
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
        BandsCommand.Apply(o); // --lean, --sitefix, --millfix, --set (n7 retune)
        _play = o.TryGetValue("play", out var pl) ? pl switch { "careful" => TradePlay.Careful, "carriers" => TradePlay.CarelessCarriers, "helpers" => TradePlay.CarelessHelpers, _ => TradePlay.LeftAlone } : TradePlay.LeftAlone;
        Console.WriteLine($"PLAY {TownTradePlayer.Name(_play)}");
        Trace = o.ContainsKey("trace");
        var bots = new List<BotKind>();
        if (which is "all" or "payback") bots.Add(BotKind.Payback);
        if (which is "all" or "cheapest") bots.Add(BotKind.Cheapest);
        if (which is "four" or "random") bots.Add(BotKind.Random);
        if (which is "four" or "saver") bots.Add(BotKind.Saver);
        if (which == "four") { bots.Insert(0, BotKind.Cheapest); bots.Insert(0, BotKind.Payback); }
        var rows = new Row[(z - a + 1) * bots.Count];
        System.Threading.Tasks.Parallel.For(0, rows.Length, i => rows[i] = Play(a + i / bots.Count, bots[i % bots.Count], river, max));
        Console.WriteLine("seed | bot | charter min | buys | herd 1st | seal min | buys 2nd | coin/min 2nd | herd 2nd | own 1st | own 2nd | hats/min 1st | hats/min 2nd");
        foreach (var x in rows)
            Console.WriteLine($"{x.Seed,4} | {x.Bot,-8} | {Min(x.Charter)} | {x.Buys1,4} | {Pct(x.Herd1, x.Samples1)} | {Min(x.Seal)} | {x.Buys2,4} | {(x.Minutes2 > 0 ? x.Earned2 / x.Minutes2 : 0),8:F0} | {Pct(x.Herd2, x.Samples2)} | {Pct(x.Own1, x.Trips1)} | {Pct(x.Own2, x.Trips2)} | {PerMin(x.Moves1, x.Charter / 60)} | {PerMin(x.Moves2, x.Minutes2)}");
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
            line += $" | own {Pct(s.Sum(x => x.Own1), s.Sum(x => x.Trips1))}" + (river ? $" / {Pct(s.Sum(x => x.Own2), s.Sum(x => x.Trips2))}" : "")
                  + $" | hats/min {PerMin(s.Sum(x => x.Moves1), s.Sum(x => Math.Max(0, x.Charter) / 60))}" + (river ? $" / {PerMin(s.Sum(x => x.Moves2), s.Sum(x => x.Minutes2))}" : "");
            Console.WriteLine(line);
            if (o.ContainsKey("away"))
                for (int q = 1; q < Town.TradeCount; q++)
                {
                    int all = 0; for (int j = 0; j < 11; j++) all += s.Sum(x => x.ByTrade2[q, j]);
                    var sb = new System.Text.StringBuilder($"AWAY {k} {(Trade)q,-6} trips {all,6}:");
                    for (int j = 1; j < 11; j++) { int c = s.Sum(x => x.ByTrade2[q, j]); if (c > 0) sb.Append($" {(JobKind)j} {100.0 * c / Math.Max(1, all):F0}%"); }
                    Console.WriteLine(sb.ToString());
                }
        }
        return 0;
    }

    private static Row Play(int seed, BotKind kind, bool river, int maxMinutes)
    {
        var t = new Town(seed);
        var bot = new TownBot(kind, seed) { PlayOn = river };
        var tp = new TownTradePlayer(_play);
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
                if (Trace && e.Kind == TownEventKind.Purchase && e.Time < 660) Console.WriteLine($"BUY {e.Time / 60,5:F2} {e.Item} {e.Index} {e.Coin:F0} coin {t.Coin:F0} cpm {t.CoinPerMinute:F0} req {(t.RequestActive ? t.Request.Good.ToString() : "-")}");
                if (e.Kind == TownEventKind.Purchase) { if (rungAt < 0) row.Buys1++; else if (row.Seal < 0) row.Buys2++; }
                if (e.Kind == TownEventKind.TradeChanged && e.Level == 0) { if (rungAt < 0) row.Moves1++; else if (row.Seal < 0) row.Moves2++; }
                if (e.Kind is TownEventKind.Delivery or TownEventKind.LandingDelivery)
                {
                    var tr = t.Villager(e.Villager).Trade;
                    if (tr == Trade.None) continue;
                    bool own = Town.TradeOfJob(JobOf(e)) == tr; // by the rule in force (step 3: grain and flour to the boats are the porters')
                    if (rungAt >= 0 && row.Seal < 0) row.ByTrade2[(int)tr, (int)JobOf(e)]++;
                    if (rungAt < 0) { row.Trips1++; if (own) row.Own1++; }
                    else if (row.Seal < 0) { row.Trips2++; if (own) row.Own2++; }
                }
            }
            if (t.Tick % (10 * TownRules.TickHz) == 0 && t.VillagerCount >= 4 && row.Seal < 0 && (rungAt >= 0 || row.Charter < 0))
            {
                Array.Clear(byJob);
                for (int i = 0; i < t.VillagerCount; i++) byJob[(int)t.Villager(i).Job]++;
                bool herd = byJob.Max() * 4 > t.VillagerCount * 3;
                if (Trace && herd) Console.WriteLine($"HERD {seed} {kind} {t.Time / 60,5:F1} n {t.VillagerCount} job {(JobKind)Array.IndexOf(byJob, byJob.Max())} req {(t.RequestActive ? (int)t.Request.Good : 0)}");
                if (rungAt < 0) { row.Samples1++; if (herd) row.Herd1++; }
                else { row.Samples2++; if (herd) row.Herd2++; }
            }
            bot.Act(t);
            tp.Act(t);
            if (Trace && t.Tick % (30 * TownRules.TickHz) == 0)
            {
                var sb = new System.Text.StringBuilder($"TRACE {seed} {kind} {t.Time / 60,5:F1} min lvl {t.Level,2} n {t.VillagerCount,2} | trades");
                for (int q = 0; q < Town.TradeCount; q++) sb.Append($" {(Trade)q}:{t.TradeInfo((Trade)q).Members}/{Math.Min(9999, t.TradeEarnPerHead((Trade)q)),4:F0}/{t.TradeInfo((Trade)q).Away,2}");
                sb.Append(" | jobs");
                for (int q = 1; q < 11; q++) { int c = 0; for (int i = 0; i < t.VillagerCount; i++) if ((int)t.Villager(i).Job == q) c++; if (c > 0) sb.Append($" {(JobKind)q}:{c}"); }
                Console.WriteLine(sb.ToString());
            }
            if (!river && row.Charter >= 0) break;
            if (river && row.Seal >= 0) break;
        }
        if (river && row.Seal < 0 && rungAt >= 0) { row.Earned2 = t.CoinEarned - earnedAtRing; row.Minutes2 = (t.Time - rungAt) / 60; }
        return row;
    }

    /// <summary>The job a delivery event was made on (its kind and good say it: the landing, or home and the market).</summary>
    private static JobKind JobOf(in TownEvent e) => e.Kind == TownEventKind.LandingDelivery
        ? e.Good switch { Good.Grain => JobKind.GrainBoat, Good.Flour => JobKind.FlourBoat, Good.Wool => JobKind.WoolBoat, _ => JobKind.ClothBoat }
        : e.Good switch { Good.Water => JobKind.Water, Good.Grain => JobKind.GrainHome, Good.Flour => JobKind.Flour, Good.Wool => JobKind.WoolMarket, _ => JobKind.ClothMarket };

    private static string PerMin(int n, double minutes) => minutes <= 0 ? "  -" : (n / minutes).ToString("F2", Inv).PadLeft(5);
    private static string Min(double s) => s < 0 ? "  -  " : (s / 60).ToString("F1", Inv).PadLeft(5);
    private static string Pct(int n, int d) => d == 0 ? "  -" : $"{100.0 * n / d,3:F0} %";
}

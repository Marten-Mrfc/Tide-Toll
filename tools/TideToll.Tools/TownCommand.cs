using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TideToll.Sim.Town;

namespace TideToll.Tools;

/// <summary>
/// town [--seed 1] [--bot cheapest|payback|random|all] [--minutes 60] [--markdown] [--plan] [--seeds 1-10]
/// A bot plays the incremental town and the pacing table is printed with the brief's targets checked.
/// --set "Name=value;Arr=1,2,3" overrides TownRules fields for one run (tuning).
/// --plan draws the town plan as text; --seeds runs every bot on a range of seeds and prints only the summary.
/// Exit code 1 when a target is missed.
/// </summary>
public static class TownCommand
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(Dictionary<string, string> o)
    {
        int seed = o.TryGetValue("seed", out var s) ? int.Parse(s, Inv) : 1;
        int minutes = o.TryGetValue("minutes", out var m) ? int.Parse(m, Inv) : TownTargets.RunMinutes;
        bool md = o.ContainsKey("markdown");
        if (o.TryGetValue("set", out var sets)) ApplySets(sets);
        if (o.ContainsKey("plan")) { Console.WriteLine(DrawPlan(TownPlanner.Build(seed))); return 0; }
        if (o.ContainsKey("detail")) return Detail(seed, o.TryGetValue("bot", out var db) && db == "random" ? BotKind.Random : db == "payback" ? BotKind.Payback : BotKind.Cheapest);
        var bots = new List<BotKind>();
        string which = o.TryGetValue("bot", out var b) ? b : "all";
        if (which is "all" or "cheapest") bots.Add(BotKind.Cheapest);
        if (which is "all" or "payback") bots.Add(BotKind.Payback);
        if (which is "all" or "random") bots.Add(BotKind.Random);
        if (which is "all" or "saver") bots.Add(BotKind.Saver);
        if (o.ContainsKey("extras")) return Extras(seed);
        if (o.ContainsKey("choices")) { int rc = 0; foreach (var k in bots) rc |= Choices(seed, k); return rc; }
        if (o.TryGetValue("buys", out var bw)) return Buys(seed, bots[^1], bw);
        if (o.TryGetValue("shopat", out var sa)) return ShopAt(seed, double.Parse(sa, Inv));
        if (o.TryGetValue("seeds", out var range) && o.ContainsKey("misses"))
        {
            // v2.1 tuning: every bot on every seed of the range (in parallel), then every miss with a count per kind.
            var p = range.Split('-');
            int a = int.Parse(p[0], Inv), z = p.Length > 1 ? int.Parse(p[1], Inv) : a;
            var all = new List<string>[z - a + 1];
            var seedRuns = new List<PacingResult>[z - a + 1];
            var rows = new string[z - a + 1];
            System.Threading.Tasks.Parallel.For(a, z + 1, sd =>
            {
                var plan = TownPlanner.Build(sd);
                var res = new List<PacingResult>();
                var row = new StringBuilder($"{sd,4}");
                foreach (var k in bots) { var r = TownPacing.Run(sd, k, minutes, plan: plan); res.Add(r); row.Append($" | {Min(r.Charter)} ; {r.LongestNothingAffordable:F0} ; req {r.RequestsMet}/{r.RequestsAsked}"); }
                seedRuns[sd - a] = res;
                all[sd - a] = TownPacing.Misses(res); rows[sd - a] = row.ToString();
            });
            foreach (var r in rows) Console.WriteLine(r);
            var flat = new List<string>(); foreach (var l in all) flat.AddRange(l);
            foreach (var x in flat) Console.WriteLine("  MISSED " + x);
            if (o.ContainsKey("averages"))
            {
                // Rules v2.2: the bell on frozen payback towns (minutes 10 and 20), see TownPacing.BellWorth.
                var w = new double[z - a + 1][];
                System.Threading.Tasks.Parallel.For(a, z + 1, sd => w[sd - a] = TownPacing.BellWorth(sd));
                var flatW = w.SelectMany(x => x).ToArray();
                Console.WriteLine($"bell (frozen payback towns at minutes {string.Join(",", TownTargets.BellAtMinutes)}): average +{flatW.Average() * 100:F1} % (comparisons {flatW.Min() * 100:F1} to {flatW.Max() * 100:F1}), per seed " + string.Join(" ", w.Select(x => string.Join("/", x.Select(v => (v * 100).ToString("F1", Inv))))));
                foreach (var k in new[] { BotKind.Cheapest, BotKind.Payback })
                {
                    var sh = seedRuns.SelectMany(x => x).Where(r => r.Bot == k).Select(r => r.RequestsAsked == 0 ? 0 : (double)r.RequestsMet / r.RequestsAsked).ToArray();
                    if (sh.Length > 0) Console.WriteLine($"{TownBot.Name(k)}: requests met {sh.Average() * 100:F0} % on average (runs {sh.Min() * 100:F0} to {sh.Max() * 100:F0})");
                }
            }
            Console.WriteLine($"misses: {flat.Count} (purchases {flat.FindAll(x => x.Contains("purchases in minute")).Count}, nothing affordable {flat.FindAll(x => x.Contains("nothing affordable")).Count}, age {flat.FindAll(x => x.Contains("charter")).Count}, map {flat.FindAll(x => x.Contains("on the map")).Count})");
            return flat.Count == 0 ? 0 : 1;
        }
        if (o.TryGetValue("seeds", out var range2))
        {
            range = range2;
            var p = range.Split('-');
            int a = int.Parse(p[0], Inv), z = p.Length > 1 ? int.Parse(p[1], Inv) : a;
            bool okAll = true;
            Console.WriteLine("seed | " + string.Join(" | ", bots.ConvertAll(k => TownBot.Name(k))) + " | (charter minute; longest nothing-affordable s)");
            for (int sd = a; sd <= z; sd++)
            {
                var row = new StringBuilder($"{sd,4}");
                var res = new List<PacingResult>();
                foreach (var k in bots) { var r = TownPacing.Run(sd, k, minutes); res.Add(r); row.Append($" | {Min(r.Charter)} ; {r.LongestNothingAffordable:F0}"); }
                okAll &= TownPacing.Misses(res).Count == 0;
                Console.WriteLine(row.ToString());
            }
            return okAll ? 0 : 1;
        }
        var results = new List<PacingResult>();
        foreach (var k in bots)
        {
            var r = TownPacing.Run(seed, k, minutes);
            results.Add(r);
            Console.WriteLine(Table(r, md));
        }
        var misses = TownPacing.Misses(results);
        Console.WriteLine(md ? "### Targets\n" : "Targets:");
        if (misses.Count == 0) Console.WriteLine(md ? "All targets met." : "  all met");
        foreach (var x in misses) Console.WriteLine((md ? "- " : "  MISSED ") + x);
        return misses.Count == 0 ? 0 : 1;
    }

    /// <summary>--set Name=value;Name=v1,v2,... overrides TownRules fields for this run (tuning only).</summary>
    private static void ApplySets(string sets)
    {
        foreach (var kv in sets.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = kv.Split('=');
            var f = typeof(TownRules).GetField(p[0]) ?? throw new ArgumentException($"no TownRules.{p[0]}");
            if (f.FieldType == typeof(double[]))
            {
                var arr = (double[])f.GetValue(null)!;
                var vals = p[1].Split(',');
                for (int i = 0; i < arr.Length && i < vals.Length; i++) arr[i] = double.Parse(vals[i], Inv);
            }
            else if (f.FieldType == typeof(int[]))
            {
                var arr = (int[])f.GetValue(null)!;
                var vals = p[1].Split(',');
                for (int i = 0; i < arr.Length && i < vals.Length; i++) arr[i] = int.Parse(vals[i], Inv);
            }
            else if (f.FieldType == typeof(int)) f.SetValue(null, int.Parse(p[1], Inv));
            else f.SetValue(null, double.Parse(p[1], Inv));
            Console.WriteLine($"set {p[0]} = {p[1]}");
        }
    }

    /// <summary>--detail: play to the charter, then print roads, workplaces, jobs and the CPU time of one more game minute.</summary>
    private static int Detail(int seed, BotKind kind)
    {
        var t = new Town(seed);
        var bot = new TownBot(kind, seed);
        int maxQueue = 0; double maxCrowd = 0;
        while (!t.HasCharter && t.Time < 3600)
        {
            t.Step(); bot.Act(t);
            for (int w = 0; w < t.WorkplaceCount; w++) maxQueue = Math.Max(maxQueue, t.Workplace(w).QueueLength);
            for (int r = 0; r < t.RoadCount; r++) if (t.RoadOpen(r)) maxCrowd = Math.Max(maxCrowd, t.RoadInfo(r).Crowding);
        }
        Console.WriteLine($"seed {seed} {TownBot.Name(kind)}: charter at {t.Time / 60:F1} min, {t.VillagerCount} villagers, pace {t.PaceLevel}, load {t.LoadLevel}, paved {t.RoadsPaved}, trips {t.Trips}, coin/min {t.CoinPerMinute:F0}");
        Console.WriteLine($"max queue {maxQueue}, max crowding {maxCrowd:F2}; delivered Water {t.Delivered(Good.Water)}, Grain {t.Delivered(Good.Grain)}, Flour {t.Delivered(Good.Flour)}");
        Console.WriteLine("road | len | wear | surface | walkers | comfort | footsteps");
        for (int r = 0; r < t.RoadCount; r++)
        {
            var v = t.RoadInfo(r);
            Console.WriteLine($"{r,4} | {t.Plan.Roads[r].Length,5:F1} | {v.Wear:F2} | {v.Surface,-7} | {v.Walkers,2} | {v.Comfort:F1} | {v.Footsteps:F0}");
        }
        for (int w = 0; w < t.WorkplaceCount; w++) { var x = t.Workplace(w); Console.WriteLine($"workplace {w} {x.Kind} open {x.Open} places {x.Places}"); }
        int unlocked = 0; for (int a = 0; a < t.AchievementCount; a++) if (t.Achievement(a).Unlocked) unlocked++;
        Console.WriteLine($"achievements unlocked {unlocked}/{t.AchievementCount}");
        double best = double.MaxValue;
        for (int rep = 0; rep < 5; rep++)
        {
            double t0 = TideToll.Sim.Testing.CpuClock.NowMs();
            for (int k = 0; k < 60 * TownRules.TickHz; k++) t.Step();
            best = Math.Min(best, TideToll.Sim.Testing.CpuClock.NowMs() - t0);
        }
        Console.WriteLine($"one game minute at the end of the age: {best:F2} ms CPU (best of 5)");
        return 0;
    }

    /// <summary>--buys A-B: every purchase of one bot (the last --bot) between minutes A and B, with coin and income (tuning).</summary>
    private static int Buys(int seed, BotKind kind, string range)
    {
        var p = range.Split('-');
        double a = double.Parse(p[0], Inv) * 60, z = double.Parse(p[1], Inv) * 60;
        var t = new Town(seed); var bot = new TownBot(kind, seed); var ev = new List<TownEvent>();
        while (t.Time < z)
        {
            t.Step(); bot.Act(t); t.DrainEvents(ev);
            foreach (var e in ev)
                if (e.Time >= a && e.Kind is TownEventKind.Purchase or TownEventKind.LevelReached or TownEventKind.RequestMet or TownEventKind.RequestAsked)
                    Console.WriteLine($"{e.Time / 60:F2} {e.Kind} {(e.Kind == TownEventKind.Purchase ? e.Item.ToString() : "")} {e.Coin:F0} L{e.Level} | coin {t.Coin:F0} rate {t.CoinPerMinute:F0}");
        }
        return 0;
    }

    /// <summary>--choices: per ten-minute window, how often two or more kinds were affordable at once (sampled every 5 s), crowding and queues (tuning, round 2).</summary>
    private static int Choices(int seed, BotKind kind)
    {
        var t = new Town(seed); var bot = new TownBot(kind, seed); var items = new List<ShopItem>();
        var samples = new int[4]; var two = new int[4]; var three = new int[4]; var crowd = new int[4]; var queue = new int[4]; var secs = new int[4]; var kinds = new double[4];
        var seen = new bool[16];
        while (!t.HasCharter && t.Time < 3600)
        {
            t.Step();
            bool sample = t.Tick % TownRules.TickHz == 0;
            if (!sample) { bot.Act(t); continue; }
            int w = Math.Min(3, (int)(t.Time / 600)); secs[w]++;
            bool c = false, q = false;
            for (int r = 0; r < t.RoadCount; r++) if (t.RoadOpen(r) && t.RoadInfo(r).Crowding > 1) c = true;
            for (int i = 0; i < t.WorkplaceCount; i++) if (t.Workplace(i).QueueLength > 0) q = true;
            if (c) crowd[w]++; if (q) queue[w]++;
            if ((int)t.Time % 5 == 0)
            {
                // kinds open for sale (no lock or room reason) that cost no more than the coin in hand plus 30 s of income (what a person weighs)
                t.ShopItems(items); Array.Clear(seen);
                int n = 0; double reach = t.Coin + t.CoinPerMinute * 0.5;
                foreach (var it in items) if (it.Price > 0 && it.Price <= reach && (it.Affordable || it.Reason.StartsWith("Not enough")) && !seen[(int)it.Kind]) { seen[(int)it.Kind] = true; n++; }
                samples[w]++; kinds[w] += n; if (n >= 2) two[w]++; if (n >= 3) three[w]++;
            }
            bot.Act(t);
        }
        for (int w = 0; w < 4; w++)
            if (secs[w] > 0) Console.WriteLine($"seed {seed} {TownBot.Name(kind)} min {w * 10}-{w * 10 + 10}: kinds affordable {kinds[w] / Math.Max(1, samples[w]):F2}, 2+ kinds {100.0 * two[w] / Math.Max(1, samples[w]):F0} %, 3+ kinds {100.0 * three[w] / Math.Max(1, samples[w]):F0} %, crowded {100.0 * crowd[w] / secs[w]:F1} %, queue {100.0 * queue[w] / secs[w]:F0} %");
        return 0;
    }

    /// <summary>--extras: bell worth, paving worth and requests met per bot on one seed.</summary>
    private static int Extras(int seed)
    {
        var bw = TownPacing.BellWorth(seed);
        Console.WriteLine($"seed {seed} bell on frozen payback towns: " + string.Join(", ", bw.Select((v, i) => $"minute {TownTargets.BellAtMinutes[i]} +{v * 100:F1} %")));
        foreach (BotKind k in Enum.GetValues<BotKind>())
        {
            var a = TownPacing.Run(seed, k, TownTargets.RunMinutes);
            var np = TownPacing.Run(seed, k, TownTargets.RunMinutes, noPave: true);
            Console.WriteLine($"seed {seed} {TownBot.Name(k)}: charter {Min(a.Charter)}, never paving {Min(np.Charter)} ({(np.Charter < 0 ? double.NaN : np.Charter / a.Charter - 1) * 100:F0} % slower), requests met {a.RequestsMet}/{a.RequestsAsked}, longest without a purchase {a.LongestPurchaseGap:F0} s");
        }
        return 0;
    }

    /// <summary>--shopat M: the cheapest-first bot plays to minute M; then the shop is printed (tuning).</summary>
    private static int ShopAt(int seed, double minute)
    {
        var t = new Town(seed); var bot = new TownBot(BotKind.Cheapest, seed);
        while (t.Time < minute * 60) { t.Step(); bot.Act(t); }
        var items = new List<ShopItem>(); t.ShopItems(items);
        Console.WriteLine($"minute {minute}: coin {t.Coin:F0}, coin/min {t.CoinPerMinute:F0}, level {t.Level}, villagers {t.VillagerCount}/{t.Housing}; hint: {t.Hint}");
        foreach (var it in items) Console.WriteLine($"  {it.Kind,-9} {it.Name,-18} L{it.Level,-3} {it.Price,8:F0}  {it.Reason}");
        return 0;
    }

    private static string Min(double sec) => sec < 0 ? "never" : (sec / 60).ToString("F1", Inv);

    public static string Table(PacingResult r, bool md)
    {
        var sb = new StringBuilder();
        sb.AppendLine(md ? $"### Seed {r.Seed}, bot: {TownBot.Name(r.Bot)}\n" : $"== seed {r.Seed}, bot: {TownBot.Name(r.Bot)} ==");
        sb.AppendLine("| minute | coin | coin/min | purchases | level | villagers |");
        if (md) sb.AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var x in r.Minutes)
            sb.AppendLine($"| {x.Minute} | {x.Coin:F0} | {x.CoinPerMinute:F0} | {x.Purchases} | {x.Level} | {x.Villagers} |");
        sb.AppendLine();
        sb.AppendLine($"{(md ? "- " : "")}first coin {r.FirstCoin:F1} s, first purchase {r.FirstPurchase:F1} s, purchases {r.Purchases}");
        sb.AppendLine($"{(md ? "- " : "")}longest spell with nothing affordable {r.LongestNothingAffordable:F1} s (from {r.LongestNothingAffordableAt:F0} s)");
        sb.AppendLine($"{(md ? "- " : "")}fewest purchases in a minute: {r.MinEarlyPerMinute} in minutes 1-{TownTargets.EarlyMinutes} (minute {r.WorstEarlyMinute}), {r.MinLatePerMinute} after (minute {r.WorstLateMinute})");
        var lv = new StringBuilder();
        for (int i = 1; i < r.LevelTimes.Length; i++) lv.Append($" L{i + 1} {Min(r.LevelTimes[i])}");
        sb.AppendLine($"{(md ? "- " : "")}level reached (minute):{lv}");
        sb.AppendLine($"{(md ? "- " : "")}charter at minute {Min(r.Charter)}");
        var bk = new StringBuilder();
        for (int k = 0; k < r.ByKind.Length; k++) if (r.ByKind[k] > 0) bk.Append($" {(ShopItemKind)k} {r.ByKind[k]},");
        sb.AppendLine($"{(md ? "- " : "")}purchases by kind before the charter:{bk.ToString().TrimEnd(',')}");
        sb.AppendLine($"{(md ? "- " : "")}longest gap with nothing new on the map after minute 2: {r.LongestMapGap / 60:F1} min (from minute {r.LongestMapGapAt / 60:F1}); requests met {r.RequestsMet} of {r.RequestsAsked}");
        return sb.ToString();
    }

    /// <summary>The plan's window as text: ~ water, letters plots (C cottage, W well, F field, K market, M mill, B bell green), digits road level (a=10..), . land.</summary>
    public static string DrawPlan(TownPlan p)
    {
        int n = p.Map.Size;
        var ch = new char[n * n];
        for (int i = 0; i < ch.Length; i++) ch[i] = p.Map.Water[i] != 0 ? '~' : '.';
        foreach (var r in p.Roads)
            foreach (int c in TownPlanner.RoadCells(r, n)) ch[c] = r.Level < 10 ? (char)('0' + r.Level) : (char)('a' + r.Level - 10);
        foreach (var pl in p.Plots)
            for (int y = pl.Y; y < pl.Y + pl.H; y++)
                for (int x = pl.X; x < pl.X + pl.W; x++) ch[y * n + x] = "CWFKMB"[(int)pl.Kind];
        var sb = new StringBuilder();
        sb.AppendLine($"seed {p.Seed}: window x {p.WindowX}..{p.WindowX + p.WindowW - 1}, y {p.WindowY}..{p.WindowY + p.WindowH - 1}; {p.Roads.Length} roads, {p.Plots.Length} plots, road crossings {p.RoadCrossings}");
        for (int y = Math.Max(0, p.WindowY - 3); y < Math.Min(n, p.WindowY + p.WindowH + 3); y++)
        {
            for (int x = Math.Max(0, p.WindowX - 3); x < Math.Min(n, p.WindowX + p.WindowW + 16); x++) sb.Append(ch[y * n + x]);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

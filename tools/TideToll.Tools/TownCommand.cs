using System;
using System.Collections.Generic;
using System.Globalization;
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
        if (o.TryGetValue("seeds", out var range))
        {
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

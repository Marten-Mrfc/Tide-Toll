using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Tools;

/// <summary>
/// 'town --river [--seed N | --seeds A-B] [--bot B] [--plan] [--markdown]': plays the second age (The River) and prints, per bot,
/// the time of each level, purchases, the target checks and coin by source. Exit code 1 when a target is missed.
/// </summary>
public static class TownRiverCommand
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(Dictionary<string, string> o, List<BotKind> bots, int seed)
    {
        if (o.ContainsKey("plan")) { Console.WriteLine(DescribePlan(seed)); return 0; }
        int a = seed, z = seed;
        if (o.TryGetValue("seeds", out var range)) { var p = range.Split('-'); a = int.Parse(p[0], Inv); z = p.Length > 1 ? int.Parse(p[1], Inv) : a; }
        var jobs = new List<(int, BotKind)>();
        for (int sd = a; sd <= z; sd++) foreach (var b in bots) jobs.Add((sd, b));
        var res = new RiverPacingResult[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = 3 }, i => res[i] = TownRiverPacing.Run(jobs[i].Item1, jobs[i].Item2));
        var sb = new StringBuilder();
        int misses = 0;
        sb.AppendLine("| seed | bot | rung (min) | age (min) | buys/min | longest nothing affordable (s) | longest map gap (s) | landing share at mid | ford/ferry/foot/draw per min | longest crossing wait (s) | boats come/filled/short | coin landing / toll / river market / town |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        for (int i = 0; i < res.Length; i++)
        {
            var r = res[i];
            double age = r.MinutesOfAge;
            double bpm = r.Seal > 0 ? r.Purchases / (r.Seal / 60) : 0;
            var miss = new List<string>();
            if (r.Seal < 0) miss.Add("no seal");
            else if (r.Bot != BotKind.Saver && (age < TownRiverTargets.AgeMinutesMin || age > TownRiverTargets.AgeMinutesMax)) miss.Add("age " + age.ToString("0.0", Inv));
            if (bpm < TownRiverTargets.PurchasesPerMinuteMin) miss.Add("buys/min");
            if (r.LongestNothingAffordable > TownRiverTargets.NothingAffordableMax) miss.Add("nothing affordable");
            if (r.LongestMapGap > TownRiverTargets.MapGapMax) miss.Add("map gap");
            if (r.LandingShareAtMid < TownRiverTargets.LandingShareMin) miss.Add("landing share");
            if (r.LongestCrossingWait > TownRiverTargets.CrossingWaitMax) miss.Add("crossing wait");
            for (int c = 2; c < 5; c++) if (!(r.CapAtOpen[c] > r.PrevCapAtOpen[c]) || !(r.CapAtSeal[c] > r.CapAtSeal[c - 1])) { miss.Add("crossing capacity"); break; }
            misses += miss.Count;
            sb.Append(Inv, $"| {r.Seed} | {TownBot.Name(r.Bot)} | {r.RungAt / 60:0.0} | {age:0.0} | {bpm:0.00} | {r.LongestNothingAffordable:0} | {r.LongestMapGap:0} | {r.LandingShareAtMid:0.00} | ");
            sb.Append(Inv, $"{r.CrossPerMinute[1]:0.0}/{r.CrossPerMinute[2]:0.0}/{r.CrossPerMinute[3]:0.0}/{r.CrossPerMinute[4]:0.0} | {r.LongestCrossingWait:0} | {r.BoatsCome}/{r.BoatsFilled}/{r.BoatsShort} | ");
            sb.AppendLine(Inv, $"{r.CoinLanding:0} / {r.CoinToll:0} / {r.CoinMarket:0} / {r.CoinTown:0} |{(miss.Count > 0 ? " MISS: " + string.Join(", ", miss) : "")}");
        }
        sb.AppendLine();
        sb.AppendLine("Level times (minutes after the ring), levels 17 to 28:");
        foreach (var r in res)
        {
            sb.Append(Inv, $"- seed {r.Seed} {TownBot.Name(r.Bot)}:");
            foreach (var lt in r.LevelTimes) sb.Append(Inv, $" {(lt < 0 ? "-" : (lt / 60).ToString("0.0", Inv))}");
            sb.Append(Inv, $" | capacity at the seal (ford/ferry/foot/draw a minute): {r.CapAtSeal[1]:0}/{r.CapAtSeal[2]:0}/{r.CapAtSeal[3]:0}/{r.CapAtSeal[4]:0}");
            sb.Append(Inv, $" | touches at ring/seal/spots {r.TouchesAtRing}/{r.TouchesAtSeal}/{r.TouchSpots}");
            sb.Append(Inv, $" | ford only {r.FordOnlySeconds / 60:0.0} min, ford crossings {r.FordCrossingsBeforeFerry}, high-water wait {r.FordHighWaterWaitBeforeFerry:0} s");
            sb.Append(Inv, $" | tolls {r.TollsBeforeSeal} in {r.DrawbridgeMinutes:0.0} min, toll steps spent {r.TollStepSpent:0} earned {r.TollStepEarned:0}");
            sb.Append(" | bought:");
            for (int k = (int)ShopItemKind.Ferry; k <= (int)ShopItemKind.Winch; k++) sb.Append(Inv, $" {(ShopItemKind)k} {r.ByKind[k]}");
            sb.AppendLine();
        }
        if (o.ContainsKey("trace")) foreach (var r in res) { sb.AppendLine($"trace seed {r.Seed} {TownBot.Name(r.Bot)}:"); foreach (var l in r.Trace) sb.AppendLine("  " + l); }
        Console.Write(sb.ToString());
        Console.WriteLine($"misses: {misses}");
        return misses > 0 ? 1 : 0;
    }

    public static string DescribePlan(int seed)
    {
        var plan = TownPlanner.Build(seed);
        var rp = TownRiverPlanner.Build(plan);
        var sb = new StringBuilder();
        sb.AppendLine(Inv, $"seed {seed}: landing row {rp.LandingRow}, ford {rp.FordRow} (map ford {plan.Map.FordY}), ferry {rp.FerryRow}, footbridge {rp.FootRow}, drawbridge {rp.DrawRow}, bank node {rp.BankNode}");
        foreach (var p in rp.Plots) sb.AppendLine(Inv, $"plot {p.Id} {p.Kind} at {p.X},{p.Y} {p.W}x{p.H} level {p.Level}{(p.FarBank ? " far bank" : "")}");
        foreach (var l in rp.Links) { sb.Append(Inv, $"link {l.Id} {l.Kind} {l.A}->{l.B} length {l.Length:0.0} level {l.Level}:"); for (int i = 0; i < l.Px.Length; i++) sb.Append(Inv, $" {l.Px[i]:0.#},{l.Py[i]:0.#}"); sb.AppendLine(); }
        return sb.ToString();
    }
}

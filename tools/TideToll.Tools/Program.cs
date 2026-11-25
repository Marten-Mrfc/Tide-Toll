using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TideToll.Sim;
using TideToll.Sim.Testing;

namespace TideToll.Tools;

/// <summary>
/// Headless runner for TideToll.Sim. Commands:
///   run   [--seed 1] [--walkers 200] [--days 30] [--every 5] [--off | --legacy]   run a town (SimParams.Default; --off:
///                                                               rain and growth off, SimParams.Toy; --legacy: the round-2 model)
///   bench [--seed 1] [--budget-ms 5] [--p99-budget-ms 25]      ms per tick at 200 and 500 walkers (build in Release)
///   ticks [--seed 1] [--walkers 200] [--days 20] [--preset default|toy|river] [--repeat 1] [--top 12]
///                                                               every tick of a long run timed: mean, p99, p99.9, max and the
///                                                               worst ticks with what ran in them (route searches, decay, a new house)
///         [--hunt [--towns 10] [--over 15]]                     --hunt: towns seed.. one after another (500 walkers, 60 days
///                                                               unless given), every step over --over ms printed with what ran
///   gate  [--quick] [--threads 3] [--golden path] [--legacy]   the toy's gate tables, gate checks and the comparison with the toy
///                                                               (SimParams.Toy against toy-golden-v2.json; --legacy: round 2)
///   checks                                                      the round-2 toy's reroute, bridge, robustness, determinism and preview
///                                                               checks (on SimParams.Legacy, as ported)
///   river [--seeds 20] [--first 101] [--seed-sets 101-120,201-220] [--days 30 | 12,30] [--walkers 200] [--sweep]
///         [--threads 3] [--brief] [--only S1,S2,S5-30,S6] [--set KeeperPrice=15,BoatValueMul=3] [--trail legacy|toy]
///                                                               the river strategy experiment: S1 to S4, S5 missing 10%, 30% and
///                                                               50% of boats, S6; --sweep runs 12 and 30 days and S2, S5-10, S6 at 60
///                                                               walkers too; --set overrides RiverRules values for tuning runs
///   town  [--seed 1] [--bot cheapest|payback|random|all] [--minutes 60] [--seeds 1-10] [--markdown] [--plan]
///                                                               the incremental town: a bot plays, pacing table (TownCommand.cs)
/// Exit code 1 when a check fails or a budget is exceeded.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string cmd = args.Length > 0 ? args[0] : "help";
        var opt = ParseOptions(args.Skip(1).ToArray());
        switch (cmd)
        {
            case "run": return RunTown(opt);
            case "bench": return Bench(opt);
            case "ticks": return Ticks(opt);
            case "gate": return Gate(opt);
            case "checks": return Checks();
            case "river": return River(opt);
            case "town": return TownCommand.Run(opt);
            default:
                Console.WriteLine("usage: TideToll.Tools run|bench|gate|checks [options]  (see Program.cs)");
                return cmd is "help" or "-h" or "--help" ? 0 : 2;
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] a)
    {
        var d = new Dictionary<string, string>();
        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].StartsWith("--")) throw new ArgumentException($"unexpected argument {a[i]}");
            string key = a[i][2..];
            d[key] = i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[++i] : "true";
        }
        return d;
    }

    private static int Int(Dictionary<string, string> o, string k, int def) => o.TryGetValue(k, out var v) ? int.Parse(v) : def;
    private static double Dbl(Dictionary<string, string> o, string k, double def) => o.TryGetValue(k, out var v) ? double.Parse(v, System.Globalization.CultureInfo.InvariantCulture) : def;

    private static int RunTown(Dictionary<string, string> o)
    {
        int seed = Int(o, "seed", 1), walkers = Int(o, "walkers", 200), days = Int(o, "days", 30), every = Int(o, "every", 5);
        var w = new World(seed, walkers, o.ContainsKey("legacy") ? SimParams.Legacy : o.ContainsKey("off") ? SimParams.Toy : SimParams.Default);
        Console.WriteLine($"seed {seed}, {w.WalkerCount} walkers, {w.Buildings.Length} buildings ({w.Houses.Length} houses), {days} in-game days");
        Console.WriteLine(" day | trunk share | trunk D | detour | coverage | trips in window | walkers out | first trail");
        var sw = Stopwatch.StartNew();
        for (int d = 1; d <= days; d++)
        {
            ToyHarness.Run(w, 1);
            if (d % every == 0 || d == days)
            {
                var s = w.Stats();
                Console.WriteLine($"{d,4} | {ToyHarness.Pct(s.TrunkShare),11} | {ToyHarness.Pct(s.TrunkShareD),7} | {ToyHarness.F(s.DetourRatio, 3),6} | {ToyHarness.Pct(s.TrailCoverage),8} | {s.TripsInWindow,15} | {w.WalkersOut(),11} | {(s.FirstTrailTime < 0 ? "none" : ToyHarness.F(s.FirstTrailTime, 1) + " s")}");
            }
        }
        Console.WriteLine($"{w.Ticks} ticks in {sw.Elapsed.TotalSeconds:F1} s wall time ({sw.Elapsed.TotalMilliseconds / w.Ticks:F3} ms per tick); wear grid FNV {ToyHarness.Fnv(w.Wear):x8}");
        return 0;
    }

    private static int Bench(Dictionary<string, string> o)
    {
        int seed = Int(o, "seed", 1);
        double budget = Dbl(o, "budget-ms", 5), p99Budget = Dbl(o, "p99-budget-ms", 25);
        Console.WriteLine($"tick benchmark, seed {seed}, 600 warm-up ticks then 600 timed ({(TickBench.IsRelease ? "Release" : "Debug: numbers are not representative")}, {Environment.ProcessorCount} cores, shared machine)");
        bool ok = true;
        foreach (var (walkers, river, weather, toy) in new[] { (200, false, false, false), (500, false, false, false), (500, false, false, true), (500, true, false, false), (500, true, true, false) })
        {
            var r = TickBench.Measure(seed, walkers, river: river, weather: weather, toy: toy);
            Console.WriteLine(TickBench.Line(r));
            if (r.MeanMs > budget || r.P99Ms > p99Budget || r.AllocatedBytes != 0) ok = false;
        }
        Console.WriteLine(ok ? $"BENCH PASS (budget: mean under {budget} ms, p99 under {p99Budget} ms, 0 bytes allocated)" : $"BENCH FAIL (budget: mean under {budget} ms, p99 under {p99Budget} ms, 0 bytes allocated)");
        return ok ? 0 : 1;
    }

    private static int Ticks(Dictionary<string, string> o)
    {
        int seed = Int(o, "seed", 1), walkers = Int(o, "walkers", 200), days = Int(o, "days", 20), top = Int(o, "top", 12), repeat = Int(o, "repeat", 1);
        string preset = o.TryGetValue("preset", out var pr) ? pr : "default";
        if (o.ContainsKey("hunt"))
        {
            // The rare slow step: many towns, one after another, every step over --over ms printed with what ran in it.
            var opt = new SlowStepHunt.Options
            {
                Preset = preset, Walkers = Int(o, "walkers", 500), Days = Int(o, "days", 60), FirstSeed = seed,
                Towns = Int(o, "towns", 10), OverMs = Dbl(o, "over", 15),
            };
            var rep = SlowStepHunt.Run(opt, Console.Out);
            Console.WriteLine(SlowStepHunt.Summary(rep));
            return 0;
        }
        for (int rep = 0; rep < repeat; rep++)
        {
            var r = TickProfile.Run(seed, walkers, days, preset);
            Console.WriteLine(TickProfile.Summary(r));
            if (rep == repeat - 1) foreach (var line in TickProfile.Worst(r, top)) Console.WriteLine(line);
        }
        return 0;
    }

    private static int Gate(Dictionary<string, string> o)
    {
        bool quick = o.ContainsKey("quick");
        int threads = Int(o, "threads", Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1)));
        int n = quick ? 4 : 20;
        var sw = Stopwatch.StartNew();
        var jobs = GateTables.Range(101, n).ToList();
        if (!quick) jobs.AddRange(GateTables.Range(1, 20).Concat(GateTables.Range(201, 20)).Concat(GateTables.Range(1, 5, 500)));
        // The gate is defined on the finished toy's trail model with rain and growth off (SimParams.Toy), as the toy runs it;
        // --legacy runs the round-2 model against the round-2 toy's table instead.
        bool legacy = o.ContainsKey("legacy");
        var preset = legacy ? SimParams.Legacy : SimParams.Toy;
        var rows = GateTables.RunSeeds(jobs, threads, preset);
        List<SeedRow> Pick(int a, int b, int walkers = 200) => rows.Where(r => r.Seed >= a && r.Seed <= b && r.Walkers == walkers).ToList();
        var gate = Pick(101, 120);
        GateTables.PrintTable(Console.Out, $"GATE G3 (held-out seeds 101 to {100 + n}): every daily sample must lie within 30% to 70%", gate);
        if (!quick)
        {
            GateTables.PrintTable(Console.Out, "TUNING SET (seeds 1 to 20, report only)", Pick(1, 20));
            GateTables.PrintTable(Console.Out, "FINAL EXAM (seeds 201 to 220, report only, never tuned on)", Pick(201, 220));
            GateTables.PrintTable(Console.Out, "500 WALKERS (seeds 1 to 5, report only)", Pick(1, 5, 500));
        }
        var log = new CheckLog();
        string goldenPath = o.TryGetValue("golden", out var g) ? g : ToyGolden.DefaultPath(legacy ? "toy-golden.json" : "toy-golden-v2.json");
        var golden = ToyGolden.Load(goldenPath);
        if (!quick) ToyComparison.Compare(golden.Table(), gate, log);
        Console.WriteLine(log);
        Console.WriteLine("\nGATE CHECKS");
        var gl = new CheckLog();
        GateTables.GateChecks(gate, gl);
        Console.WriteLine(gl);
        int failed = log.Checks.Count(c => !c.Ok) + gl.Checks.Count(c => !c.Ok);
        Console.WriteLine($"\n{(failed > 0 ? $"FAILED: {failed} check(s)" : "ALL CHECKS PASSED")} in {sw.Elapsed.TotalSeconds:F0} s ({threads} threads){(quick ? " [quick mode: 4 gate seeds, no report tables]" : "")}; {(legacy ? "SimParams.Legacy" : "SimParams.Toy")}; toy golden from commit {golden.ToyCommit}");
        return failed > 0 ? 1 : 0;
    }

    private static int River(Dictionary<string, string> o)
    {
        if (o.TryGetValue("trail", out var trail))
            RiverBot.Trail = trail switch { "legacy" => SimParams.Legacy, "toy" => SimParams.Toy, _ => throw new ArgumentException("--trail legacy|toy") };
        int seeds = Int(o, "seeds", 20), first = Int(o, "first", 101), walkers = Int(o, "walkers", 200);
        int threads = Int(o, "threads", Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1)));
        bool sweep = o.ContainsKey("sweep");
        var dayList = (o.TryGetValue("days", out var ds) ? ds : sweep ? "12,30" : "30").Split(',').Select(int.Parse).ToArray();
        var sw = Stopwatch.StartNew();
        var players = RiverPlayer.All.Where(p => !o.TryGetValue("only", out var only) || only.Split(',').Any(k => p.Key == k || (k.Length == 2 && p.Key.StartsWith(k)))).ToArray();
        var rules = RiverRules.Default;
        if (o.TryGetValue("set", out var set))
        {
            rules = rules with { };
            foreach (var kv in set.Split(','))
            {
                var parts = kv.Split('=');
                var prop = typeof(RiverRules).GetProperty(parts[0]) ?? throw new ArgumentException($"no RiverRules.{parts[0]}");
                prop.SetValue(rules, Convert.ChangeType(parts[1], prop.PropertyType, System.Globalization.CultureInfo.InvariantCulture));
                Console.WriteLine($"set {parts[0]} = {parts[1]}");
            }
        }
        // Seed ranges: --first/--seeds, or --seed-sets 101-120,201-220.
        var seedList = o.TryGetValue("seed-sets", out var sets)
            ? sets.Split(',').SelectMany(r => { var ab = r.Split('-').Select(int.Parse).ToArray(); return Enumerable.Range(ab[0], ab[1] - ab[0] + 1); }).ToList()
            : Enumerable.Range(first, seeds).ToList();
        var at60 = players.Where(p => p == RiverPlayer.S2 || p == RiverPlayer.S5Miss10 || p == RiverPlayer.S6).ToArray();
        var rows = new List<RiverRunResult>();
        foreach (int days in dayList)
        {
            rows.AddRange(RiverBot.RunAll(seedList, players, walkers, days, threads, rules));
            if (sweep && walkers != 60) rows.AddRange(RiverBot.RunAll(seedList, at60, 60, days, threads, rules));
        }
        Console.WriteLine($"RIVER EXPERIMENT: seeds {string.Join(",", Ranges(seedList))}, {string.Join(" and ", dayList)} days, {walkers} walkers{(sweep ? " (S2, S5 missing 10% and S6 also at 60)" : "")}");
        RiverBot.Print(Console.Out, rows, !o.ContainsKey("brief"));
        var log = new CheckLog();
        RiverGateChecks.Check(rows, log, rules);
        Console.WriteLine(log);
        int failed = log.Checks.Count(c => !c.Ok);
        Console.WriteLine($"\n{(failed > 0 ? $"FAILED: {failed} of {log.Checks.Count} check(s)" : $"ALL {log.Checks.Count} CHECKS PASSED")} in {sw.Elapsed.TotalSeconds:F0} s ({threads} threads)");
        return failed > 0 ? 1 : 0;
    }

    private static IEnumerable<string> Ranges(List<int> s)
    {
        for (int i = 0; i < s.Count;)
        {
            int j = i;
            while (j + 1 < s.Count && s[j + 1] == s[j] + 1) j++;
            yield return s[i] == s[j] ? $"{s[i]}" : $"{s[i]}-{s[j]}";
            i = j + 1;
        }
    }

    private static int Checks()
    {
        var sw = Stopwatch.StartNew();
        var log = new CheckLog();
        log.Log("\nCHECKS");
        ToyChecks.Reroute(1, log);
        ToyChecks.Reroute(2, log);
        ToyChecks.BridgeCheck(1, log);
        ToyChecks.BridgeCheck(2, log);
        ToyChecks.Robustness(log);
        ToyChecks.Determinism(log);
        ToyPreviewChecks.Isolation(log);
        ToyPreviewChecks.Basics(log);
        ToyPreviewChecks.Agreement(new[] { 1, 2, 3, 4, 5, 6 }, log);
        Console.WriteLine(log);
        int failed = log.Checks.Count(c => !c.Ok);
        Console.WriteLine($"\n{(failed > 0 ? $"FAILED: {failed} check(s)" : "ALL CHECKS PASSED")} ({log.Checks.Count} checks) in {sw.Elapsed.TotalSeconds:F0} s");
        return failed > 0 ? 1 : 0;
    }
}

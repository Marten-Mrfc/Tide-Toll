using System;
using System.Collections.Generic;
using System.Linq;

namespace TideToll.Sim.Testing;

/// <summary>
/// The trade-offs the implementation spec (section 5) asks the river experiment to show, as checks on the
/// medians over seeds. They are reported as measured; a failing check is a finding, not a test to bend.
/// </summary>
public static class RiverGateChecks
{
    /// <summary>A large ford detour: the ford at least this many rows from the landing.</summary>
    public const double LargeDetourRows = 15;

    public static void Check(IReadOnlyList<RiverRunResult> rows, CheckLog log)
    {
        List<RiverRunResult> Of(RiverStrategy s, int walkers = 200) => rows.Where(r => r.Strategy == s && r.Walkers == walkers).ToList();
        double Med(RiverStrategy s, Func<RiverRunResult, double> f, int walkers = 200) => RiverBot.Median(Of(s, walkers).Select(f));
        string P(double v) => ToyHarness.Pct(v).Trim();
        const RiverStrategy S1 = RiverStrategy.S1FordOnly, S2 = RiverStrategy.S2Ferry, S3 = RiverStrategy.S3Footbridge,
            S4 = RiverStrategy.S4DrawbridgeNeverRaised, S5 = RiverStrategy.S5DrawbridgeManual, S6 = RiverStrategy.S6DrawbridgeKeeper;

        // 1. The footbridge is the fastest way across when the ford is far away.
        var far = Of(S3).Where(r => r.FordDetourRows >= LargeDetourRows).Select(r => r.Seed).ToHashSet();
        double TripFar(RiverStrategy s) => RiverBot.Median(Of(s).Where(r => far.Contains(r.Seed)).Select(r => r.MeanTrip));
        double t2 = TripFar(S2), t3 = TripFar(S3), t4 = TripFar(S4);
        log.Check(far.Count > 0 && t3 < t2 && t3 < t4,
            $"footbridge has the shortest mean trip of ferry, footbridge, drawbridge on the {far.Count} seeds with the ford {LargeDetourRows}+ rows from the landing (S2 {t2:F2} s, S3 {t3:F2} s, S4 {t4:F2} s)");

        // 2. The footbridge turns barges back; the ford turns nobody back.
        double b3 = Med(S3, r => r.TurnBackBarge), t1 = Med(S1, r => r.TurnBack);
        log.Check(b3 > 0.6, $"footbridge turns back more than 60% of barges (S3 median {P(b3)})");
        log.Check(t1 < 0.1, $"ford only turns back fewer than 10% of boats (S1 median {P(t1)})");

        // 3. A drawbridge that is never raised blocks like a footbridge and costs more.
        double m3 = Med(S3, r => r.Money), m4 = Med(S4, r => r.Money);
        log.Check(m4 <= m3, $"never-raised drawbridge earns no more than the footbridge (net money S4 {m4:F0}c, S3 {m3:F0}c)");

        // 4. Raising the drawbridge keeps boats coming.
        double tb5 = Med(S5, r => r.TurnBack), tb6 = Med(S6, r => r.TurnBack);
        log.Check(tb5 < 0.15 && tb6 < 0.15, $"manual and keeper drawbridges turn back fewer than 15% of boats (S5 {P(tb5)}, S6 {P(tb6)})");

        // 5. Batching by hand keeps the worst walker wait lower than the keeper's raise-per-boat.
        double w5 = Med(S5, r => r.WorstWait), w6 = Med(S6, r => r.WorstWait);
        log.Check(w5 < w6, $"manual drawbridge has the lower worst walker wait of S5 and S6 (S5 {w5:F1} s, S6 {w6:F1} s)");

        // 6. The keeper pays only with traffic.
        if (Of(S5, 60).Count > 0 && Of(S6, 60).Count > 0)
        {
            double d60 = Med(S6, r => r.Money, 60) - Med(S5, r => r.Money, 60);
            double d200 = Med(S6, r => r.Money) - Med(S5, r => r.Money);
            log.Check(d60 < 0, $"keeper minus manual net money is negative at 60 walkers ({d60:+0;-0}c)");
            log.Check(d200 > 0, $"keeper minus manual net money is positive at 200 walkers ({d200:+0;-0}c)");
        }

        // 7. No strategy dominates: none has the best median trade income, trip time and net money at once.
        var strategies = rows.Where(r => r.Walkers == 200).Select(r => r.Strategy).Distinct().ToList();
        var best = new Dictionary<string, RiverStrategy>
        {
            ["trade income"] = strategies.OrderByDescending(s => Med(s, r => r.TradeIncome)).First(),
            ["trip time"] = strategies.OrderBy(s => Med(s, r => r.MeanTrip)).First(),
            ["net money"] = strategies.OrderByDescending(s => Med(s, r => r.Money)).First(),
        };
        bool dominated = best.Values.Distinct().Count() == 1;
        log.Check(!dominated, "no strategy is best at trade income, trip time and net money at once (" + string.Join(", ", best.Select(kv => $"{kv.Key}: {RiverBot.Name(kv.Value)}")) + ")");
    }
}

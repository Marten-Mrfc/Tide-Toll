using System;
using System.Collections.Generic;
using System.Linq;

namespace TideToll.Sim.Testing;

/// <summary>
/// The river experiment's checks, on the bot's runs (seeds x players x 60 and 200 walkers, over the longest run
/// length given, normally 30 days). (a) to (g) are the lead's "real choices" (decision E, river pass 3, 25 Nov 2026);
/// the spec's earlier checks follow; the old keeper check and the old form of (b) ("the ford is clearly the slowest")
/// are printed as report only, with their result. "Most towns" means more than half of the seeds.
/// They are reported as measured; a failing check is a finding for the tuner, never a test to bend.
/// </summary>
public static class RiverGateChecks
{
    /// <summary>A large ford detour: the ford at least this many rows from the landing.</summary>
    public const double LargeDetourRows = 15;

    public static void Check(IReadOnlyList<RiverRunResult> all, CheckLog log, RiverRules? rulesUsed = null)
    {
        var rules = rulesUsed ?? RiverRules.Default;
        int days = all.Max(r => r.Days);
        var rows = all.Where(r => r.Days == days).ToList();
        List<RiverRunResult> Of(RiverPlayer p, int walkers = 200) => rows.Where(r => r.Player == p && r.Walkers == walkers).ToList();
        double Med(RiverPlayer p, Func<RiverRunResult, double> f, int walkers = 200) => RiverBot.Median(Of(p, walkers).Select(f));
        string P(double v) => ToyHarness.Pct(v).Trim();
        // Seeds on which a beats b by f (both ran): (wins, seeds).
        (int Wins, int Seeds) Beats(RiverPlayer a, RiverPlayer b, int walkers, Func<RiverRunResult, double> f)
        {
            var bs = Of(b, walkers).ToDictionary(r => r.Seed);
            var pairs = Of(a, walkers).Where(r => bs.ContainsKey(r.Seed)).Select(r => (f(r), f(bs[r.Seed]))).ToList();
            return (pairs.Count(q => q.Item1 > q.Item2), pairs.Count);
        }
        bool Most((int Wins, int Seeds) x) => x.Seeds > 0 && 2 * x.Wins > x.Seeds;
        string Of2((int Wins, int Seeds) x) => $"{x.Wins} of {x.Seeds} towns";
        RiverPlayer S1 = RiverPlayer.S1, S2 = RiverPlayer.S2, S3 = RiverPlayer.S3, S4 = RiverPlayer.S4,
            S5a = RiverPlayer.S5Miss10, S5b = RiverPlayer.S5Miss30, S5c = RiverPlayer.S5Miss50, S6 = RiverPlayer.S6;
        var players200 = rows.Where(r => r.Walkers == 200).Select(r => r.Player).Distinct().ToList();
        Func<RiverRunResult, double> money = r => r.Money;
        log.Log($"\nRIVER CHECKS over {days} days ({rows.Select(r => r.Seed).Distinct().Count()} seeds)");

        // (a) The low footbridge gives the fastest walker trips and clearly the worst trade.
        var trip = players200.ToDictionary(p => p, p => Med(p, r => r.MeanTrip));
        var fastest = trip.OrderBy(kv => kv.Value).First();
        // Restated by the lead (fix round before the playtest, 25 Nov 2026): the footbridge must give the fastest trips of the
        // crossings a player would buy for walking (ford only, ferry, footbridge, and the drawbridge strategies that raise it).
        // The never-raised drawbridge (S4) is only a footbridge at four times the price, and on the reviewer's seeds it came
        // 0.19 s faster by chance; it is printed beside the check as report only, with its time.
        var walkingTrips = trip.Where(kv => kv.Key != S4).OrderBy(kv => kv.Value).ToList();
        bool walkFound = walkingTrips.Count > 0;
        var fastestWalk = walkFound ? walkingTrips[0] : default;
        log.Check(walkFound && fastestWalk.Key == S3,
            $"(a) footbridge gives the fastest walker trips of the crossings bought for walking (median mean trip: S3 {trip.GetValueOrDefault(S3):F2} s; fastest {(walkFound ? fastestWalk.Key.Key : "none")} {fastestWalk.Value:F2} s)");
        log.Log($"  report only  (a) the never-raised drawbridge: median mean trip {(trip.TryGetValue(S4, out double s4Trip) ? $"{s4Trip:F2} s" : "not run")}");
        var trade = players200.ToDictionary(p => p, p => Med(p, r => r.TradeIncome));
        // A found flag, not a comparison with default: RiverPlayer is a record struct and the ford-only player equals default.
        var otherTrade = trade.Where(kv => kv.Key != S3 && kv.Key != S4).OrderBy(kv => kv.Value).ToList();
        bool tradeFound = otherTrade.Count > 0;
        var nextTrade = tradeFound ? otherTrade[0] : default;
        log.Check(tradeFound && trade.GetValueOrDefault(S3) <= 0.8 * nextTrade.Value,
            $"(a) footbridge has clearly the worst trade: at most 80% of every other player's but the never-raised drawbridge (S3 {trade.GetValueOrDefault(S3):F0}c, next lowest {(tradeFound ? nextTrade.Key.Key : "none")} {nextTrade.Value:F0}c; S4 {trade.GetValueOrDefault(S4):F0}c)");

        // (b) The free ford costs trade: at 200 walkers the ford-only town ends with clearly less money than the best purchase
        // in most towns (the lead's aim: 15% less at the median), and it is the richest strategy in at most a quarter of towns.
        var buyers = players200.Where(p => p != S1).ToList();
        var shortfall = new List<double>();
        int s1Richest = 0, s1Behind = 0;
        foreach (var r1 in Of(S1))
        {
            var others = buyers.SelectMany(p => Of(p).Where(r => r.Seed == r1.Seed)).ToList();
            if (others.Count == 0) continue;
            double top = others.Max(r => r.Money);
            double sf = (top - r1.Money) / Math.Max(1, Math.Abs(top));
            shortfall.Add(sf);
            if (sf >= 0.05) s1Behind++;
            if (r1.Money >= top) s1Richest++;
        }
        double medShort = RiverBot.Median(shortfall);
        log.Check(shortfall.Count > 0 && 2 * s1Behind > shortfall.Count && medShort >= 0.15,
            $"(b) the free ford costs trade: at 200 walkers ford only ends with clearly less money (5%+) than the best purchase in most towns ({s1Behind} of {shortfall.Count}) and 15%+ less at the median (median shortfall {P(medShort)}; wet grain lost, median {Med(S1, r => r.WetLost):F0}c, {P(Med(S1, r => r.WetShare))} of its grain delivered wet)");
        log.Check(shortfall.Count > 0 && 4 * s1Richest <= shortfall.Count,
            $"(b) ford only is the richest strategy at 200 walkers in at most a quarter of towns ({s1Richest} of {shortfall.Count})");
        // The old form of (b), report only.
        var far = Of(S1).Where(r => r.FordDetourRows >= LargeDetourRows).Select(r => r.Seed).ToHashSet();
        double TripFar(RiverPlayer p) => RiverBot.Median(Of(p).Where(r => far.Contains(r.Seed)).Select(r => r.MeanTrip));
        var farTrip = players200.ToDictionary(p => p, TripFar);
        var nextSlow = farTrip.Where(kv => kv.Key != S1).OrderByDescending(kv => kv.Value).FirstOrDefault();
        bool oldB = far.Count > 0 && farTrip[S1] >= 1.1 * nextSlow.Value;
        log.Log($"  report only  (b, old form) ford only is clearly the slowest (10%+ over every other player) on the {far.Count} towns with the ford {LargeDetourRows}+ rows from the landing: {(oldB ? "yes" : "no")} (S1 {farTrip.GetValueOrDefault(S1):F2} s, next slowest {nextSlow.Key.Key} {nextSlow.Value:F2} s)");
        int floodDays = rows.Sum(r => r.FloodDays), fordInFlood = rows.Sum(r => r.FordCrossingsInFlood);
        log.Check(floodDays > 0 && fordInFlood == 0, $"(b) nobody crosses the ford while the flood band is under water ({fordInFlood} crossings over {floodDays} flood days in all runs)");

        // (c) The ferry is the better buy in a small village; at 200 walkers its capacity bites.
        var c60 = Beats(S2, S5a, 60, money);
        log.Check(Most(c60), $"(c) at 60 walkers the ferry ends with more net money than the hand-raised drawbridge (S5, 10% missed) in most towns ({Of2(c60)}; medians S2 {Med(S2, money, 60):F0}c, S5 {Med(S5a, money, 60):F0}c)");
        double w60 = Med(S2, r => r.MeanWait, 60), w200 = Med(S2, r => r.MeanWait);
        double q60 = Med(S2, r => r.MaxQueue, 60), q200 = Med(S2, r => r.MaxQueue);
        double d2 = Med(S2, r => r.DeliveriesPerDay), d5 = Med(S5a, r => r.DeliveriesPerDay);
        log.Check(w200 >= 2 * w60 && q200 >= 2 * q60 && d2 < d5,
            $"(c) at 200 walkers the ferry's capacity bites: mean wait and largest queue at least double those at 60, and fewer deliveries than the drawbridge (wait {w60:F2} s -> {w200:F2} s, queue {q60:F0} -> {q200:F0}, deliveries a day S2 {d2:F1}, S5 {d5:F1})");

        // (d) The drawbridge pays back its price against the ferry within the run at 200 walkers in most towns, and not at 60.
        var d200 = Beats(S5a, S2, 200, money);
        log.Check(Most(d200), $"(d) at 200 walkers the hand-raised drawbridge (10% missed) ends with more net money than the ferry in most towns ({Of2(d200)}; medians S5 {Med(S5a, money):F0}c, S2 {Med(S2, money):F0}c)");
        var dd60 = Beats(S5a, S2, 60, money);
        log.Check(!Most(dd60), $"(d) at 60 walkers it does not ({Of2(dd60)})");

        // (e) The keeper pays when the player is not watching: it beats the 30% and 50% players at 200 walkers in most towns.
        var e30 = Beats(S6, S5b, 200, money);
        var e50 = Beats(S6, S5c, 200, money);
        var e10 = Beats(S6, S5a, 200, money);
        log.Check(Most(e30), $"(e) at 200 walkers the keeper ends with more net money than the player missing 30% of boats in most towns ({Of2(e30)}; medians S6 {Med(S6, money):F0}c, S5-30 {Med(S5b, money):F0}c)");
        log.Check(Most(e50), $"(e) at 200 walkers the keeper ends with more net money than the player missing 50% of boats in most towns ({Of2(e50)}; S5-50 {Med(S5c, money):F0}c)");
        log.Log($"  info  (e) against the player missing 10% (need not win): {Of2(e10)}; S5-10 {Med(S5a, money):F0}c");
        var hired = rows.Where(r => r.Player == S6 && r.KeeperDay >= 0).ToList();
        int unlockRaises = rules.KeeperUnlockRaises;
        bool early = hired.Any(r => r.KeeperUnlockDay < 0 || r.KeeperDay < r.KeeperUnlockDay || r.HandRaisesAtHire < unlockRaises);
        log.Check(!early, $"(e) the keeper is never hired before its unlock ({unlockRaises} hand raises): {hired.Count} hires over {rows.Count(r => r.Player == S6)} keeper runs, all after the unlock: {(early ? "no" : "yes")}");

        // (f) No strategy is best at trade income, trip time and net money at once.
        var best = new Dictionary<string, RiverPlayer>
        {
            ["trade income"] = trade.OrderByDescending(kv => kv.Value).First().Key,
            ["trip time"] = fastest.Key,
            ["net money"] = players200.OrderByDescending(p => Med(p, money)).First(),
        };
        log.Check(best.Values.Distinct().Count() > 1, "(f) no strategy is best at trade income, trip time and net money at once (" + string.Join(", ", best.Select(kv => $"{kv.Key}: {kv.Value.Key}")) + ")");

        // (g) Pacing: a crossing is affordable on the first in-game day; the drawbridge after roughly 6 to 10 days at 200
        // walkers; the keeper no sooner than its unlock (about 10 hand raises) after that.
        double cheapest = Math.Min(rules.FerryPrice, rules.FootbridgePrice);
        var firstBuys = rows.Where(r => r.Player == S2 || r.Player == S3).ToList();
        int lateFirst = firstBuys.Count(r => r.BuiltDay < 0 || r.BuiltDay > 1);
        log.Check(rules.StartMoney >= cheapest && firstBuys.Count > 0 && lateFirst == 0,
            $"(g) one crossing is affordable in the first in-game day: purse {rules.StartMoney:F0}c, ferry {rules.FerryPrice:F0}c, footbridge {rules.FootbridgePrice:F0}c; {firstBuys.Count - lateFirst} of {firstBuys.Count} ferry and footbridge runs built by day 1");
        double drawDay = Med(S5a, r => r.BuiltDay);
        log.Check(drawDay >= 6 && drawDay <= 10, $"(g) at 200 walkers the drawbridge is bought after 6 to 10 in-game days (median S5-10 {drawDay:F1}; S6 {Med(S6, r => r.BuiltDay):F1})");
        var s6 = Of(S6).Where(r => r.BuiltDay >= 0).ToList();
        double unlockAfter = RiverBot.Median(s6.Where(r => r.KeeperUnlockDay >= 0).Select(r => r.KeeperUnlockDay - r.BuiltDay));
        double hireAfter = RiverBot.Median(s6.Where(r => r.KeeperDay >= 0).Select(r => r.KeeperDay - r.BuiltDay));
        log.Check(hired.Count > 0 && !early, $"(g) the keeper comes no sooner than its unlock after the drawbridge: unlocked a median {unlockAfter:F1} days and hired {hireAfter:F1} days after the drawbridge at 200 walkers ({s6.Count(r => r.KeeperUnlockDay >= 0)} of {s6.Count} unlocked, {s6.Count(r => r.KeeperDay >= 0)} hired)");

        // The spec's earlier checks.
        double b3 = Med(S3, r => r.TurnBackBarge), t1 = Med(S1, r => r.TurnBack);
        log.Check(b3 > 0.6, $"footbridge turns back more than 60% of barges (S3 median {P(b3)})");
        log.Check(t1 < 0.1, $"ford only turns back fewer than 10% of boats (S1 median {P(t1)})");
        double m3 = Med(S3, money), m4 = Med(S4, money);
        log.Check(m4 <= m3, $"never-raised drawbridge earns no more than the footbridge (net money S4 {m4:F0}c, S3 {m3:F0}c)");
        double tb5 = Med(S5a, r => r.TurnBack), tb6 = Med(S6, r => r.TurnBack);
        log.Check(tb5 < 0.15 && tb6 < 0.15, $"manual (10% missed) and keeper drawbridges turn back fewer than 15% of boats (S5 {P(tb5)}, S6 {P(tb6)})");
        double ww5 = Med(S5a, r => r.WorstWait), ww6 = Med(S6, r => r.WorstWait);
        log.Check(ww5 < ww6, $"manual drawbridge has the lower worst walker wait of S5 (10% missed) and S6 (S5 {ww5:F1} s, S6 {ww6:F1} s)");

        // Report only: the old keeper check assumed a player who never looks away.
        double k200 = Med(S6, money) - Med(S5a, money);
        string k60 = Of(S6, 60).Count > 0 ? $"; at 60 walkers {Med(S6, money, 60) - Med(S5a, money, 60):+0;-0}c" : "";
        log.Log($"  report only  keeper minus manual (10% missed) net money at 200 walkers is positive: {(k200 > 0 ? "yes" : "no")} ({k200:+0;-0}c{k60})");
    }
}

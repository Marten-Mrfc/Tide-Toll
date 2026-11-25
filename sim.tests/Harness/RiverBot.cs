using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TideToll.Sim.Testing;

/// <summary>The six crossing strategies of the river experiment (implementation spec, section 5).</summary>
public enum RiverStrategy { S1FordOnly, S2Ferry, S3Footbridge, S4DrawbridgeNeverRaised, S5DrawbridgeManual, S6DrawbridgeKeeper }

/// <summary>One strategy on one seed: what it earned and what it cost walkers and boats.</summary>
public sealed record RiverRunResult(
    int Seed, int Walkers, RiverStrategy Strategy, int Row, double BuiltDay, double FordDetourRows,
    double TradeIncome, double BoatIncome, double MarketIncome, int Grain,
    double TurnBack, double TurnBackBarge, int BoatsDecided,
    double MeanWait, double WorstWait, double MeanTrip, double Reputation, double Money, double Upkeep, double Spent);

/// <summary>
/// The headless strategy bot: runs a town with the river rules on and one crossing strategy. It builds its
/// crossing at row LandingRow + 4 (or the first valid row a little further south) as soon as it has the coin,
/// never north of the landing. Mean trip time and mean wait cover the second half of the run (every strategy has
/// built by then); the worst wait covers the whole run. S5 raises its drawbridge by hand when a boat has waited 4 s or two boats wait,
/// and misses 10% of boats; S6 hires the keeper as soon as it can.
/// </summary>
public static class RiverBot
{
    public static string Name(RiverStrategy s) => s switch
    {
        RiverStrategy.S1FordOnly => "S1 ford only",
        RiverStrategy.S2Ferry => "S2 ferry",
        RiverStrategy.S3Footbridge => "S3 footbridge",
        RiverStrategy.S4DrawbridgeNeverRaised => "S4 drawbridge, never raised",
        RiverStrategy.S5DrawbridgeManual => "S5 drawbridge, manual",
        _ => "S6 drawbridge + keeper",
    };

    public static RiverRunResult Run(int seed, int walkers, RiverStrategy s, int days = 12, RiverRules? rules = null)
    {
        var p = SimParams.Default with { River = rules ?? RiverRules.Default };
        var w = new World(seed, walkers, p);
        var river = w.River!;
        CrossingType? type = s switch
        {
            RiverStrategy.S2Ferry => CrossingType.Ferry,
            RiverStrategy.S3Footbridge => CrossingType.Footbridge,
            RiverStrategy.S1FordOnly => null,
            _ => CrossingType.Drawbridge,
        };
        int id = -1, row = -1;
        double builtDay = -1;
        var waiting = new List<double>();
        long ticks = (long)days * w.Params.TicksPerDay;
        // Trip times and waits are measured over the second half of the run, when every strategy has built.
        int trips0 = 0, waits0 = 0;
        double tripSum0 = 0, waitSum0 = 0;
        for (long t = 0; t < ticks; t++)
        {
            w.Tick();
            if (t == ticks / 2)
            {
                var h = river.Stats;
                trips0 = h.Trips; tripSum0 = h.TripTimeSum; waits0 = h.WalkerWaits; waitSum0 = h.WalkerWaitSum;
            }
            if (type != null && id < 0 && river.Money >= river.Rules.Price(type.Value) && t % 5 == 0)
            {
                (id, row) = Build(w, type.Value);
                if (id >= 0) builtDay = w.Time / w.Params.DaySimSec;
                else if (row == -2) type = null; // nowhere valid on this map
            }
            if (id < 0) continue;
            if (s == RiverStrategy.S6DrawbridgeKeeper && river.Money >= river.Rules.KeeperPrice)
            {
                var x = river.Crossings.First(c => c.Id == id);
                if (!x.Keeper) w.HireKeeper(id);
            }
            // S6 raises by hand, like S5, until its keeper is hired.
            if (s == RiverStrategy.S5DrawbridgeManual || (s == RiverStrategy.S6DrawbridgeKeeper && !river.Crossings.First(c => c.Id == id).Keeper)) ManualPolicy(w, id, seed);
        }
        var rs = river.Stats;
        int decided = rs.Served.Sum() + rs.TurnedBack.Sum();
        int fordRow = w.Map.FordY, landing = river.LandingRow;
        return new RiverRunResult(seed, walkers, s, row, builtDay, Math.Abs(fordRow - landing),
            rs.TradeIncome, rs.BoatIncome, rs.MarketIncome, rs.GrainToBoats + rs.GrainToMarket + rs.GrainUnsold,
            rs.TurnBackRate(), rs.TurnBackRate(BoatKind.Barge), decided,
            rs.WalkerWaits > waits0 ? (rs.WalkerWaitSum - waitSum0) / (rs.WalkerWaits - waits0) : 0, rs.WalkerWaitMax,
            rs.Trips > trips0 ? (rs.TripTimeSum - tripSum0) / (rs.Trips - trips0) : 0, river.Reputation, river.Money, rs.Upkeep, rs.Spent);
    }

    /// <summary>Builds at LandingRow + 4, or the next valid row up to + 16. Returns (id, row); row -2 when no row is valid.</summary>
    public static (int Id, int Row) Build(World w, CrossingType type)
    {
        var river = w.River!;
        for (int dy = 4; dy <= 16; dy++)
        {
            int y = river.LandingRow + dy;
            if (y >= w.Grid.Height - 2) break;
            var r = w.CheckCrossing(type, w.RiverX[y], y + 0.5);
            if (!r.Ok)
            {
                if (r.Reason.StartsWith("Not enough coin")) return (-1, -1);
                continue;
            }
            var b = w.PlaceCrossing(type, w.RiverX[y], y + 0.5);
            return b.Ok ? (b.Id, y) : (-1, -1);
        }
        return (-1, -2);
    }

    /// <summary>A player who raises when a boat has waited 4 s or two boats wait, and misses 10% of boats.</summary>
    private static void ManualPolicy(World w, int id, int seed)
    {
        var river = w.River!;
        var x = river.Crossings.First(c => c.Id == id);
        if (x.State != BridgeState.Closed) return;
        double patience = river.Rules.PatienceSec;
        int count = 0;
        double oldest = 0;
        foreach (ref readonly var b in river.Boats)
        {
            if (b.State != BoatState.Waiting || b.WaitingAt != id) continue;
            if (Hash.Hash2(seed * 31 + 7, b.Id) < 0.1) continue; // this player never noticed it
            count++;
            oldest = Math.Max(oldest, patience - b.PatienceLeft);
        }
        if (oldest >= 4 || count >= 2) w.SetBridge(id, true);
    }

    public static List<RiverRunResult> RunAll(IEnumerable<int> seeds, IEnumerable<RiverStrategy> strategies, int walkers, int days, int threads, RiverRules? rules = null)
    {
        var jobs = (from s in strategies from seed in seeds select (seed, s)).ToList();
        var results = new RiverRunResult[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = threads }, i => results[i] = Run(jobs[i].seed, walkers, jobs[i].s, days, rules));
        return results.ToList();
    }

    public static double Median(IEnumerable<double> v)
    {
        var a = v.OrderBy(x => x).ToArray();
        if (a.Length == 0) return double.NaN;
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2;
    }

    /// <summary>One table per strategy (a row per seed, then the medians), and a summary table of medians.</summary>
    public static void Print(TextWriter o, IReadOnlyList<RiverRunResult> rows, bool perSeed = true)
    {
        string H = " seed | row | built d | ford-landing | trade c | boat c | market c | grain | turn-back | barges | mean wait s | worst wait s | trip s | rep | money c";
        string Line(RiverRunResult r) =>
            $"{r.Seed,5} | {r.Row,3} | {(r.BuiltDay < 0 ? "-" : r.BuiltDay.ToString("F1")),7} | {r.FordDetourRows,12:F0} | {r.TradeIncome,7:F0} | {r.BoatIncome,6:F0} | {r.MarketIncome,8:F0} | {r.Grain,5} | {ToyHarness.Pct(r.TurnBack),9} | {ToyHarness.Pct(r.TurnBackBarge),6} | {r.MeanWait,11:F2} | {r.WorstWait,12:F1} | {r.MeanTrip,6:F2} | {r.Reputation,3:F0} | {r.Money,7:F0}";
        foreach (var g in rows.GroupBy(r => (r.Strategy, r.Walkers)))
        {
            if (!perSeed) continue;
            o.WriteLine($"\n{Name(g.Key.Strategy)}, {g.Key.Walkers} walkers");
            o.WriteLine(H);
            foreach (var r in g.OrderBy(r => r.Seed)) o.WriteLine(Line(r));
        }
        o.WriteLine("\nMEDIANS OVER SEEDS");
        o.WriteLine(" strategy                      | walkers | trade c | boat c | market c | turn-back | barges | mean wait s | worst wait s | trip s | rep | money c");
        foreach (var g in rows.GroupBy(r => (r.Strategy, r.Walkers)).OrderBy(g => g.Key.Walkers).ThenBy(g => g.Key.Strategy))
        {
            var a = g.ToList();
            o.WriteLine($" {Name(g.Key.Strategy),-29} | {g.Key.Walkers,7} | {Median(a.Select(r => r.TradeIncome)),7:F0} | {Median(a.Select(r => r.BoatIncome)),6:F0} | {Median(a.Select(r => r.MarketIncome)),8:F0} | {ToyHarness.Pct(Median(a.Select(r => r.TurnBack))),9} | {ToyHarness.Pct(Median(a.Select(r => r.TurnBackBarge))),6} | {Median(a.Select(r => r.MeanWait)),11:F2} | {Median(a.Select(r => r.WorstWait)),12:F1} | {Median(a.Select(r => r.MeanTrip)),6:F2} | {Median(a.Select(r => r.Reputation)),3:F0} | {Median(a.Select(r => r.Money)),7:F0}");
        }
    }
}

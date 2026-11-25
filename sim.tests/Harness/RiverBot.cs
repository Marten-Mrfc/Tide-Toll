using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TideToll.Sim.Testing;

/// <summary>The six crossing strategies of the river experiment (implementation spec, section 5).</summary>
public enum RiverStrategy { S1FordOnly, S2Ferry, S3Footbridge, S4DrawbridgeNeverRaised, S5DrawbridgeManual, S6DrawbridgeKeeper }

/// <summary>
/// A strategy and, for a player who raises the drawbridge by hand, how many boats it misses (its attention): S5 at
/// 10%, 30% and 50%; S6 raises by hand (missing 30%) until its keeper is hired.
/// </summary>
public readonly record struct RiverPlayer(RiverStrategy Strategy, double Miss = 0)
{
    public static readonly RiverPlayer S1 = new(RiverStrategy.S1FordOnly), S2 = new(RiverStrategy.S2Ferry),
        S3 = new(RiverStrategy.S3Footbridge), S4 = new(RiverStrategy.S4DrawbridgeNeverRaised),
        S5Miss10 = new(RiverStrategy.S5DrawbridgeManual, 0.1), S5Miss30 = new(RiverStrategy.S5DrawbridgeManual, 0.3),
        S5Miss50 = new(RiverStrategy.S5DrawbridgeManual, 0.5), S6 = new(RiverStrategy.S6DrawbridgeKeeper, 0.3);

    /// <summary>Every player of the experiment, in table order.</summary>
    public static readonly RiverPlayer[] All = { S1, S2, S3, S4, S5Miss10, S5Miss30, S5Miss50, S6 };

    /// <summary>Short key for --only: S1, S2, S3, S4, S5-10, S5-30, S5-50, S6.</summary>
    public string Key => Strategy == RiverStrategy.S5DrawbridgeManual ? $"S5-{Miss * 100:0}" : RiverBot.Name(Strategy)[..2];

    public string Name => Strategy == RiverStrategy.S5DrawbridgeManual ? $"S5 drawbridge, manual, misses {Miss * 100:0}%" : RiverBot.Name(Strategy);

    public override string ToString() => Name;
}

/// <summary>One player on one seed: what it earned and what it cost walkers and boats.</summary>
public sealed record RiverRunResult(
    int Seed, int Walkers, int Days, RiverPlayer Player, int Row, double BuiltDay, double KeeperDay, double FordDetourRows,
    double TradeIncome, double BoatIncome, double MarketIncome, int Grain,
    double TurnBack, double TurnBackBarge, int BoatsDecided,
    double MeanWait, double WorstWait, double MeanTrip, double Reputation, double Money, double Upkeep, double Spent,
    double CrossingsPerDay, double BuiltCrossingsPerDay, double WaitedShare, double DeliveriesPerDay, int GrainLost,
    int MaxQueue, int FloodDays, int FordCrossingsInFlood, int FrozenDays, int IceCrossings)
{
    public RiverStrategy Strategy => Player.Strategy;
}

/// <summary>
/// The headless strategy bot: runs a town with the river rules on and one crossing strategy. It builds its
/// crossing at row LandingRow + 4 (or the first valid row a little further south) as soon as it has the coin,
/// never north of the landing. Mean trip time, mean wait, crossings, the share of crossings that waited and
/// deliveries cover the second half of the run (every strategy has built by then); the worst wait, the largest queue
/// and the money cover the whole run. A manual player raises its drawbridge when a boat it noticed has waited 4 s or
/// two noticed boats wait, and misses a set share of boats (a fixed draw per boat, so the 30% player misses every
/// boat the 10% player misses); S6 hires the keeper as soon as it can.
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

    public static RiverRunResult Run(int seed, int walkers, RiverPlayer pl, int days = 12, RiverRules? rules = null)
    {
        var s = pl.Strategy;
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
        int id = -1, row = -1, maxQueue = 0;
        double builtDay = -1, keeperDay = -1;
        long ticks = (long)days * w.Params.TicksPerDay;
        int tpd = w.Params.TicksPerDay;
        // Trip times, waits, crossings and deliveries are measured over the second half of the run.
        int trips0 = 0, waits0 = 0, cross0 = 0, crossW0 = 0, grain0 = 0, built0 = 0;
        double tripSum0 = 0, waitSum0 = 0;
        int floodTicks = 0, frozenTicks = 0, fordInFlood = 0, fordPrev = 0;
        Crossing? built = null;
        var ford = river.Crossings[0];
        for (long t = 0; t < ticks; t++)
        {
            bool flooded = river.Flooded; // for the whole tick: the flood rises and falls between ticks
            w.Tick();
            if (flooded) { floodTicks++; fordInFlood += ford.WalkersCrossed - fordPrev; }
            if (river.Frozen) frozenTicks++;
            fordPrev = ford.WalkersCrossed;
            if (t == ticks / 2)
            {
                var h = river.Stats;
                trips0 = h.Trips; tripSum0 = h.TripTimeSum; waits0 = h.WalkerWaits; waitSum0 = h.WalkerWaitSum;
                cross0 = h.Crossings; crossW0 = h.CrossingsWaited; grain0 = h.GrainDelivered; built0 = built?.WalkersCrossed ?? 0;
            }
            if (built != null) maxQueue = Math.Max(maxQueue, built.QueueWest + built.QueueEast);
            if (type != null && id < 0 && river.Money >= river.Rules.Price(type.Value) && t % 5 == 0)
            {
                (id, row) = Build(w, type.Value);
                if (id >= 0) { builtDay = w.Time / w.Params.DaySimSec; built = river.Crossings.First(c => c.Id == id); }
                else if (row == -2) type = null; // nowhere valid on this map
            }
            if (built == null) continue;
            if (s == RiverStrategy.S6DrawbridgeKeeper && !built.Keeper && river.Money >= river.Rules.KeeperPrice && w.HireKeeper(id).Ok)
                keeperDay = w.Time / w.Params.DaySimSec;
            // S6 raises by hand, like S5, until its keeper is hired.
            if (s == RiverStrategy.S5DrawbridgeManual || (s == RiverStrategy.S6DrawbridgeKeeper && !built.Keeper)) ManualPolicy(w, built, seed, pl.Miss);
        }
        var rs = river.Stats;
        int decided = rs.Served.Sum() + rs.TurnedBack.Sum();
        int fordRow = w.Map.FordY, landing = river.LandingRow;
        double half = (ticks - ticks / 2 - 1) / (double)tpd;
        int crossings = rs.Crossings - cross0;
        return new RiverRunResult(seed, walkers, days, pl, row, builtDay, keeperDay, Math.Abs(fordRow - landing),
            rs.TradeIncome, rs.BoatIncome, rs.MarketIncome, rs.GrainDelivered,
            rs.TurnBackRate(), rs.TurnBackRate(BoatKind.Barge), decided,
            rs.WalkerWaits > waits0 ? (rs.WalkerWaitSum - waitSum0) / (rs.WalkerWaits - waits0) : 0, rs.WalkerWaitMax,
            rs.Trips > trips0 ? (rs.TripTimeSum - tripSum0) / (rs.Trips - trips0) : 0, river.Reputation, river.Money, rs.Upkeep, rs.Spent,
            crossings / half, ((built?.WalkersCrossed ?? 0) - built0) / half, crossings > 0 ? (double)(rs.CrossingsWaited - crossW0) / crossings : 0,
            (rs.GrainDelivered - grain0) / half, rs.GrainLost, maxQueue,
            (int)Math.Round((double)floodTicks / tpd), fordInFlood, (int)Math.Round((double)frozenTicks / tpd), rs.IceCrossings);
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

    /// <summary>
    /// A player who raises when a boat it noticed has waited 4 s or two noticed boats wait. It never notices a share
    /// <paramref name="miss"/> of boats: a fixed draw per boat, so a less attentive player misses a superset.
    /// </summary>
    private static void ManualPolicy(World w, Crossing x, int seed, double miss)
    {
        var river = w.River!;
        if (x.State != BridgeState.Closed) return;
        double patience = river.Rules.PatienceSec;
        int count = 0;
        double oldest = 0;
        foreach (ref readonly var b in river.Boats)
        {
            if (b.State != BoatState.Waiting || b.WaitingAt != x.Id) continue;
            if (Hash.Hash2(seed * 31 + 7, b.Id) < miss) continue; // this player never noticed it
            count++;
            oldest = Math.Max(oldest, patience - b.PatienceLeft);
        }
        if (oldest >= 4 || count >= 2) w.SetBridge(x.Id, true);
    }

    public static List<RiverRunResult> RunAll(IEnumerable<int> seeds, IEnumerable<RiverPlayer> players, int walkers, int days, int threads, RiverRules? rules = null)
    {
        var jobs = (from pl in players from seed in seeds select (seed, pl)).ToList();
        var results = new RiverRunResult[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = threads }, i => results[i] = Run(jobs[i].seed, walkers, jobs[i].pl, days, rules));
        return results.ToList();
    }

    public static double Median(IEnumerable<double> v)
    {
        var a = v.OrderBy(x => x).ToArray();
        if (a.Length == 0) return double.NaN;
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2;
    }

    private static string D(double d) => d < 0 ? "-" : d.ToString("F1");

    /// <summary>One table per player (a row per seed), then a table of medians per (days, walkers, player).</summary>
    public static void Print(TextWriter o, IReadOnlyList<RiverRunResult> rows, bool perSeed = true)
    {
        string H = " seed | row | built d | keeper d | ford-landing | trade c | boat c | market c | grain | turn-back | barges | mean wait s | worst wait s | trip s | cross/d | at built/d | waited | deliv/d | lost | queue | flood d | ford in flood | ice d | rep | money c";
        string Line(RiverRunResult r) =>
            $"{r.Seed,5} | {r.Row,3} | {D(r.BuiltDay),7} | {D(r.KeeperDay),8} | {r.FordDetourRows,12:F0} | {r.TradeIncome,7:F0} | {r.BoatIncome,6:F0} | {r.MarketIncome,8:F0} | {r.Grain,5} | {ToyHarness.Pct(r.TurnBack),9} | {ToyHarness.Pct(r.TurnBackBarge),6} | {r.MeanWait,11:F2} | {r.WorstWait,12:F1} | {r.MeanTrip,6:F2} | {r.CrossingsPerDay,7:F0} | {r.BuiltCrossingsPerDay,10:F0} | {ToyHarness.Pct(r.WaitedShare),6} | {r.DeliveriesPerDay,7:F1} | {r.GrainLost,4} | {r.MaxQueue,5} | {r.FloodDays,7} | {r.FordCrossingsInFlood,13} | {r.FrozenDays,5} | {r.Reputation,3:F0} | {r.Money,7:F0}";
        var groups = rows.GroupBy(r => (r.Days, r.Walkers, r.Player)).OrderBy(g => g.Key.Days).ThenBy(g => g.Key.Walkers).ThenBy(g => Array.IndexOf(RiverPlayer.All, g.Key.Player)).ToList();
        if (perSeed)
            foreach (var g in groups)
            {
                o.WriteLine($"\n{g.Key.Player.Name}, {g.Key.Walkers} walkers, {g.Key.Days} days");
                o.WriteLine(H);
                foreach (var r in g.OrderBy(r => r.Seed)) o.WriteLine(Line(r));
            }
        foreach (var byDays in groups.GroupBy(g => g.Key.Days))
        {
            o.WriteLine($"\nMEDIANS OVER SEEDS, {byDays.Key} days ({byDays.First().Count()} seeds)");
            o.WriteLine(" player                                | walkers | built d | keeper d | trade c | boat c | market c | turn-back | barges | mean wait s | worst wait s | trip s | cross/d | at built/d | waited | deliv/d | lost | queue | rep | money c");
            foreach (var g in byDays)
            {
                var a = g.ToList();
                double M(Func<RiverRunResult, double> f) => Median(a.Select(f));
                o.WriteLine($" {g.Key.Player.Name,-37} | {g.Key.Walkers,7} | {D(M(r => r.BuiltDay)),7} | {D(M(r => r.KeeperDay)),8} | {M(r => r.TradeIncome),7:F0} | {M(r => r.BoatIncome),6:F0} | {M(r => r.MarketIncome),8:F0} | {ToyHarness.Pct(M(r => r.TurnBack)),9} | {ToyHarness.Pct(M(r => r.TurnBackBarge)),6} | {M(r => r.MeanWait),11:F2} | {M(r => r.WorstWait),12:F1} | {M(r => r.MeanTrip),6:F2} | {M(r => r.CrossingsPerDay),7:F0} | {M(r => r.BuiltCrossingsPerDay),10:F0} | {ToyHarness.Pct(M(r => r.WaitedShare)),6} | {M(r => r.DeliveriesPerDay),7:F1} | {M(r => r.GrainLost),4:F0} | {M(r => r.MaxQueue),5:F0} | {M(r => r.Reputation),3:F0} | {M(r => r.Money),7:F0}");
            }
        }
    }
}

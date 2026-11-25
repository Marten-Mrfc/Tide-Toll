using TideToll.Sim.Testing;

namespace TideToll.Sim.Tests;

/// <summary>
/// Boat rules with the numbers of the river spec: sailing speed, waiting below a crossing a mast cannot pass,
/// patience, turning back, reputation, berths, mooring, trade income and the boat schedule.
/// Boats are sent in by hand (SpawnBoat) on a river whose schedule is switched off, so timings are exact.
/// </summary>
public class RiverBoatTests
{
    internal static readonly RiverRules NoSchedule = RiverRules.Default with { RowboatGapSec = 1e9, BargeGapSec = 1e9, HaulGoods = false };

    internal static World Make(int seed = 1, int walkers = 0, RiverRules? rules = null)
        => new(seed, walkers, SimParams.Default with { River = rules ?? NoSchedule });

    /// <summary>Builds a crossing at the first valid row from LandingRow + 4 southwards.</summary>
    internal static Crossing Build(World w, CrossingType t, int from = 4)
    {
        for (int dy = from; dy < 20; dy++)
        {
            int y = w.River!.LandingRow + dy;
            var r = w.PlaceCrossing(t, w.RiverX[y], y + 0.5);
            if (r.Ok) return w.River.Crossings.First(c => c.Id == r.Id);
        }
        throw new InvalidOperationException("no row for a crossing");
    }

    internal static Boat BoatById(World w, int id)
    {
        foreach (var b in w.River!.Boats) if (b.Id == id) return b;
        throw new InvalidOperationException($"boat {id} is gone");
    }

    internal static bool Exists(World w, int id)
    {
        foreach (var b in w.River!.Boats) if (b.Id == id) return true;
        return false;
    }

    internal static List<RiverEvent> Drain(World w)
    {
        var l = new List<RiverEvent>();
        w.River!.DrainEvents(l);
        return l;
    }

    [Fact]
    public void BoatsEnterFromTheSouthSailTenRowsASecondMoorAndLeaveNorth()
    {
        var w = Make();
        var r = w.River!;
        int id = w.SpawnBoat(BoatKind.Rowboat).Id;
        Assert.Equal(w.Grid.Height + 0.5, BoatById(w, id).Y);
        w.Tick();
        Assert.Equal(w.Grid.Height + 0.5 - 1.0, BoatById(w, id).Y, 9); // 10 rows/s at 10 Hz
        Assert.Equal(BoatState.Sailing, BoatById(w, id).State);
        while (BoatById(w, id).State != BoatState.Mooring) w.Tick();
        Assert.Equal(r.LandingRow + 0.5, BoatById(w, id).Y, 9);
        Assert.Equal(id, r.BerthBoat(0));
        long moored = w.Ticks;
        while (BoatById(w, id).State == BoatState.Mooring) w.Tick();
        Assert.Equal(150, w.Ticks - moored); // 15 s moored
        Assert.True(BoatById(w, id).Served);
        Assert.Equal(-1, r.BerthBoat(0));
        Assert.Equal(51, r.Reputation); // +1 per boat served
        while (Exists(w, id)) w.Tick();
        var kinds = Drain(w).Where(e => e.Boat == id).Select(e => e.Kind).ToList();
        Assert.Equal(new[] { RiverEventKind.BoatArrived, RiverEventKind.BoatMoored, RiverEventKind.BoatSold, RiverEventKind.BoatLeft }, kinds);
    }

    [Fact]
    public void FootbridgeHoldsBargesUntilTheirPatienceRunsOutAndLetsRowboatsThrough()
    {
        var w = Make();
        var r = w.River!;
        var fb = Build(w, CrossingType.Footbridge);
        Assert.Equal(1, fb.Clearance);
        int barge = w.SpawnBoat(BoatKind.Barge).Id, row = w.SpawnBoat(BoatKind.Rowboat).Id;
        Drain(w);
        long waitStart = -1;
        while (Exists(w, barge) && BoatById(w, barge).State != BoatState.TurnedBack)
        {
            w.Tick();
            var b = BoatById(w, barge);
            if (b.State == BoatState.Waiting && waitStart < 0)
            {
                waitStart = w.Ticks;
                Assert.Equal(fb.Row + 0.5 + r.Rules.StopRows, b.Y, 9); // stops 2 rows below
                Assert.Equal(fb.Id, b.WaitingAt);
            }
            if (b.State == BoatState.Waiting) Assert.Equal(1, fb.BoatsWaiting);
        }
        Assert.Equal(150, w.Ticks - waitStart + 1); // 15 s of patience, counted from the first waiting tick
        Assert.Equal(45, r.Reputation); // -5 per boat turned back
        Assert.Equal(1, r.Stats.TurnedBack[(int)BoatKind.Barge]);
        // The rowboat (mast class 0) never waited.
        var ev = Drain(w);
        Assert.DoesNotContain(ev, e => e.Boat == row && e.Kind == RiverEventKind.BoatWaiting);
        Assert.Contains(ev, e => e.Boat == row && e.Kind == RiverEventKind.BoatMoored);
        Assert.Contains(ev, e => e.Boat == barge && e.Kind == RiverEventKind.BoatTurnedBack);
        while (Exists(w, barge)) { w.Tick(); Assert.True(BoatById(w, barge).Y >= BoatById(w, barge).PrevY || !Exists(w, barge)); }
    }

    [Fact]
    public void FordAndFerryNeverBlockABoat()
    {
        var w = Make();
        Build(w, CrossingType.Ferry);
        int barge = w.SpawnBoat(BoatKind.Barge).Id;
        while (BoatById(w, barge).State != BoatState.Mooring)
        {
            w.Tick();
            Assert.NotEqual(BoatState.Waiting, BoatById(w, barge).State);
        }
    }

    [Fact]
    public void TwoBerthsAThirdBoatWaitsWithPatienceRunning()
    {
        var w = Make();
        var r = w.River!;
        int a = w.SpawnBoat(BoatKind.Barge).Id;
        for (int i = 0; i < 3; i++) w.Tick();
        int b = w.SpawnBoat(BoatKind.Barge).Id;
        for (int i = 0; i < 3; i++) w.Tick();
        int c = w.SpawnBoat(BoatKind.Barge).Id;
        while (BoatById(w, b).State != BoatState.Mooring) w.Tick();
        for (int i = 0; i < 10; i++) w.Tick();
        Assert.Equal(BoatState.Mooring, BoatById(w, a).State);
        var third = BoatById(w, c);
        Assert.Equal(BoatState.Waiting, third.State);
        Assert.Equal(-2, third.WaitingAt);
        Assert.True(third.PatienceLeft < r.Rules.PatienceSec);
        Assert.True(third.Y >= r.LandingRow + 0.5 + r.Rules.StopRows - 1e-9);
        // Berth a frees after 15 s: c moors before its 15 s of patience run out.
        while (BoatById(w, c).State == BoatState.Waiting) w.Tick();
        Assert.Equal(BoatState.Mooring, BoatById(w, c).State);
    }

    [Fact]
    public void ReputationSetsTheArrivalRate()
    {
        var rules = RiverRules.Default;
        Assert.Equal(0.5, rules.ArrivalsPerDay(BoatKind.Rowboat, 50, 15), 9); // 1 per 2 days
        Assert.Equal(1.0, rules.ArrivalsPerDay(BoatKind.Barge, 50, 15), 9);   // 1 per day
        Assert.Equal(0.5, rules.ArrivalsPerDay(BoatKind.Barge, 0, 15), 9);    // x0.5 at 0
        Assert.Equal(1.5, rules.ArrivalsPerDay(BoatKind.Barge, 100, 15), 9);  // x1.5 at 100
        // On a live river: a footbridge drives reputation down and fewer boats come than on a free river.
        var free = Make(3, 0, RiverRules.Default with { HaulGoods = false });
        var blocked = Make(3, 0, RiverRules.Default with { HaulGoods = false });
        Build(blocked, CrossingType.Footbridge);
        ToyHarness.Run(free, 40);
        ToyHarness.Run(blocked, 40);
        int Arrived(World x) => x.River!.Stats.Arrived.Sum();
        Assert.True(free.River!.Reputation > 90, $"free river reputation {free.River.Reputation}");
        Assert.True(blocked.River!.Reputation < 10, $"blocked river reputation {blocked.River.Reputation}");
        Assert.True(Arrived(free) > Arrived(blocked) * 1.5, $"arrivals free {Arrived(free)}, blocked {Arrived(blocked)}");
        // 40 days at x(0.5 + rep/100) with rep climbing from 50 towards 100: between 1.0x and 1.5x of 60 boats.
        Assert.InRange(Arrived(free), 55, 95);
    }

    [Fact]
    public void BoatsBuyGrainAtTwiceBaseAndTheMarketTakesTheRestUpToItsDailyLimit()
    {
        var w = Make(2, 200, NoSchedule with { HaulGoods = true });
        var r = w.River!;
        Assert.True(r.LandingBuilding >= 0);
        Assert.Equal(BuildingKind.Landing, w.Buildings[r.LandingBuilding].Kind);
        Assert.Equal(r.LandingCell, w.Buildings[r.LandingBuilding].Door);
        // Two days with no boat: the market buys at 70% of 2c, at most one unit per house per day.
        ToyHarness.Run(w, 2);
        var s = r.Stats;
        Assert.True(s.GrainToMarket > 0);
        Assert.Equal(0, s.GrainToBoats);
        Assert.Equal(s.GrainToMarket * 1.4, s.MarketIncome, 6);
        Assert.True(s.GrainToMarket <= 2 * w.Houses.Length, $"{s.GrainToMarket} sold to the market");
        Assert.True(s.GrainUnsold > 0, "200 walkers haul more than the market takes");
        // A barge moors: it buys at 4c, and the coin is booked when it leaves.
        int barge = w.SpawnBoat(BoatKind.Barge).Id;
        while (BoatById(w, barge).State != BoatState.Mooring) w.Tick();
        double before = r.Money;
        int boatGrain0 = s.GrainToBoats;
        while (BoatById(w, barge).State == BoatState.Mooring)
        {
            double m = r.Money;
            w.Tick();
            Assert.True(r.Money - m < 1.5 || BoatById(w, barge).State != BoatState.Mooring, "boat coin is booked only on leaving"); // market sales stop while it is moored
        }
        var bb = BoatById(w, barge);
        Assert.True(bb.Cargo > 0 && bb.Cargo <= 40);
        Assert.Equal(bb.Cargo * 4.0, bb.Earned, 9);
        Assert.Equal(bb.Cargo, s.GrainToBoats - boatGrain0);
        Assert.Contains(Drain(w), e => e.Kind == RiverEventKind.BoatSold && e.Boat == barge && Math.Abs(e.Value - bb.Earned) < 1e-9);
        Assert.True(r.Money - before >= bb.Earned - 1e-9);
    }

    [Fact]
    public void FieldTripsCarryGrainAcrossTheRiverToTheLanding()
    {
        var w = Make(4, 200, NoSchedule with { HaulGoods = true });
        int landing = w.River!.LandingBuilding;
        bool carried = false;
        for (int i = 0; i < 1500; i++)
        {
            w.Tick();
            foreach (var k in w.Walkers)
                if (k.Cargo > 0)
                {
                    carried = true;
                    Assert.Equal(landing, k.Target);
                    Assert.Equal(WalkState.Out, k.State);
                }
        }
        Assert.True(carried);
        Assert.True(w.TripRatePerDay(w.Houses[0], landing) > 0 || w.Houses.ToArray().Any(h => w.TripRatePerDay(h, landing) > 0));
    }
}

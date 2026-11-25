using TideToll.Sim.Testing;
using static TideToll.Sim.Tests.RiverBoatTests;
using static TideToll.Sim.Tests.RiverCrossingTests;
using static TideToll.Sim.Tests.RiverWalkerTests;

namespace TideToll.Sim.Tests;

/// <summary>
/// What limits each crossing (doc, crossing table): a bridge lets on at most its walker capacity a day (footbridge about
/// 120, drawbridge about 250 while closed), a full ferry carries about 30 each way a day, the ford is slow but has no
/// limit. Queues grow with traffic, and walkers who wait deliver less grain. Also the crossing counts.
/// </summary>
public class RiverCapacityTests
{
    private static double CarriedPerDay(World w, Crossing x, int days)
    {
        int c0 = x.WalkersCrossed;
        ToyHarness.Run(w, days);
        return (double)(x.WalkersCrossed - c0) / days;
    }

    [Theory]
    [InlineData(CrossingType.Footbridge)]
    [InlineData(CrossingType.Drawbridge)]
    public void ABridgeLetsOnNoMoreThanItsCapacityAndTheQueueCostsGrain(CrossingType t)
    {
        // A narrow bridge: 20 walkers a day. 400 walkers want to cross much more often than that.
        var narrow = Rich with { HaulGoods = true, FootbridgeWalkersPerDay = 20, DrawbridgeWalkersPerDay = 20 };
        var wide = Rich with { HaulGoods = true };
        var a = Make(5, 400, narrow);
        var b = Make(5, 400, wide);
        var xa = Build(a, t);
        var xb = Build(b, t);
        ToyHarness.Run(a, 1); ToyHarness.Run(b, 1);
        int g0a = a.River!.Stats.GrainDelivered, g0b = b.River!.Stats.GrainDelivered, maxQueue = 0;
        int c0 = xa.WalkersCrossed;
        for (int i = 0; i < 4 * a.Params.TicksPerDay; i++)
        {
            a.Tick(); b.Tick();
            maxQueue = Math.Max(maxQueue, xa.QueueWest + xa.QueueEast);
        }
        double perDay = (xa.WalkersCrossed - c0) / 4.0, wideDay = 0;
        // At most the capacity (plus one bucket of 1 s), and a real queue.
        Assert.True(perDay <= 20 + 2, $"{perDay:F1} crossings a day on a bridge of 20 a day");
        Assert.True(perDay >= 10, $"only {perDay:F1} crossings a day: the bridge is not used");
        Assert.True(maxQueue >= 3, $"largest queue {maxQueue}");
        Assert.True(xa.AvgWait > 1, $"learned wait {xa.AvgWait:F2} s");
        wideDay = (xb.WalkersCrossed - 0) / 5.0;
        Assert.True(wideDay > perDay * 1.5, $"wide bridge {wideDay:F1} a day, narrow {perDay:F1}");
        // Waiting costs deliveries: the narrow bridge town hauls less grain to the landing.
        int ga = a.River.Stats.GrainDelivered - g0a, gb = b.River.Stats.GrainDelivered - g0b;
        Assert.True(ga < gb, $"grain delivered: narrow {ga}, wide {gb}");
        AssertRiverInvariants(a, "narrow bridge");
    }

    [Fact]
    public void AFullFerryCarriesAboutThirtyEachWayADay()
    {
        // Walkers who never learn the ferry's wait keep coming however long the queue: the ferry runs full.
        int seed = Enumerable.Range(1, 30).OrderByDescending(s => { var m = Make(s, 0, Rich); return Math.Abs(m.Map.FordY - m.River!.LandingRow); }).First();
        var w = Make(seed, 500, Rich with { HaulGoods = true, WaitAlpha = 0 });
        var x = Build(w, CrossingType.Ferry);
        var r = w.River!.Rules;
        ToyHarness.Run(w, 1);
        Drain(w);
        int landed = 0, legs = 0, full = 0, toEast = 0, toWest = 0;
        long lastLand = -1;
        int c0 = x.WalkersCrossed;
        for (int t = 0; t < 4 * w.Params.TicksPerDay; t++)
        {
            w.Tick();
            foreach (var e in Drain(w))
            {
                if (e.Crossing != x.Id) continue;
                if (e.Kind == RiverEventKind.FerryLanded)
                {
                    landed += (int)e.Value; legs++; lastLand = e.Tick;
                    if (x.FerrySide > 0) toEast += (int)e.Value; else toWest += (int)e.Value;
                }
                if (e.Kind == RiverEventKind.FerryDeparted)
                {
                    if (e.Value == r.FerryCapacity) full++;
                    // It turns round at the stage before anyone boards: never leaves sooner than FerryTurnSec after landing.
                    if (lastLand >= 0) Assert.True(e.Tick - lastLand >= r.FerryTurnSec * w.Params.TickHz - 1e-9, $"left {e.Tick - lastLand} ticks after landing");
                }
            }
        }
        // The busier direction (traffic is rarely even) runs at the ferry's capacity.
        double eachWayPerDay = Math.Max(toEast, toWest) / 4.0;
        double legSec = r.FerryCrossSec + r.FerryTurnSec;
        double capacity = w.Params.DaySimSec / (2 * legSec) * r.FerryCapacity; // 30 each way a day
        Assert.Equal(30, capacity, 6);
        Assert.True(full > legs / 2, $"only {full} of {legs} legs full: the ferry is not saturated");
        Assert.True(eachWayPerDay <= capacity + 1, $"{eachWayPerDay:F1} each way a day");
        Assert.True(eachWayPerDay >= 0.75 * capacity, $"{eachWayPerDay:F1} each way a day, capacity {capacity}");
        // Every rider who landed reached the far bank: counted as a crossing by the ferry.
        Assert.InRange(x.WalkersCrossed - c0, landed - r.FerryCapacity, landed);
    }

    [Fact]
    public void CrossingsAreCountedByCrossingAndWaitsByWalker()
    {
        var w = Make(2, 300, Rich with { HaulGoods = true });
        var f = Build(w, CrossingType.Ferry);
        var d = Build(w, CrossingType.Drawbridge, 9);
        for (int t = 0; t < 4 * 150; t++)
        {
            if (t % 150 == 0) w.SetBridge(d.Id, true);
            w.Tick();
        }
        var s = w.River!.Stats;
        int byCrossing = w.River.Crossings.Sum(c => c.WalkersCrossed);
        Assert.Equal(s.Crossings, byCrossing + s.IceCrossings);
        Assert.True(w.River.Crossings[0].WalkersCrossed > 0 && f.WalkersCrossed > 0 && d.WalkersCrossed > 0,
            $"ford {w.River.Crossings[0].WalkersCrossed}, ferry {f.WalkersCrossed}, drawbridge {d.WalkersCrossed}");
        // At the ferry nearly everyone waits (at the ford, only walkers who gave up a queue elsewhere first).
        foreach (var c in w.River.Crossings) Assert.InRange(c.WalkersWaited, 0, c.WalkersCrossed);
        Assert.True(f.WalkersWaited > f.WalkersCrossed / 2, $"ferry {f.WalkersWaited} of {f.WalkersCrossed} waited");
        Assert.Equal(s.CrossingsWaited, w.River.Crossings.Sum(c => c.WalkersWaited));
    }
}

using TideToll.Sim.Testing;
using static TideToll.Sim.Tests.RiverBoatTests;
using static TideToll.Sim.Tests.RiverCrossingTests;

namespace TideToll.Sim.Tests;

/// <summary>Walkers at the new crossings: they queue on the bank, a rising leaf never strands them, an erased queue disperses.</summary>
public class RiverWalkerTests
{
    /// <summary>The invariants of InvariantTests, adapted: a ferry rider stands (for the books) on its boarding cell while drawn on the boat.</summary>
    internal static void AssertRiverInvariants(World w, string when)
    {
        int visible = 0, n = w.Grid.Width;
        foreach (var k in w.Walkers)
        {
            Assert.False(double.IsNaN(k.X) || double.IsNaN(k.Y), $"{when}: walker {k.Id} position is NaN");
            if (!k.Visible) { Assert.Equal(WalkerHold.None, k.Hold); continue; }
            visible++;
            Assert.False(w.IsBlocked(k.Cell), $"{when}: walker {k.Id} on blocked cell {k.Cell} ({k.Hold})");
            if (k.Hold != WalkerHold.Riding) Assert.Equal(k.Cell, (int)Math.Floor(k.Y) * n + (int)Math.Floor(k.X));
            if (k.Hold != WalkerHold.None) Assert.Equal(0, k.Stuck);
        }
        int onCells = 0;
        foreach (ushort h in w.WalkersOnCell) onCells += h;
        Assert.Equal(visible, onCells);
        foreach (float v in w.Cost) Assert.False(float.IsNaN(v) || v < 0, $"{when}: cost {v}");
    }

    private static int WalkerOnSpan(World w, Crossing x)
    {
        foreach (var k in w.Walkers) if (k.Visible && k.Hold == WalkerHold.None && x.Cells.Contains(k.Cell)) return k.Id;
        return -1;
    }

    private static Walker ById(World w, int id)
    {
        foreach (var k in w.Walkers) if (k.Id == id) return k;
        throw new InvalidOperationException();
    }

    [Fact]
    public void AWalkerOnARisingLeafWalksOffOrIsSetDownOnTheNearerBankAndCarriesOn()
    {
        var w = Make(2, 300, Rich with { HaulGoods = true });
        var x = Build(w, CrossingType.Drawbridge);
        ToyHarness.Run(w, 1);
        int moved = 0, evicted = 0;
        for (int round = 0; round < 12 && moved + evicted < 6; round++)
        {
            int id = -1;
            for (int t = 0; t < 600 && id < 0; t++) { w.Tick(); id = WalkerOnSpan(w, x); }
            Assert.True(id >= 0, "nobody used the drawbridge");
            var before = ById(w, id);
            w.SetBridge(x.Id, true);
            // While it rises the walker keeps going.
            w.Tick();
            var during = ById(w, id);
            Assert.True(during.X != before.X || during.Y != before.Y || during.Cell != before.Cell);
            while (x.State != BridgeState.Raised) w.Tick();
            var after = ById(w, id);
            Assert.DoesNotContain(after.Cell, x.Cells);
            Assert.False(w.IsBlocked(after.Cell));
            Assert.NotEqual(0, w.Map.Bank[after.Cell]); // on a bank, not in the water
            if (Math.Abs(after.X - before.X) > 1.5) evicted++; else moved++;
            AssertRiverInvariants(w, "just raised");
            // It finishes its trip: within 40 s it has arrived somewhere.
            var state = after.State;
            int target = after.Target;
            bool arrived = false;
            for (int t = 0; t < 400 && !arrived; t++) { w.Tick(); var k = ById(w, id); arrived = k.State != state || k.Target != target; }
            Assert.True(arrived, $"walker {id} did not finish its trip after the leaf rose");
            while (x.State != BridgeState.Closed) w.Tick();
        }
        Assert.True(moved + evicted >= 6);
    }

    [Fact]
    public void WalkersQueueWhileTheBridgeIsUpAndGoWhenItIsDown()
    {
        var w = Make(2, 300, Rich with { HaulGoods = true });
        var x = Build(w, CrossingType.Drawbridge);
        ToyHarness.Run(w, 1);
        int maxQueue = 0;
        var learned = new List<double>(); // the crossing's learned wait once each raise's queue has gone over
        for (int cycle = 0; cycle < 5; cycle++)
        {
            w.SetBridge(x.Id, true);
            while (x.State != BridgeState.Lowering)
            {
                w.Tick();
                if (x.State == BridgeState.Raised) Assert.Equal(-1, WalkerOnSpan(w, x));
                maxQueue = Math.Max(maxQueue, x.QueueWest + x.QueueEast);
            }
            while (x.State != BridgeState.Closed) w.Tick();
            // Once down, the queue goes over at the bridge's capacity (about 250 a day): never faster, and nobody is left behind.
            var waiting = new HashSet<int>();
            foreach (var k in w.Walkers) if (k.Hold == WalkerHold.Waiting && k.HoldAt == x.Id) waiting.Add(k.Id);
            int queued = waiting.Count;
            double perTick = w.River!.Rules.DrawbridgeWalkersPerDay / w.Params.DaySimSec / w.Params.TickHz;
            // Walkers reaching the far bank in the first T ticks stepped on in those ticks or in the 2 s before.
            int crossed0 = x.WalkersCrossed, ticks = 0;
            double transit = 2 * w.Params.TickHz;
            while (ticks < Math.Ceiling(queued / perTick) + 2)
            {
                w.Tick(); ticks++;
                foreach (var k in w.Walkers)
                    if (!(k.Hold == WalkerHold.Waiting && k.HoldAt == x.Id)) waiting.Remove(k.Id); // its turn came (it may queue again later)
                int crossed = x.WalkersCrossed - crossed0;
                Assert.True(crossed <= perTick * (ticks + transit + w.Params.TickHz * w.River.Rules.BridgeBurstSec) + 1, $"{crossed} walkers crossed in {ticks} ticks: faster than the bridge's capacity");
            }
            Assert.True(waiting.Count == 0, $"{waiting.Count} walkers still wait at a lowered bridge {ticks} ticks after it came down ({queued} queued)");
            learned.Add(x.AvgWait);
            AssertRiverInvariants(w, $"cycle {cycle}");
            for (int t = 0; t < 100; t++) w.Tick();
        }
        Assert.True(maxQueue >= 2, $"queue never formed ({maxQueue})");
        // The learned wait reflects the raises: once a raise's queue has gone over it is above 0.5 s every time. (Restated in
        // river pass 3: this was the learned wait at the very end, 100 ticks after the last queue cleared. The learned wait
        // is a moving average over the last walkers served, and once the bridge is down those are walkers who queued a tick
        // or two for the bridge's capacity, so its value then depended on how many of those there happened to be. With
        // walkers no longer let onto the deck free past the gate (step 1) more of them queue briefly, and on the round-2
        // trail model it ended at 0.28 s although every raise's queue had taught it a wait of seconds.)
        Assert.True(learned.Min() > 0.5, $"learned wait after each raise {string.Join(", ", learned.Select(v => v.ToString("F2")))}; at the end {x.AvgWait:F2}, served {x.WalkersServed}, mean {w.River!.Stats.MeanWalkerWait}, worst {w.River.Stats.WalkerWaitMax}");
        // 1 s up, 8 s held, 1 s down at most, then the queue's turn at the bridge's capacity.
        double drain = 2 * maxQueue / (w.River!.Rules.DrawbridgeWalkersPerDay / w.Params.DaySimSec);
        Assert.True(w.River!.Stats.WalkerWaitMax <= 10.2 + drain, $"worst wait {w.River.Stats.WalkerWaitMax}");
    }

    [Fact]
    public void ErasingAFerryWithAQueueReleasesEveryoneAndLandsTheRiders()
    {
        var w = Make(2, 300, Rich with { HaulGoods = true });
        var x = Build(w, CrossingType.Ferry);
        int waiting = 0, riding = 0;
        for (int t = 0; t < 3000 && !(waiting >= 2 && riding >= 1); t++)
        {
            w.Tick();
            waiting = 0; riding = 0;
            foreach (var k in w.Walkers)
                if (k.HoldAt == x.Id) { if (k.Hold == WalkerHold.Waiting) waiting++; else if (k.Hold == WalkerHold.Riding) riding++; }
        }
        Assert.True(waiting >= 2 && riding >= 1, $"waiting {waiting}, riding {riding}");
        double money = w.River!.Money;
        Assert.Equal(1, w.Erase(x.Cells.ToArray()));
        Assert.Equal(money - 20, w.River.Money, 9); // half the 40c price, nothing refunded
        Assert.DoesNotContain(w.River.Crossings, c => c.Id == x.Id);
        foreach (var k in w.Walkers) Assert.Equal(WalkerHold.None, k.Hold);
        AssertRiverInvariants(w, "just erased");
        Assert.True(double.IsPositiveInfinity(w.CellCost(x.Cells[0])));
        for (int t = 0; t < 600; t++)
        {
            w.Tick();
            if (t % 50 == 0) AssertRiverInvariants(w, $"after erase, tick {t}");
        }
        foreach (var k in w.Walkers) Assert.Equal(WalkerHold.None, k.Hold);
        Assert.True(w.River.Stats.Trips > 0);
    }

    [Fact]
    public void ErasingARaisedDrawbridgeReleasesItsQueue()
    {
        var w = Make(2, 300, Rich with { HaulGoods = true });
        var x = Build(w, CrossingType.Drawbridge);
        ToyHarness.Run(w, 1);
        w.SetBridge(x.Id, true);
        int q = 0;
        for (int t = 0; t < 90 && q < 2; t++) { w.Tick(); q = x.QueueWest + x.QueueEast; }
        Assert.True(q >= 1);
        Assert.Equal(1, w.Erase(x.Cells.ToArray()));
        Assert.Empty(w.Bridges);
        foreach (var k in w.Walkers) Assert.Equal(WalkerHold.None, k.Hold);
        for (int t = 0; t < 300; t++) w.Tick();
        AssertRiverInvariants(w, "after erasing a raised drawbridge");
    }

    [Fact]
    public void NobodyIsStrandedThroughDaysOfRandomRaisingAndAWalkerCountChange()
    {
        var w = Make(5, 300, RiverRules.Default with { StartMoney = 2000 });
        var f = Build(w, CrossingType.Ferry);
        var d = Build(w, CrossingType.Drawbridge, 9);
        uint s = 12345;
        for (int t = 0; t < 6 * 150; t++)
        {
            s = s * 1664525 + 1013904223;
            if (s % 97 == 0) w.SetBridge(d.Id, (s >> 8) % 2 == 0);
            if (t == 300) w.SetWalkerCount(120);
            if (t == 600) w.SetWalkerCount(400);
            w.Tick();
            foreach (var k in w.Walkers) Assert.True(k.WaitT < w.Params.River!.MaxWalkerWaitSec, $"walker {k.Id} waited {k.WaitT} s");
            if (t % 30 == 0) AssertRiverInvariants(w, $"tick {t}");
        }
        Assert.True(f.WalkersServed > 0 && d.WalkersServed > 0);
    }
}

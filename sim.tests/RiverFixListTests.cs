using TideToll.Sim.Testing;
using static TideToll.Sim.Tests.RiverBoatTests;
using static TideToll.Sim.Tests.RiverCrossingTests;

namespace TideToll.Sim.Tests;

/// <summary>
/// The bug hunt's findings fixed before the playtest (25 Nov 2026): grain dropped by a walker cut off from the landing
/// is counted as lost; a pass from a bridge queue does not outlive a walker sent home; a walker on a rising leaf is set
/// down on the bank it came from.
/// </summary>
public class RiverFixListTests
{
    private static readonly RiverRules Calm = Rich with { HaulGoods = true, SpringFlood = false, WinterIce = false };

    [Fact]
    public void GrainCarriedHomeByAWalkerCutOffFromTheLandingIsCountedAsLost()
    {
        var w = Make(2, 300, Calm);
        var r = w.River!;
        Build(w, CrossingType.Footbridge, 8);
        int picked = 0, cutOff = 0;
        var cargo = new Dictionary<int, (byte Cargo, WalkState State)>();
        void Step()
        {
            w.Tick();
            foreach (var k in w.Walkers)
            {
                var prev = cargo.GetValueOrDefault(k.Id);
                if (prev.Cargo == 0 && k.Cargo != 0) picked++;
                if (prev.Cargo != 0 && k.Cargo != 0 && prev.State == WalkState.Out && k.State == WalkState.Back) cutOff++;
                cargo[k.Id] = (k.Cargo, k.State);
            }
        }
        for (int t = 0; t < 5 * w.Params.TicksPerDay; t++) Step();
        Assert.True(r.Stats.GrainDelivered > 0, "no grain reached the landing");
        // Fence the landing in: every fenceable cell two rings round its door.
        int n = w.Grid.Width, door = r.LandingCell, dx0 = door % n, dy0 = door / n;
        var ring = new List<int>();
        for (int y = dy0 - 2; y <= dy0 + 2; y++)
            for (int x = dx0 - 2; x <= dx0 + 2; x++)
                if (Math.Max(Math.Abs(x - dx0), Math.Abs(y - dy0)) == 2 && x >= 0 && y >= 0 && x < n && y < w.Grid.Height) ring.Add(y * n + x);
        Assert.True(w.PlaceFence(ring.ToArray()) > 0);
        for (int t = 0; t < 5 * w.Params.TicksPerDay; t++) Step();
        Assert.True(cutOff > 0, "no walker carrying grain was cut off from the landing");
        int carried = 0;
        foreach (var k in w.Walkers) carried += k.Cargo;
        // Every unit picked up was delivered, lost on the way, or is still carried.
        Assert.Equal(picked, r.Stats.GrainDelivered + r.Stats.GrainLost + carried);
    }

    [Fact]
    public void APassFromABridgeQueueIsSpentWhenItsWalkerIsSentHome()
    {
        var w = Make(2, 300, Calm);
        var x = Build(w, CrossingType.Drawbridge);
        ToyHarness.Run(w, 1);
        int found = -1;
        for (int cycle = 0; cycle < 8 && found < 0; cycle++)
        {
            w.SetBridge(x.Id, true);
            while (x.State != BridgeState.Closed) w.Tick();
            // Once down, the queue is let on: a walker released from it holds a pass until it steps on the deck.
            for (int t = 0; t < 100 && found < 0; t++)
            {
                w.Tick();
                var ws = w.Walkers;
                for (int i = 0; i < ws.Length; i++)
                    if (ws[i].Visible && ws[i].Hold == WalkerHold.None && ws[i].Pass && ws[i].HoldAt == x.Id && !x.Cells.Contains(ws[i].Cell)) { found = i; break; }
            }
        }
        Assert.True(found >= 0, "no walker was released from the queue with a pass");
        w.SendHomeForTests(found);
        var k = w.Walkers[found];
        Assert.Equal(WalkState.Home, k.State);
        Assert.Equal(WalkerHold.None, k.Hold);
        Assert.False(k.Pass, "the walker kept its pass after being sent home: it could later step on that bridge free");
        Assert.NotEqual(x.Id, (int)k.HoldAt);
        RiverWalkerTests.AssertRiverInvariants(w, "after sending the walker home");
    }

    [Fact]
    public void AWalkerOnARisingLeafIsSetDownOnTheBankItCameFrom()
    {
        var w = Make(2, 300, Calm);
        var x = Build(w, CrossingType.Drawbridge);
        ToyHarness.Run(w, 1);
        int setDown = 0;
        for (int round = 0; round < 40 && setDown < 4; round++)
        {
            // A walker on the span when the raise begins.
            int id = -1;
            sbyte side = 0;
            for (int t = 0; t < 600 && id < 0; t++)
            {
                w.Tick();
                foreach (var k in w.Walkers)
                    if (k.Visible && k.Hold == WalkerHold.None && x.Cells.Contains(k.Cell) && k.Side != 0)
                    { id = k.Id; side = k.Side; break; }
            }
            Assert.True(id >= 0, "nobody used the drawbridge");
            w.SetBridge(x.Id, true);
            int last = -1;
            while (x.State != BridgeState.Raised)
            {
                foreach (var k in w.Walkers) if (k.Id == id) last = k.Cell;
                w.Tick();
            }
            Walker after = default;
            foreach (var k in w.Walkers) if (k.Id == id) after = k;
            if (x.Cells.Contains(last) && after.Visible)
            {
                // It was still on the span when the leaf finished rising: set down at the end of its own bank, no crossing counted.
                setDown++;
                Assert.Equal(side < 0 ? x.WestEnd : x.EastEnd, after.Cell);
                Assert.Equal(side, after.Side);
                Assert.Equal(side, (sbyte)w.Map.Bank[after.Cell]);
            }
            RiverWalkerTests.AssertRiverInvariants(w, "just raised");
            while (x.State != BridgeState.Closed) w.Tick();
        }
        Assert.True(setDown >= 2, $"only {setDown} walkers were still on the span when it finished rising");
    }
}

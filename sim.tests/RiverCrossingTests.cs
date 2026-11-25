using TideToll.Sim.Testing;
using static TideToll.Sim.Tests.RiverBoatTests;

namespace TideToll.Sim.Tests;

/// <summary>
/// Drawbridge (manual raise and lower with the spec's timings, boats one at a time, the keeper), the ferry's
/// cycle and capacity, and the routing cost of every crossing including the learned wait.
/// </summary>
public class RiverCrossingTests
{
    internal static readonly RiverRules Rich = NoSchedule with { StartMoney = 2000 };

    private static List<RiverEventKind> Kinds(World w, int crossing) => Drain(w).Where(e => e.Crossing == crossing).Select(e => e.Kind).ToList();

    [Fact]
    public void ManualRaiseTakesOneSecondHoldsEightWithoutBoatsAndLowersInOne()
    {
        var w = Make(1, 0, Rich);
        var x = Build(w, CrossingType.Drawbridge);
        Drain(w);
        Assert.Equal(BridgeState.Closed, x.State);
        Assert.Equal(1, x.Clearance);
        Assert.True(w.SetBridge(x.Id, true).Ok);
        Assert.Equal(BridgeState.Raising, x.State);
        for (int i = 0; i < 9; i++) w.Tick();
        Assert.Equal(BridgeState.Raising, x.State);
        Assert.Equal(0.9, x.Progress, 6);
        w.Tick(); // 1.0 s
        Assert.Equal(BridgeState.Raised, x.State);
        Assert.Equal(255, x.Clearance);
        for (int i = 0; i < 79; i++) w.Tick();
        Assert.Equal(BridgeState.Raised, x.State);
        w.Tick(); // 8 s raised with no boat
        Assert.Equal(BridgeState.Lowering, x.State);
        for (int i = 0; i < 10; i++) w.Tick();
        Assert.Equal(BridgeState.Closed, x.State);
        Assert.Equal(new[] { RiverEventKind.BridgeRaising, RiverEventKind.BridgeRaised, RiverEventKind.BridgeLowering, RiverEventKind.BridgeLowered }, Kinds(w, x.Id));
        // Lowering by hand at once, and raising again from half-way down keeps the leaf where it is.
        w.SetBridge(x.Id, true);
        for (int i = 0; i < 10; i++) w.Tick();
        Assert.True(w.SetBridge(x.Id, false).Ok);
        for (int i = 0; i < 5; i++) w.Tick();
        Assert.Equal(0.5, x.Progress, 6);
        w.SetBridge(x.Id, true);
        Assert.Equal(BridgeState.Raising, x.State);
        w.Tick();
        Assert.Equal(0.6, x.Progress, 6);
    }

    [Fact]
    public void BargesWaitAtAClosedDrawbridgePassOneEveryOneAndAHalfSecondsAndItLowersBehindThem()
    {
        var w = Make(1, 0, Rich);
        var r = w.River!;
        var x = Build(w, CrossingType.Drawbridge);
        int a = w.SpawnBoat(BoatKind.Barge).Id;
        for (int i = 0; i < 3; i++) w.Tick();
        int b = w.SpawnBoat(BoatKind.Barge).Id;
        for (int i = 0; i < 3; i++) w.Tick();
        int row = w.SpawnBoat(BoatKind.Rowboat).Id;
        while (BoatById(w, b).State != BoatState.Waiting) w.Tick();
        for (int i = 0; i < 20; i++) w.Tick();
        Assert.Equal(BoatState.Waiting, BoatById(w, a).State);
        Assert.Equal(x.Id, BoatById(w, a).WaitingAt);
        Assert.Equal(2, x.BoatsWaiting);
        Drain(w);
        w.SetBridge(x.Id, true);
        var passed = new List<long>();
        while (x.State != BridgeState.Closed || passed.Count < 2)
        {
            w.Tick();
            foreach (var e in Drain(w)) if (e.Kind == RiverEventKind.BoatPassed) passed.Add(e.Tick);
            Assert.True(w.Ticks < 100000);
        }
        Assert.Equal(2, passed.Count);
        Assert.Equal(15, passed[1] - passed[0]); // 1.5 s per boat
        Assert.Equal(0, r.Stats.TurnedBack.Sum());
        Assert.Equal(2, x.BoatsPassed);
        // The rowboat (class 0) never needed the bridge.
        Assert.True(BoatById(w, row).State is BoatState.Mooring or BoatState.Leaving || !Exists(w, row));
    }

    [Fact]
    public void KeeperRaisesOneSecondAfterABargeIsWithinSixRowsAndLowersOnceItIsThrough()
    {
        var w = Make(1, 0, Rich);
        var x = Build(w, CrossingType.Drawbridge);
        Assert.True(w.HireKeeper(x.Id).Ok);
        Assert.True(x.Keeper);
        Assert.False(w.HireKeeper(x.Id).Ok);
        int barge = w.SpawnBoat(BoatKind.Barge).Id;
        long within = -1, raising = -1;
        while (Exists(w, barge) && BoatById(w, barge).State != BoatState.Mooring)
        {
            w.Tick();
            if (within < 0 && BoatById(w, barge).Y - (x.Row + 0.5) <= 6) within = w.Ticks;
            if (raising < 0 && x.State == BridgeState.Raising) raising = w.Ticks;
            Assert.NotEqual(BoatState.TurnedBack, BoatById(w, barge).State);
        }
        Assert.Equal(10, raising - within); // 1 s
        for (int i = 0; i < 40; i++) w.Tick();
        Assert.Equal(BridgeState.Closed, x.State);
        // No boat, no raise.
        for (int i = 0; i < 100; i++) { w.Tick(); Assert.Equal(BridgeState.Closed, x.State); }
    }

    [Fact]
    public void FerryCarriesSixAtMostCrossesInOneSecondAndWalkersComeOffOnTheFarStage()
    {
        var w = Make(2, 300, Rich with { HaulGoods = true });
        var x = Build(w, CrossingType.Ferry);
        Drain(w);
        var departs = new List<RiverEvent>();
        var lands = new List<RiverEvent>();
        var prevWait = new Dictionary<int, float>();
        int fcfsChecks = 0;
        for (int t = 0; t < 4 * 150; t++)
        {
            w.Tick();
            Assert.True(x.Passengers <= 6);
            var ws = w.Walkers;
            // First come, first served: whoever boarded this tick had waited at least as long as anyone left behind on that bank.
            var boarded = new List<float>();
            var left = new List<(float Wait, int Side)>();
            int boardSide = 0;
            foreach (var k in ws)
            {
                if (k.Hold == WalkerHold.Riding && k.HoldAt == x.Id && prevWait.TryGetValue(k.Id, out float pw)) { boarded.Add(pw); boardSide = w.Map.Bank[k.Cell]; }
                if (k.Hold == WalkerHold.Waiting && k.HoldAt == x.Id) left.Add((k.WaitT, w.Map.Bank[k.Cell]));
            }
            if (boarded.Count > 0)
            {
                float minBoarded = boarded.Min();
                foreach (var (wait, side) in left) if (side == boardSide) Assert.True(wait - 0.1f <= minBoarded + 1e-3f, $"a walker waiting {wait} s was left while one waiting {minBoarded} s boarded");
                fcfsChecks++;
            }
            prevWait.Clear();
            foreach (var k in ws) if (k.Hold == WalkerHold.Waiting && k.HoldAt == x.Id) prevWait[k.Id] = k.WaitT;
            foreach (var e in Drain(w))
            {
                if (e.Kind == RiverEventKind.FerryDeparted) departs.Add(e);
                if (e.Kind == RiverEventKind.FerryLanded) lands.Add(e);
            }
            foreach (var k in ws)
                if (k.Hold != WalkerHold.Riding && k.Visible) Assert.False(w.IsBlocked(k.Cell), $"walker {k.Id} on a blocked cell");
        }
        Assert.True(departs.Count > 10, $"{departs.Count} departures");
        Assert.True(fcfsChecks > 5);
        Assert.All(departs, e => Assert.InRange(e.Value, 0, 6));
        Assert.Contains(departs, e => e.Value > 0);
        // Every leg takes 1.0 s.
        for (int i = 0; i < lands.Count; i++) Assert.Equal(10, lands[i].Tick - departs[i].Tick);
        Assert.True(x.WalkersServed > 20);
        Assert.True(x.AvgWait > 0);
        Assert.True(x.Clearance >= 255);
    }

    [Fact]
    public void RoutingCostIncludesTheExpectedAndLearnedWait()
    {
        var w = Make(2, 300, Rich with { HaulGoods = true });
        var f = Build(w, CrossingType.Ferry);
        var d = Build(w, CrossingType.Drawbridge, 9);
        var fb = Build(w, CrossingType.Footbridge, 14);
        // A new crossing has learned nothing yet.
        Assert.Equal(3 * (0.75 + 1.0) / f.Cells.Count, w.CellCost(f.Cells[0]), 9);
        Assert.Equal(0.8, w.CellCost(d.Cells[0]), 9);
        Assert.Equal(0.8, w.CellCost(fb.Cells[0]), 9);
        // Keep the drawbridge up for a while so walkers learn to wait there.
        for (int t = 0; t < 900; t++)
        {
            if (t % 120 == 0) w.SetBridge(d.Id, true);
            w.Tick();
        }
        Assert.True(f.AvgWait > 0 && d.AvgWait > 0, $"ferry {f.AvgWait}, drawbridge {d.AvgWait}");
        Assert.Equal(3 * (0.75 + 1.0 + f.AvgWait) / f.Cells.Count, w.CellCost(f.Cells[0]), 9);
        Assert.Equal(0.8 + 3 * d.AvgWait / d.Cells.Count, w.CellCost(d.Cells[0]), 9);
        // A footbridge learns no wait: its deck cost is the bridge cost with the usual crowding term, and (SimParams.BridgeFlow,
        // the finished toy's trail model) the traffic discount any lane gets, floored at MinSurface.
        Assert.Equal(0.8 * DeckFlow(w, fb.Cells[0]) * Crowding(w, fb.Cells[0]), w.CellCost(fb.Cells[0]), 9);
        // Overrides used by the flow preview still win.
        Assert.True(double.IsPositiveInfinity(w.CellCost(f.Cells[0], CostOverride.Block)));
        Assert.True(double.IsPositiveInfinity(w.CellCost(f.Cells[0], CostOverride.CloseBridge)));
    }

    /// <summary>A bridge deck's traffic discount (1 when SimParams.BridgeFlow is 0): the flow table's value, floored at MinSurface.</summary>
    internal static double DeckFlow(World w, int c)
    {
        var p = w.Params;
        if (p.BridgeFlow <= 0) return 1;
        var lut = w.FlowLookup;
        return Math.Max(p.MinSurface, lut[Math.Min(lut.Length - 1, (int)(w.Flow[c] * 4.0))]);
    }

    /// <summary>The crowding multiplier CellCost applies to a cell (1 when it is nearly empty).</summary>
    internal static double Crowding(World w, int c)
    {
        var p = w.Params;
        double rho = w.Occupancy[c] / p.CellAreaM2;
        if (rho <= 0.05) return 1;
        double v = Math.Max(1 / p.CrowdMaxCostMul, 1 - Math.Exp(-p.WeidmannGamma * (1 / rho - 1 / p.WeidmannRhoMax)));
        return 1 + p.CrowdStrength * (1 / Math.Min(1, v) - 1);
    }
}

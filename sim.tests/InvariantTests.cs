using TideToll.Sim.Testing;
using static TideToll.Sim.Testing.ToyHarness;

namespace TideToll.Sim.Tests;

/// <summary>
/// Rules that must never break, checked along a run with every kind of edit: wear stays within 0 to 100,
/// no walker stands on a blocked cell, costs and fields hold no NaN, and walkers are conserved.
/// Plus the hot-path rule: a tick allocates nothing.
/// </summary>
public class InvariantTests
{
    private static void AssertInvariants(World w, int expectedWalkers, string when)
    {
        foreach (float v in w.Wear) Assert.True(v >= 0 && v <= 100, $"{when}: wear {v} out of 0..100");
        foreach (float v in w.Cost) Assert.False(float.IsNaN(v) || v < 0, $"{when}: cost {v}");
        foreach (float v in w.Flow) Assert.False(float.IsNaN(v) || v < 0, $"{when}: flow {v}");
        for (int t = 0; t < w.Buildings.Length; t++)
            foreach (float v in w.Field(t)) Assert.False(float.IsNaN(v) || v < 0, $"{when}: field {t} value {v}");
        Assert.Equal(expectedWalkers, w.WalkerCount);
        var ids = new HashSet<int>();
        int visible = 0;
        foreach (ref readonly var k in w.Walkers)
        {
            Assert.True(ids.Add(k.Id), $"{when}: duplicate walker id {k.Id}");
            Assert.InRange(k.Cell, 0, w.Grid.Count - 1);
            Assert.False(double.IsNaN(k.X) || double.IsNaN(k.Y), $"{when}: walker {k.Id} position is NaN");
            if (!k.Visible) continue;
            visible++;
            Assert.False(w.IsBlocked(k.Cell), $"{when}: walker {k.Id} stands on blocked cell {k.Cell}");
            Assert.Equal(k.Cell, (int)Math.Floor(k.Y) * w.Grid.Width + (int)Math.Floor(k.X));
        }
        int onCells = 0;
        foreach (ushort h in w.WalkersOnCell) onCells += h;
        Assert.True(onCells == visible, $"{when}: {onCells} walkers counted on cells but {visible} are walking");
        Assert.Equal(visible, w.WalkersOut());
    }

    [Fact]
    public void InvariantsHoldThroughEditsAndWalkerChanges()
    {
        // The trail model without house growth: this test holds the walker count to what it sets (growth adds villagers).
        var w = new World(7, 300, SimParams.Toy);
        int n = w.Grid.Width;
        void Ticks(int count, int walkers, string phase)
        {
            for (int i = 0; i < count; i++)
            {
                w.Tick();
                if (i % 25 == 0) AssertInvariants(w, walkers, $"{phase}, tick {w.Ticks}");
            }
            AssertInvariants(w, walkers, $"{phase}, end");
        }
        Ticks(450, 300, "start");
        // A fence right across the town, through walkers, then a bridge and an erase.
        Assert.True(w.PlaceFence(Line4(w, 10, 60, 117, 70).ToArray()) > 50);
        AssertInvariants(w, 300, "just after fencing");
        Ticks(300, 300, "fenced");
        Bridge? b = null;
        for (int y = 10; y < n - 10 && b == null; y++) if (Math.Abs(y - w.Map.FordY) > 8) b = w.PlaceBridge(w.RiverX[y], y);
        Assert.NotNull(b);
        Ticks(300, 300, "bridged");
        w.SetWalkerCount(500);
        Ticks(300, 500, "500 walkers");
        w.Erase(b!.Cells.ToArray());
        Assert.Empty(w.Bridges);
        w.SetWalkerCount(50);
        Ticks(300, 50, "50 walkers, bridge erased");
        w.SetWalkerCount(9999);
        Assert.Equal(w.Params.MaxWalkers, w.WalkerCount);
        w.SetWalkerCount(-3);
        AssertInvariants(w, 0, "no walkers");
    }

    [Fact]
    public void TickAndStatsAllocateNothing()
    {
        // Warm-up runs every path once (ticks, Stats, a walker count change), so one-time JIT and runtime set-up is
        // not counted. Stats() must allocate nothing even on its first call: Metrics creates the runtime's shared
        // int sort helper (the 24 bytes this test used to report when run alone) in its constructor.
        var w = new World(3, 500);
        long s0 = GC.GetAllocatedBytesForCurrentThread();
        w.Stats();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - s0);
        for (int i = 0; i < 300; i++) w.Tick();
        w.SetWalkerCount(200);
        w.SetWalkerCount(500);
        w.Tick();
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 300; i++) w.Tick(); // includes field rebuilds, decay sweeps and the first-trail check
        var s = w.Stats();
        w.SetWalkerCount(200);
        w.SetWalkerCount(500);
        w.Tick();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - a0;
        Assert.Equal(0, allocated);
        Assert.Equal(500, s.Walkers);
    }

    /// <summary>The finished toy's trail model (two views, detour cap, explorers) with gravel and gates down: still allocation-free.</summary>
    [Fact]
    public void TickOnTheToyPresetWithGravelAndGatesAllocatesNothing()
    {
        var w = new World(3, 500, SimParams.Toy);
        for (int i = 0; i < 300; i++) w.Tick();
        var line = new List<int>();
        w.Grid.Line4(20, 40, 40, 60, line);
        w.PlaceGravel(line.ToArray());
        line.Clear();
        w.Grid.Line4(60, 20, 60, 40, line);
        w.PlaceFence(line.ToArray());
        w.PlaceGate(line[line.Count / 2]);
        for (int i = 0; i < 300; i++) w.Tick();
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 300; i++) w.Tick();
        w.Stats();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - a0);
    }

    [Fact]
    public void SteppedPreviewEqualsComputedPreviewAndDoesNotTouchTheWorld()
    {
        var w = new World(4, 200);
        Run(w, 6);
        int[] fence = FenceAcrossRoute(w, w.Houses[0], 0.3);
        uint wear = Fnv(w.Wear), cost = Fnv(w.Cost), rng = w.RngState;
        var p1 = new FlowPredictor(w);
        p1.Begin(new WhatIf { Block = fence });
        int steps = 0;
        while (p1.Step()) steps++;
        var p2 = new FlowPredictor(w).Compute(new WhatIf { Block = fence });
        Assert.True(steps >= w.OfKind(BuildingKind.Well).Length + 2, $"only {steps} steps");
        Assert.Equal(FlowPredictor.Describe(p2, b => b.ToString()), FlowPredictor.Describe(p1.Result!, b => b.ToString()));
        Assert.Equal(p2.ReroutedPerDay, p1.Result!.ReroutedPerDay);
        Assert.Equal((wear, cost, rng), (Fnv(w.Wear), Fnv(w.Cost), w.RngState));
    }

    [Fact]
    public void GroundFollowsTheToyThresholds()
    {
        var p = SimParams.Default;
        Assert.Equal(Ground.Grass, p.GroundOf(9.99, true));
        Assert.Equal(Ground.Trampled, p.GroundOf(10, false));
        Assert.Equal(Ground.Trail, p.GroundOf(49.9, true)); // hollow only counts from the track threshold up
        Assert.Equal(Ground.Track, p.GroundOf(50, false));
        Assert.Equal(Ground.Hollow, p.GroundOf(50, true));
    }

    [Fact]
    public void JsMathMatchesJavaScript()
    {
        Assert.Equal(5.0, JsMath.Hypot(3, 4));
        Assert.Equal(0.0, JsMath.Hypot(0, -0.0));
        Assert.Equal(3.0, JsMath.Round(2.5));
        Assert.Equal(-2.0, JsMath.Round(-2.5));
        Assert.Equal(-3.0, JsMath.Round(-2.6));
    }
}

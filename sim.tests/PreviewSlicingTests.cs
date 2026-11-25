using TideToll.Sim.Testing;
using static TideToll.Sim.Testing.ToyHarness;

namespace TideToll.Sim.Tests;

/// <summary>
/// The flow preview in bounded slices (FlowPredictor.Step(maxWork)) gives exactly the result of the unbounded
/// path, on 10 seeds, for a fence, a footbridge and (with the river rules) a ferry, whatever the slice size.
/// </summary>
public class PreviewSlicingTests
{
    internal static void AssertSame(FlowPreview a, FlowPreview b, string what)
    {
        Assert.True(a.More == b.More, what + ": More");
        Assert.True(a.ReroutedPerDay == b.ReroutedPerDay, what + ": ReroutedPerDay");
        Assert.True(a.BoatsBlockedPerDay == b.BoatsBlockedPerDay && a.BoatsNeedRaisePerDay == b.BoatsNeedRaisePerDay, what + ": boats");
        Assert.Equal(a.Blocked, b.Blocked);
        Assert.Equal(a.Changes.Count, b.Changes.Count);
        for (int i = 0; i < a.Changes.Count; i++)
        {
            RouteChange x = a.Changes[i], y = b.Changes[i];
            Assert.True(x.Home == y.Home && x.Dest == y.Dest && x.Rate == y.Rate && x.Reachable == y.Reachable && x.CostRatio.Equals(y.CostRatio), $"{what}: change {i}");
            Assert.True(x.OldPts.SequenceEqual(y.OldPts) && x.NewPts.SequenceEqual(y.NewPts), $"{what}: route points of change {i}");
        }
    }

    private static FlowPreview Sliced(World w, in WhatIf change, int budget, out int calls)
    {
        var p = new FlowPredictor(w);
        p.Begin(change);
        calls = 1;
        while (p.Step(budget)) calls++;
        return p.Result!;
    }

    [Fact]
    public void SlicedPreviewIsBitIdenticalToTheUnboundedOneOnTenSeeds()
    {
        for (int seed = 1; seed <= 10; seed++)
        {
            bool river = seed % 3 == 0;
            var w = river ? new World(seed, 200, SimParams.Default with { River = RiverRules.Default with { StartMoney = 1000 } }) : new World(seed, 200);
            Run(w, 6);
            var changes = new List<(string, WhatIf)> { ("fence", new WhatIf { Block = FenceAcrossRoute(w, w.Houses[0], 0.3) }) };
            for (int y = 20; y < 110; y += 7)
            {
                if (river)
                {
                    var c = w.CrossingWhatIf(CrossingType.Ferry, w.RiverX[y], y + 0.5);
                    if (c != null) { changes.Add(("ferry", c.Value)); break; }
                }
                else if (Math.Abs(y - w.Map.FordY) > 6 && w.BridgeCellsAt(w.RiverX[y], y) is { } cells)
                {
                    changes.Add(("bridge", new WhatIf { Bridge = cells }));
                    break;
                }
            }
            Assert.Equal(2, changes.Count);
            uint wear = Fnv(w.Wear), rng = w.RngState;
            foreach (var (name, change) in changes)
            {
                var whole = new FlowPredictor(w).Compute(change);
                var stepped = new FlowPredictor(w);
                stepped.Begin(change);
                while (stepped.Step()) { }
                AssertSame(whole, stepped.Result!, $"seed {seed} {name} Step()");
                foreach (int budget in new[] { 1, 97, 1000, 1 << 20 })
                {
                    var r = Sliced(w, change, budget, out int calls);
                    AssertSame(whole, r, $"seed {seed} {name} budget {budget}");
                    if (budget == 1000) Assert.True(calls > 10, $"seed {seed}: only {calls} slices of 1000 units");
                }
            }
            Assert.Equal((wear, rng), (Fnv(w.Wear), w.RngState));
        }
    }

    [Fact]
    public void ASliceOfAThousandUnitsIsShort()
    {
        // Thread CPU time, so a busy machine does not count; the worst of three previews' slices must stay under 2 ms.
        var w = new World(2, 200);
        Run(w, 20);
        var change = new WhatIf { Block = FenceAcrossRoute(w, w.Houses[0], 0.3) };
        double best = double.PositiveInfinity;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var p = new FlowPredictor(w);
            p.Begin(change);
            double worst = 0;
            bool more = true;
            while (more)
            {
                double t0 = CpuClock.NowMs();
                more = p.Step(1000);
                worst = Math.Max(worst, CpuClock.NowMs() - t0);
            }
            best = Math.Min(best, worst);
        }
        Console.WriteLine($"PREVIEW SLICE worst 1000-unit slice {best:F2} ms ({(CpuClock.IsCpuTime ? "thread CPU time" : "wall time")})");
        if (TickBench.IsRelease) Assert.True(best < 2, $"worst slice {best:F2} ms");
    }
}

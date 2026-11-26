using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>River polish: smooth lanes, the cached river hint, boats' cargo in loads.</summary>
public class TownRiverPolishTests
{
    [Fact]
    public void LanesAreSmoothCurvesWithFixedEnds()
    {
        for (int seed = 1; seed <= 30; seed++)
        {
            var rp = TownRiverPlanner.Build(TownPlanner.Build(seed));
            int smooth = 0;
            foreach (var l in rp.Links)
            {
                Assert.Equal(rp.Px[l.A], l.Px[0], 9); Assert.Equal(rp.Py[l.A], l.Py[0], 9);
                Assert.Equal(rp.Px[l.B], l.Px[^1], 9); Assert.Equal(rp.Py[l.B], l.Py[^1], 9);
                Assert.Same(l.Cum, l.PCum);
                Assert.Equal(l.Cum[^1], l.PCum[^1]);
                Assert.True(l.Xs.Length >= 2 && l.Xs.Length == l.Ys.Length);
                if (l.Kind != RiverLinkKind.Lane) { Assert.Equal(2, l.Px.Length); continue; }
                // A curve: no turn sharper than 60 degrees between two curve points (a grid corner is 90).
                for (int i = 1; i + 1 < l.Px.Length; i++)
                {
                    double a1 = Math.Atan2(l.Py[i] - l.Py[i - 1], l.Px[i] - l.Px[i - 1]), a2 = Math.Atan2(l.Py[i + 1] - l.Py[i], l.Px[i + 1] - l.Px[i]);
                    double turn = Math.Abs(Math.IEEERemainder(a2 - a1, 2 * Math.PI));
                    if (turn > 0.01 && turn < Math.PI / 3) smooth++;
                }
                // At follows the curve and its ends.
                l.At(0, out double x0, out double y0, out _); Assert.Equal(l.Px[0], x0, 9); Assert.Equal(l.Py[0], y0, 9);
                l.At(l.Cum[^1], out double x1, out double y1, out _); Assert.Equal(l.Px[^1], x1, 9); Assert.Equal(l.Py[^1], y1, 9);
            }
            Assert.True(smooth > 0, $"seed {seed}: no lane bends gently");
        }
    }

    [Fact]
    public void LanesBendNoSharperThanTheFirstAgesRoads()
    {
        // Every lane turn between two curve points is under 90 degrees (the grid's square corners are gone).
        for (int seed = 1; seed <= 30; seed++)
        {
            var rp = TownRiverPlanner.Build(TownPlanner.Build(seed));
            foreach (var l in rp.Links)
            {
                if (l.Kind != RiverLinkKind.Lane) continue;
                for (int i = 1; i + 1 < l.Px.Length; i++)
                {
                    double a1 = Math.Atan2(l.Py[i] - l.Py[i - 1], l.Px[i] - l.Px[i - 1]), a2 = Math.Atan2(l.Py[i + 1] - l.Py[i], l.Px[i + 1] - l.Px[i]);
                    double turn = Math.Abs(Math.IEEERemainder(a2 - a1, 2 * Math.PI));
                    Assert.True(turn < Math.PI / 2 - 0.01, $"seed {seed} lane {l.Id}: a turn of {turn * 180 / Math.PI:0} degrees at {l.Px[i]:0.0},{l.Py[i]:0.0}");
                }
            }
        }
    }

    [Fact]
    public void TheRiverHintAndCargoTextBuildNoStringPerRead()
    {
        var t = TownRiverTests.AtCharter();
        Assert.True(t.RingNextAge());
        for (int k = 0; k < 600; k++) t.Step();
        for (int k = 0; k < 600 && t.Boat(0).Phase == BoatPhase.Gone; k++) t.Step();
        string h1 = t.RiverHint, h2 = t.RiverHint;
        Assert.Same(h1, h2);
        _ = t.BoatCargoText(0); _ = t.BoatCargoText(1); _ = t.BoatCargoText(2);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 100; k++) { _ = t.RiverHint; _ = t.BoatCargoText(0); _ = t.BoatCargoText(1); _ = t.BoatCargoText(2); }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void CargoTextCountsLoads()
    {
        var t = TownRiverTests.AtCharter();
        Assert.True(t.RingNextAge());
        Assert.Equal("", t.BoatCargoText(-1));
        Assert.Equal("", t.BoatCargoText(99));
        for (int k = 0; k < 6000; k++)
        {
            t.Step();
            for (int i = 0; i < t.BoatCount; i++)
            {
                var b = t.Boat(i);
                string s = t.BoatCargoText(i);
                if (b.Phase == BoatPhase.Gone) { Assert.Equal("", s); continue; }
                int want = t.LoadsOf(b.Want);
                Assert.StartsWith(b.Good + " ", s);
                Assert.EndsWith(" of " + want + (want == 1 ? " load" : " loads"), s);
                Assert.True(want <= 200, $"{want} loads");
            }
        }
        Assert.Equal(0, t.LoadsOf(0));
    }
}

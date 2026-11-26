using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>Round 6 (W12): the hint speaks of a crowded road only when the road has been crowded for a while.</summary>
public class TownCrowdHintTests
{
    [Fact]
    public void CrowdingCountsOnlyWhileItLastsAndTheHintWaits()
    {
        var t = new T(1);
        int r = -1;
        for (int i = 0; i < t.Plan.Roads.Length; i++) if (t.S.RoadOpen[i]) { r = i; break; }
        Assert.True(r >= 0);
        t.S.RoadWalkers[r] += 50;   // far over any road's comfort
        for (int k = 0; k < TownRules.TickHz * (T.CrowdHintSeconds - 3); k++) t.Step();
        Assert.InRange(t.CrowdedSeconds(r), 1, T.CrowdHintSeconds - 1);
        Assert.DoesNotContain("crowded", t.Hint);
        for (int k = 0; k < TownRules.TickHz * 5; k++) t.Step();
        Assert.True(t.CrowdedSeconds(r) >= T.CrowdHintSeconds, $"crowded {t.CrowdedSeconds(r)} s");
        // A break resets it.
        t.S.RoadWalkers[r] -= 50;
        for (int k = 0; k < TownRules.TickHz * 2; k++) t.Step();
        Assert.Equal(0, t.CrowdedSeconds(r));
    }
}

using TideToll.Sim.Testing;

namespace TideToll.Sim.Tests;

/// <summary>The slow-step hunt (tools: ticks --hunt) records every step over its limit with what ran in it.</summary>
public class SlowStepHuntTests
{
    [Fact]
    public void WithNoLimitEveryStepIsRecordedWithWhatRanInIt()
    {
        var o = new SlowStepHunt.Options { Preset = "river", Walkers = 60, Days = 5, FirstSeed = 3, Towns = 1, OverMs = -1 };
        var r = SlowStepHunt.Run(o);
        int tpd = SimParams.Default.TicksPerDay;
        Assert.Equal(5L * tpd, r.Steps);
        Assert.Equal(r.Steps, r.Slow.Count);
        Assert.Equal(Enumerable.Range(1, (int)r.Steps).Select(i => (long)i), r.Slow.Select(s => s.Tick));
        Assert.Contains(r.Slow, s => s.What.Contains("route searches"));
        Assert.Contains(r.Slow, s => s.What.Contains("decay step"));
        // Day boundaries every TicksPerDay ticks, and a season boundary at the end of day 4 (SeasonSimSec 60 s, days of 15 s).
        Assert.All(r.Slow.Where(s => s.Tick % tpd == 0), s => Assert.Contains($"day {s.Tick / tpd} ends", s.What));
        Assert.Contains("SEASON BOUNDARY", r.Slow.Single(s => s.Tick == SimParams.Default.TicksPerSeason).What);
        // The first of a kind is marked once.
        Assert.Single(r.Slow, s => s.What.Contains("decay step (FIRST in this hunt)"));
        Assert.Contains("SLOW seed 3 tick", SlowStepHunt.Line(r.Slow[0], 60));
    }
}

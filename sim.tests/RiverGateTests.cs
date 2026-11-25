using TideToll.Sim.Testing;
using Xunit.Abstractions;

namespace TideToll.Sim.Tests;

/// <summary>Runs the river experiment once: S1 to S6 on seeds 101 to 120 for 12 days at 200 walkers, and S5 and S6 at 60.</summary>
public sealed class RiverRunsFixture
{
    public List<RiverRunResult> Rows { get; }

    public RiverRunsFixture()
    {
        int threads = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1));
        var seeds = Enumerable.Range(101, 20);
        Rows = RiverBot.RunAll(seeds, Enum.GetValues<RiverStrategy>(), 200, 12, threads);
        Rows.AddRange(RiverBot.RunAll(seeds, new[] { RiverStrategy.S5DrawbridgeManual, RiverStrategy.S6DrawbridgeKeeper }, 60, 12, threads));
    }
}

/// <summary>
/// Automated stand-in for the river go criteria: the crossing choices are real trade-offs (not interchangeable, none
/// dominant). Each trade-off the spec names is one test, so the report shows exactly which hold. Category=Gate.
/// </summary>
[Trait("Category", "Gate")]
public class RiverGateTests : IClassFixture<RiverRunsFixture>
{
    private readonly RiverRunsFixture _f;
    private readonly ITestOutputHelper _out;
    public RiverGateTests(RiverRunsFixture f, ITestOutputHelper output) { _f = f; _out = output; }

    private void Check(string startsWith)
    {
        var log = new CheckLog();
        RiverGateChecks.Check(_f.Rows, log);
        var sw = new StringWriter();
        RiverBot.Print(sw, _f.Rows, perSeed: false);
        _out.WriteLine(sw.ToString());
        _out.WriteLine(log.ToString());
        var c = log.Checks.Single(k => k.What.StartsWith(startsWith));
        Assert.True(c.Ok, c.What + "\n" + sw);
    }

    [Fact] public void FootbridgeIsFastestWhenTheFordIsFar() => Check("footbridge has the shortest mean trip");
    [Fact] public void FootbridgeTurnsBackBarges() => Check("footbridge turns back more than 60%");
    [Fact] public void FordTurnsBackAlmostNobody() => Check("ford only turns back fewer than 10%");
    [Fact] public void NeverRaisedDrawbridgeIsNoBetterThanAFootbridge() => Check("never-raised drawbridge earns no more");
    [Fact] public void RaisedDrawbridgesKeepBoatsComing() => Check("manual and keeper drawbridges turn back fewer than 15%");
    [Fact] public void ManualBatchingKeepsTheWorstWaitLower() => Check("manual drawbridge has the lower worst walker wait");
    [Fact] public void KeeperDoesNotPayAtLowTraffic() => Check("keeper minus manual net money is negative at 60");
    [Fact] public void KeeperPaysAtHighTraffic() => Check("keeper minus manual net money is positive at 200");
    [Fact] public void NoStrategyDominates() => Check("no strategy is best at trade income");
}

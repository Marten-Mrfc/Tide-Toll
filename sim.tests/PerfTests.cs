using TideToll.Sim.Testing;
using Xunit.Abstractions;

namespace TideToll.Sim.Tests;

/// <summary>
/// Tick budget (port plan, section 6), Release only: after 600 warm-up ticks, 600 timed ticks at 500 walkers
/// must average under 5 ms with a 99th percentile under 25 ms (field rebuilds), and allocate nothing.
/// Generous on purpose: other jobs share the machine. Run with ./check --bench or
/// dotnet test -c Release --filter Category=Perf.
/// </summary>
[Trait("Category", "Perf")]
public class PerfTests
{
    private readonly ITestOutputHelper _out;
    public PerfTests(ITestOutputHelper output) => _out = output;

    [ReleaseFact]
    public void TickAt500WalkersIsWithinBudgetAndAllocationFree()
    {
        foreach (int walkers in new[] { 200, 500 })
        {
            var r = TickBench.Measure(1, walkers);
            _out.WriteLine(TickBench.Line(r));
            Console.WriteLine(TickBench.Line(r));
            Assert.Equal(0, r.AllocatedBytes);
            if (walkers == 500)
            {
                Assert.True(r.MeanMs < 5, TickBench.Line(r));
                Assert.True(r.P99Ms < 25, TickBench.Line(r));
            }
        }
    }

    [ReleaseFact]
    public void TickAt500WalkersWithTheRiverRulesIsWithinBudgetAndAllocationFree()
    {
        var r = TickBench.Measure(1, 500, river: true);
        _out.WriteLine(TickBench.Line(r));
        Console.WriteLine(TickBench.Line(r));
        Assert.Equal(0, r.AllocatedBytes);
        Assert.True(r.MeanMs < 5, TickBench.Line(r));
        Assert.True(r.P99Ms < 25, TickBench.Line(r));
    }
}

/// <summary>A fact that is skipped in Debug builds, where timings mean nothing.</summary>
public sealed class ReleaseFactAttribute : FactAttribute
{
    public ReleaseFactAttribute() { if (!TickBench.IsRelease) Skip = "Release only: timings are meaningless in Debug"; }
}

using TideToll.Sim.Testing;

namespace TideToll.Sim.Tests;

/// <summary>
/// Long runs that use several cores: they run alone, after the parallel tests, so they never starve the
/// timing checks (the preview's 20 ms budget) of CPU.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LongRunsCollection
{
    public const string Name = "Long runs, not in parallel";
}

/// <summary>
/// The river rules are off by default. These hashes were recorded from the code before the river rules
/// existed (commit 0424d6c, 200 walkers, 20 days): a default world must keep producing exactly them.
/// </summary>
[Collection(LongRunsCollection.Name)]
public class RiverOffBaselineTests
{
    [Theory]
    [InlineData(1, 0x69fedec1u, 3678870941u)]
    [InlineData(2, 0xeee68767u, 3020060899u)]
    [InlineData(3, 0x1862407cu, 2121553375u)]
    public void DefaultWorldMatchesThePreRiverBaseline(int seed, uint wearFnv, uint rngState)
    {
        var w = new World(seed, 200);
        ToyHarness.Run(w, 20);
        Assert.Equal((wearFnv, rngState), (ToyHarness.Fnv(w.Wear), w.RngState));
    }
}

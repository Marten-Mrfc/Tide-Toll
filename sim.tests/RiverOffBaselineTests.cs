using TideToll.Sim.Testing;

namespace TideToll.Sim.Tests;

/// <summary>
/// The river rules are off by default. These hashes were recorded from the code before the river rules
/// existed (commit 0424d6c, 200 walkers, 20 days): a default world must keep producing exactly them.
/// </summary>
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

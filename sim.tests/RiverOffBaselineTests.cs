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
/// Tests that assert a time: they run alone, after the parallel tests, so the other test threads of this process (their
/// garbage collections, their share of the cores and caches) do not count against the code being timed.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingCollection
{
    public const string Name = "Timing, alone";
}

/// <summary>
/// The river rules are off by default and must stay invisible when off. The round-2 hashes were recorded from the code
/// before the river rules existed (commit 0424d6c, 200 walkers, 20 days): the round-2 model (SimParams.Legacy) must keep
/// producing exactly them. The finished toy's hashes (toy-golden-v2.json `baselines`, recorded by running the toy, never
/// from C#) hold the same for the current model, with rain and growth off (SimParams.Toy) and on (SimParams.Default): the
/// day-20 wear hash and the walkers' next three random draws.
/// </summary>
[Collection(LongRunsCollection.Name)]
public class RiverOffBaselineTests
{
    [Theory]
    [InlineData(1, 0x69fedec1u, 3678870941u)]
    [InlineData(2, 0xeee68767u, 3020060899u)]
    [InlineData(3, 0x1862407cu, 2121553375u)]
    public void TheRoundTwoModelMatchesThePreRiverBaseline(int seed, uint wearFnv, uint rngState)
    {
        var w = new World(seed, 200, SimParams.Legacy);
        ToyHarness.Run(w, 20);
        Assert.Equal((wearFnv, rngState), (ToyHarness.Fnv(w.Wear), w.RngState));
    }

    [Theory]
    [InlineData("off", 1)]
    [InlineData("off", 2)]
    [InlineData("off", 3)]
    [InlineData("live", 1)]
    [InlineData("live", 2)]
    [InlineData("live", 3)]
    public void TheCurrentModelMatchesTheFinishedToysBaseline(string family, int seed)
    {
        var g = Golden2.Root.GetProperty("baselines").EnumerateArray()
            .First(b => b.GetProperty("family").GetString() == family && b.GetProperty("seed").GetInt32() == seed);
        var p = family == "off" ? SimParams.Toy : SimParams.Default;
        var w = new World(seed, g.GetProperty("walkers").GetInt32(), p);
        ToyHarness.Run(w, g.GetProperty("days").GetInt32());
        Assert.Equal(g.GetProperty("wearFnv").GetUInt32(), ToyHarness.Fnv(w.Wear));
        // The walkers' stream: the toy's next three draws, read from a copy of the generator's state.
        var r = new Mulberry32(w.RngState);
        foreach (var v in g.GetProperty("next").EnumerateArray()) Assert.Equal(v.GetDouble(), r.NextDouble());
    }

    [Fact]
    public void TheDefaultIsTheFinishedToyWithRainAndGrowth()
    {
        Assert.Equal(SimParams.Toy with { RainOn = true, GrowthOn = true }, SimParams.Default);
        Assert.True(SimParams.Default.RainOn && SimParams.Default.GrowthOn && SimParams.Default.PreviewV2);
        Assert.Equal(SimParams.Default.WearPerStep, new SimParams().WearPerStep);
    }
}

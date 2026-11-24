namespace TideToll.Sim.Tests;

// Golden values come from the toy's own rng.ts, run by sim.tests/golden/export.mjs at the commit recorded
// in toy-golden.json. JavaScript prints the shortest round-trip double, so parsing gives the exact bits.
// They must match exactly, not approximately.
public class RngGoldenTests
{
    [Fact]
    public void GoldenComesFromTheToySnapshot() => Assert.Equal("a057b50", Golden.Toy.ToyCommit);

    [Fact]
    public void Mulberry32MatchesTypeScript()
    {
        int n = 0;
        foreach (var c in Golden.Root.GetProperty("rng").EnumerateArray())
        {
            var rng = new Mulberry32(c.GetProperty("seed").GetUInt32());
            foreach (var e in c.GetProperty("values").EnumerateArray()) { Assert.Equal(e.GetDouble(), rng.NextDouble()); n++; }
        }
        Assert.Equal(30, n);
    }

    [Fact]
    public void Hash2MatchesTypeScript()
    {
        int n = 0;
        foreach (var c in Golden.Root.GetProperty("hash").EnumerateArray())
        {
            Assert.Equal(c.GetProperty("value").GetDouble(), Hash.Hash2(c.GetProperty("a").GetInt32(), c.GetProperty("b").GetInt32()));
            n++;
        }
        Assert.Equal(8, n);
    }

    [Fact]
    public void SameSeedSameSequenceAndStateRoundTrips()
    {
        var a = new Mulberry32(99);
        a.NextDouble();
        var saved = a.State;
        double next = a.NextDouble();
        var b = new Mulberry32(0) { State = saved };
        Assert.Equal(next, b.NextDouble());
    }

    [Fact]
    public void ValuesStayInUnitInterval()
    {
        var r = new Mulberry32(5);
        for (int i = 0; i < 100_000; i++)
        {
            double v = r.NextDouble();
            Assert.True(v >= 0 && v < 1);
        }
    }
}

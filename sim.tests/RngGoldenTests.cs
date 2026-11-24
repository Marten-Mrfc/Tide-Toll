namespace TideToll.Sim.Tests;

// Golden values were made by running the toy's own TypeScript (prototypes/desire-lines/src/sim/rng.ts)
// in Node: the script golden.ts, bundled with the toy's esbuild and run with `node golden.mjs`
// (kept in the Phase 0 scratchpad, phase0-golden/). JavaScript prints the shortest round-trip
// double, so parsing these literals gives the exact bits. They must match exactly, not approximately.
public class RngGoldenTests
{
    [Theory]
    [InlineData(0u, new[] { 0.26642920868471265, 0.0003297457005828619, 0.2232720274478197, 0.1462021479383111, 0.46732782293111086, 0.5450490827206522 })]
    [InlineData(1u, new[] { 0.6270739405881613, 0.002735721180215478, 0.5274470399599522, 0.9810509674716741, 0.9683778982143849, 0.281103502959013 })]
    [InlineData(42u, new[] { 0.6011037519201636, 0.44829055899754167, 0.8524657934904099, 0.6697340414393693, 0.17481389874592423, 0.5265925421845168 })]
    [InlineData(123456789u, new[] { 0.2577907438389957, 0.9707721115555614, 0.7853280142880976, 0.20616457983851433, 0.30307188746519387, 0.7470660470426083 })]
    [InlineData(4294967295u, new[] { 0.8964226141106337, 0.189478256739676, 0.7156526781618595, 0.9440599093213677, 0.8452364315744489, 0.5391399988438934 })]
    public void Mulberry32MatchesTypeScript(uint seed, double[] expected)
    {
        var rng = new Mulberry32(seed);
        foreach (double e in expected) Assert.Equal(e, rng.NextDouble());
    }

    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(1, 0, 0.5081244609318674)]
    [InlineData(0, 1, 0.7682745542842895)]
    [InlineData(7, 1234, 0.6900073629803956)]
    [InlineData(42, 16383, 0.23021085793152452)]
    [InlineData(-1, 5, 0.047537319362163544)]
    [InlineData(2147483647, -2147483648, 0.5404983453918248)]
    [InlineData(100000, 99999, 0.7724837700370699)]
    public void Hash2MatchesTypeScript(int a, int b, double expected) => Assert.Equal(expected, Hash.Hash2(a, b));

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

namespace TideToll.Sim.Tests;

public class GridTests
{
    private static readonly Grid G = new(128, 128);

    private static (int x, int y)[] Line(int x0, int y0, int x1, int y1)
    {
        var cells = new List<int>();
        G.Line4(x0, y0, x1, y1, cells);
        return cells.Select(c => (G.X(c), G.Y(c))).ToArray();
    }

    // Expected cells were printed by the toy's line4 (grid.ts), same script as the RNG goldens.
    [Fact]
    public void Line4MatchesTypeScript()
    {
        Assert.Equal(new[] { (0, 0), (1, 0), (2, 0), (2, 1), (3, 1), (4, 1), (4, 2), (5, 2) }, Line(0, 0, 5, 2));
        Assert.Equal(new[] { (3, 3), (2, 3), (2, 2), (1, 2), (1, 1), (0, 1), (0, 0) }, Line(3, 3, 0, 0));
        Assert.Equal(new[] { (2, 7), (2, 6), (2, 5), (2, 4), (2, 3), (2, 2), (2, 1) }, Line(2, 7, 2, 1));
        Assert.Equal(new[] { (0, 0), (1, 0), (1, 1), (2, 1), (2, 2), (3, 2), (3, 3), (4, 3), (4, 4) }, Line(0, 0, 4, 4));
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(10, 3, 1, 9)]
    [InlineData(5, 5, 20, 6)]
    [InlineData(60, 2, 0, 127)]
    public void Line4IsFourConnectedAndHitsBothEnds(int x0, int y0, int x1, int y1)
    {
        var pts = Line(x0, y0, x1, y1);
        Assert.Equal((x0, y0), pts[0]);
        Assert.Equal((x1, y1), pts[^1]);
        for (int i = 1; i < pts.Length; i++)
            Assert.Equal(1, Math.Abs(pts[i].x - pts[i - 1].x) + Math.Abs(pts[i].y - pts[i - 1].y));
    }

    [Fact]
    public void IndexRoundTripsOnANonSquareGrid()
    {
        var g = new Grid(512, 384);
        Assert.Equal(512 * 384, g.Count);
        int c = g.Idx(511, 383);
        Assert.Equal((511, 383), (g.X(c), g.Y(c)));
        Assert.False(g.Inside(512, 0));
        Assert.False(g.Inside(0, -1));
    }
}

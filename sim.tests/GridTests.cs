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

    // Expected cells come from the toy's line4 (grid.ts), exported to golden/toy-golden.json.
    [Fact]
    public void Line4MatchesTypeScript()
    {
        int n = 0;
        foreach (var c in Golden.Root.GetProperty("line4").EnumerateArray())
        {
            var f = c.GetProperty("from"); var t = c.GetProperty("to");
            var cells = new List<int>();
            G.Line4(f[0].GetInt32(), f[1].GetInt32(), t[0].GetInt32(), t[1].GetInt32(), cells);
            Assert.Equal(c.GetProperty("cells").EnumerateArray().Select(e => e.GetInt32()).ToArray(), cells.ToArray());
            n++;
        }
        Assert.Equal(7, n);
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

    // Review fix: an endpoint outside the grid used to be wrapped by Idx into the neighbouring row
    // (x = -1 became the previous row's last cell), so a fence dragged off the map edge fenced the far side.
    [Theory]
    [InlineData(-3, 10, 4, 12)]
    [InlineData(125, 50, 131, 47)]
    [InlineData(5, -4, 9, 3)]
    [InlineData(60, 125, 58, 140)]
    [InlineData(-5, -5, -1, -9)]
    public void Line4ClipsPointsOutsideTheGrid(int x0, int y0, int x1, int y1)
    {
        var cells = new List<int>();
        G.Line4(x0, y0, x1, y1, cells);
        // Same walk with no grid limit, done by hand: the clipped line is exactly its in-grid points, in order.
        var expect = new List<(int, int)>();
        foreach (var (x, y) in Unclipped(x0, y0, x1, y1))
            if (G.Inside(x, y)) expect.Add((x, y));
        Assert.Equal(expect, cells.Select(c => (G.X(c), G.Y(c))).ToList());
        Assert.All(cells, c => Assert.InRange(c, 0, G.Count - 1));
        // No cell may sit more than a step from the line's own x range (a wrapped cell would be ~128 away).
        Assert.All(cells, c => Assert.InRange(G.X(c), Math.Min(x0, x1), Math.Max(x0, x1)));
    }

    private static IEnumerable<(int, int)> Unclipped(int x0, int y0, int x1, int y1)
    {
        yield return (x0, y0);
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x1 > x0 ? 1 : -1, sy = y1 > y0 ? 1 : -1;
        int err = dx + dy, x = x0, y = y0;
        while (x != x1 || y != y1)
        {
            int e2 = 2 * err;
            bool stepX = e2 >= dy, stepY = e2 <= dx;
            if (stepX && stepY) { x += sx; yield return (x, y); err += dy + dx; y += sy; }
            else if (stepX) { err += dy; x += sx; }
            else { err += dx; y += sy; }
            yield return (x, y);
        }
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

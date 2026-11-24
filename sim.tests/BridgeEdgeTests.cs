namespace TideToll.Sim.Tests;

/// <summary>
/// Review fix: BridgeCellsAt checked the bridge's ends with cells[0] - 1 and cells[^1] + 1, which read the
/// previous or next row when the water reached the first or last column (and index -1 on row 0).
/// Generated maps keep the river near the middle, so these tests move the water of one row to the edge.
/// </summary>
public class BridgeEdgeTests
{
    private static World WithRowAtEdge(int row, bool west, out int[] water)
    {
        var w = new World(1, 0);
        var m = w.Map;
        int n = w.Grid.Width;
        for (int x = 0; x < n; x++) { m.Water[row * n + x] = 0; m.Ford[row * n + x] = 0; }
        water = new int[3];
        for (int k = 0; k < 3; k++)
        {
            int x = west ? k : n - 1 - k;
            m.Water[row * n + x] = 1;
            water[k] = row * n + x;
        }
        Array.Sort(water);
        m.RiverX[row] = west ? 1.5f : n - 1.5f;
        return w;
    }

    [Theory]
    [InlineData(0, true)]     // row 0: cells[0] - 1 was index -1
    [InlineData(30, true)]    // cells[0] - 1 was the last cell of row 29
    [InlineData(30, false)]   // cells[^1] + 1 was the first cell of row 31
    [InlineData(127, false)]  // last row: cells[^1] + 1 was one past the grid
    public void NoBridgeWhenAnEndWouldBeOffTheMap(int row, bool west)
    {
        var w = WithRowAtEdge(row, west, out var water);
        Assert.True(Math.Abs(row - w.Map.FordY) > 2);
        var cells = new List<int>();
        bool ok = w.BridgeCellsAt(w.Map.RiverX[row], row + 0.5, cells);
        Assert.False(ok);
        Assert.Empty(cells);
        Assert.Null(w.PlaceBridge(w.Map.RiverX[row], row + 0.5));
        Assert.True(water.Length == 3);
    }

    [Fact]
    public void BridgeOneCellInFromTheEdgeStillFits()
    {
        // Water in columns 1..3 of a row: the west end lands on column 0, which is open ground.
        var w = new World(1, 0);
        var m = w.Map;
        int n = w.Grid.Width, row = 20;
        for (int x = 0; x < n; x++) { m.Water[row * n + x] = 0; m.Ford[row * n + x] = 0; }
        for (int x = 1; x <= 3; x++) m.Water[row * n + x] = 1;
        m.RiverX[row] = 2.5f;
        var cells = new List<int>();
        Assert.True(w.BridgeCellsAt(2.5, row + 0.5, cells));
        Assert.Equal(new[] { row * n + 1, row * n + 2, row * n + 3 }, cells);
    }
}

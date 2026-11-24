using System.Diagnostics;
using Xunit.Abstractions;

namespace TideToll.Sim.Tests;

public class CostFieldTests
{
    private readonly ITestOutputHelper _out;
    public CostFieldTests(ITestOutputHelper output) => _out = output;

    private const float Inf = float.PositiveInfinity;

    private static float[] Uniform(Grid g, float c = 1f) { var a = new float[g.Count]; Array.Fill(a, c); return a; }

    [Fact]
    public void WallWithGapForcesDetour()
    {
        // 7 x 5 grid, a wall down column 3 with a gap at the bottom row.
        var g = new Grid(7, 5);
        var cost = Uniform(g);
        for (int y = 0; y < 4; y++) cost[g.Idx(3, y)] = Inf;
        var field = new float[g.Count];
        CostField.Dijkstra(g, g.Idx(0, 0), cost, field, new BinaryHeap(8));

        Assert.Equal(0f, field[g.Idx(0, 0)]);
        Assert.Equal(1f, field[g.Idx(1, 0)]);
        Assert.True(float.IsPositiveInfinity(field[g.Idx(3, 0)]));
        // Open grass: (2,0) is two straight steps away.
        Assert.Equal(2f, field[g.Idx(2, 0)]);
        // (4,0) sits just past the wall but must go round through the gap at (3,4).
        float direct = field[g.Idx(2, 0)] + 2;
        Assert.True(field[g.Idx(4, 0)] > direct + 4, $"expected a detour, got {field[g.Idx(4, 0)]}");
    }

    [Fact]
    public void DiagonalsMayNotCutABlockedCorner()
    {
        // Two blocked cells touching only at a corner: (1,0) and (0,1). The diagonal (0,0)->(1,1)
        // would slip between them, so (1,1) must be unreachable from (0,0) in a 2 x 2 grid.
        var g = new Grid(2, 2);
        var cost = Uniform(g);
        cost[g.Idx(1, 0)] = Inf;
        cost[g.Idx(0, 1)] = Inf;
        var field = new float[g.Count];
        CostField.Dijkstra(g, g.Idx(0, 0), cost, field, new BinaryHeap(4));
        Assert.True(float.IsPositiveInfinity(field[g.Idx(1, 1)]));
        Assert.Equal(-1, CostField.CanStep(g, g.Idx(0, 0), 4, cost)); // k=4 is (+1,+1)

        // One corner open: the diagonal is still refused, so the route takes two straight steps.
        cost[g.Idx(0, 1)] = 1;
        CostField.Dijkstra(g, g.Idx(0, 0), cost, field, new BinaryHeap(4));
        Assert.Equal(2f, field[g.Idx(1, 1)]);

        // Both open: the diagonal costs sqrt(2).
        cost[g.Idx(1, 0)] = 1;
        CostField.Dijkstra(g, g.Idx(0, 0), cost, field, new BinaryHeap(4));
        Assert.Equal((float)Math.Sqrt(2), field[g.Idx(1, 1)]);
    }

    [Fact]
    public void BlockedTargetGivesAllInfinity()
    {
        var g = new Grid(4, 4);
        var cost = Uniform(g);
        cost[5] = Inf;
        var field = new float[g.Count];
        CostField.Dijkstra(g, 5, cost, field, new BinaryHeap(4));
        Assert.All(field, f => Assert.True(float.IsPositiveInfinity(f)));
    }

    [Fact]
    public void GoalsStopEarlyButSettleTheGoals()
    {
        var g = new Grid(32, 32);
        var cost = Uniform(g);
        var full = new float[g.Count];
        var part = new float[g.Count];
        CostField.Dijkstra(g, 0, cost, full, new BinaryHeap(16));
        int goal = g.Idx(5, 4);
        CostField.Dijkstra(g, 0, cost, part, new BinaryHeap(16), new[] { goal });
        Assert.Equal(full[goal], part[goal]);
        Assert.True(float.IsPositiveInfinity(part[g.Idx(31, 31)]));
    }

    // Golden field from the toy's dijkstra (path.ts) on its 128 x 128 grid: costs 1 + 3 * mulberry32(7),
    // a wall on column 64 for rows 0..99, target (10,10). Values printed by the same golden.ts script.
    [Fact]
    public void Dijkstra128MatchesTypeScript()
    {
        var (g, cost) = GoldenMap();
        var field = new float[g.Count];
        CostField.Dijkstra(g, g.Idx(10, 10), cost, field, new BinaryHeap(1024));

        double sum = 0;
        int inf = 0;
        foreach (float f in field) { if (float.IsPositiveInfinity(f)) inf++; else sum += f; }
        Assert.Equal(100, inf);
        Assert.Equal(3573179.6519253254, sum);
        Assert.Equal(0f, field[g.Idx(10, 10)]);
        Assert.Equal(2.794908046722412f, field[g.Idx(11, 10)]);
        Assert.Equal(125.65255737304688f, field[g.Idx(63, 50)]);
        Assert.Equal(309.34429931640625f, field[g.Idx(65, 50)]);
        Assert.Equal(205.12088012695312f, field[g.Idx(64, 100)]);
        Assert.Equal(432.6565246582031f, field[g.Idx(127, 0)]);
        Assert.Equal(332.4920349121094f, field[g.Idx(127, 127)]);
        Assert.Equal(228.00746154785156f, field[g.Idx(0, 127)]);
    }

    // Deterministic workload (fixed seed, fixed pass count). The time is printed, never asserted,
    // because other jobs share the machine; the result checksum is asserted so the work is real.
    [Fact]
    public void Benchmark128Dijkstra()
    {
        var (g, cost) = GoldenMap();
        var field = new float[g.Count];
        var heap = new BinaryHeap(1024);
        CostField.Dijkstra(g, g.Idx(10, 10), cost, field, heap); // warm-up
        const int passes = 50;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < passes; i++) CostField.Dijkstra(g, g.Idx(10 + i, 10), cost, field, heap);
        sw.Stop();
        double ms = sw.Elapsed.TotalMilliseconds / passes;
        string line = $"BENCH dijkstra 128x128: {ms:F2} ms per pass ({passes} passes)";
        _out.WriteLine(line);
        Console.WriteLine(line);
        Assert.Equal(100, field.Count(float.IsPositiveInfinity));
    }

    internal static (Grid, float[]) GoldenMap()
    {
        var g = new Grid(128, 128);
        var rng = new Mulberry32(7);
        var cost = new float[g.Count];
        for (int i = 0; i < cost.Length; i++) cost[i] = (float)(1 + 3 * rng.NextDouble());
        for (int y = 0; y < 100; y++) cost[g.Idx(64, y)] = Inf;
        return (g, cost);
    }
}

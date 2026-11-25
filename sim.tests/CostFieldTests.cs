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
    // a wall on column 64 for rows 0..99, target (10,10). Exported to golden/toy-golden.json.
    [Fact]
    public void Dijkstra128MatchesTypeScript()
    {
        var (g, cost) = GoldenMap();
        var field = new float[g.Count];
        CostField.Dijkstra(g, g.Idx(10, 10), cost, field, new BinaryHeap(1024));

        var gold = Golden.Root.GetProperty("dijkstra");
        double sum = 0;
        int inf = 0;
        foreach (float f in field) { if (float.IsPositiveInfinity(f)) inf++; else sum += f; }
        Assert.Equal(gold.GetProperty("infCount").GetInt32(), inf);
        Assert.Equal(gold.GetProperty("sum").GetDouble(), sum);
        Assert.Equal(gold.GetProperty("fieldFnv").GetUInt32(), TideToll.Sim.Testing.ToyHarness.Fnv<float>(field));
        int probes = 0;
        foreach (var p in gold.GetProperty("probes").EnumerateArray())
        {
            Assert.Equal((float)p.GetProperty("value").GetDouble(), field[g.Idx(p.GetProperty("x").GetInt32(), p.GetProperty("y").GetInt32())]);
            probes++;
        }
        Assert.Equal(8, probes);
    }

    // Deterministic workload (fixed seed, fixed pass count). The time is printed, never asserted,
    // because other jobs share the machine; the result checksum is asserted so the work is real.
    [Fact]
    public void Benchmark128Dijkstra()
    {
        var (g, cost) = GoldenMap();
        var field = new float[g.Count];
        var heap = new BinaryHeap(1024);
        var bq = new BucketQueue(g.Count);
        CostField.Dijkstra(g, g.Idx(10, 10), cost, field, heap); // warm-up
        CostField.Dijkstra(g, g.Idx(10, 10), cost, field, heap, bq);
        const int passes = 50;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < passes; i++) CostField.Dijkstra(g, g.Idx(10 + i, 10), cost, field, heap);
        sw.Stop();
        double ms = sw.Elapsed.TotalMilliseconds / passes;
        sw.Restart();
        for (int i = 0; i < passes; i++) CostField.Dijkstra(g, g.Idx(10 + i, 10), cost, field, heap, bq);
        sw.Stop();
        double msB = sw.Elapsed.TotalMilliseconds / passes;
        string line = $"BENCH dijkstra 128x128: heap {ms:F2} ms, bucket queue {msB:F2} ms per pass ({passes} passes)";
        _out.WriteLine(line);
        Console.WriteLine(line);
        Assert.Equal(100, field.Count(float.IsPositiveInfinity));
    }

    // ---------------------------------------------------------------- bucket queue (river pass 3, plan step A10)

    private static void AssertSameField(float[] a, float[] b, string what)
    {
        for (int i = 0; i < a.Length; i++)
            if (BitConverter.SingleToInt32Bits(a[i]) != BitConverter.SingleToInt32Bits(b[i]))
                Assert.Fail($"{what}: cell {i} heap {a[i]:R}, bucket queue {b[i]:R}");
    }

    private static void BothWays(Grid g, float[] cost, int target, string what, BucketQueue? bq = null)
    {
        var heap = new float[g.Count];
        var bucket = new float[g.Count];
        Array.Fill(bucket, 123f);
        CostField.Dijkstra(g, target, cost, heap, new BinaryHeap(64));
        CostField.Dijkstra(g, target, cost, bucket, new BinaryHeap(64), bq ?? new BucketQueue(g.Count));
        AssertSameField(heap, bucket, what);
    }

    [Fact]
    public void TheBucketQueueGivesExactlyTheHeapsField()
    {
        var (g, cost) = GoldenMap();
        var bq = new BucketQueue(g.Count);
        for (int t = 0; t < 6; t++) BothWays(g, cost, g.Idx(10 + 20 * t, 10 + 17 * t), $"golden map, target {t}", bq);
        // Wide cost ranges (a ferry's water, a learned drawbridge wait), walls with corners, a blocked target.
        var r = new Mulberry32(99);
        for (int m = 0; m < 6; m++)
        {
            var c = new float[g.Count];
            for (int i = 0; i < c.Length; i++)
            {
                double u = r.NextDouble();
                c[i] = u < 0.08 ? Inf : u < 0.1 ? (float)(5 + 40 * r.NextDouble()) : (float)(0.2 + 3 * r.NextDouble());
            }
            BothWays(g, c, g.Idx(64, 64), $"random map {m}", bq);
            c[g.Idx(3, 3)] = Inf;
            BothWays(g, c, g.Idx(3, 3), $"random map {m}, blocked target", bq);
        }
        // Falls back to the heap: a cell that costs nothing, and a range wider than the ring.
        var z = Uniform(g); z[g.Idx(5, 5)] = 0;
        BothWays(g, z, g.Idx(1, 1), "a free cell");
        var wide = Uniform(g, 0.01f); wide[g.Idx(50, 50)] = 1000;
        BothWays(g, wide, g.Idx(1, 1), "a ring wider than the scratch");
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void TheBucketQueueGivesExactlyTheHeapsFieldOnLiveTowns(int seed, bool river)
    {
        // Both views of a town that has worn its paths (and, with the river rules, a ferry and a drawbridge).
        var p = river ? SimParams.Default with { River = RiverRules.Default with { StartMoney = 1000 } } : SimParams.Default;
        var w = new World(seed, 200, p);
        if (river)
        {
            TideToll.Sim.Testing.RiverBot.Build(w, CrossingType.Ferry);
            TideToll.Sim.Testing.RiverBot.Build(w, CrossingType.Drawbridge);
        }
        TideToll.Sim.Testing.ToyHarness.Run(w, 6);
        var bq = new BucketQueue(w.Grid.Count);
        foreach (var view in new[] { w.Cost.ToArray(), w.CostX.ToArray() })
            for (int b = 0; b < w.Buildings.Length; b += 3) BothWays(w.Grid, view, w.Buildings[b].Door, $"seed {seed}, building {b}", bq);
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

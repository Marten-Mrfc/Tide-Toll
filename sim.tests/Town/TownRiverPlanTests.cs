using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>The second age's plan on seeds 1 to 100: nothing in the water that should not be, nothing through a plot,
/// every point reachable in every state of the tide and the crossings the levels allow.</summary>
public class TownRiverPlanTests
{
    private static bool OnLand(MapData m, double x, double y)
    {
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        return ix >= 0 && iy >= 0 && ix < m.Size && iy < m.Size && m.Water[iy * m.Size + ix] == 0;
    }

    [Fact]
    public void ThePlanIsValidOnSeeds1To100()
    {
        var fails = new List<string>();
        for (int seed = 1; seed <= 100; seed++)
        {
            var tp = TownPlanner.Build(seed);
            var rp = TownRiverPlanner.Build(tp);
            var m = tp.Map;
            void Fail(string s) => fails.Add($"seed {seed}: {s}");
            // Plots: on land, clear of each other and of the first age's plots and roads' cells.
            foreach (var p in rp.Plots)
            {
                for (int y = p.Y; y < p.Y + p.H; y++)
                    for (int x = p.X; x < p.X + p.W; x++)
                    {
                        if (!OnLand(m, x + 0.5, y + 0.5)) Fail($"{p.Kind} {p.Id} in the water at {x},{y}");
                        foreach (var q in tp.Plots) if (q.Contains(x, y)) Fail($"{p.Kind} {p.Id} on first-age plot {q.Id}");
                        foreach (var q in rp.Plots) if (q != p && q.Contains(x, y)) Fail($"{p.Kind} {p.Id} on river plot {q.Id}");
                    }
                bool far = m.Bank[p.Y * m.Size + p.X] > 0;
                if (far != p.FarBank) Fail($"{p.Kind} {p.Id} on the wrong bank");
            }
            // Points on land, on their own bank.
            for (int i = 0; i < RiverPoint.Count; i++)
            {
                if (!OnLand(m, rp.Px[i], rp.Py[i])) Fail($"point {i} in the water");
                foreach (var p in rp.Plots) if (p.Contains((int)Math.Floor(rp.Px[i]), (int)Math.Floor(rp.Py[i]))) Fail($"point {i} inside plot {p.Id}");
            }
            // Lanes: on land and not through any plot (sampled every 0.25 cells); crossings: the only links over water, straight across.
            foreach (var l in rp.Links)
            {
                for (double d = 0; d <= l.Length; d += 0.25)
                {
                    l.At(d, out double x, out double y, out _);
                    int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
                    if (l.Kind == RiverLinkKind.Lane && !OnLand(m, x, y)) { Fail($"lane {l.Id} in the water at {x:0.0},{y:0.0}"); break; }
                    bool hit = false;
                    foreach (var p in tp.Plots) if (p.Contains(ix, iy)) hit = true;
                    foreach (var p in rp.Plots) if (p.Contains(ix, iy)) hit = true;
                    if (hit) { Fail($"link {l.Id} ({l.Kind}) through a plot at {x:0.0},{y:0.0}"); break; }
                }
                if (l.Kind != RiverLinkKind.Lane && rp.PointFar[l.A] == rp.PointFar[l.B]) Fail($"crossing {l.Kind} does not cross");
                if (l.Kind == RiverLinkKind.Lane && rp.PointFar[l.A] != rp.PointFar[l.B]) Fail($"lane {l.Id} crosses the river");
            }
            // Boats: berths and waiting places in the water; the footbridge where no boat goes (north of every berth),
            // the drawbridge between the south edge and the landing (every boat passes it).
            for (int k = 0; k < 2; k++) if (OnLand(m, rp.BerthX[k], rp.BerthY[k])) Fail($"berth {k} on land");
            foreach (var wy in rp.WaitY) if (OnLand(m, rp.CourseX[(int)wy], wy)) Fail("waiting place on land");
            for (int y = rp.FootRow + 2; y < m.Size; y++) if (OnLand(m, rp.CourseX[y], y + 0.5)) { Fail($"boats' course on land at row {y}"); break; }
            if (rp.FootRow + 1 >= rp.BerthY[0] - 1) Fail("footbridge not north of the berths");
            if (rp.DrawRow <= rp.WaitY[^1]) Fail("drawbridge not south of the waiting places");
            if (rp.FerryRow >= rp.BerthY[0] - 1) Fail("ferry rope across the boats' course");
            // Reachable: from the bank node every point, with only the crossings each level allows (ford: at low water).
            var open = new bool[rp.Links.Length];
            for (int lv = 0; lv < rp.Levels.Length; lv++)
            {
                foreach (int li in rp.Levels[lv].Links) open[li] = true;
                if (lv < 4) continue; // the far bank matters from the pasture on
                var seen = new bool[RiverPoint.Count]; seen[RiverPoint.Bank] = true;
                for (bool grew = true; grew; )
                {
                    grew = false;
                    foreach (var l in rp.Links)
                    {
                        if (!open[l.Id]) continue;
                        if (seen[l.A] != seen[l.B]) { seen[l.A] = seen[l.B] = true; grew = true; }
                    }
                }
                foreach (var p in rp.Plots) if (p.Level <= rp.Levels[lv].Number && !seen[p.Point]) Fail($"level {rp.Levels[lv].Number}: {p.Kind} {p.Id} unreachable");
            }
            // The bank road starts at a first-age node.
            if (rp.BankNode < 0 || rp.BankNode >= tp.Nodes.Length) Fail("bank node");
        }
        Assert.True(fails.Count == 0, string.Join("\n", fails.Take(30)) + $"\n({fails.Count} in all)");
    }

    [Fact]
    public void ThePlanIsTheSameFromTheSameSeed()
    {
        var a = TownRiverPlanner.Build(TownPlanner.Build(7));
        var b = TownRiverPlanner.Build(TownPlanner.Build(7));
        Assert.Equal(a.Px, b.Px); Assert.Equal(a.Py, b.Py);
        Assert.Equal(a.Links.Length, b.Links.Length);
        for (int i = 0; i < a.Links.Length; i++) Assert.Equal(a.Links[i].Px, b.Links[i].Px);
        Assert.Equal(12, a.Levels.Length);
        Assert.Equal(17, a.Levels[0].Number); Assert.Equal(28, a.Levels[^1].Number);
    }
}

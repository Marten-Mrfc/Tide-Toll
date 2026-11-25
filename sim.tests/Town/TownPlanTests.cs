using TideToll.Sim.Town;

namespace TideToll.Sim.Tests.Town;

public class TownPlanTests
{
    public static IEnumerable<object[]> Seeds() { for (int s = 1; s <= 100; s++) yield return new object[] { s }; }

    [Fact]
    public void PlanIsValidOnSeeds1To100()
    {
        var problems = new List<string>();
        for (int seed = 1; seed <= 100; seed++)
        {
            var p = TownPlanner.Build(seed);
            problems.AddRange(Check(p));
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems.Take(30)));
    }

    public static List<string> Check(TownPlan p)
    {
        var bad = new List<string>();
        int n = p.Map.Size;
        string S(string m) => $"seed {p.Seed}: {m}";
        // Plots: inside the map, on dry west-bank land, not overlapping, within the window budget.
        int minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0;
        foreach (var a in p.Plots)
        {
            for (int y = a.Y; y < a.Y + a.H; y++)
                for (int x = a.X; x < a.X + a.W; x++)
                {
                    if (x < 0 || y < 0 || x >= n || y >= n) { bad.Add(S($"plot {a.Id} off the map")); continue; }
                    if (p.Map.Water[y * n + x] != 0) bad.Add(S($"plot {a.Id} in water"));
                    if (p.Map.Bank[y * n + x] != -1) bad.Add(S($"plot {a.Id} not on the west bank"));
                }
            foreach (var b in p.Plots)
                if (b.Id > a.Id && a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H) bad.Add(S($"plots {a.Id} and {b.Id} overlap"));
            minX = Math.Min(minX, a.X); minY = Math.Min(minY, a.Y); maxX = Math.Max(maxX, a.X + a.W); maxY = Math.Max(maxY, a.Y + a.H);
        }
        // Roads: never through water or a plot; ends are nodes.
        foreach (var r in p.Roads)
        {
            if (r.Xs[0] != p.Nodes[r.A].X || r.Ys[0] != p.Nodes[r.A].Y || r.Xs[^1] != p.Nodes[r.B].X || r.Ys[^1] != p.Nodes[r.B].Y) bad.Add(S($"road {r.Id} does not end at its nodes"));
            if (r.Length <= 0) bad.Add(S($"road {r.Id} has no length"));
            foreach (int c in TownPlanner.RoadCells(r, n))
            {
                int x = c % n, y = c / n;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x + 1); maxY = Math.Max(maxY, y + 1);
                if (p.Map.Water[c] != 0) bad.Add(S($"road {r.Id} crosses water at {x},{y}"));
                foreach (var a in p.Plots) if (a.Contains(x, y)) bad.Add(S($"road {r.Id} crosses plot {a.Id} at {x},{y}"));
            }
        }
        if (maxX - minX > 50 || maxY - minY > 42) bad.Add(S($"town spans {maxX - minX} x {maxY - minY} cells"));
        // Riverside left free: nothing within 5 cells of water.
        foreach (var a in p.Plots)
            for (int y = a.Y; y < a.Y + a.H; y++)
                if (p.Map.RiverX[y] - p.Map.RiverHalfW - (a.X + a.W) < 5) bad.Add(S($"plot {a.Id} too close to the river"));
        // Level 1: exactly one cottage, the well, one road between them.
        var l1 = p.Level(1);
        var kinds = l1.Plots.Select(i => p.Plots[i].Kind).OrderBy(k => k).ToArray();
        if (!kinds.SequenceEqual(new[] { PlotKind.Cottage, PlotKind.Well })) bad.Add(S("level 1 is not one cottage and the well"));
        if (l1.Roads.Length != 1) bad.Add(S("level 1 has not exactly one road"));
        else
        {
            var r = p.Roads[l1.Roads[0]];
            var ends = new[] { r.A, r.B }.OrderBy(x => x).ToArray();
            var doors = l1.Plots.Select(i => p.Plots[i].Node).OrderBy(x => x).ToArray();
            if (!ends.SequenceEqual(doors)) bad.Add(S("the level 1 road does not join the cottage and the well"));
        }
        // Reachability at every level: every given plot's door is reachable from the well over the roads given so far.
        for (int lv = 1; lv <= p.MaxLevel; lv++)
        {
            var seen = new bool[p.Nodes.Length];
            int well = p.Plots.First(x => x.Kind == PlotKind.Well).Node;
            var stack = new Stack<int>(); stack.Push(well); seen[well] = true;
            while (stack.Count > 0)
            {
                int u = stack.Pop();
                foreach (int ri in p.Nodes[u].Roads)
                {
                    var r = p.Roads[ri];
                    if (r.Level > lv) continue;
                    int v = r.A == u ? r.B : r.A;
                    if (!seen[v]) { seen[v] = true; stack.Push(v); }
                }
            }
            foreach (var a in p.Plots) if (a.Level <= lv && !seen[a.Node]) bad.Add(S($"plot {a.Id} not reachable at level {lv}"));
        }
        // Later roads close loops: more roads than a tree needs.
        if (p.Roads.Length <= p.Nodes.Length - 1) bad.Add(S("no loops"));
        if (p.RoadCrossings != 0) bad.Add(S($"{p.RoadCrossings} roads cross another road"));
        return bad;
    }

    [Fact]
    public void LevelOneStartsWithOneVillagerAtTheCottage()
    {
        var t = new TideToll.Sim.Town.Town(1);
        Assert.Equal(1, t.VillagerCount);
        Assert.Equal(1, t.Level);
        var v = t.Villager(0);
        Assert.Equal(PlotKind.Cottage, t.Plan.Plots[v.Home].Kind);
        int open = 0;
        for (int r = 0; r < t.RoadCount; r++) if (t.RoadOpen(r)) open++;
        Assert.Equal(1, open);
    }

    [Fact]
    public void PlanIsTheSameForTheSameSeed()
    {
        var a = TownPlanner.Build(7); var b = TownPlanner.Build(7);
        Assert.Equal(a.Roads.Length, b.Roads.Length);
        for (int i = 0; i < a.Roads.Length; i++) { Assert.Equal(a.Roads[i].Xs, b.Roads[i].Xs); Assert.Equal(a.Roads[i].Ys, b.Roads[i].Ys); }
    }
}

using System;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 3, round 3 (asset track LF): which map cells are taken by the town in either age, so that decoration (trees,
/// gardens, animals) never stands on a lane, a road, a landing, a crossing's end, a plot, a queue spot or the water.
/// Read from the plan only (both ages: the river plan is built from the first age's plan by the same planner the
/// simulation uses), so the same town always leaves the same ground free. Built once per seed, never per frame.
/// </summary>
public static class TownSpace
{
    private static int _seed = int.MinValue;
    private static bool[] _taken = Array.Empty<bool>();
    private static RiverPlan? _rp;
    private static bool[] _yard = Array.Empty<bool>();
    private static bool[] _road = Array.Empty<bool>();

    /// <summary>Night 3 (LF, after the review): within 3 cells of a road (its cells or its drawn curve), where
    /// villagers walk and queue; a garden standing there reads as a villager's load.</summary>
    public static bool NearRoad(TownPlan plan, int x, int y)
    {
        Taken(plan);
        int n = TownGround.N;
        return x < 0 || y < 0 || x >= n || y >= n || _road[y * n + x];
    }

    public static bool NearRoadRect(TownPlan plan, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) if (NearRoad(plan, x, y)) return true;
        return false;
    }

    /// <summary>The ring round every cottage (both ages) kept for its garden: trees stay out of it.</summary>
    public static bool Yard(TownPlan plan, int x, int y)
    {
        Taken(plan);
        int n = TownGround.N;
        return x >= 0 && y >= 0 && x < n && y < n && _yard[y * n + x];
    }

    /// <summary>The second age's plan for this seed (built here once; display only).</summary>
    public static RiverPlan River(TownPlan plan) { Taken(plan); return _rp!; }

    /// <summary>Taken cells (N x N, y * N + x).</summary>
    public static bool[] Taken(TownPlan plan)
    {
        if (_seed == plan.Seed && _taken.Length > 0) return _taken;
        int n = TownGround.N;
        var t = new bool[n * n];
        var map = plan.Map;
        for (int i = 0; i < n * n; i++) if (map.Water[i] != 0) t[i] = true;
        // Plots with a cell of margin (cottages widen with extensions; queues stand at the doors).
        foreach (var p in plan.Plots) Rect(t, p.X - 1, p.Y - 1, p.X + p.W + 1, p.Y + p.H + 1);
        // Roads (the drawn curve and the cells) with a cell to each side: queue spots stand on them.
        foreach (var r in plan.Roads)
        {
            for (int k = 0; k < r.Xs.Length; k++) Rect(t, r.Xs[k] - 1, r.Ys[k] - 1, r.Xs[k] + 2, r.Ys[k] + 2);
            for (int k = 0; k < r.Px.Length; k++) Disc(t, r.Px[k], r.Py[k], 1.6);
        }
        var road = new bool[n * n];
        foreach (var r in plan.Roads)
        {
            for (int k = 0; k < r.Xs.Length; k++) Rect(road, r.Xs[k] - 3, r.Ys[k] - 3, r.Xs[k] + 4, r.Ys[k] + 4);
            for (int k = 0; k < r.Px.Length; k++) Disc(road, r.Px[k], r.Py[k], 3.6);
        }
        _road = road;
        var rp = TownRiverPlanner.Build(plan);
        _rp = rp;
        foreach (var p in rp.Plots) Rect(t, p.X - 1, p.Y - 1, p.X + p.W + 1, p.Y + p.H + 1);
        var land = rp.PlotOf(RiverPlotKind.Landing);
        Rect(t, land.X - 5, land.Y - 5, land.X + land.W + 5, land.Y + land.H + 5);
        foreach (var l in rp.Links)
        {
            for (int k = 0; k < l.Px.Length; k++) Disc(t, l.Px[k], l.Py[k], 1.6);
            for (int k = 0; k < l.Xs.Length; k++) Rect(t, l.Xs[k] - 1, l.Ys[k] - 1, l.Xs[k] + 2, l.Ys[k] + 2);
            if (l.Kind == RiverLinkKind.Lane || l.Px.Length == 0) continue;
            int m = l.Px.Length - 1;
            Disc(t, l.Px[0], l.Py[0], 6); Disc(t, l.Px[m], l.Py[m], 6);       // a crossing's ends and their worn ways
        }
        for (int i = 0; i < rp.Px.Length; i++) Disc(t, rp.Px[i], rp.Py[i], 2.5);  // every river point (doors, berths)
        var yd = new bool[n * n];
        foreach (var p in plan.Plots) if (p.Kind == PlotKind.Cottage) Rect(yd, p.X - 3, p.Y - 3, p.X + p.W + 3, p.Y + p.H + 3);
        foreach (var p in rp.Plots) if (p.Kind is RiverPlotKind.Cottage or RiverPlotKind.Weaver) Rect(yd, p.X - 3, p.Y - 3, p.X + p.W + 3, p.Y + p.H + 3);
        _seed = plan.Seed;
        _yard = yd;
        _taken = t;
        return t;
    }

    /// <summary>Night 3 (LF): cells kept clear of trees round the bank's rock outcrops (TownLife), so a willow never
    /// grows through a rock. Set by <see cref="KeepClear"/> before the trees are planted for this seed.</summary>
    private static bool[] _clear = Array.Empty<bool>();
    private static int _clearSeed = int.MinValue;

    public static void KeepClear(TownPlan plan, int x, int y, int r)
    {
        int n = TownGround.N;
        if (_clearSeed != plan.Seed || _clear.Length != n * n) { _clear = new bool[n * n]; _clearSeed = plan.Seed; }
        Rect(_clear, x - r, y - r, x + r + 1, y + r + 1);
    }

    public static bool KeptClear(TownPlan plan, int x, int y)
    {
        int n = TownGround.N;
        return _clearSeed == plan.Seed && x >= 0 && y >= 0 && x < n && y < n && _clear[y * n + x];
    }

    public static bool Free(TownPlan plan, int x, int y)
    {
        int n = TownGround.N;
        return x >= 1 && y >= 1 && x < n - 1 && y < n - 1 && !Taken(plan)[y * n + x];
    }

    /// <summary>All cells of the rectangle [x0, x1) x [y0, y1) free.</summary>
    public static bool FreeRect(TownPlan plan, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) if (!Free(plan, x, y)) return false;
        return true;
    }

    private static void Rect(bool[] t, int x0, int y0, int x1, int y1)
    {
        int n = TownGround.N;
        for (int y = Math.Max(0, y0); y < Math.Min(n, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(n, x1); x++) t[y * n + x] = true;
    }

    /// <summary>Cells whose centre lies within r of (cx, cy) (map cells; points already at cell centres).</summary>
    private static void Disc(bool[] t, double cx, double cy, double r)
    {
        int n = TownGround.N;
        for (int y = (int)Math.Floor(cy - r); y <= (int)Math.Ceiling(cy + r); y++)
            for (int x = (int)Math.Floor(cx - r); x <= (int)Math.Ceiling(cx + r); x++)
            {
                if (x < 0 || y < 0 || x >= n || y >= n) continue;
                double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                if (dx * dx + dy * dy <= r * r) t[y * n + x] = true;
            }
    }
}

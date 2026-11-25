using System;

namespace TideToll.Sim;

/// <summary>
/// Cost-to-go fields, ported from the toy's path.ts. One field per target (each destination
/// and each house door); walkers descend the field instead of running A* per trip, because
/// at 500 walkers and high speed per-trip search costs far more than one field per target.
/// </summary>
public static class CostField
{
    /// <summary>
    /// Fills <paramref name="field"/> with the cost to reach <paramref name="target"/> from every cell.
    /// <paramref name="cost"/> is the per-cell step cost, +Infinity where blocked. An edge costs its
    /// length times the mean of its two cells' costs, which is symmetric, so expanding outward from
    /// the target gives cost-to-go. Diagonals may not cut a blocked corner, so walkers never slip
    /// between two diagonal fence posts. With <paramref name="goals"/>, it stops once all of them
    /// are settled. Pure: pass scratch arrays to compute a what-if field without touching live state.
    /// </summary>
    public static void Dijkstra(in Grid grid, int target, float[] cost, float[] field, BinaryHeap heap,
        ReadOnlySpan<int> goals = default)
    {
        if (cost.Length != grid.Count || field.Length != grid.Count)
            throw new ArgumentException("cost and field must have one entry per grid cell.");
        Array.Fill(field, float.PositiveInfinity);
        heap.Clear();
        if (!(cost[target] < float.PositiveInfinity)) return;
        field[target] = 0;
        heap.Push(0, target);
        int w = grid.Width, h = grid.Height;
        int left = goals.IsEmpty ? -1 : goals.Length;
        while (heap.Count > 0)
        {
            int u = heap.Pop();
            double du = heap.LastKey;
            if (du > field[u]) continue; // stale entry
            if (left > 0 && goals.IndexOf(u) >= 0 && --left == 0) return;
            int ux = u % w, uy = u / w;
            float cu = cost[u];
            for (int k = 0; k < 8; k++)
            {
                int vx = ux + Grid.DX[k], vy = uy + Grid.DY[k];
                if (vx < 0 || vy < 0 || vx >= w || vy >= h) continue;
                int v = vy * w + vx;
                float cv = cost[v];
                if (float.IsPositiveInfinity(cv)) continue;
                if (k >= 4 && (float.IsPositiveInfinity(cost[uy * w + vx]) || float.IsPositiveInfinity(cost[vy * w + ux]))) continue;
                // Sum in double, then round to float, exactly as the toy does with Math.fround.
                float nd = (float)(du + Grid.DL[k] * 0.5 * ((double)cu + cv));
                if (nd < field[v]) { field[v] = nd; heap.Push(nd, v); }
            }
        }
    }

    /// <summary>
    /// Cost of walking straight from (x0,y0) to (x1,y1), or +Infinity if the line crosses a blocked cell or cuts a blocked
    /// corner. With <paramref name="limit"/> it may stop early and return any value above it once the cost is sure to exceed it.
    /// </summary>
    public static double LineCost(in Grid grid, float[] cost, double x0, double y0, double x1, double y1, double limit = double.PositiveInfinity)
    {
        int n = grid.Width;
        double len = JsMath.Hypot(x1 - x0, y1 - y0);
        int steps = Math.Max(1, (int)Math.Ceiling(len / 0.2));
        double ds = len / steps;
        int pcx = (int)Math.Floor(x0), pcy = (int)Math.Floor(y0);
        double sum = 0;
        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            int cx = (int)Math.Floor(x0 + (x1 - x0) * t), cy = (int)Math.Floor(y0 + (y1 - y0) * t);
            int c = cy * n + cx;
            if (cx != pcx || cy != pcy)
            {
                if (float.IsPositiveInfinity(cost[c])) return double.PositiveInfinity;
                if (cx != pcx && cy != pcy && (float.IsPositiveInfinity(cost[pcy * n + cx]) || float.IsPositiveInfinity(cost[cy * n + pcx])))
                    return double.PositiveInfinity;
                pcx = cx; pcy = cy;
            }
            sum += cost[c] * ds;
            if (sum > limit) return sum;
        }
        return sum;
    }

    /// <summary>
    /// Length in cells of the route a plain field descent takes from <paramref name="start"/> to <paramref name="door"/>
    /// (steepest descent, no taste or noise), or +Infinity if it cannot get there (the toy's routeLength).
    /// </summary>
    public static double RouteLength(in Grid grid, float[] field, float[] cost, int start, int door)
    {
        int u = start;
        double len = 0;
        for (int s = 0; s < 4 * grid.Width && u != door; s++)
        {
            int best = -1, bk = 0;
            float bv = field[u];
            for (int k = 0; k < 8; k++)
            {
                int v = CanStep(grid, u, k, cost);
                if (v >= 0 && field[v] < bv) { bv = field[v]; best = v; bk = k; }
            }
            if (best < 0) return double.PositiveInfinity;
            len += Grid.DL[bk]; u = best;
        }
        return u == door ? len : double.PositiveInfinity;
    }

    /// <summary>The cell reached by stepping from u in direction k, or -1 if bounds, blocking or the corner rule forbid it.</summary>
    public static int CanStep(in Grid grid, int u, int k, float[] cost)
    {
        int w = grid.Width;
        int ux = u % w, uy = u / w;
        int vx = ux + Grid.DX[k], vy = uy + Grid.DY[k];
        if (!grid.Inside(vx, vy)) return -1;
        int v = vy * w + vx;
        if (float.IsPositiveInfinity(cost[v])) return -1;
        if (k >= 4 && (float.IsPositiveInfinity(cost[uy * w + vx]) || float.IsPositiveInfinity(cost[vy * w + ux]))) return -1;
        return v;
    }
}

/// <summary>
/// <see cref="CostField.Dijkstra"/> split into bounded slices: <see cref="Begin"/> sets it up and each
/// <see cref="Run"/> settles at most a given number of heap entries, so a caller can stop within a frame budget.
/// Pops, ties and float rounding are exactly those of <see cref="CostField.Dijkstra"/>, so the finished field is
/// bit-identical however the work was sliced. Allocation-free after construction.
/// </summary>
public sealed class DijkstraRun
{
    private Grid _grid;
    private float[] _cost = Array.Empty<float>(), _field = Array.Empty<float>();
    private int[]? _goals;
    private BinaryHeap? _heap;
    private int _left;

    /// <summary>Heap entries popped by the last <see cref="Run"/>.</summary>
    public int LastPops { get; private set; }

    /// <summary>True once the field is complete.</summary>
    public bool Done { get; private set; } = true;

    /// <summary>Starts a field towards <paramref name="target"/>; fills <paramref name="field"/> with +Infinity first (one pass over the grid).</summary>
    public void Begin(in Grid grid, int target, float[] cost, float[] field, BinaryHeap heap, int[]? goals = null)
    {
        if (cost.Length != grid.Count || field.Length != grid.Count)
            throw new ArgumentException("cost and field must have one entry per grid cell.");
        _grid = grid; _cost = cost; _field = field; _heap = heap; _goals = goals;
        Array.Fill(field, float.PositiveInfinity);
        heap.Clear();
        Done = false;
        if (!(cost[target] < float.PositiveInfinity)) { Done = true; return; }
        field[target] = 0;
        heap.Push(0, target);
        _left = goals == null || goals.Length == 0 ? -1 : goals.Length;
    }

    /// <summary>Pops at most <paramref name="maxPops"/> heap entries (stale ones included). Returns true while work remains.</summary>
    public bool Run(int maxPops)
    {
        LastPops = 0;
        if (Done) return false;
        var heap = _heap!;
        float[] cost = _cost, field = _field;
        int w = _grid.Width, h = _grid.Height;
        for (int pops = 0; pops < maxPops; pops++)
        {
            if (heap.Count == 0) { Done = true; return false; }
            int u = heap.Pop();
            LastPops++;
            double du = heap.LastKey;
            if (du > field[u]) continue; // stale entry
            if (_left > 0 && Array.IndexOf(_goals!, u) >= 0 && --_left == 0) { Done = true; return false; }
            int ux = u % w, uy = u / w;
            float cu = cost[u];
            for (int k = 0; k < 8; k++)
            {
                int vx = ux + Grid.DX[k], vy = uy + Grid.DY[k];
                if (vx < 0 || vy < 0 || vx >= w || vy >= h) continue;
                int v = vy * w + vx;
                float cv = cost[v];
                if (float.IsPositiveInfinity(cv)) continue;
                if (k >= 4 && (float.IsPositiveInfinity(cost[uy * w + vx]) || float.IsPositiveInfinity(cost[vy * w + ux]))) continue;
                float nd = (float)(du + Grid.DL[k] * 0.5 * ((double)cu + cv));
                if (nd < field[v]) { field[v] = nd; heap.Push(nd, v); }
            }
        }
        if (heap.Count == 0) Done = true;
        return !Done;
    }
}

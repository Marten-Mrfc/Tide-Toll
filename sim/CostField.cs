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

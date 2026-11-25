using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

/// <summary>The planner's road router: 8-way A* on the map grid, then a polyline that keeps the bends (used once per plan).</summary>
internal sealed class Router
{
    private readonly int _n;
    private readonly float[] _noise;
    private readonly byte[] _blocked;
    private readonly float[] _penalty;
    private readonly byte[] _road;
    private readonly double[] _g;
    private readonly int[] _from;
    private readonly int[] _stamp;
    private int _gen;
    private readonly List<int> _line = new();
    private readonly Grid _grid;
    private int _sx, _sy, _tx, _ty;

    public Router(int n, float[] noise, byte[] blocked, float[] penalty, byte[] road)
    {
        _n = n; _noise = noise; _blocked = blocked; _penalty = penalty; _road = road;
        _g = new double[n * n]; _from = new int[n * n]; _stamp = new int[n * n];
        _grid = new Grid(n, n);
    }

    private bool NearEnds(int x, int y) =>
        (Math.Abs(x - _sx) <= 2 && Math.Abs(y - _sy) <= 2) || (Math.Abs(x - _tx) <= 2 && Math.Abs(y - _ty) <= 2);

    private bool Open(int x, int y, bool avoidRoads)
    {
        if (x < 0 || y < 0 || x >= _n || y >= _n) return false;
        int i = y * _n + x;
        if (_blocked[i] != 0) return false;
        if (avoidRoads && _road[i] != 0 && !NearEnds(x, y)) return false;
        return true;
    }

    public List<(int X, int Y)>? Route(int sx, int sy, int tx, int ty, bool avoidRoads)
    {
        _sx = sx; _sy = sy; _tx = tx; _ty = ty;
        _gen++;
        int n = _n;
        var heap = new PriorityQueue<int, (double, int)>();
        int s = sy * n + sx, t = ty * n + tx;
        _g[s] = 0; _from[s] = -1; _stamp[s] = _gen;
        heap.Enqueue(s, (H(sx, sy), s));
        var closed = new HashSet<int>();
        while (heap.Count > 0)
        {
            int c = heap.Dequeue();
            if (!closed.Add(c)) continue;
            if (c == t) break;
            int cx = c % n, cy = c / n;
            for (int k = 0; k < 8; k++)
            {
                int x = cx + Grid.DX[k], y = cy + Grid.DY[k];
                if (!Open(x, y, avoidRoads)) continue;
                if (k >= 4 && (!Open(cx + Grid.DX[k], cy, avoidRoads) || !Open(cx, cy + Grid.DY[k], avoidRoads))) continue;
                int i = y * n + x;
                double step = Grid.DL[k] * (1 + 0.9 * (_noise[i] + 1)) + _penalty[i];
                double ng = _g[c] + step;
                if (_stamp[i] == _gen && ng >= _g[i]) continue;
                _stamp[i] = _gen; _g[i] = ng; _from[i] = c;
                heap.Enqueue(i, (ng + H(x, y), i));
            }
        }
        if (_stamp[t] != _gen) return null;
        var path = new List<int>();
        for (int c = t; c != -1; c = _from[c]) path.Add(c);
        path.Reverse();
        return Simplify(path, avoidRoads);
    }

    private double H(int x, int y)
    {
        int dx = Math.Abs(x - _tx), dy = Math.Abs(y - _ty);
        return Math.Max(dx, dy) + (Math.Sqrt(2) - 1) * Math.Min(dx, dy);
    }

    /// <summary>Greedy: from each kept point, jump to the farthest path cell whose straight line stays open and near the path.</summary>
    private List<(int X, int Y)> Simplify(List<int> path, bool avoidRoads)
    {
        int n = _n;
        var pts = new List<(int, int)>();
        int i = 0;
        pts.Add((path[0] % n, path[0] / n));
        while (i < path.Count - 1)
        {
            int best = i + 1;
            for (int j = i + 2; j < path.Count && j <= i + 9; j++)
                if (LineOk(path, i, j, avoidRoads)) best = j;
            pts.Add((path[best] % n, path[best] / n));
            i = best;
        }
        return pts;
    }

    private bool LineOk(List<int> path, int i, int j, bool avoidRoads)
    {
        int n = _n;
        int x0 = path[i] % n, y0 = path[i] / n, x1 = path[j] % n, y1 = path[j] / n;
        _line.Clear();
        _grid.Line4(x0, y0, x1, y1, _line);
        foreach (int c in _line) if (!Open(c % n, c / n, avoidRoads)) return false;
        double len = Math.Sqrt((double)(x1 - x0) * (x1 - x0) + (double)(y1 - y0) * (y1 - y0));
        for (int k = i + 1; k < j; k++)
        {
            int px = path[k] % n, py = path[k] / n;
            double dev = Math.Abs((double)(x1 - x0) * (y0 - py) - (double)(x0 - px) * (y1 - y0)) / len;
            if (dev > 0.9) return false;
        }
        return true;
    }

    /// <summary>Rules v2.1: number of corner-cutting passes over the densified polyline.</summary>
    public const int SmoothPasses = 24;

    /// <summary>
    /// Rules v2.1: turns the routed grid polyline into a gentle curve. The polyline is cut into steps of about half a cell,
    /// then each inner point is moved halfway to the middle of its neighbours (a corner-cutting pass), SmoothPasses times.
    /// The two ends stay at the doors. A move is kept only if both segments next to the point stay on open cells
    /// (no water, plot or earlier road, as for the route itself), so the curve never leaves the ground the route was allowed on.
    /// </summary>
    public void Smooth(List<(int X, int Y)> pts, bool avoidRoads, out double[] px, out double[] py)
    {
        var xs = new List<double>(); var ys = new List<double>();
        xs.Add(pts[0].X); ys.Add(pts[0].Y);
        for (int i = 1; i < pts.Count; i++)
        {
            double ax = pts[i - 1].X, ay = pts[i - 1].Y, bx = pts[i].X, by = pts[i].Y;
            int k = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)) * 2));
            for (int j = 1; j <= k; j++) { xs.Add(ax + (bx - ax) * j / k); ys.Add(ay + (by - ay) * j / k); }
        }
        px = xs.ToArray(); py = ys.ToArray();
        int m = px.Length;
        for (int pass = 0; pass < SmoothPasses; pass++)
            for (int i = 1; i < m - 1; i++)
            {
                double qx = 0.5 * px[i] + 0.25 * (px[i - 1] + px[i + 1]);
                double qy = 0.5 * py[i] + 0.25 * (py[i - 1] + py[i + 1]);
                if (SegOk(px[i - 1], py[i - 1], qx, qy, avoidRoads) && SegOk(qx, qy, px[i + 1], py[i + 1], avoidRoads)) { px[i] = qx; py[i] = qy; }
            }
    }

    private bool SegOk(double ax, double ay, double bx, double by, bool avoidRoads)
    {
        double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
        int k = Math.Max(1, (int)Math.Ceiling(len * 10));
        int ox = (int)Math.Floor(ax + 0.5), oy = (int)Math.Floor(ay + 0.5);
        if (!Open(ox, oy, avoidRoads)) return false;
        for (int j = 1; j <= k; j++)
        {
            int cx = (int)Math.Floor(ax + (bx - ax) * j / k + 0.5), cy = (int)Math.Floor(ay + (by - ay) * j / k + 0.5);
            if (cx == ox && cy == oy) continue;
            if (!Open(cx, cy, avoidRoads)) return false;
            if (cx != ox && cy != oy && (!Open(cx, oy, avoidRoads) || !Open(ox, cy, avoidRoads))) return false;
            ox = cx; oy = cy;
        }
        return true;
    }

    public void Mark(Road r)
    {
        foreach (int c in TownPlanner.RoadCells(r, _n)) _road[c] = 1;
    }
}

using System;
using System.Collections.Generic;

namespace TideToll.Game;

/// <summary>
/// The toy's fenceLines (render.ts): fence cells are 4-connected staircases, so they are walked into chains
/// between ends and junctions and simplified (Douglas-Peucker, tolerance 0.75 cells), and a diagonal fence draws
/// as one straight run instead of a zigzag. Points are cell centres in cell units. Runs only when fences change.
/// </summary>
public static class FenceLines
{
    /// <summary>Polylines as flat x, y lists (a single point for an isolated cell).</summary>
    public static List<List<float>> Build(ReadOnlySpan<byte> fence, int n)
    {
        var cells = new List<int>();
        for (int c = 0; c < fence.Length; c++) if (fence[c] != 0) cells.Add(c);
        var set = new HashSet<int>(cells);
        var seen = new HashSet<long>();
        var lines = new List<List<float>>();
        long nc = fence.Length;
        long Edge(int a, int b) => Math.Min(a, b) * nc + Math.Max(a, b);
        var nb = new List<int>(4);
        void Neighbours(int c)
        {
            nb.Clear();
            int x = c % n;
            if (x + 1 < n && set.Contains(c + 1)) nb.Add(c + 1);
            if (x > 0 && set.Contains(c - 1)) nb.Add(c - 1);
            if (set.Contains(c + n)) nb.Add(c + n);
            if (set.Contains(c - n)) nb.Add(c - n);
        }
        void Walk(int start, int next)
        {
            var chain = new List<int> { start };
            int prev = start, cur = next;
            seen.Add(Edge(prev, cur));
            for (;;)
            {
                chain.Add(cur);
                Neighbours(cur);
                if (nb.Count != 2) break;
                int nx = nb[0] == prev ? nb[1] : nb[0];
                if (seen.Contains(Edge(cur, nx))) break;
                seen.Add(Edge(cur, nx));
                prev = cur; cur = nx;
            }
            var pts = new List<float>(chain.Count * 2);
            foreach (int c in chain) { pts.Add(c % n + 0.5f); pts.Add(c / n + 0.5f); }
            lines.Add(Simplify(pts, 0.75f));
        }
        for (int pass = 0; pass < 2; pass++)
            foreach (int c in cells)
            {
                Neighbours(c);
                if (nb.Count == 0 && pass == 0) { lines.Add(new List<float> { c % n + 0.5f, c / n + 0.5f }); continue; }
                if (pass == 0 && nb.Count == 2) continue; // chain interiors are reached from their ends
                var mine = nb.ToArray();
                foreach (int o in mine) if (!seen.Contains(Edge(c, o))) Walk(c, o);
            }
        return lines;
    }

    private static List<float> Simplify(List<float> pts, float tol)
    {
        int n = pts.Count / 2;
        if (n <= 2) return pts;
        var keep = new bool[n];
        keep[0] = keep[n - 1] = true;
        var stack = new Stack<(int, int)>();
        stack.Push((0, n - 1));
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();
            float ax = pts[2 * a], ay = pts[2 * a + 1], bx = pts[2 * b], by = pts[2 * b + 1];
            float len = MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            if (len == 0) len = 1;
            int worst = -1;
            float wd = tol;
            for (int i = a + 1; i < b; i++)
            {
                float d = MathF.Abs((bx - ax) * (ay - pts[2 * i + 1]) - (ax - pts[2 * i]) * (by - ay)) / len;
                if (d > wd) { wd = d; worst = i; }
            }
            if (worst >= 0) { keep[worst] = true; stack.Push((a, worst)); stack.Push((worst, b)); }
        }
        var outp = new List<float>();
        for (int i = 0; i < n; i++) if (keep[i]) { outp.Add(pts[2 * i]); outp.Add(pts[2 * i + 1]); }
        return outp;
    }
}

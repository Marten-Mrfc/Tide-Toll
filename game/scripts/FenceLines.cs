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

    /// <summary>One gate as the toy marks it (ink.ts gateMark): its centre and the fence's direction there, in cells.</summary>
    public sealed class GateMark
    {
        public float X, Y, Ux = 1, Uy, Half;
        public int Cells;
    }

    /// <summary>The toy's gateMark: centre of the gate's cells, the principal axis of the fence cells round it, the half-opening.</summary>
    public static GateMark Mark(ReadOnlySpan<byte> fence, int n, int a, int b)
    {
        float x = a % n + 0.5f, y = a / n + 0.5f;
        int cells = 1;
        if (b >= 0) { x = (x + b % n + 0.5f) / 2f; y = (y + b / n + 0.5f) / 2f; cells = 2; }
        double sxx = 0, syy = 0, sxy = 0;
        int k = 0, cx = (int)MathF.Floor(x), cy = (int)MathF.Floor(y);
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int vx = cx + dx, vy = cy + dy;
                if (vx < 0 || vy < 0 || vx >= n || vy >= n || fence[vy * n + vx] == 0) continue;
                double ox = vx + 0.5 - x, oy = vy + 0.5 - y;
                sxx += ox * ox; syy += oy * oy; sxy += ox * oy; k++;
            }
        double ang = k > 1 ? 0.5 * Math.Atan2(2 * sxy, sxx - syy) : 0;
        return new GateMark { X = x, Y = y, Ux = (float)Math.Cos(ang), Uy = (float)Math.Sin(ang), Half = cells > 1 ? 1.05f : 0.9f, Cells = cells };
    }

    /// <summary>
    /// The toy's cutForGates: cuts each gate's opening out of a fence polyline and moves the gate onto the rail (its
    /// centre to the nearest point, its direction to the rail's). Pieces carry whether their start and end are gate posts.
    /// </summary>
    public static List<(List<float> pts, bool a, bool b)> Cut(List<float> ln, List<GateMark> gates)
    {
        int n = ln.Count / 2;
        float[] acc = new float[n];
        for (int i = 1; i < n; i++) acc[i] = acc[i - 1] + MathF.Sqrt(Sq(ln[2 * i] - ln[2 * i - 2]) + Sq(ln[2 * i + 1] - ln[2 * i - 1]));
        float total = acc[n - 1];
        var cuts = new List<(float, float)>();
        foreach (var g in gates)
        {
            float best = float.MaxValue, at = 0, px = g.X, py = g.Y, dx = g.Ux, dy = g.Uy;
            for (int i = 0; i + 1 < n; i++)
            {
                float ax = ln[2 * i], ay = ln[2 * i + 1], bx = ln[2 * i + 2], by = ln[2 * i + 3];
                float l2 = Sq(bx - ax) + Sq(by - ay);
                if (l2 <= 0) l2 = 1e-9f;
                float t = Math.Clamp(((g.X - ax) * (bx - ax) + (g.Y - ay) * (by - ay)) / l2, 0f, 1f);
                float d = MathF.Sqrt(Sq(ax + (bx - ax) * t - g.X) + Sq(ay + (by - ay) * t - g.Y));
                if (d < best)
                {
                    best = d; at = acc[i] + t * (acc[i + 1] - acc[i]);
                    px = ax + (bx - ax) * t; py = ay + (by - ay) * t;
                    float l = MathF.Sqrt(l2); dx = (bx - ax) / l; dy = (by - ay) / l;
                }
            }
            if (n > 1 && best < 0.9f)
            {
                cuts.Add((at - g.Half, at + g.Half));
                g.X = px; g.Y = py; g.Ux = dx; g.Uy = dy;
            }
        }
        var pieces = new List<(List<float>, bool, bool)>();
        if (cuts.Count == 0) { pieces.Add((ln, false, false)); return pieces; }
        cuts.Sort((p, q) => p.Item1.CompareTo(q.Item1));
        void PointAt(float s, List<float> outp)
        {
            int i = 0;
            while (i + 2 < n && acc[i + 1] < s) i++;
            float t = acc[i + 1] > acc[i] ? (s - acc[i]) / (acc[i + 1] - acc[i]) : 0;
            outp.Add(ln[2 * i] + (ln[2 * i + 2] - ln[2 * i]) * t);
            outp.Add(ln[2 * i + 1] + (ln[2 * i + 3] - ln[2 * i + 1]) * t);
        }
        float s0 = 0;
        bool startGate = false;
        void Emit(float s1, bool endGate)
        {
            if (s1 - s0 <= 0.05f) return;
            var pts = new List<float>();
            PointAt(s0, pts);
            for (int i = 1; i < n - 1; i++) if (acc[i] > s0 + 1e-6f && acc[i] < s1 - 1e-6f) { pts.Add(ln[2 * i]); pts.Add(ln[2 * i + 1]); }
            PointAt(s1, pts);
            pieces.Add((pts, startGate, endGate));
        }
        foreach (var (c0, c1) in cuts)
        {
            if (c1 <= s0) continue;
            Emit(Math.Max(s0, Math.Min(total, c0)), true);
            s0 = Math.Max(s0, c1); startGate = true;
        }
        if (s0 < total) Emit(total, false);
        return pieces;
    }

    private static float Sq(float v) => v * v;

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

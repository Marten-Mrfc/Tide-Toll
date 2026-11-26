using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

public enum RiverPlotKind : byte { Landing = 0, Weaver = 1, Pasture = 2, Cottage = 3 }

/// <summary>Kind of a river link: a lane on one bank, or one of the four crossings.</summary>
public enum RiverLinkKind : byte { Lane = 0, Ford = 1, Ferry = 2, Footbridge = 3, Drawbridge = 4 }

/// <summary>Fixed river point ids (RiverPlan.Points index). Cottage doors follow from CottageNear0.</summary>
public static class RiverPoint
{
    public const int Bank = 0, Landing = 1, Weaver = 2, Pasture = 3, FordNear = 4, FordFar = 5, FerryNear = 6, FerryFar = 7,
        FootNear = 8, FootFar = 9, DrawNear = 10, DrawFar = 11, FarHub = 12, CottageNear0 = 13, CottageNear1 = 14, CottageFar0 = 15, CottageFar1 = 16, Count = 17;
}

public sealed class RiverPlot
{
    public int Id;
    public RiverPlotKind Kind;
    public int X, Y, W, H;       // footprint cells
    public int Point;            // its door: RiverPlan.Px/Py[Point]
    public int Level;            // town level that lays it out (17 = the age's first)
    public bool FarBank;
    public bool Contains(int x, int y) => x >= X && y >= Y && x < X + W && y < Y + H;
}

public sealed class RiverLink
{
    public int Id;
    public RiverLinkKind Kind;
    public int A, B;             // river point ids (A on the near bank for crossings)
    public double[] Px = Array.Empty<double>(), Py = Array.Empty<double>(); // polyline, map cells (already at cell centres: draw as is)
    public double[] Cum = Array.Empty<double>();
    public double Length;
    public int Level;

    /// <summary>Point at a distance from A along the polyline, with the heading towards B.</summary>
    public void At(double d, out double x, out double y, out double heading)
    {
        int n = Px.Length;
        if (d <= 0) { x = Px[0]; y = Py[0]; heading = Math.Atan2(Py[1] - Py[0], Px[1] - Px[0]); return; }
        int i = 1;
        while (i < n - 1 && Cum[i] < d) i++;
        double seg = Cum[i] - Cum[i - 1];
        double f = seg > 1e-9 ? Math.Clamp((d - Cum[i - 1]) / seg, 0, 1) : 1;
        x = Px[i - 1] + (Px[i] - Px[i - 1]) * f; y = Py[i - 1] + (Py[i] - Py[i - 1]) * f;
        heading = Math.Atan2(Py[i] - Py[i - 1], Px[i] - Px[i - 1]);
    }
}

public sealed class RiverLevelDef
{
    public int Number;           // town level (17..28)
    public string Gift = "";
    public double RenownAbove;   // minutes of the ring-time coin rate above the renown held at the ring (Town.RiverLevelRenown gives renown)
    public int[] Plots = Array.Empty<int>();
    public int[] Links = Array.Empty<int>();
}

/// <summary>
/// The plan of the second age, built from the seed's map. Every position is in map cells, already at cell centres
/// (unlike the first-age Road.Xs/Ys, do not add 0.5). The near bank is the town's (west) bank.
/// </summary>
public sealed class RiverPlan
{
    public int Seed;
    public int BankNode;                 // first-age road node where the bank road starts
    public double[] Px = new double[RiverPoint.Count], Py = new double[RiverPoint.Count];
    public bool[] PointFar = new bool[RiverPoint.Count];
    public RiverPlot[] Plots = Array.Empty<RiverPlot>();
    public RiverLink[] Links = Array.Empty<RiverLink>();
    public RiverLevelDef[] Levels = Array.Empty<RiverLevelDef>();
    public int LandingRow, FordRow, FerryRow, FootRow, DrawRow;
    /// <summary>Boats' course: river centre x for every map row (boats come from the south edge, row 127, and go back the same way).</summary>
    public double[] CourseX = Array.Empty<double>();
    public double[] BerthX = new double[2], BerthY = new double[2];
    public double[] WaitY = new double[3];
    public int LinkOf(RiverLinkKind k) { for (int i = 0; i < Links.Length; i++) if (Links[i].Kind == k) return i; return -1; }
    public RiverPlot PlotOf(RiverPlotKind k) { foreach (var p in Plots) if (p.Kind == k) return p; return Plots[0]; }
}

public static class TownRiverPlanner
{
    public static RiverPlan Build(TownPlan tp)
    {
        var map = tp.Map;
        int n = map.Size;
        var rp = new RiverPlan { Seed = tp.Seed };
        int EdgeW(int y) { for (int x = 0; x < n; x++) if (map.Water[y * n + x] != 0) return x; return n / 2; }
        int EdgeE(int y) { for (int x = n - 1; x >= 0; x--) if (map.Water[y * n + x] != 0) return x + 1; return n / 2 + 1; }

        int yL = Math.Clamp(tp.WindowY + tp.WindowH / 2, 18, n - 20);
        rp.LandingRow = yL;
        rp.FootRow = yL - 14; rp.FerryRow = yL - 8; rp.DrawRow = yL + 11;
        // The ford: the map's own ford row when it is clear of the other crossings and the landing's plots, else the nearest clear row.
        int fy = map.FordY;
        bool Clear(int y) => y >= 3 && y < n - 3 && Math.Abs(y - rp.FootRow) > 2 && Math.Abs(y - rp.FerryRow) > 2 && Math.Abs(y - rp.DrawRow) > 2 && (y < yL - 5 || y > yL + 9);
        if (!Clear(fy)) { for (int d = 1; d < n; d++) { if (Clear(fy - d)) { fy -= d; break; } if (Clear(fy + d)) { fy += d; break; } } }
        rp.FordRow = fy;

        rp.CourseX = new double[n];
        for (int y = 0; y < n; y++) rp.CourseX[y] = (EdgeW(y) + EdgeE(y)) * 0.5;
        for (int k = 0; k < 2; k++) { int by = yL - 1 + k * 3; rp.BerthX[k] = EdgeW(by) + 1.0; rp.BerthY[k] = by + 0.5; }
        for (int k = 0; k < 3; k++) rp.WaitY[k] = yL + 4.5 + k * 2;

        // Plots.
        var plots = new List<RiverPlot>();
        RiverPlot P(RiverPlotKind k, int x, int y, int w, int h, int point, int level, bool far)
        {
            var p = new RiverPlot { Id = plots.Count, Kind = k, X = x, Y = y, W = w, H = h, Point = point, Level = level, FarBank = far };
            plots.Add(p); return p;
        }
        int ew = EdgeW(yL);
        for (int y = yL - 1; y <= yL; y++) ew = Math.Min(ew, EdgeW(y));
        var landing = P(RiverPlotKind.Landing, ew - 3, yL - 1, 3, 2, RiverPoint.Landing, 17, false);
        int wy = yL + 4, wx = MinEdgeW(EdgeW, wy, wy + 2) - 4;
        var weaver = P(RiverPlotKind.Weaver, wx, wy, 3, 3, RiverPoint.Weaver, 21, false);
        var cn0 = P(RiverPlotKind.Cottage, MinEdgeW(EdgeW, yL - 4, yL - 3) - 3, yL - 4, 2, 2, RiverPoint.CottageNear0, 18, false);
        var cn1 = P(RiverPlotKind.Cottage, MinEdgeW(EdgeW, yL + 8, yL + 9) - 3, yL + 8, 2, 2, RiverPoint.CottageNear1, 18, false);
        int py0 = yL - 2, pe = MaxEdgeE(EdgeE, py0, py0 + 3);
        var pasture = P(RiverPlotKind.Pasture, pe + 4, py0, 5, 4, RiverPoint.Pasture, 20, true);
        var cf0 = P(RiverPlotKind.Cottage, MaxEdgeE(EdgeE, yL - 10, yL - 9) + 3, yL - 10, 2, 2, RiverPoint.CottageFar0, 24, true);
        var cf1 = P(RiverPlotKind.Cottage, MaxEdgeE(EdgeE, yL + 6, yL + 7) + 3, yL + 6, 2, 2, RiverPoint.CottageFar1, 24, true);
        rp.Plots = plots.ToArray();

        // Points: doors face the river (near bank: east side; far bank: west side), crossing heads one cell off the water.
        void Pt(int id, double x, double y, bool far) { rp.Px[id] = x; rp.Py[id] = y; rp.PointFar[id] = far; }
        // Bank node: the first-age node nearest the landing.
        int bn = 0; double bd = double.MaxValue;
        foreach (var nd in tp.Nodes)
        {
            double d = Math.Abs(nd.X - landing.X) + Math.Abs(nd.Y - landing.Y);
            if (d < bd) { bd = d; bn = nd.Id; }
        }
        rp.BankNode = bn;
        Pt(RiverPoint.Bank, tp.Nodes[bn].X + 0.5, tp.Nodes[bn].Y + 0.5, false);
        Pt(RiverPoint.Landing, landing.X - 0.5, landing.Y + 1.5, false);
        Pt(RiverPoint.Weaver, weaver.X - 0.5, weaver.Y + 1.5, false);
        Pt(RiverPoint.CottageNear0, cn0.X - 0.5, cn0.Y + 1.5, false);
        Pt(RiverPoint.CottageNear1, cn1.X - 0.5, cn1.Y + 0.5, false);
        Pt(RiverPoint.Pasture, pasture.X - 0.5, pasture.Y + 2.5, true);
        Pt(RiverPoint.CottageFar0, cf0.X - 0.5, cf0.Y + 1.5, true);
        Pt(RiverPoint.CottageFar1, cf1.X - 0.5, cf1.Y + 0.5, true);
        void Heads(int near, int far, int row) { Pt(near, EdgeW(row) - 0.5, row + 0.5, false); Pt(far, EdgeE(row) + 0.5, row + 0.5, true); }
        Heads(RiverPoint.FordNear, RiverPoint.FordFar, rp.FordRow);
        Heads(RiverPoint.FerryNear, RiverPoint.FerryFar, rp.FerryRow);
        Heads(RiverPoint.FootNear, RiverPoint.FootFar, rp.FootRow);
        Heads(RiverPoint.DrawNear, RiverPoint.DrawFar, rp.DrawRow);
        int hy = yL + 3;
        Pt(RiverPoint.FarHub, MaxEdgeE(EdgeE, hy, hy) + 2.5, hy + 0.5, true);

        // Lanes on each bank: shortest 4-connected paths of free cells (water and the plots of both ages blocked); crossings straight over the water.
        var blocked = new byte[n * n];
        for (int i = 0; i < n * n; i++) if (map.Water[i] != 0) blocked[i] = 1;
        foreach (var p in tp.Plots) for (int y = p.Y; y < p.Y + p.H; y++) for (int x = p.X; x < p.X + p.W; x++) blocked[y * n + x] = 1;
        foreach (var p in rp.Plots) for (int y = p.Y; y < p.Y + p.H; y++) for (int x = p.X; x < p.X + p.W; x++) if (x >= 0 && y >= 0 && x < n && y < n) blocked[y * n + x] = 1;
        var links = new List<RiverLink>();
        void Lane(int a, int b, int level, RiverLinkKind kind = RiverLinkKind.Lane)
        {
            var l = new RiverLink { Id = links.Count, Kind = kind, A = a, B = b, Level = level };
            var xs = new List<double> { rp.Px[a] }; var ys = new List<double> { rp.Py[a] };
            if (kind == RiverLinkKind.Lane)
            {
                var r = Bfs(n, blocked, (int)Math.Floor(rp.Px[a]), (int)Math.Floor(rp.Py[a]), (int)Math.Floor(rp.Px[b]), (int)Math.Floor(rp.Py[b]));
                // Cell centres, 4-connected, keeping only the bends.
                if (r != null)
                    for (int i = 1; i < r.Count - 1; i++)
                    {
                        bool bend = (r[i].X - r[i - 1].X) != (r[i + 1].X - r[i].X) || (r[i].Y - r[i - 1].Y) != (r[i + 1].Y - r[i].Y);
                        if (bend) { xs.Add(r[i].X + 0.5); ys.Add(r[i].Y + 0.5); }
                    }
            }
            xs.Add(rp.Px[b]); ys.Add(rp.Py[b]);
            l.Px = xs.ToArray(); l.Py = ys.ToArray(); l.Cum = new double[l.Px.Length];
            for (int i = 1; i < l.Px.Length; i++) l.Cum[i] = l.Cum[i - 1] + Math.Sqrt((l.Px[i] - l.Px[i - 1]) * (l.Px[i] - l.Px[i - 1]) + (l.Py[i] - l.Py[i - 1]) * (l.Py[i] - l.Py[i - 1]));
            l.Length = Math.Max(0.5, l.Cum[^1]);
            links.Add(l);
        }
        Lane(RiverPoint.Bank, RiverPoint.Landing, 17);
        Lane(RiverPoint.Landing, RiverPoint.CottageNear0, 18);
        Lane(RiverPoint.Landing, RiverPoint.CottageNear1, 18);
        Lane(RiverPoint.Landing, RiverPoint.FordNear, 19);
        Lane(RiverPoint.FordNear, RiverPoint.FordFar, 19, RiverLinkKind.Ford);
        Lane(RiverPoint.FordFar, RiverPoint.FarHub, 19);
        Lane(RiverPoint.FarHub, RiverPoint.Pasture, 20);
        Lane(RiverPoint.Landing, RiverPoint.Weaver, 21);
        Lane(RiverPoint.Landing, RiverPoint.FerryNear, 22);
        Lane(RiverPoint.FerryNear, RiverPoint.FerryFar, 22, RiverLinkKind.Ferry);
        Lane(RiverPoint.FerryFar, RiverPoint.FarHub, 22);
        Lane(RiverPoint.FarHub, RiverPoint.CottageFar0, 24);
        Lane(RiverPoint.FarHub, RiverPoint.CottageFar1, 24);
        Lane(RiverPoint.Landing, RiverPoint.FootNear, 25);
        Lane(RiverPoint.FootNear, RiverPoint.FootFar, 25, RiverLinkKind.Footbridge);
        Lane(RiverPoint.FootFar, RiverPoint.FarHub, 25);
        Lane(RiverPoint.Landing, RiverPoint.DrawNear, 27);
        Lane(RiverPoint.DrawNear, RiverPoint.DrawFar, 27, RiverLinkKind.Drawbridge);
        Lane(RiverPoint.DrawFar, RiverPoint.FarHub, 27);
        rp.Links = links.ToArray();

        var levels = new RiverLevelDef[TownRulesRiver.LevelGift.Length];
        for (int i = 0; i < levels.Length; i++)
        {
            int num = tp.MaxLevel + 1 + i;
            var pl = new List<int>(); var ll = new List<int>();
            foreach (var p in rp.Plots) if (p.Level == num) pl.Add(p.Id);
            foreach (var l in rp.Links) if (l.Level == num) ll.Add(l.Id);
            levels[i] = new RiverLevelDef { Number = num, Gift = TownRulesRiver.LevelGift[i], RenownAbove = TownRulesRiver.LevelRenown[i], Plots = pl.ToArray(), Links = ll.ToArray() };
        }
        rp.Levels = levels;
        return rp;
    }

    /// <summary>Shortest 4-connected path of free cells (start and end cells may be anything); null when none.</summary>
    private static List<(int X, int Y)>? Bfs(int n, byte[] blocked, int sx, int sy, int tx, int ty)
    {
        var from = new int[n * n];
        Array.Fill(from, -1);
        var q = new Queue<int>();
        int s0 = sy * n + sx, t0 = ty * n + tx;
        from[s0] = s0; q.Enqueue(s0);
        Span<int> dx = stackalloc int[] { 1, -1, 0, 0 }, dy = stackalloc int[] { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            int c = q.Dequeue();
            if (c == t0) break;
            int cx = c % n, cy = c / n;
            // Prefer the direction towards the target first, so paths are tidy.
            for (int k = 0; k < 4; k++)
            {
                int x = cx + dx[k], y = cy + dy[k];
                if (x < 0 || y < 0 || x >= n || y >= n) continue;
                int i = y * n + x;
                if (from[i] >= 0) continue;
                if (blocked[i] != 0 && i != t0) continue;
                from[i] = c; q.Enqueue(i);
            }
        }
        if (from[t0] < 0) return null;
        var path = new List<(int X, int Y)>();
        for (int c = t0; ; c = from[c]) { path.Add((c % n, c / n)); if (c == s0) break; }
        path.Reverse();
        return path;
    }

    private static int MinEdgeW(Func<int, int> e, int a, int b) { int m = int.MaxValue; for (int y = a; y <= b; y++) m = Math.Min(m, e(y)); return m; }
    private static int MaxEdgeE(Func<int, int> e, int a, int b) { int m = int.MinValue; for (int y = a; y <= b; y++) m = Math.Max(m, e(y)); return m; }
}

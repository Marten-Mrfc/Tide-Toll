using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

/// <summary>
/// Lays out the first-age town for a seed: a fixed village template (plots and which doors each level's roads join)
/// placed on the west bank, cottages nudged by the seed, the template flipped north-south on half the seeds, and every
/// road routed by A* over the map grid (around water, plots and earlier roads; the map's smooth noise bends the lanes),
/// then simplified to a polyline of grid points.
/// </summary>
public static class TownPlanner
{
    public const int TemplateW = 33, TemplateH = 36;
    private const int RiverMargin = 6;   // cells kept free between the town window and the water

    private readonly struct TPlot
    {
        public readonly string Name; public readonly PlotKind Kind; public readonly int X, Y, W, H, Level; public readonly int Offer;
        public TPlot(string n, PlotKind k, int x, int y, int w, int h, int level, int offer = -1)
        { Name = n; Kind = k; X = x; Y = y; W = w; H = h; Level = level; Offer = offer; }
    }

    // Offer ids: 0 second bucket, 1 market stall, 2 second field, 3 mill.
    private static readonly TPlot[] Template =
    {
        new("c1", PlotKind.Cottage, 9, 12, 3, 3, 1),
        new("well", PlotKind.Well, 16, 17, 2, 2, 1),
        new("c2", PlotKind.Cottage, 5, 21, 3, 3, 2),
        new("bell", PlotKind.BellGreen, 20, 12, 3, 3, 2),
        new("f1", PlotKind.Field, 2, 2, 9, 6, 3),
        new("c3", PlotKind.Cottage, 11, 26, 3, 3, 4),
        new("market", PlotKind.Market, 27, 11, 5, 4, 5, 1),
        new("c4", PlotKind.Cottage, 22, 24, 3, 3, 7),
        new("c5", PlotKind.Cottage, 2, 13, 3, 3, 8),
        new("f2", PlotKind.Field, 13, 1, 9, 6, 9, 2),
        new("c6", PlotKind.Cottage, 17, 31, 3, 3, 11),
        new("mill", PlotKind.Mill, 25, 2, 4, 4, 13, 3),
        new("c7", PlotKind.Cottage, 29, 20, 3, 3, 14),
    };

    private static readonly (int Level, string A, string B)[] TemplateRoads =
    {
        (1, "c1", "well"),
        (2, "c2", "well"), (2, "bell", "well"),
        (3, "f1", "c1"),
        (4, "c3", "c2"),
        (5, "market", "bell"),
        (6, "c3", "well"),
        (7, "c4", "well"), (8, "c5", "c1"),
        (9, "f2", "f1"), (10, "f2", "market"),
        (11, "c6", "c3"), (12, "c6", "c4"),
        (13, "mill", "market"),
        (14, "c7", "market"), (15, "c7", "c4"),
        (16, "c5", "c2"),
    };

    private static readonly string[] Gifts =
    {
        "The start: a cottage, the well and a trail",
        "A cottage, a lane and the bell green",
        "The first field and its track",
        "A cottage, a lane and a second bucket for the well",
        "The market square, with a stall to buy",
        "A shortcut that closes a loop",
        "A cottage and its lane",
        "A cottage by the first field",
        "A second field to buy, its track and cobbles",
        "A track from the second field to the market",
        "A cottage and its lane",
        "A lane that joins the south cottages",
        "The mill to buy and its road",
        "A cottage by the market",
        "A lane that closes the east loop",
        "The town charter and a last lane",
    };

    public static TownPlan Build(int seed) => Build(seed, MapGen.Generate(seed));

    public static TownPlan Build(int seed, MapData map)
    {
        int n = map.Size;
        var rng = new Mulberry32(unchecked((uint)(seed * 40503 + 977)));
        bool flip = rng.NextDouble() < 0.5;

        // Water and its margin are off limits.
        var blocked = new byte[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                if (x < 1 || y < 1 || x >= n - 1 || y >= n - 1 || Math.Abs(x + 0.5 - map.RiverX[y]) < map.RiverHalfW + 2.5)
                    blocked[y * n + x] = 1;

        // Window: the rows where the west bank is widest, near the middle of the map.
        int bestOy = 10; double bestScore = double.NegativeInfinity; int bestEdge = 0;
        for (int oy = 6; oy + TemplateH + 6 < n; oy++)
        {
            double edge = double.PositiveInfinity;
            for (int y = oy - 2; y < oy + TemplateH + 2; y++) edge = Math.Min(edge, map.RiverX[y] - map.RiverHalfW);
            double score = edge - 0.08 * Math.Abs(oy + TemplateH / 2.0 - n / 2.0);
            if (score > bestScore) { bestScore = score; bestOy = oy; bestEdge = (int)Math.Floor(edge); }
        }
        int ox = Math.Max(3, bestEdge - RiverMargin - TemplateW);
        int oyy = bestOy;

        var plan = new TownPlan { Seed = seed, Map = map, WindowX = ox, WindowY = oyy, WindowW = TemplateW, WindowH = TemplateH };

        // Plots, with doors facing the well.
        var plots = new List<Plot>();
        var names = new Dictionary<string, int>();
        foreach (var t in Template)
        {
            int ty = flip ? TemplateH - t.Y - t.H : t.Y;
            var p = new Plot { Id = plots.Count, Kind = t.Kind, X = ox + t.X, Y = oyy + ty, W = t.W, H = t.H, Level = t.Level, Offer = t.Offer };
            plots.Add(p);
            names[t.Name] = p.Id;
        }
        var well = plots[names["well"]];
        foreach (var p in plots) SetDoor(p, flip, well);
        // Nudge cottages by the seed when the nudge keeps every plot clear.
        foreach (var p in plots)
        {
            if (p.Kind != PlotKind.Cottage || p.Level == 1) continue;
            int dx = (int)Math.Floor(rng.NextDouble() * 3) - 1, dy = (int)Math.Floor(rng.NextDouble() * 3) - 1;
            p.X += dx; p.Y += dy; SetDoor(p, flip, well);
            if (!PlotFits(p, plots, blocked, n)) { p.X -= dx; p.Y -= dy; SetDoor(p, flip, well); }
        }
        plan.Plots = plots.ToArray();

        // Nodes: one per plot door.
        var nodes = new List<RoadNode>();
        foreach (var p in plots) { p.Node = nodes.Count; nodes.Add(new RoadNode { Id = nodes.Count, X = p.DoorX, Y = p.DoorY }); }

        // Plot cells block roads; cells next to a plot cost more.
        var penalty = new float[n * n];
        foreach (var p in plots)
            for (int y = p.Y - 1; y <= p.Y + p.H; y++)
                for (int x = p.X - 1; x <= p.X + p.W; x++)
                {
                    if (x < 0 || y < 0 || x >= n || y >= n) continue;
                    if (p.Contains(x, y)) blocked[y * n + x] = 1; else penalty[y * n + x] = 2.5f;
                }
        foreach (var nd in nodes) { blocked[nd.Y * n + nd.X] = 0; penalty[nd.Y * n + nd.X] = 0; }

        var roadCell = new byte[n * n];
        var roads = new List<Road>();
        var router = new Router(n, map.Noise, blocked, penalty, roadCell);
        foreach (var (lv, a, b) in TemplateRoads)
        {
            var na = nodes[plots[names[a]].Node]; var nb = nodes[plots[names[b]].Node];
            var pts = router.Route(na.X, na.Y, nb.X, nb.Y, avoidRoads: true);
            if (pts == null) { pts = router.Route(na.X, na.Y, nb.X, nb.Y, avoidRoads: false); plan.RoadCrossings++; }
            if (pts == null) throw new InvalidOperationException($"town plan: no way for road {a}-{b} on seed {seed}");
            var r = MakeRoad(roads.Count, na.Id, nb.Id, lv, pts);
            roads.Add(r);
            router.Mark(r);
        }
        plan.Roads = roads.ToArray();
        foreach (var nd in nodes)
        {
            var list = new List<int>();
            foreach (var r in roads) if (r.A == nd.Id || r.B == nd.Id) list.Add(r.Id);
            nd.Roads = list.ToArray();
        }
        plan.Nodes = nodes.ToArray();

        plan.Offers = new[]
        {
            new Offer { Id = 0, Kind = OfferKind.SecondBucket, Name = "Second bucket", Price = TownRules.SecondBucketPrice, Level = 4, Plot = names["well"] },
            new Offer { Id = 1, Kind = OfferKind.MarketStall, Name = "Market stall", Price = TownRules.MarketStallPrice, Level = 5, Plot = names["market"] },
            new Offer { Id = 2, Kind = OfferKind.SecondField, Name = "Second field", Price = TownRules.SecondFieldPrice, Level = 9, Plot = names["f2"] },
            new Offer { Id = 3, Kind = OfferKind.Mill, Name = "Mill", Price = TownRules.MillPrice, Level = 13, Plot = names["mill"] },
        };

        int nl = TownRules.LevelRenown.Length;
        var levels = new LevelDef[nl];
        for (int i = 0; i < nl; i++)
        {
            int lv = i + 1;
            var lp = new List<int>(); foreach (var p in plots) if (p.Level == lv) lp.Add(p.Id);
            var lr = new List<int>(); foreach (var r in roads) if (r.Level == lv) lr.Add(r.Id);
            var lo = new List<int>(); foreach (var o in plan.Offers) if (o.Level == lv) lo.Add(o.Id);
            levels[i] = new LevelDef
            {
                Number = lv, Renown = TownRules.LevelRenown[i], Gift = Gifts[i], Plots = lp.ToArray(), Roads = lr.ToArray(), Offers = lo.ToArray(),
                Charter = lv == nl, UnlocksBell = lv == 2, UnlocksCobbles = lv == TownRules.CobblesFromLevel,
            };
        }
        plan.Levels = levels;
        return plan;
    }

    /// <summary>The door sits in the middle of the side facing the well (the town's heart); the well's own door faces the town side.</summary>
    private static void SetDoor(Plot p, bool flip, Plot? well)
    {
        double dx = 0, dy = flip ? -1 : 1;
        if (well != null && well != p)
        {
            dx = (well.X + well.W / 2.0) - (p.X + p.W / 2.0);
            dy = (well.Y + well.H / 2.0) - (p.Y + p.H / 2.0);
        }
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            p.DoorX = dx > 0 ? p.X + p.W : p.X - 1;
            p.DoorY = p.Y + p.H / 2;
        }
        else
        {
            p.DoorX = p.X + p.W / 2;
            p.DoorY = dy > 0 ? p.Y + p.H : p.Y - 1;
        }
    }

    private static bool PlotFits(Plot p, List<Plot> plots, byte[] blocked, int n)
    {
        for (int y = p.Y - 1; y <= p.Y + p.H; y++)
            for (int x = p.X - 1; x <= p.X + p.W; x++)
            {
                if (x < 1 || y < 1 || x >= n - 1 || y >= n - 1 || blocked[y * n + x] != 0) return false;
                foreach (var q in plots)
                {
                    if (q == p) continue;
                    if (x >= q.X - 1 && y >= q.Y - 1 && x <= q.X + q.W && y <= q.Y + q.H) return false;
                    if (x == q.DoorX && y == q.DoorY) return false;
                }
            }
        return true;
    }

    private static Road MakeRoad(int id, int a, int b, int level, List<(int X, int Y)> pts)
    {
        var r = new Road { Id = id, A = a, B = b, Level = level, Xs = new int[pts.Count], Ys = new int[pts.Count], Cum = new double[pts.Count] };
        for (int i = 0; i < pts.Count; i++)
        {
            r.Xs[i] = pts[i].X; r.Ys[i] = pts[i].Y;
            if (i > 0) r.Cum[i] = r.Cum[i - 1] + Math.Sqrt((double)(pts[i].X - pts[i - 1].X) * (pts[i].X - pts[i - 1].X) + (double)(pts[i].Y - pts[i - 1].Y) * (pts[i].Y - pts[i - 1].Y));
        }
        r.Length = r.Cum[^1];
        return r;
    }

    /// <summary>All grid cells a road covers (4-connected line per segment), for checks and for painting.</summary>
    public static List<int> RoadCells(Road r, int size)
    {
        var cells = new List<int>();
        var g = new Grid(size, size);
        for (int i = 0; i + 1 < r.Xs.Length; i++) g.Line4(r.Xs[i], r.Ys[i], r.Xs[i + 1], r.Ys[i + 1], cells);
        return cells;
    }
}

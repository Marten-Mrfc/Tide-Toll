using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

public enum PlotKind : byte { Cottage = 0, Well = 1, Field = 2, Market = 3, Mill = 4, BellGreen = 5 }

public enum OfferKind : byte { SecondBucket = 0, MarketStall = 1, SecondField = 2, Mill = 3 }

/// <summary>A planned plot: footprint (map cells X..X+W-1, Y..Y+H-1), its door cell and the road node at the door.</summary>
public sealed class Plot
{
    public int Id;
    public PlotKind Kind;
    public int X, Y, W, H;
    public int DoorX, DoorY;
    public int Node;
    /// <summary>The level whose gift lays this plot out (1 = the start).</summary>
    public int Level;
    /// <summary>Offer that must be bought before it works (-1: works as soon as given).</summary>
    public int Offer = -1;
    public bool Contains(int x, int y) => x >= X && y >= Y && x < X + W && y < Y + H;
}

/// <summary>A road node: a grid point (cell) where roads end; every plot door is one.</summary>
public sealed class RoadNode
{
    public int Id;
    public int X, Y;
    public int[] Roads = Array.Empty<int>();
}

/// <summary>A road: polyline of grid cells from node A to node B with a stable id (its index).</summary>
public sealed class Road
{
    public int Id;
    public int A, B;
    public int[] Xs = Array.Empty<int>(), Ys = Array.Empty<int>();
    /// <summary>Distance along the road at each point, in cells (Cum[0] = 0, Cum[^1] = Length).</summary>
    public double[] Cum = Array.Empty<double>();
    public double Length;
    public int Level;
    /// <summary>Rules v2.1: the drawn road, a gentle curve through the grid route (same units as Xs/Ys: draw at +0.5).
    /// First point = node A, last = node B exactly. Villagers walk it; <see cref="Length"/> is its length.</summary>
    public double[] Px = Array.Empty<double>(), Py = Array.Empty<double>();
    /// <summary>Distance along the curve at each curve point (PCum[0] = 0, PCum[^1] = Length).</summary>
    public double[] PCum = Array.Empty<double>();

    /// <summary>Map position (cell units, cell centres at +0.5) and heading (radians, atan2(dy, dx)) at distance d from A,
    /// on the curve (rules v2.1). Binary search: cost grows with the log of the number of points, no allocation.</summary>
    public void At(double d, out double x, out double y, out double heading)
    {
        if (d <= 0) d = 0;
        if (d >= Length) d = Length;
        if (Px.Length < 2)
        {
            int s0 = 0;
            while (s0 < Cum.Length - 2 && Cum[s0 + 1] < d) s0++;
            double seg0 = Cum[s0 + 1] - Cum[s0];
            double t0 = seg0 > 0 ? (d - Cum[s0]) / seg0 : 0;
            double ex = Xs[s0 + 1] - Xs[s0], ey = Ys[s0 + 1] - Ys[s0];
            x = Xs[s0] + 0.5 + ex * t0; y = Ys[s0] + 0.5 + ey * t0; heading = Math.Atan2(ey, ex);
            return;
        }
        // Largest s with PCum[s] <= d, at most Px.Length - 2.
        int lo = 0, hi = PCum.Length - 2;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (PCum[mid] <= d) lo = mid; else hi = mid - 1;
        }
        int s = lo;
        double seg = PCum[s + 1] - PCum[s];
        double t = seg > 0 ? (d - PCum[s]) / seg : 0;
        double dx = Px[s + 1] - Px[s], dy = Py[s + 1] - Py[s];
        x = Px[s] + 0.5 + dx * t;
        y = Py[s] + 0.5 + dy * t;
        heading = Math.Atan2(dy, dx);
    }
}

public sealed class Offer
{
    public int Id;
    public OfferKind Kind;
    public string Name = "";
    public double Price;
    public int Level;
    public int Plot = -1;
}

public sealed class LevelDef
{
    public int Number;
    public double Renown;
    public string Gift = "";
    public int[] Plots = Array.Empty<int>();
    public int[] Roads = Array.Empty<int>();
    public int[] Offers = Array.Empty<int>();
    public bool Charter;
    public bool UnlocksBell;
    public bool UnlocksCobbles;
}

/// <summary>The whole first-age town for one seed. Built once by <see cref="TownPlanner.Build"/>; never changed afterwards.</summary>
public sealed class TownPlan
{
    public int Seed;
    public MapData Map = null!;
    public Plot[] Plots = Array.Empty<Plot>();
    public RoadNode[] Nodes = Array.Empty<RoadNode>();
    public Road[] Roads = Array.Empty<Road>();
    public LevelDef[] Levels = Array.Empty<LevelDef>();
    public Offer[] Offers = Array.Empty<Offer>();
    /// <summary>The window the first-age town fits in (map cells).</summary>
    public int WindowX, WindowY, WindowW, WindowH;
    /// <summary>Roads that had to cross another road's cells (0 on a good plan).</summary>
    public int RoadCrossings;

    public int MaxLevel => Levels.Length;
    public LevelDef Level(int n) => Levels[n - 1];
}

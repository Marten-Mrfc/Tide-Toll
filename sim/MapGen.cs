using System;
using System.Collections.Generic;

namespace TideToll.Sim;

/// <summary>
/// The toy's mapgen.ts: a meandering north-south river with a ford, a well, a field, a market across
/// the river and a hamlet of houses. Keeps the toy's exact order of random draws, so the same seed gives
/// the same map (proven against the toy on seeds 1 to 50 by the golden tests).
/// </summary>
public static class MapGen
{
    private static readonly int[] SizeW = { 3, 2, 9, 5 }; // house, well, field, market
    private static readonly int[] SizeH = { 3, 2, 6, 4 };

    public static MapData Generate(int seed, int size = 128)
    {
        var g = new Gen(seed, size);
        return g.Run();
    }

    /// <summary>Hash noise in [-1, 1], two separable box blurs of radius 3, normalised by its largest magnitude.</summary>
    internal static float[] SmoothNoise(int seed, int n)
    {
        int nc = n * n;
        var a = new float[nc];
        for (int i = 0; i < nc; i++) a[i] = (float)(Hash.Hash2(seed, i) * 2 - 1);
        var b = new float[nc];
        const int r = 3;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double s = 0;
                    int cnt = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int xx = pass == 0 ? x + k : x, yy = pass == 0 ? y : y + k;
                        if (xx >= 0 && yy >= 0 && xx < n && yy < n) { s += a[yy * n + xx]; cnt++; }
                    }
                    b[y * n + x] = (float)(s / cnt);
                }
            (a, b) = (b, a);
        }
        double mx = 1e-6;
        for (int i = 0; i < nc; i++) mx = Math.Max(mx, Math.Abs((double)a[i]));
        for (int i = 0; i < nc; i++) a[i] = (float)(a[i] / mx);
        return a;
    }

    private sealed class Gen
    {
        private readonly int _n;
        private readonly MapData _m;
        private Mulberry32 _r;
        private readonly byte[] _occupied;
        private readonly List<Building> _buildings = new();

        public Gen(int seed, int n)
        {
            _n = n;
            _m = new MapData(seed, n);
            _r = new Mulberry32(unchecked((uint)(seed * 7919 + 13)));
            _occupied = new byte[n * n];
        }

        private double R() => _r.NextDouble();

        public MapData Run()
        {
            int n = _n;
            var m = _m;
            // A north-south river that meanders: two sines of different period.
            double a1 = 5 + R() * 6, f1 = 0.035 + R() * 0.03, p1 = R() * 6.283;
            double a2 = 1.5 + R() * 2.5, f2 = 0.1 + R() * 0.05, p2 = R() * 6.283;
            double cx = 60 + R() * 8;
            for (int y = 0; y < n; y++) m.RiverX[y] = (float)(cx + a1 * Math.Sin(y * f1 + p1) + a2 * Math.Sin(y * f2 + p2));
            m.RiverHalfW = (3 + Math.Floor(R() * 3)) / 2; // 3 to 5 cells wide
            m.FordY = 40 + (int)Math.Floor(R() * 48);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double d = x + 0.5 - m.RiverX[y];
                    int i = y * n + x;
                    if (Math.Abs(d) <= m.RiverHalfW)
                    {
                        m.Water[i] = 1;
                        if (Math.Abs(y - m.FordY) <= 1) m.Ford[i] = 1;
                    }
                    else m.Bank[i] = (sbyte)(d < 0 ? -1 : 1);
                }

            // East or west for the main village is picked per seed so maps vary.
            int home = R() < 0.5 ? -1 : 1;
            int nHouses = 6 + (int)Math.Floor(R() * 3);
            // Destinations first so they always fit; the market is across the river.
            Place(BuildingKind.Well, home, 30, 46, 36, 84);
            bool north = R() < 0.5;
            // The toy's `||`: the wide fallback is only tried (and only draws) if the first fails.
            if (!Place(BuildingKind.Field, home, 8, 34, north ? 10 : 88, north ? 30 : 110))
                Place(BuildingKind.Field, home, 6, 40, 6, 116);
            Place(BuildingKind.Market, -home, 18, 40, 30, 92);
            int made = 0;
            int eastHouses = R() < 0.6 ? 1 : 0;
            // Houses cluster round the well; the field and market lie further out.
            var well = _buildings[0];
            int wx = home == 1 ? n - 1 - well.X - well.W : well.X, wy = well.Y;
            for (int i = 0; i < nHouses - eastHouses; i++)
                if (Place(BuildingKind.House, home, Math.Max(4, wx - 24), wx + 12, Math.Max(6, wy - 26), Math.Min(118, wy + 26))) made++;
            for (int i = 0; i < eastHouses; i++) if (Place(BuildingKind.House, -home, 22, 48, 14, 110)) made++;
            // Fallback so every map has a working town even on a crowded seed.
            for (int t = 0; made < 4 && t < 20; t++) if (Place(BuildingKind.House, home, 4, 56, 4, 120)) made++;

            m.Buildings = _buildings.ToArray();
            m.Noise = SmoothNoise(m.Seed, n);
            return m;
        }

        private bool Free(int x, int y, int w, int h, int side)
        {
            const int mg = 2; // keep a walkable margin round every building
            int n = _n;
            if (x - mg < 2 || y - mg < 2 || x + w + mg > n - 2 || y + h + mg > n - 2) return false;
            for (int yy = y - mg; yy < y + h + mg; yy++)
                for (int xx = x - mg; xx < x + w + mg; xx++)
                {
                    int i = yy * n + xx;
                    if (_occupied[i] != 0 || _m.Bank[i] != side) return false;
                    if (Math.Abs(xx + 0.5 - _m.RiverX[yy]) < _m.RiverHalfW + 4) return false;
                }
            return true;
        }

        private bool Place(BuildingKind kind, int side, int x0, int x1, int y0, int y1)
        {
            int w = SizeW[(int)kind], h = SizeH[(int)kind], n = _n;
            for (int t = 0; t < 400; t++)
            {
                // Coordinates are given for the west bank and mirrored for the east.
                int x = (int)Math.Floor(x0 + R() * (x1 - x0));
                if (side == 1) x = n - 1 - x - w;
                int y = (int)Math.Floor(y0 + R() * (y1 - y0));
                if (!Free(x, y, w, h, side)) continue;
                for (int yy = y; yy < y + h; yy++)
                    for (int xx = x; xx < x + w; xx++)
                    {
                        _occupied[yy * n + xx] = 1;
                        if (kind == BuildingKind.Field) _m.Crop[yy * n + xx] = 1;
                        else _m.Footprint[yy * n + xx] = 1;
                    }
                int door;
                if (kind == BuildingKind.Field)
                {
                    // The field gate sits just outside the edge facing the river.
                    int gx = side == -1 ? x + w : x - 1;
                    door = (y + (h >> 1)) * n + gx;
                }
                else
                {
                    door = (y + h - 1) * n + x + (w >> 1);
                    _m.Footprint[door] = 0;
                }
                _occupied[door] = 1;
                _buildings.Add(new Building(_buildings.Count, kind, x, y, w, h, door));
                return true;
            }
            return false;
        }
    }
}

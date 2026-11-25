using System.Collections.Generic;
using System;

namespace TideToll.Sim;

/// <summary>Health read-outs of a town (the toy's Stats: HealthStats plus the clock).</summary>
public readonly struct SimStats
{
    /// <summary>Mean actual / straight route length over trips ending in the window; NaN with no trips.</summary>
    public double DetourRatio { get; init; }
    /// <summary>Share of walkable land at trail state or above.</summary>
    public double TrailCoverage { get; init; }
    /// <summary>Literal trunk share: footsteps on the busiest 10% of trail cells / all footsteps.</summary>
    public double TrunkShare { get; init; }
    /// <summary>Research variant: the same numerator / footsteps on trail cells.</summary>
    public double TrunkShareD { get; init; }
    public int TripsInWindow { get; init; }
    public int TrailCells { get; init; }
    public int Walkers { get; init; }
    public double SimTime { get; init; }
    public int Day { get; init; }
    /// <summary>0 Spring, 1 Summer, 2 Autumn, 3 Winter.</summary>
    public int Season { get; init; }
    public int Year { get; init; }
    /// <summary>Sim time when a connected trail of FirstTrailCells cells first existed, or -1.</summary>
    public double FirstTrailTime { get; init; }
}

/// <summary>
/// The toy's metrics.ts: footsteps over a rolling window kept as ring buckets, and a log of completed
/// trips. The trip log is a fixed ring rather than the toy's spliced arrays; every buffer is allocated
/// once, so recording and computing never allocate.
/// </summary>
public sealed class Metrics
{
    private readonly SimParams _p;
    private readonly int _n, _nc;
    private readonly double _bucketSec, _windowSec;
    private readonly ushort[][] _buckets;
    private int _cur;
    private double _bucketStart;
    // Completed trips as a ring of parallel arrays: end time, path length, straight length.
    private double[] _tripEnd, _tripLen, _tripStraight;
    private int _tripHead, _tripCount;
    // Scratch for Compute and LargestTrail.
    private readonly uint[] _window;
    private readonly int[] _trail;
    private readonly byte[] _seen;
    private readonly int[] _stack;

    public double FirstTrailTime { get; internal set; } = -1;

    internal Metrics(SimParams p)
    {
        _p = p;
        _n = p.GridWidth;
        _nc = _n * _n;
        _bucketSec = p.MetricWindowDays * p.DaySimSec / p.MetricBuckets;
        _windowSec = p.MetricWindowDays * p.DaySimSec;
        _buckets = new ushort[p.MetricBuckets][];
        for (int b = 0; b < _buckets.Length; b++) _buckets[b] = new ushort[_nc];
        int cap = Math.Max(4096, p.MaxWalkers * 16);
        _tripEnd = new double[cap];
        _tripLen = new double[cap];
        _tripStraight = new double[cap];
        _window = new uint[_nc];
        _trail = new int[_nc];
        _seen = new byte[_nc];
        _stack = new int[_nc];
        // The first Array.Sort of an int range of 2 or more creates the runtime's shared sort helper (24 bytes, once
        // per process). Do it here, so Stats() allocates nothing even on its first call.
        Array.Sort(_trail, 0, 2);
    }

    internal void Advance(double time)
    {
        if (time - _bucketStart >= _bucketSec)
        {
            _bucketStart = time;
            _cur = (_cur + 1) % _buckets.Length;
            Array.Clear(_buckets[_cur]);
            // Forget trips older than the window.
            double cutoff = time - _windowSec;
            while (_tripCount > 0 && _tripEnd[_tripHead] < cutoff)
            {
                _tripHead = (_tripHead + 1) % _tripEnd.Length;
                _tripCount--;
            }
        }
    }

    internal void Footstep(int c)
    {
        ushort[] b = _buckets[_cur];
        if (b[c] < 65535) b[c]++;
    }

    internal void Trip(double time, double len, double straight)
    {
        if (straight <= 3) return; // trips this short say nothing about detours
        if (_tripCount == _tripEnd.Length) GrowTrips(); // never at the default walker cap
        int i = (_tripHead + _tripCount) % _tripEnd.Length;
        _tripEnd[i] = time; _tripLen[i] = len; _tripStraight[i] = straight;
        _tripCount++;
    }

    private void GrowTrips()
    {
        int cap = _tripEnd.Length;
        var e = new double[cap * 2]; var l = new double[cap * 2]; var s = new double[cap * 2];
        for (int k = 0; k < _tripCount; k++)
        {
            int i = (_tripHead + k) % cap;
            e[k] = _tripEnd[i]; l[k] = _tripLen[i]; s[k] = _tripStraight[i];
        }
        _tripEnd = e; _tripLen = l; _tripStraight = s; _tripHead = 0;
    }

    /// <summary>Footsteps per cell over the window, written into <paramref name="output"/> (one entry per cell).</summary>
    public void Window(Span<uint> output)
    {
        if (output.Length != _nc) throw new ArgumentException("output needs one entry per cell.");
        output.Clear();
        foreach (ushort[] b in _buckets)
            for (int i = 0; i < _nc; i++) output[i] += b[i];
    }

    /// <summary>Footsteps per cell over the window (a view of an internal buffer, valid until the next call).</summary>
    public ReadOnlySpan<uint> Window()
    {
        Window(_window);
        return _window;
    }

    internal SimStats Compute(double time, float[] wear, byte[] walkable)
    {
        double cutoff = time - _windowSec;
        double sum = 0;
        int n = 0;
        for (int k = 0; k < _tripCount; k++)
        {
            int i = (_tripHead + k) % _tripEnd.Length;
            if (_tripEnd[i] >= cutoff) { sum += _tripLen[i] / _tripStraight[i]; n++; }
        }

        double thr = _p.ThresholdTrail;
        Window(_window);
        uint[] fw = _window;
        int land = 0, nTrail = 0;
        double total = 0, onTrail = 0;
        for (int i = 0; i < _nc; i++)
        {
            total += fw[i];
            if (walkable[i] == 0) continue;
            land++;
            if (wear[i] >= thr) { _trail[nTrail++] = (int)fw[i]; onTrail += fw[i]; }
        }
        // The toy sorts descending and sums the top k; only the sum is read, so ties cannot matter.
        Array.Sort(_trail, 0, nTrail);
        int top = (int)Math.Ceiling(nTrail * _p.TrunkTopShare);
        double topSum = 0;
        for (int i = 0; i < top; i++) topSum += _trail[nTrail - 1 - i];
        return new SimStats
        {
            DetourRatio = n > 0 ? sum / n : double.NaN,
            TrailCoverage = land > 0 ? (double)nTrail / land : 0,
            TrunkShare = total > 0 ? topSum / total : 0,
            TrunkShareD = onTrail > 0 ? topSum / onTrail : 0,
            TripsInWindow = n,
            TrailCells = nTrail,
        };
    }

    /// <summary>Size of the largest 8-connected group of trail cells, stopping early at <paramref name="enough"/>.</summary>
    internal int LargestTrail(float[] wear, byte[] walkable, int enough)
    {
        double thr = _p.ThresholdTrail;
        int n = _n;
        Array.Clear(_seen);
        int best = 0;
        for (int i = 0; i < _nc; i++)
        {
            if (_seen[i] != 0 || wear[i] < thr || walkable[i] == 0) continue;
            int size = 0, sp = 0;
            _stack[sp++] = i;
            _seen[i] = 1;
            while (sp > 0)
            {
                int u = _stack[--sp];
                size++;
                int ux = u % n, uy = u / n;
                for (int k = 0; k < 8; k++)
                {
                    int x = ux + Grid.DX[k], y = uy + Grid.DY[k];
                    if (x < 0 || y < 0 || x >= n || y >= n) continue;
                    int v = y * n + x;
                    if (_seen[v] == 0 && wear[v] >= thr && walkable[v] != 0) { _seen[v] = 1; _stack[sp++] = v; }
                }
            }
            if (size > best) best = size;
            if (best >= enough) return best;
        }
        return best;
    }
    /// <summary>
    /// Median trunk width in cells (the toy's trailWidth, report only; allocates): for the busiest 10% of trail cells (by
    /// footsteps in <paramref name="steps"/>, ties in cell order), the run of trail cells straight across the trail's local
    /// direction (the traffic-weighted principal axis of the trail cells within 3 cells).
    /// </summary>
    public static int TrailWidth(SimParams p, ReadOnlySpan<float> wear, ReadOnlySpan<byte> walkable, ReadOnlySpan<uint> steps)
    {
        int n = p.GridWidth, nc = n * n;
        double thr = p.ThresholdTrail;
        var cells = new List<int>();
        for (int c = 0; c < nc; c++) if (walkable[c] != 0 && wear[c] >= thr) cells.Add(c);
        // Stable sort by footsteps, descending (JavaScript's sort is stable): insertion order breaks ties.
        uint[] st = steps.ToArray();
        int[] order = cells.ToArray();
        var keys = new long[order.Length];
        for (int i = 0; i < order.Length; i++) keys[i] = -(long)st[order[i]] * nc + i;
        Array.Sort(keys, order);
        int top = (int)Math.Ceiling(order.Length * p.TrunkTopShare);
        var widths = new List<int>();
        float[] wr = wear.ToArray();
        bool Trail(int x, int y) => x >= 0 && y >= 0 && x < n && y < n && wr[y * n + x] >= thr;
        for (int i = 0; i < top; i++)
        {
            int c = order[i], cx = c % n, cy = c / n;
            double sxx = 0, syy = 0, sxy = 0;
            for (int y = cy - 3; y <= cy + 3; y++)
                for (int x = cx - 3; x <= cx + 3; x++)
                {
                    if (!Trail(x, y)) continue;
                    double m = st[y * n + x] + 1.0;
                    sxx += m * ((double)(x - cx) * (x - cx)); syy += m * ((double)(y - cy) * (y - cy)); sxy += m * (x - cx) * (y - cy);
                }
            double ang = 0.5 * Math.Atan2(2 * sxy, sxx - syy); // along the trail
            double px = -Math.Sin(ang), py = Math.Cos(ang);  // across it
            int w = 1;
            for (int s = 1; s >= -1; s -= 2)
                for (int t = 1; t <= 8; t++)
                {
                    if (!Trail((int)JsMath.Round(cx + px * s * t), (int)JsMath.Round(cy + py * s * t))) break;
                    w++;
                }
            widths.Add(w);
        }
        widths.Sort();
        return widths.Count > 0 ? widths[widths.Count >> 1] : 0;
    }
}

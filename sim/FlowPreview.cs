using System;
using System.Collections.Generic;
using System.Globalization;

namespace TideToll.Sim;

/// <summary>A hypothetical edit. Cells are grid indices; null means none.</summary>
public readonly struct WhatIf
{
    /// <summary>New fence cells.</summary>
    public int[]? Block { get; init; }
    /// <summary>Fence cells removed.</summary>
    public int[]? Open { get; init; }
    /// <summary>Water cells that become a footbridge.</summary>
    public int[]? Bridge { get; init; }
    /// <summary>Footbridge cells that become water again.</summary>
    public int[]? CloseBridge { get; init; }
}

/// <summary>One (house, destination) pair whose typical route the change would alter.</summary>
public sealed class RouteChange
{
    public int Home { get; init; }
    public int Dest { get; init; }
    /// <summary>Trips out per day on this pair.</summary>
    public double Rate { get; init; }
    /// <summary>Typical route now, x,y pairs in cell units.</summary>
    public float[] OldPts { get; init; } = Array.Empty<float>();
    /// <summary>Typical route after the change (cut short where it is blocked).</summary>
    public float[] NewPts { get; init; } = Array.Empty<float>();
    public bool Reachable { get; init; }
    /// <summary>New route cost / old route cost (+Infinity when blocked).</summary>
    public double CostRatio { get; init; }
}

/// <summary>The result of a flow preview (the toy's FlowPreview).</summary>
public sealed class FlowPreview
{
    /// <summary>The biggest changed pairs, at most maxPairs, busiest first.</summary>
    public List<RouteChange> Changes { get; } = new();
    /// <summary>Changed pairs left out.</summary>
    public int More { get; init; }
    /// <summary>Trips per day over all changed pairs.</summary>
    public double ReroutedPerDay { get; init; }
    /// <summary>Destinations cut off, and for how many houses, in the order first found.</summary>
    public List<(int Dest, int Houses)> Blocked { get; } = new();
    /// <summary>World.Version the preview was computed against.</summary>
    public int Version { get; init; }
}

/// <summary>
/// Flow preview (the toy's preview.ts): where traffic would go if the player placed or removed something.
/// It reads the live wear, crowding and trip counts, but writes only to its own scratch arrays, never to the
/// world's fields, costs or random stream, so previewing cannot change a run. Run it in steps with
/// <see cref="Begin"/> and <see cref="Step"/> (each step does at most one Dijkstra, a few ms), or at once
/// with <see cref="Compute"/>.
/// </summary>
public sealed class FlowPredictor
{
    private const double MinRate = 0.5;      // pairs with fewer trips a day than this are ignored
    private const double BaseMaxAge = 1;     // sim seconds before the base fields are rebuilt
    private const double SameCost = 0.002;   // a route whose cost changes less than this is unchanged
    private const double AffectedCost = 0.03; // ... and is affected if it changes more than this
    private const double AffectedCells = 3;  // ... or moves more than this many cells anywhere

    private enum Phase { Idle, Start, Base, Hyp, Collect, Done }

    private readonly World _w;
    private readonly int _n;
    private readonly float[] _baseCost, _cost;
    private readonly float[][] _base, _hyp;
    private readonly BinaryHeap _heap;
    private double _baseTime = -1e9;
    private int _baseVersion = -1;
    private readonly int[] _dests, _doors, _tracePath;
    private readonly List<float> _pts = new();

    private Phase _phase = Phase.Idle;
    private int _di, _pendingVersion, _maxPairs;
    private double _pendingTime;
    private int[] _block = Array.Empty<int>(), _open = Array.Empty<int>(), _bridge = Array.Empty<int>(), _close = Array.Empty<int>();

    public FlowPredictor(World world)
    {
        _w = world;
        _n = world.Grid.Width;
        int nc = world.Grid.Count;
        _baseCost = new float[nc];
        _cost = new float[nc];
        _heap = new BinaryHeap(nc);
        var b = world.Buildings;
        var dests = new List<int>();
        foreach (var x in b) if (x.Kind != BuildingKind.House) dests.Add(x.Id);
        _dests = dests.ToArray();
        var houses = world.Houses;
        _doors = new int[houses.Length];
        for (int i = 0; i < houses.Length; i++) _doors[i] = b[houses[i]].Door;
        _base = new float[b.Length][];
        _hyp = new float[b.Length][];
        for (int i = 0; i < b.Length; i++) { _base[i] = Array.Empty<float>(); _hyp[i] = Array.Empty<float>(); }
        foreach (int d in _dests) { _base[d] = new float[nc]; _hyp[d] = new float[nc]; }
        _tracePath = new int[world.Params.Lookahead];
    }

    /// <summary>The finished preview, once <see cref="Step"/> has returned false.</summary>
    public FlowPreview? Result { get; private set; }

    /// <summary>Starts a preview of <paramref name="change"/>; the cells are copied.</summary>
    public void Begin(in WhatIf change, int maxPairs = 5)
    {
        _block = (int[])(change.Block ?? Array.Empty<int>()).Clone();
        _open = (int[])(change.Open ?? Array.Empty<int>()).Clone();
        _bridge = (int[])(change.Bridge ?? Array.Empty<int>()).Clone();
        _close = (int[])(change.CloseBridge ?? Array.Empty<int>()).Clone();
        _maxPairs = maxPairs;
        Result = null;
        _phase = Phase.Start;
    }

    /// <summary>Does about one Dijkstra's worth of work. Returns true while more steps remain.</summary>
    public bool Step()
    {
        var B = _w.Buildings;
        switch (_phase)
        {
            case Phase.Start:
                if (_w.Version != _baseVersion || _w.Time - _baseTime > BaseMaxAge)
                {
                    _baseVersion = -1; // marks the base incomplete until it is rebuilt
                    _pendingVersion = _w.Version;
                    _pendingTime = _w.Time;
                    _w.BuildCost(_baseCost);
                    _di = 0;
                    _phase = Phase.Base;
                }
                else StartHyp();
                return true;
            case Phase.Base:
                CostField.Dijkstra(_w.Grid, B[_dests[_di]].Door, _baseCost, _base[_dests[_di]], _heap, _doors);
                if (++_di == _dests.Length) { _baseVersion = _pendingVersion; _baseTime = _pendingTime; StartHyp(); }
                return true;
            case Phase.Hyp:
                CostField.Dijkstra(_w.Grid, B[_dests[_di]].Door, _cost, _hyp[_dests[_di]], _heap, _doors);
                if (++_di == _dests.Length) _phase = Phase.Collect;
                return true;
            case Phase.Collect:
                Result = Collect();
                _phase = Phase.Done;
                return false;
            default:
                return false;
        }
    }

    /// <summary>Runs a preview to completion.</summary>
    public FlowPreview Compute(in WhatIf change, int maxPairs = 5)
    {
        Begin(change, maxPairs);
        while (Step()) { }
        return Result!;
    }

    private void StartHyp()
    {
        Array.Copy(_baseCost, _cost, _cost.Length);
        Apply(_open, CostOverride.Open);
        Apply(_bridge, CostOverride.Bridge);
        Apply(_close, CostOverride.CloseBridge);
        Apply(_block, CostOverride.Block);
        _di = 0;
        _phase = _dests.Length > 0 ? Phase.Hyp : Phase.Collect;
    }

    private void Apply(int[] cells, CostOverride ov)
    {
        foreach (int c in cells) _cost[c] = (float)_w.CellCost(c, ov);
    }

    private FlowPreview Collect()
    {
        var w = _w;
        var B = w.Buildings;
        var all = new List<RouteChange>();
        var blocked = new List<(int Dest, int Houses)>();
        foreach (int h in w.Houses)
        {
            int door = B[h].Door;
            foreach (int d in _dests)
            {
                double rate = w.TripRatePerDay(h, d);
                if (rate < MinRate) continue;
                float f0 = _base[d][door], f1 = _hyp[d][door];
                if (!(f0 < float.PositiveInfinity)) continue; // not reachable now either
                bool reachable = f1 < float.PositiveInfinity;
                double costRatio = reachable ? (double)f1 / f0 : double.PositiveInfinity;
                if (reachable && Math.Abs(costRatio - 1) < SameCost) continue;
                float[] oldPts = TraceRoute(_base[d], _baseCost, door, B[d].Door);
                float[] newPts = reachable ? TraceRoute(_hyp[d], _cost, door, B[d].Door) : CutAtBlock(oldPts, _cost);
                if (reachable && Math.Abs(costRatio - 1) <= AffectedCost && MaxDeviation(newPts, oldPts) <= AffectedCells) continue;
                if (!reachable)
                {
                    int k = blocked.FindIndex(q => q.Dest == d);
                    if (k < 0) blocked.Add((d, 1)); else blocked[k] = (d, blocked[k].Houses + 1);
                }
                all.Add(new RouteChange { Home = h, Dest = d, Rate = rate, OldPts = oldPts, NewPts = newPts, Reachable = reachable, CostRatio = costRatio });
            }
        }
        // Busiest first; a stable insertion sort, as the toy's Array.prototype.sort is stable.
        for (int i = 1; i < all.Count; i++)
        {
            var x = all[i];
            int j = i - 1;
            while (j >= 0 && all[j].Rate < x.Rate) { all[j + 1] = all[j]; j--; }
            all[j + 1] = x;
        }
        double rerouted = 0;
        foreach (var c in all) rerouted += c.Rate;
        var p = new FlowPreview { More = Math.Max(0, all.Count - _maxPairs), ReroutedPerDay = rerouted, Version = w.Version };
        for (int i = 0; i < all.Count && i < _maxPairs; i++) p.Changes.Add(all[i]);
        p.Blocked.AddRange(blocked);
        return p;
    }

    /// <summary>
    /// The typical route from <paramref name="start"/> to <paramref name="door"/> (no taste, no step noise), as
    /// x,y points in cell units: the walker's aim rule, re-aimed on entering each cell.
    /// </summary>
    public float[] TraceRoute(ReadOnlySpan<float> field, ReadOnlySpan<float> cost, int start, int door)
        => TraceRoute(field.ToArray(), cost.ToArray(), start, door);

    /// <summary>The typical route walkers take now from cell <paramref name="start"/> to building <paramref name="building"/>'s door (live field and cost).</summary>
    public float[] TraceRoute(int building, int start)
        => TraceRoute(_w.FieldArray(building), _w.CostArray, start, _w.Buildings[building].Door);

    internal float[] TraceRoute(float[] field, float[] cost, int start, int door)
    {
        int n = _n;
        double x = start % n + 0.5, y = start / n + 0.5;
        int cell = start;
        _pts.Clear();
        _pts.Add((float)x); _pts.Add((float)y);
        for (int it = 0; it < 4 * n && cell != door; it++)
        {
            if (!_w.ChooseAim(field, cost, x, y, cell, door, 0, false, -1, _tracePath, out double ax, out double ay)) break;
            double d = JsMath.Hypot(ax - x, ay - y);
            if (d < 1e-6) break;
            // Walk towards the aim until entering a new cell (where a walker re-aims) or arriving.
            double ux = (ax - x) / d, uy = (ay - y) / d;
            double t = 0;
            int nc = cell;
            while (t < d)
            {
                t = Math.Min(d, t + 0.1);
                nc = (int)Math.Floor(y + uy * t) * n + (int)Math.Floor(x + ux * t);
                if (nc != cell) break;
            }
            x += ux * t; y += uy * t; cell = nc;
            _pts.Add((float)x); _pts.Add((float)y);
        }
        return _pts.ToArray();
    }

    /// <summary>The largest distance from any point of <paramref name="a"/> to the nearest point of <paramref name="b"/>, in cells.</summary>
    public static double MaxDeviation(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        double worst = 0;
        for (int i = 0; i < a.Length; i += 2)
        {
            double best = double.PositiveInfinity;
            for (int j = 0; j < b.Length; j += 2)
            {
                double dx = (double)a[i] - b[j], dy = (double)a[i + 1] - b[j + 1];
                double d = dx * dx + dy * dy;
                if (d < best) best = d;
            }
            if (best > worst) worst = best;
        }
        return Math.Sqrt(worst);
    }

    /// <summary>The route up to the first point that the change blocks.</summary>
    private float[] CutAtBlock(float[] pts, float[] cost)
    {
        for (int i = 0; i < pts.Length; i += 2)
            if (float.IsPositiveInfinity(cost[(int)Math.Floor(pts[i + 1]) * _n + (int)Math.Floor(pts[i])]))
                return pts[..Math.Max(2, i)];
        return pts;
    }

    /// <summary>One-line readout, e.g. "Reroutes about 40 walkers a day (+8% longer)".</summary>
    public static string Describe(FlowPreview p, Func<int, string> name)
    {
        var parts = new List<string>();
        foreach (var (dest, houses) in p.Blocked)
            parts.Add($"Blocks the {name(dest)} for {houses} house{(houses == 1 ? "" : "s")}");
        double n = p.ReroutedPerDay;
        if (n < 0.5) return parts.Count > 0 ? parts[0] : "No change to routes";
        string shown = n < 5
            ? Num(Math.Max(1, JsMath.Round(n)))
            : "about " + Num(Math.Max(5, JsMath.Round(n / 5) * 5));
        string line = $"Reroutes {shown} walker{(shown == "1" ? "" : "s")} a day";
        RouteChange? big = p.Changes.Find(c => c.Reachable);
        if (big != null && Math.Abs(big.CostRatio - 1) >= 0.05)
        {
            double pct = JsMath.Round((big.CostRatio - 1) * 100);
            line += pct > 0 ? $" (+{Num(pct)}% longer)" : $" ({Num(-pct)}% shorter)";
        }
        parts.Add(line);
        return string.Join(". ", parts);
    }

    private static string Num(double v) => v.ToString("0", CultureInfo.InvariantCulture);
}

using System;
using System.Collections.Generic;

namespace TideToll.Sim;

// The flow preview's second half (preview.ts, after the fields): classifying each (house, destination, group) as
// changed or not, the busiest first, tracing the shown routes, and the result.
public sealed partial class FlowPredictor
{
    private void BeginCollect()
    {
        _phase = Phase.Collect;
        _hi = 0;
        _dj = 0;
        _all = new List<Candidate>();
        _blockedPairs = new List<(int Dest, int Houses)>();
        // Walkers and explorers per house (walkers are assigned to houses in turn).
        Array.Clear(_walkersOf);
        Array.Clear(_explorersOf);
        foreach (ref readonly var k in _w.Walkers)
        {
            if ((uint)k.Home >= (uint)_nb) continue;
            _walkersOf[k.Home]++;
            if (k.Explorer) _explorersOf[k.Home]++;
        }
    }

    private void AddBlocked(int d)
    {
        var blocked = _blockedPairs!;
        int k = blocked.FindIndex(q => q.Dest == d);
        if (k < 0) blocked.Add((d, 1)); else blocked[k] = (d, blocked[k].Houses + 1);
    }

    /// <summary>
    /// Classifies one (house, destination) pair, in house then destination order: each group of its trips (ordinary,
    /// explorers) whose route the change alters becomes a candidate. Returns the work done.
    /// </summary>
    private int CollectPair()
    {
        var w = _w;
        var B = w.Buildings;
        int h = w.Houses[_hi], d = _dests[_dj];
        if (++_dj == _dests.Length) { _dj = 0; _hi++; }
        int door = B[h].Door, to = B[d].Door;
        double rate = w.TripRatePerDay(h, d);
        if (!(_base[d][door] < float.PositiveInfinity)) return PairWork; // not reachable now either
        if (rate < (_v2 ? MinRateV2 : MinRateV1))
        {
            // A rare pair still counts if the change cuts a house with villagers off (version 2).
            if (!_v2 || _walkersOf[h] == 0 || _hyp[d][door] < float.PositiveInfinity) return PairWork;
            _all!.Add(new Candidate { Home = h, Dest = d, Rate = rate, CostRatio = double.PositiveInfinity, F0 = _base[d], C0 = _baseCost, F1 = _hyp[d], C1 = _cost });
            AddBlocked(d);
            return PairWork;
        }
        int work = PairWork;
        double ex = (double)_explorersOf[h] / Math.Max(1, _walkersOf[h]);
        bool cappedBase = false, cappedHyp = false;
        double cap = w.Params.DetourCap;
        if (cap > 0)
        {
            int n = _n;
            double straight = JsMath.Hypot(door % n - to % n, door / n - to / n);
            if (straight > 4)
            {
                cappedBase = CostField.RouteLength(w.Grid, _base[d], _baseCost, door, to) > cap * straight;
                cappedHyp = CostField.RouteLength(w.Grid, _hyp[d], _cost, door, to) > cap * straight;
                work += 2 * RouteLengthWork;
            }
        }
        // Two groups of trips: the house's ordinary walkers and its explorers.
        for (int g = 0; g < 2; g++)
        {
            bool explorers = g == 1;
            double share = explorers ? ex : 1 - ex;
            bool x0 = explorers || cappedBase, x1 = explorers || cappedHyp;
            if (rate * share < MinGroupRate) continue;
            float[] f0 = x0 ? _baseX[d] : _base[d], c0 = x0 ? _baseCostX : _baseCost;
            float[] f1 = x1 ? _hypX[d] : _hyp[d], c1 = x1 ? _costX : _cost;
            bool reachable = f1[door] < float.PositiveInfinity;
            // The cost change as the walker after the change judges it.
            double costRatio = reachable ? (double)f1[door] / (x1 ? _baseX[d] : _base[d])[door] : double.PositiveInfinity;
            if (reachable && x0 == x1 && Math.Abs(costRatio - 1) < SameCost) continue;
            float[]? newPts = null;
            if (reachable && Math.Abs(costRatio - 1) <= AffectedCost)
            {
                newPts = TraceRoute(f1, c1, door, to);
                double moved = MaxDeviation(newPts, BaseRoute(d, h, x0, f0, c0, out bool traced));
                work += (traced ? 2 : 1) * TraceWork;
                if (moved <= AffectedCells) continue;
            }
            // Blocking cuts off every walker of the pair alike: one entry for all of its trips.
            _all!.Add(new Candidate
            {
                Home = h, Dest = d, Rate = reachable ? rate * share : rate, Explorers = explorers, Frugal = x1, Reachable = reachable,
                CostRatio = costRatio, X0 = x0, F0 = f0, C0 = c0, F1 = f1, C1 = c1, NewPts = newPts,
            });
            if (!reachable) { AddBlocked(d); break; }
        }
        return work;
    }

    /// <summary>Busiest first; a stable insertion sort, as the toy's Array.prototype.sort is stable.</summary>
    private void SortCandidates()
    {
        var all = _all!;
        for (int i = 1; i < all.Count; i++)
        {
            var x = all[i];
            int j = i - 1;
            while (j >= 0 && all[j].Rate < x.Rate) { all[j + 1] = all[j]; j--; }
            all[j + 1] = x;
        }
        _ti = 0;
        _phase = Phase.Trace;
    }

    /// <summary>Traces one shown change's routes (old on the base, new after the change). Returns the work done.</summary>
    private int TraceShown(Candidate c)
    {
        var B = _w.Buildings;
        c.OldShown = BaseRoute(c.Dest, c.Home, c.X0, c.F0, c.C0, out bool traced);
        int work = traced ? TraceWork : 1;
        if (!c.Reachable) c.NewShown = CutAtBlock(c.OldShown, c.C1);
        else if (c.NewPts != null) c.NewShown = c.NewPts;
        else { c.NewShown = TraceRoute(c.F1, c.C1, B[c.Home].Door, B[c.Dest].Door); work += TraceWork; }
        return work;
    }

    private FlowPreview Finish()
    {
        var w = _w;
        var all = _all!;
        double rr = 0, rs = 0, rerouted = 0;
        foreach (var c in all) if (c.Reachable) { rr += c.Rate; rs += c.Rate * c.CostRatio; }
        foreach (var c in all) rerouted += c.Rate;
        bool gravelOnly = _gravel.Length > 0 && _block.Length == 0 && _open.Length == 0 && _bridge.Length == 0 && _close.Length == 0
            && _ungravel.Length == 0 && _gate.Length == 0 && _ungate.Length == 0 && _crossing == null;
        double boatsBlocked = 0, boatsRaise = 0;
        if (_crossing is { } cw) (boatsBlocked, boatsRaise) = w.BoatsHeldPerDay(cw.Type, cw.Row);
        var p = new FlowPreview
        {
            More = Math.Max(0, all.Count - _maxPairs), ReroutedPerDay = rerouted, MeanCostRatio = rr > 0 ? rs / rr : 1,
            // The note stands in for "No change to routes", which the readout says below half a trip a day.
            Note = gravelOnly && rerouted < 0.5 ? GravelNoteFor(w, _gravel) : GravelNote.None,
            OnAverage = _v2, Version = _startVersion, BoatsBlockedPerDay = boatsBlocked, BoatsNeedRaisePerDay = boatsRaise,
        };
        for (int i = 0; i < all.Count && i < _maxPairs; i++)
        {
            var c = all[i];
            p.Changes.Add(new RouteChange
            {
                Home = c.Home, Dest = c.Dest, Rate = c.Rate, Explorers = c.Explorers, Frugal = c.Frugal, Reachable = c.Reachable,
                CostRatio = c.CostRatio, OldPts = c.OldShown!, NewPts = c.NewShown!,
            });
        }
        p.Blocked.AddRange(_blockedPairs!);
        _all = null; _blockedPairs = null;
        return p;
    }

    /// <summary>The typical route from house h to destination d on the base fields (traced once per base).</summary>
    private float[] BaseRoute(int d, int h, bool frugal, float[] f, float[] c, out bool traced)
    {
        int key = (d * _nb + h) * 2 + (frugal ? 1 : 0);
        traced = !_baseRoutes.TryGetValue(key, out var r);
        if (traced) { r = TraceRoute(f, c, _w.Buildings[h].Door, _w.Buildings[d].Door); _baseRoutes[key] = r; }
        return r!;
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

    /// <summary>The route up to the first point that the change blocks.</summary>
    private float[] CutAtBlock(float[] pts, float[] cost)
    {
        for (int i = 0; i < pts.Length; i += 2)
            if (float.IsPositiveInfinity(cost[(int)Math.Floor(pts[i + 1]) * _n + (int)Math.Floor(pts[i])]))
                return pts[..Math.Max(2, i)];
        return pts;
    }
}

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
    /// <summary>A river crossing that would be built (river rules only); see <see cref="World.CrossingWhatIf"/>.</summary>
    public CrossingWhatIf? Crossing { get; init; }
}

/// <summary>
/// A crossing in a what-if: its water cells get the same per-cell cost as the built crossing, with no learned wait
/// yet (a ferry's expected load, headway and ride; a bridge's deck). Row and clearance give the boats it would hold.
/// </summary>
public readonly struct CrossingWhatIf
{
    public CrossingType Type { get; init; }
    public int Row { get; init; }
    public int[] Cells { get; init; }
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
    /// <summary>River rules: boats a day that the previewed crossing would turn back (masts above its clearance), and those that would need a drawbridge raised.</summary>
    public double BoatsBlockedPerDay { get; init; }
    public double BoatsNeedRaisePerDay { get; init; }
}

/// <summary>
/// Flow preview (the toy's preview.ts): where traffic would go if the player placed or removed something.
/// It reads the live wear, crowding and trip counts, but writes only to its own scratch arrays, never to the
/// world's fields, costs or random stream, so previewing cannot change a run. Run it in steps with
/// <see cref="Begin"/> and <see cref="Step()"/> (each step does at most one Dijkstra, a few ms), in bounded slices with
/// <see cref="Step(int)"/> (a work budget, for a frame that must stop within about 2 ms), or at once with
/// <see cref="Compute"/>. All three give bit-identical results.
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
    private int _di, _hi, _dj, _pendingVersion, _maxPairs;
    private readonly DijkstraRun _run = new();
    private List<RouteChange>? _all;
    private List<(int Dest, int Houses)>? _blockedPairs;
    private double _pendingTime;
    private int[] _block = Array.Empty<int>(), _open = Array.Empty<int>(), _bridge = Array.Empty<int>(), _close = Array.Empty<int>();
    private CrossingWhatIf? _crossing;

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
        _crossing = change.Crossing is { } cw ? new CrossingWhatIf { Type = cw.Type, Row = cw.Row, Cells = (int[])(cw.Cells ?? Array.Empty<int>()).Clone() } : null;
        _maxPairs = maxPairs;
        Result = null;
        _phase = Phase.Start;
    }

    /// <summary>Does one Dijkstra (or the set-up, or the collection of results). Returns true while more steps remain.</summary>
    public bool Step() => Advance(int.MaxValue, oneItem: true);

    /// <summary>
    /// Does at most about <paramref name="maxWork"/> units of work, then returns: true while more remains. A unit is one
    /// heap entry popped by a Dijkstra (0.1 to 0.3 microseconds in Release); rebuilding the base costs, a pass over the
    /// grid (about 0.3 ms), is one block of <see cref="CostRebuildWork"/> units, and the result collection counts per
    /// pair examined and route traced. The result is bit-identical to <see cref="Step()"/> and <see cref="Compute"/> however the
    /// work is sliced. A frame that must stop within about 2 ms calls this with about 1000 units at a time (0.1 to
    /// 0.4 ms a call on this machine) until its own clock says stop.
    /// </summary>
    public bool Step(int maxWork) => Advance(Math.Max(1, maxWork), oneItem: false);

    /// <summary>Work units charged for one rebuild of the base costs (it cannot be split: it is one snapshot of the town).</summary>
    public const int CostRebuildWork = 1024;
    /// <summary>Work units charged per (house, destination) pair examined and per route traced while collecting the result (a trace is about 0.1 to 0.3 ms).</summary>
    public const int PairWork = 4, TraceWork = 1000;

    private bool Advance(int budget, bool oneItem)
    {
        var B = _w.Buildings;
        while (budget > 0)
        {
            switch (_phase)
            {
                case Phase.Start:
                    if (_w.Version != _baseVersion || _w.Time - _baseTime > BaseMaxAge)
                    {
                        _baseVersion = -1; // marks the base incomplete until it is rebuilt
                        _pendingVersion = _w.Version;
                        _pendingTime = _w.Time;
                        _w.BuildCost(_baseCost);
                        budget -= CostRebuildWork;
                        _di = 0;
                        _phase = Phase.Base;
                        if (_dests.Length > 0) _run.Begin(_w.Grid, B[_dests[0]].Door, _baseCost, _base[_dests[0]], _heap, _doors);
                    }
                    else StartHyp();
                    if (oneItem) return true;
                    break;
                case Phase.Base:
                case Phase.Hyp:
                    if (_dests.Length == 0) { _baseVersion = _pendingVersion; _baseTime = _pendingTime; StartHyp(); break; }
                    if (_run.Run(budget)) return true; // out of budget mid-field
                    budget -= _run.LastPops;
                    _di++;
                    if (_phase == Phase.Base && _di == _dests.Length) { _baseVersion = _pendingVersion; _baseTime = _pendingTime; StartHyp(); }
                    else if (_phase == Phase.Hyp && _di == _dests.Length) BeginCollect();
                    else
                    {
                        int d = _dests[_di];
                        if (_phase == Phase.Base) _run.Begin(_w.Grid, B[d].Door, _baseCost, _base[d], _heap, _doors);
                        else _run.Begin(_w.Grid, B[d].Door, _cost, _hyp[d], _heap, _doors);
                    }
                    if (oneItem) return true;
                    break;
                case Phase.Collect:
                    if (_hi < _w.Houses.Length && _dests.Length > 0) budget -= CollectPair();
                    else _hi = _w.Houses.Length;
                    if (_hi < _w.Houses.Length && !oneItem) break;
                    if (_hi < _w.Houses.Length) continue; // Step(): the whole collection is one step
                    Result = Finish();
                    _phase = Phase.Done;
                    return false;
                default:
                    return false;
            }
        }
        return _phase != Phase.Done && _phase != Phase.Idle;
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
        if (_crossing is { } cw) foreach (int c in cw.Cells) _cost[c] = (float)_w.WhatIfCrossingCost(cw.Type, cw.Cells.Length, c);
        _di = 0;
        if (_dests.Length > 0)
        {
            _phase = Phase.Hyp;
            _run.Begin(_w.Grid, _w.Buildings[_dests[0]].Door, _cost, _hyp[_dests[0]], _heap, _doors);
        }
        else BeginCollect();
    }

    private void BeginCollect()
    {
        _phase = Phase.Collect;
        _hi = 0;
        _dj = 0;
        _all = new List<RouteChange>();
        _blockedPairs = new List<(int Dest, int Houses)>();
    }

    private void Apply(int[] cells, CostOverride ov)
    {
        foreach (int c in cells) _cost[c] = (float)_w.CellCost(c, ov);
    }

    /// <summary>Compares the base and what-if routes of one (house, destination) pair, in house then destination order. Returns the work done.</summary>
    private int CollectPair()
    {
        var w = _w;
        var B = w.Buildings;
        int h = w.Houses[_hi], d = _dests[_dj];
        if (++_dj == _dests.Length) { _dj = 0; _hi++; }
        int door = B[h].Door;
        double rate = w.TripRatePerDay(h, d);
        if (rate < MinRate) return PairWork;
        float f0 = _base[d][door], f1 = _hyp[d][door];
        if (!(f0 < float.PositiveInfinity)) return PairWork; // not reachable now either
        bool reachable = f1 < float.PositiveInfinity;
        double costRatio = reachable ? (double)f1 / f0 : double.PositiveInfinity;
        if (reachable && Math.Abs(costRatio - 1) < SameCost) return PairWork;
        float[] oldPts = TraceRoute(_base[d], _baseCost, door, B[d].Door);
        float[] newPts = reachable ? TraceRoute(_hyp[d], _cost, door, B[d].Door) : CutAtBlock(oldPts, _cost);
        int work = PairWork + (reachable ? 2 : 1) * TraceWork;
        if (reachable && Math.Abs(costRatio - 1) <= AffectedCost && MaxDeviation(newPts, oldPts) <= AffectedCells) return work;
        var blocked = _blockedPairs!;
        if (!reachable)
        {
            int k = blocked.FindIndex(q => q.Dest == d);
            if (k < 0) blocked.Add((d, 1)); else blocked[k] = (d, blocked[k].Houses + 1);
        }
        _all!.Add(new RouteChange { Home = h, Dest = d, Rate = rate, OldPts = oldPts, NewPts = newPts, Reachable = reachable, CostRatio = costRatio });
        return work;
    }

    private FlowPreview Finish()
    {
        var w = _w;
        var all = _all!;
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
        double boatsBlocked = 0, boatsRaise = 0;
        if (_crossing is { } cw) (boatsBlocked, boatsRaise) = w.BoatsHeldPerDay(cw.Type, cw.Row);
        var p = new FlowPreview { More = Math.Max(0, all.Count - _maxPairs), ReroutedPerDay = rerouted, Version = w.Version, BoatsBlockedPerDay = boatsBlocked, BoatsNeedRaisePerDay = boatsRaise };
        for (int i = 0; i < all.Count && i < _maxPairs; i++) p.Changes.Add(all[i]);
        p.Blocked.AddRange(_blockedPairs!);
        _all = null; _blockedPairs = null;
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
        string line = DescribeRoutes(p, name);
        if (p.BoatsBlockedPerDay > 0.05) line += $". Blocks barges: about {Boats(p.BoatsBlockedPerDay)} a day will turn back";
        if (p.BoatsNeedRaisePerDay > 0.05) line += $". Barges need it raised: about {Boats(p.BoatsNeedRaisePerDay)} a day";
        return line;
    }

    private static string Boats(double n)
    {
        double r = Math.Max(1, JsMath.Round(n));
        return Num(r) + (r == 1 ? " boat" : " boats");
    }

    private static string DescribeRoutes(FlowPreview p, Func<int, string> name)
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

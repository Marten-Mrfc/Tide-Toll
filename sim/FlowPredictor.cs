using System;
using System.Collections.Generic;

namespace TideToll.Sim;

/// <summary>
/// Flow preview (the toy's preview.ts): where traffic would go if the player placed or removed something.
/// It reads the live wear, crowding and trip counts, but writes only to its own scratch arrays, never to the
/// world's fields, costs or random stream, so previewing cannot change a run. Run it in steps with
/// <see cref="Begin"/> and <see cref="Step()"/> (each step does at most one Dijkstra, a few ms), in bounded slices with
/// <see cref="Step(int)"/> (a work budget, for a frame that must stop within about 2 ms), or at once with
/// <see cref="Compute"/>. All three give bit-identical results.
/// <para>
/// Two versions, chosen by <see cref="SimParams.PreviewV2"/>. Version 1 is round 2's (the <see cref="SimParams.Legacy"/>
/// preset). Version 2 is the finished toy's: both kinds of walker (each destination gets an ordinary and a frugal field,
/// and each (house, destination) pair's trips are split the way the walkers split them: a house's explorers always walk
/// frugally, the others when the ordinary route is over the detour cap, before or after the change), a field the change
/// cannot reach is copied from the base instead of recomputed, pairs from 0.2 trips a day (0.5 before) and rare pairs
/// that the change cuts off, the rate-weighted mean cost change and the gravel note.
/// </para>
/// </summary>
public sealed partial class FlowPredictor
{
    private const double MinRateV1 = 0.5;     // pairs with fewer trips a day than this are ignored (round 2)
    private const double MinRateV2 = 0.2;     // ... (finished toy: a grown cottage makes 0.3 to 0.4 trips a day to each)
    private const double MinGroupRate = 0.05; // ... and groups (ordinary or explorer trips) with fewer than this
    private const double BaseMaxAge = 1;      // sim seconds before the base fields are rebuilt
    private const double SameCost = 0.002;    // a route whose cost changes less than this is unchanged
    private const double AffectedCost = 0.03; // ... and is affected if it changes more than this
    private const double AffectedCells = 3;   // ... or moves more than this many cells anywhere

    private enum Phase { Idle, Start, Base, Hyp, Collect, Trace, Done }

    /// <summary>A changed route before it is traced for display.</summary>
    private sealed class Candidate
    {
        public int Home, Dest;
        public double Rate, CostRatio;
        public bool Explorers, Frugal, Reachable, X0;
        public float[] F0 = null!, C0 = null!, F1 = null!, C1 = null!;
        public float[]? NewPts, OldShown, NewShown;
    }

    private readonly World _w;
    private readonly int _n, _nc;
    private readonly bool _v2, _frugal;
    private readonly int _views;
    private readonly float[] _baseCost, _cost, _baseCostX, _costX;
    private float[][] _base = Array.Empty<float[]>(), _hyp = Array.Empty<float[]>();
    private float[][] _baseX = Array.Empty<float[]>(), _hypX = Array.Empty<float[]>();
    private readonly BinaryHeap _heap;
    private double _baseTime = -1e9;
    private int _baseVersion = -1;
    private int _nb = -1;
    private int[] _dests = Array.Empty<int>(), _doors = Array.Empty<int>();
    private int[] _walkersOf = Array.Empty<int>(), _explorersOf = Array.Empty<int>();
    private readonly int[] _tracePath;
    private readonly List<float> _pts = new();
    /// <summary>Typical routes on the base fields, by destination, house and kind; valid while the base is.</summary>
    private readonly Dictionary<int, float[]> _baseRoutes = new();

    private Phase _phase = Phase.Idle;
    private int _job, _hi, _dj, _ti, _pendingVersion, _startVersion, _maxPairs;
    private bool _jobRunning;
    private readonly DijkstraRun _run = new();
    private List<Candidate>? _all;
    private List<(int Dest, int Houses)>? _blockedPairs;
    private double _pendingTime;
    private int[] _block = Array.Empty<int>(), _open = Array.Empty<int>(), _bridge = Array.Empty<int>(), _close = Array.Empty<int>();
    private int[] _gravel = Array.Empty<int>(), _ungravel = Array.Empty<int>(), _gate = Array.Empty<int>(), _ungate = Array.Empty<int>();
    /// <summary>Per-cell override flags of the change being applied (the toy's flags), and the cells touched.</summary>
    private readonly CostOverride[] _flags;
    private readonly List<int> _touched = new();
    private CrossingWhatIf? _crossing;

    public FlowPredictor(World world)
    {
        _w = world;
        _n = world.Grid.Width;
        _nc = world.Grid.Count;
        _v2 = world.Params.PreviewV2;
        _frugal = world.Params.UseFrugal;
        _views = _frugal ? 2 : 1;
        _baseCost = new float[_nc];
        _cost = new float[_nc];
        _baseCostX = _frugal ? new float[_nc] : Array.Empty<float>();
        _costX = _frugal ? new float[_nc] : Array.Empty<float>();
        _flags = new CostOverride[_nc];
        _heap = new BinaryHeap(_nc);
        _tracePath = new int[world.Params.Lookahead];
        Init();
    }

    /// <summary>(Re)builds the per-building tables: at construction and when house growth has added buildings.</summary>
    private void Init()
    {
        var b = _w.Buildings;
        _nb = b.Length;
        _baseVersion = -1;
        _baseRoutes.Clear();
        var dests = new List<int>();
        foreach (var x in b) if (x.Kind != BuildingKind.House) dests.Add(x.Id);
        _dests = dests.ToArray();
        var houses = _w.Houses;
        _doors = new int[houses.Length];
        for (int i = 0; i < houses.Length; i++) _doors[i] = b[houses[i]].Door;
        float[][] Set(bool make)
        {
            var s = new float[_nb][];
            for (int i = 0; i < _nb; i++) s[i] = Array.Empty<float>();
            if (make) foreach (int d in _dests) s[d] = new float[_nc];
            return s;
        }
        // Destinations are never added (growth adds houses only), so fields already made are kept.
        _base = Keep(_base, Set(true)); _hyp = Keep(_hyp, Set(true));
        _baseX = Keep(_baseX, Set(_frugal)); _hypX = Keep(_hypX, Set(_frugal));
        _walkersOf = new int[_nb];
        _explorersOf = new int[_nb];
    }

    private static float[][] Keep(float[][] old, float[][] made)
    {
        for (int i = 0; i < old.Length && i < made.Length; i++)
            if (old[i].Length == made[i].Length) made[i] = old[i];
        return made;
    }

    /// <summary>The finished preview, once <see cref="Step()"/> has returned false.</summary>
    public FlowPreview? Result { get; private set; }

    /// <summary>Starts a preview of <paramref name="change"/>; the cells are copied.</summary>
    public void Begin(in WhatIf change, int maxPairs = 5)
    {
        _block = (int[])(change.Block ?? Array.Empty<int>()).Clone();
        _open = (int[])(change.Open ?? Array.Empty<int>()).Clone();
        _bridge = (int[])(change.Bridge ?? Array.Empty<int>()).Clone();
        _close = (int[])(change.CloseBridge ?? Array.Empty<int>()).Clone();
        _gravel = (int[])(change.Gravel ?? Array.Empty<int>()).Clone();
        _ungravel = (int[])(change.Ungravel ?? Array.Empty<int>()).Clone();
        _gate = (int[])(change.Gate ?? Array.Empty<int>()).Clone();
        _ungate = (int[])(change.Ungate ?? Array.Empty<int>()).Clone();
        _crossing = change.Crossing is { } cw ? new CrossingWhatIf { Type = cw.Type, Row = cw.Row, Cells = (int[])(cw.Cells ?? Array.Empty<int>()).Clone() } : null;
        _maxPairs = maxPairs;
        Result = null;
        _phase = Phase.Start;
    }

    /// <summary>Does one Dijkstra (or the set-up, the collection of results, or one shown route). Returns true while more steps remain.</summary>
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
    /// <summary>
    /// Work units charged per (house, destination) pair examined, per route traced (about 0.1 to 0.3 ms), per route
    /// length measured for the detour cap, and per field copied from the base.
    /// </summary>
    public const int PairWork = 4, TraceWork = 1000, RouteLengthWork = 100, CopyWork = 64;

    private bool Advance(int budget, bool oneItem)
    {
        while (budget > 0)
        {
            switch (_phase)
            {
                case Phase.Start:
                    if (_w.Buildings.Length != _nb) Init();
                    _startVersion = _w.Version;
                    if (_w.Version != _baseVersion || _w.Time - _baseTime > BaseMaxAge)
                    {
                        _baseVersion = -1; // marks the base incomplete until it is rebuilt
                        _pendingVersion = _w.Version;
                        _pendingTime = _w.Time;
                        _w.BuildCost(_baseCost, _frugal ? _baseCostX : null, 0, _nc);
                        _baseRoutes.Clear();
                        budget -= CostRebuildWork;
                        _phase = Phase.Base;
                        _job = 0;
                        if (_job < Jobs) BeginJob(); else FieldsDone();
                    }
                    else StartHyp();
                    if (oneItem) return true;
                    break;
                case Phase.Base:
                case Phase.Hyp:
                    if (_jobRunning)
                    {
                        if (_run.Run(budget)) return true; // out of budget mid-field
                        budget -= _run.LastPops;
                    }
                    else budget -= CopyWork;
                    if (++_job < Jobs) BeginJob(); else FieldsDone();
                    if (oneItem) return true;
                    break;
                case Phase.Collect:
                    if (_hi < _w.Houses.Length && _dests.Length > 0) budget -= CollectPair();
                    else _hi = _w.Houses.Length;
                    if (_hi < _w.Houses.Length) continue; // Step(): the whole collection is one step
                    SortCandidates();
                    if (oneItem) return true;
                    break;
                case Phase.Trace:
                    if (_ti < _all!.Count && _ti < _maxPairs) { budget -= TraceShown(_all[_ti]); _ti++; if (oneItem) return true; break; }
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

    /// <summary>Fields to make in the Base and Hyp phases: per destination, the ordinary one, then the frugal one.</summary>
    private int Jobs => _dests.Length * _views;

    private void BeginJob()
    {
        int d = _dests[_job / _views];
        bool x = _job % _views == 1;
        int door = _w.Buildings[d].Door;
        if (_phase == Phase.Base)
        {
            _run.Begin(_w.Grid, door, x ? _baseCostX : _baseCost, x ? _baseX[d] : _base[d], _heap, _doors);
            _jobRunning = true;
            return;
        }
        float[] bf = x ? _baseX[d] : _base[d], hf = x ? _hypX[d] : _hyp[d];
        float[] bc = x ? _baseCostX : _baseCost, hc = x ? _costX : _cost;
        if (_v2 && OutOfReach(bf, bc, hc)) { Array.Copy(bf, hf, _nc); _jobRunning = false; }
        else { _run.Begin(_w.Grid, door, hc, hf, _heap, _doors); _jobRunning = true; }
    }

    private void FieldsDone()
    {
        if (_phase == Phase.Base) { _baseVersion = _pendingVersion; _baseTime = _pendingTime; StartHyp(); }
        else BeginCollect();
    }

    private void StartHyp()
    {
        Array.Copy(_baseCost, _cost, _nc);
        if (_frugal) Array.Copy(_baseCostX, _costX, _nc);
        _touched.Clear();
        // The changed cells re-costed under all their overrides at once (the toy's flags, in its order of kinds).
        Flag(_block, CostOverride.Block); Flag(_open, CostOverride.Open); Flag(_bridge, CostOverride.Bridge); Flag(_close, CostOverride.CloseBridge);
        Flag(_gravel, CostOverride.Gravel); Flag(_ungravel, CostOverride.Ungravel);
        Flag(_gate, _gate.Length == 2 ? CostOverride.Gate | CostOverride.GatePair : CostOverride.Gate); Flag(_ungate, CostOverride.Ungate);
        foreach (int c in _touched)
        {
            _cost[c] = (float)_w.CellCost(c, _flags[c]);
            if (_frugal) _costX[c] = (float)_w.CellCost(c, _flags[c], frugal: true);
            _flags[c] = CostOverride.None;
        }
        if (_crossing is { } cw)
            foreach (int c in cw.Cells)
            {
                // A crossing's cost is a wait, not worn ground: the same in both views (sync plan, conflict A).
                float k = (float)_w.WhatIfCrossingCost(cw.Type, cw.Cells.Length, c);
                _cost[c] = k;
                if (_frugal) _costX[c] = k;
                _touched.Add(c);
            }
        _phase = Phase.Hyp;
        _job = 0;
        if (Jobs > 0) BeginJob(); else BeginCollect();
    }

    private void Flag(int[] cells, CostOverride ov)
    {
        foreach (int c in cells)
        {
            if ((uint)c >= (uint)_flags.Length) continue;
            if (_flags[c] == CostOverride.None) _touched.Add(c);
            _flags[c] |= ov;
        }
    }

    /// <summary>
    /// Whether a change leaves a field as it is wherever routes from the houses run: it only makes cells dearer, and
    /// every changed cell and its neighbours are at least as far as the farthest house door (so no route to a door, and
    /// no diagonal step past a corner, touches them). Then the base field serves.
    /// </summary>
    private bool OutOfReach(float[] field, float[] baseCost, float[] cost)
    {
        float t = 0;
        foreach (int g in _doors) t = Math.Max(t, field[g]);
        int n = _n;
        foreach (int c in _touched)
        {
            if (cost[c] < baseCost[c]) return false;
            int x = c % n, y = c / n;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (x + dx < 0 || y + dy < 0 || x + dx >= n || y + dy >= n) continue;
                    if (field[(y + dy) * n + x + dx] < t) return false;
                }
        }
        return true;
    }
}

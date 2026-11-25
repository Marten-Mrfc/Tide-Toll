using System;
using System.Collections.Generic;

namespace TideToll.Sim;

/// <summary>
/// The simulation (the toy's world.ts): wear grid, tools, cost-to-go fields and walkers. Engine-free,
/// seeded and deterministic: a seed plus the same commands at the same ticks always gives the same run.
/// Nothing in <see cref="Tick"/> allocates. This file holds the state, field refresh and tick; WorldGround.cs the
/// cell costs, WorldWalk.cs the walkers, WorldEdit.cs the tools, WorldViews.cs the read-only API.
/// </summary>
public sealed partial class World
{
    // Cost multipliers from wear and from recent traffic are lookup tables.
    private const int WearLutSteps = 10, FlowLutSteps = 4;

    private readonly SimParams _p;
    private readonly Grid _g;
    private readonly int _n, _nc;
    private readonly MapData _map;
    private readonly double _dt;
    private readonly double[] _stateSpeedMul, _surfaceCost;

    private double _time;
    private long _ticks;
    private int _version;
    private readonly float[] _wear, _hollowHold, _cost, _costX, _occ, _flow, _tripRate;
    private readonly byte[] _hollow, _fence, _land, _gravel, _gate, _gatePair;
    /// <summary>Whether walkers ever use the frugal view (SimParams.UseFrugal): otherwise costX and fieldsX are empty.</summary>
    private readonly bool _useFrugal;
    private readonly short[] _bridgeAt;
    private readonly ushort[] _here;
    private readonly List<Bridge> _bridges = new();
    private readonly float[][] _fields, _fieldsX;
    private readonly double[] _fieldTime;
    private readonly bool[] _urgent;
    private readonly Walker[] _walkers;
    private int _walkerCount;
    private readonly Metrics _metrics;
    private Mulberry32 _rng;
    private readonly float[] _wearLut = new float[100 * WearLutSteps + 1];
    private readonly float[] _flowLut = new float[256 * FlowLutSteps + 1];
    private readonly BinaryHeap _heap;
    private int _nextId;
    private double _nextDecay;
    private readonly int[][] _byKind = new int[5][];
    // Buildings: the map's, then houses added by growth (pre-sized: _b has room for SimParams.MaxGrownHouses more when
    // growth is on). _nb buildings stand now; _bs is the fixed stride of the trip table (home * _bs + dest).
    private readonly Building[] _b;
    private int _nb;
    private readonly int _bs;
    private readonly int[] _houses;
    private int _houseCount;
    private readonly int[] _aimPath;

    public World(int seed, int? walkers = null, SimParams? p = null)
    {
        _p = p ?? SimParams.Default;
        _p.Validate();
        _river = _p.River;
        _g = _p.Grid;
        _n = _g.Width;
        _nc = _g.Count;
        _dt = 1.0 / _p.TickHz;
        _stateSpeedMul = new double[5];
        _surfaceCost = new double[5];
        for (int i = 0; i < 5; i++) { _stateSpeedMul[i] = _p.StateSpeedMul[i]; _surfaceCost[i] = _p.SurfaceCost[i]; }
        Seed = seed;
        _map = MapGen.Generate(seed, _n);
        if (_river != null) InitRiverMap();
        _rng = new Mulberry32(unchecked((uint)(seed * 31 + 5)));
        Building[] b = _map.Buildings;
        for (int k = 0; k < 5; k++)
        {
            var ids = new List<int>();
            foreach (var x in b) if ((int)x.Kind == k) ids.Add(x.Id);
            _byKind[k] = ids.ToArray();
        }
        _growthOn = _p.GrowthOn && _river == null;
        int grow = _growthOn ? _p.MaxGrownHouses : 0;
        _nb = b.Length;
        _bs = b.Length + grow;
        _b = new Building[_bs];
        Array.Copy(b, _b, b.Length);
        int[] h0 = _byKind[(int)BuildingKind.House];
        _houses = new int[h0.Length + grow];
        Array.Copy(h0, _houses, h0.Length);
        _houseCount = h0.Length;
        _useFrugal = _p.UseFrugal;
        _wear = new float[_nc]; _hollowHold = new float[_nc]; _cost = new float[_nc];
        _costX = _useFrugal ? new float[_nc] : Array.Empty<float>();
        _occ = new float[_nc]; _flow = new float[_nc];
        _hollow = new byte[_nc]; _fence = new byte[_nc]; _land = new byte[_nc];
        _gravel = new byte[_nc]; _gate = new byte[_nc]; _gatePair = new byte[_nc];
        _bridgeAt = new short[_nc];
        Array.Fill(_bridgeAt, (short)-1);
        _here = new ushort[_nc];
        _tripRate = new float[_bs * _bs];
        _metrics = new Metrics(_p);
        _heap = new BinaryHeap(_nc * 4);
        _walkers = new Walker[_p.MaxWalkers];
        _aimPath = new int[_p.Lookahead];
        _nextDecay = _p.DecayStepSimSec;
        _rainOn = _p.RainOn && _river == null;
        _weather = new RainWeather(seed, _p);
        _mud = new byte[_nc];
        for (int i = 0; i < 5; i++) _mudCostMul[i] = _p.MudCostMul[i];
        for (int i = 0; i < _wearLut.Length; i++)
            _wearLut[i] = (float)(1 - _p.TrailDiscount * Math.Pow(Math.Min(1, (double)i / WearLutSteps / _p.DiscScale), _p.DiscPow));
        for (int i = 0; i < _flowLut.Length; i++)
            _flowLut[i] = (float)(1 - _p.FlowDiscount * (1 - Math.Exp(-((double)i / FlowLutSteps) / _p.FlowScale)));
        for (int i = 0; i < _nc; i++) _land[i] = (byte)(_map.Water[i] == 0 && _map.Footprint[i] == 0 ? 1 : 0);
        if (_river != null) InitRiver(seed);
        _fields = new float[_bs][];
        _fieldsX = new float[_bs][];
        _fieldTime = new double[_bs];
        _urgent = new bool[_bs];
        for (int t = 0; t < _bs; t++)
        {
            _fields[t] = new float[_nc];
            _fieldsX[t] = _useFrugal ? new float[_nc] : Array.Empty<float>();
            _fieldTime[t] = -1e9; _urgent[t] = true;
        }
        RebuildCosts();
        _fronted = new byte[_nc];
        if (_growthOn) InitGrowth(seed);
        for (int t = 0; t < _nb; t++) RefreshField(t);
        SetWalkerCount(walkers ?? _p.DefaultWalkers);
    }

    // ---------------------------------------------------------------- fields

    private void RefreshField(int t)
    {
        CostField.Dijkstra(_g, _b[t].Door, _cost, _fields[t], _heap);
        if (_useFrugal) CostField.Dijkstra(_g, _b[t].Door, _costX, _fieldsX[t], _heap);
        _fieldTime[t] = _time;
        _urgent[t] = false;
    }

    private void RefreshFields()
    {
        bool rebuilt = false;
        // After an edit, destination fields first: most walkers out are heading to one.
        int budget = _p.UrgentFieldsPerTick;
        for (int pass = 0; pass < 2; pass++)
            for (int t = 0; t < _nb && budget > 0; t++)
                if (_urgent[t] && (_b[t].Kind == BuildingKind.House) == (pass == 1))
                {
                    if (!rebuilt) { RebuildCosts(); rebuilt = true; }
                    RefreshField(t);
                    budget--;
                }
        if (rebuilt) return;
        // Otherwise rebuild at most the one stalest field, once it is due.
        int oldest = 0;
        for (int t = 1; t < _nb; t++) if (_fieldTime[t] < _fieldTime[oldest]) oldest = t;
        if (_time - _fieldTime[oldest] >= _p.FieldRefreshSimSec) { RebuildCosts(); RefreshField(oldest); }
    }

    // ---------------------------------------------------------------- tick

    /// <summary>Advances the simulation by one fixed tick (1 / TickHz sim seconds). Allocation-free.</summary>
    public void Tick()
    {
        double dt = _dt;
        _time += dt;
        _ticks++;
        _metrics.Advance(_time);
        double a = dt / _p.CrowdAvgSimSec, keep = 1 - a;
        float[] occ = _occ;
        ushort[] here = _here;
        for (int i = 0; i < _nc; i++) occ[i] = (float)(occ[i] * keep + here[i] * a);
        float[] tr = _tripRate;
        double tk = Math.Exp(-dt / (_p.TripRateDays * _p.DaySimSec));
        for (int h = 0, nb = _nb; h < nb; h++)
            for (int i = h * _bs, e = i + nb; i < e; i++) tr[i] = (float)(tr[i] * tk);
        // A new shower turns worn ground to mud at once; dry ground clears it at once.
        if (_rainOn && (_weather.Update(_time) || (_mudCells > 0 && _weather.Wetness <= 0 && !_weather.Raining))) UpdateMud();
        RefreshFields();
        if (_river != null) RiverStep(dt);
        for (int i = 0; i < _walkerCount; i++) { _curWalker = i; StepWalker(ref _walkers[i], dt); }
        if (_time >= _nextDecay)
        {
            _nextDecay += _p.DecayStepSimSec;
            Decay(_p.DecayStepSimSec);
            if (_rainOn) UpdateMud();
            if (_metrics.FirstTrailTime < 0 && _metrics.LargestTrail(_wear, _land, _p.FirstTrailCells) >= _p.FirstTrailCells)
                _metrics.FirstTrailTime = _time;
        }
        if (_growthOn && _time >= _nextHouseTime)
        {
            _nextHouseTime += _p.HouseEverySimSec;
            GrowHouse();
        }
        if (_river != null) RiverSettle();
    }

    private void Decay(double dt)
    {
        var p = _p;
        double fLow = Math.Pow(0.5, dt / p.HalfLifeLow), fMid = Math.Pow(0.5, dt / p.HalfLifeMid);
        double fHigh = Math.Pow(0.5, dt / p.HalfLifeHigh), fHol = Math.Pow(0.5, dt / p.HalfLifeHollow);
        double t25 = p.ThresholdTrail, t50 = p.ThresholdTrack, t85 = p.ThresholdHollow, hold = p.HollowHoldSimSec;
        float[] wear = _wear, flow = _flow, hh = _hollowHold;
        byte[] hollow = _hollow;
        double ff = Math.Pow(0.5, dt / p.FlowHalfLife);
        for (int i = 0; i < _nc; i++) flow[i] = (float)(flow[i] * ff);
        for (int i = 0; i < _nc; i++)
        {
            double w = wear[i];
            if (w <= 0) continue;
            if (w >= t85)
            {
                hh[i] = (float)(hh[i] + dt);
                if (hh[i] >= hold) hollow[i] = 1;
            }
            else hh[i] = 0;
            if (hollow[i] != 0 && w < t50) hollow[i] = 0;
            double f = hollow[i] != 0 ? fHol : w < t25 ? fLow : w < t50 ? fMid : fHigh;
            double nw = w * f;
            // Gravel stops a trail regrowing: wear under it never decays below trail.
            if (_gravel[i] != 0 && w >= t25 && nw < t25) nw = t25;
            // A fronted cell (a house's frontage) does not fade below track while the house stands.
            if (_fronted[i] != 0 && w >= p.FrontageFloor && nw < p.FrontageFloor) nw = p.FrontageFloor;
            wear[i] = nw < 0.01 ? 0 : (float)nw;
        }
    }
}

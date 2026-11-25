using System;

namespace TideToll.Sim;

/// <summary>A plot for a cottage (growth.ts Plot): a 3 x 3 footprint, its door, and the frontage it faces.</summary>
public readonly struct Plot
{
    /// <summary>Footprint top-left cell.</summary>
    public int X { get; init; }
    public int Y { get; init; }
    public int Door { get; init; }
    /// <summary>The frontage cell the door faces.</summary>
    public int Front { get; init; }
    /// <summary>The frontage cells along the plot's front (wear 50 or more): FrontedCount of F0, F1, F2.</summary>
    public int FrontedCount { get; init; }
    public int F0 { get; init; }
    public int F1 { get; init; }
    public int F2 { get; init; }
    public double Score { get; init; }
    /// <summary>The fronted cells as an array (allocates; tests and tools).</summary>
    public int[] Fronted() => FrontedCount switch { 0 => Array.Empty<int>(), 1 => new[] { F0 }, 2 => new[] { F0, F1 }, _ => new[] { F0, F1, F2 } };
}

/// <summary>A house added by growth (growth.ts GrownHouse), for the game to draw it appearing.</summary>
public readonly struct GrownHouse
{
    public int Building { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Door { get; init; }
    public int Front { get; init; }
    /// <summary>Sim seconds when it was built, and the in-game day (1-based).</summary>
    public double Time { get; init; }
    public int Day { get; init; }
}

// House growth by the frontage rules (the toy's growth.ts and world.ts "house growth", round 4). A Hamlet household
// arrives every HouseEverySimSec while the town is below the Hamlet target and takes the best valid plot. A plot is a
// 3 x 3 cottage with its garden, its door in the middle of the side facing its frontage cell. It is valid when (1) its
// door faces a cell at wear 50 or more (the frontage); (2) none of its 9 cells is at wear 25 or more; (3) it is on dry
// land (no water, ford, bridge, field, fence, gravel, building or door) a cell clear of the water's edge and 2 cells
// from the map's edge; (4) it keeps a one-cell gap from every other building; (5) it blocks no way through (every door
// walkers can reach now stays reachable: a flood fill, on the best 8 plots only). Score: 3 x frontage wear / 100, plus
// access (mean over the well, field and market of 1 - route cost / 100, floored at 0), plus 1 per house within 6 cells
// (up to 3), minus 1 if the frontage is mud now, minus the crowding there (up to 1). Ties go to the first plot in scan
// order. The search allocates nothing: the best 8 are kept in a fixed array, each new plot inserted after every plot of
// equal or higher score, which is exactly the first 8 of the toy's stable sort.
public sealed partial class World
{
    private const int PlotTries = 8;
    // Orientations: the house lies in direction k (0 +x, 1 -x, 2 +y, 3 -y) from the frontage cell; top-left corner of the
    // 3 x 3 relative to the frontage cell.
    private static readonly int[] OrientX = { 1, -3, -1, -1 }, OrientY = { -1, -1, 1, -3 };

    private readonly bool _growthOn;
    private Mulberry32 _growthRng;
    private double _nextHouseTime;
    /// <summary>Per cell, how many standing houses front it: it does not decay below FrontageFloor.</summary>
    private byte[] _fronted = Array.Empty<byte>();
    private GrownHouse[] _grown = Array.Empty<GrownHouse>();
    private int _grownCount;
    // Scratch of the plot search (allocated once, with growth on).
    private byte[] _isDoor = Array.Empty<byte>(), _seenBefore = Array.Empty<byte>(), _seenAfter = Array.Empty<byte>(), _plotMask = Array.Empty<byte>();
    private int[] _stack = Array.Empty<int>();
    private readonly Plot[] _top = new Plot[PlotTries];
    private int _topCount, _candidates;

    /// <summary>Whether this run has house growth (SimParams.GrowthOn with the river rules off).</summary>
    public bool GrowthOn => _growthOn;
    /// <summary>Houses added by growth, in the order they appeared.</summary>
    public ReadOnlySpan<GrownHouse> Grown => new(_grown, 0, _grownCount);
    /// <summary>Per cell, how many standing grown houses front it (all zero with growth off).</summary>
    public ReadOnlySpan<byte> Fronted => _fronted;
    /// <summary>
    /// Growth has used every house slot the world pre-sized (SimParams.MaxGrownHouses): no more households come, whatever
    /// the population. The slots cover a town growing to its Hamlet target; only lowering the walker count by hand while
    /// the town grows can use them up (the toy has no such limit). Shown so the game can say so instead of growth
    /// stopping silently (river pass 3).
    /// </summary>
    public bool GrowthRoomFull => _growthOn && _nb == _bs;
    /// <summary>Houses growth may still add before <see cref="GrowthRoomFull"/> (0 with growth off).</summary>
    public int GrowthRoomLeft => _growthOn ? _bs - _nb : 0;
    /// <summary>Sim time of the next household's arrival check.</summary>
    public double NextHouseTime => _nextHouseTime;

    /// <summary>The growth stream's seed: <c>((seed * 40503) ^ 0x6f7d) &gt;&gt;&gt; 0</c>. Exact in the toy's doubles for every int seed (|seed * 40503| &lt; 2^47).</summary>
    public static uint GrowthStreamSeed(int seed) => unchecked((uint)((long)seed * 40503L) ^ 0x6f7du);

    private void InitGrowth(int seed)
    {
        _growthRng = new Mulberry32(GrowthStreamSeed(seed));
        _nextHouseTime = _p.HouseFirstSimSec;
        _grown = new GrownHouse[_bs - _nb];
        _isDoor = new byte[_nc];
        _seenBefore = new byte[_nc];
        _seenAfter = new byte[_nc];
        _plotMask = new byte[_nc];
        _stack = new int[_nc];
    }

    /// <summary>A household arrives if the town is below the Hamlet target and a valid plot exists (and the pre-sized room is not used up).</summary>
    private void GrowHouse()
    {
        if (_walkerCount + _p.HouseholdSize > Math.Min(_p.HamletTarget, _p.MaxWalkers)) return;
        if (_nb == _bs) return; // GrowthRoomFull: only after the walker count was lowered by hand many times
        if (ChoosePlot(out Plot plot)) AddHouse(plot);
    }

    /// <summary>
    /// Every valid plot by rules 1 to 4 is scored; the best <see cref="PlotTries"/> are kept, best first (ties in scan
    /// order), in <c>_top</c>, and the count of all in <c>_candidates</c>.
    /// </summary>
    private void CandidatePlots()
    {
        int n = _n;
        var m = _map;
        byte[] isDoor = _isDoor;
        Array.Clear(isDoor);
        for (int i = 0; i < _nb; i++) isDoor[_b[i].Door] = 1;
        _topCount = 0;
        _candidates = 0;
        double frontWear = _p.FrontageWear, clear = _p.PlotClearWear;
        for (int f = 0; f < _nc; f++)
        {
            if (_wear[f] < frontWear || _land[f] == 0 || isDoor[f] != 0) continue;
            int fx = f % n, fy = f / n;
            for (int k = 0; k < 4; k++)
            {
                int x0 = fx + OrientX[k], y0 = fy + OrientY[k];
                if (x0 < 3 || y0 < 3 || x0 + 3 > n - 3 || y0 + 3 > n - 3) continue;
                bool ok = true;
                for (int y = y0 - 1; y <= y0 + 3 && ok; y++)
                    for (int x = x0 - 1; x <= x0 + 3 && ok; x++)
                    {
                        int c = y * n + x;
                        bool ring = x < x0 || y < y0 || x > x0 + 2 || y > y0 + 2;
                        // The ring keeps clear of water and other buildings; the footprint must be free ground.
                        if (ring) ok = m.Water[c] == 0 && m.Footprint[c] == 0 && isDoor[c] == 0;
                        else ok = _land[c] == 1 && m.Crop[c] == 0 && _fence[c] == 0 && _gravel[c] == 0 && _bridgeAt[c] < 0 && isDoor[c] == 0 && _wear[c] < clear;
                    }
                if (!ok) continue;
                int door = (fy + Grid.DY[k]) * n + fx + Grid.DX[k];
                // The plot's front: the frontage cell and its two neighbours along the side, where worn to track.
                int cnt = 0, f0 = 0, f1 = 0, f2 = 0;
                for (int s = -1; s <= 1; s++)
                {
                    int ax = fx + (k < 2 ? 0 : s), ay = fy + (k < 2 ? s : 0);
                    if (ax < 0 || ay < 0 || ax >= n || ay >= n) continue;
                    int a = ay * n + ax;
                    if (_land[a] == 0 || _wear[a] < frontWear) continue;
                    if (cnt == 0) f0 = a; else if (cnt == 1) f1 = a; else f2 = a;
                    cnt++;
                }
                _candidates++;
                double score = PlotScore(x0, y0, door, f);
                // Insert after every kept plot of equal or higher score (a stable sort's order), if it makes the best few.
                int at = _topCount;
                while (at > 0 && _top[at - 1].Score < score) at--;
                if (at >= PlotTries) continue;
                for (int j = Math.Min(_topCount, PlotTries - 1); j > at; j--) _top[j] = _top[j - 1];
                _top[at] = new Plot { X = x0, Y = y0, Door = door, Front = f, FrontedCount = cnt, F0 = f0, F1 = f1, F2 = f2, Score = score };
                if (_topCount < PlotTries) _topCount++;
            }
        }
    }

    private double PlotScore(int x0, int y0, int door, int front)
    {
        double s = 3 * (double)_wear[front] / 100;
        double acc = 0;
        for (int kind = 1; kind <= 3; kind++) // well, field, market
        {
            double best = double.PositiveInfinity;
            foreach (int b in _byKind[kind]) best = Math.Min(best, _fields[b][door]);
            acc += best < double.PositiveInfinity ? Math.Max(0, 1 - best / 100) : 0;
        }
        s += acc / 3;
        int near = 0;
        double cx = x0 + 1.5, cy = y0 + 1.5;
        for (int i = 0; i < _houseCount; i++)
        {
            var b = _b[_houses[i]];
            if (JsMath.Hypot(b.X + b.W / 2.0 - cx, b.Y + b.H / 2.0 - cy) <= _p.PlotNeighbourRadius) near++;
        }
        s += Math.Min(3, near);
        if (_rainOn && _mud[front] != 0 && _weather.Wetness > 0) s -= 1;
        s -= Math.Min(1, (double)_occ[front]);
        return s;
    }

    /// <summary>Open ground reachable from the first open building door (the walkers' step rule), with the plot mask shut when <paramref name="withPlot"/>.</summary>
    private void Reachable(byte[] seen, bool withPlot)
    {
        Array.Clear(seen);
        int n = _n, start = -1;
        for (int i = 0; i < _nb && start < 0; i++) if (Open(_b[i].Door, withPlot)) start = _b[i].Door;
        if (start < 0) return;
        int[] stack = _stack;
        int sp = 0;
        stack[sp++] = start;
        seen[start] = 1;
        while (sp > 0)
        {
            int u = stack[--sp], ux = u % n, uy = u / n;
            for (int k = 0; k < 8; k++)
            {
                int vx = ux + Grid.DX[k], vy = uy + Grid.DY[k];
                if (vx < 0 || vy < 0 || vx >= n || vy >= n) continue;
                int v = vy * n + vx;
                if (seen[v] != 0 || !Open(v, withPlot)) continue;
                if (k >= 4 && (!Open(uy * n + vx, withPlot) || !Open(vy * n + ux, withPlot))) continue;
                seen[v] = 1;
                stack[sp++] = v;
            }
        }
    }

    private bool Open(int c, bool withPlot) => !IsBlocked(c) && !(withPlot && _plotMask[c] != 0);

    /// <summary>Whether placing the plot's footprint (minus its door) keeps every door that is reachable now reachable (_seenBefore must hold the fill without it).</summary>
    private bool KeepsWaysOpen(in Plot p)
    {
        int n = _n;
        for (int y = p.Y; y < p.Y + 3; y++) for (int x = p.X; x < p.X + 3; x++) _plotMask[y * n + x] = 1;
        _plotMask[p.Door] = 0;
        Reachable(_seenAfter, withPlot: true);
        for (int y = p.Y; y < p.Y + 3; y++) for (int x = p.X; x < p.X + 3; x++) _plotMask[y * n + x] = 0;
        for (int i = 0; i < _nb; i++) if (_seenBefore[_b[i].Door] != 0 && _seenAfter[_b[i].Door] == 0) return false;
        return _seenAfter[p.Door] == 1;
    }

    /// <summary>The plot the next household takes: the first of the best few that keeps every way open. False if none.</summary>
    private bool ChoosePlot(out Plot plot)
    {
        plot = default;
        CandidatePlots();
        if (_topCount == 0) return false;
        Reachable(_seenBefore, withPlot: false);
        for (int i = 0; i < _topCount; i++)
            if (KeepsWaysOpen(_top[i])) { plot = _top[i]; return true; }
        return false;
    }

    /// <summary>Builds a cottage on a plot and moves its household in (the toy's addHouse). Allocation-free.</summary>
    private int AddHouse(in Plot p)
    {
        int n = _n, id = _nb;
        _b[id] = new Building(id, BuildingKind.House, p.X, p.Y, 3, 3, p.Door);
        _nb++;
        _houses[_houseCount++] = id;
        for (int y = p.Y; y < p.Y + 3; y++)
            for (int x = p.X; x < p.X + 3; x++)
            {
                int c = y * n + x;
                if (c == p.Door) continue;
                _map.Footprint[c] = 1;
                _land[c] = 0;
                _mud[c] = 0;
            }
        // The trip table has a fixed stride: the new house's row and column are already there, at zero.
        Array.Fill(_fields[id], float.PositiveInfinity);
        if (_useFrugal) Array.Fill(_fieldsX[id], float.PositiveInfinity);
        _fieldTime[id] = -1e9;
        _urgent[id] = true;
        if (p.FrontedCount > 0) _fronted[p.F0]++;
        if (p.FrontedCount > 1) _fronted[p.F1]++;
        if (p.FrontedCount > 2) _fronted[p.F2]++;
        _grown[_grownCount++] = new GrownHouse { Building = id, X = p.X, Y = p.Y, Door = p.Door, Front = p.Front, Time = _time, Day = (int)Math.Floor(_time / _p.DaySimSec) + 1 };
        // Every field is rebuilt, one a tick, as after any edit (the new house's own one too).
        Edited();
        for (int i = 0; i < _p.HouseholdSize && _walkerCount < _p.MaxWalkers; i++) AddWalker(id, ref _growthRng);
        return id;
    }

    /// <summary>The plot search now, for tests and tools: the count of valid plots by rules 1 to 4 and the best few, best first, each with whether it keeps every way open; and the plot growth would take.</summary>
    public (int Candidates, (Plot Plot, bool Keeps)[] Top, Plot? Chosen) PlotSearch()
    {
        if (!_growthOn) throw new InvalidOperationException("The plot search needs SimParams.GrowthOn (and the river rules off).");
        CandidatePlots();
        var top = new (Plot, bool)[_topCount];
        Reachable(_seenBefore, withPlot: false);
        for (int i = 0; i < _topCount; i++) top[i] = (_top[i], KeepsWaysOpen(_top[i]));
        return (_candidates, top, ChoosePlot(out var c) ? c : null);
    }
}

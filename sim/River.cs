using System;
using System.Collections.Generic;

namespace TideToll.Sim;

// The river rules: state, set-up, the per-tick river step and the season books. Boats are in RiverBoats.cs,
// crossings and walker queues in RiverGates.cs, commands in RiverCommands.cs. All of it is skipped when
// SimParams.River is null: no allocation, no code on the tick, no change to any result.
public sealed partial class World
{
    private RiverRules? _river;
    private short[] _crossAt = Array.Empty<short>();
    private readonly List<Crossing> _crossings = new();
    private Boat[] _boats = Array.Empty<Boat>();
    private int _boatCount, _nextBoatId, _nextCrossingId, _ticketSeq;
    private Mulberry32 _riverRng;
    private double _money, _rep;
    private readonly double[] _nextArrival = new double[2];
    private int _landingRow, _landingCell, _landingB = -1;
    private int[] _berth = Array.Empty<int>();
    private RiverEvent[] _events = Array.Empty<RiverEvent>();
    private int _evHead, _evCount;
    private RiverStats? _rs;
    private RiverView? _riverView;

    /// <summary>The river: boats, crossings, money and reputation. Null when the river rules are off.</summary>
    public RiverView? River => _riverView;

    /// <summary>Before the buildings are indexed: the landing row and cell, and the Landing building when goods are hauled.</summary>
    private void InitRiverMap()
    {
        var r = _river!;
        var m = _map;
        int n = _n;
        int market = -1;
        foreach (var b in m.Buildings) if (b.Kind == BuildingKind.Market) { market = b.Id; break; }
        int side = market >= 0 ? m.Bank[m.Buildings[market].Door] : 1;
        _landingRow = Math.Max(r.LandingMinRow, Math.Min(r.LandingMaxRow, market >= 0 ? m.Buildings[market].Door / n : n / 2));
        int rx = (int)Math.Floor(m.RiverX[_landingRow]);
        int x = rx;
        while (x > 0 && x < n - 1 && m.Water[_landingRow * n + x] != 0) x += side;
        _landingCell = _landingRow * n + x;
        if (r.HaulGoods)
        {
            var list = new List<Building>(m.Buildings);
            _landingB = list.Count;
            list.Add(new Building(_landingB, BuildingKind.Landing, x, _landingRow, 1, 1, _landingCell));
            m.Buildings = list.ToArray();
        }
    }

    /// <summary>After the grids exist: the ford's crossing record, boats, berths, events, money.</summary>
    private void InitRiver(int seed)
    {
        var r = _river!;
        _crossAt = new short[_nc];
        Array.Fill(_crossAt, (short)-1);
        _boats = new Boat[r.MaxBoats];
        _berth = new int[r.Berths];
        Array.Fill(_berth, -1);
        _events = new RiverEvent[256];
        _riverRng = new Mulberry32(unchecked((uint)(seed * 7919 + 13)));
        _money = r.StartMoney;
        _rep = r.ReputationStart;
        _rs = new RiverStats();
        // The ford: the water cells of its centre row; it never blocks a boat and never queues.
        var cells = new List<int>();
        for (int x = 0; x < _n; x++) { int c = _map.FordY * _n + x; if (_map.Ford[c] != 0) cells.Add(c); }
        _crossings.Add(new Crossing(_nextCrossingId++, CrossingType.Ford, _map.FordY, cells.ToArray(), -1, -1, -1, r, _p.MaxWalkers));
        // First arrivals: within one gap of the start.
        for (int k = 0; k < 2; k++) _nextArrival[k] = ArrivalGap((BoatKind)k) * _riverRng.NextDouble();
        InitSeasons(seed);
        _riverView = new RiverView(this);
    }

    private double ArrivalGap(BoatKind k)
    {
        var r = _river!;
        double jitter = 1 + (_riverRng.NextDouble() * 2 - 1) * r.GapJitter;
        return r.Gap(k) / (0.5 + _rep / 100) * jitter;
    }

    /// <summary>Tick step 4: boat spawns, boat moves and patience, drawbridges and keepers, ferries.</summary>
    private void RiverStep(double dt)
    {
        foreach (var x in _crossings) { x.QueueWest = 0; x.QueueEast = 0; x.BoatsWaiting = 0; }
        SpawnBoats();
        MoveBoats(dt);
        for (int i = 0; i < _crossings.Count; i++)
        {
            var x = _crossings[i];
            if (x.Type == CrossingType.Drawbridge) StepDrawbridge(x, dt);
            else if (x.Type == CrossingType.Ferry) StepFerry(x, dt);
            if (x.Type == CrossingType.Drawbridge || x.Type == CrossingType.Footbridge) StepBridgeGate(x, dt);
        }
    }

    /// <summary>Tick step 7: the books at day and season boundaries.</summary>
    private void RiverSettle()
    {
        var rs = _rs!;
        bool dayEnds = _ticks % _p.TicksPerDay == 0;
        if (dayEnds || _flooded) SeasonBoundary(_ticks / _p.TicksPerDay, dayEnds);
        if (dayEnds)
        {
            rs.IncomeYesterday = rs.IncomeDay;
            rs.IncomeDay = 0; rs.ServedToday = 0; rs.TurnedBackToday = 0; rs.MarketToday = 0;
        }
        if (_ticks % _p.TicksPerSeason == 0)
        {
            var r = _river!;
            double tax = r.HearthTax * _byKind[(int)BuildingKind.House].Length;
            double costs = 0;
            foreach (var x in _crossings)
            {
                if (x.Type == CrossingType.Ferry) costs += r.FerryUpkeep + r.FerryWage;
                else if (x.Type == CrossingType.Drawbridge) costs += r.DrawbridgeUpkeep + (x.Keeper ? r.KeeperWage : 0);
            }
            double interest = _money < 0 ? -_money * r.DebtInterest : 0;
            _money += tax - costs - interest;
            rs.Tax += tax; rs.Upkeep += costs + interest;
            rs.IncomeDay += tax; rs.IncomeSeason += tax;
            rs.IncomeLastSeason = rs.IncomeSeason;
            rs.IncomeSeason = 0;
            Emit(RiverEventKind.SeasonSettled, -1, -1, tax - costs - interest);
        }
    }

    private void Earn(double coin, bool boat)
    {
        var rs = _rs!;
        _money += coin;
        rs.IncomeDay += coin; rs.IncomeSeason += coin;
        if (boat) rs.BoatIncome += coin; else rs.MarketIncome += coin;
    }

    private void Spend(double coin) { _money -= coin; _rs!.Spent += coin; }

    private void Emit(RiverEventKind kind, int boat, int crossing, double value)
    {
        if (_events.Length == 0) return;
        int k = (_evHead + _evCount) % _events.Length;
        _events[k] = new RiverEvent(kind, _ticks, boat, crossing, value);
        if (_evCount < _events.Length) _evCount++;
        else _evHead = (_evHead + 1) % _events.Length; // full: the oldest is dropped
    }

    internal void DrainRiverEvents(List<RiverEvent> into)
    {
        for (int i = 0; i < _evCount; i++) into.Add(_events[(_evHead + i) % _events.Length]);
        _evHead = 0; _evCount = 0;
    }

    private Crossing? CrossingById(int id)
    {
        for (int i = 0; i < _crossings.Count; i++) if (_crossings[i].Id == id) return _crossings[i];
        return null;
    }

    // Read by RiverView.
    internal ReadOnlySpan<Boat> BoatSpan => new(_boats, 0, _boatCount);
    internal IReadOnlyList<Crossing> CrossingList => _crossings;
    internal double MoneyValue => _money;
    internal double ReputationValue => _rep;
    internal int LandingRowValue => _landingRow;
    internal int LandingCellValue => _landingCell;
    internal int LandingBuilding => _landingB;
    internal RiverStats RiverStatsValue => _rs!;
    internal double NextArrival(BoatKind k) => _nextArrival[(int)k];
    internal int BerthBoat(int i) => _berth[i];
}

/// <summary>Running totals of the river rules, for the chapter page, the bot and tests. Read-only outside the simulation.</summary>
public sealed class RiverStats
{
    public double IncomeDay { get; internal set; }
    public double IncomeYesterday { get; internal set; }
    public double IncomeSeason { get; internal set; }
    public double IncomeLastSeason { get; internal set; }
    public double BoatIncome { get; internal set; }
    public double MarketIncome { get; internal set; }
    public double Tax { get; internal set; }
    /// <summary>Upkeep, wages and debt interest paid.</summary>
    public double Upkeep { get; internal set; }
    /// <summary>Builds, keepers and erase costs paid.</summary>
    public double Spent { get; internal set; }
    public int GrainToBoats { get; internal set; }
    public int GrainToMarket { get; internal set; }
    /// <summary>Grain that found no boat and no room at the market (the market's daily limit was reached).</summary>
    public int GrainUnsold { get; internal set; }
    /// <summary>Units the market bought today (it takes at most MarketUnitsPerHouseDay per house).</summary>
    public int MarketToday { get; internal set; }
    public int ServedToday { get; internal set; }
    public int TurnedBackToday { get; internal set; }
    // Per boat kind (index by (int)BoatKind).
    internal readonly int[] ArrivedK = new int[2], ServedK = new int[2], TurnedBackK = new int[2];
    public IReadOnlyList<int> Arrived => ArrivedK;
    public IReadOnlyList<int> Served => ServedK;
    public IReadOnlyList<int> TurnedBack => TurnedBackK;
    public int WalkerWaits { get; internal set; }
    public double WalkerWaitSum { get; internal set; }
    public double WalkerWaitMax { get; internal set; }
    /// <summary>Walkers who crossed the river (reached the other bank), by any crossing or the ice, and how many waited at a gate first.</summary>
    public int Crossings { get; internal set; }
    public int CrossingsWaited { get; internal set; }
    /// <summary>Crossings made over the ice.</summary>
    public int IceCrossings { get; internal set; }
    /// <summary>Grain dropped on the way: its carrier gave up (waited too long, or was sent home).</summary>
    public int GrainLost { get; internal set; }
    /// <summary>Grain that reached the landing (to a boat, to the market, or unsold).</summary>
    public int GrainDelivered => GrainToBoats + GrainToMarket + GrainUnsold;
    public int Trips { get; internal set; }
    public double TripTimeSum { get; internal set; }

    public double TradeIncome => BoatIncome + MarketIncome;
    public double MeanWalkerWait => WalkerWaits > 0 ? WalkerWaitSum / WalkerWaits : 0;
    public double MeanTripTime => Trips > 0 ? TripTimeSum / Trips : 0;
    /// <summary>Boats turned back out of those that arrived and have been decided (served or turned back).</summary>
    public double TurnBackRate(BoatKind? k = null)
    {
        int s = 0, t = 0;
        for (int i = 0; i < 2; i++) if (k == null || (int)k == i) { s += ServedK[i]; t += TurnedBackK[i]; }
        return s + t > 0 ? (double)t / (s + t) : 0;
    }
}

/// <summary>Read-only view of the river for the game: spans and lists over live state; read them each frame.</summary>
public sealed class RiverView
{
    private readonly World _w;
    internal RiverView(World w) => _w = w;

    public RiverRules Rules => _w.Params.River!;
    public ReadOnlySpan<Boat> Boats => _w.BoatSpan;
    /// <summary>Every crossing, the ford first (id 0).</summary>
    public IReadOnlyList<Crossing> Crossings => _w.CrossingList;
    public double Money => _w.MoneyValue;
    public double Reputation => _w.ReputationValue;
    public double IncomeDay => _w.RiverStatsValue.IncomeDay;
    public double IncomeSeason => _w.RiverStatsValue.IncomeSeason;
    public int ServedToday => _w.RiverStatsValue.ServedToday;
    public int TurnedBackToday => _w.RiverStatsValue.TurnedBackToday;
    public RiverStats Stats => _w.RiverStatsValue;
    /// <summary>Row of the landing; boats moor there.</summary>
    public int LandingRow => _w.LandingRowValue;
    /// <summary>The landing's land cell on the market bank.</summary>
    public int LandingCell => _w.LandingCellValue;
    /// <summary>Building index of the Landing (walkers' destination), -1 when goods are not hauled.</summary>
    public int LandingBuilding => _w.LandingBuilding;
    /// <summary>Boat id moored at berth i, -1 when free.</summary>
    public int BerthBoat(int i) => _w.BerthBoat(i);
    public int Berths => Rules.Berths;
    /// <summary>Sim time of the next arrival of a boat kind.</summary>
    public double NextArrival(BoatKind k) => _w.NextArrival(k);
    /// <summary>Expected boat arrivals per day now, of one kind.</summary>
    public double ArrivalsPerDay(BoatKind k) => Rules.ArrivalsPerDay(k, Reputation, _w.Params.DaySimSec);

    /// <summary>Spring high water: the flood band is under water and the ford is closed.</summary>
    public bool Flooded => _w.FloodedValue;
    /// <summary>Winter ice: boats are stopped, trade with boats is paused and walkers cross anywhere.</summary>
    public bool Frozen => _w.FrozenValue;
    /// <summary>The rest of spring after a flood: worn paths in the flood band are mud.</summary>
    public bool Muddy => _w.MuddyValue;
    /// <summary>Walkers may use the ford (closed while the flood band is under water).</summary>
    public bool FordOpen => !_w.FloodedValue;
    /// <summary>Sim time the flood band dries (becomes mud), -1 when not flooded.</summary>
    public double FloodEndsAt => _w.FloodEndsAt;
    /// <summary>1 on the cells of the flood band (land beside the river that floods in spring), per cell; fixed for the map.</summary>
    public ReadOnlySpan<byte> FloodBand => _w.BandSpan;
    /// <summary>Wear of paths across the ice (0 to 100, per cell; only on water cells while frozen, wiped at the thaw). Never part of World.Wear.</summary>
    public ReadOnlySpan<float> IceWear => _w.IceWearSpan;

    /// <summary>Moves the events since the last call into <paramref name="into"/> (oldest first). Allocates nothing once the list has room.</summary>
    public void DrainEvents(List<RiverEvent> into) => _w.DrainRiverEvents(into);
}

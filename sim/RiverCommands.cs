using System;
using System.Globalization;

namespace TideToll.Sim;

// River commands: build a footbridge, ferry or drawbridge, raise and lower a drawbridge, hire its keeper,
// and the river side of erase. Each returns a RiverResult with a reason in words when it refuses.
public sealed partial class World
{
    private string _lastRefusal = "";

    /// <summary>The reason the last river command (or PlaceBridge with the river rules on) refused, "" if none.</summary>
    public string LastRiverRefusal => _lastRefusal;

    private RiverResult Refuse(string why)
    {
        _lastRefusal = why;
        return RiverResult.Refused(why);
    }

    private static string Coin(double c) => Math.Floor(c + 1e-9).ToString("0", CultureInfo.InvariantCulture) + "c";

    /// <summary>Why a keeper cannot be hired yet, with the hand raises still to go, in words.</summary>
    private string KeeperLockedReason(Crossing x)
    {
        int left = x.RaisesToKeeper, all = _river!.KeeperUnlockRaises;
        return $"No keeper will take this bridge on yet: raise it by hand {Times(left)} more first ({CountWord(x.HandRaises)} of {CountWord(all)} raises done)";
    }

    private static readonly string[] Words =
    {
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen",
        "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty",
    };

    private static string CountWord(int n) => n >= 0 && n < Words.Length ? Words[n] : n.ToString(CultureInfo.InvariantCulture);

    /// <summary>"once", "twice", "three times".</summary>
    internal static string Times(int n) => n switch
    {
        0 => "no times",
        1 => "once",
        2 => "twice",
        _ => CountWord(n) + " times",
    };

    public static string CrossingName(CrossingType t) => t switch
    {
        CrossingType.Footbridge => "footbridge",
        CrossingType.Ferry => "ferry",
        CrossingType.Drawbridge => "drawbridge",
        _ => "ford",
    };

    /// <summary>
    /// Whether a crossing of type <paramref name="t"/> can be built for a click at (x, y), with the reason when
    /// not (spot, then coin). Fills <see cref="_bridgeScratch"/> with its water cells. Builds nothing.
    /// </summary>
    public RiverResult CheckCrossing(CrossingType t, double x, double y)
    {
        string? spot = SpotProblem(t, x, y);
        if (spot != null) return Refuse(spot);
        double price = _river!.Price(t);
        if (_money < price - 1e-9)
        {
            Emit(RiverEventKind.CannotAfford, -1, -1, price);
            return Refuse($"Not enough coin: the {CrossingName(t)} costs {Coin(price)}, you have {Coin(_money)}");
        }
        _lastRefusal = "";
        return RiverResult.Success(-1);
    }

    /// <summary>Why a crossing cannot stand at (x, y), or null if it can (coin aside). Fills the bridge scratch list.</summary>
    private string? SpotProblem(CrossingType t, double x, double y)
    {
        if (_river == null) return "The river rules are off";
        if (t == CrossingType.Ford) return "Fords are where the river is shallow; they cannot be built";
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        if (!_g.Inside(ix, iy)) return "That is off the map";
        if (Math.Abs(ix - (int)Math.Floor(_map.RiverX[iy])) > _map.RiverHalfW + 4) return "Crossings go across the river: click on the water";
        if (Math.Abs(iy - _map.FordY) <= 2) return "Too close to the ford";
        if (Math.Abs(iy - _landingRow) <= 1) return "That would block the landing";
        foreach (var c in _crossings)
            if (c.Type != CrossingType.Ford && Math.Abs(c.Row - iy) < 3) return $"Too close to the {CrossingName(c.Type)}: boats need room to wait between crossings";
        if (!BridgeCellsAt(x, y, _bridgeScratch)) return "Both banks must be open ground here";
        return null;
    }

    /// <summary>
    /// The flow-preview change for building a crossing of type <paramref name="t"/> at (x, y), or null when none can
    /// stand there (coin is not checked: the preview shows what it would do). Allocates; for the tool ghost.
    /// </summary>
    public WhatIf? CrossingWhatIf(CrossingType t, double x, double y)
    {
        if (SpotProblem(t, x, y) != null) return null;
        return new WhatIf { Crossing = new CrossingWhatIf { Type = t, Row = (int)Math.Floor(y), Cells = _bridgeScratch.ToArray() } };
    }

    /// <summary>Per-cell routing cost of a would-be crossing's water cell, with nothing learned yet.</summary>
    public double WhatIfCrossingCost(CrossingType t, int cells, int c)
        => t == CrossingType.Ferry && _river != null ? FerryCellCost(cells, 0) : CellCost(c, CostOverride.Bridge);

    /// <summary>
    /// Boats a day, at today's reputation, that a crossing of type t at a row would turn back (masts above a fixed
    /// clearance) and that would need a drawbridge raised. A crossing north of the landing holds no arriving boat.
    /// </summary>
    public (double Blocked, double NeedRaise) BoatsHeldPerDay(CrossingType t, int row)
    {
        var r = _river;
        if (r == null || row <= _landingRow) return (0, 0);
        double blocked = 0, raise = 0;
        for (int k = 0; k < 2; k++)
        {
            var kind = (BoatKind)k;
            double perDay = r.ArrivalsPerDay(kind, _rep, _p.DaySimSec);
            int mast = r.Mast(kind);
            if (t == CrossingType.Footbridge && mast > r.FootbridgeClearance) blocked += perDay;
            if (t == CrossingType.Drawbridge && mast > r.DrawbridgeClosedClearance) raise += perDay;
        }
        return (blocked, raise);
    }

    /// <summary>Builds a crossing across the river at the row of (x, y) and pays for it.</summary>
    public RiverResult PlaceCrossing(CrossingType t, double x, double y)
    {
        var ok = CheckCrossing(t, x, y);
        if (!ok.Ok) return ok;
        var r = _river!;
        int row = (int)Math.Floor(y);
        int[] cells = _bridgeScratch.ToArray();
        int west = cells[0] - 1, east = cells[^1] + 1;
        int bridgeId = -1;
        if (t != CrossingType.Ferry) bridgeId = AddBridgeFromScratch(row).Id;
        var cr = new Crossing(_nextCrossingId++, t, row, cells, west, east, bridgeId, r, _p.MaxWalkers, _keeperTally);
        _crossings.Add(cr);
        foreach (int c in cells) _crossAt[c] = (short)cr.Id;
        Spend(r.Price(t));
        Emit(RiverEventKind.CrossingBuilt, -1, cr.Id, r.Price(t));
        Edited();
        return RiverResult.Success(cr.Id, $"Built a {CrossingName(t)} for {Coin(r.Price(t))}");
    }

    public RiverResult PlaceFootbridge(double x, double y) => PlaceCrossing(CrossingType.Footbridge, x, y);
    public RiverResult PlaceFerry(double x, double y) => PlaceCrossing(CrossingType.Ferry, x, y);
    public RiverResult PlaceDrawbridge(double x, double y) => PlaceCrossing(CrossingType.Drawbridge, x, y);

    /// <summary>Raises or lowers a drawbridge by hand. Boats pass a raised bridge one at a time; it lowers itself once they are through.</summary>
    public RiverResult SetBridge(int id, bool raise)
    {
        if (_river == null) return Refuse("The river rules are off");
        var x = CrossingById(id);
        if (x == null || x.Type != CrossingType.Drawbridge) return Refuse("That is not a drawbridge");
        if (raise)
        {
            if (_frozen) return Refuse("The river is frozen: no boat can pass");
            if (x.State == BridgeState.Closed || x.State == BridgeState.Lowering)
            {
                StartRaise(x);
                // Raising by hand teaches the town the boats: a raise counts (CountHandRaise, when a boat passes) only if a
                // masted boat was waiting below the bridge now and passes during this raise.
                MarkHandRaise(x);
            }
        }
        else
        {
            if (x.State == BridgeState.Raised && BoatPassing(x)) return Refuse("A boat is passing under the bridge");
            if (x.State == BridgeState.Raised || x.State == BridgeState.Raising) StartLower(x);
        }
        _lastRefusal = "";
        return RiverResult.Success(id);
    }

    /// <summary>Hires a keeper for a drawbridge: raises for each masted boat and lowers once it is through. Wages are paid each season.</summary>
    public RiverResult HireKeeper(int id)
    {
        if (_river == null) return Refuse("The river rules are off");
        var x = CrossingById(id);
        if (x == null || x.Type != CrossingType.Drawbridge) return Refuse("Only a drawbridge has a keeper");
        if (x.Keeper) return Refuse("This drawbridge already has a keeper");
        if (x.RaisesToKeeper > 0) return Refuse(KeeperLockedReason(x));
        double price = _river.KeeperPrice;
        if (_money < price - 1e-9)
        {
            Emit(RiverEventKind.CannotAfford, -1, id, price);
            return Refuse($"Not enough coin: the keeper costs {Coin(price)} to hire, you have {Coin(_money)}");
        }
        x.Keeper = true;
        Spend(price);
        Emit(RiverEventKind.KeeperHired, -1, id, price);
        _lastRefusal = "";
        return RiverResult.Success(id, $"Hired a keeper for {Coin(price)}");
    }

    /// <summary>Hook of Erase for cell c: removes a ferry on it (if the purse allows). Returns how many crossings were removed.</summary>
    private int RiverEraseAt(int c)
    {
        int id = _crossAt[c];
        if (id < 0) return 0;
        var x = CrossingById(id);
        if (x == null || x.Type != CrossingType.Ferry) return 0;
        return RemoveCrossing(x) ? 1 : 0;
    }

    /// <summary>Hook of Erase before a footbridge or drawbridge is removed: false (and nothing removed) when the purse cannot pay for it.</summary>
    private bool RiverBridgeErased(int bridgeId)
    {
        foreach (var x in _crossings)
            if (x.BridgeId == bridgeId && x.Type != CrossingType.Ford) return RemoveCrossing(x);
        return true;
    }

    /// <summary>What taking a crossing down costs (EraseCostShare of its price).</summary>
    public double EraseCost(CrossingType t) => _river == null ? 0 : _river.EraseCostShare * _river.Price(t);

    /// <summary>
    /// Whether the purse can pay for taking crossing <paramref name="id"/> down without going below the debt limit,
    /// with the reason in words when not. Removes nothing.
    /// </summary>
    public RiverResult CheckErase(int id)
    {
        if (_river == null) return Refuse("The river rules are off");
        var x = CrossingById(id);
        if (x == null || x.Type == CrossingType.Ford) return Refuse("There is nothing here to take down");
        double cost = EraseCost(x.Type);
        if (_money - cost < _river.DebtLimit - 1e-9)
        {
            Emit(RiverEventKind.CannotAfford, -1, id, cost);
            return Refuse($"Not enough coin: taking down the {CrossingName(x.Type)} costs {Coin(cost)}, you have {Coin(_money)} " +
                          $"and the purse may not go below {Coin(_river.DebtLimit)}");
        }
        _lastRefusal = "";
        return RiverResult.Success(id);
    }

    private int _eraseRefused = -1; // crossing refused in the current Erase call (asked once, not once per cell)

    private bool RemoveCrossing(Crossing x)
    {
        if (x.Id == _eraseRefused) return false;
        if (!CheckErase(x.Id).Ok) { _eraseRefused = x.Id; return false; }
        ReleaseAll(x);
        foreach (int c in x.Cells) _crossAt[c] = -1;
        for (int i = 0; i < _boatCount; i++) if (_boats[i].Through == x.Id) _boats[i].Through = -1;
        _crossings.Remove(x);
        double cost = EraseCost(x.Type);
        Spend(cost);
        Emit(RiverEventKind.CrossingRemoved, -1, x.Id, cost);
        return true;
    }

    /// <summary>
    /// Sends a boat in from the south edge now, outside the schedule (for tests, tutorials and events). It does not
    /// move the schedule and draws nothing from the river's random stream.
    /// </summary>
    public RiverResult SpawnBoat(BoatKind kind)
    {
        if (_river == null) return Refuse("The river rules are off");
        if (_boatCount >= _boats.Length) return Refuse("The river is full of boats");
        AddBoat(kind);
        return RiverResult.Success(_nextBoatId - 1);
    }

    /// <summary>Crossing id on cell c, -1 none (the ford is not marked on cells).</summary>
    public int CrossingAt(int c) => _river == null || c < 0 || c >= _nc ? -1 : _crossAt[c];
}

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
        if (_river == null) return Refuse("The river rules are off");
        if (t == CrossingType.Ford) return Refuse("Fords are where the river is shallow; they cannot be built");
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        if (!_g.Inside(ix, iy)) return Refuse("That is off the map");
        if (Math.Abs(ix - (int)Math.Floor(_map.RiverX[iy])) > _map.RiverHalfW + 4) return Refuse("Crossings go across the river: click on the water");
        if (Math.Abs(iy - _map.FordY) <= 2) return Refuse("Too close to the ford");
        if (Math.Abs(iy - _landingRow) <= 1) return Refuse("That would block the landing");
        foreach (var c in _crossings)
            if (c.Type != CrossingType.Ford && Math.Abs(c.Row - iy) < 3) return Refuse($"Too close to the {CrossingName(c.Type)}: boats need room to wait between crossings");
        if (!BridgeCellsAt(x, y, _bridgeScratch)) return Refuse("Both banks must be open ground here");
        double price = _river.Price(t);
        if (_money < price - 1e-9)
        {
            Emit(RiverEventKind.CannotAfford, -1, -1, price);
            return Refuse($"Not enough coin: the {CrossingName(t)} costs {Coin(price)}, you have {Coin(_money)}");
        }
        _lastRefusal = "";
        return RiverResult.Success(-1);
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
        var cr = new Crossing(_nextCrossingId++, t, row, cells, west, east, bridgeId, r, _p.MaxWalkers);
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
            if (x.State == BridgeState.Closed || x.State == BridgeState.Lowering) StartRaise(x);
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

    /// <summary>Hook of Erase for cell c: removes a ferry on it. Returns how many crossings were removed.</summary>
    private int RiverEraseAt(int c)
    {
        int id = _crossAt[c];
        if (id < 0) return 0;
        var x = CrossingById(id);
        if (x == null || x.Type != CrossingType.Ferry) return 0;
        RemoveCrossing(x);
        return 1;
    }

    /// <summary>Hook of Erase before a footbridge or drawbridge is removed.</summary>
    private void RiverBridgeErased(int bridgeId)
    {
        foreach (var x in _crossings)
            if (x.BridgeId == bridgeId && x.Type != CrossingType.Ford) { RemoveCrossing(x); return; }
    }

    private void RemoveCrossing(Crossing x)
    {
        ReleaseAll(x);
        foreach (int c in x.Cells) _crossAt[c] = -1;
        for (int i = 0; i < _boatCount; i++) if (_boats[i].Through == x.Id) _boats[i].Through = -1;
        _crossings.Remove(x);
        double cost = _river!.EraseCostShare * _river.Price(x.Type);
        Spend(cost);
        Emit(RiverEventKind.CrossingRemoved, -1, x.Id, cost);
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

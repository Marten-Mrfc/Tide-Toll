using System;

namespace TideToll.Sim;

// Boats: arrivals on a schedule, sailing north to the landing, waiting at crossings they cannot pass
// (patience runs, then they turn back), mooring and buying grain, leaving by the north edge.
public sealed partial class World
{
    // Why a boat cannot go further this tick.
    private const int NoBlock = -1, BerthFull = -2, BoatAhead = -3, MoorHere = -4;

    private void SpawnBoats()
    {
        var r = _river!;
        for (int k = 0; k < 2; k++)
        {
            if (_time < _nextArrival[k]) continue;
            var kind = (BoatKind)k;
            _nextArrival[k] += ArrivalGap(kind);
            if (_boatCount >= _boats.Length) continue; // the river is full: this boat never comes
            AddBoat(kind);
        }
    }

    private void AddBoat(BoatKind kind)
    {
        var r = _river!;
        double y = _n + 0.5;
        _boats[_boatCount++] = new Boat
        {
            Id = _nextBoatId, Kind = kind, State = BoatState.Sailing, Mast = (byte)r.Mast(kind), Y = y, PrevY = y, Dir = -1,
            Capacity = r.Capacity(kind), PatienceLeft = r.PatienceSec, WaitingAt = -1, Through = -1, Berth = -1,
        };
        _rs!.Arrived[(int)kind]++;
        Emit(RiverEventKind.BoatArrived, _nextBoatId, -1, (int)kind);
        _nextBoatId++;
    }

    /// <summary>Whether crossing x stops boat b now. Grants a waiting boat its turn under a raised drawbridge.</summary>
    private bool BoatBlocked(Crossing x, ref Boat b, double stopY)
    {
        var r = _river!;
        switch (x.Type)
        {
            case CrossingType.Footbridge:
                return b.Mast > r.FootbridgeClearance;
            case CrossingType.Drawbridge:
                if (b.Mast <= r.DrawbridgeClosedClearance || b.Through == x.Id) return false;
                if (x.State == BridgeState.Raised && _time >= x.NextPass - 1e-9 && b.Y <= stopY + 1e-6)
                {
                    b.Through = x.Id;
                    x.NextPass = _time + r.PerBoatSec;
                    x.PassedThisRaise++;
                    x.BoatsPassed++;
                    Emit(RiverEventKind.BoatPassed, b.Id, x.Id, 0);
                    return false;
                }
                return true;
            default:
                return false; // the ford and the ferry never block a boat
        }
    }

    private void MoveBoats(double dt)
    {
        var r = _river!;
        double step = r.SailRowsPerSec * dt, landY = _landingRow + 0.5;
        for (int i = 0; i < _boatCount; i++)
        {
            ref Boat b = ref _boats[i];
            b.PrevY = b.Y;
            if (b.State == BoatState.TurnedBack) { b.Y += step; continue; }
            if (b.State == BoatState.Mooring)
            {
                b.MoorLeft -= dt;
                if (b.MoorLeft <= 0) LeaveBerth(ref b);
                continue;
            }
            // How far north this boat may go: the landing, a crossing it cannot pass, or the boat ahead.
            double limit = -1e9;
            int blocker = NoBlock;
            if (!b.Served)
            {
                bool free = false;
                for (int j = 0; j < _berth.Length; j++) if (_berth[j] < 0) { free = true; break; }
                limit = free ? landY : landY + r.StopRows;
                blocker = free ? MoorHere : BerthFull;
            }
            for (int j = 0; j < _crossings.Count; j++)
            {
                var x = _crossings[j];
                double row = x.Row + 0.5, stop = row + r.StopRows;
                if (b.Through == x.Id && b.Y < row - 1) b.Through = -1;
                if (!(row < b.Y) || stop <= limit) continue;
                if (BoatBlocked(x, ref b, stop)) { limit = stop; blocker = x.Id; }
            }
            int ahead = -1;
            for (int j = 0; j < _boatCount; j++)
            {
                if (j == i) continue;
                ref Boat o = ref _boats[j];
                if (o.State == BoatState.TurnedBack || o.State == BoatState.Mooring || !(o.Y < b.Y)) continue;
                if (ahead < 0 || o.Y > _boats[ahead].Y) ahead = j;
            }
            if (ahead >= 0 && _boats[ahead].Y + r.StopRows > limit)
            {
                limit = _boats[ahead].Y + r.StopRows;
                blocker = BoatAhead;
            }
            double ny = Math.Min(b.Y, Math.Max(b.Y - step, limit));
            bool atLimit = ny <= limit + 1e-9;
            b.Y = ny;
            if (atLimit && blocker == MoorHere) { Moor(ref b, landY); continue; }
            if (atLimit && blocker != NoBlock)
            {
                int at = blocker == BoatAhead ? (_boats[ahead].State == BoatState.Waiting ? _boats[ahead].WaitingAt : BoatAhead) : blocker;
                if (b.State != BoatState.Waiting) Emit(RiverEventKind.BoatWaiting, b.Id, at, 0);
                b.State = BoatState.Waiting;
                b.WaitingAt = at;
                if (at >= 0) { var x = CrossingById(at); if (x != null) x.BoatsWaiting++; }
                // A served boat never turns back; it waits for free.
                if (!b.Served)
                {
                    b.PatienceLeft -= dt;
                    if (b.PatienceLeft <= 0) TurnBack(ref b);
                }
            }
            else
            {
                b.State = b.Served ? BoatState.Leaving : BoatState.Sailing;
                b.WaitingAt = -1;
            }
        }
        // Boats off the map are gone; keep the order of the rest.
        int keep = 0;
        for (int i = 0; i < _boatCount; i++)
        {
            ref Boat b = ref _boats[i];
            bool gone = b.State == BoatState.TurnedBack ? b.Y > _n + 2 : b.Y < -1.5;
            if (gone) { if (b.State != BoatState.TurnedBack) Emit(RiverEventKind.BoatLeft, b.Id, -1, b.Earned); continue; }
            if (keep != i) _boats[keep] = b;
            keep++;
        }
        _boatCount = keep;
    }

    private void Moor(ref Boat b, double landY)
    {
        int berth = -1;
        for (int j = 0; j < _berth.Length; j++) if (_berth[j] < 0) { berth = j; break; }
        if (berth < 0) { b.Y = landY + _river!.StopRows; return; } // taken this tick by a boat ahead
        _berth[berth] = b.Id;
        b.Berth = berth;
        b.Y = landY;
        b.State = BoatState.Mooring;
        b.MoorLeft = _river!.MooringSec;
        b.WaitingAt = -1;
        Emit(RiverEventKind.BoatMoored, b.Id, -1, berth);
    }

    private void LeaveBerth(ref Boat b)
    {
        var r = _river!;
        if (b.Berth >= 0) _berth[b.Berth] = -1;
        b.Berth = -1;
        b.Served = true;
        b.State = BoatState.Leaving;
        Earn(b.Earned, true);
        _rep = Math.Min(100, _rep + r.ReputationServed);
        _rs!.Served[(int)b.Kind]++;
        _rs.ServedToday++;
        Emit(RiverEventKind.BoatSold, b.Id, -1, b.Earned);
    }

    private void TurnBack(ref Boat b)
    {
        var r = _river!;
        b.State = BoatState.TurnedBack;
        b.Dir = 1;
        b.WaitingAt = -1;
        b.Through = -1;
        _rep = Math.Max(0, _rep - r.ReputationTurnedBack);
        _rs!.TurnedBack[(int)b.Kind]++;
        _rs.TurnedBackToday++;
        Emit(RiverEventKind.BoatTurnedBack, b.Id, -1, 0);
    }

    /// <summary>Hook at the end of a field visit: take one grain and carry it to the landing. False when there is nothing to haul.</summary>
    private bool HaulLeg(ref Walker w)
    {
        if (_landingB < 0 || w.Cargo != 0 || _map.Buildings[w.Target].Kind != BuildingKind.Field) return false;
        if (!(_fields[_landingB][w.Cell] < float.PositiveInfinity)) return false;
        w.Cargo = 1;
        Depart(ref w, _landingB, WalkState.Out);
        return true;
    }

    /// <summary>Hook of Arrive: trip time, and grain sold at the landing (dropped if the walker gave up and went home).</summary>
    private void RiverArrive(ref Walker w)
    {
        var rs = _rs!;
        rs.Trips++;
        rs.TripTimeSum += _time - w.TripT0;
        if (w.Cargo == 0) return;
        if (w.Target == _landingB && w.State == WalkState.Out) SellGrain();
        w.Cargo = 0;
    }

    /// <summary>A walker with grain at the landing: a moored boat with room buys it, otherwise the village market.</summary>
    private void SellGrain()
    {
        var r = _river!;
        for (int j = 0; j < _berth.Length; j++)
        {
            int id = _berth[j];
            if (id < 0) continue;
            for (int i = 0; i < _boatCount; i++)
            {
                ref Boat b = ref _boats[i];
                if (b.Id != id || b.Cargo >= b.Capacity) continue;
                b.Cargo++;
                b.Earned += r.GrainPrice * r.BoatValueMul;
                _rs!.GrainToBoats++;
                return;
            }
        }
        var rs = _rs!;
        if (rs.MarketToday >= r.MarketUnitsPerHouseDay * _byKind[(int)BuildingKind.House].Length) { rs.GrainUnsold++; return; }
        rs.MarketToday++;
        Earn(r.GrainPrice * r.MarketShare, false);
        rs.GrainToMarket++;
    }
}

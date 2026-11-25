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
        for (int k = 0; k < 2; k++)
        {
            if (_frozen) { _nextArrival[k] += _dt; continue; } // the schedule waits for the thaw
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
            Capacity = r.Capacity(kind), PatienceLeft = r.PatienceSec, WaitingAt = -1, Through = -1, Berth = -1, HandWaitAt = -1,
        };
        _rs!.ArrivedK[(int)kind]++;
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
                    if (x.HandRaiseLive && b.HandWaitAt == x.Id) CountHandRaise(x);
                    Emit(RiverEventKind.BoatPassed, b.Id, x.Id, 0);
                    return false;
                }
                return true;
            default:
                return false; // the ford and the ferry never block a boat
        }
    }

    /// <summary>Whether crossing id stops a mast of this class while it is not raised.</summary>
    private bool MastBlocked(int id, int mast)
    {
        var x = CrossingById(id);
        if (x == null) return false;
        var r = _river!;
        return x.Type switch
        {
            CrossingType.Footbridge => mast > r.FootbridgeClearance,
            CrossingType.Drawbridge => mast > r.DrawbridgeClosedClearance,
            _ => false,
        };
    }

    private void MoveBoats(double dt)
    {
        var r = _river!;
        double step = r.SailRowsPerSec * dt, landY = _landingRow + 0.5;
        if (_frozen)
        {
            // Held fast in the ice: nothing moves and no clock runs (patience, mooring).
            for (int i = 0; i < _boatCount; i++) _boats[i].PrevY = _boats[i].Y;
            return;
        }
        for (int i = 0; i < _boatCount; i++)
        {
            ref Boat b = ref _boats[i];
            b.PrevY = b.Y;
            if (b.State == BoatState.TurnedBack) { b.Y += step; continue; }
            if (b.State == BoatState.Mooring)
            {
                b.MoorLeft -= dt;
                if (b.MoorLeft <= 1e-9) LeaveBerth(ref b);
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
                // A boat held at a crossing this one can pass is overtaken, not queued behind.
                if (o.State == BoatState.Waiting && o.WaitingAt >= 0 && !MastBlocked(o.WaitingAt, b.Mast)) continue;
                if (ahead < 0 || o.Y > _boats[ahead].Y) ahead = j;
            }
            if (ahead >= 0 && _boats[ahead].Y + r.StopRows > limit + 1e-9)
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
                b.PatienceLeft -= dt;
                if (b.PatienceLeft <= 1e-9)
                {
                    if (!b.Served) TurnBack(ref b);
                    else
                    {
                        // A served boat held north of the landing (a footbridge there never opens) goes home the way
                        // it came, with no loss of reputation, rather than blocking the river for ever.
                        b.State = BoatState.TurnedBack;
                        b.Dir = 1;
                        b.WaitingAt = -1;
                    }
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
            if (gone) { if (b.State != BoatState.TurnedBack || b.Served) Emit(RiverEventKind.BoatLeft, b.Id, -1, b.Earned); continue; }
            if (keep != i) _boats[keep] = b;
            keep++;
        }
        _boatCount = keep;
    }

    private void Moor(ref Boat b, double landY)
    {
        int berth = -1;
        for (int j = 0; j < _berth.Length; j++) if (_berth[j] < 0) { berth = j; break; }
        if (berth < 0)
        {
            // Taken this tick by a boat ahead: hold short of the landing like a boat that found the berths full,
            // but never move back south of where the boat was before this tick's move.
            b.Y = Math.Min(b.PrevY, Math.Max(b.Y, landY + _river!.StopRows));
            if (b.State != BoatState.Waiting) Emit(RiverEventKind.BoatWaiting, b.Id, BerthFull, 0);
            b.State = BoatState.Waiting;
            b.WaitingAt = BerthFull;
            return;
        }
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
        b.PatienceLeft = r.PatienceSec; // only counts down if something north of the landing holds it
        Earn(b.Earned, true);
        _rep = Math.Min(100, _rep + r.ReputationServed);
        _rs!.ServedK[(int)b.Kind]++;
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
        _rs!.TurnedBackK[(int)b.Kind]++;
        _rs.TurnedBackToday++;
        Emit(RiverEventKind.BoatTurnedBack, b.Id, -1, 0);
    }

    /// <summary>Hook at the end of a field visit: take one grain and carry it to the landing. False when there is nothing to haul.</summary>
    private bool HaulLeg(ref Walker w)
    {
        if (_landingB < 0 || w.Cargo != 0 || _b[w.Target].Kind != BuildingKind.Field) return false;
        if (!(_fields[_landingB][w.Cell] < float.PositiveInfinity)) return false;
        w.Cargo = 1;
        w.CargoWet = false;
        Depart(ref w, _landingB, WalkState.Out);
        return true;
    }

    /// <summary>Hook of Arrive: trip time, and grain sold at the landing (dropped, and counted in GrainLost, if the walker gave up and went home).</summary>
    private void RiverArrive(ref Walker w)
    {
        var rs = _rs!;
        rs.Trips++;
        rs.TripTimeSum += _time - w.TripT0;
        if (w.Cargo == 0) return;
        if (w.Target == _landingB && w.State == WalkState.Out) SellGrain(w.CargoWet);
        else rs.GrainLost += w.Cargo; // cut off from the landing, it gave up and carried the grain home: lost, and counted
        w.Cargo = 0;
        w.CargoWet = false;
    }

    /// <summary>
    /// A walker with grain at the landing: a moored boat with room buys it, otherwise the village market. Wet grain (it
    /// came through the ford) sells for RiverRules.WetGrainValueShare of its value at either.
    /// </summary>
    private void SellGrain(bool wet)
    {
        var r = _river!;
        double share = wet ? r.WetGrainValueShare : 1;
        var st = _rs!;
        if (wet) st.GrainDeliveredWet++;
        for (int j = 0; j < _berth.Length && !_frozen; j++) // trade with boats pauses on the ice
        {
            int id = _berth[j];
            if (id < 0) continue;
            for (int i = 0; i < _boatCount; i++)
            {
                ref Boat b = ref _boats[i];
                if (b.Id != id || b.Cargo >= b.Capacity) continue;
                b.Cargo++;
                b.Earned += r.GrainPrice * r.BoatValueMul * share;
                if (wet) st.WetValueLost += r.GrainPrice * r.BoatValueMul * (1 - share);
                st.GrainToBoats++;
                return;
            }
        }
        var rs = _rs!;
        if (rs.MarketToday >= r.MarketUnitsPerHouseDay * _houseCount) { rs.GrainUnsold++; return; }
        rs.MarketToday++;
        Earn(r.GrainPrice * r.MarketShare * share, false);
        if (wet) rs.WetValueLost += r.GrainPrice * r.MarketShare * (1 - share);
        rs.GrainToMarket++;
    }
}

using System;

namespace TideToll.Sim;

// Crossings on the tick: the drawbridge state machine and its keeper, the ferry's cycle, walkers waiting
// at a gate (a drawbridge that is not down, or a ferry stage), and the routing cost of each crossing.
public sealed partial class World
{
    private int _curWalker; // index of the walker being stepped (for the ferry queues)

    // ---------------------------------------------------------------- routing cost

    /// <summary>Per-cell cost of a ferry's water cells: (expected load and headway wait + ride + learned wait), in cost units, spread over the cells.</summary>
    internal double FerryCellCost(Crossing x) => FerryCellCost(x.Cells.Count, x.AvgWait);

    internal double FerryCellCost(int cells, double avgWait)
    {
        var r = _river!;
        return _p.WalkSpeedCellsPerSec * (r.FerryExpectedWaitSec + r.FerryCrossSec + avgWait) / Math.Max(1, cells);
    }

    /// <summary>Extra cost per drawbridge cell for the learned wait.</summary>
    internal double DrawbridgeExtra(Crossing x) => _p.WalkSpeedCellsPerSec * x.AvgWait / Math.Max(1, x.Cells.Count);

    /// <summary>Hook of CellCost for cells with a crossing: NaN when the cell is not special.</summary>
    private double RiverCellCost(int c, CostOverride ov)
    {
        int id = _crossAt[c];
        if (id < 0 || ov == CostOverride.Block || ov == CostOverride.CloseBridge || ov == CostOverride.Bridge) return double.NaN;
        var x = CrossingById(id);
        if (x == null) return double.NaN;
        if (x.Type == CrossingType.Ferry) return FerryCellCost(x);
        if (x.Type == CrossingType.Drawbridge) return _p.BridgeCost + DrawbridgeExtra(x);
        return double.NaN;
    }

    /// <summary>Hook of IsBlocked: a raised drawbridge span is no ground to stand on.</summary>
    private bool RiverSpanBlocked(int c)
    {
        int id = _crossAt[c];
        if (id < 0 || _bridgeAt[c] < 0) return false;
        var x = CrossingById(id);
        return x != null && x.Type == CrossingType.Drawbridge && x.State == BridgeState.Raised;
    }

    // ---------------------------------------------------------------- walkers at a gate

    /// <summary>Hook of the walker's step, before it enters cell c: true when it must stop and wait on its bank.</summary>
    private bool RiverGate(ref Walker w, int c)
    {
        int id = _crossAt[c];
        if (id < 0 || _crossAt[w.Cell] == id) return false;
        var x = CrossingById(id);
        if (x == null || !x.Gated) return false;
        if (x.Type == CrossingType.Drawbridge && x.State == BridgeState.Closed) return false;
        w.Hold = WalkerHold.Waiting;
        w.HoldAt = (short)id;
        w.WaitT = 0;
        w.Ticket = ++_ticketSeq;
        if (x.Type == CrossingType.Ferry) (SideOf(w.Cell) < 0 ? x.West! : x.East!).Push(_curWalker, w.Ticket);
        return true;
    }

    private int SideOf(int c)
    {
        int s = _map.Bank[c];
        if (s != 0) return s;
        return c % _n + 0.5 < _map.RiverX[c / _n] ? -1 : 1;
    }

    /// <summary>A waiting walker's tick. True when it is released and walks on this tick.</summary>
    private bool RiverHold(ref Walker w, double dt)
    {
        var x = CrossingById(w.HoldAt);
        if (x == null) { Release(ref w); return true; }
        w.WaitT = (float)(w.WaitT + dt);
        w.Stuck = 0;
        if (SideOf(w.Cell) < 0) x.QueueWest++; else x.QueueEast++;
        if (x.Type == CrossingType.Drawbridge && x.State == BridgeState.Closed)
        {
            RecordWait(x, w.WaitT);
            Release(ref w);
            return true;
        }
        if (w.WaitT >= _river!.MaxWalkerWaitSec)
        {
            RecordWait(x, w.WaitT);
            Release(ref w);
            SendHome(ref w);
            return false;
        }
        // Once a second: does the route still use this crossing (its learned wait may have sent it elsewhere)?
        if ((int)w.WaitT != (int)(w.WaitT - dt) && !RouteUses(ref w, x.Id))
        {
            RecordWait(x, w.WaitT);
            Release(ref w);
            return true;
        }
        return false;
    }

    private bool RouteUses(ref Walker w, int id)
    {
        float[] f = _fields[w.Target];
        int u = w.Cell;
        for (int s = 0; s < 3; s++)
        {
            u = DescendStep(f, _cost, u, w.Taste, true, w.Id);
            if (u < 0) return false;
            if (_crossAt[u] == id) return true;
        }
        return false;
    }

    private static void Release(ref Walker w)
    {
        w.Hold = WalkerHold.None;
        w.WaitT = 0;
        w.HasAim = false;
    }

    private void RecordWait(Crossing x, double wait)
    {
        x.AvgWait += _river!.WaitAlpha * (wait - x.AvgWait);
        x.WalkersServed++;
        var rs = _rs!;
        rs.WalkerWaits++;
        rs.WalkerWaitSum += wait;
        if (wait > rs.WalkerWaitMax) rs.WalkerWaitMax = wait;
    }

    /// <summary>Sets walkers waiting at (or riding) a crossing that is going away free: riders land on the far stage.</summary>
    private void ReleaseAll(Crossing x)
    {
        for (int i = 0; i < _walkerCount; i++)
        {
            ref Walker w = ref _walkers[i];
            if (w.Hold == WalkerHold.None || w.HoldAt != x.Id) continue;
            if (w.Hold == WalkerHold.Riding) LandRider(ref w, x.FerryFrom == -1 ? x.EastEnd : x.WestEnd);
            else Release(ref w);
        }
        x.West?.Clear();
        x.East?.Clear();
        x._passengers = 0;
    }

    // ---------------------------------------------------------------- drawbridge

    private bool MastedBoatNear(Crossing x, double rows)
    {
        var r = _river!;
        double row = x.Row + 0.5;
        for (int i = 0; i < _boatCount; i++)
        {
            ref Boat b = ref _boats[i];
            if (b.State == BoatState.TurnedBack || b.State == BoatState.Mooring || b.Mast <= r.DrawbridgeClosedClearance) continue;
            double d = b.Y - row;
            if (d > 0 && d <= rows) return true;
        }
        return false;
    }

    private bool BoatPassing(Crossing x)
    {
        for (int i = 0; i < _boatCount; i++) if (_boats[i].Through == x.Id) return true;
        return false;
    }

    private void StartRaise(Crossing x)
    {
        double p = x.State == BridgeState.Lowering ? x.Progress : 0;
        x.State = BridgeState.Raising;
        x.StateT = p * _river!.RaiseSec;
        x.KeeperT = 0;
        Emit(RiverEventKind.BridgeRaising, -1, x.Id, 0);
    }

    private void StartLower(Crossing x)
    {
        double p = x.State == BridgeState.Raising ? x.Progress : 1;
        x.State = BridgeState.Lowering;
        x.StateT = (1 - p) * _river!.LowerSec;
        Emit(RiverEventKind.BridgeLowering, -1, x.Id, 0);
    }

    private void StepDrawbridge(Crossing x, double dt)
    {
        var r = _river!;
        switch (x.State)
        {
            case BridgeState.Closed:
                x.Progress = 0;
                if (x.Keeper && MastedBoatNear(x, r.KeeperSeeRows))
                {
                    x.KeeperT += dt;
                    if (x.KeeperT >= r.KeeperDelaySec - 1e-9) StartRaise(x);
                }
                else x.KeeperT = 0;
                break;
            case BridgeState.Raising:
                x.StateT += dt;
                x.Progress = Math.Min(1, x.StateT / r.RaiseSec);
                if (x.StateT >= r.RaiseSec - 1e-9)
                {
                    x.State = BridgeState.Raised;
                    x.StateT = 0;
                    x.Progress = 1;
                    x.NextPass = _time;
                    x.PassedThisRaise = 0;
                    // Walkers still on the span are moved off to the nearer end.
                    for (int i = 0; i < _walkerCount; i++)
                    {
                        ref Walker w = ref _walkers[i];
                        if (w.Visible && w.Hold == WalkerHold.None && _crossAt[w.Cell] == x.Id) Evict(ref w);
                    }
                    Emit(RiverEventKind.BridgeRaised, -1, x.Id, 0);
                }
                break;
            case BridgeState.Raised:
                x.StateT += dt;
                bool near = MastedBoatNear(x, r.HoldRowsBelow) || BoatPassing(x);
                if (!near && (x.PassedThisRaise > 0 || x.Keeper || x.StateT >= r.ManualMaxRaisedSec - 1e-9)) StartLower(x);
                break;
            case BridgeState.Lowering:
                x.StateT += dt;
                x.Progress = Math.Max(0, 1 - x.StateT / r.LowerSec);
                if (x.StateT >= r.LowerSec - 1e-9)
                {
                    x.State = BridgeState.Closed;
                    x.StateT = 0;
                    x.Progress = 0;
                    Emit(RiverEventKind.BridgeLowered, -1, x.Id, 0);
                }
                break;
        }
    }

    // ---------------------------------------------------------------- ferry

    private bool ValidRider(int idx, int ticket, int id, WalkerHold hold)
    {
        if (idx < 0 || idx >= _walkerCount) return false;
        ref Walker w = ref _walkers[idx];
        return w.Hold == hold && w.HoldAt == id && w.Ticket == ticket;
    }

    /// <summary>Drops stale entries; true if a walker is really waiting at the head.</summary>
    private bool QueueHasWalker(Crossing x, WalkerQueue q)
    {
        while (q.Peek(out int idx, out int ticket))
        {
            if (ValidRider(idx, ticket, x.Id, WalkerHold.Waiting)) return true;
            q.Pop();
        }
        return false;
    }

    private void StepFerry(Crossing x, double dt)
    {
        var r = _river!;
        int n = _n;
        if (x.Sailing)
        {
            x.StateT += dt;
            double t = Math.Min(1, x.StateT / r.FerryCrossSec);
            x.Progress = x.FerryFrom < 0 ? t : 1 - t;
            int from = x.FerryFrom < 0 ? x.WestEnd : x.EastEnd, to = x.FerryFrom < 0 ? x.EastEnd : x.WestEnd;
            double fx = from % n + 0.5, fy = from / n + 0.5, tx = to % n + 0.5, ty = to / n + 0.5;
            for (int k = 0; k < x._passengers; k++)
            {
                if (!ValidRider(x.PassengerIdx[k], x.PassengerTicket[k], x.Id, WalkerHold.Riding)) continue;
                ref Walker w = ref _walkers[x.PassengerIdx[k]];
                w.Px = w.X; w.Py = w.Y;
                w.X = fx + (tx - fx) * t; w.Y = fy + (ty - fy) * t;
            }
            if (t < 1) return;
            int landed = 0;
            for (int k = 0; k < x._passengers; k++)
            {
                if (!ValidRider(x.PassengerIdx[k], x.PassengerTicket[k], x.Id, WalkerHold.Riding)) continue;
                LandRider(ref _walkers[x.PassengerIdx[k]], to);
                landed++;
            }
            x._passengers = 0;
            x.Sailing = false;
            x.FerrySide = -x.FerryFrom;
            x.StateT = 0;
            x.LoadT = -1;
            Emit(RiverEventKind.FerryLanded, -1, x.Id, landed);
            return;
        }
        x.Progress = x.FerrySide < 0 ? 0 : 1;
        WalkerQueue here = x.FerrySide < 0 ? x.West! : x.East!, there = x.FerrySide < 0 ? x.East! : x.West!;
        while (x._passengers < r.FerryCapacity && QueueHasWalker(x, here))
        {
            here.Peek(out int idx, out int ticket);
            here.Pop();
            ref Walker w = ref _walkers[idx];
            RecordWait(x, w.WaitT);
            w.Hold = WalkerHold.Riding;
            w.WaitT = 0;
            x.PassengerIdx[x._passengers] = idx;
            x.PassengerTicket[x._passengers] = ticket;
            x._passengers++;
            if (x.LoadT < 0) x.LoadT = 0;
        }
        if (x.LoadT >= 0)
        {
            x.LoadT += dt;
            if (x.LoadT >= r.FerryLoadSec - 1e-9 || x._passengers >= r.FerryCapacity) FerryDepart(x);
        }
        else if (QueueHasWalker(x, there)) FerryDepart(x); // fetch the walkers on the other bank
    }

    private void FerryDepart(Crossing x)
    {
        x.Sailing = true;
        x.FerryFrom = x.FerrySide;
        x.FerrySide = 0;
        x.StateT = 0;
        x.LoadT = -1;
        Emit(RiverEventKind.FerryDeparted, -1, x.Id, x._passengers);
    }

    /// <summary>A rider steps off onto a stage cell and walks on.</summary>
    private void LandRider(ref Walker w, int cell)
    {
        _here[w.Cell]--;
        _here[cell]++;
        w.Cell = cell;
        w.X = cell % _n + 0.5; w.Y = cell / _n + 0.5;
        w.Hold = WalkerHold.None;
        w.WaitT = 0;
        w.HasAim = false;
        w.Stuck = 0;
        if (IsBlocked(cell)) Evict(ref w);
    }
}

using System;

namespace TideToll.Sim;

// Crossings on the tick: the drawbridge state machine and its keeper, the ferry's cycle, and the routing cost of
// each crossing. Walkers waiting at a gate are in RiverQueues.cs.
public sealed partial class World
{
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
        if (_flooded && _map.Ford[c] != 0 && ov != CostOverride.Bridge && !_frozen) return double.PositiveInfinity; // closed in the flood
        if (_frozen && _bridgeAt[c] < 0) return double.NaN; // ice, ferry water included
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
        x.KeeperT = -1;
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
                if (x.Keeper && !_frozen && MastedBoatNear(x, r.KeeperSeeRows))
                {
                    // The keeper sees the boat this tick and raises KeeperDelaySec later.
                    x.KeeperT = x.KeeperT < 0 ? 0 : x.KeeperT + dt;
                    if (x.KeeperT >= r.KeeperDelaySec - 1e-9) StartRaise(x);
                }
                else x.KeeperT = -1;
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
                // On the ice no boat moves, so a raised leaf comes down at once (walkers cross the span on the ice).
                if (_frozen || (!near && (x.PassedThisRaise > 0 || x.Keeper || x.StateT >= r.ManualMaxRaisedSec - 1e-9))) StartLower(x);
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

    private void StepFerry(Crossing x, double dt)
    {
        if (_frozen) return; // frozen in at its stage
        var r = _river!;
        int n = _n;
        if (x.Sailing)
        {
            x.StateT += dt;
            double t = Math.Min(1, x.StateT / r.FerryCrossSec + 1e-9);
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
            x.DockT = 0;
            Emit(RiverEventKind.FerryLanded, -1, x.Id, landed);
            return;
        }
        x.Progress = x.FerrySide < 0 ? 0 : 1;
        // Unloading and turning round at the stage: the doc's ~30 walkers each way a day for a full ferry.
        if (x.DockT < r.FerryTurnSec - 1e-9) { x.DockT += dt; return; }
        WalkerQueue here = x.FerrySide < 0 ? x.West! : x.East!, there = x.FerrySide < 0 ? x.East! : x.West!;
        while (x._passengers < r.FerryCapacity && QueueHasWalker(x, here))
        {
            here.Peek(out int idx, out int ticket);
            here.Pop();
            ref Walker w = ref _walkers[idx];
            RecordWait(x, w.WaitT);
            w.Hold = WalkerHold.Riding;
            w.WaitT = 0;
            w.Via = (short)x.Id;
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
        RiverEnter(ref w, cell);
        w.X = cell % _n + 0.5; w.Y = cell / _n + 0.5;
        w.Hold = WalkerHold.None;
        w.WaitT = 0;
        w.HasAim = false;
        w.Stuck = 0;
        if (IsBlocked(cell)) Evict(ref w);
    }
}

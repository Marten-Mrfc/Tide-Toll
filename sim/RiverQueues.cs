using System;

namespace TideToll.Sim;

// Walkers at a gate: every crossing but the ford has a first come, first served queue on each bank. A walker waits
// there for the ferry, for a drawbridge that is not down, or when a bridge is at its walker capacity (the doc's
// crossing table: footbridge about 120 a day, drawbridge about 250 a day while closed), kept as a token bucket that
// fills at that rate. Also the count of walkers crossing the river, by crossing.
public sealed partial class World
{
    private int _curWalker; // index of the walker being stepped (for the queues)

    /// <summary>Hook of the walker's step, before it enters cell c: true when it must stop and wait on its bank.</summary>
    private bool RiverGate(ref Walker w, int c)
    {
        int id = _crossAt[c];
        if (id < 0 || _crossAt[w.Cell] == id) return false;
        var x = CrossingById(id);
        if (x == null || x.Type == CrossingType.Ford || _frozen) return false; // on ice every crossing is open ground
        if (w.Pass && w.HoldAt == id) { w.Pass = false; return false; }     // its turn came in the queue
        // A walker only clipping the end of an open bridge on its way along the bank (its line cuts the corner of an end
        // cell) is not queued for it: it would be released at its first route check and queue again, for ever.
        if (x.Type != CrossingType.Ferry && BridgeOpen(x) && !RouteUses(ref w, id)) return false;
        WalkerQueue q = SideOf(w.Cell) < 0 ? x.West! : x.East!;
        // Straight on when nobody is queued on either bank and the bridge has room; otherwise to the back of the queue.
        if (x.Type != CrossingType.Ferry && BridgeOpen(x) && x.Tokens >= 1 && !QueueHasWalker(x, x.West!) && !QueueHasWalker(x, x.East!))
        {
            x.Tokens -= 1;
            return false;
        }
        w.Hold = WalkerHold.Waiting;
        w.HoldAt = (short)id;
        w.WaitT = 0;
        w.Waited = true;
        w.Ticket = ++_ticketSeq;
        q.Push(_curWalker, w.Ticket);
        return true;
    }

    /// <summary>Walkers may step onto a bridge's span: a footbridge always, a drawbridge only while it is down.</summary>
    private static bool BridgeOpen(Crossing x) => x.Type == CrossingType.Footbridge || (x.Type == CrossingType.Drawbridge && x.State == BridgeState.Closed);

    /// <summary>Walkers a second a bridge lets on (its capacity per day spread over the day), and the bucket's size.</summary>
    private double BridgeRate(Crossing x)
        => (x.Type == CrossingType.Drawbridge ? _river!.DrawbridgeWalkersPerDay : _river!.FootbridgeWalkersPerDay) / _p.DaySimSec;

    /// <summary>Tick step of a footbridge or drawbridge gate: fill the bucket, then let queued walkers on, oldest ticket first.</summary>
    private void StepBridgeGate(Crossing x, double dt)
    {
        double rate = BridgeRate(x);
        x.Tokens = Math.Min(Math.Max(1, rate * _river!.BridgeBurstSec), x.Tokens + rate * dt);
        if (!BridgeOpen(x) || _frozen) return;
        while (x.Tokens >= 1)
        {
            bool west = QueueHasWalker(x, x.West!), east = QueueHasWalker(x, x.East!);
            if (!west && !east) break;
            int iw = 0, tw = int.MaxValue, ie = 0, te = int.MaxValue;
            if (west) x.West!.Peek(out iw, out tw);
            if (east) x.East!.Peek(out ie, out te);
            bool fromWest = west && (!east || tw < te);
            (fromWest ? x.West! : x.East!).Pop();
            ref Walker w = ref _walkers[fromWest ? iw : ie];
            RecordWait(x, w.WaitT);
            Release(ref w);
            w.Pass = true;
            x.Tokens -= 1;
        }
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
        if (x == null || _frozen) { Release(ref w); return true; }
        w.WaitT = (float)(w.WaitT + dt);
        w.Stuck = 0;
        if (SideOf(w.Cell) < 0) x.QueueWest++; else x.QueueEast++;
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
        w.Pass = false;
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

    // ---------------------------------------------------------------- counting crossings

    /// <summary>Hook of the walker's step after it entered cell c: notes the crossing it is on and counts a crossing on reaching the other bank.</summary>
    private void RiverEnter(ref Walker w, int c)
    {
        int id = _crossAt[c];
        if (id >= 0) w.Via = (short)id;
        else if (_map.Water[c] != 0) w.Via = (short)(_frozen ? IceVia : _map.Ford[c] != 0 ? 0 : -1);
        int bank = _map.Bank[c];
        if (bank == 0) return;
        if (w.Side != 0 && bank != w.Side) CountCrossing(ref w);
        w.Side = (sbyte)bank;
    }

    /// <summary>Crossing id credited for a walk across the ice.</summary>
    internal const int IceVia = -2;

    private void CountCrossing(ref Walker w)
    {
        var rs = _rs!;
        rs.Crossings++;
        if (w.Waited) rs.CrossingsWaited++;
        if (w.Via == IceVia) rs.IceCrossings++;
        else
        {
            var x = CrossingById(w.Via);
            if (x != null)
            {
                x.WalkersCrossed++;
                if (w.Waited) x.WalkersWaited++;
            }
        }
        w.Waited = false;
    }

    /// <summary>The walker was moved without walking (sent home, set down off a span): note its bank without counting a crossing.</summary>
    private void RiverPlaced(ref Walker w)
    {
        int bank = _map.Bank[w.Cell];
        if (bank != 0) w.Side = (sbyte)bank;
    }
}

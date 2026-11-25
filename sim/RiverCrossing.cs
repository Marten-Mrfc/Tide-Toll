using System;
using System.Collections.Generic;

namespace TideToll.Sim;

/// <summary>
/// One river crossing as the game sees it (read-only outside the simulation): the ford, a footbridge, a
/// ferry or a drawbridge. Cells are its water cells along one row, west to east; the ferry's stages and a
/// bridge's ends are the land cells just off them.
/// </summary>
public sealed class Crossing
{
    public int Id { get; }
    public CrossingType Type { get; }
    /// <summary>River row (the ford's centre row).</summary>
    public int Row { get; }
    public IReadOnlyList<int> Cells => _cells;
    /// <summary>Land cell at the west and east end (ferry stages, bridge ends); -1 for the ford.</summary>
    public int WestEnd { get; }
    public int EastEnd { get; }
    /// <summary>Footbridge or drawbridge: its id in World.Bridges; -1 otherwise.</summary>
    public int BridgeId { get; }

    /// <summary>Highest mast class that passes right now (255 = every boat).</summary>
    public int Clearance
    {
        get
        {
            switch (Type)
            {
                case CrossingType.Footbridge: return _rules.FootbridgeClearance;
                case CrossingType.Drawbridge: return State == BridgeState.Raised ? 255 : _rules.DrawbridgeClosedClearance;
                default: return 255;
            }
        }
    }

    /// <summary>Drawbridge leaf state (Closed for every other type).</summary>
    public BridgeState State { get; internal set; }
    /// <summary>Seconds in the current drawbridge state, or in the ferry's current leg.</summary>
    public double StateT { get; internal set; }
    /// <summary>Drawbridge leaf from 0 (down) to 1 (up); ferry position from 0 (west stage) to 1 (east stage).</summary>
    public double Progress { get; internal set; }
    public bool Keeper { get; internal set; }
    /// <summary>
    /// Drawbridge: the town's count of hand raises that let a waiting masted boat through (a boat was waiting below the
    /// bridge when the raise began and passed during it). The count belongs to the town, not to this bridge: every
    /// drawbridge, a rebuilt one included, reports the same number. A raise with nothing waiting, or the keeper's, does
    /// not count. 0 for other types.
    /// </summary>
    public int HandRaises => Type == CrossingType.Drawbridge ? _tally.HandRaises : 0;
    /// <summary>Drawbridge: counted hand raises still to go before a keeper can be hired (RiverRules.KeeperUnlockRaises); 0 when unlocked, and for other types.</summary>
    public int RaisesToKeeper => Type == CrossingType.Drawbridge ? Math.Max(0, _rules.KeeperUnlockRaises - HandRaises) : 0;
    /// <summary>Drawbridge: a keeper can be hired for it (it has been raised by hand often enough).</summary>
    public bool KeeperUnlocked => Type == CrossingType.Drawbridge && RaisesToKeeper == 0;
    /// <summary>Walkers waiting on each bank (as of the last tick).</summary>
    public int QueueWest { get; internal set; }
    public int QueueEast { get; internal set; }
    /// <summary>Boats held below this crossing (as of the last tick).</summary>
    public int BoatsWaiting { get; internal set; }
    /// <summary>Learned average walker wait in seconds (moving average over served walkers); routing adds it to the cost.</summary>
    public double AvgWait { get; internal set; }
    /// <summary>Ferry: -1 docked at the west stage, +1 east, 0 crossing.</summary>
    public int FerrySide { get; internal set; }
    public int Passengers => _passengers;
    /// <summary>Walkers served after a wait (boarded, let on, or gave up).</summary>
    public int WalkersServed { get; internal set; }
    /// <summary>Walkers who reached the other bank by this crossing, and how many of them waited at a gate first.</summary>
    public int WalkersCrossed { get; internal set; }
    public int WalkersWaited { get; internal set; }
    /// <summary>Footbridge and drawbridge: walkers that may step on now (a bucket filled at the bridge's capacity per day).</summary>
    public double Tokens { get; internal set; }
    public int BoatsPassed { get; internal set; }

    private readonly int[] _cells;
    private readonly RiverRules _rules;
    private readonly KeeperTally _tally;

    // Ferry and queue state (internal to the simulation).
    internal readonly int[] PassengerIdx, PassengerTicket;
    internal int _passengers;
    internal int FerryFrom;          // side the current leg started from
    internal double LoadT = -1;      // seconds since the first passenger boarded, -1 none
    internal double DockT;           // seconds since the ferry last docked (it turns round before boarding)
    internal bool Sailing;
    internal readonly WalkerQueue? West, East;
    // Drawbridge.
    internal double NextPass;
    internal int PassedThisRaise;
    internal double KeeperT = -1;
    internal bool HandRaiseLive;     // the current raise is a hand raise that began with a masted boat waiting below and has not yet counted

    internal Crossing(int id, CrossingType type, int row, int[] cells, int westEnd, int eastEnd, int bridgeId, RiverRules rules, int maxWalkers, KeeperTally tally)
    {
        Id = id; Type = type; Row = row; _cells = cells; WestEnd = westEnd; EastEnd = eastEnd; BridgeId = bridgeId; _rules = rules; _tally = tally;
        if (type != CrossingType.Ford)
        {
            West = new WalkerQueue(maxWalkers * 2 + 8);
            East = new WalkerQueue(maxWalkers * 2 + 8);
        }
        if (type == CrossingType.Ferry)
        {
            PassengerIdx = new int[rules.FerryCapacity];
            PassengerTicket = new int[rules.FerryCapacity];
            FerrySide = -1;
        }
        else { PassengerIdx = PassengerTicket = Array.Empty<int>(); }
        Tokens = 1;
    }
}

/// <summary>The town's hand raises that let a waiting boat through (RiverRules.KeeperUnlockRaises): shared by all its drawbridges.</summary>
internal sealed class KeeperTally
{
    public int HandRaises;
}

/// <summary>First come, first served queue of (walker index, ticket); stale entries are skipped when they reach the head.</summary>
internal sealed class WalkerQueue
{
    private readonly int[] _idx, _ticket;
    private int _head, _count;

    public WalkerQueue(int capacity) { _idx = new int[capacity]; _ticket = new int[capacity]; }

    public int Count => _count;

    public void Push(int idx, int ticket)
    {
        if (_count == _idx.Length) { _head = (_head + 1) % _idx.Length; _count--; } // drop the oldest (cannot happen: capacity is twice the walkers)
        int k = (_head + _count) % _idx.Length;
        _idx[k] = idx; _ticket[k] = ticket;
        _count++;
    }

    public bool Peek(out int idx, out int ticket)
    {
        idx = ticket = 0;
        if (_count == 0) return false;
        idx = _idx[_head]; ticket = _ticket[_head];
        return true;
    }

    public void Pop() { if (_count > 0) { _head = (_head + 1) % _idx.Length; _count--; } }
    public void Clear() { _head = 0; _count = 0; }
}

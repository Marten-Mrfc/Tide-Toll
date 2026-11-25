using System.Collections.Generic;

namespace TideToll.Sim;

/// <summary>What a walker is doing: at home, walking out, at a destination, or walking back.</summary>
public enum WalkState : byte { Home = 0, Out = 1, Away = 2, Back = 3 }

/// <summary>
/// One walker (the toy's Walker). Stored by value in a pre-sized array, because one step touches
/// nearly every field, and changing the walker count never allocates. Positions are in cell units
/// (the centre of cell x is x + 0.5); X, Y are this tick's and Px, Py the previous tick's, for
/// interpolated rendering.
/// </summary>
public struct Walker
{
    public int Id;
    /// <summary>Building index of the walker's house.</summary>
    public int Home;
    public WalkState State;
    public int Cell;
    // Point the walker heads for in a straight line; recomputed on entering each cell.
    public double AimX, AimY;
    public bool HasAim;
    /// <summary>Dwell countdown in sim seconds.</summary>
    public double Timer;
    /// <summary>Building index walked to.</summary>
    public int Target;
    public int TripStart;
    public double TripLen;
    public double Stuck;
    /// <summary>-TasteNoise..+TasteNoise: how much this walker favours worn ground.</summary>
    public double Taste;
    /// <summary>Weighs distance more than company, on every trip (Hash2(Id, 77) &lt; SimParams.ExplorerShare; no random draw).</summary>
    public bool Explorer;
    /// <summary>This trip is walked on the frugal view (an explorer, or a trip over the detour cap).</summary>
    public bool Frugal;
    public double X, Y, Px, Py;
    // River rules only (always zero without them).
    /// <summary>Waiting at a crossing or riding the ferry (see <see cref="WalkerHold"/>).</summary>
    public WalkerHold Hold;
    /// <summary>Crossing id while <see cref="Hold"/> is not None.</summary>
    public short HoldAt;
    /// <summary>Units of grain carried to the landing.</summary>
    public byte Cargo;
    /// <summary>The grain carried went through the ford and is wet: it sells for RiverRules.WetGrainValueShare of its value.</summary>
    public bool CargoWet;
    /// <summary>Seconds waited at the current crossing.</summary>
    public float WaitT;
    internal int Ticket;
    internal double TripT0;
    /// <summary>Bank the walker last stood on (-1 west, +1 east, 0 not yet known), for counting crossings.</summary>
    internal sbyte Side;
    /// <summary>Crossing id last stood on (ford 0; -2 the ice; -1 none), credited when the walker reaches the other bank.</summary>
    internal short Via;
    /// <summary>Waited at a gate since its last crossing.</summary>
    internal bool Waited;
    /// <summary>Holds a pass through the gate of crossing Via (granted from the queue); spent on stepping onto it.</summary>
    internal bool Pass;

    /// <summary>Drawn on the map: walking out or back (not inside a building).</summary>
    public readonly bool Visible => State == WalkState.Out || State == WalkState.Back;
}

/// <summary>A footbridge: its id, river row and water cells.</summary>
public sealed class Bridge
{
    public int Id { get; }
    public int Row { get; }
    public IReadOnlyList<int> Cells => _cells;
    private readonly int[] _cells;

    internal Bridge(int id, int row, int[] cells) { Id = id; Row = row; _cells = cells; }
}

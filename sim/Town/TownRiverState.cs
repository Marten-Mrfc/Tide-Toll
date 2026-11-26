using System;

namespace TideToll.Sim.Town;

public enum BoatKind : byte { Rowboat = 0, Barge = 1 }

/// <summary>What a boat is doing. Arriving: coming up from the south edge; Waiting: on the river for a free berth;
/// Moored: at a berth, taking cargo; Leaving: going back down to the south edge; Gone: slot free.</summary>
public enum BoatPhase : byte { Gone = 0, Arriving = 1, Waiting = 2, Moored = 3, Leaving = 4 }

/// <summary>One boat (saved as is). Boats live in a fixed array of TownRulesRiver.MaxBoats slots.</summary>
public sealed class BoatState
{
    public int Id = -1;              // boat number since the age began (-1: slot never used)
    public BoatKind Kind;
    public BoatPhase Phase;
    public Good Good;                // cargo wanted
    public int Want, Loaded;
    public double Patience;          // seconds left
    public int Berth = -1, WaitSlot = -1;
    public bool Request;             // a skipper's request (named, bonus doubled)
    public int Skipper;              // index into TownRulesRiver.SkipperNames
    public bool TollPaid, Filled;
    public double X, Y, PrevX, PrevY, Heading;
}

/// <summary>The second age's whole mutable state; null in TownState.River until the age is rung in.</summary>
public sealed class RiverState
{
    public double StartTime;
    public double RenownBase;            // renown held when the age was rung in
    public double RenownScale = 1;       // renown per level-minute (TownRulesRiver.LevelRenown): the best coin rate at the ring
    public int Reached;                  // river levels reached (0..12)
    public bool Seal;
    public bool[] PlotGiven = Array.Empty<bool>();
    public bool[] LinkOpen = Array.Empty<bool>();
    public bool FerryOffered, FerryBought, BerthOffered, BerthBought, CraneOffered;
    public int Crane, Prices, WoolValue, ClothValue, Raft, Rope, Toll, Winch;
    public int Cottages;                 // river cottages given (each houses TownRules.PerCottage)

    public BoatState[] Boats = Array.Empty<BoatState>();
    public double NextBoatAt;
    public int BoatsCome, BoatsFilled, BoatsShort, BoatRequestsMet;

    // Ferry: raft position 0 (near bank) .. 1 (far bank); Dir +1 to far, -1 to near, 0 docked.
    public double RaftPos, RaftDock;
    public int RaftDir;
    public int[] Aboard = new int[16];
    public int AboardN;
    public int RaftTrips;

    // Drawbridge: Lift 0 (down) .. 1 (up for a boat).
    public double Lift;
    public int OnDraw;                   // walkers on the drawbridge now
    public int OnFord, OnFoot;           // walkers on the ford and the footbridge now
    public int Lifts;

    public int PastureBusy, WeaverBusy;

    // Counts by RiverLinkKind (Lane unused): crossings made and seconds the crossing existed.
    public long[] Crossed = new long[5];
    public double[] OpenSeconds = new double[5];
    public double EarnedLanding, EarnedToll, EarnedMarket;
    public double[] EarnedBy = new double[6];   // by Good, second age only
    public long[] Delivered = new long[6];      // by Good, second age only
    public long Tolls;
    public bool[] Achieved = new bool[16];
}
